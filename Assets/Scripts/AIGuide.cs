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

    // Variables for wizard components
    public string result;
    public int role = 1; // 1: human, 2: robot, 3: cane, 4: guide dog, 5: bird, 6: invisible

    // Start is called before the first frame update
    void Start()
    {
        // Find necessary components to the attached GameObject
        m_GuideFollowScript = FindObjectOfType<GuideFollow>(); // On XR Rig

        // Add necessary components to the attached GameObject
        m_AutomaticModificationScript = gameObject.AddComponent<AutomaticModification>();
        m_AutomatedGuideScript = gameObject.AddComponent<AutomaticGuide>();
        m_VRHandlingScript = gameObject.AddComponent<VRHandling>();
        m_OpenAIQueriesScript = gameObject.AddComponent<OpenAIQueries>();

        // Set up realtime client
        realtimeClient = gameObject.AddComponent<RealtimeGuideClient>();

        string basePrompt = GetFormattedPrompt();

        // Load config and connect to client
        realtimeClient.LoadConfig();
        realtimeClient._voiceDetectionOn = false;
        realtimeClient.Connect(basePrompt);

        // This line is needed if we use the invisible guide role
        if (role == 6)
            DisableColliders(FindObjectOfType<GuideRoleSync>().gameObject);

        Debug.Log("AIGuide is active!");
    }

    // For ensuring proper realtime data
    public string GetFormattedPrompt()
    {
        // Ensure data is fresh
        m_OpenAIQueriesScript.LoadRoomDescriptions();
        m_OpenAIQueriesScript.getGuideRole();

        return "You are a " + m_OpenAIQueriesScript.role + ", named Giddy. " + m_OpenAIQueriesScript.contextClassification + " " + m_OpenAIQueriesScript.memoClassifications +
                                     " The names and descriptions of key objects are: " + m_OpenAIQueriesScript.objectClassifications +
                                     " " + m_OpenAIQueriesScript.queryClassifications;
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
        bool isDown = m_VRHandlingScript.isButtonPressed && !isRecording;
        bool isUp = !m_VRHandlingScript.isButtonPressed && isRecording;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(CaptureAndSendContext());
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            _ = realtimeClient.StopRecordingAndCommit();
        }

        // Separate set with flag vars for VR
        if (isDown && !isRecording)
        {
            isRecording = true; // Lock it immediately
            StartCoroutine(CaptureAndSendContext());
        }

        if (isUp && isRecording)
        {
            isRecording = false; // Unlock
            _ = realtimeClient.StopRecordingAndCommit();
        }
    }

    // Coroutine for sending info to the realtime API to prevent freezing in VR
    private IEnumerator CaptureAndSendContext()
    {
        // Start Audio Recording immediately
        realtimeClient.StartRecording();

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
            Debug.Log("Image uploaded. sending to Vision API...");

            // Call our helper function to get image descriptions from GPT-4
            Task<string> viewpointTask = realtimeClient.GetImageDescriptionAsync(camSystem.viewpointImageLink);
            Task<string> birdsEyeTask = realtimeClient.GetImageDescriptionAsync(camSystem.birdsEyeImageLink);

            while (!viewpointTask.IsCompleted || !birdsEyeTask.IsCompleted)
            {
                yield return null; // Let Unity render the next frame
            }

            string viewpointDesc = viewpointTask.Status == TaskStatus.RanToCompletion ? viewpointTask.Result : "Error reading viewpoint.";
            string birdsEyeDesc = birdsEyeTask.Status == TaskStatus.RanToCompletion ? birdsEyeTask.Result : "Error reading bird's eye.";

            string fullContext = $"[Visual Context] Viewpoint: {viewpointDesc} | Bird's Eye: {birdsEyeDesc}";

            Debug.Log("Injecting Combined Context: " + fullContext);
            realtimeClient.SendTextContext(fullContext);
            //realtimeClient.SendImageContext(camSystem.birdsEyeImageLink);
            //realtimeClient.SendImageContext(camSystem.viewpointImageLink);
        }
        else
        {
            Debug.LogError("Screenshot upload timed out!");
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
            case "listening":
                {
                    //Debug.Log("Played listening sound");
                    audioSource.clip = Resources.Load<AudioClip>("Audio/listening");
                    audioSource.mute = false;
                    audioSource.loop = false;
                    audioSource.Play();
                    break;
                }
            case "done_listening":
                {
                    //Debug.Log("Played done listening sound");
                    audioSource.clip = Resources.Load<AudioClip>("Audio/done_listening");
                    audioSource.mute = false;
                    audioSource.loop = false;
                    audioSource.Play();
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
            Debug.Log("Was passed a target for guidance " + m_OpenAIQueriesScript.targetForGuidance);

            // Calls to highlight the object
            if (!isHighlighted)
                HighlightSelectedReaderReference(m_OpenAIQueriesScript.targetForGuidance);

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
                        m_SharedMovementScript.playerGrabbingGuide = false; // Mark as false when we reach the destination to reset grab for next call
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
                        m_SharedMovementScript.playerGrabbingGuide = false; // Mark as false when we reach the destination to reset grab for next call
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