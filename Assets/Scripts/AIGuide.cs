using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;
using UnityEngine.XR;

public class AIGuide : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private AutomaticGuide m_AutomatedGuideScript;
    private OpenAIQueries m_OpenAIQueriesScript;

    // Variables for assigning XR input
    private bool rightControllerGrabbed = false;
    private bool leftControllerGrabbed = false;
    [HideInInspector]
    public InputDevice rightXRController;
    [HideInInspector]
    public InputDevice leftXRController;

    // Variables for monitoring
    private bool m_audioCaptured = false;
    private int whisperCalls = 0;
    private int completionCalls = 0;
    private int alloyCalls = 0;
    private int voiceCalls = 0;
    private bool firstQuery = true;

    // Start is called before the first frame update
    void Start()
    {
        // Add necessary components to the attached GameObject
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            gameObject.AddComponent<AudioSource>();
        NavMeshAgent navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
            gameObject.AddComponent<NavMeshAgent>();
        gameObject.AddComponent<WizardControls>();

        m_AutomatedGuideScript = gameObject.AddComponent(typeof(AutomaticGuide)) as AutomaticGuide;
        m_OpenAIQueriesScript = gameObject.AddComponent(typeof(OpenAIQueries)) as OpenAIQueries;

        if (m_OpenAIQueriesScript == null || m_AutomatedGuideScript == null)
            Debug.LogWarning("One or more required scripts for AIGuide has not been found - please ensure that the GameObject with AIGuide also has OpenAIQueries and AutomaticGuide");
        else
            Debug.Log("AIGuide is active!");

        // Begin capturing screenshots every 30 secs to keep guide updated on scene
        //InvokeRepeating("CaptureScreenshot", 0f, 30f);
    }

    // Update is called once per frame
    void Update()
    {
        // Calls until two controllers are assigned
        //getControllers();

        // If PC user presses and holds space or the right primary button on an XR controller
        if (Input.GetKey(KeyCode.Space) || rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButtonValue) && primaryButtonValue)
        {
            m_OpenAIQueriesScript.CaptureAudio();

            // If, after the primary button was being held, it is no longer being held
            if (rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out bool checkPrimaryButtonValue) && !checkPrimaryButtonValue)
                m_audioCaptured = true;

            // Reset call counters so they can each be called once more
            whisperCalls = 0;
            completionCalls = 0;
            alloyCalls = 0;
            voiceCalls = 0;
        }

        // If the user lifts finger off space or the primary button, assume their query is completed
        if ((Input.GetKeyUp(KeyCode.Space) || m_audioCaptured == true) && whisperCalls == 0)
        {
            m_OpenAIQueriesScript.recordingInProgress = false;
            m_audioCaptured = false;
            // Call the Whisper API to transcribe the recorded speech to text
            var transcribeResult = m_OpenAIQueriesScript.CallWhisper(m_OpenAIQueriesScript.audioSource.clip);
            whisperCalls += 1;
        }

        // Checking for completion of speech transcription
        if (m_OpenAIQueriesScript.whisperCompleted && completionCalls == 0)
        {
            // Construct the query to send to GPT-4 - ADD guideClassification
            // If this is the first query, send all classifcations - after that, the guide should remember the scene and player context
            if (firstQuery)
            {
                m_OpenAIQueriesScript.text = m_OpenAIQueriesScript.playerClassification + m_OpenAIQueriesScript.objectClassifications + "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.memoClassifications;
                firstQuery = false;
            }
            else
            {
                m_OpenAIQueriesScript.text = "Imagine the player said this: " + m_OpenAIQueriesScript.query + ". " + m_OpenAIQueriesScript.queryClassifications + m_OpenAIQueriesScript.memoClassifications;
            }

            // Call the CallCompletion method with the user's recorded voice query
            var guideResult = m_OpenAIQueriesScript.CallCompletion(m_OpenAIQueriesScript.text);
            completionCalls += 1;
        }

        // Checking for completion of query to GPT-4
        if (m_OpenAIQueriesScript.completionCompleted && alloyCalls == 0)
        {
            // Create the audio clip of whatever whatever output has been stored in the result variable
            var speechResult = m_OpenAIQueriesScript.CallAlloyTTS();
            alloyCalls += 1;
        }

        // Checking for completion of audio clip of the guide's response to the user query
        if (m_OpenAIQueriesScript.alloyCompleted && voiceCalls == 0)
        {
            // Play the guide's response
            m_OpenAIQueriesScript.audioSource.clip = m_OpenAIQueriesScript.guideVoice;
            if (!m_OpenAIQueriesScript.audioSource.isPlaying)
                m_OpenAIQueriesScript.audioSource.Play();
            voiceCalls += 1;
        }

        // Checking if a target GameObject was selected to be moved to
        if (m_OpenAIQueriesScript.targetForGuidance != null)
        {
            Debug.Log("Has a target to move to");
            if (m_OpenAIQueriesScript.modeOfTransportation == "guide")
                m_AutomatedGuideScript.GuideToPosition(m_OpenAIQueriesScript.targetForGuidance);
            else
                m_AutomatedGuideScript.TeleportToPosition(m_OpenAIQueriesScript.targetForGuidance);
        }
    }

    public void getControllers()
    {
        if (!rightControllerGrabbed || !leftControllerGrabbed)
        {
            // Makes a list for input devices + fills it with devices that match the characteristics we give in the Unity editor
            // Narrows devices list using characteristics to just the controller we want to use
            List<InputDevice> devices = new List<InputDevice>();

            InputDeviceCharacteristics rightController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Right;
            InputDevices.GetDevicesWithCharacteristics(rightController, devices);

            InputDeviceCharacteristics leftController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Left;
            InputDevices.GetDevicesWithCharacteristics(leftController, devices);

            Debug.Log("Grabbing devices");
            Debug.Log("Found devices " + devices);

            if (!rightControllerGrabbed)
                rightXRController = devices[2]; //attached to right controller
            if (!leftControllerGrabbed)
                leftXRController = devices[1]; // attached to left controller

            if (devices[2] != null) // rightXRController
            {
                Debug.Log("Grabbed right controller successfully");
                rightControllerGrabbed = true;
            }

            if (devices[1] != null) // leftXRController
            {
                Debug.Log("Grabbed left controller successfully");
                leftControllerGrabbed = true;
            }
        }
    }

    // BELOW ARE ALL METHODS FOR UPLOADING IMAGES

    // Keep track of asset refresh
    private bool refreshed = false;

    public void CaptureScreenshot()
    {
        ScreenCapture.CaptureScreenshot(Application.dataPath + "/Resources/Screenshots/capture.png");
        Debug.Log("Screenshot captured!");
        refreshed = false;
        RefreshAssets();
    }

    void RefreshAssets()
    {
        UnityEditor.AssetDatabase.Refresh();

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
        Texture2D capturedScreenshot = Resources.Load<Texture2D>("Screenshots/capture");
        // Decompresses the screenshot texture to work with encoding, encodes texture to a byte array in PNG format, then converts that array to a base64 string
        Texture2D preppedScreenshot = capturedScreenshot.DeCompress();
        string imageString = System.Convert.ToBase64String(ImageConversion.EncodeToPNG(preppedScreenshot));

        // Takes the byte array of the imageData and passed it to IMGUR for upload
        byte[] imageData = ImageConversion.EncodeToPNG(preppedScreenshot);
        StartCoroutine(UploadImage(imageData));
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