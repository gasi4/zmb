using UnityEngine;
using UnityEngine.InputSystem;
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
    public ActionBasedController rightController;
    public ActionBasedController leftController;

    [Header("VR Input Actions (Action-based XRI)")]
    public InputActionProperty rightStick;
    public InputActionProperty lookAction;

    [Header("VR Grab Mode")]
    public InputActionProperty storeToInventoryModifier;

    [Header("VR Delivery (Right Grip)")]
    public InputActionProperty rightGripAction;
    public float vrDeliveryDebounce = 0.25f;
    private float _nextAllowedVrDeliveryTime;

    [Header("Editor Movement Settings")]
    public float mouseSensitivity = 2f;
    public float walkSpeed = 5f;
    public float throwForce = 5f;

    [Header("Debug")]
    public bool debugMode = true;

    [Header("VR Hold")]
    public float holdDistance = 0.5f;
    public float holdSmoothness = 15f;

    [Header("VR Look (Right Stick)")]
    public float lookSensitivity = 90f;
    public float maxPitch = 80f;

    [Header("Inventory")]
    public InventoryManager inventoryManager;
    public KeyCode inventoryToggleKey = KeyCode.Tab;
    public KeyCode pickupKey = KeyCode.E;

    private GameObject heldObject = null;
    private Rigidbody heldRigidbody = null;
    private float xRotation = 0f;
    private float yaw = 0f;

    [Header("VR Inventory Toggle (Action-based)")]
    public InputActionProperty vrInventoryToggleAction;
    public float vrToggleDebounce = 0.25f;
    private float _nextAllowedVrToggleTime;

    [Header("Inventory Sync")]
    public bool syncInventoryWithHand = true;

    private ItemScriptableObject heldInventoryItem;
    private bool inputEnabled = true;

    [Header("Delivery Point")]
    public KeyCode placeOnDeliveryKey = KeyCode.E;
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

            deliveryInteractionRange = Mathf.Max(deliveryInteractionRange, 10f);
        }

        if (playerCamera != null)
        {
            Vector3 e = playerCamera.localEulerAngles;
            yaw = e.y;
            xRotation = NormalizeAngle(e.x);
        }
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
    }

    bool IsStoreToInventoryModifierPressed()
    {
        if (!vrModeActive) return false;
        InputAction a = storeToInventoryModifier.action;
        if (a == null || !a.enabled) return false;
        return a.ReadValue<float>() > 0.5f;
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void Update()
    {
        if (!inputEnabled) return;

        if (vrModeActive)
        {
            HandleVRMode();
        }
        else
        {
            HandleEditorMode();
        }

        if (Input.GetKeyDown(inventoryToggleKey))
            inventoryManager?.ToggleInventory();

        if (Input.GetKeyDown(placeOnDeliveryKey))
            TryPlaceOnDeliveryPoint();

        if (Input.GetKeyDown(pickupKey))
            TryPickupItem();
    }

    void HandleVRMode()
    {
        HandleVRLook();
        HandleGrabVR();
        HandleVRInventoryToggle();
        HandleVRDelivery();

        if (unifiedRay != null && rightHandTransform != null)
        {
            unifiedRay.vrModeActive = true;
            unifiedRay.rightHandTransform = rightHandTransform;
        }
    }

    void HandleVRDelivery()
    {
        if (!vrModeActive) return;
        if (Time.time < _nextAllowedVrDeliveryTime) return;
        if (rightGripAction.action == null || !rightGripAction.action.enabled) return;

        bool pressed = rightGripAction.action.ReadValue<float>() > 0.5f;
        if (!pressed) return;

        DeliveryPoint dp = FindDeliveryPointPlayerIsInside();
        if (dp == null) return;

        // Если есть что положить (предмет в руке или в инвентаре) – пытаемся разместить
        if (heldObject != null || heldInventoryItem != null)
        {
            TryPlaceOnDeliveryPoint();
        }

        _nextAllowedVrDeliveryTime = Time.time + vrDeliveryDebounce;
    }

    DeliveryPoint FindDeliveryPointPlayerIsInside()
    {
        DeliveryPoint[] points = FindObjectsOfType<DeliveryPoint>();
        if (points == null) return null;

        for (int i = 0; i < points.Length; i++)
        {
            DeliveryPoint p = points[i];
            if (p == null) continue;
            if (p.IsPlayerInInteractionZone(transform))
                return p;
        }
        return null;
    }

    void HandleEditorMode()
    {
        HandleEditorCamera();
        HandleEditorMovement();
        HandleEditorGrab();

        if (unifiedRay != null)
        {
            unifiedRay.vrModeActive = false;
        }
    }

    void HandleVRInventoryToggle()
    {
        if (inventoryManager == null) return;
        if (vrInventoryToggleAction.action == null) return;
        if (!vrInventoryToggleAction.action.enabled) return;
        if (Time.time < _nextAllowedVrToggleTime) return;

        bool pressed = vrInventoryToggleAction.action.ReadValue<float>() > 0.5f;

        if (pressed)
        {
            inventoryManager.ToggleInventory();
            _nextAllowedVrToggleTime = Time.time + vrToggleDebounce;
        }
    }

    void HandleVRLook()
    {
        if (playerCamera == null) return;

        InputAction action =
            lookAction.action != null && lookAction.action.enabled
                ? lookAction.action
                : rightStick.action;

        if (action == null || !action.enabled) return;

        Vector2 stick = action.ReadValue<Vector2>();
        if (stick.sqrMagnitude < 0.0001f) return;

        float dt = Time.deltaTime;

        yaw += stick.x * lookSensitivity * dt;
        xRotation -= stick.y * lookSensitivity * dt;
        xRotation = Mathf.Clamp(xRotation, -maxPitch, maxPitch);

        playerCamera.localRotation = Quaternion.Euler(xRotation, yaw, 0f);
    }

    void TryPlaceOnDeliveryPoint()
    {
        // Поиск DeliveryPoint
        DeliveryPoint deliveryPoint = null;
        bool selectedByZone = false;
        DeliveryPoint[] points = FindObjectsOfType<DeliveryPoint>();
        if (points == null || points.Length == 0)
        {
            Debug.Log("Не найден Delivery Point на сцене!");
            return;
        }

        // Приоритет: зона, в которой стоит игрок
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].IsPlayerInInteractionZone(transform))
            {
                deliveryPoint = points[i];
                selectedByZone = true;
                break;
            }
        }

        // Fallback: ближайший
        if (deliveryPoint == null)
        {
            float bestDist = float.MaxValue;
            foreach (var p in points)
            {
                if (p == null) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    deliveryPoint = p;
                }
            }
        }

        if (deliveryPoint == null)
        {
            Debug.Log("Не найден подходящий Delivery Point!");
            return;
        }

        // Проверка дистанции (если не в зоне)
        if (!selectedByZone)
        {
            float distance = Vector3.Distance(transform.position, deliveryPoint.transform.position);
            if (distance > deliveryInteractionRange)
            {
                Debug.Log($"Слишком далеко от Delivery Point! Дистанция: {distance:F1}, нужно: {deliveryInteractionRange}");
                return;
            }
        }

        // --- Определяем, что будем класть ---
        GameObject objectToPlace = heldObject;
        Item itemComponent = objectToPlace != null ? objectToPlace.GetComponent<Item>() : null;
        slot sourceSlot = null; // слот инвентаря, из которого взят предмет (если применимо)

        // Случай 1: В руке физический объект с Item (из мира или из инвентаря, но уже созданный)
        if (objectToPlace != null && itemComponent != null && itemComponent.item != null)
        {
            // Это готовый объект, ничего не делаем
        }
        // Случай 2: В руке визуальный предмет из инвентаря (heldInventoryItem)
        else if (objectToPlace == null && heldInventoryItem != null)
        {
            if (heldInventoryItem.WorldPrefab == null)
            {
                Debug.LogError($"Предмет {heldInventoryItem.ItemName} не имеет WorldPrefab!");
                return;
            }
            // Ищем слот в инвентаре с таким же предметом
            if (inventoryManager != null && inventoryManager.slots != null)
            {
                foreach (var slot in inventoryManager.slots)
                {
                    if (slot != null && !slot.isEmpty && slot.item == heldInventoryItem)
                    {
                        sourceSlot = slot;
                        break;
                    }
                }
            }
            // Создаём временный объект в мире
            GameObject worldItem = Instantiate(heldInventoryItem.WorldPrefab);
            worldItem.transform.position = transform.position + transform.forward * 0.5f;
            worldItem.transform.rotation = Quaternion.identity;

            itemComponent = worldItem.GetComponent<Item>();
            if (itemComponent == null) itemComponent = worldItem.AddComponent<Item>();
            itemComponent.item = heldInventoryItem;
            itemComponent.amount = 1;

            objectToPlace = worldItem;
        }
        // Случай 3: В руке ничего нет – пробуем взять первый предмет из инвентаря
        else if (objectToPlace == null && inventoryManager != null && inventoryManager.slots != null)
        {
            foreach (var slot in inventoryManager.slots)
            {
                if (slot != null && !slot.isEmpty && slot.item != null && slot.item.WorldPrefab != null)
                {
                    GameObject worldItem = Instantiate(slot.item.WorldPrefab);
                    worldItem.transform.position = transform.position + transform.forward * 0.5f;
                    worldItem.transform.rotation = Quaternion.identity;

                    itemComponent = worldItem.GetComponent<Item>();
                    if (itemComponent == null) itemComponent = worldItem.AddComponent<Item>();
                    itemComponent.item = slot.item;
                    itemComponent.amount = slot.amount;

                    objectToPlace = worldItem;
                    sourceSlot = slot;
                    break;
                }
            }
            if (objectToPlace == null)
            {
                Debug.Log("В инвентаре нет предметов для размещения!");
                return;
            }
        }
        else
        {
            Debug.Log("Нечего класть на точку!");
            return;
        }

        // Ищем зомби, который ждёт вещь
        ZombieCustomer nearestZombie = FindNearestWaitingZombie();
        if (nearestZombie == null)
        {
            Debug.Log("Нет зомби, ожидающих вещь!");
            if (objectToPlace != heldObject) Destroy(objectToPlace);
            return;
        }

        // Пытаемся положить вещь на точку
        if (deliveryPoint.PlaceItem(objectToPlace, nearestZombie))
        {
            Debug.Log($"✅ Вещь {objectToPlace.name} положена на Delivery Point для зомби {nearestZombie.name}");

            // Если предмет был взят из инвентаря – очищаем соответствующий слот
            if (sourceSlot != null)
            {
                sourceSlot.ClearSlot();
            }
            // Если предмет был в heldInventoryItem, дополнительно сбрасываем его
            if (heldInventoryItem != null)
            {
                heldInventoryItem = null;
            }

            // Очищаем руку игрока
            ClearHeldItem();
        }
        else
        {
            Debug.Log("Не удалось положить вещь на Delivery Point!");
            if (objectToPlace != heldObject) Destroy(objectToPlace);
        }
    }

    ZombieCustomer FindNearestWaitingZombie()
    {
        // Приоритет: зомби на первом месте очереди
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

        // Fallback: ближайший зомби в состоянии Waiting / GettingAngry / Angry
        ZombieCustomer[] allZombies = FindObjectsOfType<ZombieCustomer>();
        ZombieCustomer nearestZombie = null;
        float minDistance = float.MaxValue;

        foreach (var zombie in allZombies)
        {
            if (zombie == null) continue;

            if (zombie.currentState == ZombieCustomer.ZombieState.Waiting ||
                zombie.currentState == ZombieCustomer.ZombieState.GettingAngry ||
                zombie.currentState == ZombieCustomer.ZombieState.Angry)
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
    #endregion

    #region Item Pickup System
    void TryPickupItem()
    {
        if (unifiedRay == null) return;
        if (unifiedRay.Raycast(out RaycastHit hit, out Ray ray))
        {
            HandleObjectInteraction(hit.collider.gameObject);
        }
    }

    

    void TryGrab()
    {
        if (unifiedRay.Raycast(out RaycastHit hit, out _))
        {
            Debug.Log($"Луч попал в: {hit.collider.gameObject.name}, слой: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            HandleObjectInteraction(hit.collider.gameObject);
        }
        else
        {
            Debug.Log("Луч никуда не попал");
        }
    }

    void GrabPhysicalObject(GameObject obj)
    {
        if (obj == null) return;

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

        Debug.Log("📦 Физический предмет взят в руку");
    }

    public void GrabItemFromInventory(ItemScriptableObject item, int amount)
    {
        if (item == null || item.HandPrefab == null)
        {
            Debug.LogError("❌ Нет HandPrefab");
            return;
        }

        if (heldObject != null)
        {
            HideHeldObject();
        }

        Transform targetHand = vrModeActive ? rightHandTransform : handForEmulation;
        if (targetHand == null) return;

        heldObject = Instantiate(item.HandPrefab, targetHand);
        heldObject.name = item.ItemName + "_Hand";
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        heldObject.transform.localScale = Vector3.one;

        DestroyIfExists<Rigidbody>(heldObject);
        DestroyIfExists<Collider>(heldObject);

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

        GameObject anchor = new GameObject("HandAnchor");
        anchor.transform.SetParent(targetHand);

        Vector3 parentScale = targetHand.lossyScale;
        Vector3 inverseScale = new Vector3(
            1f / parentScale.x,
            1f / parentScale.y,
            1f / parentScale.z
        );

        anchor.transform.localScale = inverseScale;
        anchor.transform.localPosition = new Vector3(0f, -0.1f, 0.2f);
        anchor.transform.localRotation = Quaternion.identity;

        Vector3 originalScale = heldObject.transform.localScale;

        heldObject.transform.SetParent(anchor.transform);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        heldObject.transform.localScale = originalScale;

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
            Drop();
        }
    }

    void Drop()
    {
        if (heldObject == null) return;

        // Проверяем, находимся ли в зоне доставки
        if (TryGetDeliveryPointInZone(out DeliveryPoint dp))
        {
            // Есть ли зомби, который ждёт?
            ZombieCustomer zombie = FindTargetZombie();
            if (zombie != null)
            {
                // Пытаемся положить предмет на точку
                if (TryPlaceOnDeliveryPointDirect(heldObject, zombie, dp))
                {
                    // Успешно передали – очищаем руку и выходим
                    ClearHeldItem();
                    return;
                }
            }
        }

        // Иначе – обычное бросание
        Debug.Log($"Бросаю: {heldObject.name}");
        if (inventoryManager != null && inventoryManager.HasItemInHand())
        {
            inventoryManager.DropHeldItem();
        }
        else
        {
            heldObject.transform.SetParent(null);
            if (heldRigidbody != null)
            {
                heldRigidbody.isKinematic = false;
                Vector3 throwDirection = vrModeActive && rightHandTransform != null
                    ? rightHandTransform.forward
                    : (playerCamera != null ? playerCamera.forward : transform.forward);
                heldRigidbody.velocity = throwDirection * throwForce;
            }
            heldObject = null;
            heldRigidbody = null;
        }
    }
    private bool TryPlaceOnDeliveryPointDirect(GameObject obj, ZombieCustomer zombie, DeliveryPoint dp)
    {
        if (obj == null || zombie == null || dp == null) return false;

        // Если предмет из инвентаря (heldInventoryItem) – создаём временный WorldPrefab
        if (heldInventoryItem != null)
        {
            if (heldInventoryItem.WorldPrefab == null)
            {
                Debug.LogError("Нет WorldPrefab для предмета из инвентаря");
                return false;
            }
            GameObject worldItem = Instantiate(heldInventoryItem.WorldPrefab);
            worldItem.transform.position = obj.transform.position;
            worldItem.transform.rotation = obj.transform.rotation;
            Item itemComp = worldItem.GetComponent<Item>();
            if (itemComp == null) itemComp = worldItem.AddComponent<Item>();
            itemComp.item = heldInventoryItem;
            itemComp.amount = 1; // или нужное количество

            // Пытаемся разместить на точке
            bool result = dp.PlaceItem(worldItem, zombie);
            if (result)
            {
                // Удаляем визуальный объект из руки
                Destroy(obj);
            }
            else
            {
                Destroy(worldItem);
            }
            return result;
        }
        else
        {
            // Обычный физический объект
            return dp.PlaceItem(obj, zombie);
        }
    }

    void HandleGrabVR()
    {
        if (rightController == null || unifiedRay == null) return;

        if (rightController.activateAction == null || !rightController.activateAction.action.enabled)
        {
            Debug.LogError("Activate action not configured!");
            return;
        }

        float activateValue = rightController.activateAction.action.ReadValue<float>();
        Debug.Log($"Activate value: {activateValue}");

        bool pressed = activateValue > 0.5f;

        if (pressed)
        {
            if (heldObject == null)
                TryGrab();
        }
        else
        {
            if (heldObject != null)
                Drop();
        }
    }
    #endregion

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

    // Добавьте эти вспомогательные методы в класс FinalPlayerController

    // Возвращает DeliveryPoint, в зоне которого находится игрок (или null)
    private bool TryGetDeliveryPointInZone(out DeliveryPoint result)
    {
        result = null;
        DeliveryPoint[] points = FindObjectsOfType<DeliveryPoint>();
        if (points == null) return false;
        foreach (var p in points)
        {
            if (p != null && p.IsPlayerInInteractionZone(transform))
            {
                result = p;
                return true;
            }
        }
        return false;
    }

    // Возвращает первого зомби в очереди или ближайшего ожидающего
    private ZombieCustomer FindTargetZombie()
    {
        CustomerQueueManager queue = FindObjectOfType<CustomerQueueManager>();
        if (queue != null)
        {
            ZombieCustomer first = queue.GetFirstWaitingZombie();
            if (first != null) return first;
        }
        // Fallback: ближайший Waiting/Angry зомби
        ZombieCustomer[] all = FindObjectsOfType<ZombieCustomer>();
        ZombieCustomer nearest = null;
        float minDist = float.MaxValue;
        foreach (var z in all)
        {
            if (z == null) continue;
            if (z.currentState == ZombieCustomer.ZombieState.Waiting ||
                z.currentState == ZombieCustomer.ZombieState.GettingAngry ||
                z.currentState == ZombieCustomer.ZombieState.Angry)
            {
                float d = Vector3.Distance(transform.position, z.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = z;
                }
            }
        }
        return nearest;
    }

    // Замените существующий метод HandleObjectInteraction этим
    void HandleObjectInteraction(GameObject obj)
    {
        if (obj == null) return;
        if (obj.GetComponentInParent<WashingMachineWithInventory>() != null) return;
        if (obj.GetComponentInParent<WashingMachineUI>() != null) return;

        Item itemComponent = obj.GetComponent<Item>();
        bool isInventoryItem = (itemComponent != null && itemComponent.item != null);

        if (isInventoryItem)
        {
            // Проверяем, находимся ли мы в зоне доставки
            if (TryGetDeliveryPointInZone(out DeliveryPoint dp))
            {
                ZombieCustomer zombie = FindTargetZombie();
                if (zombie != null)
                {
                    // Пытаемся передать предмет напрямую в DeliveryPoint
                    if (dp.PlaceItem(obj, zombie))
                    {
                        Debug.Log($"✅ Предмет {obj.name} передан в DeliveryPoint для зомби {zombie.name}");
                        // Не уничтожаем obj – DeliveryPoint сама позаботится о нём (переместит, удалит и т.д.)
                        return;
                    }
                    else
                    {
                        Debug.LogWarning("Не удалось передать предмет в DeliveryPoint, добавляем в инвентарь");
                    }
                }
                else
                {
                    Debug.Log("Нет зомби для получения предмета, добавляем в инвентарь");
                }
            }

            // Если не в зоне доставки или передача не удалась – добавляем в инвентарь как обычно
            inventoryManager?.AddItem(itemComponent.item, itemComponent.amount);
            Destroy(obj);
            Debug.Log($"✅ {itemComponent.item.ItemName} добавлен в инвентарь");
        }
        else
        {
            GrabPhysicalObject(obj);
        }
    }
}