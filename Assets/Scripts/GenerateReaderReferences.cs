using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GenerateReaderReferences : MonoBehaviour
{
    // Variables and resources for creating Reader References
    private int floorsLayer = 10;
    private int keyItemsLayer = 13;
    private GameObject readerReferencePrefab;

    // Variables for updating room config
    private List<string> objectNames = new List<string>();
    private string jsonFileName = "RoomDescriptions.json";

    // Variables to load PlayHT credentials + alt-text
    [HideInInspector]
    public string playHTApiKey = "be0df08ac90e4fefb83a20c4325f6e46";
    [HideInInspector]
    public string playHTUserId = "tzQHNKayacM2E5DkkjMScWkPSy32";
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    void Start()
    {
        // Load the Reader Reference prefab from Resources
        readerReferencePrefab = Resources.Load<GameObject>("Screenreader/Reader Reference");

        if (readerReferencePrefab == null)
        {
            Debug.LogError("Reader Reference prefab not found in Resources folder.");
            return;
        }

        // Find all objects in both target layers
        AddReaderReferencesToLayer(floorsLayer);
        AddReaderReferencesToLayer(keyItemsLayer);

        if (objectNames.Count > 0)
            UpdateRoomDescriptions();
    }

    void AddReaderReferencesToLayer(int targetLayer)
    {
        // Get all objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        // Loop through each object
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == targetLayer)
            {
                GameObject readerReferenceInstance = Instantiate(readerReferencePrefab, obj.transform); // Instantiate the Reader Reference as a child of each object
                objectNames.Add(obj.name); // Add the objects name for the list of key objects in our config file
            }
        }
    }

    void UpdateRoomDescriptions()
    {
        // Get current scene name
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string newEntryKey = sceneName + "_Objects";
        string newEntryValue = string.Join(", ", objectNames);

        // Load the current JSON file
        Dictionary<string, string> roomDescriptions = new Dictionary<string, string>();

        string jsonPath = Path.Combine(Application.dataPath, "Resources", jsonFileName);
        if (File.Exists(jsonPath))
        {
            string jsonContent = File.ReadAllText(jsonPath);
            // Deserialize existing content into the dictionary
            roomDescriptions = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
        }

        // Add or update the entry for the current scene
        roomDescriptions[newEntryKey] = newEntryValue;

        // Convert back to JSON format and save the file
        string updatedJson = JsonConvert.SerializeObject(roomDescriptions, Formatting.Indented);
        File.WriteAllText(jsonPath, updatedJson);

        Debug.Log($"Updated {newEntryKey} in RoomDescriptions.json");
    }
}
