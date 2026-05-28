using System;
using UnityEngine;

[Serializable]
public class SolenoidPulseSettings
{
    [Range(0f, 1f)]
    public float duty = 0.35f;

    public int phase = TeensyHardwarePinout.ActiveSolenoidPhase;
    public int durationMs = 120;

    public void Clamp()
    {
        duty = Mathf.Clamp01(duty);
        phase = phase == 0 ? 0 : 1;
        durationMs = Mathf.Max(1, durationMs);
    }
}
