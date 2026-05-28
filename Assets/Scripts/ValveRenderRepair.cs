using UnityEngine;

public static class ValveRenderRepair
{
    private const string RepairObjectName = "RuntimeValveRendererRepair";

    public static bool EnsureVisible(Transform valveRoot, string label, out string status)
    {
        status = "No valve root.";

        if (valveRoot == null)
        {
            return false;
        }

        valveRoot.gameObject.SetActive(true);

        Renderer[] renderers = valveRoot.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            EnableRenderers(renderers);
            status = label + " renderers enabled: " + renderers.Length;
            return true;
        }

        Renderer sourceRenderer = FindSiblingRenderer(valveRoot);

        if (sourceRenderer == null)
        {
            status = label + " has no renderer and no sibling renderer to copy.";
            Debug.LogWarning(status);
            return false;
        }

        MeshFilter sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();

        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
        {
            status = label + " found a sibling renderer, but it has no MeshFilter mesh.";
            Debug.LogWarning(status);
            return false;
        }

        Transform sourceValveRoot = FindDirectChildRoot(sourceRenderer.transform, valveRoot.parent);
        GameObject repairObject = GetOrCreateRepairObject(valveRoot);
        Transform repairTransform = repairObject.transform;

        if (sourceValveRoot != null)
        {
            Matrix4x4 sourceLocalMatrix =
                sourceValveRoot.worldToLocalMatrix * sourceRenderer.transform.localToWorldMatrix;

            repairTransform.localPosition = sourceLocalMatrix.GetColumn(3);
            repairTransform.localRotation = sourceLocalMatrix.rotation;
            repairTransform.localScale = sourceLocalMatrix.lossyScale;
        }
        else
        {
            repairTransform.localPosition = Vector3.zero;
            repairTransform.localRotation = Quaternion.identity;
            repairTransform.localScale = Vector3.one;
        }

        MeshFilter meshFilter = repairObject.GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            meshFilter = repairObject.AddComponent<MeshFilter>();
        }

        meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer meshRenderer = repairObject.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            meshRenderer = repairObject.AddComponent<MeshRenderer>();
        }

        CopyRendererSettings(sourceRenderer, meshRenderer);
        meshRenderer.enabled = true;

        status = label + " repaired by copying renderer from " + sourceRenderer.name + ".";
        Debug.LogWarning(status);
        return true;
    }

    private static void EnableRenderers(Renderer[] renderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
        }
    }

    private static Renderer FindSiblingRenderer(Transform valveRoot)
    {
        if (valveRoot == null || valveRoot.parent == null)
        {
            return null;
        }

        for (int i = 0; i < valveRoot.parent.childCount; i++)
        {
            Transform sibling = valveRoot.parent.GetChild(i);

            if (sibling == valveRoot || !sibling.name.Contains("Valve"))
            {
                continue;
            }

            Renderer[] renderers = sibling.GetComponentsInChildren<Renderer>(true);

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];

                if (renderer != null && renderer.GetComponent<MeshFilter>() != null)
                {
                    return renderer;
                }
            }
        }

        return null;
    }

    private static Transform FindDirectChildRoot(Transform child, Transform parent)
    {
        if (child == null || parent == null)
        {
            return null;
        }

        Transform current = child;

        while (current != null && current.parent != parent)
        {
            current = current.parent;
        }

        return current;
    }

    private static GameObject GetOrCreateRepairObject(Transform valveRoot)
    {
        Transform existing = valveRoot.Find(RepairObjectName);

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing.gameObject;
        }

        GameObject repairObject = new GameObject(RepairObjectName);
        repairObject.transform.SetParent(valveRoot, false);
        return repairObject;
    }

    private static void CopyRendererSettings(Renderer source, MeshRenderer target)
    {
        target.sharedMaterials = source.sharedMaterials;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.lightProbeUsage = source.lightProbeUsage;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
        target.renderingLayerMask = source.renderingLayerMask;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
    }
}
