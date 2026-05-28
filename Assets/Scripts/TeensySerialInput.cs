using System;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Must match Serial.begin(...) in Assets/trumpal_final_code/trumpal_final_code.ino.")]
    public int baudRate = 115200;
    [Tooltip("Connect to the Teensy automatically when the scene starts.")]
    public bool connectOnStart = true;
    [Tooltip("Try the configured port first, then scan other COM ports. Leave on for plug-and-play.")]
    public bool autoDetectPort = true;
    [Tooltip("Keep retrying if the Teensy is plugged in after Play starts or briefly disconnects.")]
    public bool reconnectWhenDisconnected = true;
    public float reconnectIntervalSeconds = 2f;

    [Header("Teensy pinout from final firmware")]
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
    [Tooltip("Debug only. Leave off for MVP. Raw Teensy V bits use the fixed THRESH value and can pin valves down when real rest distance is below that value.")]
    public bool acceptTeensyPressBitsAsFallback = false;
    [Tooltip("Read-only runtime status.")]
    public bool isCalibrated = false;
    [Tooltip("Read-only runtime status.")]
    public bool isCalibrating = false;
    [Tooltip("Wait this long for live D1/D2/D3 data before sampling calibration. Prevents stale 80/65 fallback if serial is still connecting.")]
    public float calibrationWaitForDataSeconds = 4f;
    [Tooltip("For close-mounted sensors, cap press delta to this fraction of calibrated rest. 0.25 turns a 33 mm rest into roughly a 25 mm press threshold.")]
    public float closeSensorPressDeltaFraction = 0.25f;
    [Tooltip("Smallest allowed press threshold delta after auto calibration. Keep small for close-mounted sensors.")]
    public float minimumPressEnterDeltaMM = 1.5f;

    [Header("ToF anti-jitter")]
    [Tooltip("After auto calibration finishes, keep all ToF valves released for this long. This prevents startup sensor settling from twitching valves.")]
    public float postCalibrationReleaseGuardSeconds = 0.75f;
    [Tooltip("Distance must stay past the press threshold this long before Unity accepts a valve press.")]
    public float pressDebounceSeconds = 0.08f;
    [Tooltip("Distance must stay past the release threshold this long before Unity accepts valve release.")]
    public float releaseDebounceSeconds = 0.04f;
    [Range(0.5f, 0.95f)]
    [Tooltip("Auto calibration uses this percentile of released samples as rest distance. 0.75 ignores low noisy dips that can cause false presses.")]
    public float calibrationRestPercentile = 0.75f;
    [Tooltip("Minimum live samples before percentile calibration is trusted for a lane.")]
    public int minimumCalibrationSamplesPerLane = 5;
    [Tooltip("Reject startup rest calibration above this distance. A VL6180X reading near 96 mm is usually saturated or pointed at the wrong surface for this valve rig.")]
    public float maxReasonableRestDistanceMM = 90f;
    [Tooltip("When a lane calibrates above Max Reasonable Rest Distance, keep the previous rest value and wait for a valid live value instead.")]
    public bool rejectOutOfRangeCalibration = true;
    [Tooltip("If a lane was calibrated to a stale close value but live released distance is much farther away, re-baseline it during play.")]
    public bool allowLiveRestRebaseline = true;
    [Tooltip("Live released distance must be this much above the stored rest before Unity treats the old rest as stale.")]
    public float restRebaselineDeltaMM = 8f;
    [Tooltip("Live released distance must stay stable this long before Unity rewrites the rest baseline.")]
    public float restRebaselineStableSeconds = 0.20f;

    [Header("ToF channel diagnostics")]
    [Tooltip("Latest active mux channel reported by firmware for valve 1.")]
    public int activeValve1TofMuxChannel = 0;
    [Tooltip("Latest active mux channel reported by firmware for valve 2.")]
    public int activeValve2TofMuxChannel = 6;
    [Tooltip("Latest active mux channel reported by firmware for valve 3.")]
    public int activeValve3TofMuxChannel = 7;
    [TextArea(2, 4)]
    public string tofHealthReadout = "ToF channels not checked yet.";

    [Header("ToF animation amount")]
    [Tooltip("Gameplay stays discrete, but valve animation can still show partial ToF travel before the pressed threshold is crossed.")]
    public bool useAnalogAmountsWhileDiscrete = true;
    [Range(0f, 0.5f)]
    [Tooltip("Small analog amount ignored for animation so idle ToF noise does not wiggle valves.")]
    public float analogAmountDeadZone = 0.08f;

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

    private int latestTC1 = 0;
    private int latestTC2 = 6;
    private int latestTC3 = 7;

    private float latestS1 = 0f;
    private float latestS2 = 0f;
    private float latestS3 = 0f;

    private float latestE1 = 0f;
    private float latestE2 = 0f;
    private float latestE3 = 0f;

    private bool[] calibratedPressedStates = new bool[3];
    private bool[] pendingPressedStates = new bool[3];
    private float[] pendingPressedStateSince = new float[3];
    private bool[] laneHasLiveCalibration = new bool[3];
    private int[] restRebaselineCandidateDistance = new int[3] { 255, 255, 255 };
    private float[] restRebaselineCandidateSince = new float[3];
    private bool legacyTesterWarningShown = false;
    private float postCalibrationReleaseGuardUntil = 0f;

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

        int tc1;
        int tc2;
        int tc3;

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

            tc1 = latestTC1;
            tc2 = latestTC2;
            tc3 = latestTC3;

            s1 = latestS1;
            s2 = latestS2;
            s3 = latestS3;

            e1 = latestE1;
            e2 = latestE2;
            e3 = latestE3;
        }

        if (isCalibrated)
        {
            TryLazyCalibrateLane(0, d1);
            TryLazyCalibrateLane(1, d2);
            TryLazyCalibrateLane(2, d3);
            TryLiveRestRebaseline(0, d1);
            TryLiveRestRebaseline(1, d2);
            TryLiveRestRebaseline(2, d3);
        }

        float a1 = DistanceToPressAmount(d1, valve1RestDistanceMM, valve1PressedDistanceMM);
        float a2 = DistanceToPressAmount(d2, valve2RestDistanceMM, valve2PressedDistanceMM);
        float a3 = DistanceToPressAmount(d3, valve3RestDistanceMM, valve3PressedDistanceMM);

        if (isCalibrating || IsPostCalibrationReleaseGuardActive())
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
            v1 = GetDiscretePressedState(0, d1);
            v2 = GetDiscretePressedState(1, d2);
            v3 = GetDiscretePressedState(2, d3);

            a1 = GetDiscreteModeValveAmount(0, d1, v1, a1);
            a2 = GetDiscreteModeValveAmount(1, d2, v2, a2);
            a3 = GetDiscreteModeValveAmount(2, d3, v3, a3);
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
        UpdateActiveTofChannels(tc1, tc2, tc3);
        UpdateLiveToFReadout(d1, d2, d3, v1, v2, v3, a1, a2, a3);
    }

    IEnumerator AutoCalibrateStartup()
    {
        isCalibrating = true;
        isCalibrated = false;
        ValveInputState.ClearAll();

        for (int i = 0; i < laneHasLiveCalibration.Length; i++)
        {
            laneHasLiveCalibration[i] = false;
        }

        float previousTimeScale = Time.timeScale;

        if (pauseGameDuringCalibration)
        {
            Time.timeScale = 0f;
        }

        float[] sums = new float[3];
        int[] sampleCounts = new int[3];
        List<int>[] calibrationSamples = CreateCalibrationSampleBuckets();
        float waitForDataEndTime = Time.realtimeSinceStartup + Mathf.Max(0f, calibrationWaitForDataSeconds);

        while (Time.realtimeSinceStartup < waitForDataEndTime && !HasAnyValidLiveDistance())
        {
            if (pauseGameDuringCalibration)
            {
                Time.timeScale = 0f;
            }

            yield return null;
        }

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

            AddCalibrationSample(0, d1, sums, sampleCounts, calibrationSamples);
            AddCalibrationSample(1, d2, sums, sampleCounts, calibrationSamples);
            AddCalibrationSample(2, d3, sums, sampleCounts, calibrationSamples);

            yield return null;
        }

        ApplyCalibrationSamples(sums, sampleCounts, calibrationSamples);
        ResetDiscreteToFStates();

        ValveInputState.ClearAll();
        postCalibrationReleaseGuardUntil = Time.realtimeSinceStartup + Mathf.Max(0f, postCalibrationReleaseGuardSeconds);
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

    List<int>[] CreateCalibrationSampleBuckets()
    {
        return new List<int>[]
        {
            new List<int>(),
            new List<int>(),
            new List<int>()
        };
    }

    void AddCalibrationSample(
        int laneIndex,
        int distanceMM,
        float[] sums,
        int[] sampleCounts,
        List<int>[] calibrationSamples
    )
    {
        if (!IsValidDistance(distanceMM))
        {
            return;
        }

        sums[laneIndex] += distanceMM;
        sampleCounts[laneIndex] += 1;

        if (calibrationSamples != null &&
            laneIndex >= 0 &&
            laneIndex < calibrationSamples.Length &&
            calibrationSamples[laneIndex] != null)
        {
            calibrationSamples[laneIndex].Add(distanceMM);
        }
    }

    void ApplyCalibrationSamples(float[] sums, int[] sampleCounts, List<int>[] calibrationSamples)
    {
        bool accepted1;
        bool accepted2;
        bool accepted3;

        valve1RestDistanceMM = GetValidatedCalibratedRestDistance(0, sums, sampleCounts, calibrationSamples, valve1RestDistanceMM, out accepted1);
        valve2RestDistanceMM = GetValidatedCalibratedRestDistance(1, sums, sampleCounts, calibrationSamples, valve2RestDistanceMM, out accepted2);
        valve3RestDistanceMM = GetValidatedCalibratedRestDistance(2, sums, sampleCounts, calibrationSamples, valve3RestDistanceMM, out accepted3);

        laneHasLiveCalibration[0] = accepted1;
        laneHasLiveCalibration[1] = accepted2;
        laneHasLiveCalibration[2] = accepted3;

        ApplyPressedThresholdsFromCalibratedRest();
    }

    float GetValidatedCalibratedRestDistance(
        int laneIndex,
        float[] sums,
        int[] sampleCounts,
        List<int>[] calibrationSamples,
        float fallbackRestDistanceMM,
        out bool accepted
    )
    {
        accepted = false;

        float calibratedRestDistance = GetCalibratedRestDistance(
            laneIndex,
            sums,
            sampleCounts,
            calibrationSamples,
            fallbackRestDistanceMM
        );

        if (sampleCounts[laneIndex] <= 0)
        {
            return fallbackRestDistanceMM;
        }

        if (!IsReasonableRestDistance(calibratedRestDistance))
        {
            Debug.LogWarning(
                "Rejected ToF calibration for valve " + (laneIndex + 1) +
                ": rest=" + calibratedRestDistance.ToString("0.0") +
                " mm. Keeping previous rest=" + fallbackRestDistanceMM.ToString("0.0") +
                " mm. Check mux channel/aiming."
            );

            return fallbackRestDistanceMM;
        }

        accepted = true;
        return calibratedRestDistance;
    }

    float GetCalibratedRestDistance(
        int laneIndex,
        float[] sums,
        int[] sampleCounts,
        List<int>[] calibrationSamples,
        float fallbackRestDistanceMM
    )
    {
        if (sampleCounts[laneIndex] <= 0)
        {
            return fallbackRestDistanceMM;
        }

        if (calibrationSamples != null &&
            laneIndex >= 0 &&
            laneIndex < calibrationSamples.Length &&
            calibrationSamples[laneIndex] != null &&
            calibrationSamples[laneIndex].Count >= minimumCalibrationSamplesPerLane)
        {
            return GetCalibrationPercentile(calibrationSamples[laneIndex]);
        }

        return sums[laneIndex] / sampleCounts[laneIndex];
    }

    float GetCalibrationPercentile(List<int> samples)
    {
        samples.Sort();

        if (samples.Count <= 0)
        {
            return 0f;
        }

        int index = Mathf.RoundToInt((samples.Count - 1) * calibrationRestPercentile);
        index = Mathf.Clamp(index, 0, samples.Count - 1);

        return samples[index];
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
        bool rawPressed = wasPressed
            ? distanceMM <= exitPressedDistanceMM
            : distanceMM <= enterPressedDistanceMM;

        return ApplyDiscreteStateDebounce(laneIndex, rawPressed);
    }

    float GetDiscreteModeValveAmount(int laneIndex, int distanceMM, bool pressed, float rawAmount)
    {
        if (pressed)
        {
            return 1f;
        }

        if (!useAnalogAmountsWhileDiscrete || !IsValidDistance(distanceMM))
        {
            return 0f;
        }

        float deadZone = Mathf.Clamp01(analogAmountDeadZone);

        if (rawAmount <= deadZone)
        {
            return 0f;
        }

        return Mathf.InverseLerp(deadZone, 1f, Mathf.Clamp01(rawAmount));
    }

    bool ApplyDiscreteStateDebounce(int laneIndex, bool rawPressed)
    {
        bool currentPressed = calibratedPressedStates[laneIndex];
        float now = Time.realtimeSinceStartup;

        if (rawPressed == currentPressed)
        {
            pendingPressedStates[laneIndex] = rawPressed;
            pendingPressedStateSince[laneIndex] = now;
            return currentPressed;
        }

        if (pendingPressedStates[laneIndex] != rawPressed)
        {
            pendingPressedStates[laneIndex] = rawPressed;
            pendingPressedStateSince[laneIndex] = now;
            return currentPressed;
        }

        float requiredStableSeconds = rawPressed
            ? Mathf.Max(0f, pressDebounceSeconds)
            : Mathf.Max(0f, releaseDebounceSeconds);

        if (now - pendingPressedStateSince[laneIndex] >= requiredStableSeconds)
        {
            calibratedPressedStates[laneIndex] = rawPressed;
            pendingPressedStateSince[laneIndex] = now;
            return rawPressed;
        }

        return currentPressed;
    }

    void ResetDiscreteToFStates()
    {
        float now = Time.realtimeSinceStartup;

        for (int i = 0; i < calibratedPressedStates.Length; i++)
        {
            calibratedPressedStates[i] = false;
            pendingPressedStates[i] = false;
            pendingPressedStateSince[i] = now;
        }
    }

    bool IsPostCalibrationReleaseGuardActive()
    {
        return Time.realtimeSinceStartup < postCalibrationReleaseGuardUntil;
    }

    void TryLazyCalibrateLane(int laneIndex, int distanceMM)
    {
        if (laneIndex < 0 || laneIndex >= laneHasLiveCalibration.Length)
        {
            return;
        }

        if (laneHasLiveCalibration[laneIndex] || !IsValidDistance(distanceMM))
        {
            return;
        }

        if (!IsReasonableRestDistance(distanceMM))
        {
            return;
        }

        SetRestDistance(laneIndex, distanceMM);
        ApplyPressedThresholdsFromCalibratedRest();
        calibratedPressedStates[laneIndex] = false;
        pendingPressedStates[laneIndex] = false;
        pendingPressedStateSince[laneIndex] = Time.realtimeSinceStartup;
        laneHasLiveCalibration[laneIndex] = true;

        Debug.Log("Lazy ToF calibrated valve " + (laneIndex + 1) + " rest to " + distanceMM + " mm");
    }

    void TryLiveRestRebaseline(int laneIndex, int distanceMM)
    {
        if (!allowLiveRestRebaseline ||
            laneIndex < 0 ||
            laneIndex >= restRebaselineCandidateDistance.Length)
        {
            return;
        }

        if (!IsValidDistance(distanceMM) || !IsReasonableRestDistance(distanceMM))
        {
            ResetRestRebaselineCandidate(laneIndex);
            return;
        }

        if (calibratedPressedStates[laneIndex])
        {
            ResetRestRebaselineCandidate(laneIndex);
            return;
        }

        float currentRest = GetRestDistance(laneIndex);
        float requiredJump = Mathf.Max(1f, restRebaselineDeltaMM);
        bool currentRestIsSuspicious = !IsReasonableRestDistance(currentRest);
        bool liveDistanceIsMuchFarther = distanceMM > currentRest + requiredJump;

        if (!currentRestIsSuspicious && !liveDistanceIsMuchFarther)
        {
            ResetRestRebaselineCandidate(laneIndex);
            return;
        }

        float now = Time.realtimeSinceStartup;
        int candidateDistance = restRebaselineCandidateDistance[laneIndex];

        if (!IsValidDistance(candidateDistance) || Mathf.Abs(candidateDistance - distanceMM) > 2)
        {
            restRebaselineCandidateDistance[laneIndex] = distanceMM;
            restRebaselineCandidateSince[laneIndex] = now;
            return;
        }

        if (now - restRebaselineCandidateSince[laneIndex] < Mathf.Max(0f, restRebaselineStableSeconds))
        {
            return;
        }

        SetRestDistance(laneIndex, distanceMM);
        ApplyPressedThresholdsFromCalibratedRest();
        calibratedPressedStates[laneIndex] = false;
        pendingPressedStates[laneIndex] = false;
        pendingPressedStateSince[laneIndex] = now;
        laneHasLiveCalibration[laneIndex] = true;
        ResetRestRebaselineCandidate(laneIndex);

        Debug.LogWarning(
            "Re-baselined ToF valve " + (laneIndex + 1) +
            " rest to live released distance " + distanceMM +
            " mm because the previous rest was stale."
        );
    }

    void ResetRestRebaselineCandidate(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= restRebaselineCandidateDistance.Length)
        {
            return;
        }

        restRebaselineCandidateDistance[laneIndex] = 255;
        restRebaselineCandidateSince[laneIndex] = 0f;
    }

    float GetRestDistance(int laneIndex)
    {
        if (laneIndex == 0) return valve1RestDistanceMM;
        if (laneIndex == 1) return valve2RestDistanceMM;
        return valve3RestDistanceMM;
    }

    void SetRestDistance(int laneIndex, float distanceMM)
    {
        if (laneIndex == 0)
        {
            valve1RestDistanceMM = distanceMM;
        }
        else if (laneIndex == 1)
        {
            valve2RestDistanceMM = distanceMM;
        }
        else
        {
            valve3RestDistanceMM = distanceMM;
        }
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
        return GetRestDistance(laneIndex) - GetEffectiveReleaseDelta(laneIndex);
    }

    void ApplyPressedThresholdsFromCalibratedRest()
    {
        valve1PressedDistanceMM = Mathf.Max(1f, valve1RestDistanceMM - GetEffectivePressEnterDelta(0));
        valve2PressedDistanceMM = Mathf.Max(1f, valve2RestDistanceMM - GetEffectivePressEnterDelta(1));
        valve3PressedDistanceMM = Mathf.Max(1f, valve3RestDistanceMM - GetEffectivePressEnterDelta(2));
    }

    bool HasAnyValidLiveDistance()
    {
        lock (stateLock)
        {
            return IsValidDistance(latestD1) ||
                IsValidDistance(latestD2) ||
                IsValidDistance(latestD3);
        }
    }

    bool IsReasonableRestDistance(float distanceMM)
    {
        if (!rejectOutOfRangeCalibration)
        {
            return true;
        }

        return distanceMM > 0f && distanceMM <= Mathf.Max(1f, maxReasonableRestDistanceMM);
    }

    float GetEffectivePressEnterDelta(int laneIndex)
    {
        float restDistance = Mathf.Max(1f, GetRestDistance(laneIndex));
        float requestedDelta = Mathf.Max(minimumPressEnterDeltaMM, pressEnterDeltaMM);
        float closeSensorDeltaCap = Mathf.Max(
            minimumPressEnterDeltaMM,
            restDistance * Mathf.Clamp(closeSensorPressDeltaFraction, 0.05f, 0.9f)
        );

        return Mathf.Min(requestedDelta, closeSensorDeltaCap);
    }

    float GetEffectiveReleaseDelta(int laneIndex)
    {
        float pressDelta = GetEffectivePressEnterDelta(laneIndex);
        float requestedReleaseDelta = Mathf.Clamp(releaseDeltaMM, 0.5f, pressDelta - 0.25f);
        float closeSensorReleaseCap = Mathf.Max(1f, pressDelta * 0.55f);

        return Mathf.Min(requestedReleaseDelta, closeSensorReleaseCap);
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

        tofHealthReadout =
            BuildTofHealthLine(1, d1, activeValve1TofMuxChannel) + "\n" +
            BuildTofHealthLine(2, d2, activeValve2TofMuxChannel) + "\n" +
            BuildTofHealthLine(3, d3, activeValve3TofMuxChannel);
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

    string BuildTofHealthLine(int valveNumber, int distanceMM, int muxChannel)
    {
        bool valid = IsValidDistance(distanceMM);
        bool reasonable = valid && IsReasonableRestDistance(distanceMM);

        return "V" + valveNumber +
            " mux=SC" + muxChannel +
            " live=" + distanceMM + "mm " +
            (reasonable ? "OK" : "CHECK SENSOR/MUX");
    }

    void UpdateActiveTofChannels(int tc1, int tc2, int tc3)
    {
        activeValve1TofMuxChannel = Mathf.Clamp(tc1, 0, 7);
        activeValve2TofMuxChannel = Mathf.Clamp(tc2, 0, 7);
        activeValve3TofMuxChannel = Mathf.Clamp(tc3, 0, 7);
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
                    "Flash Assets/trumpal_final_code/trumpal_final_code.ino so Unity receives V/D/S/E/TH fields."
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
                else if (key == "TC1")
                {
                    int.TryParse(value, out latestTC1);
                }
                else if (key == "TC2")
                {
                    int.TryParse(value, out latestTC2);
                }
                else if (key == "TC3")
                {
                    int.TryParse(value, out latestTC3);
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

    public bool SendLine(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        lock (writeLock)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                Debug.LogWarning("Teensy serial is not connected. Not sent: " + command);
                return false;
            }

            try
            {
                serialPort.WriteLine(command);

                if (printOutgoingCommands)
                {
                    Debug.Log("Teensy OUT: " + command);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Teensy serial write issue: " + exception.Message);
                return false;
            }
        }
    }

    [ContextMenu("ToF/Scan Firmware Mux Channels")]
    public void SendTofScan()
    {
        SendLine("TOFSCAN");
    }

    [ContextMenu("ToF/Send Inspector Mux Map")]
    public void SendInspectorTofMap()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);

        SendLine(
            "TOFMAPALL," +
            Mathf.Clamp(hardwarePinout[0].tofMuxChannel, 0, 7) + "," +
            Mathf.Clamp(hardwarePinout[1].tofMuxChannel, 0, 7) + "," +
            Mathf.Clamp(hardwarePinout[2].tofMuxChannel, 0, 7)
        );
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
        calibrationWaitForDataSeconds = Mathf.Clamp(calibrationWaitForDataSeconds, 0f, 10f);
        pressEnterDeltaMM = Mathf.Max(1f, pressEnterDeltaMM);
        minimumPressEnterDeltaMM = Mathf.Clamp(minimumPressEnterDeltaMM, 1f, pressEnterDeltaMM);
        closeSensorPressDeltaFraction = Mathf.Clamp(closeSensorPressDeltaFraction, 0.05f, 0.9f);
        releaseDeltaMM = Mathf.Clamp(releaseDeltaMM, 0.5f, pressEnterDeltaMM - 0.5f);
        postCalibrationReleaseGuardSeconds = Mathf.Clamp(postCalibrationReleaseGuardSeconds, 0f, 2f);
        pressDebounceSeconds = Mathf.Clamp(pressDebounceSeconds, 0f, 0.25f);
        releaseDebounceSeconds = Mathf.Clamp(releaseDebounceSeconds, 0f, 0.25f);
        calibrationRestPercentile = Mathf.Clamp(calibrationRestPercentile, 0.5f, 0.95f);
        minimumCalibrationSamplesPerLane = Mathf.Clamp(minimumCalibrationSamplesPerLane, 1, 200);
        maxReasonableRestDistanceMM = Mathf.Clamp(maxReasonableRestDistanceMM, 1f, 254f);
        restRebaselineDeltaMM = Mathf.Clamp(restRebaselineDeltaMM, 1f, 100f);
        restRebaselineStableSeconds = Mathf.Clamp(restRebaselineStableSeconds, 0f, 2f);
        analogAmountDeadZone = Mathf.Clamp01(analogAmountDeadZone);

        if (!Application.isPlaying)
        {
            ApplyPressedThresholdsFromCalibratedRest();
        }
    }
}
