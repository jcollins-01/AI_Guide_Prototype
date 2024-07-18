using Normal.Realtime;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using OpenAI;

public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    private AudioSource _audioSource;
    private string apiKey;

    private void Awake()
    {
        _audioSource = GetComponentInChildren<AudioSource>();
        if (_audioSource == null)
            Debug.LogError("AudioSource missing from this GameObject");
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
            AudioClip guideVoice = await ConvertResultToSpeech(result);
            if (guideVoice != null)
            {
                _audioSource.clip = guideVoice;
                _audioSource.Play();
                Debug.Log("Played audio clip from guide voice");
            }
            else
            {
                Debug.LogWarning("Failed to convert result to speech.");
            }
        }
    }

    private async Task<AudioClip> ConvertResultToSpeech(string result)
    {
        //Debug.Log("Reached ConvertResultToSpeech");
        var client = new OpenAIClient(apiKey); // Replace with your OpenAI API key

        var speechRequest = new OpenAI.Audio.SpeechRequest(result, "tts-1", OpenAI.Audio.SpeechVoice.Alloy);
        AudioClip output = null;

        try
        {
            var speechResponse = await client.AudioEndpoint.CreateSpeechAsync(speechRequest);
            output = speechResponse.Item2; // grabs the AudioClip created in the Tuple speechResponse
            Debug.Log("Created audio clip of voiced result");
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
    }

    void GetAPIKey()
    {
        //OpenAIQueries aIQueries = FindObjectOfType<OpenAIQueries>();
        //apiKey = aIQueries.apiKey;
    }
}
