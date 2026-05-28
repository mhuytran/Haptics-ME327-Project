using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NoteSpawner))]
public class NoteSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NoteSpawner spawner = (NoteSpawner)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Use Spawn Mode = RandomDebug for generated fingering practice. Use Spawn Mode = SongChart for From-The-Start audio/chart sync. In Play mode, Space starts/pauses and R restarts the song chart.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Random Debug Mode"))
        {
            Undo.RecordObject(spawner, "Set Random Debug Mode");
            spawner.spawnMode = NoteSpawner.NoteSpawnMode.RandomDebug;
            EditorUtility.SetDirty(spawner);
        }

        if (GUILayout.Button("Song Chart Mode"))
        {
            Undo.RecordObject(spawner, "Set Song Chart Mode");
            spawner.spawnMode = NoteSpawner.NoteSpawnMode.SongChart;
            EditorUtility.SetDirty(spawner);
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
        }
    }
}
