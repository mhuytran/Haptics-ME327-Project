using UnityEngine;
using UnityEngine.InputSystem;

public class HardwareDebugModeController : MonoBehaviour
{
    [Header("debug panel")]
    public GameObject hardwareDebugPanel;

    [Header("game manager")]
    public RhythmGameManager rhythmGameManager;

    [Header("shortcut")]
    public bool requireCtrl = true;
    public bool requireShift = true;
    public Key debugToggleKey = Key.H;

    [Header("debug behavior")]
    public bool startHidden = true;
    public bool makeHealthInfiniteInDebugMode = true;

    [Header("debug logging")]
    public bool printDebugStateChanges = true;

    private bool debugModeActive = false;
    private float originalMissDamage = 0f;
    private bool cachedOriginalMissDamage = false;

    void Start()
    {
        if (rhythmGameManager == null)
        {
            rhythmGameManager = FindAnyObjectByType<RhythmGameManager>();
        }

        if (rhythmGameManager != null && !cachedOriginalMissDamage)
        {
            originalMissDamage = rhythmGameManager.missDamage;
            cachedOriginalMissDamage = true;
        }

        if (hardwareDebugPanel != null && startHidden)
        {
            hardwareDebugPanel.SetActive(false);
        }

        debugModeActive = hardwareDebugPanel != null && hardwareDebugPanel.activeSelf;

        ApplyDebugModeState();
    }

    void Update()
    {
        if (WasToggleShortcutPressed())
        {
            ToggleDebugMode();
        }

        if (debugModeActive && makeHealthInfiniteInDebugMode)
        {
            ForceInfiniteHealth();
        }
    }

    bool WasToggleShortcutPressed()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        bool ctrlOk = !requireCtrl ||
                      Keyboard.current.leftCtrlKey.isPressed ||
                      Keyboard.current.rightCtrlKey.isPressed;

        bool shiftOk = !requireShift ||
                       Keyboard.current.leftShiftKey.isPressed ||
                       Keyboard.current.rightShiftKey.isPressed;

        bool keyPressed = Keyboard.current[debugToggleKey].wasPressedThisFrame;

        return ctrlOk && shiftOk && keyPressed;
    }

    public void ToggleDebugMode()
    {
        SetDebugMode(!debugModeActive);
    }

    public void EnableDebugMode()
    {
        SetDebugMode(true);
    }

    public void DisableDebugMode()
    {
        SetDebugMode(false);
    }

    void SetDebugMode(bool enabled)
    {
        debugModeActive = enabled;

        if (hardwareDebugPanel != null)
        {
            hardwareDebugPanel.SetActive(debugModeActive);
        }

        ApplyDebugModeState();

        if (printDebugStateChanges)
        {
            Debug.Log(debugModeActive
                ? "Hardware debug mode ENABLED. Infinite health active."
                : "Hardware debug mode DISABLED. Normal health restored.");
        }
    }

    void ApplyDebugModeState()
    {
        if (rhythmGameManager == null)
        {
            rhythmGameManager = FindAnyObjectByType<RhythmGameManager>();
        }

        if (rhythmGameManager == null)
        {
            return;
        }

        if (!cachedOriginalMissDamage)
        {
            originalMissDamage = rhythmGameManager.missDamage;
            cachedOriginalMissDamage = true;
        }

        if (debugModeActive && makeHealthInfiniteInDebugMode)
        {
            rhythmGameManager.missDamage = 0f;
            ForceInfiniteHealth();
        }
        else
        {
            rhythmGameManager.missDamage = originalMissDamage;
        }
    }

    void ForceInfiniteHealth()
    {
        if (rhythmGameManager == null)
        {
            return;
        }

        rhythmGameManager.currentHealth = rhythmGameManager.maxHealth;

        if (rhythmGameManager.uiFeedback != null)
        {
            rhythmGameManager.uiFeedback.SetHealth(1f);
        }
    }
}