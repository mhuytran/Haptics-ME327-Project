using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameplayUIFeedback : MonoBehaviour
{
    [Header("text")]
    public TMP_Text scoreText;
    public TMP_Text feedbackText;
    public TMP_Text comboText;
    public TMP_Text powerUpText;

    [Header("health")]
    public Slider healthBar;
    public Image healthFill;
    public TMP_Text healthText;

    [Header("health colors")]
    public float criticalHealthThreshold = 0.20f;
    public Color healthyColor = new Color(0.55f, 1.0f, 0.45f, 1.0f);
    public Color criticalRedBright = new Color(1.0f, 0.05f, 0.05f, 1.0f);
    public Color criticalRedDim = new Color(0.35f, 0.0f, 0.0f, 1.0f);
    public float criticalFlashSpeed = 6.0f;

    [Header("margin power overlay")]
    public Image powerTop;
    public Image powerBottom;
    public Image powerLeft;
    public Image powerRight;

    [Header("game over")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverScoreText;
    public GameOverLeaderboardSubmitter gameOverSubmitter;
    public string homeSceneName = "HomeScene";

    [Header("timing")]
    public float feedbackDuration = 0.45f;
    public float powerUpDuration = 1.1f;
    public float superSaiyanOverlayDuration = 5.0f;

    [Header("punch scale")]
    public float feedbackPunchScale = 1.35f;
    public float comboPunchScale = 1.20f;
    public float powerUpPunchScale = 1.45f;
    public float scaleReturnSpeed = 10.0f;

    [Header("margin aura")]
    public float powerPulseSpeed = 5.0f;
    public float powerBaseAlpha = 0.18f;
    public float powerPulseAlpha = 0.10f;
    public float maxPowerAlpha = 0.35f;

    private float feedbackClearTime = 0f;
    private float powerUpClearTime = 0f;
    private float overlayEndTime = 0f;

    private RectTransform feedbackRect;
    private RectTransform comboRect;
    private RectTransform powerUpRect;

    private Vector3 feedbackBaseScale = Vector3.one;
    private Vector3 comboBaseScale = Vector3.one;
    private Vector3 powerUpBaseScale = Vector3.one;

    private int activeMultiplier = 1;
    private float currentNormalizedHealth = 1f;

    void Start()
    {
        if (feedbackText != null)
        {
            feedbackRect = feedbackText.GetComponent<RectTransform>();
            feedbackBaseScale = feedbackRect.localScale;
            feedbackText.text = "";
        }

        if (comboText != null)
        {
            comboRect = comboText.GetComponent<RectTransform>();
            comboBaseScale = comboRect.localScale;
            comboText.text = "";
        }

        if (powerUpText != null)
        {
            powerUpRect = powerUpText.GetComponent<RectTransform>();
            powerUpBaseScale = powerUpRect.localScale;
            powerUpText.text = "";
        }

        if (healthBar != null && healthFill == null && healthBar.fillRect != null)
        {
            healthFill = healthBar.fillRect.GetComponent<Image>();
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        SetAllMarginRaycastTargets(false);
        SetMarginOverlayAlpha(0f);
        SetHealth(1f);
    }

    void Update()
    {
        if (feedbackText != null && Time.time >= feedbackClearTime)
        {
            feedbackText.text = "";
        }

        if (powerUpText != null && Time.time >= powerUpClearTime)
        {
            powerUpText.text = "";
        }

        SmoothScale(feedbackRect, feedbackBaseScale);
        SmoothScale(comboRect, comboBaseScale);
        SmoothScale(powerUpRect, powerUpBaseScale);

        UpdateHealthVisual();
        UpdatePowerOverlay();
    }

    void SmoothScale(RectTransform rect, Vector3 baseScale)
    {
        if (rect == null)
        {
            return;
        }

        rect.localScale = Vector3.Lerp(
            rect.localScale,
            baseScale,
            scaleReturnSpeed * Time.unscaledDeltaTime
        );
    }

    public void SetScore(int score, int multiplier)
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = "score: " + score;
    }

    public void SetHealth(float normalizedHealth)
    {
        currentNormalizedHealth = Mathf.Clamp01(normalizedHealth);

        if (healthBar != null)
        {
            healthBar.value = currentNormalizedHealth;
        }

        if (healthText != null)
        {
            healthText.text = Mathf.RoundToInt(currentNormalizedHealth * 100f) + "%";
        }

        UpdateHealthVisual();
    }

    void UpdateHealthVisual()
    {
        if (healthFill == null)
        {
            return;
        }

        if (currentNormalizedHealth <= criticalHealthThreshold)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * criticalFlashSpeed) + 1f) * 0.5f;
            healthFill.color = Color.Lerp(criticalRedDim, criticalRedBright, pulse);
        }
        else
        {
            healthFill.color = healthyColor;
        }
    }

    public void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null)
        {
            return;
        }

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackClearTime = Time.time + feedbackDuration;

        if (feedbackRect != null)
        {
            feedbackRect.localScale = feedbackBaseScale * feedbackPunchScale;
        }
    }

    public void ShowCombo(int combo, int multiplier)
    {
        if (comboText == null)
        {
            return;
        }

        if (combo < 3)
        {
            comboText.text = "";
            return;
        }

        if (multiplier >= 20)
        {
            comboText.text = combo + "-NOTE STREAK\n20x MAX POWER";
            comboText.color = new Color(1f, 0.95f, 0.1f);
        }
        else if (multiplier > 1)
        {
            comboText.text = combo + "-NOTE STREAK\n" + multiplier + "x SCORE MULTIPLIER";
            comboText.color = new Color(1f, 0.82f, 0.05f);
        }
        else
        {
            comboText.text = combo + "-NOTE STREAK";
            comboText.color = Color.yellow;
        }

        if (comboRect != null)
        {
            comboRect.localScale = comboBaseScale * comboPunchScale;
        }
    }

    public void ShowPowerUpText(int multiplier)
    {
        if (powerUpText == null)
        {
            return;
        }

        powerUpText.text = "POWER ON!";
        powerUpText.color = GetPowerColor(multiplier);
        powerUpClearTime = Time.time + powerUpDuration;

        if (powerUpRect != null)
        {
            powerUpRect.localScale = powerUpBaseScale * powerUpPunchScale;
        }
    }

    public void SetPowerMode(int multiplier)
    {
        activeMultiplier = multiplier;

        if (multiplier <= 1)
        {
            overlayEndTime = 0f;
            SetMarginOverlayAlpha(0f);
        }
    }

    public void ShowPowerBurst(int multiplier)
    {
        activeMultiplier = multiplier;
        overlayEndTime = Time.time + superSaiyanOverlayDuration;
    }

    public void ShowSmallFlash(Color color, float alpha)
    {
        ApplyMarginOverlayColor(color, alpha);
    }

    public void ShowGameOver(int finalScore)
    {
        SetPowerMode(1);
        SetMarginOverlayAlpha(0f);

        if (feedbackText != null)
        {
            feedbackText.text = "";
        }

        if (comboText != null)
        {
            comboText.text = "";
        }

        if (powerUpText != null)
        {
            powerUpText.text = "";
        }

        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = "Final Score: " + finalScore;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (gameOverSubmitter != null)
        {
            gameOverSubmitter.ResetGameOverFlow();
        }
    }

    // Keep these public methods only as fallback.
    // Prefer wiring Play Again / Home buttons to GameOverLeaderboardSubmitter
    // so the user must save their score before navigating.
    public void OnPlayAgainButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnHomeButtonPressed()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(homeSceneName))
        {
            SceneManager.LoadScene(homeSceneName);
        }
    }

    void UpdatePowerOverlay()
    {
        if (Time.time >= overlayEndTime || activeMultiplier <= 1)
        {
            SetMarginOverlayAlpha(0f);
            return;
        }

        float pulse = (Mathf.Sin(Time.time * powerPulseSpeed) + 1f) * 0.5f;
        float multiplierAlphaBoost = Mathf.Log(activeMultiplier, 2f) * 0.035f;

        float alpha = powerBaseAlpha + multiplierAlphaBoost + pulse * powerPulseAlpha;
        alpha = Mathf.Clamp(alpha, 0f, maxPowerAlpha);

        ApplyMarginOverlayColor(GetPowerColor(activeMultiplier), alpha);
    }

    void ApplyMarginOverlayColor(Color color, float alpha)
    {
        color.a = alpha;

        if (powerTop != null) powerTop.color = color;
        if (powerBottom != null) powerBottom.color = color;
        if (powerLeft != null) powerLeft.color = color;
        if (powerRight != null) powerRight.color = color;
    }

    void SetMarginOverlayAlpha(float alpha)
    {
        SetImageAlpha(powerTop, alpha);
        SetImageAlpha(powerBottom, alpha);
        SetImageAlpha(powerLeft, alpha);
        SetImageAlpha(powerRight, alpha);
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    void SetAllMarginRaycastTargets(bool value)
    {
        if (powerTop != null) powerTop.raycastTarget = value;
        if (powerBottom != null) powerBottom.raycastTarget = value;
        if (powerLeft != null) powerLeft.raycastTarget = value;
        if (powerRight != null) powerRight.raycastTarget = value;
    }

    Color GetPowerColor(int multiplier)
    {
        if (multiplier >= 20)
        {
            return new Color(1f, 1f, 0.15f, 1f);
        }

        if (multiplier >= 8)
        {
            return new Color(1f, 0.95f, 0.1f, 1f);
        }

        if (multiplier >= 4)
        {
            return new Color(1f, 0.65f, 0.05f, 1f);
        }

        return new Color(1f, 0.85f, 0.05f, 1f);
    }
}