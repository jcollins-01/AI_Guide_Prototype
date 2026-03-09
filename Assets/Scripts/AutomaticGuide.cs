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
    private Animator animator;

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
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (animator == null || agent == null) return;

        // Calculate the speed based on NavMesh velocity
        float currentSpeed = agent.velocity.magnitude;

        // We divide by the agent's max speed so the value stays between 0 and 1 for the Blend Tree
        float speedPercent = currentSpeed / agent.speed;

        // Tell the Animator the new speed - DampTime (0.1f) makes the transition look smooth instead of robotic
        animator.SetFloat("Speed", speedPercent, 0.1f, Time.deltaTime);
    }

    // Version used for Wizard when assigned target object directly in Editor
    public void GuideToPosition()
    {
        if (m_targetObject != null)
        {
            Debug.Log("Target is not null " + m_targetObject.name);
            // If the target object is one of the people avatars, take user to the gameobject of one of the people
            switch (m_targetObject.name)
            {
                case "Couple By Fountain":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    Debug.Log("Switching on target name Couple By Fountain");
                    break;
                case "Couple By Southern Gazebo":
                    m_targetObject = GameObject.Find("5_m_Talking2");
                    break;
                case "Huddle of People by Gazebo":
                    m_targetObject = GameObject.Find("3_f_Talking");
                    break;
                case "Dancing People":
                    m_targetObject = GameObject.Find("3_f@House Dancing");
                    break;
                case "Western Huddle of People":
                    m_targetObject = GameObject.Find("3_f_Talking");
                    break;
                case "Couple by the Platform":
                    m_targetObject = GameObject.Find("4_m_Talking1");
                    break;
                case "Couple By Puffy Tree":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
                case "Northwest Huddle of People":
                    m_targetObject = GameObject.Find("2_f_Talking2");
                    break;
                case "Couple By the Yellow-Roofed Gazebo":
                    m_targetObject = GameObject.Find("4_m_Talking1");
                    Debug.Log("Switching on target name Couple By the Yellow-Roofed Gazebo");
                    break;
                case "Couple By the Fountain":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    Debug.Log("Switching on target name Couple By Fountain");
                    break;
            }

            Debug.Log("The target object is " + m_targetObject);
            Debug.Log("The target positition destination is " + m_targetObject.transform.position);

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
            // If the target object is one of the people avatars, take user to the gameobject of one of the people
            switch (m_targetObject.name)
            {
                case "Couple By Fountain":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
                case "Couple By Southern Gazebo":
                    m_targetObject = GameObject.Find("5_m_Talking2");
                    break;
                case "Huddle of People by Gazebo":
                    m_targetObject = GameObject.Find("3_f_Talking");
                    break;
                case "Dancing People":
                    m_targetObject = GameObject.Find("3_f@House Dancing");
                    break;
                case "Western Huddle of People":
                    m_targetObject = GameObject.Find("3_f_Talking");
                    break;
                case "Couple by the Platform":
                    m_targetObject = GameObject.Find("4_m_Talking1");
                    break;
                case "Couple By Puffy Tree":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
                case "Northwest Huddle of People":
                    m_targetObject = GameObject.Find("2_f_Talking2");
                    break;
                case "Couple By the Yellow-Roofed Gazebo":
                    m_targetObject = GameObject.Find("4_m_Talking1");
                    break;
                case "Couple By the Fountain":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
            }

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
            // If the target object is one of the people avatars, take user to the gameobject of one of the people
            switch (m_targetObject.name)
            {
                case "Couple By Fountain":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
                case "Couple By Southern Gazebo":
                    m_targetObject = GameObject.Find("5_m_Talking2");
                    break;
                case "Huddle of People by Gazebo":
                    m_targetObject = GameObject.Find("3_f_Talking");
                    break;
                case "Dancing People":
                    m_targetObject = GameObject.Find("3_f@House Dancing");
                    break;
                case "Western Huddle of People":
                    m_targetObject = GameObject.Find("3_f_Talking");
                    break;
                case "Couple by the Platform":
                    m_targetObject = GameObject.Find("4_m_Talking1");
                    break;
                case "Couple By Puffy Tree":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
                case "Northwest Huddle of People":
                    m_targetObject = GameObject.Find("2_f_Talking2");
                    break;
                case "Couple By the Yellow-Roofed Gazebo":
                    m_targetObject = GameObject.Find("4_m_Talking1");
                    break;
                case "Couple By the Fountain":
                    m_targetObject = GameObject.Find("6_m_Talking1");
                    break;
            }

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

    // Cancels current teleportation
    // Stops the agent and clears the target so the guide can follow the player again
    public void CancelGuidance()
    {
        targetActive = false;
        m_targetObject = null;
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }
}