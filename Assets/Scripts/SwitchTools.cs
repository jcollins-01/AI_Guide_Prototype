using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchTools : MonoBehaviour
{
    // Set these in Unity Editor to switch tools
    public bool VRGuideActive = false;
    public bool VRScreenreaderActive = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // If the guide is meant to be active in the scene
        if (VRGuideActive)
        {
            FindObjectOfType<VRScreenreader>().gameObject.SetActive(false); // disable screenreader
        }

        // If the screenreader is meant to be active in the scene
        if (VRScreenreaderActive)
        {
            FindObjectOfType<GuideFollow>().gameObject.SetActive(false); // disable XR Origin (Guide Rig)

            // Find all RealtimeAvatarManagers, if they have "Guide Avatar" assigned, deactivate them
            var managers = FindObjectsOfType<RealtimeAvatarManager>();
            foreach (RealtimeAvatarManager manager in managers)
            {
                if (manager.localAvatarPrefab.name == "Guide Avatar")
                    manager.enabled = false;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
