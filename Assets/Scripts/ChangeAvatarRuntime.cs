using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeAvatarRuntime : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private AIGuide m_AIGuideScript;
    private SharedMovement m_SharedMovementScript;

    // GameObjects for avatar assignment
    private GameObject theGuide;
    private GameObject human;
    private GameObject dog;
    private GameObject cane;
    private GameObject robot;
    private GameObject bird;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool aiGuideFound = false;
    private bool avatarsFound = false;

    // Variable to assign publicly in the editor; set in RoomManager
    //public bool isPlayer;

    private void Start()
    {
        // Find and load appropriate resources
        m_AIGuideScript = GetComponent<AIGuide>();
    }

    // Update is called once per frame
    void Update()
    {
        getSharedMovement();
        getAIGuide();
        getPossibleModels();

        // Assign the guide's appearance by its role, called constantly in case of role updates
        if (sharedMovementFound && aiGuideFound && avatarsFound)
            assignAvatarByRole();
    }

    private void assignAvatarByRole()
    {
        // Grab role to see if it has changed
        int role = m_AIGuideScript.role;

        // Assign renderers by role, deactivate non-role, activate others
        if (role == 1) // Human
        {
            Debug.Log("Assigning human avatar");
            EnableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 2) // Guide Dog
        {
            Debug.Log("Assigning dog avatar");
            DisableAllRenderers(human);
            EnableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 3) // White Cane
        {
            Debug.Log("Assigning cane avatar");
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            EnableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 4) // Robot
        {
            Debug.Log("Assigning robot avatar");
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            EnableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 5) // Bird
        {
            Debug.Log("Assigning bird avatar");
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            EnableAllRenderers(bird);
        }
        else // Invisible Guide
        {
            Debug.Log("Assigning NO avatar");
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
    }

    private void DisableAllRenderers(GameObject model)
    {
        if (model != null)
        {
            // Get all Renderer components in the children of the object
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            // Disable each Renderer component
            foreach (Renderer renderer in renderers)
                renderer.enabled = false;
        }
    }

    private void EnableAllRenderers(GameObject model)
    {
        if (model != null)
        {
            // Get all Renderer components in the children of the object
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            // Disable each Renderer component
            foreach (Renderer renderer in renderers)
                renderer.enabled = true;
        }
    }

    // Finds all avatars in the scene with AvatarUnassigned tag, gives a random avatar
    // This is done for confederates so they have random appearances
    private void pickAvatarAtRandomForAll()
    {
        GameObject[] avatarsInScene;
        avatarsInScene = GameObject.FindGameObjectsWithTag("AvatarUnassigned");

        foreach (GameObject avatar in avatarsInScene)
        {
            int random = Random.Range(0, 4);
            Debug.Log("Found unassigned avatar " + avatar + ". Assigning it num: " + random);

            // Grab all possible models for avatars
            GameObject model1 = avatar.transform.Find("Model 1").gameObject;
            GameObject model2 = avatar.transform.Find("Model 2").gameObject;
            GameObject model3 = avatar.transform.Find("Model 3").gameObject;
            GameObject model4 = avatar.transform.Find("Model 4").gameObject;

            if (random == 0) // Choose the first model
            {
                model1.SetActive(true);
                model2.SetActive(false);
                model3.SetActive(false);
                model4.SetActive(false);
            }
            else if (random == 1) // Choose the second model
            {
                model1.SetActive(false);
                model2.SetActive(true);
                model3.SetActive(false);
                model4.SetActive(false);
            }
            else if (random == 2) // Choose the third model
            {
                model1.SetActive(false);
                model2.SetActive(false);
                model3.SetActive(true);
                model4.SetActive(false);
            }
            else // Choose the fourth model
            {
                model1.SetActive(false);
                model2.SetActive(false);
                model3.SetActive(false);
                model4.SetActive(true);
            }

            avatar.tag = "AvatarAssigned";
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
        else
        {
            theGuide = m_SharedMovementScript.theGuide;
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

    private void getPossibleModels()
    {
        if (human == null || dog == null || cane == null || robot == null || bird == null)
        {
            // Grab all possible models for avatars
            human = theGuide.transform.parent.transform.Find("Human Model").gameObject;
            dog = theGuide.transform.parent.transform.Find("Guide Dog Model").gameObject;
            cane = theGuide.transform.parent.transform.Find("White Cane Model").gameObject;
            robot = theGuide.transform.parent.transform.Find("Robot Model").gameObject;
            bird = theGuide.transform.parent.transform.Find("Bird Model").gameObject;
        }
        else
            avatarsFound = true;
    }
}
