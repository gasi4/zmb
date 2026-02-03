using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class UnifiedRay : MonoBehaviour
{
    [Header("VR Settings")]
    public bool vrModeActive = true; // По умолчанию true для XR
    public Transform rightHandTransform;
    public float maxDistance = 10f;
    public LayerMask layerMask = ~0;

    [Header("Editor Settings")]
    public Transform editorCameraTransform;

    [Header("Visualization")]
    public bool showLine = true;
    public Color defaultColor = Color.red;
    public Color hitColor = Color.green;
    public float lineWidth = 0.1f; // Уменьшил, так как LineRenderer странно работает

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        // НАСТРОЙКИ ДЛЯ БОЛЕЕ ТОЛСТОГО ЛУЧА
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = showLine;
        lineRenderer.useWorldSpace = true;

        // МЕНЯЕМ ШЕЙДЕР И НАСТРОЙКИ МАТЕРИАЛА
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = defaultColor;

        // ДОПОЛНИТЕЛЬНЫЕ НАСТРОЙКИ ДЛЯ ВИДИМОСТИ
        lineRenderer.numCapVertices = 8; // Сглаженные концы
        lineRenderer.numCornerVertices = 8; // Сглаженные углы
    }

    void Start()
    {
        // Принудительно обновляем луч при старте
        UpdateRay();
    }

    void Update()
    {
        if (!showLine)
        {
            if (lineRenderer.enabled)
                lineRenderer.enabled = false;
            return;
        }

        if (!lineRenderer.enabled)
            lineRenderer.enabled = true;

        UpdateRay();
    }

    void UpdateRay()
    {
        Vector3 origin = GetRayOrigin();
        Vector3 direction = GetRayDirection();

        Vector3 endPos = origin + direction * maxDistance;
        Color color = defaultColor;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Collide))
        {
            endPos = hit.point;
            if (hit.collider.GetComponent<Rigidbody>() != null ||
                hit.collider.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>() != null)
                color = hitColor;
        }

        DrawLine(origin, endPos, color);
    }

    Vector3 GetRayOrigin()
    {
        if (vrModeActive && rightHandTransform != null)
            return rightHandTransform.position;

        if (!vrModeActive && editorCameraTransform != null)
            return editorCameraTransform.position;

        return transform.position;
    }

    Vector3 GetRayDirection()
    {
        if (vrModeActive && rightHandTransform != null)
            return rightHandTransform.forward;

        if (!vrModeActive && editorCameraTransform != null)
            return editorCameraTransform.forward;

        return transform.forward;
    }

    void DrawLine(Vector3 from, Vector3 to, Color color)
    {
        if (lineRenderer == null) return;

        lineRenderer.SetPosition(0, from);
        lineRenderer.SetPosition(1, to);
        lineRenderer.material.color = color;
    }

    public bool Raycast(out RaycastHit hit, out Ray rayOut)
    {
        Vector3 origin = GetRayOrigin();
        Vector3 direction = GetRayDirection();
        rayOut = new Ray(origin, direction);
        return Physics.Raycast(origin, direction, out hit, maxDistance, layerMask, QueryTriggerInteraction.Collide);
    }
}