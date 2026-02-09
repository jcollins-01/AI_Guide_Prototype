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
    [Tooltip("Valid landing surfaces. Set to Floors | Water.")]
    public LayerMask teleportableLayers = 0; // set in Inspector

    [Tooltip("Major blockers. Example: Obstacles | Interactable | Person | NPC | Key Items (and maybe Default).")]
    public LayerMask majorObstacleLayers = 0; // set in Inspector

    [Tooltip("Minor blockers with smaller radius. Example: Plants.")]
    public LayerMask minorObstacleLayers = 0; // set in Inspector

    [Tooltip("If you already use a Restricted tag, keep it as a hard override.")]
    public string restrictedTag = "Restricted";

    [Tooltip("Major collision radius in meters.")]
    public float checkRadius = 0.5f;

    [Tooltip("Minor collision radius in meters.")]
    public float minorCheckRadius = 0.12f;

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
        // 1) Floor or water check using raycast is more reliable than CheckSphere alone
        if (!Physics.Raycast(pos + Vector3.up * 1.0f, Vector3.down, out RaycastHit floorHit, 2.5f, teleportableLayers, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("[Provider] No valid teleport surface under destination");
            return false;
        }

        // Use the hit point for slope and collision checks so we are aligned to the real surface
        Vector3 feetPos = floorHit.point;

        // 2) Slope validation
        float slope = Vector3.Angle(floorHit.normal, Vector3.up);
        if (slope > maxSlope)
        {
            Debug.LogWarning($"[Provider] Surface too steep: {slope:F1} degrees");
            return false;
        }

        // 3) Major collision check (big blockers)
        Collider[] majorNearby = Physics.OverlapSphere(feetPos, checkRadius, majorObstacleLayers, QueryTriggerInteraction.Ignore);
        Debug.Log($"[Provider] Major check at {feetPos}, found {majorNearby.Length} colliders within {checkRadius}m");

        foreach (Collider col in majorNearby)
        {
            if (col == null || !col.enabled) continue;

            // Hard override: restricted tag blocks always
            if (!string.IsNullOrEmpty(restrictedTag) && col.CompareTag(restrictedTag))
            {
                Debug.LogWarning($"[Provider] Blocked by Restricted: {col.name}");
                return false;
            }

            float dist = Vector3.Distance(col.ClosestPoint(feetPos), feetPos);
            if (dist < checkRadius)
            {
                Debug.LogWarning($"[Provider] Blocked teleport (major), too close to {col.name} ({dist:F2}m)");
                return false;
            }
        }

        // 4) Minor collision check (small clutter)
        if (minorObstacleLayers.value != 0 && minorCheckRadius > 0f)
        {
            Collider[] minorNearby = Physics.OverlapSphere(feetPos, minorCheckRadius, minorObstacleLayers, QueryTriggerInteraction.Ignore);
            Debug.Log($"[Provider] Minor check at {feetPos}, found {minorNearby.Length} colliders within {minorCheckRadius}m");

            foreach (Collider col in minorNearby)
            {
                if (col == null || !col.enabled) continue;

                if (!string.IsNullOrEmpty(restrictedTag) && col.CompareTag(restrictedTag))
                {
                    Debug.LogWarning($"[Provider] Blocked by Restricted (minor): {col.name}");
                    return false;
                }

                float dist = Vector3.Distance(col.ClosestPoint(feetPos), feetPos);
                if (dist < minorCheckRadius)
                {
                    Debug.LogWarning($"[Provider] Blocked teleport (minor), inside/too close to {col.name} ({dist:F2}m)");
                    return false;
                }
            }
        }

        Debug.Log("[Provider] Teleport destination approved");
        return true;
    }
}
