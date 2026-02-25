using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ZombieSounds : MonoBehaviour
{
    [Header("�����")]
    [Tooltip("��������� ����� ����������/������ (������ ������������)")]
    public AudioClip[] idleSounds;

    [Tooltip("����� ��� �����")]
    public AudioClip[] attackSounds;

    [Tooltip("���� ����� ������")]
    public AudioClip[] angrySounds;

    [Tooltip("����� �����")]
    public AudioClip[] footstepSounds;

    [Header("���������")]
    [Range(2f, 5f)] public float volume = 0.7f;

    [Tooltip("���/���� �������� ����� ���������� �������")]
    public float idleMinInterval = 3f;
    public float idleMaxInterval = 8f;

    [Tooltip("�������� �����")]
    public float footstepInterval = 0.5f;

    [Header("3D ����")]
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
        // �������� AudioSource � ��� ������, ����, ������
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f;          // ������ 3D
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        // ������ AudioSource � ��� ����� (����� �� ���������� �����)
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

        // �� ���������� ������ �����
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

    // ==================== PUBLIC � ������� �� ZombieCustomer ====================

    public void PlayAttackSound()
    {
        if (attackSounds.Length > 0)
            PlayRandom(attackSounds);
    }

    public void PlayAngrySound()
    {
        if (angrySounds.Length > 0)
            PlayRandom(angrySounds);

        // ���� ����� ������ ����
        idleMinInterval = 2f;
        idleMaxInterval = 5f;
    }

    // ==================== PRIVATE ====================

    void PlayRandom(AudioClip[] clips)
    {
        if (clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        audioSource.pitch = Random.Range(0.85f, 1.15f);  // ������� ������ ���
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