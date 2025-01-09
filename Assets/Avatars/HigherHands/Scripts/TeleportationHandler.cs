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

    // Bools to control for screenreader/guide switch
    private bool screenreaderActive = false;

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
        if (m_VRScreenreaderScript && m_VRScreenreaderScript.gameObject.activeInHierarchy)
            screenreaderActive = true;
    }

    // Update is called once per frame
    void Update()
    {
        bool leftIsPressed = CheckIfButtonDown(leftTarget);
        leftRay.enabled = leftIsPressed;
        leftReticle.SetActive(leftIsPressed);
        if (screenreaderActive && leftIsPressed)
            CheckForReticleHit(leftTarget, leftRay);

        bool rightIsPressed = CheckIfButtonDown(rightTarget);
        rightRay.enabled = rightIsPressed;
        rightReticle.SetActive(rightIsPressed);
        if (screenreaderActive && rightIsPressed)
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
        // Ensure the reticle exists and is active
        GameObject reticle = ray.reticle;
        if (reticle == null || !reticle.activeInHierarchy)
            return;

        // Get the position of the reticle
        Vector3 reticlePosition = reticle.transform.position;

        // Perform a Physics.OverlapSphere to check objects at the reticle's position
        float sphereRadius = 0.02f;
        int layerMask = ~LayerMask.GetMask("Ignore Raycast");
        Collider[] hitColliders = Physics.OverlapSphere(reticlePosition, sphereRadius, layerMask);

        if (hitColliders.Length > 0)
        {
            foreach (Collider hitCollider in hitColliders)
            {
                //Debug.Log("Teleport reticle hit an object: " + hitCollider.gameObject.name);
                // Call the screenreader function for the hit object
                m_VRScreenreaderScript.TeleportCheckReferenceAndPlayAudio(hitCollider.gameObject);
            }
        }
        else
            Debug.Log("No objects detected at reticle position.");
    }
}
