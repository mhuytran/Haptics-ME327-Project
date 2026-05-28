using System.Globalization;
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

    [Header("hardware safety caps")]
    [Range(0f, 1f)]
    public float maxSolenoidDuty = SolenoidPulseSettings.MaxRecommendedDuty;
    public int maxSolenoidDurationMs = SolenoidPulseSettings.MaxRecommendedDurationMs;

    [Header("ERM pre-cue ramp")]
    public bool useRampedPreCue = true;
    [Range(0f, 1f)]
    public float preCueErmDuty = 0.25f;
    public int preCueErmRampMs = 800;
    public int preCueErmHoldMs = 120;
    [Range(0f, 1f)]
    public float maxErmDuty = 0.45f;
    public int maxErmRampMs = 1200;

    private bool emergencyStopped = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            bool thisLivesWithSerialInput = GetComponent<TeensySerialInput>() != null;
            bool instanceLivesWithSerialInput = Instance.GetComponent<TeensySerialInput>() != null;

            if (thisLivesWithSerialInput && !instanceLivesWithSerialInput)
            {
                Instance.enabled = false;
                Instance = this;
                return;
            }

            enabled = false;
            return;
        }

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
        int channelNumber = GetTeensyChannelNumber(laneIndex);

        if (!useRampedPreCue)
        {
            Send("PRECUE," + channelNumber);
            return;
        }

        float safeDuty = Mathf.Clamp(preCueErmDuty, 0f, Mathf.Clamp01(maxErmDuty));
        int safeRampMs = Mathf.Clamp(preCueErmRampMs, 1, Mathf.Max(1, maxErmRampMs));
        int safeHoldMs = Mathf.Clamp(preCueErmHoldMs, 0, 500);

        Send(
            "PRECUE," +
            channelNumber + "," +
            safeDuty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            safeRampMs + "," +
            safeHoldMs
        );
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
        float safeMaxDuty = Mathf.Clamp(
            maxSolenoidDuty,
            0f,
            SolenoidPulseSettings.MaxRecommendedDuty
        );
        int safeMaxDurationMs = Mathf.Clamp(
            maxSolenoidDurationMs,
            1,
            SolenoidPulseSettings.MaxRecommendedDurationMs
        );

        duty = Mathf.Clamp(duty, 0f, safeMaxDuty);
        phase = phase == 0 ? 0 : 1;
        durationMs = Mathf.Clamp(durationMs, 1, safeMaxDurationMs);

        Send(
            "SOL," +
            channelNumber + "," +
            duty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            phase + "," +
            durationMs
        );
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
        maxSolenoidDuty = Mathf.Clamp(maxSolenoidDuty, 0f, SolenoidPulseSettings.MaxRecommendedDuty);
        maxSolenoidDurationMs = Mathf.Clamp(
            maxSolenoidDurationMs,
            1,
            SolenoidPulseSettings.MaxRecommendedDurationMs
        );
        maxErmDuty = Mathf.Clamp(maxErmDuty, 0f, 0.45f);
        preCueErmDuty = Mathf.Clamp(preCueErmDuty, 0f, maxErmDuty);
        maxErmRampMs = Mathf.Clamp(maxErmRampMs, 1, 1200);
        preCueErmRampMs = Mathf.Clamp(preCueErmRampMs, 1, maxErmRampMs);
        preCueErmHoldMs = Mathf.Clamp(preCueErmHoldMs, 0, 500);
    }
}
