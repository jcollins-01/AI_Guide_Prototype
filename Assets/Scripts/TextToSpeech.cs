using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class TextToSpeech : MonoBehaviour
{
    // Your OpenAI API key
    private string apiKey = "YOUR_API_KEY";

    // The URL for the OpenAI Text to Speech API
    private string apiUrl = "https://api.openai.com/v1/tts";

    // Coroutine to send the text to the API and play the audio
    public IEnumerator PlayTextToSpeech(string text)
    {
        // Create a JSON object with the text to send to the API
        // JSONObject json = new JSONObject();
        string json = "text";
        //json.AddField("text", text);

        // Create a UnityWebRequest to send the text to the API
        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json.ToString());
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