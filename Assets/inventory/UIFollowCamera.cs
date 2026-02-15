using UnityEngine;

public class UIFollowCamera : MonoBehaviour
{
    [Header("Camera Reference")]
    public Transform playerCamera; // Камера игрока

    [Header("Position Settings")]
    public float distance = 2.0f; // Расстояние от камеры
    public float height = 1.0f; // Высота относительно камеры
    public float horizontalOffset = 0f; // Смещение влево/вправо (0 = по центру)

    [Header("Smooth Movement")]
    public bool smoothMovement = true;
    public float smoothSpeed = 10f;

    [Header("Rotation")]
    public bool alwaysFaceCamera = true; // Всегда повернут лицом к камере

    private bool isVisible = false;

    void Start()
    {
        // Если камера не назначена, пробуем найти
        if (playerCamera == null)
        {
            // Ищем основную камеру
            Camera cam = Camera.main;
            if (cam != null)
                playerCamera = cam.transform;
            else
                Debug.LogError("UIFollowCamera: Player Camera not assigned and Camera.main not found!");
        }

        // Изначально скрыт
        gameObject.SetActive(false);

    }

    void LateUpdate()
    {
        if (!isVisible || playerCamera == null) return;

        // Вычисляем целевую позицию перед камерой
        Vector3 targetPosition = playerCamera.position +
                                playerCamera.forward * distance +
                                playerCamera.up * height +
                                playerCamera.right * horizontalOffset;

        if (smoothMovement)
        {
            // Плавное движение
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        }
        else
        {
            // Мгновенное перемещение
            transform.position = targetPosition;
        }

        if (alwaysFaceCamera)
        {
            // Поворачиваем UI лицом к камере
            transform.LookAt(playerCamera);
            // Корректируем поворот (так как UI должен смотреть на камеру, но не быть перевернутым)
            transform.Rotate(0, 180, 0);
        }
    }

    public void Show()
    {
        // ПРИНУДИТЕЛЬНАЯ НАСТРОЙКА CANVAS
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = playerCamera.GetComponent<Camera>();
            canvas.planeDistance = 1;
            canvas.sortingOrder = 100; // Высокий приоритет
            Debug.Log($"Canvas configured: mode={canvas.renderMode}, camera={canvas.worldCamera?.name}");
        }
        else
        {
            Debug.LogError("No Canvas component found!");
            return;
        }
        Debug.Log("★★★★★ UIFollowCamera.Show() STARTED ★★★★★");
        Debug.Log($"isVisible before: {isVisible}");
        Debug.Log($"gameObject.activeSelf before: {gameObject.activeSelf}");
        Debug.Log($"playerCamera null? {playerCamera == null}");

        if (playerCamera != null)
        {
            Debug.Log($"Camera name: {playerCamera.name}");
            Debug.Log($"Camera position: {playerCamera.position}");
            Debug.Log($"Camera forward: {playerCamera.forward}");
        }

        isVisible = true;
        gameObject.SetActive(true);

        Debug.Log($"gameObject.activeSelf after: {gameObject.activeSelf}");
        Debug.Log($"transform.position before move: {transform.position}");

        if (playerCamera != null)
        {
            Vector3 newPos = playerCamera.position +
                           playerCamera.forward * distance +
                           playerCamera.up * height +
                           playerCamera.right * horizontalOffset;

            transform.position = newPos;
            Debug.Log($"transform.position after move: {transform.position}");
            Debug.Log($"Distance from camera: {Vector3.Distance(transform.position, playerCamera.position)}");

            if (alwaysFaceCamera)
            {
                transform.LookAt(playerCamera);
                transform.Rotate(0, 180, 0);
                Debug.Log($"transform.rotation: {transform.rotation.eulerAngles}");
            }

            // ВИЗУАЛЬНЫЙ МАРКЕР - поставить куб на месте UI
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.position = transform.position;
            marker.transform.localScale = Vector3.one * 0.2f;
            marker.name = "UI_POSITION_MARKER";
            Destroy(marker, 3f); // Исчезнет через 3 секунды
        }
        else
        {
            Debug.LogError("★★★★★ playerCamera is NULL! ★★★★★");
        }

        Debug.Log($"isVisible after: {isVisible}");
        Debug.Log("★★★★★ UIFollowCamera.Show() FINISHED ★★★★★\n");


    }

    public void Hide()
    {
        isVisible = false;
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (isVisible)
            Hide();
        else
            Show();
    }
}