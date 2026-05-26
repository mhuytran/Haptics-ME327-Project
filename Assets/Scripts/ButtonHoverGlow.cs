using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ButtonHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("references")]
    public Button button;
    public Image buttonImage;
    public TMP_Text buttonText;

    [Header("colors")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.2f, 1.0f, 0.35f, 1.0f);
    public Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 1.0f);

    public Color normalTextColor = Color.black;
    public Color hoverTextColor = Color.black;
    public Color disabledTextColor = new Color(0.18f, 0.18f, 0.18f, 1.0f);

    [Header("animation")]
    public float animationSpeed = 10.0f;
    public float normalScale = 1.0f;
    public float hoverScale = 1.08f;

    private bool isHovering = false;
    private Vector3 baseScale;
    private bool previousInteractableState = true;

    void Awake()
    {
        baseScale = transform.localScale;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (buttonImage == null)
        {
            buttonImage = GetComponent<Image>();
        }

        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TMP_Text>();
        }

        if (button != null)
        {
            // prevents Unity's built-in Color Tint from fighting this script
            button.transition = Selectable.Transition.None;
            previousInteractableState = button.interactable;
        }

        ForceVisualRefresh();
    }

    void OnEnable()
    {
        ForceVisualRefresh();
    }

    void Update()
    {
        bool interactable = button == null || button.interactable;

        // if interactable changed, immediately refresh instead of slowly lerping from gray
        if (interactable != previousInteractableState)
        {
            previousInteractableState = interactable;
            isHovering = false;
            ForceVisualRefresh();
        }

        Color targetButtonColor;
        Color targetTextColor;
        float targetScale;

        if (!interactable)
        {
            targetButtonColor = disabledColor;
            targetTextColor = disabledTextColor;
            targetScale = normalScale;
        }
        else if (isHovering)
        {
            targetButtonColor = hoverColor;
            targetTextColor = hoverTextColor;
            targetScale = hoverScale;
        }
        else
        {
            targetButtonColor = normalColor;
            targetTextColor = normalTextColor;
            targetScale = normalScale;
        }

        if (buttonImage != null)
        {
            buttonImage.color = Color.Lerp(
                buttonImage.color,
                targetButtonColor,
                animationSpeed * Time.unscaledDeltaTime
            );
        }

        if (buttonText != null)
        {
            buttonText.color = Color.Lerp(
                buttonText.color,
                targetTextColor,
                animationSpeed * Time.unscaledDeltaTime
            );
        }

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            baseScale * targetScale,
            animationSpeed * Time.unscaledDeltaTime
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable)
        {
            return;
        }

        isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
    }

    public void ForceVisualRefresh()
    {
        bool interactable = button == null || button.interactable;

        if (buttonImage != null)
        {
            buttonImage.color = interactable ? normalColor : disabledColor;
        }

        if (buttonText != null)
        {
            buttonText.color = interactable ? normalTextColor : disabledTextColor;
        }

        transform.localScale = baseScale * normalScale;
    }
}