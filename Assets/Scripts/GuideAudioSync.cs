using Normal.Realtime;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using OpenAI;
using System;
using UnityEngine.Networking;
using System.Text;
using System.IO;

public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    // Scripts we need access to
    private AIGuide m_AIGuideScript;
    private OpenAIQueries m_openAIQueriesScript;
    private VRHandling m_VRHandlingScript;

    public AudioSource _audioSource;
    private string apiKey;
    [HideInInspector]
    public string playHTApiKey = "f61e1eb6d0024f31b3c5f721b39ba574";
    [HideInInspector]
    public string playHTUserId = "T3JXXeEXYZcVhFPCGE6ohOj5CN22";

    private void Awake()
    {
        _audioSource = GameObject.Find("Guide Voice").GetComponent<AudioSource>(); // grabs an audio source specifically for sharing guide voice
        m_AIGuideScript = GameObject.Find("Human Model").GetComponent<AIGuide>();

        if (_audioSource == null)
            Debug.LogError("AudioSource missing from this GameObject");

        if (m_AIGuideScript == null)
            Debug.LogError("AI Guide missing from this GameObject");
    }

    protected override void OnRealtimeModelReplaced(GuideAudioSyncModel previousModel, GuideAudioSyncModel currentModel)
    {
        if (previousModel != null)
            previousModel.resultDidChange -= ResultDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.result = null; // Ensure initial state for result
            currentModel.resultDidChange += ResultDidChange;
        }
    }

    private async void ResultDidChange(GuideAudioSyncModel model, string result)
    {
        //Debug.Log("Detected that the result did change: " + result);

        if (!string.IsNullOrEmpty(result))
        {
            StartCoroutine(ConvertTextToAudio(result));
            Debug.Log("Played audio clip from guide voice");

            /*AudioClip guideVoice = await ConvertResultToSpeech(result);
            if (guideVoice != null)
            {
                _audioSource.clip = guideVoice;
                _audioSource.Play();
                Debug.Log("Played audio clip from guide voice");
            }
            else
            {
                Debug.LogWarning("Failed to convert result to speech.");
            }*/
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
                _audioSource.clip = audioClip;
                // Maybe have to do a if ! is playing
                _audioSource.Play();

                Debug.Log("Playing audio from MP3 data...");
            }
        }
    }

    private async Task<AudioClip> ConvertResultToSpeech(string result)
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
                m_openAIQueriesScript.modeOfTransportation = words[1].Trim();

                m_openAIQueriesScript.targetForGuidance = GameObject.Find(targetName);
                if (m_openAIQueriesScript.targetForGuidance != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. Grab on to me and I will take you to " + m_openAIQueriesScript.targetForGuidance.name;
                            break;
                        case 2:
                            result = "Understood. Grab on to me and I will take you to " + m_openAIQueriesScript.targetForGuidance.name;
                            break;
                        case 3:
                            result = "Very well. Grab on to me and I will take you to " + m_openAIQueriesScript.targetForGuidance.name;
                            break;
                        case 4:
                            result = "Okay. Grab on to me and I will take you to " + m_openAIQueriesScript.targetForGuidance.name;
                            break;
                    }
                    //result = "Alright. Grab on to me and I will take you to " + m_openAIQueriesScript.targetForGuidance.name;
                }
            }
            else // they are trying to modify, turn this into an if for modify
            {
                // Assign the first word to targetName and the second word to modification
                string targetName = words[0].Trim();
                m_openAIQueriesScript.modeOfModification = words[1].Trim();

                m_openAIQueriesScript.targetForModification = GameObject.Find(targetName);
                if (m_openAIQueriesScript.targetForModification != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                        case 2:
                            result = "Understood. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                        case 3:
                            result = "Very well. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                        case 4:
                            result = "Okay. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                    }
                    //result = "Alright. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                }
            }
            /*else if (secondWord.Equals("modify", StringComparison.OrdinalIgnoreCase)) // they are trying to modify, turn this into an if for modify
            {
                // Assign the first word to targetName and the second word to modification
                string targetName = words[0].Trim();
                m_openAIQueriesScript.modeOfModification = words[1].Trim();

                m_openAIQueriesScript.targetForModification = GameObject.Find(targetName);
                if (m_openAIQueriesScript.targetForModification != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                        case 2:
                            result = "Understood. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                        case 3:
                            result = "Very well. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                        case 4:
                            result = "Okay. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                            break;
                    }
                    //result = "Alright. I will add an audio beacon to " + m_openAIQueriesScript.targetForModification.name;
                }
            }
            else
                result = "Sorry, could you repeat that?";*/
        }

        //Debug.Log("Reached ConvertResultToSpeech");
        var client = new OpenAIClient(apiKey);

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
            //Debug.Log("Created audio clip of voiced result with voice for " + m_AIGuideScript.role);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Exception in ConvertResultToSpeech:\n" + e);
        }

        return output;
    }

    public void SetResult(string result)
    {
        //Debug.Log("Reached SetResult in GuideAudioSync");
        if (model != null)
            model.result = result;
        else
            Debug.LogError("Model is not initialized.");
    }

    // Start is called before the first frame update
    void Start()
    {
        gameObject.AddComponent<GuideController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (apiKey == null)
            GetAPIKey();

        if (m_openAIQueriesScript == null)
            if (FindObjectOfType<OpenAIQueries>())
                m_openAIQueriesScript = FindObjectOfType<OpenAIQueries>();

        if (m_VRHandlingScript == null)
            GetVRHandling();
        else
        {
            if (m_VRHandlingScript.isMutingButtonPressed)
                _audioSource.Stop();
        }

        // If this is in a confed client and the audio source is null, grab it from a guide in the scene
        if (FindObjectOfType<ConfederateHandler>() && _audioSource == null)
            if (GameObject.FindWithTag("Guide"))
                _audioSource = GameObject.Find("Guide Voice").GetComponent<AudioSource>();
    }

    void GetAPIKey()
    {
        // If there's a guide in the scene, get the api key (a confed won't have this available until a guide joins them)
        if (GameObject.FindWithTag("Guide"))
        {
            OpenAIQueries aIQueries = FindObjectOfType<OpenAIQueries>();
            apiKey = aIQueries.apiKey;
        }
    }

    void GetVRHandling()
    {
        // If there's a guide in the scene, get the VR handling script from them
        if (GameObject.FindWithTag("Guide"))
            m_VRHandlingScript = FindObjectOfType<VRHandling>();
    }
}
