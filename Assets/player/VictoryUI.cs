using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class VictoryUI : MonoBehaviour
{
    private static VictoryUI instance;

    [Header("UI (������� � ����������)")]
    public GameObject root;
    public Image overlay;
    public TMP_Text titleText;
    public Button toMenuButton;

    [Header("������")]
    public string title = "������";

    [Header("�������")]
    public string menuSceneName = "MainMenu";

    [Header("VR ���������")]
    [Tooltip("���������� ������ �� ������ (� ������)")]
    public float distanceFromCamera = 2.0f;

    [Tooltip("������� ������ � ����")]
    public float worldScale = 0.002f;

    [Tooltip("������ �������� ������������ ���� (� ������)")]
    public float heightOffset = 0f;

    [Tooltip("���� true � ������ ������ �������������� � ������")]
    public bool facePlayer = true;

    private Canvas canvas;
    private Transform vrCamera;

    void Awake()
    {
        instance = this;

        canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null && root != null)
            canvas = root.GetComponentInParent<Canvas>(true);

        // ========== ��������� CANVAS � WORLD SPACE ==========
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

        // ����������� � World Space
        canvas.renderMode = RenderMode.WorldSpace;

        // �������, ����� UI �� ��� ����������
        canvas.transform.localScale = Vector3.one * worldScale;

        // ������� �������� � ������ (World Space ��� �� ����)
        canvas.worldCamera = null;

        // ��������� TrackedDeviceGraphicRaycaster ��� XR ��������������
        // (����� ������ �������� � VR �������������)
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        // ������� ������� GraphicRaycaster (�����������)
        GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
        if (gr != null)
            Destroy(gr);
    }

    /// <summary>
    /// ������� VR-������ (Main Camera � XR Rig)
    /// </summary>
    Transform GetVRCamera()
    {
        if (vrCamera != null) return vrCamera;

        // ������ 1: Camera.main
        if (Camera.main != null)
        {
            vrCamera = Camera.main.transform;
            return vrCamera;
        }

        // ������ 2: ���� �� ����
        GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (camObj != null)
        {
            vrCamera = camObj.transform;
            return vrCamera;
        }

        Debug.LogWarning("VictoryUI: �� ������� VR-������!");
        return null;
    }

    /// <summary>
    /// ��������� ������ ����� VR-�������
    /// </summary>
    void PositionInFrontOfPlayer()
    {
        Transform cam = GetVRCamera();
        if (cam == null) return;

        // �������: ����� ������� �� �������� ����������
        Vector3 forward = cam.forward;
        forward.y = 0f; // ������� ������ �����/����, ����� ������ ���� �� ������ ����
        if (forward.sqrMagnitude < 0.001f)
            forward = cam.forward; // fallback ���� ������� ����� �����/����

        forward.Normalize();

        Vector3 position = cam.position
            + forward * distanceFromCamera
            + Vector3.up * heightOffset;

        // ������ Canvas (�� root, � ��� Canvas)
        Transform target = canvas != null ? canvas.transform : (root != null ? root.transform : transform);

        target.position = position;

        // ������������ ����� � ������
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
            Debug.LogError("VictoryUI: �� ������ � �����.");
            return;
        }

        instance.ShowInternal();
    }

    void ShowInternal()
    {
        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        // ��������� sorting
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;
        }

        if (titleText != null)
            titleText.text = title;

        // �������� TMP-������
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

        // ========== ��������: ������ ������ ����� ������� ==========
        PositionInFrontOfPlayer();

        Canvas.ForceUpdateCanvases();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // ���� ������ ����� � facePlayer � ������ ������������ � ������
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