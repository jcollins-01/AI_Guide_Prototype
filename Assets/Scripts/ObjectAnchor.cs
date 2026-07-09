using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ObjectAnchor
{
    // Geometric/Perceptual Space
    public GameObject gameObjectReference;
    public string instanceID;
    public Vector3 lastKnownPosition;

    // Semantic Space (Shared Vocabulary)
    public string technicalName; // e.g., "Local Hospital"
    public List<string> userAliases = new List<string>(); // e.g., ["blue-striped building", "the hospital"]
    public string ongoingDescription;

    // Visual Reference Data
    public string localScreenshotBase64;
    public Color uniqueColorID; // For unique color masking filters

    public ObjectAnchor(GameObject go, string techName)
    {
        gameObjectReference = go;
        instanceID = go.GetInstanceID().ToString();
        lastKnownPosition = go.transform.position;
        technicalName = techName;
    }
}