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
    private SwitchTools m_SwitchToolsScript;

    // Variables for monitoring
    private bool guideRoleAssigned = false;
    private bool guideRoleAssignedStart = false;

    private GameObject lastHighlightedTarget;
    private Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();

    private bool isRecording = false;

    private bool wasMutingLastFrame = false;
    private bool wasVRButtonDownLastFrame = false;

    // Variables for hazard detection
    private bool hazardDetectionEnabled = true; // hazard detection feature toggle
    private float dangerZoneDistance = 1.5f;
    private float hazardCheckInterval = 0.25f; // hazard detection frequency (see CheckHazardDistances())
    private float hazardPromptCooldown = 6.0f; // response frequency from guide
    private LayerMask hazardLayerMask;
    private int maxHazardsDetected = 10;
    private Collider[] hazardObjectColliders;
    private float nextHazardCheckTime = 0f;
    private float lastHazardPromptTime = -999f;
    private GameObject lastHazardPrompted;
    private Vector3 previousPlayerPosition;
    private Vector3 trueWorldVelocity;
    public Transform headsetTransform;

    // Variables for prompting the user if they need assistance
    private float lastPlayerInteractionTime;
    private float idleTimeout = 60f; 
    private bool hasPromptedForHelp = false;

    // Variables for continuous description during routes
    private bool isDescribingRoute = false;
    private Coroutine routeDescriptionCoroutine;
    private string destination;

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

        InvokeRepeating("UpdateVisualContext", 2.0f, 7.0f);

        Debug.Log("AIGuide is active!");

        hazardLayerMask = LayerMask.GetMask("Key Items");
        hazardObjectColliders = new Collider[maxHazardsDetected];
    }

    private void AddGuideComponents()
    {
        // Find necessary components to the attached GameObject
        m_GuideFollowScript = FindObjectOfType<GuideFollow>(); // On XR Rig
        m_SwitchToolsScript = FindObjectOfType<SwitchTools>(); // On Room Manager

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
        realtimeClient._legacyHoldToSpeakOn = m_SwitchToolsScript != null && m_SwitchToolsScript.legacyHoldToSpeak;
        realtimeClient._continuousVoiceOn = m_SwitchToolsScript != null && m_SwitchToolsScript.continuousVoice;
        realtimeClient._defaultPushToTalkOn = m_SwitchToolsScript == null || m_SwitchToolsScript.UseDefaultPushToTalk;

        switch (m_SwitchToolsScript.activeGuideType)
        {
            case SwitchTools.GuideType.Baseline:
                realtimeClient.Connect(basePrompt, true); // tell the client which type of initial session update to pass
                break;
            case SwitchTools.GuideType.AllCombined:
                realtimeClient.Connect(basePrompt, true); // tell the client which type of initial session update to pass
                break;
            default:
                realtimeClient.Connect(basePrompt, false);
                break;
        }

        realtimeClient.OnAutoStopRecording += HandleAutoStop; // Subscribe to the event of whenever the client auto-stops (detected a user stopped speaking)
        m_SwitchToolsScript.OnGuideConfigurationChanged += HandleGuideTypeChanged;
        //realtimeClient.OnServerDetectedSpeechStart += () => playEffect("listening"); // Subscribe to event of detecting a user's speech  starting (continuous voice)
        //realtimeClient.OnServerDetectedSpeechStop += () => playEffect("done_listening"); // Subscribe to event of detecting a user's speech stopping (continuous voice)

        // Reset player interaction time with the client
        lastPlayerInteractionTime = Time.time;
    }

    // For ensuring proper realtime data
    public string GetFormattedPrompt()
    {
        // Ensure data is fresh
        m_OpenAIQueriesScript.LoadRoomDescriptions();
        m_OpenAIQueriesScript.getGuideRole();

        // Determine baseline or version of improved guide
        string prompt = "";

        switch (m_SwitchToolsScript.activeGuideType)
        {
            case SwitchTools.GuideType.Baseline:
                Debug.Log("Using the baseline guide!");
                prompt = "You are Giddy, a " + m_OpenAIQueriesScript.role + ". You are a sighted guide for a blind player. " + m_OpenAIQueriesScript.contextClassification +
                   " THE NAVIGATION REGISTRY: Names and descriptions of objects in the scene. When following navigation or modification commands, use ONLY these names: " + m_OpenAIQueriesScript.objectClassifications +
                   m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.guideRules; // used to have + m_OpenAIQueriesScript.commandClassifications
                break;
            case SwitchTools.GuideType.ObjectDescription:
                Debug.Log("Using the object description guide!");
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                    $"The player is asking you about what an object looks like. {m_OpenAIQueriesScript.objectDescriptionGuideline}";
                break;
            case SwitchTools.GuideType.ObjectLocation:
                Debug.Log("Using the object location guide!");
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                    $"The player is asking you about where an object is. {m_OpenAIQueriesScript.objectLocationGuideline}";
                break;
            case SwitchTools.GuideType.SceneUnderstanding:
                Debug.Log("Using the scene understanding guide!");
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                    $"The player is asking you about what the scene around you both is like. {m_OpenAIQueriesScript.sceneUnderstandingGuideline}";
                break;
            case SwitchTools.GuideType.Navigation:
                Debug.Log("Using the navigation guide!");
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                    $"The player is asking you for information to help with navigating somewhere on their own. { m_OpenAIQueriesScript.spaceNavigationGuideline}";
                break;
            case SwitchTools.GuideType.ObjectGrabbing:
                Debug.Log("Using the object grabbing guide!");
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                    $"The player is asking you to help them grab an object. {m_OpenAIQueriesScript.grabbingObjectGuideline}";
                break;
            case SwitchTools.GuideType.SightedGuidance:
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                $"The player wants help moving to a specific object. THE NAVIGATION REGISTRY: {m_OpenAIQueriesScript.objectClassifications}";
                break;
            case SwitchTools.GuideType.AllCombined:
                Debug.Log("Using the all-guideline intention guide!");
                StringBuilder sbPrompt = new StringBuilder();

                // Base Persona & Rules
                sbPrompt.AppendLine($"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player.");
                sbPrompt.AppendLine(m_OpenAIQueriesScript.contextClassification);
                sbPrompt.AppendLine($"THE NAVIGATION REGISTRY: {m_OpenAIQueriesScript.objectClassifications}");
                // New guideline on trust/revealing uncertainty
                sbPrompt.Append(m_OpenAIQueriesScript.trustGuideline);

                // Command functions for guidance, teleportation, and modification are handled by the tools architecture native to Realtime

                // Conditional Behavioral Guidelines
                sbPrompt.AppendLine("\n### CONDITIONAL GUIDELINES ###");
                sbPrompt.AppendLine("Depending on what the user asks, apply the following rules. If the user has multiple intents, combine the rules naturally.");

                sbPrompt.AppendLine("\nIF THE USER WANTS AN OBJECT DESCRIPTION:");
                sbPrompt.AppendLine(m_OpenAIQueriesScript.objectDescriptionGuideline);

                sbPrompt.AppendLine("\nIF THE USER IS LOCATING A SPECIFIC OBJECT:");
                sbPrompt.AppendLine(m_OpenAIQueriesScript.objectLocationGuideline);

                sbPrompt.AppendLine(m_OpenAIQueriesScript.sceneUnderstandingGuideline);

                sbPrompt.AppendLine("\nIF THE USER WANTS INFORMATION TO HELP THEM NAVIGATE SOMEWHERE ON THEIR OWN:");
                sbPrompt.AppendLine(m_OpenAIQueriesScript.spaceNavigationGuideline);

                sbPrompt.AppendLine("\nIF THE USER IS REACHING FOR OR GRABBING AN OBJECT:");
                sbPrompt.AppendLine(m_OpenAIQueriesScript.grabbingObjectGuideline);

                sbPrompt.AppendLine("\nIF THE USER NEEDS TECHNICAL SUPPORT:");
                sbPrompt.AppendLine(m_OpenAIQueriesScript.technicalSupportGuideline);

                prompt = sbPrompt.ToString();
                break;
            default:
                // Using the improved guide, but haven't set a specific intention yet
                Debug.Log("Providing only basic information/introduction to the guide session!");
                prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}";
                break;
        }
        return prompt;
    }
    /*public string GetFormattedPrompt()
    {
        // Ensure data is fresh
        m_OpenAIQueriesScript.LoadRoomDescriptions();
        m_OpenAIQueriesScript.getGuideRole();

        // Determine baseline or version of improved guide
        string prompt;

        if (m_SwitchToolsScript.baselineGuide)
        {
            Debug.Log("Using the baseline guide!");
            prompt = "You are Giddy, a " + m_OpenAIQueriesScript.role + ". You are a sighted guide for a blind player. " + m_OpenAIQueriesScript.contextClassification +
               " THE NAVIGATION REGISTRY: Names and descriptions of objects in the scene. When following navigation or modification commands, use ONLY these names: " + m_OpenAIQueriesScript.objectClassifications +
               m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.guideRules; // used to have + m_OpenAIQueriesScript.commandClassifications
        }
        else if (m_SwitchToolsScript.objectDescriptionGuide)
        {
            Debug.Log("Using the object description guide!");
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                $"The player is asking you about what an object looks like. {m_OpenAIQueriesScript.objectDescriptionGuideline}";
        }
        else if (m_SwitchToolsScript.objectLocationGuide)
        {
            Debug.Log("Using the object location guide!");
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" + 
                $"The player is asking you about where an object is. {m_OpenAIQueriesScript.objectLocationGuideline}";
        }
        else if (m_SwitchToolsScript.sceneUnderstandingGuide)
        {
            Debug.Log("Using the scene understanding guide!");
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                $"The player is asking you about what the scene around you both is like. {m_OpenAIQueriesScript.sceneUnderstandingGuideline}";
        }
        else if (m_SwitchToolsScript.navigationGuide)
        {
            Debug.Log("Using the navigation guide!");
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                $"The player is asking you for information to help with navigating somewhere on their own. { m_OpenAIQueriesScript.spaceNavigationGuideline}";
        }
        else if (m_SwitchToolsScript.objectGrabbingGuide)
        {
            Debug.Log("Using the object grabbing guide!");
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                $"The player is asking you to help them grab an object. {m_OpenAIQueriesScript.grabbingObjectGuideline}";
        }
        else if (m_SwitchToolsScript.sightedGuidanceGuide)
        {
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}" +
                $"The player wants help moving to a specific object. THE NAVIGATION REGISTRY: {m_OpenAIQueriesScript.objectClassifications}";
        }
        else if (m_SwitchToolsScript.allCombinedGuide) // Deprecated for now - won't be using unless we learn more about how it determines intention
        {
            Debug.Log("Using the all-guideline intention guide!");
            StringBuilder sbPrompt = new StringBuilder();

            // Base Persona & Rules
            sbPrompt.AppendLine($"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player.");
            sbPrompt.AppendLine(m_OpenAIQueriesScript.contextClassification);
            sbPrompt.AppendLine($"THE NAVIGATION REGISTRY: {m_OpenAIQueriesScript.objectClassifications}");
            // New guideline on trust/revealing uncertainty
            sbPrompt.Append(m_OpenAIQueriesScript.trustGuideline);
            
            // Command functions for guidance, teleportation, and modification are handled by the tools architecture native to Realtime

            // Conditional Behavioral Guidelines
            sbPrompt.AppendLine("\n### CONDITIONAL GUIDELINES ###");
            sbPrompt.AppendLine("Depending on what the user asks, apply the following rules. If the user has multiple intents, combine the rules naturally.");

            sbPrompt.AppendLine("\nIF THE USER WANTS AN OBJECT DESCRIPTION:");
            sbPrompt.AppendLine(m_OpenAIQueriesScript.objectDescriptionGuideline);

            sbPrompt.AppendLine("\nIF THE USER IS LOCATING A SPECIFIC OBJECT:");
            sbPrompt.AppendLine(m_OpenAIQueriesScript.objectLocationGuideline);

            sbPrompt.AppendLine(m_OpenAIQueriesScript.sceneUnderstandingGuideline);

            sbPrompt.AppendLine("\nIF THE USER WANTS INFORMATION TO HELP THEM NAVIGATE SOMEWHERE ON THEIR OWN:");
            sbPrompt.AppendLine(m_OpenAIQueriesScript.spaceNavigationGuideline);

            sbPrompt.AppendLine("\nIF THE USER IS REACHING FOR OR GRABBING AN OBJECT:");
            sbPrompt.AppendLine(m_OpenAIQueriesScript.grabbingObjectGuideline);

            sbPrompt.AppendLine("\nIF THE USER NEEDS TECHNICAL SUPPORT:");
            sbPrompt.AppendLine(m_OpenAIQueriesScript.technicalSupportGuideline);

            prompt = sbPrompt.ToString();
        }
        else
        {
            // Using the improved guide, but haven't set a specific intention yet
            Debug.Log("Providing only basic information/introduction to the guide session!");
            prompt = $"You are Giddy, a warm, friendly, but still professional sighted guide for a blind player. {m_OpenAIQueriesScript.contextClassification}";
        }

        return prompt;
    }*/

    // Handles event of user done talking
    private void HandleAutoStop()
    {
        playEffect("done_listening");
        isRecording = false; // Unlock it so they can press the button again later
    }

    // Checks if a new guide type was assigned and switches prompts accordingly
    public async void HandleGuideTypeChanged()
    {
        if (realtimeClient == null) return;

        // Regenerate the fresh prompt string based on the newly toggled bools
        string freshPrompt = GetFormattedPrompt();

        // Determine which type of update this is (guidance updates use the tools structure) and push to OpenAI
        if (m_SwitchToolsScript.activeGuideType.Equals(SwitchTools.GuideType.SightedGuidance))
            await realtimeClient.UpdateGuidancePrompt(freshPrompt);
        else
            await realtimeClient.UpdateLivePrompt(freshPrompt);

        /*if (m_SwitchToolsScript.sightedGuidanceGuide)
            await realtimeClient.UpdateGuidancePrompt(freshPrompt);
        else
            await realtimeClient.UpdateLivePrompt(freshPrompt);*/

        Debug.Log($"Guide version shifted successfully. Guide was told: {freshPrompt}");
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

        // If we're in a scene run from a guide client
        if (FindObjectOfType<GuideFollow>())
        {
            // Call the guide
            RealtimeGuide();

            // Only perform the following for improved guides - not the baseline
            if (!m_SwitchToolsScript.activeGuideType.Equals(SwitchTools.GuideType.Baseline))
            {
                // See if the player has been silent for a while
                CheckForIdlePlayer();

                // Check the player's velocity so we can determine hazards
                checkPlayerVelocity();

                // Check for objects too close to the player
                if (!isDescribingRoute) // prevent the hazard alerts from interrupting the guidance descriptions
                    CheckHazardDistances();
            }

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

    private void CheckForIdlePlayer()
    {
        // If the AI hasn't prompted yet, and 60 seconds have passed since the last interaction
        if (!hasPromptedForHelp && (Time.time - lastPlayerInteractionTime) >= idleTimeout)
        {
            TriggerHelpPrompt();
        }
    }

    private void checkPlayerVelocity()
    {
        // Assign the headset transform
        if (headsetTransform == null)
            headsetTransform = m_SharedMovementScript.playerRig.Camera.transform;
        else
        {
            // Calculate true world-space velocity based on position delta
            // We ignore the Y axis to strictly track ground-plane movement
            Vector3 currentPos = new Vector3(headsetTransform.position.x, 0, headsetTransform.position.z);
            Vector3 prevPos = new Vector3(previousPlayerPosition.x, 0, previousPlayerPosition.z);

            trueWorldVelocity = (currentPos - prevPos) / Time.deltaTime;
            previousPlayerPosition = headsetTransform.position;
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

        if (realtimeClient._defaultPushToTalkOn)
        {
            // Default push-to-talk mode: press once, then auto-stop after silence.

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
                    playEffect("listening");
                    realtimeClient._isContinuousSessionActive = true;
                    realtimeClient.StartRecording(); // Opens mic permanently
                    Debug.Log("Continuous Voice Mode: ON");
                }
                else
                {
                    // Toggle OFF
                    playEffect("done_listening");
                    realtimeClient._isContinuousSessionActive = false;
                    _ = realtimeClient.StopRecordingSilently(); // Closes mic
                    Debug.Log("Continuous Voice Mode: OFF");
                }
            }
        }
        else
        {
            // Legacy hold-to-speak mode.

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

            // Call our helper function to get image descriptions from GPT-4
            Task<string> visionTask = realtimeClient.GetImageDescriptionAsync(camSystem.viewpointImageBase64, camSystem.birdsEyeImageBase64);

            while (!visionTask.IsCompleted)
            {
                yield return null; // Let Unity render the next frame
            }

            string visionDesc = visionTask.Status == TaskStatus.RanToCompletion ? visionTask.Result : "Error reading images .";

            string fullContext = $"[Visual Context] {visionDesc}";

            //Debug.Log("Injecting Combined Context: " + fullContext);
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

    // This is never actually used to highlight objects of targeted description - leave it for now, since a user probably wants to see the normal appearance of what's being described
    private void checkDescriptionRequests()
    {
        //Debug.Log("The guide audio source is " + m_OpenAIQueriesScript.audioSource.gameObject.transform.parent.name + " and is playing " + m_OpenAIQueriesScript.audioSource.isPlaying);
        // Checking if a target GameObject was selected to be descsribed
        if (m_OpenAIQueriesScript.targetForDescription != null)
        {
            // Call to highlight the game object being described while the guide is talking
            Debug.Log("Has a target to describe: " + m_OpenAIQueriesScript.targetForDescription);

            m_OpenAIQueriesScript.targetForDescription = null;
        }
    }

    void HighlightSelectedReaderReference(GameObject selectedReference)
    {
        // Add a glow around the selectedReference + brighten its color
        Renderer renderer = selectedReference.GetComponent<Renderer>();
        if (!originalMaterials.ContainsKey(selectedReference))
            originalMaterials.Add(selectedReference, renderer.material);

        renderer.material = Resources.Load<Material>("Screenreader/Glow");
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
            Debug.Log("Has a target to modify: " + m_OpenAIQueriesScript.targetForModification);

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

                    // Ensure targetActive is true so we don't start it the same frame we reach the goal
                    if (m_AutomatedGuideScript.targetActive)
                    {
                        StartRouteDescriptions(currentTarget.name);
                    }

                    // If they reach the target, make it stop grabbing and stop moving
                    if (!m_AutomatedGuideScript.targetActive)
                    {
                        StopRouteDescriptions(); // stop the guide from talking

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
                StopRouteDescriptions(); // keep the guide from talking if there are no targets

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
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
    }

    private void CheckHazardDistances()
    {
        if (!hazardDetectionEnabled || Time.time < nextHazardCheckTime) return;
        nextHazardCheckTime = Time.time + hazardCheckInterval;

        // CRAMPED SPACE FILTER: Use the new true velocity
        if (trueWorldVelocity.magnitude < 0.3f) return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            headsetTransform.position,
            dangerZoneDistance,
            hazardObjectColliders,
            hazardLayerMask
        );

        GameObject bestCandidate = null;
        float highestUrgency = -1f;
        Vector3 normalizedVelocity = trueWorldVelocity.normalized;

        float playerRadius = 0.45f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hazardObjectColliders[i];
            if (hit == null) continue;

            // Flatten positions to the XZ plane to avoid false positives from ceiling/floor height differences
            Vector3 hazardPoint = hit.ClosestPoint(headsetTransform.position);
            Vector3 flatHazardPoint = new Vector3(hazardPoint.x, 0, hazardPoint.z);
            Vector3 flatPlayerPoint = new Vector3(headsetTransform.position.x, 0, headsetTransform.position.z);

            Vector3 vectorToHazard = flatHazardPoint - flatPlayerPoint;

            // 1. DIRECTION FILTER: Ensure the object is in the direction of movement
            float forwardAlignment = Vector3.Dot(normalizedVelocity, vectorToHazard.normalized);
            if (forwardAlignment < 0.1f) continue;

            // 2. TRAJECTORY FILTER: Cylinder check
            Vector3 projectedPath = Vector3.Project(vectorToHazard, normalizedVelocity);
            float perpendicularDistance = Vector3.Distance(vectorToHazard, projectedPath);

            if (perpendicularDistance > (playerRadius + 0.1f)) continue;

            float distance = Vector3.Distance(flatPlayerPoint, flatHazardPoint);

            // CALCULATE URGENCY
            float urgency = forwardAlignment / (distance + 0.1f);

            if (urgency > highestUrgency)
            {
                highestUrgency = urgency;
                bestCandidate = hit.gameObject;
            }
        }

        if (bestCandidate != null && ShouldPrompt(bestCandidate))
        {
            HandleHazardPrompt(bestCandidate);
        }
    }

    private bool ShouldPrompt(GameObject hazard)
    {
        bool cooldownReady = Time.time - lastHazardPromptTime >= hazardPromptCooldown;
        bool isDifferentHazard = hazard != lastHazardPrompted;

        // In a cramped space, we increase the cooldown
        return cooldownReady || isDifferentHazard;
    }

    private void HandleHazardPrompt(GameObject hazard)
    {
        string hazardName = hazard.name;
        string prompt = $"The player is approaching {hazardName}. " +
                        "Let them know briefly and clearly. DO NOT add any extra fluff. For example: " +
                        "Oh, you're about to walk into a tree. Be careful." +
                        "If you keep going forward, you'll walk straight into the tall building." +
                        "I think you're getting too close to a bush. You might want to step to the side to move around it." +
                        "You're coming up on the tall building now. " +
                        "Use the conversation history to see how you've warned the player before and change up your language.";
        // Debug.Log("Hazard Detection Response: " + prompt);

        _ = realtimeClient.SendManualPrompt(prompt);
    }

    // May need to think about selectively sending hazards // making a harsher cooldown timer between when it can warn

    private void TriggerHelpPrompt()
    {
        // Immediately set to true to prevent firing multiple times
        hasPromptedForHelp = true;

        // Frame the prompt for the LLM so it knows the context of why it is speaking
        string prompt = "The player has been silent for a minute. " +
                        "Proactively, briefly, and naturally ask them if they need any help, guidance, or directions. For example: " +
                        "Hey, I noticed you've been quiet for a while. Do you need any help?" +
                        "Is there anything I can help you with?" +
                        "Remember, I can provide directions or guidance if you need it. Just let me know.";

        // Send to your existing client
        _ = realtimeClient.SendManualPrompt(prompt);
    }

    // Resets the interaction timer between player and guide (idle player timer)
    public void RecordPlayerInteraction()
    {
        lastPlayerInteractionTime = Time.time;
        hasPromptedForHelp = false; // Reset the flag so the AI can check in again later
    }

    private void StartRouteDescriptions(string targetName)
    {
        if (!isDescribingRoute)
        {
            isDescribingRoute = true;
            routeDescriptionCoroutine = StartCoroutine(RouteDescriptionLoop(targetName));
            destination = targetName;
        }
    }

    private void StopRouteDescriptions()
    {
        if (isDescribingRoute)
        {
            isDescribingRoute = false;
            if (routeDescriptionCoroutine != null)
            {
                StopCoroutine(routeDescriptionCoroutine);
                routeDescriptionCoroutine = null;
            }

            // Clear the backlog queue and active audio so it doesn't read the outdated, back-up prompts
            if (realtimeClient != null)
            {
                realtimeClient.StopAiSpeech();

                // Send a final description of what the user arrived at
                string prompt = $"We arrived at our destination: {destination}." +
                                $"Look at your latest visual context. Briefly describe what the user is looking at, focused on the destination, and inform them that they arrived.";

                _ = realtimeClient.SendManualPrompt(prompt);

                destination = ""; // Reset destination until the next target
            }
        }
    }

    private IEnumerator RouteDescriptionLoop(string targetName)
    {
        // Wait a second before starting so the guide doesn't speak over the initial "Let's go" sound/prompt
        yield return new WaitForSeconds(1.5f);

        // Set a minimum silence duration between descriptions
        float minimumSilenceInterval = 5.0f; // testing natural conversation intervals while walking

        while (isDescribingRoute)
        {
            string prompt = $"We are currently navigating towards the {targetName}. " +
                            $"Look at your latest visual context. Briefly describe ONE important, NEW object the user is walking past right now. " +
                            $"The object you choose should be relevant to what a blind person being guided would want to hear about as they're being helped around. " +
                            $"If there is an avatar dressed in gray clothing with black hair, don't mention it. " +
                            //$"You should only give one simple sentence with no more than ten words, but change up your sentence structure regularly. For example: " +
                            $"You should only give one simple sentence, but change up your sentence structure regularly. For example: " +
                            "We're walking down a street lined with cartoonish trees." +
                            "We're passing a short, colorful building with a flat roof." +
                            "We're going in between a line of puffy green trees." +
                            "There's a patch of colorful flowers to your left.";

            _ = realtimeClient.SendManualPrompt(prompt);

            // Wait 1-2 seconds to allow the network request to go out
            yield return new WaitForSeconds(1.5f);

            // Now wait for the AI to actually finish talking
            yield return new WaitUntil(() => !realtimeClient._isAiSpeaking);

            // Force the AI to be quiet for X seconds before looking for the next object -
            // prevents the AI from spamming descriptions of the same area
            yield return new WaitForSeconds(minimumSilenceInterval);
        }
    }
}
