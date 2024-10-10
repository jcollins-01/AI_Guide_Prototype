using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class VRScreenreader : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private VRHandling m_VRHandlingScript;
    private SharedMovement m_SharedMovementScript;

    // Components to grab from scripts
    private TeleportationProvider teleport;

    // Variables to access XR Controllers
    private InputDevice rightXRController;
    private InputDevice leftXRController;
    private bool controllersGrabbed = false;

    // Variables to hold reader references we need globally
    List<GameObject> readerReferences = new List<GameObject>();
    List<GameObject> environmentCues = new List<GameObject>();
    Dictionary<GameObject, float> referencesAndDistances = new Dictionary<GameObject, float>();

    // Variables for reader reticles and raycast
    public LayerMask raycastLayerMask;
    GameObject leftReaderReticle;
    GameObject rightReaderReticle;
    GameObject thePlayer;

    // Monitoring bools
    private bool sharedMovementFound = false;

    // Start is called before the first frame update
    void Start()
    {
        GetReaderReferences();

        leftReaderReticle = Resources.Load<GameObject>("Screenreader/Left Reader Reticle");
        rightReaderReticle = Resources.Load<GameObject>("Screenreader/Right Reader Reticle");
        teleport = FindObjectOfType<TeleportationProvider>();
    }

    // Update is called once per frame
    void Update()
    {
        // Get needed components that couldn't be grabbed at Start
        if (!controllersGrabbed)
            AssignHandling();

        if (!sharedMovementFound)
            getSharedMovement();

        // Activate audio screenreader functions
        if (controllersGrabbed)
        {
            // Perform raycast for left and right controllers
            ShootRaycast(leftXRController, leftReaderReticle);
            ShootRaycast(rightXRController, rightReaderReticle);

            if (teleport && sharedMovementFound)
                PlayReferenceAudioPostTeleport();
        }

        // Activate haptic screenreader functions
        if (sharedMovementFound)
            PlayHapticsNearingObstacles();
    }

    public void CheckReferenceAndPlayAudio(Collider hit)
    {
        Debug.Log("Reader reticle is being checked");
        AudioSource selectedAudio;

        if (environmentCues.Contains(hit.gameObject))
        {
            Debug.Log("Hit is a layout / environmental object: " + hit.gameObject.layer);
            selectedAudio = hit.transform.Find("Environment Cue").GetComponent<AudioSource>();
            if (!selectedAudio.isPlaying)
                selectedAudio.Play(); // Play the sound automatically as it is hit
            Debug.Log("Now playing from " + selectedAudio);
        }
        else // User needs to press button for sounds to play
        {
            // If PC user presses and holds space
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // If this is an environment object, play its label and description
                if (hit.transform.Find("Environment Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    Debug.Log("Reader reticle is hitting a reader reference with an environment label + description");
                    selectedAudio = hit.transform.Find("Environment Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying)
                        selectedAudio.Play();
                    Debug.Log("Now playing from " + selectedAudio);
                }
                // If this is a key item object, play its label and description
                if (hit.transform.Find("Object Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    Debug.Log("Reader reticle is hitting a reader reference with an object label + description");
                    selectedAudio = hit.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying)
                        selectedAudio.Play();
                    Debug.Log("Now playing from " + selectedAudio);
                }
            }

            // If VR user presses right primary button on an XR controller
            if (m_VRHandlingScript.isButtonPressed)
            {
                // If this is an environment object, play its label and description
                if (hit.transform.Find("Environment Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    Debug.Log("Reader reticle is hitting a reader reference with an environment label + description");
                    selectedAudio = hit.transform.Find("Environment Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying)
                        selectedAudio.Play();
                    Debug.Log("Now playing from " + selectedAudio);
                }
                // If this is a key item object, play its label and description
                if (hit.transform.Find("Object Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    Debug.Log("Reader reticle is hitting a reader reference with an object label + description");
                    selectedAudio = hit.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying)
                        selectedAudio.Play();
                    Debug.Log("Now playing from " + selectedAudio);
                }
            }
        }
    }

    public void ReticleTouchingReaderReference(Collider hit)
    {
        Debug.Log("Teleport reticle is being checked");
        AudioSource selectedAudio;

        // If the object being touched by the teleport reticle is in the readerReferences
        if (readerReferences.Contains(hit.gameObject))
        {
            Debug.Log("Teleport reticle is hitting a reader reference");
            // If this is an environment object, play its label to tell the reader the name of the environment their reticle is on
            if (hit.transform.Find("Environment Label").GetComponent<AudioSource>().clip != null)
            {
                selectedAudio = hit.transform.Find("Environment Label").GetComponent<AudioSource>();
                if (!selectedAudio.isPlaying)
                    selectedAudio.Play();
                Debug.Log("Now playing from " + selectedAudio);
            }
        }
    }

    private void PlayReferenceAudioPostTeleport()
    {
        AudioSource selectedAudio;

        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done)
        {
            // Check the location of the player, find the nearest reader reference that is an environment object, play its label
            // If the value of distance attached to the given reference matches the smallestDistance
            float smallestDistance = CheckSmallestReferenceDistance();

            foreach (GameObject reference in referencesAndDistances.Keys)
            {
                // If the value of distance attached to the given reference matches the smallestDistance
                if (referencesAndDistances[reference] == smallestDistance)
                {
                    Debug.Log("Closest environmental object is " + reference.name);
                    // If this is an environment object WITH a label (ex. Floor but not Wall), play its label to tell the reader the name of the environment their reticle is on
                    if (reference.transform.Find("Environment Label").GetComponent<AudioSource>().clip != null)
                    {
                        selectedAudio = reference.transform.Find("Environment Label").GetComponent<AudioSource>();
                        if (!selectedAudio.isPlaying)
                            selectedAudio.Play();
                        Debug.Log("Now playing from " + selectedAudio);
                    }
                }
            }
        }
    }

    void GetReaderReferences()
    {
        // Get all reader reference objects in scene
        GameObject[] tempReaderReferences = GameObject.FindGameObjectsWithTag("Reader Reference");

        foreach(GameObject reference in tempReaderReferences)
        {
            readerReferences.Add(reference);

            // If on any of the layers Interactable, Obstacles, Entrance, Floor or Wall, Person, NPC
            if (reference.layer == 7 || reference.layer == 8 || reference.layer == 9 || reference.layer == 10 || reference.layer == 11 || reference.layer == 12)
            {
                environmentCues.Add(reference);
            }
        }
    }

    private void ShootRaycast(InputDevice controller, GameObject reticle)
    {
        if (controller.isValid)
        {
            // Check if the controller has a valid position and rotation
            if (controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 controllerPosition) &&
                controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion controllerRotation))
            {
                // Define the direction of the raycast based on the controller's rotation
                Vector3 direction = controllerRotation * Vector3.forward;

                // Shoot a ray from the controller's position
                Ray ray = new Ray(controllerPosition, direction);
                RaycastHit hit;

                // Perform the raycast and check if it hits something
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, raycastLayerMask))
                {
                    // Move the reticle to the hit point
                    reticle.transform.position = hit.point;
                    reticle.SetActive(true);
                }
                else
                {
                    // Hide the reticle if the raycast doesn't hit anything
                    reticle.SetActive(false);
                }
            }
        }
    }

    void PlayHapticsNearingObstacles()
    {
        float smallestDistance = CheckSmallestReferenceDistance();

        // If the player is getting too close within a certain range of an object, play warning impulses at various strengths
        if (smallestDistance < 5f && smallestDistance > 2f)
        {
            Debug.Log("Within 5f of potential obstacle");
            rightXRController.SendHapticImpulse(1u, 1f, 0.25f);
            leftXRController.SendHapticImpulse(1u, 1f, 0.25f);
        }
        else if (smallestDistance < 2f && smallestDistance > 0.1f)
        {
            Debug.Log("Within 2f of potential obstacle");
            rightXRController.SendHapticImpulse(1u, 1f, 0.5f);
            leftXRController.SendHapticImpulse(1u, 1f, 0.5f);
        }
        else if (smallestDistance < 0.1f)
        {
            Debug.Log("Within 0.1f of potential obstacle");
            rightXRController.SendHapticImpulse(1u, 1f, 1f);
            leftXRController.SendHapticImpulse(1u, 1f, 1f);
        }
    }

    private float CheckSmallestReferenceDistance()
    {
        Debug.Log("Checking for nearby obstacles");

        float distance;
        List<float> distancesToReferences = new List<float>();
        referencesAndDistances.Clear(); // Reset dict values with each check

        // Calculate the distance between the player and each object in environmentCues
        foreach (GameObject reference in environmentCues)
        {
            distance = Vector3.Distance(reference.transform.position, thePlayer.transform.position);
            distancesToReferences.Add(distance);
            referencesAndDistances.Add(reference, distance);
        }

        return distancesToReferences.Min();
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
