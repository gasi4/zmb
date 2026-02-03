using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ZombieUI : MonoBehaviour
{
    [Header("UI элементы")]
    public Transform uiContainer; // Контейнер для UI элементов зомби
    public GameObject customerUIPrefab; // Префаб UI одного зомби

    [Header("Ссылки")]
    public ZombieSpawnManager spawnManager;

    private Dictionary<ZombieCustomer, GameObject> zombieUIElements = new Dictionary<ZombieCustomer, GameObject>();

    void Start()
    {
        // Проверка настройки
        if (uiContainer == null)
        {
            Debug.LogError("UI Container не назначен в ZombieUI!");
            enabled = false;
            return;
        }

        if (customerUIPrefab == null)
        {
            Debug.LogError("Customer UI Prefab не назначен в ZombieUI!");
            enabled = false;
            return;
        }

        // Скрываем префаб если он активен
        customerUIPrefab.SetActive(false);
    }

    void Update()
    {
        if (spawnManager == null)
        {
            // Попробуем найти автоматически
            spawnManager = FindObjectOfType<ZombieSpawnManager>();
            if (spawnManager == null) return;
        }

        var activeZombies = spawnManager.GetActiveZombies();

        // Обновляем UI для каждого зомби
        foreach (var zombie in activeZombies)
        {
            if (zombie != null && !zombieUIElements.ContainsKey(zombie))
            {
                AddZombieUI(zombie);
            }
        }

        // Удаляем UI для уничтоженных зомби
        List<ZombieCustomer> toRemove = new List<ZombieCustomer>();
        foreach (var kvp in zombieUIElements)
        {
            if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy)
                toRemove.Add(kvp.Key);
        }

        foreach (var zombie in toRemove)
        {
            RemoveZombieUI(zombie);
        }
    }

    void AddZombieUI(ZombieCustomer zombie)
    {
        if (customerUIPrefab == null || uiContainer == null)
        {
            Debug.LogWarning("Не могу создать UI: префаб или контейнер не назначен");
            return;
        }

        // Создаем UI элемент
        GameObject uiElement = Instantiate(customerUIPrefab, uiContainer);
        uiElement.name = "ZombieUI_" + zombie.name;
        uiElement.SetActive(true); // Активируем

        // Настраиваем UI
        Slider patienceSlider = uiElement.GetComponentInChildren<Slider>();
        if (patienceSlider != null)
        {
            zombie.patienceSlider = patienceSlider;

            // Устанавливаем значения
            patienceSlider.maxValue = zombie.waitTime;
            patienceSlider.value = zombie.waitTime;

            // Находим fill для изменения цвета
            Image fillImage = patienceSlider.fillRect.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = Color.green;
        }

        // Настраиваем текст
        TextMeshProUGUI nameText = uiElement.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = $"Зомби {zombieUIElements.Count + 1}";
        }

        zombieUIElements[zombie] = uiElement;

        Debug.Log($"Создан UI для зомби: {zombie.name}");
    }

    void RemoveZombieUI(ZombieCustomer zombie)
    {
        if (zombieUIElements.ContainsKey(zombie))
        {
            Destroy(zombieUIElements[zombie]);
            zombieUIElements.Remove(zombie);
            Debug.Log($"Удален UI для зомби: {zombie.name}");
        }
    }
}