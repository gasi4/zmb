using UnityEngine;
using UnityEngine.UI;
public class PlayerHealth : MonoBehaviour
{
    private bool isDead = false;

    [Header("��������")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI")]
    public Slider healthSlider;
    public Image damageFlash;

    void UpdateHealthUI()
    {
        if (healthSlider == null) return;
        healthSlider.minValue = 0f;
        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
    }

    public void BindUI(Slider slider, Image flash)
    {
        healthSlider = slider;
        damageFlash = flash;

        UpdateHealthUI();

        if (damageFlash != null)
            damageFlash.color = Color.clear;
    }

    [Header("���������")]
    public float zombieDamage = 10f;
    public float damageCooldown = 1f;

    private float lastDamageTime = -999f;

    void Start()
    {
        currentHealth = maxHealth;

        UpdateHealthUI();

        if (damageFlash != null)
            damageFlash.color = Color.clear;
    }

    void OnTriggerStay(Collider other)
    {
        // ВАЖНО: урон от зомби теперь считается в ZombieCustomer.TryAttack() по дистанции.
        // Этот триггер оставлял эффект "нужно включить IsTrigger/поменять высоту, чтобы били",
        // и мог давать двойной урон. Отключаем.
        return;
        if (other == null) return;

        // Для XR/разных префабов зомби тег может стоять не на корневом коллайдере.
        // Поэтому проверяем ZombieCustomer в родителях, а тег делаем опциональным.
        if (!(other.CompareTag("Zombie") || other.GetComponentInParent<ZombieCustomer>() != null))
            return;

        // Урон только от агрессивного зомби
        ZombieCustomer zombie = other.GetComponentInParent<ZombieCustomer>();
        if (zombie == null || zombie.currentState != ZombieCustomer.ZombieState.Angry)
            return;

        // Наносим урон постоянно при контакте (ограничение по damageCooldown внутри TakeDamage)
        TakeDamage(zombieDamage);
    }

    public void TakeDamage(float damage)
    {
        if (Time.time - lastDamageTime < damageCooldown) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        lastDamageTime = Time.time;

        UpdateHealthUI();

        if (healthSlider == null)
            Debug.LogWarning("PlayerHealth: healthSlider не привязан — HP уменьшается, но UI не обновится");

        // ������ �����������
        if (damageFlash != null)
            StartCoroutine(FlashDamage());

        // ��������� ������
        if (currentHealth <= 0)
        {
            Die();
        }

        Debug.Log($"����� ������� ����: {damage}. ��������: {currentHealth}");
    }

    System.Collections.IEnumerator FlashDamage()
    {
        damageFlash.color = new Color(1, 0, 0, 0.3f);
        yield return new WaitForSeconds(0.1f);
        damageFlash.color = Color.clear;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // Отключаем управление
        var playerController = GetComponent<FinalPlayerController>();
        if (playerController != null)
            playerController.SetInputEnabled(false);

        // Экран поражения
        GameOverUI.Show();
    }
}