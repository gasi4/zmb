using System.Collections.Generic;
using UnityEngine;

public class WashingMachine : MonoBehaviour
{
    public enum WashMode { Colored, Delicate, Quick }

    [System.Serializable]
    public class WashModeSettings
    {
        public WashMode mode;
        public string displayName;
        public float duration;
        public string description;
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
    public Transform spawnPoint;

    [Header("Режимы стирки")]
    public List<WashModeSettings> washModes = new List<WashModeSettings>();
    [SerializeField] protected WashMode currentMode = WashMode.Colored;

    [HideInInspector] public bool isWashing = false;
    private float washTimer = 0f;
    private float currentWashDuration = 5f;

    public List<ClothesItem> slots = new List<ClothesItem>();
    [Header("UI")]
    public WashingMachineUI ui;

    void Start()
    {
        if (washModes.Count == 0) InitializeWashModes();
        UpdateCurrentWashDuration();
    }

    void InitializeWashModes()
    {
        washModes = new List<WashModeSettings>
        {
            new WashModeSettings { mode = WashMode.Colored, displayName = "ЦВЕТНОЕ", duration = 12f, description = "Для цветного белья" },
            new WashModeSettings { mode = WashMode.Delicate, displayName = "ДЕЛИКАТНОЕ", duration = 15f, description = "Для деликатных тканей" },
            new WashModeSettings { mode = WashMode.Quick, displayName = "БЫСТРАЯ", duration = 5f, description = "Быстрая стирка" }
        };
    }

    void UpdateCurrentWashDuration()
    {
        foreach (var mode in washModes)
            if (mode.mode == currentMode)
            {
                currentWashDuration = mode.duration;
                return;
            }
        currentWashDuration = 5f;
    }

    void Update()
    {
        if (!isWashing) return;
        washTimer += Time.deltaTime;
        if (washTimer >= currentWashDuration) FinishWashing();
    }

    public bool LoadClothes(GameObject obj, int originalSlot = -1)
    {
        if (slots.Count >= maxCapacity || isWashing || obj == null) return false;

        slots.Add(new ClothesItem { obj = obj, name = obj.name, originalSlotIndex = originalSlot });
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
        if (slots.Count == 0 || isWashing) return;
        isWashing = true;
        washTimer = 0f;
        UpdateCurrentWashDuration();
        ui?.UpdateUIPublic();
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
    }

    public float GetProgressPercentage() => Mathf.Clamp01(washTimer / currentWashDuration) * 100f;
    public float GetRemainingTime() => Mathf.Max(0f, currentWashDuration - washTimer);
    public int GetLoadedCount() => slots.Count;
    public WashMode GetCurrentMode() => currentMode;
    public float GetCurrentWashDuration() => currentWashDuration;

    public void SetMode(WashMode mode)
    {
        if (isWashing) return;

        currentMode = mode;
        UpdateCurrentWashDuration();

        // Передаем напрямую, без Convert
        var settings = GetCurrentModeSettings();
        ui?.UpdateModeDisplay(settings);
    }

    public WashModeSettings GetCurrentModeSettings()
    {
        if (washModes.Count == 0) InitializeWashModes();
        foreach (var mode in washModes)
            if (mode.mode == currentMode) return mode;
        return washModes[0];
    }
}