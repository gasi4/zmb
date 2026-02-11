using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI")]
    public Slider healthSlider;
    public Image damageFlash;

    [Header("Target")]
    public PlayerHealth playerHealth;

    [Header("Layout")]
    public bool autoPosition = true;

    void Awake()
    {
        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);

        if (damageFlash == null)
            damageFlash = GetComponentInChildren<Image>(true);

        if (autoPosition && healthSlider != null)
        {
            RectTransform rt = healthSlider.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }

        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.BindUI(healthSlider, damageFlash);
    }
}