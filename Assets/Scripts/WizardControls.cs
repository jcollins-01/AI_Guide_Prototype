using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voice2Action;

public class WizardControls : MonoBehaviour
{
    // Variables to hold the scripts we access as the wizard
    private QueryDescription m_QueryDescriptionScript;
    private AutomaticGuide m_AutomatedGuideScript;
    private VoiceIntentController m_VoiceIntentController;
    
    // Start is called before the first frame update
    void Start()
    {
        m_QueryDescriptionScript = FindObjectOfType(typeof(QueryDescription)) as QueryDescription;
        m_AutomatedGuideScript = FindObjectOfType(typeof(AutomaticGuide)) as AutomaticGuide;

        if (m_QueryDescriptionScript == null || m_AutomatedGuideScript == null)
        {
            Debug.LogWarning("One or more required scripts for WizardControls has not been found - please ensure that the GameObject with WizardControls also has QueryDescription");
        }
        else
        {
            Debug.Log("WizardControls are active - ready for the wizard to intervene at any time!");
            // Description of the controls the wizard can use
            Debug.Log("Press space to call a test CV query on the scene from the guide's camera");
            // Ex. press left arrow to capture a picture to the left and query it
            Debug.Log("Drag a target game object into the Wizard Controls editor and press g to move the guide to that target, or t to teleport");
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Call a computer vision query on the scene from various directions
        if (Input.GetKeyDown("left"))
        {
            Debug.Log("Wizard called a CV query for the left side of the scene");
            m_QueryDescriptionScript.QueryLeft();
        }

        if (Input.GetKeyDown("right"))
        {
            Debug.Log("Wizard called a CV query for the right side of the scene");
            m_QueryDescriptionScript.QueryRight();
        }

        if (Input.GetKeyDown("up"))
        {
            Debug.Log("Wizard called a CV query for the front of the scene");
            m_QueryDescriptionScript.QueryFront();
        }

        if (Input.GetKeyDown("down"))
        {
            Debug.Log("Wizard called a CV query for behind the user in the scene");
            m_QueryDescriptionScript.QueryBehind();
        }

        if (Input.GetKeyDown("space"))
        {
            Debug.Log("Wizard called a CV query for the entire scene");
            StartCoroutine(m_QueryDescriptionScript.QueryScene());
        }

        // Call a pathfinding algorithm to guide the user to a specific object
        if (Input.GetKeyDown("g"))
        {
            Debug.Log("Wizard called a pathfind to a target object");
            m_AutomatedGuideScript.GuideToPosition();
        }

        // Call a position change to teleport the user to a specific object
        if (Input.GetKeyDown("t"))
        {
            Debug.Log("Wizard called a teleport to a target object");
            m_AutomatedGuideScript.TeleportToPosition();
        }
    }
}
