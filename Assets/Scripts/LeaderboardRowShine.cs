using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LeaderboardRowShine : MonoBehaviour
{
    [Header("shine colors")]
    public Color baseColor = Color.white;
    public Color shineColor = Color.white;
    public Color glowColor = Color.white;

    [Header("shine animation")]
    public float shineSpeed = 3.0f;
    public float shineWidth = 2.5f;
    public float shineStrength = 0.85f;

    [Header("pulse")]
    public float pulseSpeed = 2.0f;
    public float glowPulseAmount = 0.10f;

    [Header("tmp material")]
    public float outlineWidth = 0.08f;
    public float glowOuter = 0.20f;
    public float glowPower = 0.45f;

    private TMP_Text text;
    private Material runtimeMaterial;
    private bool shineActive = false;

    void Awake()
    {
        // Give each row its own TMP material so glow changes do not affect other text.
        text = GetComponent<TMP_Text>();
        SetupRuntimeMaterial();
    }

    void Update()
    {
        if (!shineActive || text == null)
        {
            return;
        }

        AnimateTextShine();
        AnimateGlow();
    }

    public void SetMedalStyle(Color newBaseColor, Color newShineColor, Color newGlowColor)
    {
        // Enable animated shine/glow for top-ranked leaderboard rows.
        baseColor = newBaseColor;
        shineColor = newShineColor;
        glowColor = newGlowColor;
        shineActive = true;

        SetupRuntimeMaterial();
        ApplyGlow(glowOuter, glowPower);
        text.color = baseColor;
    }

    public void SetNormalStyle(Color normalColor)
    {
        // Disable medal animation for lower rows and restore a plain color.
        shineActive = false;

        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        text.color = normalColor;
        ClearGlow();
        text.ForceMeshUpdate();
    }

    void SetupRuntimeMaterial()
    {
        if (text == null || text.fontSharedMaterial == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(text.fontSharedMaterial);
            text.fontMaterial = runtimeMaterial;
        }

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

        text.UpdateMeshPadding();
    }

    void AnimateGlow()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1.0f) * 0.5f;
        float animatedGlowOuter = glowOuter + pulse * glowPulseAmount;

        ApplyGlow(animatedGlowOuter, glowPower);
    }

    void ApplyGlow(float outer, float power)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty("_GlowColor"))
        {
            runtimeMaterial.SetColor("_GlowColor", glowColor);
        }

        if (runtimeMaterial.HasProperty("_GlowOuter"))
        {
            runtimeMaterial.SetFloat("_GlowOuter", outer);
        }

        if (runtimeMaterial.HasProperty("_GlowPower"))
        {
            runtimeMaterial.SetFloat("_GlowPower", power);
        }
    }

    void ClearGlow()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty("_GlowOuter"))
        {
            runtimeMaterial.SetFloat("_GlowOuter", 0f);
        }

        if (runtimeMaterial.HasProperty("_GlowPower"))
        {
            runtimeMaterial.SetFloat("_GlowPower", 0f);
        }
    }

    void AnimateTextShine()
    {
        // Sweep a bright band across visible TMP characters by editing vertex colors.
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
