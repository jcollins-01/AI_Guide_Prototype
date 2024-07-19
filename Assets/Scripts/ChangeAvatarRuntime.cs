using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeAvatarRuntime : MonoBehaviour
{
    // Role for multiplayer syncing
    private int _role;

    // Variables to hold scripts we need access to
    private AIGuide m_AIGuideScript;
    private GuideRoleSync m_guideRoleSync;
    private ConfederateRoleSync m_confederateRoleSync;
    private ConfederateHandler m_ConfederateHandlerScript;
    private GuideFollow m_GuideFollowScript;

    // GameObjects for guide avatar assignment
    private GameObject human;
    private GameObject dog;
    private GameObject cane;
    private GameObject robot;
    private GameObject bird;

    // GameObjects for confederate avatar assignment
    private int confederateRoleOne;
    private int confederateRoleTwo;
    private GameObject model1;
    private GameObject model2;
    private GameObject model3;
    private GameObject model4;

    // Monitoring bools
    private bool aiGuideFound = false;
    private bool avatarsFound = false;
    private bool roleSyncFound = false;
    private bool confederateRoleSyncFound = false;
    private bool confederateHandlerFound = false;
    private bool guideFollowFound = false;
    private bool confederateOneAssigned = false;
    private bool confederateTwoAssigned = false;

    private void Start()
    {
        // Ignore collisions between Player or Confederate and XR Rig - ADDED FOR USING GUIDE AS BASE OF CONFEDERATE
        Physics.IgnoreLayerCollision(3, 6, true);
        CharacterController control = FindObjectOfType<CharacterController>();
        control.detectCollisions = true;

        // If we are a confederate object with one of these tags, we can pick the random role to send to network
        if (tag == "Confederate_1")
            AssignConfederateOne();
        /*if (tag == "Confederate_2")
            AssignConfederateTwo();*/
    }

    // Update is called once per frame
    void Update()
    {
        if (!aiGuideFound)
            getAIGuide();
        getPossibleModels();
        if (!roleSyncFound)
            getRoleSync();
        if (!confederateRoleSyncFound)
            getConfederateRoleSync();
        if (!confederateHandlerFound)
            getConfederateHandler();
        if (!guideFollowFound)
            getGuideFollow();

        // Assign the guide's appearance by its role, called constantly in case of role updates
        if (guideFollowFound && avatarsFound) // If we are in the guide scene (GF) + have models for guide (AF), we can assign the guide
        {
            assignGuideAvatarByRole();

            // If a confederate_1 joins the scene, re-assign the confed avatar models to the newest confederate_1
            if (GameObject.FindWithTag("Confederate_1")) // Grab all possible models for confederate one avatars
            {
                model1 = GameObject.Find("Model 1").gameObject;
                model2 = GameObject.Find("Model 2").gameObject;
                model3 = GameObject.Find("Model 3").gameObject;
                model4 = GameObject.Find("Model 4").gameObject;
            }
        }

        if (confederateHandlerFound) // If we are the confederate (CH) + have models for guide and confederate (AF), we can assign the confederate
        {
            // If confederate 1 has joined the scene, re-assign the confed avatar models to the newest confederate_1
            if (GameObject.FindWithTag("Confederate_1"))
            {
                model1 = GameObject.Find("Model 1").gameObject;
                model2 = GameObject.Find("Model 2").gameObject;
                model3 = GameObject.Find("Model 3").gameObject;
                model4 = GameObject.Find("Model 4").gameObject;

                assignConfederateAvatarOneByRole();
            }
        }
    }

    private void AssignConfederateOne()
    {
        // Pick random role to assign for confederate 1
        confederateRoleOne = Random.Range(7, 11);
        //Debug.Log("Random role for confederate one is " + confederateRoleOne);
    }

    private void assignGuideAvatarByRole()
    {
        // Grab role to see if it has changed
        int role = m_AIGuideScript.role;
        UpdateAvatar(role);

        // Set the multiplayer network role if we have the sync component
        SetNewRole(role);
    }

    public void assignConfederateAvatarOneByRole()
    {
        if (!confederateOneAssigned)
        {
            // Set the local avatar to the correct role
            int role = confederateRoleOne;
            UpdateAvatar(role);

            // Set the multiplayer network role if we have the sync component
            SetNewConfederateRole(role);
            confederateOneAssigned = true;
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
            // Grab all possible models for guide avatars - go to parent to search for all models underneath
            if (GameObject.FindWithTag("Guide")) // If a guide has entered the scene, we can start looking for these models
            {
                human = GameObject.Find("Human Model").gameObject;
                dog = GameObject.Find("Guide Dog Model").gameObject;
                cane = GameObject.Find("White Cane Model").gameObject;
                robot = GameObject.Find("Robot Model").gameObject;
                bird = GameObject.Find("Bird Model").gameObject; //gameObject.transform.parent.transform.Find
            }
        }
        else
            avatarsFound = true;
    }

    private void getConfederateHandler()
    {
        if (FindObjectOfType<ConfederateHandler>())
        {
            if (m_ConfederateHandlerScript == null)
                m_ConfederateHandlerScript = FindObjectOfType<ConfederateHandler>();
            else
                confederateHandlerFound = true;
        }
    }

    private void getGuideFollow()
    {
        // If there is a GuideFollow component in the scene (we are in the scene with the Guide's rig), look to assign guide follow
        // This will not work for a confederate scene
        if (FindObjectOfType<GuideFollow>())
        {
            if (m_GuideFollowScript == null)
                m_GuideFollowScript = FindObjectOfType<GuideFollow>();
            else
                guideFollowFound = true;
        }
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
        //Debug.Log("Set a new role from network: " + role);
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
            Debug.LogError("GuideRoleSync is not initialized.");
    }

    private void getConfederateRoleSync()
    {
        if (m_confederateRoleSync == null)
            m_confederateRoleSync = FindObjectOfType<ConfederateRoleSync>();
        else
            confederateRoleSyncFound = true;

    }

    public void SetConfederateRole(int role)
    {
        Debug.Log("Set a new confederate role from network: " + role);
        _role = role;
        UpdateAvatar(_role);
    }

    public int GetConfederateCurrentRole()
    {
        return _role;
    }

    private void SetNewConfederateRole(int role)
    {
        if (confederateRoleSyncFound)
            m_confederateRoleSync.SetConfederateRole(role);
        else
            Debug.LogError("ConfederateRoleSync is not initialized.");
    }

    private void UpdateAvatar(int role)
    {
        // Assign renderers by role, deactivate non-role, activate others
        if (role == 1) // Human
        {
            // Enable selected guide avatar
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
        else if (role == 7) // Model 1
        {
            Debug.Log("Trying to update to role 7");
            // Enable correct confederate model
            EnableAllRenderers(model1);
            DisableAllRenderers(model2);
            DisableAllRenderers(model3);
            DisableAllRenderers(model4);
        }
        else if (role == 8) // Model 2
        {
            Debug.Log("Trying to update to role 8");
            DisableAllRenderers(model1);
            EnableAllRenderers(model2);
            DisableAllRenderers(model3);
            DisableAllRenderers(model4);
        }
        else if (role == 9) // Model 3
        {
            Debug.Log("Trying to update to role 9");
            DisableAllRenderers(model1);
            DisableAllRenderers(model2);
            EnableAllRenderers(model3);
            DisableAllRenderers(model4);
        }
        else if (role == 10) // Model 4
        {
            Debug.Log("Trying to update to role 10");
            DisableAllRenderers(model1);
            DisableAllRenderers(model2);
            DisableAllRenderers(model3);
            EnableAllRenderers(model4);
        }
    }
}
