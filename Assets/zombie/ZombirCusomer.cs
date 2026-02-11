
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ZombieCustomer : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Animator на зомби (если не задан — будет найден в детях).")]
    public Animator animator;
    [Tooltip("Trigger для удара (если используешь Trigger в Animator).")]
    public string attackTrigger = "Attack";
    [Tooltip("Bool для состояния атаки (если используешь bool в Animator).")]
    public string isAttackingBool = "IsAttacking";
    [Tooltip("Если true — выставляем IsAttacking bool вместо Trigger.")]
    public bool useIsAttackingBool = false;

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
    public float interactionDistance = 1f; // дистанция для НЕ-боевых взаимодействий (очередь/выдача/уход)

    [Header("Attack")]
    public float attackDamage = 25f;
    public float attackCooldown = 1f;
    public float attackRange = 1.6f;
    private float lastAttackTime = -999f;
    private PlayerHealth cachedPlayerHealth;
    private Coroutine attackCoroutine;

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
    private bool waveManagerNotified = false;
    private bool waveManagerDespawnNotified = false;
    private int currentSpawnIndex = 0; // Индекс текущей точки спавна
    private static bool isQuitting = false;

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

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

        // Если вещь лежит на DeliveryPoint — удаляем её, даже если PickupItemFromPoint вызван не из DeliveryPoint
        if (deliveryPoint != null)
            deliveryPoint.ForceClearForZombie(this);

        // Уничтожаем все созданные предметы
        ClearAllSpawnedItems();

        // Снимаемся с очереди (освобождаем место)
        LeaveQueue();

        // Уведомляем менеджер об успешной доставке (волна считается пройденной по этому событию)
        NotifyWaveManager();

        // Короткая пауза перед уходом
        StartCoroutine(WaitAndLeave(0.5f)); ;
    }

    IEnumerator WaitAndLeave(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Leave();
    }

    public void GoToDeliveryPoint(DeliveryPoint point)
    {
        if ((currentState != ZombieState.Waiting && currentState != ZombieState.GettingAngry && currentState != ZombieState.Angry) || point == null)
            return;

        // Реалистичный сдвиг очереди: как только первый ушёл с QueuePoint1 — следующий становится первым
        if (queueManager != null)
            queueManager.OnFrontZombieLeftPoint(this);

        currentState = ZombieState.GoingToDelivery;
        deliveryPoint = point;

        // Если зомби был в агре — прекращаем атаки и идём за вещью
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // Убираем UI терпения
        if (patienceUI != null)
            patienceUI.SetActive(false);

        // Идем к точке выдачи
        if (useSimpleMovement)
        {
            if (simpleMovement == null)
                simpleMovement = GetComponent<SimpleZombieMovement>();

            if (simpleMovement == null)
                simpleMovement = gameObject.AddComponent<SimpleZombieMovement>();

            Transform targetTf = point.dropPosition != null ? point.dropPosition : point.transform;
            simpleMovement.SetTarget(targetTf);

            // Останавливаемся "по краю" сферы pickupRadius, а не в центре точки.
            // Чуть меньше радиуса, чтобы гарантированно попасть в условие distance <= pickupRadius.
            simpleMovement.stoppingDistance = Mathf.Max(0.1f, point.pickupRadius - 0.05f);
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
            simpleMovement.stoppingDistance = interactionDistance;
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
            if (distance < interactionDistance)
            {
                ArrivedAtServicePoint();
            }
        }

        // НЕ подбираем вещь сами по дистанции: этим занимается DeliveryPoint (он знает currentItem/pickupRadius).
        // Иначе мы можем переключить состояние раньше и DeliveryPoint не отдаст предмет.
        if (currentState == ZombieState.GoingToDelivery && deliveryPoint != null)
        {
            // ничего
        }

        // Терпение должно убывать и в Waiting, и в GettingAngry (раньше останавливалось на 50%).
        if (currentState == ZombieState.Waiting || currentState == ZombieState.GettingAngry)
        {
            UpdatePatience();
        }

        // Атака по кд даже если стоим на месте (через дистанцию до capsule collider)
        if (currentState == ZombieState.Angry)
        {
            if (cachedPlayerHealth == null)
                cachedPlayerHealth = playerTarget != null ? playerTarget.GetComponentInParent<PlayerHealth>() : null;

            if (cachedPlayerHealth == null)
                cachedPlayerHealth = FindObjectOfType<PlayerHealth>();

            TryAttack(cachedPlayerHealth);
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

        // ВАЖНО: при агре НЕ считаем зомби "завершённым" и НЕ удаляем запрашиваемую вещь.
        // Игрок должен иметь возможность забрать её, постирать и принести на DeliveryPoint.

        // Убираем UI терпения
        if (patienceUI != null)
        {
            patienceUI.SetActive(false);
        }

        // Привязываем цель к реальному объекту игрока (а не к камере/ригу), чтобы дистанция считалась правильно
        if (cachedPlayerHealth == null)
            cachedPlayerHealth = playerTarget != null ? playerTarget.GetComponentInParent<PlayerHealth>() : null;

        if (cachedPlayerHealth == null)
            cachedPlayerHealth = FindObjectOfType<PlayerHealth>();

        if (cachedPlayerHealth != null)
            playerTarget = cachedPlayerHealth.transform;

        // Начинаем преследовать игрока
        if (playerTarget != null)
        {
            SimpleZombieMovement movement = GetComponent<SimpleZombieMovement>();
            if (movement != null)
            {
                movement.SetTarget(playerTarget);

                // Требование: скорость = 1.5 * значение, заданное до начала (берём walkSpeed как базовую)
                movement.speed = walkSpeed * 1.5f;

                // Держим дистанцию перед игроком: примерно attackRange (чуть меньше, чтобы стабильно входить в радиус удара),
                // но не слишком маленькую, чтобы не "входить" в игрока.
                movement.stoppingDistance = Mathf.Clamp(attackRange - 0.2f, 0.8f, 2.5f);
            }
        }

        // Сразу проигрываем атаку, чтобы визуально не "зависал" в Idle при старте агра
        PlayAttackAnimation();

        // Запускаем "удары" по кд отдельной корутиной — так урон идёт даже когда оба стоят на месте
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackLoop());
    }

    void TryAttack(PlayerHealth ph)
    {
        if (ph == null) return;

        // Во время атаки всегда разворачиваемся к игроку (по XZ), чтобы удар выглядел правильно
        if (playerTarget != null)
        {
            Vector3 lookDir = playerTarget.position - transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 12f * Time.deltaTime);
        }

        // Бьём только если реально в радиусе.
        // Считаем дистанцию по XZ до ближайшей точки capsule (иначе из-за Y может не бить в упор).
        Vector2 zXZ = new Vector2(transform.position.x, transform.position.z);
        float dist = Vector2.Distance(zXZ, new Vector2(ph.transform.position.x, ph.transform.position.z));

        CapsuleCollider cc = ph.GetComponentInChildren<CapsuleCollider>();
        if (cc != null)
        {
            Vector3 closest = cc.ClosestPoint(transform.position);
            dist = Vector2.Distance(zXZ, new Vector2(closest.x, closest.z));
        }

        // Допуск, чтобы удар срабатывал стабильно даже когда оба стоят на месте
        // и когда коллайдеры/выталкивание держат дистанцию чуть больше attackRange.
        if (dist > attackRange + 0.9f) return;

        if (Time.time - lastAttackTime < attackCooldown) return;

        lastAttackTime = Time.time;

        // Анимация удара
        PlayAttackAnimation();

        ph.TakeDamage(attackDamage);
    }

    void PlayAttackAnimation()
    {
        if (animator == null) return;

        // Вариант 1: Trigger Attack
        if (!useIsAttackingBool && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        // Вариант 2: bool IsAttacking
        if (useIsAttackingBool && !string.IsNullOrEmpty(isAttackingBool))
            animator.SetBool(isAttackingBool, true);
    }

    void StopAttackAnimationBool()
    {
        if (!useIsAttackingBool) return;

        if (animator == null) return;

        if (!string.IsNullOrEmpty(isAttackingBool))
            animator.SetBool(isAttackingBool, false);
    }

    // Убрано OnCollisionStay и OnTriggerStay, т.к. теперь урон по dist в TryAttack (из Update/AttackLoop).
    // Это позволяет наносить урон без Trigger на зомби (чтобы не проходить сквозь), 
    // но с физическим collider'ом на игроке (для барьера).

    System.Collections.IEnumerator AttackLoop()
    {
        while (currentState == ZombieState.Angry)
        {
            if (cachedPlayerHealth == null)
                cachedPlayerHealth = playerTarget != null ? playerTarget.GetComponentInParent<PlayerHealth>() : null;

            if (cachedPlayerHealth == null)
                cachedPlayerHealth = FindObjectOfType<PlayerHealth>();

            // Если по какой-то причине преследование остановилось — обновим цель на игрока
            if (playerTarget != null)
            {
                SimpleZombieMovement movement = GetComponent<SimpleZombieMovement>();
                if (movement != null && movement.target != playerTarget)
                    movement.SetTarget(playerTarget);
            }

            TryAttack(cachedPlayerHealth);

            yield return new WaitForSeconds(attackCooldown);
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

        StopAttackAnimationBool();

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
        if (waveManagerNotified) return;

        waveManagerNotified = true;

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

        // Отдельно сообщаем WaveManager, что зомби реально исчез (Destroy)
        // (нужно, чтобы победный экран показывался после ухода последнего зомби).
        if (!isQuitting && Application.isPlaying)
        {
            NotifyWaveManagerDespawn();
        }
    }

    void NotifyWaveManagerDespawn()
    {
        if (waveManagerDespawnNotified) return;

        waveManagerDespawnNotified = true;

        ZombieWaveManager waveManager = ZombieWaveManager.Instance;
        if (waveManager != null)
        {
            waveManager.OnZombieDespawned(this);
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
