using UnityEngine;

public class FlyingNote : MonoBehaviour
{
    public int laneIndex;
    public float targetHitTime;
    public bool resolved = false;

    [Header("fingering group")]
    public int fingeringGroupId = -1;
    public int requiredValveMask = 0;
    public string fingeringName = "";
    public Color noteColor = Color.white;

    [Header("pre-cue")]
    public bool useDistanceBasedPreCue = true;
    public float preCueDistanceFromTarget = 1.0f;
    public float preCueLeadTime = 0.85f;
    private bool preCueSent = false;

    [Header("hold note")]
    public bool isHoldNote = false;
    public float holdDuration = 0f;
    public bool holdStarted = false;

    [Header("hold visual")]
    public float holdTailWidth = 0.06f;
    public Color holdTailColor = Color.cyan;

    private Vector3 spawnPosition;
    private Vector3 targetPosition;
    private Vector3 hitEffectPosition;
    private Quaternion hitEffectRotation;

    private float spawnTime;

    private RhythmGameManager gameManager;
    private LineRenderer holdTail;
    private FlatNoteSmashEffect activeHoldSmash;

    public void Initialize(
        int lane,
        Vector3 spawn,
        Vector3 target,
        float hitTime,
        RhythmGameManager manager,
        bool hold,
        float duration
    )
    {
        Initialize(
            lane,
            spawn,
            target,
            target,
            transform.rotation,
            hitTime,
            manager,
            hold,
            duration
        );
    }

    public void Initialize(
        int lane,
        Vector3 spawn,
        Vector3 target,
        Vector3 hitPosition,
        Quaternion hitRotation,
        float hitTime,
        RhythmGameManager manager,
        bool hold,
        float duration
    )
    {
        laneIndex = lane;
        fingeringGroupId = -1;
        requiredValveMask = 1 << lane;
        fingeringName = "Valve " + (lane + 1);

        spawnPosition = spawn;
        targetPosition = target;
        hitEffectPosition = hitPosition;
        hitEffectRotation = hitRotation;

        targetHitTime = hitTime;
        spawnTime = Time.time;
        gameManager = manager;

        isHoldNote = hold;
        holdDuration = duration;

        transform.position = spawnPosition;

        if (isHoldNote)
        {
            CreateHoldTail();
        }
    }

    void Update()
    {
        if (gameManager == null || resolved)
        {
            return;
        }

        TrySendPreCue();

        float travelFraction = Mathf.InverseLerp(spawnTime, targetHitTime, Time.time);
        travelFraction = Mathf.Clamp01(travelFraction);

        transform.position = Vector3.Lerp(spawnPosition, targetPosition, travelFraction);

        if (isHoldNote)
        {
            UpdateHoldTail();

            if (!holdStarted && Time.time > targetHitTime + gameManager.missWindow)
            {
                Miss();
                return;
            }

            if (holdStarted)
            {
                bool stillHolding = ValveInputState.GetValve(laneIndex);

                if (gameManager != null)
                {
                    stillHolding = gameManager.IsFingeringHeld(this);
                }

                float holdEndTime = targetHitTime + holdDuration;

                if (!stillHolding && Time.time < holdEndTime)
                {
                    Miss();
                    return;
                }

                if (Time.time >= holdEndTime)
                {
                    CompleteHold();
                    return;
                }
            }
        }
        else
        {
            if (Time.time > targetHitTime + gameManager.missWindow)
            {
                Miss();
                return;
            }
        }
    }

    void TrySendPreCue()
    {
        if (preCueSent)
        {
            return;
        }

        bool shouldSendPreCue = false;

        if (useDistanceBasedPreCue)
        {
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
            shouldSendPreCue = distanceToTarget <= preCueDistanceFromTarget;
        }
        else
        {
            float timeUntilHit = targetHitTime - Time.time;
            shouldSendPreCue = timeUntilHit <= preCueLeadTime && timeUntilHit > 0f;
        }

        if (shouldSendPreCue)
        {
            preCueSent = true;

            if (gameManager != null)
            {
                gameManager.OnNotePreCue(this);
            }
        }
    }

    public float GetDistanceToTarget()
    {
        return Vector3.Distance(transform.position, targetPosition);
    }

    public float GetTimeUntilHit()
    {
        float timeUntilHit = targetHitTime - Time.time;
        return Mathf.Max(0f, timeUntilHit);
    }

    public void SetFingeringGroup(int groupId, int valveMask, string name)
    {
        fingeringGroupId = groupId;
        requiredValveMask = valveMask == 0 ? 1 << laneIndex : valveMask;
        fingeringName = string.IsNullOrEmpty(name) ? "Valve " + (laneIndex + 1) : name;
    }

    public void SetNoteColor(Color color)
    {
        noteColor = color;
    }

    public int GetRequiredValveMask()
    {
        if (requiredValveMask != 0)
        {
            return requiredValveMask;
        }

        return 1 << laneIndex;
    }

    public void ResolveTapHit(string rating)
    {
        if (resolved)
        {
            return;
        }

        TriggerTapSmash(IsPerfectRating(rating));

        resolved = true;
        gameManager.UnregisterNote(this);

        Destroy(gameObject);
    }

    public void ResolveHoldCompleteFromGroup()
    {
        if (resolved)
        {
            return;
        }

        StopHoldSmash();
        TriggerHoldEndSmash();

        resolved = true;
        gameManager.UnregisterNote(this);

        Destroy(gameObject);
    }

    public void ResolveMissFromGroup()
    {
        if (resolved)
        {
            return;
        }

        StopHoldSmash();

        resolved = true;
        gameManager.UnregisterNote(this);

        Destroy(gameObject);
    }

    public void StartHold(string rating)
    {
        if (resolved || holdStarted)
        {
            return;
        }

        TriggerHoldSmash(IsPerfectRating(rating));

        holdStarted = true;
    }

    void CompleteHold()
    {
        if (resolved)
        {
            return;
        }

        StopHoldSmash();
        TriggerHoldEndSmash();

        resolved = true;
        gameManager.OnHoldCompleted(this);

        Destroy(gameObject);
    }

    void Miss()
    {
        if (resolved)
        {
            return;
        }

        StopHoldSmash();

        resolved = true;
        gameManager.OnNoteMissed(this);

        Destroy(gameObject);
    }

    bool IsPerfectRating(string rating)
    {
        if (string.IsNullOrEmpty(rating))
        {
            return false;
        }

        return rating.ToUpper().Contains("PERFECT");
    }

    void TriggerTapSmash(bool perfectHit)
    {
        NoteGlowPulse glowPulse = GetComponent<NoteGlowPulse>();

        if (glowPulse == null)
        {
            return;
        }

        glowPulse.PlayTapSmash(hitEffectPosition, hitEffectRotation, perfectHit);
    }

    void TriggerHoldSmash(bool perfectHit)
    {
        NoteGlowPulse glowPulse = GetComponent<NoteGlowPulse>();

        if (glowPulse == null)
        {
            return;
        }

        activeHoldSmash = glowPulse.StartHoldSmash(
            hitEffectPosition,
            hitEffectRotation,
            perfectHit
        );
    }

    void TriggerHoldEndSmash()
    {
        NoteGlowPulse glowPulse = GetComponent<NoteGlowPulse>();

        if (glowPulse == null)
        {
            return;
        }

        glowPulse.PlayHoldEndSmash(hitEffectPosition, hitEffectRotation);
    }

    void StopHoldSmash()
    {
        if (activeHoldSmash != null)
        {
            activeHoldSmash.StopEffect();
            activeHoldSmash = null;
        }
    }

    void CreateHoldTail()
    {
        holdTail = gameObject.AddComponent<LineRenderer>();
        holdTail.useWorldSpace = true;
        holdTail.positionCount = 2;
        holdTail.startWidth = holdTailWidth;
        holdTail.endWidth = holdTailWidth;
        holdTail.numCapVertices = 8;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = holdTailColor;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", holdTailColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", holdTailColor);
        }

        holdTail.material = material;
    }

    void UpdateHoldTail()
    {
        if (holdTail == null)
        {
            return;
        }

        float delayedTime = Time.time - holdDuration;
        float tailFraction = Mathf.InverseLerp(spawnTime, targetHitTime, delayedTime);
        tailFraction = Mathf.Clamp01(tailFraction);

        Vector3 tailPosition = Vector3.Lerp(spawnPosition, targetPosition, tailFraction);

        holdTail.SetPosition(0, transform.position);
        holdTail.SetPosition(1, tailPosition);
    }
}
