using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ZombieSounds : MonoBehaviour
{
    [Header("Звуки")]
    [Tooltip("Случайные звуки бормотания/стонов (играют периодически)")]
    public AudioClip[] idleSounds;

    [Tooltip("Звуки при атаке")]
    public AudioClip[] attackSounds;

    [Tooltip("Звук когда злится")]
    public AudioClip[] angrySounds;

    [Tooltip("Звуки шагов")]
    public AudioClip[] footstepSounds;

    [Header("Настройки")]
    [Range(2f, 5f)] public float volume = 0.7f;

    [Tooltip("Мин/макс интервал между случайными стонами")]
    public float idleMinInterval = 3f;
    public float idleMaxInterval = 8f;

    [Tooltip("Интервал шагов")]
    public float footstepInterval = 0.5f;

    [Header("3D звук")]
    public float minDistance = 1f;
    public float maxDistance = 15f;

    private AudioSource audioSource;
    private AudioSource footstepSource;
    private float nextIdleTime;
    private float nextFootstepTime;
    private ZombieCustomer zombie;
    private SimpleZombieMovement movement;

    void Start()
    {
        // Основной AudioSource — для стонов, атак, злости
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f;          // полный 3D
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        // Второй AudioSource — для шагов (чтобы не перебивали стоны)
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.spatialBlend = 1f;
        footstepSource.minDistance = minDistance;
        footstepSource.maxDistance = maxDistance;
        footstepSource.playOnAwake = false;
        footstepSource.volume = volume * 0.5f;

        zombie = GetComponent<ZombieCustomer>();
        movement = GetComponent<SimpleZombieMovement>();

        nextIdleTime = Time.time + Random.Range(0.5f, idleMaxInterval);
    }

    void Update()
    {
        HandleIdleSounds();
        HandleFootsteps();
    }

    void HandleIdleSounds()
    {
        if (Time.time < nextIdleTime) return;
        if (idleSounds.Length == 0) return;

        // Не перебиваем другие звуки
        if (audioSource.isPlaying) return;

        PlayRandom(idleSounds);
        nextIdleTime = Time.time + Random.Range(idleMinInterval, idleMaxInterval);
    }

    void HandleFootsteps()
    {
        if (footstepSounds.Length == 0) return;
        if (movement == null) return;
        if (!movement.isMoving) return;

        if (Time.time < nextFootstepTime) return;

        PlayFootstep();
        nextFootstepTime = Time.time + footstepInterval;
    }

    // ==================== PUBLIC — вызывай из ZombieCustomer ====================

    public void PlayAttackSound()
    {
        if (attackSounds.Length > 0)
            PlayRandom(attackSounds);
    }

    public void PlayAngrySound()
    {
        if (angrySounds.Length > 0)
            PlayRandom(angrySounds);

        // Злой зомби стонет чаще
        idleMinInterval = 2f;
        idleMaxInterval = 5f;
    }

    // ==================== PRIVATE ====================

    void PlayRandom(AudioClip[] clips)
    {
        if (clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        audioSource.pitch = Random.Range(0.85f, 1.15f);  // немного разный тон
        audioSource.PlayOneShot(clip, volume);
    }

    void PlayFootstep()
    {
        if (footstepSounds.Length == 0) return;
        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        if (clip == null) return;

        footstepSource.pitch = Random.Range(0.9f, 1.1f);
        footstepSource.PlayOneShot(clip, volume * 0.5f);
    }
}