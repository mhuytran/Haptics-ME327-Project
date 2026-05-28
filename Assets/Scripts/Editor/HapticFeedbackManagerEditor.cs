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
            "MVP commands are X, PRECUE, TAPCOMPLETE, HOLDSTART, HOLDCOMPLETE, and MISS. Bench tuning commands are SOL, SOLRAMP, ERM, ERMRAMP, TEST, TESTCH, and THRESH. Team TEST profile: ERM 1000ms quadratic ramp, 100ms hold, duty 1.00, 1000ms delay, solenoid 800ms quadratic ramp, 350ms hold, duty 1.00.",
            MessageType.Info
        );

        if (GUILayout.Button("Apply Team 12V Feel Defaults"))
        {
            Undo.RecordObject(manager, "Apply Team 12V Feel Defaults");
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
