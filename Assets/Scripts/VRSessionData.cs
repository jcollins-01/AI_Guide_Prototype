using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TrackedEvent
{
    public string objectName;
    public bool passedInVR;
    public bool verifiedInEditor; // The toggle you will click after the session
}

[CreateAssetMenu(fileName = "NewSessionData", menuName = "VR Logging/Session Data")]
public class VRSessionData : ScriptableObject
{
    public string participantID = "P001";
    public List<TrackedEvent> trialEvents = new List<TrackedEvent>();

    // Call this to clear data when a new participant starts
    public void ClearSession()
    {
        trialEvents.Clear();
    }
}