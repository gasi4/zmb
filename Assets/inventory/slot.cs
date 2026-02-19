using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class slot : MonoBehaviour, IPointerClickHandler
{
    public ItemScriptableObject item;
    public int amount;
    public bool isEmpty = true;
    public GameObject iconGameObject;
    public TMP_Text itemAmountText;

    private InventoryManager inventoryManager;
    private FinalPlayerController playerController;

    void Awake()
    {
        // Ищем иконку по имени "Icon" или первый дочерний
        iconGameObject = transform.Find("Icon")?.gameObject;
        if (iconGameObject == null && transform.childCount > 0)
            iconGameObject = transform.GetChild(0).gameObject;

        itemAmountText = GetComponentInChildren<TMP_Text>();

        inventoryManager = FindObjectOfType<InventoryManager>();
        playerController = FindObjectOfType<FinalPlayerController>();

        UpdateVisual();
    }   
    void UpdateVisual()
    {
        if (iconGameObject != null)
        {
            Image img = iconGameObject.GetComponent<Image>();
            if (img != null)
            {
                if (isEmpty || item == null)
                {
                    img.color = new Color(1, 1, 1, 0);
                    img.sprite = null;
                }
                else if (item.Icon != null)
                {
                    img.color = new Color(1, 1, 1, 1);
                    img.sprite = item.Icon;
                }
            }
        }

        if (itemAmountText != null)
        {
            itemAmountText.text = isEmpty ? "" : amount.ToString();
        }
    }

    // Клик по слоту
    public void OnPointerClick(PointerEventData eventData)
    {
        // Режим выбора для стиральной машины
        if (inventoryManager != null && inventoryManager.IsInWashingMachineSelectionMode())
        {
            if (!isEmpty)
            {
                inventoryManager.SelectCurrentItemForWashing(this);
            }
            return;
        }

        // Обычная логика инвентаря
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isEmpty)
            {
                // Пустой слот – кладём предмет из руки
                inventoryManager.PlaceItemToSlot(this);
            }
            else
            {
                // Занятый слот – берём предмет в руку
                inventoryManager.TakeItemFromSlot(this);
            }
        }
    }



public void FillSlot(ItemScriptableObject newItem, int newAmount)
{
    item = newItem;
    amount = newAmount;
    isEmpty = false;
    if (iconGameObject != null) iconGameObject.SetActive(true);
    UpdateVisual();
}

    public void ClearSlot()
    {
        item = null;
        amount = 0;
        isEmpty = true;
        UpdateVisual();
    }
}