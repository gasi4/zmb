using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WashingMachineUI : MonoBehaviour
{
    [Header("Основные элементы")]
    public GameObject panel;

    [Header("Режимы стирки")]
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

    [Header("Ссылка на стиральную машину (основной скрипт)")]
    public WashingMachineWithInventory washingMachine;

    [Header("Слоты стиральной машины")]
    public slot[] washingSlot;

    [Header("Статус и информация")]
    public Text statusText;
    public Text loadedCountText;

    [Header("Таймер")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI simpleTimerText;

    void Start()
    {
        if (washingMachine == null)
            washingMachine = FindObjectOfType<WashingMachineWithInventory>();

        SetupModeToggles();

        if (startWashButton != null)
            startWashButton.onClick.AddListener(() => washingMachine?.StartWashingProcess());

        UpdateUI();
    }

    void SetupModeToggles()
    {
        coloredToggle.onValueChanged.AddListener(isOn => { if (isOn) washingMachine?.SetMode(WashingMachineWithInventory.WashMode.Colored); });
        delicateToggle.onValueChanged.AddListener(isOn => { if (isOn) washingMachine?.SetMode(WashingMachineWithInventory.WashMode.Delicate); });
        quickToggle.onValueChanged.AddListener(isOn => { if (isOn) washingMachine?.SetMode(WashingMachineWithInventory.WashMode.Quick); });
    }

    void Update()
    {
        if (washingMachine == null) return;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (washingMachine == null) return;

        var settings = washingMachine.GetCurrentModeSettingsPublic();
        if (modeNameText != null) modeNameText.text = settings.displayName;
        if (modeDescriptionText != null) modeDescriptionText.text = settings.description;
        if (durationText != null) durationText.text = $"Длительность: {settings.duration} сек.";

        if (progressSlider != null)
            progressSlider.value = washingMachine.GetProgressPercentage() / 100f;

        if (washingMachine.isWashing)
        {
            float remaining = washingMachine.GetRemainingTime();
            if (timerText != null) timerText.text = $"Осталось: {remaining:F1} сек.";
            if (simpleTimerText != null) simpleTimerText.text = $"{remaining:F1}";
        }
        else
        {
            if (timerText != null) timerText.text = "Готово к стирке";
            if (simpleTimerText != null) simpleTimerText.text = "0.0";
        }

        if (loadedCountText != null)
            loadedCountText.text = $"Загружено: {washingMachine.GetLoadedCount()}/{washingMachine.MaxCapacity}";

        if (statusText != null)
        {
            if (washingMachine.isWashing)
                statusText.text = "СТИРКА...";
            else if (washingMachine.GetLoadedCount() > 0)
                statusText.text = "ГОТОВО К СТИРКЕ";
            else
                statusText.text = "ПУСТО";
        }

        if (startWashButton != null)
            startWashButton.interactable = !washingMachine.isWashing && washingMachine.GetLoadedCount() > 0;
    }

    // Публичные методы, вызываемые из WashingMachineWithInventory
    public void UpdateUIPublic() => UpdateUI();

    public void SetStatus(string status)
    {
        // Можно обновить статус, но UpdateUI уже делает это
        UpdateUI();
    }
    public void UpdateModeDisplay(WashingMachine.WashModeSettings settings)
    {
        // Преобразуем enum, предполагая, что значения совпадают
        var newSettings = new WashingMachineWithInventory.WashModeSettings
        {
            mode = (WashingMachineWithInventory.WashMode)(int)settings.mode,
            displayName = settings.displayName,
            duration = settings.duration,
            description = settings.description
        };
        // Вызываем существующий метод с правильным типом
        UpdateModeDisplay(newSettings);
    }
    public void UpdateModeDisplay(WashingMachineWithInventory.WashModeSettings settings)
    {
        UpdateUI();
    }
}