using UnityEngine;

public class PerspectiveFixedScreenSize : MonoBehaviour
{
    [Header("screen size target")]
    public float desiredScreenDiameterPixels = 38f;

    [Header("flat disc shape")]
    public float thicknessFraction = 0.08f;

    [Header("correction quality")]
    public int correctionIterations = 3;

    [Header("safety")]
    public float minWorldDiameter = 0.02f;
    public float maxWorldDiameter = 3.0f;

    [Header("rotation")]
    public bool preserveInitialRotation = true;

    private Camera mainCamera;
    private Quaternion initialLocalRotation;

    void Awake()
    {
        mainCamera = Camera.main;
        initialLocalRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        // Estimate world size from camera projection, then refine by measuring screen pixels.
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (preserveInitialRotation)
        {
            transform.localRotation = initialLocalRotation;
        }

        float diameter = EstimateWorldDiameter();
        diameter = Mathf.Clamp(diameter, minWorldDiameter, maxWorldDiameter);

        // force a flat cylinder/disc shape every frame
        transform.localScale = new Vector3(
            diameter,
            diameter * thicknessFraction,
            diameter
        );

        for (int i = 0; i < correctionIterations; i++)
        {
            float measuredDiameter = MeasureProjectedDiameterPixels();

            if (measuredDiameter <= 0.001f)
            {
                return;
            }

            float correction = desiredScreenDiameterPixels / measuredDiameter;
            diameter *= correction;
            diameter = Mathf.Clamp(diameter, minWorldDiameter, maxWorldDiameter);

            transform.localScale = new Vector3(
                diameter,
                diameter * thicknessFraction,
                diameter
            );
        }
    }

    float EstimateWorldDiameter()
    {
        // First-pass conversion from desired pixels to world units at the object's depth.
        Vector3 cameraSpacePosition = mainCamera.transform.InverseTransformPoint(transform.position);
        float depth = Mathf.Abs(cameraSpacePosition.z);

        if (depth <= mainCamera.nearClipPlane)
        {
            depth = mainCamera.nearClipPlane;
        }

        float verticalFovRadians = mainCamera.fieldOfView * Mathf.Deg2Rad;
        float frustumHeightAtDepth = 2f * depth * Mathf.Tan(verticalFovRadians * 0.5f);
        float worldUnitsPerPixel = frustumHeightAtDepth / mainCamera.pixelHeight;

        return desiredScreenDiameterPixels * worldUnitsPerPixel;
    }

    float MeasureProjectedDiameterPixels()
    {
        // Measure projected X/Z diameters to correct for perspective and object rotation.
        Vector3 centerScreen = mainCamera.WorldToScreenPoint(transform.position);

        if (centerScreen.z <= 0f)
        {
            return 0f;
        }

        Vector3 xLeft = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(-0.5f, 0f, 0f)));
        Vector3 xRight = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(0.5f, 0f, 0f)));

        Vector3 zBack = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(0f, 0f, -0.5f)));
        Vector3 zFront = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(0f, 0f, 0.5f)));

        float xDiameter = Vector2.Distance(new Vector2(xLeft.x, xLeft.y), new Vector2(xRight.x, xRight.y));
        float zDiameter = Vector2.Distance(new Vector2(zBack.x, zBack.y), new Vector2(zFront.x, zFront.y));

        return Mathf.Max(xDiameter, zDiameter);
    }
}
