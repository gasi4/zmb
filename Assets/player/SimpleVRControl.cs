using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SimpleVRControl : MonoBehaviour
{
    public UnifiedRay unifiedRay;
    public TeleportationProvider teleportProvider;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (unifiedRay != null && teleportProvider != null)
            {
                if (unifiedRay.Raycast(out RaycastHit hit, out Ray ray))
                {
                    var request = new TeleportRequest()
                    {
                        destinationPosition = hit.point,
                        destinationRotation = transform.rotation
                    };
                    teleportProvider.QueueTeleportRequest(request);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("Захват предмета!");
            // можно вызвать тот же TryGrabUnified в FinalPlayerController
            // или реализовать аналогичную логику тут
            // Например, попытаемся захватить объект через unifiedRay:
            if (unifiedRay != null)
            {
                if (unifiedRay.Raycast(out RaycastHit hit, out Ray ray))
                {
                    Debug.Log($"VR: Попал в {hit.collider.name}");
                    var rb = hit.collider.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        // инстант-родитель к руке:
                        var rightHand = unifiedRay.rightHandTransform;
                        if (rightHand != null)
                            hit.collider.gameObject.transform.position = rightHand.position + Vector3.forward * 0.2f;
                    }
                }
            }
        }
    }
}
