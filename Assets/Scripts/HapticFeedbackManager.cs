using System.Globalization;
using System.Collections;
using UnityEngine;

public enum HapticCommandProfile
{
    MvpOnly,
    MvpAndTuning
}

public enum SolenoidRampCurve
{
    Linear = 0,
    Quadratic = 1,
    Exponential = 2
}

public class HapticFeedbackManager : MonoBehaviour
{
    public static HapticFeedbackManager Instance { get; private set; }

    [Header("serial source")]
    public TeensySerialInput teensySerialInput;

    [Header("Teensy pinout from final firmware")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("MVP command filter")]
    [TextArea(5, 9)]
    public string commandGuide =
        "Needed for gameplay: X, PRECUE, TAPCOMPLETE, HOLDSTART, HOLDCOMPLETE, MISS. Needed for bench tuning: SOL, SOLRAMP, ERM, ERMRAMP, TEST, TESTCH, THRESH. Not used by Unity MVP: READ, STREAM, RATE, A/B/C quick keys, blocking scale loops.";
    [Tooltip("MvpAndTuning lets inspector tune buttons send SOL/SOLRAMP/ERM/ERMRAMP/TEST/TESTCH/THRESH. MvpOnly blocks those and keeps gameplay commands only.")]
    public HapticCommandProfile commandProfile = HapticCommandProfile.MvpAndTuning;
    [Tooltip("Leave on so accidental raw/debug commands cannot be sent through this manager.")]
    public bool filterOutgoingCommands = true;

    [Header("Debug")]
    public bool enableHaptics = true;
    public bool printCommands = true;

    [Header("Solenoid safety caps")]
    [Range(0f, 1f)]
    [Tooltip("Maximum solenoid duty Unity is allowed to send. Firmware also caps this.")]
    public float maxSolenoidDuty = 1.00f;
    [Tooltip("Maximum solenoid pulse duration Unity is allowed to send. Firmware also caps this.")]
    public int maxSolenoidDurationMs = 350;

    [Header("playable dwell before push-off")]
    [Tooltip("Delay TAPCOMPLETE/HOLDCOMPLETE/MISS commands so the player gets time to press or hold before the solenoid pushes back. Set to 0 for instant push-off.")]
    public int completionPushDelayMs = 320;

    [Header("ERM pre-cue ramp")]
    public bool useRampedPreCue = true;
    [Range(0f, 1f)]
    [Tooltip("Main ERM pre-cue strength. Team 12V feel-test value is 1.00.")]
    public float preCueErmDuty = 1.00f;
    [Tooltip("Gameplay ERM pre-cue ramp time. Long enough to feel the motor ramp, short enough to cue near the note.")]
    public int preCueErmRampMs = 250;
    [Tooltip("ERM hold time after the ramp completes.")]
    public int preCueErmHoldMs = 200;
    [Tooltip("ERM pre-cue ramp curve. Linear reaches feelable duty quickly for gameplay.")]
    public SolenoidRampCurve preCueErmRampCurve = SolenoidRampCurve.Linear;
    [Range(0f, 1f)]
    public float maxErmDuty = 1.00f;
    public int maxErmRampMs = 1200;

    [Header("Quick bench tuning")]
    [Range(1, 3)]
    public int tuneLane = 1;
    [Range(0f, 1f)]
    public float tuneSolenoidDuty = 1.00f;
    public int tuneSolenoidDurationMs = 350;
    [Tooltip("Standalone tester TEST rampTimeMs equivalent, sent as SOLRAMP.")]
    public int tuneSolenoidRampMs = 800;
    [Tooltip("Standalone tester func equivalent: Linear = 0, Quadratic = 1, Exponential = 2.")]
    public SolenoidRampCurve tuneSolenoidRampCurve = SolenoidRampCurve.Quadratic;
    [Tooltip("Standalone tester holdTimeMs equivalent after the ramp reaches peak duty.")]
    public int tuneSolenoidRampHoldMs = 350;
    [Range(0f, 1f)]
    public float tuneErmDuty = 1.00f;
    public int tuneErmDurationMs = 150;
    public int tuneErmRampMs = 1000;
    [Tooltip("Team TEST eFunc equivalent for the ERM ramp.")]
    public SolenoidRampCurve tuneErmRampCurve = SolenoidRampCurve.Quadratic;
    public int tuneErmHoldMs = 100;
    [Tooltip("Team TEST delayMs between the ERM finishing and the solenoid starting.")]
    public int tuneTeamSequenceDelayMs = 1000;
    public int tuneRawTofThresholdMM = 65;
    [TextArea(2, 4)]
    public string lastCommandStatus = "No haptic command sent yet.";

    private bool emergencyStopped = false;
    private int delayedCompletionGeneration = 0;

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
            safeHoldMs + "," +
            ((int)preCueErmRampCurve)
        );
    }

    public void SendTapComplete(int laneIndex, bool perfect)
    {
        string rating = perfect ? "PERFECT" : "GOOD";
        SendCompletionAfterPlayableDwell("TAPCOMPLETE," + GetTeensyChannelNumber(laneIndex) + "," + rating);
    }

    public void SendHoldStart(int laneIndex)
    {
        Send("HOLDSTART," + GetTeensyChannelNumber(laneIndex));
    }

    public void SendHoldComplete(int laneIndex)
    {
        SendCompletionAfterPlayableDwell("HOLDCOMPLETE," + GetTeensyChannelNumber(laneIndex));
    }

    public void SendMiss(int laneIndex)
    {
        SendCompletionAfterPlayableDwell("MISS," + GetTeensyChannelNumber(laneIndex));
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

    public void TestSolenoidRamp(
        int channelIndex,
        int rampMs,
        SolenoidRampCurve curve,
        int holdMs,
        float peakDuty
    )
    {
        int channelNumber = GetTeensyChannelNumber(channelIndex);
        float safeMaxDuty = Mathf.Clamp(
            maxSolenoidDuty,
            0f,
            SolenoidPulseSettings.MaxRecommendedDuty
        );

        rampMs = Mathf.Clamp(rampMs, 1, 1000);
        holdMs = Mathf.Clamp(holdMs, 0, maxSolenoidDurationMs);
        peakDuty = Mathf.Clamp(peakDuty, 0f, safeMaxDuty);

        Send(
            "SOLRAMP," +
            channelNumber + "," +
            rampMs + "," +
            ((int)curve) + "," +
            holdMs + "," +
            peakDuty.ToString("0.00", CultureInfo.InvariantCulture)
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

    public void TestErmRamp(
        int channelIndex,
        float duty,
        int rampMs,
        int holdMs,
        SolenoidRampCurve curve = SolenoidRampCurve.Linear
    )
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
            holdMs + "," +
            ((int)curve)
        );
    }

    public void TestTeamHapticSequence(
        int channelIndex,
        int ermRampMs,
        SolenoidRampCurve ermCurve,
        int ermHoldMs,
        float ermPeakDuty,
        int delayMs,
        int solenoidRampMs,
        SolenoidRampCurve solenoidCurve,
        int solenoidHoldMs,
        float solenoidPeakDuty
    )
    {
        int channelNumber = GetTeensyChannelNumber(channelIndex);
        float safeMaxSolenoidDuty = Mathf.Clamp(
            maxSolenoidDuty,
            0f,
            SolenoidPulseSettings.MaxRecommendedDuty
        );

        ermRampMs = Mathf.Clamp(ermRampMs, 1, Mathf.Max(1, maxErmRampMs));
        ermHoldMs = Mathf.Clamp(ermHoldMs, 0, 500);
        ermPeakDuty = Mathf.Clamp(ermPeakDuty, 0f, Mathf.Clamp01(maxErmDuty));
        delayMs = Mathf.Clamp(delayMs, 0, 5000);
        solenoidRampMs = Mathf.Clamp(solenoidRampMs, 1, 1000);
        solenoidHoldMs = Mathf.Clamp(solenoidHoldMs, 0, maxSolenoidDurationMs);
        solenoidPeakDuty = Mathf.Clamp(solenoidPeakDuty, 0f, safeMaxSolenoidDuty);

        string commandPrefix = channelNumber == 1
            ? "TEST"
            : "TESTCH," + channelNumber;

        Send(
            commandPrefix + "," +
            ermRampMs + "," +
            ((int)ermCurve) + "," +
            ermHoldMs + "," +
            ermPeakDuty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            delayMs + "," +
            solenoidRampMs + "," +
            ((int)solenoidCurve) + "," +
            solenoidHoldMs + "," +
            solenoidPeakDuty.ToString("0.00", CultureInfo.InvariantCulture)
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

    [ContextMenu("Tune/Ramp Selected Solenoid")]
    public void TuneRampSelectedSolenoid()
    {
        TestSolenoidRamp(
            tuneLane - 1,
            tuneSolenoidRampMs,
            tuneSolenoidRampCurve,
            tuneSolenoidRampHoldMs,
            tuneSolenoidDuty
        );
    }

    [ContextMenu("Tune/Pulse Selected ERM")]
    public void TunePulseSelectedErm()
    {
        TestErm(tuneLane - 1, tuneErmDuty, tuneErmDurationMs);
    }

    [ContextMenu("Tune/Ramp Selected ERM")]
    public void TuneRampSelectedErm()
    {
        TestErmRamp(tuneLane - 1, tuneErmDuty, tuneErmRampMs, tuneErmHoldMs, tuneErmRampCurve);
    }

    [ContextMenu("Tune/Team TEST Sequence")]
    public void TuneTeamTestSequence()
    {
        TestTeamHapticSequence(
            tuneLane - 1,
            tuneErmRampMs,
            tuneErmRampCurve,
            tuneErmHoldMs,
            tuneErmDuty,
            tuneTeamSequenceDelayMs,
            tuneSolenoidRampMs,
            tuneSolenoidRampCurve,
            tuneSolenoidRampHoldMs,
            tuneSolenoidDuty
        );
    }

    [ContextMenu("Tune/Send Raw ToF Debug Threshold")]
    public void TuneSendRawTofThreshold()
    {
        SendRawTofThreshold(tuneRawTofThresholdMM);
    }

    [ContextMenu("Tune/Apply Game-Ready Haptic Defaults")]
    public void ApplyTeam12VFeelDefaults()
    {
        maxSolenoidDuty = 1.00f;
        maxSolenoidDurationMs = 350;
        completionPushDelayMs = 320;

        useRampedPreCue = true;
        preCueErmDuty = 1.00f;
        preCueErmRampMs = 250;
        preCueErmHoldMs = 200;
        preCueErmRampCurve = SolenoidRampCurve.Linear;
        maxErmDuty = 1.00f;
        maxErmRampMs = 1200;

        tuneSolenoidDuty = 1.00f;
        tuneSolenoidDurationMs = 350;
        tuneSolenoidRampMs = 800;
        tuneSolenoidRampCurve = SolenoidRampCurve.Quadratic;
        tuneSolenoidRampHoldMs = 350;

        tuneErmDuty = 1.00f;
        tuneErmDurationMs = 150;
        tuneErmRampMs = 1000;
        tuneErmRampCurve = SolenoidRampCurve.Quadratic;
        tuneErmHoldMs = 100;
        tuneTeamSequenceDelayMs = 1000;

        OnValidate();
    }

    public void AllOff()
    {
        CancelPendingCompletionCommands();
        Send("X");
    }

    public void EmergencyAllOff()
    {
        CancelPendingCompletionCommands();
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

    void OnDisable()
    {
        CancelPendingCompletionCommands();
    }

    void SendCompletionAfterPlayableDwell(string command)
    {
        int safeDelayMs = Mathf.Clamp(completionPushDelayMs, 0, 1000);

        if (safeDelayMs <= 0 || !isActiveAndEnabled)
        {
            Send(command);
            return;
        }

        int generation = delayedCompletionGeneration;
        StartCoroutine(SendCompletionAfterDelay(command, safeDelayMs, generation));

        lastCommandStatus = "Scheduled after " + safeDelayMs + "ms: " + command;

        if (printCommands)
        {
            Debug.Log("Scheduled haptic completion after " + safeDelayMs + "ms: " + command);
        }
    }

    IEnumerator SendCompletionAfterDelay(string command, int delayMs, int generation)
    {
        yield return new WaitForSecondsRealtime(delayMs / 1000f);

        if (generation != delayedCompletionGeneration)
        {
            yield break;
        }

        Send(command);
    }

    void CancelPendingCompletionCommands()
    {
        delayedCompletionGeneration++;
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

        bool sent = teensySerialInput.SendLine(command);
        lastCommandStatus = sent
            ? "Sent: " + command
            : "NOT SENT, Teensy serial is disconnected: " + command;

        if (!sent && printCommands)
        {
            Debug.LogWarning(lastCommandStatus);
        }
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
             commandName == "SOLRAMP" ||
             commandName == "ERM" ||
             commandName == "ERMRAMP" ||
             commandName == "TEST" ||
             commandName == "TESTCH" ||
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
        completionPushDelayMs = Mathf.Clamp(completionPushDelayMs, 0, 1000);
        maxErmDuty = Mathf.Clamp(maxErmDuty, 0f, 1.0f);
        preCueErmDuty = Mathf.Clamp(preCueErmDuty, 0f, maxErmDuty);
        maxErmRampMs = Mathf.Clamp(maxErmRampMs, 1, 1200);
        preCueErmRampMs = Mathf.Clamp(preCueErmRampMs, 1, maxErmRampMs);
        preCueErmHoldMs = Mathf.Clamp(preCueErmHoldMs, 0, 500);
        tuneLane = Mathf.Clamp(tuneLane, 1, TeensyHardwarePinout.ChannelCount);
        tuneSolenoidDuty = Mathf.Clamp(tuneSolenoidDuty, 0f, maxSolenoidDuty);
        tuneSolenoidDurationMs = Mathf.Clamp(tuneSolenoidDurationMs, 1, maxSolenoidDurationMs);
        tuneSolenoidRampMs = Mathf.Clamp(tuneSolenoidRampMs, 1, 1000);
        tuneSolenoidRampHoldMs = Mathf.Clamp(tuneSolenoidRampHoldMs, 0, maxSolenoidDurationMs);
        tuneErmDuty = Mathf.Clamp(tuneErmDuty, 0f, maxErmDuty);
        tuneErmDurationMs = Mathf.Clamp(tuneErmDurationMs, 1, 500);
        tuneErmRampMs = Mathf.Clamp(tuneErmRampMs, 1, maxErmRampMs);
        tuneErmHoldMs = Mathf.Clamp(tuneErmHoldMs, 0, 500);
        tuneTeamSequenceDelayMs = Mathf.Clamp(tuneTeamSequenceDelayMs, 0, 5000);
        tuneRawTofThresholdMM = Mathf.Clamp(tuneRawTofThresholdMM, 1, 254);
    }
}
