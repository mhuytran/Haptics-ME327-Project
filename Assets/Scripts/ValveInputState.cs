using UnityEngine;

public static class ValveInputState
{
    private static readonly object stateLock = new object();

    private static bool[] keyboardValves = new bool[3];
    private static bool[] teensyValves = new bool[3];

    public static bool GetValve(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex > 2)
        {
            return false;
        }

        lock (stateLock)
        {
            return keyboardValves[laneIndex] || teensyValves[laneIndex];
        }
    }

    public static void SetKeyboardValve(int laneIndex, bool pressed)
    {
        if (laneIndex < 0 || laneIndex > 2)
        {
            return;
        }

        lock (stateLock)
        {
            keyboardValves[laneIndex] = pressed;
        }
    }

    public static void SetTeensyValve(int laneIndex, bool pressed)
    {
        if (laneIndex < 0 || laneIndex > 2)
        {
            return;
        }

        lock (stateLock)
        {
            teensyValves[laneIndex] = pressed;
        }
    }

    public static void ClearTeensyState()
    {
        lock (stateLock)
        {
            for (int i = 0; i < 3; i++)
            {
                teensyValves[i] = false;
            }
        }
    }
}