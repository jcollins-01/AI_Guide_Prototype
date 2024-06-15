using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuideModels : MonoBehaviour
{
    // Variables to hold scripts / components we need access to
    private SharedMovement m_SharedMovementScript;
    private AIGuide m_AIGuideScript;

    // Game Objects we use to manage guide appearance
    private GameObject theGuide;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Calls until the shared movement and AI Guide scripts are assigned (when we have a player and a guide)
        getSharedMovement();
        getAIGuide();

        // If we have the guide and access to its role, we can alter appearances
        if (theGuide != null && m_AIGuideScript != null)
        {
            // If invisible, disabled renderers - enable them otherwise
            if (m_AIGuideScript.role == 6)
                DisableRenderers(theGuide);
            else
                EnableRenderers(theGuide);

            // Code for switching between models / animations based on role
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();

        theGuide = m_SharedMovementScript.theGuide;
    }

    private void getAIGuide()
    {
        if (m_AIGuideScript == null)
            m_AIGuideScript = FindObjectOfType<AIGuide>();
    }

    private void DisableRenderers(GameObject theGuide)
    {
        // Disable all Renderer components on the guide
        SkinnedMeshRenderer[] renderers = theGuide.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in renderers)
            renderer.enabled = false;
    }

    private void EnableRenderers(GameObject theGuide)
    {
        // Disable all Renderer components on the guide
        SkinnedMeshRenderer[] renderers = theGuide.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in renderers)
            renderer.enabled = true;
    }
}
