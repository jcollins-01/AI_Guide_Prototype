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
    private bool audioAssigned = false;

    // Start is called before the first frame update
    void Start()
    {
        // Grabs scripts already in the scene at start
        m_PlayAudioScript = FindObjectOfType<PlayAudio>(); // On XR rig
    }

    // Update is called once per frame
    void Update()
    {
        // Continuously look for an AIGuide object (for a guide to join the scene)
        if (!aiGuideFound)
            getAIGuide();
        if (!audioAssigned)
            AssignConfederateAudio();

        if (aiGuideFound) // && confederateVersion)
        {
            m_ChangeAvatarRuntimeScript = FindObjectOfType<ChangeAvatarRuntime>();

            // Assign confederate a random appearance and share it to the network
            //AssignConfederateAvatar();
        }
    }

    private void AssignConfederateAudio()
    {
        if (!audioAssigned)
        {
            if (GameObject.FindWithTag("Confederate_1"))
            {
                m_PlayAudioScript.playerAudio = GameObject.FindWithTag("Confederate_1").GetComponentInChildren<AudioSource>();
                audioAssigned = true;
            }

            if (GameObject.FindWithTag("Confederate_2"))
            {
                m_PlayAudioScript.playerAudio = GameObject.FindWithTag("Confederate_2").GetComponentInChildren<AudioSource>();
                audioAssigned = true;
            }
        }
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
