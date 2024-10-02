using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using Newtonsoft.Json.Linq;

public class ChatGPTToPlayHT : MonoBehaviour
{
    // OpenAI API and PlayHT credentials
    [HideInInspector]
    public string openAiApiKey;
    [HideInInspector]
    public string playHTApiKey = "f61e1eb6d0024f31b3c5f721b39ba574";
    [HideInInspector]
    public string playHTUserId = "T3JXXeEXYZcVhFPCGE6ohOj5CN22";
    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";

    public AudioSource audioSource; // Audio source to play the streamed audio

    void Start()
    {
        LoadConfig();
        StartCoroutine(CallChatGPTAndStreamAudio("Tell me a joke."));
        // replace this with a string that is the m_OpenAIQueriesScript.text, line 319 in AIGuide
    }

    private void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(configFileName);
        if (configAsset != null)
        {
            // Parse the JSON data from config.json and assign apiKey values accordingly
            ConfigData configData = JsonUtility.FromJson<ConfigData>(configAsset.text);
            openAiApiKey = configData.APIKey;
        }
        else
        {
            Debug.LogError("Config file not found in Resources folder: " + configFileName);
        }
    }

    IEnumerator CallChatGPTAndStreamAudio(string prompt)
    {
        // Call ChatGPT and stream the response
        string chatGptUrl = "https://api.openai.com/v1/chat/completions";
        string chatGptModel = "gpt-3.5-turbo"; // Model ID

        // Prepare the request body for OpenAI API
        var jsonData = "{\"model\": \"" + chatGptModel + "\", \"messages\": [{\"role\": \"user\", \"content\": \"" + prompt + "\"}], \"stream\": true}";

        using (UnityWebRequest chatRequest = new UnityWebRequest(chatGptUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            chatRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            chatRequest.downloadHandler = new DownloadHandlerBuffer();
            chatRequest.SetRequestHeader("Content-Type", "application/json");
            chatRequest.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);

            // Send the request
            yield return chatRequest.SendWebRequest();

            if (chatRequest.result == UnityWebRequest.Result.ConnectionError || chatRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error calling ChatGPT: " + chatRequest.error);
                yield break;
            }
            else
            {
                // Previous version where streamed response is parsed separately
                //string responseText = chatRequest.downloadHandler.text;
                //string chatGptResponseText = ExtractChatGptText(responseText);
                // Now pass the response text to PlayHT for voice conversion
                //StartCoroutine(ConvertTextToAudio(chatGptResponseText));

                yield return StartCoroutine(ConvertTextToAudio(StreamChatGptResponse(chatRequest.downloadHandler.text)));
            }
        }
    }

    // Coroutine to stream the ChatGPT response and yield each content chunk
    private IEnumerator StreamChatGptResponse(string responseText)
    {
        // We will assume that `responseText` is a stream of multiple JSON objects
        var responseLines = responseText.Split('\n');

        foreach (var line in responseLines)
        {
            if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("data:"))
            {
                var jsonData = line.Substring("data:".Length).Trim();

                if (jsonData == "[DONE]")
                {
                    Debug.Log("Streaming complete.");
                    yield break;  // End the coroutine when the stream is done
                }

                // Parse the JSON chunk dynamically to extract content
                JObject jsonObject = JObject.Parse(jsonData);
                var content = jsonObject["choices"]?[0]?["delta"]?["content"]?.ToString();

                if (!string.IsNullOrEmpty(content))
                {
                    Debug.Log("Received content: " + content);

                    // Yield each content string incrementally
                    yield return content;
                }
            }
        }
    }

    IEnumerator ConvertTextToAudio(IEnumerator streamedText)
    {
        while (streamedText.MoveNext())
        {
            string text = streamedText.Current as string;
            if (!string.IsNullOrEmpty(text))
            {
                // PlayHT API to convert text to audio
                string playHTUrl = "https://play.ht/api/v2/tts/stream";

                var playHTData = "{\"voice\":\"s3://voice-cloning-zero-shot/a59cb96d-bba8-4e24-81f2-e60b888a0275/charlottenarrativesaad/manifest.json\", \"text\":\"" + text + "\"}";
                Debug.Log("PlayHT Request Body: " + playHTData); // Print the request body

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

                        // Save MP3 data to a file (optional)
                        string path = Path.Combine(Application.persistentDataPath, "audio.mp3");
                        File.WriteAllBytes(path, mp3Data);
                        Debug.Log("Audio file saved to: " + path);

                        // Optionally, play the audio in Unity (assuming you have an AudioSource ready)
                        StartCoroutine(PlayAudioFromMp3Data(mp3Data));
                        // Get the audio file URL and stream the audio
                        /*string responseText = playHTRequest.downloadHandler.text;
                        Debug.Log("PlayHT successful response text: " + responseText);
                        string audioUrl = ExtractAudioUrl(responseText);
                        Debug.Log("PlayHT successful response text: " + responseText);
                        StartCoroutine(StreamAudio(audioUrl));*/
                    }
                }
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
                AudioSource audioSource = GetComponent<AudioSource>();
                audioSource.clip = audioClip;
                audioSource.Play();

                Debug.Log("Playing audio from MP3 data...");
            }
        }
    }

    IEnumerator StreamAudio(string url)
    {
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result == UnityWebRequest.Result.ConnectionError || audioRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error streaming audio: " + audioRequest.error);
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("Audio streamed and played successfully!");
            }
        }
    }

    // Helper to extract text from ChatGPT response cleanly
    string ExtractChatGptText(string responseText)
    {
        return responseText;
        
        // Deserialize the JSON response into C# objects
        //ChatGPTResponse response = JsonUtility.FromJson<ChatGPTResponse>(responseText);

        // Extract the content from the first choice
        //string cleanText = response.choices[0]?.delta;

        // Sanitize the text if needed
        //return cleanText.Trim();  // Make sure to remove any leading/trailing spaces or special characters
    }

    // Helper to extract audio URL from PlayHT response
    string ExtractAudioUrl(string responseText)
    {
        // Extract the audio file URL from the PlayHT API response
        // You may need to use JSON parsing libraries like Newtonsoft.JSON here
        // Placeholder logic:
        return "https://path-to-your-audio-file.mp3"; // Replace with actual extraction logic
    }

    // Class to hold the api key from our config file
    private class ConfigData
    {
        public string APIKey;
    }

    // Classes to assist with cleaning up the response from ChatGPT
    [System.Serializable]
    public class ChatGPTChoice
    {
        public string delta;  // This might change depending on the structure of the response
    }

    [System.Serializable]
    public class ChatGPTResponse
    {
        public ChatGPTChoice[] choices;
    }
}