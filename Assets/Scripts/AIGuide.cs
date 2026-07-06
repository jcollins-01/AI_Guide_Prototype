using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
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
    private RealtimeGuideClient realtimeClient;
    private CameraSystem camSystem;

    // Variables for monitoring
    private bool guideRoleAssigned = false;
    private bool guideRoleAssignedStart = false;

    private GameObject lastHighlightedTarget;
    private Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();

    private bool isRecording = false;

    private bool wasMutingLastFrame = false;

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
        realtimeClient._defaultPushToTalkOn = true;

        realtimeClient.Connect(basePrompt, true); // tell the client which type of initial session update to pass

        realtimeClient.OnAutoStopRecording += HandleAutoStop; // Subscribe to the event of whenever the client auto-stops (detected a user stopped speaking)
    }

    // For ensuring proper realtime data
    public string GetFormattedPrompt()
    {
        // Ensure data is fresh
        m_OpenAIQueriesScript.LoadRoomDescriptions();
        m_OpenAIQueriesScript.getGuideRole();

        // Determine baseline or version of improved guide
        string prompt = "";

        Debug.Log("Using the baseline guide!");
        prompt = "You are Giddy, a " + m_OpenAIQueriesScript.role + ". You are a sighted guide for a blind player. " + m_OpenAIQueriesScript.contextClassification +
           " THE NAVIGATION REGISTRY: Names and descriptions of objects in the scene. When following navigation or modification commands, use ONLY these names: " + m_OpenAIQueriesScript.objectClassifications +
           m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.guideRules; // used to have + m_OpenAIQueriesScript.commandClassifications

        return prompt;
    }

    // Handles event of user done talking
    private void HandleAutoStop()
    {
        playEffect("done_listening");
        isRecording = false; // Unlock it so they can press the button again later
    }

    // Update is called once per frame
    void Update()
    {
        // Calls until the appropriate scripts are assigned (when we have a player and a guide)
        // Needed for access to the player's interactions with the guide + sharing guide audio over network + adding/accessing camera system
        getSharedMovement();

        // If we're in a scene run from a guide client
        if (FindObjectOfType<GuideFollow>())
        {
            // Call the guide
            RealtimeGuide();

            // Determine if guidance is required based on GPT-4 response
            checkGuidanceRequests();

            // Determine if modification is required based on GPT-4 response
            checkModificationRequests();

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
        // Create a strict "Down This Frame" trigger for VR to prevent toggle spamming

        // Default push-to-talk mode: press once, then auto-stop after silence
        // Start recording on press, but ONLY if we aren't already recording
        if ((spaceDown || vrButtonDown) && !isRecording)
        {
            playEffect("listening");
            isRecording = true;
            StartCoroutine(CaptureAndSendContext());
        }

        // Logic to mute the guide
        if (m_VRHandlingScript.isMutingButtonPressed || Input.GetKeyDown(KeyCode.M))
        {
            // Only fire if this is the FIRST frame the button is down
            if (!wasMutingLastFrame)
            {
                _ = realtimeClient.StopAiSpeech();
                // Failsafe: if guide is taking the user somewhere and gets stuck, mute cancels guidance and resets
                m_AutomatedGuideScript.CancelGuidance();
                m_GuideFollowScript.enabled = true;
                m_SharedMovementScript.guideCollider.enabled = false;
                m_SharedMovementScript.ForceStopAndReset();
                m_OpenAIQueriesScript.targetForGuidance = null;
                wasMutingLastFrame = true; // Lock it
            }
        }
        else
        {
            wasMutingLastFrame = false; // Reset when the user lets go
        }
    }

    // Coroutine for sending info to the realtime API to prevent freezing in VR
    private IEnumerator CaptureAndSendContext()
    {
        // Start Audio Recording immediately
        realtimeClient.StartRecording();
        realtimeClient._isProcessingCommand = false;
        StartCoroutine(CaptureImageContext());
        yield return null;
    }

    private IEnumerator CaptureImageContext()
    {
        // Trigger the screenshot
        camSystem.converted = false;
        camSystem.CaptureScreenshot();

        // Wait for the capture to finish and convert
        while (!camSystem.converted)
        {
            yield return null; 
        }

        // Send the context once we have the links
        if (camSystem.converted)
        {
            //Debug.Log("Images converted. Sending to Vision API...");
            realtimeClient.SendVisualContext(camSystem.viewpointImageBase64, camSystem.birdsEyeImageBase64);
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

    void HighlightSelectedReaderReference(GameObject selectedReference)
    {
        // Add a glow around the selectedReference + brighten its color
        Renderer renderer = selectedReference.GetComponent<Renderer>();
        if (!originalMaterials.ContainsKey(selectedReference))
            originalMaterials.Add(selectedReference, renderer.material);

        renderer.material = Resources.Load<Material>("Materials/Glow");
    }

    void ClearPreviousHighlight(GameObject selectedReference)
    {
        //selectedReference.GetComponent<Renderer>().material = previousMaterial;

        if (originalMaterials.TryGetValue(selectedReference, out Material originalMat))
        {
            selectedReference.GetComponent<Renderer>().material = originalMat;
            originalMaterials.Remove(selectedReference); // Clean up the dictionary
        }
    }

    IEnumerator ClearAfterTenSeconds(GameObject selectedReference)
    {
        yield return new WaitForSeconds(10f);
        ClearPreviousHighlight(selectedReference);
    }

    private void checkModificationRequests()
    {
        // Checking if a target GameObject was selected to be modified
        if (m_OpenAIQueriesScript.targetForModification != null)
        {
            // Call to create an audio beacon, then immediately set the target to null so it doesn't continuously call for beacon creation
            // Also calls to highlight the object while the temporary audio beacon exists
            //Debug.Log("Has a target to modify: " + m_OpenAIQueriesScript.targetForModification);

            GameObject currentTarget = m_OpenAIQueriesScript.targetForModification;

            if (lastHighlightedTarget != currentTarget)
            {
                HighlightSelectedReaderReference(currentTarget);
                lastHighlightedTarget = currentTarget;
            }

            m_AutomaticModificationScript.AddAudioBeacon(currentTarget);
            // Wait for 10 seconds (length of audio beacon), then clear material
            StartCoroutine(ClearAfterTenSeconds(currentTarget));
            m_OpenAIQueriesScript.targetForModification = null;
            lastHighlightedTarget = null;
        }
    }

    private void checkGuidanceRequests()
    {
        // Checking if a target GameObject was selected to be moved to
        if (m_OpenAIQueriesScript.targetForGuidance != null)
        {
            //Debug.Log("Was passed a target for guidance " + m_OpenAIQueriesScript.targetForGuidance);

            GameObject currentTarget = m_OpenAIQueriesScript.targetForGuidance;

            if (lastHighlightedTarget != currentTarget)
            {
                HighlightSelectedReaderReference(currentTarget);
                lastHighlightedTarget = currentTarget;
            }

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
                    m_AutomatedGuideScript.GuideToPosition(currentTarget); // was openAiQueries.targetForGuidance
                    // Calculate the distance between thePlayer and the current GameObject to monitor for player getting disconnected
                    float distance = Vector3.Distance(transform.position, m_SharedMovementScript.thePlayer.transform.position);

                    // If they reach the target, make it stop grabbing and stop moving
                    if (!m_AutomatedGuideScript.targetActive)
                    {
                        m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
                        m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
                        playEffect("subway_chime");
                        m_SharedMovementScript.ForceStopAndReset();
                        ClearPreviousHighlight(currentTarget);
                        m_OpenAIQueriesScript.targetForGuidance = null;
                        lastHighlightedTarget = null;
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
                        //Debug.Log("Guide reached the object");
                        StartCoroutine(DelayGuideStopDuringTeleport(currentTarget));
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

    IEnumerator DelayGuideStopDuringTeleport(GameObject currentTarget)
    {
        yield return new WaitForSeconds(1f);
        m_GuideFollowScript.enabled = true; // Turn guide follow back on if no target is given to the guide
        m_SharedMovementScript.guideCollider.enabled = false; // Turns collider off so guide won't be grabbed accidentally as it follows the player
        playEffect("subway_chime");
        m_SharedMovementScript.ForceStopAndReset();
        ClearPreviousHighlight(currentTarget);
        m_OpenAIQueriesScript.targetForGuidance = null;
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
        {
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
            // Can grab the camSystem once the shared movement script has been added + has added its own camera
            if (camSystem == null && m_SharedMovementScript.camera != null)
                camSystem = m_SharedMovementScript.camera;
        }
    }
}
