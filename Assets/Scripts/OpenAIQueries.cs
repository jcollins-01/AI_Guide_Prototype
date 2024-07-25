using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIQueries : MonoBehaviour
{
    // OpenAI variables
    public static OpenAIClient client { get; set; }
    // OpenAI API key
    [HideInInspector]
    public string apiKey;
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    // Variables to hold scripts we need access to
    private CameraSystem m_CameraSystemScript;
    private GuideAudioSync m_GuideAudioSync;
    private AIGuide m_AIGuideScript;

    // Variables to construct OpenAI queries
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
            return "If the player seems like they want to describe the entire scene, then succintly summarize the scene as though you are helping the player understand the game they are in. " +
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

        // Calls continously to check for a role change
        getGuideRole();
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
                    result = "Alright. Grab on to me and I will take you to " + targetForGuidance.name;
            }
            else // they are trying to modify
            {
                // Assign the first word to targetName and the second word to modification
                string targetName = words[0].Trim();
                modeOfModification = words[1].Trim();

                targetForModification = GameObject.Find(targetName);
                if (targetForModification != null)
                    result = "Alright. I will add an audio beacon to " + targetForModification.name;
            }
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
                if (currentSceneName.Equals("GuideTest_Networked"))
                {
                    objectClassifications = descriptionData.GuideTest_Networked;
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
        public string GuideTest_Networked;
        public string GuidePark1_Networked;
        public string GuidePark2_Networked;
        public string GuidePark3_Networked;
        public string Test_Objects;
        public string Park1_Objects;
        public string Park2_Objects;
        public string Park3_Objects;
    }
}