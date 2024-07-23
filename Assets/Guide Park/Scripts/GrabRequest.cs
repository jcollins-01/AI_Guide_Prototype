using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class GrabRequest : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private PlayAudio m_PlayAudioScript;
    private VRHandling m_VRHandlingScript;
    private SharedMovement m_SharedMovementScript;

    // Game objects and components for determining grabbing player
    private GameObject thePlayer;
    private RealtimeTransform realtimeTransform;
    private XRGrabInteractable xrGrabInteractable;

    // Monitoring bools
    private bool playAudioFound = false;
    private bool controllersGrabbed = false;
    private bool sharedMovementFound = false;
    private bool gripping1 = false;
    private bool gripping2 = false;

    // Variables to access XR Controllers
    private InputDevice rightXRController;
    private InputDevice leftXRController;

    // Bools and components for handling grabbing audio
    private int grabSoundCount = 0;
    private bool grabbed = false;
    private AudioSource playerAudio;
    private AudioClip grabSound;
    
    // Start is called before the first frame update
    void Start()
    {
        // Grab necessary components on the game object being grabbed
        realtimeTransform = GetComponent<RealtimeTransform>();
        xrGrabInteractable = GetComponent<XRGrabInteractable>();

        // Assign sounds from Resources
        grabSound = Resources.Load<AudioClip>("Audio/grabbed");
    }

    // Update is called once per frame
    void Update()
    {
        // Grab necessary components from other scripts
        if (!playAudioFound)
            getPlayAudio();
        if (!controllersGrabbed)
            getControllers();
        if (!sharedMovementFound)
            getSharedMovement();

        // Once we have all necessary components, we can check for grab requests
        if (playAudioFound && controllersGrabbed)
            checkGrabRequest();
    }

    private void checkGrabRequest()
    {
        if (rightXRController.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            if (gripValue < 0.1f)
                gripping1 = false;
            else // We are gripping with this controller
                gripping1 = true;
        }

        if (leftXRController.TryGetFeatureValue(CommonUsages.grip, out float gripValue2))
        {
            if (gripValue2 < 0.1f)
                gripping2 = false;
            else // We are gripping with this controller
                gripping2 = true;
        }

        if (gripping1 == false && gripping2 == false) // If neither controller is gripping a grabbable
        {
            //Debug.Log("Neither controller is gripping grabbable - collisions back on.");
            //Physics.IgnoreLayerCollision(10, 6, false); // Teleportation Area
            //Physics.IgnoreLayerCollision(10, 7, false); // Non-Teleport Objects
            Physics.IgnoreLayerCollision(7, 0, false); // Default
            Physics.IgnoreLayerCollision(7, 6, false); // XR Rig
            Physics.IgnoreLayerCollision(7, 3, false); // Player
        }

        if (xrGrabInteractable.isSelected && (gripping1 || gripping2)) // If selected AND pressing a grip button - prevents gripping from teleport ray
        {
            realtimeTransform.RequestOwnership();
            grabbed = true;

            playerAudio.clip = grabSound;
            if (grabSoundCount == 0)
            {
                playerAudio.Play();
                grabSoundCount += 1;
            }

            // Ignore collisions between Default objects (layer 0), XRRig (layer 6), Player (layer 3), Teleport Area (layer 6)
            // Non-Teleport Obstacles (layer 8), and Interactable (layer 7)
            //Physics.IgnoreLayerCollision(10, 6, true); // Teleportation Area
            //Physics.IgnoreLayerCollision(10, 7, true); // Non-Teleport Objects
            Physics.IgnoreLayerCollision(7, 0, true); // Default
            Physics.IgnoreLayerCollision(7, 6, true); // XR Rig
            Physics.IgnoreLayerCollision(7, 3, true); // Player
        }
        else
            grabSoundCount = 0;

        // If we grabbed an object as the participant for Park 2 (scavenger hunt), transform the scale when an object is grabbed so it disappears
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("GuidePark2_Networked"))
        {
            if (grabbed == true && thePlayer.GetComponent<RealtimeView>().isOwnedLocallyInHierarchy)
                xrGrabInteractable.transform.localScale = new Vector3(0, 0, 0);
        }
    }

    private void getPlayAudio()
    {
        if (m_PlayAudioScript == null)
            m_PlayAudioScript = FindObjectOfType<PlayAudio>();
        else
        {
            playerAudio = m_PlayAudioScript.playerAudio;
            if (playerAudio != null)
                playAudioFound = true;
        }
    }

    private void getControllers()
    {
        if (m_VRHandlingScript == null)
            m_VRHandlingScript = FindObjectOfType<VRHandling>();
        else
        {
            rightXRController = m_VRHandlingScript.rightXRController;
            leftXRController = m_VRHandlingScript.leftXRController;
            controllersGrabbed = true;
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
        else
        {
            thePlayer = m_SharedMovementScript.thePlayer;
            if (thePlayer != null)
                sharedMovementFound = true;
        }
    }
}