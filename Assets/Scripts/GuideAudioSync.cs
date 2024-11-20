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
    private bool isPlayingAudio;

    // Config file to hold api keys, credentials
    [HideInInspector]
    private const string configFileName = "config";
    private string apiKey;
    [HideInInspector]
    public string playHTApiKey;
    [HideInInspector]
    public string playHTUserId;

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
        Debug.Log("Detected that the result did change: " + result);
        Debug.Log("PlayHT credentials are " + playHTApiKey + " " + playHTUserId);

        if (!string.IsNullOrEmpty(result))
            StartCoroutine(StreamTextToPlayHT(result));
    }

    // Coroutine to send a chunk of text to PlayHT for real-time audio conversion
    private IEnumerator StreamTextToPlayHT(string textChunk)
    {
        string playHTUrl = "https://play.ht/api/v2/tts/stream";
        string voice = "s3://voice-cloning-zero-shot/a59cb96d-bba8-4e24-81f2-e60b888a0275/charlottenarrativesaad/manifest.json"; // Default voice, Human

        // Customize the voice as per the role
        if (m_AIGuideScript.role == 2) voice = "s3://voice-cloning-zero-shot/b41d1a8c-2c99-4403-8262-5808bc67c3e0/bentonsaad/manifest.json";
        else if (m_AIGuideScript.role == 3) voice = "s3://voice-cloning-zero-shot/d82d246c-148b-457f-9668-37b789520891/adolfosaad/manifest.json";
        else if (m_AIGuideScript.role == 4) voice = "s3://voice-cloning-zero-shot/f6594c50-e59b-492c-bac2-047d57f8bdd8/susanadvertisingsaad/manifest.json";
        else if (m_AIGuideScript.role == 5) voice = "s3://voice-cloning-zero-shot/3a831d1f-2183-49de-b6d8-33f16b2e9867/dylansaad/manifest.json";
        else if (m_AIGuideScript.role == 6) voice = "s3://voice-cloning-zero-shot/1afba232-fae0-4b69-9675-7f1aac69349f/delilahsaad/manifest.json";

        var playHTData = "{\"voice\":\"" + voice + "\", \"text\":\"" + textChunk + "\"}";

        using (UnityWebRequest playHTRequest = new UnityWebRequest(playHTUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(playHTData);
            playHTRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            playHTRequest.downloadHandler = new DownloadHandlerBuffer();
            playHTRequest.SetRequestHeader("Content-Type", "application/json");
            playHTRequest.SetRequestHeader("Authorization", "Bearer " + playHTApiKey);
            playHTRequest.SetRequestHeader("X-User-ID", playHTUserId);

            // Send the request
            yield return playHTRequest.SendWebRequest();

            if (playHTRequest.result == UnityWebRequest.Result.ConnectionError || playHTRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error calling PlayHT: " + playHTRequest.error);
                Debug.LogError("Response Text: " + playHTRequest.downloadHandler.text);
                yield break;
            }
            else
            {
                //Debug.Log("PlayHT audio chunk conversion successful!");
                // Get the binary MP3 data from the response and play it sequentially
                byte[] mp3Data = playHTRequest.downloadHandler.data;
                yield return StartCoroutine(PlayAudioSequentially(mp3Data));
            }
        }
    }

    // Coroutine to play audio chunks sequentially without overlapping
    // Coroutine to play audio chunks sequentially without overlapping
    private IEnumerator PlayAudioSequentially(byte[] mp3Data)
    {
        // Wait until the previous audio chunk is finished
        while (isPlayingAudio)
            yield return null;  // Wait until the current audio has stopped

        // Mark as playing
        isPlayingAudio = true;

        // Create a temporary file for the MP3 data
        string tempPath = Path.Combine(Application.persistentDataPath, "tempAudio.mp3");
        File.WriteAllBytes(tempPath, mp3Data);

        // Load the audio file as an AudioClip
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result == UnityWebRequest.Result.ConnectionError || audioRequest.result == UnityWebRequest.Result.ProtocolError)
                Debug.LogError("Error loading audio: " + audioRequest.error);
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                _audioSource.clip = audioClip;
                _audioSource.loop = false;
                float startTime = Time.time;  // Capture the time when the audio starts
                float clipLength = _audioSource.clip.length;
                _audioSource.Play();

                // Wait until the audio has finished playing before allowing the next chunk
                while (_audioSource.isPlaying) // was just yield return null in the while loop
                {
                    float elapsedTime = Time.time - startTime;
                    // If the audio has reached a not playing state, or the time it is active is longer than the length of the clip, manually stop it
                    // For highlights
                    if (elapsedTime >= clipLength)
                    {
                        _audioSource.Stop();  // Force stop if it somehow keeps playing
                        Debug.Log("Audio manually stopped in guide audio sync.");
                        break;
                    }
                    yield return null;
                }

                Debug.Log("Audio chunk finished playing.");
            }
        }
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

    // Start is called before the first frame update
    void Start()
    {
        gameObject.AddComponent<GuideController>();
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
    }
}
