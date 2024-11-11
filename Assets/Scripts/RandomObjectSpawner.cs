using UnityEngine;
using UnityEngine.SceneManagement;

public class RandomObjectSpawner : MonoBehaviour
{
    // Variables to share with other scripts
    [HideInInspector]
    public GameObject spawnedObject;
    [HideInInspector]
    public GameObject spawnSource;
    public int timesObjectUnloaded = 0;

    // Scripts we need access to
    private ShortTaskController m_ShortTaskControllerScript;

    void Start()
    {
        // Assign spawnSource to be the GameObject this script is on
        spawnSource = this.gameObject;
        m_ShortTaskControllerScript = FindObjectOfType<ShortTaskController>();
    }

    private void Update()
    {
        CheckObjectUnloaded();
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

        float yHeight = 1f;
        if (spawnSource.name == "Bag") // For spawning objects out of the bags
            yHeight = 0.25f; // Sets the height lower so it appears the object is inside the bag

        // Get the position and rotation of the spawnSource
        Vector3 spawnPosition = spawnSource.transform.position + new Vector3(0, yHeight, 0); // Adjust Y position as needed to place it on top of the spawnSource
        Quaternion spawnRotation = Quaternion.identity;

        // Instantiate the random prefab on top of the spawnSource
        spawnedObject = Instantiate(randomPrefab, spawnPosition, spawnRotation);

        // Check if the object is being spawned for unloading or preparation task
        if (m_ShortTaskControllerScript.taskName == "Unloading")
        {
            spawnedObject.AddComponent<UnloadObject>();
            spawnedObject.GetComponent<UnloadObject>().AssignBag(spawnSource);
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
                    SpawnRandomObject();
                }
            }
        }
    }

    // Maybe have another one for CheckObjectPrepared()
}