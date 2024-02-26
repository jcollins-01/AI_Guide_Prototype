using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class TeleportationHandler : MonoBehaviour
{
    public XRController leftTarget;
    public XRController rightTarget;
    public InputHelpers.Button teleportRayTrigger;
    public float activationThreshold = 0.2f;

    private XRInteractorLineVisual leftRay;
    private GameObject leftReticle;

    private XRInteractorLineVisual rightRay;
    private GameObject rightReticle;

    private TeleportationProvider teleport;
    private CharacterController characterController;
    private float characterControllerCenterY;
    private float characterControllerHeight;

    // Start is called before the first frame update
    void Start()
    {
        leftRay = leftTarget.gameObject.GetComponent<XRInteractorLineVisual>();
        leftReticle = leftRay.reticle;

        rightRay = rightTarget.gameObject.GetComponent<XRInteractorLineVisual>();
        rightReticle = rightRay.reticle;

        teleport = this.gameObject.GetComponent<TeleportationProvider>();
        characterController = this.gameObject.GetComponent<CharacterController>();
        characterControllerCenterY = 0.88f;
        characterControllerHeight = 1.6f;
    }

    // Update is called once per frame
    void Update()
    {
        bool leftIsPressed = CheckIfButtonDown(leftTarget);
        leftRay.enabled = leftIsPressed;
        leftReticle.SetActive(leftIsPressed);

        bool rightIsPressed = CheckIfButtonDown(rightTarget);
        rightRay.enabled = rightIsPressed;
        rightReticle.SetActive(rightIsPressed);

        // If the action of teleportation has completed
        if (teleport.locomotionPhase == LocomotionPhase.Done)
        {
            characterController.center = new Vector3(0f, characterControllerCenterY, 0f);
            characterController.height = characterControllerHeight;
        }
    }

    public bool CheckIfButtonDown(XRController controller)
    {
        InputHelpers.IsPressed(controller.inputDevice, teleportRayTrigger, out bool isPressed, activationThreshold);
        return isPressed;
    }
}
