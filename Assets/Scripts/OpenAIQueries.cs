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
using UnityEngine.Networking;
using System.Collections;
using OpenAI;
using OpenAI.Chat;

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
    public static OpenAIClient client { get; set; }

    [HideInInspector] private const string configFileName = "config"; // Config file to hold api keys, credentials
    private string _apiKey; // Set this securely

    // Events to hook into legacy existing Audio/UI systems
    public Action<string> OnTextReceived;
    public Action<string> OnAudioDeltaReceived; // Base64 PCM16 audio from OpenAI

    public bool _isConnected = false;
    private bool _guideAudioSourceFound = false;
    private bool _isAiSpeaking = false;
    public bool _isProcessingCommand = false;

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

    // Variables for jitter on audio sharing over network
    private Queue<float[]> _jitterBuffer = new Queue<float[]>();
    private bool _isBuffering = true;
    private const int BufferThreshold = 5; // Start playing once we have 5 chunks

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

        // Open a client for getting descriptions of images
        client = new OpenAIClient(_apiKey);

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
        //Debug.Log("Adding a new message to the realtime conversation");

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
        //if (!string.IsNullOrEmpty(screenshotUrl))
            //await SendImageContext(screenshotUrl);

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
            // Debug.LogWarning("Recording too short (no audio sent). Skipping audio commit to avoid API error.");
            _openAIQueriesScript.PlayPredeterminedAudio("emptyQuery");
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

        // Handle the jittering sound of the audio over the network
        HandleJitter();

        // Find guide audio source before handling anything with output audio
        if (!_guideAudioSourceFound)
        {
            outputSource = GameObject.Find("Human Model").GetComponent<AudioSource>();
            _guideAudioSourceFound = true;
        }
        else
        {
            // Pull from the thread-safe queue on the Main Thread to figure out audio playback logic
            if (!outputSource.isPlaying && _audioPlaybackQueue.TryDequeue(out float[] nextChunk))
                PlayAudioChunk(nextChunk);
        }
    }

    private void HandleJitter()
    {
        if (!_isBuffering && _jitterBuffer.Count > 0)
        {
            // Only pull from buffer if we aren't currently playing something
            float[] nextChunk = _jitterBuffer.Dequeue();
            _audioPlaybackQueue.Enqueue(nextChunk);
        }

        // If buffer runs dry, pause and re-buffer
        if (_jitterBuffer.Count == 0) _isBuffering = true;
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
        // Convert Base64 back to float[] and play it
        byte[] pcmData = System.Convert.FromBase64String(base64Audio);
        float[] floatData = ConvertPCM16ToFloats(pcmData);

        _jitterBuffer.Enqueue(floatData);

        // If we were empty, start buffering before we play
        if (_jitterBuffer.Count >= BufferThreshold)
            _isBuffering = false;
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
                        // Call ElevenLabs to speak the text instead

                        OnTextReceived?.Invoke(textDelta);
                        break;
                    }
                    else // Only capture the text
                    {
                        string textDelta = (string)jsonObj["delta"];
                        _textBuffer.Append(textDelta);

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

    public void SendTextContext(string text)
    {
        var eventData = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[]
                {
                new { type = "input_text", text = text }
            }
            }
        };

        SendJson(eventData); // Send the data instead of serializing, since SendJson serializes already
    }

    // Gets a text description of the images taken to pass to Realtime API
    public async Task<string> GetImageDescriptionAsync(string viewpointUrl, string birdsEyeUrl)
    {
        List<Content> content = new List<Content>
            {
                new Content(ContentType.Text, "You are looking at two views of a VR scene. Image 1 is the user's view, Image 2 is a bird's eye map. Describe the scene's layout and what the user is facing in one concise paragraph."),
                new Content(ContentType.ImageUrl, viewpointUrl),
                new Content(ContentType.ImageUrl, birdsEyeUrl)
            };

        var chatPrompts = new List<Message>
            {
                new(Role.User, content),
            };

        var chatRequest = new ChatRequest(chatPrompts, model: "gpt-4o", maxTokens: 300);
        string output = "N/A";
        try
        {
            // Call the API
            var chatResponse = await client.ChatEndpoint.GetCompletionAsync(chatRequest);

            output = chatResponse.FirstChoice.ToString();
            //Debug.Log("Image description by GPT-4: " + output);
            string result = output;

            // Return the text
            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Vision Error: {e.Message}");
            return null; // Return null on failure
        }
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
        Debug.Log("Reached speak custom text");
        if (!_isConnected) return;

        // Cancel existing audio and clear the queue
        await SendJson(new { type = "response.cancel" });
        ClearLocalAudioBuffer();

        Debug.Log($"Injecting custom TTS: {customText}");

        // Create a conversation item (the text we want it to say)
        var textItem = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "system",
                content = new[]
                {
                new { type = "input_text", text = text = $"The user has triggered a command. Your absolute priority is to say exactly this phrase and nothing else: \"{customText}\"" }
            }
            }
        };

        await SendJson(textItem);

        // Ask the API to generate the audio for that item
        await SendJson(new { type = "response.create" });
    }

    private void ClearLocalAudioBuffer()
    {
        if (outputSource.isPlaying)
            outputSource.Stop();

        _audioPlaybackQueue.Clear();

        Debug.Log("Local audio buffer cleared to make way for custom TTS.");
    }

    // Call this when the user starts their voice input (button down)
    public void ResetCommandLock()
    {
        _isProcessingCommand = false;
        Debug.Log("Lock Reset: Ready for new user commands.");
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

    public string objectNames;
    public List<string> roles = new List<string>
    {
        "warm, friendly, but still professional sighted guide",
        "formal and assertive assistant, who talks like a robot",
        "computer-like, succinct assistant, who gives the straight facts",
        "very friendly, excited companion, who is eager to please who you're talking to",
        "wise, old-fashioned, slightly Shakespearean-sounding mentor",
        "gentle, sweet, soft-spoken assistant slipping in words here and there"
    };
    [HideInInspector]
    public string contextClassification = "YOUR EYES (Visual Context): You will receive periodic text updates labeled 'VISUAL CONTEXT'. +" +
        "This is your current reality. If you see a new person, a new object (like a cylinder), or a change in the scene, mention it naturally.";
    [HideInInspector]
    public string objectClassifications = ""; // Manual descriptions of key objects: left blank to be dynamically set by RoomDescriptions file
    [HideInInspector]
    public string commandClassifications = "COMMAND RULES: 1. Teleport/Guide: If the player wants to move to an object in the Registry, reply: '[Object Name], teleport' or '[Object Name], guide'." +
        "2. Modify: If they want to add sound to a Registry object, reply: '[Object Name], modify'.";
    [HideInInspector]
    public string guideRules = "GUIDANCE RULES: If a new object/avatar appears that is NOT in the Registry, describe it spatially (e.g., 'A new player just joined, standing to your left'). " +
        "For navigation, give clock-face directions (e.g., 'The door is at your 2 o'clock')." +
        "Give estimates of distance in feet (e.g., 'The trash can is 5 feet in front of you')." +
        "Never mention 'photos' or 'images.'" +
        "Limit: 150 words.";
    [HideInInspector]
    public string queryClassifications
    {
        get
        {
            return "PRIORITY: If the 'VISUAL CONTEXT' shows new avatars or objects not in the Registry, alert the player immediately. " +
                   "Use the Registry " + objectNames + " for technical commands (teleport/modify/guide). " +
                   "For everything else, rely on the Visual Context provided in the chat history. " +
                   "If a user asks 'What's around me?', synthesize the Registry and the Visual Context into a spatial summary.";
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
    private AudioClip humanApology;
    private AudioClip robotApology;
    private AudioClip dogApology;
    private AudioClip caneApology;
    private AudioClip birdApology;
    private AudioClip invisibleApology;
    private AudioClip confirmGuideAlright;
    private AudioClip confirmGuideVeryWell;

    private void Start()
    {
        // Find and load appropriate resources
        m_AIGuideScript = GetComponent<AIGuide>();
        audioSource = GameObject.Find("Human Model").GetComponent<AudioSource>(); // Ensure we grab the guide audio source for OpenAI, not PlayAudio
        LoadRoomDescriptions();
        LoadPredeterminedAudio();
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
        confirmGuideAlright = Resources.Load<AudioClip>("Audio/confirmGuideOne");
        confirmGuideVeryWell = Resources.Load<AudioClip>("Audio/confirmGuideTwo");
    }

    public void PlayPredeterminedAudio(string audioCue)
    {
        // Maybe hijack the guide voice here and have it say the custom lines for guidance/modification?
        switch(audioCue)
        {
            case "emptyQuery":
                switch (role)
                {
                    case "warm, friendly, but still professional sighted guide":
                        audioSource.clip = humanApology;
                        break;
                    case "formal and assertive assistant, who talks like a robot":
                        audioSource.clip = robotApology;
                        break;
                    case "computer-like, succinct assistant, who gives the straight facts":
                        audioSource.clip = caneApology;
                        break;
                    case "very friendly, excited companion, who is eager to please who you're talking to":
                        audioSource.clip = dogApology;
                        break;
                    case "wise, old-fashioned, slightly Shakespearean-sounding mentor":
                        audioSource.clip = birdApology;
                        break;
                    case "gentle, sweet, soft-spoken assistant slipping in words here and there":
                        audioSource.clip = invisibleApology;
                        break;
                }
                break;
            /*case "guidance":
                int randReply = UnityEngine.Random.Range(1, 3);

                switch (randReply)
                {
                    case 1:
                        audioSource.clip = confirmGuideAlright;
                        break;
                    case 2:
                        audioSource.clip = confirmGuideVeryWell;
                        break;
                }
                break;
            case "modification": // Might not need this one
                break;*/
        }

        audioSource.Play();
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
        Debug.Log("Checking string " + result);
        if (FindObjectOfType<RealtimeGuideClient>()._isProcessingCommand) return result;

        // Get all possible object names
        string[] names = objectNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        string detectedObjectName = "";
        string detectedKeyword = "";

        // Scan the result for any of our known object names
        foreach (string name in names)
        {
            string trimmedName = name.Trim();
            if (result.IndexOf(trimmedName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                detectedObjectName = trimmedName;
                break; // Found our object!
            }
        }

        // If we didn't find a known object name, just return the result as normal speech
        if (string.IsNullOrEmpty(detectedObjectName)) return result;

        // Now scan the result for our command keywords
        if (result.IndexOf("guide", StringComparison.OrdinalIgnoreCase) >= 0) detectedKeyword = "guide";
        else if (result.IndexOf("teleport", StringComparison.OrdinalIgnoreCase) >= 0) detectedKeyword = "teleport";
        else if (result.IndexOf("modify", StringComparison.OrdinalIgnoreCase) >= 0) detectedKeyword = "modify";

        // If we have BOTH an object and a keyword, trigger the logic
        if (!string.IsNullOrEmpty(detectedKeyword))
        {
            if (detectedKeyword == "guide" || detectedKeyword == "teleport")
            {
                FindObjectOfType<RealtimeGuideClient>()._isProcessingCommand = true;

                modeOfTransportation = detectedKeyword;
                targetForGuidance = GameObject.Find(detectedObjectName);

                if (targetForGuidance != null)
                {
                    // Return a randomized confirmation message
                    string[] options = {
                    $"Alright. Press the grip button to confirm, and I will take you to the {detectedObjectName}.",
                    $"Understood. If you'd like to be guided to the {detectedObjectName}, just press the grip button.",
                    $"Very well. Squeeze the grip button and I'll lead the way to the {detectedObjectName}.",
                    $"Okay! I'm ready to guide you to the {detectedObjectName}. Just confirm with the grip button."
                };
                    return options[UnityEngine.Random.Range(0, options.Length)];
                }
            }
            else if (detectedKeyword == "modify")
            {
                modeOfModification = "modify";
                targetForModification = GameObject.Find(detectedObjectName);

                if (targetForModification != null)
                {
                    return $"Understood. I am adding an audio beacon to the {detectedObjectName} now.";
                }
            }
        }

        // If we get here, it means it was just a normal conversation about an object
        // but not an actual command, so just return the original text.
        return result;
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