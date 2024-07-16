using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

public class ConfederateHandler : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private PlayAudio m_PlayAudioScript;
    public ChangeAvatarRuntime m_ChangeAvatarRuntimeScript;
    public AIGuide m_AIGuideScript;

    // GameObjects for avatar assignment
    public GameObject theGuide;
    public GameObject human;
    public GameObject dog;
    public GameObject cane;
    public GameObject robot;
    public GameObject bird;

    // Game Objects
    private XROrigin confederateRig;

    // Bools to share with other scripts
    public bool confederateVersion;
    
    // Start is called before the first frame update
    void Start()
    {
        // Get components already added on confederate GameObject
        //m_ChangeAvatarRuntimeScript = FindObjectOfType<ChangeAvatarRuntime>();
        // Assigns the player's XR Origin from a list of all realtimeViews in the scene
        confederateRig = FindObjectOfType<XROrigin>();
        m_PlayAudioScript = FindObjectOfType<PlayAudio>();
        
        // If we are in a confederate scene, assign the player owned locally in hierarchy to the audio source
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("Con_GuideTest_Networked") || currentSceneName.Equals("Con_Park1_Networked") || currentSceneName.Equals("Con_Park2_Networked") || currentSceneName.Equals("Con_Park3_Networked"))
        {
            AssignConfederateAudio();
            confederateVersion = true;
        }
        else
            confederateVersion = false;
            

        // Ignore collisions between Player and XR Rig
        Physics.IgnoreLayerCollision(3, 6, true);
        CharacterController control = FindObjectOfType<CharacterController>();
        control.detectCollisions = true;
    }

    // Update is called once per frame
    void Update()
    {
        // If there's an AIGuide object in the scene, assign the scripts
        if (FindObjectOfType<AIGuide>())
        {
            m_AIGuideScript = FindObjectOfType<AIGuide>();
            m_ChangeAvatarRuntimeScript = FindObjectOfType<ChangeAvatarRuntime>();
                
            // Assign confederate appearances continuously as confederates appear
            m_ChangeAvatarRuntimeScript.pickAvatarAtRandomForAll();
        }
    }

    private void AssignConfederateAudio()
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
                m_PlayAudioScript.playerAudio = currentPlayer.GetComponent<AudioSource>();
        }
    }

    // Function to check if an object is the root of its hierarchy
    bool IsRootObject(GameObject obj)
    {
        // Check if the object has no parent
        return obj.transform.parent == null;
    }
}
