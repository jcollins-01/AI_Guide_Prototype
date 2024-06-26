using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeAvatarRuntime : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private AIGuide m_AIGuideScript;

    // Variable to assign publicly in the editor; set in RoomManager
    public bool isPlayer;

    private void Start()
    {
        // Find and load appropriate resources
        m_AIGuideScript = GetComponent<AIGuide>();
    }

    // Update is called once per frame
    void Update()
    {
        // If playing a confederate, assign avatars randomly
        if (!isPlayer)
            pickAvatarAtRandomForAll();
        else // Else, assign the guide by a role, leave participant as a default avatar
            assignAvatarByRole();
    }

    private void assignAvatarByRole()
    {
        int role = m_AIGuideScript.role;

        // Maybe do a check to ensure we have Shared Movement before continuing here
        GameObject guideAvatar = GetComponent<SharedMovement>().theGuide;

        // Grab all possible models for avatars
        GameObject model1 = guideAvatar.transform.Find("Model 1").gameObject;
        GameObject model2 = guideAvatar.transform.Find("Model 2").gameObject;
        GameObject model3 = guideAvatar.transform.Find("Model 3").gameObject;
        GameObject model4 = guideAvatar.transform.Find("Model 4").gameObject;
        GameObject model5 = guideAvatar.transform.Find("Model 5").gameObject;

        // Assign renderers by role, deactivate non-role, activate others
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
}
