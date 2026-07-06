using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class VisibleGrabRequest : MonoBehaviour
{
    private RealtimeTransform realtimeTransform;
    private XRGrabInteractable xrGrabInteractable;

    private InputDevice targetDevice;
    private InputDevice targetDevice2;
    private bool gripping1 = false;
    private bool gripping2 = false;

    private int grabSoundCount = 0;
    public AudioSource playerAudio;
    public AudioClip grabSound;

    // Start is called before the first frame update
    void Start()
    {
        realtimeTransform = GetComponent<RealtimeTransform>();
        xrGrabInteractable = GetComponent<XRGrabInteractable>();

        List<InputDevice> devices = new List<InputDevice>();

        InputDeviceCharacteristics rightController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Right;
        InputDevices.GetDevicesWithCharacteristics(rightController, devices);

        InputDeviceCharacteristics leftController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Left;
        InputDevices.GetDevicesWithCharacteristics(leftController, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[2]; //attached to right controller, was 2
            targetDevice2 = devices[1]; // attached to left controller, was 1
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (xrGrabInteractable.isSelected)
        {
            realtimeTransform.RequestOwnership();
            playerAudio.clip = grabSound;
            if (grabSoundCount == 0)
            {
                playerAudio.Play();
                grabSoundCount += 1;
            }

            // Ignore collisions between Default objects (layer 0), XRRig (layer 8), Players (layer 9), Teleport Area (layer 6)
            // and Non-Teleport Obstacles (layer 7)
            Physics.IgnoreLayerCollision(10, 6, true);
            Physics.IgnoreLayerCollision(10, 7, true);
            Physics.IgnoreLayerCollision(10, 8, true);
            Physics.IgnoreLayerCollision(10, 9, true);
        }
        else
        {
            grabSoundCount = 0;
        }

        if (targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            if (gripValue < 0.1f)
                gripping1 = false;
            else // We are gripping with this controller
                gripping1 = true;
        }

        if (targetDevice2.TryGetFeatureValue(CommonUsages.grip, out float gripValue2))
        {
            if (gripValue2 < 0.1f)
                gripping2 = false;
            else // We are gripping with this controller
                gripping2 = true;
        }

        if (gripping1 == false && gripping2 == false) // If neither controller is gripping a grabbable
        {
            //Debug.Log("Neither controller is gripping grabbable - collisions back on.");
            Physics.IgnoreLayerCollision(10, 6, false);
            Physics.IgnoreLayerCollision(10, 7, false);
            Physics.IgnoreLayerCollision(10, 8, false);
            Physics.IgnoreLayerCollision(10, 9, false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collided with " + collision.collider);
    }
}
