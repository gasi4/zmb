using UnityEngine;

public class DeliveryPoint : MonoBehaviour
{
    [Header("Настройки точки выдачи")]
    public float pickupRadius = 1.5f;
    public Transform dropPosition;

    [Header("Зона взаимодействия игрока")]
    public Collider interactionZone; // триггер/коллайдер зоны перед полкой

    [Header("Визуальные эффекты")]
    public GameObject highlightEffect;

    [Header("Debug")]
    public bool showGizmos = true;

    private GameObject currentItem;
    private ZombieCustomer waitingZombie;

    void Start()
    {
        if (highlightEffect != null)
            highlightEffect.SetActive(false);

        // Если зона не назначена — пробуем взять коллайдер с этого объекта
        if (interactionZone == null)
            interactionZone = GetComponent<Collider>();

        // Важно: для "области" проще всего использовать Trigger
        if (interactionZone != null)
            interactionZone.isTrigger = true;
    }

    void Update()
    {
        // Если есть вещь и зомби, проверяем расстояние
        if (currentItem != null && waitingZombie != null)
        {
            // Отдаем только когда зомби реально пришел за вещью
            if (waitingZombie.currentState == ZombieCustomer.ZombieState.GoingToDelivery)
            {
                float distance = Vector3.Distance(waitingZombie.transform.position, transform.position);

                if (distance <= pickupRadius)
                {
                    DeliverItemToZombie();
                }
            }
        }
    }

    // Игрок кладет вещь на точку
    public bool PlaceItem(GameObject item, ZombieCustomer zombie)
    {
        if (currentItem != null) return false;

        currentItem = item;
        waitingZombie = zombie;

        // Отцепляем от руки игрока/якоря и закрепляем на точке
        item.transform.SetParent(dropPosition != null ? dropPosition : transform, true);

        // Позиционируем вещь
        if (dropPosition != null)
        {
            item.transform.position = dropPosition.position;
            item.transform.rotation = dropPosition.rotation;
        }
        else
        {
            item.transform.position = transform.position + Vector3.up * 0.5f;
        }

        // Отключаем физику и коллайдер
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider col = item.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Включаем подсветку
        if (highlightEffect != null)
            highlightEffect.SetActive(true);

        Debug.Log($"Вещь {item.name} помещена на точку для зомби {zombie.name}");

        // Теперь зомби должен сам подойти к точке и забрать вещь
        zombie.GoToDeliveryPoint(this);

        return true;
    }

    // Зомби забирает вещь
    void DeliverItemToZombie()
    {
        if (currentItem == null || waitingZombie == null) return;

        // Отдаем вещь зомби
        waitingZombie.PickupItemFromPoint(); // ← Просто вызываем метод зомби

        // Очищаем точку
        ClearPoint();

        Debug.Log("Вещь отдана зомби!");
    }

    void ClearPoint()
    {
        // Очищаем ссылки
        currentItem = null;
        waitingZombie = null;

        // Отключаем подсветку
        if (highlightEffect != null)
            highlightEffect.SetActive(false);
    }

    public bool IsAvailable()
    {
        return currentItem == null;
    }

    // Игрок находится в области взаимодействия перед полкой?
    public bool IsPlayerInInteractionZone(Transform player)
    {
        if (player == null) return false;
        if (interactionZone == null) return true; // fallback: если зоны нет, работаем как раньше

        // Bounds.Contains часто промахивается из‑за pivot игрока (например, у ног).
        // ClosestPoint дает более надёжную проверку "внутри коллайдера".
        Vector3 p = player.position;
        Vector3 closest = interactionZone.ClosestPoint(p);
        return (closest - p).sqrMagnitude < 0.0001f;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);

        if (dropPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(dropPosition.position, 0.2f);
        }
    }
}