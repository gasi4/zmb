using UnityEngine;
using UnityEngine.UI;
public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI")]
    public Slider healthSlider;
    public Image damageFlash;

    [Header("Настройки")]
    public float zombieDamage = 10f;
    public float damageCooldown = 1f;

    private float lastDamageTime = 0f;

    void Start()
    {
        currentHealth = maxHealth;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Zombie"))
        {
            TakeDamage(zombieDamage);

            // Отталкиваем зомби
            ZombieCustomer zombie = other.GetComponent<ZombieCustomer>();
            if (zombie != null)
            {
                // Можно добавить отбрасывание
            }
        }
    }

    void TakeDamage(float damage)
    {
        if (Time.time - lastDamageTime < damageCooldown) return;

        currentHealth -= damage;
        lastDamageTime = Time.time;

        // Обновляем UI
        if (healthSlider != null)
            healthSlider.value = currentHealth;

        // Эффект повреждения
        if (damageFlash != null)
            StartCoroutine(FlashDamage());

        // Проверяем смерть
        if (currentHealth <= 0)
        {
            Die();
        }

        Debug.Log($"Игрок получил урон: {damage}. Здоровье: {currentHealth}");
    }

    System.Collections.IEnumerator FlashDamage()
    {
        damageFlash.color = new Color(1, 0, 0, 0.3f);
        yield return new WaitForSeconds(0.1f);
        damageFlash.color = Color.clear;
    }

    void Die()
    {
        Debug.Log("Игрок умер!");
        // Здесь: экран Game Over, перезапуск уровня и т.д.

        // Отключаем управление
        var playerController = GetComponent<FinalPlayerController>();
        if (playerController != null)
            playerController.SetInputEnabled(false);

        // Показываем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}