using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UnityHardwareDebugPanel : MonoBehaviour
{
    [Header("references")]
    public TeensySerialInput teensySerialInput;
    public HapticFeedbackManager hapticFeedbackManager;

    [Header("live telemetry text")]
    public TMP_Text hardwareStateText;

    [Header("button feedback text")]
    public TMP_Text hardwareStatusText;

    [Header("ui inputs")]
    public TMP_InputField thresholdInput;
    public TMP_InputField solenoidDutyInput;
    public TMP_InputField solenoidDurationInput;
    public TMP_InputField ermDutyInput;
    public TMP_InputField ermDurationInput;

    [Header("defaults")]
    public int defaultThresholdMM = 35;
    public float defaultSolenoidDuty = 0.25f;
    public int defaultSolenoidDurationMs = 300;
    public float defaultERMDuty = 0.25f;
    public int defaultERMDurationMs = 120;

    [Header("keyboard testing")]
    public bool enableKeyboardTesting = true;

    [Header("display")]
    public float displayUpdateInterval = 0.10f;

    [Header("status feedback")]
    public Color normalStatusColor = Color.white;
    public Color successStatusColor = new Color(0.3f, 1.0f, 0.35f, 1.0f);
    public Color warningStatusColor = new Color(1.0f, 0.85f, 0.15f, 1.0f);
    public Color errorStatusColor = new Color(1.0f, 0.25f, 0.15f, 1.0f);
    public float statusPunchScale = 1.18f;
    public float statusPunchDuration = 0.16f;

    private float displayTimer = 0f;
    private Vector3 statusBaseScale = Vector3.one;
    private Coroutine statusPunchCoroutine;

    void Start()
    {
        if (teensySerialInput == null)
        {
            teensySerialInput = TeensySerialInput.Instance;
        }

        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hardwareStatusText != null)
        {
            statusBaseScale = hardwareStatusText.rectTransform.localScale;
            SetStatus("Ready", normalStatusColor);
        }

        InitializeInputFields();
        ConfigureInputFieldVisibility();
        RefreshHardwareStateText();
    }

    void Update()
    {
        displayTimer += Time.unscaledDeltaTime;

        if (displayTimer >= displayUpdateInterval)
        {
            displayTimer = 0f;
            RefreshHardwareStateText();
        }

        if (enableKeyboardTesting && !IsTypingInInputField())
        {
            HandleKeyboardTesting();
        }
    }

    void InitializeInputFields()
    {
        if (thresholdInput != null && string.IsNullOrWhiteSpace(thresholdInput.text))
        {
            thresholdInput.text = defaultThresholdMM.ToString();
        }

        if (solenoidDutyInput != null && string.IsNullOrWhiteSpace(solenoidDutyInput.text))
        {
            solenoidDutyInput.text = defaultSolenoidDuty.ToString("0.00", CultureInfo.InvariantCulture);
        }

        if (solenoidDurationInput != null && string.IsNullOrWhiteSpace(solenoidDurationInput.text))
        {
            solenoidDurationInput.text = defaultSolenoidDurationMs.ToString();
        }

        if (ermDutyInput != null && string.IsNullOrWhiteSpace(ermDutyInput.text))
        {
            ermDutyInput.text = defaultERMDuty.ToString("0.00", CultureInfo.InvariantCulture);
        }

        if (ermDurationInput != null && string.IsNullOrWhiteSpace(ermDurationInput.text))
        {
            ermDurationInput.text = defaultERMDurationMs.ToString();
        }
    }

    void ConfigureInputFieldVisibility()
    {
        ConfigureInputField(thresholdInput, TMP_InputField.ContentType.IntegerNumber);
        ConfigureInputField(solenoidDutyInput, TMP_InputField.ContentType.DecimalNumber);
        ConfigureInputField(solenoidDurationInput, TMP_InputField.ContentType.IntegerNumber);
        ConfigureInputField(ermDutyInput, TMP_InputField.ContentType.DecimalNumber);
        ConfigureInputField(ermDurationInput, TMP_InputField.ContentType.IntegerNumber);

        if (hardwareStateText != null)
        {
            hardwareStateText.raycastTarget = false;
        }

        if (hardwareStatusText != null)
        {
            hardwareStatusText.raycastTarget = false;
        }
    }

    void ConfigureInputField(TMP_InputField inputField, TMP_InputField.ContentType contentType)
    {
        if (inputField == null)
        {
            return;
        }

        inputField.interactable = true;
        inputField.contentType = contentType;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.readOnly = false;

        if (inputField.textComponent != null)
        {
            inputField.textComponent.color = Color.black;
            inputField.textComponent.raycastTarget = false;
        }

        TMP_Text placeholderText = inputField.placeholder as TMP_Text;

        if (placeholderText != null)
        {
            placeholderText.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            placeholderText.raycastTarget = false;
        }

        inputField.ForceLabelUpdate();
    }

    bool IsTypingInInputField()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        if (selectedObject == null)
        {
            return false;
        }

        return selectedObject.GetComponent<TMP_InputField>() != null ||
               selectedObject.GetComponentInParent<TMP_InputField>() != null;
    }

    void HandleKeyboardTesting()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        bool ctrlHeld =
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            TestSolenoidLane(0);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            TestSolenoidLane(1);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            TestSolenoidLane(2);
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            TestERMLane(0);
        }

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            TestERMLane(1);
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            TestERMLane(2);
        }

        if (Keyboard.current.xKey.wasPressedThisFrame && !ctrlHeld)
        {
            AllOff();
        }
    }

    public void ApplyThreshold()
    {
        int threshold = ReadIntInput(thresholdInput, defaultThresholdMM);
        threshold = Mathf.Clamp(threshold, 1, 254);

        if (!TryGetHapticManager())
        {
            SetStatus("ERROR: No HapticFeedbackManager found", errorStatusColor);
            return;
        }

        hapticFeedbackManager.SendThreshold(threshold);
        SetStatus("Applied threshold: " + threshold + " mm", successStatusColor);
    }

    public void TestSolenoid1()
    {
        TestSolenoidLane(0);
    }

    public void TestSolenoid2()
    {
        TestSolenoidLane(1);
    }

    public void TestSolenoid3()
    {
        TestSolenoidLane(2);
    }

    public void TestERM1()
    {
        TestERMLane(0);
    }

    public void TestERM2()
    {
        TestERMLane(1);
    }

    public void TestERM3()
    {
        TestERMLane(2);
    }

    public void AllOff()
    {
        if (!TryGetHapticManager())
        {
            SetStatus("ERROR: No HapticFeedbackManager found", errorStatusColor);
            return;
        }

        hapticFeedbackManager.AllOff();
        SetStatus("All outputs OFF", warningStatusColor);
    }

    void TestSolenoidLane(int laneIndex)
    {
        if (!TryGetHapticManager())
        {
            SetStatus("ERROR: No HapticFeedbackManager found", errorStatusColor);
            return;
        }

        float duty = ReadFloatInput(solenoidDutyInput, defaultSolenoidDuty);
        int durationMs = ReadIntInput(solenoidDurationInput, defaultSolenoidDurationMs);

        duty = Mathf.Clamp01(duty);
        durationMs = Mathf.Clamp(durationMs, 1, 5000);

        hapticFeedbackManager.SendSolenoidTest(laneIndex, duty, durationMs);

        SetStatus(
            "Sent Solenoid " + (laneIndex + 1) +
            ": duty " + duty.ToString("0.00", CultureInfo.InvariantCulture) +
            ", phase 1, " + durationMs + " ms",
            successStatusColor
        );
    }

    void TestERMLane(int laneIndex)
    {
        if (!TryGetHapticManager())
        {
            SetStatus("ERROR: No HapticFeedbackManager found", errorStatusColor);
            return;
        }

        float duty = ReadFloatInput(ermDutyInput, defaultERMDuty);
        int durationMs = ReadIntInput(ermDurationInput, defaultERMDurationMs);

        duty = Mathf.Clamp01(duty);
        durationMs = Mathf.Clamp(durationMs, 1, 5000);

        hapticFeedbackManager.SendERMTest(laneIndex, duty, durationMs);

        SetStatus(
            "Sent ERM " + (laneIndex + 1) +
            ": duty " + duty.ToString("0.00", CultureInfo.InvariantCulture) +
            ", " + durationMs + " ms",
            successStatusColor
        );
    }

    bool TryGetHapticManager()
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        return hapticFeedbackManager != null;
    }

    void RefreshHardwareStateText()
    {
        if (hardwareStateText == null)
        {
            return;
        }

        if (teensySerialInput == null)
        {
            teensySerialInput = TeensySerialInput.Instance;
        }

        if (teensySerialInput == null)
        {
            hardwareStateText.text = "No TeensySerialInput found.";
            return;
        }

        hardwareStateText.text =
            teensySerialInput.GetHardwareStateSummary() +
            "\n\nKeyboard Debug\n" +
            "1/2/3 = Solenoid 1/2/3 phase 1\n" +
            "F1/F2/F3 = ERM 1/2/3\n" +
            "X = all off";
    }

    void SetStatus(string message, Color color)
    {
        if (hardwareStatusText == null)
        {
            Debug.Log(message);
            return;
        }

        hardwareStatusText.text = message;
        hardwareStatusText.color = color;

        if (statusPunchCoroutine != null)
        {
            StopCoroutine(statusPunchCoroutine);
        }

        statusPunchCoroutine = StartCoroutine(PunchStatusText());
    }

    IEnumerator PunchStatusText()
    {
        if (hardwareStatusText == null)
        {
            yield break;
        }

        RectTransform rect = hardwareStatusText.rectTransform;
        float elapsed = 0f;

        rect.localScale = statusBaseScale * statusPunchScale;

        while (elapsed < statusPunchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / statusPunchDuration);

            rect.localScale = Vector3.Lerp(
                statusBaseScale * statusPunchScale,
                statusBaseScale,
                t
            );

            yield return null;
        }

        rect.localScale = statusBaseScale;
    }

    float ReadFloatInput(TMP_InputField inputField, float fallback)
    {
        if (inputField == null)
        {
            return fallback;
        }

        if (float.TryParse(
                inputField.text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value))
        {
            return value;
        }

        SetStatus("Invalid number: " + inputField.name, errorStatusColor);
        return fallback;
    }

    int ReadIntInput(TMP_InputField inputField, int fallback)
    {
        if (inputField == null)
        {
            return fallback;
        }

        if (int.TryParse(inputField.text, out int value))
        {
            return value;
        }

        SetStatus("Invalid integer: " + inputField.name, errorStatusColor);
        return fallback;
    }
}