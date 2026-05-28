using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnitySolenoidTester : MonoBehaviour
{
    [Header("references")]
    public HapticFeedbackManager hapticFeedbackManager;
    public RhythmGameManager rhythmGameManager;
    public NoteSpawner noteSpawner;

    [Header("Teensy pinout from trumpal_teensy_code")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("global fallback pulse")]
    public float testDuty = 0.35f;
    public int testDurationMs = 300;

    [Header("phase")]
    public int normalPhase = TeensyHardwarePinout.ActiveSolenoidPhase;
    public int shiftedPhase = 0;

    [Header("per-lane pulse tuning")]
    public bool usePerLanePulseSettings = true;
    public bool applyPulseSettingsToGameplay = true;
    public SolenoidPulseSettings[] solenoidPulseSettings = new SolenoidPulseSettings[]
    {
        new SolenoidPulseSettings(),
        new SolenoidPulseSettings(),
        new SolenoidPulseSettings()
    };

    [Header("note cue tuning")]
    public bool applyPreCueTuningToSpawner = true;
    public bool useDistanceBasedPreCue = true;
    public float preCueDistanceFromTarget = 0.45f;
    public float fallbackPreCueLeadTime = 0.35f;

    [Header("animation during tests")]
    public bool animateValveOnTest = true;
    public float testAnimationPressSeconds = 0.12f;

    [Header("debug")]
    public bool enableKeyboardTesting = true;

    void Start()
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (rhythmGameManager == null)
        {
            rhythmGameManager = Object.FindAnyObjectByType<RhythmGameManager>();
        }

        if (noteSpawner == null)
        {
            noteSpawner = Object.FindAnyObjectByType<NoteSpawner>();
        }

        EnsureSolenoidSettings();
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        ApplyPinoutToReferences();
        ApplyTuningToGameplay();
    }

    void Update()
    {
        if (!enableKeyboardTesting)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        bool shiftHeld =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        int phase = shiftHeld ? shiftedPhase : normalPhase;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            TestSolenoid1(phase);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            TestSolenoid2(phase);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            TestSolenoid3(phase);
        }

        if (Keyboard.current.xKey.wasPressedThisFrame &&
            !Keyboard.current.leftCtrlKey.isPressed &&
            !Keyboard.current.rightCtrlKey.isPressed)
        {
            AllOff();
        }
    }

    public void TestSolenoid1Phase0()
    {
        TestSolenoid(0, 0);
    }

    public void TestSolenoid1Phase1()
    {
        TestSolenoid(0, 1);
    }

    public void TestSolenoid2Phase0()
    {
        TestSolenoid(1, 0);
    }

    public void TestSolenoid2Phase1()
    {
        TestSolenoid(1, 1);
    }

    public void TestSolenoid3Phase0()
    {
        TestSolenoid(2, 0);
    }

    public void TestSolenoid3Phase1()
    {
        TestSolenoid(2, 1);
    }

    public void AllOff()
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.AllOff();
        }
    }

    void TestSolenoid1(int phase)
    {
        TestSolenoid(0, phase);
    }

    void TestSolenoid2(int phase)
    {
        TestSolenoid(1, phase);
    }

    void TestSolenoid3(int phase)
    {
        TestSolenoid(2, phase);
    }

    void TestSolenoid(int channelIndex, int phase)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager == null)
        {
            Debug.LogWarning("No HapticFeedbackManager found. Cannot test solenoid.");
            return;
        }

        float duty = testDuty;
        int durationMs = testDurationMs;

        if (usePerLanePulseSettings)
        {
            EnsureSolenoidSettings();
            SolenoidPulseSettings settings = solenoidPulseSettings[channelIndex];
            duty = settings.duty;
            durationMs = settings.durationMs;

            if (phase != shiftedPhase)
            {
                phase = settings.phase;
            }
        }

        hapticFeedbackManager.TestSolenoid(channelIndex, duty, phase, durationMs);

        if (animateValveOnTest)
        {
            StartCoroutine(AnimateValveTest(channelIndex));
        }

        int teensyChannelNumber = TeensyHardwarePinout.LaneToUnityChannelNumber(
            channelIndex,
            hardwarePinout
        );

        Debug.Log(
            "Testing solenoid channel " + teensyChannelNumber +
            " duty=" + duty +
            " phase=" + phase +
            " durationMs=" + durationMs +
            " on " + TeensyHardwarePinout.GetDebugLabel(channelIndex, hardwarePinout)
        );
    }

    IEnumerator AnimateValveTest(int channelIndex)
    {
        float endTime = Time.time + Mathf.Max(0.01f, testAnimationPressSeconds);

        while (Time.time < endTime)
        {
            ValveInputState.SetTeensyValveAmount(channelIndex, 1f);
            yield return null;
        }

        ValveInputState.SetTeensyValveAmount(channelIndex, 0f);
    }

    public void ApplyTuningToGameplay()
    {
        EnsureSolenoidSettings();
        ApplyPinoutToReferences();

        if (applyPulseSettingsToGameplay && rhythmGameManager != null)
        {
            rhythmGameManager.solenoidPushSettings = solenoidPulseSettings;
        }

        if (applyPreCueTuningToSpawner && noteSpawner != null)
        {
            noteSpawner.useDistanceBasedPreCue = useDistanceBasedPreCue;
            noteSpawner.preCueDistanceFromTarget = preCueDistanceFromTarget;
            noteSpawner.fallbackPreCueLeadTime = fallbackPreCueLeadTime;
        }
    }

    void ApplyPinoutToReferences()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.hardwarePinout = hardwarePinout;
        }

        TeensySerialInput serialInput = TeensySerialInput.Instance;

        if (serialInput != null)
        {
            serialInput.hardwarePinout = hardwarePinout;
        }
    }

    void EnsureSolenoidSettings()
    {
        if (solenoidPulseSettings == null || solenoidPulseSettings.Length != 3)
        {
            SolenoidPulseSettings[] resizedSettings = new SolenoidPulseSettings[3];

            for (int i = 0; i < resizedSettings.Length; i++)
            {
                if (solenoidPulseSettings != null && i < solenoidPulseSettings.Length)
                {
                    resizedSettings[i] = solenoidPulseSettings[i];
                }

                if (resizedSettings[i] == null)
                {
                    resizedSettings[i] = new SolenoidPulseSettings();
                }
            }

            solenoidPulseSettings = resizedSettings;
        }

        for (int i = 0; i < solenoidPulseSettings.Length; i++)
        {
            if (solenoidPulseSettings[i] == null)
            {
                solenoidPulseSettings[i] = new SolenoidPulseSettings();
            }

            solenoidPulseSettings[i].Clamp();
        }
    }

    void OnValidate()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        EnsureSolenoidSettings();
        ApplyPinoutToReferences();
        ApplyTuningToGameplay();
    }
}
