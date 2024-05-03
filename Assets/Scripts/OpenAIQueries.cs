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
    private Texture2D capturedScreenshot;

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

        // Begin capturing screenshots every 30 secs to keep guide updated on scene
        //InvokeRepeating("CaptureScreenshot", 0f, 30f);
        CaptureScreenshot();
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
            new Content(ContentType.ImageUrl, "https://i.postimg.cc/wMmyKDRz/Bird-s-Eye.png") //imageShackLink "https://i.postimg.cc/wMmyKDRz/Bird-s-Eye.png" $"data:image/png;base64,{Convert.ToBase64String(capturedScreenshot.EncodeToPNG())}"
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

    // BELOW ARE ALL METHODS FOR UPLOADING IMAGES

    // Keep track of asset refresh
    private bool refreshed = false;

    public void CaptureScreenshot()
    {
        ScreenCapture.CaptureScreenshot(Application.dataPath + "/Resources/Screenshots/viewpointCapture.png");
        Debug.Log("Screenshot captured!");
        refreshed = false;
        RefreshAssets();
    }

    void RefreshAssets()
    {
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh(); // This function is only available in the editor; can't be used in builds to the headset
#endif
        if (!refreshed) // Run this coroutine to make sure we refresh before moving on
            StartCoroutine(WaitForRefresh());
        else
        {
            Debug.Log("Assets refreshed!");
            UploadImage();
        }
    }

    IEnumerator WaitForRefresh()
    {
        yield return new WaitForSeconds(2);
        RefreshAssets();
        refreshed = true;
    }

    void UploadImage()
    {
        Debug.Log("Uploading screenshot to Image Shack");
        // Loads the screenshot (Unity considers it a texture) from Resources
        capturedScreenshot = Resources.Load<Texture2D>("Screenshots/viewpointCapture");
        // Decompresses the screenshot texture to work with encoding, encodes texture to a byte array in PNG format, then converts that array to a base64 string
        Texture2D preppedScreenshot = capturedScreenshot.DeCompress();
        string imageString = System.Convert.ToBase64String(ImageConversion.EncodeToPNG(preppedScreenshot));

        // Takes the byte array of the imageData and passed it to IMGUR for upload
        byte[] imageData = ImageConversion.EncodeToPNG(preppedScreenshot);
        //StartCoroutine(UploadImage(imageData));
    }

    // Image Shack API Key, requested from "https://imageshack.com/contact/api"
    private string imageApiKey = "468CGIVYeba088be6297f37babc219efe571c8bd";
    public string m_imageShackLink; // Used to pass the hosted image to queries

    IEnumerator UploadImage(byte[] imageData)
    {
        // Set up form data
        WWWForm form = new WWWForm();
        form.AddField("key", imageApiKey);
        form.AddBinaryData("fileupload", imageData, "image.png", "image/png"); // was "image.jpg", "image/jpeg"

        // Send the POST request
        using (UnityWebRequest www = UnityWebRequest.Post("https://post.imageshack.us/upload_api.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // Parse the response
                string responseText = www.downloadHandler.text;
                Debug.Log("Upload successful!");
                Debug.Log("Response: " + responseText);
                string imageLink = ParseXmlResponse(responseText);
                Debug.Log("image_link: " + imageLink);
                m_imageShackLink = imageLink;
            }
            else
            {
                Debug.LogError("Upload failed: " + www.error);
            }
        }
    }

    // Parse XML response and extract xmlns data
    string ParseXmlResponse(string xmlResponse)
    {
        // Create XML document and load the XML string
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlResponse);

        // Create an XmlNamespaceManager for resolving namespaces
        XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("ns", "http://ns.imageshack.us/imginfo/8/");

        // Placeholder for link to return
        string imageLink = "";

        // Get the image_link element using the namespace manager
        XmlNode imageLinkNode = xmlDoc.SelectSingleNode("//ns:links/ns:image_link", nsManager);

        // Check if imageLinkNode is not null
        if (imageLinkNode != null)
        {
            // Get the value of the image_link element
            imageLink = imageLinkNode.InnerText;
        }
        else
        {
            Debug.LogError("image_link not found in the XML.");
        }

        return imageLink;
    }
}

// Class to check out for uploading to PostImages instead of ImageShack
//https://github.com/ShareX/ShareX/issues/472
/*
public class ImageUploader : MonoBehaviour
{
    public string imageFilePath; // Path to the image file on your device
    public string uploadUrl = "https://postimages.org/json/rr";

    public void UploadImage()
    {
        StartCoroutine(UploadImageCoroutine());
    }

    IEnumerator UploadImageCoroutine()
    {
        if (string.IsNullOrEmpty(imageFilePath))
        {
            Debug.LogError("Image file path is not specified.");
            yield break;
        }

        // Load the image bytes
        byte[] imageBytes = System.IO.File.ReadAllBytes(imageFilePath);

        // Create a UnityWebRequest to upload the image
        using (UnityWebRequest www = new UnityWebRequest(uploadUrl, "POST"))
        {
            // Set the content type to "multipart/form-data"
            www.SetRequestHeader("Content-Type", "multipart/form-data");

            // Create a multipart form data
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", imageBytes, "image.png", "image/png");

            // Set the form data to the UnityWebRequest
            www.uploadHandler = new UploadHandlerRaw(form.data);
            www.uploadHandler.contentType = "multipart/form-data";

            // Send the request
            yield return www.SendWebRequest();

            // Check for errors
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogError("Error uploading image: " + www.error);
            }
            else
            {
                // Image uploaded successfully
                Debug.Log("Image uploaded successfully. Response: " + www.downloadHandler.text);
            }
        }
    }
}
*/