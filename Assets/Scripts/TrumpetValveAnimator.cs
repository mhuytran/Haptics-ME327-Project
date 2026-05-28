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
    public float motionSpeed = 8.0f;
    public float releaseMotionSpeed = 14.0f;

    [Header("analog ToF control")]
    public bool useAnalogValveAmount = false;

    [Header("debug animation source")]
    [Tooltip("Off for hardware runs so haptic test routines cannot make valves appear pressed on their own.")]
    public bool allowDebugValveAnimation = false;

    [Header("visual anti-jitter")]
    [Tooltip("How long a new pressed visual state must stay stable before the model moves.")]
    public float visualPressDebounceSeconds = 0.12f;
    [Tooltip("How long a new released visual state must stay stable before the model moves.")]
    public float visualReleaseDebounceSeconds = 0.10f;
    [Range(0f, 0.5f)]
    [Tooltip("Tiny analog ToF amounts below this value are treated as released for visuals.")]
    public float visualAmountDeadZone = 0.05f;

    [Header("solenoid release follow")]
    public bool followSolenoidRelease = true;
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

    private Vector3 restLocalPosition;
    private float displayedPressAmount = 0f;
    private float filteredPressAmount = 0f;
    private float pendingPressAmount = 0f;
    private float pendingPressAmountSince = 0f;

    void Start()
    {
        RefreshPinoutReadout();
        restLocalPosition = transform.localPosition;
        pendingPressAmountSince = Time.unscaledTime;
    }

    void Update()
    {
        float sensedPressAmount;

        if (useAnalogValveAmount)
        {
            // Uses continuous ToF distance from Teensy.
            // 0 = valve up, 1 = valve fully pressed.
            sensedPressAmount = ValveInputState.GetValveAmount(laneIndex, allowDebugValveAnimation);
        }
        else
        {
            // Uses binary keyboard/Teensy press state.
            sensedPressAmount = ValveInputState.GetValve(laneIndex, allowDebugValveAnimation) ? 1f : 0f;
        }

        sensedPressAmount = GetVisualDebouncedPressAmount(sensedPressAmount);

        latestDistanceMM = ValveInputState.GetValveDistanceMM(laneIndex);
        latestSolenoidDuty = ValveInputState.GetSolenoidDuty(laneIndex);
        targetPressAmount = GetStableTargetPressAmount(sensedPressAmount, latestSolenoidDuty);

        float amountSpeed = targetPressAmount < displayedPressAmount
            ? Mathf.Max(motionSpeed, releaseMotionSpeed)
            : motionSpeed;

        displayedPressAmount = Mathf.MoveTowards(
            displayedPressAmount,
            targetPressAmount,
            Mathf.Max(0.01f, amountSpeed) * Time.deltaTime
        );

        latestPressAmount = displayedPressAmount;
        RefreshPinoutReadout();

        Vector3 targetPosition = restLocalPosition + pressedLocalOffset * displayedPressAmount;
        transform.localPosition = targetPosition;
    }

    float GetVisualDebouncedPressAmount(float sensedPressAmount)
    {
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
        solenoidDutyForFullRelease = Mathf.Clamp(solenoidDutyForFullRelease, 0.01f, 1f);
        solenoidDutyDeadZone = Mathf.Clamp(solenoidDutyDeadZone, 0f, 0.1f);
        RefreshPinoutReadout();
    }
}
