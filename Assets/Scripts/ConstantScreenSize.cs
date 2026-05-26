using UnityEngine;

public class ConstantScreenSize : MonoBehaviour
{
    public float referenceDistance = 5.0f;
    public float sizeMultiplier = 1.0f;

    private Camera mainCamera;
    private Vector3 originalLocalScale;

    void Start()
    {
        mainCamera = Camera.main;
        originalLocalScale = transform.localScale;
    }

    void Update()
    {
        if (mainCamera == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
        float scaleFactor = distance / referenceDistance;

        transform.localScale = originalLocalScale * scaleFactor * sizeMultiplier;
    }
}