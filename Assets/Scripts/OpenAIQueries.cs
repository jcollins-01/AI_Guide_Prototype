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

    // Tracking bools for general processes
    public bool _isConnected = false;
    private bool _guideAudioSourceFound = false;
    private bool _isAiSpeaking = false;
    public bool _isProcessingCommand = false;
    private bool _foundFirstSentence = false;

    // For handling special case speech
    private string _firstFullSentence;
    private long _totalSamplesReceived = 0;
    private long _samplesAtFirstSentence = 0;

    // Microphone Variables
    private AudioClip _micClip;
    private string _micDevice;
    private int _lastMicPos;
    private bool _isRecording;

    // Variables for voice detection
    public bool _continuousVoiceOn = false; // Set from AIGuide
    [HideInInspector] public bool _isContinuousSessionActive = false;
    public Action OnServerDetectedSpeechStart;
    public Action OnServerDetectedSpeechStop;

    public bool _defaultPushToTalkOn = true;
    public bool _legacyHoldToSpeakOn = false;
    public Action OnAutoStopRecording; // Sent to AIGuide (to tell it when the voice has stopped)
    private float _silenceTimer = 0f;
    private float _silenceThreshold = 1.2f; // Seconds of silence before auto-stopping
    private float _volumeThreshold = 0.05f; // Minimum volume to be considered talking, was 0.02f, caught computer fan
    private bool _hasSpoken = false; // Prevents auto-stopping before a user starts talking

    // Variables for other customizations
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
    private const string OPENAI_REALTIME_URL = "wss://api.openai.com/v1/realtime?model=gpt-realtime-2"; // was gpt-4o-realtime-preview, was deprecated on May 7th - 

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

    public async Task Connect(string systemInstructions, bool usingBaseline)
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Authorization", "Bearer " + _apiKey);
        //_webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1"); // this was the beta call, now deprecated

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
            if (usingBaseline)
                await SendSessionUpdate(systemInstructions);
            else
                await FirstSessionUpdate(systemInstructions);
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection Failed: {e.Message}");
        }
    }

    private async Task SendSessionUpdate(string instructions)
    {
        Debug.Log("Sending all functions at once for first session (baseline or all combined intention guide)");
        // Dynamically assign the turn_detection to be either null (push to talk) or handled by the voice activity
        object turnDetectionConfig = _continuousVoiceOn ? new { type = "server_vad" } : null;

        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                type = "realtime", // required by the general model, new from beta
                output_modalities = new[] { "audio" }, // Ask for just audio, now assumes text is included
                instructions = instructions,
                audio = new
                {
                    input = new
                    {
                        // Format is now an object, not a string
                        format = new { type = "audio/pcm", rate = 24000 }, // format = "pcm16",
                        turn_detection = turnDetectionConfig
                    },
                    output = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 }, // format = "pcm16",
                        voice = "alloy" // Options: alloy, echo, shimmer
                    }
                },
                /*voice = "alloy", // Options: alloy, echo, shimmer
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                turn_detection = turnDetectionConfig,*/
                tools = new[] // Allows us to make a case to directly call our Unity functions for guidance, no string parsing/partially generated responses
                {
                    new
                    {
                        type = "function",
                        name = "trigger_guidance",
                        description = "Call this when the user wants you to take them to a specific object, or asks for sighted guide to a specific object.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                target_object = new { type = "string", description = "The exact name of the object the user wants to go to, chosen from the Navigation Registry." }
                            },
                            required = new[] { "target_object" }
                        }
                    },
                    new
                    {
                        type = "function",
                        name = "trigger_teleportation",
                        description = "Call this when the user wants you to teleport them directly to a specific object.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                target_object = new { type = "string", description = "The exact name of the object the user wants to go to, chosen from the Navigation Registry." }
                            },
                            required = new[] { "target_object" }
                        }
                    } // Deprecated modification + audio beacons for now
                    /*,
                    new
                    {
                        type = "function",
                        name = "trigger_modification",
                        description = "Call this when the user wants you to modify an object or add an audio beacon to it.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                target_object = new { type = "string", description = "The exact name of the object to modify, chosen from the Navigation Registry." }
                            },
                            required = new[] { "target_object" }
                        }
                    }*/
                }
            }
        };

        await SendJson(sessionUpdate);
    }

    // For the improved guide, when it only gives basic context to the guide so a human user can switch the context
    private async Task FirstSessionUpdate(string instructions)
    {
        Debug.Log("Sending only basic guide context (manual intention guide)");
        // Dynamically assign the turn_detection to be either null (push to talk) or handled by the voice activity
        object turnDetectionConfig = _continuousVoiceOn ? new { type = "server_vad" } : null;

        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                type = "realtime", // required by the general model, new from beta
                output_modalities = new[] { "audio" }, // Ask for just audio, now assumes text is included
                instructions = instructions,
                audio = new
                {
                    input = new
                    {
                        // Format is now an object, not a string
                        format = new { type = "audio/pcm", rate = 24000 }, // format = "pcm16",
                        turn_detection = turnDetectionConfig
                    },
                    output = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 }, // format = "pcm16",
                        voice = "alloy" // Options: alloy, echo, shimmer
                    }
                }
            }
        };

        await SendJson(sessionUpdate);
    }

    // may have to have another version to trigger the guidance function // UpdateGuidancePrompt
    public async Task UpdateLivePrompt(string newInstructions)
    {
        if (!_isConnected) return;

        var updateSession = new
        {
            type = "session.update",
            session = new
            {
                type = "realtime", // required by the general model, new from beta
                instructions = newInstructions
            }
        };
        Debug.Log("The instructions received were: " + newInstructions);
        Debug.Log("[Realtime] Dynamically updating guide instructions on the server...");
        await SendJson(updateSession);
    }

    public async Task UpdateGuidancePrompt(string newInstructions)
    {
        if (!_isConnected) return;

        var updateSession = new
        {
            type = "session.update",
            session = new
            {
                type = "realtime", // required by the general model, new from beta
                instructions = newInstructions,
                tools = new[] 
                {
                    new
                    {
                        type = "function",
                        name = "trigger_guidance",
                        description = "Call this when the player wants you to take them to a specific object, or asks for sighted guide to a specific object.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                target_object = new { type = "string", description = "The exact name of the object the user wants to go to, chosen from the Navigation Registry." }
                            },
                            required = new[] { "target_object" }
                        }
                    },
                    new
                    {
                        type = "function",
                        name = "trigger_teleportation",
                        description = "Call this when the player wants you to teleport them directly to a specific object.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                target_object = new { type = "string", description = "The exact name of the object the user wants to go to, chosen from the Navigation Registry." }
                            },
                            required = new[] { "target_object" }
                        }
                    }
                }
            }
        };
        Debug.Log("The instructions received were: " + newInstructions);
        Debug.Log("[Realtime] Dynamically updating session to do guidance actions...");
        await SendJson(updateSession);
    }

    public void StartRecording()
    {
        if (!_isConnected) return;
        Debug.Log("Recording started");

        // Reset variables for voice detection
        _hasSpoken = false;
        _silenceTimer = 0f;

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

        // Only commit if we actually sent audio (prevents empty call errors)
        if (_totalSamplesSent > 0)
        {
            // Tell OpenAI we are done talking and want a response
            await SendJson(new { type = "input_audio_buffer.commit" });
            await SendJson(new { type = "response.create" });
            //Debug.Log($"Committed {_totalSamplesSent} samples and requested response.");
        }
        else
        {
            // Debug.LogWarning("Recording too short (no audio sent). Skipping audio commit to avoid API error.");
            _openAIQueriesScript.PlayPredeterminedAudio("emptyQuery");
        }

        _totalSamplesSent = 0; // Reset for next time
        _lastMicPos = 0;
    }

    public async Task StopRecordingSilently()
    {
        Debug.Log("Closing continuous session quietly.");

        _isRecording = false;
        _isContinuousSessionActive = false;

        if (Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);

        if (_isConnected)
        {
            // Tell the server to forget any audio it just heard
            await SendJson(new { type = "input_audio_buffer.clear" });

            // If the AI is currently mid-sentence while you turn it off, shut it up immediately.
            await StopAiSpeech();
        }

        _totalSamplesSent = 0;
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

            // Voice activity detection is only used for the default push-to-talk flow.
            if (_defaultPushToTalkOn && !_continuousVoiceOn && !_legacyHoldToSpeakOn)
                ProcessVoiceActivity(samples);

            // Noise gate to ensure we aren't treating backround noise/AI voice as user voice
            float maxVol = 0f;
            foreach (var s in samples) if (Mathf.Abs(s) > maxVol) maxVol = Mathf.Abs(s);

            // Check if the AI is generating a response or currently playing the tail end of a prior response
            bool isOutputtingSound = _isAiSpeaking || (outputSource != null && outputSource.isPlaying);

            // If the AI is speaking, and the mic isn't picking up a loud interruption, zero out the audio to prevent the AI from hearing its own echo
            if (isOutputtingSound && maxVol < 0.15f)
                Array.Clear(samples, 0, samples.Length); // Fills the array with 0s (pure silence)

            byte[] pcmData = ConvertFloatsToPCM16(samples);
            string base64Audio = Convert.ToBase64String(pcmData);
            await SendJson(new { type = "input_audio_buffer.append", audio = base64Audio });
        }
    }

    private void ProcessVoiceActivity(float[] samples)
    {
        float maxVol = 0f;
        foreach (var s in samples)
        {
            if (Mathf.Abs(s) > maxVol) maxVol = Mathf.Abs(s);
        }

        // If volume spikes above our threshold, the user is talking
        if (maxVol > _volumeThreshold)
        {
            Debug.Log("User is talking");
            _hasSpoken = true;
            _silenceTimer = 0f; // Reset the silence timer
        }
        // If they were talking, but are now quiet
        else if (_hasSpoken)
        {
            // Calculate how much time this chunk of samples represents
            _silenceTimer += (float)samples.Length / SAMPLE_RATE;

            // If they have been silent longer than our threshold
            if (_silenceTimer > _silenceThreshold)
            {
                _hasSpoken = false;
                _silenceTimer = 0f;

                Debug.Log("Silence detected. Auto-stopping recording.");

                // Alert AIGuide that the user has stopped speaking
                OnAutoStopRecording?.Invoke();

                // Execute the stop and commit
                _ = StopRecordingAndCommit();
            }
        }
    }

    // Handle the incoming RPC data on Remote Clients
    public void ReceiveRemoteAudio(string base64Audio)
    {
        Debug.Log("Received remote audio from a guide on another client - converting locally");
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
                case "input_audio_buffer.speech_started":
                    OnServerDetectedSpeechStart?.Invoke();

                    // If the AI is talking, instantly shut it up locally
                    if (_isAiSpeaking)
                    {
                        Debug.Log("Server VAD detected user interruption. Clearing local audio.");
                        ClearLocalAudioBuffer();
                        _isAiSpeaking = false;
                    }
                    break;

                case "input_audio_buffer.speech_stopped":
                    OnServerDetectedSpeechStop?.Invoke(); // The Server VAD heard the user stop speaking and is generating a response
                    break;

                case "response.created":
                    
                    _textBuffer.Clear();
                    _isAiSpeaking = true;
                    _foundFirstSentence = false;
                    _totalSamplesReceived = 0;
                    break;
                
                case "response.output_audio.delta": // was response.audio.delta
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

                        // Capture the samples of audio in this sentence -- then we can compare THAT to the audio source samples
                        _totalSamplesReceived += floatData.Length;
                        _audioPlaybackQueue.Enqueue(floatData); // Send the samples to be played by the audio source

                        // Check if we should broadcast this to the network
                        //if (ShouldShareResponse() && guideAudioSync != null)
                            //guideAudioSync.BroadcastAudioChunk(floatData);

                        OnAudioDeltaReceived?.Invoke(base64Audio);
                        break;
                    }

                case "response.audio_transcript.delta": // Use this instead of or in addition to text.delta
                    string transcriptDelta = (string)jsonObj["delta"];
                    _textBuffer.Append(transcriptDelta);

                    _firstFullSentence = "";
                    bool isSentenceEnder = transcriptDelta.EndsWith(".") || transcriptDelta.EndsWith("!") || transcriptDelta.EndsWith("?"); ;
                    if (isSentenceEnder && !_foundFirstSentence && _textBuffer.ToString().Length > 15)
                    {
                        _foundFirstSentence = true;
                        _firstFullSentence = _textBuffer.ToString();
                        //Debug.Log($"First full sentence is {_firstFullSentence}");

                        // Assuming a standard speaking rate of ~15 characters per second and a standard OpenAI sample rate of 24,000 Hz
                        float estimatedSeconds = _firstFullSentence.Length / 15f - 0.4f; // substract 400 ms for the delay
                        _samplesAtFirstSentence = (int)(estimatedSeconds * 24000);
                        //Debug.Log($"Total samples needed for first sentence from assuming standard speaking is {_samplesAtFirstSentence}");
                    }

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
                        Debug.Log($"Text Chunk: {textDelta}");

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
                    }
                    else
                    {
                        Debug.LogError($"Response Finished with Error: {status}");
                        Debug.LogError($"Details: {responseObj["status_details"]}");
                    }
                    break;

                case "response.function_call_arguments.done":
                    string callId = (string)jsonObj["call_id"];
                    string functionName = (string)jsonObj["name"];
                    string argumentsJson = (string)jsonObj["arguments"]; // This comes in as a stringified JSON object

                    Debug.Log($"[Tools] AI called function: {functionName} with args: {argumentsJson}");

                    // Parse the arguments
                    JObject argsObj = JObject.Parse(argumentsJson);
                    string targetName = (string)argsObj["target_object"];

                    _isProcessingCommand = true; // Lock commands just like your old script did

                    if (functionName == "trigger_guidance")
                    {
                        modeOfTransportation = "guide";
                        Debug.Log("Going to pass on a command to guide the user to an object");
                        targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

                        if (targetForGuidance != null)
                        {
                            string[] options = {
                                $"Of course. Press the grip button to confirm, and I will take you to the {targetName}.",
                                $"Sure. Squeeze the grip button to confirm and I'll lead the way to the {targetName}."
                            };
                            string audioResponse = options[UnityEngine.Random.Range(0, options.Length)];
                            //string audioResponse = $"Press the grip button to confirm, and I will take you to the {targetName}.";
                            _ = SpeakCustomText(audioResponse); // Inject custom confirmation audio
                            _openAIQueriesScript.targetForGuidance = targetForGuidance;
                        }
                        else
                        {
                            Debug.Log("The target for guidance thinks it's null");
                        }
                    }
                    else if (functionName == "trigger_teleportation")
                    {
                        modeOfTransportation = "teleport";
                        Debug.Log("Going to pass on a command to teleport the user to an object");
                        targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

                        if (targetForGuidance != null)
                        {
                            string[] options = {
                                $"Sure. If you'd like me to teleport us to the {targetName}, just press the grip button.",
                                $"Of course. I'm ready to teleport us to the {targetName}. Just confirm with the grip button."
                            };
                            string audioResponse = options[UnityEngine.Random.Range(0, options.Length)];
                            //string audioResponse = $"Press the grip button to confirm, and I will teleport us to the {targetName}.";
                            _ = SpeakCustomText(audioResponse);
                            _openAIQueriesScript.targetForGuidance = targetForGuidance;
                        }
                    }
                    else if (functionName == "trigger_modification")
                    {
                        modeOfModification = "modify";
                        Debug.Log("Going to pass on a command to modify an object");
                        targetForModification = _openAIQueriesScript.GetClosestObjectByName(targetName);

                        if (targetForModification != null)
                        {
                            string audioResponse = $"I have added an audio beacon to the {targetName}.";
                            _ = SpeakCustomText(audioResponse);
                            _openAIQueriesScript.targetForModification = targetForModification;
                        }
                    }

                    // Crucial: You must send a response back to the API acknowledging the tool was handled
                    var functionResult = new
                    {
                        type = "conversation.item.create",
                        item = new
                        {
                            type = "function_call_output",
                            call_id = callId,
                            output = "{\"success\": true}" // Tell the AI the action was completed in Unity
                        }
                    };
                    _ = SendJson(functionResult);
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

        Debug.Log($"[Realtime] Injecting session with the following visual content: {text}");
        SendJson(eventData); // Send the data instead of serializing, since SendJson serializes already
    }

    public async Task SendManualPrompt(string prompt)
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
                    new { type = "input_text", text = prompt }
                }
            }
        };

        await SendJson(eventData);
        await SendJson(new { type = "response.create" });
    }

    // Gets a text description of the images taken to pass to Realtime API
    public async Task<string> GetImageDescriptionAsync(string viewpointBase64, string birdsEyeBase64)
    {
        List<Content> content = new List<Content>
            {
                new Content(ContentType.Text, "You are looking at two views of a VR scene. Image 1 is the user's view, Image 2 is a bird's eye map. Describe the scene's layout and what the user is facing in one concise paragraph."),
                new Content(ContentType.ImageUrl, viewpointBase64),
                new Content(ContentType.ImageUrl, birdsEyeBase64)
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
        //Debug.Log("Reached speak custom text");
        if (!_isConnected) return;

        // Cancel existing audio and clear the queue -- only cancel the audio is the server is streaking a response
        // in the new API, this throws a hard error that breaks the system if the response is already done streaming
        /*if (_isAiSpeaking)
        {
            await SendJson(new { type = "response.cancel" });
            _isAiSpeaking = false; // reset it locally so it's accurate
        }*/
        
        ClearLocalAudioBuffer();

        //Debug.Log($"Injecting custom TTS: {customText}");

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
                new { type = "input_text", text = $"The user has triggered a command. Your absolute priority is to say exactly this phrase and nothing else: \"{customText}\"" }
            }
            }
        };

        await SendJson(textItem);

        // Ask the API to generate the audio for that item
        await SendJson(new { type = "response.create" });
        Debug.Log("Sent the new response");
    }

    public async Task StopAiSpeech()
    {
        // Tell the server to stop generating immediately
        if (_isConnected && _webSocket.State == WebSocketState.Open)
            await SendJson(new { type = "response.cancel" });

        // Clear all local audio (the queue and the physical source)
        ClearLocalAudioBuffer();

        // Reset internal state flags
        _isAiSpeaking = false;
        _textBuffer.Clear();

        // Reset command locks if necessary
        _isProcessingCommand = false;

        Debug.Log("AI Speech interrupted and buffers cleared.");
    }

    private void ClearLocalAudioBuffer()
    {
        if (outputSource.isPlaying)
            outputSource.Stop();

        _audioPlaybackQueue.Clear();

        //Debug.Log("Local audio buffer cleared to make way for custom TTS.");
    }

    // Call this when the user starts their voice input (button down)
    public void ResetCommandLock()
    {
        _isProcessingCommand = false;
        //Debug.Log("Lock Reset: Ready for new user commands.");
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
}

public class OpenAIQueries : MonoBehaviour
{
    // Variables to hold scripts we need access to
    private AIGuide m_AIGuideScript;
    private SharedMovement m_SharedMovementScript;
    public RealtimeAvatarVoice _avatarVoice;

    // Universal guide variables
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

    // Baseline guide variables
    [HideInInspector]
    public string contextClassification = "YOUR EYES (Visual Context): You will receive periodic text updates labeled 'VISUAL CONTEXT'. +" +
        "This is your current reality. If you see a new person, a new object (like a cylinder), or a change in the scene, mention it naturally.";
    [HideInInspector]
    public string objectClassifications = ""; // Manual descriptions of key objects: left blank to be dynamically set by RoomDescriptions file
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

    // Improved guide variables
    [HideInInspector]
    public string trustGuideline = "As you guide the player, inform them of your own uncertainty and mistakes so they can gauge whether to trust your advice.";

    [HideInInspector]
    public string objectDescriptionGuideline = "1. Describe objects with the minimum viable details for the player’s current goals and context. Convey additional information upon request" +
        "2. Follow the specified order for object details: First, identify the object. Second, provide its geometric properties (shape, size, spatial relationships of its parts). " +
        "Third, provide its manipulability (how the player can tactilely interact with it) and texture. Fourth, provide its color." +
        "3. If the user requests a detailed decsription, prioritize thematic descriptions with clarifying adjectives (e.g., strong red, vibrant polka-dots).";
    /*public string objectDescriptionGuideline = "Keep object descriptions objective, concise, and jargon-free. " +
        "Follow the specified order for object details: First, define what an object is, including its name from the Navigation Registry if it is on the registry; second, provide its shape and size; third, provide its color; " +
        "fourth, provide its orientation or the spatial relationship of its parts such as handles; and fifth, provide physical properties like its material. Let the user ask follow-up questions for more details." +
        "Example Input: {What is that small thing on the table?} " +
        "Example Output: {It’s a cylindrical mug about the size of your hand, painted brown. It has a crescent-shaped handle at its midpoint, on one side of the mug. It seems to be ceramic.}" +
        "Example Input: {What's the nearest building I see over there?} " +
        "Example Output: The nearest building is a tall skyscraper called Local Hospital. It's a rectangular building around 30 meters tall and has eight floors, with blue windows, a white roof, and white walls. Its door is facing you, and it seems to be made of metal and glass.}";*/

    [HideInInspector]
    public string objectLocationGuideline = "1. When a player is over one meter away from an object, convey its location via clock-face directions and the estimated distance-to-target in a standard unit of measurement." +
        "2. When a player is within one meter of an object, convey its location with continuous, micro-steps on how the player should move (e.g., Turn left, one step forward)" +
        "3. When you give feedback on an object's location the first time, identify the object you are providing feedback on so the player can ensure it's the correct one.";
    /*public string objectLocationGuideline = "Give the object’s precise location using clock system directions and the estimated distance-to-target. " +
        "Provide the distance in a standard unit of measurement (e.g., feet and inches, or meters and centimeters)." +
        "Example Output: {The mug is at your 1 o’clock, about 2 feet away.}";*/

    [HideInInspector]
    public string sceneUnderstandingGuideline = "1. Give a scene description focused on details most relevant to a player's current context and goals. Provide more information upon request." +
        "2. Within your descriptions, mention key landmarks in the scene and the estimated distances between them in a standard unit of measurement." +
        "3. Build your descriptions around scene content that a player has already mentioned.";
    /*public string sceneUnderstandingGuideline = "If the environment is unfamiliar to the user, first give high-level information that helps them determine what kind of place they are in. " +
        "Then, mention major landmarks that are relevant to the user’s current situation or interests. Finally, note any objects or information points close to the user, giving their precise location using clock system directions and the estimated distance-to-target. " +
        "Provide the distance in a standard unit of measurement (e.g., feet and inches, or meters and centimeters). " +
        "If the environment is familiar, prioritize information about the nearest objects or information points, again providing precise locations of these objects." +
        "Example Output: {You’re in a small rectangular kitchen. There’s a counter in front of you, a sink to your left, and a doorway behind you. A box of fruit is on the floor at 12 o’clock, one foot away.}";*/

    [HideInInspector]
    public string spaceNavigationGuideline = "1. If a player wants help planning a route to walk, use allocentric spatial references to describe the space." +
        "2. If a player wants quick navigation assistance, use turn-by-turn phrasing (e.g., move forward ten feet, then turn left) to provide quick steps of what is next." +
        "3. If a player wants detailed navigation assistance, provide the following information: First, any nearby landmarks. Second, the next steps of their route." +
        "Third, a summary of their position in the overall layout of the scene (e.g., You are currently around the middle of the city market district, heading towards the north.).";
    /*public string spaceNavigationGuideline = "When a user is actively navigating, prioritize information about object locations, dimensions, and identities over other details. " +
        "Provide information on object appearance or state (i.e., what’s happening to it) only if requested or relevant for how a user needs to navigate around that object." +
        "Example Output: {You’re at a four-way intersection. The café is across the street at your 11 o’clock, twenty feet away. There is a green light at the crosswalk, showing you can walk across.}" +
        "During navigation, inform users about which directions or open spaces are traversable, and about the presence of obstacles that would impede movement." +
        "Example Output: {There is clear walking space directly ahead for about 8 feet, with a counter on your left and a wall on your right.}" +
        "Use allocentric spatial references when helping the user plan out and follow routes through the scene. " +
        "You may use the relation of landmarks or information points in the scene to each other, cardinal directions, or patterns you notice in the scene, such as streets laid out in a grid or particular shape, to help guide the user. " +
        "Use these types of references in combination or separately, based on how the user prefers to be guided." +
        "Example Output: {North is in front of you; the lake is to the northeast.} {The city streets are laid out in a grid. After passing three streets, you can turn left to reach the museum.}";*/

    [HideInInspector]
    public string grabbingObjectGuideline = "1. Provide grasping information in the following order: First, which hand the player needs to move. " +
        "Second, the direction to move it in (using the vectors left, right, up, down, forward, and backward). Third, the distance to move it in a standard unit of measurement. " +
        "Fourth, the orientation of their hand when reaching in order to grab the object." +
        "2. Provide guidance for grasping with the right hand unless the player specifies using another.";
    /*public string grabbingObjectGuideline = "When you begin helping the user grab an object, first provide the object’s precise location using clock system directions and the estimated distance-to-target. " +
        "Provide the distance in a standard unit of measurement (e.g., feet and inches, or meters and centimeters)." +
        "Then, note the body part they should move, the direction they need to move it in (using the vectors left, right, up, down, forward, and backward), the distance they need to move it (using a standard unit of measurement), and the orientation of their body part when moving in order to grab the object." +
        "Use the command “Stop” to prevent them from overreaching or to re-evaluate their movements when they have gone too far off course. After using “Stop,” re-explain the precise location of the object before beginning repeated relative guidance again." +
        "Inform the user when they have reached the target object." +
        "Example Output: {The paper cup is at 2 o’clock, ten inches away. Move your hand left two inches with your palm facing left.} {Move your hand forward five inches with your palm facing left.} {Stop. The paper cup is now at your 9 o’clock five inches away.} {Move your hand left five inches with your palm facing left.} {You are now grabbing the paper cup}";*/

    [HideInInspector]
    public string technicalSupportGuideline = "Consider common issues related to VR experiences such as guardian boundaries, headset and controller batteries, cord connections, etc. as you offer advice for any technical problems. " +
        "Be sure to ask the user follow-up questions about what exactly they are experiencing to help narrow down the issue. Be sure to repeat details from the user’s question in your follow-up communication and answers so that they know you are understanding their problems correctly." +
        "Example Output: {If you are seeing a black screen with strange lines every time you move your head, you might be too close to the headset’s guardian boundary. This is a safety setting like an invisible wall it puts around you to make sure you don’t move too much and run into something. Let’s try backing up so that you are farther away from that boundary. Did that help?}";

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

    // Pre-saved messages
    private AudioClip humanApology;
    private AudioClip robotApology;
    private AudioClip dogApology;
    private AudioClip caneApology;
    private AudioClip birdApology;
    private AudioClip invisibleApology;

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
        // Calls until the necessary components are assigned
        getAvatarVoice();
        getSharedMovement();

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
        }

        audioSource.Play();
    }

    private void getAvatarVoice()
    {
        if (_avatarVoice == null)
            _avatarVoice = GameObject.FindWithTag("Player").GetComponentInChildren<RealtimeAvatarVoice>();
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
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

    public GameObject GetClosestObjectByName(string name)
    {
        // Find EVERY object in the scene (active only)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        GameObject closest = null;
        float minDistance = Mathf.Infinity;
        Vector3 playerPos = m_SharedMovementScript.thePlayer.transform.position;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name == name)
            {
                float dist = Vector3.Distance(obj.transform.position, playerPos);
                if (dist < minDistance)
                {
                    closest = obj;
                    minDistance = dist;
                }
            }
        }

        if (closest != null)
        {
            Debug.Log($"[Logic] Found {name} closest to player at distance: {minDistance}");
        }
        return closest;
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
            else
            {
                List<string> filteredObjectClassificationsList = new List<string>();
                string[] objectClassificationsArray = objectClassifications.Split('|');
                foreach(string str in objectClassificationsArray)
                {
                    if (str.Contains(':'))
                    {
                        filteredObjectClassificationsList.Add(str);
                    }
                }
                objectClassificationsArray = filteredObjectClassificationsList.ToArray();
                objectClassifications = string.Join(" | ", objectClassificationsArray);

                // Debug.Log("These are the objectClassifications:" + objectClassifications);
                // Debug.Log("These are the objectNames the AI reads from:" + objectNames);
            }
        }
        else
        {
            Debug.LogError("RoomDescriptions.json file not found in Resources folder.");
        }
    }
}
