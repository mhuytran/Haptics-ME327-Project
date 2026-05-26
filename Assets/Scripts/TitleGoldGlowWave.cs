using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class TitleGoldGlowWave : MonoBehaviour
{
    [Header("base colors")]
    public Color silverColor = new Color(0.92f, 0.95f, 1.0f, 1.0f);
    public Color goldColor = new Color(1.0f, 0.72f, 0.08f, 1.0f);
    public Color glowColor = new Color(1.0f, 0.62f, 0.05f, 1.0f);

    [Header("shine sweep")]
    public Color shineColor = Color.white;
    public float shineSpeed = 4.0f;
    public float shineWidth = 2.2f;
    public float shineStrength = 0.85f;

    [Header("flash")]
    public float flashSpeed = 3.0f;
    public float characterFlashOffset = 0.25f;

    [Header("scale pulse")]
    public float pulseAmount = 0.035f;
    public float pulseSpeed = 2.5f;

    [Header("tmp glow material")]
    public bool useTMPGlow = true;
    public float glowOuter = 0.35f;
    public float glowPower = 0.55f;
    public float outlineWidth = 0.18f;

    private TMP_Text titleText;
    private Vector3 baseScale;
    private Material runtimeMaterial;

    void Awake()
    {
        titleText = GetComponent<TMP_Text>();
        baseScale = transform.localScale;

        SetupRuntimeMaterial();
    }

    void Update()
    {
        AnimateScalePulse();
        AnimateTextMesh();
        AnimateGlowMaterial();
    }

    void SetupRuntimeMaterial()
    {
        if (!useTMPGlow || titleText == null || titleText.fontSharedMaterial == null)
        {
            return;
        }

        runtimeMaterial = new Material(titleText.fontSharedMaterial);
        titleText.fontMaterial = runtimeMaterial;

        if (runtimeMaterial.HasProperty("_OutlineColor"))
        {
            runtimeMaterial.SetColor("_OutlineColor", Color.black);
        }

        if (runtimeMaterial.HasProperty("_OutlineWidth"))
        {
            runtimeMaterial.SetFloat("_OutlineWidth", outlineWidth);
        }

        if (runtimeMaterial.HasProperty("_GlowColor"))
        {
            runtimeMaterial.SetColor("_GlowColor", glowColor);
        }

        if (runtimeMaterial.HasProperty("_GlowOuter"))
        {
            runtimeMaterial.SetFloat("_GlowOuter", glowOuter);
        }

        if (runtimeMaterial.HasProperty("_GlowPower"))
        {
            runtimeMaterial.SetFloat("_GlowPower", glowPower);
        }

        titleText.UpdateMeshPadding();
    }

    void AnimateScalePulse()
    {
        float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }

    void AnimateGlowMaterial()
    {
        if (!useTMPGlow || runtimeMaterial == null)
        {
            return;
        }

        float flash = (Mathf.Sin(Time.unscaledTime * flashSpeed) + 1.0f) * 0.5f;
        Color animatedGlow = Color.Lerp(silverColor, glowColor, flash);

        if (runtimeMaterial.HasProperty("_GlowColor"))
        {
            runtimeMaterial.SetColor("_GlowColor", animatedGlow);
        }

        if (runtimeMaterial.HasProperty("_GlowOuter"))
        {
            float animatedOuter = Mathf.Lerp(glowOuter * 0.55f, glowOuter, flash);
            runtimeMaterial.SetFloat("_GlowOuter", animatedOuter);
        }
    }

    void AnimateTextMesh()
    {
        if (titleText == null)
        {
            return;
        }

        titleText.ForceMeshUpdate();

        TMP_TextInfo textInfo = titleText.textInfo;
        int visibleCharacterCount = textInfo.characterCount;

        if (visibleCharacterCount == 0)
        {
            return;
        }

        float shinePosition = Mathf.Repeat(
            Time.unscaledTime * shineSpeed,
            visibleCharacterCount + shineWidth * 2.0f
        ) - shineWidth;

        for (int characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[characterIndex];

            if (!charInfo.isVisible)
            {
                continue;
            }

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Color32[] colors = textInfo.meshInfo[materialIndex].colors32;

            float flash = Mathf.Sin(
                Time.unscaledTime * flashSpeed + characterIndex * characterFlashOffset
            );

            flash = (flash + 1.0f) * 0.5f;

            Color baseColor = Color.Lerp(silverColor, goldColor, flash);

            float distanceFromShine = Mathf.Abs(characterIndex - shinePosition);
            float shineAmount = 1.0f - Mathf.Clamp01(distanceFromShine / shineWidth);
            shineAmount = shineAmount * shineAmount * shineStrength;

            Color finalColor = Color.Lerp(baseColor, shineColor, shineAmount);
            Color32 finalColor32 = finalColor;

            for (int i = 0; i < 4; i++)
            {
                colors[vertexIndex + i] = finalColor32;
            }
        }

        for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
        {
            textInfo.meshInfo[meshIndex].mesh.colors32 = textInfo.meshInfo[meshIndex].colors32;
            titleText.UpdateGeometry(textInfo.meshInfo[meshIndex].mesh, meshIndex);
        }
    }
}