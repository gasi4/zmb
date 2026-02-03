using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro; // Добавляем using для TMPro

public class WashingMachineUI : MonoBehaviour
{
    [Header("Основные элементы")]
    public GameObject panel;
    public Button closeButton;

    [Header("Режимы стирки - ТЕПЕРЬ ТОЛЬКО 3")]
    public Toggle coloredToggle;      // Цветное белье
    public Toggle delicateToggle;     // Деликатное
    public Toggle quickToggle;        // Быстрая стирка

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
    public TextMeshProUGUI timerText; // Для отображения "Осталось: X.X сек"
    public TextMeshProUGUI simpleTimerText; // Альтернативный вариант: просто "X.X"

    private bool isWashing = false;

    void Start()
    {
        if (panel != null)
            panel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseUI);

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

    // ДОБАВЬ ЭТОТ МЕТОД - он отсутствовал
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
        // Вариант 1: С текстовым описанием
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

        // Вариант 2: Только число
        if (simpleTimerText != null)
        {
            if (remainingTime > 0)
            {
                simpleTimerText.text = $"{remainingTime:F1}";
                simpleTimerText.color = Color.yellow;

                // Можно добавить визуальную обратную связь при малом времени
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

    public void ToggleMenu()
    {
        if (panel == null) { Debug.LogError("❌ PANEL NULL"); return; }

        bool state = !panel.activeSelf;
        panel.SetActive(state);

        if (state)
        {
            UpdateUI();
            UpdateModeInfo(); // Теперь этот метод существует
        }

        Debug.Log("🖼 PANEL ACTIVE = " + panel.activeSelf);
    }

    public void OpenUI()
    {
        if (panel == null) return;

        panel.SetActive(true);
        UpdateUI();
        UpdateModeInfo(); // Теперь этот метод существует

        // Включаем курсор для клика
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Блокируем движение игрока
        var player = FindObjectOfType<FinalPlayerController>();
        if (player != null)
        {
            player.SetInputEnabled(false);
            if (player.unifiedRay != null)
                player.unifiedRay.enabled = false;
        }

        Debug.Log("🧺 UI открыт");
    }

    public void CloseUI()
    {
        if (panel == null) return;

        panel.SetActive(false);

        // Включаем курсор обратно
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var player = FindObjectOfType<FinalPlayerController>();
        if (player != null)
        {
            player.SetInputEnabled(true);
            if (player.unifiedRay != null)
                player.unifiedRay.enabled = true;
        }

        Debug.Log("❌ UI закрыт");
    }

    // ДОБАВЬ ЭТОТ МЕТОД для установки прогресса
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

        // Также обновляем таймер в зависимости от статуса
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

        // Обновляем прогресс
        if (progressSlider != null)
            progressSlider.value = washingMachine.GetProgressPercentage() / 100f;

        // Обновляем таймер
        if (washingMachine.isWashing)
        {
            float remaining = washingMachine.GetRemainingTime();
            UpdateTimerDisplay(remaining);
        }

        // Количество загруженных вещей
        if (loadedCountText != null)
        {
            loadedCountText.text = $"Загружено: {washingMachine.GetLoadedCount()}/{washingMachine.maxCapacity}";
        }

        // Кнопка старта
        startWashButton.interactable =
            !washingMachine.isWashing &&
            HasClothesInSlots();

        // Обновляем статус
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

        // Обновляем состояние тогглов
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

        // Делаем тогглы неактивными во время стирки
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
        // Обновляем UI, если панель открыта
        if (panel != null && panel.activeSelf && washingMachine != null)
        {
            if (washingMachine.isWashing)
            {
                float remainingTime = washingMachine.GetRemainingTime();
                UpdateTimerDisplay(remainingTime);

                // Обновляем прогресс бар
                float progress = washingMachine.GetProgressPercentage() / 100f;
                SetProgress(progress);
            }
            else
            {
                // Если стирка не идет, показываем готовность
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

            // Обновляем остальной UI
            UpdateUIPublic();
        }
    }

    // Метод для обновления отображения режима (вызывается из WashingMachine)
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
        // Убедиться, что все тогглы выключены сначала
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

        // Устанавливаем режим по умолчанию
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