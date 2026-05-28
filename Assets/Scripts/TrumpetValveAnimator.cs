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

    void Start()
    {
        RefreshPinoutReadout();
        restLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float sensedPressAmount;

        if (useAnalogValveAmount)
        {
            // Uses continuous ToF distance from Teensy.
            // 0 = valve up, 1 = valve fully pressed.
            sensedPressAmount = ValveInputState.GetValveAmount(laneIndex);
        }
        else
        {
            // Uses binary keyboard/Teensy press state.
            sensedPressAmount = ValveInputState.GetValve(laneIndex) ? 1f : 0f;
        }

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
        solenoidDutyForFullRelease = Mathf.Clamp(solenoidDutyForFullRelease, 0.01f, 1f);
        solenoidDutyDeadZone = Mathf.Clamp(solenoidDutyDeadZone, 0f, 0.1f);
        RefreshPinoutReadout();
    }
}
