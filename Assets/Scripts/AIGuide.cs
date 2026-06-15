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

    // Variables for monitoring
    private bool guideRoleAssigned = false;
    private bool guideRoleAssignedStart = false;
    private bool isHighlighted = false;
    private GameObject lastHighlightedTarget;
    private Material previousMaterial;
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
        SwitchTools switchTools = FindObjectOfType<SwitchTools>();

        string basePrompt = GetFormattedPrompt();

        // Load config and connect to client
        realtimeClient.LoadConfig();
        realtimeClient._legacyHoldToSpeakOn = switchTools != null && switchTools.legacyHoldToSpeak;
        realtimeClient._continuousVoiceOn = switchTools != null && switchTools.continuousVoice;
        realtimeClient._defaultPushToTalkOn = switchTools == null || switchTools.UseDefaultPushToTalk;
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

        // Determine baseline or improved guide
        string prompt;
        bool baseline = true; // was false

        if (baseline)
        {
            Debug.Log("Using the baseline guide!");
            prompt = "You are Giddy, a " + m_OpenAIQueriesScript.role + ". You are a sighted guide for a blind player. " + m_OpenAIQueriesScript.contextClassification +
               " THE NAVIGATION REGISTRY: Names and descriptions of objects in the scene. When following navigation or modification commands, use ONLY these names: " + m_OpenAIQueriesScript.objectClassifications +
               m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.guideRules; // used to have + m_OpenAIQueriesScript.commandClassifications
        }
        else
        {
            Debug.Log("Using the improved intention guide!");
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

        return prompt;
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

            // Check for objects too close to the player
            CheckHazardDistances();

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
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
    }

    /*private void CheckHazardDistances()
    {
        if (!hazardDetectionEnabled) 
        {
            return;
        };

        if (Time.time < nextHazardCheckTime)
        {
            return;
        };
        nextHazardCheckTime = Time.time + hazardCheckInterval;

        Transform playerTransform = m_SharedMovementScript.thePlayer.transform;
        int hitCount = Physics.OverlapSphereNonAlloc(
            playerTransform.position,
            dangerZoneDistance,
            hazardObjectColliders,
            hazardLayerMask
        );

        if (hitCount <= 0)
        {
            return;
        };

        // for now, we will just choose the closest object (but there could be many more in the "danger zone")
        GameObject closestHazard = null; 
        float closestDistance = Mathf.Infinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hazardObjectColliders[i];
            if (hit == null)
            {
                continue;
            }

            GameObject hazard = hit.gameObject;
            float distance = Vector3.Distance(playerTransform.position, hazard.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestHazard = hazard;
            }
        }

        if (closestHazard == null)
        {
            return;
        };

        bool cooldownReady = Time.time - lastHazardPromptTime >= hazardPromptCooldown;
        bool isDifferentHazard = closestHazard != lastHazardPrompted;
        // we do not want the same hazard to be repeated too much
        // but new hazards will always be prompted immediately
        if (!cooldownReady && !isDifferentHazard)
        {
            return;
        };
        // Debug.Log(lastHazardPromptTime);
        lastHazardPromptTime = Time.time;
        lastHazardPrompted = closestHazard;

        string hazardName = closestHazard.name;
        string prompt = $"Hazard detected: {hazardName}. " + $"The player is too close to this object. " +
                        $"Warn the player briefly and clearly. " + $"Mention the object by name. Do not wait for the player to speak.";
        // Debug.Log("Hazard Detection Response: " + prompt);

        _ = realtimeClient.SendManualPrompt(prompt);
    }*/

    private void CheckHazardDistances()
    {
        if (!hazardDetectionEnabled || Time.time < nextHazardCheckTime) return;
        nextHazardCheckTime = Time.time + hazardCheckInterval;

        Transform playerTransform = m_SharedMovementScript.thePlayer.transform;
        Vector3 velocity = m_SharedMovementScript.GetVelocity();

        // CRAMPED SPACE FILTER: If moving slower than 0.3m/s, assume the user is navigating carefully or standing still
        if (velocity.magnitude < 0.3f) return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            playerTransform.position,
            dangerZoneDistance,
            hazardObjectColliders,
            hazardLayerMask
        );

        GameObject bestCandidate = null;
        float highestUrgency = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hazardObjectColliders[i];
            if (hit == null) continue;

            Vector3 closestPointOnHazard = hit.ClosestPoint(playerTransform.position);
            Vector3 directionToHazard = (closestPointOnHazard - playerTransform.position).normalized;

            // DIRECT PATH FILTER: Use Dot Product to see if the hazard is in front of the movement -- 1.0 = directly in path, 0.0 = to the side, -1.0 = behind
            float pathAlignment = Vector3.Dot(velocity.normalized, directionToHazard);

            // Only warn if the object is within a ~60 degree cone in front of movement (> 0.5)
            if (pathAlignment < 0.5f) continue;

            float distance = Vector3.Distance(playerTransform.position, closestPointOnHazard);

            // CALCULATE URGENCY: Alignment / Distance, prioritize things directly in the path, even if something else is slightly closer to the side
            float urgency = pathAlignment / (distance + 0.1f);

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
        string prompt = $"Hazard detected: {hazardName}. " + $"The player is too close to this object. " +
                        $"Warn the player briefly and clearly. " + $"Mention the object by name. Do not wait for the player to speak.";
        // Debug.Log("Hazard Detection Response: " + prompt);

        _ = realtimeClient.SendManualPrompt(prompt);
    }
}
