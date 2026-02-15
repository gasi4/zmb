using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class WashingMachineUI : MonoBehaviour
{
    [Header("Основные элементы")]
    public GameObject panel;
    public Button closeButton;

    [Header("Режимы стирки - ТЕПЕРЬ ТОЛЬКО 3")]
    public Toggle coloredToggle;
    public Toggle delicateToggle;
    public Toggle quickToggle;

    [Header("Информация о режиме")]
    public Text modeNameText;
    public Text modeDescriptionText;
    public Text durationText;

    [Header("Индикаторы")]
    public Slider progressSlider;

    [Header("Кнопки действий")]
    public Button startWashButton;

    [Header("Ссылка на стиральную машину")]
    public WashingMachine washingMachine;

    [Header("Ссылка на инвентарь")]
    public InventoryManager inventoryManager;

    [Header("Дебаг")]
    public bool debugMode = true;

    [Header("Слоты стиральной машины")]
    public slot[] washingSlot;

    [Header("Статус и информация")]
    public Text statusText;
    public Text loadedCountText;

    [Header("Таймер - ТОЛЬКО СЕКУНДЫ")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI simpleTimerText;

    private bool isWashing = false;

    void Start()
    {
        // ========== ИЗМЕНЕНИЕ: панель всегда включена ==========
        if (panel != null)
            panel.SetActive(true);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseUI); // можно оставить, но CloseUI пустой

        startWashButton?.onClick.AddListener(StartWashing);

        SetupModeToggles();
        FindManagers();

        if (timerText != null)
        {
            timerText.text = "Готово к стирке";
            timerText.color = Color.green;
        }

        if (simpleTimerText != null)
        {
            simpleTimerText.text = "0.0";
            simpleTimerText.color = Color.green;
        }
    }

    void UpdateModeInfo()
    {
        if (washingMachine == null) return;

        var settings = washingMachine.GetCurrentModeSettings();

        if (modeNameText != null)
            modeNameText.text = settings.displayName;

        if (modeDescriptionText != null)
            modeDescriptionText.text = settings.description;

        if (durationText != null)
            durationText.text = $"Длительность: {settings.duration} сек.";
    }

    public void UpdateTimerDisplay(float remainingTime)
    {
        if (timerText != null)
        {
            if (remainingTime > 0)
            {
                timerText.text = $"Осталось: {remainingTime:F1} сек";
                timerText.color = Color.yellow;
            }
            else
            {
                timerText.text = "Готово!";
                timerText.color = Color.green;
            }
        }

        if (simpleTimerText != null)
        {
            if (remainingTime > 0)
            {
                simpleTimerText.text = $"{remainingTime:F1}";
                simpleTimerText.color = Color.yellow;
                if (remainingTime < 3f)
                {
                    simpleTimerText.color = Color.red;
                }
            }
            else
            {
                simpleTimerText.text = "0.0";
                simpleTimerText.color = Color.green;
            }
        }
    }

    void FindManagers()
    {
        if (washingMachine == null) washingMachine = FindObjectOfType<WashingMachine>();
        if (inventoryManager == null) inventoryManager = FindObjectOfType<InventoryManager>();
    }

    // ========== ИЗМЕНЕНИЕ: методы управления видимостью теперь пустые ==========
    public void ToggleMenu() { }
    public void OpenUI() { }
    public void CloseUI() { }

    public void SetProgress(float value)
    {
        if (progressSlider != null)
            progressSlider.value = value;
    }

    public void UpdateUI(string status = "Idle")
    {
        if (panel != null && !panel.activeSelf)
        {
            panel.SetActive(true);
        }

        UpdateUIPublic();

        if (statusText != null)
        {
            statusText.text = $"Статус: {status}";
        }
    }

    public void SetStatus(string status, Color color)
    {
        if (statusText != null)
        {
            statusText.text = status;
            statusText.color = color;
        }

        if (status == "Стирка..." && timerText != null)
        {
            timerText.color = Color.yellow;
        }
        else if (status == "Готово!" && timerText != null)
        {
            timerText.text = "Готово!";
            timerText.color = Color.green;
        }
    }

    public void SetStatus(string status)
    {
        SetStatus(status, Color.white);
    }

    public void UpdateUIPublic()
    {
        if (washingMachine == null) return;

        if (progressSlider != null)
            progressSlider.value = washingMachine.GetProgressPercentage() / 100f;

        if (washingMachine.isWashing)
        {
            float remaining = washingMachine.GetRemainingTime();
            UpdateTimerDisplay(remaining);
        }

        if (loadedCountText != null)
        {
            loadedCountText.text = $"Загружено: {washingMachine.GetLoadedCount()}/{washingMachine.maxCapacity}";
        }

        startWashButton.interactable =
            !washingMachine.isWashing &&
            HasClothesInSlots();

        if (statusText != null)
        {
            if (washingMachine.isWashing)
            {
                statusText.text = "СТИРКА...";
                statusText.color = Color.yellow;
            }
            else if (HasClothesInSlots())
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

        UpdateTogglesState();
    }

    bool HasClothesInSlots()
    {
        foreach (var slot in washingSlot)
        {
            if (!slot.isEmpty)
                return true;
        }
        return false;
    }

    void UpdateTogglesState()
    {
        bool isWashingActive = washingMachine != null && washingMachine.isWashing;

        if (coloredToggle != null)
            coloredToggle.interactable = !isWashingActive;

        if (delicateToggle != null)
            delicateToggle.interactable = !isWashingActive;

        if (quickToggle != null)
            quickToggle.interactable = !isWashingActive;
    }

    void StartWashing()
    {
        if (washingMachine == null) return;

        washingMachine.StartWashing();
        UpdateUIPublic();
    }

    void Update()
    {
        // ========== ИЗМЕНЕНИЕ: убрали проверку Escape ==========
        // if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        //     CloseUI();

        if (panel != null && panel.activeSelf && washingMachine != null)
        {
            if (washingMachine.isWashing)
            {
                float remainingTime = washingMachine.GetRemainingTime();
                UpdateTimerDisplay(remainingTime);
                float progress = washingMachine.GetProgressPercentage() / 100f;
                SetProgress(progress);
            }
            else
            {
                if (timerText != null)
                {
                    timerText.text = "Готово к стирке";
                    timerText.color = Color.green;
                }

                if (simpleTimerText != null)
                {
                    simpleTimerText.text = "0.0";
                    simpleTimerText.color = Color.green;
                }
            }

            UpdateUIPublic();
        }
    }

    public void UpdateModeDisplay(WashingMachine.WashModeSettings settings)
    {
        UpdateModeInfo();
        UpdateUIPublic();
    }

    void TakeClothes()
    {
        if (washingMachine == null || inventoryManager == null) return;

        List<WashingMachine.ClothesItem> toTake = new List<WashingMachine.ClothesItem>(washingMachine.slots);

        foreach (var item in toTake)
        {
            int emptySlot = inventoryManager.FindEmptySlot();
            if (emptySlot != -1)
                inventoryManager.ReturnItemToSlot(item.obj, emptySlot);
        }

        washingMachine.slots.Clear();
        isWashing = false;
        UpdateUIPublic();
    }

    void SetupModeToggles()
    {
        if (coloredToggle != null)
        {
            coloredToggle.isOn = false;
            coloredToggle.onValueChanged.RemoveAllListeners();
            coloredToggle.onValueChanged.AddListener(isOn => {
                if (isOn && washingMachine != null)
                {
                    washingMachine.SetMode(WashingMachine.WashMode.Colored);
                    UpdateModeInfo();
                }
            });
        }

        if (delicateToggle != null)
        {
            delicateToggle.isOn = false;
            delicateToggle.onValueChanged.RemoveAllListeners();
            delicateToggle.onValueChanged.AddListener(isOn => {
                if (isOn && washingMachine != null)
                {
                    washingMachine.SetMode(WashingMachine.WashMode.Delicate);
                    UpdateModeInfo();
                }
            });
        }

        if (quickToggle != null)
        {
            quickToggle.isOn = false;
            quickToggle.onValueChanged.RemoveAllListeners();
            quickToggle.onValueChanged.AddListener(isOn => {
                if (isOn && washingMachine != null)
                {
                    washingMachine.SetMode(WashingMachine.WashMode.Quick);
                    UpdateModeInfo();
                }
            });
        }

        if (coloredToggle != null)
        {
            coloredToggle.isOn = true;
            if (washingMachine != null)
            {
                washingMachine.SetMode(WashingMachine.WashMode.Colored);
                UpdateModeInfo();
            }
        }
    }
}