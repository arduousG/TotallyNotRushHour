using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;

    [Header("Music")]
    [SerializeField] private AudioClip gameplayMusicLoop;
    [SerializeField] private AudioClip menuMusicLoop;

    [Header("SFX")]
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip carSelect;
    [SerializeField] private AudioClip moveSuccess;
    [SerializeField] private AudioClip invalidMove;
    [SerializeField] private AudioClip win;
    [SerializeField] private AudioClip reset;

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 0.9f;

    [Header("UI Click Tuning")]
    [Range(0f, 3f)] [SerializeField] private float uiClickGain = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogUiClickPlayback = false;

    private const string MusicVolumePrefKey = "rushhour.audio.music";
    private const string SfxVolumePrefKey = "rushhour.audio.sfx";
    private const string UiVolumePrefKey = "rushhour.audio.ui";

    //Keeps music transitions smooth when switching menu/game states
    private Coroutine musicFadeRoutine;
    //Used for WebGL autoplay recovery in case the browser blocks playback until user interaction
    private bool musicRequested;
    //Stores the last requested track/vol so retry logic can resume correctly
    private AudioClip requestedMusicClip;
    private float requestedMusicVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadSavedVolumes();
        EnsureAudioSources();
        PreloadImportantAudio();
        ApplyVolumes();
    }

    private void OnApplicationQuit()
    {
        SaveVolumes();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveVolumes();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveVolumes();
        }
    }

    private void Update()
    {
#if UNITY_WEBGL
        TryRecoverWebGlMusicPlayback();
#endif
    }

    private void PreloadImportantAudio()
    {
        // should feel instant when triggered
        PreloadClip(win);
        PreloadClip(moveSuccess);
        PreloadClip(invalidMove);
        PreloadClip(carSelect);
        PreloadClip(reset);
        PreloadClip(uiClick);
    }

    private void PreloadClip(AudioClip clip)
    {
        if (clip != null)
        {
            clip.LoadAudioData();
        }
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        uiSource.loop = false;
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
    }

    private void ApplyVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }

        if (uiSource != null)
        {
            uiSource.volume = uiVolume;
        }
    }

    public void PlayMusicLoop()
    {
        //back-compat wrapper: route old call sites to gameplay loop
        PlayGameplayMusicLoop();
    }

    public void StopMusic()
    {
        musicRequested = false;
        requestedMusicClip = null;

        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    public void PlayMusicLoopWithFade(float duration = 0.25f)
    {
        //back-compat wrapper: route old call sites to gameplay loop + fade
        PlayGameplayMusicLoopWithFade(duration);
    }

    public void PlayGameplayMusicLoop()
    {
        // Use gameplay loop for active puzzle states.
        PlayMusicClip(gameplayMusicLoop, musicVolume);
    }

    public void PlayGameplayMusicLoopWithFade(float duration = 0.25f)
    {
        // Fade-in helper for scene/panel transitions into gameplay.
        PlayMusicClipWithFade(gameplayMusicLoop, musicVolume, duration);
    }

    public void PlayMenuMusicLoopWithFade(float duration = 0.25f)
    {
        //Fallback to gameplay loop if no dedicated menu track is assigned
        AudioClip clip = menuMusicLoop != null ? menuMusicLoop : gameplayMusicLoop;
        PlayMusicClipWithFade(clip, musicVolume, duration);
    }

    public float MusicVolume
    {
        get { return musicVolume; }
    }

    public float SfxVolume
    {
        get { return sfxVolume; }
    }

    public float UiVolume
    {
        get { return uiVolume; }
    }

    private void PlayMusicClip(AudioClip clip, float targetVolume)
    {
        if (musicSource == null || clip == null)
            return;

        //cache requested state so WebGL can retry on input if autoplay is blocked
        musicRequested = true;
        requestedMusicClip = clip;
        requestedMusicVolume = Mathf.Clamp01(targetVolume);

        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (musicSource.clip != clip)
        {
            musicSource.clip = clip;
        }

        musicSource.volume = requestedMusicVolume;

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void PlayMusicClipWithFade(AudioClip clip, float targetVolume, float duration)
    {
        if (musicSource == null || clip == null)
            return;

        musicRequested = true;
        requestedMusicClip = clip;
        requestedMusicVolume = Mathf.Clamp01(targetVolume);

        if (musicSource.clip != clip)
        {
            musicSource.clip = clip;
        }

        if (!musicSource.isPlaying)
        {
            musicSource.volume = 0f;
            musicSource.Play();
        }

        StartMusicFade(requestedMusicVolume, duration, stopAtEnd: false);
    }

    public void StopMusicWithFade(float duration = 0.25f)
    {
        if (musicSource == null)
            return;

        //clear requested track so retry logic does not restart music by mistake
        musicRequested = false;
        requestedMusicClip = null;

        if (!musicSource.isPlaying)
        {
            return;
        }

        StartMusicFade(0f, duration, stopAtEnd: true);
    }

    public void FadeMusicTo(float targetVolume, float duration = 0.25f)
    {
        if (musicSource == null)
            return;

        //keep requested vol in sync for later recoveries/restarts
        requestedMusicVolume = Mathf.Clamp01(targetVolume);
        StartMusicFade(requestedMusicVolume, duration, stopAtEnd: false);
    }

    public void PlayUIClick()
    {
        if (uiClick == null)
            return;

        if (debugLogUiClickPlayback)
        {
            Debug.Log("AudioManager PlayUIClick fired: " + uiClick.name + " gain=" + uiClickGain);
        }

        if (uiSource != null && uiSource.enabled && uiSource.gameObject.activeInHierarchy)
        {
            uiSource.PlayOneShot(uiClick, uiClickGain);
            return;
        }

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(uiClick, uiClickGain);
        }
    }

    public void PlayCarSelect()
    {
        PlayOneShot(carSelect);
    }

    public void PlayMoveSuccess()
    {
        PlayOneShot(moveSuccess);
    }

    public void PlayInvalidMove()
    {
        PlayOneShot(invalidMove);
    }

    public void PlayWin()
    {
        if (sfxSource == null || win == null)
            return;

        // Stop short SFX to give win cue priority
        sfxSource.Stop();
        sfxSource.clip = win;
        sfxSource.Play();
    }

    public void PlayReset()
    {
        PlayOneShot(reset);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    public void SetMusicVolume(float value)
    {
        // Keep requested volume in sync with slider updates.
        musicVolume = Mathf.Clamp01(value);
        requestedMusicVolume = musicVolume;
        ApplyVolumes();
        SaveVolumes();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        SaveVolumes();
    }

    public void SetUiVolume(float value)
    {
        uiVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        SaveVolumes();
    }

    public void SetVolumes(float newMusicVolume, float newSfxVolume, float newUiVolume)
    {
        musicVolume = Mathf.Clamp01(newMusicVolume);
        sfxVolume = Mathf.Clamp01(newSfxVolume);
        uiVolume = Mathf.Clamp01(newUiVolume);
        ApplyVolumes();
        SaveVolumes();
    }

    private void LoadSavedVolumes()
    {
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, musicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, sfxVolume));
        uiVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(UiVolumePrefKey, uiVolume));
    }

    private void SaveVolumes()
    {
        // Persist user mix so menu/in-game settings survive relaunch.
        PlayerPrefs.SetFloat(MusicVolumePrefKey, musicVolume);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, sfxVolume);
        PlayerPrefs.SetFloat(UiVolumePrefKey, uiVolume);
        PlayerPrefs.Save();
    }

    private void StartMusicFade(float targetVolume, float duration, bool stopAtEnd)
    {
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
        }

        musicFadeRoutine = StartCoroutine(FadeMusicRoutine(Mathf.Clamp01(targetVolume), Mathf.Max(0.01f, duration), stopAtEnd));
    }

    private IEnumerator FadeMusicRoutine(float targetVolume, float duration, bool stopAtEnd)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        musicSource.volume = targetVolume;

        if (stopAtEnd && Mathf.Approximately(targetVolume, 0f))
        {
            musicSource.Stop();
        }

        musicFadeRoutine = null;
    }

#if UNITY_WEBGL
    private void TryRecoverWebGlMusicPlayback()
    {
        //Browsers can block audio until user input; retry once a gesture is detected
        if (!musicRequested || musicSource == null)
        {
            return;
        }

        if (musicSource.isPlaying)
        {
            return;
        }

        if (!HasWebGlUserGestureThisFrame())
        {
            return;
        }

        if (requestedMusicClip == null)
        {
            requestedMusicClip = gameplayMusicLoop != null ? gameplayMusicLoop : menuMusicLoop;
        }

        if (requestedMusicClip == null)
        {
            return;
        }

        if (musicSource.clip != requestedMusicClip)
        {
            musicSource.clip = requestedMusicClip;
        }

        musicSource.Play();
        if (musicSource.isPlaying)
        {
            float targetVolume = requestedMusicVolume > 0f ? requestedMusicVolume : musicVolume;
            StartMusicFade(targetVolume, 0.2f, stopAtEnd: false);
        }
    }

    private bool HasWebGlUserGestureThisFrame()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            return true;
        }

        return Input.touchCount > 0;
    }
#endif
}
