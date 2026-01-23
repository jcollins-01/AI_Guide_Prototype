using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;

public class PlayAudio : MonoBehaviour
{
    // Components to grab from scripts
    private TeleportationProvider teleport;
    private ActionBasedContinuousMoveProvider move;
    private XRInteractionManager interactionManager;
    private GameObject thePlayer;
    private GameObject theGuide;
    private int role;
    private CharacterController characterController;
    private Vector3 lastKnownPosition;
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

    // Variables to hold scripts we need access to
    private SharedMovement m_SharedMovementScript;
    private GuideFollow m_GuideFollowScript;
    private AudioClipSync m_audioClipSync;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool guideFollowFound = false;
    private bool audioClipSyncAssigned = false;

    // Audio sources for sonification
    public AudioSource playerAudio;

    // Sound effects for player sonification
    private AudioClip teleportEffect;
    private AudioClip walkEffect;
    private AudioClip woodEffect;
    private AudioClip waterEffect;
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

    // Start is called before the first frame update
    void Start()
    {
        // Grab necessary components from scene
        interactionManager = FindObjectOfType<XRInteractionManager>();
        teleport = FindObjectOfType<TeleportationProvider>();
        move = FindObjectOfType<ActionBasedContinuousMoveProvider>();
        characterController = GetComponentInParent<CharacterController>();
        if (characterController != null)
            lastKnownPosition = characterController.transform.position;

        // Assign sounds from Resources
        teleportEffect = Resources.Load<AudioClip>("Audio/teleport");
        walkEffect = Resources.Load<AudioClip>("Audio/walk");
        woodEffect = Resources.Load<AudioClip>("Audio/wood-walk");
        waterEffect = Resources.Load<AudioClip>("Audio/water-walk");
        grassEffect = Resources.Load<AudioClip>("Audio/grass-walk");
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
        // Grab components we need access to
        if (!sharedMovementFound)
        {
            getSharedMovement();
            if (!sharedMovementFound && !sharedMovementLogged)
            {
                Debug.Log("[PlayAudio] SharedMovement not ready; waiting for thePlayer/theGuide.");
                sharedMovementLogged = true;
            }
        }
        if (!guideFollowFound)
        {
            getGuideFollow();
            if (!guideFollowFound && !guideFollowLogged)
            {
                Debug.Log("[PlayAudio] GuideFollow not ready; waiting for guide rig.");
                guideFollowLogged = true;
            }
        }
        if (!audioClipSyncAssigned)
            assignAudioClipSync();

        // If we have shared movement components assigned (a guide and player) or the confederates are in the scene
        if (sharedMovementFound || GameObject.FindWithTag("Confederate"))
        {
            // If we're calling Audio from a PlayAudio component on the guide's rig, use the guide's audio source
            if (GetComponent<GuideFollow>())
                playerAudio = theGuide.transform.parent.GetComponentInParent<AudioSource>(); // Ensure we grab the audio source for Play Audio, not Open AI

            // If we're calling Audio from a PlayAudio component on a confederate, use the confederate's audio source
            if (GameObject.FindWithTag("Confederate"))
            {
                playerAudio = GameObject.FindWithTag("Confederate").GetComponentInChildren<AudioSource>();
                Debug.Log("[PlayAudio] Using Confederate-tagged audio source (first found).");
            }

            if (playerAudio != null)
            {
                string srcLabel = $"{playerAudio.name} (tag {playerAudio.transform.tag})";
                if (lastAudioSourceLog != srcLabel)
                {
                    Debug.Log($"[PlayAudio] Using audio source: {srcLabel}");
                    lastAudioSourceLog = srcLabel;
                }
            }

            if (playerAudio.isPlaying)
                currentClip = playerAudio.clip;
            else
                currentClip = noEffect;

            // Ensure we have a character controller reference before checking movement
            if (characterController == null)
            {
                characterController = GetComponentInParent<CharacterController>();
                if (characterController == null && !missingControllerLogged)
                {
                    Debug.Log("[PlayAudio] No CharacterController found on parent. Walking sounds will not trigger.");
                    missingControllerLogged = true;
                }
            }

            if (playerAudio != null && characterController != null)
            {
                CheckTeleport();
                CheckTurning();

                Vector3 currPosition = characterController.transform.position;
                playAudioForMovingPlayer(currPosition, lastKnownPosition);
                playAudioForMovingGuide(currPosition, lastKnownPosition);
                lastKnownPosition = currPosition;
            }
            else if (playerAudio == null && !missingAudioSourceLogged)
            {
                Debug.Log("[PlayAudio] No AudioSource assigned; skipping audio playback.");
                missingAudioSourceLogged = true;
            }
        }
        else
        {
            Debug.Log("[PlayAudio] Update skipped: neither shared movement found nor Confederate tag present.");
        }
    }

    public void CheckTeleport()
    {
        if (teleport == null)
        {
            if (!teleportProviderMissingLogged)
            {
                Debug.Log("[PlayAudio] TeleportationProvider missing; teleport sound will not play.");
                teleportProviderMissingLogged = true;
            }
            return;
        }
        if (teleportEffect == null && !teleportClipMissingLogged)
        {
            Debug.Log("[PlayAudio] Teleport clip not loaded; please check Resources/Audio/teleport.");
            teleportClipMissingLogged = true;
        }

        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done && teleportEffect != null)
        {
            playerAudio.clip = teleportEffect;
            playerAudio.Play();
            LogClip("Teleport completed", teleportEffect);
            //m_audioClipSync.SetClipName(teleportEffect.name);
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
                Debug.Log("[PlayAudio] SnapTurn provider missing; turn sound will not play.");
                snapTurnMissingLogged = true;
            }
            return;
        }
        if (turnEffect == null && !snapTurnClipMissingLogged)
        {
            Debug.Log("[PlayAudio] Turn clip not loaded; please check Resources/Audio/turn.");
            snapTurnClipMissingLogged = true;
        }

        if (snapTurn.locomotionPhase == LocomotionPhase.Moving && turnEffect != null) // If the movement of snap turning is active
        {
            playerAudio.clip = turnEffect;
            playerAudio.Play();
            LogClip("Snap turn", turnEffect);
            //m_audioClipSync.SetClipName(turnEffect.name);
        }
    }

    private void playAudioForMovingPlayer(Vector3 currPosition, Vector3 lastPosition)
    {
        bool isMoving = currPosition != lastPosition;
        string clipName = playerAudio && playerAudio.clip ? playerAudio.clip.name : "none";
        Debug.Log($"[PlayAudio] Player path check: moving={isMoving}, surface={surfaceMaterial}, currentClip={clipName}, sourceTag={playerAudio.transform.tag}");
        // If our audio is not coming from a guide, use the player audio clips
        if (playerAudio.transform.tag != "Guide")
        {
            // If our last clip playing was any of the walking effects, we don't wait for them to be done playing before switching
            if (playerAudio.clip == walkEffect || playerAudio.clip == woodEffect || playerAudio.clip == grassEffect || playerAudio.clip == waterEffect)
            {
                if (isMoving)
                {
                    if (surfaceMaterial == "wood")
                    {
                        playerAudio.clip = woodEffect;
                        //m_audioClipSync.SetClipName(woodEffect.name);
                    }
                    else if (surfaceMaterial == "water")
                    {
                        playerAudio.clip = waterEffect;
                        //m_audioClipSync.SetClipName(waterEffect.name);
                    }  
                    else if (surfaceMaterial == "grass")
                    {
                        playerAudio.clip = grassEffect;
                        //m_audioClipSync.SetClipName(grassEffect.name);
                    }
                    else
                    {
                        playerAudio.clip = walkEffect;
                        //m_audioClipSync.SetClipName(walkEffect.name);
                    }

                    if (!playerAudio.isPlaying)
                    {
                        playerAudio.Play(); // maybe have to mute here for that local audio thing
                        LogClip("Player walking", playerAudio.clip);
                    }
                }
                else
                {
                    Debug.Log("[PlayAudio] Player not moving; walk clip unchanged.");
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
                            //m_audioClipSync.SetClipName(woodEffect.name);
                        }  
                        else if (surfaceMaterial == "water")
                        {
                            playerAudio.clip = waterEffect;
                            //m_audioClipSync.SetClipName(waterEffect.name);
                        }
                        else if (surfaceMaterial == "grass")
                        {
                            playerAudio.clip = grassEffect;
                            //m_audioClipSync.SetClipName(grassEffect.name);
                        }
                        else
                        {
                            playerAudio.clip = walkEffect;
                            //m_audioClipSync.SetClipName(walkEffect.name);
                        }

                        if (!playerAudio.isPlaying)
                        {
                            playerAudio.Play();
                            LogClip("Player walking (waited for clip end)", playerAudio.clip);
                        }
                    }
                    else
                    {
                        Debug.Log("[PlayAudio] Player not moving; no footstep played.");
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
            bool isMoving = currPosition != lastPosition;
            string clipName = playerAudio && playerAudio.clip ? playerAudio.clip.name : "none";
            if (FindObjectOfType<AIGuide>())
                role = FindObjectOfType<AIGuide>().role;
            if (role != lastLoggedRole)
            {
                Debug.Log($"[PlayAudio] Guide role detected: {role}");
                lastLoggedRole = role;
            }
            Debug.Log($"[PlayAudio] Guide path check: moving={isMoving}, surface={surfaceMaterial}, currentClip={clipName}, role={role}");

            // If our last clip playing was any of the walking effects, we don't wait for them to be done playing before switching
            if (playerAudio.clip == walkEffect || playerAudio.clip == woodEffect || playerAudio.clip == grassEffect || playerAudio.clip == waterEffect ||
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
                                //m_audioClipSync.SetClipName(woodEffect.name);
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                //m_audioClipSync.SetClipName(caneWalkEffect.name);
                                break;
                            case 4: // dog
                                playerAudio.clip = dogWalkEffect;
                                //m_audioClipSync.SetClipName(dogWalkEffect.name);
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                //m_audioClipSync.SetClipName(noEffect.name);
                                break;
                        }
                    }
                    else if (surfaceMaterial == "water")
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = waterEffect;
                                //m_audioClipSync.SetClipName(waterEffect.name);
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                break;
                            case 3: // cane
                                playerAudio.clip = noEffect;
                                //m_audioClipSync.SetClipName(noEffect.name);
                                break;
                            case 4: // dog
                                playerAudio.clip = waterEffect;
                                //m_audioClipSync.SetClipName(waterEffect.name);
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                //m_audioClipSync.SetClipName(noEffect.name);
                                break;
                        }
                    }
                    else if (surfaceMaterial == "grass")
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = grassEffect;
                                //m_audioClipSync.SetClipName(grassEffect.name);
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                //m_audioClipSync.SetClipName(caneWalkEffect.name);
                                break;
                            case 4: // dog
                                playerAudio.clip = grassEffect;
                                //m_audioClipSync.SetClipName(grassEffect.name);
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                //m_audioClipSync.SetClipName(noEffect.name);
                                break;
                        }
                    }
                    else
                    {
                        switch (role)
                        {
                            case 1: // human
                                playerAudio.clip = walkEffect;
                                //m_audioClipSync.SetClipName(walkEffect.name);
                                break;
                            case 2: // robot
                                playerAudio.clip = robotWalkEffect;
                                //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                break;
                            case 3: // cane
                                playerAudio.clip = caneWalkEffect;
                                //m_audioClipSync.SetClipName(caneWalkEffect.name);
                                break;
                            case 4: // dog
                                playerAudio.clip = dogWalkEffect;
                                //m_audioClipSync.SetClipName(dogWalkEffect.name);
                                break;
                            case 5: // bird
                                playerAudio.clip = birdFlyEffect;
                                //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                break;
                            case 6: // invisible
                                playerAudio.clip = noEffect;
                                //m_audioClipSync.SetClipName(noEffect.name);
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
                    Debug.Log("[PlayAudio] Guide not moving; no footstep played.");
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
                                    //m_audioClipSync.SetClipName(woodEffect.name);
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    //m_audioClipSync.SetClipName(caneWalkEffect.name);
                                    break;
                                case 4: // dog
                                    playerAudio.clip = dogWalkEffect;
                                    //m_audioClipSync.SetClipName(dogWalkEffect.name);
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    //m_audioClipSync.SetClipName(noEffect.name);
                                    break;
                            }
                        }
                        else if (surfaceMaterial == "water")
                        {
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = waterEffect;
                                    //m_audioClipSync.SetClipName(waterEffect.name);
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                    break;
                                case 3: // cane
                                    playerAudio.clip = noEffect;
                                    //m_audioClipSync.SetClipName(noEffect.name);
                                    break;
                                case 4: // dog
                                    playerAudio.clip = waterEffect;
                                    //m_audioClipSync.SetClipName(waterEffect.name);
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    //m_audioClipSync.SetClipName(noEffect.name);
                                    break;
                            }
                        }
                        else if (surfaceMaterial == "grass")
                        {
                            switch (role)
                            {
                                case 1: // human
                                    playerAudio.clip = grassEffect;
                                    //m_audioClipSync.SetClipName(grassEffect.name);
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    //m_audioClipSync.SetClipName(caneWalkEffect.name);
                                    break;
                                case 4: // dog
                                    playerAudio.clip = grassEffect;
                                    //m_audioClipSync.SetClipName(grassEffect.name);
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    //m_audioClipSync.SetClipName(noEffect.name);
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
                                    //m_audioClipSync.SetClipName(walkEffect.name);
                                    break;
                                case 2: // robot
                                    playerAudio.clip = robotWalkEffect;
                                    //m_audioClipSync.SetClipName(robotWalkEffect.name);
                                    break;
                                case 3: // cane
                                    playerAudio.clip = caneWalkEffect;
                                    //m_audioClipSync.SetClipName(caneWalkEffect.name);
                                    break;
                                case 4: // dog
                                    playerAudio.clip = dogWalkEffect;
                                    //m_audioClipSync.SetClipName(dogWalkEffect.name);
                                    break;
                                case 5: // bird
                                    playerAudio.clip = birdFlyEffect;
                                    //m_audioClipSync.SetClipName(birdFlyEffect.name);
                                    break;
                                case 6: // invisible
                                    playerAudio.clip = noEffect;
                                    //m_audioClipSync.SetClipName(noEffect.name);
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
                        Debug.Log("[PlayAudio] Guide not moving; no footstep played.");
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
        //Debug.Log("Collided with " + hit.transform.tag + " object.");

        // Collect surface materials for all objects we collide with to share over network
        if (hit.transform.tag == "Wood")
            surfaceMaterial = "wood";
        else if (hit.transform.tag == "Water")
            surfaceMaterial = "water";
        else if (hit.transform.tag == "Grass")
            surfaceMaterial = "grass";
        else
            surfaceMaterial = "other";
        if (surfaceMaterial != lastSurfaceMaterial)
        {
            Debug.Log($"[PlayAudio] Surface set to {surfaceMaterial} via collision with {hit.transform.name} (tag {hit.transform.tag}, layer {hit.gameObject.layer})");
            lastSurfaceMaterial = surfaceMaterial;
        }

        // If we hit Obstacles (layer 8), play a collision sound
        if (hit.gameObject.layer == 8)
        {
            if (hit.transform.tag == "Wood")
            {
                playerAudio.clip = woodCollisionEffect;
                //m_audioClipSync.SetClipName(woodCollisionEffect.name);
            }
            else if (hit.transform.tag == "Player") // When collisions between Player and Rig are on at the scene open, ensure no collision sound occurs
            {
                playerAudio.clip = noEffect;
                //m_audioClipSync.SetClipName(noEffect.name);
            }
            else
            {
                playerAudio.clip = collisionEffect;
                //m_audioClipSync.SetClipName(collisionEffect.name);
            } 

            if (!playerAudio.isPlaying)
            {
                playerAudio.Play();
                LogClip($"Collision with {hit.transform.name}", playerAudio.clip);
            }
        }
        else
        {
            Debug.Log($"[PlayAudio] Collision with {hit.transform.name} on layer {hit.gameObject.layer}; no collision SFX because layer != 8.");
        }
    }

    // NOT IN USE
    private void assignAudioClipSync()
    {
        if (FindObjectOfType<AudioClipSync>())
        {
            // Find each appropriate clip sync based on our role
            if (gameObject.tag == "Guide Rig")
            {
                m_audioClipSync = GameObject.FindWithTag("Guide").gameObject.GetComponent<AudioClipSync>();
                audioClipSyncAssigned = true;
            }

            if (gameObject.tag == "Player Rig")
            {
                m_audioClipSync = GameObject.FindWithTag("Player").gameObject.GetComponent<AudioClipSync>();
                audioClipSyncAssigned = true;
            }

            if (gameObject.tag == "Confederate Rig")
            {
                m_audioClipSync = GameObject.FindWithTag("Confederate").gameObject.GetComponent<AudioClipSync>();
                audioClipSyncAssigned = true;
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
            if (theGuide != null && thePlayer != null && !FindObjectOfType<VRScreenreader>())
            {
                // Assign playerAudio component after we have access to thePlayer
                if (playerAudio == null)
                    playerAudio = thePlayer.GetComponentInParent<AudioSource>();

                sharedMovementFound = true;
                Debug.Log($"[PlayAudio] SharedMovement found. thePlayer={thePlayer.name}, theGuide={theGuide.name}, screenreader=false");
            }
            else if (thePlayer != null && FindObjectOfType<VRScreenreader>()) // Look for only player if we're in screenreader mode
            {
                // Assign playerAudio component after we have access to thePlayer
                if (playerAudio == null)
                    playerAudio = thePlayer.GetComponentInParent<AudioSource>();

                sharedMovementFound = true;
                Debug.Log($"[PlayAudio] SharedMovement found. thePlayer={thePlayer.name}, screenreader=true");
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
                Debug.Log("[PlayAudio] GuideFollow located; guide audio path enabled.");
            }
        }
    }

    private void LogClip(string reason, AudioClip clip)
    {
        string clipName = clip ? clip.name : "null";
        string srcName = playerAudio ? playerAudio.name : "no-source";
        string srcTag = playerAudio && playerAudio.transform ? playerAudio.transform.tag : "no-tag";
        Debug.Log($"[PlayAudio] {reason}: clip={clipName}, source={srcName}, tag={srcTag}");
    }
}
