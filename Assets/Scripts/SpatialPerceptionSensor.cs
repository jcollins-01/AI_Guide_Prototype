using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SpatialPerceptionSensor : MonoBehaviour
{
    public float viewRadius = 1000.0f; // Represents the "edges of the world" distance, 150
    public float fieldOfViewAngle = 120.0f;  // Restricts vision to a forward cone (human-like peripheral vision)
    public LayerMask obstacleLayer;

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

    /*void PerformSpatialSweep()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, interactableLayer);

        foreach (var hit in hits)
        {
            GameObject obj = hit.gameObject;
            Debug.Log($"Seeing {obj.name}");

            // Cast to the closest physical point on the surface, not the bounding box center
            Vector3 targetCenter = hit.ClosestPoint(playerHeadset.position);
            // Edge case fallback: If the headset is inside the object, ClosestPoint returns the headset position.
            if (targetCenter == playerHeadset.position) targetCenter = hit.bounds.center;

            Vector3 directionToTarget = targetCenter - playerHeadset.position;

            // Field of view check
            float angleToTarget = Vector3.Angle(playerHeadset.forward, directionToTarget);
            if (angleToTarget > fieldOfViewAngle / 2f)
                continue; // Outside FOV

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
    }*/

    void PerformSpatialSweep()
    {
        // 1. Get the mathematical planes of the headset's current field of view
        Camera headsetCam = playerHeadset.GetComponent<Camera>();
        if (headsetCam == null) return; // Ensure the headset has a camera attached

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(headsetCam);
        LayerMask sightMask = interactableLayer | obstacleLayer;

        // 2. Find all interactable objects in the scene (You can cache this list in Start() for better performance)
        // Using FindObjectsOfType is fine for prototyping, but in production, maintain a List of all hazards/interactables
        Collider[] allInteractables = Physics.OverlapSphere(playerHeadset.position, viewRadius, interactableLayer, QueryTriggerInteraction.Ignore);

        foreach (Collider col in allInteractables)
        {
            GameObject obj = col.gameObject;
            if (obj.transform.root == playerHeadset.root) continue;

            // 3. Fast Culling: Is the object's bounding box even inside the camera view?
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, col.bounds))
            {
                // 4. It's in the camera view! Now test for occlusion.
                // Instead of just the center, we define multiple test points on the object's bounds
                Vector3 center = col.bounds.center;
                Vector3 extents = col.bounds.extents;

                Vector3[] testPoints = new Vector3[]
                {
                    center, // Center
                    center + new Vector3(0, extents.y, 0), // Top
                    center + new Vector3(0, -extents.y, 0), // Bottom
                    center + new Vector3(extents.x, 0, 0), // Right
                    center + new Vector3(-extents.x, 0, 0), // Left
                    center + new Vector3(0, extents.y, 0) + (playerHeadset.position - center).normalized * extents.z // Nearest Top Edge
                };

                bool isVisible = false;

                // 5. Fire rays at all points. If even ONE hits the object, it is partially visible!
                foreach (Vector3 point in testPoints)
                {
                    Vector3 directionToPoint = point - playerHeadset.position;
                    float distanceToPoint = directionToPoint.magnitude;

                    if (Physics.Raycast(playerHeadset.position, directionToPoint.normalized, out RaycastHit sightHit, viewRadius, sightMask, QueryTriggerInteraction.Ignore))
                    {
                        if (sightHit.collider.gameObject == obj || sightHit.transform.IsChildOf(obj.transform))
                        {
                            isVisible = true;
                            Debug.DrawLine(playerHeadset.position, sightHit.point, Color.green, 1f);
                            break; // Stop checking this object, we know we can see it
                        }
                    }
                }

                if (isVisible)
                {
                    string id = obj.GetInstanceID().ToString();

                    if (!activeAnchors.ContainsKey(id))
                    {
                        Color uniqueColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
                        ObjectAnchor newAnchor = new ObjectAnchor(obj, obj.name)
                        {
                            uniqueColorID = uniqueColor
                        };
                        activeAnchors.Add(id, newAnchor);

                        Debug.Log($"Guide saw {obj.name}. Assigned Mask Color: #{ColorUtility.ToHtmlStringRGBA(uniqueColor)}");
                    }

                    activeAnchors[id].lastKnownPosition = obj.transform.position;
                }
            }
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
    
    // Helper function specifically to get hand distances while grabbing and check if we've sucessfully grabbed an anchor object
    public float GetHandDistanceToTargetByName(string targetName)
    {
        // Iterate through the active anchors to find the one matching the target name
        foreach (var kvp in activeAnchors)
        {
            ObjectAnchor anchor = kvp.Value;

            // Check if the technical name matches the target
            if (string.Equals(anchor.technicalName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return GetAnchorHandDistance(anchor);
            }

            // Also check if it matches any user-assigned aliases, just in case there was an AI hallucination at some point
            foreach (string alias in anchor.userAliases)
            {
                if (string.Equals(alias, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return GetAnchorHandDistance(anchor);
                }
            }
        }

        // If the object isn't found in the dictionary, return a massive distance 
        // so it never accidentally triggers a false "success" state
        return float.MaxValue;
    }

    // Another helper for grabbing - this one is meant to help us grab the height of an obstacle
    public string GetAnchorHandVerticalOffset(ObjectAnchor obj)
    {
        // Default to the transform position
        Vector3 targetPos = obj.gameObjectReference.transform.position;

        // Use the closest point on the collider if available for accuracy
        Collider col = obj.gameObjectReference.GetComponent<Collider>();
        if (col != null)
        {
            targetPos = col.ClosestPoint(playerHandRight.position);
        }

        // Calculate the difference purely on the Y (Up/Down) axis
        float yDifference = targetPos.y - playerHandRight.position.y;

        // Create a semantic string for the LLM
        if (Mathf.Abs(yDifference) < 0.03f) // If within 3cm, consider it level
        {
            return "Level with hand";
        }
        else if (yDifference > 0)
        {
            return $"{yDifference:F2}m Above hand";
        }
        else
        {
            return $"{Mathf.Abs(yDifference):F2}m Below hand";
        }
    }

    // For when we call with a game object directly
    public float GetObjectRelativeAngle(GameObject obj)
    {
        // Default to the transform position
        Vector3 targetPos = obj.transform.position;

        // Use the closest point on the collider if available for accuracy
        Collider col = obj.GetComponent<Collider>();
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
                sb.AppendLine($"  Vertical Offset from Player's Right Hand: {GetAnchorHandVerticalOffset(anchor)}");
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
        // Run this to clean-up any objects that were destroyed/not properly cleaned in other scripts
        List<string> staleKeys = activeAnchors
            .Where(kvp => kvp.Value.gameObjectReference == null)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string key in staleKeys)
        {
            activeAnchors.Remove(key);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("SPATIAL TELEMETRY OF ALL ENCOUNTERED OBJECTS (Hidden from user):");

        int count = 0;
        foreach (var kvp in activeAnchors)
        {
            ObjectAnchor anchor = kvp.Value;
            sb.AppendLine($"- Registry Name: {anchor.technicalName}");
            sb.AppendLine($"  Distance to Player: {GetAnchorPlayerDistance(anchor):F2}m");
            //sb.AppendLine($"  Distance to Player's Right Hand: {GetAnchorHandDistance(anchor):F2}m");
            //sb.AppendLine($"  Vertical Offset from Player's Right Hand: {GetAnchorHandVerticalOffset(anchor)}");
            //sb.AppendLine($"  Relative Angle to Player: {GetAnchorRelativeAngle(anchor):F0}° (0=Front, 90=Right, 180=Back, 270=Left)");
            //sb.AppendLine($"  Mask Color ID: #{ColorUtility.ToHtmlStringRGBA(anchor.uniqueColorID)}");
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