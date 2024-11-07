using UnityEngine;
using UnityEngine.SceneManagement;

public class RandomObjectSpawner : MonoBehaviour
{
    public GameObject table; // Assign your table GameObject in the Inspector
    private GameObject spawnedObject;

    void Start()
    {
        // Call the SpawnRandomObject method to spawn an object on the table every ten seconds
        InvokeRepeating("SpawnRandomObject", 0f, 10f);
    }

    public void SpawnRandomObject()
    {
        Debug.Log("Spawning object");
        // Destroy any existing spawned object first to ensure only one object is present
        if (spawnedObject != null)
        {
            Destroy(spawnedObject);
        }

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

        // Get the position and rotation of the table
        Vector3 spawnPosition = table.transform.position + new Vector3(0, 1.0f, 0); // Adjust Y position as needed to place it on top of the table
        Quaternion spawnRotation = Quaternion.identity;

        // Instantiate the random prefab on top of the table
        spawnedObject = Instantiate(randomPrefab, spawnPosition, spawnRotation);
        // Add the component that checks for interaction with the object
    }
}