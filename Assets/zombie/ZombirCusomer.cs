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
    public float waitTime = 30f;
    public float patienceDecreaseRate = 1f;
    public float walkSpeed = 2f;
    public float angrySpeed = 4f;

    [Header("Точки движения")]
    public Transform spawnPoint;
    public Transform servicePoint;
    public Transform playerTarget;

    [Header("Предметы")]
    public GameObject requestedItemPrefab;
    public Transform[] itemSpawnPoints;
    public float itemSpawnOffset = 0.5f;

    [Header("Точка выдачи")]
    public DeliveryPoint deliveryPoint;
    public float deliveryPickupDistance = 1f;

    [Header("Очередь")]
    public CustomerQueueManager queueManager;
    public ZombieSpawnManager spawnManager;
    private bool removedFromQueue = false;

    public enum ZombieState
    {
        Spawning,
        WalkingToQueue,
        InLine,
        Waiting,
        GettingAngry,
        Angry,
        GoingToDelivery,
        PickingUpItem,
        Leaving
    }

    private GameObject itemToPickup;

    [Header("UI")]
    public Slider patienceSlider;
    public GameObject patienceUI;
    public GameObject patienceUIPrefab;

    [Header("Движение")]
    public bool useSimpleMovement = true;
    public float interactionDistance = 1f;

    [Header("Attack")]
    public float attackDamage = 25f;
    public float attackCooldown = 1f;
    public float attackRange = 1.6f;
    public float attackStopDistance = 1.4f;
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
    private GameObject spawnedItem;
    private bool itemDelivered = false;
    private List<GameObject> spawnedItems = new List<GameObject>();
    private bool waveManagerNotified = false;
    private bool waveManagerDespawnNotified = false;
    private int currentSpawnIndex = 0;
    private static bool isQuitting = false;

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        currentPatience = waitTime;
        InitializeUI();

        if (patienceUI != null)
            patienceUI.SetActive(true);

        if (patienceSlider != null)
        {
            patienceSlider.maxValue = waitTime;
            patienceSlider.value = currentPatience;
        }

        StartCoroutine(SpawnSequence());
    }

    public void PickupItemFromPoint()
    {
        if (currentState != ZombieState.GoingToDelivery)
            return;

        currentState = ZombieState.PickingUpItem;

        if (testMode)
            Debug.Log($"{gameObject.name} забирает вещь с точки");

        if (spawnedItem != null)
        {
            Destroy(spawnedItem);
            spawnedItem = null;
        }

        if (deliveryPoint != null)
            deliveryPoint.ForceClearForZombie(this);

        ClearAllSpawnedItems();
        LeaveQueue();
        NotifyWaveManager();
        StartCoroutine(WaitAndLeave(0.5f));
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

        if (queueManager != null)
            queueManager.OnFrontZombieLeftPoint(this);

        currentState = ZombieState.GoingToDelivery;
        deliveryPoint = point;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (patienceUI != null)
            patienceUI.SetActive(false);

        if (useSimpleMovement)
        {
            if (simpleMovement == null)
                simpleMovement = GetComponent<SimpleZombieMovement>();

            if (simpleMovement == null)
                simpleMovement = gameObject.AddComponent<SimpleZombieMovement>();

            Transform targetTf = point.dropPosition != null ? point.dropPosition : point.transform;
            simpleMovement.SetTarget(targetTf);
            simpleMovement.stoppingDistance = Mathf.Max(0.1f, point.pickupRadius - 0.05f);
            simpleMovement.speed = walkSpeed;
        }

        if (testMode)
            Debug.Log($"{gameObject.name} идет к точке выдачи");
    }

    void InitializeUI()
    {
        if (patienceUI != null)
            return;

        if (patienceUIPrefab != null)
        {
            patienceUI = Instantiate(patienceUIPrefab, transform);
            patienceUI.name = $"{patienceUIPrefab.name}_{gameObject.name}";
            patienceSlider = patienceUI.GetComponentInChildren<Slider>();
            if (patienceSlider != null)
            {
                patienceSlider.maxValue = waitTime;
                patienceSlider.value = currentPatience;
            }
        }
    }

    IEnumerator SpawnSequence()
    {
        currentState = ZombieState.Spawning;
        yield return new WaitForSeconds(1f);

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
        if (currentState != ZombieState.Spawning && currentState != ZombieState.WalkingToQueue && currentState != ZombieState.InLine)
            return;

        if (servicePoint == null)
        {
            if (testMode)
                Debug.LogWarning($"{gameObject.name}: servicePoint не назначен — не могу идти к очереди");
            return;
        }

        currentState = ZombieState.WalkingToQueue;

        if (useSimpleMovement)
        {
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

        if (queueManager != null && !queueManager.IsFrontZombie(this))
        {
            currentState = ZombieState.InLine;
            return;
        }

        currentState = ZombieState.Waiting;
        itemSpawnedSuccessfully = false;

        if (itemSpawnPoints != null && itemSpawnPoints.Length > 0 && itemSpawnPoints[0] != null)
        {
            Vector3 direction = (itemSpawnPoints[0].position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        SpawnRequestedItem();

        if (itemSpawnedSuccessfully)
        {
            if (patienceUI != null)
                patienceUI.SetActive(true);

            currentPatience = waitTime;

            if (patienceSlider != null)
            {
                patienceSlider.maxValue = waitTime;
                patienceSlider.value = currentPatience;
            }
        }
        else
        {
            if (spawnedItem == null)
                CantSpawnItem();
            else
                itemSpawnedSuccessfully = true;
        }
    }

    void Update()
    {
        if (currentState == ZombieState.WalkingToQueue && servicePoint != null)
        {
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = servicePoint.position; b.y = 0f;
            float distance = Vector3.Distance(a, b);
            if (distance <= interactionDistance)
                ArrivedAtServicePoint();
        }

        if (currentState == ZombieState.Waiting || currentState == ZombieState.GettingAngry)
        {
            UpdatePatience();
        }

        if (currentState == ZombieState.Angry)
        {
            if (cachedPlayerHealth == null)
                cachedPlayerHealth = playerTarget != null ? playerTarget.GetComponentInParent<PlayerHealth>() : null;

            if (cachedPlayerHealth == null)
                cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>();

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

        Transform spawnPoint = GetFreeSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogError($"{gameObject.name}: Не могу найти точку спавна!");
            itemSpawnedSuccessfully = false;
            return;
        }

        spawnedItem = Instantiate(requestedItemPrefab, spawnPoint.position, spawnPoint.rotation);
        spawnedItem.name = $"RequestedItem_{gameObject.name}";
        itemSpawnedSuccessfully = true;
        spawnedItems.Add(spawnedItem);

        Renderer renderer = spawnedItem.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = true;
        else
        {
            Renderer[] childRenderers = spawnedItem.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in childRenderers)
                r.enabled = true;
        }

        ZombieRequestItem interactable = spawnedItem.GetComponent<ZombieRequestItem>();
        if (interactable == null)
            interactable = spawnedItem.AddComponent<ZombieRequestItem>();
        interactable.SetZombieCustomer(this);
    }

    Transform GetFreeSpawnPoint()
    {
        if (itemSpawnPoints == null || itemSpawnPoints.Length == 0)
            return CreateTemporarySpawnPoint();

        for (int i = 0; i < itemSpawnPoints.Length; i++)
        {
            int index = (currentSpawnIndex + i) % itemSpawnPoints.Length;
            if (itemSpawnPoints[index] == null) continue;
            if (!IsSpawnPointOccupied(itemSpawnPoints[index].position))
            {
                currentSpawnIndex = (index + 1) % itemSpawnPoints.Length;
                return itemSpawnPoints[index];
            }
        }

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
        GameObject table = GameObject.FindGameObjectWithTag("Table");
        if (table != null) return table.transform.position;
        if (servicePoint != null) return servicePoint.position;
        return transform.position + transform.forward * 2f;
    }

    bool IsSpawnPointOccupied(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, 0.3f);
        foreach (Collider col in colliders)
        {
            if (col.gameObject != gameObject &&
                (col.CompareTag("Item") || col.GetComponent<Item>() != null))
                return true;
        }
        return false;
    }

    void CantSpawnItem()
    {
        GetAngry();
    }

    void UpdatePatience()
    {
        if (itemDelivered) return;

        currentPatience -= Time.deltaTime * patienceDecreaseRate;

        if (patienceSlider != null)
        {
            patienceSlider.value = currentPatience;
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

        if (currentPatience <= 0f && currentState != ZombieState.Angry)
            GetAngry();
        else if (currentPatience <= waitTime * 0.5f && currentState == ZombieState.Waiting)
            StartGettingAngry();
    }

    void LateUpdate()
    {
        if (patienceUI != null && patienceUI.activeSelf)
        {
            Vector3 worldPos = transform.position + Vector3.up * 2.2f;

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            Transform head = null;
            if (animator != null && animator.isHuman)
                head = animator.GetBoneTransform(HumanBodyBones.Head);

            if (head != null)
                worldPos = head.position + Vector3.up * 8f;
            else
            {
                Renderer r = GetComponentInChildren<Renderer>();
                if (r != null)
                    worldPos = r.bounds.max + Vector3.up * 0.2f;
            }

            patienceUI.transform.position = worldPos;

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = patienceUI.transform.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    patienceUI.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    void StartGettingAngry()
    {
        currentState = ZombieState.GettingAngry;
    }

    void GetAngry()
    {
        if (currentState == ZombieState.Angry) return;

        currentState = ZombieState.Angry;
        LeaveQueue();

        if (patienceUI != null)
            patienceUI.SetActive(false);

        if (cachedPlayerHealth == null)
            cachedPlayerHealth = playerTarget != null ? playerTarget.GetComponentInParent<PlayerHealth>() : null;
        if (cachedPlayerHealth == null)
            cachedPlayerHealth = FindObjectOfType<PlayerHealth>();

        if (cachedPlayerHealth != null)
            playerTarget = cachedPlayerHealth.transform;

        if (playerTarget != null)
        {
            SimpleZombieMovement movement = GetComponent<SimpleZombieMovement>();
            if (movement != null)
            {
                movement.SetTarget(playerTarget);
                movement.speed = walkSpeed * 1.5f;
                movement.stoppingDistance = Mathf.Max(attackRange - 0.1f, 1.2f);
            }
        }
    }

    void TryAttack(PlayerHealth ph)
    {
        if (ph == null) return;

        if (playerTarget != null)
        {
            SimpleZombieMovement chase = GetComponent<SimpleZombieMovement>();
            if (chase != null)
                chase.SetTarget(playerTarget);
        }

        if (playerTarget != null)
        {
            Vector3 lookDir = playerTarget.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 12f * Time.deltaTime);
        }

        Vector2 zXZ = new Vector2(transform.position.x, transform.position.z);
        float dist = Vector2.Distance(zXZ, new Vector2(ph.transform.position.x, ph.transform.position.z));

        CapsuleCollider cc = ph.GetComponentInChildren<CapsuleCollider>();
        if (cc != null)
        {
            Vector3 closest = cc.ClosestPoint(transform.position);
            dist = Vector2.Distance(zXZ, new Vector2(closest.x, closest.z));

            if (dist > attackRange + 0.15f) return;

            if (Time.time - lastAttackTime < attackCooldown) return;

            lastAttackTime = Time.time;
            PlayAttackAnimation();
            ph.TakeDamage(attackDamage);
        }

        void PlayAttackAnimation()
        {
            if (animator == null) return;
            if (!useIsAttackingBool && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);
            if (useIsAttackingBool && !string.IsNullOrEmpty(isAttackingBool))
                animator.SetBool(isAttackingBool, true);
        }

        SimpleZombieMovement movement = GetComponent<SimpleZombieMovement>();
        if (movement != null)
        {
            if (dist <= attackRange)
            {
                movement.isMoving = false;
                movement.UpdateAnimMovement();
            }
            else
            {
                movement.isMoving = true;
                movement.UpdateAnimMovement();
            }
        }
    }

    public void DeliverItem(GameObject deliveredItem)
    {
        if (testMode)
            Debug.Log($"DeliverItem вызван, но используется новая система");
    }

    void ClearAllSpawnedItems()
    {
        foreach (GameObject item in spawnedItems)
        {
            if (item != null)
                Destroy(item);
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
            Destroy(gameObject);
        }
    }

    void NotifyWaveManager()
    {
        if (waveManagerNotified) return;
        waveManagerNotified = true;

        ZombieWaveManager waveManager = ZombieWaveManager.Instance;
        if (waveManager != null)
        {
            waveManager.OnZombieFinished(this);
        }
        else if (testMode)
        {
            Debug.LogWarning($"Зомби {gameObject.name}: не найден ZombieWaveManager! " +
                           $"Это нормально при завершении игры.");
        }
    }

    void OnDestroy()
    {
        ClearAllSpawnedItems();
        LeaveQueue();

        if (spawnManager != null)
            spawnManager.NotifyZombieRemoved(this);

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