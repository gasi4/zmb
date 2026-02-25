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

        if (titleText != null)
            titleText.text = title;

        // ===== VR: размещаем канвас перед лицом игрока =====
        PositionInFrontOfPlayer();

        // Оверлей на задний план
        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        Canvas.ForceUpdateCanvases();

        // В VR НЕ ставим Time.timeScale = 0, 
        // иначе XR rig перестанет трекать голову!
        // Time.timeScale = 0f;  // ← УБЕРИ ЭТО В VR

        Debug.Log("GameOverUI: показан!");
    }

    void PositionInFrontOfPlayer()
    {
        // Ищем VR-камеру
        Camera vrCam = Camera.main;
        if (vrCam == null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Ставим Canvas в World Space (на случай если забыл в инспекторе)
        canvas.renderMode = RenderMode.WorldSpace;

        // Размещаем перед глазами
        Transform canvasTransform = canvas.transform;
        float distanceFromFace = 2f; // метры перед игроком

        canvasTransform.position = vrCam.transform.position
                                  + vrCam.transform.forward * distanceFromFace;
        canvasTransform.rotation = Quaternion.LookRotation(
            canvasTransform.position - vrCam.transform.position
        );

        // Масштаб для World Space (чтобы не был гигантским)
        canvasTransform.localScale = Vector3.one * 0.002f;
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