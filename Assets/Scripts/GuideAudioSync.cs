using Normal.Realtime;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using OpenAI;
using System;
using UnityEngine.Networking;
using System.Text;
using System.IO;
using Newtonsoft.Json;

public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    // Scripts we need access to
    private AIGuide m_AIGuideScript;
    private OpenAIQueries m_openAIQueriesScript;
    private VRHandling m_VRHandlingScript;
    private RealtimeGuideClient _guideClient;

    public AudioSource _audioSource;
    private bool isPlayingAudio;

    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";
    private string apiKey;
    [HideInInspector]
    public string playHTApiKey;
    [HideInInspector]
    public string playHTUserId;
    private string elevenLabsApiKey;
    private string elevenLabsModelId = "eleven_turbo_v2";

    // Performance variables
    private float latencyStartTime;
    private float timeToFirstAudio;
    private bool capturedFirstAudioTime;

    private void Awake()
    {
        // Explicitly set the values here since old ones are being cached
        LoadConfig();
        //Debug.Log("PlayHT credentials are " + playHTApiKey + " " + playHTUserId);
        _audioSource = GameObject.Find("Guide Voice").GetComponent<AudioSource>(); // grabs an audio source specifically for sharing guide voice
        m_AIGuideScript = GameObject.Find("Human Model").GetComponent<AIGuide>();

        if (_audioSource == null)
            Debug.LogError("AudioSource missing from this GameObject");

        if (m_AIGuideScript == null)
            Debug.LogError("AI Guide missing from this GameObject");
    }

    private void Start()
    {
        // Find the main client script on this object or the human model
        _guideClient = GetComponent<RealtimeGuideClient>();
        if (_guideClient == null)
            _guideClient = FindObjectOfType<RealtimeGuideClient>();

        gameObject.AddComponent<GuideController>();
    }

    // Called by the Host when they get a chunk from OpenAI
    public void BroadcastAudioChunk(string base64Audio)
    {
        // Send this string to everyone in the room via RPC
        //realtimeView.Rpc("RpcReceiveAudioChunk", RpcTarget.All, base64Audio);
    }

    // This method runs on EVERY client (Host and Remotes)
    //[RealtimeRpc(Reliable = true)] // True prevents popping/clicking from lost packets
    private void RpcReceiveAudioChunk(string base64Audio)
    {
        // If we are the Host (the one who sent it), we already played it directly to ensure zero latency. So we ignore this RPC.
        if (realtimeView.isOwnedLocallySelf) return;

        // If we are a Remote Client, send this data to our audio player
        if (_guideClient != null)
        {
            _guideClient.ReceiveRemoteAudio(base64Audio);
        }
    }

    protected override void OnRealtimeModelReplaced(GuideAudioSyncModel previousModel, GuideAudioSyncModel currentModel)
    {
        if (previousModel != null)
            previousModel.resultDidChange -= ResultDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.result = null; // Ensure initial state for result
            //currentModel.resultDidChange += ResultDidChange;
        }
    }

    private async void ResultDidChange(GuideAudioSyncModel model, string result)
    {
        //Debug.Log("Detected that the result did change: " + result);
        //Debug.Log("PlayHT credentials are " + playHTApiKey + " " + playHTUserId);

        if (!string.IsNullOrEmpty(result))
            StartCoroutine(StreamTextToPlayHT(result));
    }

    // Coroutine to send a chunk of text to PlayHT for real-time audio conversion
    private IEnumerator StreamTextToPlayHT(string textChunk)
    {
        Debug.Log("Started coroutine for audio");

        string voiceId = "SAz9YHcvj6GT2YYXdXww"; // Human default

        // Default payload for voices that don't require special prompting / human voice
        var payloadObj = new
        {
            text = textChunk,
            model_id = elevenLabsModelId,
            voice_settings = new
            {
                stability = 0.5f,
                similarity_boost = 0.7f,
                style = 0.02f,
                use_speaker_boost = true
            }
        };

        if (m_AIGuideScript.role == 2)
        {
            voiceId = "bIHbv24MWmeRgasZH58o";

            payloadObj = new
            {
                text = textChunk,
                model_id = elevenLabsModelId,
                voice_settings = new
                {
                    stability = 1.0f, // Max stability makes voice monotone and predictable (less breathy)
                    similarity_boost = 0.0f, // Min similarity makes voice less like a specific person, more general
                    style = 0.0f, // Disable all emotional "flair"
                    use_speaker_boost = false
                }
            };
        }
        else if (m_AIGuideScript.role == 3)
        {
            voiceId = "N2lVS1w4EtoT3dr4eOWO";

            payloadObj = new
            {
                text = textChunk,
                model_id = elevenLabsModelId,
                voice_settings = new
                {
                    stability = 0.5f,
                    similarity_boost = 0.75f,
                    style = 0.3f, // Increased style to help the AI follow the "serious" flair we added
                    use_speaker_boost = true
                }
            };
        }
        else if (m_AIGuideScript.role == 4)
        {
            voiceId = "cgSgspJ2msm6clMCkdW9";

            payloadObj = new
            {
                text = textChunk,
                model_id = elevenLabsModelId,
                voice_settings = new
                {
                    stability = 0.4f, // Lower the stability to make it more emotive / breathier
                    similarity_boost = 0.75f,
                    style = 0.3f, // Increased style to help the AI follow the "eager" flair we added
                    use_speaker_boost = true
                }
            };
        }
        else if (m_AIGuideScript.role == 5)
        {
            voiceId = "JBFqnCBsd6RMkjVDRZzb";

            payloadObj = new
            {
                text = textChunk,
                model_id = elevenLabsModelId,
                voice_settings = new
                {
                    stability = 0.4f, // Lower the stability to make it more emotive / breathier
                    similarity_boost = 0.75f,
                    style = 0.5f, // Increased style to help the AI follow the "dramatic" flair we added
                    use_speaker_boost = true
                }
            };
        }
        else if (m_AIGuideScript.role == 6)
        {
            voiceId = "XrExE9yKIg1WjnnlVkGX";

            payloadObj = new
            {
                text = textChunk,
                model_id = elevenLabsModelId,
                voice_settings = new
                {
                    stability = 0.3f, // Lower the stability to make it more emotive / breathier
                    similarity_boost = 0.75f,
                    style = 0.5f, // Increased style to help the AI follow the "whisper" flair we added
                    use_speaker_boost = true
                }
            };
        }

        // Combine the variables into the url for posting
        string finalUrl = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}/stream?optimize_streaming_latency=3";

        // Convert object to JSON string
        string jsonBody = JsonConvert.SerializeObject(payloadObj);

        Debug.Log($"Using API Key: {elevenLabsApiKey}");

        using (UnityWebRequest elevenLabsRequest = UnityWebRequestMultimedia.GetAudioClip(finalUrl, AudioType.MPEG))
        {
            elevenLabsRequest.method = UnityWebRequest.kHttpVerbPOST;

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            elevenLabsRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            elevenLabsRequest.downloadHandler = new DownloadHandlerAudioClip(finalUrl, AudioType.MPEG);

            elevenLabsRequest.SetRequestHeader("Content-Type", "application/json");
            elevenLabsRequest.SetRequestHeader("xi-api-key", elevenLabsApiKey); // Use 'xi-api-key', NOT 'Authorization'
            elevenLabsRequest.SetRequestHeader("Accept", "audio/mpeg");

            // Send the request
            yield return elevenLabsRequest.SendWebRequest();

            if (elevenLabsRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error calling ElevenLabs: " + elevenLabsRequest.error);
                //Debug.LogError("Response Text: " + elevenLabsRequest.downloadHandler.text);

                Debug.LogError("Error Code: " + elevenLabsRequest.responseCode);
                if (elevenLabsRequest.downloadHandler.data != null)
                {
                    string errorJson = Encoding.UTF8.GetString(elevenLabsRequest.downloadHandler.data);
                    Debug.LogError("ElevenLabs Detailed Error: " + errorJson);
                }
            }
            else
            {
                // Debug.Log("ElevenLabs audio chunk conversion successful!");
                // DownloadHandlerAudioClip automatically converts the MP3 data into a Unity AudioClip
                AudioClip clip = DownloadHandlerAudioClip.GetContent(elevenLabsRequest);

                if (clip != null)
                {
                    // Assuming PlayAudioSequentially now accepts AudioClip:
                    yield return StartCoroutine(PlayAudioSequentially(clip));
                }
            }
        }
    }

    // Coroutine to play audio chunks sequentially without overlapping
    private IEnumerator PlayAudioSequentially(AudioClip clip) // was byte[] mp3Data
    {
        // Wait until the previous audio chunk is finished
        while (isPlayingAudio)
            yield return null;  // Wait until the current audio has stopped

        // Mark as playing
        isPlayingAudio = true;

        if (clip == null)
        {
            Debug.LogError("PlayAudioSequentially received a null AudioClip!");
            isPlayingAudio = false;
            yield break;
        }

        // Set up the audio source
        _audioSource.clip = clip;
        _audioSource.loop = false;
        float startTime = Time.time;
        float clipLength = _audioSource.clip.length;

        // Check if this is the first audio playback for timing
        if (!capturedFirstAudioTime)
        {
            timeToFirstAudio = Time.realtimeSinceStartup - latencyStartTime;
            Debug.Log($"[Timing] Time to First Audio (User Hears Voice): {timeToFirstAudio:F2} seconds");
            capturedFirstAudioTime = true;
        }

        _audioSource.Play();
        Debug.Log($"Playing audio chunk. Length: {clipLength:F2}s");

        // Wait until the audio has finished playing before allowing the next chunk
        while (_audioSource.isPlaying)
        {
            float elapsedTime = Time.time - startTime;

            // Manual stop safety check
            if (elapsedTime >= clipLength)
            {
                _audioSource.Stop();
                Debug.Log("Audio manually stopped based on clip length.");
                break;
            }
            yield return null;
        }

        Debug.Log("Audio chunk finished playing.");

        // Reset state for the next item in the queue
        isPlayingAudio = false;
    }

    public void SetResult(string result)
    {
        //Debug.Log("Reached SetResult in GuideAudioSync");
        if (model != null)
            model.result = result;
        else
            Debug.LogError("Model is not initialized.");
    }

    private void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(configFileName);
        if (configAsset != null)
        {
            // Parse the JSON data from config.json and assign apiKey values accordingly
            ConfigData configData = JsonUtility.FromJson<ConfigData>(configAsset.text);
            apiKey = configData.APIKey;
            playHTApiKey = configData.PlayHTAPIKey;
            playHTUserId = configData.PlayHTUserID;
            elevenLabsApiKey = configData.ElevenLabsAPIKey;
        }
        else
        {
            Debug.LogError("Config file not found in Resources folder: " + configFileName);
        }
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

    private class ConfigData
    {
        public string APIKey;
        public string PlayHTAPIKey;
        public string PlayHTUserID;
        public string ElevenLabsAPIKey;
    }
}
