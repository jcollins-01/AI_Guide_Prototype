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
    private GenerateReaderReferences m_GenerateReaderReferencesScript;

    // Components to grab from scripts
    private TeleportationProvider teleport;

    // Variables to access XR Controllers
    private InputDevice rightXRController;
    private InputDevice leftXRController;
    private bool controllersGrabbed = false;

    // Variables to hold reader references we need globally
    List<GameObject> readerReferences = new List<GameObject>();
    Dictionary<GameObject, float> referencesAndDistances = new Dictionary<GameObject, float>();
    Dictionary<GameObject, bool> objectsHitByRays = new Dictionary<GameObject, bool>();

    // Variables for reader reticles and raycast
    public GameObject leftParentController;
    public GameObject rightParentController;
    GameObject leftReaderReticle;
    GameObject rightReaderReticle;
    GameObject thePlayer;
    Material glowMaterial;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool referencesReady = false;
    private bool referencesFound = false;

    // Start is called before the first frame update
    void Start()
    {
        m_VRHandlingScript = gameObject.AddComponent<VRHandling>();
        m_GenerateReaderReferencesScript = gameObject.AddComponent<GenerateReaderReferences>();

        // Load and instantiate the reader reticles into the scene
        leftReaderReticle = Resources.Load<GameObject>("Screenreader/Left Reader Reticle");
        rightReaderReticle = Resources.Load<GameObject>("Screenreader/Right Reader Reticle");
        leftReaderReticle = Instantiate(leftReaderReticle);
        rightReaderReticle = Instantiate(rightReaderReticle);
        glowMaterial = Resources.Load<Material>("Screenreader/Glow");

        // Ensure the reticles are active and initially hidden
        leftReaderReticle.SetActive(false);
        rightReaderReticle.SetActive(false);

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

        // Check if all initial reader references have been generated and assigned audio
        if (!referencesReady)
            CheckIfReaderReferencesReady();

        // Activate audio screenreader functions
        if (controllersGrabbed && referencesFound)
        {
            // Perform raycast for left and right controllers
            ShootRaycast(leftXRController, leftReaderReticle, leftParentController);
            ShootRaycast(rightXRController, rightReaderReticle, rightParentController);

            if (teleport && sharedMovementFound)
                PlayReferenceAudioPostTeleport();
        }

        // Activate haptic screenreader functions - not in use
        //if (sharedMovementFound && referencesFound)
            //PlayHapticsNearingObstacles();
    }

    private void ReaderCheckReferenceAndPlayAudio(GameObject readerReference)
    {
        GameObject hit = readerReference.transform.parent.gameObject;
        Debug.Log("Reader reticle is hitting " + hit.name);
        AudioSource selectedAudio;

        if (readerReferences.Contains(readerReference))
        {
            if (hit.layer == 13 || hit.layer == 7) // If in Key Items or Interactables layer
            {
                // If the dict doesn't already have this hit, or if it does have the hit, but the hit is not marked as true
                if (!objectsHitByRays.ContainsKey(hit) || objectsHitByRays[hit] != true)
                {
                    // Update dict to show current object is being hit by ray
                    if (!objectsHitByRays.ContainsKey(hit))
                        objectsHitByRays.Add(hit, true);
                    else
                        objectsHitByRays[hit] = true;

                    PlayHapticImpulse(); // Play a short haptic impulse to signal to user that they're hitting an object
                    Debug.Log("Buzz played on item: " + hit.name);
                    Debug.Log("Value for object in the dict is " + objectsHitByRays[hit].ToString());
                    Debug.Log("Dict size is " + objectsHitByRays.Count);
                }

                if (m_VRHandlingScript.isButtonPressed)
                {
                    Debug.Log("Waiting for button press to read");
                    // Play audio label if the button is pressed
                    selectedAudio = readerReference.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying && selectedAudio.clip != null) // If the source isn't playing and the clip is assigned
                    {
                        selectedAudio.Play();
                        HighlightSelectedReaderReference(hit, selectedAudio);
                        Debug.Log("Now playing from " + selectedAudio.transform.parent.transform.parent.name);
                    }
                }
            }
            else if (hit.layer == 10) // If in Floors layer // Might want to remove this part, only play sound if trying to teleport
            {
                // Play audio label automatically as it is hit
                selectedAudio = readerReference.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                if (!selectedAudio.isPlaying && selectedAudio.clip != null) // If the source isn't playing and the clip is assigned
                {
                    selectedAudio.Play(); 
                    HighlightSelectedReaderReference(hit, selectedAudio);
                    Debug.Log("Now playing from " + selectedAudio.transform.parent.transform.parent.name);
                }
            }

            // Mark all objects not hit by the ray as false so that they can trigger buzzes later
            foreach (GameObject obj in objectsHitByRays.Keys.ToList())
            {
                if (obj != hit)
                    objectsHitByRays[obj] = false;
            }
        }
    }

    private void PlayHapticImpulse()
    {
        rightXRController.SendHapticImpulse(1u, 0.25f, 0.25f);
        leftXRController.SendHapticImpulse(1u, 0.25f, 0.25f);
    }

    // This is a function used so that the TeleportationHandler can play the audio of a teleportable area when the teleport reticle hits it
    // Teleport reticle is separate from the reader reticle
    public void TeleportCheckReferenceAndPlayAudio(GameObject hit)
    {
        //Debug.Log("Teleport reticle is being checked with " + hit.name);
        AudioSource selectedAudio;

        // If the object being touched by the teleport reticle is in the readerReferences AND is a Wall and Floor layer item
        if (readerReferences.Contains(hit) && hit.layer == 10)
        {
            // This is an environment object, so play its label to tell the reader the name of the environment their reticle is on
            selectedAudio = hit.transform.Find("Object Label + Description").GetComponent<AudioSource>();
            
            if (selectedAudio.clip != null)
            {
                //Debug.Log("Teleport reticle is hitting a reader reference with an environment label + description on layer " + hit.layer);
                if (!selectedAudio.isPlaying)
                {
                    selectedAudio.Play(); // Play the sound automatically as it is hit
                    HighlightSelectedReaderReference(hit.transform.parent.gameObject, selectedAudio);
                    Debug.Log("Now playing from " + selectedAudio.transform.parent.transform.parent.name);
                }
            }
        }
    }

    private void PlayReferenceAudioPostTeleport()
    {
        //Debug.Log("Checking for post teleport audio labels");
        AudioSource selectedAudio;

        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done)
        {
            // Check the location of the player, find the nearest reader reference that is an environment object, play its label
            // If the value of distance attached to the given reference matches the smallestDistance
            float smallestDistance = CheckSmallestReferenceDistance("teleport");
            Debug.Log("After checking in post teleport, smallestDistance is " + smallestDistance);

            foreach (GameObject reference in referencesAndDistances.Keys)
            {
                // If the value of distance attached to the given reference matches the smallestDistance
                if (referencesAndDistances[reference] == smallestDistance)
                {
                    Debug.Log("Closest environmental object is " + reference.transform.parent.name);
                    // Play the label of the closest floor / environmental object
                    selectedAudio = reference.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying)
                        selectedAudio.Play();
                    Debug.Log("Now playing from " + selectedAudio.transform.parent.transform.parent.name);
                }
            }
        }
    }

    private void GetReaderReferences()
    {
        Debug.Log("Getting reader references");
        // Get all reader reference objects in scene
        GameObject[] tempReaderReferences = GameObject.FindGameObjectsWithTag("Reader Reference");

        foreach(GameObject reference in tempReaderReferences)
        {
            readerReferences.Add(reference);
            Debug.Log("Found reference " + reference.transform.parent.name);
        }

        Debug.Log("Reader refs size is " + readerReferences.Count);
        if (readerReferences.Count > 0)
        {
            referencesFound = true;
            ResizeReaderReferences();
        }
    }

    private void ResizeReaderReferences()
    {
        foreach(GameObject reference in readerReferences)
        {
            // Get the parent of the readerReference
            Transform parentTransform = reference.transform.parent;
            if (parentTransform != null)
            {
                // Get the MeshFilter of the parent to find the shape
                MeshFilter parentMeshFilter = parentTransform.GetComponent<MeshFilter>();
                Collider parentCollider = parentTransform.GetComponent<Collider>();
                if (parentMeshFilter != null && parentCollider != null)
                {
                    // Temporarily detach from parent to apply world scale correctly
                    Transform originalParent = reference.transform.parent;
                    reference.transform.SetParent(null); // Detach to set global scale

                    // Set position, rotation, and scale directly from the parent
                    reference.transform.position = parentTransform.position;
                    reference.transform.rotation = parentTransform.rotation;
                    reference.transform.localScale = parentTransform.localScale;

                    // Match the collider dimensions and type
                    Collider referenceCollider = reference.GetComponent<Collider>();

                    if (referenceCollider != null)
                        Destroy(referenceCollider); // Remove the current collider since it may not be the same type as the parent

                    // Add the same type of collider as the parent collider and make it slightly larger
                    if (parentCollider is BoxCollider parentBoxCollider)
                    {
                        BoxCollider newBoxCollider = reference.gameObject.AddComponent<BoxCollider>();
                        newBoxCollider.center = parentBoxCollider.center;
                        newBoxCollider.size = parentBoxCollider.size * 1.05f; // Increase size by 5%
                    }
                    else if (parentCollider is SphereCollider parentSphereCollider)
                    {
                        SphereCollider newSphereCollider = reference.gameObject.AddComponent<SphereCollider>();
                        newSphereCollider.center = parentSphereCollider.center;
                        newSphereCollider.radius = parentSphereCollider.radius * 1.05f; // Increase radius by 5%
                    }
                    else if (parentCollider is CapsuleCollider parentCapsuleCollider)
                    {
                        CapsuleCollider newCapsuleCollider = reference.gameObject.AddComponent<CapsuleCollider>();
                        newCapsuleCollider.center = parentCapsuleCollider.center;
                        newCapsuleCollider.radius = parentCapsuleCollider.radius * 1.05f; // Increase radius by 5%
                        newCapsuleCollider.height = parentCapsuleCollider.height * 1.05f; // Increase height by 5%
                        newCapsuleCollider.direction = parentCapsuleCollider.direction;
                    }
                    else if (parentCollider is MeshCollider parentMeshCollider)
                    {
                        MeshCollider newMeshCollider = reference.gameObject.AddComponent<MeshCollider>();
                        newMeshCollider.sharedMesh = parentMeshCollider.sharedMesh;
                        newMeshCollider.convex = parentMeshCollider.convex;
                        // Cannot uniformly "enlarge" a MeshCollider easily
                    }

                    // Reattach to the original parent
                    reference.transform.SetParent(originalParent);

                    // If readerReference also has a MeshFilter, replace its mesh with the parent's
                    MeshFilter referenceMeshFilter = reference.GetComponent<MeshFilter>();
                    if (referenceMeshFilter != null)
                        referenceMeshFilter.sharedMesh = parentMeshFilter.sharedMesh;
                }
            }
        }
    }

    private void ShootRaycast(InputDevice controller, GameObject reticle, GameObject parentController)
    {
        // Check if the controller has a valid position and rotation
        if (controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 controllerPosition) && controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion controllerRotation))
        {
            // Define the direction of the raycast based on the controller's rotation
            Vector3 direction = controllerRotation * Vector3.forward;

            // Find the actual handheld controllers and match the world position to that transform
            controllerPosition = parentController.transform.position;

            // Ensure the ray's direction accounts for player rotation (snap turning)
            if (sharedMovementFound)
            {
                Quaternion adjustedRotation = thePlayer.transform.rotation * controllerRotation;
                direction = adjustedRotation * Vector3.forward;
            }

            // Shoot a ray from the controller's position
            Ray ray = new Ray(controllerPosition, direction);
            RaycastHit hit;

            // Draw a ray in editor for debug
            Debug.DrawRay(controllerPosition, direction * 10f, Color.green);

            // Perform the raycast and check if it hits something
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.AllLayers))
            {
                //Debug.Log("Performing ray cast and hit something");
                // Move the reticle to the hit point
                reticle.transform.position = hit.point;
                reticle.SetActive(true);
                if (hit.transform.gameObject.GetComponentInChildren<XRGrabInteractable>())
                    ReaderCheckReferenceAndPlayAudio(hit.transform.GetChild(0).gameObject);
                else
                    ReaderCheckReferenceAndPlayAudio(hit.transform.gameObject);
            }
            else
            {
                // Hide the reticle if the raycast doesn't hit anything
                //Debug.Log("Performing ray cast but it didn't hit anything");
                reticle.SetActive(false);
            }
        }
    }

    void HighlightSelectedReaderReference(GameObject selectedReference, AudioSource selectedAudio)
    {
        Material previousMaterial = selectedReference.GetComponent<Renderer>().material;

        // Add a glow around the selectedReference + brighten its color
        selectedReference.GetComponent<Renderer>().material = glowMaterial;

        // Return selectedReference renderers to normal after coroutine finishes
        StartCoroutine(WaitForAudioToEnd(selectedReference, selectedAudio, previousMaterial));
    }

    IEnumerator WaitForAudioToEnd(GameObject selectedReference, AudioSource selectedAudio, Material previousMaterial)
    {
        // Wait until the audio finishes
        yield return new WaitWhile(() => selectedAudio.isPlaying);

        // Restore the original material
        selectedReference.GetComponent<Renderer>().material = previousMaterial;
    }

    /**
     * DEPRECATED: Not in use after switching to Owlchemy Labs baseline
     * Could possibly be re-implemented in future versions of improved prototypes
     */
    void PlayHapticsNearingObstacles()
    {
        float smallestDistance = CheckSmallestReferenceDistance("haptics");

        // If the player is getting too close within a certain range of an object, play warning impulses at various strengths
        if (smallestDistance < 3f && smallestDistance > 2.5f)
        {
            Debug.Log("Within 3f of potential obstacle - approaching");
            rightXRController.SendHapticImpulse(1u, 0.25f, 0.25f);
            leftXRController.SendHapticImpulse(1u, 0.25f, 0.25f);
        }
        else if (smallestDistance < 2.5f && smallestDistance > 1.7f)
        {
            Debug.Log("Within 2.5f of potential obstacle - even closer");
            rightXRController.SendHapticImpulse(1u, 0.5f, 0.25f);
            leftXRController.SendHapticImpulse(1u, 0.5f, 0.25f);
        }
        else if (smallestDistance < 1.7f) // This one gets rid of haptics since it could be bothersome while standing beside an object
        {
            Debug.Log("Within 1.7f of potential obstacle - right next to");
            rightXRController.StopHaptics();
            leftXRController.StopHaptics();
        }
    }

    private float CheckSmallestReferenceDistance(string version)
    {
        float distance;
        List<float> distancesToReferences = new List<float>();
        referencesAndDistances.Clear(); // Reset dict values with each check

        if (version.Equals("teleport"))
        {
            // Calculate the distance between the player and each object in environmentCues
            foreach (GameObject reference in readerReferences)
            {
                // If the reference has an environment label (is part of the floor spaces on layer 10) and its clip is assigned
                if (reference.layer == 10 && reference.transform.Find("Object Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    distance = Vector3.Distance(reference.transform.position, thePlayer.transform.position);
                    distancesToReferences.Add(distance);
                    referencesAndDistances.Add(reference, distance);
                }
            }
        }
        /*else // Haptics version - no longer in use
        {
            // Calculate the distance between the player and each object in environmentCues
            foreach (GameObject reference in environmentCues)
            {
                // If the reference has a null environment label (is NOT part of the floor spaces)
                if (reference.transform.Find("Environment Label").GetComponent<AudioSource>().clip == null)
                {
                    distance = Vector3.Distance(reference.transform.parent.gameObject.transform.position, thePlayer.transform.position);
                    distancesToReferences.Add(distance);
                    referencesAndDistances.Add(reference, distance);
                }
            }
        }*/

        //Debug.Log("Checking for nearby obstacles - smallest distance is " + distancesToReferences.Min());
        return distancesToReferences.Min();
    }

    private void CheckIfReaderReferencesReady()
    {
        if (m_GenerateReaderReferencesScript != null)
        {
            if (m_GenerateReaderReferencesScript.audioAssigned == true)
            {
                referencesReady = true;
                GetReaderReferences();
            }
        }
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
