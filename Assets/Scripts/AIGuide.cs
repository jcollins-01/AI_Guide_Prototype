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
    private SharedMovement m_SharedMovementScript;
    private GuideFollow m_GuideFollowScript;
    private AutomaticModification m_AutomaticModificationScript;

    // Variables for monitoring
    private int whisperCalls = 0;
    private int completionCalls = 0;
    private int alloyCalls = 0;
    private int voiceCalls = 0;
    private bool firstQuery = true;
    private bool buttonPressed = false;

    // Variables to share with other scripts
    public Camera birdEyeCamera;

    // Variables for testing -- TO BE REMOVED LATER
    public bool playerGrab = false;

    // Start is called before the first frame update
    void Start()
    {
        // Add necessary components to the attached GameObject
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            gameObject.AddComponent<AudioSource>();
        NavMeshAgent navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
            gameObject.AddComponent<NavMeshAgent>();
        //gameObject.AddComponent<WizardControls>();

        createBirdEyeCamera();

        m_GuideFollowScript = gameObject.AddComponent<GuideFollow>();
        m_AutomaticModificationScript = gameObject.AddComponent<AutomaticModification>();
        m_AutomatedGuideScript = gameObject.AddComponent(typeof(AutomaticGuide)) as AutomaticGuide;
        m_OpenAIQueriesScript = gameObject.AddComponent(typeof(OpenAIQueries)) as OpenAIQueries;
        m_VRHandlingScript = gameObject.AddComponent(typeof(VRHandling)) as VRHandling;

        Debug.Log("AIGuide is active!");
    }

    // Update is called once per frame
    void Update()
    {
        // Calls until the shared movement script is assigned (when we have a player and a guide)
        // Needed for access to the player's interactions with the guide
        getSharedMovement();

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
            // If this is the first query, send all classifcations - after that, the guide should remember the player, photo, and scene contexts
            if (firstQuery)
            {
                m_OpenAIQueriesScript.text = m_OpenAIQueriesScript.playerClassification + m_OpenAIQueriesScript.photoClassification + m_OpenAIQueriesScript.objectClassifications + "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.memoClassifications;
                firstQuery = false;
            }
            else
            {
                m_OpenAIQueriesScript.text = "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.memoClassifications;
            }

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
            Debug.Log("Has a target to move to: " + m_OpenAIQueriesScript.targetForGuidance);
            m_SharedMovementScript.guideCollider.enabled = true; // Turns guide collider on so it's grabbable when there is a specific move target

            // If the player is grabbing the guide, call for the movement functions as appropriate
            // Turn off guide follow so that the guide begins to lead the player
            if (m_SharedMovementScript.playerGrabbingGuide || playerGrab == true)
            {
                m_GuideFollowScript.enabled = false;
                if (m_OpenAIQueriesScript.modeOfTransportation == "guide")
                    m_AutomatedGuideScript.GuideToPosition(m_OpenAIQueriesScript.targetForGuidance);
                else
                    m_AutomatedGuideScript.TeleportToPosition(m_OpenAIQueriesScript.targetForGuidance);
            }
        }
        else
        {
            m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
            //m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
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

    private void createBirdEyeCamera()
    {
        float birdHeight = 15f;
        Vector3 birdRotation = new Vector3(65f, 0f, 0f);
        GameObject newCamera = new GameObject("Bird's Eye Camera");
        birdEyeCamera = newCamera.AddComponent<Camera>();

        // Camera has specified height it goes above the guide to get bird's eye view + rotation to look down at the scene
        birdHeight = birdHeight + transform.position.y;
        birdEyeCamera.transform.position = new Vector3(transform.position.x, birdHeight, transform.position.z);
        birdEyeCamera.transform.eulerAngles = birdRotation;
    }
}