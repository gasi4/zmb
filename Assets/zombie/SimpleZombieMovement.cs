using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimpleZombieMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public Transform target;
    public float speed = 2f;
    public float stoppingDistance = 1f;
    public float rotationSpeed = 5f;

    [Header("Компоненты")]
    [HideInInspector] public bool isMoving = true;
    private bool isInitialized = false;
    private NavMeshAgent agent;

    [Header("Animation")]
    public Animator animator;
    public string speedParam = "Speed";
    public bool setIsMovingBool = false;
    public string isMovingParam = "IsMoving";

    [Header("NavMesh")]
    public float pathUpdateInterval = 0.3f;
    private float pathUpdateTimer = 0f;

    // Сглаживание анимации
    private float smoothSpeed = 0f;
    private const float ANIM_SMOOTH_SPEED = 5f;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // ========== УБИРАЕМ ВСЁ ЧТО МЕШАЕТ ==========

        // Rigidbody конфликтует с NavMeshAgent — УДАЛЯЕМ
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
            Debug.Log($"{gameObject.name}: Удалён Rigidbody (конфликт с NavMeshAgent)");
        }

        // Удаляем Rigidbody с дочерних тоже
        foreach (Rigidbody childRb in GetComponentsInChildren<Rigidbody>())
        {
            if (childRb != null)
                Destroy(childRb);
        }

        // CharacterController тоже не нужен
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            Destroy(cc);
            Debug.Log($"{gameObject.name}: Удалён CharacterController");
        }

        // ========== NavMeshAgent ==========
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        agent.angularSpeed = 0f;         // МЫ сами вращаем — не NavMeshAgent
        agent.acceleration = 8f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 70); // разный приоритет чтобы не толкались
        agent.autoTraverseOffMeshLink = true;
        agent.updateRotation = false;    // МЫ вращаем
        agent.updateUpAxis = true;
        agent.baseOffset = 0f;
        agent.radius = 0.4f;            // подгони под размер зомби
        agent.height = 2f;              // подгони под размер зомби

        // ========== Animator ==========
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        // ========== Target ==========
        if (target == null)
        {
            GameObject servicePoint = GameObject.Find("ServicePoint");
            if (servicePoint != null)
                target = servicePoint.transform;
            else
            {
                Debug.LogError($"{gameObject.name}: Target не найден!");
                enabled = false;
                return;
            }
        }

        // ========== Ставим на NavMesh ==========
        StartCoroutine(PlaceOnNavMesh());
    }

    System.Collections.IEnumerator PlaceOnNavMesh()
    {
        // Ждём 1 кадр чтобы Destroy(Rigidbody) успел сработать
        yield return null;

        // Выравниваем поворот ПЕРЕД размещением
        Vector3 euler = transform.eulerAngles;
        euler.x = 0f;
        euler.z = 0f;
        transform.eulerAngles = euler;

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Не могу найти NavMesh рядом!");
                enabled = false;
                yield break;
            }
        }

        isInitialized = true;

        // Сразу задаём цель
        if (target != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            SetNavMeshDestination(target.position);
        }
    }

    void Update()
    {
        if (!isInitialized) return;
        if (agent == null || !agent.isOnNavMesh) return;

        // КАЖДЫЙ КАДР фиксим поворот
        ForceUpright();

        // Анимация
        UpdateAnimation();

        if (target == null || !isMoving)
        {
            StopAgent();
            return;
        }

        // Обновляем путь
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0f)
        {
            pathUpdateTimer = pathUpdateInterval;
            UpdateDestination();
        }

        // Поворот
        SmoothRotation();

        // Проверяем дошёл ли
        CheckArrival();
    }

    void LateUpdate()
    {
        // ПОСЛЕ всех расчётов — ещё раз фиксим
        if (isInitialized)
            ForceUpright();
    }

    // ==================== ПОВОРОТ ====================

    void ForceUpright()
    {
        // Принудительно убираем ЛЮБОЙ наклон
        Vector3 euler = transform.eulerAngles;
        euler.x = 0f;
        euler.z = 0f;
        transform.eulerAngles = euler;
    }

    void SmoothRotation()
    {
        if (agent == null) return;

        Vector3 moveDir = agent.desiredVelocity;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude > 0.05f)
        {
            // Поворачиваемся в сторону движения
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);

            // Убираем наклон из целевого поворота
            Vector3 targetEuler = targetRot.eulerAngles;
            targetEuler.x = 0f;
            targetEuler.z = 0f;
            targetRot = Quaternion.Euler(targetEuler);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
        }
        else if (target != null)
        {
            // Стоим — смотрим на цель
            Vector3 dirToTarget = target.position - transform.position;
            dirToTarget.y = 0f;

            if (dirToTarget.sqrMagnitude > 0.1f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dirToTarget.normalized, Vector3.up);

                Vector3 lookEuler = lookRot.eulerAngles;
                lookEuler.x = 0f;
                lookEuler.z = 0f;
                lookRot = Quaternion.Euler(lookEuler);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    lookRot,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    // ==================== НАВИГАЦИЯ ====================

    void UpdateDestination()
    {
        if (target == null || agent == null || !agent.isOnNavMesh) return;

        Vector3 destination = target.position;

        bool isPlayerTarget = target.CompareTag("Player") ||
                              target.GetComponentInParent<PlayerHealth>() != null;

        if (!isPlayerTarget)
        {
            Collider targetCol = target.GetComponentInChildren<Collider>();
            if (targetCol != null && !targetCol.isTrigger)
                destination = targetCol.ClosestPoint(transform.position);
        }

        SetNavMeshDestination(destination);
    }

    void SetNavMeshDestination(Vector3 worldPos)
    {
        // Находим ближайшую точку на NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(worldPos, out hit, 5f, NavMesh.AllAreas))
            worldPos = hit.position;

        agent.stoppingDistance = stoppingDistance;
        agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(worldPos);
    }

    void CheckArrival()
    {
        if (target == null) return;
        if (agent.pathPending) return;

        bool isPlayerTarget = target.CompareTag("Player") ||
                              target.GetComponentInParent<PlayerHealth>() != null;

        float distance = agent.remainingDistance;

        // Фоллбэк если remainingDistance врёт
        if (distance < 0.01f && agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatTarget = new Vector3(target.position.x, 0, target.position.z);
            distance = Vector3.Distance(flatPos, flatTarget);
        }

        // Путь невозможен
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid ||
            agent.pathStatus == NavMeshPathStatus.PathPartial)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(target.position, out hit, 5f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // Игрок
        if (isPlayerTarget)
        {
            if (distance <= stoppingDistance + 0.1f)
                StopAgent();
            else if (agent.isStopped)
            {
                agent.isStopped = false;
                UpdateDestination();
            }
            return;
        }

        // Не игрок — дошёл?
        if (distance <= stoppingDistance + 0.1f)
        {
            ZombieCustomer zombie = GetComponent<ZombieCustomer>();
            bool isAngry = zombie != null &&
                           zombie.currentState == ZombieCustomer.ZombieState.Angry;

            if (!isAngry)
            {
                isMoving = false;
                StopAgent();
                OnReachedTarget();
            }
        }
    }

    void StopAgent()
    {
        if (agent != null && agent.isOnNavMesh && !agent.isStopped)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    // ==================== АНИМАЦИЯ ====================

    void UpdateAnimation()
    {
        if (animator == null) return;
        if (!animator.isActiveAndEnabled) return;
        if (animator.runtimeAnimatorController == null) return;

        // Целевая скорость
        float targetAnimSpeed = 0f;

        if (isMoving && agent != null && agent.isOnNavMesh && !agent.isStopped)
        {
            float agentSpeed = agent.velocity.magnitude;

            // Если агент движется — Walk, иначе Idle
            if (agentSpeed > 0.1f)
                targetAnimSpeed = 1f;
            else
                targetAnimSpeed = 0f;
        }

        // СГЛАЖИВАНИЕ — главное от дёрганья idle/walk
        smoothSpeed = Mathf.MoveTowards(smoothSpeed, targetAnimSpeed, ANIM_SMOOTH_SPEED * Time.deltaTime);

        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, smoothSpeed);

        if (setIsMovingBool && !string.IsNullOrEmpty(isMovingParam))
            animator.SetBool(isMovingParam, smoothSpeed > 0.1f);
    }

    // ==================== EVENTS ====================

    void OnReachedTarget()
    {
        ZombieCustomer zombie = GetComponent<ZombieCustomer>();
        if (zombie == null) return;

        switch (zombie.currentState)
        {
            case ZombieCustomer.ZombieState.WalkingToQueue:
                zombie.ArrivedAtServicePoint();
                break;
            case ZombieCustomer.ZombieState.GoingToDelivery:
                zombie.PickupItemFromPoint();
                break;
            case ZombieCustomer.ZombieState.Angry:
                break;
            case ZombieCustomer.ZombieState.Leaving:
                Destroy(gameObject);
                break;
        }
    }

    // ==================== PUBLIC ====================

    // ==================== PUBLIC ====================

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
        {
            Debug.LogError($"{gameObject.name}: null цель!");
            return;
        }

        target = newTarget;
        isMoving = true;
        smoothSpeed = 0f;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            SetNavMeshDestination(newTarget.position);
        }
    }

    // ЭТО ДОБАВЬ — публичная обёртка для ZombieCustomer
    public void UpdateAnimMovement()
    {
        UpdateAnimation();
    }

    // ==================== GIZMOS ====================

    void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, 0.3f);
        }

        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.cyan;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
                Gizmos.DrawLine(corners[i], corners[i + 1]);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }
}