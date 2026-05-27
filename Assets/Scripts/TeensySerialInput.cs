using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class TeensySerialInput : MonoBehaviour
{
    public static TeensySerialInput Instance { get; private set; }

    public enum PressDirection
    {
        DistanceDecreasesWhenPressed,
        DistanceIncreasesWhenPressed
    }

    private enum ValveState
    {
        RestLocked,
        PressCandidate,
        Pressed,
        ReleaseCandidate,
        ReleaseLockout
    }

    [Header("serial")]
    public string portName = "COM3";
    public int baudRate = 115200;
    public bool connectOnStart = true;

    [Header("ToF direction")]
    public PressDirection pressDirection = PressDirection.DistanceDecreasesWhenPressed;

    [Header("rest calibration")]
    public bool autoCalibrateRestOnStart = true;
    public float restCalibrationWarmupSeconds = 0.50f;
    public float restCalibrationSeconds = 1.00f;

    [Header("manual rest baseline, mm")]
    public float valve1RestDistanceMM = 35.0f;
    public float valve2RestDistanceMM = 34.0f;
    public float valve3RestDistanceMM = 40.0f;

    [Header("press delta threshold, mm")]
    public float valve1PressDeltaThresholdMM = 1.20f;
    public float valve2PressDeltaThresholdMM = 1.20f;
    public float valve3PressDeltaThresholdMM = 1.20f;

    [Header("release delta threshold, mm")]
    public float valve1ReleaseDeltaThresholdMM = 0.35f;
    public float valve2ReleaseDeltaThresholdMM = 0.35f;
    public float valve3ReleaseDeltaThresholdMM = 0.35f;

    [Header("median filter")]
    public bool useMedianFilter = false;
    public int medianWindow = 5;

    [Header("adaptive noise rejection")]
    public bool useAdaptivePressThreshold = true;
    public float fixedEnvironmentalMarginMM = 0.15f;
    public float noiseEstimateSpeed = 5.0f;
    public float noiseThresholdMultiplier = 3.5f;
    public float maxAdaptivePressThresholdMM = 2.00f;

    [Header("rest baseline tracking")]
    public bool trackRestBaselineWhenInactive = true;
    public float restBaselineTrackSpeedMMPerSecond = 0.20f;
    public float maxRestCorrectionPerFrameMM = 0.012f;

    [Header("press confirmation")]
    public float pressDebounceSeconds = 0.10f;
    public float pressConfidenceDecaySeconds = 0.08f;

    [Header("release confirmation")]
    public float minimumPressedSeconds = 0.13f;
    public float releaseDebounceSeconds = 0.003f;
    public float releaseConfidenceDecaySeconds = 0.20f;

    [Header("release lockout")]
    public float releaseLockoutSeconds = 0.30f;
    public float postReleaseExtraDeltaMM = 0.20f;
    public float postReleasePressDebounceSeconds = 0.18f;
    public float postReleaseConfidenceDecaySeconds = 0.12f;

    [Header("chord release sync")]
    public bool enableChordReleaseSync = true;

    [Tooltip("Maximum time to wait so multiple pressed valves can release together.")]
    public float chordReleaseSyncWindowSeconds = 0.06f;

    [Tooltip("Only sync release when at least this many valves were pressed together.")]
    public int minimumChordSize = 2;

    [Header("debug")]
    public bool printIncomingLines = false;
    public bool printOutgoingCommands = true;

    private const int LaneCount = 3;
    private const int MaxMedianWindow = 9;

    private SerialPort serialPort;
    private Thread readThread;
    private volatile bool keepReading = false;

    private readonly object writeLock = new object();
    private readonly object stateLock = new object();

    private ValveState[] valveStates = new ValveState[LaneCount];

    private bool[] stablePressed = new bool[LaneCount];
    private float[] stableAmount = new float[LaneCount];

    private float[] pressConfidence = new float[LaneCount];
    private float[] releaseConfidence = new float[LaneCount];
    private float[] postReleaseConfidence = new float[LaneCount];

    private float[] pressedStartTime = new float[LaneCount];
    private float[] releaseLockoutEndTime = new float[LaneCount];

    private float[] noiseEstimateMM = new float[LaneCount];

    private int[] latestD = new int[LaneCount] { 255, 255, 255 };
    private int[] filteredD = new int[LaneCount] { 255, 255, 255 };

    private bool[] rawTeensyPressed = new bool[LaneCount];

    private float[] latestSolenoidDuty = new float[LaneCount];
    private int[] latestSolenoidPhase = new int[LaneCount];
    private float[] latestERMDuty = new float[LaneCount];

    private int[,] distanceHistory = new int[LaneCount, MaxMedianWindow];
    private int[] historyCount = new int[LaneCount];
    private int[] historyIndex = new int[LaneCount];

    private bool calibrationWarmupActive = false;
    private float calibrationWarmupTimer = 0f;

    private bool calibrationActive = false;
    private float calibrationTimer = 0f;
    private float[] calibrationSum = new float[LaneCount];
    private int[] calibrationCount = new int[LaneCount];

    private bool chordReleaseActive = false;
    private float chordReleaseStartTime = -999f;
    private bool[] chordMember = new bool[LaneCount];
    private bool[] chordReleaseReady = new bool[LaneCount];

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResetRuntimeState();

        if (connectOnStart)
        {
            Connect();
        }

        if (autoCalibrateRestOnStart)
        {
            StartCalibrationWarmup();
        }
    }

    void Update()
    {
        if (calibrationWarmupActive)
        {
            calibrationWarmupTimer += Time.deltaTime;
            ForceAllRest();
            PushToValveInputState();

            if (calibrationWarmupTimer >= restCalibrationWarmupSeconds)
            {
                calibrationWarmupActive = false;
                BeginRestCalibration();
            }

            return;
        }

        if (calibrationActive)
        {
            UpdateRestCalibration();
            ForceAllRest();
            PushToValveInputState();
            return;
        }

        int[] localD = new int[LaneCount];

        lock (stateLock)
        {
            for (int i = 0; i < LaneCount; i++)
            {
                localD[i] = latestD[i];
            }
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            filteredD[lane] = FilterDistance(lane, localD[lane]);
        }

        UpdateLane(0, filteredD[0], ref valve1RestDistanceMM, valve1PressDeltaThresholdMM, valve1ReleaseDeltaThresholdMM);
        UpdateLane(1, filteredD[1], ref valve2RestDistanceMM, valve2PressDeltaThresholdMM, valve2ReleaseDeltaThresholdMM);
        UpdateLane(2, filteredD[2], ref valve3RestDistanceMM, valve3PressDeltaThresholdMM, valve3ReleaseDeltaThresholdMM);

        ResolveChordReleaseGroup();

        PushToValveInputState();
    }

    void StartCalibrationWarmup()
    {
        calibrationWarmupActive = true;
        calibrationWarmupTimer = 0f;
        ResetRuntimeState();
        Debug.Log("ToF calibration warmup started. Do not press valves.");
    }

    public void BeginRestCalibration()
    {
        calibrationActive = true;
        calibrationTimer = 0f;

        for (int i = 0; i < LaneCount; i++)
        {
            calibrationSum[i] = 0f;
            calibrationCount[i] = 0;
        }

        ResetRuntimeState();

        Debug.Log("ToF rest calibration started. Do not press valves.");
    }

    void UpdateRestCalibration()
    {
        calibrationTimer += Time.deltaTime;

        int[] localD = new int[LaneCount];

        lock (stateLock)
        {
            for (int i = 0; i < LaneCount; i++)
            {
                localD[i] = latestD[i];
            }
        }

        for (int i = 0; i < LaneCount; i++)
        {
            if (IsValidDistance(localD[i]))
            {
                calibrationSum[i] += localD[i];
                calibrationCount[i]++;
            }
        }

        if (calibrationTimer >= restCalibrationSeconds)
        {
            if (calibrationCount[0] > 0) valve1RestDistanceMM = calibrationSum[0] / calibrationCount[0];
            if (calibrationCount[1] > 0) valve2RestDistanceMM = calibrationSum[1] / calibrationCount[1];
            if (calibrationCount[2] > 0) valve3RestDistanceMM = calibrationSum[2] / calibrationCount[2];

            calibrationActive = false;
            ResetRuntimeState();

            Debug.Log(
                "ToF rest calibration complete. Rest: " +
                "D1=" + valve1RestDistanceMM.ToString("0.00", CultureInfo.InvariantCulture) + ", " +
                "D2=" + valve2RestDistanceMM.ToString("0.00", CultureInfo.InvariantCulture) + ", " +
                "D3=" + valve3RestDistanceMM.ToString("0.00", CultureInfo.InvariantCulture)
            );
        }
    }

    void ResetRuntimeState()
    {
        for (int i = 0; i < LaneCount; i++)
        {
            valveStates[i] = ValveState.RestLocked;

            stablePressed[i] = false;
            stableAmount[i] = 0f;

            pressConfidence[i] = 0f;
            releaseConfidence[i] = 0f;
            postReleaseConfidence[i] = 0f;

            pressedStartTime[i] = -999f;
            releaseLockoutEndTime[i] = -999f;

            noiseEstimateMM[i] = 0f;

            historyCount[i] = 0;
            historyIndex[i] = 0;
            filteredD[i] = 255;

            chordMember[i] = false;
            chordReleaseReady[i] = false;

            for (int j = 0; j < MaxMedianWindow; j++)
            {
                distanceHistory[i, j] = 255;
            }
        }

        chordReleaseActive = false;
        chordReleaseStartTime = -999f;

        ValveInputState.ClearAll();
    }

    int FilterDistance(int lane, int distance)
    {
        if (!IsValidDistance(distance))
        {
            return IsValidDistance(filteredD[lane]) ? filteredD[lane] : 255;
        }

        if (!useMedianFilter)
        {
            return distance;
        }

        int window = Mathf.Clamp(medianWindow, 1, MaxMedianWindow);

        distanceHistory[lane, historyIndex[lane]] = distance;
        historyIndex[lane] = (historyIndex[lane] + 1) % window;
        historyCount[lane] = Mathf.Min(historyCount[lane] + 1, window);

        int count = historyCount[lane];
        int[] temp = new int[count];

        for (int i = 0; i < count; i++)
        {
            temp[i] = distanceHistory[lane, i];
        }

        Array.Sort(temp);

        return temp[count / 2];
    }

    void UpdateLane(
        int lane,
        int distanceMM,
        ref float restDistanceMM,
        float basePressDeltaThresholdMM,
        float releaseDeltaThresholdMM
    )
    {
        float deltaMM = ComputeSignedDelta(distanceMM, restDistanceMM);

        UpdateNoiseEstimateIfResting(lane, deltaMM);

        float effectivePressThresholdMM = GetEffectivePressThreshold(lane, basePressDeltaThresholdMM);

        switch (valveStates[lane])
        {
            case ValveState.RestLocked:
                UpdateRestLocked(lane, distanceMM, ref restDistanceMM, deltaMM, effectivePressThresholdMM);
                break;

            case ValveState.PressCandidate:
                UpdatePressCandidate(lane, deltaMM, effectivePressThresholdMM);
                break;

            case ValveState.Pressed:
                UpdatePressed(lane, deltaMM, releaseDeltaThresholdMM);
                break;

            case ValveState.ReleaseCandidate:
                UpdateReleaseCandidate(lane, deltaMM, releaseDeltaThresholdMM);
                break;

            case ValveState.ReleaseLockout:
                UpdateReleaseLockout(lane, distanceMM, ref restDistanceMM, deltaMM, effectivePressThresholdMM);
                break;
        }
    }

    void UpdateRestLocked(
        int lane,
        int distanceMM,
        ref float restDistanceMM,
        float deltaMM,
        float effectivePressThresholdMM
    )
    {
        ForceLaneRest(lane);
        TrackRestBaseline(lane, distanceMM, ref restDistanceMM, deltaMM, effectivePressThresholdMM);

        if (deltaMM >= effectivePressThresholdMM)
        {
            valveStates[lane] = ValveState.PressCandidate;
            pressConfidence[lane] = 0f;
        }
    }

    void UpdatePressCandidate(int lane, float deltaMM, float effectivePressThresholdMM)
    {
        ForceLaneRest(lane);

        bool evidence = deltaMM >= effectivePressThresholdMM;

        pressConfidence[lane] = UpdateConfidence(
            pressConfidence[lane],
            evidence,
            pressDebounceSeconds,
            pressConfidenceDecaySeconds
        );

        if (pressConfidence[lane] >= 1f)
        {
            EnterPressed(lane);
            return;
        }

        if (pressConfidence[lane] <= 0f && !evidence)
        {
            valveStates[lane] = ValveState.RestLocked;
        }
    }

    void EnterPressed(int lane)
    {
        valveStates[lane] = ValveState.Pressed;

        stablePressed[lane] = true;
        stableAmount[lane] = 1f;

        pressedStartTime[lane] = Time.time;
        releaseConfidence[lane] = 0f;
        postReleaseConfidence[lane] = 0f;

        chordReleaseReady[lane] = false;
    }

    void UpdatePressed(int lane, float deltaMM, float releaseDeltaThresholdMM)
    {
        stablePressed[lane] = true;
        stableAmount[lane] = 1f;

        bool oldEnoughToRelease = Time.time - pressedStartTime[lane] >= minimumPressedSeconds;
        bool releaseEvidence = oldEnoughToRelease && deltaMM <= releaseDeltaThresholdMM;

        if (releaseEvidence)
        {
            valveStates[lane] = ValveState.ReleaseCandidate;
            releaseConfidence[lane] = 0f;

            TryStartChordRelease(lane);
        }
    }

    void UpdateReleaseCandidate(int lane, float deltaMM, float releaseDeltaThresholdMM)
    {
        stablePressed[lane] = true;
        stableAmount[lane] = 1f;

        bool evidence = deltaMM <= releaseDeltaThresholdMM;

        releaseConfidence[lane] = UpdateConfidence(
            releaseConfidence[lane],
            evidence,
            releaseDebounceSeconds,
            releaseConfidenceDecaySeconds
        );

        if (releaseConfidence[lane] >= 1f)
        {
            chordReleaseReady[lane] = true;

            if (!chordReleaseActive)
            {
                EnterReleaseLockout(lane);
            }

            return;
        }

        if (releaseConfidence[lane] <= 0f && !evidence)
        {
            valveStates[lane] = ValveState.Pressed;
            chordReleaseReady[lane] = false;
        }
    }

    void TryStartChordRelease(int laneThatStartedRelease)
    {
        if (!enableChordReleaseSync)
        {
            return;
        }

        if (chordReleaseActive)
        {
            return;
        }

        int activeCount = 0;

        for (int i = 0; i < LaneCount; i++)
        {
            bool laneIsActive =
                valveStates[i] == ValveState.Pressed ||
                valveStates[i] == ValveState.ReleaseCandidate;

            chordMember[i] = laneIsActive;

            if (laneIsActive)
            {
                activeCount++;
            }
        }

        if (activeCount < Mathf.Max(2, minimumChordSize))
        {
            ClearChordRelease();
            return;
        }

        chordReleaseActive = true;
        chordReleaseStartTime = Time.time;

        for (int i = 0; i < LaneCount; i++)
        {
            chordReleaseReady[i] = false;
        }

        chordReleaseReady[laneThatStartedRelease] = false;
    }

    void ResolveChordReleaseGroup()
    {
        if (!chordReleaseActive)
        {
            return;
        }

        int memberCount = 0;
        int readyCount = 0;

        for (int i = 0; i < LaneCount; i++)
        {
            if (!chordMember[i])
            {
                continue;
            }

            memberCount++;

            if (chordReleaseReady[i])
            {
                readyCount++;
            }
        }

        bool allReady = memberCount > 0 && readyCount == memberCount;
        bool timedOut = Time.time - chordReleaseStartTime >= chordReleaseSyncWindowSeconds;

        if (allReady)
        {
            for (int i = 0; i < LaneCount; i++)
            {
                if (chordMember[i])
                {
                    EnterReleaseLockout(i);
                }
            }

            ClearChordRelease();
            return;
        }

        if (timedOut)
        {
            for (int i = 0; i < LaneCount; i++)
            {
                if (chordMember[i] && chordReleaseReady[i])
                {
                    EnterReleaseLockout(i);
                }
            }

            ClearChordRelease();
        }
    }

    void ClearChordRelease()
    {
        chordReleaseActive = false;
        chordReleaseStartTime = -999f;

        for (int i = 0; i < LaneCount; i++)
        {
            chordMember[i] = false;
            chordReleaseReady[i] = false;
        }
    }

    void EnterReleaseLockout(int lane)
    {
        valveStates[lane] = ValveState.ReleaseLockout;

        stablePressed[lane] = false;
        stableAmount[lane] = 0f;

        releaseLockoutEndTime[lane] = Time.time + releaseLockoutSeconds;
        postReleaseConfidence[lane] = 0f;
        releaseConfidence[lane] = 0f;
    }

    void UpdateReleaseLockout(
        int lane,
        int distanceMM,
        ref float restDistanceMM,
        float deltaMM,
        float effectivePressThresholdMM
    )
    {
        ForceLaneRest(lane);
        TrackRestBaseline(lane, distanceMM, ref restDistanceMM, deltaMM, effectivePressThresholdMM);

        if (Time.time < releaseLockoutEndTime[lane])
        {
            return;
        }

        float reactivationThreshold = effectivePressThresholdMM + postReleaseExtraDeltaMM;
        bool reactivationEvidence = deltaMM >= reactivationThreshold;

        postReleaseConfidence[lane] = UpdateConfidence(
            postReleaseConfidence[lane],
            reactivationEvidence,
            postReleasePressDebounceSeconds,
            postReleaseConfidenceDecaySeconds
        );

        if (postReleaseConfidence[lane] >= 1f)
        {
            EnterPressed(lane);
            return;
        }

        if (deltaMM <= effectivePressThresholdMM)
        {
            valveStates[lane] = ValveState.RestLocked;
            postReleaseConfidence[lane] = 0f;
        }
    }

    float UpdateConfidence(float current, bool evidence, float riseSeconds, float fallSeconds)
    {
        if (evidence)
        {
            current += Time.deltaTime / Mathf.Max(0.001f, riseSeconds);
        }
        else
        {
            current -= Time.deltaTime / Mathf.Max(0.001f, fallSeconds);
        }

        return Mathf.Clamp01(current);
    }

    void ForceLaneRest(int lane)
    {
        stablePressed[lane] = false;
        stableAmount[lane] = 0f;
    }

    void UpdateNoiseEstimateIfResting(int lane, float deltaMM)
    {
        if (valveStates[lane] != ValveState.RestLocked &&
            valveStates[lane] != ValveState.ReleaseLockout)
        {
            return;
        }

        float restNoise = Mathf.Max(0f, Mathf.Abs(deltaMM));
        float t = 1f - Mathf.Exp(-noiseEstimateSpeed * Time.deltaTime);

        noiseEstimateMM[lane] = Mathf.Lerp(noiseEstimateMM[lane], restNoise, t);
    }

    float GetEffectivePressThreshold(int lane, float basePressDeltaThresholdMM)
    {
        if (!useAdaptivePressThreshold)
        {
            return basePressDeltaThresholdMM;
        }

        float threshold =
            basePressDeltaThresholdMM +
            fixedEnvironmentalMarginMM +
            noiseEstimateMM[lane] * noiseThresholdMultiplier;

        return Mathf.Min(threshold, maxAdaptivePressThresholdMM);
    }

    void TrackRestBaseline(
        int lane,
        int distanceMM,
        ref float restDistanceMM,
        float deltaMM,
        float effectivePressThresholdMM
    )
    {
        if (!trackRestBaselineWhenInactive)
        {
            return;
        }

        if (!IsValidDistance(distanceMM))
        {
            return;
        }

        if (deltaMM >= effectivePressThresholdMM)
        {
            return;
        }

        float maxStep = Mathf.Min(
            restBaselineTrackSpeedMMPerSecond * Time.deltaTime,
            maxRestCorrectionPerFrameMM
        );

        restDistanceMM = Mathf.MoveTowards(restDistanceMM, distanceMM, maxStep);
    }

    float ComputeSignedDelta(int distanceMM, float restDistanceMM)
    {
        if (!IsValidDistance(distanceMM))
        {
            return 0f;
        }

        if (pressDirection == PressDirection.DistanceDecreasesWhenPressed)
        {
            return restDistanceMM - distanceMM;
        }

        return distanceMM - restDistanceMM;
    }

    void PushToValveInputState()
    {
        for (int i = 0; i < LaneCount; i++)
        {
            ValveInputState.SetTeensyValve(i, stablePressed[i]);
            ValveInputState.SetTeensyValveAmount(i, stableAmount[i]);
        }
    }

    void ForceAllRest()
    {
        for (int i = 0; i < LaneCount; i++)
        {
            ForceLaneRest(i);
        }
    }

    bool IsValidDistance(int distanceMM)
    {
        return distanceMM >= 0 && distanceMM < 255;
    }

    public void Connect()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            return;
        }

        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.WriteTimeout = 50;
            serialPort.NewLine = "\n";
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;

            serialPort.Open();

            keepReading = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.IsBackground = true;
            readThread.Start();

            Debug.Log("Connected to Teensy on " + portName);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not connect to Teensy on " + portName + ": " + exception.Message);
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
                // Normal serial timeout.
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

                if (key.Length == 2)
                {
                    int lane = key[1] - '1';

                    if (lane < 0 || lane >= LaneCount)
                    {
                        continue;
                    }

                    if (key[0] == 'D')
                    {
                        int.TryParse(value, out latestD[lane]);
                    }
                    else if (key[0] == 'V')
                    {
                        rawTeensyPressed[lane] = value == "1";
                    }
                    else if (key[0] == 'S')
                    {
                        TryParseFloat(value, out latestSolenoidDuty[lane]);
                    }
                    else if (key[0] == 'E')
                    {
                        TryParseFloat(value, out latestERMDuty[lane]);
                    }
                }
                else if (key.Length == 3 && key.StartsWith("SP"))
                {
                    int lane = key[2] - '1';

                    if (lane >= 0 && lane < LaneCount)
                    {
                        int.TryParse(value, out latestSolenoidPhase[lane]);
                    }
                }
            }
        }
    }

    bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
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

    string GetStateCode(int lane)
    {
        if (valveStates[lane] == ValveState.RestLocked) return "R";
        if (valveStates[lane] == ValveState.PressCandidate) return "PC";
        if (valveStates[lane] == ValveState.Pressed) return "P";
        if (valveStates[lane] == ValveState.ReleaseCandidate) return "RC";
        return "L";
    }

    float GetRestDistance(int lane)
    {
        if (lane == 0) return valve1RestDistanceMM;
        if (lane == 1) return valve2RestDistanceMM;
        return valve3RestDistanceMM;
    }

    public string GetHardwareStateSummary()
    {
        lock (stateLock)
        {
            return
                "TOF / Discrete Valve States\n" +
                "D1:" + latestD[0] + " F1:" + filteredD[0] + " A1:" + stableAmount[0].ToString("0.00", CultureInfo.InvariantCulture) + " V1:" + (stablePressed[0] ? "1" : "0") + " S:" + GetStateCode(0) + " N:" + noiseEstimateMM[0].ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "D2:" + latestD[1] + " F2:" + filteredD[1] + " A2:" + stableAmount[1].ToString("0.00", CultureInfo.InvariantCulture) + " V2:" + (stablePressed[1] ? "1" : "0") + " S:" + GetStateCode(1) + " N:" + noiseEstimateMM[1].ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "D3:" + latestD[2] + " F3:" + filteredD[2] + " A3:" + stableAmount[2].ToString("0.00", CultureInfo.InvariantCulture) + " V3:" + (stablePressed[2] ? "1" : "0") + " S:" + GetStateCode(2) + " N:" + noiseEstimateMM[2].ToString("0.00", CultureInfo.InvariantCulture) + "\n\n" +
                "Chord Release\n" +
                "Active:" + (chordReleaseActive ? "1" : "0") + "\n\n" +
                "Raw Teensy V\n" +
                "RV1:" + (rawTeensyPressed[0] ? "1" : "0") + " RV2:" + (rawTeensyPressed[1] ? "1" : "0") + " RV3:" + (rawTeensyPressed[2] ? "1" : "0") + "\n\n" +
                "Rest Baseline\n" +
                "R1:" + GetRestDistance(0).ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "R2:" + GetRestDistance(1).ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "R3:" + GetRestDistance(2).ToString("0.00", CultureInfo.InvariantCulture) + "\n\n" +
                "Solenoids\n" +
                "S1:" + latestSolenoidDuty[0].ToString("0.00", CultureInfo.InvariantCulture) + " SP1:" + latestSolenoidPhase[0] + "\n" +
                "S2:" + latestSolenoidDuty[1].ToString("0.00", CultureInfo.InvariantCulture) + " SP2:" + latestSolenoidPhase[1] + "\n" +
                "S3:" + latestSolenoidDuty[2].ToString("0.00", CultureInfo.InvariantCulture) + " SP3:" + latestSolenoidPhase[2] + "\n\n" +
                "ERMs\n" +
                "E1:" + latestERMDuty[0].ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "E2:" + latestERMDuty[1].ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                "E3:" + latestERMDuty[2].ToString("0.00", CultureInfo.InvariantCulture);
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
}