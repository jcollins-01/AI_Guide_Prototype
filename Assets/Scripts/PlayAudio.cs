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

    // Variables to hold scripts we need access to
    private SharedMovement m_SharedMovementScript;
    //private ConfederateHandler m_ConfederateHandlerScript;
    private GuideFollow m_GuideFollowScript;

    // Monitoring bools
    private bool sharedMovementFound = false;
    //private bool confederateHandlerFound = false;
    private bool guideFollowFound = false;

    // Audio sources for sonification
    public AudioSource playerAudio;
    private AudioSource guideAudio;

    // Sound effects for player sonification
    private AudioClip teleportEffect;
    private AudioClip walkEffect;
    private AudioClip woodEffect;
    private AudioClip waterEffect;
    private AudioClip grassEffect;
    private AudioClip turnEffect;
    private AudioClip woodCollisionEffect;
    private AudioClip collisionEffect;

    // Sound effects for guide sonification
    private AudioClip idleEffect;
    private AudioClip noEffect; // For sharing sound properly

    // For sharing audio over network (not implemented yet)
    private AudioClip currentClip;
    private string surfaceMaterial;

    // Start is called before the first frame update
    void Start()
    {
        // Grab necessary components from scene
        interactionManager = FindObjectOfType<XRInteractionManager>();
        teleport = FindObjectOfType<TeleportationProvider>();
        move = FindObjectOfType<ActionBasedContinuousMoveProvider>();

        // Assign sounds from Resources
        teleportEffect = Resources.Load<AudioClip>("Audio/teleport");
        walkEffect = Resources.Load<AudioClip>("Audio/walk");
        woodEffect = Resources.Load<AudioClip>("Audio/wood-walk");
        waterEffect = Resources.Load<AudioClip>("Audio/water-walk");
        grassEffect = Resources.Load<AudioClip>("Audio/grass-walk");
        turnEffect = Resources.Load<AudioClip>("Audio/turn");
        woodCollisionEffect = Resources.Load<AudioClip>("Audio/wooden-collision");
        collisionEffect = Resources.Load<AudioClip>("Audio/general-collision");

        idleEffect = Resources.Load<AudioClip>("Audio/guide_idle");
        noEffect = Resources.Load<AudioClip>("Audio/nothing");
    }

    // Update is called once per frame
    void Update()
    {
        // Grab components we need access to
        //if (!confederateHandlerFound)
            //getConfederateHandler();
        if (!sharedMovementFound)
            getSharedMovement();
        if (!guideFollowFound)
            getGuideFollow();

        // If we have shared movement components assigned (a guide and player) OR we found the confederate handler and are a confederate
        if (sharedMovementFound) // || (confederateHandlerFound && m_ConfederateHandlerScript.confederateVersion))
        {
            if (playerAudio.isPlaying)
                currentClip = playerAudio.clip;
            else
                currentClip = noEffect;

            // Check movements of players and guide
            Vector3 lastPosition = transform.GetComponentInParent<CharacterController>().transform.position;
            //Debug.Log("Checking last position");
            StartCoroutine(checkTeleport());
            StartCoroutine(checkTurning());
            StartCoroutine(checkMoving(lastPosition));
        }
    }

    public IEnumerator checkTeleport()
    {
        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done)
        {
            playerAudio.clip = teleportEffect;
            playerAudio.Play();
        }

        yield return new WaitForSeconds(0F);
    }

    public IEnumerator checkTurning()
    {
        //DeviceBasedSnapTurnProvider snapTurn = FindObjectOfType<DeviceBasedSnapTurnProvider>();
        ActionBasedSnapTurnProvider snapTurn = FindObjectOfType<ActionBasedSnapTurnProvider>();

        if (snapTurn.locomotionPhase == LocomotionPhase.Moving) // If the movement of snap turning is active
        {
            playerAudio.clip = turnEffect;
            playerAudio.Play();
        }

        yield return new WaitForSeconds(0F);
    }

    public IEnumerator checkMoving(Vector3 lastPosition)
    {
        // First wait to check for a new position
        yield return new WaitForSeconds(0.000001f);
        // If the Vector3 of the currPosition is different from the lastPosition
        Vector3 currPosition = transform.GetComponentInParent<CharacterController>().transform.position;

        // If our last clip playing was any of the walking effects, we don't wait for them to be done playing before switching
        if (playerAudio.clip == walkEffect || playerAudio.clip == woodEffect || playerAudio.clip == grassEffect || playerAudio.clip == waterEffect)
        {
            if (currPosition != lastPosition)
            {
                if (surfaceMaterial == "wood")
                    playerAudio.clip = woodEffect;
                else if (surfaceMaterial == "water")
                    playerAudio.clip = waterEffect;
                else if (surfaceMaterial == "grass")
                    playerAudio.clip = grassEffect;
                else
                    playerAudio.clip = walkEffect;

                if (!playerAudio.isPlaying)
                    playerAudio.Play();
            }
            else // If position hasn't changed
            {
                if (guideFollowFound) // if we are playing as the player with a guide
                {
                    playerAudio.clip = idleEffect;
                    if (!playerAudio.isPlaying)
                        playerAudio.Play();
                }
            }
        }
        else // We wait for the audio clip to finish before assigning a walk clip
        {
            if (!playerAudio.isPlaying)
            {
                if (currPosition != lastPosition)
                {
                    if (surfaceMaterial == "wood")
                        playerAudio.clip = woodEffect;
                    else if (surfaceMaterial == "water")
                        playerAudio.clip = waterEffect;
                    else if (surfaceMaterial == "grass")
                        playerAudio.clip = grassEffect;
                    else
                        playerAudio.clip = walkEffect;

                    if (!playerAudio.isPlaying)
                        playerAudio.Play();
                }
                else // If position hasn't changed
                {
                    if (guideFollowFound) // if we are playing as the player with a guide
                    {
                        playerAudio.clip = idleEffect;
                        if (!playerAudio.isPlaying)
                            playerAudio.Play();
                    }
                }
            } // End if (!playerAudio.isPlaying)
        }

        yield return new WaitForSeconds(0.000001f);
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

        // If we hit Obstacles (layer 8), play a collision sound
        if (hit.gameObject.layer == 8)
        {
            if (hit.transform.tag == "Wood")
                playerAudio.clip = woodCollisionEffect;
            else if (hit.transform.tag == "Player") // When collisions between Player and Rig are on at the scene open, ensure no collision sound occurs
                playerAudio.clip = noEffect;
            else
                playerAudio.clip = collisionEffect;
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
        }
    }

    /*private void getConfederateHandler()
    {
        if (m_ConfederateHandlerScript == null)
            m_ConfederateHandlerScript = FindObjectOfType<ConfederateHandler>();
        else
            confederateHandlerFound = true;
    }*/

    private void getGuideFollow()
    {
        // If there is a GuideFollow component in the scene (we are in the scene with the Guide's rig), look to assign guide follow
        // This will not work for a confederate scene
        if (FindObjectOfType<GuideFollow>())
        {
            if (m_GuideFollowScript == null)
                m_GuideFollowScript = FindObjectOfType<GuideFollow>();
            else
                guideFollowFound = true;
        }
    }
}