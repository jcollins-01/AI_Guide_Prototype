using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SpatialPerceptionSensor : MonoBehaviour
{
    public float viewRadius = 5.0f;
    public LayerMask interactableLayer;
    public Transform playerHeadset;
    public Transform playerHandRight; // For tracking grab movements, needs it own thing

    private Dictionary<string, ObjectAnchor> activeAnchors = new Dictionary<string, ObjectAnchor>();

    CameraSystem camSystem;


    void Start()
    {
        // Periodic sensor sweeps mimicking human line-of-sight (cover the area around the guide without having to move its head)
        InvokeRepeating(nameof(PerformSpatialSweep), 0.2f, 0.5f);
    }

    private void Update()
    {
        getCameraSystem();
    }

    void PerformSpatialSweep()
    {
        // Cast a sensory sphere around the guide's line of sight
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, viewRadius, transform.forward, viewRadius, interactableLayer);

        foreach (var hit in hits)
        {
            GameObject obj = hit.collider.gameObject;
            string id = obj.GetInstanceID().ToString();

            if (!activeAnchors.ContainsKey(id))
            {
                // Generate a highly distinct random color for the AI to recognize
                Color uniqueColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);

                // New object encountered! Create an Anchor to track it
                ObjectAnchor newAnchor = new ObjectAnchor(obj, obj.name);
                activeAnchors.Add(id, newAnchor);

                Debug.Log($"Guide encountered a new object {obj} by the name of {obj.name}");
                Debug.Log($"Current number of unique objects we've seen is {GetObjectAnchors()}");

                // Trigger the mask screenshot and save the base64 via callback
                if (camSystem != null)
                {
                    camSystem.CaptureObjectMask(obj, uniqueColor, (base64String) =>
                    {
                        newAnchor.localScreenshotBase64 = base64String;
                        Debug.Log($"Successfully saved mask screenshot for {obj.name}");
                    });
                }
            }

            // Update real-time spatial properties
            activeAnchors[id].lastKnownPosition = obj.transform.position;
        }
    }

    // Returns the number of objects we've encoutered so far
    public int GetObjectAnchors()
    {
        return activeAnchors.Count;
    }

    public float GetAnchorPlayerDistance(ObjectAnchor obj)
    {
        float distanceToPlayer = Vector3.Distance(playerHeadset.position, obj.gameObjectReference.transform.position);
        return distanceToPlayer;
    }

    public float GetAnchorHandDistance(ObjectAnchor obj)
    {
        float distanceToHand = Vector3.Distance(playerHandRight.position, obj.gameObjectReference.transform.position);
        return distanceToHand;
    }

    // Call this from your Realtime WebSocket client when the AI learns a new name
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

    // Resolves queries like "Go to the blue-striped building" - do a keyword search and see if a known alias matches an object
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
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("CURRENT SPATIAL TELEMETRY (Hidden from user):");

        int count = 0;
        foreach (var kvp in activeAnchors)
        {
            ObjectAnchor anchor = kvp.Value;

            // Only include objects within a relevant radius to save tokens
            if (GetAnchorPlayerDistance(anchor) <= 5.0f)
            {
                sb.AppendLine($"- Registry Name: {anchor.technicalName}");
                sb.AppendLine($"  Distance to Player: {GetAnchorPlayerDistance(anchor):F2}m");
                sb.AppendLine($"  Distance to Right Hand: {GetAnchorHandDistance(anchor):F2}m");

                // If you have the mask color, you can pass it here so the vision model can cross-reference it
                sb.AppendLine($"  Mask Color ID: {ColorUtility.ToHtmlStringRGBA(anchor.uniqueColorID)}");
                count++;
            }
        }

        if (count == 0)
        {
            sb.AppendLine("No recognized objects within immediate vicinity.");
        }

        return sb.ToString();
    }

    // For when we are fulfilling a navigation request that may require us to access objects beyond what we can see, but are things we've encountered
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
            sb.AppendLine($"  Distance to Right Hand: {GetAnchorHandDistance(anchor):F2}m");
            sb.AppendLine($"  Mask Color ID: {ColorUtility.ToHtmlStringRGBA(anchor.uniqueColorID)}");
            count++;
        }

        if (count == 0)
        {
            sb.AppendLine("No recognized objects within immediate vicinity.");
        }

        return sb.ToString();
    }

    private void getCameraSystem()
    {
        if (camSystem == null)
        {
            SharedMovement m_SharedMovementScript = FindObjectOfType<SharedMovement>();
            // Can grab the camSystem once the shared movement script has been added + has added its own camera
            if (m_SharedMovementScript != null)
                camSystem = m_SharedMovementScript.camera;
        }
    }
}