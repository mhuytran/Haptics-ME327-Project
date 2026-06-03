using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class EmergencyAbortController : MonoBehaviour
{
    [Header("scene")]
    public string homeSceneName = "HomeScene";

    [Header("timing")]
    public float serialShutdownDelaySeconds = 0.15f;

    [Header("debug")]
    public bool logAbort = true;

    private bool abortTriggered = false;

    void Update()
    {
        // Ctrl+Shift+X is reserved for an immediate safe exit from a hardware run.
        if (abortTriggered)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        bool ctrlPressed =
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed;

        bool shiftPressed =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        bool xPressedThisFrame = Keyboard.current.xKey.wasPressedThisFrame;

        if (ctrlPressed && shiftPressed && xPressedThisFrame)
        {
            StartCoroutine(EmergencyAbortRoutine());
        }
    }

    IEnumerator EmergencyAbortRoutine()
    {
        // Mark the run unsaveable, clear local state, shut off hardware, then return home.
        abortTriggered = true;

        // Prevent this run from being saved to leaderboard or CSV.
        GameAbortState.MarkRunAborted();

        // Clear local input state so valves do not remain visually pressed.
        ValveInputState.ClearAll();

        // Always unpause before leaving the scene so animations/UI do not stay frozen later.
        Time.timeScale = 1f;

        if (logAbort)
        {
            Debug.LogWarning("Emergency abort triggered. Sending hardware shutdown and returning home.");
        }

        // Send hardware all-off command before scene changes.
        HapticFeedbackManager haptics = HapticFeedbackManager.Instance;

        if (haptics != null)
        {
            haptics.EmergencyAllOff();
        }
        else if (TeensySerialInput.Instance != null)
        {
            TeensySerialInput.Instance.SendLine("X");
        }

        // Give the serial command a short real-time window to transmit before scene load.
        yield return new WaitForSecondsRealtime(serialShutdownDelaySeconds);

        // Disconnect cleanly if the serial manager exists.
        if (TeensySerialInput.Instance != null)
        {
            TeensySerialInput.Instance.Disconnect();
        }

        SceneManager.LoadScene(homeSceneName);
    }
}
