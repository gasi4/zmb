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
        // Находим компоненты
        if (transform.childCount > 0)
        {
            iconGameObject = transform.GetChild(0).gameObject;
        }

        if (transform.childCount > 1)
        {
            itemAmountText = transform.GetChild(1).GetComponent<TMP_Text>();
        }

        // Находим InventoryManager и PlayerController
        inventoryManager = FindObjectOfType<InventoryManager>();
        playerController = FindObjectOfType<FinalPlayerController>();

        // Скрываем иконку по умолчанию
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
        // 1. Режим выбора для стиральной машины
        if (inventoryManager != null && inventoryManager.IsInWashingMachineSelectionMode())
        {
            if (!isEmpty)
            {
                inventoryManager.SelectCurrentItemForWashing(this);
            }
            return;
        }

        // 2. Обычная логика инвентаря
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isEmpty)
            {
                // ЛКМ на пустой слот: кладем предмет из руки
                PlaceItemFromHand();
            }
            else
            {
                // ЛКМ на занятый слот: берем предмет в руку
                TakeItemToHand();
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // ПКМ: специальное действие (можно убрать если не нужно)
            Debug.Log("ПКМ по слоту");
        }
    }

    void PlaceItemFromHand()
    {
        if (!isEmpty) return;
        if (!playerController.HasItemInHand()) return;

        GameObject heldObject = playerController.GetHeldItem();
        if (heldObject == null) return;

        Item itemComponent = heldObject.GetComponent<Item>();
        if (itemComponent == null)
        {
            Debug.LogError("В руке объект без Item компонента!");
            return;
        }

        FillSlot(itemComponent.item, itemComponent.amount);
        playerController.HideHeldObject();
        playerController.ClearHeldItem();
    }

    void TakeItemToHand()
    {
        if (isEmpty) return;
        if (playerController == null) return;

        ItemScriptableObject takenItem = item;
        int takenAmount = amount;

        if (takenItem.HandPrefab == null)
        {
            Debug.LogError($"❌ У предмета {takenItem.ItemName} не назначен HandPrefab");
            return;
        }

        // Создаем объект в руке
        GameObject itemInHand = Instantiate(takenItem.HandPrefab);
        Item itemComponent = itemInHand.GetComponent<Item>();
        if (itemComponent == null)
            itemComponent = itemInHand.AddComponent<Item>();

        itemComponent.item = takenItem;
        itemComponent.amount = takenAmount;

        // Передаем в руку
        playerController.GrabItemToHand(itemInHand);

        // Очищаем слот
        ClearSlot();

        Debug.Log($"✅ Взял предмет из слота в руку: {takenItem.ItemName}");
    }

    public void FillSlot(ItemScriptableObject newItem, int newAmount)
    {
        item = newItem;
        amount = newAmount;
        isEmpty = false;
        UpdateVisual();
    }

    public void ClearSlot()
    {
        item = null;
        amount = 0;
        isEmpty = true;
        UpdateVisual();
    }

    // УДАЛИТЕ метод OnClick() - он больше не нужен!
    /*
    public void OnClick()
    {
        // ... старый код ...
    }
    */
}