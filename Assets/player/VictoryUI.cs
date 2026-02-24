using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class VictoryUI : MonoBehaviour
{
    private static VictoryUI instance;

    [Header("UI (назначь в инспекторе)")]
    public GameObject root;
    public Image overlay;
    public TMP_Text titleText;
    public Button toMenuButton;

    [Header("Тексты")]
    public string title = "победа";

    [Header("Переход")]
    public string menuSceneName = "MainMenu";

    [Header("VR настройки")]
    [Tooltip("Расстояние панели от камеры (в метрах)")]
    public float distanceFromCamera = 2.0f;

    [Tooltip("Масштаб панели в мире")]
    public float worldScale = 0.002f;

    [Tooltip("Высота смещения относительно глаз (в метрах)")]
    public float heightOffset = 0f;

    [Tooltip("Если true — панель всегда поворачивается к игроку")]
    public bool facePlayer = true;

    private Canvas canvas;
    private Transform vrCamera;

    void Awake()
    {
        instance = this;

        canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null && root != null)
            canvas = root.GetComponentInParent<Canvas>(true);

        // ========== ПЕРЕВОДИМ CANVAS В WORLD SPACE ==========
        SetupWorldSpaceCanvas();

        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        if (toMenuButton != null)
            toMenuButton.onClick.AddListener(GoToMenu);

        HideImmediate();
    }

    void SetupWorldSpaceCanvas()
    {
        if (canvas == null) return;

        // Переключаем в World Space
        canvas.renderMode = RenderMode.WorldSpace;

        // Масштаб, чтобы UI не был гигантским
        canvas.transform.localScale = Vector3.one * worldScale;

        // Убираем привязку к камере (World Space сам по себе)
        canvas.worldCamera = null;

        // Добавляем TrackedDeviceGraphicRaycaster для XR взаимодействия
        // (чтобы кнопки работали с VR контроллерами)
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        // Убираем обычный GraphicRaycaster (конфликтует)
        GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
        if (gr != null)
            Destroy(gr);
    }

    /// <summary>
    /// Находит VR-камеру (Main Camera в XR Rig)
    /// </summary>
    Transform GetVRCamera()
    {
        if (vrCamera != null) return vrCamera;

        // Способ 1: Camera.main
        if (Camera.main != null)
        {
            vrCamera = Camera.main.transform;
            return vrCamera;
        }

        // Способ 2: ищем по тегу
        GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (camObj != null)
        {
            vrCamera = camObj.transform;
            return vrCamera;
        }

        Debug.LogWarning("VictoryUI: не найдена VR-камера!");
        return null;
    }

    /// <summary>
    /// Размещает панель перед VR-камерой
    /// </summary>
    void PositionInFrontOfPlayer()
    {
        Transform cam = GetVRCamera();
        if (cam == null) return;

        // Позиция: перед камерой на заданном расстоянии
        Vector3 forward = cam.forward;
        forward.y = 0f; // убираем наклон вверх/вниз, чтобы панель была на уровне глаз
        if (forward.sqrMagnitude < 0.001f)
            forward = cam.forward; // fallback если смотрим прямо вверх/вниз

        forward.Normalize();

        Vector3 position = cam.position
            + forward * distanceFromCamera
            + Vector3.up * heightOffset;

        // Ставим Canvas (не root, а сам Canvas)
        Transform target = canvas != null ? canvas.transform : (root != null ? root.transform : transform);

        target.position = position;

        // Поворачиваем лицом к игроку
        if (facePlayer)
        {
            target.rotation = Quaternion.LookRotation(target.position - cam.position, Vector3.up);
        }
    }

    // ===================== SHOW / HIDE =====================

    public static void Show()
    {
        if (instance == null)
            instance = FindObjectOfType<VictoryUI>(true);

        if (instance == null)
        {
            Debug.LogError("VictoryUI: не найден в сцене.");
            return;
        }

        instance.ShowInternal();
    }

    void ShowInternal()
    {
        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        // Поднимаем sorting
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;
        }

        if (titleText != null)
            titleText.text = title;

        // Включаем TMP-тексты
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

        if (overlay != null)
        {
            overlay.raycastTarget = false;
            overlay.transform.SetAsFirstSibling();
        }

        // ========== КЛЮЧЕВОЕ: ставим панель перед игроком ==========
        PositionInFrontOfPlayer();

        Canvas.ForceUpdateCanvases();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // Если панель видна и facePlayer — плавно поворачиваем к игроку
        bool isActive = root != null ? root.activeSelf : gameObject.activeSelf;
        if (!isActive || !facePlayer) return;

        Transform cam = GetVRCamera();
        if (cam == null) return;

        Transform target = canvas != null ? canvas.transform : transform;
        Quaternion lookRot = Quaternion.LookRotation(target.position - cam.position, Vector3.up);
        target.rotation = Quaternion.Slerp(target.rotation, lookRot, Time.unscaledDeltaTime * 5f);
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