using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;

public class PlayAudio : MonoBehaviour
{
    private const int DefaultLayer = 0;
    private const int KeyItemsLayer = 13;

    // Components to grab from scripts
    private CustomTeleportationProvider teleport;
    private ActionBasedContinuousMoveProvider move;
    private XRInteractionManager interactionManager;
    private GameObject thePlayer;
    private GameObject theGuide;
    private GameObject playerRig;
    private GameObject guideRig;
    private int role;
    private CharacterController playerCharacterController;
    private CharacterController guideCharacterController;
    private Vector3 playerLastKnownPosition;
    private Vector3 guideLastKnownPosition;
    private string lastAudioSourceLog;
    private string lastSurfaceMaterial;
    private int lastLoggedRole = -1;
    private bool sharedMovementLogged;
    private bool guideFollowLogged;
    private bool missingControllerLogged;
    private bool missingAudioSourceLogged;
    private bool teleportProviderMissingLogged;
    private bool teleportClipMissingLogged;
    private bool snapTurnMissingLogged;
    private bool snapTurnClipMissingLogged;
    private float lastMeaningfulMovementTime = float.NegativeInfinity;
    // CharacterController invokes OnControllerColliderHit every frame while contact persists.
    // Track the last-seen time per object group so obstacle cues only fire on contact enter.
    private readonly Dictionary<int, float> activeObstacleContacts = new Dictionary<int, float>();

    // Variables to hold scripts we need access to
    private SharedMovement m_SharedMovementScript;
    private GuideFollow m_GuideFollowScript;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool guideFollowFound = false;

    // Audio sources for sonification
    public AudioSource playerAudio;

    // Sound effects for player sonification
    private AudioClip teleportEffect;
    private AudioClip walkEffect;
    private AudioClip woodEffect;
    private AudioClip waterEffect;
    private AudioClip generalWalkEffect;

    private AudioClip grassEffect;
    private AudioClip turnEffect;
    private AudioClip woodCollisionEffect;
    private AudioClip collisionEffect;
    private AudioClip noEffect; // For sharing sound properly

    // Sound effects for guide sonification
    private AudioClip robotWalkEffect;
    private AudioClip caneWalkEffect;
    private AudioClip dogWalkEffect;
    private AudioClip birdFlyEffect;

    // For sharing audio over network (not implemented yet)
    public AudioClip currentClip;
    private string surfaceMaterial;

    // for dealing with trailing footstep sounds when stopping and changing direction - if the position changes back to the previous position, we don't want to play a footstep sound
    private Vector3 playerPreviousPosition;
    private Vector3 guidePreviousPosition;

    [SerializeField] private float minCollisionMoveSpeed = 0.5f;
    [SerializeField] private float minImpactAlignment = 0.35f;
    [SerializeField] private float wallScrapeVolume = 0.18f;
    [SerializeField] private float directHitVolume = 0.35f;
    [SerializeField] private float woodWallVolume = 0.24f;
    [SerializeField] private float obstacleContactExitDelay = 0.15f;
    [SerializeField] private float minWalkMoveSpeed = 0.08f;
    [SerializeField] private float walkStopGracePeriod = 0.12f;
    [SerializeField] private float minGroundSurfaceNormalY = 0.55f;

    // Start is called before the first frame update
    void Start()
    {
        // Grab necessary components from scene
        interactionManager = FindObjectOfType<XRInteractionManager>();
        teleport = FindObjectOfType<CustomTeleportationProvider>();
        move = FindObjectOfType<ActionBasedContinuousMoveProvider>();

        playerRig = GameObject.Find("XR Origin (Player Rig)");
        guideRig = GameObject.Find("XR Origin (Guide Rig)");

        if (playerRig != null)
        {
            playerCharacterController = playerRig.GetComponentInChildren<CharacterController>();
        }

        if (guideRig != null)
        {
            guideCharacterController = guideRig.GetComponentInChildren<CharacterController>();
        }

        if (playerCharacterController != null)
        {
            playerLastKnownPosition = playerCharacterController.transform.position;
            playerPreviousPosition = playerLastKnownPosition;
        }

        if (guideCharacterController != null)
        {
            guideLastKnownPosition = guideCharacterController.transform.position;
            guidePreviousPosition = guideLastKnownPosition;
        }

        // Assign sounds from Resources
        teleportEffect = Resources.Load<AudioClip>("Audio/teleport");
        walkEffect = Resources.Load<AudioClip>("Audio/walk");
        woodEffect = Resources.Load<AudioClip>("Audio/wood-walk");
        waterEffect = Resources.Load<AudioClip>("Audio/water-walk");
        grassEffect = Resources.Load<AudioClip>("Audio/grass-walk");
        generalWalkEffect = Resources.Load<AudioClip>("Audio/general-walk");
        turnEffect = Resources.Load<AudioClip>("Audio/turn");
        woodCollisionEffect = Resources.Load<AudioClip>("Audio/wooden-collision");
        collisionEffect = Resources.Load<AudioClip>("Audio/general-collision");
        noEffect = Resources.Load<AudioClip>("Audio/nothing");

        robotWalkEffect = Resources.Load<AudioClip>("Audio/robot-walk");
        caneWalkEffect = Resources.Load<AudioClip>("Audio/white-cane");
        dogWalkEffect = Resources.Load<AudioClip>("Audio/dog-walk");
        birdFlyEffect = Resources.Load<AudioClip>("Audio/bird-flap");
    }

    // Update is called once per frame
    void Update()
    {
        ClearExpiredObstacleContacts();

        // Grab components we need access to
        if (!sharedMovementFound)
        {
            getSharedMovement();
            if (!sharedMovementFound && !sharedMovementLogged)
            {
                //Debug.Log("[PlayAudio] SharedMovement not ready; waiting for thePlayer/theGuide.");
                sharedMovementLogged = true;
            }
        }
        if (!guideFollowFound)
        {
            getGuideFollow();
            if (!guideFollowFound && !guideFollowLogged)
            {
                //Debug.Log("[PlayAudio] GuideFollow not ready; waiting for guide rig.");
                guideFollowLogged = true;
            }
        }

        // If we have shared movement components assigned (a guide and player) or the confederates are in the scene
        bool hasConfederate = GameObject.FindWithTag("Confederate");
        if (sharedMovementFound || hasConfederate || playerAudio != null)
        {
            // If we're calling Audio from a PlayAudio component on the guide's rig, use the guide's audio source
            if (GetComponent<GuideFollow>())
                playerAudio = theGuide.transform.parent.GetComponentInParent<AudioSource>(); // Ensure we grab the audio source for Play Audio, not Open AI

            // If we're calling Audio from a PlayAudio component on a confederate, use the confederate's audio source
            if (GameObject.FindWithTag("Confederate"))
            {
                playerAudio = GameObject.FindWithTag("Confederate").GetComponentInChildren<AudioSource>();
                //Debug.Log("[PlayAudio] Using Confederate-tagged audio source (first found).");
            }

            if (playerAudio != null)
            {
                string srcLabel = $"{playerAudio.name} (tag {playerAudio.transform.tag})";
                if (lastAudioSourceLog != srcLabel)
                {
                    //Debug.Log($"[PlayAudio] Using audio source: {srcLabel}");
                    lastAudioSourceLog = srcLabel;
                }
            }

            if (playerAudio.isPlaying)
                currentClip = playerAudio.clip;
            else
                currentClip = noEffect;

            // Ensure we have a character controller reference before checking movement
            if (playerCharacterController == null && playerRig != null)
            {
                playerCharacterController = playerRig.GetComponentInChildren<CharacterController>();
                if (playerCharacterController == null && !missingControllerLogged)
                {
                    //Debug.Log("[PlayAudio] No CharacterController found on parent. Walking sounds will not trigger.");
                    missingControllerLogged = true;
                }
            }

            if (guideCharacterController == null && guideRig != null)
            {
                guideCharacterController = guideRig.GetComponentInChildren<CharacterController>();
            }

            if (playerAudio != null && playerCharacterController != null && guideCharacterController != null)
            {
                CheckTeleport();
                CheckTurning();

                Vector3 playerCurrPosition = playerCharacterController.transform.position;
                Vector3 guideCurrPosition = guideCharacterController.transform.position;

                // compute movement using previous frame, not stale data
                playerPreviousPosition = playerLastKnownPosition;
                playerLastKnownPosition = playerCurrPosition;

                guidePreviousPosition = guideLastKnownPosition;
                guideLastKnownPosition = guideCurrPosition;

                // Debug.Log("Moving with guide?: " + m_SharedMovementScript.movingWithGuide);

                if (!m_SharedMovementScript.movingWithGuide)
                {
                    //Debug.Log("Player is moving");
                    playAudioForMovingPlayer(playerCurrPosition, playerPreviousPosition);
                }
                else
                {
                    //Debug.Log("Guide is moving");
                    GetSurfaceUnderPlayerController(playerCharacterController);
                    playAudioForMovingPlayer(playerCurrPosition, playerPreviousPosition);
                }
                playAudioForMovingGuide(guideCurrPosition, guidePreviousPosition);
            }
            else if (playerAudio == null && !missingAudioSourceLogged)
            {
                //Debug.Log("[PlayAudio] No AudioSource assigned; skipping audio playback.");
                missingAudioSourceLogged = true;
            }
        }
        else
        {
            //Debug.Log("[PlayAudio] Update skipped: neither shared movement found nor Confederate tag present.");
        }
    }

    public void CheckTeleport()
    {
        if (teleport == null)
        {
            if (!teleportProviderMissingLogged)
            {
                //Debug.Log("[PlayAudio] TeleportationProvider missing; teleport sound will not play.");
                teleportProviderMissingLogged = true;
            }
            return;
        }
        if (teleportEffect == null && !teleportClipMissingLogged)
        {
            //Debug.Log("[PlayAudio] Teleport clip not loaded; please check Resources/Audio/teleport.");
            teleportClipMissingLogged = true;
        }

        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done && teleportEffect != null)
        {
            playerAudio.clip = teleportEffect;
            playerAudio.Play();
            LogClip("Teleport completed", teleportEffect);
        }
    }

    public void CheckTurning()
    {
        //DeviceBasedSnapTurnProvider snapTurn = FindObjectOfType<DeviceBasedSnapTurnProvider>();
        ActionBasedSnapTurnProvider snapTurn = FindObjectOfType<ActionBasedSnapTurnProvider>();

        if (snapTurn == null)
        {
            if (!snapTurnMissingLogged)
            {
                //Debug.Log("[PlayAudio] SnapTurn provider missing; turn sound will not play.");
                snapTurnMissingLogged = true;
            }
            return;
        }
        if (turnEffect == null && !snapTurnClipMissingLogged)
        {
            //Debug.Log("[PlayAudio] Turn clip not loaded; please check Resources/Audio/turn.");
            snapTurnClipMissingLogged = true;
        }

        if (snapTurn.locomotionPhase == LocomotionPhase.Moving && turnEffect != null) // If the movement of snap turning is active
        {
            playerAudio.clip = turnEffect;
            playerAudio.Play();
            LogClip("Snap turn", turnEffect);
        }
    }

    private void playAudioForMovingPlayer(Vector3 currPosition, Vector3 lastPosition)
    {
        bool isMoving = IsMeaningfullyMoving(currPosition, lastPosition);
        string clipName = playerAudio && playerAudio.clip ? playerAudio.clip.name : "none";
        //Debug.Log($"[PlayAudio] Player path check: moving={isMoving}, surface={surfaceMaterial}, currentClip={clipName}, sourceTag={playerAudio.transform.tag}");

        // HARD STOP: immediately cut walking audio when movement stops
        if (!isMoving)
        {
            if (playerAudio.isPlaying && IsWalkClip(playerAudio.clip))
            {
                playerAudio.Stop();
                playerAudio.clip = noEffect;
                LogClip("Player stopped moving; force stop walk audio", playerAudio.clip);
            }
            return;
        }

        // If our audio is not coming from a guide, use the player audio clips
        if (playerAudio.transform.tag != "Guide")
        {
            // If our last clip playing was any of the walking effects, we don't wait for them to be done playing before switching
            if (playerAudio.clip == walkEffect || playerAudio.clip == woodEffect || playerAudio.clip == grassEffect || playerAudio.clip == waterEffect || playerAudio.clip == generalWalkEffect)
            {
                if (isMoving)
                {
                    if (surfaceMaterial == "wood")
                    {
                        playerAudio.clip = woodEffect;
                    }
                    else if (surfaceMaterial == "water")
                    {
                        playerAudio.clip = waterEffect;
                    }
                    else if (surfaceMaterial == "grass")
                    {
                        playerAudio.clip = grassEffect;
                    }
                    else if (surfaceMaterial == "floor")
                    {
                        playerAudio.clip = generalWalkEffect;
                    }
                    else
                    {
                        playerAudio.clip = walkEffect;
                    }

                    if (!playerAudio.isPlaying)
                    {
                        playerAudio.Play(); // maybe have to mute here for that local audio thing
                        LogClip("Player walking", playerAudio.clip);
                    }
                }
                else
                {
                    //Debug.Log("[PlayAudio] Player not moving; walk clip unchanged.");
                }
            }
            else // We wait for the audio clip to finish before assigning a walk clip
            {
                if (!playerAudio.isPlaying)
                {
                    if (isMoving)
                    {
                        if (surfaceMaterial == "wood")
                        {
                            playerAudio.clip = woodEffect;
                        }
                        else if (surfaceMaterial == "water")
                        {
                            playerAudio.clip = waterEffect;
                        }
                        else if (surfaceMaterial == "grass")
                        {
                            playerAudio.clip = grassEffect;
                        }
                        else if (surfaceMaterial == "floor")
                        {
                            playerAudio.clip = generalWalkEffect;
                        }
                        else
                        {
                            playerAudio.clip = walkEffect;
                        }

                        if (!playerAudio.isPlaying)
                        {
                            playerAudio.Play();
                            LogClip("Player walking (waited for clip end)", playerAudio.clip);
                        }
                    }
                    else
                    {
                        //Debug.Log("[PlayAudio] Player not moving; no footstep played.");
                    }
                } // End if (!playerAudio.isPlaying)
            }
        }
    }

    private void playAudioForMovingGuide(Vector3 currPosition, Vector3 lastPosition)
    {
        // If our audio is coming from the guide, use the guide audio clips
        if (guideFollowFound && playerAudio.transform.tag == "Guide")
        {
            bool isMoving = IsMeaningfullyMoving(currPosition, lastPosition);
            string clipName = playerAudio && playerAudio.clip ? playerAudio.clip.name : "none";
            if (FindObjectOfType<AIGuide>())
                role = FindObjectOfType<AIGuide>().role;
            if (role != lastLoggedRole)
            {
                //Debug.Log($"[PlayAudio] Guide role detected: {role}");
                lastLoggedRole = role;
            }
            //Debug.Log($"[PlayAudio] Guide path check: moving={isMoving}, surface={surfaceMaterial}, currentClip={clipName}, role={role}");
            // HARD STOP: immediately cut walking audio when movement stops
            if (!isMoving)
            {
                if (playerAudio.isPlaying && IsWalkClip(playerAudio.clip))
                {
                    playerAudio.Stop();
                    playerAudio.clip = noEffect;
                    LogClip("Guide stopped moving; force stop walk audio", playerAudio.clip);
                }
                return;
            }

            // If our last clip playing was any of the walking effects, we don't wait for them to be done playing before switching
            if (playerAudio.clip == walkEffect || playerAudio.clip == woodEffect || playerAudio.clip == grassEffect || playerAudio.clip == waterEffect || playerAudio.clip == generalWalkEffect ||
                playerAudio.clip == robotWalkEffect || playerAudio.clip == caneWalkEffect || playerAudio.clip == dogWalkEffect || playerAudio.clip == birdFlyEffect)
            {
                if (isMoving)
                {
                    if (surfaceMaterial == "wood")
                    {
                        // Decide walking clip based on guide role
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = woodEffect;
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                break;
                            case 4: // dog
                                playerAudio.clip = dogWalkEffect;
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                break;
                        }
                    }
                    else if (surfaceMaterial == "water")
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = waterEffect;
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                break;
                            case 3: // cane
                                playerAudio.clip = noEffect;
                                break;
                            case 4: // dog
                                playerAudio.clip = waterEffect;
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                break;
                        }
                    }
                    else if (surfaceMaterial == "grass")
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = grassEffect;
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                break;
                            case 4: // dog
                                playerAudio.clip = grassEffect;
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                break;
                        }
                    }
                    else if (surfaceMaterial == "floor")
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = generalWalkEffect;
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                break;
                            case 4: // dog
                                playerAudio.clip = dogWalkEffect;
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                break;
                        }
                    }
                    else
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = walkEffect;
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                break;
                            case 4: // dog
                                playerAudio.clip = dogWalkEffect;
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                break;
                        }
                    }

                    if (!playerAudio.isPlaying)
                    {
                        playerAudio.Play();
                        LogClip("Guide walking", playerAudio.clip);
                    }
                }
                else // If position hasn't changed
                {
                    //Debug.Log("[PlayAudio] Guide not moving; no footstep played.");
                    // Used to play idle effect, but now we don't want that interfering with hearing the guide talk
                    /*playerAudio.clip = idleEffect;
                    if (!playerAudio.isPlaying)
                        playerAudio.Play();*/
                }
            }
            else // We wait for the audio clip to finish before assigning a walk clip
            {
                if (!playerAudio.isPlaying)
                {
                    if (isMoving)
                    {
                        if (surfaceMaterial == "wood")
                        {
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = woodEffect;
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    break;
                                case 4: // dog
                                    playerAudio.clip = dogWalkEffect;
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    break;
                            }
                        }
                        else if (surfaceMaterial == "water")
                        {
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = waterEffect;
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    break;
                                case 3: // cane
                                    playerAudio.clip = noEffect;
                                    break;
                                case 4: // dog
                                    playerAudio.clip = waterEffect;
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    break;
                            }
                        }
                        else if (surfaceMaterial == "grass")
                        {
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = grassEffect;
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    break;
                                case 4: // dog
                                    playerAudio.clip = grassEffect;
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    break;
                            }
                        }
                        else if (surfaceMaterial == "floor")
                        {
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = generalWalkEffect;
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    break;
                                case 4: // dog
                                    playerAudio.clip = dogWalkEffect;
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    break;
                            }
                        }
                        else
                        {
                            // Decide walking clip based on guide role
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = walkEffect;
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    break;
                                case 4: // dog
                                    playerAudio.clip = dogWalkEffect;
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    break;
                            }
                        }

                        if (!playerAudio.isPlaying)
                        {
                            playerAudio.Play();
                            LogClip("Guide walking (waited for clip end)", playerAudio.clip);
                        }
                    }
                    else // If position hasn't changed
                    {
                        //Debug.Log("[PlayAudio] Guide not moving; no footstep played.");
                        // Used to play idle effect, but now we don't want that interfering with hearing the guide talk
                        /*playerAudio.clip = idleEffect;
                        if (!playerAudio.isPlaying)
                            playerAudio.Play();*/
                    }
                } // End if (!playerAudio.isPlaying)
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (playerAudio == null)
            return;

        if (m_SharedMovementScript.movingWithGuide)
        {
            return;
        }

        //Debug.Log("Collided with " + hit.transform.tag + " object.");

        // Temporarily ignore wall/object side-contact entirely so it cannot retrigger walk audio.
        // Only upward-facing contacts should be allowed to classify the current footstep surface.
        if (!IsGroundSurfaceCollision(hit))
            return;

        string detectedSurfaceMaterial = GetSurfaceMaterialFromTag(hit.transform.tag);
        if (detectedSurfaceMaterial == null)
            return;

        surfaceMaterial = detectedSurfaceMaterial;
        if (surfaceMaterial != lastSurfaceMaterial)
        {
            // Debug.Log($"[PlayAudio] Surface set to {surfaceMaterial} via collision with {hit.transform.name} (tag {hit.transform.tag}, layer {hit.gameObject.layer})");
            lastSurfaceMaterial = surfaceMaterial;
        }

        // Temporarily disable all hit/object collision audio.
        // Keep the old obstacle cue path commented out for later restoration if needed.
        /*
        if (hit.gameObject.layer == 8)
        {
            int collisionGroupId = GetCollisionGroupId(hit);
            if (IsObstacleContactActive(collisionGroupId))
            {
                activeObstacleContacts[collisionGroupId] = Time.time;
                return;
            }

            AudioClip collisionClip;
            if (hit.transform.tag == "Wood")
            {
                collisionClip = woodCollisionEffect;
            }
            else if (hit.transform.tag == "Player")
            {
                collisionClip = noEffect;
            }
            else
            {
                collisionClip = noEffect;
            }

            if (ShouldPlayCollisionSound(hit, collisionClip, out float collisionVolume))
            {
                activeObstacleContacts[collisionGroupId] = Time.time;
                playerAudio.PlayOneShot(collisionClip, collisionVolume);
                LogClip($"Collision with {hit.transform.name} at volume {collisionVolume:F2}", collisionClip);
            }
        }
        */
    }

    private void GetSurfaceUnderPlayerController(CharacterController controller) // called on player controller when moving with guide
    {
        int floorMask = 1 << 10; // floors layer
        RaycastHit hit;
        if (Physics.Raycast(controller.bounds.center, Vector3.down, out hit, 5.0f, floorMask, QueryTriggerInteraction.Ignore))
        {
            // Collect surface materials for all objects the raycast collides with to share over network
            if (hit.collider.tag == "Wood")
                surfaceMaterial = "wood";
            else if (hit.collider.tag == "Water")
                surfaceMaterial = "water";
            else if (hit.collider.tag == "Grass")
                surfaceMaterial = "grass";
            else if (hit.collider.tag == "floor")
                surfaceMaterial = "floor";
            else
                surfaceMaterial = "other";

            if (surfaceMaterial != lastSurfaceMaterial)
            {
                Debug.Log($"[PlayAudio] Surface set by {this.gameObject.name} to {surfaceMaterial} via collision with {hit.collider.gameObject.name} (tag {hit.collider.tag}, layer {hit.collider.gameObject.layer})");
                lastSurfaceMaterial = surfaceMaterial;
            }
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
        else
        {
            theGuide = m_SharedMovementScript.theGuide;
            thePlayer = m_SharedMovementScript.thePlayer;
            if (theGuide != null && thePlayer != null)
            {
                // Assign playerAudio component after we have access to thePlayer
                if (playerAudio == null)
                    playerAudio = thePlayer.GetComponentInParent<AudioSource>();

                sharedMovementFound = true;
            }
            else if (thePlayer != null) 
            {
                // Assign playerAudio component after we have access to thePlayer
                if (playerAudio == null)
                    playerAudio = thePlayer.GetComponentInParent<AudioSource>();

                sharedMovementFound = true;
            }
        }
    }

    private void getGuideFollow()
    {
        // If there is a GuideFollow component in the scene (we are in the scene with the Guide's rig), look to assign guide follow
        // This will not work for a confederate scene
        if (FindObjectOfType<GuideFollow>())
        {
            if (m_GuideFollowScript == null)
                m_GuideFollowScript = FindObjectOfType<GuideFollow>();
            else
            {
                guideFollowFound = true;
                //Debug.Log("[PlayAudio] GuideFollow located; guide audio path enabled.");
            }
        }
    }
    private bool IsWalkClip(AudioClip clip)
    {
        return clip == walkEffect ||
               clip == woodEffect ||
               clip == waterEffect ||
               clip == grassEffect ||
               clip == generalWalkEffect ||
               clip == robotWalkEffect ||
               clip == caneWalkEffect ||
               clip == dogWalkEffect ||
               clip == birdFlyEffect;
    }

    private bool ShouldPlayCollisionSound(ControllerColliderHit hit, AudioClip collisionClip, out float collisionVolume)
    {
        collisionVolume = 0f;

        if (collisionClip == null || collisionClip == noEffect)
            return false;

        Vector3 movement = playerLastKnownPosition - playerPreviousPosition;
        float moveSpeed = movement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        if (moveSpeed < minCollisionMoveSpeed)
            return false;

        Vector3 moveDirection = hit.moveDirection.sqrMagnitude > 0.0001f
            ? hit.moveDirection.normalized
            : movement.normalized;
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return false;

        float impactAlignment = Vector3.Dot(moveDirection, -hit.normal);
        if (impactAlignment < minImpactAlignment)
            return false;

        bool isWallLikeContact = Mathf.Abs(hit.normal.y) < 0.35f;
        collisionVolume = GetCollisionVolume(hit, collisionClip, impactAlignment, isWallLikeContact);
        return true;
    }

    private bool IsGroundSurfaceCollision(ControllerColliderHit hit)
    {
        return hit.normal.y >= minGroundSurfaceNormalY;
    }

    private string GetSurfaceMaterialFromTag(string tagName)
    {
        if (tagName == "Wood")
            return "wood";
        if (tagName == "Water")
            return "water";
        if (tagName == "Grass")
            return "grass";
        if (tagName == "floor")
            return "floor";

        return null;
    }

    private bool IsMeaningfullyMoving(Vector3 currPosition, Vector3 lastPosition)
    {
        Vector3 movement = currPosition - lastPosition;
        movement.y = 0f;

        float moveSpeed = movement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        if (moveSpeed >= minWalkMoveSpeed)
        {
            lastMeaningfulMovementTime = Time.time;
            return true;
        }

        return Time.time - lastMeaningfulMovementTime < walkStopGracePeriod;
    }

    private void ClearExpiredObstacleContacts()
    {
        if (activeObstacleContacts.Count == 0)
            return;

        float now = Time.time;
        List<int> expiredContactIds = null;

        foreach (KeyValuePair<int, float> contact in activeObstacleContacts)
        {
            if (now - contact.Value >= obstacleContactExitDelay)
            {
                if (expiredContactIds == null)
                    expiredContactIds = new List<int>();

                expiredContactIds.Add(contact.Key);
            }
        }

        if (expiredContactIds == null)
            return;

        foreach (int contactId in expiredContactIds)
            activeObstacleContacts.Remove(contactId);
    }

    private float GetCollisionVolume(ControllerColliderHit hit, AudioClip collisionClip, float impactAlignment, bool isWallLikeContact)
    {
        float baseVolume = directHitVolume;
        if (isWallLikeContact)
            baseVolume = collisionClip == woodCollisionEffect ? woodWallVolume : wallScrapeVolume;
        else if (collisionClip == woodCollisionEffect)
            baseVolume = Mathf.Max(baseVolume, woodWallVolume);

        float alignmentFactor = Mathf.InverseLerp(minImpactAlignment, 1f, impactAlignment);
        return Mathf.Clamp(baseVolume * Mathf.Lerp(0.75f, 1f, alignmentFactor), 0f, 1f);
    }

    private void LogClip(string reason, AudioClip clip)
    {
        string clipName = clip ? clip.name : "null";
        string srcName = playerAudio ? playerAudio.name : "no-source";
        string srcTag = playerAudio && playerAudio.transform ? playerAudio.transform.tag : "no-tag";
        //Debug.Log($"[PlayAudio] {reason}: clip={clipName}, source={srcName}, tag={srcTag}");
    }
}
