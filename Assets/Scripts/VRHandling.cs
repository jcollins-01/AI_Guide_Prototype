using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class VRHandling : MonoBehaviour
{
    // Variables for assigning XR input
    public bool rightControllerGrabbed = false;
    public bool leftControllerGrabbed = false;
    [HideInInspector]
    public InputDevice rightXRController;
    [HideInInspector]
    public InputDevice leftXRController;

    // Variables for detecting button input
    public bool isButtonPressed = false;
    public bool isMutingButtonPressed = false;
    private bool wasButtonPressedLastFrame = false;

    // Update is called once per frame
    void Update()
    {
        // Calls until two controllers are assigned
        getControllers();

        // Check if the primary button is being pressed down
        if (GetPrimaryButtonPress())
            isButtonPressed = true;

        // Check if the primary button is released
        if (!GetPrimaryButtonPress() && isButtonPressed)
            isButtonPressed = false;

        // Check if the left primary button (for muting the guide) is being pressed down
        if (GetMutingButtonPress())
            isMutingButtonPressed = true;

        // Check if the left primary button is released (to cancel muting the guide)
        if (!GetMutingButtonPress() && isMutingButtonPressed)
            isMutingButtonPressed = false;

        // Remember the button state for the next frame
        wasButtonPressedLastFrame = isButtonPressed;
    }

    // Function to get the state of the primary button on the XR controller
    private bool GetPrimaryButtonPress()
    {
        if (rightXRController != null)
        {
            bool primaryButtonValue;
            if (rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonValue))
            {
                return primaryButtonValue;
            }
        }
        return false;
    }

    // Function to get the state of the primary button on the XR controller
    private bool GetMutingButtonPress()
    {
        if (leftXRController != null)
        {
            bool primaryButtonValue;
            if (leftXRController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonValue))
            {
                return primaryButtonValue;
            }
        }
        return false;
    }

    public void getControllers()
    {
        if (!rightControllerGrabbed || !leftControllerGrabbed)
        {
            // Makes a list for input devices + fills it with devices that match the characteristics we give in the Unity editor
            // Narrows devices list using characteristics to just the controller we want to use
            List<InputDevice> devices = new List<InputDevice>();

            InputDeviceCharacteristics rightController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Right;
            InputDevices.GetDevicesWithCharacteristics(rightController, devices);

            InputDeviceCharacteristics leftController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Left;
            InputDevices.GetDevicesWithCharacteristics(leftController, devices);

            // Debug.Log("Found devices " + devices);

            // If we have more than an XR headset connected, search for controllers
            if (devices.Count > 1)
            {
                if (!rightControllerGrabbed)
                    rightXRController = devices[2]; //attached to right controller
                if (!leftControllerGrabbed)
                    leftXRController = devices[1]; // attached to left controller

                if (devices[2] != null) // rightXRController
                {
                    Debug.Log("Grabbed right controller successfully");
                    rightControllerGrabbed = true;
                    //Debug.Log("The right controller is " + rightXRController.characteristics);
                    
                }

                if (devices[1] != null) // leftXRController
                {
                    Debug.Log("Grabbed left controller successfully");
                    leftControllerGrabbed = true;
                    //Debug.Log("The left controller is " + leftXRController.characteristics);
                }
            }
        }
    }
}
