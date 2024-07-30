using System.Collections;
using UnityEngine;

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
    private int screenshotsCaptured = 0;
    private int whisperCalls = 0;
    private int completionCalls = 0;
    private int alloyCalls = 0;
    private int voiceCalls = 0;
    //private bool firstQuery = true;
    private bool buttonPressed = false;
    private bool guideRoleAssigned = false;

    // Variables for wizard components
    public string result;
    public int role = 1; // 1: human, 2: robot, 3: cane, 4: guide dog, 5: bird, 6: invisible

    // Start is called before the first frame update
    void Start()
    {
        // Find necessary components to the attached GameObject
        m_GuideFollowScript = FindObjectOfType<GuideFollow>(); // On XR Rig

        // Add necessary components to the attached GameObject
        m_OpenAIQueriesScript = gameObject.AddComponent<OpenAIQueries>();
        m_AutomaticModificationScript = gameObject.AddComponent<AutomaticModification>();
        m_AutomatedGuideScript = gameObject.AddComponent<AutomaticGuide>();
        m_VRHandlingScript = gameObject.AddComponent<VRHandling>();

        Debug.Log("AIGuide is active!");

        // Set avatars to correct roles in separate scenes for the guide
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("GuideTest_Networked"))
            role = 1; // human
        else if (currentSceneName.Equals("GuidePark1_Networked"))
            role = 1; // human
        else if (currentSceneName.Equals("GuidePark2_Networked"))
            role = 4; // dog
        else if (currentSceneName.Equals("GuidePark3_Networked"))
            role = 5; // bird
        else
        {
            role = 6; // invisible - set to this for tutorial
            DisableColliders(FindObjectOfType<GuideRoleSync>().gameObject);
        }

        // Line to test guide changes over network
        //InvokeRepeating("ChangeGuideRole", 0f, 10f);
    }

    // For testing the role change over the network
    private void ChangeGuideRole()
    {
        int randRole = Random.Range(1, 6);

        role = randRole;
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
        // Calls until the appropriate scripts are assigned (when we have a player and a guide)
        // Needed for access to the player's interactions with the guide + sharing guide audio over network
        getSharedMovement();
        getAudioSync();

        // If we're in a scene run from a guide client
        if (FindObjectOfType<GuideFollow>())
        {
            // Check for space button or A button press from user
            checkUserInput();

            // Send recorded input to Whisper
            sendUserInput();

            // Take transcribed input as query and send to GPT-4
            sendQueryToGPT();

            // Play the response from GPT-4 as audio
            playGuideResponse();

            // Determine if guidance is required based on GPT-4 response
            checkGuidanceRequests();

            // Determine if modification is required based on GPT-4 response
            checkModificationRequests();
        }

        // Triggers the assignment of the avatar in static conditions (avatar set once at beginning of scene) for confederate clients
        if ((GameObject.FindWithTag("Confederate_1") || GameObject.FindWithTag("Confederate_2")) && !guideRoleAssigned)
            StartCoroutine(AssignRoleStatic());
    }

    // Method to disable all colliders in the children of this gameObject
    private void DisableColliders(GameObject model)
    {
        if (model != null)
        {
            // Get all the colliders in the children of this gameObject
            Collider[] colliders = model.GetComponentsInChildren<Collider>();

            // Loop through each collider and disable it
            foreach (Collider collider in colliders)
                collider.enabled = false;
        }
    }

    private IEnumerator AssignRoleStatic()
    {
        role = 6;
        yield return new WaitForSeconds(10f);
        // Set avatars to correct roles in separate scenes for the guide
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("GuideTest_Networked"))
            role = 1; // human
        else if (currentSceneName.Equals("GuidePark1_Networked"))
            role = 1; // human
        else if (currentSceneName.Equals("GuidePark2_Networked"))
            role = 4; // dog
        else if (currentSceneName.Equals("GuidePark3_Networked"))
            role = 5; // bird

        guideRoleAssigned = true;
    }

    private IEnumerator muteAudioSource(AudioSource source, AudioClip clip)
    {
        yield return new WaitForSeconds(clip.length);
        source.mute = true;
    }

    private void playEffect(string clipName)
    {
        AudioSource audioSource = GetComponent<AudioSource>();

        switch(clipName)
        {
            case "subway_chime":
                {
                    //Debug.Log("Played arrival effect");
                    audioSource.clip = Resources.Load<AudioClip>("Audio/subway_chime");
                    audioSource.mute = false;
                    audioSource.loop = false;
                    audioSource.Play();
                    break;
                }
            case "processing":
                {
                    //Debug.Log("Playing processing sound");
                    audioSource.clip = Resources.Load<AudioClip>("Audio/processing");
                    audioSource.mute = false;
                    audioSource.loop = true;
                    audioSource.Play();
                    break;
                }
            case "completion":
                {
                    //Debug.Log("Playing completion sound");
                    audioSource.clip = Resources.Load<AudioClip>("Audio/completion");
                    audioSource.mute = false;
                    audioSource.loop = false;
                    audioSource.Play();
                    // Mute after playing the completion effect to prevent audio doubling
                    StartCoroutine(muteAudioSource(audioSource, audioSource.clip));
                    break;
                }
        }
    }

    private void checkModificationRequests()
    {
        // Checking if a target GameObject was selected to be modified
        if (m_OpenAIQueriesScript.targetForModification != null)
        {
            // Call to create an audio beacon, then immediately set the target to null so it doesn't continuously call for beacon creation
            Debug.Log("Has a target to modify: " + m_OpenAIQueriesScript.targetForModification);
            m_AutomaticModificationScript.AddAudioBeacon(m_OpenAIQueriesScript.targetForModification);
            m_OpenAIQueriesScript.targetForModification = null;
        }
    }

    private void checkGuidanceRequests()
    {
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
                    //Debug.Log("The mode of transit is guide");
                    m_AutomatedGuideScript.GuideToPosition(m_OpenAIQueriesScript.targetForGuidance);
                    // Calculate the distance between thePlayer and the current GameObject to monitor for player getting disconnected
                    float distance = Vector3.Distance(transform.position, m_SharedMovementScript.thePlayer.transform.position);

                    // If they reach the target, make it stop grabbing and stop moving
                    if (!m_AutomatedGuideScript.targetActive)
                    {
                        m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
                        m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
                        playEffect("subway_chime");
                    }
                    else if (distance > 1.5f) // If the guide left the participant behind at some point during guidance and ended by standing more than an arm's reach away
                    {
                        m_GuideFollowScript.enabled = true; // Turn guide follow back on to make the guide return to player
                        playEffect("subway_chime"); // Play a sound effect to let the participant know the guide has returned
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
                        playEffect("subway_chime");
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
    }

    private void playGuideResponse()
    {
        // Checking for completion of query to GPT-4
        if (m_OpenAIQueriesScript.completionCompleted && alloyCalls == 0)
        {
            // Play sound effect to indicate completion of guide's processing
            playEffect("completion");

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
    }

    private void sendQueryToGPT()
    {
        // Checking for completion of speech transcription and image upload
        if (m_OpenAIQueriesScript.whisperCompleted && completionCalls == 0 && FindObjectOfType<CameraSystem>().uploaded)
        {
            // Start to play processing sound
            playEffect("processing");

            // Construct the query to send to GPT-4
            m_OpenAIQueriesScript.text = "You are a " + m_OpenAIQueriesScript.role + ", named Gideon. " + m_OpenAIQueriesScript.contextClassification + m_OpenAIQueriesScript.memoClassifications + m_OpenAIQueriesScript.objectClassifications + " Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications;

            // [DEPRECATED] If this is the first query, send all classifcations - after that, only send user query to speed up guide response time
            /*if (firstQuery)
            {
                m_OpenAIQueriesScript.text = "You are a " + m_OpenAIQueriesScript.role + ". " + m_OpenAIQueriesScript.contextClassification + m_OpenAIQueriesScript.memoClassifications + m_OpenAIQueriesScript.objectClassifications + " Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications;
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
    }

    private void sendUserInput()
    {
        // If PC user lifts finger off space, assume their query is completed
        if ((Input.GetKeyUp(KeyCode.Space)) && whisperCalls == 0 && screenshotsCaptured == 0)
        {
            m_OpenAIQueriesScript.recordingInProgress = false;
            // Take screenshots and upload to ImageShack
            FindObjectOfType<CameraSystem>().CaptureScreenshot();
            screenshotsCaptured += 1;
            Debug.Log("Screenshot captured");

            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
            whisperCalls += 1;
        }

        // If VR user lifts finger off primary button, assume their query is completed
        if (!m_VRHandlingScript.isButtonPressed && whisperCalls == 0 && buttonPressed == true && screenshotsCaptured == 0)
        {
            m_OpenAIQueriesScript.recordingInProgress = false;
            // Take screenshots and upload to ImageShack
            FindObjectOfType<CameraSystem>().CaptureScreenshot();
            screenshotsCaptured += 1;

            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
            whisperCalls += 1;
            buttonPressed = false;
        }
    }

    private void checkUserInput()
    {
        // If PC user presses and holds space
        if (Input.GetKey(KeyCode.Space))
        {
            m_OpenAIQueriesScript.CaptureAudio();

            // Reset call counters so they can each be called once more
            screenshotsCaptured = 0;
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
            screenshotsCaptured = 0;
            whisperCalls = 0;
            completionCalls = 0;
            alloyCalls = 0;
            voiceCalls = 0;
            buttonPressed = true;
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