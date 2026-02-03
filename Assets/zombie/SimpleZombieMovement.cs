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
    private bool isMoving = true;
    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
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

        isInitialized = true;
        Debug.Log($"{gameObject.name}: Инициализирован (без CharacterController)");
    }

    void Update()
    {
        if (!isInitialized || target == null || !isMoving) return;

        // Рассчитываем направление ИГНОРИРУЯ ВЫСОТУ (Y)
        Vector3 targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);
        Vector3 direction = (targetPos - transform.position).normalized;

        // Проверяем дистанцию (игнорируем высоту Y)
        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(target.position.x, 0, target.position.z)
        );

        if (distance > stoppingDistance)
        {
            // Поворачиваемся к цели
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Двигаемся к цели ПРОСТЫМ ПЕРЕМЕЩЕНИЕМ
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                speed * Time.deltaTime
            );

            // Дебаг-информация
            if (Time.frameCount % 60 == 0) // Каждую секунду
            {
                Debug.Log($"{gameObject.name}: Идет к цели. Дистанция: {distance:F2}, Позиция Y: {transform.position.y:F2}");
            }
        }
        else
        {
            // Достигли цели
            isMoving = false;
            Debug.Log($"✅ {gameObject.name} достиг цели! Позиция: {transform.position}");

            // Вызываем метод что зомби пришел
            OnReachedTarget();
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
            Debug.Log($"{gameObject.name}: Новая цель установлена: {target.name}");
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Попытка установить null цель!");
        }
    }

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
}