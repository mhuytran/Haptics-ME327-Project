using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NoteSpawner))]
public class NoteSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Add play-mode chart controls and quick spawn-mode switches.
        DrawDefaultInspector();

        NoteSpawner spawner = (NoteSpawner)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Use RandomDebug for generated fingering practice and SongChart for From-The-Start audio/chart sync. In Play mode: F9 toggles modes, F10 forces RandomDebug, F11 forces SongChart, Space starts/pauses, R restarts, [ and ] nudge chart timing.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Random Debug Mode"))
        {
            SetSpawnerMode(spawner, NoteSpawner.NoteSpawnMode.RandomDebug);
        }

        if (GUILayout.Button("Song Chart Mode"))
        {
            SetSpawnerMode(spawner, NoteSpawner.NoteSpawnMode.SongChart);
        }

        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Start Song"))
            {
                spawner.StartSong();
            }

            if (GUILayout.Button("Pause"))
            {
                spawner.PauseSong();
            }

            if (GUILayout.Button("Restart"))
            {
                spawner.RestartSong();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Toggle Mode"))
            {
                spawner.ToggleSpawnMode();
            }

            if (GUILayout.Button("Chart Earlier"))
            {
                spawner.NudgeChartEarlier();
            }

            if (GUILayout.Button("Chart Later"))
            {
                spawner.NudgeChartLater();
            }

            if (GUILayout.Button("Reset Offset"))
            {
                spawner.ResetChartOffset();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    void SetSpawnerMode(NoteSpawner spawner, NoteSpawner.NoteSpawnMode mode)
    {
        // Use the runtime API in Play mode so mode-change cleanup still runs.
        Undo.RecordObject(spawner, "Set Note Spawn Mode");

        if (Application.isPlaying)
        {
            spawner.SetSpawnMode(mode);
        }
        else
        {
            spawner.spawnMode = mode;
        }

        EditorUtility.SetDirty(spawner);
    }
}
