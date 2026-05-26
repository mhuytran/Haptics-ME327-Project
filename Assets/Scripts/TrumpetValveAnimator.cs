using UnityEngine;

public class TrumpetValveAnimator : MonoBehaviour
{
    [Header("input lane")]
    public int laneIndex = 0;

    [Header("valve motion")]
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.18f, 0f);
    public float motionSpeed = 8.0f;

    [Header("analog ToF control")]
    public bool useAnalogValveAmount = true;

    private Vector3 restLocalPosition;

    void Start()
    {
        restLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float pressAmount;

        if (useAnalogValveAmount)
        {
            // Uses continuous ToF distance from Teensy.
            // 0 = valve up, 1 = valve fully pressed.
            pressAmount = ValveInputState.GetValveAmount(laneIndex);
        }
        else
        {
            // Uses binary keyboard/Teensy press state.
            pressAmount = ValveInputState.GetValve(laneIndex) ? 1f : 0f;
        }

        Vector3 targetPosition = restLocalPosition + pressedLocalOffset * pressAmount;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetPosition,
            motionSpeed * Time.deltaTime
        );
    }
}