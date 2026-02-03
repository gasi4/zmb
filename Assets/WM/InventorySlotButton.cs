using UnityEngine;
using UnityEngine.UI;

public class InventorySlotButton : MonoBehaviour
{
    private slot mySlot;
    private InventoryManager inventoryManager;
    
    void Start()
    {
        mySlot = GetComponent<slot>();
        inventoryManager = FindObjectOfType<InventoryManager>();
        
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnSlotClicked);
        }
    }
    
    void OnSlotClicked()
    {
        if (mySlot == null || mySlot.isEmpty) return;
        
        if (inventoryManager != null && inventoryManager.IsInWashingMachineSelectionMode())
        {
            // Режим выбора для машинки
            inventoryManager.SelectCurrentItemForWashing(mySlot);
        }
        else
        {
            // Обычный режим - взять в руку
            inventoryManager?.TakeItemFromSlot(mySlot);
        }
    }
}