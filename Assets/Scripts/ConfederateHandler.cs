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

    // Monitoring bools
    private bool aiGuideFound = false;
    private bool avatarAssigned = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // Grabs scripts already in the scene at start
        m_PlayAudioScript = FindObjectOfType<PlayAudio>(); // On XR rig
        //m_SharedMovementScript = GetComponent<SharedMovement>(); // On this Game Object
        /*m_confederateHandlerSync = GetComponent<ConfederateHandlerSync>(); // On this Game Object
        if (m_confederateHandlerSync == null)
            Debug.LogError("ConfederateHandlerSync component missing from this GameObject.");*/

        // If we are in a confederate scene, assign the player owned locally in hierarchy to the audio source
        /*string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("Con_GuideTest_Networked") || currentSceneName.Equals("Con_Park1_Networked") || currentSceneName.Equals("Con_Park2_Networked") || currentSceneName.Equals("Con_Park3_Networked"))
        {
            AssignConfederate();
            confederateVersion = true;
        }
        else
        {
            confederateVersion = false;
        }*/
        

        AssignConfederateAudio();
    }

    // Update is called once per frame
    void Update()
    {
        // Continuously look for an AIGuide object (for a guide to join the scene)
        if (!aiGuideFound)
            getAIGuide();

        // If there's a guide in the scene, send our confederate role over the network
        /*if (aiGuideFound)
        {
            // Send confederate version over a network
            if (confederateVersion)
                m_confederateHandlerSync.SetConfederateVersion(true);
            else
                m_confederateHandlerSync.SetConfederateVersion(false);
        }

        // If there's a guide in the scene and we are the confederate, assign the scripts and run functions dependent on it
        /*if (aiGuideFound && confederateVersion)
        {
            m_AIGuideScript = FindObjectOfType<AIGuide>();
            m_ChangeAvatarRuntimeScript = FindObjectOfType<ChangeAvatarRuntime>();

            // Assign confederate a random appearance and share it to the network
            AssignConfederateAvatar();
        }*/

        if (aiGuideFound) // && confederateVersion)
        {
            m_ChangeAvatarRuntimeScript = FindObjectOfType<ChangeAvatarRuntime>();

            // Assign confederate a random appearance and share it to the network
            //AssignConfederateAvatar();
        }
    }

    private void AssignConfederateAudio()
    {
        m_PlayAudioScript.playerAudio = GetComponent<AudioSource>();
    }

    /*private void AssignConfederateAvatar()
    {
        if (!avatarAssigned)
        {
            // Pass our avatar changing script our local confederate
            //m_ChangeAvatarRuntimeScript.getConfederateModels(theConfederate);

            // Generates a random role from 7-10, 7: Model 1, 8: Model 2, 9: Model 3, 10: Model 4
            int randomRole = Random.Range(7, 11);
            Debug.Log("Random role is " + randomRole);
            m_ChangeAvatarRuntimeScript.assignConfederateAvatarByRole(randomRole);

            avatarAssigned = true;
        }
    }*/

    // Function to check if an object is the root of its hierarchy
    /*bool IsRootObject(GameObject obj)
    {
        // Check if the object has no parent
        return obj.transform.parent == null;
    }*/

    private void getAIGuide()
    {
        if (m_AIGuideScript == null)
            m_AIGuideScript = FindObjectOfType<AIGuide>();
        else
            aiGuideFound = true;
    }
}
