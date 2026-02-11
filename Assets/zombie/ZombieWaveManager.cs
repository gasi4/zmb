using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ZombieWaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public string waveName = "Волна";
        public int zombiesCount = 3; // Количество зомби в волне
        public float zombieWaitTime = 30f; // Время ожидания для зомби в этой волне
        public float timeBetweenZombies = 2f; // Время между появлением зомби
        public float waveStartDelay = 0f; // Задержка перед началом волны
    }

    [Header("Настройки волн")]
    public Wave[] waves;
    public int currentWaveIndex = 0;
    public bool loopWaves = false; // Зациклить волны после последней

    [Header("Точки спавна")]
    public Transform[] spawnPoints; // Несколько точек спавна для разнообразия
    public Transform servicePoint; // Точка обслуживания (стол)
    public Transform playerTarget; // Игрок

    [Header("Префабы")]
    public GameObject zombiePrefab; // Префаб зомби-клиента

    [Header("UI")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI zombiesLeftText;
    public TextMeshProUGUI waveStatusText;
    public Slider waveProgressSlider;

    [Header("Звуки и эффекты")]
    public AudioClip waveCompleteSound;
    public AudioClip newWaveSound;
    private AudioSource audioSource;

    [Header("Настройки игры")]
    public float gameStartDelay = 3f; // Задержка перед началом первой волны

    [Header("Debug")]
    public bool testMode = false; // <-- ДОБАВЛЕНО ЭТО!

    // Приватные переменные
    private List<GameObject> currentWaveZombies = new List<GameObject>();
    private List<ZombieCustomer> activeZombies = new List<ZombieCustomer>();
    private int zombiesSpawnedInCurrentWave = 0;
    private int zombiesKilledInCurrentWave = 0;
    private int zombiesDespawnedInCurrentWave = 0;
    private bool isWaveActive = false;
    private Coroutine spawnCoroutine;

    // Синглтон для легкого доступа
    public static ZombieWaveManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = 0.5f;
        }
    }

    void Start()
    {
        InitializeUI();
        StartCoroutine(GameStartSequence());
    }

    void InitializeUI()
    {
        if (waveProgressSlider != null)
        {
            waveProgressSlider.maxValue = 1f;
            waveProgressSlider.value = 0f;
        }

        UpdateWaveUI();
    }

    IEnumerator GameStartSequence()
    {
        if (waveStatusText != null)
            waveStatusText.text = "ПРИГОТОВЬТЕСЬ...";

        yield return new WaitForSeconds(gameStartDelay);

        StartNextWave();
    }

    public void StartNextWave()
    {
        if (currentWaveIndex >= waves.Length)
        {
            if (loopWaves)
            {
                currentWaveIndex = 0;
            }
            else
            {
                GameComplete();
                return;
            }
        }

        StartCoroutine(StartWaveCoroutine(waves[currentWaveIndex]));
    }

    IEnumerator StartWaveCoroutine(Wave wave)
    {
        // Очищаем списки
        currentWaveZombies.Clear();
        activeZombies.Clear();
        zombiesSpawnedInCurrentWave = 0;
        zombiesKilledInCurrentWave = 0;
        zombiesDespawnedInCurrentWave = 0;
        isWaveActive = true;

        // Обновляем UI
        UpdateWaveUI();

        if (waveStatusText != null)
            waveStatusText.text = $"ВОЛНА {currentWaveIndex + 1} НАЧИНАЕТСЯ...";

        // Задержка перед началом волны
        if (wave.waveStartDelay > 0)
        {
            yield return new WaitForSeconds(wave.waveStartDelay);
        }

        // Звук новой волны
        if (newWaveSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(newWaveSound);
        }

        if (waveStatusText != null)
            waveStatusText.text = "ВОЛНА АКТИВНА";

        // Запускаем спавн зомби
        spawnCoroutine = StartCoroutine(SpawnZombiesWave(wave));

        // Ждем пока все зомби не будут созданы
        yield return new WaitUntil(() => zombiesSpawnedInCurrentWave >= wave.zombiesCount);

        // Ждем пока все зомби не будут обработаны (убиты или уйдут)
        yield return new WaitUntil(() => zombiesKilledInCurrentWave >= wave.zombiesCount);

        // Волна завершена
        CompleteCurrentWave();
    }

    IEnumerator SpawnZombiesWave(Wave wave)
    {
        for (int i = 0; i < wave.zombiesCount; i++)
        {
            // Ждем, пока предыдущий зомби не уйдет или не будет убит
            // Только если это не первый зомби в волне
            if (i > 0)
            {
                // Ждем пока текущий активный зомби не уйдет
                yield return new WaitUntil(() => activeZombies.Count == 0);

                // Дополнительная задержка между зомби
                if (wave.timeBetweenZombies > 0)
                {
                    yield return new WaitForSeconds(wave.timeBetweenZombies);
                }
            }

            // Спавним зомби
            SpawnZombie(wave.zombieWaitTime);
            zombiesSpawnedInCurrentWave++;

            // Обновляем UI
            UpdateWaveUI();

            yield return null;
        }
    }

    void SpawnZombie(float waitTime)
    {
        if (zombiePrefab == null)
        {
            Debug.LogError("Zombie prefab не назначен!");
            return;
        }

        // Выбираем случайную точку спавна
        Transform spawnPoint = GetRandomSpawnPoint();

        // Создаем зомби
        GameObject zombieObj = Instantiate(zombiePrefab, spawnPoint.position, spawnPoint.rotation);
        zombieObj.name = $"Zombie_Wave{currentWaveIndex + 1}_#{zombiesSpawnedInCurrentWave + 1}";

        // Получаем компонент ZombieCustomer
        ZombieCustomer zombieCustomer = zombieObj.GetComponent<ZombieCustomer>();
        if (zombieCustomer != null)
        {
            // Настраиваем зомби
            zombieCustomer.SetupZombie(
                spawnPoint,
                servicePoint,
                playerTarget,
                waitTime,
                GetDeliveryPoint() // ← ПЕРЕДАЕМ ТОЧКУ ВЫДАЧИ
            );

            // Добавляем в списки
            currentWaveZombies.Add(zombieObj);
            activeZombies.Add(zombieCustomer);

            if (testMode)
                Debug.Log($"👤 Создан зомби {zombieObj.name}, время ожидания: {waitTime} сек");
        }
        else
        {
            Debug.LogError($"У префаба {zombiePrefab.name} нет компонента ZombieCustomer!");
            Destroy(zombieObj);
        }
    }

    DeliveryPoint GetDeliveryPoint()
    {
        // Ищем DeliveryPoint в сцене
        DeliveryPoint point = FindObjectOfType<DeliveryPoint>();
        if (point == null && testMode)
        {
            Debug.LogWarning("Не найден DeliveryPoint в сцене!");
        }
        return point;
    }

    Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            // Создаем временную точку если массив пустой
            GameObject tempPoint = new GameObject("TempSpawnPoint");
            tempPoint.transform.position = transform.position + transform.forward * 5f;
            tempPoint.transform.rotation = Quaternion.identity;
            return tempPoint.transform;
        }

        return spawnPoints[Random.Range(0, spawnPoints.Length)];
    }

    // Вызывается зомби когда он завершил "обслуживание" (получил вещь/ушёл в агр и т.п.)
    public void OnZombieFinished(ZombieCustomer zombie)
    {
        if (activeZombies.Contains(zombie))
        {
            activeZombies.Remove(zombie);
            zombiesKilledInCurrentWave++;

            if (testMode)
                Debug.Log($"✅ Зомби завершил обслуживание. Осталось: {activeZombies.Count} активных");

            UpdateWaveUI();

            // Проверяем не завершена ли волна
            if (zombiesKilledInCurrentWave >= waves[currentWaveIndex].zombiesCount)
            {
                if (testMode)
                    Debug.Log($"Все зомби волны {currentWaveIndex + 1} обработаны");
            }
        }
    }

    // Вызывается когда объект зомби реально уничтожен (Destroy) — нужно для победного экрана
    public void OnZombieDespawned(ZombieCustomer zombie)
    {
        zombiesDespawnedInCurrentWave++;

        if (testMode)
            Debug.Log($"🧹 Зомби исчез (Destroy). Despawned: {zombiesDespawnedInCurrentWave}");
    }

    void CompleteCurrentWave()
    {
        isWaveActive = false;

        // Запоминаем размер текущей волны (до инкремента индекса)
        int finishedWaveZombieCount = waves[currentWaveIndex].zombiesCount;

        // Звук завершения волны
        if (waveCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(waveCompleteSound);
        }

        // Обновляем UI
        if (waveStatusText != null)
            waveStatusText.text = "ВОЛНА ЗАВЕРШЕНА";

        if (testMode)
            Debug.Log($"🎉 Волна {currentWaveIndex + 1} завершена!");

        // Переходим к следующей волне
        currentWaveIndex++;

        // Запускаем следующую волну через задержку
        if (currentWaveIndex < waves.Length || loopWaves)
        {
            float delayBeforeNextWave = 3f; // 3 секунды между волнами
            StartCoroutine(StartNextWaveWithDelay(delayBeforeNextWave));
        }
        else
        {
            // Победа должна показываться только после того, как последний зомби реально ушёл (Destroy)
            StartCoroutine(WaitLastZombieDespawnThenComplete(finishedWaveZombieCount));
        }
    }

    IEnumerator WaitLastZombieDespawnThenComplete(int expectedDespawnCount)
    {
        yield return new WaitUntil(() => zombiesDespawnedInCurrentWave >= expectedDespawnCount);
        GameComplete();
    }

    IEnumerator StartNextWaveWithDelay(float delay)
    {
        if (waveStatusText != null)
            waveStatusText.text = $"СЛЕДУЮЩАЯ ВОЛНА ЧЕРЕЗ {delay}С...";

        yield return new WaitForSeconds(delay);

        StartNextWave();
    }

    void UpdateWaveUI()
    {
        if (waves == null || currentWaveIndex >= waves.Length) return;

        Wave currentWave = waves[currentWaveIndex];

        // Текст волны
        if (waveText != null)
        {
            waveText.text = $"ВОЛНА: {currentWaveIndex + 1}/{waves.Length}";
        }

        // Текст оставшихся зомби
        if (zombiesLeftText != null)
        {
            int zombiesLeft = currentWave.zombiesCount - zombiesKilledInCurrentWave;
            zombiesLeftText.text = $"ЗОМБИ: {zombiesLeft}/{currentWave.zombiesCount}";
        }

        // Прогресс волны
        if (waveProgressSlider != null)
        {
            float progress = 0f;
            if (currentWave.zombiesCount > 0)
            {
                progress = (float)zombiesKilledInCurrentWave / currentWave.zombiesCount;
            }
            waveProgressSlider.value = progress;
        }

        // Дополнительная информация в статусе если волна активна
        if (isWaveActive && waveStatusText != null && zombiesSpawnedInCurrentWave < currentWave.zombiesCount)
        {
            int zombiesToSpawn = currentWave.zombiesCount - zombiesSpawnedInCurrentWave;
            waveStatusText.text = $"ЖДЕМ ЗОМБИ: {zombiesToSpawn} ОСТАЛОСЬ";
        }
    }

    void GameComplete()
    {
        isWaveActive = false;

        if (waveStatusText != null)
            waveStatusText.text = "ИГРА ЗАВЕРШЕНА!";

        if (testMode)
            Debug.Log("🎮 ИГРА ЗАВЕРШЕНА! Все волны пройдены.");

        // В этом проекте победный экран показывает ZombieSpawnManager.
        // (ZombieWaveManager может оставаться в сцене для UI, но победу не триггерит.)
    }

    // Метод для принудительного запуска волны (для тестирования)
    public void StartWave(int waveIndex)
    {
        if (waveIndex >= 0 && waveIndex < waves.Length)
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
            }

            currentWaveIndex = waveIndex;
            StartNextWave();
        }
    }

    // Метод для перезапуска игры
    public void RestartGame()
    {
        // Останавливаем все корутины
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        // Уничтожаем всех текущих зомби
        foreach (GameObject zombie in currentWaveZombies)
        {
            if (zombie != null)
            {
                Destroy(zombie);
            }
        }

        // Сбрасываем переменные
        currentWaveIndex = 0;
        currentWaveZombies.Clear();
        activeZombies.Clear();
        zombiesSpawnedInCurrentWave = 0;
        zombiesKilledInCurrentWave = 0;
        isWaveActive = false;

        // Перезапускаем игру
        StartCoroutine(GameStartSequence());
    }

    // Метод для проверки активна ли волна
    public bool IsWaveActive()
    {
        return isWaveActive;
    }

    // Метод для получения текущей волны
    public Wave GetCurrentWave()
    {
        if (currentWaveIndex < waves.Length)
        {
            return waves[currentWaveIndex];
        }
        return null;
    }

    void OnDestroy()
    {
        // Очищаем при уничтожении
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
    }

    // Для отладки в редакторе
    void OnGUI()
    {
        if (Application.isEditor && testMode) // <-- ТЕПЕРЬ testMode СУЩЕСТВУЕТ
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== Wave Manager Debug ===");
            GUILayout.Label($"Текущая волна: {currentWaveIndex + 1}/{waves.Length}");
            GUILayout.Label($"Зомби создано: {zombiesSpawnedInCurrentWave}");
            GUILayout.Label($"Зомби обработано: {zombiesKilledInCurrentWave}");
            GUILayout.Label($"Активных зомби: {activeZombies.Count}");
            GUILayout.Label($"Волна активна: {isWaveActive}");

            if (GUILayout.Button("Следующая волна"))
            {
                StartNextWave();
            }

            if (GUILayout.Button("Перезапуск"))
            {
                RestartGame();
            }

            GUILayout.EndArea();
        }
    }
}