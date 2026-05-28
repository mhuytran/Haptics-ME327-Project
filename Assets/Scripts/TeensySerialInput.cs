using System;
using System.Collections;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class TeensySerialInput : MonoBehaviour
{
    public static TeensySerialInput Instance { get; private set; }

    [Header("Serial connection")]
    [Tooltip("Fallback COM port. If Auto Detect Port is on, this is tried first and then Unity tries every detected serial port.")]
    public string portName = "COM3";
    [Tooltip("Must match Serial.begin(...) in trumpal_teensy_code.ino.")]
    public int baudRate = 115200;
    [Tooltip("Connect to the Teensy automatically when the scene starts.")]
    public bool connectOnStart = true;
    [Tooltip("Try the configured port first, then scan other COM ports. Leave on for plug-and-play.")]
    public bool autoDetectPort = true;
    [Tooltip("Keep retrying if the Teensy is plugged in after Play starts or briefly disconnects.")]
    public bool reconnectWhenDisconnected = true;
    public float reconnectIntervalSeconds = 2f;

    [Header("Teensy pinout from trumpal_teensy_code")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("ToF calibration, mm")]
    [TextArea(4, 8)]
    public string tofCalibrationGuide =
        "Plug in with all valves released. Auto calibration samples the live ToF rest distance, usually near 80 mm. Pressed threshold = rest - Press Enter Delta, so 80 and 15 gives a 65 mm press threshold. If presses do not register, lower Press Enter Delta. If idle valves falsely press, raise it.";

    [Tooltip("Auto-filled at startup from D1. Expected bench value is around 80 mm with valve 1 released.")]
    public float valve1RestDistanceMM = 80f;
    [Tooltip("Auto-filled at startup as rest minus Press Enter Delta. Expected bench value is around 65 mm.")]
    public float valve1PressedDistanceMM = 65f;

    [Tooltip("Auto-filled at startup from D2. Expected bench value is around 80 mm with valve 2 released.")]
    public float valve2RestDistanceMM = 80f;
    [Tooltip("Auto-filled at startup as rest minus Press Enter Delta. Expected bench value is around 65 mm.")]
    public float valve2PressedDistanceMM = 65f;

    [Tooltip("Auto-filled at startup from D3. Expected bench value is around 80 mm with valve 3 released.")]
    public float valve3RestDistanceMM = 80f;
    [Tooltip("Auto-filled at startup as rest minus Press Enter Delta. Expected bench value is around 65 mm.")]
    public float valve3PressedDistanceMM = 65f;

    [Header("ToF auto calibration")]
    [Tooltip("Leave on for MVP. At Play start the game pauses, samples released valves, then writes the rest/pressed mm fields above.")]
    public bool autoCalibrateOnStart = true;
    [Tooltip("Pause gameplay during calibration so the first notes do not move while ToF is finding rest distances.")]
    public bool pauseGameDuringCalibration = true;
    public float startupCalibrationPauseSeconds = 1.5f;
    [Tooltip("Main ToF tuning value. Pressed threshold = calibrated rest - this value. 15 mm turns an 80 mm rest into a 65 mm press threshold.")]
    public float pressEnterDeltaMM = 15f;
    [Tooltip("Release hysteresis. Released again when distance rises above rest - this value. 8 mm gives release around 72 mm when rest is 80 mm.")]
    public float releaseDeltaMM = 8f;
    [Tooltip("MVP mode: Unity only cares about pressed or released, using the calibrated ToF threshold.")]
    public bool useDiscreteToFStates = true;
    [Tooltip("Debug only. Uses Teensy's raw V1/V2/V3 bits instead of Unity's calibrated ToF states.")]
    public bool useTeensyDebugPressBits = false;
    [Tooltip("Recommended for bench testing. If Unity calibration is wrong but Teensy's raw V1/V2/V3 says pressed, still count the valve as pressed.")]
    public bool acceptTeensyPressBitsAsFallback = true;
    [Tooltip("Read-only runtime status.")]
    public bool isCalibrated = false;
    [Tooltip("Read-only runtime status.")]
    public bool isCalibrating = false;

    [Header("legacy press threshold")]
    public bool derivePressedStateFromDistance = false;
    [Range(0f, 1f)]
    public float pressAmountThreshold = 0.65f;

    [Header("debug")]
    public bool printIncomingLines = false;
    public bool printOutgoingCommands = true;

    [Header("Live ToF readout")]
    [TextArea(4, 8)]
    public string liveToFReadout = "Waiting for Teensy data.";
    public int liveValve1DistanceMM = 255;
    public int liveValve2DistanceMM = 255;
    public int liveValve3DistanceMM = 255;
    public bool liveValve1Pressed = false;
    public bool liveValve2Pressed = false;
    public bool liveValve3Pressed = false;
    [Range(0f, 1f)]
    public float liveValve1Amount = 0f;
    [Range(0f, 1f)]
    public float liveValve2Amount = 0f;
    [Range(0f, 1f)]
    public float liveValve3Amount = 0f;

    private SerialPort serialPort;
    private Thread readThread;
    private volatile bool keepReading = false;
    private float nextReconnectTime = 0f;

    private readonly object writeLock = new object();
    private readonly object stateLock = new object();

    private bool latestV1 = false;
    private bool latestV2 = false;
    private bool latestV3 = false;

    private int latestD1 = 255;
    private int latestD2 = 255;
    private int latestD3 = 255;

    private float latestS1 = 0f;
    private float latestS2 = 0f;
    private float latestS3 = 0f;

    private float latestE1 = 0f;
    private float latestE2 = 0f;
    private float latestE3 = 0f;

    private bool[] calibratedPressedStates = new bool[3];
    private bool legacyTesterWarningShown = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (connectOnStart)
        {
            Connect();
        }

        if (autoCalibrateOnStart)
        {
            StartCoroutine(AutoCalibrateStartup());
        }
    }

    void Update()
    {
        TryReconnectIfNeeded();

        bool v1;
        bool v2;
        bool v3;

        int d1;
        int d2;
        int d3;

        float s1;
        float s2;
        float s3;

        float e1;
        float e2;
        float e3;

        lock (stateLock)
        {
            v1 = latestV1;
            v2 = latestV2;
            v3 = latestV3;

            d1 = latestD1;
            d2 = latestD2;
            d3 = latestD3;

            s1 = latestS1;
            s2 = latestS2;
            s3 = latestS3;

            e1 = latestE1;
            e2 = latestE2;
            e3 = latestE3;
        }

        float a1 = DistanceToPressAmount(d1, valve1RestDistanceMM, valve1PressedDistanceMM);
        float a2 = DistanceToPressAmount(d2, valve2RestDistanceMM, valve2PressedDistanceMM);
        float a3 = DistanceToPressAmount(d3, valve3RestDistanceMM, valve3PressedDistanceMM);

        if (isCalibrating)
        {
            v1 = false;
            v2 = false;
            v3 = false;

            a1 = 0f;
            a2 = 0f;
            a3 = 0f;
        }
        else if (useDiscreteToFStates && isCalibrated)
        {
            bool rawV1 = v1;
            bool rawV2 = v2;
            bool rawV3 = v3;

            v1 = GetDiscretePressedState(0, d1);
            v2 = GetDiscretePressedState(1, d2);
            v3 = GetDiscretePressedState(2, d3);

            if (acceptTeensyPressBitsAsFallback)
            {
                v1 = v1 || rawV1;
                v2 = v2 || rawV2;
                v3 = v3 || rawV3;
            }

            a1 = v1 ? 1f : 0f;
            a2 = v2 ? 1f : 0f;
            a3 = v3 ? 1f : 0f;
        }
        else if (derivePressedStateFromDistance)
        {
            v1 = a1 >= pressAmountThreshold || (useTeensyDebugPressBits && v1);
            v2 = a2 >= pressAmountThreshold || (useTeensyDebugPressBits && v2);
            v3 = a3 >= pressAmountThreshold || (useTeensyDebugPressBits && v3);
        }
        else if (!useTeensyDebugPressBits)
        {
            if (!acceptTeensyPressBitsAsFallback)
            {
                v1 = false;
                v2 = false;
                v3 = false;
            }

            a1 = v1 ? 1f : a1;
            a2 = v2 ? 1f : a2;
            a3 = v3 ? 1f : a3;
        }

        ApplyValveStateFromTeensyChannel(1, v1, a1, d1, s1, e1);
        ApplyValveStateFromTeensyChannel(2, v2, a2, d2, s2, e2);
        ApplyValveStateFromTeensyChannel(3, v3, a3, d3, s3, e3);
        UpdateLiveToFReadout(d1, d2, d3, v1, v2, v3, a1, a2, a3);
    }

    IEnumerator AutoCalibrateStartup()
    {
        isCalibrating = true;
        isCalibrated = false;
        ValveInputState.ClearAll();

        float previousTimeScale = Time.timeScale;

        if (pauseGameDuringCalibration)
        {
            Time.timeScale = 0f;
        }

        float[] sums = new float[3];
        int[] sampleCounts = new int[3];
        float endTime = Time.realtimeSinceStartup + Mathf.Max(0.1f, startupCalibrationPauseSeconds);

        while (Time.realtimeSinceStartup < endTime)
        {
            if (pauseGameDuringCalibration)
            {
                Time.timeScale = 0f;
            }

            int d1;
            int d2;
            int d3;

            lock (stateLock)
            {
                d1 = latestD1;
                d2 = latestD2;
                d3 = latestD3;
            }

            AddCalibrationSample(0, d1, sums, sampleCounts);
            AddCalibrationSample(1, d2, sums, sampleCounts);
            AddCalibrationSample(2, d3, sums, sampleCounts);

            yield return null;
        }

        ApplyCalibrationSamples(sums, sampleCounts);

        for (int i = 0; i < calibratedPressedStates.Length; i++)
        {
            calibratedPressedStates[i] = false;
        }

        ValveInputState.ClearAll();
        isCalibrated = true;
        isCalibrating = false;

        if (pauseGameDuringCalibration)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }

        Debug.Log(
            "ToF auto-calibrated rest distances: " +
            valve1RestDistanceMM.ToString("0.0") + "mm, " +
            valve2RestDistanceMM.ToString("0.0") + "mm, " +
            valve3RestDistanceMM.ToString("0.0") + "mm"
        );
    }

    void AddCalibrationSample(int laneIndex, int distanceMM, float[] sums, int[] sampleCounts)
    {
        if (!IsValidDistance(distanceMM))
        {
            return;
        }

        sums[laneIndex] += distanceMM;
        sampleCounts[laneIndex] += 1;
    }

    void ApplyCalibrationSamples(float[] sums, int[] sampleCounts)
    {
        valve1RestDistanceMM = GetCalibratedRestDistance(0, sums, sampleCounts, valve1RestDistanceMM);
        valve2RestDistanceMM = GetCalibratedRestDistance(1, sums, sampleCounts, valve2RestDistanceMM);
        valve3RestDistanceMM = GetCalibratedRestDistance(2, sums, sampleCounts, valve3RestDistanceMM);

        ApplyPressedThresholdsFromCalibratedRest();
    }

    float GetCalibratedRestDistance(
        int laneIndex,
        float[] sums,
        int[] sampleCounts,
        float fallbackRestDistanceMM
    )
    {
        if (sampleCounts[laneIndex] <= 0)
        {
            return fallbackRestDistanceMM;
        }

        return sums[laneIndex] / sampleCounts[laneIndex];
    }

    bool GetDiscretePressedState(int laneIndex, int distanceMM)
    {
        if (!IsValidDistance(distanceMM))
        {
            return calibratedPressedStates[laneIndex];
        }

        float enterPressedDistanceMM = GetPressEnterDistance(laneIndex);
        float exitPressedDistanceMM = GetReleaseDistance(laneIndex);
        bool wasPressed = calibratedPressedStates[laneIndex];
        bool pressed = wasPressed
            ? distanceMM <= exitPressedDistanceMM
            : distanceMM <= enterPressedDistanceMM;

        calibratedPressedStates[laneIndex] = pressed;
        return pressed;
    }

    float GetRestDistance(int laneIndex)
    {
        if (laneIndex == 0) return valve1RestDistanceMM;
        if (laneIndex == 1) return valve2RestDistanceMM;
        return valve3RestDistanceMM;
    }

    float GetPressedDistance(int laneIndex)
    {
        if (laneIndex == 0) return valve1PressedDistanceMM;
        if (laneIndex == 1) return valve2PressedDistanceMM;
        return valve3PressedDistanceMM;
    }

    float GetPressEnterDistance(int laneIndex)
    {
        return GetPressedDistance(laneIndex);
    }

    float GetReleaseDistance(int laneIndex)
    {
        return GetRestDistance(laneIndex) - Mathf.Max(0.5f, releaseDeltaMM);
    }

    void ApplyPressedThresholdsFromCalibratedRest()
    {
        float clampedDelta = Mathf.Max(1f, pressEnterDeltaMM);
        valve1PressedDistanceMM = Mathf.Max(1f, valve1RestDistanceMM - clampedDelta);
        valve2PressedDistanceMM = Mathf.Max(1f, valve2RestDistanceMM - clampedDelta);
        valve3PressedDistanceMM = Mathf.Max(1f, valve3RestDistanceMM - clampedDelta);
    }

    void UpdateLiveToFReadout(
        int d1,
        int d2,
        int d3,
        bool v1,
        bool v2,
        bool v3,
        float a1,
        float a2,
        float a3
    )
    {
        liveValve1DistanceMM = d1;
        liveValve2DistanceMM = d2;
        liveValve3DistanceMM = d3;
        liveValve1Pressed = v1;
        liveValve2Pressed = v2;
        liveValve3Pressed = v3;
        liveValve1Amount = a1;
        liveValve2Amount = a2;
        liveValve3Amount = a3;

        string status = isCalibrating ? "CALIBRATING" : (isCalibrated ? "CALIBRATED" : "WAITING");

        liveToFReadout =
            "Status: " + status + "\n" +
            BuildValveReadout(1, d1, v1, a1) + "\n" +
            BuildValveReadout(2, d2, v2, a2) + "\n" +
            BuildValveReadout(3, d3, v3, a3);
    }

    string BuildValveReadout(int valveNumber, int distanceMM, bool pressed, float amount)
    {
        int laneIndex = valveNumber - 1;

        return "V" + valveNumber +
            " D=" + distanceMM + " mm" +
            " rest=" + GetRestDistance(laneIndex).ToString("0.0") +
            " press<=" + GetPressEnterDistance(laneIndex).ToString("0.0") +
            " release>" + GetReleaseDistance(laneIndex).ToString("0.0") +
            " amount=" + amount.ToString("0.00") +
            " state=" + (pressed ? "PRESSED" : "released");
    }

    bool IsValidDistance(int distanceMM)
    {
        return distanceMM > 0 && distanceMM < 255;
    }

    void TryReconnectIfNeeded()
    {
        if (!connectOnStart || !reconnectWhenDisconnected || IsConnected())
        {
            return;
        }

        if (Time.unscaledTime < nextReconnectTime)
        {
            return;
        }

        nextReconnectTime = Time.unscaledTime + Mathf.Max(0.5f, reconnectIntervalSeconds);
        Connect();
    }

    void ApplyValveStateFromTeensyChannel(
        int teensyChannelNumber,
        bool pressed,
        float amount,
        int distanceMM,
        float solenoidDuty,
        float ermDuty
    )
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);

        int laneIndex = TeensyHardwarePinout.UnityChannelNumberToLaneIndex(
            teensyChannelNumber,
            hardwarePinout
        );

        ValveInputState.SetTeensyValve(laneIndex, pressed);
        ValveInputState.SetTeensyValveAmount(laneIndex, amount);
        ValveInputState.SetTeensyValveDistanceMM(laneIndex, distanceMM);
        ValveInputState.SetTeensySolenoidDuty(laneIndex, solenoidDuty);
        ValveInputState.SetTeensyErmDuty(laneIndex, ermDuty);
    }

    float DistanceToPressAmount(int distanceMM, float restDistanceMM, float pressedDistanceMM)
    {
        if (distanceMM < 0 || distanceMM >= 255)
        {
            return 0f;
        }

        float denominator = restDistanceMM - pressedDistanceMM;

        if (Mathf.Abs(denominator) < 0.001f)
        {
            return 0f;
        }

        float amount = (restDistanceMM - distanceMM) / denominator;

        return Mathf.Clamp01(amount);
    }

    public bool IsConnected()
    {
        return serialPort != null && serialPort.IsOpen;
    }

    public void Connect()
    {
        if (IsConnected())
        {
            return;
        }

        string[] candidatePorts = GetCandidatePorts();

        foreach (string candidatePort in candidatePorts)
        {
            if (TryConnectPort(candidatePort))
            {
                return;
            }
        }

        Debug.LogWarning("Could not connect to Teensy. Checked: " + string.Join(", ", candidatePorts));
    }

    string[] GetCandidatePorts()
    {
        if (!autoDetectPort)
        {
            return new string[] { portName };
        }

        string[] availablePorts = SerialPort.GetPortNames();
        string[] candidatePorts = new string[Mathf.Max(1, availablePorts.Length + 1)];

        candidatePorts[0] = portName;
        int index = 1;

        for (int i = 0; i < availablePorts.Length; i++)
        {
            if (availablePorts[i] == portName)
            {
                continue;
            }

            if (index >= candidatePorts.Length)
            {
                break;
            }

            candidatePorts[index] = availablePorts[i];
            index++;
        }

        if (index == candidatePorts.Length)
        {
            return candidatePorts;
        }

        string[] trimmedPorts = new string[index];

        for (int i = 0; i < index; i++)
        {
            trimmedPorts[i] = candidatePorts[i];
        }

        return trimmedPorts;
    }

    bool TryConnectPort(string candidatePort)
    {
        try
        {
            serialPort = new SerialPort(candidatePort, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.WriteTimeout = 50;
            serialPort.NewLine = "\n";
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;

            serialPort.Open();
            portName = candidatePort;

            keepReading = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.IsBackground = true;
            readThread.Start();

            Debug.Log("Connected to Teensy on " + portName);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not connect to Teensy on " + candidatePort + ": " + exception.Message);
            serialPort = null;
            return false;
        }
    }

    public void Disconnect()
    {
        keepReading = false;

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(200);
        }

        if (serialPort != null)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            catch
            {
                // Ignore close errors.
            }
        }

        serialPort = null;
    }

    void ReadSerialLoop()
    {
        while (keepReading)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                {
                    Thread.Sleep(10);
                    continue;
                }

                string line = serialPort.ReadLine();
                ParseIncomingLine(line);
            }
            catch (TimeoutException)
            {
                // Normal when no data arrives during timeout.
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Teensy serial read issue: " + exception.Message);
                Thread.Sleep(50);
            }
        }
    }

    void ParseIncomingLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        line = line.Trim();

        if (printIncomingLines)
        {
            Debug.Log("Teensy IN: " + line);
        }

        if (LooksLikeStandaloneTesterOutput(line))
        {
            if (!legacyTesterWarningShown)
            {
                legacyTesterWarningShown = true;
                Debug.LogWarning(
                    "The Teensy is running the standalone haptic tester, not the Unity telemetry firmware. " +
                    "Flash trumpal_final_code.ino so Unity receives V/D/S/E/TH fields."
                );
            }

            return;
        }

        string[] tokens = line.Split(',');

        lock (stateLock)
        {
            foreach (string token in tokens)
            {
                string[] keyValue = token.Split(':');

                if (keyValue.Length != 2)
                {
                    continue;
                }

                string key = keyValue[0].Trim();
                string value = keyValue[1].Trim();

                if (key == "V1")
                {
                    latestV1 = value == "1";
                }
                else if (key == "V2")
                {
                    latestV2 = value == "1";
                }
                else if (key == "V3")
                {
                    latestV3 = value == "1";
                }
                else if (key == "D1")
                {
                    int.TryParse(value, out latestD1);
                }
                else if (key == "D2")
                {
                    int.TryParse(value, out latestD2);
                }
                else if (key == "D3")
                {
                    int.TryParse(value, out latestD3);
                }
                else if (key == "S1")
                {
                    latestS1 = ParseSerialFloat(value, latestS1);
                }
                else if (key == "S2")
                {
                    latestS2 = ParseSerialFloat(value, latestS2);
                }
                else if (key == "S3")
                {
                    latestS3 = ParseSerialFloat(value, latestS3);
                }
                else if (key == "E1")
                {
                    latestE1 = ParseSerialFloat(value, latestE1);
                }
                else if (key == "E2")
                {
                    latestE2 = ParseSerialFloat(value, latestE2);
                }
                else if (key == "E3")
                {
                    latestE3 = ParseSerialFloat(value, latestE3);
                }
            }
        }
    }

    bool LooksLikeStandaloneTesterOutput(string line)
    {
        return line.Contains("Haptic Serial Tester") ||
            line.StartsWith("Actuating ") ||
            line.StartsWith("Waiting delay ") ||
            line.StartsWith("Sequence Complete") ||
            line.StartsWith("ERROR: Unknown command");
    }

    float ParseSerialFloat(string value, float fallback)
    {
        if (float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float parsed
        ))
        {
            return parsed;
        }

        return fallback;
    }

    public void SendLine(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        lock (writeLock)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                return;
            }

            try
            {
                serialPort.WriteLine(command);

                if (printOutgoingCommands)
                {
                    Debug.Log("Teensy OUT: " + command);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Teensy serial write issue: " + exception.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        SendLine("X");
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnValidate()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
        startupCalibrationPauseSeconds = Mathf.Max(0.1f, startupCalibrationPauseSeconds);
        pressEnterDeltaMM = Mathf.Max(1f, pressEnterDeltaMM);
        releaseDeltaMM = Mathf.Clamp(releaseDeltaMM, 0.5f, pressEnterDeltaMM - 0.5f);

        if (!Application.isPlaying)
        {
            ApplyPressedThresholdsFromCalibratedRest();
        }
    }
}
