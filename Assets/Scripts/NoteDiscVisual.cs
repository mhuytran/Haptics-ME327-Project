using UnityEngine;

public class NoteDiscVisual : MonoBehaviour
{
    [Header("constant apparent size")]
    public float referenceDistance = 5.0f;
    public float sizeMultiplier = 1.0f;

    [Header("camera facing")]
    public bool faceCamera = true;

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

        // preserves thin disc shape
        transform.localScale = originalLocalScale * scaleFactor * sizeMultiplier;

        if (faceCamera)
        {
            Vector3 toCamera = (mainCamera.transform.position - transform.position).normalized;

            // unity cylinder disc has its flat face normal along local y
            transform.rotation = Quaternion.FromToRotation(Vector3.up, toCamera);
        }
    }
}