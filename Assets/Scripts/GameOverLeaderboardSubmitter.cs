using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverLeaderboardSubmitter : MonoBehaviour
{
    [Header("references")]
    public RhythmGameManager rhythmGameManager;
    public TMP_InputField playerNameInput;
    public Button saveScoreButton;
    public Button playAgainButton;
    public Button homeButton;

    [Header("scene")]
    public string homeSceneName = "HomeScene";

    private bool scoreSaved = false;
    private bool transitionStarted = false;

    void Awake()
    {
        if (rhythmGameManager == null)
        {
            rhythmGameManager = FindAnyObjectByType<RhythmGameManager>();
        }
    }

    void OnDisable()
    {
        if (playerNameInput != null)
        {
            playerNameInput.onValueChanged.RemoveListener(OnNameChanged);
        }
    }

    public void ResetGameOverFlow()
    {
        scoreSaved = false;
        transitionStarted = false;

        if (playerNameInput != null)
        {
            playerNameInput.gameObject.SetActive(true);
            playerNameInput.interactable = true;
            playerNameInput.text = "";

            playerNameInput.onValueChanged.RemoveListener(OnNameChanged);
            playerNameInput.onValueChanged.AddListener(OnNameChanged);
        }

        ForceButtonVisible(saveScoreButton);
        ForceButtonVisible(playAgainButton);
        ForceButtonVisible(homeButton);

        SetButtonInteractable(saveScoreButton, false);
        SetButtonInteractable(playAgainButton, false);
        SetButtonInteractable(homeButton, false);

        UpdateSaveButtonState();
    }

    void OnNameChanged(string newName)
    {
        UpdateSaveButtonState();
    }

    void UpdateSaveButtonState()
    {
        bool canSave =
            HasValidName() &&
            !scoreSaved &&
            !transitionStarted &&
            !GameAbortState.CurrentRunAborted;

        SetButtonInteractable(saveScoreButton, canSave);
    }

    bool HasValidName()
    {
        if (playerNameInput == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(playerNameInput.text);
    }

    public void SubmitScore()
    {
        if (scoreSaved || transitionStarted || !HasValidName())
        {
            return;
        }

        // emergency-aborted runs are never saved
        if (GameAbortState.CurrentRunAborted)
        {
            return;
        }

        if (rhythmGameManager == null)
        {
            rhythmGameManager = FindAnyObjectByType<RhythmGameManager>();
        }

        if (rhythmGameManager == null)
        {
            return;
        }

        string playerName = playerNameInput.text.Trim();
        int finalScore = rhythmGameManager.score;

        LeaderboardStore.AddScore(playerName, finalScore);

        scoreSaved = true;

        if (playerNameInput != null)
        {
            playerNameInput.interactable = false;
        }

        SetButtonInteractable(saveScoreButton, false);
        SetButtonInteractable(playAgainButton, true);
        SetButtonInteractable(homeButton, true);
    }

    public void OnPlayAgainButtonPressed()
    {
        if (!scoreSaved || transitionStarted)
        {
            return;
        }

        transitionStarted = true;
        LockAllControls();

        Time.timeScale = 1f;
        GameAbortState.ResetForNewRun();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnHomeButtonPressed()
    {
        if (!scoreSaved || transitionStarted)
        {
            return;
        }

        transitionStarted = true;
        LockAllControls();

        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(homeSceneName))
        {
            SceneManager.LoadScene(homeSceneName);
        }
    }

    void LockAllControls()
    {
        SetButtonInteractable(saveScoreButton, false);
        SetButtonInteractable(playAgainButton, false);
        SetButtonInteractable(homeButton, false);

        if (playerNameInput != null)
        {
            playerNameInput.interactable = false;
        }
    }

    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
        {
            return;
        }

        ForceButtonVisible(button);

        button.interactable = interactable;

        ButtonHoverGlow hoverGlow = button.GetComponent<ButtonHoverGlow>();

        if (hoverGlow != null)
        {
            hoverGlow.ForceVisualRefresh();
        }
    }

    void ForceButtonVisible(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(true);

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Image buttonImage = button.GetComponent<Image>();

        if (buttonImage != null)
        {
            Color imageColor = buttonImage.color;
            imageColor.a = 1f;
            buttonImage.color = imageColor;
        }

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);

        if (buttonText != null)
        {
            buttonText.gameObject.SetActive(true);

            Color textColor = buttonText.color;
            textColor.a = 1f;
            buttonText.color = textColor;
        }

        RectTransform rect = button.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }
    }
}