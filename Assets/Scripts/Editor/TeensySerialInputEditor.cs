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
            "ToF MVP setup: leave valves released when Play starts. Auto calibration waits for live D1/D2/D3 data, writes rest distances, then derives press/release thresholds from those live values. Keep raw Teensy V fallback off unless you are deliberately debugging THRESH.",
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
            input.acceptTeensyPressBitsAsFallback = false;
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

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Scan ToF Mux"))
            {
                input.SendTofScan();
            }

            if (GUILayout.Button("Send ToF Map"))
            {
                input.SendInspectorTofMap();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
