using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static InventoryManager;

public class WashingMachineWithInventory : WashingMachine
{
    [Header("UI Elements")]
    public Canvas machineCanvas;
    public Transform slotsParent;

    [Header("Ссылка на InventoryManager")]
    public InventoryManager playerInventory;
    public FinalPlayerController player;

    [Header("Slots")]
    public List<slot> machineSlots = new List<slot>(4);

    [Header("Слайдеры")]
    public Slider capacitySlider;
    public Slider progressSlider;

    [Header("Кнопки")]
    public Button selectFromInventoryBtn;
    public Button startWashButton;
    public Button clearMachineButton;
    public Button closeButton;

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

    private slot currentTargetSlot;
    private Coroutine washingCoroutine;

    // Таймер стирки (локальный — чтобы не зависеть от WashingMachine.Update(), который здесь перекрыт)
    private float washingTimer = 0f;
    private float washingDuration = 0f;

    // Сохраняем, что именно стираем, чтобы после окончания выдать "чистые" предметы
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
                    RemoveFromMachine(index, true);
                });
            }
            else
            {
                existingButton.onClick.RemoveAllListeners();
                existingButton.onClick.AddListener(() =>
                {
                    RemoveFromMachine(index, true);
                });
            }
        }

        if (selectFromInventoryBtn != null)
            selectFromInventoryBtn.onClick.AddListener(OpenPlayerInventoryForSelection);

        if (startWashButton != null)
            startWashButton.onClick.AddListener(StartWashingProcess);

        if (clearMachineButton != null)
            clearMachineButton.onClick.AddListener(ClearAllSlots);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseUI);

        SetupWashModeToggles();
        SetMode(WashMode.Colored);

        if (machineCanvas != null)
            machineCanvas.gameObject.SetActive(false);

        if (capacitySlider != null)
        {
            capacitySlider.maxValue = 4f;
            capacitySlider.value = 0f;
        }

        if (progressSlider != null)
            progressSlider.value = 0f;
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

    public void OpenPlayerInventoryForSelection()
    {
        if (playerInventory == null)
        {
            Debug.LogError("InventoryManager не назначен!");
            return;
        }

        bool hasEmptySlot = false;
        foreach (var s in machineSlots)
        {
            if (s.isEmpty)
            {
                hasEmptySlot = true;
                break;
            }
        }

        if (!hasEmptySlot)
        {
            Debug.LogWarning("Нет свободных слотов в стиральной машине!");
            return;
        }

        CloseUI();

        if (!playerInventory.isOpened)
            playerInventory.ToggleInventory();

        playerInventory.OpenForWashingMachineSelection(this, -1);

        if (player != null)
            player.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenMachineUI()
    {
        if (machineCanvas == null)
        {
            Debug.LogError("❌ machineCanvas не назначен!");
            return;
        }

        EnsureEventSystemExists();

        machineCanvas.gameObject.SetActive(true);
        Canvas canvas = machineCanvas.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        GraphicRaycaster raycaster = machineCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = true;

        UpdateUI();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (player != null)
        {
            player.SetInputEnabled(false);
            if (player.unifiedRay != null)
                player.unifiedRay.enabled = false;
        }
    }

    void EnsureEventSystemExists()
    {
        EventSystem eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    public void CloseUI()
    {
        if (machineCanvas != null)
            machineCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (player != null) player.SetInputEnabled(true);
        if (player != null && player.unifiedRay != null)
            player.unifiedRay.enabled = true;
    }

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
        if (IsUIOpen() && Input.GetKeyDown(KeyCode.Escape))
            CloseUI();

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

    void StartWashingProcess()
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
        // Используем локальные washingTimer/washingDuration, чтобы таймер точно работал
        while (washingTimer < washingDuration)
        {
            washingTimer += Time.deltaTime;

            if (progressSlider != null && washingDuration > 0f)
                progressSlider.value = Mathf.Clamp01(washingTimer / washingDuration);

            // Обновляем текст таймера сразу во время стирки
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
        // ВАЖНО: останавливаем стирку, иначе UI/таймер будут думать, что всё ещё стираем
        isWashing = false;

        // Создаем "чистые" вещи на выходе из машинки, чтобы их можно было взять в руку и отнести в DeliveryPoint
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
                    itemComp.MakeClean(); // внутри выставится тег CleanThing (если он создан)

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

        // Очищаем слоты
        ClearAllSlots();

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