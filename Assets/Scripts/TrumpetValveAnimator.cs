using UnityEngine;

public class TrumpetValveAnimator : MonoBehaviour
{
    [Header("Teensy pinout from trumpal_teensy_code")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("input lane")]
    [Tooltip("0 = valve 1, 1 = valve 2, 2 = valve 3. This picks the matching Teensy ToF channel.")]
    public int laneIndex = 0;

    [Header("valve motion")]
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.18f, 0f);
    public float motionSpeed = 8.0f;

    [Header("analog ToF control")]
    public bool useAnalogValveAmount = true;

    [Header("debug readout")]
    public string pinoutReadout = "";
    public int latestDistanceMM = 255;
    [Range(0f, 1f)]
    public float latestPressAmount = 0f;

    private Vector3 restLocalPosition;

    void Start()
    {
        RefreshPinoutReadout();
        restLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float pressAmount;

        if (useAnalogValveAmount)
        {
            // Uses continuous ToF distance from Teensy.
            // 0 = valve up, 1 = valve fully pressed.
            pressAmount = ValveInputState.GetValveAmount(laneIndex);
        }
        else
        {
            // Uses binary keyboard/Teensy press state.
            pressAmount = ValveInputState.GetValve(laneIndex) ? 1f : 0f;
        }

        latestDistanceMM = ValveInputState.GetValveDistanceMM(laneIndex);
        latestPressAmount = pressAmount;
        RefreshPinoutReadout();

        Vector3 targetPosition = restLocalPosition + pressedLocalOffset * pressAmount;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetPosition,
            motionSpeed * Time.deltaTime
        );
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
        RefreshPinoutReadout();
    }
}
