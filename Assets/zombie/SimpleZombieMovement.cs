using System.Collections;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class SimpleZombieMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    public Transform target; // Цель (стол или игрок)
    public float speed = 2f;
    public float stoppingDistance = 1f;
    public float rotationSpeed = 5f;

    [Header("Компоненты")]
    private bool isMoving = true;
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

        // Анимация: скорость/движение (даже если нет цели, пусть будет Idle)
        UpdateAnimMovement();

        if (target == null || !isMoving) return;

        // Ищем коллайдер цели и идём к ближайшей точке на нём, чтобы не заходить "внутрь".
        // ВАЖНО: trigger-зоны не должны влиять на остановку.
        Collider targetCol = target.GetComponentInChildren<Collider>();
        bool canUseClosestPoint = targetCol != null && !targetCol.isTrigger;

        // Но для игрока (XR rig/CharacterController и т.п.) ClosestPoint часто даёт точки "сбоку",
        // из-за чего зомби прижимается/входит в игрока. Поэтому в этом случае используем transform.position.
        bool isPlayerTarget = target.CompareTag("Player") || target.GetComponentInParent<PlayerHealth>() != null;
        Vector3 aimPoint = (canUseClosestPoint && !isPlayerTarget) ? targetCol.ClosestPoint(transform.position) : target.position;

        // Рассчитываем направление ИГНОРИРУЯ ВЫСОТУ (Y)
        Vector3 targetPos = new Vector3(aimPoint.x, transform.position.y, aimPoint.z);
        Vector3 direction = (targetPos - transform.position).normalized;

        // Проверяем дистанцию (игнорируем высоту Y)
        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(aimPoint.x, 0, aimPoint.z)
        );

        // Небольшой допуск, чтобы зомби гарантированно "считал" цель достигнутой
        // даже если анти-пенетрация/ClosestPoint держат дистанцию чуть > stoppingDistance.
        const float arriveEpsilon = 0.05f;

        if (distance > stoppingDistance + arriveEpsilon)
        {
            // Поворачиваемся к цели
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // НЕ даём пересечь stoppingDistance даже при большом шаге за кадр
            float maxStep = speed * Time.deltaTime;
            float allowedStep = Mathf.Max(0f, distance - stoppingDistance);
            float step = Mathf.Min(maxStep, allowedStep);

            if (step > 0f)
                transform.position += direction * step;

            // Дебаг-информация (раз в секунду)
            if (Time.frameCount % 60 == 0)
            {
                string tName = target != null ? target.name : "null";
                Debug.Log($"{gameObject.name}: Идет к цели '{tName}'. Dist={distance:F2}. Self={transform.position}. TargetPos={aimPoint}");
            }
        }

        else
        {
            // Достигли цели
            ZombieCustomer zombie = GetComponent<ZombieCustomer>();
            bool isAngry = zombie != null && zombie.currentState == ZombieCustomer.ZombieState.Angry;

            // В агре не "останавливаемся навсегда" — зомби должен продолжать преследование/атаки.
            if (!isAngry)
            {
                isMoving = false;
                UpdateAnimMovement();
                Debug.Log($"✅ {gameObject.name} достиг цели! Позиция: {transform.position}");
                OnReachedTarget();
                // После достижения цели прекращаем дальнейшие вычисления на этом кадре,
                // иначе ComputePenetration может продолжать "выталкивать" и зомби начинает кружить.
                return;
            }
        }

        // Жёсткий анти-проход через коллайдер цели: если мы пересеклись — выталкиваемся наружу.
        // ВАЖНО: trigger-коллайдеры (например interactionZone у DeliveryPoint) НЕ должны выталкивать,
        // иначе зомби физически не сможет войти в зону и "застрянет" с дистанцией ~10.
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

    void UpdateAnimMovement()
    {
        if (animator == null) return;
        if (!animator.isActiveAndEnabled) return;
        if (animator.runtimeAnimatorController == null) return;

        // Если Animator на другом объекте (не на корне зомби), то этот MonoBehaviour
        // может быть на другом transform и не знать, движется ли модель.
        // Поэтому выставляем Speed по фактической скорости перемещения КОРНЯ (transform зомби),
        // иначе при движении root-объекта скорость AnimatorTransform может оставаться ~0
        // и контроллер будет всегда уходить в Idle.
        Transform animTf = transform;
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
        ;

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
    // Вместо него можно добавить простую проверку столкновений если нуж
    void OnDrawGizmosSelected()
    {
        {
            // Визуализация в редакторе
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
                Gizmos.DrawWireSphere(target.position, 0.5f);

            }

            void OnEnable()
            {
                // На старте/после включения задаём стартовую точку для расчёта скорости анимации,
                // чтобы не получать 0 и не уходить в Idle на первые кадры.
                if (animator == null)
                    animator = GetComponentInChildren<Animator>(true);

                _lastAnimPos = transform.position;
                _lastAnimPosInitialized = true;
            }
        }
    }
}