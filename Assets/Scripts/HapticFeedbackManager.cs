using UnityEngine;

public class HapticFeedbackManager : MonoBehaviour
{
    public static HapticFeedbackManager Instance { get; private set; }

    [Header("serial source")]
    public TeensySerialInput teensySerialInput;

    [Header("debug")]
    public bool enableHaptics = true;
    public bool printCommands = true;

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
        string command = "PRECUE," + (laneIndex + 1);
        Send(command);
    }

    public void SendTapComplete(int laneIndex, bool perfect)
    {
        string rating = perfect ? "PERFECT" : "GOOD";
        string command = "TAPCOMPLETE," + (laneIndex + 1) + "," + rating;
        Send(command);
    }

    public void SendHoldStart(int laneIndex)
    {
        string command = "HOLDSTART," + (laneIndex + 1);
        Send(command);
    }

    public void SendHoldComplete(int laneIndex)
    {
        string command = "HOLDCOMPLETE," + (laneIndex + 1);
        Send(command);
    }

    public void SendMiss(int laneIndex)
    {
        string command = "MISS," + (laneIndex + 1);
        Send(command);
    }

    public void AllOff()
    {
        Send("X");
    }

    void Send(string command)
    {
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