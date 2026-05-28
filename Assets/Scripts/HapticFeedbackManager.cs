using UnityEngine;

public class HapticFeedbackManager : MonoBehaviour
{
    public static HapticFeedbackManager Instance { get; private set; }

    [Header("serial source")]
    public TeensySerialInput teensySerialInput;

    [Header("Teensy pinout from trumpal_teensy_code")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("debug")]
    public bool enableHaptics = true;
    public bool printCommands = true;

    private bool emergencyStopped = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (teensySerialInput == null)
        {
            teensySerialInput = TeensySerialInput.Instance;
        }
    }

    public void SendPreCue(int laneIndex)
    {
        Send("PRECUE," + GetTeensyChannelNumber(laneIndex));
    }

    public void SendTapComplete(int laneIndex, bool perfect)
    {
        string rating = perfect ? "PERFECT" : "GOOD";
        Send("TAPCOMPLETE," + GetTeensyChannelNumber(laneIndex) + "," + rating);
    }

    public void SendHoldStart(int laneIndex)
    {
        Send("HOLDSTART," + GetTeensyChannelNumber(laneIndex));
    }

    public void SendHoldComplete(int laneIndex)
    {
        Send("HOLDCOMPLETE," + GetTeensyChannelNumber(laneIndex));
    }

    public void SendMiss(int laneIndex)
    {
        Send("MISS," + GetTeensyChannelNumber(laneIndex));
    }

    public void SendSolenoidPush(int laneIndex, SolenoidPulseSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("No solenoid pulse settings found for " + GetPinoutLabel(laneIndex));
            return;
        }

        settings.Clamp();
        TestSolenoid(laneIndex, settings.duty, settings.phase, settings.durationMs);
    }

    public void TestSolenoid(int channelIndex, float duty, int phase, int durationMs)
    {
        int channelNumber = GetTeensyChannelNumber(channelIndex);
        duty = Mathf.Clamp01(duty);
        phase = phase == 0 ? 0 : 1;
        durationMs = Mathf.Max(1, durationMs);

        Send("SOL," + channelNumber + "," + duty.ToString("0.00") + "," + phase + "," + durationMs);
    }

    public void AllOff()
    {
        Send("X");
    }

    public void EmergencyAllOff()
    {
        emergencyStopped = true;

        if (teensySerialInput == null)
        {
            teensySerialInput = TeensySerialInput.Instance;
        }

        if (printCommands)
        {
            Debug.LogWarning("Emergency haptic shutdown: X");
        }

        if (teensySerialInput != null)
        {
            teensySerialInput.SendLine("X");
        }
    }

    public void ResetEmergencyStop()
    {
        emergencyStopped = false;
    }

    private void Send(string command)
    {
        if (emergencyStopped)
        {
            return;
        }

        if (!enableHaptics)
        {
            return;
        }

        if (teensySerialInput == null)
        {
            teensySerialInput = TeensySerialInput.Instance;
        }

        if (teensySerialInput == null)
        {
            Debug.LogWarning("No TeensySerialInput found. Haptic command not sent: " + command);
            return;
        }

        if (printCommands)
        {
            Debug.Log("Haptic command: " + command);
        }

        teensySerialInput.SendLine(command);
    }

    int GetTeensyChannelNumber(int laneIndex)
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        return TeensyHardwarePinout.LaneToUnityChannelNumber(laneIndex, hardwarePinout);
    }

    string GetPinoutLabel(int laneIndex)
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        return TeensyHardwarePinout.GetDebugLabel(laneIndex, hardwarePinout);
    }

    void OnValidate()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
    }
}
