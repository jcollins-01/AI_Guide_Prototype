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

            // This is checked multiple times/updated as the guide moves, as GuideToPosition is called in AIGuide's Update flow
            if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending) // Check if the agent has reached the destination
            {
                Debug.Log($"[Automatic Guide] Agent reached the target {target.name} via guidance");
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
            TeleportNextTo(target.gameObject);

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

    public void TeleportNextTo(GameObject targetObject, float padding = 1.5f)
    {
        if (targetObject == null || agent == null) return;

        // Try to get the size of the object to know how far to stay away
        Vector3 targetPos = targetObject.transform.position;
        float offsetDistance = padding;

        Collider targetCollider = targetObject.GetComponent<Collider>();
        if (targetCollider != null)
        {
            // Use the largest extent (width/depth) so we stay outside the long side of big objects
            offsetDistance += Mathf.Max(targetCollider.bounds.extents.x, targetCollider.bounds.extents.z);
        }

        // Find the guaranteed NavMesh floor directly under/at the target object
        Vector3 lockedFloorPos = targetPos;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit floorHit, 2.0f, NavMesh.AllAreas))
        {
            // Even if the object is on a table/other surface, we still get the floor height
            lockedFloorPos = floorHit.position;
        }

        // Determine the direction (warp to the side closest to the agent's current position) and keep direction strictly on the flat XZ plane
        Vector3 directionToAgent = (agent.transform.position - lockedFloorPos);
        directionToAgent.y = 0;
        directionToAgent.Normalize();

        // If the agent and target are at the exact same spot, default to 'Forward'
        if (directionToAgent == Vector3.zero) directionToAgent = Vector3.forward;

        // Calculate where we want to go, locking the Y height to the room's floor
        Vector3 desiredPoint = lockedFloorPos + (directionToAgent * offsetDistance);
        desiredPoint.y = lockedFloorPos.y;

        // Ensure we don't cross any walls using a Linecast - temporarily disable the target's collider so the line doesn't hit the object itself
        if (targetCollider != null) targetCollider.enabled = false;

        // Lift the line slightly (0.5f) so it doesn't scrape the floor and trigger false hits
        Vector3 lineStart = lockedFloorPos + (Vector3.up * 0.5f);
        Vector3 lineEnd = desiredPoint + (Vector3.up * 0.5f);

        if (Physics.Linecast(lineStart, lineEnd, out RaycastHit wallHit))
        {
            // We hit an interior wall! Clamp the position just inside the room
            desiredPoint = wallHit.point - (directionToAgent * agent.radius);
            desiredPoint.y = lockedFloorPos.y; // Re-enforce the floor height
            //Debug.Log($"Linecast hit {wallHit.collider.name}. Clamping inside room.");
        }

        if (targetCollider != null) targetCollider.enabled = true;

        // Final NavMesh check using a TINY radius (1.0f)
        // Because the Y-axis is locked to the target's floor, a 1.0f radius shouldn't reach a roof or any space below the room flooring
        if (NavMesh.SamplePosition(desiredPoint, out NavMeshHit finalHit, 1.0f, NavMesh.AllAreas))
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.Warp(finalHit.position);
            //Debug.Log($"Successfully warped next to {targetObject.name} at {finalHit.position}");
        }
        else
        {
            // If the tiny search fails (e.g., clamped into a weird corner), warp directly onto the target's floor position
            //Debug.Log("Offset NavMesh search failed. Warping directly to target's floor instead.");
            agent.isStopped = true;
            agent.ResetPath();
            agent.Warp(lockedFloorPos); // Force it and hope for the best
        }
    }

    // Cancels current teleportation
    // Stops the agent and clears the target so the guide can follow the player again
    public void CancelGuidance()
    {
        Debug.Log("Canceled guidance");
        targetActive = false;
        Debug.Log($"[Cancel guidance] For chime to play, targetActive needs to be false, it is {targetActive}");
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