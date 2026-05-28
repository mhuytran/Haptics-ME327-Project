using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class NoteSpawner : MonoBehaviour
{
    public enum NoteSpawnMode
    {
        RandomDebug,
        SongChart
    }

    [Serializable]
    public class TrumpetFingering
    {
        public string noteName = "F / Bb";
        public bool valve1 = true;
        public bool valve2 = false;
        public bool valve3 = false;
        public float holdDuration = 0f;

        public TrumpetFingering()
        {
        }

        public TrumpetFingering(
            string noteName,
            bool valve1,
            bool valve2,
            bool valve3,
            float holdDuration
        )
        {
            this.noteName = noteName;
            this.valve1 = valve1;
            this.valve2 = valve2;
            this.valve3 = valve3;
            this.holdDuration = holdDuration;
        }

        public bool UsesLane(int lane)
        {
            if (lane == 0) return valve1;
            if (lane == 1) return valve2;
            if (lane == 2) return valve3;
            return false;
        }

        public bool IsOpen()
        {
            return !valve1 && !valve2 && !valve3;
        }
    }

    [Serializable]
    public class SongChart
    {
        public string songTitle = "";
        public string audioFile = "";
        public float bpm = 120f;
        public string timeSignature = "4/4";
        public float leadInSeconds = 0f;
        public string conversionMode = "";
        public SongChartNote[] notes;
    }

    [Serializable]
    public class SongChartNote
    {
        public float time = 0f;
        public float duration = 0f;
        public int[] laneMask;
        public int pitchMidi = 0;
        public string pitchName = "";
        public string type = "tap";
        public bool generatedOpenFill = false;
        public bool generatedGapFill = false;
        public string sourceFile = "";
    }

    [Header("references")]
    public GameObject notePrefab;
    public RhythmGameManager gameManager;

    public Transform spawnPoint1;
    public Transform spawnPoint2;
    public Transform spawnPoint3;

    public Transform valveTarget1;
    public Transform valveTarget2;
    public Transform valveTarget3;

    [Header("hit effect planes")]
    public Transform hitPlane1;
    public Transform hitPlane2;
    public Transform hitPlane3;

    [Header("note timing")]
    public NoteSpawnMode spawnMode = NoteSpawnMode.RandomDebug;
    public float spawnInterval = 2.0f;
    public float noteTravelTime = 1.5f;
    public bool waitForPreviousFingeringToResolve = true;

    [Header("song chart mode")]
    [TextArea(4, 7)]
    public string songModeGuide =
        "RandomDebug keeps spawning generated fingerings. SongChart loads From-The-Start from Resources/TrumpetCharts, plays the audio, and spawns the PDF/OMR note chart in sync. Keyboard testing still uses A/S/D for valves 1/2/3.";
    public TextAsset songChartJson;
    public string songChartResourcePath = "TrumpetCharts/From-The-Start";
    public AudioSource songAudioSource;
    public AudioClip songAudioClip;
    public string songAudioResourcePath = "TrumpetCharts/From-The-Start";
    public bool autoStartSong = true;
    public bool allowSongKeyboardControls = true;
    public Key startPauseSongKey = Key.Space;
    public Key restartSongKey = Key.R;
    public bool useChartLeadInSeconds = true;
    public float manualLeadInSeconds = 3.0f;
    public float additionalChartOffsetSeconds = 0f;
    public bool useSongDurationsAsHolds = true;
    public float minimumSongHoldDuration = 0.45f;
    public bool skipVeryLateSongNotes = true;
    public float lateSongNoteSkipSeconds = 0.35f;
    public bool loopSongChart = false;
    public string loadedSongTitle = "";
    public int loadedSongNoteCount = 0;
    public int nextSongNoteIndex = 0;
    public float currentSongTime = 0f;
    public string currentSongStatus = "Song chart not loaded.";

    [Header("haptic cue tuning")]
    public bool useDistanceBasedPreCue = true;
    public float preCueDistanceFromTarget = 1.0f;
    public float fallbackPreCueLeadTime = 0.85f;

    [Header("lane colors")]
    public Color lane1Color = Color.green;
    public Color lane2Color = new Color(0.1882353f, 0.627451f, 1.0f, 1.0f);
    public Color lane3Color = Color.red;

    [Header("lane color targets")]
    public Material lane1LineMaterial;
    public Material lane2LineMaterial;
    public Material lane3LineMaterial;

    [Header("hold-note safety")]
    public float holdSafetyBuffer = 0.15f;

    [Header("trumpet fingering generation")]
    public bool useTrumpetFingerings = true;
    public bool randomizeFingerings = true;
    public string lastSpawnedFingering = "";
    public TrumpetFingering[] validFingerings = new TrumpetFingering[]
    {
        new TrumpetFingering("F / Bb - 1", true, false, false, 0f),
        new TrumpetFingering("F# / B - 2", false, true, false, 0f),
        new TrumpetFingering("E / A - 1+2", true, true, false, 0.8f),
        new TrumpetFingering("D / G - 1+3", true, false, true, 0f),
        new TrumpetFingering("Eb / Ab - 2+3", false, true, true, 1.0f),
        new TrumpetFingering("C# / F# - 1+2+3", true, true, true, 0f),
        new TrumpetFingering("C / G - open", false, false, false, 0f)
    };

    private float spawnTimer = 0f;
    private int patternIndex = 0;
    private int nextFingeringGroupId = 1;
    private int lastRandomFingeringIndex = -1;

    private float[] laneHoldBusyUntil = new float[3];
    private SongChart loadedSongChart;
    private bool songStarted = false;
    private bool songPaused = false;
    private float fallbackSongStartTime = 0f;

    private int[] lanePattern = new int[]
    {
        0, 1, 2, 0,
        2, 1, 0, 1,
        2, 0, 1, 2,
        1, 0, 2, 1
    };

    private float[] holdDurationPattern = new float[]
    {
        0.0f, 0.8f, 0.0f, 1.2f,
        0.0f, 1.6f, 0.0f, 0.6f,
        2.0f, 0.0f, 1.0f, 0.0f,
        1.4f, 0.0f, 0.7f, 0.0f
    };

    void Start()
    {
        if (gameManager == null)
        {
            gameManager = UnityEngine.Object.FindAnyObjectByType<RhythmGameManager>();
        }

        ApplyLanePaletteToMaterials();
        LoadSongChartIfNeeded();
    }

    void Update()
    {
        if (spawnMode == NoteSpawnMode.SongChart)
        {
            UpdateSongChartMode();
            return;
        }

        spawnTimer += Time.deltaTime;

        if (waitForPreviousFingeringToResolve &&
            gameManager != null &&
            gameManager.HasActiveNotes())
        {
            return;
        }

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnNextNote();
        }
    }

    void SpawnNextNote()
    {
        if (useTrumpetFingerings)
        {
            SpawnNextTrumpetFingering();
            return;
        }

        SpawnLegacySingleLaneNote();
    }

    void SpawnLegacySingleLaneNote()
    {
        int lane = lanePattern[patternIndex];
        float requestedHoldDuration = holdDurationPattern[patternIndex];

        patternIndex = (patternIndex + 1) % lanePattern.Length;

        float targetHitTime = Time.time + noteTravelTime;

        bool requestedHold = requestedHoldDuration > 0.05f;
        bool overlapsExistingHold = requestedHold && targetHitTime < laneHoldBusyUntil[lane];

        float finalHoldDuration = overlapsExistingHold ? 0f : requestedHoldDuration;
        bool isHoldNote = finalHoldDuration > 0.05f;

        if (isHoldNote)
        {
            laneHoldBusyUntil[lane] = targetHitTime + finalHoldDuration + holdSafetyBuffer;
        }

        int groupId = nextFingeringGroupId++;
        SpawnLaneNote(
            lane,
            targetHitTime,
            finalHoldDuration,
            groupId,
            1 << lane,
            "Valve " + (lane + 1)
        );
    }

    void SpawnNextTrumpetFingering()
    {
        EnsureValidFingerings();

        TrumpetFingering fingering = GetNextFingering();

        if (fingering == null)
        {
            return;
        }

        float targetHitTime = Time.time + noteTravelTime;
        float requestedHoldDuration = Mathf.Max(0f, fingering.holdDuration);
        int requiredValveMask = FingeringToMask(fingering);
        bool requestedHold = requestedHoldDuration > 0.05f;
        bool overlapsExistingHold = requestedHold && FingeringOverlapsExistingHold(fingering, targetHitTime);

        float finalHoldDuration = overlapsExistingHold ? 0f : requestedHoldDuration;
        bool isHoldNote = finalHoldDuration > 0.05f;

        if (isHoldNote)
        {
            MarkFingeringHoldBusy(fingering, targetHitTime, finalHoldDuration);
        }

        bool spawnedAnyLane = false;
        int groupId = nextFingeringGroupId++;

        for (int lane = 0; lane < 3; lane++)
        {
            if (!fingering.UsesLane(lane))
            {
                continue;
            }

            SpawnLaneNote(
                lane,
                targetHitTime,
                finalHoldDuration,
                groupId,
                requiredValveMask,
                fingering.noteName
            );
            spawnedAnyLane = true;
        }

        if (spawnedAnyLane)
        {
            lastSpawnedFingering = fingering.noteName;
        }
    }

    void SpawnLaneNote(
        int lane,
        float targetHitTime,
        float holdDuration,
        int groupId,
        int requiredValveMask,
        string fingeringName
    )
    {
        Transform spawnPoint = GetSpawnPoint(lane);
        Transform valveTarget = GetValveTarget(lane);
        Transform hitPlane = GetHitPlane(lane);

        bool isHoldNote = holdDuration > 0.05f;

        GameObject noteObject = Instantiate(
            notePrefab,
            spawnPoint.position,
            notePrefab.transform.rotation
        );

        Color noteColor = GetLaneColor(lane);

        NoteGlowPulse glowPulse = noteObject.GetComponent<NoteGlowPulse>();
        if (glowPulse != null)
        {
            glowPulse.SetColor(noteColor);
        }

        FlyingNote note = noteObject.GetComponent<FlyingNote>();

        if (note == null)
        {
            note = noteObject.AddComponent<FlyingNote>();
        }

        note.holdTailColor = noteColor;
        note.holdTailWidth = isHoldNote ? 0.08f : 0.0f;
        note.SetNoteColor(noteColor);
        note.useDistanceBasedPreCue = useDistanceBasedPreCue;
        note.preCueDistanceFromTarget = preCueDistanceFromTarget;
        note.preCueLeadTime = fallbackPreCueLeadTime;

        Vector3 hitPosition = hitPlane != null ? hitPlane.position : valveTarget.position;
        Quaternion hitRotation = hitPlane != null ? hitPlane.rotation : valveTarget.rotation;

        note.Initialize(
            lane,
            spawnPoint.position,
            valveTarget.position,
            hitPosition,
            hitRotation,
            targetHitTime,
            gameManager,
            isHoldNote,
            holdDuration
        );

        note.SetFingeringGroup(groupId, requiredValveMask, fingeringName);

        gameManager.RegisterNote(note);
    }

    int FingeringToMask(TrumpetFingering fingering)
    {
        int mask = 0;

        for (int lane = 0; lane < 3; lane++)
        {
            if (fingering.UsesLane(lane))
            {
                mask |= 1 << lane;
            }
        }

        return mask;
    }

    TrumpetFingering GetNextFingering()
    {
        int fingeringCount = validFingerings.Length;
        int maxAttempts = Mathf.Max(1, fingeringCount * 2);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int index;

            if (randomizeFingerings)
            {
                index = UnityEngine.Random.Range(0, fingeringCount);

                if (fingeringCount > 1 && index == lastRandomFingeringIndex)
                {
                    continue;
                }
            }
            else
            {
                index = patternIndex;
                patternIndex = (patternIndex + 1) % fingeringCount;
            }

            TrumpetFingering fingering = validFingerings[index];

            if (!IsSpawnableFingering(fingering))
            {
                continue;
            }

            lastRandomFingeringIndex = index;
            return fingering;
        }

        return null;
    }

    bool IsSpawnableFingering(TrumpetFingering fingering)
    {
        if (fingering == null)
        {
            return false;
        }

        // The current lane UI only renders pressed valves, so open notes are documented
        // in the list but skipped until there is an open-note visual target.
        return !fingering.IsOpen();
    }

    void UpdateSongChartMode()
    {
        LoadSongChartIfNeeded();
        HandleSongKeyboardControls();

        if (!songStarted && autoStartSong && CanStartSongNow())
        {
            StartSong();
        }

        if (!songStarted || songPaused || loadedSongChart == null)
        {
            return;
        }

        currentSongTime = GetSongTime();

        if (songAudioSource != null &&
            songAudioSource.clip != null &&
            !songAudioSource.isPlaying &&
            currentSongTime >= songAudioSource.clip.length - 0.05f)
        {
            if (loopSongChart)
            {
                RestartSong();
            }
            else
            {
                currentSongStatus = "Song finished.";
                songStarted = false;
            }

            return;
        }

        SongChartNote[] notes = loadedSongChart.notes;

        if (notes == null)
        {
            return;
        }

        while (nextSongNoteIndex < notes.Length)
        {
            SongChartNote chartNote = notes[nextSongNoteIndex];
            float hitSongTime = GetChartNoteHitSongTime(chartNote);
            float timeUntilHit = hitSongTime - currentSongTime;

            if (skipVeryLateSongNotes && timeUntilHit < -lateSongNoteSkipSeconds)
            {
                nextSongNoteIndex++;
                continue;
            }

            if (timeUntilHit > noteTravelTime)
            {
                break;
            }

            SpawnSongChartNote(chartNote, hitSongTime, timeUntilHit);
            nextSongNoteIndex++;
        }
    }

    void HandleSongKeyboardControls()
    {
        if (!allowSongKeyboardControls || Keyboard.current == null)
        {
            return;
        }

        if (WasKeyPressed(startPauseSongKey))
        {
            if (!songStarted)
            {
                StartSong();
            }
            else if (songPaused)
            {
                ResumeSong();
            }
            else
            {
                PauseSong();
            }
        }

        if (WasKeyPressed(restartSongKey))
        {
            RestartSong();
        }
    }

    bool WasKeyPressed(Key key)
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl keyControl = Keyboard.current[key];
        return keyControl != null && keyControl.wasPressedThisFrame;
    }

    bool CanStartSongNow()
    {
        return TeensySerialInput.Instance == null || !TeensySerialInput.Instance.isCalibrating;
    }

    [ContextMenu("Song/Start Song Chart")]
    public void StartSong()
    {
        LoadSongChartIfNeeded();

        if (loadedSongChart == null)
        {
            currentSongStatus = "Cannot start: no song chart loaded.";
            return;
        }

        EnsureSongAudioSource();

        nextSongNoteIndex = 0;
        songStarted = true;
        songPaused = false;
        fallbackSongStartTime = Time.time;

        if (songAudioSource != null && songAudioSource.clip != null)
        {
            songAudioSource.Stop();
            songAudioSource.time = 0f;
            songAudioSource.Play();
        }

        currentSongStatus = "Playing " + loadedSongTitle;
    }

    [ContextMenu("Song/Pause Song Chart")]
    public void PauseSong()
    {
        if (!songStarted)
        {
            return;
        }

        songPaused = true;

        if (songAudioSource != null)
        {
            songAudioSource.Pause();
        }

        currentSongStatus = "Paused " + loadedSongTitle;
    }

    [ContextMenu("Song/Resume Song Chart")]
    public void ResumeSong()
    {
        if (!songStarted)
        {
            return;
        }

        songPaused = false;

        if (songAudioSource != null && songAudioSource.clip != null)
        {
            songAudioSource.UnPause();
        }
        else
        {
            fallbackSongStartTime = Time.time - currentSongTime;
        }

        currentSongStatus = "Playing " + loadedSongTitle;
    }

    [ContextMenu("Song/Restart Song Chart")]
    public void RestartSong()
    {
        if (gameManager != null)
        {
            gameManager.ClearActiveNotes();
        }

        StartSong();
    }

    void LoadSongChartIfNeeded()
    {
        if (loadedSongChart != null)
        {
            return;
        }

        if (songChartJson == null && !string.IsNullOrWhiteSpace(songChartResourcePath))
        {
            songChartJson = Resources.Load<TextAsset>(songChartResourcePath);
        }

        if (songChartJson == null)
        {
            currentSongStatus = "No song chart JSON assigned or found at Resources/" + songChartResourcePath;
            return;
        }

        loadedSongChart = JsonUtility.FromJson<SongChart>(songChartJson.text);

        if (loadedSongChart == null || loadedSongChart.notes == null)
        {
            currentSongStatus = "Song chart JSON could not be parsed.";
            loadedSongChart = null;
            return;
        }

        Array.Sort(
            loadedSongChart.notes,
            (a, b) => GetChartNoteHitSongTime(a).CompareTo(GetChartNoteHitSongTime(b))
        );

        loadedSongTitle = string.IsNullOrEmpty(loadedSongChart.songTitle)
            ? songChartJson.name
            : loadedSongChart.songTitle;
        loadedSongNoteCount = loadedSongChart.notes.Length;
        currentSongStatus = "Loaded " + loadedSongTitle + " (" + loadedSongNoteCount + " notes).";

        EnsureSongAudioSource();
    }

    void EnsureSongAudioSource()
    {
        if (songAudioClip == null && !string.IsNullOrWhiteSpace(songAudioResourcePath))
        {
            songAudioClip = Resources.Load<AudioClip>(songAudioResourcePath);
        }

        if (songAudioSource == null)
        {
            songAudioSource = GetComponent<AudioSource>();
        }

        if (songAudioSource == null)
        {
            songAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (songAudioSource.clip == null && songAudioClip != null)
        {
            songAudioSource.clip = songAudioClip;
        }

        songAudioSource.playOnAwake = false;
    }

    float GetSongTime()
    {
        if (songAudioSource != null && songAudioSource.clip != null)
        {
            return songAudioSource.time;
        }

        return Mathf.Max(0f, Time.time - fallbackSongStartTime);
    }

    float GetChartNoteHitSongTime(SongChartNote chartNote)
    {
        float leadIn = useChartLeadInSeconds && loadedSongChart != null
            ? loadedSongChart.leadInSeconds
            : manualLeadInSeconds;

        return Mathf.Max(0f, leadIn + additionalChartOffsetSeconds + chartNote.time);
    }

    void SpawnSongChartNote(SongChartNote chartNote, float hitSongTime, float timeUntilHit)
    {
        int requiredValveMask = LaneArrayToValveMask(chartNote.laneMask);

        if (requiredValveMask == 0)
        {
            return;
        }

        int groupId = nextFingeringGroupId++;
        float targetHitTime = Time.time + Mathf.Max(0.02f, timeUntilHit);
        float holdDuration = GetSongHoldDuration(chartNote);
        string fingeringName = BuildSongFingeringName(chartNote, requiredValveMask);

        for (int lane = 0; lane < 3; lane++)
        {
            if ((requiredValveMask & (1 << lane)) == 0)
            {
                continue;
            }

            SpawnLaneNote(
                lane,
                targetHitTime,
                holdDuration,
                groupId,
                requiredValveMask,
                fingeringName
            );
        }

        lastSpawnedFingering = fingeringName;
        currentSongStatus =
            "Playing " + loadedSongTitle +
            " t=" + currentSongTime.ToString("0.00") +
            " next=" + nextSongNoteIndex + "/" + loadedSongNoteCount;
    }

    int LaneArrayToValveMask(int[] laneMask)
    {
        int mask = 0;

        if (laneMask == null)
        {
            return mask;
        }

        for (int i = 0; i < laneMask.Length; i++)
        {
            int laneIndex = laneMask[i] - 1;

            if (laneIndex >= 0 && laneIndex < 3)
            {
                mask |= 1 << laneIndex;
            }
        }

        return mask;
    }

    float GetSongHoldDuration(SongChartNote chartNote)
    {
        if (!useSongDurationsAsHolds)
        {
            return 0f;
        }

        float duration = Mathf.Max(0f, chartNote.duration);

        if (duration < minimumSongHoldDuration)
        {
            return 0f;
        }

        return duration;
    }

    string BuildSongFingeringName(SongChartNote chartNote, int requiredValveMask)
    {
        string noteName = string.IsNullOrEmpty(chartNote.pitchName)
            ? "Song note"
            : chartNote.pitchName;

        return noteName + " - " + ValveMaskToLabel(requiredValveMask);
    }

    string ValveMaskToLabel(int valveMask)
    {
        if (valveMask == 0)
        {
            return "open";
        }

        string label = "";

        for (int lane = 0; lane < 3; lane++)
        {
            if ((valveMask & (1 << lane)) == 0)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(label))
            {
                label += "+";
            }

            label += (lane + 1).ToString();
        }

        return label;
    }

    bool FingeringOverlapsExistingHold(TrumpetFingering fingering, float targetHitTime)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if (fingering.UsesLane(lane) && targetHitTime < laneHoldBusyUntil[lane])
            {
                return true;
            }
        }

        return false;
    }

    void MarkFingeringHoldBusy(TrumpetFingering fingering, float targetHitTime, float holdDuration)
    {
        float busyUntil = targetHitTime + holdDuration + holdSafetyBuffer;

        for (int lane = 0; lane < 3; lane++)
        {
            if (fingering.UsesLane(lane))
            {
                laneHoldBusyUntil[lane] = busyUntil;
            }
        }
    }

    void EnsureValidFingerings()
    {
        if (validFingerings == null || validFingerings.Length == 0)
        {
            validFingerings = new TrumpetFingering[]
            {
                new TrumpetFingering("F / Bb - 1", true, false, false, 0f),
                new TrumpetFingering("F# / B - 2", false, true, false, 0f),
                new TrumpetFingering("E / A - 1+2", true, true, false, 0.8f),
                new TrumpetFingering("D / G - 1+3", true, false, true, 0f),
                new TrumpetFingering("Eb / Ab - 2+3", false, true, true, 1.0f),
                new TrumpetFingering("C# / F# - 1+2+3", true, true, true, 0f)
            };
        }
    }

    Transform GetSpawnPoint(int lane)
    {
        if (lane == 0) return spawnPoint1;
        if (lane == 1) return spawnPoint2;
        return spawnPoint3;
    }

    Transform GetValveTarget(int lane)
    {
        if (lane == 0) return valveTarget1;
        if (lane == 1) return valveTarget2;
        return valveTarget3;
    }

    Transform GetHitPlane(int lane)
    {
        if (lane == 0) return hitPlane1 != null ? hitPlane1 : valveTarget1;
        if (lane == 1) return hitPlane2 != null ? hitPlane2 : valveTarget2;
        return hitPlane3 != null ? hitPlane3 : valveTarget3;
    }

    Color GetLaneColor(int lane)
    {
        if (lane == 0) return lane1Color;
        if (lane == 1) return lane2Color;
        return lane3Color;
    }

    void ApplyLanePaletteToMaterials()
    {
        ApplyMaterialColor(lane1LineMaterial, lane1Color);
        ApplyMaterialColor(lane2LineMaterial, lane2Color);
        ApplyMaterialColor(lane3LineMaterial, lane3Color);
    }

    void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        color.a = 1f;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    void OnValidate()
    {
        ApplyLanePaletteToMaterials();
    }
}
