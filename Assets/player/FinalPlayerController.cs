using UnityEngine;
using UnityEngine.EventSystems;
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
    [Tooltip("Экшен для правого стика (Vector2). В XRI обычно: XRI RightHand/Turn или похожий.")]
    public InputActionProperty rightStick;

    [Tooltip("Если задан — используется для взгляда вместо rightStick.")]
    public InputActionProperty lookAction;

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
    public float lookSensitivity = 90f; // градусов/сек при значении стика = 1
    public float maxPitch = 80f;

    [Header("Inventory")]
    public InventoryManager inventoryManager;
    public KeyCode inventoryToggleKey = KeyCode.Tab;
    public KeyCode pickupKey = KeyCode.E; // Кнопка для подбора предметов
    private GameObject heldObject = null;
    private Rigidbody heldRigidbody = null;
    private float xRotation = 0f;
    private float yaw = 0f;

    [Header("VR Inventory Toggle (Action-based)")]
    public InputActionProperty vrInventoryToggleAction; // кнопка на левом контроллере
    public float vrToggleDebounce = 0.25f;
    private float _nextAllowedVrToggleTime;
    private bool _vrInventoryWasPressed;

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

        // VR Inventory: гарантируем, что экшен включён
        if (vrModeActive && vrInventoryToggleAction.action != null && !vrInventoryToggleAction.action.enabled)
        {
            vrInventoryToggleAction.action.Enable();
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

            if (playerCamera != null)
            {
                Vector3 e = playerCamera.localEulerAngles;
                yaw = e.y;
                xRotation = NormalizeAngle(e.x);
            }
        }
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void Update()
    {
        if (vrModeActive)
        {
            HandleVRLook();
            HandleVRInventoryToggle();
        }

        // Editor fallback
        if (Input.GetKeyDown(inventoryToggleKey))
            inventoryManager?.ToggleInventory();
    }

    // ===================== НОВАЯ ЛОГИКА КНОПКИ ИНВЕНТАРЯ =====================
    void HandleVRInventoryToggle()
    {
        if (inventoryManager == null) return;
        if (vrInventoryToggleAction.action == null) return;

        if (!vrInventoryToggleAction.action.enabled)
            vrInventoryToggleAction.action.Enable();

        if (Time.time < _nextAllowedVrToggleTime) return;

        bool pressedNow = vrInventoryToggleAction.action.ReadValue<float>() > 0.5f;

        if (pressedNow && !_vrInventoryWasPressed)
        {
            if (inventoryManager.isOpened)
            {
                // Если инвентарь открыт — просто закрываем
                inventoryManager.ToggleInventory();
            }
            else
            {
                // Если закрыт — пытаемся положить предмет из руки
                bool stored = TryStoreHeldItemInInventory();
                if (!stored)
                {
                    // Не удалось положить — открываем инвентарь
                    inventoryManager.ToggleInventory();
                }
            }

            _nextAllowedVrToggleTime = Time.time + vrToggleDebounce;
            _vrInventoryWasPressed = true;
        }
        else if (!pressedNow && _vrInventoryWasPressed)
        {
            _vrInventoryWasPressed = false;
        }
    }

    // Пытается положить предмет из руки в инвентарь. Возвращает true, если успешно.
    private bool TryStoreHeldItemInInventory()
    {
        if (inventoryManager == null) return false;

        // Случай 1: в руке предмет из инвентаря (визуал)
        if (HasInventoryItemInHand())
        {
            ItemScriptableObject item = heldInventoryItem;
            int amount = 1; // или используйте реальное количество, если оно хранится

            if (inventoryManager.TryAddItemToEmptySlot(item, amount))
            {
                ClearHeldItem();
                return true;
            }
            return false;
        }

        // Случай 2: в руке физический объект, который можно подобрать в инвентарь
        if (heldObject != null)
        {
            Item itemComp = heldObject.GetComponent<Item>();
            if (itemComp != null && itemComp.item != null)
            {
                if (inventoryManager.TryAddItemToEmptySlot(itemComp.item, itemComp.amount))
                {
                    Destroy(heldObject);
                    ClearHeldItem();
                    return true;
                }
            }
        }

        return false;
    }
    // ===================== КОНЕЦ НОВОЙ ЛОГИКИ =====================

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
    }

    void HandleVRLook()
    {
        if (playerCamera == null) return;

        InputAction action = lookAction.action != null ? lookAction.action : rightStick.action;
        if (action == null || !action.enabled) return;

        Vector2 stick = action.ReadValue<Vector2>();
        if (stick.sqrMagnitude < 0.0001f) return;

        float dt = Time.deltaTime;

        yaw += stick.x * lookSensitivity * dt;
        xRotation -= stick.y * lookSensitivity * dt;
        xRotation = Mathf.Clamp(xRotation, -maxPitch, maxPitch);

        playerCamera.localRotation = Quaternion.Euler(xRotation, yaw, 0f);
    }

    // ДОБАВЛЕНО: Метод для попытки положить вещь на Delivery Point
    void TryPlaceOnDeliveryPoint()
    {
        if (heldObject == null)
        {
            Debug.Log("В руке нет вещи для класть на точку!");
            return;
        }

        DeliveryPoint[] points = FindObjectsOfType<DeliveryPoint>();
        if (points == null || points.Length == 0)
        {
            Debug.Log("Не найден Delivery Point на сцене!");
            return;
        }

        DeliveryPoint deliveryPoint = null;
        bool selectedByZone = false;

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].IsPlayerInInteractionZone(transform))
            {
                deliveryPoint = points[i];
                selectedByZone = true;
                break;
            }
        }

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

        if (!selectedByZone)
        {
            float distance = Vector3.Distance(transform.position, deliveryPoint.transform.position);
            if (distance > deliveryInteractionRange)
            {
                Debug.Log($"Слишком далеко от Delivery Point! Дистанция: {distance:F1}, нужно: {deliveryInteractionRange}");
                return;
            }
        }

        Item itemComponent = heldObject.GetComponent<Item>();
        if (itemComponent == null)
        {
            if (heldInventoryItem != null && heldInventoryItem.WorldPrefab != null)
            {
                GameObject worldItem = Instantiate(heldInventoryItem.WorldPrefab);
                worldItem.transform.position = heldObject.transform.position;
                worldItem.transform.rotation = heldObject.transform.rotation;

                itemComponent = worldItem.GetComponent<Item>();
                if (itemComponent == null) itemComponent = worldItem.AddComponent<Item>();

                itemComponent.item = heldInventoryItem;
                itemComponent.amount = 1;

                Destroy(heldObject);
                heldObject = worldItem;
            }
            else
            {
                Debug.Log("У объекта нет компонента Item и нет WorldPrefab у предмета из инвентаря!");
                return;
            }
        }

        ZombieCustomer nearestZombie = FindNearestWaitingZombie();
        if (nearestZombie == null)
        {
            Debug.Log("Нет зомби, ожидающих вещь!");
            return;
        }

        if (deliveryPoint.PlaceItem(heldObject, nearestZombie))
        {
            Debug.Log($"✅ Вещь {heldObject.name} положена на Delivery Point для зомби {nearestZombie.name}");
            ClearHeldItem();
        }
        else
        {
            Debug.Log("Не удалось положить вещь на Delivery Point!");
        }
    }

    ZombieCustomer FindNearestWaitingZombie()
    {
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

    bool IsWashingMachineUIOpen()
    {
        WashingMachineWithInventory[] machines = FindObjectsOfType<WashingMachineWithInventory>();
        foreach (var machine in machines)
        {
            if (machine.machineCanvas != null && machine.machineCanvas.gameObject.activeSelf)
                return true;
        }

        WashingMachineUI[] oldMachines = FindObjectsOfType<WashingMachineUI>();
        foreach (var machine in oldMachines)
        {
            if (machine.panel != null && machine.panel.activeSelf)
                return true;
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

    void UpdateHeldObjectEditor() { }
    #endregion

    #region Item Pickup System
    void TryPickupItem()
    {
        if (unifiedRay == null) return;

        if (unifiedRay.Raycast(out RaycastHit hit, out Ray ray))
        {
            HandleHitObject(hit);
        }
    }

    void HandleHitObject(RaycastHit hit)
    {
        GameObject hitObject = hit.collider.gameObject;
        Debug.Log($"🎯 Луч попал в: {hitObject.name}");

        var machine = hitObject.GetComponentInParent<WashingMachineWithInventory>();
        if (machine != null)
        {
            Debug.Log("🧺 Открываю UI новой стиралки");
            machine.OpenMachineUI();

            if (unifiedRay != null) unifiedRay.enabled = false;
            SetInputEnabled(false);
            return;
        }

        var oldMachine = hitObject.GetComponentInParent<WashingMachineUI>();
        if (oldMachine != null)
        {
            Debug.Log("🧺 Открываю UI старой стиралки");
            oldMachine.ToggleMenu();

            if (unifiedRay != null) unifiedRay.enabled = false;
            SetInputEnabled(false);
            return;
        }

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

    void GrabPhysicalObject(GameObject obj)
    {
        if (obj == null) return;

        if (obj.GetComponentInParent<WashingMachineWithInventory>() != null) return;
        if (obj.GetComponentInParent<WashingMachineUI>() != null) return;

        Item itemComponent = obj.GetComponent<Item>();
        bool isInventoryItem = (itemComponent != null && itemComponent.item != null);

        if (isInventoryItem)
        {
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
        Vector3 inverseScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        anchor.transform.localScale = inverseScale;

        anchor.transform.localPosition = new Vector3(0f, -0.1f, 20f);
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

    void HandleGrabVR()
    {
        if (rightController == null || unifiedRay == null) return;
        if (rightController.selectAction == null) return;

        bool pressed = rightController.selectAction.action.ReadValue<float>() > 0.5f;

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