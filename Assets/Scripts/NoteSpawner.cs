using System;
using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
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
    public float spawnInterval = 0.8f;
    public float noteTravelTime = 1.5f;

    [Header("haptic cue tuning")]
    public bool useDistanceBasedPreCue = true;
    public float preCueDistanceFromTarget = 0.45f;
    public float fallbackPreCueLeadTime = 0.35f;

    [Header("lane colors")]
    public Color lane1Color = Color.green;
    public Color lane2Color = Color.cyan;
    public Color lane3Color = Color.red;

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
    private int lastRandomFingeringIndex = -1;

    private float[] laneHoldBusyUntil = new float[3];

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

    void Update()
    {
        spawnTimer += Time.deltaTime;

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

        SpawnLaneNote(lane, targetHitTime, finalHoldDuration);
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
        bool requestedHold = requestedHoldDuration > 0.05f;
        bool overlapsExistingHold = requestedHold && FingeringOverlapsExistingHold(fingering, targetHitTime);

        float finalHoldDuration = overlapsExistingHold ? 0f : requestedHoldDuration;
        bool isHoldNote = finalHoldDuration > 0.05f;

        if (isHoldNote)
        {
            MarkFingeringHoldBusy(fingering, targetHitTime, finalHoldDuration);
        }

        bool spawnedAnyLane = false;

        for (int lane = 0; lane < 3; lane++)
        {
            if (!fingering.UsesLane(lane))
            {
                continue;
            }

            SpawnLaneNote(lane, targetHitTime, finalHoldDuration);
            spawnedAnyLane = true;
        }

        if (spawnedAnyLane)
        {
            lastSpawnedFingering = fingering.noteName;
        }
    }

    void SpawnLaneNote(int lane, float targetHitTime, float holdDuration)
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

        gameManager.RegisterNote(note);
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
}
