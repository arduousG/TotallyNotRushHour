using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class EnvironmentManager : MonoBehaviour
{
    public GameObject beginnerEnvironment;
    public GameObject intermediateEnvironment;
    public GameObject advancedEnvironment;
    public GameObject expertEnvironment;

    [Header("Endless Sky")]
    public Material endlessSkyboxMaterial;
    public bool useRuntimeEndlessSun = true;
    [FormerlySerializedAs("sunLight")]
    public Light sunLightTemplate;
    public float endlessTimeOfDay = 0.3f;
    public float ambientIntensity = 0.85f;
    public Gradient ambientSkyGradient;
    public Gradient sunColorGradient;
    public bool animateEndlessTime = true;
    public bool updateGlobalSkyDuringRealtimeAnimation = false;
    public bool clampEndlessLightingColors = true;
    public float realtimeDayLengthSeconds = 240f;
    public float dynamicGiRefreshInterval = 1f;
    public float maxGradientColorChannel = 1f;
    public float maxSunIntensity = 1.15f;
    public float maxSkyExposure = 1.15f;

    private GameObject currentEnvironment;
    private Material runtimeEndlessSkybox;
    private Material previousSkybox;
    private Light previousSun;
    private Color previousAmbientLight;
    private float previousAmbientIntensity;
    private AmbientMode previousAmbientMode;
    private bool previousSunWasEnabled;
    private bool sunTemplateWasEnabled;
    private bool disabledPreviousSun;
    private bool disabledSunTemplate;
    private Light runtimeEndlessSunLight;
    private Light activeEndlessSunLight;
    private bool endlessSkyActive;
    private bool hasSavedRenderSettings;
    private float lastDynamicGiRefreshTime = -999f;

    void Awake()
    {
        EnsureDefaultGradients();
    }

    void OnDisable()
    {
        DisableEndlessSky();
    }

    void OnDestroy()
    {
        DisableEndlessSky();
    }

    void Update()
    {
        if (!endlessSkyActive || !animateEndlessTime || realtimeDayLengthSeconds <= 0f)
        {
            return;
        }

        endlessTimeOfDay = Mathf.Repeat(endlessTimeOfDay + Time.deltaTime / realtimeDayLengthSeconds, 1f);

        if (updateGlobalSkyDuringRealtimeAnimation)
        {
            ApplyEndlessLighting(false);
            return;
        }

        ApplyEndlessSun();
    }

    public void LoadEnvironment(PuzzleController.Diff diff)
    {
        Debug.Log("Loading environment: " + diff);

        DisableEndlessSky();
        HideEnvironment();

        GameObject envToLoad = null;

        switch(diff)
        {
            case PuzzleController.Diff.Beginner:
                envToLoad = beginnerEnvironment;
                break;

            case PuzzleController.Diff.Intermediate:
                envToLoad = intermediateEnvironment;
                break;

            case PuzzleController.Diff.Advanced:
                envToLoad = advancedEnvironment;
                break;

            case PuzzleController.Diff.Expert:
                envToLoad = expertEnvironment;
                break;
        }

        if(envToLoad != null)
        {
            currentEnvironment =
                Instantiate(envToLoad);

            Debug.Log("Environment spawned");
        }
        else
        {
            Debug.Log("Environment prefab missing");
        }
    }

    public void HideEnvironment()
    {
        if(currentEnvironment != null)
        {
            Destroy(currentEnvironment);
        }
    }

    public void LoadEndlessEnvironment()
    {
        HideEnvironment();

        if (!endlessSkyActive)
        {
            SaveRenderSettings();
        }

        runtimeEndlessSkybox = GetOrCreateEndlessSkybox();
        if (runtimeEndlessSkybox != null)
        {
            RenderSettings.skybox = runtimeEndlessSkybox;
        }

        activeEndlessSunLight = GetOrCreateEndlessSunLight();
        if (activeEndlessSunLight != null)
        {
            DisableSceneSunLightsForEndless();
            RenderSettings.sun = activeEndlessSunLight;
        }

        endlessSkyActive = true;
        SetEndlessTime(endlessTimeOfDay, true);
    }

    public void AdvanceEndlessTime(float amount)
    {
        if (!endlessSkyActive)
        {
            LoadEndlessEnvironment();
        }

        SetEndlessTime(endlessTimeOfDay + amount, true);
    }

    public void SetEndlessTime(float normalizedTime)
    {
        SetEndlessTime(normalizedTime, true);
    }

    void SetEndlessTime(float normalizedTime, bool forceEnvironmentRefresh)
    {
        endlessTimeOfDay = Mathf.Repeat(normalizedTime, 1f);
        ApplyEndlessLighting(forceEnvironmentRefresh);
    }

    void ApplyEndlessLighting(bool forceEnvironmentRefresh)
    {
        Color ambientColor = EvaluateEndlessGradient(ambientSkyGradient, endlessTimeOfDay);

        RenderSettings.ambientLight = ambientColor * ambientIntensity;

        if (runtimeEndlessSkybox != null)
        {
            if (runtimeEndlessSkybox.HasProperty("_SkyTint"))
            {
                runtimeEndlessSkybox.SetColor("_SkyTint", ambientColor);
            }

            if (runtimeEndlessSkybox.HasProperty("_Exposure"))
            {
                float daylight = Mathf.Clamp01(Mathf.Sin(endlessTimeOfDay * Mathf.PI));
                runtimeEndlessSkybox.SetFloat("_Exposure", Mathf.Min(maxSkyExposure, Mathf.Lerp(0.45f, 1.15f, daylight)));
            }
        }

        ApplyEndlessSun();
        RefreshDynamicGiIfNeeded(forceEnvironmentRefresh);
    }

    void ApplyEndlessSun()
    {
        if (activeEndlessSunLight != null)
        {
            activeEndlessSunLight.color = EvaluateEndlessGradient(sunColorGradient, endlessTimeOfDay);
            activeEndlessSunLight.intensity = Mathf.Min(maxSunIntensity, Mathf.Lerp(0.15f, 1.15f, Mathf.Clamp01(Mathf.Sin(endlessTimeOfDay * Mathf.PI))));
            activeEndlessSunLight.transform.rotation = Quaternion.Euler((endlessTimeOfDay * 360f) - 90f, 170f, 0f);
        }
    }

    void DisableEndlessSky()
    {
        if (!endlessSkyActive)
        {
            return;
        }

        endlessSkyActive = false;
        RestoreRenderSettings();
        DestroyRuntimeEndlessSun();
        RefreshDynamicGiIfNeeded(true);
    }

    void SaveRenderSettings()
    {
        previousSkybox = RenderSettings.skybox;
        previousSun = RenderSettings.sun;
        previousAmbientLight = RenderSettings.ambientLight;
        previousAmbientIntensity = RenderSettings.ambientIntensity;
        previousAmbientMode = RenderSettings.ambientMode;
        previousSunWasEnabled = previousSun != null && previousSun.enabled;
        sunTemplateWasEnabled = sunLightTemplate != null && sunLightTemplate.enabled;
        hasSavedRenderSettings = true;
    }

    void RestoreRenderSettings()
    {
        if (!hasSavedRenderSettings)
        {
            return;
        }

        RenderSettings.skybox = previousSkybox;
        RenderSettings.sun = previousSun;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientLight = previousAmbientLight;
        RenderSettings.ambientIntensity = previousAmbientIntensity;
        RestoreSceneSunLights();
        hasSavedRenderSettings = false;
        activeEndlessSunLight = null;
    }

    void DisableSceneSunLightsForEndless()
    {
        if (!useRuntimeEndlessSun)
        {
            return;
        }

        if (previousSun != null && previousSun != activeEndlessSunLight && previousSun.enabled)
        {
            previousSun.enabled = false;
            disabledPreviousSun = true;
        }

        if (sunLightTemplate != null && sunLightTemplate != previousSun && sunLightTemplate != activeEndlessSunLight && sunLightTemplate.enabled)
        {
            sunLightTemplate.enabled = false;
            disabledSunTemplate = true;
        }
    }

    void RestoreSceneSunLights()
    {
        if (disabledPreviousSun && previousSun != null)
        {
            previousSun.enabled = previousSunWasEnabled;
        }

        if (disabledSunTemplate && sunLightTemplate != null)
        {
            sunLightTemplate.enabled = sunTemplateWasEnabled;
        }

        disabledPreviousSun = false;
        disabledSunTemplate = false;
    }

    Light GetOrCreateEndlessSunLight()
    {
        if (!useRuntimeEndlessSun)
        {
            return sunLightTemplate;
        }

        if (runtimeEndlessSunLight != null)
        {
            runtimeEndlessSunLight.gameObject.SetActive(true);
            return runtimeEndlessSunLight;
        }

        GameObject sunObj = new GameObject("EndlessRuntimeSun");
        runtimeEndlessSunLight = sunObj.AddComponent<Light>();
        runtimeEndlessSunLight.type = LightType.Directional;

        if (sunLightTemplate != null)
        {
            runtimeEndlessSunLight.color = sunLightTemplate.color;
            runtimeEndlessSunLight.intensity = sunLightTemplate.intensity;
            runtimeEndlessSunLight.shadows = sunLightTemplate.shadows;
            runtimeEndlessSunLight.shadowStrength = sunLightTemplate.shadowStrength;
            runtimeEndlessSunLight.shadowBias = sunLightTemplate.shadowBias;
            runtimeEndlessSunLight.shadowNormalBias = sunLightTemplate.shadowNormalBias;
            runtimeEndlessSunLight.transform.rotation = sunLightTemplate.transform.rotation;
        }

        return runtimeEndlessSunLight;
    }

    void DestroyRuntimeEndlessSun()
    {
        activeEndlessSunLight = null;

        if (runtimeEndlessSunLight == null)
        {
            return;
        }

        Destroy(runtimeEndlessSunLight.gameObject);
        runtimeEndlessSunLight = null;
    }

    Material GetOrCreateEndlessSkybox()
    {
        if (runtimeEndlessSkybox != null)
        {
            return runtimeEndlessSkybox;
        }

        if (endlessSkyboxMaterial != null)
        {
            runtimeEndlessSkybox = new Material(endlessSkyboxMaterial);
            return runtimeEndlessSkybox;
        }

        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader == null)
        {
            return null;
        }

        runtimeEndlessSkybox = new Material(skyShader);
        return runtimeEndlessSkybox;
    }

    void EnsureDefaultGradients()
    {
        if (ambientSkyGradient == null)
        {
            ambientSkyGradient = new Gradient();
        }

        if (ambientSkyGradient.colorKeys == null || ambientSkyGradient.colorKeys.Length == 0)
        {
            ambientSkyGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.02f, 0.03f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.95f, 0.55f, 0.32f), 0.23f),
                    new GradientColorKey(new Color(0.5f, 0.75f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.95f, 0.45f, 0.35f), 0.77f),
                    new GradientColorKey(new Color(0.02f, 0.03f, 0.08f), 1f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        if (sunColorGradient == null)
        {
            sunColorGradient = new Gradient();
        }

        if (sunColorGradient.colorKeys == null || sunColorGradient.colorKeys.Length == 0)
        {
            sunColorGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.25f, 0.35f, 0.8f), 0f),
                    new GradientColorKey(new Color(1f, 0.63f, 0.34f), 0.25f),
                    new GradientColorKey(Color.white, 0.5f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.35f), 0.75f),
                    new GradientColorKey(new Color(0.25f, 0.35f, 0.8f), 1f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }
    }

    void RefreshDynamicGiIfNeeded(bool force)
    {
        if (!force && dynamicGiRefreshInterval > 0f && Time.unscaledTime - lastDynamicGiRefreshTime < dynamicGiRefreshInterval)
        {
            return;
        }

        lastDynamicGiRefreshTime = Time.unscaledTime;
        DynamicGI.UpdateEnvironment();
    }

    Color EvaluateEndlessGradient(Gradient gradient, float time)
    {
        if (gradient == null)
        {
            return Color.white;
        }

        Color color = gradient.Evaluate(time);
        if (!clampEndlessLightingColors)
        {
            return color;
        }

        float maxChannel = Mathf.Max(0f, maxGradientColorChannel);
        color.r = Mathf.Clamp(color.r, 0f, maxChannel);
        color.g = Mathf.Clamp(color.g, 0f, maxChannel);
        color.b = Mathf.Clamp(color.b, 0f, maxChannel);
        color.a = Mathf.Clamp01(color.a);
        return color;
    }
}
