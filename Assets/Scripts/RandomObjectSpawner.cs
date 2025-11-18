using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class RandomObjectSpawner : MonoBehaviour
{
    // Variables to share with other scripts
    [HideInInspector]
    public GameObject spawnedObject;
    [HideInInspector]
    public GameObject spawnSource;
    public int timesObjectUnloaded = 0;
    public int timesObjectPrepared = 0;

    // Components for audio
    private AudioSource audioSource;
    private AudioClip unloaded;
    private AudioClip preparing;

    // Scripts we need access to
    private ShortTaskController m_ShortTaskControllerScript;

    void Start()
    {
        // Assign spawnSource to be the GameObject this script is on
        spawnSource = this.gameObject;
        Debug.Log("Spawn source is " +  this.gameObject.name);
        m_ShortTaskControllerScript = FindObjectOfType<ShortTaskController>();

        // Assign audio components for indicating an object has been unloaded / prepared
        audioSource = this.gameObject.AddComponent<AudioSource>();
        unloaded = Resources.Load<AudioClip>("Audio/completion"); 
        string sceneName = SceneManager.GetActiveScene().name;
        switch (sceneName)
        {
            case "Kitchen":
                preparing = Resources.Load<AudioClip>("Audio/chop");
                break;
            case "Alien Spaceship Repair Shop":
                preparing = Resources.Load<AudioClip>("Audio/repair");
                break;
            case "Flower Shop":
                preparing = Resources.Load<AudioClip>("Audio/water-walk");
                break;
        }
    }

    private void Update()
    {
        CheckObjectUnloaded();
        CheckObjectPrepared();
    }

    public void SpawnRandomObject()
    {
        Debug.Log("Spawning object");

        // Extra check since the first time this script is added, this function is called, occasionally before Start runs
        if (spawnSource == null)
            spawnSource = this.gameObject;
        if (m_ShortTaskControllerScript == null)
            m_ShortTaskControllerScript = FindObjectOfType<ShortTaskController>();

        // Get the current scene name
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Load all prefabs from Resources/Environment/{currentSceneName}
        Object[] objects = Resources.LoadAll($"Environments/{currentSceneName}", typeof(GameObject));

        // Check if any objects were found
        if (objects.Length == 0)
        {
            Debug.LogWarning($"No objects found in Resources/Environments/{currentSceneName}");
            return;
        }

        // Select a random prefab from the loaded objects
        GameObject randomPrefab = (GameObject)objects[Random.Range(0, objects.Length)];

        // Get the position and rotation of the spawnSource
        Vector3 spawnPosition = spawnSource.transform.position + new Vector3(0, 1.5f, 0); // Can adjust Y position to make it fall from a higher place above spawnSource
        Quaternion spawnRotation = Quaternion.identity;

        // Instantiate the random prefab on top of the spawnSource
        spawnedObject = Instantiate(randomPrefab, spawnPosition, spawnRotation);

        // Check if the object is being spawned for unloading or preparation task
        if (m_ShortTaskControllerScript.taskName == "Unloading")
        {
            spawnedObject.AddComponent<UnloadObject>();
            spawnedObject.GetComponent<UnloadObject>().AssignBag(spawnSource);
        }
        else // Task is Preparation
        {
            spawnedObject.AddComponent<PrepareObject>();
            spawnedObject.GetComponent<PrepareObject>().AssignTable(spawnSource);
            // Destroy the grabbable component so the object can't be grabbed accidentally
            Destroy(spawnedObject.GetComponentInChildren<XRGrabInteractable>());
        }
    }

    void CheckObjectUnloaded()
    {
        // If the object has an UnloadObject component added to it by the task controller
        if (spawnedObject != null)
        {
            if (spawnedObject.GetComponent<UnloadObject>())
            {
                if (spawnedObject.GetComponent<UnloadObject>().playerUnloadedObject)
                {
                    Debug.Log("Player unloaded object - destroying object and spawning a new one");
                    Destroy(spawnedObject); // Destroy object to ensure only one exists at a given time
                    timesObjectUnloaded++;
                    audioSource.clip = unloaded;
                    audioSource.Play();
                    SpawnRandomObject();
                }
            }
        }
    }

    void CheckObjectPrepared()
    {
        // If the object has a PrepareObject component added to it by the task controller
        if (spawnedObject != null)
        {
            if (spawnedObject.GetComponent<PrepareObject>())
            {
                // If player is mid-preparation
                if (spawnedObject.GetComponent<PrepareObject>().playerMidPreparation)
                {
                    Debug.Log("Player starting preparing object");
                    if (!audioSource.isPlaying)
                    {
                        audioSource.clip = preparing;
                        audioSource.Play();
                    }
                }
                
                // If player finishes preparation
                if (spawnedObject.GetComponent<PrepareObject>().playerPreparedObject)
                {
                    Debug.Log("Player prepared object - destroying object and spawning a new one");
                    Destroy(spawnedObject); // Destroy object to ensure only one exists at a given time
                    timesObjectPrepared++;
                    audioSource.clip = unloaded;
                    audioSource.Play();
                    SpawnRandomObject();
                }
            }
        }
    }
}