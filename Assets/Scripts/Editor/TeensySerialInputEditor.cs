using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TeensySerialInput))]
public class TeensySerialInputEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TeensySerialInput input = (TeensySerialInput)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "ToF MVP setup: leave valves released when Play starts. Auto calibration writes live rest distances, then sets press thresholds to rest - Press Enter Delta. Default 15 mm makes an 80 mm rest become a 65 mm press threshold.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Apply 80/65 Defaults"))
        {
            Undo.RecordObject(input, "Apply ToF 80/65 Defaults");
            input.valve1RestDistanceMM = 80f;
            input.valve2RestDistanceMM = 80f;
            input.valve3RestDistanceMM = 80f;
            input.pressEnterDeltaMM = 15f;
            input.releaseDeltaMM = 8f;
            input.valve1PressedDistanceMM = 65f;
            input.valve2PressedDistanceMM = 65f;
            input.valve3PressedDistanceMM = 65f;
            EditorUtility.SetDirty(input);
        }

        if (GUILayout.Button("MVP Auto-Cal On"))
        {
            Undo.RecordObject(input, "Enable MVP ToF Auto Calibration");
            input.autoCalibrateOnStart = true;
            input.pauseGameDuringCalibration = true;
            input.useDiscreteToFStates = true;
            input.useTeensyDebugPressBits = false;
            input.derivePressedStateFromDistance = false;
            EditorUtility.SetDirty(input);
        }

        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reconnect Teensy"))
            {
                input.Disconnect();
                input.Connect();
            }

            if (GUILayout.Button("Disconnect"))
            {
                input.Disconnect();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
