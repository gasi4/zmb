using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WashingMachineWithInventory : MonoBehaviour
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
    public Button coloredModeButton;
    public Button delicateModeButton;
    public Button quickModeButton;
    public Color selectedColor = Color.yellow;
    public Color normalColor = Color.white;
    public Color disabledColor = Color.gray;

    [Header("Информация о режиме")]
    public TextMeshProUGUI modeNameText;
    public TextMeshProUGUI durationText;

    [Header("Таймер и статус")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI worldTimerText;
    public float finishedMessageTime = 7f;

    [Header("Выход чистых вещей")]
    public Transform cleanItemsSpawnPoint;

    [Header("Основные элементы")]
    public GameObject panel;

    public enum WashMode { Colored, Delicate, Quick }

    [System.Serializable]
    public class WashModeSettings
    {
        public WashMode mode;
        public string displayName;
        public float duration;
        public string description;
    }

    public List<WashModeSettings> washModes = new List<WashModeSettings>();
    private WashMode currentMode = WashMode.Colored;

    // -----------------
    private float washingTimer = 0f;
    private float washingDuration = 0f;
    private Coroutine washingCoroutine;
    private List<(ItemScriptableObject item, int amount)> washedItems = new List<(ItemScriptableObject item, int amount)>();
    public bool isWashing = false;
    private Dictionary<Button, Color> originalButtonColors = new Dictionary<Button, Color>();

    public float GetProgressPercentage() => washingDuration > 0 ? Mathf.Clamp01(washingTimer / washingDuration) * 100f : 0f;
    public float GetRemainingTime() => Mathf.Max(0f, washingDuration - washingTimer);
    public int GetLoadedCount() => GetItemsCount();
    public int MaxCapacity => machineSlots.Count;

    // Сделаем метод публичным, чтобы UI мог получать настройки
    public WashModeSettings GetCurrentModeSettingsPublic() => GetCurrentModeSettings();

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

        // Инициализация слотов
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
                machineSlots[i].gameObject.AddComponent<Button>().onClick.AddListener(() => RemoveFromMachine(index));
            }
            else
            {
                existingButton.onClick.RemoveAllListeners();
                existingButton.onClick.AddListener(() => RemoveFromMachine(index));
            }
        }

        selectFromInventoryBtn?.onClick.AddListener(AddItemsFromPlayerInventoryAutomatically);
        startWashButton?.onClick.AddListener(StartWashingProcess);
        clearMachineButton?.onClick.AddListener(ClearAllSlots);
        closeButton?.onClick.AddListener(() => { });

        SetupModeButtons();
        SetMode(WashMode.Colored);

        if (machineCanvas != null) machineCanvas.gameObject.SetActive(true);

        if (capacitySlider != null) { capacitySlider.maxValue = 4; capacitySlider.value = 0; }
        if (progressSlider != null) progressSlider.value = 0;

        UpdateUI();
    }

    void SetupModeButtons()
    {
        void SetupButton(Button btn, WashMode mode)
        {
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { if (!isWashing) SetMode(mode); });
                originalButtonColors[btn] = btn.GetComponent<Image>()?.color ?? normalColor;
            }
        }

        SetupButton(coloredModeButton, WashMode.Colored);
        SetupButton(delicateModeButton, WashMode.Delicate);
        SetupButton(quickModeButton, WashMode.Quick);
    }

    void HighlightModeButton(WashMode mode)
    {
        Color targetNormal = isWashing ? disabledColor : normalColor;
        SetButtonColor(coloredModeButton, targetNormal);
        SetButtonColor(delicateModeButton, targetNormal);
        SetButtonColor(quickModeButton, targetNormal);

        Button selected = mode switch
        {
            WashMode.Colored => coloredModeButton,
            WashMode.Delicate => delicateModeButton,
            WashMode.Quick => quickModeButton,
            _ => null
        };
        if (selected != null) SetButtonColor(selected, selectedColor);
    }

    void SetButtonColor(Button btn, Color color)
    {
        if (btn != null)
        {
            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }

    public void SetMode(WashMode mode)
    {
        if (isWashing) return;
        currentMode = mode;
        HighlightModeButton(mode);
        UpdateModeDisplay();
    }

    void UpdateModeDisplay()
    {
        if (modeNameText != null && durationText != null)
        {
            WashModeSettings settings = GetCurrentModeSettings();
            modeNameText.text = settings.displayName;
            durationText.text = $"Длительность: {settings.duration} сек.";
        }
    }

    // -----------------------------
    public void AddItemsFromPlayerInventoryAutomatically()
    {
        if (playerInventory == null || isWashing) return;

        for (int p = playerInventory.slots.Count - 1; p >= 0; p--)
        {
            slot playerSlot = playerInventory.slots[p];
            if (playerSlot == null || playerSlot.isEmpty || playerSlot.item == null) continue;

            int machineIndex = FindFirstEmptyMachineSlotIndex();
            if (machineIndex == -1) break;

            machineSlots[machineIndex].FillSlot(playerSlot.item, playerSlot.amount);
            playerSlot.ClearSlot();
        }

        UpdateUI();
    }

    int FindFirstEmptyMachineSlotIndex()
    {
        for (int i = 0; i < machineSlots.Count; i++)
            if (machineSlots[i].isEmpty) return i;
        return -1;
    }

    void RemoveFromMachine(int machineSlotIndex)
    {
        if (machineSlotIndex < 0 || machineSlotIndex >= machineSlots.Count) return;
        slot s = machineSlots[machineSlotIndex];
        if (s.isEmpty) return;

        if (playerInventory != null && s.item != null && s.item.WorldPrefab != null)
        {
            GameObject go = Instantiate(s.item.WorldPrefab);
            playerInventory.ReturnItemToSlot(go, playerInventory.FindEmptySlot());
        }

        s.ClearSlot();
        UpdateUI();
    }

    void ClearAllSlots()
    {
        for (int i = 0; i < machineSlots.Count; i++)
            RemoveFromMachine(i);
    }

    public void StartWashingProcess()
    {
        if (isWashing || GetItemsCount() == 0) return;

        SaveWashedItems();

        WashModeSettings settings = GetCurrentModeSettings();
        washingDuration = settings.duration;
        washingTimer = 0f;

        if (washingCoroutine != null) StopCoroutine(washingCoroutine);
        washingCoroutine = StartCoroutine(WashingProgress());

        UpdateUI();
    }

    void SaveWashedItems()
    {
        washedItems.Clear();
        foreach (var s in machineSlots)
            if (!s.isEmpty && s.item != null)
                washedItems.Add((s.item, s.amount));
    }

    IEnumerator WashingProgress()
    {
        isWashing = true;
        while (washingTimer < washingDuration)
        {
            washingTimer += Time.deltaTime;
            if (progressSlider != null)
                progressSlider.value = Mathf.Clamp01(washingTimer / washingDuration);

            if (worldTimerText != null)
            {
                float remaining = Mathf.Max(0f, washingDuration - washingTimer);
                worldTimerText.text = $"{Mathf.Ceil(remaining)} сек";
            }

            yield return null;
        }

        FinishWashingProcess();
    }

    void FinishWashingProcess()
    {
        isWashing = false;

        if (cleanItemsSpawnPoint != null)
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

        }

        washedItems.Clear();
        for (int i = 0; i < machineSlots.Count; i++)
            machineSlots[i].ClearSlot();

        if (progressSlider != null) progressSlider.value = 1f;
        if (timerText != null) timerText.text = "Стирка завершена!";
        if (statusText != null) statusText.text = "ГОТОВО";

        StartCoroutine(ShowFinishedMessage());
        UpdateUI();
    }

    IEnumerator ShowFinishedMessage()
    {
        if (worldTimerText == null) yield break;
        worldTimerText.gameObject.SetActive(true);
        worldTimerText.text = "Вещь постиралась";
        yield return new WaitForSeconds(finishedMessageTime);
        worldTimerText.gameObject.SetActive(false);
    }

    int GetItemsCount()
    {
        int count = 0;
        foreach (var s in machineSlots)
            if (!s.isEmpty) count++;
        return count;
    }

    public void UpdateUI()
    {
        int load = GetItemsCount();
        if (capacitySlider != null) capacitySlider.value = load;
        if (startWashButton != null) startWashButton.interactable = !isWashing && load > 0;
        if (clearMachineButton != null) clearMachineButton.interactable = !isWashing && load > 0;
        if (selectFromInventoryBtn != null) selectFromInventoryBtn.interactable = !isWashing && load < machineSlots.Count;

        HighlightModeButton(currentMode);
        UpdateModeDisplay();

        if (worldTimerText != null && isWashing)
        {
            float remaining = Mathf.Max(0f, washingDuration - washingTimer);
            worldTimerText.text = $"{remaining:F1} сек";
            worldTimerText.color = Color.yellow;
        }
    }

    // ---------------- Публичный метод для внутреннего GetCurrentModeSettings
    public WashModeSettings GetCurrentModeSettings()
    {
        if (washModes.Count == 0)
        {
            // Инициализация дефолтных режимов
            washModes = new List<WashModeSettings>
            {
                new WashModeSettings { mode = WashMode.Colored, displayName = "ЦВЕТНОЕ", duration = 12f, description = "Для цветного белья" },
                new WashModeSettings { mode = WashMode.Delicate, displayName = "ДЕЛИКАТНОЕ", duration = 15f, description = "Для деликатных тканей" },
                new WashModeSettings { mode = WashMode.Quick, displayName = "БЫСТРАЯ", duration = 5f, description = "Быстрая стирка" }
            };
        }

        foreach (var mode in washModes)
            if (mode.mode == currentMode) return mode;

        return washModes[0];
    }
}