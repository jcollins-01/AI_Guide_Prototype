using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

public class ConfederateHandler : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private PlayAudio m_PlayAudioScript;
    private ChangeAvatarRuntime m_ChangeAvatarRuntimeScript;
    private AIGuide m_AIGuideScript;
    private SharedMovement m_SharedMovementScript;

    // Game Objects
    public GameObject theConfederate;

    // Bools to share with other scripts
    public bool confederateVersion;

    // Monitoring bools
    private bool aiGuideFound = false;
    private bool avatarAssigned = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // Grabs scripts already in the scene at start
        m_PlayAudioScript = FindObjectOfType<PlayAudio>(); // On XR rig
        m_SharedMovementScript = GetComponent<SharedMovement>(); // On this Game Object
        
        // If we are in a confederate scene, assign the player owned locally in hierarchy to the audio source
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("Con_GuideTest_Networked") || currentSceneName.Equals("Con_Park1_Networked") || currentSceneName.Equals("Con_Park2_Networked") || currentSceneName.Equals("Con_Park3_Networked"))
        {
            AssignConfederate();
            confederateVersion = true;
            // Send confederate version over a network
        }
        else
        {
            confederateVersion = false;
            // Send confederate version over a network
        }

    }

    // Update is called once per frame
    void Update()
    {
        // Continuously look for an AIGuide object (for a guide to join the scene)
        if (!aiGuideFound)
            getAIGuide();

        // If there's a guide in the scene and we are the confederate, assign the scripts and run functions dependent on it
        if (aiGuideFound && confederateVersion)
        {
            m_AIGuideScript = FindObjectOfType<AIGuide>();
            m_ChangeAvatarRuntimeScript = FindObjectOfType<ChangeAvatarRuntime>();

            // Assign confederate a random appearance and share it to the network
            AssignConfederateAvatar();
        }
    }

    private void AssignConfederate()
    {
        // Gets a list of all realtimeViews in the scene
        var foundViews = FindObjectsOfType<Normal.Realtime.RealtimeView>();
        List<GameObject> foundPlayers = new List<GameObject>();

        // Checks which ones are root objects, which would make them players
        foreach (Normal.Realtime.RealtimeView realtimeView in foundViews)
        {
            if (IsRootObject(realtimeView.gameObject))
                foundPlayers.Add(realtimeView.gameObject);
        }

        foreach (GameObject currentPlayer in foundPlayers)
        {
            // If the found player is owned locally in the hierarchy, this is our local confederate
            if (currentPlayer.GetComponent<RealtimeView>().isOwnedLocallyInHierarchy)
            {
                theConfederate = currentPlayer;
                AssignConfederateAudio();
            }  
        }
    }

    private void AssignConfederateAudio()
    {
        m_PlayAudioScript.playerAudio = theConfederate.GetComponent<AudioSource>();
    }

    private void AssignConfederateAvatar()
    {
        if (!avatarAssigned)
        {
            // Pass our avatar changing script our local confederate
            m_ChangeAvatarRuntimeScript.getConfederateModels(theConfederate);

            // Generates a random role from 7-10, 7: Model 1, 8: Model 2, 9: Model 3, 10: Model 4
            int randomRole = Random.Range(7, 11);
            m_ChangeAvatarRuntimeScript.assignConfederateAvatarByRole(randomRole);

            avatarAssigned = true;
        }
    }

    // Function to check if an object is the root of its hierarchy
    bool IsRootObject(GameObject obj)
    {
        // Check if the object has no parent
        return obj.transform.parent == null;
    }

    private void getAIGuide()
    {
        if (m_AIGuideScript == null)
            m_AIGuideScript = FindObjectOfType<AIGuide>();
        else
            aiGuideFound = true;
    }
}
