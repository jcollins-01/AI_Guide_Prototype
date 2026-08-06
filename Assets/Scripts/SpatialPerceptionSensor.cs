using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class SpatialPerceptionSensor : MonoBehaviour
{
    public float viewRadius = 150.0f; // Represents the "edges of the world" distance, 150
    public float fieldOfViewAngle = 120.0f;  // Restricts vision to a forward cone (human-like peripheral vision)

    public LayerMask interactableLayer;
    public Transform playerHeadset;
    public Transform playerHandRight;

    // Changed to public so CameraSystem can access the anchors for batch masking
    public Dictionary<string, ObjectAnchor> activeAnchors = new Dictionary<string, ObjectAnchor>();

    CameraSystem camSystem;
    public Material unlitMaskMaterialBase;

    // Color mapping system
    public Camera semanticCamera;
    public Shader unlitColorShader;

    // Registries for mapping colors back to GameObjects
    private Dictionary<Color32, GameObject> globalObjectRegistry = new Dictionary<Color32, GameObject>();
    private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>();
    private Dictionary<GameObject, Material> semanticMaterials = new Dictionary<GameObject, Material>();

    void Start()
    {
        if (semanticCamera != null)
        {
            semanticCamera.enabled = false; // Prevents Unity from auto-rendering it every frame
        }

        InitializeSemanticRegistry(); // find all the key items and assign them a special color

        InvokeRepeating(nameof(PerformSpatialSweep), 0.5f, 0.5f); // lower repeat rate to prevent frame queuing 
    }

    private void Update()
    {
        getCameraSystem();
    }

    void SaveSnapshotToPNG()
    {
        if (semanticCamera.targetTexture == null) return;

        RenderTexture rt = semanticCamera.targetTexture;
        RenderTexture.active = rt;

        // Create a new Texture2D and read the active Render Texture into it
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // Encode to PNG and save to your Assets folder
        byte[] bytes = tex.EncodeToPNG();
        string filepath = Application.dataPath + "/SemanticVisionDebug.png";
        System.IO.File.WriteAllBytes(filepath, bytes);

        Debug.Log($"[Perception Sensor] SAVED SNAPSHOT TO: {filepath}");
    }

    // Scan the environment and assign a secret color ID to every potential interactable
    void InitializeSemanticRegistry()
    {
        Debug.Log("[Perception Sensor] Doing semantic sweep and Blackout pass");

        // Create a universal blackout material to add to ANYTHING that's not on the interactable layer
        Material blackoutMat = new Material(unlitColorShader);
        blackoutMat.color = Color.black;

        // Find all the renderers in the scene - literally anything with an appearance
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        Collider[] allInteractables = Physics.OverlapSphere(Vector3.zero, 100000, interactableLayer, QueryTriggerInteraction.Ignore);
        HashSet<GameObject> interactableObjects = new HashSet<GameObject>();

        // Log our interactables for easy checking
        foreach (Collider col in allInteractables)
        {
            interactableObjects.Add(col.gameObject);
        }

        // Assign the colors and the blackout materials to all the objects
        foreach (Renderer r in allRenderers)
        {
            GameObject obj = r.gameObject;
            if (obj.transform.root == playerHeadset.root) continue; // Skip player
            if (semanticMaterials.ContainsKey(obj)) continue; // Skip if already processed

            if (interactableObjects.Contains(obj))
            {
                // It's a target! Generate a spaced, Linear-corrected color (to fix the color mismatch errors)
                Color expectedRenderColor;
                Color originalColorFloat;
                bool isColorSafe;

                // Start the process to generate unique, distinct colors for all objects
                do
                {
                    isColorSafe = true;
                    // Generate a bright, float-based color
                    originalColorFloat = new Color(
                        UnityEngine.Random.Range(0.2f, 1.0f),
                        UnityEngine.Random.Range(0.2f, 1.0f),
                        UnityEngine.Random.Range(0.2f, 1.0f),
                        1.0f);

                    // Pre-calculate the gamma shift - this is the exact value the camera will see!
                    // If we don't do this, then the color shifts upon camera render and they never line up
                    expectedRenderColor = originalColorFloat.linear;

                    // Ensure this color is safely distant (at least 40 units) from all other assigned colors
                    foreach (Color32 existingColor in globalObjectRegistry.Keys)
                    {
                        Color32 newColor32 = expectedRenderColor;
                        float dist = Mathf.Abs(newColor32.r - existingColor.r) +
                                     Mathf.Abs(newColor32.g - existingColor.g) +
                                     Mathf.Abs(newColor32.b - existingColor.b);

                        if (dist < 40) // If it's too close, reject it and generate a new one
                        {
                            isColorSafe = false;
                            break;
                        }
                    }
                } while (!isColorSafe);

                // Store the SHIFTED color in the registry
                globalObjectRegistry.Add(expectedRenderColor, obj);

                // Apply the ORIGINAL color to the material (Unity will shift it down to match the registry)
                Material semanticMat = new Material(unlitColorShader);
                semanticMat.color = originalColorFloat;

                semanticMaterials.Add(obj, semanticMat);
                originalMaterials.Add(obj, r.materials);
            }
            else
            {
                // It's a background/non-interactable, so we apply the blackout material
                // This ensures it occludes objects behind it, but doesn't create random false-positive colors
                // (like when it was seeing the city bank that was behind us in the colors of the kitchen in front)
                semanticMaterials.Add(obj, blackoutMat);
                originalMaterials.Add(obj, r.materials);
            }
        }
    }

    void PerformSpatialSweep()
    {
        if (semanticCamera == null || semanticCamera.targetTexture == null) return; // have to have the color mask cam set up
        //Debug.Log("[Perception Sensor] Doing spatial sweep");

        // Prepare the scene to take a color-mapped snapshot instead of a physics raycast
        foreach (var kvp in semanticMaterials)
        {
            GameObject obj = kvp.Key;
            if (obj == null) continue; // should have an object to apply our color map mat to
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                // Force all materials on the mesh to become the solid semantic color from our color mappings
                Material[] solidMats = new Material[r.materials.Length];
                Array.Fill(solidMats, kvp.Value);
                r.materials = solidMats;
            }
        }

        // Take the color-mapped snapshot
        semanticCamera.Render();
        //Debug.Log($"[Perception Sensor] Re-mapped colors and took snapshot");

        // ONLY DO THIS TO DEBUG! (Saving files during a repeating invoke freezes everything)
        //SaveSnapshotToPNG();

        // Restore the scene immediately to original materials, so player never sees the color swap
        foreach (var kvp in originalMaterials)
        {
            GameObject obj = kvp.Key;
            if (obj == null) continue;
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.materials = kvp.Value;
            }
        }

        // Ask the GPU to send the pixels to the CPU asynchronously - prevents freezing the game by taking lots of screenshots
        // allows us to share the info in pieces when it's manageable
        AsyncGPUReadback.Request(semanticCamera.targetTexture, 0, TextureFormat.RGBA32, OnCompleteReadback);

        /*Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, interactableLayer);

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
        }*/
    }

    // Process the pixels in our color-mapped screenshots to see what we see
    void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        //Debug.Log($"[Perception Sensor] RFinished the readback");
        if (request.hasError)
        {
            Debug.LogError("GPU Readback error.");
            return;
        }

        NativeArray<Color32> pixels = request.GetData<Color32>();

        // Pluck out only the unique, non-black colors in this specific snapshot
        // Instead of running complex math on 65,000 pixels, this whittles it down to maybe 5 to 50 colors
        HashSet<Color32> uniqueColorsInFrame = new HashSet<Color32>();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];

            // Skip pure black background / background objects
            if (pixel.r == 0 && pixel.g == 0 && pixel.b == 0) continue;

            uniqueColorsInFrame.Add(pixel);
        }

        HashSet<GameObject> visibleObjectsThisFrame = new HashSet<GameObject>();

        // Do a fuzzy match to compare the few colors we saw against our global registry
        foreach (Color32 cameraColor in uniqueColorsInFrame)
        {
            GameObject matchedObject = null;
            float closestDistance = float.MaxValue;
            int tolerance = 25; // How much the RGB values are allowed to shift combined (absorbs float errors, small shifts)

            foreach (var kvp in globalObjectRegistry)
            {
                Color32 registryColor = kvp.Key;

                // Calculate the "Manhattan Distance" between the color we saw and the color in the registry
                // A known way to give it some tolerance instead of ONLY accepting identical color codes
                float colorDistance = Mathf.Abs(cameraColor.r - registryColor.r) +
                                      Mathf.Abs(cameraColor.g - registryColor.g) +
                                      Mathf.Abs(cameraColor.b - registryColor.b);

                if (colorDistance < closestDistance && colorDistance < tolerance)
                {
                    closestDistance = colorDistance;
                    matchedObject = kvp.Value;
                }
            }

            if (matchedObject != null)
            {
                visibleObjectsThisFrame.Add(matchedObject);
            }
            else
            {
                // This line appears so often...since it's basically comparing every slight shift/pixel/shade in the whole photo
                // Leave it commented unless you're debugging and having serious issues with the system not registering any objects
                //Debug.LogWarning($"[Perception Sensor] COLOR MISMATCH! Saw RGB({cameraColor.r}, {cameraColor.g}, {cameraColor.b}) but couldn't find a match within tolerance.");
            }
        }

        // Finally, we can generate the anchor for the object taht we're seeing in the CURRENT frame
        // This maintains the guide only learning about what it encounters with the user
        foreach (GameObject obj in visibleObjectsThisFrame)
        {
            string id = obj.GetInstanceID().ToString();

            if (!activeAnchors.ContainsKey(id))
            {
                Color32 assignedColor = semanticMaterials[obj].color;
                ObjectAnchor newAnchor = new ObjectAnchor(obj, obj.name);
                newAnchor.uniqueColorID = assignedColor;
                activeAnchors.Add(id, newAnchor);

                Debug.Log($"[Perception Sensor] Guide saw {obj.name}. Assigned Mask Color: #{ColorUtility.ToHtmlStringRGBA(assignedColor)}");
                //Debug.Log($"Number of objects we've seen is now {GetObjectAnchors()}");
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