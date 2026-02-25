using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRCollisionHandler : MonoBehaviour
{
    [Header("������")]
    public Transform cameraTransform;
    public Transform cameraOffset;

    [Header("����������")]
    public float gravity = 9.81f;

    private CharacterController cc;
    private Vector3 lastCameraLocalPos;
    private float verticalVelocity = 0f;

    void Start()
    {
        cc = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraOffset == null && cameraTransform != null)
            cameraOffset = cameraTransform.parent;

        if (cameraTransform != null)
            lastCameraLocalPos = transform.InverseTransformPoint(cameraTransform.position);
    }

    void Update()
    {
        if (cameraTransform == null) return;

        MoveWithCamera();
        ApplyGravity();
    }

    void MoveWithCamera()
    {
        Vector3 cameraLocalPos = transform.InverseTransformPoint(cameraTransform.position);

        Vector3 delta = new Vector3(
            cameraLocalPos.x - lastCameraLocalPos.x,
            0f,
            cameraLocalPos.z - lastCameraLocalPos.z
        );

        if (delta.magnitude > 0.001f)
        {
            Vector3 worldDelta = transform.TransformDirection(delta);
            cc.Move(worldDelta);

            Vector3 actualCameraLocal = transform.InverseTransformPoint(cameraTransform.position);
            Vector3 diff = new Vector3(
                cameraLocalPos.x - actualCameraLocal.x,
                0f,
                cameraLocalPos.z - actualCameraLocal.z
            );

            if (diff.magnitude > 0.01f && cameraOffset != null)
                cameraOffset.localPosition -= diff;
        }

        lastCameraLocalPos = transform.InverseTransformPoint(cameraTransform.position);
    }

    void ApplyGravity()
    {
        if (cc.isGrounded)
            verticalVelocity = -0.5f;
        else
            verticalVelocity -= gravity * Time.deltaTime;

        cc.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }
}