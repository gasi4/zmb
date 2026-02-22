using UnityEngine;

public class DeliveryZoneTrigger : MonoBehaviour
{
    [HideInInspector] public DeliveryPoint point;
    public bool playerInside { get; private set; }

    void Awake()
    {
        point = GetComponentInParent<DeliveryPoint>();
        if (point == null)
            Debug.LogError("DeliveryZoneTrigger: не найден DeliveryPoint в родителях!", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Отладочный лог
        Debug.Log($"В зону вошёл объект: {other.name}");

        // Проверяем, что вошедший объект — чистый предмет (Item)
        Item itemComp = other.GetComponentInParent<Item>();
        if (itemComp != null && itemComp.isClean)
        {
            Debug.Log($"Чистый предмет {other.name} обнаружен в зоне");
            point?.AttractItem(other.gameObject);
            return; // не проверяем игрока дальше, предмет уже обработан
        }

        // Проверка на игрока
        if (other.GetComponentInParent<FinalPlayerController>() != null)
        {
            playerInside = true;
            Debug.Log("Игрок в зоне");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        if (other.GetComponentInParent<FinalPlayerController>() != null)
            playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.GetComponentInParent<FinalPlayerController>() != null)
            playerInside = false;
    }
}