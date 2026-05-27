using UnityEngine;

public class TrumpetValveAnimator : MonoBehaviour
{
    public enum ValveControlMode
    {
        BinaryPress,
        DiscreteToF
    }

    [Header("input lane")]
    public int laneIndex = 0;

    [Header("control mode")]
    public ValveControlMode controlMode = ValveControlMode.DiscreteToF;

    [Header("valve travel")]
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.18f, 0f);

    [Header("motion speed")]
    public float pressSpeed = 90.0f;
    public float releaseSpeed = 70.0f;

    private Vector3 restLocalPosition;
    private float currentAmount = 0f;

    void Awake()
    {
        restLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float targetAmount = GetTargetAmount();

        float speed = targetAmount > currentAmount ? pressSpeed : releaseSpeed;

        currentAmount = Mathf.MoveTowards(
            currentAmount,
            targetAmount,
            speed * Time.deltaTime
        );

        transform.localPosition = restLocalPosition + pressedLocalOffset * currentAmount;
    }

    float GetTargetAmount()
    {
        if (controlMode == ValveControlMode.BinaryPress)
        {
            return ValveInputState.GetValve(laneIndex) ? 1f : 0f;
        }

        return ValveInputState.GetValveAmount(laneIndex);
    }
}