using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ZombieUI : MonoBehaviour
{
    [Header("UI ��������")]
    public Transform uiContainer; // ��������� ��� UI ��������� �����
    public GameObject customerUIPrefab; // ������ UI ������ �����

    [Header("������")]
    public ZombieSpawnManager spawnManager;

    [Header("Привязка UI к миру")]
    public Camera targetCamera;
    [Tooltip("Доп.смещение над головой (в метрах)")]
    public float extraHeadOffset = 0.6f;
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Tooltip("Смещение UI в пикселях (X вправо, Y вверх)")]
    public Vector2 screenOffset = new Vector2(0f, 80f);

    private Dictionary<ZombieCustomer, GameObject> zombieUIElements = new Dictionary<ZombieCustomer, GameObject>();

    void Start()
    {
        // �������� ���������
        if (uiContainer == null)
        {
            Debug.LogError("UI Container �� �������� � ZombieUI!");
            enabled = false;
            return;
        }

        if (customerUIPrefab == null)
        {
            Debug.LogError("Customer UI Prefab �� �������� � ZombieUI!");
            enabled = false;
            return;
        }

        // �������� ������ ���� �� �������
        customerUIPrefab.SetActive(false);
    }

    void Update()
    {
        if (spawnManager == null)
        {
            // ��������� ����� �������������
            spawnManager = FindObjectOfType<ZombieSpawnManager>();
            if (spawnManager == null) return;
        }

        var activeZombies = spawnManager.GetActiveZombies();

        // ��������� UI ��� ������� �����
        foreach (var zombie in activeZombies)
        {
            if (zombie != null && !zombieUIElements.ContainsKey(zombie))
            {
                AddZombieUI(zombie);
            }
        }

        // ������� UI ��� ������������ �����
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

        UpdateZombieUIPositions();
    }

    void AddZombieUI(ZombieCustomer zombie)
    {
        if (customerUIPrefab == null || uiContainer == null)
        {
            Debug.LogWarning("�� ���� ������� UI: ������ ��� ��������� �� ��������");
            return;
        }

        // Создаём UI сразу под корневым Canvas (координаты anchoredPosition считаются относительно родителя)
        Canvas canvas = uiContainer != null ? uiContainer.GetComponentInParent<Canvas>() : null;
        Transform parentTf = canvas != null ? canvas.transform : uiContainer;

        GameObject uiElement = Instantiate(customerUIPrefab, parentTf);
        uiElement.name = "ZombieUI_" + zombie.name;
        uiElement.SetActive(true); // ����������

        // Частая причина "UI создан, но не видно": RectTransform улетает за экран / scale=0 / alpha=0.
        // Принудительно нормализуем трансформ и укладываем элементы списком вниз.
        RectTransform rt = uiElement.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Если у префаба якоря Stretch (0..1), элемент растягивается на весь экран.
            // Принудительно делаем "точечные" якоря, чтобы отображался только сам слайдер/панель.
            Vector2 savedSize = rt.sizeDelta;

            rt.localScale = Vector3.one;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = savedSize;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Позицию дальше будет ставить UpdateZombieUIPositions() над зомби
        }

        // Если uiContainer использует LayoutGroup, он может переопределять anchoredPosition,
        // из-за этого UI "залипает" и перестает следовать за зомби.
        LayoutElement le = uiElement.GetComponent<LayoutElement>();
        if (le == null) le = uiElement.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        CanvasGroup cg = uiElement.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        // ВАЖНО: если внутри CustomerUIPrefab дочерняя Panel выключена, обычный GetComponentInChildren её не увидит.
        // Поэтому включаем Panel и ищем компоненты с includeInactive=true.
        Transform panelTf = uiElement.transform.Find("Panel");
        if (panelTf != null)
            panelTf.gameObject.SetActive(true);

        // ����������� UI
        Slider patienceSlider = uiElement.GetComponentInChildren<Slider>(true);
        if (patienceSlider != null)
        {
            zombie.patienceSlider = patienceSlider;

            // Диапазон строго из ZombieCustomer.waitTime
            patienceSlider.minValue = 0f;
            patienceSlider.maxValue = zombie.waitTime;
            patienceSlider.value = zombie.waitTime;

            // ������� fill ��� ��������� �����
            Image fillImage = patienceSlider.fillRect != null ? patienceSlider.fillRect.GetComponent<Image>() : null;
            if (fillImage != null)
                fillImage.color = Color.green;
        }
        else
        {
            Debug.LogWarning($"ZombieUI: не найден Slider в CustomerUIPrefab для {zombie.name}");
        }

        // ����������� �����
        TextMeshProUGUI nameText = uiElement.GetComponentInChildren<TextMeshProUGUI>(true);
        if (nameText != null)
        {
            nameText.text = $"����� {zombieUIElements.Count + 1}";
        }

        zombieUIElements[zombie] = uiElement;

        Debug.Log($"������ UI ��� �����: {zombie.name}");
    }

    void UpdateZombieUIPositions()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null) return;

        // Позиционируем относительно корневого Canvas, а не uiContainer.
        // Иначе если uiContainer маленький/не на весь экран — localPoint будет "мимо".
        Canvas canvas = uiContainer != null ? uiContainer.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;

        RectTransform canvasRt = canvas.transform as RectTransform;
        if (canvasRt == null) return;

        Camera uiCam = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCam = canvas.worldCamera != null ? canvas.worldCamera : targetCamera;

        foreach (var kvp in zombieUIElements)
        {
            ZombieCustomer zombie = kvp.Key;
            GameObject uiGo = kvp.Value;
            if (zombie == null || uiGo == null) continue;

            RectTransform uiRt = uiGo.GetComponent<RectTransform>();
            if (uiRt == null) continue;

            // Ставим UI над головой: сначала пытаемся найти реальную "голову" (кость/трансформ), иначе fallback через bounds.
            // ВАЖНО: extraHeadOffset добавляем ВСЕГДА поверх worldOffset, чтобы он реально влиял на позицию.
            Vector3 worldPos = zombie.transform.position + worldOffset + Vector3.up * extraHeadOffset;

            Transform headTf = null;
            Animator anim = zombie.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                headTf = anim.GetBoneTransform(HumanBodyBones.Head);
            }

            if (headTf == null)
            {
                // ВАЖНО: Transform.Find ищет только прямых детей, поэтому ищем рекурсивно
                headTf = FindDeepChild(zombie.transform, "Head") ??
                         FindDeepChild(zombie.transform, "head") ??
                         FindDeepChild(zombie.transform, "mixamorig:Head") ??
                         FindDeepChild(zombie.transform, "Bip001 Head");
            }

            if (headTf != null)
            {
                worldPos = headTf.position + Vector3.up * extraHeadOffset;
            }
            else
            {
                Renderer r = zombie.GetComponentInChildren<Renderer>();
                if (r != null)
                    worldPos = r.bounds.max + Vector3.up * extraHeadOffset;
            }

            Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);

            // Если за камерой — скрываем
            if (screenPos.z <= 0f)
            {
                uiGo.SetActive(false);
                continue;
            }
            else if (!uiGo.activeSelf)
            {
                uiGo.SetActive(true);
            }

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, uiCam, out localPoint))
            {
                uiRt.anchoredPosition = localPoint + screenOffset;
            }

            // Если по какой-то причине элемент остался под uiContainer, то переведём позицию в локальные координаты родителя
            if (uiRt.parent != null && uiRt.parent != canvasRt)
            {
                RectTransform parentRt = uiRt.parent as RectTransform;
                if (parentRt != null)
                {
                    Vector2 parentLocal;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screenPos, uiCam, out parentLocal))
                        uiRt.anchoredPosition = parentLocal + screenOffset;
                }
            }
        }
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    void RemoveZombieUI(ZombieCustomer zombie)
    {
        if (zombieUIElements.ContainsKey(zombie))
        {
            Destroy(zombieUIElements[zombie]);
            zombieUIElements.Remove(zombie);
            Debug.Log("Удален UI зомби");
        }
    }
}