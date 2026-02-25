using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("������")]
    public AudioClip musicClip;

    [Header("���������")]
    [Range(0f, 1f)] public float defaultVolume = 0.5f;
    public bool loop = true;

    private AudioSource audioSource;

    public const string VOLUME_KEY = "settings.musicVolume";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.clip = musicClip;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.volume = GetSavedVolume();
    }

    public void StartMusic()
    {
        if (audioSource == null) return;
        if (audioSource.isPlaying) return;

        if (musicClip != null)
            audioSource.clip = musicClip;

        audioSource.volume = GetSavedVolume();
        audioSource.Play();
    }

    public void StopMusic()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    public void SetVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);

        if (audioSource != null)
            audioSource.volume = volume;

        PlayerPrefs.SetFloat(VOLUME_KEY, volume);
        PlayerPrefs.Save();

        // ������: ���� ������ ������� � ������ �� ������ � ���������
        if (volume > 0f && audioSource != null && !audioSource.isPlaying)
        {
            if (musicClip != null)
                audioSource.clip = musicClip;
            audioSource.Play();
        }

        // ��������� 0 � �����
        if (volume <= 0f && audioSource != null && audioSource.isPlaying)
            audioSource.Pause();
    }

    public float GetSavedVolume()
    {
        return PlayerPrefs.GetFloat(VOLUME_KEY, defaultVolume);
    }

    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }
}