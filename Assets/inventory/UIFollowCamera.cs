using UnityEngine;

public class UIFollowCamera : MonoBehaviour
{
    [SerializeField] private float distance = 1.5f; // расстояние от камеры
    [SerializeField] private float heightOffset = -0.2f; // смещение по высоте
    [SerializeField] private bool keepUpright = true; // не наклонять HUD по pitch/roll головы

    private Transform cameraTransform;

    void Start()
    {
        Camera cam = Camera.main;
        if (cam != null) cameraTransform = cam.transform;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Позиция: строго от камеры
        Vector3 targetPosition =
            cameraTransform.position +
            cameraTransform.forward * distance +
            Vector3.up * heightOffset;

        transform.position = targetPosition;

        // Поворот: лицом к камере, но по желанию без наклона
        if (keepUpright)
        {
            transform.rotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
        }
        else
        {
            Vector3 toCamera = (cameraTransform.position - transform.position);
            if (toCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(-toCamera);
        }
    }
}