using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnitySolenoidTester : MonoBehaviour
{
    [Header("references")]
    public HapticFeedbackManager hapticFeedbackManager;
    public RhythmGameManager rhythmGameManager;
    public NoteSpawner noteSpawner;

    [Header("Teensy pinout from final firmware")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("global fallback pulse")]
    public float testDuty = 1.00f;
    public int testDurationMs = 220;

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
    public bool useDistanceBasedPreCue = false;
    public float preCueDistanceFromTarget = 1.0f;
    public float fallbackPreCueLeadTime = 0.35f;

    [Header("animation during tests")]
    public bool animateValveOnTest = true;
    public float testAnimationPressSeconds = 0.12f;
    public float testAnimationReleaseSeconds = 0.45f;

    [Header("debug")]
    public bool enableKeyboardTesting = true;
    [Tooltip("Number keys 1/2/3 run the full team ERM-delay-solenoid sequence. Hold Ctrl with a number key for solenoid-only.")]
    public bool useTeamSequenceForKeyboardTesting = true;

    private Coroutine[] debugAnimationRoutines = new Coroutine[3];

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

        bool ctrlHeld =
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed;

        int phase = shiftHeld ? shiftedPhase : normalPhase;
        bool forceSolenoidOnly = ctrlHeld;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            RunKeyboardHapticTest(0, phase, forceSolenoidOnly);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            RunKeyboardHapticTest(1, phase, forceSolenoidOnly);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            RunKeyboardHapticTest(2, phase, forceSolenoidOnly);
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
        ClearDebugAnimation();

        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.AllOff();
        }
    }

    void RunKeyboardHapticTest(int channelIndex, int phase, bool forceSolenoidOnly)
    {
        if (useTeamSequenceForKeyboardTesting && !forceSolenoidOnly)
        {
            TestTeamSequence(channelIndex);
            return;
        }

        TestSolenoid(channelIndex, phase);
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

        StartDebugAnimation(
            channelIndex,
            false,
            0f,
            0,
            0,
            0,
            duty,
            0,
            durationMs
        );

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

    void TestTeamSequence(int channelIndex)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager == null)
        {
            Debug.LogWarning("No HapticFeedbackManager found. Cannot run team haptic sequence.");
            return;
        }

        hapticFeedbackManager.TestTeamHapticSequence(
            channelIndex,
            hapticFeedbackManager.tuneErmRampMs,
            hapticFeedbackManager.tuneErmRampCurve,
            hapticFeedbackManager.tuneErmHoldMs,
            hapticFeedbackManager.tuneErmDuty,
            hapticFeedbackManager.tuneTeamSequenceDelayMs,
            hapticFeedbackManager.tuneSolenoidRampMs,
            hapticFeedbackManager.tuneSolenoidRampCurve,
            hapticFeedbackManager.tuneSolenoidRampHoldMs,
            hapticFeedbackManager.tuneSolenoidDuty
        );

        StartDebugAnimation(
            channelIndex,
            true,
            hapticFeedbackManager.tuneErmDuty,
            hapticFeedbackManager.tuneErmRampMs,
            hapticFeedbackManager.tuneErmHoldMs,
            hapticFeedbackManager.tuneTeamSequenceDelayMs,
            hapticFeedbackManager.tuneSolenoidDuty,
            hapticFeedbackManager.tuneSolenoidRampMs,
            hapticFeedbackManager.tuneSolenoidRampHoldMs
        );

        int teensyChannelNumber = TeensyHardwarePinout.LaneToUnityChannelNumber(
            channelIndex,
            hardwarePinout
        );

        Debug.Log(
            "Testing full haptic sequence on channel " + teensyChannelNumber +
            " using ERM ramp=" + hapticFeedbackManager.tuneErmRampMs +
            "ms hold=" + hapticFeedbackManager.tuneErmHoldMs +
            " duty=" + hapticFeedbackManager.tuneErmDuty +
            ", delay=" + hapticFeedbackManager.tuneTeamSequenceDelayMs +
            "ms, solenoid ramp=" + hapticFeedbackManager.tuneSolenoidRampMs +
            "ms hold=" + hapticFeedbackManager.tuneSolenoidRampHoldMs +
            " duty=" + hapticFeedbackManager.tuneSolenoidDuty
        );
    }

    void StartDebugAnimation(
        int channelIndex,
        bool includesErm,
        float ermDuty,
        int ermRampMs,
        int ermHoldMs,
        int delayMs,
        float solenoidDuty,
        int solenoidRampMs,
        int solenoidHoldMs
    )
    {
        if (!animateValveOnTest)
        {
            return;
        }

        if (channelIndex < 0 || channelIndex >= debugAnimationRoutines.Length)
        {
            return;
        }

        if (debugAnimationRoutines[channelIndex] != null)
        {
            StopCoroutine(debugAnimationRoutines[channelIndex]);
        }

        debugAnimationRoutines[channelIndex] = StartCoroutine(
            AnimateValveHapticSequence(
                channelIndex,
                includesErm,
                ermDuty,
                ermRampMs,
                ermHoldMs,
                delayMs,
                solenoidDuty,
                solenoidRampMs,
                solenoidHoldMs
            )
        );
    }

    IEnumerator AnimateValveHapticSequence(
        int channelIndex,
        bool includesErm,
        float ermDuty,
        int ermRampMs,
        int ermHoldMs,
        int delayMs,
        float solenoidDuty,
        int solenoidRampMs,
        int solenoidHoldMs
    )
    {
        float startTime = Time.time;
        float ermSeconds = includesErm
            ? Mathf.Max(0f, ermRampMs + ermHoldMs) / 1000f
            : 0f;
        float releaseStartSeconds = includesErm
            ? Mathf.Max(testAnimationPressSeconds, (ermRampMs + ermHoldMs + delayMs) / 1000f)
            : Mathf.Max(0.01f, testAnimationPressSeconds);

        while (Time.time - startTime < releaseStartSeconds)
        {
            float elapsed = Time.time - startTime;
            ValveInputState.SetDebugValve(channelIndex, true, 1f);
            ValveInputState.SetDebugSolenoidDuty(channelIndex, 0f);
            ValveInputState.SetDebugErmDuty(
                channelIndex,
                includesErm && elapsed <= ermSeconds ? ermDuty : 0f
            );

            yield return null;
        }

        float releaseSeconds = Mathf.Max(
            0.05f,
            Mathf.Max(testAnimationReleaseSeconds, (solenoidRampMs + solenoidHoldMs) / 1000f)
        );
        float releaseStartTime = Time.time;

        while (Time.time - releaseStartTime < releaseSeconds)
        {
            ValveInputState.SetDebugValve(channelIndex, false, 0f);
            ValveInputState.SetDebugSolenoidDuty(channelIndex, solenoidDuty);
            ValveInputState.SetDebugErmDuty(channelIndex, 0f);

            yield return null;
        }

        ValveInputState.ClearDebugLane(channelIndex);
        debugAnimationRoutines[channelIndex] = null;
    }

    void ClearDebugAnimation()
    {
        for (int i = 0; i < debugAnimationRoutines.Length; i++)
        {
            if (debugAnimationRoutines[i] != null)
            {
                StopCoroutine(debugAnimationRoutines[i]);
                debugAnimationRoutines[i] = null;
            }

            ValveInputState.ClearDebugLane(i);
        }
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
        testDuty = Mathf.Clamp01(testDuty);
        testDurationMs = Mathf.Clamp(testDurationMs, 1, SolenoidPulseSettings.MaxRecommendedDurationMs);
        testAnimationPressSeconds = Mathf.Max(0.01f, testAnimationPressSeconds);
        testAnimationReleaseSeconds = Mathf.Max(0.05f, testAnimationReleaseSeconds);
        EnsureSolenoidSettings();
        ApplyPinoutToReferences();
        ApplyTuningToGameplay();
    }

    void OnDisable()
    {
        ClearDebugAnimation();
    }
}
