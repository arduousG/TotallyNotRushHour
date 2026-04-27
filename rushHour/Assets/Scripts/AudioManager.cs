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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureAudioSources();
        PreloadImportantAudio();
        ApplyVolumes();
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
        if (musicSource == null || gameplayMusicLoop == null)
            return;

        if (musicSource.clip != gameplayMusicLoop)
        {
            musicSource.clip = gameplayMusicLoop;
        }

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    public void PlayUIClick()
    {
        if (uiClick == null)
            return;

        if (debugLogUiClickPlayback)
        {
            Debug.Log("AudioManager PlayUIClick fired: " + uiClick.name + " gain=" + uiClickGain);
        }

        if (uiSource != null)
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
}
