using UnityEngine;
using System.Collections;

public class SimpleZombieMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public Transform target; // Цель (стол или игрок)
    public float speed = 2f;
    public float stoppingDistance = 1f;
    public float rotationSpeed = 5f;

    [Header("Компоненты")]
    [HideInInspector] public bool isMoving = true;
    private bool isInitialized = false;
    private Collider selfCol;

    [Header("Animation")]
    [Tooltip("Animator на зомби (если не задан — будет найден в детях).")]
    public Animator animator;
    [Tooltip("Имя float-параметра скорости в Animator.")]
    public string speedParam = "Speed";
    [Tooltip("Если true — будет выставлять bool IsMoving (если он есть в Animator).")]
    public bool setIsMovingBool = false;
    [Tooltip("Имя bool-параметра движения в Animator (опционально).")]
    public string isMovingParam = "IsMoving";

    [Header("Anti-penetration")]
    public float penetrationSkin = 0.02f;


    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null && animator.avatar == null)
        {
            Debug.LogWarning($"{gameObject.name}: Animator найден, но Avatar = None. " +
                             "Для humanoid-анимаций нужен Avatar в FBX (Rig->Humanoid) или назначь Avatar вручную.");
        }

        // Проверяем что target назначен
        if (target == null)
        {
            Debug.LogWarning($"{gameObject.name}: Target не назначен! Буду искать 'ServicePoint'");

            // Автоматически ищем ServicePoint
            GameObject servicePoint = GameObject.Find("ServicePoint");
            if (servicePoint != null)
            {
                target = servicePoint.transform;
                Debug.Log($"Найден ServicePoint: {target.name}");
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Не могу найти ServicePoint!");
                enabled = false; // Отключаем скрипт
                return;
            }
        }

        // УДАЛЯЕМ CharacterController если есть
        CharacterController oldController = GetComponent<CharacterController>();
        if (oldController != null)
        {
            Destroy(oldController);
            Debug.Log($"{gameObject.name}: Удален CharacterController");
        }

        // ДОБАВЛЯЕМ Rigidbody для простой физики
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // НАСТРАИВАЕМ Rigidbody (важно!)
        rb.isKinematic = true;       // Кинематический = не падает от гравитации
        rb.useGravity = false;       // Отключаем гравитацию
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ |
                         RigidbodyConstraints.FreezePositionY; // Замораживаем движение по Y

        // Коллайдер нужен, чтобы не заходить внутрь игрока/препятствий
        selfCol = GetComponent<Collider>();
        if (selfCol == null)
            selfCol = GetComponentInChildren<Collider>();

        isInitialized = true;
        Debug.Log($"{gameObject.name}: Инициализирован (без CharacterController)");
    }

    void Update()
    {
        if (!isInitialized) return;

        // Анимация: скорость/движение
        UpdateAnimMovement();

        if (target == null || !isMoving) return;

        // Ищем коллайдер цели
        Collider targetCol = target.GetComponentInChildren<Collider>();
        bool canUseClosestPoint = targetCol != null && !targetCol.isTrigger;

        // Является ли цель игроком
        bool isPlayerTarget =
            target.CompareTag("Player") ||
            target.GetComponentInParent<PlayerHealth>() != null;

        // Точка, к которой идём
        Vector3 aimPoint =
            (canUseClosestPoint && !isPlayerTarget)
            ? targetCol.ClosestPoint(transform.position)
            : target.position;

        // Направление (без Y)
        Vector3 targetPos = new Vector3(aimPoint.x, transform.position.y, aimPoint.z);
        Vector3 direction = (targetPos - transform.position).normalized;

        // Дистанция по XZ
        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(aimPoint.x, 0, aimPoint.z)
        );

        const float arriveEpsilon = 0.05f;

        // ===================== 🔴 ГЛАВНЫЙ ФИКС 🔴 =====================
        // Если цель — игрок и мы уже на нужной дистанции → СТОИМ, НЕ ДВИГАЕМСЯ
        if (isPlayerTarget && distance <= stoppingDistance)
        {
            isMoving = false;
            UpdateAnimMovement();

            // Только поворачиваемся к игроку
            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    lookRot,
                    rotationSpeed * Time.deltaTime
                );
            }

            return; // ⛔ НЕ даём логике движения идти дальше
        }
        // =============================================================

        if (distance > stoppingDistance + arriveEpsilon)
        {
            // Поворачиваемся к цели
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            // Ограничиваем шаг, чтобы не перескочить stoppingDistance
            float maxStep = speed * Time.deltaTime;
            float allowedStep = Mathf.Max(0f, distance - stoppingDistance);
            float step = Mathf.Min(maxStep, allowedStep);

            if (step > 0f)
                transform.position += direction * step;
        }
        else
        {
            // Достигли цели (НЕ игрок)
            ZombieCustomer zombie = GetComponent<ZombieCustomer>();
            bool isAngry = zombie != null &&
                           zombie.currentState == ZombieCustomer.ZombieState.Angry;

            if (!isAngry)
            {
                isMoving = false;
                UpdateAnimMovement();
                OnReachedTarget();
                return;
            }
        }

        // Анти-пенетрация (оставляем, но теперь она почти не срабатывает)
        if (selfCol != null && targetCol != null && !targetCol.isTrigger)
        {
            Vector3 dir;
            float distPen;

            if (Physics.ComputePenetration(
                    selfCol, transform.position, transform.rotation,
                    targetCol, targetCol.transform.position, targetCol.transform.rotation,
                    out dir, out distPen))
            {
                transform.position += dir * (distPen + penetrationSkin);
            }
        }
    }

    void OnReachedTarget()
    {
        ZombieCustomer zombie = GetComponent<ZombieCustomer>();
        if (zombie == null)
        {
            Debug.LogWarning($"{gameObject.name}: Не найден компонент ZombieCustomer!");
            return;
        }

        // Важно: зомби ходит к разным целям (очередь/выдача/выход).
        // Нельзя всегда вызывать ArrivedAtServicePoint().
        switch (zombie.currentState)
        {
            case ZombieCustomer.ZombieState.WalkingToQueue:
                zombie.ArrivedAtServicePoint();
                break;

            case ZombieCustomer.ZombieState.GoingToDelivery:
                zombie.PickupItemFromPoint();
                break;

            case ZombieCustomer.ZombieState.Angry:
                // В агре целевая точка = игрок, тут не делаем ничего (урон обрабатывается в ZombieCustomer)
                break;

            case ZombieCustomer.ZombieState.Leaving:
                Destroy(gameObject);
                break;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null)
        {
            target = newTarget;
            isMoving = true;
            UpdateAnimMovement();
            Debug.Log($"{gameObject.name}: Новая цель установлена: {target.name}");
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Попытка установить null цель!");
        }
    }

    public void UpdateAnimMovement()
    {
        if (animator == null) return;
        if (!animator.isActiveAndEnabled) return;
        if (animator.runtimeAnimatorController == null) return;

        // Если Animator на другом объекте (не на корне зомби), то этот MonoBehaviour
        // может быть на другом transform и не знать, движется ли модель.
        // Поэтому выставляем Speed по фактической скорости перемещения объекта, на котором висит animator.
        Transform animTf = animator.transform;
        float realSpeed = 0f;
        if (_lastAnimPosInitialized)
        {
            realSpeed = Vector3.Distance(animTf.position, _lastAnimPos) / Mathf.Max(0.0001f, Time.deltaTime);
        }
        else
        {
            // Первый кадр: ещё нет предыдущей позиции, поэтому realSpeed=0 и Animator уходит в Idle.
            // Если мы реально "должны двигаться" — форсим стартовое значение Speed.
            if (isMoving && target != null)
                realSpeed = Mathf.Max(0f, speed);
        }
        _lastAnimPos = animTf.position;
        _lastAnimPosInitialized = true;

        // Нормализуем скорость в диапазон 0..1 (Animator обычно использует пороги вроде 0.1).
        float normalized = (speed > 0.0001f) ? (realSpeed / speed) : realSpeed;
        float targetSpeed = Mathf.Clamp01(normalized);

        // Анти-дребезг: около точки назначения возможны микросдвиги (penetration/ClosestPoint),
        // из-за которых Speed прыгает вокруг порога и Animator быстро переключает Walk/Idle.
        if (!isMoving)
            targetSpeed = 0f;

        // Speed (float)
        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, targetSpeed);

        // IsMoving (bool) — опционально, чтобы не ломать существующий Animator
        if (setIsMovingBool && !string.IsNullOrEmpty(isMovingParam))
            animator.SetBool(isMovingParam, targetSpeed > 0.05f);
    }

    private Vector3 _lastAnimPos;
    private bool _lastAnimPosInitialized = false;

    // УДАЛЯЕМ OnControllerColliderHit - он был только для CharacterController
    // Вместо него можно добавить простую проверку столкновений если нужно

    void OnDrawGizmosSelected()
    {
        // Визуализация в редакторе
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, 0.5f);
        }
    }

    void OnEnable()
    {
        // На старте/после включения задаём стартовую точку для расчёта скорости анимации,
        // чтобы не получать 0 и не уходить в Idle на первые кадры.
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            _lastAnimPos = animator.transform.position;
            _lastAnimPosInitialized = true;
        }
    }
}