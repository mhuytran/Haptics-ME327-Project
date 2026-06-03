using UnityEngine;

public class TrumpetLaneFourDividers : MonoBehaviour
{
    [Header("lane centers")]
    public Transform spawnPoint1;
    public Transform spawnPoint2;
    public Transform spawnPoint3;

    public Transform valveTarget1;
    public Transform valveTarget2;
    public Transform valveTarget3;

    [Header("visual style")]
    public Color dividerColor = Color.white;
    public float dividerWidth = 0.035f;

    [Header("lane boundary tuning")]
    public float outerLaneScale = 0.5f;
    public float startTrimFraction = 0.00f;
    public float endTrimFraction = 0.06f;

    [Header("debug")]
    public bool showCenterDebugPaths = false;

    private LineRenderer leftOuter;
    private LineRenderer between12;
    private LineRenderer between23;
    private LineRenderer rightOuter;

    private LineRenderer center1;
    private LineRenderer center2;
    private LineRenderer center3;

    void Start()
    {
        // Create runtime line renderers for the four lane boundaries and optional center guides.
        leftOuter = CreateLine("left outer divider", dividerColor, dividerWidth);
        between12 = CreateLine("divider between lane 1 and 2", dividerColor, dividerWidth);
        between23 = CreateLine("divider between lane 2 and 3", dividerColor, dividerWidth);
        rightOuter = CreateLine("right outer divider", dividerColor, dividerWidth);

        center1 = CreateLine("debug center lane 1", Color.green, 0.012f);
        center2 = CreateLine("debug center lane 2", Color.blue, 0.012f);
        center3 = CreateLine("debug center lane 3", Color.red, 0.012f);
    }

    void Update()
    {
        // Recompute divider endpoints from the current spawn/target transforms each frame.
        if (MissingReferences())
        {
            return;
        }

        Vector3 s1 = spawnPoint1.position;
        Vector3 s2 = spawnPoint2.position;
        Vector3 s3 = spawnPoint3.position;

        Vector3 t1 = valveTarget1.position;
        Vector3 t2 = valveTarget2.position;
        Vector3 t3 = valveTarget3.position;

        // optional debug lines: exact note paths
        SetLineVisible(center1, showCenterDebugPaths);
        SetLineVisible(center2, showCenterDebugPaths);
        SetLineVisible(center3, showCenterDebugPaths);

        UpdateLine(center1, s1, t1);
        UpdateLine(center2, s2, t2);
        UpdateLine(center3, s3, t3);

        // four lane boundaries computed from the three lane centers
        Vector3 sLeftOuter = s1 + outerLaneScale * (s1 - s2);
        Vector3 tLeftOuter = t1 + outerLaneScale * (t1 - t2);

        Vector3 sBetween12 = 0.5f * (s1 + s2);
        Vector3 tBetween12 = 0.5f * (t1 + t2);

        Vector3 sBetween23 = 0.5f * (s2 + s3);
        Vector3 tBetween23 = 0.5f * (t2 + t3);

        Vector3 sRightOuter = s3 + outerLaneScale * (s3 - s2);
        Vector3 tRightOuter = t3 + outerLaneScale * (t3 - t2);

        UpdateTrimmedLine(leftOuter, sLeftOuter, tLeftOuter);
        UpdateTrimmedLine(between12, sBetween12, tBetween12);
        UpdateTrimmedLine(between23, sBetween23, tBetween23);
        UpdateTrimmedLine(rightOuter, sRightOuter, tRightOuter);
    }

    bool MissingReferences()
    {
        return spawnPoint1 == null || spawnPoint2 == null || spawnPoint3 == null ||
               valveTarget1 == null || valveTarget2 == null || valveTarget3 == null;
    }

    LineRenderer CreateLine(string lineName, Color color, float width)
    {
        // Build a simple unlit line renderer with rounded caps.
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.parent = transform;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 8;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        line.material = material;

        return line;
    }

    void UpdateTrimmedLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        // Trim the divider so it does not visually collide with the valve targets.
        Vector3 trimmedStart = Vector3.Lerp(start, end, startTrimFraction);
        Vector3 trimmedEnd = Vector3.Lerp(start, end, 1.0f - endTrimFraction);

        UpdateLine(line, trimmedStart, trimmedEnd);
    }

    void UpdateLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        if (line == null)
        {
            return;
        }

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    void SetLineVisible(LineRenderer line, bool visible)
    {
        if (line != null)
        {
            line.enabled = visible;
        }
    }
}
