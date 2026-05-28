using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeTrumpetDancer : MonoBehaviour
{
    private static readonly List<HomeTrumpetDancer> activeDrivers = new List<HomeTrumpetDancer>();

    [Header("scene safety")]
    public string allowedSceneName = "HomeScene";
    [Tooltip("Off for hardware demos so valves do not animate unless real input or an explicit test moves them.")]
    public bool autoAnimateValves = false;

    [Header("valve references")]
    public Transform valve1;
    public Transform valve2;
    public Transform valve3;

    [Header("disable conflicting gameplay scripts")]
    public bool disableGameplayValveAnimators = true;

    [Header("render safety")]
    public bool repairValveRenderersOnStart = true;

    [Header("valve press direction")]
    public Vector3 localPressDirection = new Vector3(0f, -1f, 0f);
    public float pressDepth = 0.35f;

    [Header("timing")]
    public float transitionTime = 0.18f;
    public float holdTime = 0.75f;
    public float restTime = 0.18f;

    [Header("motion shape")]
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 valve1Rest;
    private Vector3 valve2Rest;
    private Vector3 valve3Rest;

    private Vector3 valve1Start;
    private Vector3 valve2Start;
    private Vector3 valve3Start;

    private Vector3 valve1Target;
    private Vector3 valve2Target;
    private Vector3 valve3Target;

    private int patternIndex = 0;
    private float stateTimer = 0f;
    private bool hasCachedRestPositions = false;
    private bool restoreRestPositionsOnDisable = true;

    private enum State
    {
        MovingToPattern,
        HoldingPattern,
        MovingToRest,
        Resting
    }

    private State state = State.MovingToPattern;

    private readonly bool[][] patterns = new bool[][]
    {
        new bool[] { false, false, false }, // open
        new bool[] { true,  false, false }, // valve 1
        new bool[] { false, true,  false }, // valve 2
        new bool[] { false, false, true  }, // valve 3
        new bool[] { true,  true,  false }, // valves 1 + 2
        new bool[] { false, true,  true  }, // valves 2 + 3
        new bool[] { true,  false, true  }, // valves 1 + 3
        new bool[] { true,  true,  true  }, // all valves
        new bool[] { false, true,  false }, // valve 2
        new bool[] { true,  false, false }  // valve 1
    };

    void Start()
    {
        if (SceneManager.GetActiveScene().name != allowedSceneName)
        {
            restoreRestPositionsOnDisable = false;
            enabled = false;
            return;
        }

        if (valve1 == null || valve2 == null || valve3 == null)
        {
            restoreRestPositionsOnDisable = false;
            enabled = false;
            return;
        }

        CacheRestPositions();

        if (repairValveRenderersOnStart)
        {
            RepairValveRenderers();
        }

        if (!autoAnimateValves)
        {
            enabled = false;
            return;
        }

        if (!TryClaimValveDriver())
        {
            restoreRestPositionsOnDisable = false;
            enabled = false;
            return;
        }

        if (disableGameplayValveAnimators)
        {
            DisableConflictingValveScripts();
        }

        localPressDirection = localPressDirection.normalized;

        BeginMoveToPattern();
    }

    bool TryClaimValveDriver()
    {
        for (int i = activeDrivers.Count - 1; i >= 0; i--)
        {
            HomeTrumpetDancer driver = activeDrivers[i];

            if (driver == null || !driver.isActiveAndEnabled)
            {
                activeDrivers.RemoveAt(i);
                continue;
            }

            if (SharesAnyValveWith(driver))
            {
                Debug.LogWarning(
                    "Disabling duplicate HomeTrumpetDancer on " + name +
                    " because " + driver.name + " already drives this trumpet rig."
                );
                return false;
            }
        }

        activeDrivers.Add(this);
        return true;
    }

    bool SharesAnyValveWith(HomeTrumpetDancer other)
    {
        if (other == null)
        {
            return false;
        }

        return MatchesValve(other.valve1) ||
            MatchesValve(other.valve2) ||
            MatchesValve(other.valve3);
    }

    bool MatchesValve(Transform otherValve)
    {
        if (otherValve == null)
        {
            return false;
        }

        return valve1 == otherValve ||
            valve2 == otherValve ||
            valve3 == otherValve;
    }

    void Update()
    {
        if (valve1 == null || valve2 == null || valve3 == null)
        {
            return;
        }

        stateTimer += Time.unscaledDeltaTime;

        if (state == State.MovingToPattern)
        {
            AnimateTowardTargets();

            if (stateTimer >= transitionTime)
            {
                SnapToTargets();
                stateTimer = 0f;
                state = State.HoldingPattern;
            }
        }
        else if (state == State.HoldingPattern)
        {
            SnapToTargets();

            if (stateTimer >= holdTime)
            {
                BeginMoveToRest();
            }
        }
        else if (state == State.MovingToRest)
        {
            AnimateTowardTargets();

            if (stateTimer >= transitionTime)
            {
                SnapToTargets();
                stateTimer = 0f;
                state = State.Resting;
            }
        }
        else if (state == State.Resting)
        {
            SnapToTargets();

            if (stateTimer >= restTime)
            {
                patternIndex = (patternIndex + 1) % patterns.Length;
                BeginMoveToPattern();
            }
        }
    }

    void CacheRestPositions()
    {
        valve1Rest = valve1.localPosition;
        valve2Rest = valve2.localPosition;
        valve3Rest = valve3.localPosition;
        hasCachedRestPositions = true;
    }

    void BeginMoveToPattern()
    {
        state = State.MovingToPattern;
        stateTimer = 0f;

        CacheCurrentAsStart();

        bool[] pattern = patterns[patternIndex];

        valve1Target = GetTargetPosition(valve1Rest, pattern[0]);
        valve2Target = GetTargetPosition(valve2Rest, pattern[1]);
        valve3Target = GetTargetPosition(valve3Rest, pattern[2]);
    }

    void BeginMoveToRest()
    {
        state = State.MovingToRest;
        stateTimer = 0f;

        CacheCurrentAsStart();

        valve1Target = valve1Rest;
        valve2Target = valve2Rest;
        valve3Target = valve3Rest;
    }

    void CacheCurrentAsStart()
    {
        valve1Start = valve1.localPosition;
        valve2Start = valve2.localPosition;
        valve3Start = valve3.localPosition;
    }

    Vector3 GetTargetPosition(Vector3 restPosition, bool pressed)
    {
        if (!pressed)
        {
            return restPosition;
        }

        return restPosition + localPressDirection * pressDepth;
    }

    void AnimateTowardTargets()
    {
        float t = transitionTime <= 0f ? 1f : Mathf.Clamp01(stateTimer / transitionTime);
        float eased = motionCurve.Evaluate(t);

        valve1.localPosition = Vector3.LerpUnclamped(valve1Start, valve1Target, eased);
        valve2.localPosition = Vector3.LerpUnclamped(valve2Start, valve2Target, eased);
        valve3.localPosition = Vector3.LerpUnclamped(valve3Start, valve3Target, eased);
    }

    void SnapToTargets()
    {
        valve1.localPosition = valve1Target;
        valve2.localPosition = valve2Target;
        valve3.localPosition = valve3Target;
    }

    void DisableConflictingValveScripts()
    {
        DisableTrumpetValveAnimator(valve1);
        DisableTrumpetValveAnimator(valve2);
        DisableTrumpetValveAnimator(valve3);
    }

    void RepairValveRenderers()
    {
        string status;
        ValveRenderRepair.EnsureVisible(valve1, "Valve 1", out status);
        ValveRenderRepair.EnsureVisible(valve2, "Valve 2", out status);
        ValveRenderRepair.EnsureVisible(valve3, "Valve 3", out status);
    }

    void DisableTrumpetValveAnimator(Transform valve)
    {
        if (valve == null)
        {
            return;
        }

        TrumpetValveAnimator animator = valve.GetComponent<TrumpetValveAnimator>();

        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    void OnDisable()
    {
        activeDrivers.Remove(this);

        if (!restoreRestPositionsOnDisable || !hasCachedRestPositions)
        {
            return;
        }

        if (valve1 != null) valve1.localPosition = valve1Rest;
        if (valve2 != null) valve2.localPosition = valve2Rest;
        if (valve3 != null) valve3.localPosition = valve3Rest;
    }
}
