using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchTools : MonoBehaviour
{
    // Set these in Unity Editor to switch tools/features
    public bool VRGuideActive = false;
    public bool VRScreenreaderActive = false;
    public bool pushToTalk = false;
    public bool continuousVoice = false;
    [HideInInspector] public bool personalVoicesOn = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // If the guide is meant to be active in the scene
        if (VRGuideActive)
            FindObjectOfType<VRScreenreader>().gameObject.SetActive(false); // disable screenreader

        // If the screenreader is meant to be active in the scene
        if (VRScreenreaderActive)
            DisableGuide();

        // If neither tool is active, apply confederate settings
        if (!VRGuideActive && !VRScreenreaderActive)
        {
            FindObjectOfType<VRScreenreader>().gameObject.SetActive(false); // disable screenreader
            DisableGuide();
            ApplyRandomConfederateAvatar();
        }
    }

    private void DisableGuide()
    {
        GameObject guide = FindObjectOfType<GuideFollow>().gameObject;
        guide.SetActive(false); // disable XR Origin (Guide Rig)

        // Find all RealtimeAvatarManagers, if they have "Guide Avatar" assigned, deactivate them
        var managers = FindObjectsOfType<RealtimeAvatarManager>();
        foreach (RealtimeAvatarManager manager in managers)
        {
            if (manager.localAvatarPrefab.name == "Guide Avatar")
                manager.enabled = false;
        }
    }

    private void ApplyRandomConfederateAvatar()
    {
        // Find all RealtimeAvatarManagers, if they have "Guide Avatar" assigned, deactivate them
        var managers = FindObjectsOfType<RealtimeAvatarManager>();
        foreach (RealtimeAvatarManager manager in managers)
        {
            if (manager.localAvatarPrefab.name == "Player Avatar")
            {
                int randConfed = Random.Range(1, 3);

                switch(randConfed)
                {
                    case 1:
                        manager.localAvatarPrefab = Resources.Load<GameObject>("Confederate_One Avatar");
                        break;
                    case 2:
                        manager.localAvatarPrefab = Resources.Load<GameObject>("Confederate_Two Avatar");
                        break;
                }
            }
        }
    }
}
