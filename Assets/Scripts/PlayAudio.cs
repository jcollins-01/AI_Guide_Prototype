using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;

public class PlayAudio : MonoBehaviour
{
    // Components to grab from scene and scripts
    public AudioSource playerAudio;
    private TeleportationProvider teleport;
    private ActionBasedContinuousMoveProvider move;
    private XRInteractionManager interactionManager;
    private GameObject thePlayer;
    private GameObject theGuide;

    // Variables to hold scripts we need access to
    private SharedMovement m_SharedMovementScript;

    // Monitoring bools
    private bool sharedMovementFound = false;

    // Sound effects for sonification
    private AudioClip teleportEffect;
    private AudioClip walkEffect;
    private AudioClip woodEffect;
    private AudioClip waterEffect;
    private AudioClip grassEffect;
    private AudioClip turnEffect;
    private AudioClip idleEffect; // Guide only
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
        idleEffect = Resources.Load<AudioClip>("Audio/guide_idle");
        noEffect = Resources.Load<AudioClip>("Audio/nothing");
    }

    // Update is called once per frame
    void Update()
    {
        if (!sharedMovementFound)
            getSharedMovement();
        
        if (sharedMovementFound)
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

    IEnumerator checkTeleport()
    {
        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done)
        {
            playerAudio.clip = teleportEffect;
            playerAudio.Play();
        }

        yield return new WaitForSeconds(0F);
    }

    IEnumerator checkTurning()
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

    IEnumerator checkMoving(Vector3 lastPosition)
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
                if (theGuide.GetComponentInParent<RealtimeView>().isOwnedLocallyInHierarchy) //if we are playing as theGuide
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
                    if (theGuide.GetComponentInParent<RealtimeView>().isOwnedLocallyInHierarchy) //if we are playing as theGuide
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

        if (hit.transform.tag == "Wood")
            surfaceMaterial = "wood";
        else if (hit.transform.tag == "Water")
            surfaceMaterial = "water";
        else if (hit.transform.tag == "Grass")
            surfaceMaterial = "grass";
        else
            surfaceMaterial = "other";
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
}