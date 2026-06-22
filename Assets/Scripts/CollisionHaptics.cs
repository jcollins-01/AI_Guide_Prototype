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
            Debug.Log($"Should have collided with object {other.gameObject.name}");
            objectsCurrentlyTouching++;

            // If this is the FIRST object we are touching, trigger the sequence
            if (objectsCurrentlyTouching == 1 && controllersGrabbed)
            {
                // Play the harsh initial haptic
                activeController.SendHapticImpulse(0u, initialAmplitude, initialDuration);

                // Start the dimmer, continuous loop
                if (hapticLoopCoroutine != null) StopCoroutine(hapticLoopCoroutine);
                hapticLoopCoroutine = StartCoroutine(ContinuousHapticLoop());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == keyItemsLayerMask)
        {
            Debug.Log($"exited object {other.gameObject.name}");
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
            }
        }
    }

    private IEnumerator ContinuousHapticLoop()
    {
        // Wait out the initial harsh pulse so they don't overlap and cancel out
        yield return new WaitForSeconds(initialDuration);

        while (true)
        {
            if (controllersGrabbed)
            {
                // Send a smaller, continuous pulse
                activeController.SendHapticImpulse(0u, continuousAmplitude, continuousTickRate);
            }

            // Wait exactly the length of the pulse before sending the next one
            // This creates a smooth, continuous vibration
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
                    if (activeController != null)
                        Debug.Log("Active on right controller");
                }
                    
                else
                {
                    
                    activeController = m_VRHandlingScript.leftXRController;
                    if (activeController != null)
                        Debug.Log("Active on left controller");
                }
                    

                controllersGrabbed = true;
            }
        }
    }
}
