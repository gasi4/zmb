using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;

public class FinalPlayerController : MonoBehaviour
{
    [Header("Settings")]
    public bool vrModeActive = false;

    [Header("Editor References")]
    public Transform playerCamera;
    public Transform handForEmulation;

    [Header("Unified Ray")]
    public UnifiedRay unifiedRay;

    [Header("VR References")]
    public Transform rightHandTransform;
    public XRController rightController;
    public InputHelpers.Button grabButton = InputHelpers.Button.Trigger;

    [Header("Editor Movement Settings")]
    public float mouseSensitivity = 2f;
    public float walkSpeed = 5f;
    public float throwForce = 5f;

    [Header("Debug")]
    public bool debugMode = true;

    [Header("VR Hold")]
    public float holdDistance = 0.5f;
    public float holdSmoothness = 15f;

    [Header("Inventory")]
    public InventoryManager inventoryManager;
    public KeyCode inventoryToggleKey = KeyCode.Tab;
    public KeyCode pickupKey = KeyCode.E; // Кнопка для подбора предметов

    private GameObject heldObject = null;
    private Rigidbody heldRigidbody = null;
    private float xRotation = 0f;

    [Header("Inventory Sync")]
    public bool syncInventoryWithHand = true;

    private ItemScriptableObject heldInventoryItem;

    private bool inputEnabled = true;

    // ДОБАВЛЕНО: Delivery Point взаимодействие
    [Header("Delivery Point")]
    public KeyCode placeOnDeliveryKey = KeyCode.E; // Кнопка для класть на точку
    public float deliveryInteractionRange = 10f;

    void Start()
    {
        if (!vrModeActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (unifiedRay != null)
        {
            unifiedRay.showLine = true;
            unifiedRay.vrModeActive = vrModeActive;

            if (vrModeActive && rightHandTransform != null)
            {
                unifiedRay.rightHandTransform = rightHandTransform;
            }
        }

        // ВАЖНО: значение из инспектора может переопределять дефолт в коде,
        // поэтому принудительно держим дистанцию не меньше 10.
        deliveryInteractionRange = Mathf.Max(deliveryInteractionRange, 10f);
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
    }

    void Update()
    {
        // 1️⃣ Всегда проверяем TAB
        if (Input.GetKeyDown(inventoryToggleKey))
        {
            if (inventoryManager != null)
            {
                inventoryManager.ToggleInventory();
            }
        }

        // 2️⃣ Проверка UI других элементов (инвентарь или стиральная машина)
        bool inventoryOpen = inventoryManager != null && inventoryManager.isOpened;
        bool washingMachineOpen = IsWashingMachineUIOpen();

        if (washingMachineOpen)
        {
            // Если открыта стиралка, блокируем управление
            return;
        }

        // 3️⃣ Если инвентарь открыт, не блокируем проверку TAB (она уже сработала),
        // но можно блокировать остальное управление игроком
        if (!inventoryOpen)
        {
            if (!inputEnabled) return;

            HandleEditorMode();

            // Одна кнопка E:
            // - если в руке есть предмет -> пытаемся положить на DeliveryPoint
            // - если руки пустые -> подбираем/взаимодействуем лучом
            if (Input.GetKeyDown(pickupKey) || Input.GetKeyDown(placeOnDeliveryKey))
            {
                if (heldObject != null)
                    TryPlaceOnDeliveryPoint();
                else
                    TryPickupItem();
            }
        }
    }

    // ДОБАВЛЕНО: Метод для попытки положить вещь на Delivery Point
    void TryPlaceOnDeliveryPoint()
    {
        if (heldObject == null)
        {
            Debug.Log("В руке нет вещи для класть на точку!");
            return;
        }

        // Ищем подходящий DeliveryPoint: сначала тот, в зоне которого стоит игрок,
        // иначе — ближайший в радиусе.
        DeliveryPoint[] points = FindObjectsOfType<DeliveryPoint>();
        if (points == null || points.Length == 0)
        {
            Debug.Log("Не найден Delivery Point на сцене!");
            return;
        }

        DeliveryPoint deliveryPoint = null;
        bool selectedByZone = false;

        // 1) Приоритет: стоим в зоне перед полкой
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].IsPlayerInInteractionZone(transform))
            {
                deliveryPoint = points[i];
                selectedByZone = true;
                break;
            }
        }

        // 2) Fallback: ближайший по дистанции
        if (deliveryPoint == null)
        {
            float bestDist = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                float d = Vector3.Distance(transform.position, points[i].transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    deliveryPoint = points[i];
                }
            }
        }

        if (deliveryPoint == null)
        {
            Debug.Log("Не найден подходящий Delivery Point!");
            return;
        }

        // Если мы стоим в зоне перед полкой — расстояние до центра точки не важно.
        // Иначе (fallback) оставляем проверку по дистанции.
        if (!selectedByZone)
        {
            float distance = Vector3.Distance(transform.position, deliveryPoint.transform.position);
            if (distance > deliveryInteractionRange)
            {
                Debug.Log($"Слишком далеко от Delivery Point! Дистанция: {distance:F1}, нужно: {deliveryInteractionRange}");
                return;
            }
        }

        // Проверяем что это предмет (если в руке визуал из инвентаря — создаём world-версию)
        Item itemComponent = heldObject.GetComponent<Item>();
        if (itemComponent == null)
        {
            if (heldInventoryItem != null && heldInventoryItem.WorldPrefab != null)
            {
                // Меняем "визуал в руке" на реальный объект мира с компонентом Item
                GameObject worldItem = Instantiate(heldInventoryItem.WorldPrefab);
                worldItem.transform.position = heldObject.transform.position;
                worldItem.transform.rotation = heldObject.transform.rotation;

                itemComponent = worldItem.GetComponent<Item>();
                if (itemComponent == null) itemComponent = worldItem.AddComponent<Item>();

                itemComponent.item = heldInventoryItem;
                itemComponent.amount = 1;

                // Заменяем heldObject на созданный предмет
                Destroy(heldObject);
                heldObject = worldItem;
            }
            else
            {
                Debug.Log("У объекта нет компонента Item и нет WorldPrefab у предмета из инвентаря!");
                return;
            }
        }

        // ✅ НЕ ИЩЕМ ВЛАДЕЛЬЦА - ВЕЩЬ МОЖЕТ БЫТЬ БЕЗ ВЛАДЕЛЬЦА
        // Ищем ближайшего зомби, который ждет вещь
        ZombieCustomer nearestZombie = FindNearestWaitingZombie();
        if (nearestZombie == null)
        {
            Debug.Log("Нет зомби, ожидающих вещь!");
            return;
        }

        // Пытаемся положить вещь на точку
        if (deliveryPoint.PlaceItem(heldObject, nearestZombie))
        {
            Debug.Log($"✅ Вещь {heldObject.name} положена на Delivery Point для зомби {nearestZombie.name}");

            // Очищаем руку
            ClearHeldItem();
        }
        else
        {
            Debug.Log("Не удалось положить вещь на Delivery Point!");
        }
    }

    // Метод для поиска ближайшего зомби, который ждет вещь
    ZombieCustomer FindNearestWaitingZombie()
    {
        // 1) Приоритет: зомби на первом месте очереди
        CustomerQueueManager queue = FindObjectOfType<CustomerQueueManager>();
        if (queue != null)
        {
            ZombieCustomer first = queue.GetFirstWaitingZombie();
            if (first != null)
            {
                Debug.Log($"Найден зомби в очереди: {first.name}");
                return first;
            }
        }

        // 2) Fallback: ближайший зомби в состоянии Waiting
        ZombieCustomer[] allZombies = FindObjectsOfType<ZombieCustomer>();
        ZombieCustomer nearestZombie = null;
        float minDistance = float.MaxValue;

        foreach (var zombie in allZombies)
        {
            if (zombie == null) continue;

            // В очереди зомби может перейти в GettingAngry, но он всё ещё ждёт выдачу.
            if (zombie.currentState == ZombieCustomer.ZombieState.Waiting ||
                zombie.currentState == ZombieCustomer.ZombieState.GettingAngry)
            {
                float distance = Vector3.Distance(transform.position, zombie.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestZombie = zombie;
                }
            }
        }

        if (nearestZombie != null)
            Debug.Log($"Найден зомби {nearestZombie.name} на расстоянии {minDistance:F1}");
        else
            Debug.Log("Не найден ни один зомби в состоянии Waiting");

        return nearestZombie;
    }

    bool IsWashingMachineUIOpen()
    {
        // Ищем все стиральные машины в сцене
        WashingMachineWithInventory[] machines = FindObjectsOfType<WashingMachineWithInventory>();

        foreach (var machine in machines)
        {
            // Проверяем, активен ли Canvas стиральной машины
            if (machine.machineCanvas != null && machine.machineCanvas.gameObject.activeSelf)
            {
                return true;
            }
        }

        // Также проверяем старую стиральную машину (WashingMachineUI)
        WashingMachineUI[] oldMachines = FindObjectsOfType<WashingMachineUI>();
        foreach (var machine in oldMachines)
        {
            if (machine.panel != null && machine.panel.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    void HandleVRMode()
    {
        HandleGrabVR();

        if (unifiedRay != null && rightHandTransform != null)
        {
            unifiedRay.vrModeActive = true;
            unifiedRay.rightHandTransform = rightHandTransform;
        }
    }

    void HandleEditorMode()
    {
        HandleEditorCamera();
        HandleEditorMovement();
        HandleEditorGrab();

        if (heldObject != null && handForEmulation != null)
        {
            UpdateHeldObjectEditor();
        }

        if (unifiedRay != null)
        {
            unifiedRay.vrModeActive = false;
        }
    }

    #region Editor Mode Controls
    void HandleEditorCamera()
    {
        if (playerCamera == null) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        if (mouseX != 0 || mouseY != 0)
        {
            transform.Rotate(0, mouseX * mouseSensitivity, 0);
            xRotation -= mouseY * mouseSensitivity;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0, 0);
        }
    }

    void HandleEditorMovement()
    {
        float h = Input.GetAxis("Horizontal") * walkSpeed * Time.deltaTime;
        float v = Input.GetAxis("Vertical") * walkSpeed * Time.deltaTime;

        Vector3 move = transform.forward * v + transform.right * h;
        transform.Translate(move, Space.World);
    }

    void HandleEditorGrab()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (heldObject == null)
                TryGrab();
            else
                Drop();
        }
    }

    void UpdateHeldObjectEditor()
    {

    }
    #endregion

    #region Item Pickup System
    void TryPickupItem()
    {
        if (unifiedRay == null) return;

        // Просто используем готовый метод Raycast
        if (unifiedRay.Raycast(out RaycastHit hit, out Ray ray))
        {
            HandleHitObject(hit);
        }
    }

    void HandleHitObject(RaycastHit hit)
    {
        GameObject hitObject = hit.collider.gameObject;
        Debug.Log($"🎯 Луч попал в: {hitObject.name}");

        // 1️⃣ Проверяем стиральные машины
        var machine = hitObject.GetComponentInParent<WashingMachineWithInventory>();
        if (machine != null)
        {
            Debug.Log("🧺 Открываю UI новой стиралки");
            machine.OpenMachineUI();

            // Отключаем физический захват и лучи игрока
            if (unifiedRay != null) unifiedRay.enabled = false;
            SetInputEnabled(false);
            return;
        }

        var oldMachine = hitObject.GetComponentInParent<WashingMachineUI>();
        if (oldMachine != null)
        {
            Debug.Log("🧺 Открываю UI старой стиралки");
            oldMachine.ToggleMenu();

            // Отключаем физический захват и лучи игрока
            if (unifiedRay != null) unifiedRay.enabled = false;
            SetInputEnabled(false);
            return;
        }

        // 2️⃣ Если это предмет мира, берём в руку
        GrabItemToHand(hitObject);
    }

    void TryGrab()
    {
        if (unifiedRay == null) return;

        if (debugMode) Debug.Log("Пытаюсь схватить...");

        if (unifiedRay.Raycast(out RaycastHit hit, out _))
        {
            GrabPhysicalObject(hit.collider.gameObject);
        }
    }

    // В разделе Grab System добавь/измени:
    void GrabPhysicalObject(GameObject obj)
    {
        if (obj == null) return;

        // Если это стиралка — выходим
        if (obj.GetComponentInParent<WashingMachineWithInventory>() != null) return;
        if (obj.GetComponentInParent<WashingMachineUI>() != null) return;

        Item itemComponent = obj.GetComponent<Item>();
        bool isInventoryItem = (itemComponent != null && itemComponent.item != null);

        if (isInventoryItem)
        {
            // Добавляем в инвентарь и берём в руку
            inventoryManager?.AddItem(itemComponent.item, itemComponent.amount);
            GrabItemFromInventory(itemComponent.item, itemComponent.amount);
            Destroy(obj);
            return;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.Log("Нет Rigidbody для захвата");
            return;
        }

        heldObject = obj;
        heldRigidbody = rb;
        heldRigidbody.isKinematic = true;

        if (handForEmulation != null)
        {
            heldObject.transform.SetParent(handForEmulation);
            heldObject.transform.localPosition = Vector3.zero;
            heldObject.transform.localRotation = Quaternion.identity;
        }

        Debug.Log("📦 Предмет взят в руку");
    }

    public void GrabItemFromInventory(ItemScriptableObject item, int amount)
    {
        if (item == null || item.HandPrefab == null)
        {
            Debug.LogError("❌ Нет HandPrefab");
            return;
        }

        // Если уже что-то в руке - убираем
        if (heldObject != null)
        {
            // НЕ вызываем Drop() - просто скрываем
            HideHeldObject();
        }

        // Создаем визуал предмета в руке
        Transform targetHand = vrModeActive ? rightHandTransform : handForEmulation;
        if (targetHand == null) return;

        heldObject = Instantiate(item.HandPrefab, targetHand);
        heldObject.name = item.ItemName + "_Hand";
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        heldObject.transform.localScale = Vector3.one;

        // Убираем физику
        DestroyIfExists<Rigidbody>(heldObject);
        DestroyIfExists<Collider>(heldObject);

        // Сохраняем ссылку на предмет инвентаря
        heldInventoryItem = item;

        Debug.Log($"✅ {item.ItemName} визуально экипирован в руку");
    }

    void DestroyIfExists<T>(GameObject obj) where T : Component
    {
        var c = obj.GetComponent<T>();
        if (c != null) Destroy(c);
    }

    public void GrabItemToHand(GameObject item)
    {
        if (item == null) return;

        if (heldObject != null)
            HideHeldObject();

        heldObject = item;

        Transform targetHand = vrModeActive ? rightHandTransform : handForEmulation;
        if (targetHand == null)
        {
            Debug.LogError("Hand transform not assigned");
            return;
        }

        // Создаём anchor
        GameObject anchor = new GameObject("HandAnchor");
        anchor.transform.SetParent(targetHand);

        // Вычисляем компенсированный масштаб
        Vector3 parentScale = targetHand.lossyScale; // глобальный масштаб родителя
        Vector3 inverseScale = new Vector3(
            1f / parentScale.x,
            1f / parentScale.y,
            1f / parentScale.z
        );

        anchor.transform.localScale = inverseScale;

        // Смещение вперед относительно руки
        anchor.transform.localPosition = new Vector3(0f, -0.1f, 20f);
        anchor.transform.localRotation = Quaternion.identity;

        // Сохраняем оригинальный масштаб предмета
        Vector3 originalScale = heldObject.transform.localScale;

        // Вставляем предмет в anchor
        heldObject.transform.SetParent(anchor.transform);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        heldObject.transform.localScale = originalScale;

        // Отключаем Rigidbody/Collider
        DestroyIfExists<Rigidbody>(heldObject);
        DestroyIfExists<Collider>(heldObject);

        heldObject.SetActive(true);

        Debug.Log("Item grabbed, scaled and offset correctly");
    }

    public void HideHeldObject()
    {
        if (heldObject == null) return;

        heldObject.SetActive(false);
        heldObject.transform.SetParent(null);

        heldRigidbody = null;
    }

    public bool HasInventoryItemInHand()
    {
        return heldInventoryItem != null;
    }

    public ItemScriptableObject GetInventoryItemInHand()
    {
        return heldInventoryItem;
    }

    void CheckIfObjectMatchesInventoryItem(GameObject item)
    {
        if (inventoryManager == null) return;

        Item itemComponent = item.GetComponent<Item>();
        if (itemComponent != null && itemComponent.item != null)
        {
            Debug.Log($"Объект соответствует предмету инвентаря: {itemComponent.item.ItemName}");
        }
        else
        {
            Debug.Log($"Объект не имеет компонента Item. Имя: {item.name}");
        }
    }

    public void DropHeldItem()
    {
        if (inventoryManager != null && inventoryManager.HasItemInHand())
        {
            inventoryManager.DropHeldItem();
        }
        else if (heldObject != null)
        {
            Drop(); // Стандартный метод для физических объектов
        }
    }

    void Drop()
    {
        if (heldObject == null) return;

        Debug.Log($"Бросаю: {heldObject.name}");

        // Проверяем, предмет из инвентаря или из мира
        if (inventoryManager != null && inventoryManager.HasItemInHand())
        {
            // Предмет из инвентаря
            inventoryManager.DropHeldItem();
        }
        else
        {
            // Физический объект из мира
            // Сначала снимаем родителя
            heldObject.transform.SetParent(null);

            // Если есть Rigidbody, применяем физику
            if (heldRigidbody != null)
            {
                heldRigidbody.isKinematic = false;

                Vector3 throwDirection = vrModeActive && rightHandTransform != null
                    ? rightHandTransform.forward
                    : (playerCamera != null ? playerCamera.forward : transform.forward);
                heldRigidbody.velocity = throwDirection * throwForce;
            }

            // Очищаем ссылки
            heldObject = null;
            heldRigidbody = null;
        }
    }

    void HandleGrabVR()
    {
        if (rightController == null || unifiedRay == null) return;

        bool pressed;
        rightController.inputDevice.IsPressed(grabButton, out pressed);

        if (pressed)
        {
            if (heldObject == null) TryGrab();
        }
        else
        {
            if (heldObject != null) Drop();
        }
    }
    #endregion

    // Дополнительные методы
    public bool HasItemInHand()
    {
        return heldObject != null;
    }

    public GameObject GetHeldItem()
    {
        return heldObject;
    }

    public void ClearHeldItem()
    {
        // Если это был "визуальный" предмет из инвентаря (обычно без компонента Item), уничтожаем его
        if (heldObject != null && heldObject.GetComponent<Item>() == null)
        {
            Destroy(heldObject);
        }

        heldObject = null;
        heldRigidbody = null;
        heldInventoryItem = null;
    }

    public void SetHeldItemDirectly(GameObject item)
    {
        heldObject = item;
        if (item != null)
        {
            heldRigidbody = item.GetComponent<Rigidbody>();
            if (heldRigidbody != null) heldRigidbody.isKinematic = true;
        }
        else
        {
            heldRigidbody = null;
        }
    }

    public ItemScriptableObject GetItemInHand()
    {
        if (heldObject == null) return null;

        Item itemComponent = heldObject.GetComponent<Item>();
        if (itemComponent != null && itemComponent.item != null)
        {
            return itemComponent.item;
        }

        return null;
    }

    public int GetItemAmountInHand()
    {
        if (heldObject == null) return 0;

        Item itemComponent = heldObject.GetComponent<Item>();
        if (itemComponent != null)
        {
            return itemComponent.amount;
        }

        return 1;
    }
}