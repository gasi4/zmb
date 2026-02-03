using UnityEngine;

public class VRDebugController : MonoBehaviour
{
    [Header("Настройки")]
    public bool useVRControls = false; // Поставь true когда подключаешь VR очки
    public float mouseSensitivity = 2f;
    public float moveSpeed = 15f;
    public float teleportDistance = 10f;

    [Header("Отладка")]
    public bool showDebugRay = true;

    private float xRotation = 0f;

    void Start()
    {
        // Фиксируем курсор в центре экрана
        Cursor.lockState = CursorLockMode.Locked;

        // Сбрасываем позицию камеры
        transform.localPosition = new Vector3(0, 1.6f, 0);
        transform.localRotation = Quaternion.identity;

        // Сбрасываем вращение XR Origin
        if (transform.parent != null)
            transform.parent.rotation = Quaternion.identity;

        Debug.Log("VRDebugController запущен. Режим: " + (useVRControls ? "VR" : "Редактор"));
    }

    void Update()
    {
        // === ВРАЩЕНИЕ КАМЕРЫ (работает всегда) ===
        HandleCameraRotation();

        // === ДВИЖЕНИЕ ===
        if (!useVRControls)
        {
            HandleEditorMovement(); // WASD + телепортация для редактора
        }
        // В режиме VR ничего не делаем - управление через XR контроллеры

        // === ОТЛАДКА: рисуем луч ===
        if (showDebugRay)
        {
            Debug.DrawRay(transform.position, transform.forward * 5f, Color.green);
        }
    }

    void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Вертикальное вращение (камера)
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Горизонтальное вращение (XR Origin)
        if (transform.parent != null)
            transform.parent.Rotate(0f, mouseX, 0f);
    }

    void HandleEditorMovement()
    {
        // === WASD ДВИЖЕНИЕ ===
        float moveHorizontal = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime;
        float moveVertical = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime;

        if (transform.parent != null)
        {
            // Двигаемся относительно направления взгляда
            Vector3 move = transform.parent.forward * moveVertical +
                          transform.parent.right * moveHorizontal;
            move.y = 0; // Не двигаемся по вертикали

            transform.parent.Translate(move, Space.World);
        }

        // === ПРОСТАЯ ТЕЛЕПОРТАЦИЯ НА T ===
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (transform.parent != null)
            {
                // Бросаем луч вперёд для определения точки телепортации
                Ray ray = new Ray(transform.position, transform.forward);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 10f))
                {
                    // Телепортируемся в точку попадания
                    transform.parent.position = hit.point + Vector3.up * 0.1f;
                    Debug.Log("Телепортировался в точку: " + hit.point);
                }
                else
                {
                    // Или просто вперёд на фиксированное расстояние
                    transform.parent.position += transform.forward * teleportDistance;
                    Debug.Log("Телепортировался вперёд на " + teleportDistance + " метров");
                }
            }
        }

        // === ВЗЯТИЕ ПРЕДМЕТА НА G (для теста) ===
        if (Input.GetKeyDown(KeyCode.G))
        {
            TestGrabInteraction();
        }

        // === ПЕРЕКЛЮЧЕНИЕ РЕЖИМА НА V (для быстрого теста) ===
        if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleVRMode();
        }
    }

    void TestGrabInteraction()
    {
        // Бросаем луч вперёд для поиска предметов
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 3f))
        {
            Debug.Log("Нашёл объект: " + hit.collider.gameObject.name);

            // Проверяем можно ли взять этот объект
            var grabItem = hit.collider.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
            if (grabItem != null)
            {
                Debug.Log("Можно взять предмет: " + grabItem.name);
                // Здесь можно добавить логику взятия
            }
        }
    }

    // === ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ УПРАВЛЕНИЯ ===

    public void ToggleVRMode()
    {
        useVRControls = !useVRControls;
        Debug.Log("Режим изменён на: " + (useVRControls ? "VR" : "Редактор"));
    }

    public void SetVRMode(bool vrEnabled)
    {
        useVRControls = vrEnabled;
        Debug.Log("Режим установлен: " + (useVRControls ? "VR" : "Редактор"));
    }

    public void TeleportTo(Vector3 position)
    {
        if (transform.parent != null)
        {
            transform.parent.position = position;
            Debug.Log("Принудительная телепортация в: " + position);
        }
    }

    // === ДЛЯ ОТЛАДКИ В РЕДАКТОРЕ ===
    void OnGUI()
    {
        if (!useVRControls)
        {
            GUI.Box(new Rect(10, 10, 250, 100), "РЕДАКТОРНЫЙ РЕЖИМ");
            GUI.Label(new Rect(20, 40, 230, 20), "WASD - Движение");
            GUI.Label(new Rect(20, 60, 230, 20), "Мышь - Вращение камеры");
            GUI.Label(new Rect(20, 80, 230, 20), "T - Телепортация вперёд");
            GUI.Label(new Rect(20, 100, 230, 20), "G - Взять предмет (тест)");
            GUI.Label(new Rect(20, 120, 230, 20), "V - Переключить режим VR");
        }
        else
        {
            GUI.Box(new Rect(10, 10, 250, 60), "VR РЕЖИМ");
            GUI.Label(new Rect(20, 40, 230, 20), "Управление через контроллеры");
        }
    }
}