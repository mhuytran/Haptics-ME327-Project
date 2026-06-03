using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class NoteGlowPulse : MonoBehaviour
{
    [Header("note glow color")]
    public Color baseColor = Color.cyan;
    public float brightness = 2.2f;

    [Header("idle pulse")]
    public float pulseSpeed = 5.0f;
    public float pulseAmount = 0.18f;

    [Header("tap smash size")]
    public bool enableSmashBurst = true;
    public float tapDuration = 0.34f;

    [Tooltip("Starting diameter of the tap smash ring in world units.")]
    public float tapStartDiameter = 0.12f;

    [Tooltip("Final diameter of the tap smash ring in world units.")]
    public float tapEndDiameter = 1.20f;

    [Tooltip("Thickness of the ring, not the diameter.")]
    public float tapRingThickness = 0.07f;

    public int tapSegments = 96;

    [Header("tap streak size")]
    public int tapStreakCount = 14;

    [Tooltip("Inner diameter where radial streaks begin.")]
    public float tapStreakInnerDiameter = 0.16f;

    [Tooltip("Outer diameter where radial streaks end.")]
    public float tapStreakOuterDiameter = 1.10f;

    public float tapStreakWidth = 0.045f;

    [Header("hold smash size")]
    [Tooltip("Base diameter of the pulsing hold smash ring.")]
    public float holdDiameter = 0.64f;

    public float holdRingThickness = 0.07f;
    public float holdPulseSpeed = 7.0f;

    [Tooltip("Fractional pulse amount. 0.14 means ±14% size pulse.")]
    public float holdPulseAmount = 0.14f;

    public int holdStreakCount = 12;

    [Tooltip("Inner diameter of hold-note radial streaks.")]
    public float holdStreakInnerDiameter = 0.44f;

    [Tooltip("Outer diameter of hold-note radial streaks.")]
    public float holdStreakOuterDiameter = 1.00f;

    public float holdStreakWidth = 0.04f;

    [Header("hold end smash")]
    [Tooltip("Multiplier applied to tapEndDiameter when a hold note completes.")]
    public float holdEndDiameterMultiplier = 1.25f;

    [Header("surface offset")]
    public float surfaceOffset = 0.015f;

    private Renderer noteRenderer;
    private Material runtimeMaterial;

    void Awake()
    {
        // Use a runtime material so each note can glow independently.
        noteRenderer = GetComponent<Renderer>();
        CreateRuntimeMaterial();
    }

    void Update()
    {
        // Idle notes gently pulse while they travel toward the valve target.
        float pulse = 1.0f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        ApplyColor(pulse);
    }

    public void SetColor(Color newColor)
    {
        baseColor = newColor;
        ApplyColor(1.0f);
    }

    public void PlayTapSmash(Vector3 position, Quaternion rotation, bool perfectHit)
    {
        // Spawn a short-lived flat ring/streak burst on the hit plane.
        if (!enableSmashBurst)
        {
            return;
        }

        Color smashColor = perfectHit
            ? Color.Lerp(baseColor, Color.white, 0.35f)
            : baseColor;

        Vector3 adjustedPosition = position + rotation * Vector3.forward * surfaceOffset;

        GameObject smashObject = new GameObject("FLAT_TAP_SMASH");
        smashObject.transform.position = adjustedPosition;
        smashObject.transform.rotation = rotation;

        FlatNoteSmashEffect effect = smashObject.AddComponent<FlatNoteSmashEffect>();

        effect.InitializeTap(
            smashColor,
            tapDuration,
            tapStartDiameter * 0.5f,
            tapEndDiameter * 0.5f,
            tapRingThickness,
            tapSegments,
            tapStreakCount,
            tapStreakInnerDiameter * 0.5f,
            tapStreakOuterDiameter * 0.5f,
            tapStreakWidth,
            perfectHit
        );
    }

    public FlatNoteSmashEffect StartHoldSmash(Vector3 position, Quaternion rotation, bool perfectHit)
    {
        // Hold notes keep a pulsing effect alive until the hold ends or misses.
        Color smashColor = perfectHit
            ? Color.Lerp(baseColor, Color.white, 0.25f)
            : baseColor;

        Vector3 adjustedPosition = position + rotation * Vector3.forward * surfaceOffset;

        GameObject smashObject = new GameObject("FLAT_HOLD_SMASH");
        smashObject.transform.position = adjustedPosition;
        smashObject.transform.rotation = rotation;

        FlatNoteSmashEffect effect = smashObject.AddComponent<FlatNoteSmashEffect>();

        effect.InitializeHold(
            smashColor,
            holdDiameter * 0.5f,
            holdRingThickness,
            tapSegments,
            holdPulseSpeed,
            holdPulseAmount,
            holdStreakCount,
            holdStreakInnerDiameter * 0.5f,
            holdStreakOuterDiameter * 0.5f,
            holdStreakWidth
        );

        return effect;
    }

    public void PlayHoldEndSmash(Vector3 position, Quaternion rotation)
    {
        if (!enableSmashBurst)
        {
            return;
        }

        Color smashColor = Color.Lerp(baseColor, Color.white, 0.35f);
        Vector3 adjustedPosition = position + rotation * Vector3.forward * surfaceOffset;

        GameObject smashObject = new GameObject("FLAT_HOLD_END_SMASH");
        smashObject.transform.position = adjustedPosition;
        smashObject.transform.rotation = rotation;

        FlatNoteSmashEffect effect = smashObject.AddComponent<FlatNoteSmashEffect>();

        effect.InitializeTap(
            smashColor,
            tapDuration,
            tapStartDiameter * 0.5f,
            tapEndDiameter * holdEndDiameterMultiplier * 0.5f,
            tapRingThickness,
            tapSegments,
            tapStreakCount,
            tapStreakInnerDiameter * 0.5f,
            tapStreakOuterDiameter * holdEndDiameterMultiplier * 0.5f,
            tapStreakWidth,
            true
        );
    }

    void CreateRuntimeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        runtimeMaterial = new Material(shader);
        noteRenderer.material = runtimeMaterial;

        ApplyColor(1.0f);
    }

    void ApplyColor(float pulse)
    {
        // Write color to whichever shader property the active pipeline exposes.
        if (runtimeMaterial == null)
        {
            return;
        }

        Color glowColor = baseColor * Mathf.Max(0.0f, brightness * pulse);
        glowColor.a = 1.0f;

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", glowColor);
        }

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", glowColor);
        }
    }
}

public class FlatNoteSmashEffect : MonoBehaviour
{
    private enum EffectMode
    {
        Tap,
        Hold
    }

    private EffectMode mode;

    private Color baseColor;

    private float tapDuration;
    private float tapStartRadius;
    private float tapEndRadius;
    private bool perfectHit;

    private float holdRadius;
    private float holdPulseSpeed;
    private float holdPulseAmount;
    private float spinAngle = 0f;

    private float ringWidth;
    private int segments;

    private int streakCount;
    private float streakInnerRadius;
    private float streakOuterRadius;
    private float streakWidth;

    private float elapsedTime = 0f;

    private Mesh ringMesh;
    private Mesh streakMesh;

    private Material ringMaterial;
    private Material streakMaterial;

    public void InitializeTap(
        Color color,
        float duration,
        float startRadius,
        float endRadius,
        float width,
        int segmentCount,
        int radialStreakCount,
        float radialStreakInnerRadius,
        float radialStreakOuterRadius,
        float radialStreakWidth,
        bool isPerfectHit
    )
    {
        // Tap effects expand and fade over a fixed duration.
        mode = EffectMode.Tap;

        baseColor = color;
        tapDuration = Mathf.Max(0.01f, duration);
        tapStartRadius = startRadius;
        tapEndRadius = endRadius;
        perfectHit = isPerfectHit;

        ringWidth = width;
        segments = Mathf.Max(24, segmentCount);

        streakCount = Mathf.Max(0, radialStreakCount);
        streakInnerRadius = radialStreakInnerRadius;
        streakOuterRadius = radialStreakOuterRadius;
        streakWidth = radialStreakWidth;

        CreateMeshes();
        UpdateTapVisual(0f);
    }

    public void InitializeHold(
        Color color,
        float radius,
        float width,
        int segmentCount,
        float pulseSpeed,
        float pulseAmount,
        int radialStreakCount,
        float radialStreakInnerRadius,
        float radialStreakOuterRadius,
        float radialStreakWidth
    )
    {
        // Hold effects pulse continuously until StopEffect destroys the object.
        mode = EffectMode.Hold;

        baseColor = color;
        holdRadius = radius;
        holdPulseSpeed = pulseSpeed;
        holdPulseAmount = pulseAmount;

        ringWidth = width;
        segments = Mathf.Max(24, segmentCount);

        streakCount = Mathf.Max(0, radialStreakCount);
        streakInnerRadius = radialStreakInnerRadius;
        streakOuterRadius = radialStreakOuterRadius;
        streakWidth = radialStreakWidth;

        CreateMeshes();
    }

    void Update()
    {
        if (mode == EffectMode.Tap)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsedTime / tapDuration);
            UpdateTapVisual(t);

            if (t >= 1.0f)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            UpdateHoldVisual();
        }
    }

    public void StopEffect()
    {
        Destroy(gameObject);
    }

    void CreateMeshes()
    {
        ringMesh = CreateMeshObject("flat smash ring", out ringMaterial);
        streakMesh = CreateMeshObject("flat smash streaks", out streakMaterial);
    }

    Mesh CreateMeshObject(string objectName, out Material material)
    {
        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(transform, false);
        meshObject.transform.localPosition = Vector3.zero;
        meshObject.transform.localRotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = objectName;
        meshFilter.sharedMesh = mesh;

        material = CreateTransparentMaterial(baseColor);
        meshRenderer.material = material;

        return mesh;
    }

    void UpdateTapVisual(float t)
    {
        // Cubic ease-out makes the ring expand quickly and then fade smoothly.
        float eased = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
        float alpha = Mathf.Lerp(perfectHit ? 1.0f : 0.80f, 0f, t);

        Color color = baseColor;
        color.a = alpha;

        SetMaterialColor(ringMaterial, color);
        SetMaterialColor(streakMaterial, color);

        float radius = Mathf.Lerp(tapStartRadius, tapEndRadius, eased);
        float streakOuter = Mathf.Lerp(streakInnerRadius, streakOuterRadius, eased);
        float streakInner = Mathf.Lerp(0f, streakInnerRadius, eased);

        BuildRingMesh(ringMesh, radius, ringWidth, segments, color);
        BuildStreakMesh(streakMesh, streakCount, streakInner, streakOuter, streakWidth, color, 0f);
    }

    void UpdateHoldVisual()
    {
        float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * holdPulseSpeed) * holdPulseAmount;
        float radius = holdRadius * pulse;

        spinAngle += Time.unscaledDeltaTime * 3.5f;

        Color color = baseColor;
        color.a = 0.85f;

        SetMaterialColor(ringMaterial, color);
        SetMaterialColor(streakMaterial, color);

        BuildRingMesh(ringMesh, radius, ringWidth, segments, color);

        BuildStreakMesh(
            streakMesh,
            streakCount,
            streakInnerRadius * pulse,
            streakOuterRadius * pulse,
            streakWidth,
            color,
            spinAngle
        );
    }

    Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);
        material.color = color;
        material.renderQueue = 3000;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    void BuildRingMesh(Mesh mesh, float radius, float width, int segmentCount, Color color)
    {
        // Build a flat annulus mesh in local XY space.
        if (mesh == null)
        {
            return;
        }

        float outerRadius = radius + width * 0.5f;
        float innerRadius = Mathf.Max(0.001f, radius - width * 0.5f);

        Vector3[] vertices = new Vector3[segmentCount * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segmentCount * 12];

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = ((float)i / segmentCount) * Mathf.PI * 2.0f;

            Vector3 outer = new Vector3(
                Mathf.Cos(angle) * outerRadius,
                Mathf.Sin(angle) * outerRadius,
                0f
            );

            Vector3 inner = new Vector3(
                Mathf.Cos(angle) * innerRadius,
                Mathf.Sin(angle) * innerRadius,
                0f
            );

            vertices[i * 2] = outer;
            vertices[i * 2 + 1] = inner;

            colors[i * 2] = color;
            colors[i * 2 + 1] = color;
        }

        int tri = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % segmentCount;

            int outerA = i * 2;
            int innerA = i * 2 + 1;
            int outerB = next * 2;
            int innerB = next * 2 + 1;

            triangles[tri++] = outerA;
            triangles[tri++] = outerB;
            triangles[tri++] = innerA;

            triangles[tri++] = innerA;
            triangles[tri++] = outerB;
            triangles[tri++] = innerB;

            triangles[tri++] = outerA;
            triangles[tri++] = innerA;
            triangles[tri++] = outerB;

            triangles[tri++] = innerA;
            triangles[tri++] = innerB;
            triangles[tri++] = outerB;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    void BuildStreakMesh(
        Mesh mesh,
        int radialStreakCount,
        float innerRadius,
        float outerRadius,
        float width,
        Color color,
        float angleOffset
    )
    {
        // Build radial rectangles that read as impact streaks around the note.
        if (mesh == null)
        {
            return;
        }

        if (radialStreakCount <= 0)
        {
            mesh.Clear();
            return;
        }

        Vector3[] vertices = new Vector3[radialStreakCount * 4];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[radialStreakCount * 12];

        int vertex = 0;
        int tri = 0;

        for (int i = 0; i < radialStreakCount; i++)
        {
            float angle = angleOffset + ((float)i / radialStreakCount) * Mathf.PI * 2.0f;

            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 tangent = new Vector3(-direction.y, direction.x, 0f);

            Vector3 inner = direction * innerRadius;
            Vector3 outer = direction * outerRadius;
            Vector3 halfWidth = tangent * width * 0.5f;

            vertices[vertex + 0] = inner - halfWidth;
            vertices[vertex + 1] = inner + halfWidth;
            vertices[vertex + 2] = outer - halfWidth;
            vertices[vertex + 3] = outer + halfWidth;

            colors[vertex + 0] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;

            triangles[tri++] = vertex + 0;
            triangles[tri++] = vertex + 2;
            triangles[tri++] = vertex + 1;

            triangles[tri++] = vertex + 1;
            triangles[tri++] = vertex + 2;
            triangles[tri++] = vertex + 3;

            triangles[tri++] = vertex + 0;
            triangles[tri++] = vertex + 1;
            triangles[tri++] = vertex + 2;

            triangles[tri++] = vertex + 1;
            triangles[tri++] = vertex + 3;
            triangles[tri++] = vertex + 2;

            vertex += 4;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }
}
