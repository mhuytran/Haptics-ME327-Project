using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class EmergencyAbortController : MonoBehaviour
{
    [Header("scene")]
    public string homeSceneName = "HomeScene";

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

        bool xPressedThisFrame =
            Keyboard.current.xKey.wasPressedThisFrame;

        if (ctrlPressed && shiftPressed && xPressedThisFrame)
        {
            EmergencyAbort();
        }
    }

    public void EmergencyAbort()
    {
        abortTriggered = true;

        // mark the current run as invalid so no score can be saved
        GameAbortState.MarkRunAborted();

        // unpause if game was paused at game over
        Time.timeScale = 1f;

        if (logAbort)
        {
            Debug.LogWarning("Emergency abort triggered. Current run invalidated. Returning to home screen.");
        }

        SceneManager.LoadScene(homeSceneName);
    }
}