using UnityEngine;

public static class ValveInputState
{
    private static readonly object stateLock = new object();

    private static bool[] keyboardValves = new bool[3];
    private static bool[] teensyValves = new bool[3];

    // Analog valve depth from ToF distance.
    // 0 = valve fully up / not pressed
    // 1 = valve fully pressed
    private static float[] teensyValveAmounts = new float[3];
    private static int[] teensyValveDistancesMM = new int[3] { 255, 255, 255 };
    private static float[] teensySolenoidDuty = new float[3];
    private static float[] teensyErmDuty = new float[3];

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
            // Keyboard still gives full press for testing.
            if (keyboardValves[laneIndex])
            {
                return 1f;
            }

            return Mathf.Clamp01(teensyValveAmounts[laneIndex]);
        }
    }

    public static int GetValveDistanceMM(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return 255;
        }

        lock (stateLock)
        {
            return teensyValveDistancesMM[laneIndex];
        }
    }

    public static float GetSolenoidDuty(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return 0f;
        }

        lock (stateLock)
        {
            return Mathf.Clamp01(teensySolenoidDuty[laneIndex]);
        }
    }

    public static float GetErmDuty(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return 0f;
        }

        lock (stateLock)
        {
            return Mathf.Clamp01(teensyErmDuty[laneIndex]);
        }
    }

    public static int GetValveMask()
    {
        int mask = 0;

        lock (stateLock)
        {
            for (int i = 0; i < 3; i++)
            {
                if (keyboardValves[i] || teensyValves[i])
                {
                    mask |= 1 << i;
                }
            }
        }

        return mask;
    }

    public static void SetKeyboardValve(int laneIndex, bool pressed)
    {
        if (!IsValidLane(laneIndex))
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
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            teensyValves[laneIndex] = pressed;
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

    public static void SetTeensyValveDistanceMM(int laneIndex, int distanceMM)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            teensyValveDistancesMM[laneIndex] = distanceMM;
        }
    }

    public static void SetTeensySolenoidDuty(int laneIndex, float duty)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            teensySolenoidDuty[laneIndex] = Mathf.Clamp01(duty);
        }
    }

    public static void SetTeensyErmDuty(int laneIndex, float duty)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            teensyErmDuty[laneIndex] = Mathf.Clamp01(duty);
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
                teensyValveDistancesMM[i] = 255;
                teensySolenoidDuty[i] = 0f;
                teensyErmDuty[i] = 0f;
            }
        }
    }

    private static bool IsValidLane(int laneIndex)
    {
        return laneIndex >= 0 && laneIndex < 3;
    }
}
