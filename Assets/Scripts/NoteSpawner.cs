using UnityEngine;

public class NoteSpawner : MonoBehaviour
{
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

    [Header("lane colors")]
    public Color lane1Color = Color.green;
    public Color lane2Color = Color.cyan;
    public Color lane3Color = Color.red;

    [Header("hold-note safety")]
    public float holdSafetyBuffer = 0.15f;

    private float spawnTimer = 0f;
    private int patternIndex = 0;

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
        int lane = lanePattern[patternIndex];
        float requestedHoldDuration = holdDurationPattern[patternIndex];

        patternIndex = (patternIndex + 1) % lanePattern.Length;

        Transform spawnPoint = GetSpawnPoint(lane);
        Transform valveTarget = GetValveTarget(lane);
        Transform hitPlane = GetHitPlane(lane);

        float targetHitTime = Time.time + noteTravelTime;

        bool requestedHold = requestedHoldDuration > 0.05f;
        bool overlapsExistingHold = requestedHold && targetHitTime < laneHoldBusyUntil[lane];

        float finalHoldDuration = overlapsExistingHold ? 0f : requestedHoldDuration;
        bool isHoldNote = finalHoldDuration > 0.05f;

        if (isHoldNote)
        {
            laneHoldBusyUntil[lane] = targetHitTime + finalHoldDuration + holdSafetyBuffer;
        }

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
            finalHoldDuration
        );

        gameManager.RegisterNote(note);
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