using UnityEngine;

public static class ValveInputState
{
    public enum InputSource
    {
        None,
        Keyboard,
        Teensy
    }

    private static readonly object stateLock = new object();

    private static bool[] keyboardValves = new bool[3];
    private static bool[] teensyValves = new bool[3];
    private static float[] teensyValveAmounts = new float[3];

    private static InputSource[] lastPressSource = new InputSource[3];

    public static bool GetValve(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return false;
        }

        lock (stateLock)
        {
            return keyboardValves[laneIndex] || teensyValves[laneIndex];
        }
    }

    public static float GetValveAmount(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return 0f;
        }

        lock (stateLock)
        {
            if (keyboardValves[laneIndex])
            {
                return 1f;
            }

            return Mathf.Clamp01(teensyValveAmounts[laneIndex]);
        }
    }

    public static InputSource GetLastPressSource(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return InputSource.None;
        }

        lock (stateLock)
        {
            return lastPressSource[laneIndex];
        }
    }

    public static void SetKeyboardValve(int laneIndex, bool pressed)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            bool wasPressed = keyboardValves[laneIndex];
            keyboardValves[laneIndex] = pressed;

            if (pressed && !wasPressed)
            {
                lastPressSource[laneIndex] = InputSource.Keyboard;
            }
        }
    }

    public static void SetTeensyValve(int laneIndex, bool pressed)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            bool wasPressed = teensyValves[laneIndex];
            teensyValves[laneIndex] = pressed;

            if (pressed && !wasPressed)
            {
                lastPressSource[laneIndex] = InputSource.Teensy;
            }
        }
    }

    public static void SetTeensyValveAmount(int laneIndex, float amount)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            teensyValveAmounts[laneIndex] = Mathf.Clamp01(amount);
        }
    }

    public static void ClearAll()
    {
        lock (stateLock)
        {
            for (int i = 0; i < 3; i++)
            {
                keyboardValves[i] = false;
                teensyValves[i] = false;
                teensyValveAmounts[i] = 0f;
                lastPressSource[i] = InputSource.None;
            }
        }
    }

    private static bool IsValidLane(int laneIndex)
    {
        return laneIndex >= 0 && laneIndex < 3;
    }
}