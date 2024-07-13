using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR;

public class AIGuide : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private AutomaticGuide m_AutomatedGuideScript;
    private OpenAIQueries m_OpenAIQueriesScript;
    private VRHandling m_VRHandlingScript;
    public SharedMovement m_SharedMovementScript;
    public GuideFollow m_GuideFollowScript;
    private AutomaticModification m_AutomaticModificationScript;
    public GuideAudioSync m_guideAudioSync;

    // Variables for monitoring
    private int whisperCalls = 0;
    private int completionCalls = 0;
    private int alloyCalls = 0;
    private int voiceCalls = 0;
    //private bool firstQuery = true;
    private bool buttonPressed = false;

    // Variables for wizard components
    public string result;
    public int role = 1; // 1: human, 2: robot, 3: cane, 4: guide dog, 5: bird, 6: invisible

    // Start is called before the first frame update
    void Start()
    {
        // Find necessary components to the attached GameObject
        m_GuideFollowScript = FindObjectOfType<GuideFollow>();

        // Add necessary components to the attached GameObject
        m_AutomaticModificationScript = gameObject.AddComponent<AutomaticModification>();
        m_AutomatedGuideScript = gameObject.AddComponent<AutomaticGuide>();
        m_OpenAIQueriesScript = gameObject.AddComponent<OpenAIQueries>();
        m_VRHandlingScript = gameObject.AddComponent<VRHandling>();

        Debug.Log("AIGuide is active!");
    }

    // Method to test if result is working
    public void SetNewResult(string result)
    {
        // Debug.Log("Reached SetNewResult");
        if (m_guideAudioSync != null)
            m_guideAudioSync.SetResult(result);
        else
            Debug.LogError("GuideAudioSync is not initialized.");
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.A))
        {
            Debug.Log("Sent a result from the Wizard");
            SetNewResult(result);
        }

        // Calls until the appropriate scripts are assigned (when we have a player and a guide)
        // Needed for access to the player's interactions with the guide + sharing guide audio over network
        getSharedMovement();
        getAudioSync();

        // If PC user presses and holds space
        if (Input.GetKey(KeyCode.Space))
        {
            m_OpenAIQueriesScript.CaptureAudio();

            // Reset call counters so they can each be called once more
            whisperCalls = 0;
            completionCalls = 0;
            alloyCalls = 0;
            voiceCalls = 0;
        }

        // If VR user presses right primary button on an XR controller
        if (m_VRHandlingScript.isButtonPressed)
        {
            m_OpenAIQueriesScript.CaptureAudio();

            // Reset call counters so they can each be called once more
            whisperCalls = 0;
            completionCalls = 0;
            alloyCalls = 0;
            voiceCalls = 0;
            buttonPressed = true;
        }

        // If PC user lifts finger off space, assume their query is completed
        if ((Input.GetKeyUp(KeyCode.Space)) && whisperCalls == 0)
        {
            m_OpenAIQueriesScript.recordingInProgress = false;
            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
            whisperCalls += 1;
        }

        // If VR user lifts finger off primary button, assume their query is completed
        if (!m_VRHandlingScript.isButtonPressed && whisperCalls == 0 && buttonPressed == true)
        {
            m_OpenAIQueriesScript.recordingInProgress = false;
            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
            whisperCalls += 1;
            buttonPressed = false;
        }

        // Checking for completion of speech transcription
        if (m_OpenAIQueriesScript.whisperCompleted && completionCalls == 0)
        {
            // Construct the query to send to GPT-4 - ADD guideClassification
            m_OpenAIQueriesScript.text = "You are a " + m_OpenAIQueriesScript.role + ". " + m_OpenAIQueriesScript.contextClassification + m_OpenAIQueriesScript.memoClassifications + m_OpenAIQueriesScript.objectClassifications + " Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications;

            // [DEPRECATED] If this is the first query, send all classifcations - after that, the guide should remember the player, photo, and scene contexts
            /*if (firstQuery)
            {
                m_OpenAIQueriesScript.text = m_OpenAIQueriesScript.playerClassification + m_OpenAIQueriesScript.photoClassification + m_OpenAIQueriesScript.objectClassifications + "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.memoClassifications;
                firstQuery = false;
            }
            else
            {
                m_OpenAIQueriesScript.text = "Now, imagine the player said this: " + m_OpenAIQueriesScript.query;
            }*/

            // Call the CallCompletion method with the user's recorded voice query
            var guideResult = m_OpenAIQueriesScript.CallCompletion(m_OpenAIQueriesScript.text);
            completionCalls += 1;
        }

        // Checking for completion of query to GPT-4
        if (m_OpenAIQueriesScript.completionCompleted && alloyCalls == 0)
        {
            // Create the audio clip of whatever whatever output has been stored in the result variable
            var speechResult = m_OpenAIQueriesScript.CallAlloyTTS();
            alloyCalls += 1;
        }

        // Checking for completion of audio clip of the guide's response to the user query
        if (m_OpenAIQueriesScript.alloyCompleted && voiceCalls == 0)
        {
            // Play the guide's response
            m_OpenAIQueriesScript.audioSource.clip = m_OpenAIQueriesScript.guideVoice;
            if (!m_OpenAIQueriesScript.audioSource.isPlaying)
                m_OpenAIQueriesScript.audioSource.Play();
            voiceCalls += 1;
        }

        // Checking if a target GameObject was selected to be moved to
        if (m_OpenAIQueriesScript.targetForGuidance != null)
        {
            //Debug.Log("Has a target to move to: " + m_OpenAIQueriesScript.targetForGuidance);
            m_SharedMovementScript.guideCollider.enabled = true; // Turns guide collider on so it's grabbable when there is a specific move target

            // If the player is grabbing the guide, call for the movement functions as appropriate
            // Turn off guide follow so that the guide begins to lead the player
            if (m_SharedMovementScript.playerGrabbingGuide)
            {
                m_GuideFollowScript.enabled = false;
                if (m_OpenAIQueriesScript.modeOfTransportation == "guide")
                {
                    // Debug.Log("The mode of transit is guide");
                    m_AutomatedGuideScript.GuideToPosition(m_OpenAIQueriesScript.targetForGuidance);
                    // If they reach the target, make it stop grabbing and stop moving
                    if (!m_AutomatedGuideScript.targetActive)
                    {
                        Debug.Log("Played arrival effect");
                        m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
                        m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
                        AudioSource audioSource = GetComponent<AudioSource>();
                        audioSource.clip = Resources.Load<AudioClip>("Audio/subway_chime");
                        audioSource.mute = false;
                        audioSource.Play();
                    }
                } 
                else
                {
                    //Debug.Log("The mode of transit is teleport");
                    m_AutomatedGuideScript.TeleportToPosition(m_OpenAIQueriesScript.targetForGuidance);
                    // If they reach the target, make it stop grabbing and stop moving
                    if (!m_AutomatedGuideScript.targetActive)
                    {
                        m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
                        m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
                        AudioSource audioSource = GetComponent<AudioSource>();
                        audioSource.clip = Resources.Load<AudioClip>("Audio/subway_chime");
                        audioSource.Play();
                    }
                }
            }
        }
        else
        {
            if (m_SharedMovementScript != null)
            {
                m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
                m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
                m_SharedMovementScript.OnTriggerExit(m_SharedMovementScript.guideCollider); // Triggers the exit event so the system sets the guide's grabbing trigger to false
            }
        }

        // Checking if a target GameObject was selected to be modified
        if (m_OpenAIQueriesScript.targetForModification != null)
        {
            // Call to create an audio beacon, then immediately set the target to null so it doesn't continuously call for beacon creation
            Debug.Log("Has a target to modify: " + m_OpenAIQueriesScript.targetForModification);
            m_AutomaticModificationScript.AddAudioBeacon(m_OpenAIQueriesScript.targetForModification);
            m_OpenAIQueriesScript.targetForModification = null;
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
    }

    private void getAudioSync()
    {
        if (m_guideAudioSync == null)
            m_guideAudioSync = FindObjectOfType<GuideAudioSync>();
    }
}