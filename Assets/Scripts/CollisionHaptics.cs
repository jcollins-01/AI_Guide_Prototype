using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class CollisionHaptics : MonoBehaviour
{
    // Variables to hold scripts and Game Objects we need access to
    private VRHandling m_VRHandlingScript;
    public enum HandSide { Left, Right }
    public HandSide handSide;

    // Variables to access XR Controllers
    private InputDevice activeController;
    private bool controllersGrabbed = false;

    // Haptic settings
    private float initialAmplitude = 0.8f;
    private float initialDuration = 0.15f;
    private float continuousAmplitude = 0.15f;
    private float continuousTickRate = 0.1f;

    private int keyItemsLayerMask;
    private Coroutine hapticLoopCoroutine;

    // Using a List instead of an int to track what we are actually touching
    private List<Collider> touchingColliders = new List<Collider>();
    private bool isHapticPlaying = false;

    void Start()
    {
        keyItemsLayerMask = LayerMask.NameToLayer("Key Items");

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (!controllersGrabbed)
            AssignHandling();

        // Clean up our list to remove any objects that were Destroyed or Disabled (like with the table spawner)
        for (int i = touchingColliders.Count - 1; i >= 0; i--)
        {
            if (touchingColliders[i] == null || !touchingColliders[i].gameObject.activeInHierarchy)
            {
                touchingColliders.RemoveAt(i);
            }
        }

        // If the list is empty but haptics are still running, shut them off
        if (touchingColliders.Count == 0 && isHapticPlaying)
        {
            StopHapticSequence();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == keyItemsLayerMask)
        {
            // Only add if it's not already in the list
            if (!touchingColliders.Contains(other))
            {
                touchingColliders.Add(other);
            }

            // If this is the FIRST object we are touching, trigger the sequence
            if (touchingColliders.Count == 1 && controllersGrabbed && !isHapticPlaying)
            {
                if (hapticLoopCoroutine != null)
                    StopCoroutine(hapticLoopCoroutine);

                isHapticPlaying = true;
                hapticLoopCoroutine = StartCoroutine(HapticSequence());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == keyItemsLayerMask)
        {
            touchingColliders.Remove(other);

            // Stop the continuous haptics when we are no longer touching ANY key items
            if (touchingColliders.Count == 0 && isHapticPlaying)
            {
                StopHapticSequence();
            }
        }
    }

    // Extracted the stop logic into a helper method so it can be called safely from Update or OnTriggerExit
    private void StopHapticSequence()
    {
        isHapticPlaying = false;

        if (hapticLoopCoroutine != null)
        {
            StopCoroutine(hapticLoopCoroutine);
            hapticLoopCoroutine = null;
        }

        if (controllersGrabbed)
        {
            activeController.StopHaptics();
        }
    }

    private IEnumerator HapticSequence()
    {
        // Play the harsh initial haptic
        activeController.SendHapticImpulse(0u, initialAmplitude, initialDuration);
        yield return new WaitForSeconds(initialDuration);

        // Loop the dimmer, continuous haptic
        while (true)
        {
            if (controllersGrabbed)
            {
                activeController.SendHapticImpulse(0u, continuousAmplitude, continuousTickRate + 0.05f);
            }
            yield return new WaitForSeconds(continuousTickRate);
        }
    }

    void AssignHandling()
    {
        m_VRHandlingScript = FindObjectOfType<VRHandling>();

        if (m_VRHandlingScript != null)
        {
            if (m_VRHandlingScript.rightControllerGrabbed && m_VRHandlingScript.leftControllerGrabbed)
            {
                if (handSide == HandSide.Right)
                    activeController = m_VRHandlingScript.rightXRController;
                else
                    activeController = m_VRHandlingScript.leftXRController;

                controllersGrabbed = true;
            }
        }
    }
}

/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class CollisionHaptics : MonoBehaviour
{
    // Variables to hold scripts and Game Objects we need access to
    private VRHandling m_VRHandlingScript;
    public enum HandSide { Left, Right }
    public HandSide handSide;

    // Variables to access XR Controllers
    private InputDevice activeController;
    private bool controllersGrabbed = false;

    // Haptic settings
    private float initialAmplitude = 0.8f;
    private float initialDuration = 0.15f;
    private float continuousAmplitude = 0.15f;
    private float continuousTickRate = 0.1f;

    private int keyItemsLayerMask;
    private Coroutine hapticLoopCoroutine;
    private int objectsCurrentlyTouching = 0;

    // Start is called before the first frame update
    void Start()
    {
        // Cache the layer index for performance
        keyItemsLayerMask = LayerMask.NameToLayer("Key Items");

        // Ensure the Rigidbody is kinematic so the hand doesn't get pushed by physics
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (controllersGrabbed == false)
            AssignHandling();
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (other.gameObject.layer == keyItemsLayerMask)
        {
            //Debug.Log($"Should have collided with object {other.gameObject.name}");
            objectsCurrentlyTouching++;

            // If this is the FIRST object we are touching, trigger the sequence
            if (objectsCurrentlyTouching == 1 && controllersGrabbed)
            {
                if (hapticLoopCoroutine != null) 
                    StopCoroutine(hapticLoopCoroutine);

                hapticLoopCoroutine = StartCoroutine(HapticSequence());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == keyItemsLayerMask)
        {
            //Debug.Log($"exited object {other.gameObject.name}");
            objectsCurrentlyTouching--;

            // Prevent negative counts just in case of physics glitches
            if (objectsCurrentlyTouching <= 0)
            {
                objectsCurrentlyTouching = 0;

                // Stop the continuous haptics when we are no longer touching ANY key items
                if (hapticLoopCoroutine != null)
                {
                    StopCoroutine(hapticLoopCoroutine);
                    hapticLoopCoroutine = null;
                }

                // Force the motor to shut off immediately to prevent hanging vibrations
                if (controllersGrabbed)
                {
                    activeController.StopHaptics();
                }
            }
        }
    }

    private IEnumerator HapticSequence()
    {
        // Play the harsh initial haptic
        activeController.SendHapticImpulse(0u, initialAmplitude, initialDuration);

        // Wait for the initial pulse to finish
        yield return new WaitForSeconds(initialDuration);

        // Loop the dimmer, continuous haptic
        while (true)
        {
            if (controllersGrabbed)
            {
                // Make the duration slightly longer than the wait time
                // This forces the new motor command to overwrite the old one BEFORE it finishes, creating an uninterrupted hum
                activeController.SendHapticImpulse(0u, continuousAmplitude, continuousTickRate + 0.05f);
            }

            yield return new WaitForSeconds(continuousTickRate);
        }
    }

    void AssignHandling()
    {
        m_VRHandlingScript = FindObjectOfType<VRHandling>();

        // If we have the VR Handling script, and both controllers have been grabbed
        if (m_VRHandlingScript != null)
        {
            if (m_VRHandlingScript.rightControllerGrabbed && m_VRHandlingScript.leftControllerGrabbed)
            {
                // Assign the active controller based on the Inspector selection
                if (handSide == HandSide.Right)
                {
                    activeController = m_VRHandlingScript.rightXRController;
                    //if (activeController != null)
                        //Debug.Log("Active on right controller");
                }
                    
                else
                {
                    activeController = m_VRHandlingScript.leftXRController;
                    //if (activeController != null)
                        //Debug.Log("Active on left controller");
                }

                controllersGrabbed = true;
            }
        }
    }
}*/
