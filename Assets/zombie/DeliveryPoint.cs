using UnityEngine;

public class DeliveryPoint : MonoBehaviour
{
    [Header("Настройки точки выдачи")]
    public float pickupRadius = 1.5f;
    public Transform dropPosition; // Сюда будет притягиваться предмет

    [Header("Зона взаимодействия игрока")]
    public Collider interactionZone;

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

        if (interactionZone == null)
            interactionZone = GetComponent<Collider>();

        if (interactionZone != null)
            interactionZone.isTrigger = true;
    }

    void Update()
    {
        if (currentItem != null && waitingZombie != null)
        {
            if (waitingZombie.currentState == ZombieCustomer.ZombieState.GoingToDelivery)
            {
                Transform p = dropPosition != null ? dropPosition : transform;
                float distance = Vector3.Distance(waitingZombie.transform.position, p.position);
                if (distance <= pickupRadius)
                    DeliverItemToZombie();
            }
        }
    }

    // Новый метод: притягивает предмет к точке (без зомби)
    public void AttractItem(GameObject item)
    {
        if (item == null) return;

        Item itemComp = item.GetComponent<Item>();
        if (itemComp != null && !itemComp.isClean)
        {
            Debug.LogWarning("Пытаемся притянуть грязный предмет - не положим на полку");
            return;
        }

        Transform target = dropPosition != null ? dropPosition : transform;
        item.transform.SetParent(target);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider col = item.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        if (currentItem != null && currentItem != item)
            Destroy(currentItem);
        currentItem = item;

        if (highlightEffect != null)
            highlightEffect.SetActive(true);

        Debug.Log($"Предмет {item.name} притянут к DeliveryPoint");

        // --- НОВЫЙ КОД: поиск зомби и отправка его к точке ---
        ZombieCustomer zombie = FindNearestWaitingZombie();
        if (zombie != null)
        {
            waitingZombie = zombie;
            zombie.GoToDeliveryPoint(this);
            Debug.Log($"Зомби {zombie.name} отправлен к точке за предметом");
        }
        else
        {
            Debug.Log("Нет зомби, ожидающих вещь");
        }
    }

    // Вспомогательный метод для поиска ближайшего ожидающего зомби
    private ZombieCustomer FindNearestWaitingZombie()
    {
        // Приоритет: первый в очереди
        CustomerQueueManager queue = FindObjectOfType<CustomerQueueManager>();
        if (queue != null)
        {
            ZombieCustomer first = queue.GetFirstWaitingZombie();
            if (first != null)
                return first;
        }

        // Fallback: ближайший зомби в состоянии Waiting или GettingAngry
        ZombieCustomer[] all = FindObjectsOfType<ZombieCustomer>();
        ZombieCustomer nearest = null;
        float minDist = float.MaxValue;
        foreach (var z in all)
        {
            if (z == null) continue;
            if (z.currentState == ZombieCustomer.ZombieState.Waiting ||
                z.currentState == ZombieCustomer.ZombieState.GettingAngry)
            {
                float d = Vector3.Distance(transform.position, z.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = z;
                }
            }
        }
        return nearest;
    }

    public bool PlaceItem(GameObject item, ZombieCustomer zombie)
    {
        if (currentItem != null) return false;
        if (item == null || zombie == null) return false;

        Item itemComp = item.GetComponent<Item>();
        if (itemComp == null || !itemComp.isClean)
        {
            Debug.LogWarning("DeliveryPoint: нельзя положить грязную вещь");
            return false;
        }

        currentItem = item;
        waitingZombie = zombie;

        Transform target = dropPosition != null ? dropPosition : transform;
        item.transform.SetParent(target, true);
        item.transform.position = target.position;
        item.transform.rotation = target.rotation;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider col = item.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (highlightEffect != null)
            highlightEffect.SetActive(true);

        Debug.Log($"Вещь {item.name} помещена на точку для зомби {zombie.name}");
        zombie.GoToDeliveryPoint(this);
        return true;
    }

    void DeliverItemToZombie()
    {
        if (currentItem == null || waitingZombie == null) return;

        Destroy(currentItem);
        waitingZombie.PickupItemFromPoint();
        ClearPoint();
        Debug.Log("Вещь отдана зомби!");
    }

    public void ForceClearForZombie(ZombieCustomer zombie)
    {
        if (zombie == null || waitingZombie == null || zombie != waitingZombie) return;
        if (currentItem != null)
            Destroy(currentItem);
        ClearPoint();
    }

    void ClearPoint()
    {
        currentItem = null;
        waitingZombie = null;
        if (highlightEffect != null)
            highlightEffect.SetActive(false);
    }

    public bool IsAvailable() => currentItem == null;

    public bool IsPlayerInInteractionZone(Transform player)
    {
        if (player == null) return false;
        if (interactionZone == null) return true;
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