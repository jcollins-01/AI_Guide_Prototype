using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.Interaction.Toolkit;

[AddComponentMenu("XR/Locomotion/Custom Teleportation Provider (Debug Safe)")]
public class CustomTeleportationProvider : TeleportationProvider
{
    protected new TeleportRequest currentRequest { get; set; }
    [System.NonSerialized] protected new bool validRequest;

    [System.NonSerialized] float m_DelayTime; // prevents duplicate serialization

    [Header("Teleport Validation")]
    public LayerMask teleportableLayers = 1 << 10; // Floors
    public LayerMask obstacleLayers = ~0;          // Everything
    public float checkRadius = 0.5f;
    public float maxSlope = 10f;

    public float delayTimeCustom = 0.1f; // if you need editable delay time, use this one

    public override bool QueueTeleportRequest(TeleportRequest teleportRequest)
    {
        Debug.Log($"[Provider] QueueTeleportRequest called. Destination = {teleportRequest.destinationPosition}");
        currentRequest = teleportRequest;
        validRequest = true;
        return true;
    }

    [System.NonSerialized] bool m_HasExclusiveLocomotion;
    [System.NonSerialized] float m_TimeStarted = -1f;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[Provider] Awake");
    }

    protected virtual void OnEnable() => Debug.Log("[Provider] OnEnable");
    protected virtual void OnDisable() => Debug.Log("[Provider] OnDisable");

    protected override void Update()
    {
        Debug.Log($"[Provider] Update - validRequest={validRequest}, locomotionPhase={locomotionPhase}");

        if (!validRequest)
        {
            locomotionPhase = LocomotionPhase.Idle;
            return;
        }

        if (!m_HasExclusiveLocomotion)
        {
            Debug.Log("[Provider] Attempting BeginLocomotion()");
            if (!BeginLocomotion())
            {
                Debug.Log("[Provider] BeginLocomotion() failed");
                return;
            }

            m_HasExclusiveLocomotion = true;
            locomotionPhase = LocomotionPhase.Started;
            m_TimeStarted = Time.time;
            Debug.Log("[Provider] Locomotion started successfully");
        }

        if (delayTimeCustom > 0f && Time.time - m_TimeStarted < delayTimeCustom)
        {
            Debug.Log($"[Provider] Waiting delay ({Time.time - m_TimeStarted}/{delayTimeCustom})");
            return;
        }

        locomotionPhase = LocomotionPhase.Moving;
        Debug.Log("[Provider] Moving phase reached");

        if (!IsTeleportDestinationValid(currentRequest.destinationPosition))
        {
            Debug.LogWarning("[Provider] Teleport blocked by validation");
            EndLocomotion();
            m_HasExclusiveLocomotion = false;
            validRequest = false;
            locomotionPhase = LocomotionPhase.Idle;
            return;
        }

        var xrOrigin = system?.xrOrigin;
        if (xrOrigin != null)
        {
            Debug.Log("[Provider] Moving XR Origin");
            var heightAdjustment = xrOrigin.Origin.transform.up * xrOrigin.CameraInOriginSpaceHeight;
            var cameraDestination = currentRequest.destinationPosition + heightAdjustment;
            xrOrigin.MoveCameraToWorldLocation(cameraDestination);
        }
        else
        {
            Debug.LogWarning("[Provider] XR Origin is null!");
        }

        EndLocomotion();
        m_HasExclusiveLocomotion = false;
        validRequest = false;
        locomotionPhase = LocomotionPhase.Done;
        Debug.Log("[Provider] Teleportation completed");
    }

    bool IsTeleportDestinationValid(Vector3 pos)
    {
        float minDistance = 0.5f; // Minimum allowed distance from objects
        int layerMask = ~0; // Check all layers
        Collider[] nearby = Physics.OverlapSphere(pos, minDistance, layerMask, QueryTriggerInteraction.Ignore);

        Debug.Log($"[Provider] Checking teleport at {pos}, found {nearby.Length} colliders within {minDistance}m");

        foreach (Collider col in nearby)
        {
            if (col == null) continue;

            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            Debug.Log($"[Provider] Colliders Detected: {col.name} (Layer: {layerName}) on {col.gameObject.name}");
        }

        foreach (Collider col in nearby)
        {
            if (!col.enabled) continue;

            // Skip floor or teleport-related surfaces
            if (col.CompareTag("Travel Target") ||  col.CompareTag("Everything"))
            {
                Debug.Log($"[Provider] Ignoring floor collider: {col.name}");
                continue;
            }

            // Skip layers you mark as safe
            int layer = col.gameObject.layer;
            if (layer == LayerMask.NameToLayer("Teleportable") ||
                layer == LayerMask.NameToLayer("Ground") ||
                layer == LayerMask.NameToLayer("Ignore Raycast") ||
                layer == LayerMask.NameToLayer("Floors"))
            {
                Debug.Log($"[Provider] Ignoring safe layer collider: {col.name}");
                continue;
            }

            // Check actual distance
            float dist = Vector3.Distance(col.ClosestPoint(pos), pos);
            if (dist < minDistance)
            {
                Debug.LogWarning($"[Provider] Blocked teleport, too close to {col.name} ({dist:F2}m)");
                return false;
            }
        }

        // Standard floor check
        bool onFloor = Physics.CheckSphere(pos, 0.05f, teleportableLayers);
        Debug.Log($"[Provider] OnFloor={onFloor}");
        if (!onFloor)
        {
            Debug.LogWarning("[Provider] Not on a valid floor surface");
            return false;
        }

        // Slope validation
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hit, 2f, teleportableLayers))
        {
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > maxSlope)
            {
                Debug.LogWarning($"[Provider] Surface too steep: {slope:F1}°");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("[Provider] No floor raycast hit");
            return false;
        }

        Debug.Log("[Provider] Teleport destination approved");
        return true;
    }




}
