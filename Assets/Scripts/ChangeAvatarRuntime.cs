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
    private GuideFollow m_GuideFollowScript;

    // GameObjects for guide avatar assignment
    private GameObject human;
    private GameObject dog;
    private GameObject cane;
    private GameObject robot;
    private GameObject bird;

    // Monitoring bools
    private bool aiGuideFound = false;
    private bool avatarsFound = false;
    private bool roleSyncFound = false;
    private bool guideFollowFound = false;

    private void Start()
    {
        // Ignore collisions between Player or Confederate and XR Rig - ADDED FOR USING GUIDE AS BASE OF CONFEDERATE
        Physics.IgnoreLayerCollision(3, 6, true);
        CharacterController control = FindObjectOfType<CharacterController>();
        control.detectCollisions = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!aiGuideFound)
            getAIGuide();
        getPossibleModels();
        if (!roleSyncFound)
            getRoleSync();
        if (!guideFollowFound)
            getGuideFollow();

        // Assign the guide's appearance by its role, called constantly in case of role updates
        if (guideFollowFound && avatarsFound) // If we are in the guide scene (GF) + have models for guide (AF), we can assign the guide
            assignGuideAvatarByRole();
    }

    private void assignGuideAvatarByRole()
    {
        // Grab role to see if it has changed
        int role = m_AIGuideScript.role;
        UpdateAvatar(role);

        // Set the multiplayer network role if we have the sync component
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
        if (human == null || robot == null || cane == null || dog == null || bird == null)
        {
            // Grab all possible models for guide avatars - go to parent to search for all models underneath
            if (GameObject.FindWithTag("Guide")) // If a guide has entered the scene, we can start looking for these models
            {
                human = GameObject.Find("Human Model").gameObject;
                robot = GameObject.Find("Robot Model").gameObject;
                cane = GameObject.Find("White Cane Model").gameObject;
                dog = GameObject.Find("Guide Dog Model").gameObject;
                bird = GameObject.Find("Bird Model").gameObject; //gameObject.transform.parent.transform.Find
            }
        }
        else
            avatarsFound = true;
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
        Debug.Log("Set a new role from network: " + role);
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

    private void UpdateAvatar(int role)
    {
        // Assign renderers by role, deactivate non-role, activate others
        if (role == 1) // Human
        {
            // Enable selected guide avatar
            EnableAllRenderers(human);
            DisableAllRenderers(robot);
            DisableAllRenderers(cane);
            DisableAllRenderers(dog);
            DisableAllRenderers(bird);
        }
        else if (role == 2) // Guide Dog
        {
            DisableAllRenderers(human);
            EnableAllRenderers(robot);
            DisableAllRenderers(cane);
            DisableAllRenderers(dog);
            DisableAllRenderers(bird);
        }
        else if (role == 3) // White Cane
        {
            DisableAllRenderers(human);
            DisableAllRenderers(robot);
            EnableAllRenderers(cane);
            DisableAllRenderers(dog);
            DisableAllRenderers(bird);
        }
        else if (role == 4) // Robot
        {
            DisableAllRenderers(human);
            DisableAllRenderers(robot);
            DisableAllRenderers(cane);
            EnableAllRenderers(dog);
            DisableAllRenderers(bird);
        }
        else if (role == 5) // Bird
        {
            DisableAllRenderers(human);
            DisableAllRenderers(robot);
            DisableAllRenderers(cane);
            DisableAllRenderers(dog);
            EnableAllRenderers(bird);
        }
        else if (role == 6) // Invisible Guide
        {
            DisableAllRenderers(human);
            DisableAllRenderers(robot);
            DisableAllRenderers(cane);
            DisableAllRenderers(dog);
            DisableAllRenderers(bird);
        } // END OF GUIDE ROLES ----- BEGINNING OF CONFEDERATE ROLES
    }
}
