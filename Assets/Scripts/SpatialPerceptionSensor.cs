using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SpatialPerceptionSensor : MonoBehaviour
{
    public float viewRadius = 30.0f;
    public LayerMask interactableLayer;
    public Transform playerHeadset;
    public Transform playerHandRight;

    // Changed to public so CameraSystem can access the anchors for batch masking
    public Dictionary<string, ObjectAnchor> activeAnchors = new Dictionary<string, ObjectAnchor>();

    CameraSystem camSystem;
    public Material unlitMaskMaterialBase;

    void Start()
    {
        InvokeRepeating(nameof(PerformSpatialSweep), 0.2f, 0.5f);
    }

    private void Update()
    {
        getCameraSystem();
    }

    void PerformSpatialSweep()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, interactableLayer);

        foreach (var hit in hits)
        {
            GameObject obj = hit.gameObject;
            string id = obj.GetInstanceID().ToString();

            if (!activeAnchors.ContainsKey(id))
            {
                // Generate a highly distinct random color
                Color uniqueColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);

                ObjectAnchor newAnchor = new ObjectAnchor(obj, obj.name);
                newAnchor.uniqueColorID = uniqueColor; // Ensure the color is saved to the anchor
                activeAnchors.Add(id, newAnchor);

                Debug.Log($"Guide encountered {obj.name}. Assigned Mask Color: #{ColorUtility.ToHtmlStringRGBA(uniqueColor)}");
                Debug.Log($"Number of objects we've seen is now {GetObjectAnchors()}");
            }

            activeAnchors[id].lastKnownPosition = obj.transform.position;
        }
    }

    // Call this from AI Guide right before talking to the LLM
    public void RequestVisualTelemetry(Action onComplete)
    {
        if (camSystem != null)
        {
            // Triggers the batch capture in CameraSystem, passing the active anchors
            camSystem.CaptureMaskedScenes(activeAnchors, onComplete);
        }
        else
        {
            Debug.LogError("CameraSystem not found. Cannot capture scene masks.");
            onComplete?.Invoke();
        }
    }

    public void RequestHandTelemetry(Action onComplete)
    {
        if (camSystem != null)
        {
            // Triggers the batch capture in CameraSystem, passing the active anchors
            camSystem.CaptureMaskedHands(activeAnchors, onComplete);
        }
        else
        {
            Debug.LogError("CameraSystem not found. Cannot capture hand masks.");
            onComplete?.Invoke();
        }
    }

    public int GetObjectAnchors()
    {
        return activeAnchors.Count;
    }

    public float GetAnchorPlayerDistance(ObjectAnchor obj)
    {
        Collider col = obj.gameObjectReference.GetComponent<Collider>();
        if (col != null)
        {
            // Gets the closest point on the collider to the headset
            Vector3 closestPoint = col.ClosestPoint(playerHeadset.position);
            return Vector3.Distance(playerHeadset.position, closestPoint);
        }

        // Fallback just in case the object has no collider
        return Vector3.Distance(playerHeadset.position, obj.gameObjectReference.transform.position);
    }

    public float GetAnchorHandDistance(ObjectAnchor obj)
    {
        Collider col = obj.gameObjectReference.GetComponent<Collider>();
        if (col != null)
        {
            // Gets the closest point on the collider to the hand
            Vector3 closestPoint = col.ClosestPoint(playerHandRight.position);
            return Vector3.Distance(playerHandRight.position, closestPoint);
        }

        return Vector3.Distance(playerHandRight.position, obj.gameObjectReference.transform.position);
    }

    public float GetAnchorRelativeAngle(ObjectAnchor obj)
    {
        // Default to the transform position
        Vector3 targetPos = obj.gameObjectReference.transform.position;

        // Use the closest point on the collider if available for accuracy
        Collider col = obj.gameObjectReference.GetComponent<Collider>();
        if (col != null)
        {
            targetPos = col.ClosestPoint(playerHeadset.position);
        }

        // Get the direction from the player's headset to the object
        Vector3 directionToTarget = targetPos - playerHeadset.position;

        // Flatten the Y-axis so height differences don't skew the angle
        directionToTarget.y = 0;
        Vector3 forward = playerHeadset.forward;
        forward.y = 0;

        // Calculate the signed angle (-180 to 180 degrees) around the Up axis
        float angle = Vector3.SignedAngle(forward, directionToTarget, Vector3.up);

        // Normalize to a 0-360 degree range for easier LLM interpretation
        if (angle < 0)
        {
            angle += 360f;
        }

        return angle;
    }

    public void AddAliasToAnchor(GameObject target, string newAlias)
    {
        string id = target.GetInstanceID().ToString();
        if (activeAnchors.TryGetValue(id, out ObjectAnchor anchor))
        {
            if (!anchor.userAliases.Contains(newAlias.ToLower()))
            {
                anchor.userAliases.Add(newAlias.ToLower());
            }
        }
    }

    public GameObject ResolveObjectByAlias(string query)
    {
        string lowerQuery = query.ToLower();
        foreach (var anchor in activeAnchors.Values)
        {
            if (anchor.technicalName.ToLower() == lowerQuery) return anchor.gameObjectReference;

            foreach (var alias in anchor.userAliases)
            {
                if (lowerQuery.Contains(alias) || alias.Contains(lowerQuery))
                {
                    return anchor.gameObjectReference;
                }
            }
        }
        return null;
    }

    public string GetDynamicSpatialContext()
    {
        // Run this to clean-up any objects that were destroyed/not properly cleaned in other scripts
        List<string> staleKeys = activeAnchors
            .Where(kvp => kvp.Value.gameObjectReference == null)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string key in staleKeys)
        {
            activeAnchors.Remove(key);
        }

        // Now get the spatial context with the cleaned list
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("CURRENT SPATIAL TELEMETRY (Hidden from user):");

        int count = 0;
        foreach (var kvp in activeAnchors)
        {
            ObjectAnchor anchor = kvp.Value;

            if (GetAnchorPlayerDistance(anchor) <= viewRadius)
            {
                sb.AppendLine($"- Registry Name: {anchor.technicalName}");
                sb.AppendLine($"  Distance to Player: {GetAnchorPlayerDistance(anchor):F2}m");
                sb.AppendLine($"  Distance to Player's Right Hand: {GetAnchorHandDistance(anchor):F2}m");
                sb.AppendLine($"  Relative Angle to Player: {GetAnchorRelativeAngle(anchor):F0}° (0=Front, 90=Right, 180=Back, 270=Left)");
                sb.AppendLine($"  Mask Color ID: #{ColorUtility.ToHtmlStringRGBA(anchor.uniqueColorID)}");
                if (anchor.userAliases.Count > 0)
                {
                    sb.AppendLine($"  Known Aliases: {string.Join(", ", anchor.userAliases)}");
                }
                count++;
            }
        }

        if (count == 0) sb.AppendLine("No recognized objects within immediate vicinity.");

        return sb.ToString();
    }

    public string GetAllSpatialContext()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("SPATIAL TELEMETRY OF ALL ENCOUNTERED OBJECTS (Hidden from user):");

        int count = 0;
        foreach (var kvp in activeAnchors)
        {
            ObjectAnchor anchor = kvp.Value;
            sb.AppendLine($"- Registry Name: {anchor.technicalName}");
            sb.AppendLine($"  Distance to Player: {GetAnchorPlayerDistance(anchor):F2}m");
            sb.AppendLine($"  Distance to Player's Right Hand: {GetAnchorHandDistance(anchor):F2}m");
            sb.AppendLine($"  Relative Angle to Player: {GetAnchorRelativeAngle(anchor):F0}° (0=Front, 90=Right, 180=Back, 270=Left)");
            sb.AppendLine($"  Mask Color ID: #{ColorUtility.ToHtmlStringRGBA(anchor.uniqueColorID)}");
            if (anchor.userAliases.Count > 0)
            {
                sb.AppendLine($"  Known Aliases: {string.Join(", ", anchor.userAliases)}");
            }
            count++;
        }

        if (count == 0) sb.AppendLine("No recognized objects within immediate vicinity.");

        return sb.ToString();
    }

    private void getCameraSystem()
    {
        if (camSystem == null)
        {
            SharedMovement m_SharedMovementScript = FindObjectOfType<SharedMovement>();
            if (m_SharedMovementScript != null)
                camSystem = m_SharedMovementScript.camera;
        }
    }
}