using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrepareObject : MonoBehaviour
{
    public bool playerPreparedObject = false;
   
    private GameObject table;
    private GameObject prepTool;

    // Variables for handling preparation zone and timing
    public float requiredHoldTime = 3.0f; // Time in seconds the button must be held
    private float holdTime = 0.0f;
    private bool isToolNearby = false;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("A new object has been spawned for preparation");

        // Add necessary components for grabbing and detecting grip button with object
        this.gameObject.layer = 7; // Make the object Interactable if it isn't already
        this.gameObject.AddComponent<Rigidbody>();
        this.gameObject.AddComponent<BoxCollider>(); // To ensure it doesn't fall through the table, can comment to test if unloading works

        // Add a collider to the table for physics
        if (table != null)
        {
            if (!table.GetComponent<Collider>())
                table.AddComponent<BoxCollider>();
        }

        // Assign prepTool based on environment
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        switch(sceneName)
        {
            case "Kitchen":
                prepTool = GameObject.Find("Knife");
                break;
            case "Alien Spaceship Repair Shop":
                prepTool = GameObject.Find("Sonic Screwdriver");
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Need to press and hold primary button for three seconds once knife is pressed against object to prepare it
    }

    public void AssignTable(GameObject passedTable)
    {
        table = passedTable;
    }
}
