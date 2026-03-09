using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[AddComponentMenu("XR/Teleportation Area (Debug)")]
public class TeleportationArea : BaseTeleportationInteractable
{
    [Tooltip("Which layers are valid for teleportation (e.g., floors)")]
    public LayerMask teleportableLayers;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (teleportationProvider == null)
        {
            teleportationProvider = FindObjectOfType<CustomTeleportationProvider>();
            //Debug.Log($"[Area] Auto-linked provider: {teleportationProvider}");
        }
        else
        {
            //Debug.Log($"[Area] Provider already set: {teleportationProvider}");
        }
    }

    protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request)
    {
        if (hit.collider.CompareTag("Restricted"))
        {
            Debug.LogWarning("[Area] Cannot teleport here - restricted surface.");
            return false;
        }

        request.destinationPosition = hit.point;
        request.destinationRotation = transform.rotation;
        return true;
    }


    protected override void OnDisable()
    {
        base.OnDisable();
        //Debug.Log("[Area] Disabled");
    }
}
