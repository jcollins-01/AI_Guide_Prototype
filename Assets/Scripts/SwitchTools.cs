using Normal.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SwitchTools : MonoBehaviour
{
    // Define an event that other scripts can listen to
    public event Action OnGuideConfigurationChanged;

    // Set these in Unity Editor to switch tools/features
    [Header("Tool Activation")]
    public bool VRGuideActive = false;
    public bool VRScreenreaderActive = false;

    [Header("Type of Guide")]
    /*public bool baselineGuide = false;
    public bool objectDescriptionGuide = false;
    public bool objectLocationGuide = false;
    public bool sceneUnderstandingGuide = false;
    public bool navigationGuide = false;
    public bool objectGrabbingGuide = false;
    public bool sightedGuidanceGuide = false;
    [HideInInspector] public bool allCombinedGuide = false; // deprecated for now - use later if we trust the AI to determine user intention*/

    [SerializeField] private bool _baselineGuide;
    public bool baselineGuide
    {
        get => _baselineGuide;
        set
        {
            if (_baselineGuide != value)
            {
                _baselineGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [SerializeField] private bool _objectDescriptionGuide;
    public bool objectDescriptionGuide
    {
        get => _objectDescriptionGuide;
        set
        {
            if (_objectDescriptionGuide != value)
            {
                _objectDescriptionGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [SerializeField] private bool _objectLocationGuide;
    public bool objectLocationGuide
    {
        get => _objectLocationGuide;
        set
        {
            if (_objectLocationGuide != value)
            {
                _objectLocationGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [SerializeField] private bool _sceneUnderstandingGuide;
    public bool sceneUnderstandingGuide
    {
        get => _sceneUnderstandingGuide;
        set
        {
            if (_sceneUnderstandingGuide != value)
            {
                _sceneUnderstandingGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [SerializeField] private bool _navigationGuide;
    public bool navigationGuide
    {
        get => _navigationGuide;
        set
        {
            if (_navigationGuide != value)
            {
                _navigationGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [SerializeField] private bool _objectGrabbingGuide;
    public bool objectGrabbingGuide
    {
        get => _objectGrabbingGuide;
        set
        {
            if (_objectGrabbingGuide != value)
            {
                _objectGrabbingGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [SerializeField] private bool _sightedGuidanceGuide;
    public bool sightedGuidanceGuide
    {
        get => _sightedGuidanceGuide;
        set
        {
            if (_sightedGuidanceGuide != value)
            {
                _sightedGuidanceGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    private bool _allCombinedGuide = false; // definitively set to false for now - use later if we trust the AI to determine user intention
    public bool allCombinedGuide
    {
        get => _allCombinedGuide;
        set
        {
            if (_allCombinedGuide != value)
            {
                _allCombinedGuide = value;
                OnGuideConfigurationChanged?.Invoke(); // Notify listeners!
            }
        }
    }

    [FormerlySerializedAs("pushToTalk")]
    [HideInInspector] public bool legacyHoldToSpeak = false;
    [HideInInspector] public bool continuousVoice = false;
    [HideInInspector] public bool personalVoicesOn = false;

    // Default guide interaction is the newer push-to-talk flow unless a legacy mode is explicitly enabled.
    public bool UseDefaultPushToTalk => !continuousVoice && !legacyHoldToSpeak;
    
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
                int randConfed = UnityEngine.Random.Range(1, 3);

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Only trigger updates if the simulation is actively running
        if (Application.isPlaying)
        {
            Debug.Log("[SwitchTools] Inspector toggle detected.");
            OnGuideConfigurationChanged?.Invoke();
        }
    }
#endif
}
