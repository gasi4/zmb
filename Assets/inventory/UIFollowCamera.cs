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
        Debug.Log($"UIFollowCamera.Show() called. Camera exists: {playerCamera != null}");

        isVisible = true;
        gameObject.SetActive(true);

        if (playerCamera != null)
        {
            Vector3 newPos = playerCamera.position +
                           playerCamera.forward * distance +
                           playerCamera.up * height +
                           playerCamera.right * horizontalOffset;

            transform.position = newPos;
            Debug.Log($"UI positioned at: {newPos}");

            if (alwaysFaceCamera)
            {
                transform.LookAt(playerCamera);
                transform.Rotate(0, 180, 0);
            }

            Debug.Log($"UI active: {gameObject.activeSelf}, position: {transform.position}");
        }
        else
        {
            Debug.LogError("UIFollowCamera: playerCamera is null in Show()!");
        }
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