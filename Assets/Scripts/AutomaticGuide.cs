using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AutomaticGuide : MonoBehaviour
{
    public GameObject targetObject; // The target game object to move towards
    private NavMeshAgent agent; // Reference to the NavMeshAgent component

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>(); // Get the NavMeshAgent component attached to the same game object
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on this game object.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Version used for testing when assigned target object directly in Editor
    public void GuideToPosition()
    {
        if (targetObject != null)
        {
            agent.SetDestination(targetObject.transform.position); // Set the destination of the NavMeshAgent to the position of the target's transform
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }

    // Version used for final when calling a function with an assigned target object from voice2action
    public void GuideToPosition(Transform target)
    {
        if (target != null)
        {
            agent.SetDestination(target.position); // Set the destination of the NavMeshAgent to the position of the target's transform
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }
}
