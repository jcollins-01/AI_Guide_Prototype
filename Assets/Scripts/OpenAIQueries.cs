using OpenAI;
using OpenAI.Chat;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class OpenAIQueries : MonoBehaviour
{
    public static OpenAIClient client { get; set; }

    // OpenAI API key
    [HideInInspector]
    public string apiKey;
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    // Strings to hold the different pieces of the query message
    public string userQuery = "What's going on in here?";

    [HideInInspector]
    public string playerClassification = "Imagine that the player is the yellow pill-shaped object in the lower left corner of this image. ";
    [HideInInspector]
    public string objectClassifications = "The upright, yellow cube is named Tall Building. " +
        "The upright, green cube is named Short Building. " +
        "The red cylinder to the right of Tall Building is named Red Car Back. " +
        "The green cylinder next to Tall Building is named Green Car. " +
        "The long, yellow cube laying on its side is named Sideways Building. " +
        "The red cylinder in front of Sideways Building is named Red Car Front. " +
        "The green, flattened oval in the back is named Landmark. ";
    [HideInInspector]
    public string queryClassifications = "If the player seems like they want to describe the entire scene, then describe the scene as though you are helping the player understand the game they are in. " +
        "If the player seems like they want to describe a particular object in the scene, describe the object in the image they are referring to. " +
        "If the player seems like they want to go to a particular object in the scene, tell me only the name of the object in the image they would be referring to, plus the word 'teleport' after a comma if it seems like they want to teleport to the object and 'guide' after a comma if they don't specify teleportation" +
        " - ONLY DO THIS IF YOU'RE SURE THE PLAYER WANTS TO TRAVEL TO THAT OBJECT, and provide a description of the object if you aren't sure. ";
    // To use later when playing with guide roles - search for guideClassification to find all places that need to be updated
    [HideInInspector]
    public string memoClassifications = "Limit your reply to 300 words or less.";
    //private string guideClassification = "While answering, imagine that you are a tour guide for the environment.";

    // OpenAI audio, text message, result variables
    [HideInInspector]
    public string text;
    [HideInInspector]
    public GameObject targetForGuidance;
    //[HideInInspector]
    public string modeOfTransportation;

    public string query;
    public string result;
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
        audioSource = (AudioSource)FindObjectOfType(typeof(AudioSource));
        LoadConfig();

        Debug.Log("OpenAI is ready to be queried.");

        // Create an instance of the OpenAI client
        client = new OpenAIClient(apiKey);

        // Default query to begin with
        text = playerClassification + objectClassifications + "Imagine the player said this: " + userQuery + ". " + queryClassifications + memoClassifications; // ADD guideClassification
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
            new Content(ContentType.ImageUrl, "https://i.postimg.cc/wMmyKDRz/Bird-s-Eye.png")
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
        return output;
    }

    public async Task<AudioClip> CallAlloyTTS()
    {
        // If the result was a GameObject for guidance, create a custom speech message
        string[] words = result.Split(',');
        if (words.Length == 2)
        {
            // Assign the first word to targetName and the second word to modeOfTransportation
            string targetName = words[0].Trim();
            modeOfTransportation = words[1].Trim();

            targetForGuidance = GameObject.Find(targetName);
            if (targetForGuidance != null)
                result = "Alright. I am taking you to " + targetForGuidance.name;
        }

        var speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Alloy);

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
        targetForGuidance = null;
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

    private class ConfigData
    {
        public string APIKey;
    }
}