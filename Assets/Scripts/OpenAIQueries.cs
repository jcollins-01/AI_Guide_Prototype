using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Normal.Realtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;
using System.Threading;
using System.Collections.Concurrent;

public class ConfigData
{
    public string APIKey;
    public string PlayHTAPIKey;
    public string PlayHTUserID;
    public string ElevenLabsAPIKey;
}

public class InteractionLog
{
    public string timestamp;
    public string queryNumber;
    public string userQuery;
    public string guideResponse;
    public string guideRole;
    public string chosenObjectTarget;
    public string chosenAction;

    // Metrics
    public float latencyToFirstToken;
    public float latencyToFirstAudio;
    public float totalGenerationTime;
}

public class RealtimeGuideClient : MonoBehaviour
{
    // Access the OpenAIQueries class so we can change variables as needed
    private OpenAIQueries _openAIQueriesScript;
    private GuideAudioSync guideAudioSync;
    private AIGuide aiGuideScript;

    public AudioSource outputSource;

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;

    [HideInInspector] private const string configFileName = "config"; // Config file to hold api keys, credentials
    private string _apiKey; // Set this securely

    // Events to hook into legacy existing Audio/UI systems
    public Action<string> OnTextReceived;
    public Action<string> OnAudioDeltaReceived; // Base64 PCM16 audio from OpenAI

    private bool _isConnected = false;
    private bool _guideAudioSourceFound = false;
    private bool _isAiSpeaking = false;

    // Microphone Variables
    private AudioClip _micClip;
    private string _micDevice;
    private int _lastMicPos;
    private bool _isRecording;

    // Variables for voice detection
    public bool _voiceDetectionOn; // Set from AIGuide
    private bool personalVoicesMode = false;

    // The Thread-Safe Queue
    private ConcurrentQueue<float[]> _audioPlaybackQueue = new ConcurrentQueue<float[]>();
    private const int SAMPLE_RATE = 24000; // OpenAI's native rate
    private int _totalSamplesSent = 0;

    // Configuration
    private const string OPENAI_REALTIME_URL = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview";

    // OpenAI audio, text message, result variables
    [HideInInspector] public string text;
    [HideInInspector] public GameObject targetForGuidance;
    [HideInInspector] public string modeOfTransportation;
    [HideInInspector] public GameObject targetForModification;
    [HideInInspector] public string modeOfModification;
    [HideInInspector] public GameObject targetForDescription;

    private StringBuilder _textBuffer = new StringBuilder(); // Buffer to accumulate GPT response chunks before sending to ElevenLabs/logging

    private void Start()
    {
        _openAIQueriesScript = FindObjectOfType<OpenAIQueries>();
        aiGuideScript = GetComponent<AIGuide>();
        if (_openAIQueriesScript != null)
            Debug.Log("Found the queries script");

        // Determine which version of audio generation is to be used
        personalVoicesMode = FindObjectOfType<SwitchTools>().personalVoicesOn;

        _micDevice = Microphone.devices[0];
    }

    public async Task Connect(string systemInstructions)
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Authorization", "Bearer " + _apiKey);
        _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            await _webSocket.ConnectAsync(new Uri(OPENAI_REALTIME_URL), CancellationToken.None);
            _isConnected = true;
            Debug.Log("Connected to Realtime API");
            Debug.Log("The instructions received were: " + systemInstructions);

            // Start listening for responses immediately
            _ = ReceiveLoop();

            // Configure the session (Set the "System Prompt")
            await SendSessionUpdate(systemInstructions);
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection Failed: {e.Message}");
        }
    }

    private async Task SendSessionUpdate(string instructions)
    {
        Debug.Log("Adding a new message to the realtime conversation");

        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" }, // Ask for both or just audio
                instructions = instructions,
                voice = "alloy", // Options: alloy, echo, shimmer
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                turn_detection = (object)null //new { type = "server_vad" } // Auto-detects when user stops talking!
            }
        };

        await SendJson(sessionUpdate);
    }

    public void StartRecording()
    {
        if (!_isConnected) return;
        Debug.Log("Recording started");

        // Interrupt any current AI speech if the user interrupts
        outputSource.Stop();
        _audioPlaybackQueue = new ConcurrentQueue<float[]>(); // Clear pending audio

        // Check if the AI is speaking, and interrupt it if so to let the user record/talk
        if (_isAiSpeaking)
        {
            _ = SendJson(new { type = "response.cancel" }); // Tell API to stop generating
            _isAiSpeaking = false;
        }

        _ = SendJson(new { type = "input_audio_buffer.clear" }); // Clear the buffer so it hears only our new audio

        _isRecording = true;
        _lastMicPos = 0;
        _totalSamplesSent = 0;
        _micClip = Microphone.Start(_micDevice, true, 20, 24000); // 24kHz is standard for OpenAI Realtime
    }

    public async Task StopRecordingAndCommit(string screenshotUrl = null)
    {
        Debug.Log("Recording stopped");
        // Flush the final bits of audio
        await HandleMicStreaming();

        _isRecording = false;
        Microphone.End(_micDevice);

        // If we have a screenshot, send it NOW as a user message item
        if (!string.IsNullOrEmpty(screenshotUrl))
            await SendImageContext(screenshotUrl);

        // Only commit if we actually sent audio (prevents empty call errors)
        if (_totalSamplesSent > 0)
        {
            // Tell OpenAI we are done talking and want a response
            await SendJson(new { type = "input_audio_buffer.commit" });
            await SendJson(new { type = "response.create" });
            Debug.Log($"Committed {_totalSamplesSent} samples and requested response.");
        }
        else
        {
            Debug.LogWarning("Recording too short (no audio sent). Skipping audio commit to avoid API error.");
        }

        _totalSamplesSent = 0; // Reset for next time
        _lastMicPos = 0;
    }

    void Update()
    {
        // Find the guide audio sync component to share over network
        getAudioSync();
        
        // Call continuous microphone streaming logic
        HandleMicStreaming();

        // Find guide audio source before handling anything with output audio
        if (!_guideAudioSourceFound)
        {
            outputSource = GameObject.Find("Human Model").GetComponent<AudioSource>();
            _guideAudioSourceFound = true;
            Debug.Log("Got our guide's audio source!");
        }
        else
        {
            // Pull from the thread-safe queue on the Main Thread to figure out audio playback logic
            if (!outputSource.isPlaying && _audioPlaybackQueue.TryDequeue(out float[] nextChunk))
                PlayAudioChunk(nextChunk);
        }
    }

    private async Task HandleMicStreaming()
    {
        if (!_isRecording || !_isConnected) return;

        int currentPos = Microphone.GetPosition(_micDevice);
        if (currentPos == _lastMicPos) return;

        float[] samples;

        // Normal read (head is ahead of last position)
        if (currentPos > _lastMicPos)
        {
            samples = new float[currentPos - _lastMicPos];
            _micClip.GetData(samples, _lastMicPos);
        }
        // Wrap-around read (head looped back to the start of the clip)
        else
        {
            int samplesToRead = (_micClip.samples - _lastMicPos) + currentPos;
            samples = new float[samplesToRead];

            // Read from last position to the very end of the clip
            float[] part1 = new float[_micClip.samples - _lastMicPos];
            _micClip.GetData(part1, _lastMicPos);

            // Read from the start of the clip to the current position
            float[] part2 = new float[currentPos];
            _micClip.GetData(part2, 0);

            // Combine them into one array
            Array.Copy(part1, 0, samples, 0, part1.Length);
            Array.Copy(part2, 0, samples, part1.Length, part2.Length);
        }

        _lastMicPos = currentPos;

        // Convert the float array [-1.0, 1.0] to PCM16 bytes and send
        if (samples != null && samples.Length > 0)
        {
            _totalSamplesSent += samples.Length;

            // MicCheck(samples);

            byte[] pcmData = ConvertFloatsToPCM16(samples);
            string base64Audio = Convert.ToBase64String(pcmData);

            await SendJson(new { type = "input_audio_buffer.append", audio = base64Audio });
        }
    }

    private void MicCheck(float[] samples)
    {
        // Checks when mic is silent/active
        float maxVol = 0f;
        foreach (var s in samples) if (Mathf.Abs(s) > maxVol) maxVol = Mathf.Abs(s);

        if (maxVol < 0.001f)
        {
            // If this keeps spamming, your mic is dead/muted!
            Debug.LogWarning("Mic is capturing silence! Check OS Permissions or Device Name.");
        }
        else
        {
            Debug.Log("Mic active: " + maxVol);
        }
    }

    // Handle the incoming RPC data on Remote Clients
    public void ReceiveRemoteAudio(string base64Audio)
    {
        Debug.Log($"[Client] Received remote audio chunk: {base64Audio.Length} chars");

        // Convert Base64 back to float[] and play it
        byte[] pcmData = System.Convert.FromBase64String(base64Audio);
        float[] floatData = ConvertPCM16ToFloats(pcmData);

        // Add to queue just like normal
        _audioPlaybackQueue.Enqueue(floatData);
    }

    // Define the logic for sharing voice over network
    private bool ShouldShareResponse()
    {
        if (aiGuideScript == null) return true; // Default to share if no script found

        // If role is 6, it's private (Local only). Otherwise, share.
        if (aiGuideScript.role == 6)
        {
            return false;
        }
        return true;
    }

    private void PlayAudioChunk(float[] data)
    {
        // Debug.Log("Got a response chunk to play as audio");
        AudioClip clip = AudioClip.Create("ResponseChunk", data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        outputSource.clip = clip;
        outputSource.Play();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 64];
        while (_webSocket.State == WebSocketState.Open)
        {
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage); // Continuously appends the new chnks of the result until we hit the EndOfMessage

                string json = Encoding.UTF8.GetString(ms.ToArray());
                // Realtime API has many events. We filter for the ones we need.
                HandleServerEvent(json);
            }
        }
    }

    private void HandleServerEvent(string json)
    {
        try
        {
            JObject jsonObj = JObject.Parse(json);
            string type = (string)jsonObj["type"];

            switch (type)
            {
                case "response.created":
                    
                    _textBuffer.Clear();
                    _isAiSpeaking = true;
                    break;
                
                case "response.audio.delta":
                    // Native Audio stream from OpenAI (Fastest possible latency)
                    //Debug.Log("Got response audio");
                    if (personalVoicesMode) // Don't do anything with the native audio stream from OpenAI
                        break;
                    else
                    {
                        // If we aren't using personal voices, then stream from the native audio
                        string base64Audio = (string)jsonObj["delta"];
                        byte[] pcmData = Convert.FromBase64String(base64Audio);
                        float[] floatData = ConvertPCM16ToFloats(pcmData);
                        _audioPlaybackQueue.Enqueue(floatData);

                        // Check if we should broadcast this to the network
                        if (ShouldShareResponse() && guideAudioSync != null)
                            guideAudioSync.BroadcastAudioChunk(base64Audio);

                        OnAudioDeltaReceived?.Invoke(base64Audio);
                        break;
                    }

                case "response.audio_transcript.delta": // Use this instead of or in addition to text.delta
                    string transcriptDelta = (string)jsonObj["delta"];
                    _textBuffer.Append(transcriptDelta);
                    //Debug.Log($"Transcript Chunk: {transcriptDelta}");
                    break;

                case "response.text.delta":
                    // Use the text grabbed here to pass to ElevenLabs
                    //Debug.Log("Got response text to use in ElevenLabs or log");
                    if (personalVoicesMode) // Still capture the text, but additionally pass it to ElevenLabs
                    {
                        string textDelta = (string)jsonObj["delta"];
                        _textBuffer.Append(textDelta);
                        _openAIQueriesScript.CheckForTargetForDescription(textDelta);

                        OnTextReceived?.Invoke(textDelta);
                        break;
                    }
                    else // Only capture the text
                    {
                        string textDelta = (string)jsonObj["delta"];
                        _textBuffer.Append(textDelta);
                        _openAIQueriesScript.CheckForTargetForDescription(textDelta);

                        OnTextReceived?.Invoke(textDelta);
                        break;
                    } 

                case "response.done":
                    _isAiSpeaking = false;
                    // Log the FULL details to see why it finished
                    var responseObj = jsonObj["response"];
                    string status = (string)responseObj["status"];

                    if (status == "completed")
                    {
                        string remainingText = _textBuffer.ToString().Trim();
                        Debug.Log($"Full Response Captured: {remainingText}");
                        // Send the last generated text chunk to see if there was a target identified
                        string customResponse = _openAIQueriesScript.CheckForGuidanceOrModification(remainingText);

                        // If it was actually changed into one of the random responses chosen by the CheckForGuidance... function
                        if (!string.IsNullOrEmpty(customResponse) && customResponse != remainingText)
                        {
                            // STOP the current audio (the "Cube, guide" whisper) 
                            outputSource.Stop();
                            _audioPlaybackQueue = new ConcurrentQueue<float[]>();

                            // Make the AI speak our custom confirmation for the user
                            if (personalVoicesMode)
                            {
                                // Call ElevenLabs to speak the text instead
                            }
                            else
                            {
                                _ = SpeakCustomText(customResponse);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError($"Response Finished with Error: {status}");
                        Debug.LogError($"Details: {responseObj["status_details"]}");
                    }
                    break;

                case "error":
                    Debug.LogError($"Realtime Error: {json}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Realtime Error, Exception Thrown: {e}");
        }
    }

    private async Task SendJson(object data)
    {
        if (_webSocket.State != WebSocketState.Open) return;
        string json = JsonConvert.SerializeObject(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task SendImageContext(string screenshotLink)
    {
        var imageMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[] {
                    new { type = "image_url", image_url = new { url = screenshotLink } }
                }
            }
        };
        await SendJson(imageMessage);
        Debug.Log("Sent Image Context to Realtime API for " + screenshotLink);
    }

    // CONVERTERS
    private byte[] ConvertFloatsToPCM16(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float sample = Math.Clamp(samples[i], -1f, 1f);
            short val = (short)(sample * 32767f);
            bytes[i * 2] = (byte)(val & 0xff);
            bytes[i * 2 + 1] = (byte)((val >> 8) & 0xff);
        }
        return bytes;
    }

    private float[] ConvertPCM16ToFloats(byte[] bytes)
    {
        float[] floats = new float[bytes.Length / 2];
        for (int i = 0; i < floats.Length; i++)
        {
            short val = BitConverter.ToInt16(bytes, i * 2);
            floats[i] = val / 32768f;
        }
        return floats;
    }

    private void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
    }

    public async Task SpeakCustomText(string customText)
    {
        if (!_isConnected) return;
        Debug.Log($"Injecting custom TTS: {customText}");

        // Create a conversation item (the text we want it to say)
        var textItem = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "assistant",
                content = new[]
                {
                new { type = "text", text = customText }
            }
            }
        };

        await SendJson(textItem);

        // Ask the API to generate the audio for that item
        await SendJson(new { type = "response.create" });
    }

    private void getAudioSync()
    {
        if (guideAudioSync == null)
            guideAudioSync = FindObjectOfType<GuideAudioSync>();
    }

    public void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(configFileName);
        if (configAsset != null)
        {
            // Parse the JSON data from config.json and assign apiKey values accordingly
            ConfigData configData = JsonUtility.FromJson<ConfigData>(configAsset.text);
            _apiKey = configData.APIKey;
            //_elevenLabsApiKey = configData.ElevenLabsAPIKey;
        }
        else
        {
            Debug.LogError("Config file not found in Resources folder: " + configFileName);
        }
    }

    // ElevenLabs sample code
    /*
     * // Coroutine to send a chunk of text to PlayHT for real-time audio conversion
    private IEnumerator StreamTextToPlayHT(string textChunk)
    {
        Debug.Log("Started coroutine for audio");

        // Customize the voice as per the role
        // 1: human, 2: robot, 3: cane, 4: guide dog, 5: bird, 6: invisible
        // Human - River "SAz9YHcvj6GT2YYXdXww"
        // Robot - Will "bIHbv24MWmeRgasZH58o"
        // Cane - Callum "N2lVS1w4EtoT3dr4eOWO" / Adam "pNInz6obpgDQGcFmaJgB"
        // Dog -  Jessica "cgSgspJ2msm6clMCkdW9" / Harry "SOYHLrjzK2X1ezoPC6cr"
        // Bird - George "JBFqnCBsd6RMkjVDRZzb" / Lily "pFZP5JQG7iQjIQuC4Bku"
        // Invisible - Matilda "XrExE9yKIg1WjnnlVkGX"

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
        audioSource.clip = clip;
        audioSource.loop = false;
        float startTime = Time.time;
        float clipLength = audioSource.clip.length;

        // Check if this is the first audio playback for timing
        if (!capturedFirstAudioTime)
        {
            timeToFirstAudio = Time.realtimeSinceStartup - latencyStartTime;
            Debug.Log($"[Timing] Time to First Audio (User Hears Voice): {timeToFirstAudio:F2} seconds");
            capturedFirstAudioTime = true;
        }

        audioSource.Play();
        Debug.Log($"Playing audio chunk. Length: {clipLength:F2}s");

        // Wait until the audio has finished playing before allowing the next chunk
        while (audioSource.isPlaying)
        {
            float elapsedTime = Time.time - startTime;

            // Manual stop safety check
            if (elapsedTime >= clipLength)
            {
                audioSource.Stop();
                Debug.Log("Audio manually stopped based on clip length.");
                break;
            }
            yield return null;
        }

        Debug.Log("Audio chunk finished playing.");

        // Reset state for the next item in the queue
        isPlayingAudio = false;
    }
    */
}

public class OpenAIQueries : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private AIGuide m_AIGuideScript;
    public RealtimeAvatarVoice _avatarVoice;

    private string objectNames;
    public List<string> roles = new List<string>
    {
        "warm, friendly, but still professional sighted guide",
        "formal and assertive assistant, who talks like a robot",
        "computer-like, succinct assistant, who gives the straight facts",
        "very friendly, excited companion, who is eager to please who you're talking to",
        "wise, old-fashioned, slightly Shakespearean-sounding mentor", //posh
        "gentle, sweet, soft-spoken assistant slipping in words here and there"
    };
    [HideInInspector]
    public string contextClassification = "The two photos you are seeing are two views of a video game. One of these photos is the bird's eye view of the entire scene. " +
        "The other photo is the player's current perspective and what they are currently looking at in the scene." +
        "The player is going to ask you questions about the contents in these photos.";
    [HideInInspector]
    public string memoClassifications = "Limit your reply to 150 words or less - DO NOT GO OVER THIS WORD LIMIT. Don't mention the two photos when replying; speak to the player as though you are in the game next to them.";
    [HideInInspector]
    public string objectClassifications = ""; // Manual descriptions of key objects: left blank to be dynamically set by RoomDescriptions file
    [HideInInspector]
    public string queryClassifications // Variable set up so that it initializes itself with the most recent values for objectNames
    {
        get
        {//succintly summarize?
            return "Make sure to respond to all the player's questions, including interpersonal ones like how you are, what your name is, what you want to do, etc.  " +
                   "If the player seems like they want to describe the entire scene, then succinctly summarize the scene as though you are helping the player understand the game they are in. " +
                   "If the player seems like they want to describe a particular object in the scene, describe the object in the image they are referring to. " +
                   "If it seems like they want to go to a particular object in the scene, tell me only the name of the object in the image they would be referring to, " +
                   "plus the word 'teleport' after a comma if it seems like they want to teleport to the object " +
                   "and 'guide' after a comma if they don't specify teleportation." +
                   "ONLY GIVE ME AN OBJECT NAME FROM THIS LIST: " + objectNames + "." +
                   "Only do this if you're sure they want to go to an object." +
                   "If it seems like they want to add a sound effect to a particular object, tell me only the name of the object in the image they would be referring to, " +
                   "plus the word 'modify' after a comma." +
                   "ONLY GIVE ME AN OBJECT NAME FROM THIS LIST: " + objectNames + "." +
                   "Only do this if you're sure they want to add a sound." +
                   "If the player asks for help in finding a particular object, give them directions for how they might want to orient themselves to face the object, as though the player is blind and cannot see any visual markers. " +
                   "If the question the user asks doesn't fit into any of the above categories, respond to them to the best of your ability. Again, LIMIT YOUR REPLY TO 150 WORDS OR LESS - THIS IS IMPORTANT.";
        }
    }

    // OpenAI audio, text message, result variables
    [HideInInspector] public string text;
    [HideInInspector] public GameObject targetForGuidance;
    [HideInInspector] public string modeOfTransportation;
    [HideInInspector] public GameObject targetForModification;
    [HideInInspector] public string modeOfModification;
    [HideInInspector] public GameObject targetForDescription;

    public string query;
    public string role;
    public AudioSource audioSource;
    public AudioClip guideVoice;

    // Pre-saved messages
    [HideInInspector] public AudioClip humanApology;
    [HideInInspector] public AudioClip robotApology;
    [HideInInspector] public AudioClip dogApology;
    [HideInInspector] public AudioClip caneApology;
    [HideInInspector] public AudioClip birdApology;
    [HideInInspector] public AudioClip invisibleApology;

    private void Start()
    {
        // Find and load appropriate resources
        m_AIGuideScript = GetComponent<AIGuide>();
        audioSource = GameObject.Find("Human Model").GetComponent<AudioSource>(); // Ensure we grab the guide audio source for OpenAI, not PlayAudio
        LoadRoomDescriptions();
        //LoadPredeterminedAudio();
    }

    private void Update()
    {
        // Calls until the audio sync is assigned
        getAvatarVoice();

        // Calls continously to check for a role change
        getGuideRole();
    }
    
    private void LoadPredeterminedAudio()
    {
        humanApology = Resources.Load<AudioClip>("Audio/humanApologyRiver");
        robotApology = Resources.Load<AudioClip>("Audio/robotApologyWill");
        caneApology = Resources.Load<AudioClip>("Audio/caneApologyCallum");
        dogApology = Resources.Load<AudioClip>("Audio/dogApologyJessica");
        birdApology = Resources.Load<AudioClip>("Audio/birdApologyGeorge");
        invisibleApology = Resources.Load<AudioClip>("Audio/invisibleApologyMatilda");
    }

    private void getAvatarVoice()
    {
        if (_avatarVoice == null)
            _avatarVoice = GameObject.FindWithTag("Player").GetComponentInChildren<RealtimeAvatarVoice>();
    }

    public void getGuideRole()
    {
        // Do checks to ensure role has been initialized with its most recent values so we don't go out of bounds
        if (m_AIGuideScript == null)
            m_AIGuideScript = GetComponent<AIGuide>(); // This check happens when we call it from the realtime set-up

        int index = m_AIGuideScript.role - 1;

        if (index < 0 || index >= roles.Count)
            return;
        
        // The role becomes the string value contained at the index we sent over from AIGuide
        role = roles[index];
    }

    // Checks if the result is guide or modify before we send a reply to PlayHT to be converted to audio
    public string CheckForGuidanceOrModification(string result)
    {
        // If the result was a GameObject for guidance, create a custom speech message
        string[] words = result.Split(',');
        if (words.Length == 2)
        {
            Debug.Log("Two word response for guidance or modification");

            // Define characters to strip: whitespace, periods, commas, and quotes
            char[] charsToTrim = { ' ', '.', ',', '"', '\'', '!', '?' };
            string secondWord = words[1].Trim(charsToTrim);
            Debug.Log(words[1]);
            if (secondWord.Equals("guide", StringComparison.OrdinalIgnoreCase) || secondWord.Equals("teleport", StringComparison.OrdinalIgnoreCase))
            {
                // Assign the first word to targetName and the second word to modeOfTransportation
                string targetName = words[0].Trim();
                modeOfTransportation = words[1].Trim();

                targetForGuidance = GameObject.Find(targetName);
                if (targetForGuidance != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. Press the grip button to confirm if you wish to be guided, and I will take you to " + targetForGuidance.name;
                            break;
                        case 2:
                            result = "Understood. Press the grip button to confirm if you wish to be guided, and I will take you to " + targetForGuidance.name;
                            break;
                        case 3:
                            result = "Very well. Press the grip button to confirm if you wish to be guided, and I will take you to " + targetForGuidance.name;
                            break;
                        case 4:
                            result = "Okay. Press the grip button to confirm if you wish to be guided, and I will take you to " + targetForGuidance.name;
                            break;
                    }
                }
            }
            else if (secondWord.Equals("modify", StringComparison.OrdinalIgnoreCase)) // they are trying to modify
            {
                // Assign the first word to targetName and the second word to modification
                string targetName = words[0].Trim();
                modeOfModification = words[1].Trim();

                targetForModification = GameObject.Find(targetName);
                if (targetForModification != null)
                {
                    int randReply = UnityEngine.Random.Range(1, 5);

                    switch (randReply)
                    {
                        case 1:
                            result = "Alright. I will add an audio beacon to " + targetForModification.name;
                            break;
                        case 2:
                            result = "Understood. I will add an audio beacon to " + targetForModification.name;
                            break;
                        case 3:
                            result = "Very well. I will add an audio beacon to " + targetForModification.name;
                            break;
                        case 4:
                            result = "Okay. I will add an audio beacon to " + targetForModification.name;
                            break;
                    }
                }
            }
        }
        Debug.Log("We have a target for guidance " + targetForGuidance + " or a target for modification " + targetForModification);
        return result;
    }

    public void CheckForTargetForDescription(string textToSend)
    {
        // Split objectNames by commas into an array
        string[] names = objectNames.Split(',');

        // Loop through each name in the array
        foreach (string name in names)
        {
            string trimmedName = name.Trim();

            if (textToSend.Contains(trimmedName))
            {
                targetForDescription = GameObject.Find(trimmedName);
                Debug.Log("Found and set target: " + trimmedName);
            }
        }
        Debug.Log("No matching object found in the text.");
    }

    public void LoadRoomDescriptions()
    {
        TextAsset descriptionsAsset = Resources.Load<TextAsset>("RoomDescriptions");
        string jsonFilePath = Path.Combine(Application.dataPath, "Resources", "RoomDescriptions.json");

        if (descriptionsAsset != null)
        {
            // Load and parse the JSON file into a dictionary
            string jsonContent = File.ReadAllText(jsonFilePath);
            Dictionary<string, string> descriptionDataDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

            // Once we have the descriptions, check the current scene and set objectClassifications to the appropriate description
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string sceneDescriptionKey = currentSceneName;
            string sceneObjectsKey = currentSceneName + "_Objects";

            objectClassifications = descriptionDataDict[sceneDescriptionKey];
            objectNames = descriptionDataDict[sceneObjectsKey];

            //Debug.Log("objectClassifications set to: " + objectClassifications);
            //Debug.Log("objectNames set to: " + objectNames);

            if (objectClassifications == null || objectNames == null)
            {
                Debug.LogWarning("Description for the current scene not found in RoomDescriptions.json.");
            }
        }
        else
        {
            Debug.LogError("RoomDescriptions.json file not found in Resources folder.");
        }
    }
}