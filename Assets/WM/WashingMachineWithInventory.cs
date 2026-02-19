using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InventoryManager;

public class WashingMachineWithInventory : WashingMachine
{
    [Header("UI Elements")]
    public Canvas machineCanvas;
    public Transform slotsParent;

    [Header("Ссылка на InventoryManager")]
    public InventoryManager playerInventory;
    public FinalPlayerController player; // может не использоваться теперь

    [Header("Slots")]
    public List<slot> machineSlots = new List<slot>(4);

    [Header("Слайдеры")]
    public Slider capacitySlider;
    public Slider progressSlider;

    [Header("Кнопки")]
    public Button selectFromInventoryBtn; // кнопка "add"
    public Button startWashButton;
    public Button clearMachineButton;
    public Button closeButton; // возможно, больше не нужен

    [Header("Режимы стирки")]
    public Toggle coloredToggle;
    public Toggle delicateToggle;
    public Toggle quickToggle;

    [Header("Информация о режиме")]
    public TextMeshProUGUI modeNameText;
    public TextMeshProUGUI durationText;

    [Header("Предметы в машинке")]
    [SerializeField] private int currentLoad = 0;

    [Header("Таймер и статус")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText;

    [Header("Выход чистых вещей")]
    public Transform cleanItemsSpawnPoint;

    private Coroutine washingCoroutine;
    private float washingTimer = 0f;
    private float washingDuration = 0f;
    private readonly List<(ItemScriptableObject item, int amount)> washedItems = new List<(ItemScriptableObject item, int amount)>();

    void Start()
    {
        if (playerInventory == null) playerInventory = FindObjectOfType<InventoryManager>();
        if (player == null) player = FindObjectOfType<FinalPlayerController>();

        if (cleanItemsSpawnPoint == null)
        {
            GameObject spawnPoint = new GameObject("CleanItemsSpawnPoint");
            spawnPoint.transform.position = transform.position + transform.forward * 2f + Vector3.up * 0.5f;
            spawnPoint.transform.parent = transform;
            cleanItemsSpawnPoint = spawnPoint.transform;
        }

        // Настройка слотов машины
        if (machineSlots.Count == 0 && slotsParent != null)
        {
            foreach (Transform t in slotsParent)
            {
                slot s = t.GetComponent<slot>();
                if (s != null) machineSlots.Add(s);
            }
        }

        for (int i = 0; i < machineSlots.Count; i++)
        {
            int index = i;
            machineSlots[i].ClearSlot();
            machineSlots[i].iconGameObject.SetActive(true);

            Button existingButton = machineSlots[i].GetComponent<Button>();
            if (existingButton == null)
            {
                machineSlots[i].gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    RemoveFromMachine(index);
                });
            }
            else
            {
                existingButton.onClick.RemoveAllListeners();
                existingButton.onClick.AddListener(() =>
                {
                    RemoveFromMachine(index);
                });
            }
        }

        // Привязка кнопок
        if (selectFromInventoryBtn != null)
            selectFromInventoryBtn.onClick.AddListener(AddItemsFromPlayerInventoryAutomatically);

        if (startWashButton != null)
            startWashButton.onClick.AddListener(StartWashingProcess);

        if (clearMachineButton != null)
            clearMachineButton.onClick.AddListener(ClearAllSlots);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseUI); // можно удалить, если кнопка не нужна

        SetupWashModeToggles();
        SetMode(WashMode.Colored);

        // UI всегда активен
        if (machineCanvas != null)
            machineCanvas.gameObject.SetActive(true);

        if (capacitySlider != null)
        {
            capacitySlider.maxValue = 4f;
            capacitySlider.value = 0f;
        }

        if (progressSlider != null)
            progressSlider.value = 0f;

        UpdateUI();
    }

    void SetupWashModeToggles()
    {
        if (coloredToggle != null)
            coloredToggle.onValueChanged.AddListener(isOn => { if (isOn) SetMode(WashMode.Colored); });

        if (delicateToggle != null)
            delicateToggle.onValueChanged.AddListener(isOn => { if (isOn) SetMode(WashMode.Delicate); });

        if (quickToggle != null)
            quickToggle.onValueChanged.AddListener(isOn => { if (isOn) SetMode(WashMode.Quick); });

        if (coloredToggle != null)
            coloredToggle.isOn = true;
    }

    public new void SetMode(WashMode mode)
    {
        base.SetMode(mode);
        UpdateModeDisplay();
    }

    /// <summary>
    /// Автоматически переносит предметы из инвентаря игрока в свободные слоты машины.
    /// </summary>
    public void AddItemsFromPlayerInventoryAutomatically()
    {
        if (playerInventory == null)
        {
            Debug.LogError("❌ InventoryManager не назначен!");
            return;
        }

        if (isWashing)
        {
            Debug.LogWarning("⚠️ Нельзя добавлять предметы во время стирки!");
            return;
        }

        // Отладка: состояние инвентаря
        Debug.Log($"📦 Инвентарь игрока: {playerInventory.slots.Count} слотов.");
        for (int i = 0; i < playerInventory.slots.Count; i++)
        {
            slot s = playerInventory.slots[i];
            Debug.Log($"   Слот {i}: isEmpty={s.isEmpty}, item={(s.item != null ? s.item.name : "null")}");
        }

        // Отладка: состояние слотов машины
        Debug.Log($"🧺 Машина: {machineSlots.Count} слотов.");
        for (int i = 0; i < machineSlots.Count; i++)
        {
            slot s = machineSlots[i];
            Debug.Log($"   Слот {i}: isEmpty={s.isEmpty}, item={(s.item != null ? s.item.name : "null")}");
        }

        int movedCount = 0;
        for (int p = playerInventory.slots.Count - 1; p >= 0; p--)
        {
            slot playerSlot = playerInventory.slots[p];
            if (playerSlot == null || playerSlot.isEmpty || playerSlot.item == null)
            {
                Debug.Log($"   Пропускаем слот игрока {p}: пустой или null");
                continue;
            }

            int machineIndex = FindFirstEmptyMachineSlotIndex();
            if (machineIndex == -1)
            {
                Debug.Log("   Нет свободных слотов в машине, прерываем цикл.");
                break;
            }

            Debug.Log($"   Переносим предмет '{playerSlot.item.name}' из слота {p} в слот машины {machineIndex}");
            machineSlots[machineIndex].FillSlot(playerSlot.item, playerSlot.amount);
            playerSlot.ClearSlot();
            movedCount++;
        }

        if (movedCount == 0)
            Debug.Log("❌ Нет предметов для переноса (или машинка заполнена).");
        else
            Debug.Log($"✅ Перенесено предметов в стиральную машину: {movedCount}");

        UpdateUI();
    }

    int FindFirstEmptyMachineSlotIndex()
    {
        for (int i = 0; i < machineSlots.Count; i++)
        {
            if (machineSlots[i] != null && machineSlots[i].isEmpty)
                return i;
        }
        return -1;
    }

    // Методы открытия/закрытия UI больше не нужны, но можно оставить для совместимости
    public void OpenMachineUI() { /* UI всегда открыт */ }
    public void CloseUI() { /* UI всегда открыт, можно ничего не делать или скрыть, если нужно */ }

    public bool IsUIOpen() => machineCanvas != null && machineCanvas.gameObject.activeSelf;

    public void UpdateUI()
    {
        currentLoad = GetItemsCount();

        for (int i = 0; i < machineSlots.Count; i++)
        {
            if (machineSlots[i] != null && machineSlots[i].isEmpty)
                machineSlots[i].ClearSlot();
        }

        if (capacitySlider != null)
            capacitySlider.value = currentLoad;

        bool full = currentLoad >= 4;

        if (selectFromInventoryBtn != null)
            selectFromInventoryBtn.interactable = !full && !isWashing;

        if (startWashButton != null)
            startWashButton.interactable = currentLoad > 0 && !isWashing;

        if (clearMachineButton != null)
            clearMachineButton.interactable = currentLoad > 0 && !isWashing;

        UpdateModeDisplay();

        if (timerText != null)
        {
            if (isWashing)
            {
                float remaining = Mathf.Max(0f, washingDuration - washingTimer);
                timerText.text = $"Осталось: {remaining:F1} сек.";
                timerText.color = Color.yellow;
            }
            else
            {
                timerText.text = "Готово к стирке";
                timerText.color = Color.green;
            }
        }

        if (statusText != null)
        {
            if (isWashing)
            {
                statusText.text = "СТИРКА...";
                statusText.color = Color.yellow;
            }
            else if (currentLoad > 0)
            {
                statusText.text = "ГОТОВО К СТИРКЕ";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "ПУСТО";
                statusText.color = Color.gray;
            }
        }

        if (!isWashing && durationText != null)
        {
            float duration = GetCurrentWashDuration();
            durationText.text = $"Длительность: {duration} сек.";
        }
    }

    void Update()
    {
        // Убрано закрытие по Escape, так как UI всегда открыт
        if (isWashing)
            UpdateUI();
    }

    void RemoveFromMachine(int machineSlotIndex)
    {
        if (machineSlotIndex < 0 || machineSlotIndex >= machineSlots.Count) return;
        slot s = machineSlots[machineSlotIndex];
        if (s.isEmpty) return;

        ItemScriptableObject itemToReturn = s.item;
        int amount = s.amount;

        if (playerInventory != null && itemToReturn.WorldPrefab != null)
        {
            GameObject go = Instantiate(itemToReturn.WorldPrefab);
            Item itemComp = go.GetComponent<Item>();
            itemComp.item = itemToReturn;
            itemComp.amount = amount;
            playerInventory.ReturnItemToSlot(go, playerInventory.FindEmptySlot());
        }

        s.ClearSlot();
        currentLoad--;
        UpdateUI();
    }

    void ClearAllSlots()
    {
        for (int i = 0; i < machineSlots.Count; i++)
            RemoveFromMachine(i);
        UpdateUI();
    }

    public void StartWashingProcess()
    {
        if (currentLoad == 0 || isWashing)
        {
            Debug.LogWarning("Нечего стирать или стирка уже идет!");
            return;
        }

        SaveWashedItems();
        washingDuration = GetCurrentWashDuration();
        washingTimer = 0f;
        StartWashing();

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.maxValue = 1f;
        }

        UpdateUI();

        if (washingCoroutine != null)
            StopCoroutine(washingCoroutine);

        washingCoroutine = StartCoroutine(WashingProgress());
    }

    void SaveWashedItems()
    {
        washedItems.Clear();
        foreach (var slot in machineSlots)
        {
            if (!slot.isEmpty && slot.item != null)
            {
                washedItems.Add((slot.item, Mathf.Max(1, slot.amount)));
            }
        }
        Debug.Log($"Сохранено {washedItems.Count} вещей для стирки");
    }

    System.Collections.IEnumerator WashingProgress()
    {
        while (washingTimer < washingDuration)
        {
            washingTimer += Time.deltaTime;

            if (progressSlider != null && washingDuration > 0f)
                progressSlider.value = Mathf.Clamp01(washingTimer / washingDuration);

            if (timerText != null)
            {
                float remaining = Mathf.Max(0f, washingDuration - washingTimer);
                timerText.text = $"Осталось: {remaining:F1} сек.";
                timerText.color = Color.yellow;
            }

            yield return null;
        }

        FinishWashingProcess();
    }

    void FinishWashingProcess()
    {
        isWashing = false;

        if (cleanItemsSpawnPoint != null && washedItems.Count > 0)
        {
            float offsetStep = 0.25f;
            int spawned = 0;

            foreach (var entry in washedItems)
            {
                if (entry.item == null || entry.item.WorldPrefab == null) continue;

                for (int i = 0; i < entry.amount; i++)
                {
                    Vector3 pos = cleanItemsSpawnPoint.position + cleanItemsSpawnPoint.right * (spawned * offsetStep);
                    GameObject go = Instantiate(entry.item.WorldPrefab, pos, cleanItemsSpawnPoint.rotation);

                    Item itemComp = go.GetComponent<Item>();
                    if (itemComp == null) itemComp = go.AddComponent<Item>();

                    itemComp.item = entry.item;
                    itemComp.amount = 1;
                    itemComp.MakeClean();

                    spawned++;
                }
            }

            Debug.Log($"✅ Стирка завершена! Выдано чистых вещей: {spawned}");
        }
        else
        {
            Debug.Log("Стирка завершена, но cleanItemsSpawnPoint не задан или нечего выдавать");
        }

        washedItems.Clear();

        for (int i = 0; i < machineSlots.Count; i++)
        {
            if (machineSlots[i] != null)
                machineSlots[i].ClearSlot();
        }
        currentLoad = 0;

        if (progressSlider != null)
            progressSlider.value = 1f;

        if (timerText != null)
            timerText.text = "Стирка завершена!";

        if (statusText != null)
            statusText.text = "ГОТОВО";

        UpdateUI();
    }

    int GetItemsCount()
    {
        int count = 0;
        foreach (var s in machineSlots)
            if (!s.isEmpty) count++;
        return count;
    }

    public bool AddItemToMachine(ItemScriptableObject item, int amount = 1)
    {
        if (currentLoad >= machineSlots.Count || isWashing)
            return false;

        foreach (var slot in machineSlots)
        {
            if (slot.isEmpty)
            {
                slot.FillSlot(item, amount);
                currentLoad++;
                UpdateUI();
                return true;
            }
        }
        return false;
    }

    void UpdateModeDisplay()
    {
        if (modeNameText != null)
        {
            switch (currentMode)
            {
                case WashMode.Colored:
                    modeNameText.text = "ЦВЕТНОЕ БЕЛЬЁ";
                    modeNameText.color = Color.blue;
                    break;
                case WashMode.Delicate:
                    modeNameText.text = "ДЕЛИКАТНОЕ";
                    modeNameText.color = Color.magenta;
                    break;
                case WashMode.Quick:
                    modeNameText.text = "БЫСТРАЯ СТИРКА";
                    modeNameText.color = Color.green;
                    break;
            }
        }

        if (durationText != null)
        {
            float duration = GetCurrentWashDuration();
            durationText.text = $"Длительность: {duration} сек.";
        }
    }
}