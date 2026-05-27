using System.Globalization;
using UnityEngine;

public class HapticFeedbackManager : MonoBehaviour
{
    public static HapticFeedbackManager Instance { get; private set; }

    [Header("serial source")]
    public TeensySerialInput teensySerialInput;

    [Header("debug")]
    public bool enableHaptics = true;
    public bool printCommands = true;

    [Header("solenoid setup")]
    public int solenoidPhase = 1;

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
        Send("PRECUE," + (laneIndex + 1));
    }

    public void SendTapComplete(int laneIndex, bool perfect)
    {
        string rating = perfect ? "PERFECT" : "GOOD";
        Send("TAPCOMPLETE," + (laneIndex + 1) + "," + rating);
    }

    public void SendHoldStart(int laneIndex)
    {
        Send("HOLDSTART," + (laneIndex + 1));
    }

    public void SendHoldComplete(int laneIndex)
    {
        Send("HOLDCOMPLETE," + (laneIndex + 1));
    }

    public void SendMiss(int laneIndex)
    {
        Send("MISS," + (laneIndex + 1));
    }

    // Current correct version: solenoid always uses phase 1.
    public void SendSolenoidTest(int laneIndex, float duty, int durationMs)
    {
        SendSolenoidCommand(laneIndex, duty, solenoidPhase, durationMs);
    }

    // Compatibility overload for older scripts that still pass phase.
    // The passed phase is intentionally ignored because your hardware only uses phase 1.
    public void SendSolenoidTest(int laneIndex, float duty, int ignoredPhase, int durationMs)
    {
        SendSolenoidCommand(laneIndex, duty, solenoidPhase, durationMs);
    }

    public void TestSolenoid(int laneIndex, float duty, int durationMs)
    {
        SendSolenoidCommand(laneIndex, duty, solenoidPhase, durationMs);
    }

    public void TestSolenoid(int laneIndex, float duty, int ignoredPhase, int durationMs)
    {
        SendSolenoidCommand(laneIndex, duty, solenoidPhase, durationMs);
    }

    void SendSolenoidCommand(int laneIndex, float duty, int phase, int durationMs)
    {
        duty = Mathf.Clamp01(duty);
        phase = 1;
        durationMs = Mathf.Max(1, durationMs);

        string command =
            "SOL," +
            (laneIndex + 1) + "," +
            duty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            phase + "," +
            durationMs;

        Send(command);
    }

    public void SendERMTest(int laneIndex, float duty, int durationMs)
    {
        duty = Mathf.Clamp01(duty);
        durationMs = Mathf.Max(1, durationMs);

        string command =
            "ERM," +
            (laneIndex + 1) + "," +
            duty.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            durationMs;

        Send(command);
    }

    public void TestERM(int laneIndex, float duty, int durationMs)
    {
        SendERMTest(laneIndex, duty, durationMs);
    }

    public void SendThreshold(int thresholdMM)
    {
        thresholdMM = Mathf.Clamp(thresholdMM, 1, 254);
        Send("THRESH," + thresholdMM);
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
}