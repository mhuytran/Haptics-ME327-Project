using UnityEngine;
using UnityEngine.InputSystem;

public class TrumpetValveAnimator : MonoBehaviour
{
    public int laneIndex = 0;

    [Header("valve motion")]
    public Vector3 maxPressedLocalOffset = new Vector3(0f, -0.18f, 0f);

    [Header("speed")]
    public float pressSpeed = 2.0f;   // larger = faster downward motion
    public float releaseSpeed = 1.5f; // larger = faster upward return

    private Vector3 restLocalPosition;
    private Vector3 targetPressedPosition;

    void Start()
    {
        restLocalPosition = transform.localPosition;
        targetPressedPosition = restLocalPosition + maxPressedLocalOffset;
    }

    void Update()
    {
        bool pressed = ValveInputState.GetValve(laneIndex);

        Vector3 targetPosition = pressed ? targetPressedPosition : restLocalPosition;
        float speed = pressed ? pressSpeed : releaseSpeed;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetPosition,
            speed * Time.deltaTime
        );
    }
}