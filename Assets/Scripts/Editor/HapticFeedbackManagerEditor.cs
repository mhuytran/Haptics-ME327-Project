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
            "MVP commands are X, PRECUE, TAPCOMPLETE, HOLDSTART, HOLDCOMPLETE, and MISS. Bench tuning commands are SOL, ERM, ERMRAMP, and THRESH. Set Command Profile to MvpOnly when tuning is finished.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Pulse Solenoid"))
        {
            manager.TunePulseSelectedSolenoid();
        }

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

        if (GUILayout.Button("Send ToF Threshold"))
        {
            manager.TuneSendRawTofThreshold();
        }

        if (GUILayout.Button("All Off"))
        {
            manager.AllOff();
        }

        EditorGUILayout.EndHorizontal();
    }
}
