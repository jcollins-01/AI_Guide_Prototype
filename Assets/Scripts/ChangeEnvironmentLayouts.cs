using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeEnvironmentLayouts : MonoBehaviour
{
    // Variables to hold our ordered layouts
    public bool roomLayoutOne = false;
    public bool roomLayoutTwo = false;
    public bool roomLayoutThree = false;

    public bool tableLayoutOne = false;
    public bool tableLayoutTwo = false;
    public bool tableLayoutThree = false;

    // Hold the objects we manipulate as we change layouts
    private GameObject[] availableLayouts = new GameObject[6]; // 1-3 = room layouts, 4-6 = table layouts

    // Start is called before the first frame update
    void Start()
    {
        CheckCurrentScene();
    }

    private void CheckCurrentScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        switch (currentSceneName)
        {
            case "Kitchen":
                availableLayouts[0] = GameObject.Find("Kitchen"); // Final room
                availableLayouts[1] = GameObject.Find("Kitchen 2");
                availableLayouts[2] = GameObject.Find("Kitchen 3");
                //availableLayouts[3] = GameObject.Find("Kitchen Prep Table"); // Final table
                //availableLayouts[4] = GameObject.Find("Kitchen Prep Table 2");
                //availableLayouts[5] = GameObject.Find("Kitchen Prep Table 3");
                break;
            case "Pharmacy":
                availableLayouts[0] = GameObject.Find("Pharmacy"); // Final room
                availableLayouts[1] = GameObject.Find("Pharmacy 2");
                availableLayouts[2] = GameObject.Find("Pharmacy 3");
                availableLayouts[3] = GameObject.Find("Pharmacy Prep Table"); // Final table
                availableLayouts[4] = GameObject.Find("Pharmacy Prep Table 2");
                availableLayouts[5] = GameObject.Find("Pharmacy Prep Table 3");
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {
        availableLayouts[0].SetActive(roomLayoutOne);
        availableLayouts[1].SetActive(roomLayoutTwo);
        availableLayouts[2].SetActive(roomLayoutThree);
    }
}
