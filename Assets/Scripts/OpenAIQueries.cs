using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections;
using System.Collections.Generic;
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

    // Open AI query variables (query varies)
    public List<string> roles = new List<string>
    {
        "warm, friendly, but still professional tour guide",
        "formal and assertive assistant, who talks like a robot",
        "computer-like, succinct assistant, who gives the straight facts",
        "very friendly, excited companion, who is eager to please who you're talking to",
        "wise, old-fashioned, slightly Shakespearean-sounding mentor",
        "gentle, sweet, soft-spoken assistant who gives very brief statements, as though slipping in words to someone without trying to interrupt what they're doing"
    };
    [HideInInspector]
    public string contextClassification = "The two photos you are seeing are two views of a video game. One of these photos is the bird's eye view of the entire scene. The other photo is the player's current perspective and what they are currently looking at in the scene.";
    [HideInInspector]
    public string objectClassifications = "The upright, yellow cube is named Tall Building. " +
        "The upright, green cube is named Short Building. " +
        "The long, yellow cube laying on its side is named Sideways Building. " +
        "The red cylinder in front of Sideways Building is named Red Car. " +
        "The green, flattened oval in the back is named Landmark. ";
    [HideInInspector]
    public string queryClassifications = "If the player seems like they want to describe the entire scene, then describe the scene as though you are helping the player understand the game they are in. " +
        "If the player seems like they want to describe a particular object in the scene, describe the object in the image they are referring to. " +
        "If it seems like they want to to go to a particular object in the scene, tell me only the name of the object in the image they would be referring to, " +
        "plus the word 'teleport' after a comma if it seems like they want to teleport to the object " +
        "and 'guide' after a comma if they don't specify teleportation." +
        "ONLY GIVE ME AN OBJECT NAME FROM THIS LIST: Landmark, Sideways Building, Tall Building, Red Car, Short Building." +
        "Only do this is you're sure they want to go to an object - describe the scene for the player if you are unsure what they want." +
        "If it seems like they want to add a sound effect to a particular object, tell me only the name of the object in the image they would be referring to, " +
        "plus the word 'modify' after a comma." +
        "ONLY GIVE ME AN OBJECT NAME FROM THIS LIST: Landmark, Sideways Building, Tall Building, Red Car, Short Building." +
        "Only do this is you're sure they want to add a sound - describe the scene for the player if you are unsure what they want.";
    // To use later when playing with guide roles - search for guideClassification to find all places that need to be updated
    [HideInInspector]
    public string memoClassifications = "Limit your reply to 300 words or less.";

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
    private Texture2D capturedScreenshot;

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
        audioSource = FindObjectOfType<AudioSource>();
        LoadConfig();
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
        // The role becomes the string value contained at the index we sent over from AIGuide
        role = roles[m_AIGuideScript.role-1];
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
            new Content(ContentType.ImageUrl, m_CameraSystemScript.birdsEyeImageLink),
            new Content(ContentType.ImageUrl, m_CameraSystemScript.viewpointImageLink)
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

    private void getAudioSync()
    {
        if (m_GuideAudioSync == null)
            m_GuideAudioSync = FindObjectOfType<GuideAudioSync>();
    }

    public void SetNewResult(string result)
    {
        Debug.Log("Reached SetNewResult");
        if (m_GuideAudioSync != null)
            m_GuideAudioSync.SetResult(result);
        else
            Debug.LogError("GuideAudioSync is not initialized.");
    }

    private class ConfigData
    {
        public string APIKey;
    }
}