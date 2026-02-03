using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Добавьте эту строку!

public class InventoryManager : MonoBehaviour
{
    public GameObject uiPanel;
    public FinalPlayerController playerController;
    public Transform inventoryPanel;
    public List<slot> slots = new List<slot>();
    public bool isOpened = false;

    // Предмет в руке
    private ItemScriptableObject heldItem;
    private int heldItemAmount = 0;


    public List<GameObject> items = new List<GameObject>();
    private WashingMachineWithInventory currentMachine;
    private int targetSlot = -1;


    // В классе InventoryManager
    public void OpenForWashingMachineSelection(WashingMachineWithInventory machine, int slotIndex)
    {
        currentMachine = machine;
        targetSlot = slotIndex;

        // Открываем инвентарь если закрыт
        if (!isOpened)
        {
            ToggleInventory();
        }

        // Добавляем информацию о режиме выбора
        Debug.Log($"Режим выбора предмета для машинки активен. Кликните на предмет для перемещения в машинку");

        // Можно также добавить визуальную индикацию режима
        // Например, изменить цвет заголовка или добавить подсказку
    }



    public void SelectItemForWashing(GameObject item)
    {
        if (currentMachine == null || item == null) return;

        // Ищем первый пустой слот в машинке
        int emptySlot = -1;
        for (int i = 0; i < currentMachine.machineSlots.Count; i++)
        {
            if (currentMachine.machineSlots[i].isEmpty)
            {
                emptySlot = i;
                break;
            }
        }

        if (emptySlot == -1)
        {
            Debug.LogWarning("Нет свободных слотов в машинке!");
            return;
        }

        // Кладём предмет в слот машинки
        Item itemComp = item.GetComponent<Item>();
        if (itemComp == null) return;

        currentMachine.machineSlots[emptySlot].FillSlot(itemComp.item, itemComp.amount);

        // Убираем предмет из инвентаря игрока
        RemoveItemFromSlot(items.IndexOf(item));
        item.SetActive(false);

        // Обновляем UI машинки
        currentMachine.UpdateUI();

        Debug.Log($"Предмет {itemComp.item.ItemName} добавлен в машинку в слот {emptySlot}");
    }


    public GameObject GetItemFromSlot(int index)
    {
        if (index < 0 || index >= items.Count) return null;
        return items[index];
    }

    public bool RemoveItemFromSlot(int index)
    {
        if (index < 0 || index >= items.Count) return false;
        items[index].SetActive(false);
        items.RemoveAt(index);
        return true;
    }

    public int FindEmptySlot() => items.Count; // упрощено
    public void ReturnItemToSlot(GameObject item, int slot) => items.Add(item);

    void Start()
    {
        Debug.Log("InventoryManager Start вызван");

        // Закрываем при старте
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
            isOpened = false;
        }

        // Находим все слоты
        if (inventoryPanel != null)
        {
            Debug.Log($"Начинаю поиск слотов в {inventoryPanel.name}, childCount: {inventoryPanel.childCount}");

            for (int i = 0; i < inventoryPanel.childCount; i++)
            {
                Transform child = inventoryPanel.GetChild(i);
                slot currentSlot = child.GetComponent<slot>();

                if (currentSlot != null)
                {
                    slots.Add(currentSlot);

                    // ДОБАВЛЯЕМ КНОПКУ ТОЛЬКО ЕСЛИ ЕЁ НЕТ
                    Button existingButton = child.GetComponent<Button>();
                    if (existingButton == null)
                    {
                        existingButton = child.gameObject.AddComponent<Button>();
                    }

                    // Устанавливаем обработчик
                    int index = i; // для лямбды
                    existingButton.onClick.RemoveAllListeners();
                    existingButton.onClick.AddListener(() =>
                    {
                        // Вызываем метод OnClick слота
                        //currentSlot.OnClick();
                    });

                    Debug.Log($"Добавлен слот: {child.name}");
                }
                else
                {
                    Debug.LogWarning($"У объекта {child.name} нет компонента slot");
                }
            }
        }
        else
        {
            Debug.LogError("inventoryPanel не назначен!");
        }

        Debug.Log($"Всего найдено слотов: {slots.Count}");
    }

    void Update()
    {
        // Быстрое сброс предмета по Q
        if (Input.GetKeyDown(KeyCode.Q) && HasItemInHand())
        {
            DropHeldItem();
        }

        // Тест: синхронизация с контроллером
        SyncWithController();
    }

    // Синхронизация с контроллером
    void SyncWithController()
    {
        if (playerController == null) return;

        // Если у контроллера есть предмет инвентаря в руке, а у нас нет - синхронизируем
        if (playerController.HasInventoryItemInHand() && !HasItemInHand())
        {
            ItemScriptableObject controllerItem = playerController.GetInventoryItemInHand();
            if (controllerItem != null)
            {
                heldItem = controllerItem;
                heldItemAmount = 1; // По умолчанию 1
                Debug.Log($"Синхронизирован предмет из контроллера: {heldItem.ItemName}");
            }
        }
    }

    // Переключение инвентаря
    public void ToggleInventory()
    {
        isOpened = !isOpened;
        uiPanel.SetActive(isOpened);

        if (isOpened)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            playerController?.SetInputEnabled(false);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            playerController?.SetInputEnabled(true);
        }

        Debug.Log($"Инвентарь {(isOpened ? "открыт" : "закрыт")}");
    }


    // Добавить предмет из мира в инвентарь
    public void AddItem(ItemScriptableObject _item, int amount)
    {
        if (_item == null)
        {
            Debug.LogError("Попытка добавить null предмет!");
            return;
        }

        Debug.Log($"Пытаюсь добавить предмет: {_item.ItemName} x{amount}");

        // Ищем пустой слот
        foreach (slot slot in slots)
        {
            if (slot.isEmpty)
            {
                slot.FillSlot(_item, amount);
                Debug.Log($"Добавлен предмет: {_item.ItemName} x{amount} в слот {slot.gameObject.name}");
                return;
            }
        }

        Debug.LogWarning("Нет свободных слотов!");
    }

    // Взять предмет из слота в руку
    public void TakeItemFromSlot(slot fromSlot)
    {
        if (fromSlot.isEmpty || fromSlot.item == null)
            return;

        // Если уже есть предмет в руке - кладем его обратно
        if (HasItemInHand())
        {
            PlaceItemToSlot(fromSlot);
            return;
        }

        // Берем предмет из слота
        heldItem = fromSlot.item;
        heldItemAmount = fromSlot.amount;

        fromSlot.ClearSlot();

        if (playerController != null)
        {
            playerController.GrabItemFromInventory(heldItem, heldItemAmount);
        }

        Debug.Log($"Взял предмет из слота: {heldItem.ItemName}");
    }


    // Положить предмет из руки в слот
    // В классе InventoryManager
    public void PlaceItemToSlot(slot toSlot)
    {
        Debug.Log($"PlaceItemToSlot вызван для слота: {toSlot.gameObject.name}");

        if (!HasItemInHand())
        {
            Debug.Log("В руке нет предмета инвентаря!");
            return;
        }

        // СОХРАНЯЕМ ССЫЛКИ ПЕРЕД ОЧИСТКОЙ
        ItemScriptableObject itemToPlace = heldItem;
        int amountToPlace = heldItemAmount;

        Debug.Log($"Пытаюсь положить в слот: {itemToPlace?.ItemName} x{amountToPlace}");

        if (itemToPlace == null)
        {
            Debug.LogError("Предмет для размещения null!");
            ClearHand();
            return;
        }

        // Кладем предмет в слот
        toSlot.FillSlot(itemToPlace, amountToPlace);

        // Очищаем руку ПОСЛЕ размещения
        ClearHand();

        Debug.Log($"Предмет {itemToPlace.ItemName} положен в слот");
    }

    // Очистить руку
    public void ClearHand()
    {
        heldItem = null;
        heldItemAmount = 0;

        if (playerController != null)
        {
            playerController.ClearHeldItem();
        }

        Debug.Log("Рука очищена (InventoryManager)");
    }

    // Проверка, есть ли предмет в руке
    public bool HasItemInHand()
    {
        return heldItem != null;
    }

    // Сбросить предмет из руки на землю
    public void DropHeldItem()
    {
        if (!HasItemInHand()) return;

        Debug.Log($"Сбрасываю предмет: {heldItem.ItemName}");

        if (heldItem.WorldPrefab != null && playerController != null)
        {
            GameObject droppedItem = Instantiate(heldItem.WorldPrefab);

            Vector3 dropPos =
                playerController.transform.position +
                playerController.transform.forward * 2f +
                Vector3.up * 0.5f;

            droppedItem.transform.position = dropPos;

            // Добавляем компонент Item
            Item itemComponent = droppedItem.GetComponent<Item>();
            if (itemComponent == null)
                itemComponent = droppedItem.AddComponent<Item>();

            itemComponent.item = heldItem;
            itemComponent.amount = heldItemAmount;

            // Rigidbody
            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb == null)
                rb = droppedItem.AddComponent<Rigidbody>();

            rb.velocity = playerController.transform.forward * 3f;
        }

        ClearHand();
    }


    // Получить предмет из руки
    public ItemScriptableObject GetHeldItem()
    {
        return heldItem;
    }

    // Получить количество предметов в руке
    public int GetHeldItemAmount()
    {
        return heldItemAmount;
    }

    // В классе InventoryManager добавьте:

    private bool isInWashingMachineSelectionMode = false;

    // Проверка режима выбора
    public bool IsInWashingMachineSelectionMode()
    {
        return currentMachine != null;
    }

    // Метод для выбора предмета для машинки
    public void SelectCurrentItemForWashing(slot selectedSlot)
    {
        if (currentMachine == null || selectedSlot == null || selectedSlot.isEmpty)
        {
            Debug.LogWarning("Невозможно выбрать предмет для машинки");
            return;
        }

        // 1. Находим пустой слот в машинке
        int emptySlotIndex = -1;
        for (int i = 0; i < currentMachine.machineSlots.Count; i++)
        {
            if (currentMachine.machineSlots[i].isEmpty)
            {
                emptySlotIndex = i;
                break;
            }
        }

        if (emptySlotIndex == -1)
        {
            Debug.LogWarning("В машинке нет свободных слотов!");
            return;
        }

        // 2. Перемещаем предмет
        currentMachine.machineSlots[emptySlotIndex].FillSlot(selectedSlot.item, selectedSlot.amount);

        // 3. Очищаем слот в инвентаре игрока
        selectedSlot.ClearSlot();

        // 4. Обновляем UI машинки
        currentMachine.UpdateUI();

        // 5. Закрываем инвентарь
        if (isOpened)
        {
            ToggleInventory();
        }

        // 6. Сбрасываем режим выбора
        currentMachine = null;
        targetSlot = -1;

        Debug.Log($"✅ Предмет перемещен в стиральную машину в слот {emptySlotIndex}");

        // 7. Включаем управление игроком
        var player = FindObjectOfType<FinalPlayerController>();
        if (player != null)
        {
            player.SetInputEnabled(true);
        }

        // 8. Возвращаем курсор в нормальное состояние
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    // Синхронизировать предмет в рукем
    public void SyncHeldItem(ItemScriptableObject item, int amount)
    {
        heldItem = item;
        heldItemAmount = amount;

        Debug.Log($"Синхронизирован предмет в руке: {item?.ItemName} x{amount}");
    }
}