using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeAvatarRuntime : MonoBehaviour
{
    // Role for multiplayer syncing
    private int _role;

    // Variables to hold scripts we need access to
    private AIGuide m_AIGuideScript;
    private SharedMovement m_SharedMovementScript;
    private GuideRoleSync m_guideRoleSync;
    private ConfederateHandler m_ConfederateHandlerScript;

    // GameObjects for guide avatar assignment
    private GameObject theGuide;
    private GameObject human;
    private GameObject dog;
    private GameObject cane;
    private GameObject robot;
    private GameObject bird;

    // GameObjects for confederate avatar assignment
    private GameObject theConfederate;
    private GameObject model1;
    private GameObject model2;
    private GameObject model3;
    private GameObject model4;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool aiGuideFound = false;
    private bool avatarsFound = false;
    private bool roleSyncFound = false;
    private bool confederateHandlerFound = false;

    private void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Call until each respective component is found or assigned
        if (!sharedMovementFound)
            getSharedMovement();
        if (!aiGuideFound)
            getAIGuide();
        if (sharedMovementFound && !avatarsFound)
            getPossibleModels();
        if (!roleSyncFound)
            getRoleSync();
        if (!confederateHandlerFound)
            getConfederateHandler();

        // Assign the guide's appearance by its role, called constantly in case of role updates
        if (sharedMovementFound && aiGuideFound && avatarsFound && confederateHandlerFound)
        {
            // If the local client is NOT a confederate version, we can update the guide's role from it
            if (!m_ConfederateHandlerScript.confederateVersion)
                assignGuideAvatarByRole();
        }

        // Continuously search for unassigned confederates and assign them random avatars
        //pickAvatarAtRandomForAll();
    }

    private void assignGuideAvatarByRole()
    {
        // Grab role to see if it has changed
        int role = m_AIGuideScript.role;
        UpdateAvatar(role);

        // Set the multiplayer network role if we have the sync component
        SetNewRole(role);
    }

    // Called from ConfederateHandler
    public void assignConfederateAvatarByRole(int role)
    {
        Debug.Log("Sending confederate role to network + updating avatar");
        UpdateAvatar(role);
        SetNewRole(role);
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
    // This is done if we want confederates to have random appearances, not shared over the network
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

            // Change their tag once assigned so the script ignores them
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
        // Grab all possible models for guide avatars
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

    // Called from ConfederateHandler
    public void getConfederateModels(GameObject passedConfederate)
    {
        theConfederate = passedConfederate;
        // Grab all possible models for confederate avatars
        model1 = theConfederate.transform.Find("Model 1").gameObject;
        model2 = theConfederate.transform.Find("Model 2").gameObject;
        model3 = theConfederate.transform.Find("Model 3").gameObject;
        model4 = theConfederate.transform.Find("Model 4").gameObject;
    }

    private void getConfederateHandler()
    {
        if (m_ConfederateHandlerScript == null)
            m_ConfederateHandlerScript = FindObjectOfType<ConfederateHandler>();
        else
            confederateHandlerFound = true;
    }

    // Methods to update the avatar across the multiplayer network
    private void getRoleSync()
    {
        if (m_guideRoleSync == null)
            m_guideRoleSync = FindObjectOfType<GuideRoleSync>();
        else
            roleSyncFound = true;
            
    }

    public void SetRole(int role)
    {
        Debug.Log("Set a new role from network" + role);
        _role = role;
        UpdateAvatar(_role);
    }

    public int GetCurrentRole()
    {
        return _role;
    }

    private void SetNewRole(int role)
    {
        if (roleSyncFound)
            m_guideRoleSync.SetRole(role);
        else
            Debug.LogError("GuideAudioSync is not initialized.");
    }

    private void UpdateAvatar(int role)
    {
        //Debug.Log("Avatar changed to role: " + role);

        // Assign renderers by role, deactivate non-role, activate others
        if (role == 1) // Human
        {
            EnableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 2) // Guide Dog
        {
            DisableAllRenderers(human);
            EnableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 3) // White Cane
        {
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            EnableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 4) // Robot
        {
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            EnableAllRenderers(robot);
            DisableAllRenderers(bird);
        }
        else if (role == 5) // Bird
        {
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            EnableAllRenderers(bird);
        }
        else if (role == 6) // Invisible Guide
        {
            DisableAllRenderers(human);
            DisableAllRenderers(dog);
            DisableAllRenderers(cane);
            DisableAllRenderers(robot);
            DisableAllRenderers(bird);
        } // END OF GUIDE ROLES ----- BEGINNING OF CONFEDERATE ROLES
        else if (role == 7 && confederateHandlerFound) // Model 1
        {
            model1.SetActive(true);
            model2.SetActive(false);
            model3.SetActive(false);
            model4.SetActive(false);
        }
        else if (role == 8 && confederateHandlerFound) // Model 2
        {
            model1.SetActive(false);
            model2.SetActive(true);
            model3.SetActive(false);
            model4.SetActive(false);
        }
        else if (role == 9 && confederateHandlerFound) // Model 3
        {
            model1.SetActive(false);
            model2.SetActive(false);
            model3.SetActive(true);
            model4.SetActive(false);
        }
        else if (role == 10 && confederateHandlerFound) // Model 4
        {
            model1.SetActive(false);
            model2.SetActive(false);
            model3.SetActive(false);
            model4.SetActive(true);
        }
    }
}
