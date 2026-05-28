using System;
using UnityEngine;

[Serializable]
public class SolenoidPulseSettings
{
    public const float MaxRecommendedDuty = 1.00f;
    public const int MaxRecommendedDurationMs = 350;

    [Range(0f, 1f)]
    public float duty = 0.80f;

    public int phase = TeensyHardwarePinout.ActiveSolenoidPhase;
    public int durationMs = 125;

    public void Clamp()
    {
        duty = Mathf.Clamp(duty, 0f, MaxRecommendedDuty);
        phase = phase == 0 ? 0 : 1;
        durationMs = Mathf.Clamp(durationMs, 1, MaxRecommendedDurationMs);
    }
}
