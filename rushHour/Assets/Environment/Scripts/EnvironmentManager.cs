using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    public GameObject beginnerEnvironment;
    public GameObject intermediateEnvironment;
    public GameObject advancedEnvironment;
    public GameObject expertEnvironment;

    [Header("Endless Sky")]
    public Material endlessSkyboxMaterial;
    public Light sunLight;
    public float endlessTimeOfDay = 0.3f;
    public float ambientIntensity = 0.85f;
    public Gradient ambientSkyGradient;
    public Gradient sunColorGradient;
    public bool animateEndlessTime = true;
    public float realtimeDayLengthSeconds = 240f;

    private GameObject currentEnvironment;
    private Material runtimeEndlessSkybox;
    private Material previousSkybox;
    private bool endlessSkyActive;

    void Awake()
    {
        EnsureDefaultGradients();
    }

    void Update()
    {
        if (!endlessSkyActive || !animateEndlessTime || realtimeDayLengthSeconds <= 0f)
        {
            return;
        }

        SetEndlessTime(endlessTimeOfDay + Time.deltaTime / realtimeDayLengthSeconds);
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
            previousSkybox = RenderSettings.skybox;
        }

        runtimeEndlessSkybox = GetOrCreateEndlessSkybox();
        if (runtimeEndlessSkybox != null)
        {
            RenderSettings.skybox = runtimeEndlessSkybox;
        }

        endlessSkyActive = true;
        SetEndlessTime(endlessTimeOfDay);
    }

    public void AdvanceEndlessTime(float amount)
    {
        if (!endlessSkyActive)
        {
            LoadEndlessEnvironment();
        }

        SetEndlessTime(endlessTimeOfDay + amount);
    }

    public void SetEndlessTime(float normalizedTime)
    {
        endlessTimeOfDay = Mathf.Repeat(normalizedTime, 1f);

        Color ambientColor = ambientSkyGradient.Evaluate(endlessTimeOfDay);
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
                runtimeEndlessSkybox.SetFloat("_Exposure", Mathf.Lerp(0.45f, 1.25f, daylight));
            }
        }

        if (sunLight != null)
        {
            sunLight.color = sunColorGradient.Evaluate(endlessTimeOfDay);
            sunLight.intensity = Mathf.Lerp(0.15f, 1.15f, Mathf.Clamp01(Mathf.Sin(endlessTimeOfDay * Mathf.PI)));
            sunLight.transform.rotation = Quaternion.Euler((endlessTimeOfDay * 360f) - 90f, 170f, 0f);
        }

        DynamicGI.UpdateEnvironment();
    }

    void DisableEndlessSky()
    {
        if (!endlessSkyActive)
        {
            return;
        }

        endlessSkyActive = false;
        RenderSettings.skybox = previousSkybox;
        DynamicGI.UpdateEnvironment();
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
}
