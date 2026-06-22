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
using OpenAI;

public class ConfigData
{
    public string APIKey;
}

public class RealtimeGuideClient : MonoBehaviour
{
    // Access the OpenAIQueries class so we can change variables as needed
    private OpenAIQueries _openAIQueriesScript;

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
    public bool _isAiSpeaking = false;
    public bool _isProcessingCommand = false;
    private bool _foundFirstSentence = false;
    private bool _isResponseActive = false;
    private bool _isUserSpeaking = false;

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
    private const string OPENAI_REALTIME_URL = "wss://api.openai.com/v1/realtime?model=gpt-realtime-2"; 

    private StringBuilder _textBuffer = new StringBuilder(); // Buffer to accumulate GPT response chunks

    private void Start()
    {
        _openAIQueriesScript = FindObjectOfType<OpenAIQueries>();
        if (_openAIQueriesScript != null)
            Debug.Log("Found the queries script");

        // Open a client for getting descriptions of images
        client = new OpenAIClient(_apiKey);

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
            await SendSessionUpdate(systemInstructions);
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
                    },
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
                    }
                }
            }
        };

        await SendJson(sessionUpdate);
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
        //Debug.Log("Closing continuous session quietly.");

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

            // Voice activity detection is only used for the default push-to-talk flow.
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
            //Debug.Log("User is talking");
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

                //Debug.Log("Silence detected. Auto-stopping recording.");

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
                    _isUserSpeaking = true;
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
                    _isUserSpeaking = false;
                    OnServerDetectedSpeechStop?.Invoke(); // The Server VAD heard the user stop speaking and is generating a response
                    _isResponseActive = true;
                    // Reset timer on the last time the user prompted the guide
                    break;

                case "response.created":
                    
                    _textBuffer.Clear();
                    _isAiSpeaking = true;
                    _isResponseActive = true; // redundant safety catch
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
                    string textDelta = (string)jsonObj["delta"];
                    _textBuffer.Append(textDelta);
                    //Debug.Log($"Text Chunk: {textDelta}");

                    OnTextReceived?.Invoke(textDelta);
                    break;

                case "response.done":
                    _isAiSpeaking = false;
                    _isResponseActive = false;
                    // Log the FULL details to see why it finished
                    var responseObj = jsonObj["response"];
                    string status = (string)responseObj["status"];

                    if (status == "completed")
                    {
                        string remainingText = _textBuffer.ToString().Trim();
                        //Debug.Log($"Full Response Captured: {remainingText}");
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
                        _openAIQueriesScript.modeOfTransportation = "guide";
                        _openAIQueriesScript.targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

                        if (_openAIQueriesScript.targetForGuidance != null)
                        {
                            string[] options = {
                                $"Of course. Press the grip button to confirm, and I will take you to the {targetName}.",
                                $"Sure. Squeeze the grip button to confirm and I'll lead the way to the {targetName}."
                            };
                            string audioResponse = options[UnityEngine.Random.Range(0, options.Length)];
                            //string audioResponse = $"Press the grip button to confirm, and I will take you to the {targetName}.";
                            _ = SpeakCustomText(audioResponse); // Inject custom confirmation audio
                        }
                    }
                    else if (functionName == "trigger_teleportation")
                    {
                        _openAIQueriesScript.modeOfTransportation = "teleport";
                        _openAIQueriesScript.targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

                        if (_openAIQueriesScript.targetForGuidance != null)
                        {
                            string[] options = {
                                $"Sure. If you'd like me to teleport us to the {targetName}, just press the grip button.",
                                $"Of course. I'm ready to teleport us to the {targetName}. Just confirm with the grip button."
                            };
                            string audioResponse = options[UnityEngine.Random.Range(0, options.Length)];
                            //string audioResponse = $"Press the grip button to confirm, and I will teleport us to the {targetName}.";
                            _ = SpeakCustomText(audioResponse);
                        }
                    }
                    else if (functionName == "trigger_modification")
                    {
                        _openAIQueriesScript.modeOfModification = "modify";
                        //Debug.Log("Going to pass on a command to modify an object");
                        _openAIQueriesScript.targetForModification = _openAIQueriesScript.GetClosestObjectByName(targetName);

                        if (_openAIQueriesScript.targetForModification != null)
                        {
                            string audioResponse = $"I have added an audio beacon to the {targetName}.";
                            _ = SpeakCustomText(audioResponse);
                        }
                    }

                    // send a response back to the API acknowledging the tool was handled
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

    // Sending images directly to realtime
    public void SendVisualContext(string viewpointBase64, string birdsEyeBase64)
    {
        var eventData = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "input_text",
                        text = "[Visual Context] You are looking at two views of the player's VR scene. Image 1 is the player's view, Image 2 is a bird's eye of the scene. If you ever see a person's avatar with a gray hoodie, black hair, and glasses, that is YOUR avatar, the guide."
                    },
                    new
                    {
                        type = "input_image",
                        image_url = viewpointBase64
                    },
                    new
                    {
                        type = "input_image",
                        image_url = birdsEyeBase64
                    }
                }
            }
        };

        //Debug.Log($"[Realtime] Injecting session with visual content");
        SendJson(eventData); // Send the data instead of serializing, since SendJson serializes already
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
    }

    // Call this when the user starts their voice input (button down)
    public void ResetCommandLock()
    {
        _isProcessingCommand = false;
        //Debug.Log("Lock Reset: Ready for new user commands.");
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
    public string contextClassification = "YOUR EYES (Visual Context): You will receive periodic text updates labeled 'VISUAL CONTEXT'. " +
        "This is your current reality. If you see a new person, a new object (like a cylinder), or a change in the scene, mention it naturally." +
        "As you respond to the player, speak as though you're in the scene with them - refrain from mentioning aspects of your internal architecture.";
    [HideInInspector]
    public string objectClassifications = ""; // Manual descriptions of key objects: left blank to be dynamically set by RoomDescriptions file
    [HideInInspector]
    public string guideRules = "GUIDANCE RULES: If a new object/avatar appears that is NOT in the Registry, describe it spatially (e.g., 'A new player just joined, standing to your left'). " +
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

    // OpenAI audio, text message, result variables
    [HideInInspector] public string text;
    [HideInInspector] public GameObject targetForGuidance;
    [HideInInspector] public string modeOfTransportation;
    [HideInInspector] public GameObject targetForModification;
    [HideInInspector] public string modeOfModification;

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
