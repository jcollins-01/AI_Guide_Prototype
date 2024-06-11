using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class SharedMovement : MonoBehaviour
{
    // Variables to hold scripts and Game Objects we need access to
    private AIGuide m_AIGuideScript;
    public VRHandling m_VRHandlingScript;
    public GameObject thePlayer;
    public GameObject theGuide;
    private XROrigin playerRig;

    // Variables to keep track of positioning between guide and player
    private Vector3 playerGuideOffset;
    private bool enteredTrigger = false;

    // Variables to access XR Controllers
    private InputDevice rightXRController;
    private InputDevice leftXRController;

    // Variables to share player actions with other scripts
    public bool playerGrabbingGuide = false;
    public CapsuleCollider guideCollider;

    // Start is called before the first frame update
    void Start()
    {
        // Assigns the player's XR Origin
        playerRig = FindObjectOfType<XROrigin>();

        // Creates the CameraSystem for the guide to keep track of Player's Movement with
        gameObject.AddComponent<CameraSystem>();

        // Ignore collisions between Player and XR Rig
        Physics.IgnoreLayerCollision(3, 6, true);
        CharacterController control = FindObjectOfType<CharacterController>();
        control.detectCollisions = true;
    }

    // Update is called once per frame
    void Update()
    {
        // Puts assignment of roles in one line that can be commented out
        // Maybe put this under an if that is only called if there is more than one realtime component in scene, more than one player
        AssignRoles();

        // If we have controllers assigned, we can send haptic impulses and try shared movement
        if (m_VRHandlingScript != null)
        {
            // Sends haptic feedback to the controller being used for "grabbing" the guide
            if (rightXRController.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && enteredTrigger)
            {
                if (gripValue > 0.1f)
                {
                    StartCoroutine(Teleport());
                    rightXRController.SendHapticImpulse(1u, 0.25f, 1f);
                    playerGrabbingGuide = true;
                }
            }
            else
            {
                //Debug.Log("NOT grabbing guide");
                StopCoroutine(Teleport());
                playerGrabbingGuide = false;
            }
                

            if (leftXRController.TryGetFeatureValue(CommonUsages.grip, out float gripValue2) && enteredTrigger)
            {
                if (gripValue2 > 0.1f)
                {
                    StartCoroutine(Teleport());
                    leftXRController.SendHapticImpulse(1u, 0.25f, 1f);
                    playerGrabbingGuide = true;
                }
            }
            else
            {
                StopCoroutine(Teleport());
                playerGrabbingGuide = false;
            }  
        }
    }

    void AssignRoles()
    {
        // If the player joins the scene before the guide, make sure to check for guide components until all are assigned
        if (theGuide == null)
            AssignGuide();

        // The player should be null since they need to be instantiated in the multiplayer scene at runtime
        if (thePlayer == null)
            AssignPlayer();
    }

    // Finds necessary components from Guide scripts + assigns Guide game object
    void AssignGuide()
    {
        // Assigns the guide as the Game Object with the AIGuide script
        theGuide = FindObjectOfType<AIGuide>().transform.gameObject;

        // Finds the VR Handling script on the Guide game object
        m_VRHandlingScript = theGuide.GetComponentInChildren<VRHandling>();

        // Grabs AIGuide script from the Game Object assigned as guide and pulls input device refs
        m_AIGuideScript = theGuide.GetComponent<AIGuide>();
        rightXRController = m_VRHandlingScript.rightXRController;
        leftXRController = m_VRHandlingScript.leftXRController;
    }

    // Sets up the needed components and determines who the main player (participant) is
    void AssignPlayer()
    {
        // Gets a list of all realtimeViews in the scene
        var foundViews = FindObjectsOfType<Normal.Realtime.RealtimeView>();
        List<GameObject> foundPlayers = new List<GameObject>();

        // Checks which ones are root objects, which would make them players
        foreach (Normal.Realtime.RealtimeView realtimeView in foundViews)
        {
            if (IsRootObject(realtimeView.gameObject))
                foundPlayers.Add(realtimeView.gameObject);
        }

        // The first player who joined the scene is marked as the participant
        thePlayer = foundPlayers[0];

        // Destroy the necessary physical components - this was needed to make sure participant/guide couldn't grab?
        //Destroy(theGuide.GetComponent<SharedMovement>());

        // Grabs the necessary physics components for Shared Movement
        Rigidbody playerRigidbody = thePlayer.GetComponent<Rigidbody>();
        CapsuleCollider playerCollider = thePlayer.GetComponent<CapsuleCollider>();
        Rigidbody guideRigidbody = theGuide.GetComponentInChildren<Rigidbody>();
        guideCollider = theGuide.GetComponentInChildren<CapsuleCollider>();

        // Sets the values appropriately for each component to perform Shared Movement
        // thePlayer needs a rigidbody, no gravity, kinematic, non-trigger collider
        playerRigidbody.useGravity = false;
        playerRigidbody.isKinematic = true;
        playerCollider.radius = 0.5f;
        playerCollider.height = 0.5f;
        playerCollider.center = new Vector3(0f, 1f, 0f);
        // theGuide needs a rigidbody, no gravity, kinematic, collider with trigger
        guideRigidbody.useGravity = false;
        guideRigidbody.isKinematic = true;
        guideCollider.isTrigger = true;
        guideCollider.radius = 1.5f;
        guideCollider.height = 0.5f;
        guideCollider.center = new Vector3(0f, 1f, 0f);
    }

    // Function to check if an object is the root of its hierarchy
    bool IsRootObject(GameObject obj)
    {
        // Check if the object has no parent
        return obj.transform.parent == null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        string name = collision.gameObject.name;
        Debug.Log("Colliding with " + name);
    }

    public void OnTriggerEnter(Collider other)
    {
        enteredTrigger = true;
        Debug.Log("Collision detected");

        // On collisions with objects, if the other object has a grab interactable component (is an interactable), keep collisions on
        // If not, turn collisions off - the guide falls in this second category where we want to ignore collisions while we're grabbing it
        XRGrabInteractable grab = other.GetComponent<XRGrabInteractable>();
        if (grab.interactorsSelecting.Count == 1)
            Physics.IgnoreCollision(thePlayer.GetComponent<Collider>(), other);
        else
            Physics.IgnoreCollision(thePlayer.GetComponent<Collider>(), other, false);
    }

    public void OnTriggerExit(Collider other)
    {
        enteredTrigger = false;
    }

    public IEnumerator Teleport()
    {
        yield return new WaitForSeconds(0f);

        // Creates a series of directional movements that work relative to world space
        Vector3 backRelative = playerRig.transform.TransformDirection(Vector3.back);
        Vector3 downRelative = playerRig.transform.TransformDirection(Vector3.down);
        Vector3 rightRelative = playerRig.transform.TransformDirection(Vector3.right);

        // Uses position of the guide as local space for directional movements
        // to create an offset of distance between the player and the guide when teleporting
        playerGuideOffset = theGuide.transform.position + (rightRelative) / 2 + (backRelative) / 2;
        playerRig.transform.position = playerGuideOffset;

        // Grabs the forward vector of the guide (direction the guide is facing)
        Vector3 forward = theGuide.transform.forward;
        // Zero out the y component of the forward vector to only keep the directions in the X, Z planes
        forward.y = 0;
        float headingAngle = Quaternion.LookRotation(forward).eulerAngles.y;
        // Applies new rotation angle so the player faces the same direction as the guide
        thePlayer.transform.rotation = Quaternion.Euler(0.0f, headingAngle, 0.0f);
    }
}
