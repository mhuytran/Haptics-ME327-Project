using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HapticFeedbackManager))]
public class HapticFeedbackManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HapticFeedbackManager manager = (HapticFeedbackManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Gameplay defaults are fast and direct: time-based ERM pre-cue, full duty, immediate solenoid push-off. Bench tuning commands remain available: SOL, SOLRAMP, ERM, ERMRAMP, TEST, TESTCH, and THRESH.",
            MessageType.Info
        );

        if (GUILayout.Button("Apply Game-Ready Haptic Defaults"))
        {
            Undo.RecordObject(manager, "Apply Game-Ready Haptic Defaults");
            manager.ApplyTeam12VFeelDefaults();
            EditorUtility.SetDirty(manager);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Pulse Solenoid"))
        {
            manager.TunePulseSelectedSolenoid();
        }

        if (GUILayout.Button("Ramp Solenoid"))
        {
            manager.TuneRampSelectedSolenoid();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Ramp ERM"))
        {
            manager.TuneRampSelectedErm();
        }

        if (GUILayout.Button("Pulse ERM"))
        {
            manager.TunePulseSelectedErm();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Team TEST Seq"))
        {
            manager.TuneTeamTestSequence();
        }

        if (GUILayout.Button("Send ToF Threshold"))
        {
            manager.TuneSendRawTofThreshold();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("All Off"))
        {
            manager.AllOff();
        }

        EditorGUILayout.EndHorizontal();
    }
}
