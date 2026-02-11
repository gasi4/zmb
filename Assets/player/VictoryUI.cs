using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class VictoryUI : MonoBehaviour
{
    private static VictoryUI instance;

    [Header("UI (назначь в инспекторе)")]
    public GameObject root;          // весь экран Victory (Panel/Overlay)
    public Image overlay;            // можно оставить красный/зелёный/любой
    public TMP_Text titleText;       // "победа" и т.п.
    public Button toMenuButton;      // кнопка "в меню"

    [Header("Тексты")]
    public string title = "победа";

    [Header("Переход")]
    public string menuSceneName = "MainMenu"; // позже настроишь

    void Awake()
    {
        instance = this;

        // Оверлей на задний план
        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        if (toMenuButton != null)
            toMenuButton.onClick.AddListener(GoToMenu);

        HideImmediate();
    }

    public static void Show()
    {
        if (instance == null)
            instance = FindObjectOfType<VictoryUI>(true);

        if (instance == null)
        {
            Debug.LogError("VictoryUI: не найден в сцене. Добавь Canvas/Panel и повесь на него VictoryUI.");
            return;
        }

        instance.ShowInternal();
    }

    void ShowInternal()
    {
        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        // Поднимаем канвас выше других UI
        Canvas c = (root != null ? root.GetComponentInParent<Canvas>(true) : GetComponentInParent<Canvas>(true));
        if (c != null)
        {
            c.overrideSorting = true;
            c.sortingOrder = 9999;
        }

        if (titleText != null)
            titleText.text = title;

        // Принудительно включаем TMP-тексты
        GameObject r = root != null ? root : gameObject;
        TMP_Text[] tmps = r.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmps)
        {
            if (t == null) continue;
            t.enabled = true;
            t.gameObject.SetActive(true);
            Color col = t.color;
            col.a = 1f;
            t.color = col;
            t.ForceMeshUpdate(true, true);
        }

        // Оверлей точно на задний план
        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        Canvas.ForceUpdateCanvases();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void HideImmediate()
    {
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    void GoToMenu()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(menuSceneName))
            SceneManager.LoadScene(menuSceneName);
        else
            SceneManager.LoadScene(0);

        HideImmediate();
    }
}