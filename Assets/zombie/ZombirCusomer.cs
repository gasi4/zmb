using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ZombieCustomer : MonoBehaviour
{
    void Awake()
    {
        // На случай если на префабе состояние/рендеры сохранены в "плохом" виде
        currentState = ZombieState.Spawning;

        Renderer[] rs = GetComponentsInChildren<Renderer>(true);
        foreach (var r in rs)
            r.enabled = true;

        gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (testMode)
            Debug.LogWarning($"{gameObject.name}: отключен (OnDisable)");
    }

    [Header("Настройки зомби")]
    public float waitTime = 30f; // Время ожидания до агрессии (УСТАНАВЛИВАЕТСЯ МЕНЕДЖЕРОМ!)
    public float patienceDecreaseRate = 1f; // Скорость снижения терпения
    public float walkSpeed = 2f;
    public float angrySpeed = 4f;

    [Header("Точки движения")]
    public Transform spawnPoint; // Точка спавна
    public Transform servicePoint; // Точка обслуживания (стол)
    public Transform playerTarget; // Игрок (цель для атаки)

    [Header("Предметы")]
    public GameObject requestedItemPrefab; // Префаб запрашиваемой вещи
    public Transform[] itemSpawnPoints; // МАССИВ точек вместо одной!
    public float itemSpawnOffset = 0.5f; // Смещение между предметами

    [Header("Точка выдачи")]
    public DeliveryPoint deliveryPoint; // Ссылка на точку выдачи
    public float deliveryPickupDistance = 1f; // Расстояние на котором зомби забирает вещь

    [Header("Очередь")]
    public CustomerQueueManager queueManager;
    public ZombieSpawnManager spawnManager;
    private bool removedFromQueue = false;
    public enum ZombieState
    {
        Spawning,           // Появление
        WalkingToQueue,     // Идет к очереди
        InLine,             // Стоит в очереди (НЕ первый, без ожидания/терпения)
        Waiting,            // Ожидает заказ (ТОЛЬКО первый)
        GettingAngry,       // Начинает злиться
        Angry,              // Атакует игрока
        GoingToDelivery,    // Идет за вещью на точку выдачи
        PickingUpItem,      // Забирает вещь с точки
        Leaving             // Уходит
    }

    private GameObject itemToPickup; // Вещь которую нужно забрать

    [Header("UI")]
    public Slider patienceSlider; // Слайдер терпения
    public GameObject patienceUI; // Объект UI терпения

    [Header("UI Префабы (для автосоздания)")]
    public GameObject patienceUIPrefab; // Префаб UI

    [Header("Движение")]
    public bool useSimpleMovement = true; // Используем простую систему

    [Header("Debug")]
    public bool testMode = false;

    [Header("Статус")]
    [SerializeField] private bool itemSpawnedSuccessfully = false;

    [SerializeField] public ZombieState currentState = ZombieState.Spawning;
    private SimpleZombieMovement simpleMovement;
    private float currentPatience;
    private GameObject spawnedItem; // Созданная вещь на столе
    private bool itemDelivered = false;
    private List<GameObject> spawnedItems = new List<GameObject>(); // Все созданные предметы
    private int currentSpawnIndex = 0; // Индекс текущей точки спавна

    private static bool isQuitting = false;
    void OnApplicationQuit()
    {
        isQuitting = true;
    }
    void Start()
    {
        // Инициализация
        currentPatience = waitTime;

        // Инициализируем UI
        InitializeUI();

        // Запускаем появление
        StartCoroutine(SpawnSequence());
    }
    public void PickupItemFromPoint()
    {
        if (currentState != ZombieState.GoingToDelivery)
            return;

        currentState = ZombieState.PickingUpItem;

        if (testMode)
            Debug.Log($"{gameObject.name} забирает вещь с точки");

        // Уничтожаем запрашиваемую вещь на столе
        if (spawnedItem != null)
        {
            Destroy(spawnedItem);
            spawnedItem = null;
        }

        // Уничтожаем все созданные предметы
        ClearAllSpawnedItems();

        // Снимаемся с очереди (освобождаем место)
        LeaveQueue();

        // Уведомляем менеджер об успешной доставке
        NotifyWaveManager();

        // Короткая пауза перед уходом
        StartCoroutine(WaitAndLeave(0.5f));
    }

    IEnumerator WaitAndLeave(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Leave();
    }

    public void GoToDeliveryPoint(DeliveryPoint point)
    {
        if ((currentState != ZombieState.Waiting && currentState != ZombieState.GettingAngry) || point == null)
            return;

        // Реалистичный сдвиг очереди: как только первый ушёл с QueuePoint1 — следующий становится первым
        if (queueManager != null)
            queueManager.OnFrontZombieLeftPoint(this);

        currentState = ZombieState.GoingToDelivery;
        deliveryPoint = point;

        // Убираем UI терпения
        if (patienceUI != null)
            patienceUI.SetActive(false);

        // Идем к точке выдачи
        if (useSimpleMovement && simpleMovement != null)
        {
            simpleMovement.SetTarget(point.transform);
            simpleMovement.stoppingDistance = deliveryPickupDistance;
            simpleMovement.speed = walkSpeed;
        }

        if (testMode)
            Debug.Log($"{gameObject.name} идет к точке выдачи");
    }

    void InitializeUI()
    {
        // Вариант 1: Если UI уже назначен в инспекторе
        if (patienceUI != null)
        {
            patienceUI.SetActive(false);
            return;
        }

        // Вариант 2: Создаем UI автоматически
        if (patienceUIPrefab != null)
        {
            // Создаем UI в мировом пространстве
            patienceUI = Instantiate(patienceUIPrefab);

            // Находим слайдер в созданном UI
            patienceSlider = patienceUI.GetComponentInChildren<Slider>();

            if (patienceSlider != null)
            {
                patienceSlider.maxValue = waitTime;
                patienceSlider.value = currentPatience;
            }

            patienceUI.SetActive(false);
        }
    }

    IEnumerator SpawnSequence()
    {
        currentState = ZombieState.Spawning;

        // Короткая пауза при появлении
        yield return new WaitForSeconds(1f);

        // Если зомби управляется очередью, servicePoint может быть назначен чуть позже.
        // В этом случае НЕ переводим его в WalkingToQueue раньше времени.
        if (servicePoint != null)
        {
            GoToServicePoint();
        }
        else if (testMode)
        {
            Debug.Log($"{gameObject.name}: servicePoint еще не назначен — жду назначения очередью");
        }
    }

    public void GoToServicePoint()
    {
        // Разрешаем повторно вызывать, когда очередь сдвигает зомби вперед
        if (currentState != ZombieState.Spawning && currentState != ZombieState.WalkingToQueue && currentState != ZombieState.InLine)
            return;

        // Нельзя идти, если точка не назначена
        if (servicePoint == null)
        {
            if (testMode)
                Debug.LogWarning($"{gameObject.name}: servicePoint не назначен — не могу идти к очереди");
            return;
        }

        currentState = ZombieState.WalkingToQueue;

        // Используем простую систему движения
        if (useSimpleMovement)
        {
            // Получаем или добавляем SimpleZombieMovement
            simpleMovement = GetComponent<SimpleZombieMovement>();
            if (simpleMovement == null)
            {
                simpleMovement = gameObject.AddComponent<SimpleZombieMovement>();
                simpleMovement.speed = walkSpeed;
                simpleMovement.stoppingDistance = 1.5f;
            }

            simpleMovement.speed = walkSpeed;
            simpleMovement.stoppingDistance = 1.5f;
            simpleMovement.SetTarget(servicePoint);
        }
    }

    public void ArrivedAtServicePoint()
    {
        if (currentState != ZombieState.WalkingToQueue) return;

        // Если это НЕ первый в очереди (не QueuePoint1) — просто стоим и ждём сдвига
        if (queueManager != null && !queueManager.IsFrontZombie(this))
        {
            currentState = ZombieState.InLine;
            return;
        }

        currentState = ZombieState.Waiting;

        // Сбрасываем флаг перед попыткой создания
        itemSpawnedSuccessfully = false;

        // Поворачиваемся к столу
        if (itemSpawnPoints != null && itemSpawnPoints.Length > 0 && itemSpawnPoints[0] != null)
        {
            Vector3 direction = (itemSpawnPoints[0].position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        // Спавним запрашиваемую вещь на столе (только у первого)
        SpawnRequestedItem();

        // Проверяем успешность создания перед показом UI
        if (itemSpawnedSuccessfully)
        {
            // Показываем UI терпения если он существует
            if (patienceUI != null)
            {
                patienceUI.SetActive(true);
            }

            // Устанавливаем начальное терпение
            currentPatience = waitTime;

            if (patienceSlider != null)
            {
                patienceSlider.maxValue = waitTime;
                patienceSlider.value = currentPatience;
            }
        }
        else
        {
            // Если предмет не создан - обрабатываем ошибку
            if (spawnedItem == null)
            {
                CantSpawnItem();
            }
            else
            {
                // Если spawnedItem существует, но флаг не установлен - исправляем
                itemSpawnedSuccessfully = true;
            }
        }
    }

    void Update()
    {
        // Проверяем достиг ли цели (простая версия)
        if (currentState == ZombieState.WalkingToQueue && servicePoint != null)
        {
            float distance = Vector3.Distance(transform.position, servicePoint.position);
            if (distance < 1.5f)
            {
                ArrivedAtServicePoint();
            }
        }

        // Проверяем достиг ли точки выдачи
        if (currentState == ZombieState.GoingToDelivery && deliveryPoint != null)
        {
            float distance = Vector3.Distance(transform.position, deliveryPoint.transform.position);
            if (distance <= deliveryPickupDistance)
            {
                // Достигли точки выдачи, забираем вещь
                PickupItemFromPoint();
            }
        }

        if (currentState == ZombieState.Waiting)
        {
            UpdatePatience();
        }
    }


    void SpawnRequestedItem()
    {
        if (requestedItemPrefab == null)
        {
            Debug.LogError($"{gameObject.name}: Нет префаба предмета для спавна!");
            itemSpawnedSuccessfully = false;
            return;
        }

        // Находим свободную точку спавна
        Transform spawnPoint = GetFreeSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.LogError($"{gameObject.name}: Не могу найти точку спавна!");
            itemSpawnedSuccessfully = false;
            return;
        }

        // Создаем предмет
        spawnedItem = Instantiate(requestedItemPrefab, spawnPoint.position, spawnPoint.rotation);
        spawnedItem.name = $"RequestedItem_{gameObject.name}";

        // Устанавливаем флаг УСПЕШНОГО создания
        itemSpawnedSuccessfully = true;

        // Добавляем в список
        spawnedItems.Add(spawnedItem);

        // Проверяем MeshRenderer/Renderer
        Renderer renderer = spawnedItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
        }
        else
        {
            // Ищем в дочерних объектах
            Renderer[] childRenderers = spawnedItem.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in childRenderers)
            {
                r.enabled = true;
            }
        }

        // Добавляем скрипт для взаимодействия
        ZombieRequestItem interactable = spawnedItem.GetComponent<ZombieRequestItem>();
        if (interactable == null)
        {
            interactable = spawnedItem.AddComponent<ZombieRequestItem>();
        }
        interactable.SetZombieCustomer(this);
    }

    Transform GetFreeSpawnPoint()
    {
        if (itemSpawnPoints == null || itemSpawnPoints.Length == 0)
        {
            return CreateTemporarySpawnPoint();
        }

        // Ищем свободную точку
        for (int i = 0; i < itemSpawnPoints.Length; i++)
        {
            int index = (currentSpawnIndex + i) % itemSpawnPoints.Length;

            if (itemSpawnPoints[index] == null) continue;

            // Проверяем нет ли предмета на этой точке
            bool isOccupied = IsSpawnPointOccupied(itemSpawnPoints[index].position);

            if (!isOccupied)
            {
                currentSpawnIndex = (index + 1) % itemSpawnPoints.Length;
                return itemSpawnPoints[index];
            }
        }

        // Если все точки заняты, создаем рядом со случайной
        Transform randomPoint = itemSpawnPoints[Random.Range(0, itemSpawnPoints.Length)];
        if (randomPoint != null)
        {
            Vector3 offsetPos = randomPoint.position +
                new Vector3(Random.Range(-itemSpawnOffset, itemSpawnOffset),
                           0,
                           Random.Range(-itemSpawnOffset, itemSpawnOffset));
            GameObject tempPoint = new GameObject("TempSpawnPoint");
            tempPoint.transform.position = offsetPos;
            tempPoint.transform.rotation = randomPoint.rotation;
            Destroy(tempPoint, 10f);

            return tempPoint.transform;
        }

        return CreateTemporarySpawnPoint();
    }

    Transform CreateTemporarySpawnPoint()
    {
        // Создаем точку рядом со столом
        Vector3 tablePosition = FindTablePosition();
        Vector3 spawnPosition = tablePosition + new Vector3(
            Random.Range(-1f, 1f),
            0.5f,
            Random.Range(-1f, 1f)
        );

        GameObject tempPoint = new GameObject($"TempSpawn_{gameObject.name}");
        tempPoint.transform.position = spawnPosition;
        tempPoint.transform.rotation = Quaternion.identity;
        Destroy(tempPoint, 30f);

        return tempPoint.transform;
    }

    Vector3 FindTablePosition()
    {
        // Ищем стол в сцене
        GameObject table = GameObject.FindGameObjectWithTag("Table");
        if (table != null) return table.transform.position;

        // Или используем servicePoint
        if (servicePoint != null) return servicePoint.position;

        // Или позицию зомби
        return transform.position + transform.forward * 2f;
    }

    bool IsSpawnPointOccupied(Vector3 position)
    {
        // Проверяем коллайдерами
        Collider[] colliders = Physics.OverlapSphere(position, 0.3f);

        foreach (Collider col in colliders)
        {
            // Если на точке уже есть предмет (не сам зомби)
            if (col.gameObject != gameObject &&
                (col.CompareTag("Item") || col.GetComponent<Item>() != null))
            {
                return true;
            }
        }

        return false;
    }

    void CantSpawnItem()
    {
        // Если предмета действительно нет
        GetAngry();
    }

    void UpdatePatience()
    {
        // Важная проверка: если предмет не создан или уже отдан, не обновляем терпение
        if (!itemSpawnedSuccessfully || itemDelivered)
        {
            return;
        }

        currentPatience -= Time.deltaTime * patienceDecreaseRate;

        // Обновляем UI ТОЛЬКО если слайдер существует
        if (patienceSlider != null)
        {
            patienceSlider.value = currentPatience;

            // Меняем цвет в зависимости от терпения
            Image fillImage = patienceSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                if (currentPatience < waitTime * 0.3f)
                    fillImage.color = Color.red;
                else if (currentPatience < waitTime * 0.6f)
                    fillImage.color = Color.yellow;
                else
                    fillImage.color = Color.green;
            }
        }

        // Проверяем терпение
        if (currentPatience <= 0 && currentState != ZombieState.Angry)
        {
            GetAngry();
        }
        else if (currentPatience <= waitTime * 0.5f && currentState == ZombieState.Waiting)
        {
            StartGettingAngry();
        }
    }

    void StartGettingAngry()
    {
        currentState = ZombieState.GettingAngry;
    }

    void GetAngry()
    {
        // Проверяем состояние
        if (currentState == ZombieState.Angry) return;

        currentState = ZombieState.Angry;

        // Снимаемся с очереди (освобождаем место)
        LeaveQueue();

        // Уведомляем менеджер волн о завершении зомби
        NotifyWaveManager();

        // Убираем UI
        if (patienceUI != null)
        {
            patienceUI.SetActive(false);
        }

        // Уничтожаем запрашиваемую вещь (если существует)
        if (spawnedItem != null)
        {
            Destroy(spawnedItem);
            spawnedItem = null;
        }

        // Начинаем преследовать игрока
        if (playerTarget != null)
        {
            SimpleZombieMovement movement = GetComponent<SimpleZombieMovement>();
            if (movement != null)
            {
                movement.SetTarget(playerTarget);
                movement.speed = angrySpeed;
            }
        }
    }

    public void DeliverItem(GameObject deliveredItem)
    {
        // Этот метод теперь вызывается из DeliveryPoint
        // Старая логика удалена
        if (testMode)
            Debug.Log($"DeliverItem вызван, но используется новая система");
    }

    void ClearAllSpawnedItems()
    {
        foreach (GameObject item in spawnedItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedItems.Clear();
    }

    void LeaveQueue()
    {
        if (removedFromQueue) return;
        removedFromQueue = true;
        if (queueManager != null)
            queueManager.RemoveZombie(this);
    }

    void Leave()
    {
        currentState = ZombieState.Leaving;
        LeaveQueue();

        // Идем к точке спавна
        if (spawnPoint != null)
        {
            SimpleZombieMovement movement = GetComponent<SimpleZombieMovement>();
            if (movement != null)
            {
                movement.SetTarget(spawnPoint);
                movement.speed = walkSpeed;
            }
        }
        else
        {
            // Если нет точки спавна, просто уничтожаем
            Destroy(gameObject);
        }
    }

    // Уведомляем менеджер волн о завершении зомби
    void NotifyWaveManager()
    {
        // Ищем менеджер через синглтон
        ZombieWaveManager waveManager = ZombieWaveManager.Instance;

        // ПРОВЕРЯЕМ ЕСТЬ ЛИ ОН ВООБЩЕ В СЦЕНЕ
        if (waveManager != null)
        {
            waveManager.OnZombieFinished(this);
        }
        else
        {
            // Если менеджера нет, логируем только в debug режиме
            if (testMode)
                Debug.LogWarning($"Зомби {gameObject.name}: не найден ZombieWaveManager! " +
                               $"Это нормально при завершении игры.");
        }
    }

    void OnDestroy()
    {
        ClearAllSpawnedItems();
        LeaveQueue();

        // Сообщаем спавнеру, чтобы он мог завершать волны корректно
        if (spawnManager != null)
            spawnManager.NotifyZombieRemoved(this);

        // Уведомляем менеджер ТОЛЬКО если игра еще активна
        // и мы не в процессе выхода из игры
        if (currentState != ZombieState.Leaving &&
            !isQuitting &&
            Application.isPlaying) // Добавляем проверку что игра запущена
        {
            NotifyWaveManager();
        }
    }

    public void SetupZombie(Transform spawn, Transform service, Transform player, float waitTimeSeconds, DeliveryPoint delivery = null, CustomerQueueManager queue = null, ZombieSpawnManager spawner = null)
    {
        spawnPoint = spawn;
        servicePoint = service;
        playerTarget = player;
        waitTime = waitTimeSeconds;
        currentPatience = waitTime;
        deliveryPoint = delivery;
        queueManager = queue;
        spawnManager = spawner;

        if (patienceSlider != null)
        {
            patienceSlider.maxValue = waitTime;
            patienceSlider.value = currentPatience;
        }

        if (testMode)
            Debug.Log($"Зомби {gameObject.name} настроен: waitTime={waitTime}s, deliveryPoint={delivery != null}");
    }

    void OnDrawGizmosSelected()
    {
        // Визуализация в редакторе
        if (servicePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, servicePoint.position);
        }

        if (spawnPoint != null && currentState == ZombieState.Leaving)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, spawnPoint.position);
        }
    }
}