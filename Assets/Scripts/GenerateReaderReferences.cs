using Newtonsoft.Json;
using OpenAI;
using OpenAI.Audio;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class GenerateReaderReferences : MonoBehaviour
{
    // Variables and resources for creating Reader References
    private int floorsLayer = 10;
    private int keyItemsLayer = 13;
    private int interactableLayer = 7;
    private GameObject readerReferencePrefab;
    private List<string> spawnableNames = new List<string>();
    public bool audioAssigned = false;

    // Variables for updating room config
    private List<string> objectNames = new List<string>();
    private string jsonFileName = "RoomDescriptions.json";
    private string jsonPath;
    private string sceneName;

    // Variables for storing audio and hashes with PlayHT
    private string resourcesPath; // Path to store audio in Resources/GeneratedAudio
    private string hashesFilePath; // Path to store hash values for the generated audio files
    private Dictionary<string, string> audioHashes = new Dictionary<string, string>(); // Hash cache to avoid duplicate requests
    private string audioFilePath;

    // Variables to load PlayHT credentials + alt-text
    public static OpenAIClient client { get; set; }
    [HideInInspector]
    public string playHTApiKey;
    [HideInInspector]
    public string playHTUserId;
    private string elevenLabsApiKey;
    private string openAIApiKey;
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    // Variable to handle which voice is used for generation
    public bool personalVoicesMode = false;

    void Start()
    {
        // Load relevant credentials from config file
        LoadConfig();

        // Open client
        client = new OpenAIClient(openAIApiKey);

        // Determine which version of audio generation is to be used
        personalVoicesMode = FindObjectOfType<SwitchTools>().personalVoicesOn;

        // Load the Reader Reference prefab from Resources
        readerReferencePrefab = Resources.Load<GameObject>("Screenreader/Reader Reference");
        resourcesPath = Path.Combine(Application.dataPath, "Resources/GeneratedAudio");
        hashesFilePath = Path.Combine(Application.persistentDataPath, "audio_hashes.txt");

        if (readerReferencePrefab == null)
        {
            Debug.LogError("Reader Reference prefab not found in Resources folder.");
            return;
        }

        // Find all object names of objects that can be spawned into the scene
        GetSpawnableNames();

        // Find all objects in both target layers
        AddReaderReferencesToLayer(floorsLayer);
        AddReaderReferencesToLayer(keyItemsLayer);
        AddReaderReferencesToLayer(interactableLayer);

        if (objectNames.Count > 0)
        {
            UpdateRoomDescriptions();
            LoadAudioHashes();

            // Start the audio generation process (need to do once before playing a scene)
            StartCoroutine(GenerateAudioFilesFromConfig());

            // Checks that generated audio files exist and assigns them if they do
            AssignGeneratedAudio();
        }
    }

    private void Update()
    {
        // Once audio has been assigned in one round for all static objects, check for dynamic interactables
        if (audioAssigned)
            CheckForNewInteractables();
    }

    private void GetSpawnableNames()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // Load all objects in Resources/Environments/{sceneName} folder
        Object[] resources = Resources.LoadAll($"Environments/{sceneName}", typeof(GameObject));

        // Loop through each object and add its name to the list
        foreach (Object obj in resources)
            spawnableNames.Add(obj.name);

        // Add this manually since the target is the only dynamic prefab that isn't environment specific
        spawnableNames.Add("Target Destination");
    }

    private void CheckForNewInteractables()
    {
        // Check for all interactable objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        // Loop through each object
        foreach (GameObject obj in allObjects)
        {
            string cleanedName = obj.name.Replace("(Clone)", "").Trim();
            if ((obj.layer == 7 || obj.layer == 13) && spawnableNames.Contains(cleanedName)) // If there's an interactable/key item (for target) that matches a name from our spawnable objects
            {
                // Add Reader Reference if one does not exist on the object
                GameObject readerReference = FindChildWithTag(obj, "Reader Reference");
                if (readerReference == null)
                    readerReference = Instantiate(readerReferencePrefab, obj.transform);

                // Search all generated audio files
                string[] audioFiles = Directory.GetFiles(resourcesPath, "*.mp3");
                if (audioFiles.Length > 0)
                {
                    foreach (string filePath in audioFiles)
                    {
                        // Check names of every audio file - if one matches the new interactable, assign it
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        if (fileName == cleanedName)
                            StartCoroutine(LoadAudioClipFromFile(filePath, fileName)); // Load the file as an AudioClip to be assigned to a source
                    }
                }
            }
        }
    }

    // Method to load an AudioClip from a file path
    private IEnumerator LoadAudioClipFromFile(string path, string fileName)
    {
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip("file:///" + path, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result == UnityWebRequest.Result.Success)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);

                GameObject[] gameObjects = FindObjectsOfType<GameObject>();

                foreach (GameObject currentObject in gameObjects)
                {
                    string cleanedName = currentObject.name.Replace("(Clone)", "").Trim(); // Clean names just in case of dynamic objects (clones)
                    if (cleanedName == fileName)
                    {
                        //Debug.Log("Found GameObject named " + cleanedName);
                        // Find the Reader Reference child and be sure to grab its AudioSource
                        GameObject readerReference = FindChildWithTag(currentObject, "Reader Reference");
                        if (readerReference != null)
                        {
                            AudioSource audioSource = readerReference.gameObject.GetComponentInChildren<AudioSource>();

                            if (audioSource.clip == null)
                            {
                                audioSource.clip = audioClip;
                                Debug.Log($"Assigned audio file {Path.GetFileName(path)} to GameObject {cleanedName}");
                                
                                // If we have already gone through our first round of audio assignment,
                                // Any object being assigned is a dynamic interactable
                                if (audioAssigned)
                                    FindObjectOfType<VRScreenreader>().GetReaderReferences(); // Update screenreader's ref dict with new dynamic interactable
                            }
                        }
                        else
                            Debug.Log($"GameObject {cleanedName} did not have a Reader Reference");
                    }
                }
            }
            else
            {
                Debug.LogError($"Failed to load AudioClip for file: {path}. Error: {audioRequest.error}");
            }
        }
    }

    private GameObject FindChildWithTag(GameObject parent, string tag)
    {
        foreach (Transform child in parent.transform)
        {
            if (child.CompareTag(tag))
                return child.gameObject;
        }

        return null; // Return null if no child is found
    }

    private void AssignGeneratedAudio()
    {
        // Define the directory path where audio files are stored
        string audioDirectoryPath = resourcesPath;

        //Debug.Log("Audio directory is " + audioDirectoryPath);

        // Check if the directory exists and contains audio files
        if (Directory.Exists(audioDirectoryPath))
        {
            string[] audioFiles = Directory.GetFiles(audioDirectoryPath, "*.mp3");

            if (audioFiles.Length > 0)
            {
                Debug.Log("Audio files exist in this directory.");

                foreach (string filePath in audioFiles)
                {
                    // Extract the file name without extension
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Load the file as an AudioClip to be assigned to a source
                    StartCoroutine(LoadAudioClipFromFile(filePath, fileName));
                }

                audioAssigned = true;
            }
            else
                Debug.Log("No audio files exist in this directory.");
        }
        else
            Debug.Log("Audio directory does not exist.");
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
        sceneName = SceneManager.GetActiveScene().name;
        string newEntryKey = sceneName + "_Objects";
        HashSet<string> uniqueObjectNames = new HashSet<string>(objectNames);
        string newEntryValue = string.Join(", ", uniqueObjectNames);

        // Load the current JSON file
        Dictionary<string, string> roomDescriptions = new Dictionary<string, string>();

        jsonPath = Path.Combine(Application.dataPath, "Resources", jsonFileName);
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

    // Method to read the JSON file and trigger PlayHT generation for the current scene
    public IEnumerator GenerateAudioFilesFromConfig()
    {
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
        }

        // Load the JSON data
        string jsonContent = File.ReadAllText(jsonPath);
        Dictionary<string, string> descriptionData = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
        string sceneDescriptionKey = sceneName;

        // Get the description and object list for the current scene
        if (descriptionData.ContainsKey(sceneName) && descriptionData.ContainsKey(sceneName + "_Objects"))
        {
            yield return StartCoroutine(GenerateAudioForScene(sceneName, descriptionData[sceneDescriptionKey]));
        }
    }

    private IEnumerator GenerateAudioForScene(string sceneName, string descriptions)
    {
        string[] descriptionList = descriptions.Split('|');

        for (int i = 0; i < descriptionList.Length; i++)
        {
            string description = descriptionList[i].Trim();

            string descriptionHash = GenerateHash(description);

            // Pull the object name from the description we're loading
            string[] descriptionParts = description.Split(':');
            if (descriptionParts.Length > 0)
            {
                string objectName = descriptionParts[0].Trim();

                // Generate the audio file path
                string audioFilePath = Path.Combine(resourcesPath, $"{objectName}.mp3");

                // Check if the audio already exists
                if (File.Exists(audioFilePath) && audioHashes.ContainsKey(objectName) && audioHashes[objectName] == descriptionHash)
                {
                    Debug.Log($"Audio for {objectName} already exists, skipping generation.");
                    continue; // Skip the request to PlayHT
                }

                // Generate the audio if not cached
                if (personalVoicesMode)
                    yield return StartCoroutine(GenerateAndSavePersonalAudio(objectName, description, descriptionHash));
                else
                    yield return StartCoroutine(GenerateAndSaveAudio(objectName, description, descriptionHash));
            }
            else
            {
                // It is the floor object, Ex. Near Chopping Station - the name of the object should be the description
                string objectName = description;

                // Generate the audio file path
                string audioFilePath = Path.Combine(resourcesPath, $"{objectName}.mp3");

                // Check if the audio already exists
                if (File.Exists(audioFilePath) && audioHashes.ContainsKey(objectName) && audioHashes[objectName] == descriptionHash)
                {
                    Debug.Log($"Audio for {objectName} already exists, skipping generation.");
                    continue; // Skip the request to PlayHT
                }

                // Generate the audio if not cached
                if (personalVoicesMode)
                    yield return StartCoroutine(GenerateAndSavePersonalAudio(objectName, description, descriptionHash));
                else
                    yield return StartCoroutine(GenerateAndSaveAudio(objectName, description, descriptionHash));
            }
        }
    }

    // Version that uses OpenAI default voices
    private IEnumerator GenerateAndSaveAudio(string objectName, string description, string descriptionHash)
    {
        Debug.Log("Reaching save audio");
        audioFilePath = Path.Combine(resourcesPath, $"{objectName}.mp3");

        var ttsRequest = new SpeechRequest(description, model: "tts-1", voice: SpeechVoice.Alloy, responseFormat: SpeechResponseFormat.MP3);

        var task = client.AudioEndpoint.CreateSpeechAsync(ttsRequest);

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError($"TTS Task failed: {task.Exception}");
            yield break;
        }

        // Task.Result is where the actual path string lives
        var result = task.Result;
        string actualPath = result.ToString();
        // Clean up the string to get a valid Windows path
        string clipPath = actualPath.Replace("file://", "").Split(',')[0].Trim('(', ' ');

        if (File.Exists(clipPath))
        {
            byte[] audioData = File.ReadAllBytes(clipPath);
            File.WriteAllBytes(audioFilePath, audioData);
            Debug.Log($"Audio for {objectName} saved at {audioFilePath}");

            audioHashes[objectName] = descriptionHash;
            SaveAudioHashes();

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        else
        {
            Debug.LogError("OpenAI returned success, but the temp file path does not exist: " + clipPath);
            yield break;
        }
    }

    private IEnumerator GenerateAndSavePersonalAudio(string objectName, string description, string descriptionHash)
    {
        string voice = "SAz9YHcvj6GT2YYXdXww"; // Default voice, Human
        audioFilePath = Path.Combine(resourcesPath, $"{objectName}.mp3");

        var playHTData = "{\"voice\":\"" + voice + "\", \"text\":\"" + description + "\"}";

        var payloadObj = new
        {
            text = description,
            model_id = "eleven_turbo_v2",
            voice_settings = new
            {
                stability = 0.5f,
                similarity_boost = 0.7f,
                style = 0.02f,
                use_speaker_boost = true
            }
        };

        string finalUrl = $"https://api.elevenlabs.io/v1/text-to-speech/{voice}/";
        string jsonBody = JsonConvert.SerializeObject(payloadObj);

        using (UnityWebRequest elevenLabsRequest = new UnityWebRequest(finalUrl, "POST"))
        {
            elevenLabsRequest.method = UnityWebRequest.kHttpVerbPOST;

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            elevenLabsRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            elevenLabsRequest.downloadHandler = new DownloadHandlerAudioClip(finalUrl, AudioType.MPEG);

            elevenLabsRequest.SetRequestHeader("Content-Type", "application/json");
            elevenLabsRequest.SetRequestHeader("xi-api-key", elevenLabsApiKey); // Use 'xi-api-key', NOT 'Authorization'
            elevenLabsRequest.SetRequestHeader("Accept", "audio/mpeg");

            Debug.Log($"Sending a request to ElevenLabs: {jsonBody}");

            yield return elevenLabsRequest.SendWebRequest();

            if (elevenLabsRequest.result == UnityWebRequest.Result.Success)
            {
                byte[] audioData = elevenLabsRequest.downloadHandler.data;
                Debug.Log($"Received audio data of size: {audioData.Length} bytes");
                File.WriteAllBytes(audioFilePath, audioData);
                Debug.Log($"Audio for {objectName} saved at {audioFilePath}");

                // Save the hash for future reference
                audioHashes[objectName] = descriptionHash;
                SaveAudioHashes();

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            else
            {
                Debug.LogError("Error calling ElevenLabs: " + elevenLabsRequest.error);
                //Debug.LogError("Response Text: " + elevenLabsRequest.downloadHandler.text);

                Debug.LogError("Error Code: " + elevenLabsRequest.responseCode);
                if (elevenLabsRequest.downloadHandler.data != null)
                {
                    string errorJson = Encoding.UTF8.GetString(elevenLabsRequest.downloadHandler.data);
                    Debug.LogError("ElevenLabs Detailed Error: " + errorJson);
                }
                yield break;
            }
        }
    }

    private void SaveAudioHashes()
    {
        using (StreamWriter writer = new StreamWriter(hashesFilePath))
        {
            foreach (var pair in audioHashes)
            {
                writer.WriteLine($"{pair.Key}:{pair.Value}");
            }
        }
    }

    // Generate a hash for the text description to ensure we don't send the files to PlayHT multiple times and spend tons of credits
    private string GenerateHash(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder hashString = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                hashString.Append(b.ToString("x2"));
            }
            return hashString.ToString();
        }
    }

    // Load saved audio hashes from a file
    private void LoadAudioHashes()
    {
        if (File.Exists(hashesFilePath))
        {
            string[] lines = File.ReadAllLines(hashesFilePath);
            foreach (string line in lines)
            {
                string[] parts = line.Split(':');
                if (parts.Length == 2)
                {
                    string objectName = parts[0];
                    string hash = parts[1];
                    audioHashes[objectName] = hash;
                }
            }
        }
    }
    private void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(configFileName);
        if (configAsset != null)
        {
            // Parse the JSON data from config.json and assign apiKey values accordingly
            ConfigData configData = JsonUtility.FromJson<ConfigData>(configAsset.text);
            playHTApiKey = configData.PlayHTAPIKey;
            playHTUserId = configData.PlayHTUserID;
            elevenLabsApiKey = configData.ElevenLabsAPIKey;
            openAIApiKey = configData.APIKey;
        }
        else
        {
            Debug.LogError("Config file not found in Resources folder: " + configFileName);
        }
    }

    private class ConfigData
    {
        public string APIKey;
        public string PlayHTAPIKey;
        public string PlayHTUserID;
        public string ElevenLabsAPIKey;
    }
}
