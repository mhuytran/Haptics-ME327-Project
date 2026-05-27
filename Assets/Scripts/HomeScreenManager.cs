using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class HomeScreenManager : MonoBehaviour
{
    [Header("scenes")]
    public string gameSceneName = "MainScene";
    public string debuggingSceneName = "DebuggingScene";

    [Header("legacy single-text leaderboard")]
    public TMP_Text leaderboardText;

    [Header("row-based leaderboard")]
    public TMP_Text[] leaderboardRows;

    [Header("normal row style")]
    public Color normalRowColor = Color.white;

    [Header("top 3 medal colors")]
    public Color goldBase = new Color(1.0f, 0.68f, 0.08f, 1.0f);
    public Color goldShine = new Color(1.0f, 0.95f, 0.35f, 1.0f);
    public Color goldGlow = new Color(1.0f, 0.72f, 0.05f, 1.0f);

    public Color silverBase = new Color(0.72f, 0.78f, 0.85f, 1.0f);
    public Color silverShine = new Color(0.95f, 0.98f, 1.0f, 1.0f);
    public Color silverGlow = new Color(0.65f, 0.80f, 1.0f, 1.0f);

    public Color bronzeBase = new Color(0.72f, 0.38f, 0.14f, 1.0f);
    public Color bronzeShine = new Color(1.0f, 0.68f, 0.28f, 1.0f);
    public Color bronzeGlow = new Color(1.0f, 0.48f, 0.12f, 1.0f);

    void Start()
    {
        Time.timeScale = 1f;
        RefreshLeaderboard();
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        GameAbortState.ResetForNewRun();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenDebuggingScene()
    {
        SceneManager.LoadScene(debuggingSceneName);
    }

    public void RefreshLeaderboard()
    {
        LeaderboardData data = LeaderboardStore.Load();

        if (data.entries == null)
        {
            data.entries = new List<LeaderboardEntry>();
        }

        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        if (leaderboardRows != null && leaderboardRows.Length > 0)
        {
            RenderRowBasedLeaderboard(data);
        }
        else if (leaderboardText != null)
        {
            leaderboardText.text = LeaderboardStore.GetFormattedLeaderboard();
        }
    }

    void RenderRowBasedLeaderboard(LeaderboardData data)
    {
        int maxRows = Mathf.Min(leaderboardRows.Length, 10);

        for (int i = 0; i < maxRows; i++)
        {
            TMP_Text row = leaderboardRows[i];

            if (row == null)
            {
                continue;
            }

            LeaderboardRowShine shine = row.GetComponent<LeaderboardRowShine>();

            if (shine == null)
            {
                shine = row.gameObject.AddComponent<LeaderboardRowShine>();
            }

            if (i < data.entries.Count)
            {
                LeaderboardEntry entry = data.entries[i];

                row.gameObject.SetActive(true);
                row.text = (i + 1) + ". " + entry.playerName + " — " + entry.score;

                ApplyRankStyle(i, shine);
            }
            else
            {
                row.text = "";
                shine.SetNormalStyle(normalRowColor);
            }
        }
    }

    void ApplyRankStyle(int index, LeaderboardRowShine shine)
    {
        if (shine == null)
        {
            return;
        }

        if (index == 0)
        {
            shine.SetMedalStyle(goldBase, goldShine, goldGlow);
        }
        else if (index == 1)
        {
            shine.SetMedalStyle(silverBase, silverShine, silverGlow);
        }
        else if (index == 2)
        {
            shine.SetMedalStyle(bronzeBase, bronzeShine, bronzeGlow);
        }
        else
        {
            shine.SetNormalStyle(normalRowColor);
        }
    }
}