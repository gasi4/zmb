using System.Collections.Generic;
using UnityEngine;

public class WashingMachine : MonoBehaviour
{
    // Оставляем только 3 режима
    public enum WashMode { Colored, Delicate, Quick }

    [System.Serializable]
    public class WashModeSettings
    {
        public WashMode mode;
        public string displayName;
        public float duration;        // Длительность стирки в секундах
        public string description;    // Описание режима
    }

    [System.Serializable]
    public class ClothesItem
    {
        public GameObject obj;
        public string name;
        public int originalSlotIndex = -1;
    }

    [Header("Настройки")]
    public int maxCapacity = 4;
    public Transform spawnPoint; // Точка спавна чистой одежды

    [Header("Режимы стирки")]
    public List<WashModeSettings> washModes = new List<WashModeSettings>();
    [SerializeField] protected WashMode currentMode = WashMode.Colored;

    [HideInInspector] public bool isWashing = false;
    private float washTimer = 0f;
    private float currentWashDuration = 5f; // Текущая длительность стирки

    public List<ClothesItem> slots = new List<ClothesItem>();

    [Header("UI")]
    public WashingMachineUI ui;

    void Start()
    {
        // Инициализируем режимы если список пуст
        if (washModes.Count == 0)
        {
            InitializeWashModes();
        }

        // Устанавливаем длительность текущего режима
        UpdateCurrentWashDuration();
    }

    void InitializeWashModes()
    {
        washModes = new List<WashModeSettings>
        {
            new WashModeSettings
            {
                mode = WashMode.Colored,
                displayName = "ЦВЕТНОЕ",
                duration = 12f, // 5 секунд для теста
                description = "Для цветного белья. Средняя температура."
            },
            new WashModeSettings
            {
                mode = WashMode.Delicate,
                displayName = "ДЕЛИКАТНОЕ",
                duration = 15f, // 8 секунд для теста
                description = "Для деликатных тканей. Низкая температура."
            },
            new WashModeSettings
            {
                mode = WashMode.Quick,
                displayName = "БЫСТРАЯ",
                duration = 5f, // 3 секунды для теста
                description = "Быстрая стирка. Для слабозагрязнённого белья."
            }
        };
    }


    void UpdateCurrentWashDuration()
    {
        foreach (var mode in washModes)
        {
            if (mode.mode == currentMode)
            {
                currentWashDuration = mode.duration;
                return;
            }
        }
        currentWashDuration = 5f; // значение по умолчанию
    }

    // ------------------ Основные методы ------------------
    void Update()
    {
        if (!isWashing) return;

        washTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(washTimer / currentWashDuration);


        if (progress >= 1f)
            FinishWashing();
    }

    public bool LoadClothes(GameObject obj, int originalSlot = -1)
    {
        if (slots.Count >= maxCapacity || isWashing || obj == null) return false;

        var item = new ClothesItem { obj = obj, name = obj.name, originalSlotIndex = originalSlot };
        slots.Add(item);

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        ui?.UpdateUIPublic();
        return true;
    }

    public void RemoveClothes(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        var item = slots[slotIndex];
        item.obj.SetActive(true);
        item.obj.transform.SetParent(null);

        slots.RemoveAt(slotIndex);
        ui?.UpdateUIPublic();
    }

    public void StartWashing()
    {
        if (slots.Count == 0 || isWashing)
        {
            Debug.LogWarning("Нечего стирать или стирка уже идет!");
            return;
        }

        isWashing = true;
        washTimer = 0f;
        UpdateCurrentWashDuration(); // Обновляем длительность перед стартом

        // UI обновление
        if (ui != null)
        {
            ui.UpdateUIPublic();
            ui.SetStatus("Стирка...");
        }

        Debug.Log($"Стирка началась! Режим: {GetCurrentModeSettings().displayName}, " +
                  $"Длительность: {currentWashDuration} сек., " +
                  $"Предметов: {slots.Count}");
    }


    void FinishWashing()
    {
        isWashing = false;
        foreach (var item in slots)
        {
            if (spawnPoint != null)
            {
                item.obj.SetActive(true);
                item.obj.transform.position = spawnPoint.position;
                item.obj.transform.rotation = spawnPoint.rotation;
                item.obj.transform.SetParent(null);
            }
        }

        slots.Clear();
        ui?.UpdateUIPublic();
        Debug.Log("Стирка завершена!");
    }

    public float GetProgressPercentage() => Mathf.Clamp01(washTimer / currentWashDuration) * 100f;

    public void SetMode(WashMode mode)
    {
        if (isWashing)
        {
            Debug.LogWarning("Нельзя менять режим во время стирки!");
            return;
        }

        currentMode = mode;
        UpdateCurrentWashDuration();

        var settings = GetCurrentModeSettings();
        Debug.Log($"Режим стирки: {settings.displayName} | Длительность: {settings.duration} сек.");

        ui?.UpdateModeDisplay(settings);
    }

    public WashModeSettings GetCurrentModeSettings()
    {
        if (washModes.Count == 0)
        {
            InitializeWashModes();
        }

        foreach (var mode in washModes)
        {
            if (mode.mode == currentMode)
                return mode;
        }

        // Если режим не найден, создаем дефолтный
        if (washModes.Count > 0)
            return washModes[0];
        else
        {
            // Создаем дефолтный режим
            var defaultMode = new WashModeSettings
            {
                mode = WashMode.Colored,
                displayName = "ЦВЕТНОЕ",
                duration = 5f,
                description = "Для цветного белья"
            };
            washModes.Add(defaultMode);
            return defaultMode;
        }
    }



    public float GetRemainingTime()
    {
        if (!isWashing) return 0f;
        return Mathf.Max(0f, currentWashDuration - washTimer);
    }

    public string GetRemainingTimeFormatted()
    {
        float remaining = GetRemainingTime();
        return $"{remaining:F1} сек";
    }

    public WashMode GetCurrentMode() => currentMode;
    public int GetLoadedCount() => slots.Count;
    public float GetCurrentWashDuration() => currentWashDuration;
}