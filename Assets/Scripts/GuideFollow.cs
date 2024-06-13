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

    // Variables to control guide's following movements
    private float followDistance = 1.5f;
    private float followSpeed = 5.0f;

    // Monitoring bools
    private bool sharedMovementFound = false;

    private void Start()
    {
        NavMeshAgent navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
            gameObject.AddComponent<NavMeshAgent>();
    }
    // Update is called once per frame
    void Update()
    {
        // Calls until the shared movement script is assigned (when we have a player and a guide)
        getSharedMovement();
        // Wait to request ownership until we have all players in scene + multiplayer room is definitely instantiated
        if (sharedMovementFound)
            requestOwnershipForGuide();

        if (sharedMovementFound)
        {
            AIGuide guide = FindObjectOfType<AIGuide>();
            if (guide != null)
            {
                if (m_SharedMovementScript.thePlayer != null)
                {
                    // Calculate the target position based on the player's position and follow distance
                    Vector3 directionToPlayer = (m_SharedMovementScript.thePlayer.transform.position - transform.position).normalized;
                    Vector3 targetPosition = m_SharedMovementScript.thePlayer.transform.position - directionToPlayer * followDistance;

                    // Move towards the target position
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, followSpeed * Time.deltaTime);

                    // Make the guide look at the player while moving
                    transform.LookAt(m_SharedMovementScript.thePlayer.transform);
                }
            }
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
        else
            sharedMovementFound = true;
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
}
