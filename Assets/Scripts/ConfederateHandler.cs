using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

public class ConfederateHandler : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private PlayAudio m_PlayAudioScript;

    // Monitoring bools
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
        if (!audioAssigned)
            AssignConfederateAudio();
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
}
