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

    private static bool[] debugValves = new bool[3];
    private static float[] debugValveAmounts = new float[3];
    private static float[] debugSolenoidDuty = new float[3];
    private static float[] debugErmDuty = new float[3];

    public static bool GetValve(int laneIndex)
    {
        return GetValve(laneIndex, true);
    }

    public static bool GetValve(int laneIndex, bool includeDebugValves)
    {
        if (!IsValidLane(laneIndex))
        {
            return false;
        }

        lock (stateLock)
        {
            return keyboardValves[laneIndex] ||
                teensyValves[laneIndex] ||
                (includeDebugValves && debugValves[laneIndex]);
        }
    }

    public static float GetValveAmount(int laneIndex)
    {
        return GetValveAmount(laneIndex, true);
    }

    public static float GetValveAmount(int laneIndex, bool includeDebugValves)
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

            if (includeDebugValves && debugValves[laneIndex])
            {
                return 1f;
            }

            float amount = Mathf.Clamp01(teensyValveAmounts[laneIndex]);

            if (includeDebugValves)
            {
                amount = Mathf.Max(amount, Mathf.Clamp01(debugValveAmounts[laneIndex]));
            }

            return amount;
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
            return Mathf.Max(
                Mathf.Clamp01(teensySolenoidDuty[laneIndex]),
                Mathf.Clamp01(debugSolenoidDuty[laneIndex])
            );
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
            return Mathf.Max(
                Mathf.Clamp01(teensyErmDuty[laneIndex]),
                Mathf.Clamp01(debugErmDuty[laneIndex])
            );
        }
    }

    public static int GetValveMask()
    {
        int mask = 0;

        lock (stateLock)
        {
            for (int i = 0; i < 3; i++)
            {
                if (keyboardValves[i] || teensyValves[i] || debugValves[i])
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

    public static void SetDebugValve(int laneIndex, bool pressed, float amount)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            debugValves[laneIndex] = pressed;
            debugValveAmounts[laneIndex] = Mathf.Clamp01(amount);
        }
    }

    public static void SetDebugSolenoidDuty(int laneIndex, float duty)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            debugSolenoidDuty[laneIndex] = Mathf.Clamp01(duty);
        }
    }

    public static void SetDebugErmDuty(int laneIndex, float duty)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            debugErmDuty[laneIndex] = Mathf.Clamp01(duty);
        }
    }

    public static void ClearDebugLane(int laneIndex)
    {
        if (!IsValidLane(laneIndex))
        {
            return;
        }

        lock (stateLock)
        {
            debugValves[laneIndex] = false;
            debugValveAmounts[laneIndex] = 0f;
            debugSolenoidDuty[laneIndex] = 0f;
            debugErmDuty[laneIndex] = 0f;
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
                debugValves[i] = false;
                debugValveAmounts[i] = 0f;
                debugSolenoidDuty[i] = 0f;
                debugErmDuty[i] = 0f;
            }
        }
    }

    private static bool IsValidLane(int laneIndex)
    {
        return laneIndex >= 0 && laneIndex < 3;
    }
}
