using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AutoStoreToInventory : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    public InventoryManager inventoryManager;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null) return;

        if (inventoryManager == null)
            inventoryManager = FindObjectOfType<InventoryManager>();

        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        Item itemComp = GetComponent<Item>();
        if (itemComp != null && itemComp.item != null && inventoryManager != null)
        {
            inventoryManager.AddItem(itemComp.item, itemComp.amount);
            Debug.Log($"Предмет {itemComp.item.ItemName} добавлен в инвентарь через Grab");
            Destroy(gameObject); // Уничтожаем объект, чтобы он не остался в руке
        }

        // Отменяем захват, чтобы объект не висел на руке (если он ещё не уничтожен)
        // Но так как мы уничтожаем объект, это не нужно
    }
}
