using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    private static GameOverUI instance;

    [Header("UI (назначь в инспекторе)")]
    public GameObject root;          // весь экран GameOver (Panel/Overlay)
    public Image overlay;            // красная полупрозрачная картинка
    public TMP_Text titleText;       // "вас убили"
    public Button tryAgainButton;    // кнопка Try Again

    [Header("Тексты")]
    public string title = "вас убили";

    void Awake()
    {
        instance = this;

        // Оверлей должен быть на заднем плане, иначе он может перекрыть TMP-тексты/кнопки
        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(Restart);

        HideImmediate();
    }

    public static void Show()
    {
        if (instance == null)
            instance = FindObjectOfType<GameOverUI>(true);

        if (instance == null)
        {
            Debug.LogError("GameOverUI: не найден в сцене. Добавь Canvas/Panel и повесь на него GameOverUI.");
            return;
        }

        instance.ShowInternal();
    }

    void ShowInternal()
    {
        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        // Поднимаем канвас меню выше других UI (на случай нескольких Canvas в сцене)
        Canvas c = (root != null ? root.GetComponentInParent<Canvas>(true) : GetComponentInParent<Canvas>(true));
        if (c != null)
        {
            c.overrideSorting = true;
            c.sortingOrder = 9999;
        }

        if (titleText != null)
            titleText.text = title;

        // На всякий случай принудительно включаем все TMP-тексты в этом меню
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

        // Оверлей точно на задний план внутри root
        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        // Обновляем канвасы после включения root, чтобы TMP/лейаут точно пересчитались
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

    void Restart()
    {
        Time.timeScale = 1f;

        int idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx);

        HideImmediate();
    }
}