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
    public GameObject leftReticle;

    private XRInteractorLineVisual rightRay;
    public GameObject rightReticle;

    private TeleportationProvider teleport;
    private CharacterController characterController;
    private float characterControllerCenterY;
    private float characterControllerHeight;

    // Variables to hold scripts we need access to
    private VRScreenreader m_VRScreenreaderScript;

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

        m_VRScreenreaderScript = FindObjectOfType<VRScreenreader>();
    }

    // Update is called once per frame
    void Update()
    {
        bool leftIsPressed = CheckIfButtonDown(leftTarget);
        leftRay.enabled = leftIsPressed;
        leftReticle.SetActive(leftIsPressed);
        CheckForReticleHit(leftTarget, leftRay);

        bool rightIsPressed = CheckIfButtonDown(rightTarget);
        rightRay.enabled = rightIsPressed;
        rightReticle.SetActive(rightIsPressed);
        CheckForReticleHit(rightTarget, rightRay);

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

    // Function to check if the ray hits something
    void CheckForReticleHit(XRController controller, XRInteractorLineVisual ray)
    {
        RaycastHit hit;
        Ray raycast = new Ray(ray.transform.position, ray.transform.forward);

        // Perform raycast to detect objects in the teleportableLayerMask
        if (Physics.Raycast(raycast, out hit, Mathf.Infinity, Physics.AllLayers))
        {
            // Check if the reticle hit a teleportable surface
            if (hit.collider != null)
            {
                //Debug.Log("Teleport reticle hit an object: " + hit.collider.gameObject.name);
                m_VRScreenreaderScript.TeleportCheckReferenceAndPlayAudio(hit.transform.gameObject);
            }
        }
    }
}
