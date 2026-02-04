using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Normal.Realtime;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading;
using System.Collections.Concurrent;

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
    public AudioSource outputSource;

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private string _apiKey; // Set this securely

    // Events to hook into legacy existing Audio/UI systems
    public Action<string> OnTextReceived;
    public Action<string> OnAudioDeltaReceived; // Base64 PCM16 audio from OpenAI

    private bool _isConnected = false;

    // Microphone Variables
    private AudioClip _micClip;
    private string _micDevice;
    private int _lastMicPos;
    private bool _isRecording;

    // The Thread-Safe Queue
    private ConcurrentQueue<float[]> _audioPlaybackQueue = new ConcurrentQueue<float[]>();
    private const int SAMPLE_RATE = 24000; // OpenAI's native rate
    private int _totalSamplesSent = 0;

    // Configuration
    private const string OPENAI_REALTIME_URL = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-10-01";

    private void Start()
    {
        outputSource = GameObject.Find("Human Model").GetComponent<AudioSource>();

        _micDevice = Microphone.devices[0];
    }

    public async Task Connect(string systemInstructions)
    {
        _apiKey = ""; // Or pull from your global config
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Authorization", "Bearer " + _apiKey);
        _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            await _webSocket.ConnectAsync(new Uri(OPENAI_REALTIME_URL), CancellationToken.None);
            _isConnected = true;
            Debug.Log("Connected to Realtime API");

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
                turn_detection = new { type = "server_vad" } // Auto-detects when user stops talking!
            }
        };

        await SendJson(sessionUpdate);
    }

    public void StartRecording()
    {
        Debug.Log("Recording started");
        _isRecording = true;
        _lastMicPos = 0;
        _micClip = Microphone.Start(_micDevice, true, 20, 24000); // 24kHz is standard for OpenAI Realtime
    }

    public async Task StopRecordingAndCommit(string screenshotUrl = null)
    {
        Debug.Log("Recording stopped");
        // 1. Fully await the final chunk of audio
        await HandleMicStreaming();

        _isRecording = false;
        Microphone.End(_micDevice);

        // Might get rid of case A
        // A. If we have a screenshot, send it NOW as a user message item
        if (!string.IsNullOrEmpty(screenshotUrl))
        {
            await SendImageContext(screenshotUrl);
        }

        // 3. SAFETY GUARD: OpenAI requires >= 100ms (2400 samples at 24kHz)
        if (_totalSamplesSent > 2400)
        {
            // B. Tell OpenAI we are done talking and want a response
            await SendJson(new { type = "input_audio_buffer.commit" });
            await SendJson(new { type = "response.create" });
            Debug.Log("Requested a response");
            Debug.Log($"Committed {_totalSamplesSent} samples.");
        }
        else
        {
            Debug.LogWarning("Recording too short (<100ms). Skipping audio commit to avoid API error.");
            // Optional: Trigger a 'Response' anyway just for the Image if you want
            if (!string.IsNullOrEmpty(screenshotUrl))
            {
                await SendJson(new { type = "response.create" });
            }
        }

        _totalSamplesSent = 0; // Reset for next time
        _lastMicPos = 0;
    }

    void Update()
    {
        // 1. Microphone Streaming Logic 
        HandleMicStreaming();

        // 2. Playback Logic: Pull from the thread-safe queue on the Main Thread
        if (!outputSource.isPlaying && _audioPlaybackQueue.TryDequeue(out float[] nextChunk))
        {
            PlayAudioChunk(nextChunk);
        }
    }

    private async Task HandleMicStreaming()
    {
        if (!_isRecording || !_isConnected) return;

        int currentPos = Microphone.GetPosition(_micDevice);
        if (currentPos == _lastMicPos) return;

        float[] samples;

        // Case A: Normal read (head is ahead of last position)
        if (currentPos > _lastMicPos)
        {
            samples = new float[currentPos - _lastMicPos];
            _micClip.GetData(samples, _lastMicPos);
        }
        // Case B: Wrap-around read (head looped back to the start of the clip)
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

            byte[] pcmData = ConvertFloatsToPCM16(samples);
            string base64Audio = Convert.ToBase64String(pcmData);

            await SendJson(new { type = "input_audio_buffer.append", audio = base64Audio });
        }

        _lastMicPos = currentPos;
    }

    private void PlayAudioChunk(float[] data)
    {
        Debug.Log("Got a response chunk to play as audio");
        AudioClip clip = AudioClip.Create("ResponseChunk", data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        outputSource.clip = clip;
        outputSource.Play();
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

    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 64];
        while (_webSocket.State == WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // Realtime API sends many events. We filter for the ones we need.
            HandleServerEvent(json);
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
                case "response.audio.delta":
                    // Native Audio stream from OpenAI (Fastest possible latency)
                    Debug.Log("Got response audio");
                    string base64Audio = (string)jsonObj["delta"];
                    OnAudioDeltaReceived?.Invoke(base64Audio);
                    break;

                case "response.text.delta":
                    // If you still want to use ElevenLabs, use this text
                    Debug.Log("Got response text to use in ElevenLabs");
                    string textDelta = (string)jsonObj["delta"];
                    OnTextReceived?.Invoke(textDelta);
                    break;

                case "response.done":
                    Debug.Log("Response generation complete.");
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

    // CONVERTERS
    private byte[] ConvertFloatsToPCM16(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short val = (short)(samples[i] * 32767f);
            BitConverter.GetBytes(val).CopyTo(bytes, i * 2);
        }
        return bytes;
    }

    private void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
    }
}

public class OpenAIQueries : MonoBehaviour
{
    // OpenAI variables
    public static OpenAIClient client { get; set; }
    // OpenAI API key
    [HideInInspector] public string apiKey;
    [HideInInspector] public string playHTApiKey; // 4f450dba6e4c4a4195b430cf4ba1e6f8 ----- 3VkVgj0xRAfAA7VLT2IzCadC7h13
    [HideInInspector] public string playHTUserId; // J1wAOyXmKrak4arON6JtwT94xuA2 ---- a4acf316cf734b12b96410f11134c5d0

    // ElevenLabs test variables
    [HideInInspector] public string elevenLabsApiKey = "sk_25c3b009eb65e25d179e6f3fe10d20fd03ac7f9556308175";
    [HideInInspector] public string elevenLabsVoiceId = "21m00Tcm4TlvDq8ikWAM"; // Temp "Rachel" voice, gonna have to check out voice switching
    [HideInInspector] public string elevenLabsModelId = "eleven_turbo_v2";
    private string elevenLabsUrl = "https://api.elevenlabs.io/v1/text-to-speech";

    // Config file to hold api keys, credentials
    [HideInInspector] private const string configFileName = "config";

    // Variables to hold scripts we need access to
    private CameraSystem m_CameraSystemScript;
    private GuideAudioSync m_GuideAudioSync;
    private AIGuide m_AIGuideScript;
    public RealtimeAvatarVoice _avatarVoice;

    // Performance variables
    private float latencyStartTime;
    private float timeToFirstToken;
    private float timeToFirstAudio;
    private bool capturedFirstTokenTime;
    private bool capturedFirstAudioTime;

    // Variables to construct OpenAI queries
    private StringBuilder textBuffer = new StringBuilder(); // Buffer to accumulate GPT response chunks before sending to PlayHT
    private StringBuilder fullResponseBuilder = new StringBuilder(); // String to store past responses in conversation history
    private const int chunkSizeThreshold = 200;  // Adjust this size to control how much text to send at once
    private bool isPlayingAudio = false;
    private bool isProcessingAudioChunk = false;
    private Queue<string> chunkQueue = new Queue<string>();

    // Variables to construct and maintain conversation history
    private List<Message> conversationHistory = new List<Message>();
    private const int maxHistoryLength = 10; // Save tokens by keeping last 10 messages only
    private int allQueriesCount = 0;

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

    // Monitoring bools
    [HideInInspector] public bool recordingInProgress = false;
    [HideInInspector] public bool whisperCompleted = false;
    [HideInInspector] public bool completionCompleted = false;
    [HideInInspector] public bool alloyCompleted = false;

    // Pre-saved messages
    public AudioClip humanApology;
    public AudioClip robotApology;
    public AudioClip dogApology;
    public AudioClip caneApology;
    public AudioClip birdApology;
    public AudioClip invisibleApology;

    private void Start()
    {
        // Find and load appropriate resources
        m_AIGuideScript = GetComponent<AIGuide>();
        audioSource = GameObject.Find("Human Model").GetComponent<AudioSource>(); // Ensure we grab the guide audio source for OpenAI, not PlayAudio
        LoadConfig();
        LoadRoomDescriptions();
        //LoadPredeterminedAudio();

        // Create an instance of the OpenAI client
        client = new OpenAIClient(apiKey);
    }

    private void Update()
    {
        // Calls until the camera system and audio sync scripts are assigned
        getCameraSystem();
        getAudioSync();
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

    private void getGuideRole()
    {
        // Do checks to ensure role has been initialized with its most recent values so we don't go out of bounds
        int index = m_AIGuideScript.role - 1;

        if (index < 0 || index >= roles.Count)
            return;
        
        // The role becomes the string value contained at the index we sent over from AIGuide
        role = roles[index];
    }

    private void getCameraSystem()
    {
        if (m_CameraSystemScript == null)
            m_CameraSystemScript = FindObjectOfType<CameraSystem>();
    }

    public void CaptureAudio()
    {
        // Resets all monitoring variables to mark the start of a new query
        whisperCompleted = false;
        completionCompleted = false;
        alloyCompleted = false;

        // Records 10 secs by default
        if (!recordingInProgress)
        {
            recordingInProgress = true;
            audioSource.mute = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1;
            audioSource.clip = Microphone.Start(Microphone.devices[0], false, 10, 44100);
            Debug.Log("Recording audio");
        }

        if (audioSource == null)
            Debug.Log("microphone not detected, audio not recorded");
    }

    public async Task<string> CallWhisper(AudioClip audioClip)
    {
        // Start timers for tracking the length of response time
        latencyStartTime = Time.realtimeSinceStartup;
        capturedFirstTokenTime = false; // Reset the timing flags for a new call to whisper/new query from user
        capturedFirstAudioTime = false;
        
        // Rebuild the audio stream in Normcore to send microphone data again
        if (_avatarVoice != null)
            _avatarVoice._rebuildAudioStream = true;

        //Debug.Log("Reached Call Whisper");
        var transcriptionRequest = new OpenAI.Audio.AudioTranscriptionRequest(audioClip, "whisper-1");

        string output = "N/A";
        try
        {
            var transcriptionResponse = await client.AudioEndpoint.CreateTranscriptionAsync(transcriptionRequest);
            output = transcriptionResponse.ToString();
            Debug.Log("Transcription of user query: " + output);
            query = output;
            whisperCompleted = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Exception in CallWhisper:\n" + e);
        }
        return output;
    }

    public async Task CallChatGPTAndStreamAudioCompletions() // was string prompt, holding full query constructed in AIGuide script
    {
        // Log the initial recording of the user query so that it isn't overwritten/doesn't change while the guide is generating a response (in case of calling while it is processing)
        string currentQuery = this.query;

        // Abort if the string was empty...
        if (string.IsNullOrEmpty(currentQuery) || currentQuery == "you" || currentQuery == "You")
        {
            Debug.LogWarning("Aborting GPT call: Invalid Query -> " + currentQuery);
            audioSource.loop = false;
            // Send a customized message telling the user that the query was invalid and ask to try again
            switch (role)
            {
                case "warm, friendly, but still professional sighted guide":
                    Debug.Log("trying to play human apology");
                    audioSource.clip = humanApology;
                    audioSource.Play();
                    break;
                case "formal and assertive assistant, who talks like a robot":
                    audioSource.clip = robotApology;
                    audioSource.Play();
                    break;
                case "computer-like, succinct assistant, who gives the straight facts":
                    audioSource.clip = caneApology;
                    audioSource.Play();
                    break;
                case "very friendly, excited companion, who is eager to please who you're talking to":
                    audioSource.clip = dogApology;
                    audioSource.Play();
                    break;
                case "wise, old-fashioned, slightly Shakespearean-sounding mentor":
                    audioSource.clip = birdApology;
                    audioSource.Play();
                    break;
                case "gentle, sweet, soft-spoken assistant slipping in words here and there":
                    audioSource.clip = invisibleApology;
                    audioSource.Play();
                    break;
            }
            
            return;
        }

        // Reset buffers for a new response
        fullResponseBuilder.Clear();
        textBuffer.Clear();

        // If conversation history gets too long, remove the oldest pair of user query + guide response stored (indices 1 and 2), we keep the basePrompt (at 0)
        while (conversationHistory.Count > maxHistoryLength)
        {
            if (conversationHistory.Count > 1)
                conversationHistory.RemoveAt(1);
        }

        // Construct static, base query to send to GPT
        string basePrompt = "You are a " + role + ", named Giddy. " + contextClassification + " " + memoClassifications +
                                     " The names and descriptions of key objects are: " + objectClassifications +
                                     " " + queryClassifications;

        // Update query with conversation history and user prompt - index 0 should always remain the basePrompt with guide instructions + most up-to-date roles, object descriptions depending on the scene
        if (conversationHistory.Count == 0 || conversationHistory[0].Role != Role.System)
            conversationHistory.Insert(0, new Message(Role.System, basePrompt)); // If history is empty, insert new basePrompt
        else
            conversationHistory[0] = new Message(Role.System, basePrompt); // If history exists, update it again since the prompt info can change as the user moves between scenes

        // Prepare chat request body for API
        var content = new List<Content>
        {
            new Content(ContentType.Text, query),
            new Content(ContentType.ImageUrl, m_CameraSystemScript.birdsEyeImageLink),
            new Content(ContentType.ImageUrl, m_CameraSystemScript.viewpointImageLink)
        };

        // Add the user's prompt/recorded message + images to conversation history
        conversationHistory.Add(new Message(Role.User, content));

        // Send the ENTIRE history with the basePrompt instructions
        // might try using a faster model like gpt-3.5-turbo, fewer max tokens, figure out how to implement caching, optimize the prompt
        var chatRequest = new ChatRequest(conversationHistory, model: "gpt-4o", maxTokens: 300); // was gpt-4-vision-preview, deprecated in Dec 2024

        // Use StreamCompletionAsync to stream the responses
        try
        {
            await client.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                // Check if the partial response has choices and if the content is not null
                if (partialResponse.Choices != null && partialResponse.Choices.Count > 0)
                {
                    var delta = partialResponse.Choices[0].Delta;

                    // Make sure the delta and content are not null
                    if (delta != null && delta.Content != null)
                    {
                        // Check if this is the first token streamed for our timing
                        if (!capturedFirstTokenTime)
                        {
                            timeToFirstToken = Time.realtimeSinceStartup - latencyStartTime;
                            Debug.Log($"[Timing] Time to First Token (GPT Response): {timeToFirstToken:F2} seconds");
                            capturedFirstTokenTime = true;
                        }

                        // Accumulate each partial response into the full response text for conversation history
                        fullResponseBuilder.Append(delta.Content);
                        
                        // Serialize each partial response as it comes to JSON for audio streaming logic
                        var jsonResponse = JsonConvert.SerializeObject(partialResponse);
                        StartCoroutine(StreamChatGptResponseToAudio(jsonResponse));
                    }
                }
            });
            
            // After the full stream is complete, save full guide response to conversation history
            string finalResponseText = fullResponseBuilder.ToString();
            conversationHistory.Add(new Message(Role.Assistant, finalResponseText));
            allQueriesCount++;
            float totalResponseGenerationTime = Time.realtimeSinceStartup - latencyStartTime;

            Debug.Log("Finished streaming response text. Added to history.");
            Debug.Log($"History Count: {conversationHistory.Count}");
            Debug.Log("Response from guide: " + finalResponseText);
            Debug.Log("User question: " + currentQuery.ToString());

            LogInteractionData(
                query,
                finalResponseText,
                role,
                totalResponseGenerationTime,
                allQueriesCount
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Error in streaming GPT-4 response: " + e.Message);
        }
    }

    private void LogInteractionData(string userText, string aiText, string currentRole, float generationTime, int currentQueryCount)
    {
        InteractionLog newLog = new InteractionLog();

        newLog.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
        newLog.queryNumber = currentQueryCount.ToString();
        newLog.userQuery = userText;
        newLog.guideResponse = aiText;
        newLog.guideRole = currentRole;

        // Check if we reset the targets between selection
        if (targetForDescription != null)
            newLog.chosenObjectTarget = targetForDescription.ToString();

        newLog.latencyToFirstToken = timeToFirstToken;
        newLog.latencyToFirstAudio = timeToFirstAudio;
        newLog.totalGenerationTime = generationTime;

        /*
        public string chosenObjectTarget;
        public string chosenAction;
         */
    }

    // Coroutine to aggregate the GPT response and stream text to PlayHT in chunks
    private IEnumerator StreamChatGptResponseToAudio(string jsonResponse)
    {
        //Debug.Log("Received JSON response: " + jsonResponse);
        // Parse the JSON response
        var jsonObject = JObject.Parse(jsonResponse);
        var content = jsonObject["choices"]?[0]?["delta"]?["content"]?.ToString();
        var finishReason = jsonObject["choices"]?[0]?["finish_reason"]?.ToString();

        // Check if content is null or empty
        if (!string.IsNullOrEmpty(content))
        {
            //Debug.Log("content added " + content);
            textBuffer.Append(content); // Add the extracted content to the buffer

            // If the buffer reaches a certain size, send it to PlayHT for real-time audio conversion
            if (textBuffer.Length >= chunkSizeThreshold || content.EndsWith(".") || content.EndsWith("!"))
            {
                string textToSend = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(textToSend))
                {
                    //Debug.Log("Queuing chunk for PlayHT: " + textToSend);
                    chunkQueue.Enqueue(textToSend);
                    CheckForTargetForDescription(textToSend);
                    textBuffer.Clear();  // Clear the buffer after queuing
                }
            }
        }

        // Handle the case where finish_reason is "stop" and the message finished without meeting one of the above requirements
        if (finishReason == "stop")
        {
            if (textBuffer.Length > 0) // Check if there's any remaining content in the buffer to process
            {
                string remainingText = textBuffer.ToString().Trim();
                if (!string.IsNullOrEmpty(remainingText))
                {
                    //Debug.Log("Queuing final chunk for PlayHT: " + CheckForGuidanceOrModification(remainingText));
                    chunkQueue.Enqueue(CheckForGuidanceOrModification(remainingText));  // Add the final chunk to the queue
                    // Debug.Log("Targets for guidance are: " + targetForGuidance + " // Targets for modification are: " + targetForModification);
                    textBuffer.Clear();  // Clear the buffer after queuing
                }
            }
        }

        // Start processing the chunks in the queue (if not already processing)
        if (!isProcessingAudioChunk && chunkQueue.Count > 0)
        {
            //Debug.Log("Starting chunk processing...");
            StartCoroutine(ProcessChunkQueue());
        }
        yield return null; // Yield to keep the coroutine responsive
    }

    // Coroutine to process the chunk queue one by one
    private IEnumerator ProcessChunkQueue()
    {
        while (chunkQueue.Count > 0)
        {
            // Wait until the previous chunk is processed before sending the next one
            isProcessingAudioChunk = true;

            string textToSend = chunkQueue.Dequeue();  // Get the next chunk from the queue
            Debug.Log("Sending chunk to PlayHT: " + textToSend);

            // If the response needs to be shared over network, use GuideAudioSync to share audio; if local, start the coroutine locally
            //if (!ShareResponseBasedOnRole(textToSend)) // Process based on role if necessary
                //yield return StartCoroutine(StreamTextToPlayHT(textToSend)); // Call the coroutine to send text to PlayHT and convert it to audio

            // Uncomment the above when we have the architecture and move to GuideAudioSync - GAS is what calls the streaming UNLESS we're local only (invisible guide)
            yield return StartCoroutine(StreamTextToPlayHT(textToSend));

            // Regardless of output above, we should factor in a delay so we don't send calls overlapping over the network or locally
            isProcessingAudioChunk = false;  // Mark the chunk processing as complete

            // Wait for a short delay between chunks (optional)
            yield return new WaitForSeconds(0.1f);  // Adjust the delay as needed
        }
        Debug.Log("All chunks processed.");
    }

    // Coroutine to send a chunk of text to PlayHT for real-time audio conversion
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

        // Default payload without extra voice prompts
        /*payloadObj = new
        {
            text = textChunk,
            model_id = elevenLabsModelId,
            voice_settings = new
            {
                stability = 0.5f,
                similarity_boost = 0.7f
            }
        };*/

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

        /*
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
        */
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

        /*
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
                audioSource.clip = audioClip;
                audioSource.loop = false;
                float startTime = Time.time;  // Capture the time when the audio starts
                float clipLength = audioSource.clip.length;

                // Check if this is the first audio playback for timing
                if (!capturedFirstAudioTime)
                {
                    timeToFirstAudio = Time.realtimeSinceStartup - latencyStartTime;
                    Debug.Log($"[Timing] Time to First Audio (User Hears Voice): {timeToFirstAudio:F2} seconds");
                    capturedFirstAudioTime = true;
                }

                audioSource.Play();

                // Wait until the audio has finished playing before allowing the next chunk
                while (audioSource.isPlaying) // was just yield return null in the while loop
                {
                    float elapsedTime = Time.time - startTime;
                    // If the audio has reached a not playing state, or the time it is active is longer than the length of the clip, manually stop it
                    // For highlights
                    if (elapsedTime >= clipLength)
                    {
                        audioSource.Stop();  // Force stop if it somehow keeps playing
                        Debug.Log("Audio manually stopped.");
                        break;
                    }
                    yield return null;
                }

                Debug.Log("Audio chunk finished playing.");
            }
        }
        isPlayingAudio = false;
        */
    }

    // Determines if the response needs to be shared and played over the network or just locally
    private bool ShareResponseBasedOnRole(string response)
    {
        if (m_AIGuideScript.role != 6)
        {
            audioSource.mute = true;
            SetNewResult(response);
            return true; // Needs to share over network
        }
        else
        {
            audioSource.mute = false;
            return false; // Needs to only play locally
        }
    }

    // Checks if the result is guide or modify before we send a reply to PlayHT to be converted to audio
    private string CheckForGuidanceOrModification(string result)
    {
        // If the result was a GameObject for guidance, create a custom speech message
        string[] words = result.Split(',');
        if (words.Length == 2)
        {
            Debug.Log("Two word response for guidance or modification");
            string secondWord = words[1].Trim();
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
        return result;
    }

    private void CheckForTargetForDescription(string textToSend)
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

    private void LoadRoomDescriptions()
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

    private void getAudioSync()
    {
        if (m_GuideAudioSync == null)
            m_GuideAudioSync = FindObjectOfType<GuideAudioSync>();
    }

    public void SetNewResult(string result)
    {
        //Debug.Log("Reached SetNewResult");
        if (m_GuideAudioSync != null)
            m_GuideAudioSync.SetResult(result);
        else
            Debug.LogError("GuideAudioSync is not initialized.");
    }

    private class ConfigData
    {
        public string APIKey;
        public string PlayHTAPIKey;
        public string PlayHTUserID;
    }
}