using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Normal.Realtime;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;

public class OpenAIQueries : MonoBehaviour
{
    // OpenAI variables
    public static OpenAIClient client { get; set; }
    // OpenAI API key
    [HideInInspector]
    public string apiKey;
    [HideInInspector]
    public string playHTApiKey = "768d1450572a41babc9dbf29c64482e9";
    [HideInInspector]
    public string playHTUserId = "URigAsmWLgYZQBsgMfSImC4rK8d2";
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    // Variables to hold scripts we need access to
    private CameraSystem m_CameraSystemScript;
    private GuideAudioSync m_GuideAudioSync;
    private AIGuide m_AIGuideScript;
    public RealtimeAvatarVoice _avatarVoice;

    // Variables to construct OpenAI queries
    private StringBuilder textBuffer = new StringBuilder(); // Buffer to accumulate GPT response chunks before sending to PlayHT
    private const int chunkSizeThreshold = 200;  // Adjust this size to control how much text to send at once
    private bool isPlayingAudio = false;
    private bool isProcessingAudioChunk = false;
    private Queue<string> chunkQueue = new Queue<string>();

    private string objectNames;
    public List<string> roles = new List<string>
    {
        "warm, friendly, but still professional sighted guide",
        "formal and assertive assistant, who talks like a robot",
        "computer-like, succinct assistant, who gives the straight facts",
        "very friendly, excited companion, who is eager to please who you're talking to",
        "wise, old-fashioned, slightly Shakespearean-sounding mentor", //posh
        "gentle, sweet, soft-spoken assistant who gives very brief statements, as though slipping in words to someone without trying to interrupt what they're doing"
    };
    [HideInInspector]
    public string contextClassification = "The two photos you are seeing are two views of a video game. One of these photos is the bird's eye view of the entire scene. " +
        "The other photo is the player's current perspective and what they are currently looking at in the scene." +
        "The player is going to ask you questions about the contents in these photos.";
    [HideInInspector]
    public string memoClassifications = "Limit your reply to 150 words or less - DO NOT GO OVER THIS WORD LIMIT. Don't mention the two photos when replying; speak to the player as though you are in the game next to them.";
    [HideInInspector]
    public string objectClassifications = ""; // Manual descriptions of key objects: left blank to be dynamically set by RoomDescriptions file
    [HideInInspector]
    public string queryClassifications // Variable set up so that it initializes itself with the most recent values for objectNames
    {
        get
        {//succintly summarize?
            return "Make sure to respond to all the player's questions, including interpersonal ones like how you are, what your name is, what you want to do, etc.  " +
                   "If the player seems like they want to describe the entire scene, then succintly summarize the scene as though you are helping the player understand the game they are in. " +
                   "If the player seems like they want to describe a particular object in the scene, describe the object in the image they are referring to. " +
                   "If it seems like they want to go to a particular object in the scene, tell me only the name of the object in the image they would be referring to, " +
                   "plus the word 'teleport' after a comma if it seems like they want to teleport to the object " +
                   "and 'guide' after a comma if they don't specify teleportation." +
                   "ONLY GIVE ME AN OBJECT NAME FROM THIS LIST: " + objectNames + "." +
                   "Only do this if you're sure they want to go to an object." +
                   "If it seems like they want to add a sound effect to a particular object, tell me only the name of the object in the image they would be referring to, " +
                   "plus the word 'modify' after a comma." +
                   "ONLY GIVE ME AN OBJECT NAME FROM THIS LIST: " + objectNames + "." +
                   "Only do this if you're sure they want to add a sound." +
                   "If the player asks for help in finding a particular object, give them directions for how they might want to orient themselves to face the object, as though the player is blind and cannot see any visual markers. " + 
                   "If the question the user asks doesn't fit into any of the above categories, respond to them to the best of your ability. Again, LIMIT YOUR REPLY TO 150 WORDS OR LESS - THIS IS IMPORTANT.";
        }
    }

    // OpenAI audio, text message, result variables
    [HideInInspector]
    public string text;
    [HideInInspector]
    public GameObject targetForGuidance;
    //[HideInInspector]
    public string modeOfTransportation;
    [HideInInspector]
    public GameObject targetForModification;
    //[HideInInspector]
    public string modeOfModification;

    public string query;
    public string role;
    public AudioSource audioSource;
    public AudioClip guideVoice;

    // Monitoring bools
    [HideInInspector]
    public bool recordingInProgress = false;
    [HideInInspector]
    public bool whisperCompleted = false;
    [HideInInspector]
    public bool completionCompleted = false;
    [HideInInspector]
    public bool alloyCompleted = false;

    private void Start()
    {
        // Find and load appropriate resources
        m_AIGuideScript = GetComponent<AIGuide>();
        audioSource = GameObject.Find("Human Model").GetComponent<AudioSource>(); // Ensure we grab the guide audio source for OpenAI, not PlayAudio
        LoadConfig();
        LoadRoomDescriptions();
        //Debug.Log("OpenAI is ready to be queried.");

        // Create an instance of the OpenAI client
        client = new OpenAIClient(apiKey);
    }

    private void Update()
    {
        // Calls until the camera system and audio sync scripts are assigned
        getCameraSystem();
        getAudioSync();
        getAvatarVoice();

        // Calls continously to check for a role change
        getGuideRole();
    }

    private void getAvatarVoice()
    {
        if (_avatarVoice == null)
            _avatarVoice = GameObject.FindWithTag("Player").GetComponentInChildren<RealtimeAvatarVoice>();
    }

    private void getGuideRole()
    {
        // Do checks to ensure role has been initialized with its most recent values so we don't go out of bounds
        int index = m_AIGuideScript.role - 1;

        if (index < 0 || index >= roles.Count)
            return;
        
        // The role becomes the string value contained at the index we sent over from AIGuide
        role = roles[index];
    }

    private void getCameraSystem()
    {
        if (m_CameraSystemScript == null)
            m_CameraSystemScript = FindObjectOfType<CameraSystem>();
    }

    public void CaptureAudio()
    {
        // Resets all monitoring variables to mark the start of a new query
        whisperCompleted = false;
        completionCompleted = false;
        alloyCompleted = false;

        // Records 10 secs by default
        if (!recordingInProgress)
        {
            recordingInProgress = true;
            audioSource.mute = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1;
            audioSource.clip = Microphone.Start(Microphone.devices[0], false, 10, 44100);
            Debug.Log("Recording audio");
        }

        if (audioSource == null)
            Debug.Log("microphone not detected, audio not recorded");
    }

    public async Task<string> CallWhisper(AudioClip audioClip)
    {
        // Rebuild the audio stream in Normcore to send microphone data again
        if (_avatarVoice != null)
            _avatarVoice._rebuildAudioStream = true;

        Debug.Log("Reached Call Whisper");
        var transcriptionRequest = new OpenAI.Audio.AudioTranscriptionRequest(audioClip, "whisper-1");

        string output = "N/A";
        try
        {
            var transcriptionResponse = await client.AudioEndpoint.CreateTranscriptionAsync(transcriptionRequest);
            output = transcriptionResponse.ToString();
            Debug.Log("Response from GPT-4: " + output);
            query = output;
            whisperCompleted = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Exception in CallWhisper:\n" + e);
        }
        return output;
    }

    public async Task CallChatGPTAndStreamAudioCompletions(string prompt)
    {
        // Prepare the chat request body for API
        var content = new List<Content>
        {
            new Content(ContentType.Text, prompt),
            new Content(ContentType.ImageUrl, m_CameraSystemScript.birdsEyeImageLink),
            new Content(ContentType.ImageUrl, m_CameraSystemScript.viewpointImageLink)
        };

        var chatPrompts = new List<Message>
        {
            new Message(Role.User, content)
        };

        var chatRequest = new ChatRequest(chatPrompts, model: "gpt-4-vision-preview", maxTokens: 300);

        // Use StreamCompletionAsync to stream the responses
        try
        {
            await client.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                // Check if the partial response has choices and if the content is not null
                if (partialResponse.Choices != null && partialResponse.Choices.Count > 0)
                {
                    var delta = partialResponse.Choices[0].Delta;

                    // Make sure the delta and content are not null
                    if (delta != null && delta.Content != null)
                    {
                        // Serialize the full partial response to JSON
                        var jsonResponse = JsonConvert.SerializeObject(partialResponse);
                        // Pass the JSON response to the audio streaming coroutine
                        StartCoroutine(StreamChatGptResponseToAudio(jsonResponse));
                    }
                }
            });
            Debug.Log("Finished streaming GPT response.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error in streaming GPT-4 response: " + e.Message);
        }
    }

    // Coroutine to aggregate the GPT response and stream text to PlayHT in chunks
    private IEnumerator StreamChatGptResponseToAudio(string jsonResponse)
    {
        //Debug.Log("Received JSON response: " + jsonResponse);
        // Parse the JSON response
        var jsonObject = JObject.Parse(jsonResponse);
        var content = jsonObject["choices"]?[0]?["delta"]?["content"]?.ToString();
        var finishReason = jsonObject["choices"]?[0]?["finish_reason"]?.ToString();

        // Check if content is null or empty
        if (!string.IsNullOrEmpty(content))
        {
            //Debug.Log("content added " + content);
            textBuffer.Append(content); // Add the extracted content to the buffer

            // If the buffer reaches a certain size, send it to PlayHT for real-time audio conversion
            if (textBuffer.Length >= chunkSizeThreshold || content.EndsWith(".") || content.EndsWith("!"))
            {
                string textToSend = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(textToSend))
                {
                    Debug.Log("Queuing chunk for PlayHT: " + textToSend);
                    chunkQueue.Enqueue(textToSend);  // Add the chunk to the queue
                    textBuffer.Clear();  // Clear the buffer after queuing
                }
            }
        }

        // Handle the case where finish_reason is "stop" and the message finished without meeting one of the above requirements
        if (finishReason == "stop")
        {
            if (textBuffer.Length > 0) // Check if there's any remaining content in the buffer to process
            {
                string remainingText = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(remainingText))
                {
                    Debug.Log("Queuing final chunk for PlayHT: " + CheckForGuidanceOrModification(remainingText));
                    chunkQueue.Enqueue(CheckForGuidanceOrModification(remainingText));  // Add the final chunk to the queue
                    textBuffer.Clear();  // Clear the buffer after queuing
                }
            }
        }

        // Start processing the chunks in the queue (if not already processing)
        if (!isProcessingAudioChunk && chunkQueue.Count > 0)
        {
            Debug.Log("Starting chunk processing...");
            StartCoroutine(ProcessChunkQueue());
        }
        yield return null; // Yield to keep the coroutine responsive
    }

    // Coroutine to process the chunk queue one by one
    private IEnumerator ProcessChunkQueue()
    {
        while (chunkQueue.Count > 0)
        {
            // Wait until the previous chunk is processed before sending the next one
            isProcessingAudioChunk = true;

            string textToSend = chunkQueue.Dequeue();  // Get the next chunk from the queue
            Debug.Log("Sending chunk to PlayHT: " + textToSend);
            ShareResponseBasedOnRole(textToSend);  // Process based on role if necessary

            // Call the coroutine to send text to PlayHT and convert it to audio
            yield return StartCoroutine(StreamTextToPlayHT(textToSend));

            isProcessingAudioChunk = false;  // Mark the chunk processing as complete

            // Wait for a short delay between chunks (optional)
            yield return new WaitForSeconds(0.1f);  // Adjust the delay as needed
        }
        Debug.Log("All chunks processed.");
    }

    // Coroutine to send a chunk of text to PlayHT for real-time audio conversion
    private IEnumerator StreamTextToPlayHT(string textChunk)
    {
        Debug.Log("Started coroutine for audio");
        string playHTUrl = "https://play.ht/api/v2/tts/stream";
        string voice = "s3://voice-cloning-zero-shot/a59cb96d-bba8-4e24-81f2-e60b888a0275/charlottenarrativesaad/manifest.json"; // Default voice, Human

        // Customize the voice as per the role
        if (m_AIGuideScript.role == 2) voice = "s3://voice-cloning-zero-shot/b41d1a8c-2c99-4403-8262-5808bc67c3e0/bentonsaad/manifest.json";
        else if (m_AIGuideScript.role == 3) voice = "s3://voice-cloning-zero-shot/d82d246c-148b-457f-9668-37b789520891/adolfosaad/manifest.json";
        else if (m_AIGuideScript.role == 4) voice = "s3://voice-cloning-zero-shot/f6594c50-e59b-492c-bac2-047d57f8bdd8/susanadvertisingsaad/manifest.json";
        else if (m_AIGuideScript.role == 5) voice = "s3://voice-cloning-zero-shot/3a831d1f-2183-49de-b6d8-33f16b2e9867/dylansaad/manifest.json";
        else if (m_AIGuideScript.role == 6) voice = "s3://voice-cloning-zero-shot/1afba232-fae0-4b69-9675-7f1aac69349f/delilahsaad/manifest.json";

        var playHTData = "{\"voice\":\"" + voice + "\", \"text\":\"" + textChunk + "\"}";

        using (UnityWebRequest playHTRequest = new UnityWebRequest(playHTUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(playHTData);
            playHTRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            playHTRequest.downloadHandler = new DownloadHandlerBuffer();
            playHTRequest.SetRequestHeader("Content-Type", "application/json");
            playHTRequest.SetRequestHeader("Authorization", "Bearer " + playHTApiKey);
            playHTRequest.SetRequestHeader("X-User-ID", playHTUserId);

            // Send the request
            yield return playHTRequest.SendWebRequest();

            if (playHTRequest.result == UnityWebRequest.Result.ConnectionError || playHTRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error calling PlayHT: " + playHTRequest.error);
                Debug.LogError("Response Text: " + playHTRequest.downloadHandler.text);
                yield break;
            }
            else
            {
                //Debug.Log("PlayHT audio chunk conversion successful!");
                // Get the binary MP3 data from the response and play it sequentially
                byte[] mp3Data = playHTRequest.downloadHandler.data;
                yield return StartCoroutine(PlayAudioSequentially(mp3Data));
            }
        }
    }

    // Coroutine to play audio chunks sequentially without overlapping
    private IEnumerator PlayAudioSequentially(byte[] mp3Data)
    {
        // Wait until the previous audio chunk is finished
        while (isPlayingAudio)
            yield return null;  // Wait until the current audio has stopped

        // Mark as playing
        isPlayingAudio = true;

        // Create a temporary file for the MP3 data
        string tempPath = Path.Combine(Application.persistentDataPath, "tempAudio.mp3");
        File.WriteAllBytes(tempPath, mp3Data);

        // Load the audio file as an AudioClip
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result == UnityWebRequest.Result.ConnectionError || audioRequest.result == UnityWebRequest.Result.ProtocolError)
                Debug.LogError("Error loading audio: " + audioRequest.error);
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                audioSource.clip = audioClip;
                audioSource.loop = false;
                audioSource.Play();

                // Wait until the audio has finished playing before allowing the next chunk
                while (audioSource.isPlaying)
                    yield return null;

                Debug.Log("Audio chunk finished playing.");
            }
        }
        isPlayingAudio = false;
    }

    private void ShareResponseBasedOnRole(string response)
    {
        if (m_AIGuideScript.role != 6)
        {
            audioSource.mute = true;
            SetNewResult(response);
        }
        else
            audioSource.mute = false;
    }

    // Checks if the result is guide or modify before we send a reply to PlayHT to be converted to audio
    private string CheckForGuidanceOrModification(string result)
    {
        // If the result was a GameObject for guidance, create a custom speech message
        string[] words = result.Split(',');
        if (words.Length == 2)
        {
            Debug.Log("Two word response for guidance or modification");
            string secondWord = words[1].Trim();
            Debug.Log(words[1]);
            if (secondWord.Equals("guide", StringComparison.OrdinalIgnoreCase) || secondWord.Equals("teleport", StringComparison.OrdinalIgnoreCase))
            {
                // Assign the first word to targetName and the second word to modeOfTransportation
                string targetName = words[0].Trim();
                modeOfTransportation = words[1].Trim();

                targetForGuidance = GameObject.Find(targetName);
                if (targetForGuidance != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. Grab on to me and I will take you to " + targetForGuidance.name;
                            break;
                        case 2:
                            result = "Understood. Grab on to me and I will take you to " + targetForGuidance.name;
                            break;
                        case 3:
                            result = "Very well. Grab on to me and I will take you to " + targetForGuidance.name;
                            break;
                        case 4:
                            result = "Okay. Grab on to me and I will take you to " + targetForGuidance.name;
                            break;
                    }
                }
            }
            else // they are trying to modify, turn this into an if for modify
            {
                // Assign the first word to targetName and the second word to modification
                string targetName = words[0].Trim();
                modeOfModification = words[1].Trim();

                targetForModification = GameObject.Find(targetName);
                if (targetForModification != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. I will add an audio beacon to " + targetForModification.name;
                            break;
                        case 2:
                            result = "Understood. Grab on to me and I will take you to " + targetForModification.name;
                            break;
                        case 3:
                            result = "Very well. Grab on to me and I will take you to " + targetForModification.name;
                            break;
                        case 4:
                            result = "Okay. Grab on to me and I will take you to " + targetForModification.name;
                            break;
                    }
                }
            }
        }
        return result;
    }

    private void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(configFileName);
        if (configAsset != null)
        {
            // Parse the JSON data from config.json and assign apiKey values accordingly
            ConfigData configData = JsonUtility.FromJson<ConfigData>(configAsset.text);
            apiKey = configData.APIKey;
        }
        else
        {
            Debug.LogError("Config file not found in Resources folder: " + configFileName);
        }
    }

    private void LoadRoomDescriptions()
    {
        TextAsset descriptionsAsset = Resources.Load<TextAsset>("RoomDescriptions");
        if (descriptionsAsset != null)
        {
            DescriptionsData descriptionData = JsonUtility.FromJson<DescriptionsData>(descriptionsAsset.text);

            // Once we have the descriptions, check the current scene and set objectClassifications to the appropriate description
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (descriptionData != null)
            {
                if (currentSceneName.Equals("Tutorial"))
                {
                    objectClassifications = descriptionData.Tutorial;
                    objectNames = descriptionData.Test_Objects;
                }
                else if (currentSceneName.Equals("GuidePark1_Networked"))
                {
                    objectClassifications = descriptionData.GuidePark1_Networked;
                    objectNames = descriptionData.Park1_Objects;
                    // Debug.Log("objectClassifications set to: " + objectClassifications);
                    // Debug.Log("queryClassifications set to: " + queryClassifications);
                }
                else if (currentSceneName.Equals("GuidePark2_Networked"))
                {
                    objectClassifications = descriptionData.GuidePark2_Networked;
                    objectNames = descriptionData.Park2_Objects;
                    // Debug.Log("objectClassifications set to: " + objectClassifications);
                }
                else if (currentSceneName.Equals("GuidePark3_Networked"))
                {
                    objectClassifications = descriptionData.GuidePark3_Networked;
                    objectNames = descriptionData.Park3_Objects;
                    // Debug.Log("objectClassifications set to: " + objectClassifications);
                }
                else
                {
                    Debug.LogWarning("Description for the current scene not found in RoomDescriptions.json.");
                }
            }
        }
        else
        {
            Debug.LogError("RoomDescriptions.json file not found in Resources folder.");
        }
    }

    private void getAudioSync()
    {
        if (m_GuideAudioSync == null)
            m_GuideAudioSync = FindObjectOfType<GuideAudioSync>();
    }

    public void SetNewResult(string result)
    {
        //Debug.Log("Reached SetNewResult");
        if (m_GuideAudioSync != null)
            m_GuideAudioSync.SetResult(result);
        else
            Debug.LogError("GuideAudioSync is not initialized.");
    }

    private class ConfigData
    {
        public string APIKey;
    }

    private class DescriptionsData
    {
        public string Tutorial;
        public string GuidePark1_Networked;
        public string GuidePark2_Networked;
        public string GuidePark3_Networked;
        public string Test_Objects;
        public string Park1_Objects;
        public string Park2_Objects;
        public string Park3_Objects;
    }
}