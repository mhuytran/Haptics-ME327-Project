using UnityEngine;
using TMPro;

public class TMPInputPlaceholderOnFocus : MonoBehaviour
{
    public TMP_InputField inputField;
    public string placeholderText = "Enter Name";

    private TMP_Text placeholderTMP;

    void Awake()
    {
        // Wire TMP input events so the placeholder hides while the field is focused or filled.
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        if (inputField == null)
        {
            return;
        }

        if (inputField.placeholder != null)
        {
            placeholderTMP = inputField.placeholder.GetComponent<TMP_Text>();
        }

        inputField.onSelect.AddListener(OnSelected);
        inputField.onDeselect.AddListener(OnDeselected);
        inputField.onValueChanged.AddListener(OnValueChanged);

        ShowPlaceholderIfEmpty();
    }

    void OnDestroy()
    {
        if (inputField == null)
        {
            return;
        }

        inputField.onSelect.RemoveListener(OnSelected);
        inputField.onDeselect.RemoveListener(OnDeselected);
        inputField.onValueChanged.RemoveListener(OnValueChanged);
    }

    void OnSelected(string value)
    {
        HidePlaceholder();
    }

    void OnDeselected(string value)
    {
        ShowPlaceholderIfEmpty();
    }

    void OnValueChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            HidePlaceholder();
        }
    }

    void HidePlaceholder()
    {
        if (placeholderTMP != null)
        {
            placeholderTMP.text = "";
        }
    }

    void ShowPlaceholderIfEmpty()
    {
        // Restore the prompt only when no user text is present.
        if (placeholderTMP == null || inputField == null)
        {
            return;
        }

        placeholderTMP.text = string.IsNullOrEmpty(inputField.text)
            ? placeholderText
            : "";
    }
}
