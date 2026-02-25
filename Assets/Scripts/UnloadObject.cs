using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class UnloadObject : MonoBehaviour
{
    public bool playerUnloadedObject = false;
    private GameObject bag;
    private Collider bagBounds;
    private bool trackingStarted = false;
    private bool objectOutsideOfBag = false;
    private float raycastDistance = 10.0f;
    private string nameOfCollidingObject;
    private Coroutine unloadConfirmRoutine;
    private bool objectGrabbed = false;

    // Variables for scripts we need access to
    private ShortTaskController m_ShortTaskControllerScript;
    private RandomObjectSpawner m_UnloadSpawnerAndPrepareTaskScript;
    private VRScreenreader m_VRScreenreaderScript;
    private SwitchTools m_SwitchToolsScript;
    private XRGrabInteractable m_XRGrabInteractableScript;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("A new object has been spawned for unloading");

        // Grab switch tools script to check for VR Guide and Screenreader status
        m_SwitchToolsScript = FindFirstObjectByType<SwitchTools>();

        // Grab the unloading bag's existing reference collider and handle collisions with it
        CheckForBagReferenceCollider();

        // Add necessary components for grabbing and detecting grip button with object
        gameObject.layer = 7; // Make the object Interactable if it isn't already
        if (gameObject.GetComponent<XRGrabInteractable>() == null)
            gameObject.AddComponent<XRGrabInteractable>();
        if (gameObject.GetComponent<GrabRequest>() == null)
            gameObject.AddComponent<GrabRequest>();
        if (gameObject.GetComponent<BoxCollider>() == null)
        {
            gameObject.AddComponent<BoxCollider>(); // To ensure it doesn't fall through the bag, can comment to test if unloading works
            // Sets the collider with a larger y to ensure it stays in the bounds of the bag - if it's too small of an object, it will pass through and count as an unload
            gameObject.GetComponent<BoxCollider>().size = new Vector3(gameObject.GetComponent<BoxCollider>().size.x, gameObject.GetComponent<BoxCollider>().size.y * 1.5f, gameObject.GetComponent<BoxCollider>().size.z);
        }

        // Add a collider to the bag for detecting its bounds + physics
        if (bag != null)
        {
            if (!bag.GetComponent<Collider>())
                bagBounds = bag.AddComponent<BoxCollider>();
            else
                bagBounds = bag.GetComponent<Collider>();

            bag.GetComponent<Collider>().isTrigger = true; // To prevent objects from spawning on top of bag or flying out of it by colliding with it

            // Grab the interaction table prefab from the task controller, and add a collider to prevent the object from falling to the floor
            m_ShortTaskControllerScript = FindObjectOfType<ShortTaskController>();
            if (!m_ShortTaskControllerScript.interactionTable.GetComponent<Collider>())
                m_ShortTaskControllerScript.interactionTable.AddComponent<BoxCollider>();

            // Grab the screenreader script to access the unloadingBagCollider
            m_VRScreenreaderScript = FindObjectOfType<VRScreenreader>();

            m_UnloadSpawnerAndPrepareTaskScript = m_ShortTaskControllerScript.unloadingBag.GetComponent<RandomObjectSpawner>();
            m_XRGrabInteractableScript = m_UnloadSpawnerAndPrepareTaskScript.spawnedObject.GetComponent<XRGrabInteractable>();

            if (m_XRGrabInteractableScript != null)
            {
                m_XRGrabInteractableScript.selectEntered.AddListener(CheckSpawnedObjectGrabbed);
                m_XRGrabInteractableScript.selectExited.AddListener(CheckSpawnedObjectReleased);
            }

            // Alt. grab the unloading bag, find child object named ReaderReference, get collider from that child, ignore collisions

            if (bagBounds != null)
                StartCoroutine(StartUnloadCheckAfterDelay()); // Start the unload check with a delay to avoid immediate triggering
        }
    }

    // Update is called once per frame
    void Update()
    {
        bool raycastCheckAbovePrepTable = CheckIfObjectAboveInteractionTable();

        // Check if the object is no longer inside the bag's bounds
        if (!bagBounds.bounds.Contains(transform.position))
        {
            objectOutsideOfBag = true;
            // Debug.Log("Player lifted the object outside of the bag.");
        }
        else if (bagBounds.bounds.Contains(transform.position))
        {
            // Debug.Log("object is still in the bag");
            objectOutsideOfBag = false;
            return;
        }

        // Check if the object has been released outside the bounds of the bag
        if (trackingStarted && !playerUnloadedObject && !objectGrabbed && raycastCheckAbovePrepTable && bagBounds != null)
        {
            if (unloadConfirmRoutine == null)
            {
                unloadConfirmRoutine = StartCoroutine(ConfirmUnloadAfterADelay());
            }
        }
        else if (trackingStarted && objectOutsideOfBag && !objectGrabbed && !raycastCheckAbovePrepTable && bagBounds != null) // object falls off table
        {
            // Debug.Log("object was not correctly placed; now destroying and spawning a new one");
            m_UnloadSpawnerAndPrepareTaskScript.ResetUnloadingPortion(false);
        }
    }

    private IEnumerator ConfirmUnloadAfterADelay()
    {
        yield return new WaitForSeconds(1.0f);

        bool onTable = !objectGrabbed && CheckIfObjectAboveInteractionTable() && 
                            nameOfCollidingObject == m_ShortTaskControllerScript.interactionTable.name;
        if (!onTable)
        {
            unloadConfirmRoutine = null;
            m_UnloadSpawnerAndPrepareTaskScript.ResetUnloadingPortion(false);
            yield break;
        }

        // Revert reference collider layer to restore proper collision effects between it and other interactables
        m_ShortTaskControllerScript.unloadingBag.layer = 13;
        m_UnloadSpawnerAndPrepareTaskScript.spawnedObject.layer = 0;
        playerUnloadedObject = true;
        // Destroy the grabbable component so the object can't be grabbed anymore
        Destroy(m_UnloadSpawnerAndPrepareTaskScript.spawnedObject.GetComponentInChildren<XRGrabInteractable>());
        m_ShortTaskControllerScript.unloadingTaskScore++;
        m_UnloadSpawnerAndPrepareTaskScript.audioSource.clip = m_UnloadSpawnerAndPrepareTaskScript.unloaded;
        m_UnloadSpawnerAndPrepareTaskScript.audioSource.Play();
        Debug.Log("move on to prep portion of task");

        m_UnloadSpawnerAndPrepareTaskScript.StartPreparationPart();
        Destroy(m_UnloadSpawnerAndPrepareTaskScript.spawnedObject.GetComponent<UnloadObject>()); // destroy script so that no other code runs after we've moved on
    }

    private void CheckSpawnedObjectGrabbed(SelectEnterEventArgs args)
    {
        objectGrabbed = true;
    }

    private void CheckSpawnedObjectReleased(SelectExitEventArgs args)
    {
        objectGrabbed = false;
    }

    void OnDestroy()
    {
        if (m_XRGrabInteractableScript != null)
        {
            m_XRGrabInteractableScript.selectEntered.RemoveListener(CheckSpawnedObjectGrabbed);
            m_XRGrabInteractableScript.selectExited.RemoveListener(CheckSpawnedObjectReleased);
        }
    }

    private bool CheckIfObjectAboveInteractionTable()
    {
        if (m_UnloadSpawnerAndPrepareTaskScript.spawnedObject != null)
        {
            RaycastHit[] returnedRaycastHits = Physics.RaycastAll(m_UnloadSpawnerAndPrepareTaskScript.spawnedObject.transform.position, Vector3.down, raycastDistance);

            foreach (RaycastHit hit in returnedRaycastHits)
            {
                if (hit.collider.gameObject == m_ShortTaskControllerScript.interactionTable)
                {
                    // Debug.Log("above prep table!");
                    return true;
                }
            }
        }
        return false;
    }

    void OnCollisionEnter(Collision other)
    {
        nameOfCollidingObject = other.gameObject.name;
        // Debug.Log($"The object that spawnedObject collided with: {nameOfCollidingObject}");
    }

    void OnCollisionExit(Collision other)
    {
        nameOfCollidingObject = null;
        // Debug.Log($"The object that spawnedObject is no longer colliding with: {other.gameObject.name}");
    }

    private IEnumerator StartUnloadCheckAfterDelay()
    {
        // Wait for a specified delay to allow the object to settle inside the bag
        yield return new WaitForSeconds(1.0f);
        trackingStarted = true;
    }

    public void AssignBag(GameObject passedBag)
    {
        bag = passedBag;
    }

    private void CheckForBagReferenceCollider()
    {
        BoxCollider bagReferenceCollider;

        if (m_SwitchToolsScript.VRGuideActive)
        {
            bagReferenceCollider = bag.GetComponentInChildren<BoxCollider>();
        }
        else
        {
            // Ignore collisions between the spawned ingredients and the unloadingBag's ref collider so ingredients can fall in bag
            bagReferenceCollider = bag.transform.Find("Reader Reference(Clone)").GetComponentInChildren<BoxCollider>();
        }
    }
}
