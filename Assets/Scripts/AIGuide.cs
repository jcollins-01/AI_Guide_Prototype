using System.Collections;
using System.Threading.Tasks;
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
    private RealtimeGuideClient realtimeClient;

    // Variables for monitoring
    private bool guideRoleAssigned = false;
    private bool guideRoleAssignedStart = false;
    private bool isHighlighted = false;
    private bool isRecording = false;
    private bool wasMutingLastFrame = false;
    private bool wasVRButtonDownLastFrame = false;

    // Variables for wizard components
    public string result;
    public int role = 1; // 1: human, 2: robot, 3: cane, 4: guide dog, 5: bird, 6: invisible

    // Audio relevant variables
    private AudioSource sfxAudioSource;
    private AudioClip chimeClip;
    private AudioClip processingClip;
    private AudioClip listeningClip;
    private AudioClip doneListeningClip;

    // Start is called before the first frame update
    void Start()
    {
        AddGuideComponents();

        SetupRealtimeClient();

        //InvokeRepeating("UpdateVisualContext", 2.0f, 7.0f);

        Debug.Log("AIGuide is active!");
    }

    private void AddGuideComponents()
    {
        // Find necessary components to the attached GameObject
        m_GuideFollowScript = FindObjectOfType<GuideFollow>(); // On XR Rig

        // Add necessary components to the attached GameObject
        m_AutomaticModificationScript = gameObject.AddComponent<AutomaticModification>();
        m_AutomatedGuideScript = gameObject.AddComponent<AutomaticGuide>();
        m_VRHandlingScript = gameObject.AddComponent<VRHandling>();
        m_OpenAIQueriesScript = gameObject.AddComponent<OpenAIQueries>();
        LoadAudioResources();

        // This line is needed if we use the invisible guide role
        if (role == 6)
            DisableColliders(FindObjectOfType<GuideRoleSync>().gameObject);
    }

    private void LoadAudioResources()
    {
        // Load clips once at the start
        chimeClip = Resources.Load<AudioClip>("Audio/subway_chime");
        processingClip = Resources.Load<AudioClip>("Audio/processing");
        listeningClip = Resources.Load<AudioClip>("Audio/listening");
        doneListeningClip = Resources.Load<AudioClip>("Audio/done_listening");

        // Add a special audio source for the sound effects so it doesn't interfere with the mic channel
        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.spatialBlend = 0; // 2D sound for UI effects (clearer)
    }

    private void SetupRealtimeClient()
    {
        // Set up realtime client
        realtimeClient = gameObject.AddComponent<RealtimeGuideClient>();

        string basePrompt = GetFormattedPrompt();

        // Load config and connect to client
        realtimeClient.LoadConfig();
        realtimeClient._pushToTalkOn = (FindObjectOfType<SwitchTools>().pushToTalk) ? true : false;
        realtimeClient._continuousVoiceOn = (FindObjectOfType<SwitchTools>().continuousVoice) ? true : false;
        realtimeClient.Connect(basePrompt);

        realtimeClient.OnAutoStopRecording += HandleAutoStop; // Subscribe to the event of whenever the client auto-stops (detected a user stopped speaking)
        //realtimeClient.OnServerDetectedSpeechStart += () => playEffect("listening"); // Subscribe to event of detecting a user's speech  starting (continuous voice)
        //realtimeClient.OnServerDetectedSpeechStop += () => playEffect("done_listening"); // Subscribe to event of detecting a user's speech stopping (continuous voice)
    }

    // For ensuring proper realtime data
    public string GetFormattedPrompt()
    {
        // Ensure data is fresh
        m_OpenAIQueriesScript.LoadRoomDescriptions();
        m_OpenAIQueriesScript.getGuideRole();

        return "You are Giddy, a " + m_OpenAIQueriesScript.role + ". You are a sighted guide for a blind player. " + m_OpenAIQueriesScript.contextClassification +
               " THE NAVIGATION REGISTRY: Names and descriptions of objects in the scene. When following navigation or modification commands, use ONLY these names: " + m_OpenAIQueriesScript.objectClassifications + 
               m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.commandClassifications + m_OpenAIQueriesScript.guideRules;
    }

    private void PresetAvatarRoles()
    {
        // Set avatars to correct roles in separate scenes for the guide
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("Tutorial"))
            role = 1; // human
        else if (currentSceneName.Equals("GuidePark1_Networked"))
            role = 2; // human
        else if (currentSceneName.Equals("GuidePark2_Networked"))
            role = 4; // dog
        else if (currentSceneName.Equals("GuidePark3_Networked"))
            role = 4; // robot
        else
            role = 1; // human is default guide for all other rooms
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
            // Call the guide
            RealtimeGuide();

            // Determine if guidance is required based on GPT-4 response
            checkGuidanceRequests();

            // Determine if modification is required based on GPT-4 response
            checkModificationRequests();

            // Determine if description is required based on GPT-4 response
            checkDescriptionRequests();

            // If any confederate is present at the start of the scene, assign the guide
            AssignSingleConfederate();

            // Check if both confederates are present and send guide roles each time they are (in case of confederates leaving and coming back)
            BothConfederatesPresent();
        }
    }

    private void RealtimeGuide()
    {
        bool vrButtonDown = m_VRHandlingScript.isButtonPressed;
        bool spaceDown = Input.GetKeyDown(KeyCode.Space);
        // For continuous voice mode
        bool spaceUp = Input.GetKeyUp(KeyCode.Space);
        // Create a strict "Down This Frame" trigger for VR to prevent toggle spamming
        bool isDownThisFrame = (vrButtonDown && !wasVRButtonDownLastFrame) || spaceDown;
        wasVRButtonDownLastFrame = vrButtonDown; // Store for next frame

        if (realtimeClient._pushToTalkOn)
        {
            // Tap-to-talk mode (using voice detection to stop)

            // Start recording on press, but ONLY if we aren't already recording
            if ((spaceDown || vrButtonDown) && !isRecording)
            {
                playEffect("listening");
                isRecording = true;
                StartCoroutine(CaptureAndSendContext());
            }
            // We DO NOT have a stop condition here. 
            // RealtimeGuideClient will detect silence, stop it, and trigger HandleAutoStop().
        }
        else if (realtimeClient._continuousVoiceOn)
        {
            if (isDownThisFrame)
            {
                if (!realtimeClient._isContinuousSessionActive)
                {
                    // Toggle ON
                    //playEffect("listening");
                    realtimeClient._isContinuousSessionActive = true;
                    realtimeClient.StartRecording(); // Opens mic permanently
                    Debug.Log("Continuous Voice Mode: ON");
                }
                else
                {
                    // Toggle OFF
                    //playEffect("done_listening");
                    realtimeClient._isContinuousSessionActive = false;
                    _ = realtimeClient.StopRecordingAndCommit(); // Closes mic
                    Debug.Log("Continuous Voice Mode: OFF");
                }
            }
        }
        else
        {
            // Push-to-talk mode (holding down the button)

            bool isDown = m_VRHandlingScript.isButtonPressed && !isRecording;
            bool isUp = !m_VRHandlingScript.isButtonPressed && isRecording;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                playEffect("listening");
                StartCoroutine(CaptureAndSendContext());
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                playEffect("done_listening");
                _ = realtimeClient.StopRecordingAndCommit();
            }

            // Separate set with flag vars for VR
            if (isDown && !isRecording)
            {
                playEffect("listening");
                isRecording = true; // Lock it immediately
                StartCoroutine(CaptureAndSendContext());
            }

            if (isUp && isRecording)
            {
                playEffect("done_listening");
                isRecording = false; // Unlock
                _ = realtimeClient.StopRecordingAndCommit();
            }
        }

        // Logic to mute the guide
        if (m_VRHandlingScript.isMutingButtonPressed || Input.GetKeyDown(KeyCode.M))
        {
            // Only fire if this is the FIRST frame the button is down
            if (!wasMutingLastFrame)
            {
                _ = realtimeClient.StopAiSpeech();
                wasMutingLastFrame = true; // Lock it
            }
        }
        else
        {
            wasMutingLastFrame = false; // Reset when the user lets go
        }
    }

    private void HandleAutoStop()
    {
        playEffect("done_listening");
        isRecording = false; // Unlock it so they can press the button again later
    }

    // Coroutine for sending info to the realtime API to prevent freezing in VR
    private IEnumerator CaptureAndSendContext()
    {
        // Start Audio Recording immediately
        realtimeClient.StartRecording();
        realtimeClient._isProcessingCommand = false;
        //StartCoroutine(CaptureImageContext());
        yield return null;
    }

    void UpdateVisualContext()
    {
        if (realtimeClient._isConnected)
        {
            StartCoroutine(CaptureImageContext());
        }
    }

    private IEnumerator CaptureImageContext()
    {
        // Trigger the screenshot
        CameraSystem camSystem = FindObjectOfType<CameraSystem>();
        camSystem.CaptureScreenshot();

        // WAIT for the upload to finish without freezing the frame
        // This loop lets the VR headset keep rendering while we wait
        float timeout = 5.0f;
        float timer = 0;
        while (!camSystem.uploaded && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Send the context once we have the links
        if (camSystem.uploaded)
        {
            Debug.Log("Images uploaded. Sending to Vision API...");

            // Call our helper function to get image descriptions from GPT-4
            Task<string> visionTask = realtimeClient.GetImageDescriptionAsync(camSystem.viewpointImageLink, camSystem.birdsEyeImageLink);

            while (!visionTask.IsCompleted)
            {
                yield return null; // Let Unity render the next frame
            }

            string visionDesc = visionTask.Status == TaskStatus.RanToCompletion ? visionTask.Result : "Error reading images .";

            string fullContext = $"[Visual Context] {visionDesc}";

            Debug.Log("Injecting Combined Context: " + fullContext);
            realtimeClient.SendTextContext(fullContext);
        }
        else
        {
            Debug.LogWarning("Image upload timed out");
        }
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

    // Triggers the assignment of the avatar in static conditions (avatar set once at beginning of scene) for confederate clients
    private void AssignSingleConfederate()
    {
        if (GameObject.FindWithTag("Confederate") && !guideRoleAssignedStart)
            StartCoroutine(AssignRoleStatic());
    }
    
    // This function makes it so that the guide role is reassigned every time we go back to having two avatars in the scene
    private void BothConfederatesPresent()
    {
        // Triggers the assignment of the avatar in static conditions (avatar set once at beginning of scene) for confederate clients
        var confederates = GameObject.FindGameObjectsWithTag("Confederate");

        if (confederates.Length == 2 && !guideRoleAssigned)
            StartCoroutine(AssignRoleStatic());
        else
            guideRoleAssigned = false;
    }

    private IEnumerator AssignRoleStatic()
    {
        role = 6;
        yield return new WaitForSeconds(5f);
        // Set avatars to correct roles in separate scenes for the guide
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("Tutorial"))
            role = 1; // human
        else if (currentSceneName.Equals("GuidePark1_Networked"))
            role = 2; // human
        else if (currentSceneName.Equals("GuidePark2_Networked"))
            role = 4; // dog, 4
        else if (currentSceneName.Equals("GuidePark3_Networked"))
            role = 4; // robot, 2

        guideRoleAssigned = true;
        guideRoleAssignedStart = true; // Stops assigning guide role for a single confed client
    }

    private IEnumerator muteAudioSource(AudioSource source, AudioClip clip)
    {
        yield return new WaitForSeconds(clip.length);
        source.mute = true;
    }

    private void playEffect(string clipName)
    {
        switch(clipName)
        {
            case "subway_chime":
                {
                    //Debug.Log("Played arrival effect");
                    sfxAudioSource.loop = false;
                    sfxAudioSource.PlayOneShot(chimeClip);
                    break;
                }
            case "processing":
                {
                    //Debug.Log("Playing processing sound");
                    sfxAudioSource.clip = processingClip;
                    sfxAudioSource.loop = true;
                    sfxAudioSource.Play();
                    break;
                }
            case "listening":
                {
                    sfxAudioSource.mute = false;
                    sfxAudioSource.loop = false;
                    sfxAudioSource.PlayOneShot(listeningClip);
                    break;
                }
            case "done_listening":
                {
                    //Debug.Log("Played done listening sound");
                    if (sfxAudioSource.clip == processingClip) sfxAudioSource.Stop(); // If processing sound was used (in cases of high latency)

                    sfxAudioSource.loop = false;
                    sfxAudioSource.PlayOneShot(doneListeningClip);
                    break;
                }
        }
    }

    private void checkDescriptionRequests()
    {
        //Debug.Log("The guide audio source is " + m_OpenAIQueriesScript.audioSource.gameObject.transform.parent.name + " and is playing " + m_OpenAIQueriesScript.audioSource.isPlaying);
        // Checking if a target GameObject was selected to be modified
        if (m_OpenAIQueriesScript.targetForDescription != null)
        {
            // Call to highlight the game object being described while the guide is talking
            Debug.Log("Has a target to describe: " + m_OpenAIQueriesScript.targetForDescription);

            // If the guide is invisible, see if the local audio player has stopped - else, check the networked one
            if (!isHighlighted)
                HighlightSelectedReaderReference(m_OpenAIQueriesScript.targetForDescription);

            m_OpenAIQueriesScript.targetForDescription = null;
        }
    }

    void HighlightSelectedReaderReference(GameObject selectedReference)
    {
        // Add a glow around the selectedReference + brighten its color
        Material previousMaterial = selectedReference.GetComponent<Renderer>().material;
        selectedReference.GetComponent<Renderer>().material = Resources.Load<Material>("Screenreader/Glow");
        isHighlighted = true;

        // Return selectedReference renderers to normal after coroutine finishes
        StartCoroutine(WaitForTenSeconds(selectedReference, previousMaterial));
    }

    IEnumerator WaitForTenSeconds(GameObject selectedReference, Material previousMaterial)
    {
        yield return new WaitForSeconds(10f);

        selectedReference.GetComponent<Renderer>().material = previousMaterial;
        isHighlighted = false;
    }

    private void checkModificationRequests()
    {
        // Checking if a target GameObject was selected to be modified
        if (m_OpenAIQueriesScript.targetForModification != null)
        {
            // Call to create an audio beacon, then immediately set the target to null so it doesn't continuously call for beacon creation
            // Also calls to highlight the object while the temporary audio beacon exists
            Debug.Log("Has a target to modify: " + m_OpenAIQueriesScript.targetForModification);
            m_AutomaticModificationScript.AddAudioBeacon(m_OpenAIQueriesScript.targetForModification);
            if (!isHighlighted)
                HighlightSelectedReaderReference(m_OpenAIQueriesScript.targetForModification);

            m_OpenAIQueriesScript.targetForModification = null;
        }
    }

    private void checkGuidanceRequests()
    {
        // Checking if a target GameObject was selected to be moved to
        if (m_OpenAIQueriesScript.targetForGuidance != null)
        {
            //Debug.Log("Was passed a target for guidance " + m_OpenAIQueriesScript.targetForGuidance);

            // Calls to highlight the object
            if (!isHighlighted)
                HighlightSelectedReaderReference(m_OpenAIQueriesScript.targetForGuidance);

            //Debug.Log("Has a target to move to: " + m_OpenAIQueriesScript.targetForGuidance);
            m_SharedMovementScript.guideCollider.enabled = true; // Turns guide collider on so it's grabbable when there is a specific move target

            // If the player is grabbing the guide, call for the movement functions as appropriate
            // Turn off guide follow so that the guide begins to lead the player
            if (m_SharedMovementScript.movingWithGuide) // was playerGrabbingGuide
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
                        m_SharedMovementScript.ForceStopAndReset();
                        m_OpenAIQueriesScript.targetForGuidance = null;
                    }
                    else if (distance > 1.5f) // If the guide left the participant behind at some point during guidance and ended by standing more than an arm's reach away
                    {
                        m_GuideFollowScript.enabled = true; // Turn guide follow back on to make the guide return to player
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
                        m_SharedMovementScript.ForceStopAndReset();
                        m_OpenAIQueriesScript.targetForGuidance = null;
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