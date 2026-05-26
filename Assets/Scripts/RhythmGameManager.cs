using System.Collections.Generic;
using UnityEngine;

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

    [Header("combo multiplier")]
    public int combo = 0;
    public int baseMultiplierComboThreshold = 10;
    public int pointMultiplier = 1;
    public int maxPointMultiplier = 20;

    [Header("ui")]
    public GameplayUIFeedback uiFeedback;

    [Header("haptics")]
    public HapticFeedbackManager hapticFeedbackManager;

    private List<FlyingNote> activeNotes = new List<FlyingNote>();
    private bool[] previousValveStates = new bool[3];

    private int previousPointMultiplier = 1;
    private bool gameOver = false;

    void Start()
    {
        Time.timeScale = 1f;

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
        if (gameOver)
        {
            return;
        }

        for (int lane = 0; lane < 3; lane++)
        {
            bool pressed = ValveInputState.GetValve(lane);

            if (pressed && !previousValveStates[lane])
            {
                TryHit(lane);
            }

            previousValveStates[lane] = pressed;
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

    public void TryHit(int lane)
    {
        if (gameOver)
        {
            return;
        }

        FlyingNote bestNote = null;
        float bestTimingError = float.MaxValue;

        foreach (FlyingNote note in activeNotes)
        {
            if (note == null || note.resolved || note.holdStarted)
            {
                continue;
            }

            if (note.laneIndex != lane)
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
            return;
        }

        if (bestTimingError <= goodWindow)
        {
            bool perfect = bestTimingError <= perfectWindow;
            string rating = perfect ? "PERFECT!" : "GOOD!";

            if (bestNote.isHoldNote)
            {
                SendHoldStart(bestNote.laneIndex);
                bestNote.StartHold(rating);
                ShowFeedback(rating, perfect ? Color.green : Color.yellow);
            }
            else
            {
                SendTapComplete(bestNote.laneIndex, perfect);
                bestNote.ResolveTapHit(rating);
                RegisterSuccessfulNote(rating, perfect, true);
            }
        }
    }

    public void OnHoldCompleted(FlyingNote note)
    {
        if (gameOver)
        {
            return;
        }

        if (note != null)
        {
            SendHoldComplete(note.laneIndex);
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
            SendMiss(note.laneIndex);
        }

        UnregisterNote(note);

        currentHealth -= missDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

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

        if (currentHealth <= 0f)
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