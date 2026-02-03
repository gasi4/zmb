using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieSpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public string waveName = "Волна";
        public int zombiesCount = 3;
        public float timeBetweenZombies = 20f;
        public float zombieWaitTime = 30f;
        public float waveStartDelay = 0f;
    }

    [Header("Префабы")]
    public GameObject zombiePrefab;
    public GameObject[] itemPrefabs;

    [Header("Точки")]
    public Transform[] spawnPoints;
    public Transform player;
    public DeliveryPoint deliveryPoint;

    [Header("Очередь")]
    public CustomerQueueManager queueManager;

    [Header("Волны")]
    public Wave[] waves;
    public bool loopWaves = false;

    [Header("Debug")]
    public bool debugMode = true;

    private readonly List<ZombieCustomer> activeZombies = new List<ZombieCustomer>();
    private Coroutine wavesCoroutine;

    void Awake()
    {
        // Быстрый чек на дубли (частая причина "ничего не спавнится")
        ZombieSpawnManager[] all = FindObjectsOfType<ZombieSpawnManager>();
        if (all != null && all.Length > 1)
        {
            Debug.LogWarning($"ZombieSpawnManager: в сцене найдено {all.Length} экземпляров! Оставь один.");
        }
    }

    void OnEnable()
    {
        // Если объект был выключен/включен — гарантируем запуск
        TryStartWaves();
    }

    void Start()
    {
        TryStartWaves();
    }

    void TryStartWaves()
    {
        if (deliveryPoint == null)
            deliveryPoint = FindObjectOfType<DeliveryPoint>();

        if (queueManager == null)
            queueManager = FindObjectOfType<CustomerQueueManager>();

        if (debugMode)
        {
            int wavesCount = waves != null ? waves.Length : 0;
            Debug.Log($"ZombieSpawnManager: запуск. waves={wavesCount}, loopWaves={loopWaves}");
        }

        if (wavesCoroutine != null)
            StopCoroutine(wavesCoroutine);

        wavesCoroutine = StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogWarning("ZombieSpawnManager: waves не настроены!");
            yield break;
        }

        int waveIndex = 0;
        while (true)
        {
            if (waveIndex >= waves.Length)
            {
                if (!loopWaves) yield break;
                waveIndex = 0;
            }

            Wave wave = waves[waveIndex];
            if (debugMode) Debug.Log($"🌊 Старт волны {waveIndex + 1}: {wave.waveName}");

            if (wave.waveStartDelay > 0f)
                yield return new WaitForSeconds(wave.waveStartDelay);

            for (int i = 0; i < wave.zombiesCount; i++)
            {
                if (debugMode)
                    Debug.Log($"🧟 Спавн зомби {i + 1}/{wave.zombiesCount} (интервал {wave.timeBetweenZombies}с)");

                try
                {
                    SpawnZombie(wave.zombieWaitTime);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ZombieSpawnManager: SpawnZombie упал с ошибкой: {e.Message}");
                }

                if (wave.timeBetweenZombies > 0f)
                    yield return new WaitForSeconds(wave.timeBetweenZombies);
            }

            // Волна считается завершенной, когда все зомби из этой волны исчезнут
            yield return new WaitUntil(() => activeZombies.Count == 0);

            if (debugMode) Debug.Log($"✅ Волна {waveIndex + 1} завершена");
            waveIndex++;
        }
    }

    void SpawnZombie(float waitTime)
    {
        if (zombiePrefab == null)
        {
            Debug.LogError("ZombieSpawnManager: zombiePrefab не назначен!");
            return;
        }

        // Подсказка: здесь должен быть prefab из Project, а не объект из Hierarchy.
        if (zombiePrefab.scene.IsValid())
        {
            Debug.LogWarning("ZombieSpawnManager: zombiePrefab ссылается на объект из сцены. Перетащи prefab-ассет из Project, иначе выключенный объект будет порождать выключенные клоны.");
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("ZombieSpawnManager: spawnPoints не назначены!");
            return;
        }

        // Берем случайную НЕ-null точку спавна
        Transform sp = null;
        for (int i = 0; i < 20; i++)
        {
            Transform candidate = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (candidate != null)
            {
                sp = candidate;
                break;
            }
        }

        if (sp == null)
        {
            Debug.LogError("ZombieSpawnManager: все spawnPoints пустые (null) — не могу создать зомби");
            return;
        }

        // ВАЖНО: если в поле zombiePrefab случайно назначен объект из сцены,
        // то при выключенном объекте клон тоже будет выключенным.
        GameObject zombieObj = Instantiate(zombiePrefab, sp.position, sp.rotation);
        zombieObj.SetActive(true);

        // На всякий случай включаем рендеры у детей
        foreach (var r in zombieObj.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        ZombieCustomer zombie = zombieObj.GetComponent<ZombieCustomer>();
        if (zombie == null)
        {
            Debug.LogError("У префаба зомби нет ZombieCustomer!");
            Destroy(zombieObj);
            return;
        }

        // Заказ
        if (itemPrefabs != null && itemPrefabs.Length > 0)
            zombie.requestedItemPrefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];

        // Настройка
        zombie.SetupZombie(sp, null, player, waitTime, deliveryPoint, queueManager, this);

        // В очередь (зомби пойдет к свободному servicePoint)
        if (queueManager != null)
        {
            try
            {
                queueManager.AddToQueue(zombie);
            }
            catch (System.Exception e)
            {
                // Любая ошибка в очереди не должна останавливать волны/спавн
                Debug.LogError($"ZombieSpawnManager: ошибка при постановке в очередь: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ZombieSpawnManager: queueManager не найден, зомби не будет стоять в очереди");
        }

        activeZombies.Add(zombie);

        if (debugMode) Debug.Log($"🧟 Spawn zombie: {zombieObj.name}");
    }

    public void NotifyZombieRemoved(ZombieCustomer zombie)
    {
        if (zombie == null) return;
        activeZombies.Remove(zombie);
    }

    public List<ZombieCustomer> GetActiveZombies() => activeZombies;
}