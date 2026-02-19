using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class VRMiddleFingerUI : MonoBehaviour
{
    [Header("Ray Interactor")]
    public XRRayInteractor rayInteractor; // интерактор для UI луча

    [Header("Input Action")]
    public InputActionReference middleFingerAction; // Action под средний палец

    private void OnEnable()
    {
        if (middleFingerAction != null)
        {
            middleFingerAction.action.performed += OnMiddleFingerPressed;
            middleFingerAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (middleFingerAction != null)
        {
            middleFingerAction.action.performed -= OnMiddleFingerPressed;
            middleFingerAction.action.Disable();
        }
    }

    private void OnMiddleFingerPressed(InputAction.CallbackContext context)
    {
        TryClickUI();
    }

    private void TryClickUI()
    {
        if (rayInteractor == null) return;

        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject target = hit.collider.gameObject;
            if (target == null) return;

            PointerEventData pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
