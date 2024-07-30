using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GuideFollow : MonoBehaviour
{
    // Variables to hold scripts / components we need access to
    private SharedMovement m_SharedMovementScript;
    private RealtimeTransform realtimeTransform;
    private AIGuide m_AIGuideScript;

    // Variables to control guide's following movements
    private float followDistance = 1.5f;
    private float followSpeed = 5.0f;
    private float rotationSpeed = 5.0f; // might be causing the bouncy issue?
    private float heightOffset = 0.2f;
    public float guideHeight = 0f;
    public float playerHeight = 0f;

    // Agents and Game Objects
    private NavMeshAgent agent;
    private GameObject theGuide;
    private GameObject thePlayer;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool aiGuideFound = false;

    private void Start()
    {
        // Find or add necessary components
        agent = gameObject.AddComponent<NavMeshAgent>();

        gameObject.AddComponent<GuideModels>();
    }
    // Update is called once per frame
    void Update()
    {
        // Calls until the shared movement and AI Guide scripts are assigned (when we have a player and a guide)
        if (!sharedMovementFound)
            getSharedMovement();
        if (!aiGuideFound)
            getAIGuide();

        // Wait to request ownership until we have all players in scene + multiplayer room is definitely instantiated
        if (sharedMovementFound)
            requestOwnershipForGuide();

        if (aiGuideFound && sharedMovementFound)
        {
            guideHeight = getAvatarHeight(theGuide);
            playerHeight = getAvatarHeight(thePlayer);
        }

        if (sharedMovementFound)
        {
            if (theGuide != null && thePlayer != null) // Making sure code is not run until both player and guide are in the scene
                setTargetPositions();
        }
    }

    private void setTargetPositions()
    {
        // Set target position and rotation based on role
        Vector3 targetPosition = theGuide.transform.position;
        Quaternion targetRotation = theGuide.transform.rotation;

        // Humanoid follow position - trailing after the player and facing them
        if (m_AIGuideScript.role == 1 || m_AIGuideScript.role == 2) // Human and robot
        {
            // Calculate the target position based on the player's position and follow distance
            Vector3 directionToPlayer = (thePlayer.transform.position - transform.position).normalized;
            targetPosition = thePlayer.transform.position - directionToPlayer * followDistance;

            // Move towards the target position
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, followSpeed * Time.deltaTime);

            // Make the guide look at the player while moving
            transform.LookAt(thePlayer.transform);
        }

        // Dog follow position - moving at the right side of the player and facing the same direction as them
        if (m_AIGuideScript.role == 4)
        {
            // Calculate the position on the right side of the player
            Vector3 offset = thePlayer.transform.right * (followDistance * 0.5f);
            targetPosition = thePlayer.transform.position + offset;

            // Rotate to face the same direction as the player
            targetRotation = thePlayer.transform.rotation;
        }

        // Cane follow position - moving in front of the player and facing away
        if (m_AIGuideScript.role == 3)
        {
            // Calculate the position in front of the player
            Vector3 offset = thePlayer.transform.forward * (followDistance * 0.5f);
            targetPosition = thePlayer.transform.position + offset;

            // Rotate to face away from the player (same direction the player is looking)
            targetRotation = thePlayer.transform.rotation;
        }

        // Bird and invisible guide follow position - moving at the right of the player's head and facing the same direction as them
        if (m_AIGuideScript.role == 5 || m_AIGuideScript.role == 6)
        {
            // Calculate the position on the right side of the player
            Vector3 offset = thePlayer.transform.right * (followDistance * 0.25f);
            targetPosition = thePlayer.transform.position + offset;
            transform.position = new Vector3(transform.position.x, transform.position.y + 0.25f, transform.position.z);

            // Rotate to face the same direction as the player
            targetRotation = thePlayer.transform.rotation;

            // Calculate the position to the right of the player, at the appropriate height
            //Vector3 offset = thePlayer.transform.right * (followDistance * 0.5f);
            //targetPosition = thePlayer.transform.position + offset + Vector3.up * (playerHeight + heightOffset - guideHeight / 2);
            //transform.position = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);

            // Rotate to face the same direction as the player
            //targetRotation = thePlayer.transform.rotation;
        }

        if (agent.isOnNavMesh)
            agent.SetDestination(targetPosition);
        else
        {
            Debug.LogWarning("NavMeshAgent is not on the NavMesh. Trying to fix it.");

            // Attempt to place the agent on the NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 1.0f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
                agent.SetDestination(targetPosition);
            }
            else
                Debug.LogError("Failed to place NavMeshAgent on the NavMesh.");
        }

        // Smoothly rotate towards the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
        else
        {
            theGuide = m_SharedMovementScript.theGuide;
            thePlayer = m_SharedMovementScript.thePlayer;
            if (theGuide != null && thePlayer != null)
                sharedMovementFound = true;
        }
    }

    private void getAIGuide()
    {
        if (m_AIGuideScript == null)
            m_AIGuideScript = FindObjectOfType<AIGuide>();
        else
            aiGuideFound = true;
    }

    private void requestOwnershipForGuide()
    {
        AIGuide guide = FindObjectOfType<AIGuide>();
        if (guide != null)
        {
            realtimeTransform = m_SharedMovementScript.theGuide.GetComponent<RealtimeTransform>();
            //realtimeTransform = GetComponent<RealtimeTransform>();

            // Request ownership of the RealtimeTransform component for the local client producing the guide
            if (realtimeTransform != null)
                realtimeTransform.RequestOwnership();
        }
    }

    float getAvatarHeight(GameObject avatar)
    {
        SkinnedMeshRenderer[] renderers = theGuide.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("No renderers found on the object.");
            return 0;
        }

        Bounds combinedBounds = renderers[0].bounds;
        foreach (SkinnedMeshRenderer renderer in renderers)
            combinedBounds.Encapsulate(renderer.bounds);

        return combinedBounds.size.y;
    }
}
