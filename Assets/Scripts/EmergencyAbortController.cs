using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        abortTriggered = true;

        // Mark this run invalid so leaderboard / CSV saving is blocked.
        GameAbortState.MarkRunAborted();

        // Clear input state so no valve stays visually pressed.
        ValveInputState.ClearAll();

        // Clear selected UI object so Unity does not keep previewing destroyed UI.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

#if UNITY_EDITOR
        Selection.activeObject = null;
#endif

        // Always unpause before changing scenes.
        Time.timeScale = 1f;

        if (logAbort)
        {
            Debug.LogWarning("Emergency abort triggered. Sending hardware shutdown and returning home.");
        }

        // Turn off all Teensy outputs before changing scenes.
        HapticFeedbackManager haptics = HapticFeedbackManager.Instance;

        if (haptics != null)
        {
            haptics.EmergencyAllOff();
        }
        else if (TeensySerialInput.Instance != null)
        {
            TeensySerialInput.Instance.SendLine("X");
        }

        // Give the serial command time to transmit.
        yield return new WaitForSecondsRealtime(serialShutdownDelaySeconds);

        // Cleanly close serial before leaving MainScene.
        if (TeensySerialInput.Instance != null)
        {
            TeensySerialInput.Instance.Disconnect();
        }

        SceneManager.LoadScene(homeSceneName);
    }
}