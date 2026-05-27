using System.Collections;
using TMPro;
using UnityEngine;

public class GameStartController : MonoBehaviour
{
    [Header("references")]
    public RhythmGameManager rhythmGameManager;
    public NoteSpawner noteSpawner;
    public TMP_Text startCountdownText;

    [Header("startup timing")]
    public float hardwareCalibrationPauseSeconds = 1.5f;
    public float readyCountdownSeconds = 3.0f;

    [Header("behavior")]
    public bool startAutomatically = true;
    public bool showReadyCountdown = true;
    public bool spawnFirstNoteImmediatelyAfterCountdown = false;

    [Header("messages")]
    public string calibrationMessage = "CALIBRATING HARDWARE\nDO NOT TOUCH THE VALVES";
    public string readyMessage = "GET READY";
    public string goMessage = "GO!";

    private bool startupFinished = false;
    private Coroutine startupRoutine;

    void Start()
    {
        if (rhythmGameManager == null)
        {
            rhythmGameManager = FindAnyObjectByType<RhythmGameManager>();
        }

        if (noteSpawner == null)
        {
            noteSpawner = FindAnyObjectByType<NoteSpawner>();
        }

        if (startCountdownText != null)
        {
            startCountdownText.text = "";
            startCountdownText.gameObject.SetActive(false);
        }

        if (startAutomatically)
        {
            startupRoutine = StartCoroutine(StartGameAfterHardwareCalibration());
        }
    }

    public void StartStartupSequenceManually()
    {
        if (startupFinished)
        {
            return;
        }

        if (startupRoutine != null)
        {
            StopCoroutine(startupRoutine);
        }

        startupRoutine = StartCoroutine(StartGameAfterHardwareCalibration());
    }

    IEnumerator StartGameAfterHardwareCalibration()
    {
        startupFinished = false;

        Time.timeScale = 1f;

        DisableGameplayForStartup();

        yield return ShowCalibrationPause();

        if (showReadyCountdown)
        {
            yield return ShowReadyCountdown();
        }

        EnableGameplayAfterStartup();
    }

    void DisableGameplayForStartup()
    {
        if (rhythmGameManager != null)
        {
            rhythmGameManager.SetGameplayActive(false);
        }

        if (noteSpawner != null)
        {
            noteSpawner.SetSpawningEnabled(false);
        }

        ValveInputState.ClearAll();

        if (rhythmGameManager != null)
        {
            rhythmGameManager.ResetValveEdgeMemory();
        }
    }

    void EnableGameplayAfterStartup()
    {
        ValveInputState.ClearAll();

        if (rhythmGameManager != null)
        {
            rhythmGameManager.ResetValveEdgeMemory();
            rhythmGameManager.SetGameplayActive(true);
        }

        if (noteSpawner != null)
        {
            noteSpawner.SetSpawningEnabled(true, spawnFirstNoteImmediatelyAfterCountdown);
        }

        if (startCountdownText != null)
        {
            startCountdownText.text = "";
            startCountdownText.gameObject.SetActive(false);
        }

        startupFinished = true;
    }

    IEnumerator ShowCalibrationPause()
    {
        if (startCountdownText != null)
        {
            startCountdownText.gameObject.SetActive(true);
        }

        float remaining = hardwareCalibrationPauseSeconds;

        while (remaining > 0f)
        {
            if (startCountdownText != null)
            {
                startCountdownText.text =
                    calibrationMessage +
                    "\n" +
                    remaining.ToString("0.0") + " s";
            }

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator ShowReadyCountdown()
    {
        if (startCountdownText != null)
        {
            startCountdownText.gameObject.SetActive(true);
        }

        float remaining = readyCountdownSeconds;

        while (remaining > 0f)
        {
            if (startCountdownText != null)
            {
                int shownNumber = Mathf.CeilToInt(remaining);

                startCountdownText.text =
                    readyMessage +
                    "\n" +
                    shownNumber;
            }

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (startCountdownText != null)
        {
            startCountdownText.text = goMessage;
        }

        yield return new WaitForSecondsRealtime(0.35f);
    }
}