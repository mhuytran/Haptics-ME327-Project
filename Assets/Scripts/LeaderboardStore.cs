using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public int score;

    public LeaderboardEntry(string playerName, int score)
    {
        this.playerName = playerName;
        this.score = score;
    }
}

[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}

public static class LeaderboardStore
{
    private const string LeaderboardKey = "TrumpalSimulatorLeaderboard";

    // visible leaderboard only keeps the top 10 unique players
    private const int MaxEntries = 10;

    private const string AttemptLogFolderPath = @"C:\Users\huyjo\Folder\Documents\Haptics Game CSV Data";
    private const string AttemptLogFileName = "trumpal_game_attempts.csv";

    public static LeaderboardData Load()
    {
        // PlayerPrefs stores the visible top-score table as JSON.
        string json = PlayerPrefs.GetString(LeaderboardKey, "");

        if (string.IsNullOrEmpty(json))
        {
            return new LeaderboardData();
        }

        LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);

        if (data == null || data.entries == null)
        {
            return new LeaderboardData();
        }

        return data;
    }

    public static bool AddScore(string playerName, int score)
    {
        // Every attempt goes to CSV, while the visible leaderboard keeps each player's best score.
        string cleanName = SanitizeName(playerName);

        // every game attempt is always written to csv
        AppendAttemptToCsv(cleanName, score);

        // leaderboard stores only each player's best score
        LeaderboardData data = Load();
        LeaderboardEntry existingEntry = FindEntryByName(data, cleanName);

        bool isNewBestScore = false;

        if (existingEntry == null)
        {
            data.entries.Add(new LeaderboardEntry(cleanName, score));
            isNewBestScore = true;
        }
        else if (score > existingEntry.score)
        {
            existingEntry.score = score;
            isNewBestScore = true;
        }

        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        // enforce top 10 visible leaderboard limit
        while (data.entries.Count > MaxEntries)
        {
            data.entries.RemoveAt(data.entries.Count - 1);
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(LeaderboardKey, json);
        PlayerPrefs.Save();

        return isNewBestScore;
    }

    public static string GetFormattedLeaderboard()
    {
        LeaderboardData data = Load();

        if (data.entries.Count == 0)
        {
            return "No scores yet";
        }

        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        string output = "";

        int displayCount = Mathf.Min(data.entries.Count, MaxEntries);

        for (int i = 0; i < displayCount; i++)
        {
            LeaderboardEntry entry = data.entries[i];
            output += (i + 1) + ". " + entry.playerName + " — " + entry.score;

            if (i < displayCount - 1)
            {
                output += "\n";
            }
        }

        return output;
    }

    public static string GetAttemptLogPath()
    {
        return Path.Combine(AttemptLogFolderPath, AttemptLogFileName);
    }

    public static string ResetLeaderboardAndStartNewCsv()
    {
        // Debug reset archives the current CSV before creating a fresh attempt log.
        ClearLeaderboard();
        string archivedPath = ArchiveCurrentAttemptLog();
        CreateFreshAttemptLogFile();

        return archivedPath;
    }

    public static void ClearLeaderboard()
    {
        PlayerPrefs.DeleteKey(LeaderboardKey);
        PlayerPrefs.Save();
    }

    public static void ClearAttemptLog()
    {
        string path = GetAttemptLogPath();

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    static string ArchiveCurrentAttemptLog()
    {
        EnsureLogFolderExists();

        string currentPath = GetAttemptLogPath();

        if (!File.Exists(currentPath))
        {
            return "No previous CSV existed";
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string archiveFileName = "trumpal_game_attempts_archive_" + timestamp + ".csv";
        string archivePath = Path.Combine(AttemptLogFolderPath, archiveFileName);

        File.Move(currentPath, archivePath);

        Debug.Log("old csv archived to: " + archivePath);

        return archivePath;
    }

    static void CreateFreshAttemptLogFile()
    {
        EnsureLogFolderExists();

        string path = GetAttemptLogPath();

        using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
        {
            writer.WriteLine("utc_timestamp,local_timestamp,player_name,score");
        }

        Debug.Log("new csv created at: " + path);
    }

    static void AppendAttemptToCsv(string playerName, int score)
    {
        // Append timestamps plus score so raw attempts are preserved beyond the top 10 list.
        EnsureLogFolderExists();

        string path = GetAttemptLogPath();
        bool fileExists = File.Exists(path);

        using (StreamWriter writer = new StreamWriter(path, true, Encoding.UTF8))
        {
            if (!fileExists)
            {
                writer.WriteLine("utc_timestamp,local_timestamp,player_name,score");
            }

            string utcTimestamp = DateTime.UtcNow.ToString("o");
            string localTimestamp = DateTime.Now.ToString("o");

            writer.WriteLine(
                EscapeCsv(utcTimestamp) + "," +
                EscapeCsv(localTimestamp) + "," +
                EscapeCsv(playerName) + "," +
                score
            );
        }

        Debug.Log("attempt saved to: " + path);
    }

    static void EnsureLogFolderExists()
    {
        if (!Directory.Exists(AttemptLogFolderPath))
        {
            Directory.CreateDirectory(AttemptLogFolderPath);
        }
    }

    static LeaderboardEntry FindEntryByName(LeaderboardData data, string playerName)
    {
        foreach (LeaderboardEntry entry in data.entries)
        {
            if (string.Equals(entry.playerName, playerName, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    static string SanitizeName(string playerName)
    {
        // Keep names short and non-empty for the home-screen leaderboard rows.
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return "Player";
        }

        playerName = playerName.Trim();

        if (playerName.Length > 12)
        {
            playerName = playerName.Substring(0, 12);
        }

        return playerName;
    }

    static string EscapeCsv(string value)
    {
        // Quote fields only when CSV syntax requires it.
        if (value == null)
        {
            return "";
        }

        bool mustQuote = value.Contains(",") ||
                         value.Contains("\"") ||
                         value.Contains("\n") ||
                         value.Contains("\r");

        value = value.Replace("\"", "\"\"");

        return mustQuote ? "\"" + value + "\"" : value;
    }
}
