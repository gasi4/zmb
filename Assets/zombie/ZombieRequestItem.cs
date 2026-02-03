using UnityEngine;

public class ZombieRequestItem : MonoBehaviour
{
    private ZombieCustomer zombieCustomer;
    private bool canInteract = true;

    public void SetZombieCustomer(ZombieCustomer customer)
    {
        zombieCustomer = customer;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canInteract) return;

        // Проверяем что игрок принес вещь
        if (other.CompareTag("Player") || other.CompareTag("CleanClothes"))
        {
            // Проверяем что это действительно постиранная вещь
            Item item = other.GetComponent<Item>();
            if (item != null && item.isClean) // Добавь поле isClean в скрипт Item
            {
                DeliverToZombie(other.gameObject);
            }
        }
    }

    void DeliverToZombie(GameObject item)
    {
        if (zombieCustomer != null)
        {
            zombieCustomer.DeliverItem(item);
            canInteract = false;
        }
    }
}