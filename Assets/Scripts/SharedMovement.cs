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
    private VRHandling m_VRHandlingScript;
    public GameObject thePlayer;
    public GameObject theGuide;
    private XROrigin playerRig;

    // Variables to keep track of positioning between guide and player
    private Vector3 playerGuideOffset;
    private bool enteredTrigger = false;

    // Variables to access XR Controllers
    private InputDevice rightXRController;
    private InputDevice leftXRController;

    // Start is called before the first frame update
    void Start()
    {
        // Assigns the guide as the Game Object with the name "Guide"
        theGuide = GameObject.Find("Guide");

        // Grabs AIGuide script from the Game Object assigned as guide and pulls input device refs
        m_AIGuideScript = theGuide.GetComponent<AIGuide>();
        rightXRController = m_VRHandlingScript.rightXRController;
        leftXRController = m_VRHandlingScript.leftXRController;
    }

    // Update is called once per frame
    void Update()
    {
        // The player should be null since they need to be instantiated in the multiplayer scene at runtime
        // The following statements set up the needed components and determine who the main player (participant) is
        if (thePlayer == null)
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
            Rigidbody guideRigidbody = theGuide.GetComponent<Rigidbody>();
            CapsuleCollider guideCollider = theGuide.GetComponent<CapsuleCollider>();

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
            guideCollider.radius = 0.5f;
            guideCollider.height = 0.5f;
            guideCollider.center = new Vector3(0f, 1f, 0f);
        }

        // Finds the game object named Guide in the hierarchy and assigns it
        if (theGuide ==  null)
        {
            theGuide = GameObject.Find("Guide");

            // If the avatar joining is NOT the player, destroy the Guide object
            // This will need to be adjusted/tested with other clients joining the scene + making sure the participant is selected properly
            // Might need to have a shared name component attached to each one
            /*if (!thePlayer.GetComponent<Normal.Realtime.RealtimeView>().isOwnedLocallyInHierarchy)
            {
                Destroy(theGuide);
            }*/

        }

        // Sends haptic feedback to the controller being used for "grabbing" the guide
        if (rightXRController.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && enteredTrigger)
        {
            if (gripValue > 0.1f)
            {
                StartCoroutine(Teleport());
                rightXRController.SendHapticImpulse(1u, 0.25f, 1f);
            }
        }
        else
            StopCoroutine(Teleport());

        if (leftXRController.TryGetFeatureValue(CommonUsages.grip, out float gripValue2) && enteredTrigger)
        {
            if (gripValue2 > 0.1f)
            {
                StartCoroutine(Teleport());
                leftXRController.SendHapticImpulse(1u, 0.25f, 1f);
            }
        }
        else
            StopCoroutine(Teleport());
    }

    // Function to check if an object is the root of its hierarchy
    bool IsRootObject(GameObject obj)
    {
        // Check if the object has no parent
        return obj.transform.parent == null;
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
