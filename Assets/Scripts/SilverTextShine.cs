using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class SilverTextShine : MonoBehaviour
{
    [Header("silver colors")]
    public Color darkSilver = new Color(0.45f, 0.48f, 0.52f, 1.0f);
    public Color lightSilver = new Color(0.92f, 0.95f, 1.0f, 1.0f);
    public Color shineColor = Color.white;

    [Header("shine sweep")]
    public float shineSpeed = 3.0f;
    public float shineWidth = 2.5f;
    public float shineStrength = 0.9f;

    [Header("pulse")]
    public float pulseSpeed = 2.0f;
    public float pulseAmount = 0.12f;

    [Header("outline / glow")]
    public bool useTMPGlow = true;
    public Color outlineColor = new Color(0.02f, 0.02f, 0.05f, 1.0f);
    public Color glowColor = new Color(0.65f, 0.8f, 1.0f, 1.0f);
    public float outlineWidth = 0.12f;
    public float glowOuter = 0.25f;
    public float glowPower = 0.45f;

    private TMP_Text text;
    private Material runtimeMaterial;

    void Awake()
    {
        // Clone the TMP material so this text can animate glow independently.
        text = GetComponent<TMP_Text>();
        SetupRuntimeMaterial();
    }

    void Update()
    {
        // Animate both per-character color and material glow.
        AnimateTextShine();
        AnimateGlow();
    }

    void SetupRuntimeMaterial()
    {
        if (!useTMPGlow || text == null || text.fontSharedMaterial == null)
        {
            return;
        }

        runtimeMaterial = new Material(text.fontSharedMaterial);
        text.fontMaterial = runtimeMaterial;

        if (runtimeMaterial.HasProperty("_OutlineColor"))
        {
            runtimeMaterial.SetColor("_OutlineColor", outlineColor);
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

        text.UpdateMeshPadding();
    }

    void AnimateGlow()
    {
        if (!useTMPGlow || runtimeMaterial == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1.0f) * 0.5f;

        if (runtimeMaterial.HasProperty("_GlowOuter"))
        {
            runtimeMaterial.SetFloat("_GlowOuter", glowOuter + pulse * pulseAmount);
        }
    }

    void AnimateTextShine()
    {
        // Modify TMP vertex colors to create a moving silver/white shine sweep.
        if (text == null)
        {
            return;
        }

        text.ForceMeshUpdate();

        TMP_TextInfo textInfo = text.textInfo;

        if (textInfo.characterCount == 0)
        {
            return;
        }

        float shinePosition = Mathf.Repeat(
            Time.unscaledTime * shineSpeed,
            textInfo.characterCount + shineWidth * 2.0f
        ) - shineWidth;

        for (int charIndex = 0; charIndex < textInfo.characterCount; charIndex++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

            if (!charInfo.isVisible)
            {
                continue;
            }

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Color32[] colors = textInfo.meshInfo[materialIndex].colors32;

            float basePulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed + charIndex * 0.2f) + 1.0f) * 0.5f;
            Color baseColor = Color.Lerp(darkSilver, lightSilver, basePulse);

            float distanceFromShine = Mathf.Abs(charIndex - shinePosition);
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
            text.UpdateGeometry(textInfo.meshInfo[meshIndex].mesh, meshIndex);
        }
    }
}
