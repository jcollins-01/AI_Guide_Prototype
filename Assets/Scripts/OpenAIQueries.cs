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
    public string playHTApiKey = "f61e1eb6d0024f31b3c5f721b39ba574";
    [HideInInspector]
    public string playHTUserId = "T3JXXeEXYZcVhFPCGE6ohOj5CN22";
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    // Variables to hold scripts we need access to
    private CameraSystem m_CameraSystemScript;
    private GuideAudioSync m_GuideAudioSync;
    private AIGuide m_AIGuideScript;
    public RealtimeAvatarVoice _avatarVoice;


    // Variables to construct OpenAI queries
    private StringBuilder fullGptResponse = new StringBuilder(); // To hold the full GPT response when streaming
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
    public string result;
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

    public IEnumerator CallChatGPTAndStreamAudio(string prompt)
    {
        // Clear any previous responses before making a new call
        fullGptResponse.Clear();
        result = string.Empty;

        // Call ChatGPT and stream the response
        string chatGptUrl = "https://api.openai.com/v1/chat/completions";
        string chatGptModel = "gpt-3.5-turbo"; // Model ID

        // Prepare the request body for OpenAI API
        var jsonData = "{\"model\": \"" + chatGptModel + "\", \"messages\": [" +
                       "{\"role\": \"user\", \"content\": \"Here is the text prompt: " + prompt + "\"}," +
                       "{\"role\": \"user\", \"content\": \"Here are two image URLs for reference: Birds Eye View: " + m_CameraSystemScript.birdsEyeImageLink + " and Viewpoint: " + m_CameraSystemScript.viewpointImageLink + "\"}" +
                       "], \"stream\": true}";

        using (UnityWebRequest chatRequest = new UnityWebRequest(chatGptUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            chatRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            chatRequest.downloadHandler = new DownloadHandlerBuffer();
            chatRequest.SetRequestHeader("Content-Type", "application/json");
            chatRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);

            // Send the request
            yield return chatRequest.SendWebRequest();

            if (chatRequest.result == UnityWebRequest.Result.ConnectionError || chatRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error calling ChatGPT: " + chatRequest.error);
                Debug.LogError("Response Code: " + chatRequest.responseCode);
                Debug.LogError("Response Text: " + chatRequest.downloadHandler.text); // Log the response from PlayHT
                yield break;
            }
            else
            {
                // Start streaming the GPT response and aggregate it
                yield return StartCoroutine(AggregateChatGptResponse(chatRequest.downloadHandler.text));

                // After the entire response is aggregated, send it to PlayHT
                yield return StartCoroutine(ConvertTextToAudio(result));
            }
        }
    }

    // Coroutine to aggregate the GPT response into a full text
    private IEnumerator AggregateChatGptResponse(string responseText)
    {
        var responseLines = responseText.Split('\n');

        foreach (var line in responseLines)
        {
            if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("data:"))
            {
                var jsonData = line.Substring("data:".Length).Trim();

                if (jsonData == "[DONE]")
                {
                    Debug.Log("Streaming complete.");
                    result = fullGptResponse.ToString();
                    Debug.Log("Response from GPT-3.5: " + result);
                    completionCompleted = true;
                    CheckForGuidanceOrModification(); // Check if the result is guide or modify, and choose an audio response for the guide if so

                    // Sends the text over the network to sync guide's audio if the role is not 6 (invisible guide)
                    // To prevent duplicate sound, mute the local audio source when sending to network, unmute for invisible
                    if (m_AIGuideScript.role != 6)
                    {
                        audioSource.mute = true;
                        SetNewResult(result);
                    }
                    else
                        audioSource.mute = false;
                    yield break;  // End the coroutine when the stream is done
                }

                JObject jsonObject = JObject.Parse(jsonData);
                var content = jsonObject["choices"]?[0]?["delta"]?["content"]?.ToString();

                if (!string.IsNullOrEmpty(content))
                {
                    //Debug.Log("Received content: " + content);
                    fullGptResponse.Append(content);  // Aggregate the content into the full response
                }
            }

            yield return null;  // Make sure to yield between lines to keep the coroutine responsive
        }
    }

    // Checks if the result is guide or modify before we send a reply to PlayHT to be converted to audio
    private void CheckForGuidanceOrModification()
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
    }

    // Send the full GPT response to PlayHT for text-to-speech conversion
    IEnumerator ConvertTextToAudio(string fullText)
    {
        string playHTUrl = "https://play.ht/api/v2/tts/stream";
        string voice = "s3://voice-cloning-zero-shot/a59cb96d-bba8-4e24-81f2-e60b888a0275/charlottenarrativesaad/manifest.json"; // Default voice, Human

        // Change speech request to new voices if the role calls for it
        if (m_AIGuideScript.role == 2)
            voice = "s3://voice-cloning-zero-shot/b41d1a8c-2c99-4403-8262-5808bc67c3e0/bentonsaad/manifest.json"; // Robot
        else if (m_AIGuideScript.role == 3)
            voice = "s3://voice-cloning-zero-shot/d82d246c-148b-457f-9668-37b789520891/adolfosaad/manifest.json"; // Mechanical cane
        else if (m_AIGuideScript.role == 4)
            voice = "s3://voice-cloning-zero-shot/f6594c50-e59b-492c-bac2-047d57f8bdd8/susanadvertisingsaad/manifest.json"; // Dog
        else if (m_AIGuideScript.role == 5)
            voice = "s3://voice-cloning-zero-shot/3a831d1f-2183-49de-b6d8-33f16b2e9867/dylansaad/manifest.json"; // Mythical bird
        else if (m_AIGuideScript.role == 6)
            voice = "s3://voice-cloning-zero-shot/1afba232-fae0-4b69-9675-7f1aac69349f/delilahsaad/manifest.json"; // Invisible

        var playHTData = "{\"voice\":\"" + voice + "\", \"text\":\"" + fullText + "\"}";

        using (UnityWebRequest playHTRequest = new UnityWebRequest(playHTUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(playHTData);
            playHTRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            playHTRequest.downloadHandler = new DownloadHandlerBuffer();
            playHTRequest.SetRequestHeader("Content-Type", "application/json");
            playHTRequest.SetRequestHeader("Authorization", "Bearer " + playHTApiKey);
            playHTRequest.SetRequestHeader("X-User-ID", playHTUserId);

            yield return playHTRequest.SendWebRequest();

            if (playHTRequest.result == UnityWebRequest.Result.ConnectionError || playHTRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error calling PlayHT: " + playHTRequest.error);
                Debug.LogError("Response Code: " + playHTRequest.responseCode);
                Debug.LogError("Response Text: " + playHTRequest.downloadHandler.text); // Log the response from PlayHT
                yield break;
            }
            else
            {
                Debug.Log("PlayHT audio conversion successful!");

                // Get the binary MP3 data from the response
                byte[] mp3Data = playHTRequest.downloadHandler.data;

                // Optionally, save MP3 data to a file
                string path = Path.Combine(Application.persistentDataPath, "audio.mp3");
                File.WriteAllBytes(path, mp3Data);
                Debug.Log("Audio file saved to: " + path);

                // Optionally, play the audio in Unity (assuming you have an AudioSource ready)
                StartCoroutine(PlayAudioFromMp3Data(mp3Data));
            }
        }
    }

    // Coroutine to play audio from MP3 binary data
    private IEnumerator PlayAudioFromMp3Data(byte[] mp3Data)
    {
        // Create a temporary file for the MP3 data
        string tempPath = Path.Combine(Application.persistentDataPath, "tempAudio.mp3");
        File.WriteAllBytes(tempPath, mp3Data);

        // Load the audio file as an AudioClip
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result == UnityWebRequest.Result.ConnectionError || audioRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error loading audio: " + audioRequest.error);
            }
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                //AudioSource audioSource = GetComponent<AudioSource>();
                audioSource.clip = audioClip;
                // Maybe have to do a if ! is playing
                audioSource.Play();

                Debug.Log("Playing audio from MP3 data...");
            }
        }
    }

    public async Task<string> CallCompletion(string userInput)
    {
        // Create the content for the message
        List<Content> content = new List<Content>
        {
            new Content(ContentType.Text, userInput),
            new Content(ContentType.ImageUrl, m_CameraSystemScript.birdsEyeImageLink), // m_CameraSystemScript.birdsEyeImageLink
            new Content(ContentType.ImageUrl, m_CameraSystemScript.viewpointImageLink) //m_CameraSystemScript.viewpointImageLink
            //new Content(ContentType.ImageUrl, "https://i.postimg.cc/wMmyKDRz/Bird-s-Eye.png") //imageShackLink "https://i.postimg.cc/wMmyKDRz/Bird-s-Eye.png" $"data:image/png;base64,{Convert.ToBase64String(capturedScreenshot.EncodeToPNG())}"
        };

        // Park 1 https://i.postimg.cc/cLjtjhqz/park1bird.png, https://i.postimg.cc/1tSGCBwQ/park2viewpoint.png
        // Park 2 https://i.postimg.cc/5yJmyCJf/park2bird.png, https://i.postimg.cc/3JmzRXHD/park2-NEWviewpoint.png
        // Park 3 https://i.postimg.cc/4dXp6TKg/park3bird.png, https://i.postimg.cc/P5GyC9Mt/park3viewpoint.png

        // Create the message to send to the API
        var chatPrompts = new List<Message>
        {
            new(Role.User, content),
        };

        var chatRequest = new ChatRequest(chatPrompts, model: "gpt-4-vision-preview", maxTokens: 300);
        string output = "N/A";
        try
        {
            var chatResponse = await client.ChatEndpoint.GetCompletionAsync(chatRequest);
            output = chatResponse.FirstChoice.ToString();
            Debug.Log("Response from GPT-4: " + output);
            result = output;
            completionCompleted = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Exception in CallCompletion:\n" + e);
        }
        // Sends the text over the network to sync guide's audio if the role is not 6 (invisible guide)
        // To prevent duplicate sound, mute the local audio source when sending to network, unmute for invisible
        if (m_AIGuideScript.role != 6)
        {
            GetComponent<AudioSource>().mute = true;
            SetNewResult(result);
        }
        else
        {
            GetComponent<AudioSource>().mute = false;
        }
        return output;
    }

    public async Task<AudioClip> CallAlloyTTS()
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
                    //result = "Alright. Grab on to me and I will take you to " + targetForGuidance.name;
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
                    //result = "Alright. I will add an audio beacon to " + targetForModification.name;
                }
            }
            /*
            else if (secondWord.Equals("modify", StringComparison.OrdinalIgnoreCase)) // they are trying to modify, turn this into an if for modify
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
                    //result = "Alright. I will add an audio beacon to " + targetForModification.name;
                } 
            }
            else
                result = "Sorry, could you repeat that?";
            */
        }

        // Initialize speech request with default voice (Alloy)
        var speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Alloy); // Human

        // Change speech request to new voices if the role calls for it
        if (m_AIGuideScript.role == 2)
            speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Echo); // Robot
        else if (m_AIGuideScript.role == 3)
            speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Onyx); // Mechanical cane
        else if (m_AIGuideScript.role == 4)
            speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Shimmer); // Dog
        else if (m_AIGuideScript.role == 5)
            speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Fable); // Mythical bird
        else if (m_AIGuideScript.role == 6)
            speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Nova); // Invisible

        AudioClip output = null;
        try
        {
            var speechResponse = await client.AudioEndpoint.CreateSpeechAsync(speechRequest);
            output = speechResponse.Item2; // grabs the AudioClip created in the Tuple speechResponse
            guideVoice = output;
            alloyCompleted = true;
            Debug.Log("Created audio clip of voiced result");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Exception in CallAlloyTTS:\n" + e);
        }
        //targetForGuidance = null;
        targetForModification = null;
        return output;
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