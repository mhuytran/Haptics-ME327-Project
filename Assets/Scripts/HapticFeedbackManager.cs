using System.Globalization;
using UnityEngine;

public enum HapticCommandProfile
{
    MvpOnly,
    MvpAndTuning
}

public class HapticFeedbackManager : MonoBehaviour
{
    public static HapticFeedbackManager Instance { get; private set; }

    [Header("serial source")]
    public TeensySerialInput teensySerialInput;

    [Header("Teensy pinout from trumpal_teensy_code")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("MVP command filter")]
    [TextArea(5, 9)]
    public string commandGuide =
        "Needed for gameplay: X, PRECUE, TAPCOMPLETE, HOLDSTART, HOLDCOMPLETE, MISS. Needed for bench tuning: SOL, ERM, ERMRAMP, THRESH. Not used by Unity MVP: READ, STREAM, RATE, A/B/C quick keys, blocking scale loops.";
    [Tooltip("MvpAndTuning lets inspector tune buttons send SOL/ERM/ERMRAMP/THRESH. MvpOnly blocks those and keeps gameplay commands only.")]
    public HapticCommandProfile commandProfile = HapticCommandProfile.MvpAndTuning;
    [Tooltip("Leave on so accidental raw/debug commands cannot be sent through this manager.")]
    public bool filterOutgoingCommands = true;

    [Header("Debug")]
    public bool enableHaptics = true;
    public bool printCommands = true;

    [Header("Solenoid safety caps")]
    [Range(0f, 1f)]
    [Tooltip("Maximum solenoid duty Unity is allowed to send. Firmware also caps this.")]
    public float maxSolenoidDuty = SolenoidPulseSettings.MaxRecommendedDuty;
    [Tooltip("Maximum solenoid pulse duration Unity is allowed to send. Firmware also caps this.")]
    public int maxSolenoidDurationMs = SolenoidPulseSettings.MaxRecommendedDurationMs;

    [Header("ERM pre-cue ramp")]
    public bool useRampedPreCue = true;
    [Range(0f, 1f)]
    [Tooltip("Main ERM pre-cue strength. Partner ramp value is 0.25.")]
    public float preCueErmDuty = 0.25f;
    [Tooltip("ERM pre-cue ramp time. Partner ramp value is 800 ms.")]
    public int preCueErmRampMs = 800;
    [Tooltip("ERM hold time after the ramp completes.")]
    public int preCueErmHoldMs = 120;
    [Range(0f, 1f)]
    public float maxErmDuty = 0.45f;
    public int maxErmRampMs = 1200;

    [Header("Quick bench tuning")]
    [Range(1, 3)]
    public int tuneLane = 1;
    [Range(0f, 1f)]
    public float tuneSolenoidDuty = 0.80f;
    public int tuneSolenoidDurationMs = 125;
    [Range(0f, 1f)]
    public float tuneErmDuty = 0.25f;
    public int tuneErmDurationMs = 120;
    public int tuneErmRampMs = 800;
    public int tuneErmHoldMs = 120;
    public int tuneRawTofThresholdMM = 65;
    [TextArea(2, 4)]
    public string lastCommandStatus = "No haptic command sent yet.";

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

    public void TestErm(int channelIndex, float duty, int durationMs)
    {
        int channelNumber = GetTeensyChannelNumber(channelIndex);
        float safeDuty = Mathf.Clamp(duty, 0f, Mathf.Clamp01(maxErmDuty));
        durationMs = Mathf.Clamp(durationMs, 1, 500);

        Send(
            "ERM," +
            channelNumber + "," +
            safeDuty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            durationMs
        );
    }

    public void TestErmRamp(int channelIndex, float duty, int rampMs, int holdMs)
    {
        int channelNumber = GetTeensyChannelNumber(channelIndex);
        float safeDuty = Mathf.Clamp(duty, 0f, Mathf.Clamp01(maxErmDuty));
        rampMs = Mathf.Clamp(rampMs, 1, Mathf.Max(1, maxErmRampMs));
        holdMs = Mathf.Clamp(holdMs, 0, 500);

        Send(
            "ERMRAMP," +
            channelNumber + "," +
            safeDuty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            rampMs + "," +
            holdMs
        );
    }

    public void SendRawTofThreshold(int thresholdMM)
    {
        thresholdMM = Mathf.Clamp(thresholdMM, 1, 254);
        Send("THRESH," + thresholdMM);
    }

    [ContextMenu("Tune/Pulse Selected Solenoid")]
    public void TunePulseSelectedSolenoid()
    {
        TestSolenoid(tuneLane - 1, tuneSolenoidDuty, TeensyHardwarePinout.ActiveSolenoidPhase, tuneSolenoidDurationMs);
    }

    [ContextMenu("Tune/Pulse Selected ERM")]
    public void TunePulseSelectedErm()
    {
        TestErm(tuneLane - 1, tuneErmDuty, tuneErmDurationMs);
    }

    [ContextMenu("Tune/Ramp Selected ERM")]
    public void TuneRampSelectedErm()
    {
        TestErmRamp(tuneLane - 1, tuneErmDuty, tuneErmRampMs, tuneErmHoldMs);
    }

    [ContextMenu("Tune/Send Raw ToF Debug Threshold")]
    public void TuneSendRawTofThreshold()
    {
        SendRawTofThreshold(tuneRawTofThresholdMM);
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

        if (filterOutgoingCommands && !IsCommandAllowed(command))
        {
            lastCommandStatus = "BLOCKED by command filter: " + command;

            if (printCommands)
            {
                Debug.LogWarning(lastCommandStatus);
            }

            return;
        }

        if (teensySerialInput == null)
        {
            teensySerialInput = TeensySerialInput.Instance;
        }

        if (teensySerialInput == null)
        {
            lastCommandStatus = "No TeensySerialInput found. Not sent: " + command;
            Debug.LogWarning(lastCommandStatus);
            return;
        }

        if (printCommands)
        {
            Debug.Log("Haptic command: " + command);
        }

        lastCommandStatus = "Sent: " + command;
        teensySerialInput.SendLine(command);
    }

    bool IsCommandAllowed(string command)
    {
        string commandName = GetCommandName(command);

        if (commandName == "X" ||
            commandName == "PRECUE" ||
            commandName == "TAPCOMPLETE" ||
            commandName == "HOLDSTART" ||
            commandName == "HOLDCOMPLETE" ||
            commandName == "MISS")
        {
            return true;
        }

        if (commandProfile == HapticCommandProfile.MvpAndTuning &&
            (commandName == "SOL" ||
             commandName == "ERM" ||
             commandName == "ERMRAMP" ||
             commandName == "THRESH"))
        {
            return true;
        }

        return false;
    }

    string GetCommandName(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "";
        }

        int commaIndex = command.IndexOf(',');
        string commandName = commaIndex >= 0 ? command.Substring(0, commaIndex) : command;
        return commandName.Trim().ToUpperInvariant();
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
        tuneLane = Mathf.Clamp(tuneLane, 1, TeensyHardwarePinout.ChannelCount);
        tuneSolenoidDuty = Mathf.Clamp(tuneSolenoidDuty, 0f, maxSolenoidDuty);
        tuneSolenoidDurationMs = Mathf.Clamp(tuneSolenoidDurationMs, 1, maxSolenoidDurationMs);
        tuneErmDuty = Mathf.Clamp(tuneErmDuty, 0f, maxErmDuty);
        tuneErmDurationMs = Mathf.Clamp(tuneErmDurationMs, 1, 500);
        tuneErmRampMs = Mathf.Clamp(tuneErmRampMs, 1, maxErmRampMs);
        tuneErmHoldMs = Mathf.Clamp(tuneErmHoldMs, 0, 500);
        tuneRawTofThresholdMM = Mathf.Clamp(tuneRawTofThresholdMM, 1, 254);
    }
}
