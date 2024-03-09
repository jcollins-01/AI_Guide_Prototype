using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Voice2Action;
using Newtonsoft.Json;

public class TextToSpeech : MonoBehaviour
{
    // Your OpenAI API key
    public string apiKey;

    // External scripts that TextToSpeech pulls its messages from
    private VoiceIntentController m_VoiceIntentController;
    private WizardControls m_WizardControlsScript;

    // The URL for the OpenAI Text to Speech API
    private string apiUrl = "https://api.openai.com/v1/tts";

    private void Start()
    {
        m_VoiceIntentController = FindObjectOfType(typeof(VoiceIntentController)) as VoiceIntentController;
        m_WizardControlsScript = FindObjectOfType(typeof(WizardControls)) as WizardControls;
        if (m_VoiceIntentController == null || m_WizardControlsScript == null)
            Debug.LogWarning("One or more required scripts for TextToSpeech has not been found - please ensure the WizardControls script and the Voice2Action system are present in scene");
        else
            Debug.Log("Ready to apply Text-to-Speech for guide messages! Press s to send a test message.");
    }

    private void Update()
    {
        if (Input.GetKeyDown("s"))
        {
            Debug.Log("Testing TextToSpeech");
            string v2aMessage = m_VoiceIntentController.m_TextToSpeechMessage;
            string wizardMessage = m_WizardControlsScript.m_TextToSpeechMessage;
            string testMessage = "This is a test message";
            StartCoroutine(PlayTextToSpeech(testMessage));
        }
    }

    // Coroutine to send the text to the API and play the audio
    public IEnumerator PlayTextToSpeech(string text)
    {
        // Create a JSON object with the text to send to the API
        TTSData newData = new TTSData { input = text, model = "tts-1", voice = "alloy" };
        string jsonString = JsonConvert.SerializeObject(newData);

        // Create a UnityWebRequest to send the text to the API
        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonString); // was json.ToString()
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerAudioClip("output.wav", AudioType.WAV);
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("Content-Type", "application/json");

        // Send the request and wait for a response
        yield return request.SendWebRequest();

        // Check for errors
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Text to Speech request failed: " + request.error);
            yield break;
        }

        // Get the audio clip from the response
        AudioClip audioClip = DownloadHandlerAudioClip.GetContent(request);

        // Play the audio clip
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.Play();

        // Wait for the audio clip to finish playing
        yield return new WaitForSeconds(audioClip.length);

        // Cleanup
        Destroy(audioSource);
    }
}

// Define a data structure that matches the JSON format
[System.Serializable]
public class TTSData
{
    public string input;
    public string voice;
    public string model;
}