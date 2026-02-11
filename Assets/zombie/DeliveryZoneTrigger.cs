using UnityEngine;

// Вешается на interactionZone (Collider) и прокидывает триггеры в DeliveryPoint.
public class DeliveryZoneTrigger : MonoBehaviour
{
    [HideInInspector] public DeliveryPoint point;

    void OnTriggerEnter(Collider other)
    {
        Handle(other);
    }

    void OnTriggerStay(Collider other)
    {
        Handle(other);
    }

    void Handle(Collider other)
    {
        if (point == null || other == null) return;

        // Зомби может иметь коллайдер на child'е — ищем компонент выше по иерархии
        ZombieCustomer z = other.GetComponentInParent<ZombieCustomer>();
        if (z == null) return;

        // interactionZone используется для игрока; выдача зомби делается по pickupRadius внутри DeliveryPoint.Update()
        // поэтому тут ничего не делаем.
    }
}