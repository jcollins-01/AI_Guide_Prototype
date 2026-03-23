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
        Transform selfTransform = agent.transform;
        Collider targetCollider = target.GetComponentInChildren<Collider>();

        if (target != null)
        {
            //Debug.Log("[Automatic Guide] We have a target passed = " + target.name);
            targetActive = true;
            agent.isStopped = false;

            Vector3 targetLocation = GetTargetLocation(targetCollider, target, selfTransform);
            // Set destination to our determined location
            agent.SetDestination(targetLocation);

            if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending) // Check if the agent has reached the destination
            {
                Debug.Log("[Automatic Guide] Agent reached the target" + target.name + " via guidance");
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
        Transform selfTransform = agent.transform;
        Collider targetCollider = target.GetComponentInChildren<Collider>();

        if (target != null)
        {
            CaseSpecificMoves();

            //Debug.Log("[Automatic Guide] We have a target passed = " + target.name);
            targetActive = true;
            agent.isStopped = true; // in guidance, it's false
            agent.ResetPath(); // Reset path in case we had just set a guide destination

            Vector3 targetLocation = GetTargetLocation(targetCollider, target, selfTransform);
            //Debug.Log("Trying to warp to target at location " + targetLocation);

            // Warp to our determined location
            if (agent.Warp(targetLocation))
            {
                //Debug.Log("Successfully warped to valid NavMesh position " + targetLocation + " near " + m_targetObject.name);
                //Debug.Log("New agent location is " + agent.transform.position);
            }
            else
            {
                Debug.Log("Could not find a valid location to warp to");
                // Try warping to the exact target position and let Unity try to resolve it
                agent.Warp(target.position);
            }

            Debug.Log("[Automatic Guide] Agent reached the target " + target.name + " via teleportation");
            targetActive = false;
            // Switch the targetForGuidance to null so guide will begin following player again
            m_OpenAIQueriesScript.targetForGuidance = null;
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }

    private Vector3 GetTargetLocation(Collider targetCollider, Transform targetTransform, Transform selfTransform)
    {
        Vector3 targetLocation;
        // Lets us grab the collider instead of just the transform of an object, since the transform pivot point is often in the center of an object, far from the nav mesh
        // If we check the collider edge, we can check for how close we are to the edge of that collider instead of getting caught in the deadZone of the pivot point between surfaces
        if (targetCollider != null)
        {
            targetLocation = targetCollider.ClosestPoint(selfTransform.position);
        }
        else
        {
            targetLocation = targetTransform.position;
        }

        targetLocation.y = selfTransform.position.y;

        // Send out a raycast and a sample position to check if there was a straight line (raycast) or a valid point on a nearby navmesh (sample)
        NavMeshQueryFilter queryFilter = new NavMeshQueryFilter() { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        NavMesh.Raycast(selfTransform.position, targetPosition: targetLocation, out NavMeshHit raycastHit, queryFilter);
        NavMesh.SamplePosition(targetLocation, out NavMeshHit samplePositionHit, maxDistance: agent.radius + agent.stoppingDistance, NavMesh.AllAreas);

        // If either of these hit
        if (raycastHit.hit || samplePositionHit.hit)
        {
            // Figure out which of the two hits is closer and go for that one
            NavMeshHit hit = GetClosestHit(raycastHit, samplePositionHit, targetLocation);
            targetLocation = hit.position - (hit.position - selfTransform.position).normalized * agent.radius; // radius helps us figure out the right place to stop
        }

        return targetLocation;
    }

    private NavMeshHit GetClosestHit(NavMeshHit hit1, NavMeshHit hit2, Vector3 target)
    {
        // If both hit, figure out the closest one and use it
        if (hit1.hit && hit2.hit)
        {
            return Vector3.Distance(hit1.position, target) <= Vector3.Distance(hit2.position, target)
                ? hit1
                : hit2;
        }

        // If only hit1 hit, we use hit1
        if (hit1.hit && !hit2.hit)
        {
            return hit1;
        }

        // If only hit2 hit, we use hit2
        return hit2;
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

    private void CaseSpecificMoves()
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
    }
}