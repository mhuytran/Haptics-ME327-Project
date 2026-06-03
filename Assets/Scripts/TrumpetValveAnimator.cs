using UnityEngine;

public class TrumpetValveAnimator : MonoBehaviour
{
    [Header("Teensy pinout from final firmware")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("input lane")]
    [Tooltip("0 = valve 1, 1 = valve 2, 2 = valve 3. This picks the matching Teensy ToF channel.")]
    public int laneIndex = 0;

    [Header("valve motion")]
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.18f, 0f);
    public float motionSpeed = 18.0f;
    public float releaseMotionSpeed = 28.0f;

    [Header("analog ToF control")]
    public bool useAnalogValveAmount = false;

    [Header("debug animation source")]
    [Tooltip("Off for hardware runs so haptic test routines cannot make valves appear pressed on their own.")]
    public bool allowDebugValveAnimation = false;

    [Header("input gate")]
    [Tooltip("Keeps valve visuals at rest unless keyboard/debug input is explicit or Teensy travel is confirmed beyond the idle noise band.")]
    public bool requireConfirmedInputForAnimation = true;
    [Tooltip("When on, Teensy-driven valve motion must pass the stricter visual confirmation gate in TeensySerialInput.")]
    public bool requireConfirmedTeensyTravel = true;

    [Header("render safety")]
    [Tooltip("Repairs hidden or missing valve renderers at startup. Useful when prefab overrides drop a valve mesh.")]
    public bool repairValveRendererOnStart = true;

    [Header("visual anti-jitter")]
    [Tooltip("How long a new pressed visual state must stay stable before the model moves.")]
    public float visualPressDebounceSeconds = 0.04f;
    [Tooltip("How long a new released visual state must stay stable before the model moves.")]
    public float visualReleaseDebounceSeconds = 0.03f;
    [Range(0f, 0.5f)]
    [Tooltip("Tiny analog ToF amounts below this value are treated as released for visuals.")]
    public float visualAmountDeadZone = 0.05f;
    [Range(0f, 0.2f)]
    [Tooltip("Small target changes below this amount are ignored so ToF noise cannot wobble the rendered valve.")]
    public float visualTargetSnapEpsilon = 0.025f;

    [Header("solenoid release follow")]
    [Tooltip("Usually off for hardware runs. The physical valve motion should come from ToF input; solenoid telemetry can otherwise tug the rendered valve around.")]
    public bool followSolenoidRelease = false;
    [Range(0.01f, 1f)]
    public float solenoidDutyForFullRelease = 0.35f;
    [Range(0f, 0.1f)]
    public float solenoidDutyDeadZone = 0.02f;

    [Header("debug readout")]
    public string pinoutReadout = "";
    public int latestDistanceMM = 255;
    [Range(0f, 1f)]
    public float latestPressAmount = 0f;
    [Range(0f, 1f)]
    public float targetPressAmount = 0f;
    [Range(0f, 1f)]
    public float latestSolenoidDuty = 0f;
    public string rendererHealthReadout = "";

    private Vector3 restLocalPosition;
    private float displayedPressAmount = 0f;
    private float filteredPressAmount = 0f;
    private float stableTargetPressAmount = 0f;
    private float pendingPressAmount = 0f;
    private float pendingPressAmountSince = 0f;

    void Start()
    {
        // Cache the valve's rest pose and repair hidden/missing renderers before animation starts.
        RefreshPinoutReadout();

        if (repairValveRendererOnStart)
        {
            ValveRenderRepair.EnsureVisible(
                transform,
                "Valve " + (laneIndex + 1),
                out rendererHealthReadout
            );
        }

        restLocalPosition = transform.localPosition;
        pendingPressAmountSince = Time.unscaledTime;
    }

    void LateUpdate()
    {
        // Read the current source-aware press amount and smooth the valve toward that target.
        float sensedPressAmount = GetSourceAwareVisualPressAmount();
        sensedPressAmount = GetVisualDebouncedPressAmount(sensedPressAmount);

        latestDistanceMM = ValveInputState.GetValveDistanceMM(laneIndex);
        latestSolenoidDuty = ValveInputState.GetSolenoidDuty(laneIndex);
        targetPressAmount = GetJitterHeldTargetPressAmount(
            GetStableTargetPressAmount(sensedPressAmount, latestSolenoidDuty)
        );

        float amountSpeed = targetPressAmount < displayedPressAmount
            ? Mathf.Max(motionSpeed, releaseMotionSpeed)
            : motionSpeed;

        displayedPressAmount = Mathf.MoveTowards(
            displayedPressAmount,
            targetPressAmount,
            Mathf.Max(0.01f, amountSpeed) * Time.deltaTime
        );

        if (Mathf.Abs(displayedPressAmount - targetPressAmount) <= 0.001f)
        {
            displayedPressAmount = targetPressAmount;
        }

        latestPressAmount = displayedPressAmount;
        RefreshPinoutReadout();

        Vector3 targetPosition = restLocalPosition + pressedLocalOffset * displayedPressAmount;
        transform.localPosition = targetPosition;
    }

    float GetSourceAwareVisualPressAmount()
    {
        // Keyboard/debug input can override; Teensy input must pass the visual confirmation gate.
        if (ValveInputState.GetKeyboardValveOnly(laneIndex))
        {
            return 1f;
        }

        if (allowDebugValveAnimation)
        {
            float debugAmount = ValveInputState.GetDebugValveAmountOnly(laneIndex);

            if (ValveInputState.GetDebugValveOnly(laneIndex))
            {
                return debugAmount > 0f ? debugAmount : 1f;
            }

            if (debugAmount > visualAmountDeadZone)
            {
                return debugAmount;
            }
        }

        bool teensyPressed = ValveInputState.GetTeensyValveOnly(laneIndex);
        float teensyAmount = ValveInputState.GetTeensyValveAmountOnly(laneIndex);

        if (!teensyPressed && (!useAnalogValveAmount || teensyAmount <= visualAmountDeadZone))
        {
            return 0f;
        }

        if (requireConfirmedInputForAnimation && requireConfirmedTeensyTravel)
        {
            TeensySerialInput input = TeensySerialInput.Instance;
            int distanceMM = ValveInputState.GetValveDistanceMM(laneIndex);

            if (input == null || !input.IsAnimationPressConfirmed(laneIndex, teensyAmount, distanceMM))
            {
                return 0f;
            }
        }

        return useAnalogValveAmount
            ? teensyAmount
            : (teensyPressed ? 1f : 0f);
    }

    float GetVisualDebouncedPressAmount(float sensedPressAmount)
    {
        // Require stable visual state changes so ToF noise does not twitch the model.
        float normalizedAmount = NormalizeVisualPressAmount(sensedPressAmount);
        float now = Time.unscaledTime;

        if (Mathf.Approximately(normalizedAmount, filteredPressAmount))
        {
            pendingPressAmount = normalizedAmount;
            pendingPressAmountSince = now;
            return filteredPressAmount;
        }

        if (!Mathf.Approximately(normalizedAmount, pendingPressAmount))
        {
            pendingPressAmount = normalizedAmount;
            pendingPressAmountSince = now;
            return filteredPressAmount;
        }

        float requiredStableSeconds = normalizedAmount > filteredPressAmount
            ? visualPressDebounceSeconds
            : visualReleaseDebounceSeconds;

        if (now - pendingPressAmountSince >= Mathf.Max(0f, requiredStableSeconds))
        {
            filteredPressAmount = normalizedAmount;
            pendingPressAmountSince = now;
        }

        return filteredPressAmount;
    }

    float NormalizeVisualPressAmount(float sensedPressAmount)
    {
        // Binary mode snaps to 0/1; analog mode remaps after a dead zone.
        sensedPressAmount = Mathf.Clamp01(sensedPressAmount);

        if (!useAnalogValveAmount)
        {
            return sensedPressAmount >= 0.5f ? 1f : 0f;
        }

        float deadZone = Mathf.Clamp01(visualAmountDeadZone);

        if (sensedPressAmount <= deadZone)
        {
            return 0f;
        }

        return Mathf.InverseLerp(deadZone, 1f, sensedPressAmount);
    }

    float GetStableTargetPressAmount(float sensedPressAmount, float solenoidDuty)
    {
        // Optional solenoid telemetry can visually pull the valve toward release.
        sensedPressAmount = Mathf.Clamp01(sensedPressAmount);

        if (!followSolenoidRelease)
        {
            return sensedPressAmount;
        }

        float releaseAmount = Mathf.InverseLerp(
            solenoidDutyDeadZone,
            Mathf.Max(solenoidDutyDeadZone + 0.01f, solenoidDutyForFullRelease),
            solenoidDuty
        );

        return Mathf.Clamp01(sensedPressAmount * (1f - releaseAmount));
    }

    float GetJitterHeldTargetPressAmount(float nextTargetPressAmount)
    {
        // Ignore tiny target changes so the rendered valve does not wobble.
        nextTargetPressAmount = Mathf.Clamp01(nextTargetPressAmount);

        if (Mathf.Abs(nextTargetPressAmount - stableTargetPressAmount) <= visualTargetSnapEpsilon)
        {
            return stableTargetPressAmount;
        }

        stableTargetPressAmount = nextTargetPressAmount;
        return stableTargetPressAmount;
    }

    void RefreshPinoutReadout()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        pinoutReadout = TeensyHardwarePinout.GetDebugLabel(laneIndex, hardwarePinout);
    }

    void OnValidate()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        laneIndex = Mathf.Clamp(laneIndex, 0, TeensyHardwarePinout.ChannelCount - 1);
        motionSpeed = Mathf.Max(0.01f, motionSpeed);
        releaseMotionSpeed = Mathf.Max(0.01f, releaseMotionSpeed);
        visualPressDebounceSeconds = Mathf.Clamp(visualPressDebounceSeconds, 0f, 0.5f);
        visualReleaseDebounceSeconds = Mathf.Clamp(visualReleaseDebounceSeconds, 0f, 0.5f);
        visualAmountDeadZone = Mathf.Clamp01(visualAmountDeadZone);
        visualTargetSnapEpsilon = Mathf.Clamp(visualTargetSnapEpsilon, 0f, 0.2f);
        solenoidDutyForFullRelease = Mathf.Clamp(solenoidDutyForFullRelease, 0.01f, 1f);
        solenoidDutyDeadZone = Mathf.Clamp(solenoidDutyDeadZone, 0f, 0.1f);
        RefreshPinoutReadout();
    }
}
