using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WizardControls : MonoBehaviour
{
    // Variables to hold the scripts we access as the wizard
    //private QueryDescription m_QueryDescriptionScript;
    private AutomaticGuide m_AutomatedGuideScript;
    private OpenAIQueries m_OpenAIQueriesScript;

    // Start is called before the first frame update
    void Start()
    {
        // Find existing scripts that are necessary
        //m_QueryDescriptionScript = FindObjectOfType(typeof(QueryDescription)) as QueryDescription;
        m_AutomatedGuideScript = FindObjectOfType(typeof(AutomaticGuide)) as AutomaticGuide;
        m_OpenAIQueriesScript = FindObjectOfType(typeof(OpenAIQueries)) as OpenAIQueries;

        if (m_OpenAIQueriesScript == null || m_AutomatedGuideScript == null)
        {
            Debug.LogWarning("One or more required scripts for WizardControls has not been found - please ensure that the GameObject with WizardControls also has OpenAIQueries and AutomaticGuide");
        }
        else
        {
            Debug.Log("WizardControls are active - ready for the wizard to intervene at any time!");
            // Description of the controls the wizard can use
            Debug.Log("Drag a target game object into the Wizard Controls editor and press G to move the guide to that target, or T to teleport");
            Debug.Log("Press N to query with a default message or alter user query field, then hit N. \n Press and hold M to record a new query. " +
            "\n To use text query instead of your voice, make sure the query field is empty. \n Press C to create a voice file of the guide's output, then V to play the file after it is created.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Check if the N key is pressed
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("Wizard called for a new query to be sent to GPT");
            // Grab the newest userQuery / query values if they have changed
            // Use manual text query if query has been erased in the Unity editor (no voice query)
            // ADD guideClassification
            if (m_OpenAIQueriesScript.query.Length > 0)
                m_OpenAIQueriesScript.text = m_OpenAIQueriesScript.playerClassification + m_OpenAIQueriesScript.objectClassifications + "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications;
            else
                m_OpenAIQueriesScript.text = m_OpenAIQueriesScript.playerClassification + m_OpenAIQueriesScript.objectClassifications + "Imagine the player said this: " + m_OpenAIQueriesScript.userQuery + ". " + m_OpenAIQueriesScript.queryClassifications;
            // Call the CallCompletion method with your desired userInput
            var guideResult = m_OpenAIQueriesScript.CallCompletion(m_OpenAIQueriesScript.text);
        }

        if (Input.GetKey(KeyCode.M))
        {
            Debug.Log("Wizard started recording a new voice query");
            // If the user is holding down M, start recording the audio
            m_OpenAIQueriesScript.CaptureAudio();
        }

        if (Input.GetKeyUp(KeyCode.M))
        {
            // If the user lifts finger off M key, assume their query is completed
            m_OpenAIQueriesScript.recordingInProgress = false;
            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("Wizard called to create a new audio clip of guide output");
            // Create the audio clip of whatever whatever output has been stored in the result variable
            var speechResult = m_OpenAIQueriesScript.CallAlloyTTS();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log("Wizard called to play the audio clip of guide output");
            // Voice the most recent audio clip created for the guide's output
            m_OpenAIQueriesScript.audioSource.clip = m_OpenAIQueriesScript.guideVoice;
            if (!m_OpenAIQueriesScript.audioSource.isPlaying)
                m_OpenAIQueriesScript.audioSource.Play();
        }

        // Call a pathfinding algorithm to guide the user to a specific object
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("Wizard called a pathfind to a target object");
            m_AutomatedGuideScript.GuideToPosition();
        }

        // Call a position change to teleport the user to a specific object
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("Wizard called a teleport to a target object");
            m_AutomatedGuideScript.TeleportToPosition();
        }
    }
}

