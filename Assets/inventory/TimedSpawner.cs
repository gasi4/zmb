using UnityEngine;

public class TimedSpawner : MonoBehaviour
{
    [Header("Настройки спавна")]
    public GameObject itemPrefab;       // Префаб предмета, который должен появиться
    public Vector3 spawnPosition;        // Точка появления (координаты в мире)
    public float spawnDelay = 10f;       // Задержка перед спавном (секунд)

    private void Start()
    {
        // Запускаем спавн через указанное количество секунд
        Invoke(nameof(SpawnItem), spawnDelay);
    }

    private void SpawnItem()
    {
        if (itemPrefab == null)
        {
            Debug.LogError("itemPrefab не назначен! Перетащите префаб в поле скрипта.");
            return;
        }

        // Создаём объект из префаба в указанной позиции, без поворота (Quaternion.identity)
        GameObject newItem = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);

        // Можно дополнительно задать имя для отладки
        newItem.name = itemPrefab.name + " (spawned)";

        Debug.Log($"Предмет {newItem.name} заспавнен через {spawnDelay} секунд в точке {spawnPosition}");
    }
}