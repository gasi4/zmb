using UnityEngine;
using UnityEngine.UI;

public class WashingMachineController : MonoBehaviour
{
    [Header("Кнопки")]
    public Button startButton;
    public Button loadButton;

    [Header("Тогглы режимов")]
    public Toggle toggleWhite;
    public Toggle toggleBlack;
    public Toggle toggleColored;
    public Toggle toggleDelicate;

    [Header("Слайдеры")]
    public Slider capacitySlider;
    public Slider progressSlider;

    void Start()
    {
        // Кнопка запуска
        startButton.onClick.AddListener(StartWashing);

        // Кнопка загрузки
        loadButton.onClick.AddListener(LoadLaundry);

        // Настройка тогглов
        toggleWhite.onValueChanged.AddListener((isOn) => {
            if (isOn) SetMode("Белое");
        });
        toggleBlack.onValueChanged.AddListener((isOn) => {
            if (isOn) SetMode("Черное");
        });
        toggleColored.onValueChanged.AddListener((isOn) => {
            if (isOn) SetMode("Цветное");
        });
        toggleDelicate.onValueChanged.AddListener((isOn) => {
            if (isOn) SetMode("Деликатное");
        });

        // Слайдер вместимости
        capacitySlider.onValueChanged.AddListener((value) => {
            Debug.Log($"Вместимость: {value} кг");
        });
    }

    void StartWashing()
    {
        Debug.Log("СТИРКА ЗАПУЩЕНА!");
        // Здесь логика запуска стирки
    }

    void LoadLaundry()
    {
        Debug.Log("Белье загружено!");
        // Здесь логика загрузки
    }

    void SetMode(string mode)
    {
        Debug.Log($"Выбран режим: {mode}");
        // Здесь логика выбора режима
    }
}