using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WizardControls : MonoBehaviour
{
    // Variables to hold the scripts we access as the wizard
    private QueryDescription m_QueryDescriptionScript;
    private AutomaticGuide m_AutomatedGuideScript;

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
            // Press space to activate a guide function?
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Call a computer vision query on the scene - THIS WILL BE REMOVED AND REPLACED WITH QUERIES FOR SPECIFIC DIRECTIONS
        if (Input.GetKeyDown("space"))
        {
            Debug.Log("Wizard called a CV query on the scene");
            m_QueryDescriptionScript.CaptureScreenshot();
        }

        if (Input.GetKeyDown("g"))
        {
            Debug.Log("Wizard called a pathfind to a target object");
            m_AutomatedGuideScript.GuideToPosition();
        }
    }
}
