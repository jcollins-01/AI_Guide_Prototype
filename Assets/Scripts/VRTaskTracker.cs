using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRTaskTracker : MonoBehaviour
{
    public VRSessionData sessionData;
    private List<string> loggedObjects = new List<string>();

    void Start()
    {
        // Optional: clear previous data on start
        // sessionData.ClearSession(); 
    }

    // Track when a user passes an object
    public void LogObjectPassed(string objName)
    {
        sessionData.trialEvents.Add(new TrackedEvent
        {
            objectName = objName,
            passedInVR = true,
            verifiedInEditor = false
        });
    }

    // Call this from a trigger collider //
    private void OnTriggerEnter(Collider other)
    {
        // This is just when they enter/touch the invisible cube zones
        if (other.CompareTag("Zone"))
        {
            // Maybe here, we run through each of the objects in the zone
            // Get every object that is registered as part of the zone we just entered
            foreach (GameObject zoneObj in other.gameObject.GetComponent<ZoneContactTracker>().touchingObjects)
            {
                // Only log them once so we only have to review them once
                if (!loggedObjects.Contains(zoneObj.name))
                {
                    LogObjectPassed(zoneObj.name);
                    loggedObjects.Add(zoneObj.name); // go by name, not object, so we don't have a million rocks and trees 
                }
            }
        }
    }
}
