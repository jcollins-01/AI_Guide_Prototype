using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AutomaticGuide : MonoBehaviour
{
    public GameObject targetObject; // The target game object to move towards
    private NavMeshAgent agent; // Reference to the NavMeshAgent component
    private WizardControls m_WizardControlsScript;

    // Start is called before the first frame update
    void Start()
    {
        m_WizardControlsScript = FindObjectOfType(typeof(WizardControls)) as WizardControls;
        agent = GetComponent<NavMeshAgent>(); // Get the NavMeshAgent component attached to the same game object
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on this game object.");
        }
    }

    // Version used for Wizard when assigned target object directly in Editor
    public void GuideToPosition()
    {
        if (targetObject != null)
        {
            m_WizardControlsScript.m_TextToSpeechMessage = "The guide will begin moving the user to " + targetObject.name;
            agent.SetDestination(targetObject.transform.position); // Set the destination of the NavMeshAgent to the position of the target's transform
            
            if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending) // Check if the agent has reached the destination
                agent.ResetPath(); // Clear the destination to stop further movement
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }

    // Version used for Wizard when assigned target object directly in Editor
    public void TeleportToPosition()
    {
        if (targetObject != null)
        {
            m_WizardControlsScript.m_TextToSpeechMessage = "The guide will begin teleporting the user to " + targetObject.name;
            agent.ResetPath(); // Reset path in case we had just set a guide destination
            var targetPosition = targetObject.transform.position;
            agent.transform.position = targetPosition + new Vector3(1f, 0f, 0f); // Sets the destination of the agent to 1 unit to the right of the target
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }
}