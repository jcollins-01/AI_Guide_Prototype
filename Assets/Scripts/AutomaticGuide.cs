using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AutomaticGuide : MonoBehaviour
{
    public GameObject m_targetObject; // The target game object to move towards
    private NavMeshAgent agent; // Reference to the NavMeshAgent component
    public bool targetActive = false; // A bool to keep track of if there is a target / when it's been reached
    private OpenAIQueries m_OpenAIQueriesScript;

    // Start is called before the first frame update
    void Start()
    {
        m_OpenAIQueriesScript = FindObjectOfType<OpenAIQueries>();
        //agent = GetComponent<NavMeshAgent>(); // Get the NavMeshAgent component attached to the same game object
        agent = FindObjectOfType<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on this game object.");
        }
    }

    // Version used for Wizard when assigned target object directly in Editor
    public void GuideToPosition()
    {
        if (m_targetObject != null)
        {
            agent.SetDestination(m_targetObject.transform.position); // Set the destination of the NavMeshAgent to the position of the target's transform
            
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
        if (m_targetObject != null)
        {
            agent.ResetPath(); // Reset path in case we had just set a guide destination
            var targetPosition = m_targetObject.transform.position;
            agent.transform.position = targetPosition + new Vector3(1f, 0f, 0f); // Sets the destination of the agent to 1 unit to the right of the target
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }

    // Version used for automated guide when calling guidance function with an assigned target object
    public void GuideToPosition(GameObject targetObject)
    {
        m_targetObject = targetObject;
        Transform target = targetObject.transform;
        if (target != null)
        {
            // Debug.Log("We have a target passed = " + target.name);
            targetActive = true;
            // Set the destination of the NavMeshAgent to the position of the target's transform
            agent.isStopped = false;
            agent.SetDestination(target.position); 

            if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending) // Check if the agent has reached the destination
            {
                agent.ResetPath(); // Clear the destination to stop further movement
                targetActive = false;
                // Switch the targetForGuidance to null so guide will begin following player again
                m_OpenAIQueriesScript.targetForGuidance = null;
            }
            // Debug.Log("The target is active: " + targetActive);
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }

    // Version used for automated guide when calling guidance function with an assigned target object
    public void TeleportToPosition(GameObject targetObject)
    {
        m_targetObject = targetObject;
        Transform target = targetObject.transform;
        if (target != null)
        {
            // Debug.Log("We have a target passed = " + target.position);
            //agent.ResetPath(); // Reset path in case we had just set a guide destination
            targetActive = true;
            agent.isStopped = true;
            agent.transform.position = target.position + new Vector3(0.1f, 0f, 0f); // Sets the destination of the agent to 1 unit to the right of the target
            // Debug.Log("Moved to = " + agent.transform.position);
            targetActive = false;
            // Switch the targetForGuidance to null so guide will begin following player again
            m_OpenAIQueriesScript.targetForGuidance = null;
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }
}