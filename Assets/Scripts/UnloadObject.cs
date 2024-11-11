using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class UnloadObject : MonoBehaviour
{
    public bool playerUnloadedObject = false;
    private GameObject bag;
    private Collider bagBounds;
    private bool isGrabbed = false;
    private bool trackingStarted = false;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("A new object has been spawned for unloading");

        // Add necessary components for grabbing and detecting grip button with object
        this.gameObject.layer = 7; // Make the object Interactable if it isn't already
        this.gameObject.AddComponent<XRGrabInteractable>();
        this.gameObject.AddComponent<GrabRequest>();
        this.gameObject.AddComponent<BoxCollider>(); // To ensure it doesn't fall through the bag, can comment to test if unloading works

        // Add a collider to the bag for detecting its bounds + physics
        if (bag != null)
        {
            if (!bag.GetComponent<Collider>())
                bagBounds = bag.AddComponent<BoxCollider>();
            else
                bagBounds = bag.GetComponent<Collider>();

            if (bagBounds != null)
                StartCoroutine(StartUnloadCheckAfterDelay()); // Start the unload check with a delay to avoid immediate triggering
        }
    }

    // Update is called once per frame
    void Update()
    {
        CheckIfGrabbed();

        // Check if the object has been released outside the bounds of the bag
        if (trackingStarted && !isGrabbed && playerUnloadedObject == false && bagBounds != null)
        {
            // Check if the object is no longer inside the bag's bounds
            if (!bagBounds.bounds.Contains(transform.position))
            {
                playerUnloadedObject = true;
                Debug.Log("Player unloaded the object outside the bag.");
            }
            else
            {
                //Debug.Log("Object is currently inside the bag, waiting to be unloaded");
            }
        }
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

    private void CheckIfGrabbed()
    {
        if (GetComponent<GrabRequest>() != null)
            isGrabbed = GetComponent<GrabRequest>().grabbed;
    }
}
