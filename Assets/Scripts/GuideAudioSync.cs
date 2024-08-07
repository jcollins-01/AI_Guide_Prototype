using Normal.Realtime;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using OpenAI;
using System;

public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    // Scripts we need access to
    private AIGuide m_AIGuideScript;
    private OpenAIQueries m_openAIQueriesScript;
    private VRHandling m_VRHandlingScript;

    public AudioSource _audioSource;
    private string apiKey;

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
