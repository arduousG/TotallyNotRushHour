using UnityEngine;

// Adds a small emissive tint so the exit marker stays visible in changing lighting.
public class ExitMarkerGlow : MonoBehaviour
{
    public Color glowColor = new Color(0.75f, 0.12f, 0.08f, 1f);
    public float emissionIntensity = 1.35f;

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        ApplyGlow();
    }

    void OnEnable()
    {
        ApplyGlow();
    }

    public void ApplyGlow()
    {
        // Use material instances for emission and property blocks for base color overrides.
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        Color emissiveColor = glowColor * Mathf.Max(0f, emissionIntensity);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer markerRenderer = renderers[i];
            if (markerRenderer == null)
            {
                continue;
            }

            Material[] materials = markerRenderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null)
                {
                    continue;
                }

                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emissiveColor);
                }
            }

            markerRenderer.GetPropertyBlock(propertyBlock);
            if (markerRenderer.sharedMaterial != null)
            {
                if (markerRenderer.sharedMaterial.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", glowColor);
                }

                if (markerRenderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    propertyBlock.SetColor("_BaseColor", glowColor);
                }

                if (markerRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    propertyBlock.SetColor("_EmissionColor", emissiveColor);
                }
            }
            markerRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
