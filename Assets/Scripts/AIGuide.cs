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

    // Variables for assigning XR input
    private bool rightControllerGrabbed = false;
    private bool leftControllerGrabbed = false;
    [HideInInspector]
    public InputDevice rightXRController;
    [HideInInspector]
    public InputDevice leftXRController;

    // Variables for monitoring
    private bool m_audioCaptured = false;
    private int whisperCalls = 0;
    private int completionCalls = 0;
    private int alloyCalls = 0;
    private int voiceCalls = 0;

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
        gameObject.AddComponent<WizardControls>();

        m_AutomatedGuideScript = gameObject.AddComponent(typeof(AutomaticGuide)) as AutomaticGuide;
        m_OpenAIQueriesScript = gameObject.AddComponent(typeof(OpenAIQueries)) as OpenAIQueries;

        if (m_OpenAIQueriesScript == null || m_AutomatedGuideScript == null)
            Debug.LogWarning("One or more required scripts for AIGuide has not been found - please ensure that the GameObject with AIGuide also has OpenAIQueries and AutomaticGuide");
        else
            Debug.Log("AIGuide is active!");
    }

    // Update is called once per frame
    void Update()
    {
        // Calls until two controllers are assigned
        //getControllers();

        // If PC user presses and holds space or the right primary button on an XR controller
        if (Input.GetKey(KeyCode.Space) || rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButtonValue) && primaryButtonValue)
        {
            m_OpenAIQueriesScript.CaptureAudio();

            // If, after the primary button was being held, it is no longer being held
            if (rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out bool checkPrimaryButtonValue) && !checkPrimaryButtonValue)
                m_audioCaptured = true;

            // Reset call counters so they can each be called once more
            whisperCalls = 0;
            completionCalls = 0;
            alloyCalls = 0;
            voiceCalls = 0;
        }

        // If the user lifts finger off space or the primary button, assume their query is completed
        if ((Input.GetKeyUp(KeyCode.Space) || m_audioCaptured == true) && whisperCalls == 0)
        {
            m_OpenAIQueriesScript.recordingInProgress = false;
            m_audioCaptured = false;
            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
            whisperCalls += 1;
        }

        // Checking for completion of speech transcription
        if (m_OpenAIQueriesScript.whisperCompleted && completionCalls == 0)
        {
            // Construct the query to send to GPT-4 - ADD guideClassification
            m_OpenAIQueriesScript.text = m_OpenAIQueriesScript.playerClassification + m_OpenAIQueriesScript.objectClassifications + "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.memoClassifications;
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
            if (m_OpenAIQueriesScript.modeOfTransportation == "guide")
                m_AutomatedGuideScript.GuideToPosition(m_OpenAIQueriesScript.targetForGuidance);
            else
                m_AutomatedGuideScript.TeleportToPosition(m_OpenAIQueriesScript.targetForGuidance);
        }
    }

    public void getControllers()
    {
        if (!rightControllerGrabbed || !leftControllerGrabbed)
        {
            // Makes a list for input devices + fills it with devices that match the characteristics we give in the Unity editor
            // Narrows devices list using characteristics to just the controller we want to use
            List<InputDevice> devices = new List<InputDevice>();

            InputDeviceCharacteristics rightController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Right;
            InputDevices.GetDevicesWithCharacteristics(rightController, devices);

            InputDeviceCharacteristics leftController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Left;
            InputDevices.GetDevicesWithCharacteristics(leftController, devices);

            Debug.Log("Grabbing devices");
            Debug.Log("Found devices " + devices);

            if (!rightControllerGrabbed)
                rightXRController = devices[2]; //attached to right controller
            if (!leftControllerGrabbed)
                leftXRController = devices[1]; // attached to left controller

            if (devices[2] != null) // rightXRController
            {
                Debug.Log("Grabbed right controller successfully");
                rightControllerGrabbed = true;
            }

            if (devices[1] != null) // leftXRController
            {
                Debug.Log("Grabbed left controller successfully");
                leftControllerGrabbed = true;
            }
        }
    }
}
