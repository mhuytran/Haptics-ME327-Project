using UnityEngine;
using UnityEngine.UI;

public class PowerMarginLayout : MonoBehaviour
{
    public Image powerTop;
    public Image powerBottom;
    public Image powerLeft;
    public Image powerRight;

    public float borderThickness = 90f;

    void Start()
    {
        ApplyLayout();
        DisableRaycasts();
    }

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        SetupTop(powerTop);
        SetupBottom(powerBottom);
        SetupLeft(powerLeft);
        SetupRight(powerRight);
        DisableRaycasts();
    }

    void SetupTop(Image image)
    {
        if (image == null) return;

        RectTransform rect = image.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, borderThickness);
    }

    void SetupBottom(Image image)
    {
        if (image == null) return;

        RectTransform rect = image.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, borderThickness);
    }

    void SetupLeft(Image image)
    {
        if (image == null) return;

        RectTransform rect = image.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        // avoid double-overlap with top and bottom bars
        rect.sizeDelta = new Vector2(borderThickness, -2f * borderThickness);
    }

    void SetupRight(Image image)
    {
        if (image == null) return;

        RectTransform rect = image.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        // avoid double-overlap with top and bottom bars
        rect.sizeDelta = new Vector2(borderThickness, -2f * borderThickness);
    }

    void DisableRaycasts()
    {
        if (powerTop != null) powerTop.raycastTarget = false;
        if (powerBottom != null) powerBottom.raycastTarget = false;
        if (powerLeft != null) powerLeft.raycastTarget = false;
        if (powerRight != null) powerRight.raycastTarget = false;
    }
}