using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class RhythmGameManager : MonoBehaviour
{
    [Header("timing windows")]
    public float perfectWindow = 0.08f;
    public float goodWindow = 0.16f;
    public float missWindow = 0.22f;

    [Header("score")]
    public int score = 0;

    [Header("health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float missDamage = 12f;
    public float lowHealthThreshold = 70f;
    public float baseComboHeal = 2f;
    public float comboHealScale = 0.25f;

    [Header("health debug")]
    public bool enableInfiniteHealthShortcut = true;
    public Key infiniteHealthShortcutKey = Key.F8;
    public bool infiniteHealth = false;

    [Header("combo multiplier")]
    public int combo = 0;
    public int baseMultiplierComboThreshold = 10;
    public int pointMultiplier = 1;
    public int maxPointMultiplier = 20;

    [Header("ui")]
    public GameplayUIFeedback uiFeedback;

    [Header("haptics")]
    public HapticFeedbackManager hapticFeedbackManager;

    [Header("solenoid push-off tuning")]
    [Tooltip("Leave off with trumpal_teensy_code.ino because TAPCOMPLETE/HOLDCOMPLETE already trigger the mapped solenoid pins on the Teensy.")]
    public bool sendSolenoidPushOnHit = false;
    public bool pushOnAnyValvePressForTuning = false;
    public SolenoidPulseSettings[] solenoidPushSettings = new SolenoidPulseSettings[]
    {
        new SolenoidPulseSettings(),
        new SolenoidPulseSettings(),
        new SolenoidPulseSettings()
    };

    [Header("MVP fingering judgment")]
    public bool requireExactFingering = true;
    public bool judgeFingeringContinuously = true;

    private List<FlyingNote> activeNotes = new List<FlyingNote>();
    private bool[] previousValveStates = new bool[3];

    private int previousPointMultiplier = 1;
    private bool gameOver = false;

    void Start()
    {
        if (TeensySerialInput.Instance == null || !TeensySerialInput.Instance.isCalibrating)
        {
            Time.timeScale = 1f;
        }

        EnsureSolenoidSettings();

        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateMultiplier();
        UpdateUI();
    }

    void Update()
    {
        HandleDebugShortcuts();

        if (gameOver)
        {
            return;
        }

        int currentValveMask = ValveInputState.GetValveMask();
        bool anyNewPress = false;

        for (int lane = 0; lane < 3; lane++)
        {
            bool pressed = ValveInputState.GetValve(lane);

            if (pressed && !previousValveStates[lane])
            {
                anyNewPress = true;
            }

            previousValveStates[lane] = pressed;
        }

        if (currentValveMask == 0)
        {
            return;
        }

        bool shouldJudge = anyNewPress || judgeFingeringContinuously;
        bool successfulHit = shouldJudge && TryHitCurrentFingering(currentValveMask);

        if (pushOnAnyValvePressForTuning && anyNewPress && !successfulHit)
        {
            SendSolenoidPushForMask(currentValveMask);
        }
    }

    public void RegisterNote(FlyingNote note)
    {
        if (gameOver)
        {
            if (note != null)
            {
                Destroy(note.gameObject);
            }

            return;
        }

        if (!activeNotes.Contains(note))
        {
            activeNotes.Add(note);
        }
    }

    public void UnregisterNote(FlyingNote note)
    {
        activeNotes.Remove(note);
    }

    public bool HasActiveNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i] == null)
            {
                activeNotes.RemoveAt(i);
                continue;
            }

            if (!activeNotes[i].resolved)
            {
                return true;
            }
        }

        return false;
    }

    public void OnNotePreCue(FlyingNote note)
    {
        if (note == null || gameOver)
        {
            return;
        }

        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.SendPreCue(note.laneIndex);
        }
    }

    public bool TryHit(int lane)
    {
        int currentValveMask = ValveInputState.GetValveMask();

        if (currentValveMask == 0)
        {
            currentValveMask = 1 << lane;
        }

        return TryHitCurrentFingering(currentValveMask);
    }

    bool TryHitCurrentFingering(int currentValveMask)
    {
        if (gameOver)
        {
            return false;
        }

        FlyingNote bestNote = null;
        float bestTimingError = float.MaxValue;

        foreach (FlyingNote note in activeNotes)
        {
            if (note == null || note.resolved || note.holdStarted)
            {
                continue;
            }

            int requiredValveMask = note.GetRequiredValveMask();

            if (!IsValveMaskAccepted(requiredValveMask, currentValveMask))
            {
                continue;
            }

            float timingError = Mathf.Abs(Time.time - note.targetHitTime);

            if (timingError < bestTimingError)
            {
                bestTimingError = timingError;
                bestNote = note;
            }
        }

        if (bestNote == null)
        {
            return false;
        }

        if (bestTimingError <= goodWindow)
        {
            bool perfect = bestTimingError <= perfectWindow;
            string rating = perfect ? "PERFECT!" : "GOOD!";
            Color feedbackColor = GetFingeringFeedbackColor(bestNote, perfect);

            if (bestNote.isHoldNote)
            {
                SendHoldStartForMask(bestNote.GetRequiredValveMask());
                StartHoldGroup(bestNote, rating);
                ShowFeedback(rating, feedbackColor);
            }
            else
            {
                if (sendSolenoidPushOnHit)
                {
                    SendSolenoidPushForMask(bestNote.GetRequiredValveMask());
                }

                SendTapCompleteForMask(bestNote.GetRequiredValveMask(), perfect);
                ResolveTapGroup(bestNote, rating);
                RegisterSuccessfulNote(rating, perfect, false);
                ShowFeedback(rating, feedbackColor);
            }

            return true;
        }

        return false;
    }

    public bool IsFingeringHeld(FlyingNote note)
    {
        if (note == null)
        {
            return false;
        }

        return IsValveMaskAccepted(note.GetRequiredValveMask(), ValveInputState.GetValveMask());
    }

    bool IsValveMaskAccepted(int requiredValveMask, int currentValveMask)
    {
        if (requiredValveMask == 0 || currentValveMask == 0)
        {
            return false;
        }

        if (requireExactFingering)
        {
            return currentValveMask == requiredValveMask;
        }

        return (currentValveMask & requiredValveMask) == requiredValveMask;
    }

    void StartHoldGroup(FlyingNote sourceNote, string rating)
    {
        List<FlyingNote> groupNotes = GetFingeringGroup(sourceNote);

        foreach (FlyingNote note in groupNotes)
        {
            if (note != null)
            {
                note.StartHold(rating);
            }
        }
    }

    void ResolveTapGroup(FlyingNote sourceNote, string rating)
    {
        List<FlyingNote> groupNotes = GetFingeringGroup(sourceNote);

        foreach (FlyingNote note in groupNotes)
        {
            if (note != null)
            {
                note.ResolveTapHit(rating);
            }
        }
    }

    void ResolveCompletedHoldGroup(FlyingNote sourceNote)
    {
        List<FlyingNote> groupNotes = GetFingeringGroup(sourceNote);

        foreach (FlyingNote note in groupNotes)
        {
            if (note == null || note == sourceNote)
            {
                continue;
            }

            note.ResolveHoldCompleteFromGroup();
        }
    }

    void ResolveMissedGroup(FlyingNote sourceNote)
    {
        List<FlyingNote> groupNotes = GetFingeringGroup(sourceNote);

        foreach (FlyingNote note in groupNotes)
        {
            if (note == null || note == sourceNote)
            {
                continue;
            }

            note.ResolveMissFromGroup();
        }
    }

    List<FlyingNote> GetFingeringGroup(FlyingNote sourceNote)
    {
        List<FlyingNote> groupNotes = new List<FlyingNote>();

        if (sourceNote == null)
        {
            return groupNotes;
        }

        int groupId = sourceNote.fingeringGroupId;

        if (groupId < 0)
        {
            groupNotes.Add(sourceNote);
            return groupNotes;
        }

        foreach (FlyingNote note in activeNotes)
        {
            if (note != null && note.fingeringGroupId == groupId)
            {
                groupNotes.Add(note);
            }
        }

        if (groupNotes.Count == 0)
        {
            groupNotes.Add(sourceNote);
        }

        return groupNotes;
    }

    Color GetFingeringFeedbackColor(FlyingNote sourceNote, bool perfect)
    {
        List<FlyingNote> groupNotes = GetFingeringGroup(sourceNote);

        if (groupNotes.Count == 0)
        {
            return perfect ? Color.green : Color.yellow;
        }

        Color mixedColor = Color.black;
        int colorCount = 0;

        foreach (FlyingNote note in groupNotes)
        {
            if (note == null)
            {
                continue;
            }

            mixedColor += note.noteColor;
            colorCount++;
        }

        if (colorCount <= 0)
        {
            return perfect ? Color.green : Color.yellow;
        }

        mixedColor /= colorCount;
        mixedColor.a = 1f;

        if (perfect)
        {
            return Color.Lerp(mixedColor, Color.white, 0.25f);
        }

        return mixedColor;
    }

    public void OnHoldCompleted(FlyingNote note)
    {
        if (gameOver)
        {
            return;
        }

        if (note != null)
        {
            SendHoldCompleteForMask(note.GetRequiredValveMask());
            ResolveCompletedHoldGroup(note);
        }

        UnregisterNote(note);
        RegisterSuccessfulNote("", true, false);
    }

    public void OnNoteMissed(FlyingNote note)
    {
        if (gameOver)
        {
            return;
        }

        if (note != null)
        {
            SendMissForMask(note.GetRequiredValveMask());
            ResolveMissedGroup(note);
        }

        UnregisterNote(note);

        if (infiniteHealth)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth -= missDamage;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        combo = 0;
        previousPointMultiplier = pointMultiplier;
        pointMultiplier = 1;

        ShowFeedback("MISS!", Color.red);

        if (uiFeedback != null)
        {
            uiFeedback.SetPowerMode(pointMultiplier);
            uiFeedback.ShowSmallFlash(Color.red, 0.25f);
        }

        UpdateUI();

        if (!infiniteHealth && currentHealth <= 0f)
        {
            TriggerGameOver();
        }
    }

    void RegisterSuccessfulNote(string feedbackMessage, bool strongHit, bool showFeedbackText)
    {
        if (gameOver)
        {
            return;
        }

        combo += 1;

        previousPointMultiplier = pointMultiplier;
        UpdateMultiplier();

        score += pointMultiplier;

        TryRecoverHealthFromCombo();

        if (showFeedbackText && !string.IsNullOrEmpty(feedbackMessage))
        {
            Color feedbackColor = strongHit ? Color.green : Color.yellow;
            ShowFeedback(feedbackMessage, feedbackColor);
        }

        if (uiFeedback != null)
        {
            uiFeedback.ShowCombo(combo, pointMultiplier);
            uiFeedback.SetPowerMode(pointMultiplier);

            if (pointMultiplier > previousPointMultiplier)
            {
                uiFeedback.ShowPowerBurst(pointMultiplier);
                uiFeedback.ShowPowerUpText(pointMultiplier);
            }
        }

        UpdateUI();
    }

    void TryRecoverHealthFromCombo()
    {
        if (infiniteHealth)
        {
            currentHealth = maxHealth;
            return;
        }

        bool healthIsLow = currentHealth < lowHealthThreshold;
        bool inComboMode = pointMultiplier > 1;

        if (!healthIsLow || !inComboMode)
        {
            return;
        }

        float comboBasedHeal = baseComboHeal + combo * comboHealScale;
        float multiplierBoost = Mathf.Log(pointMultiplier, 2f);
        float totalHeal = comboBasedHeal + multiplierBoost;

        currentHealth += totalHeal;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    void HandleDebugShortcuts()
    {
        if (!enableInfiniteHealthShortcut || Keyboard.current == null)
        {
            return;
        }

        KeyControl shortcutKey = Keyboard.current[infiniteHealthShortcutKey];

        if (shortcutKey == null || !shortcutKey.wasPressedThisFrame)
        {
            return;
        }

        infiniteHealth = !infiniteHealth;

        if (infiniteHealth)
        {
            currentHealth = maxHealth;
        }

        UpdateUI();
        Debug.Log("Infinite health " + (infiniteHealth ? "enabled" : "disabled"));
    }

    void TriggerGameOver()
    {
        gameOver = true;
        currentHealth = 0f;
        UpdateUI();

        ClearActiveNotes();

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.AllOff();
        }

        if (uiFeedback != null)
        {
            uiFeedback.ShowGameOver(score);
        }

        Time.timeScale = 0f;
    }

    void ClearActiveNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i] != null)
            {
                Destroy(activeNotes[i].gameObject);
            }
        }

        activeNotes.Clear();
    }

    void SendTapCompleteForMask(int valveMask, bool perfect)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if ((valveMask & (1 << lane)) != 0)
            {
                SendTapComplete(lane, perfect);
            }
        }
    }

    void SendHoldStartForMask(int valveMask)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if ((valveMask & (1 << lane)) != 0)
            {
                SendHoldStart(lane);
            }
        }
    }

    void SendHoldCompleteForMask(int valveMask)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if ((valveMask & (1 << lane)) != 0)
            {
                SendHoldComplete(lane);
            }
        }
    }

    void SendMissForMask(int valveMask)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if ((valveMask & (1 << lane)) != 0)
            {
                SendMiss(lane);
            }
        }
    }

    void SendSolenoidPushForMask(int valveMask)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if ((valveMask & (1 << lane)) != 0)
            {
                SendSolenoidPush(lane);
            }
        }
    }

    void SendTapComplete(int lane, bool perfect)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.SendTapComplete(lane, perfect);
        }
    }

    void SendHoldStart(int lane)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.SendHoldStart(lane);
        }
    }

    void SendHoldComplete(int lane)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.SendHoldComplete(lane);
        }
    }

    void SendMiss(int lane)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            hapticFeedbackManager.SendMiss(lane);
        }
    }

    void SendSolenoidPush(int lane)
    {
        if (hapticFeedbackManager == null)
        {
            hapticFeedbackManager = HapticFeedbackManager.Instance;
        }

        if (hapticFeedbackManager != null)
        {
            EnsureSolenoidSettings();
            hapticFeedbackManager.SendSolenoidPush(lane, solenoidPushSettings[lane]);
        }
    }

    void EnsureSolenoidSettings()
    {
        if (solenoidPushSettings == null || solenoidPushSettings.Length != 3)
        {
            SolenoidPulseSettings[] resizedSettings = new SolenoidPulseSettings[3];

            for (int i = 0; i < resizedSettings.Length; i++)
            {
                if (solenoidPushSettings != null && i < solenoidPushSettings.Length)
                {
                    resizedSettings[i] = solenoidPushSettings[i];
                }

                if (resizedSettings[i] == null)
                {
                    resizedSettings[i] = new SolenoidPulseSettings();
                }
            }

            solenoidPushSettings = resizedSettings;
        }

        for (int i = 0; i < solenoidPushSettings.Length; i++)
        {
            if (solenoidPushSettings[i] == null)
            {
                solenoidPushSettings[i] = new SolenoidPulseSettings();
            }

            solenoidPushSettings[i].Clamp();
        }
    }

    void OnValidate()
    {
        EnsureSolenoidSettings();
    }

    void UpdateMultiplier()
    {
        pointMultiplier = CalculateMultiplier(combo);
    }

    int CalculateMultiplier(int currentCombo)
    {
        if (currentCombo < baseMultiplierComboThreshold)
        {
            return 1;
        }

        int threshold = baseMultiplierComboThreshold;
        int multiplier = 1;

        while (currentCombo >= threshold)
        {
            multiplier *= 2;
            threshold *= 2;

            if (multiplier >= maxPointMultiplier)
            {
                return maxPointMultiplier;
            }
        }

        return Mathf.Min(multiplier, maxPointMultiplier);
    }

    void ShowFeedback(string message, Color color)
    {
        if (uiFeedback != null)
        {
            uiFeedback.ShowFeedback(message, color);
        }
    }

    void UpdateUI()
    {
        if (uiFeedback != null)
        {
            uiFeedback.SetScore(score, pointMultiplier);
            uiFeedback.SetHealth(currentHealth / maxHealth);
            uiFeedback.ShowCombo(combo, pointMultiplier);
            uiFeedback.SetPowerMode(pointMultiplier);
        }
    }
}
