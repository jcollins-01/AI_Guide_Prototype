using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class PrepareObject : MonoBehaviour
{
    public bool playerPreparedObject = false;
    public bool playerMidPreparation = false;
   
    private GameObject table;
    private GameObject prepTool;
    private Vector3 prepToolSpawnPosition;
    private Quaternion prepToolSpawnRotation;

    // Variables for handling preparation zone and timing
    public float requiredHoldTime = 3.0f; // Time in seconds the button must be held
    private float holdTime = 0.0f;
    private bool isToolNearby = false;

    // Variables for XR input
    private VRHandling m_VRHandlingScript;
    private InputDevice rightXRController;
    private InputDevice leftXRController;
    private bool controllersGrabbed = false;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("A new object has been spawned for preparation");

        // Find necessary components
        m_VRHandlingScript = FindObjectOfType<VRHandling>();

        // Add necessary components for spawned object physics + trigger detection with prep tool
        this.gameObject.layer = 7; // Make the object Interactable if it isn't already

        // Add a collider to the table for physics
        if (table != null)
        {
            if (!table.GetComponent<Collider>())
                table.AddComponent<BoxCollider>();
        }

        // Assign prepTool based on environment
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        switch(sceneName)
        {
            case "Kitchen":
                prepTool = GameObject.Find("Knife");
                break;
            case "Alien Spaceship Repair Shop":
                prepTool = GameObject.Find("Sonic Screwdriver Tool");
                break;
            case "Flower Shop":
                prepTool = GameObject.Find("Watering Can Tool");
                break;
            case "Monster Pet Shop":
                prepTool = GameObject.Find("Brush");
                break;
            case "Witch Cottage":
                prepTool = GameObject.Find("Magic Wand Tool");
                break;
            case "Pharmacy":
                prepTool = GameObject.Find("High Tech Scanner Tool");
                break;
        }

        prepTool.gameObject.layer = 7;
        prepToolSpawnPosition = prepTool.transform.position;
        prepToolSpawnRotation = prepTool.transform.rotation;

        // Create a new GameObject to hold a trigger collider and child it under the spawned object
        // This makes the trigger collider size a bit more precise as it follows its own scale, rather than a game object's scale when added directly
        GameObject trigger = new GameObject();
        trigger.name = "Prep Trigger Collider";
        trigger.transform.parent = this.transform.transform;

        // Get the world position and rotation of the parent object so the new child is positioned correctly
        Transform parentTransform = this.gameObject.transform;
        trigger.transform.position = parentTransform.position;
        trigger.transform.rotation = parentTransform.rotation;

        // Set up a new collider for the prep tool as a trigger so we can detect when it gets close to this spawned object
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(1f, 1f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        // Need to press and hold primary button for three seconds once knife is pressed against object to prepare it
        // Grab XR controllers for preparation
        if (!controllersGrabbed)
            AssignHandling();

        if (controllersGrabbed)
            CheckIfPrepToolNearbyAndPressed();
    }

    private void CheckIfPrepToolNearbyAndPressed()
    {
        // Create a variable to track primary button press
        bool isButtonPressed;
        rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out isButtonPressed);

        if (isToolNearby && isButtonPressed)
        {
            //Debug.Log("Entered mid prep");
            playerMidPreparation = true;
            holdTime += Time.deltaTime;
            // Debug.Log(holdTime);
            if (holdTime >= requiredHoldTime && !playerPreparedObject)
            {
                XRGrabInteractable grab = prepTool.GetComponentInChildren<XRGrabInteractable>();
                // If the prepTool is being held by user when they finish preparing
                if (grab.isSelected)
                {
                    // Forcefully detatch the held prepTool from whoever is holding it
                    IXRSelectInteractor interactor = grab.firstInteractorSelecting;
                    grab.interactionManager.SelectExit(interactor, grab);
                }

                // Use prepTool spawn position and rotation to return prepTool to original spot
                prepTool.transform.position = prepToolSpawnPosition;
                prepTool.transform.rotation = prepToolSpawnRotation;

                // Reset hold time since tool moves away too quickly for else to catch
                holdTime = 0.0f;
                playerMidPreparation = false;

                // Mark object as prepared last to hopefully prevent grabbing bug
                playerPreparedObject = true;
                Debug.Log("Player prepared the object.");
            }
        }
        else
        {
            //Debug.Log("Not prepping");
            holdTime = 0.0f; // Reset hold time if button is released or tool is not nearby
            playerMidPreparation = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == prepTool)
        {
            isToolNearby = true;
            Debug.Log("Prep tool is close to the object.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == prepTool)
        {
            isToolNearby = false;
            Debug.Log("Prep tool moved away from the object.");
        }
    }

    public void AssignTable(GameObject passedTable)
    {
        table = passedTable;
    }

    private void AssignHandling()
    {
        // If we have the VR Handling script, and both controllers have been grabbed
        if (m_VRHandlingScript != null)
        {
            if (m_VRHandlingScript.rightControllerGrabbed && m_VRHandlingScript.leftControllerGrabbed)
            {
                // Pulls input device refs
                rightXRController = m_VRHandlingScript.rightXRController;
                leftXRController = m_VRHandlingScript.leftXRController;
                controllersGrabbed = true;
            }
        }
    }
}
