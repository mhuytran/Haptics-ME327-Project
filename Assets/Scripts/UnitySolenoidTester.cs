using UnityEngine;
using UnityEngine.InputSystem;

public class UnitySolenoidTester : MonoBehaviour
{
    [Header("references")]
    public HapticFeedbackManager hapticFeedbackManager;

    [Header("test pulse")]
    public float testDuty = 0.35f;
    public int testDurationMs = 300;

    [Header("phase")]
    public int normalPhase = 0;
    public int shiftedPhase = 1;

    [Header("debug")]
    public bool enableKeyboardTesting = true;

    void Start()
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }
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

        hapticFeedbackManager.TestSolenoid(
            channelIndex,
            testDuty,
            phase,
            testDurationMs
        );

        Debug.Log(
            "Testing solenoid " + (channelIndex + 1) +
            " duty=" + testDuty +
            " phase=" + phase +
            " durationMs=" + testDurationMs
        );
    }
}