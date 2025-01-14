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
                prepTool = GameObject.Find("Sonic Screwdriver");
                break;
        }

        prepToolSpawnPosition = prepTool.transform.position;
        prepToolSpawnRotation = prepTool.transform.rotation;

        // Set up a new collider for the prep tool as a trigger so we can detect when it gets close to this spawned object
        //SphereCollider triggerCollider = prepTool.AddComponent<SphereCollider>();
        BoxCollider triggerCollider = this.gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        //triggerCollider.radius = 2f;
        triggerCollider.size = new Vector3(1.5f, 1.5f, 1.5f);
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
            if (holdTime >= requiredHoldTime && !playerPreparedObject)
            {
                playerPreparedObject = true;
                Debug.Log("Player prepared the object.");

                // Use prepTool spawn position and rotation to return prepTool to original spot
                prepTool.transform.position = prepToolSpawnPosition;
                prepTool.transform.rotation = prepToolSpawnRotation;
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
