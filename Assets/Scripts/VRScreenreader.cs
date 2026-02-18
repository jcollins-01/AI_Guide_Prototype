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

    // Variables to access XR Controllers
    private InputDevice rightXRController;
    private InputDevice leftXRController;
    private bool controllersGrabbed = false;

    // Variables to hold reader references we need globally
    public List<GameObject> readerReferences = new List<GameObject>();
    List<GameObject> resizedReaderReferences = new List<GameObject>();
    Dictionary<GameObject, float> referencesAndDistances = new Dictionary<GameObject, float>();
    Dictionary<GameObject, bool> objectsHitByLeftController = new Dictionary<GameObject, bool>();
    Dictionary<GameObject, bool> objectsHitByRightController = new Dictionary<GameObject, bool>();
    
    // Variables for reader reticles, teleport reticles, and raycast
    public GameObject leftParentController;
    public GameObject rightParentController;
    GameObject leftReaderReticle;
    GameObject rightReaderReticle;
    GameObject thePlayer;
    Material glowMaterial;
    public GameObject lastHitObject = null; // Tracks the last hit object for teleport reticle
    public Vector3 handlerReticlePosition; // Holds the value of teleport reticles created in TeleportationHandler

    // Monitoring bools
    public bool sharedMovementFound = false;
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
        }
    }

    private void ReaderCheckReferenceAndPlayAudio(GameObject readerReference, InputDevice controller)
    {
        AudioSource selectedAudio;

        // Determine which dictionary to use based on the controller
        Dictionary<GameObject, bool> objectsHitByCurrentController =
            (controller == leftXRController) ? objectsHitByLeftController : objectsHitByRightController;

        if (readerReferences.Contains(readerReference))
        {
            GameObject hit = readerReference.transform.parent.gameObject;

            if (hit.layer == 13 || hit.layer == 7) // If in Key Items or Interactables layer
            {
                // If the dict doesn't already have this hit, or if it does have the hit, but the hit is not marked as true
                if (!objectsHitByCurrentController.ContainsKey(hit) || objectsHitByCurrentController[hit] != true)
                {
                    // Update dict to show current object is being hit by ray
                    if (!objectsHitByCurrentController.ContainsKey(hit))
                        objectsHitByCurrentController.Add(hit, true);
                    else
                        objectsHitByCurrentController[hit] = true;

                    PlayHapticImpulse(controller); // Play a short haptic impulse to signal to user that they're hitting an object
                }

                // If right or left secondary buttons are pressed (left secondary is muting button for the guide)
                if (m_VRHandlingScript.isButtonPressed || m_VRHandlingScript.isMutingButtonPressed)
                {
                    // Play audio label if the button is pressed
                    selectedAudio = readerReference.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                    if (!selectedAudio.isPlaying && selectedAudio.clip != null) // If the source isn't playing and the clip is assigned
                    {
                        selectedAudio.Play();
                        HighlightSelectedReaderReference(hit, selectedAudio);
                        Debug.Log("Now playing from " + selectedAudio.transform.parent.transform.parent.name);
                    }
                    else
                        Debug.Log("Object found with reader reference but no assigned audio clip");
                }
            }

            // Mark all objects not hit by the ray as false so that they can trigger buzzes later
            foreach (GameObject obj in objectsHitByCurrentController.Keys.ToList())
            {
                if (obj != hit)
                    objectsHitByCurrentController[obj] = false;
            }
        }
        else // We are not hitting a reader reference
        {
            // Mark all objects not hit by the ray as false so that they can trigger buzzes later
            foreach (GameObject obj in objectsHitByCurrentController.Keys.ToList())
                objectsHitByCurrentController[obj] = false;
        }
    }

    private void PlayHapticImpulse(InputDevice controller)
    {
        controller.SendHapticImpulse(1u, 0.25f, 0.25f);
    }

    // This is a function used so that the TeleportationHandler can play the audio of a teleportable area when the teleport reticle hits it
    // Teleport reticle is separate from the reader reticle
    public void TeleportCheckReferenceAndPlayAudio(Vector3 reticlePosition) // was GameObject hit
    {
        //Debug.Log("Teleport reticle is being checked with " + hit.name);
        AudioSource selectedAudio;

        // Assign handlerReticlePosition to the value passed from TeleportationHandler, to be used in CheckSmallestReferenceDistance
        handlerReticlePosition = reticlePosition;

        // Set the value of the distance passed to share here
        float smallestDistance = CheckSmallestReferenceDistance("pre-teleport");

        foreach (GameObject reference in referencesAndDistances.Keys)
        {
            // If the value of distance attached to the given reference matches the smallestDistance
            if (referencesAndDistances[reference] == smallestDistance)
            {
                // This is the closest environment object, so play its label to tell the reader the name of the environment their reticle is on
                selectedAudio = reference.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                // Assign the environmental object as the hit to compare between teleports, since this object contains the appropriate name to compare
                GameObject hit = reference.transform.parent.gameObject;

                if (selectedAudio.clip != null)
                {
                    // Check if the current object is the same as the last hit object
                    if (lastHitObject != hit)
                    {
                        if (!selectedAudio.isPlaying)
                        {
                            selectedAudio.Play(); // Play the sound automatically as it is hit
                            HighlightSelectedReaderReference(hit, selectedAudio);
                            Debug.Log("Now playing from " + selectedAudio.transform.parent.transform.parent.name);
                        }

                        // Update last object hit
                        lastHitObject = hit;
                    }
                }
            }
        }
    }

    // This function is called from TeleportationHandler which most accurately detects when a teleport move has been completed
    public void PlayReferenceAudioPostTeleport()
    {
        //Debug.Log("Checking for post teleport audio labels");
        AudioSource selectedAudio;

        // Check the location of the player, find the nearest reader reference that is an environment object, play its label
        // If the value of distance attached to the given reference matches the smallestDistance
        float smallestDistance = CheckSmallestReferenceDistance("teleport");
        //Debug.Log("After checking in post teleport, smallestDistance is " + smallestDistance);

        foreach (GameObject reference in referencesAndDistances.Keys)
        {
            // If the value of distance attached to the given reference matches the smallestDistance
            if (referencesAndDistances[reference] == smallestDistance)
            {
                //Debug.Log("Closest environmental object is " + reference.transform.parent.name);
                // Play the label of the closest floor / environmental object
                selectedAudio = reference.transform.Find("Object Label + Description").GetComponent<AudioSource>();
                if (selectedAudio.isPlaying)
                    selectedAudio.Stop(); // Stops playing if we were just using the reticle to hear this area's name
                selectedAudio.Play();
                Debug.Log("Now playing post audio from " + selectedAudio.transform.parent.transform.parent.name);
            }
        }
    }

    public void GetReaderReferences()
    {
        Debug.Log("Getting reader references");

        // Clear larger readerReferences dictionary each time this is called, so our dictionary is fresh + won't call for destroyed items
        readerReferences.Clear();

        // Get all reader reference objects in scene
        GameObject[] tempReaderReferences = GameObject.FindGameObjectsWithTag("Reader Reference");

        foreach(GameObject reference in tempReaderReferences)
        {
            readerReferences.Add(reference);
            // Debug.Log("Found reference " + reference.transform.parent.name);
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
            // If our references tracker doesn't contain the reference yet (hasn't had a collider resized and added to it yet)
            if (!resizedReaderReferences.Contains(reference))
            {
                // Get the parent of the readerReference
                Transform parentTransform = reference.transform.parent;
                if (parentTransform != null)
                {
                    // Get the Collider of the parent to find the shape
                    Collider parentCollider = parentTransform.GetComponent<Collider>();
                    if (parentCollider != null)
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

                        // Remove the mesh filters and renderers from the reticle since we only need its colliders now
                        Destroy(reference.GetComponent<MeshFilter>());
                        Destroy(reference.GetComponent<MeshRenderer>());

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

                        // Add to our reference tracker so we can ensure we don't add multiple colliders as we check and resize with new objects
                        resizedReaderReferences.Add(reference);
                    }
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
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) 
            {
                //Debug.Log("Performing ray cast and hit something");
                // Move the reticle to the hit point
                reticle.transform.position = hit.point;
                reticle.SetActive(true);

                GameObject readerReference = hit.transform.gameObject; // Default assignment

                if (hit.transform.Find("Reader Reference(Clone)")) // If we find a Reader Ref child, switch assignment
                    readerReference = hit.transform.Find("Reader Reference(Clone)").gameObject;

                if (hit.transform.gameObject.GetComponentInChildren<XRGrabInteractable>())
                {
                    XRGrabInteractable grab = hit.transform.gameObject.GetComponentInChildren<XRGrabInteractable>();
                    // If the object is not being actively held by the user while reticle is touching it
                    if (!grab.isSelected)
                        ReaderCheckReferenceAndPlayAudio(readerReference, controller);
                }
                else
                    ReaderCheckReferenceAndPlayAudio(readerReference, controller);
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
        // Store original materials for each renderer
        Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();

        // Get all renderers in the selected object and its children
        Renderer[] renderers = selectedReference.GetComponentsInChildren<Renderer>();

        // Apply the glow material to all renderers and save their original materials
        foreach (Renderer renderer in renderers)
        {
            originalMaterials[renderer] = renderer.material;
            renderer.material = glowMaterial;
        }

        StartCoroutine(WaitForAudioToEnd(renderers, selectedAudio, originalMaterials));
    }

    IEnumerator WaitForAudioToEnd(Renderer[] renderers, AudioSource selectedAudio, Dictionary<Renderer, Material> originalMaterials)
    {
        // Wait until the audio finishes playing
        yield return new WaitWhile(() => selectedAudio.isPlaying);

        // Restore the original materials for all affected renderers
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && originalMaterials.ContainsKey(renderer))
            {
                renderer.material = originalMaterials[renderer];
            }
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
                if (reference.transform.parent.gameObject.layer == 10 && reference.transform.Find("Object Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    distance = Vector3.Distance(reference.transform.position, thePlayer.transform.position);
                    distancesToReferences.Add(distance);
                    referencesAndDistances.Add(reference, distance);
                }
            }
        }
        else if (version.Equals("pre-teleport"))
        {
            Debug.Log("Reached pre-teleport");
            // Calculate the distance between the player and each object in environmentCues
            foreach (GameObject reference in readerReferences)
            {
                // If the reference has an environment label (is part of the floor spaces on layer 10) and its clip is assigned
                if (reference.transform.parent.gameObject.layer == 10 && reference.transform.Find("Object Label + Description").GetComponent<AudioSource>().clip != null)
                {
                    distance = Vector3.Distance(reference.transform.position, handlerReticlePosition);
                    distancesToReferences.Add(distance);
                    referencesAndDistances.Add(reference, distance);
                }
            }
        }

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
