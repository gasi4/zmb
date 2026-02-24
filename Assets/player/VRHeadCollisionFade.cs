using UnityEngine;

public class VRHeadCollisionFade : MonoBehaviour
{
    [Header("Камера VR")]
    public Transform headCamera;

    [Header("Детекция стен")]
    public float checkRadius = 0.15f;

    [Tooltip("ОБЯЗАТЕЛЬНО выбери ТОЛЬКО слой стен, НЕ всё подряд")]
    public LayerMask wallLayers;  // ← БЕЗ значения по умолчанию, чтобы не забыть назначить

    [Header("Визуал")]
    public Color fadeColor = Color.black;
    public float fadeInSpeed = 8f;
    public float fadeOutSpeed = 5f;
    [Range(0f, 1f)] public float maxAlpha = 0.95f;
    public float sphereScale = 0.4f;

    private GameObject fadeSphere;
    private Material fadeMaterial;
    private MeshRenderer fadeRenderer;
    private float currentAlpha = 0f;
    private CharacterController playerCC;

    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    void Start()
    {
        if (headCamera == null && Camera.main != null)
            headCamera = Camera.main.transform;

        // Находим CharacterController игрока чтобы игнорировать его
        playerCC = GetComponentInParent<CharacterController>();

        if (wallLayers == 0)
            Debug.LogError("VRHeadCollisionFade: wallLayers не назначен! Назначь слой стен в инспекторе.");

        CreateFadeSphere();
    }

    void CreateFadeSphere()
    {
        fadeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fadeSphere.name = "VR_FadeSphere";

        // Убираем коллайдер сферы
        Collider col = fadeSphere.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Привязываем к камере
        fadeSphere.transform.SetParent(headCamera, false);
        fadeSphere.transform.localPosition = Vector3.zero;
        fadeSphere.transform.localRotation = Quaternion.identity;
        fadeSphere.transform.localScale = Vector3.one * sphereScale;

        // Материал
        Shader fadeShader = Shader.Find("VR/FadeOverlay");
        if (fadeShader == null)
        {
            fadeShader = Shader.Find("Unlit/Color");
            Debug.LogWarning("VRHeadCollisionFade: шейдер VR/FadeOverlay не найден, фоллбэк на Unlit/Color");
        }

        fadeMaterial = new Material(fadeShader);
        fadeMaterial.SetColor(ColorID, fadeColor);
        fadeMaterial.SetFloat(AlphaID, 0f);

        fadeRenderer = fadeSphere.GetComponent<MeshRenderer>();
        fadeRenderer.material = fadeMaterial;
        fadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fadeRenderer.receiveShadows = false;

        // ВАЖНО: ставим на слой камеры
        fadeSphere.layer = headCamera.gameObject.layer;

        fadeRenderer.enabled = false;
    }

    void Update()
    {
        if (headCamera == null || fadeMaterial == null) return;
        if (wallLayers == 0) return; // не назначен слой — не работаем

        bool insideWall = false;

        // Проверяем ТОЛЬКО стены
        Collider[] hits = Physics.OverlapSphere(
            headCamera.position,
            checkRadius,
            wallLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.isTrigger) continue;

            // Игнорируем собственный CharacterController
            if (playerCC != null && hit == playerCC) continue;

            // Игнорируем всё что является частью игрока
            if (hit.transform.IsChildOf(headCamera.root)) continue;

            insideWall = true;
            break;
        }

        // Плавный переход
        float targetAlpha = insideWall ? maxAlpha : 0f;
        float speed = insideWall ? fadeInSpeed : fadeOutSpeed;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.deltaTime);

        fadeMaterial.SetFloat(AlphaID, currentAlpha);

        bool shouldBeVisible = currentAlpha > 0.001f;
        if (fadeRenderer.enabled != shouldBeVisible)
            fadeRenderer.enabled = shouldBeVisible;
    }

    void OnDestroy()
    {
        if (fadeMaterial != null) Destroy(fadeMaterial);
        if (fadeSphere != null) Destroy(fadeSphere);
    }
}