using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class DebuggingSceneManager : MonoBehaviour
{
    [Header("password")]
    public string requiredPassword = "CHANGE_THIS_IN_INSPECTOR";

    [Header("scenes")]
    public string homeSceneName = "HomeScene";

    [Header("ui references")]
    public TMP_InputField passwordInput;
    public Button unlockButton;
    public Button resetLeaderboardAndCsvButton;
    public Button homeButton;
    public TMP_Text statusText;

    private bool authenticated = false;
    private bool actionInProgress = false;

    void Start()
    {
        LockDebugControls();

        if (statusText != null)
        {
            statusText.text = "Enter team password";
            statusText.color = Color.white;
        }

        if (passwordInput != null)
        {
            passwordInput.text = "";
        }
    }

    public void TryUnlock()
    {
        if (actionInProgress)
        {
            return;
        }

        if (passwordInput == null)
        {
            return;
        }

        string typedPassword = passwordInput.text.Trim();

        if (typedPassword == requiredPassword)
        {
            authenticated = true;
            UnlockDebugControls();

            if (statusText != null)
            {
                statusText.text = "Access granted";
                statusText.color = new Color(0.2f, 1.0f, 0.35f, 1.0f);
            }

            passwordInput.interactable = false;
            SetButtonInteractable(unlockButton, false);
        }
        else
        {
            authenticated = false;
            LockDebugControls();

            if (statusText != null)
            {
                statusText.text = "Incorrect password";
                statusText.color = Color.red;
            }
        }
    }

    public void ResetLeaderboardAndStartNewCsv()
    {
        if (!authenticated || actionInProgress)
        {
            return;
        }

        actionInProgress = true;
        LockAllButtons();

        string archivedPath = LeaderboardStore.ResetLeaderboardAndStartNewCsv();

        if (statusText != null)
        {
            statusText.text =
                "Leaderboard reset\n" +
                "Old CSV archived:\n" +
                archivedPath + "\n" +
                "New CSV started at:\n" +
                LeaderboardStore.GetAttemptLogPath();

            statusText.color = new Color(0.2f, 1.0f, 0.35f, 1.0f);
        }

        actionInProgress = false;

        SetButtonInteractable(resetLeaderboardAndCsvButton, true);
        SetButtonInteractable(homeButton, true);
    }

    public void ReturnHome()
    {
        if (actionInProgress)
        {
            return;
        }

        actionInProgress = true;
        LockAllButtons();

        SceneManager.LoadScene(homeSceneName);
    }

    void LockDebugControls()
    {
        SetButtonInteractable(resetLeaderboardAndCsvButton, false);
        SetButtonInteractable(homeButton, true);
    }

    void UnlockDebugControls()
    {
        SetButtonInteractable(resetLeaderboardAndCsvButton, true);
        SetButtonInteractable(homeButton, true);
    }

    void LockAllButtons()
    {
        SetButtonInteractable(unlockButton, false);
        SetButtonInteractable(resetLeaderboardAndCsvButton, false);
        SetButtonInteractable(homeButton, false);
    }

    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(true);
        button.interactable = interactable;

        ButtonHoverGlow hoverGlow = button.GetComponent<ButtonHoverGlow>();

        if (hoverGlow != null)
        {
            hoverGlow.ForceVisualRefresh();
        }
    }
}