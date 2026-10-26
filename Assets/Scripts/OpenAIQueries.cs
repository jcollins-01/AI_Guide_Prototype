using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Normal.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using UnityEngine;

// NOTE: OpenAI's Realtime WebSocket pipeline (ClientWebSocket, streamed audio,
// server-side tool calls) has been replaced with plain HTTP calls to:
//   1) Ollama's /api/chat endpoint (text generation)
//   2) An OpenAI-compatible /v1/audio/transcriptions endpoint (speech-to-text)
//   3) An OpenAI-compatible /v1/audio/speech endpoint (text-to-speech)
// The public interface (method names, events, bools) is kept the same as the
// original RealtimeGuideClient so AIGuide.cs does not need to change.

public class ConfigData
{
    public string APIKey; // legacy field, unused now -- kept so an old config.json doesn't fail to parse

    // Optional overrides for the local server URLs/model names below.
    // Leave any of these out of config.json to keep the Inspector defaults.
    public string OllamaUrl;
    public string OllamaModel;
    public string SttUrl;
    public string SttModel;
    public string TtsUrl;
    public string TtsModel;
    public string TtsVoice;
}

public class RealtimeGuideClient : MonoBehaviour
{
    // Access the OpenAIQueries class so we can change variables as needed
    private OpenAIQueries _openAIQueriesScript;

    public AudioSource outputSource;

    [HideInInspector] private const string configFileName = "config"; // Config file in Resources/

    [Header("Local AI Server URLs -- verify these against whatever you actually stand up")]
    [Tooltip("Ollama's chat endpoint. Default assumes Ollama running locally on its default port.")]
    public string ollamaChatUrl = "http://localhost:11434/api/chat";
    [Tooltip("Used only as a quick reachability check when Connect() is called.")]
    public string ollamaHealthUrl = "http://localhost:11434/api/tags";
    [Tooltip("MUST support both 'vision' and 'tools' in its Capabilities -- verify with `ollama show <model>` " +
             "before relying on it. gemma4:e4b or qwen3-vl are current options that support both together.")]
    public string ollamaModel = "gemma4:e4b";

    [Tooltip("OpenAI-compatible transcription endpoint, e.g. from Speaches or Kokoro-FastAPI.")]
    public string sttUrl = "http://localhost:8000/v1/audio/transcriptions";
    [Tooltip("Confirmed working: Systran/faster-whisper-small. whisper-1 also works but auto-maps to the much larger/slower faster-whisper-large-v3.")]
    public string sttModel = "Systran/faster-whisper-small";

    [Tooltip("OpenAI-compatible speech endpoint, e.g. from Speaches or Kokoro-FastAPI.")]
    public string ttsUrl = "http://localhost:8000/v1/audio/speech";
    [Tooltip("Confirmed against Speaches' own docs.")]
    public string ttsModel = "speaches-ai/Kokoro-82M-v1.0-ONNX";
    [Tooltip("Confirmed against Speaches' own docs for the Kokoro model -- change if you use a different TTS model/server.")]
    public string ttsVoice = "af_heart";

    private static readonly HttpClient _httpClient = new HttpClient();

    // Events to hook into legacy existing Audio/UI systems
    public Action<string> OnTextReceived;
    public Action<string> OnAudioDeltaReceived; // kept for interface compatibility; not populated in the HTTP pipeline (no incremental audio delta)

    // Tracking bools for general processes
    public bool _isConnected = false;
    private bool _guideAudioSourceFound = false;
    public bool _isAiSpeaking = false;
    public bool _isProcessingCommand = false;
    private bool _isUserSpeaking = false;

    // Microphone Variables
    private AudioClip _micClip;
    private string _micDevice;
    private int _lastMicPos;
    private bool _isRecording;

    // Buffer for the whole utterance (replaces streaming samples straight to a socket)
    private List<float> _pendingRecordingSamples = new List<float>();

    // Variables for voice detection
    public bool _continuousVoiceOn = false; // NOTE: continuous mode currently behaves the same as push-to-talk --
                                            // OpenAI's server-side VAD has no local equivalent yet. Revisit if you need it.
    [HideInInspector] public bool _isContinuousSessionActive = false;
    public Action OnServerDetectedSpeechStart;
    public Action OnServerDetectedSpeechStop;

    public bool _defaultPushToTalkOn = true;
    public bool _legacyHoldToSpeakOn = false;
    public Action OnAutoStopRecording; // Sent to AIGuide (to tell it when the voice has stopped)

    // Optional: returns engine-computed scene facts for the player's question (set by AIGuide to SceneContextBuilder.Build)
    public Func<string, string> SceneContextProvider;
    private float _silenceTimer = 0f;
    private float _silenceThreshold = 1.2f; // Seconds of silence before auto-stopping
    private float _volumeThreshold = 0.05f; // Minimum volume to be considered talking
    private bool _hasSpoken = false;
    private float _noSpeechTimeout = 8f; // Give up if nobody speaks, otherwise AIGuide stays locked in isRecording forever
    private float _maxRecordingLength = 18f; // Must stay below the 20s looping mic clip or the ring buffer overwrites itself
    private float _recordingTime = 0f;
    private float _recordingPeak = 0f;

    // The Thread-Safe Queue
    private ConcurrentQueue<float[]> _audioPlaybackQueue = new ConcurrentQueue<float[]>();
    private const int SAMPLE_RATE = 24000; // GuideAudioSync and the playback queue both assume this rate
    private int _totalSamplesSent = 0;

    // Variables for jitter on audio sharing over network (multiplayer avatar audio -- unrelated to the AI backend, unchanged)
    private Queue<float[]> _jitterBuffer = new Queue<float[]>();
    private bool _isBuffering = true;
    private const int BufferThreshold = 5;

    // Conversation state (the Realtime API used to keep this server-side; now we track it ourselves)
    private string _systemInstructions = "";

    // A single turn in the conversation. Images is only populated for visual-context turns --
    // Ollama's /api/chat accepts an "images" array (base64, no data: URI prefix) per message
    // for vision-capable models.
    private class ChatTurn
    {
        public string Role;
        public string Content;
        public List<string> ImagesBase64;
    }
    private List<ChatTurn> _conversationHistory = new List<ChatTurn>();

    private const string ActionInstructions =
        "\n\nACTIONS: You can trigger three actions by including a special tag anywhere in your reply: " +
        "[ACTION:{\"name\":\"trigger_guidance\",\"target_object\":\"<exact object name>\"}] to walk the player to an object, " +
        "[ACTION:{\"name\":\"trigger_teleportation\",\"target_object\":\"<exact object name>\"}] to teleport there instantly, or " +
        "[ACTION:{\"name\":\"trigger_modification\",\"target_object\":\"<exact object name>\"}] to add an audio beacon to it. " +
        "Only include this tag when the player has clearly asked for one of these three things, using the exact object name " +
        "from the Navigation Registry. Do not mention or describe the tag -- it is silently removed before your words are spoken. " +
        "If you offered to take the player somewhere and they answer yes, include the tag for that object in this reply. " +
        "\n\nSPOKEN REPLIES: Everything you write is read aloud to a blind player, so answer in at most 2-3 short sentences " +
        "(this overrides any longer word limit above). No lists, no markdown. Give the most useful information first.";

    private void Start()
    {
        _openAIQueriesScript = FindObjectOfType<OpenAIQueries>();
        if (_openAIQueriesScript != null)
            Debug.Log("Found the queries script");

        _micDevice = Microphone.devices[0];
    }

    public async Task Connect(string systemInstructions, bool usingBaseline)
    {
        _systemInstructions = systemInstructions + ActionInstructions;
        _conversationHistory.Clear();

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(ollamaHealthUrl);
            _isConnected = response.IsSuccessStatusCode;
            Debug.Log(_isConnected ? "[Ollama] Connected." : $"[Ollama] Reachable but returned {(int)response.StatusCode}.");
        }
        catch (Exception e)
        {
            _isConnected = false;
            Debug.LogError($"[Ollama] Could not reach server at {ollamaHealthUrl}: {ErrorText(e)}");
        }
        SetStatus(_isConnected ? "Ollama connected" : "Ollama NOT reachable");

        // Speaches isn't needed to connect, but warn now instead of failing silently on the first question
        string speachesHealthUrl = new Uri(ttsUrl).GetLeftPart(UriPartial.Authority) + "/health";
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(speachesHealthUrl);
            Debug.Log($"[Speaches] Health check {speachesHealthUrl}: HTTP {(int)response.StatusCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Speaches] Not reachable at {speachesHealthUrl}: {ErrorText(e)}. STT/TTS will fail -- is Docker Desktop running and the Speaches container up?");
            SetStatus("Speaches NOT reachable (Docker running?)");
        }

        // Start() picks devices[0], so this list shows whether that's actually the headset mic
        Debug.Log($"[Mic] Available devices (index 0 is used): {string.Join(" | ", Microphone.devices)}");
    }

    public void StartRecording()
    {
        if (!_isConnected)
        {
            Debug.LogWarning("[Mic] StartRecording ignored: not connected to Ollama (see the [Ollama] log at startup).");
            SetStatus("Not recording: Ollama not connected");
            return;
        }
        _turnTimer.Restart();
        Debug.Log($"[Mic] Recording started on \"{_micDevice}\"");
        SetStatus("Listening...");

        _hasSpoken = false;
        _silenceTimer = 0f;
        _recordingTime = 0f;
        _recordingPeak = 0f;

        outputSource.Stop();
        _audioPlaybackQueue = new ConcurrentQueue<float[]>();
        _speechGeneration++; // stop any sentence chunks still being synthesized for the previous reply
        _ttsInFlight = false;
        _isAiSpeaking = false;

        _pendingRecordingSamples.Clear();

        _isRecording = true;
        _lastMicPos = 0;
        _totalSamplesSent = 0;
        _micClip = Microphone.Start(_micDevice, true, 20, SAMPLE_RATE);
    }

    public async Task StopRecordingAndCommit(string screenshotUrl = null)
    {
        HandleMicStreaming(); // flush the final bit of the mic buffer
        Debug.Log($"[Mic] Recording stopped: {(float)_pendingRecordingSamples.Count / SAMPLE_RATE:F1}s of audio, peak volume {_recordingPeak:F3}");

        _isRecording = false;
        Microphone.End(_micDevice);

        // Require at least ~0.25s of audio before bothering to transcribe -- avoids empty-clip API errors
        if (_pendingRecordingSamples.Count > SAMPLE_RATE / 4)
        {
            byte[] wavBytes = EncodeWav(_pendingRecordingSamples.ToArray(), SAMPLE_RATE);
            _pendingRecordingSamples.Clear();

            SetStatus("Transcribing...");
            long sttStart = _turnTimer.ElapsedMilliseconds;
            string transcript = await TranscribeAudio(wavBytes);
            Debug.Log($"[STT] Transcript ({_turnTimer.ElapsedMilliseconds - sttStart} ms): \"{transcript}\"");

            if (string.IsNullOrWhiteSpace(transcript))
            {
                SetStatus("Heard nothing (empty transcript)");
                _openAIQueriesScript.PlayPredeterminedAudio("emptyQuery");
            }
            else
            {
                SetStatus($"You said: \"{transcript}\"");
                await SendUserMessageAndRespond(transcript);
            }
        }
        else
        {
            Debug.LogWarning("[Mic] Recording too short to transcribe (< 0.25s).");
            SetStatus("Recording too short");
            _pendingRecordingSamples.Clear();
            _openAIQueriesScript.PlayPredeterminedAudio("emptyQuery");
        }

        _totalSamplesSent = 0;
        _lastMicPos = 0;
    }

    public Task StopRecordingSilently()
    {
        _isRecording = false;
        _isContinuousSessionActive = false;

        if (Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);

        _pendingRecordingSamples.Clear();
        _ = StopAiSpeech();

        _totalSamplesSent = 0;
        _lastMicPos = 0;

        return Task.CompletedTask;
    }

    void Update()
    {
        HandleMicStreaming();
        HandleJitter();
        HandleDebugHotkey();
        HandleDebugVisionHotkey();

        if (!_guideAudioSourceFound)
        {
            outputSource = GameObject.Find("Human Model").GetComponent<AudioSource>();
            _guideAudioSourceFound = true;
        }
        else
        {
            if (!outputSource.isPlaying && _audioPlaybackQueue.TryDequeue(out float[] nextChunk))
            {
                Debug.Log($"[Playback] Playing {(float)nextChunk.Length / SAMPLE_RATE:F1}s on \"{outputSource.gameObject.name}\" (volume {outputSource.volume}, mute {outputSource.mute})");
                PlayAudioChunk(nextChunk);
            }
            else if (_isAiSpeaking && !_ttsInFlight && !outputSource.isPlaying && _audioPlaybackQueue.IsEmpty)
            {
                // Nothing left queued or playing -- the AI has finished speaking
                _isAiSpeaking = false;
            }
        }
    }

    private void HandleJitter()
    {
        if (!_isBuffering && _jitterBuffer.Count > 0)
        {
            float[] nextChunk = _jitterBuffer.Dequeue();
            _audioPlaybackQueue.Enqueue(nextChunk);
        }

        if (_jitterBuffer.Count == 0) _isBuffering = true;
    }

    private void HandleMicStreaming()
    {
        if (!_isRecording) return;

        int currentPos = Microphone.GetPosition(_micDevice);
        if (currentPos == _lastMicPos) return;

        float[] samples;

        if (currentPos > _lastMicPos)
        {
            samples = new float[currentPos - _lastMicPos];
            _micClip.GetData(samples, _lastMicPos);
        }
        else
        {
            int samplesToRead = (_micClip.samples - _lastMicPos) + currentPos;
            samples = new float[samplesToRead];

            float[] part1 = new float[_micClip.samples - _lastMicPos];
            _micClip.GetData(part1, _lastMicPos);

            float[] part2 = new float[currentPos];
            _micClip.GetData(part2, 0);

            Array.Copy(part1, 0, samples, 0, part1.Length);
            Array.Copy(part2, 0, samples, part1.Length, part2.Length);
        }

        _lastMicPos = currentPos;

        if (samples != null && samples.Length > 0)
        {
            _totalSamplesSent += samples.Length;

            ProcessVoiceActivity(samples);

            // Noise gate so we don't record the AI's own voice bleeding into the mic
            float maxVol = 0f;
            foreach (var s in samples) if (Mathf.Abs(s) > maxVol) maxVol = Mathf.Abs(s);

            bool isOutputtingSound = _isAiSpeaking || (outputSource != null && outputSource.isPlaying);
            if (isOutputtingSound && maxVol < 0.15f)
                Array.Clear(samples, 0, samples.Length);

            _pendingRecordingSamples.AddRange(samples);
        }
    }

    private void ProcessVoiceActivity(float[] samples)
    {
        float maxVol = 0f;
        foreach (var s in samples)
        {
            if (Mathf.Abs(s) > maxVol) maxVol = Mathf.Abs(s);
        }

        float chunkSeconds = (float)samples.Length / SAMPLE_RATE;
        _recordingTime += chunkSeconds;
        _recordingPeak = Mathf.Max(_recordingPeak, maxVol);

        if (maxVol > _volumeThreshold)
        {
            if (!_hasSpoken)
            {
                Debug.Log($"[Mic] Speech detected after {_recordingTime:F1}s (volume {maxVol:F3})");
                SetStatus("Hearing you...");
            }
            _hasSpoken = true;
            _silenceTimer = 0f;
        }
        else if (_hasSpoken)
        {
            _silenceTimer += chunkSeconds;

            if (_silenceTimer > _silenceThreshold)
            {
                _hasSpoken = false;
                _silenceTimer = 0f;

                Debug.Log($"[Mic] {_silenceThreshold}s of silence, auto-stopping");
                OnAutoStopRecording?.Invoke();
                _ = StopRecordingAndCommit();
                return;
            }
        }

        if (!_hasSpoken && _recordingTime > _noSpeechTimeout)
        {
            Debug.LogWarning($"[Mic] No speech in {_noSpeechTimeout}s (peak volume {_recordingPeak:F3}, threshold {_volumeThreshold}). " +
                             $"Wrong mic or too quiet? Device: \"{_micDevice}\"");
            SetStatus($"No speech detected (peak {_recordingPeak:F3})");
            OnAutoStopRecording?.Invoke(); // unlocks AIGuide.isRecording so the button works again
            _ = StopRecordingSilently();
        }
        else if (_recordingTime > _maxRecordingLength)
        {
            Debug.LogWarning($"[Mic] Hit max recording length ({_maxRecordingLength}s), sending what we have");
            _hasSpoken = false;
            OnAutoStopRecording?.Invoke();
            _ = StopRecordingAndCommit();
        }
    }

    // Handle incoming RPC audio on remote Normcore clients -- unchanged, unrelated to the AI backend
    public void ReceiveRemoteAudio(string base64Audio)
    {
        Debug.Log("Received remote audio from a guide on another client - converting locally");
        byte[] pcmData = Convert.FromBase64String(base64Audio);
        float[] floatData = ConvertPCM16ToFloats(pcmData);

        _jitterBuffer.Enqueue(floatData);

        if (_jitterBuffer.Count >= BufferThreshold)
            _isBuffering = false;
    }

    private void PlayAudioChunk(float[] data)
    {
        AudioClip clip = AudioClip.Create("ResponseChunk", data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        outputSource.clip = clip;
        outputSource.Play();
    }

    // --- Core LLM round trip -------------------------------------------------

    private async Task SendUserMessageAndRespond(string userText)
    {
        _isAiSpeaking = true;
        _conversationHistory.Add(new ChatTurn { Role = "user", Content = userText });

        var messages = new JArray
        {
            new JObject { ["role"] = "system", ["content"] = _systemInstructions }
        };
        foreach (var turn in _conversationHistory)
        {
            var turnObj = new JObject { ["role"] = turn.Role, ["content"] = turn.Content };
            if (turn.ImagesBase64 != null && turn.ImagesBase64.Count > 0)
                turnObj["images"] = new JArray(turn.ImagesBase64);
            messages.Add(turnObj);
        }

        // Fresh engine-computed positions go right before the question and are not kept in history,
        // so the model always sees where things are NOW and old positions don't pile up
        string sceneContext = null;
        try { sceneContext = SceneContextProvider?.Invoke(userText); }
        catch (Exception e) { Debug.LogWarning($"[SceneContext] Failed to build: {e.Message}"); }
        if (!string.IsNullOrEmpty(sceneContext))
        {
            messages.Insert(messages.Count - 1, new JObject { ["role"] = "user", ["content"] = sceneContext });
            Debug.Log($"[SceneContext]\n{sceneContext}");
        }

        var requestBody = new JObject
        {
            ["model"] = ollamaModel,
            ["messages"] = messages,
            ["stream"] = false,
            // gemma4 thinks by default; turning it off roughly halves latency with no measured loss in action accuracy
            ["think"] = false
        };

        int imageCount = 0;
        foreach (var turn in _conversationHistory) imageCount += turn.ImagesBase64?.Count ?? 0;
        Debug.Log($"[Ollama] Sending \"{userText}\" ({_conversationHistory.Count} turns in history, {imageCount} images attached)");
        SetStatus("Thinking (Ollama)...");
        long llmStart = _turnTimer.ElapsedMilliseconds;

        string responseText;
        try
        {
            var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(ollamaChatUrl, content);
            string raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[Ollama] HTTP {(int)response.StatusCode}: {raw}");
                SetStatus($"Ollama error HTTP {(int)response.StatusCode}");
                _isAiSpeaking = false;
                return;
            }

            JObject parsed = JObject.Parse(raw);
            responseText = parsed["message"]?["content"]?.ToString() ?? "";
            Debug.Log($"[Ollama] Response ({_turnTimer.ElapsedMilliseconds - llmStart} ms): \"{responseText}\"");
            LogInteraction(requestBody, userText, responseText, _turnTimer.ElapsedMilliseconds - llmStart);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ollama] Chat request to {ollamaChatUrl} failed: {ErrorText(e)}");
            SetStatus("Ollama request failed");
            _isAiSpeaking = false;
            return;
        }

        _conversationHistory.Add(new ChatTurn { Role = "assistant", Content = responseText });
        OnTextReceived?.Invoke(responseText);

        var (spokenText, actionTriggered) = ProcessResponseForActions(responseText);

        // If an action tag fired, HandleParsedAction already spoke a confirmation via SpeakCustomText --
        // don't also speak whatever (if anything) was left over in the raw response.
        if (!actionTriggered)
            await SpeakText(spokenText);
    }

    private (string spokenText, bool actionTriggered) ProcessResponseForActions(string responseText)
    {
        var match = Regex.Match(responseText, @"\[ACTION:(\{.*?\})\]");
        if (!match.Success)
            return (responseText.Trim(), false);

        string spoken = responseText.Remove(match.Index, match.Length).Trim();

        try
        {
            JObject action = JObject.Parse(match.Groups[1].Value);
            // If the target object isn't in the scene no confirmation is spoken, so speak the model's own text instead of going silent
            if (!HandleParsedAction(action))
                return (spoken, false);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Action] Failed to parse action tag: {e.Message}");
            return (spoken, false); // fall back to just speaking the text if the tag was malformed
        }

        return (spoken, true);
    }

    // Returns true if the action found its target and spoke a confirmation
    private bool HandleParsedAction(JObject action)
    {
        string name = (string)action["name"];
        string targetName = (string)action["target_object"];
        _isProcessingCommand = true;
        Debug.Log($"[Action] Model requested {name} -> \"{targetName}\"");
        SetStatus($"Action: {name} -> {targetName}");

        if (name == "trigger_guidance")
        {
            _openAIQueriesScript.modeOfTransportation = "guide";
            _openAIQueriesScript.targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

            if (_openAIQueriesScript.targetForGuidance != null)
            {
                string[] options = {
                    $"Of course. Reach out, take my arm and squeeze the grip button, and I will take you to the {targetName}.",
                    $"Sure. Hold on to my arm with the grip button and I'll lead the way to the {targetName}."
                };
                _ = SpeakCustomText(options[UnityEngine.Random.Range(0, options.Length)]);
                return true;
            }
        }
        else if (name == "trigger_teleportation")
        {
            _openAIQueriesScript.modeOfTransportation = "teleport";
            _openAIQueriesScript.targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

            if (_openAIQueriesScript.targetForGuidance != null)
            {
                string[] options = {
                    $"Sure. If you'd like me to teleport us to the {targetName}, take my arm and squeeze the grip button.",
                    $"Of course. I'm ready to teleport us to the {targetName}. Just hold on to my arm with the grip button."
                };
                _ = SpeakCustomText(options[UnityEngine.Random.Range(0, options.Length)]);
                return true;
            }
        }
        else if (name == "trigger_modification")
        {
            _openAIQueriesScript.modeOfModification = "modify";
            _openAIQueriesScript.targetForModification = _openAIQueriesScript.GetClosestObjectByName(targetName);

            if (_openAIQueriesScript.targetForModification != null)
            {
                _ = SpeakCustomText($"I have added an audio beacon to the {targetName}.");
                return true;
            }
        }

        Debug.LogWarning($"[Action] '{name}' target '{targetName}' not found in scene; speaking model text instead");
        _isProcessingCommand = false;
        return false;
    }

    public async Task SpeakCustomText(string customText)
    {
        if (!_isConnected) return;

        ClearLocalAudioBuffer();
        _conversationHistory.Add(new ChatTurn { Role = "assistant", Content = customText }); // keep the model's context consistent with what was actually said
        OnTextReceived?.Invoke(customText);

        await SpeakText(customText);
    }

    private async Task SpeakText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _isAiSpeaking = false;
            return;
        }

        _isAiSpeaking = true;
        Debug.Log($"[TTS] Speaking: \"{text}\"");
        SetStatus($"Guide: \"{text}\"");
        if (outputSource == null)
            Debug.LogError("[TTS] No outputSource (\"Human Model\" AudioSource) -- audio will be queued but can't play.");

        // Synthesize one sentence at a time so playback starts after the first sentence instead of after the whole reply.
        // The playback queue in Update() plays chunks back to back while later sentences are still being generated.
        int generation = _speechGeneration;
        _ttsInFlight = true;
        try
        {
            foreach (string sentence in SplitIntoSentences(text))
            {
                long ttsStart = _turnTimer.ElapsedMilliseconds;
                float[] samples = await SynthesizeSentence(sentence);
                if (generation != _speechGeneration) return; // interrupted by StopAiSpeech / a new recording
                if (samples == null) break;

                Debug.Log($"[TTS] {(float)samples.Length / SAMPLE_RATE:F1}s chunk ({_turnTimer.ElapsedMilliseconds - ttsStart} ms, " +
                          $"{_turnTimer.ElapsedMilliseconds} ms since button press): \"{sentence}\"");
                if (samples.Length > 0)
                    _audioPlaybackQueue.Enqueue(samples);
            }
        }
        finally
        {
            if (generation == _speechGeneration) _ttsInFlight = false;
        }
    }

    // Bumped whenever queued speech is thrown away, so an in-progress SpeakText loop knows to stop
    private int _speechGeneration = 0;
    // Keeps Update() from marking the AI as done speaking in the gap between two sentence chunks
    private bool _ttsInFlight = false;

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        foreach (string part in Regex.Split(text.Trim(), @"(?<=[.!?])\s+"))
        {
            // Merge very short fragments ("Sure!") into the next sentence -- one request per word costs more than it saves
            if (sentences.Count > 0 && sentences[sentences.Count - 1].Length < 25)
                sentences[sentences.Count - 1] += " " + part;
            else if (!string.IsNullOrWhiteSpace(part))
                sentences.Add(part);
        }
        return sentences;
    }

    // Returns null on failure (already logged)
    private async Task<float[]> SynthesizeSentence(string sentence)
    {
        try
        {
            var ttsBody = new JObject
            {
                ["model"] = ttsModel,
                ["input"] = sentence,
                ["voice"] = ttsVoice,
                ["response_format"] = "wav" // GuideAudioSync assumes 24kHz mono -- configure your TTS server to output that
            };

            var content = new StringContent(ttsBody.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(ttsUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[TTS] HTTP {(int)response.StatusCode}: {err}");
                SetStatus($"TTS error HTTP {(int)response.StatusCode}");
                return null;
            }

            byte[] wavBytes = await response.Content.ReadAsByteArrayAsync();
            return DecodeWav(wavBytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTS] Request to {ttsUrl} failed: {ErrorText(e)} -- is Docker Desktop running and the Speaches container up?");
            SetStatus("TTS failed: Speaches not reachable?");
            return null;
        }
    }

    private async Task<string> TranscribeAudio(byte[] wavBytes)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(wavBytes);
            audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            form.Add(audioContent, "file", "recording.wav");
            form.Add(new StringContent(sttModel), "model");

            HttpResponseMessage response = await _httpClient.PostAsync(sttUrl, form);
            string raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[STT] HTTP {(int)response.StatusCode}: {raw}");
                return null;
            }

            JObject parsed = JObject.Parse(raw);
            return parsed["text"]?.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"[STT] Request to {sttUrl} failed: {ErrorText(e)} -- is Docker Desktop running and the Speaches container up?");
            SetStatus("STT failed: Speaches not reachable?");
            return null;
        }
    }

    public Task StopAiSpeech()
    {
        ClearLocalAudioBuffer();
        _isAiSpeaking = false;
        _isProcessingCommand = false;
        Debug.Log("AI Speech interrupted and buffers cleared.");
        return Task.CompletedTask;
    }

    private void ClearLocalAudioBuffer()
    {
        _speechGeneration++;
        _ttsInFlight = false;

        if (outputSource.isPlaying)
            outputSource.Stop();

        _audioPlaybackQueue.Clear();
    }

    public void ResetCommandLock()
    {
        _isProcessingCommand = false;
    }

    // --- Quick manual test, no VR controller or microphone needed -------------
    // Press T (while the Game window has focus) to fire a hardcoded question straight
    // into the Ollama -> action-parsing -> TTS -> playback pipeline, skipping mic/STT
    // entirely (STT was already verified independently via curl earlier).
    [Header("Debug Test (Editor/Desktop only)")]
    [Tooltip("Hardcoded text sent through the pipeline when the test hotkey is pressed.")]
    public string debugTestQuery = "Hey, what is in front of me?";
    [Tooltip("Set to None to disable the hotkey entirely.")]
    public KeyCode debugTestHotkey = KeyCode.T;

    [Tooltip("Screenshots the Game view, attaches it as visual context, then asks the test query. Tests vision without AIGuide.cs.")]
    public KeyCode debugVisionHotkey = KeyCode.V;

    [Tooltip("Shows the pipeline's current stage in the Game view (desktop monitor only, not inside the headset).")]
    [SerializeField] private bool showDebugOverlay = true;

    [Tooltip("Saves every Ollama request (with its images) to <project>/DebugCaptures/interactions so Benchmark/ can replay them against other models.")]
    [SerializeField] private bool logInteractions = true;

    // One folder per question: request.json (images replaced by file names), the images as .jpg, and meta.json.
    // The request is saved exactly as sent, so the benchmark replays the real prompt, history and screenshots.
    private void LogInteraction(JObject requestBody, string userText, string responseText, long ollamaMs)
    {
        if (!logInteractions) return;
        try
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DebugCaptures", "interactions",
                $"{DateTime.Now:yyyyMMdd_HHmmss}_{scene.Replace(' ', '_')}"));
            Directory.CreateDirectory(dir);

            var saved = (JObject)requestBody.DeepClone();
            int imageIndex = 0;
            foreach (var message in saved["messages"])
            {
                if (message["images"] is JArray images)
                {
                    var names = new JArray();
                    foreach (var image in images)
                    {
                        string name = $"image_{imageIndex++}.jpg";
                        File.WriteAllBytes(Path.Combine(dir, name), Convert.FromBase64String((string)image));
                        names.Add(name);
                    }
                    message["images"] = names;
                }
            }

            File.WriteAllText(Path.Combine(dir, "request.json"), saved.ToString());
            File.WriteAllText(Path.Combine(dir, "meta.json"), new JObject
            {
                ["scene"] = scene,
                ["model"] = ollamaModel,
                ["question"] = userText,
                ["response"] = responseText,
                ["ollama_ms"] = ollamaMs
            }.ToString());
            Debug.Log($"[Benchmark] Logged interaction -> {dir}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Benchmark] Could not log interaction: {e.Message}");
        }
    }

    private readonly System.Diagnostics.Stopwatch _turnTimer = new System.Diagnostics.Stopwatch();
    private readonly List<string> _statusLines = new List<string>();
    private GUIStyle _overlayStyle;

    // Async HTTP code resumes on Unity's main thread (UnitySynchronizationContext), so touching the list here is safe
    private void SetStatus(string status)
    {
        string time = _turnTimer.IsRunning ? $"+{_turnTimer.ElapsedMilliseconds / 1000f:F1}s " : "";
        _statusLines.Add(time + status);
        if (_statusLines.Count > 8) _statusLines.RemoveAt(0);
    }

    // HttpClient wraps the useful reason (e.g. "connection refused") in InnerException
    private static string ErrorText(Exception e)
    {
        return e.InnerException != null ? $"{e.Message} ({e.InnerException.Message})" : e.Message;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || _statusLines.Count == 0) return;

        if (_overlayStyle == null)
        {
            _overlayStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, wordWrap = true };
            _overlayStyle.normal.textColor = Color.white;
        }

        string header = $"Guide  rec:{_isRecording}  speaking:{_isAiSpeaking}  ollama:{_isConnected}\n";
        GUI.Box(new Rect(10, 10, 620, 40 + 22 * _statusLines.Count), header + string.Join("\n", _statusLines), _overlayStyle);
    }

    private void HandleDebugVisionHotkey()
    {
        if (debugVisionHotkey == KeyCode.None || !Input.GetKeyDown(debugVisionHotkey)) return;

        if (!_isConnected)
        {
            Debug.LogWarning("[DebugVision] Not connected to Ollama yet.");
            return;
        }

        StartCoroutine(DebugVisionRoutine());
    }

    private IEnumerator DebugVisionRoutine()
    {
        yield return new WaitForEndOfFrame(); // the frame must be fully rendered before it can be captured

        Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();

        // Downscale to ~768px wide so the request stays small and fast
        int targetW = 768;
        int targetH = Mathf.Max(1, Mathf.RoundToInt(full.height * (targetW / (float)full.width)));
        RenderTexture rt = RenderTexture.GetTemporary(targetW, targetH);
        Graphics.Blit(full, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D small = new Texture2D(targetW, targetH, TextureFormat.RGB24, false);
        small.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        small.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        byte[] jpg = small.EncodeToJPG(75);
        Destroy(full);
        Destroy(small);

        string base64 = Convert.ToBase64String(jpg);
        Debug.Log($"[DebugVision] Captured screenshot ({jpg.Length / 1024} KB). Sending with query: \"{debugTestQuery}\"");

        // The same image is used for both slots -- this is just a vision smoke test
        SendVisualContext(base64, base64);
        _ = SendUserMessageAndRespond(debugTestQuery);
    }

    private void HandleDebugHotkey()
    {
        if (debugTestHotkey == KeyCode.None) return;

        if (Input.GetKeyDown(debugTestHotkey))
        {
            if (!_isConnected)
            {
                Debug.LogWarning("[DebugTest] Not connected to Ollama yet -- check the '[Ollama] Connected.' log fired earlier.");
                return;
            }

            Debug.Log($"[DebugTest] Firing test query: \"{debugTestQuery}\"");
            _ = SendUserMessageAndRespond(debugTestQuery);
        }
    }

    // Queues a visual-context turn into conversation history so the model sees it on its next
    // reply, mirroring how the old Realtime API injected images silently without forcing an
    // immediate response. Requires ollamaModel to be a vision-capable model (check with
    // `ollama show <model>` that "vision" is listed under Capabilities).
    public void SendVisualContext(string viewpointBase64, string birdsEyeBase64)
    {
        if (!_isConnected) return;

        // Only keep the newest screenshots: every request resends the full history, so stale
        // images would pile up and make each response slower and slower.
        _conversationHistory.RemoveAll(t => t.ImagesBase64 != null);
        Debug.Log($"[Vision] Screenshots attached (viewpoint {(viewpointBase64?.Length ?? 0) / 1024} KB, bird's eye {(birdsEyeBase64?.Length ?? 0) / 1024} KB base64)");

        _conversationHistory.Add(new ChatTurn
        {
            Role = "user",
            Content = "[Visual Context] Two silent reference images for your next answer. " +
                      "Image 1 is exactly what the player is facing right now; use it for anything in front of them. " +
                      "Image 2 is a top-down map taken from high above with roofs hidden; the red arrow marks the player and " +
                      "points the way they are facing. Use it ONLY to work out the layout around the player and which way to go. " +
                      "Image 2 is NOT the player's view: never describe it as a view, never say the view " +
                      "changed or shifted, and do not offer to go to things only visible in it unless they are in the Registry. " +
                      "If you see a person's avatar with a gray hoodie, black hair, and glasses, that is YOUR avatar, the guide.",
            ImagesBase64 = new List<string> { StripDataUriPrefix(viewpointBase64), StripDataUriPrefix(birdsEyeBase64) }
        });
    }

    // Ollama expects raw base64 with no "data:image/png;base64," prefix -- strip it if present,
    // since depending on how the screenshot was captured upstream it may or may not include one.
    private string StripDataUriPrefix(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return base64;
        int commaIndex = base64.IndexOf(',');
        return base64.StartsWith("data:") && commaIndex >= 0 ? base64.Substring(commaIndex + 1) : base64;
    }

    // --- Audio conversion helpers ---------------------------------------------

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

    private byte[] EncodeWav(float[] samples, int sampleRate)
    {
        byte[] pcmData = ConvertFloatsToPCM16(samples);
        int byteRate = sampleRate * 2; // mono, 16-bit

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmData.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // mono
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)2); // block align
        writer.Write((short)16); // bits per sample
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return ms.ToArray();
    }

    private float[] DecodeWav(byte[] wavBytes)
    {
        // Search for the "data" subchunk rather than assuming a fixed 44-byte header --
        // different TTS servers may include extra metadata chunks before the audio data.
        for (int i = 12; i < wavBytes.Length - 8; i++)
        {
            if (wavBytes[i] == 'd' && wavBytes[i + 1] == 'a' && wavBytes[i + 2] == 't' && wavBytes[i + 3] == 'a')
            {
                int dataLength = BitConverter.ToInt32(wavBytes, i + 4);
                int dataStart = i + 8;

                if (dataStart + dataLength > wavBytes.Length)
                    dataLength = wavBytes.Length - dataStart; // guard against a malformed/truncated header

                byte[] pcmBytes = new byte[dataLength];
                Array.Copy(wavBytes, dataStart, pcmBytes, 0, dataLength);
                return ConvertPCM16ToFloats(pcmBytes);
            }
        }

        Debug.LogError("[TTS] Could not find 'data' chunk in WAV response.");
        return Array.Empty<float>();
    }

    public void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(configFileName);
        if (configAsset == null)
        {
            Debug.LogWarning("Config file not found in Resources folder -- using default local server URLs from the Inspector.");
            return;
        }

        ConfigData configData = JsonUtility.FromJson<ConfigData>(configAsset.text);

        if (!string.IsNullOrEmpty(configData.OllamaUrl)) ollamaChatUrl = configData.OllamaUrl;
        if (!string.IsNullOrEmpty(configData.OllamaModel)) ollamaModel = configData.OllamaModel;
        if (!string.IsNullOrEmpty(configData.SttUrl)) sttUrl = configData.SttUrl;
        if (!string.IsNullOrEmpty(configData.SttModel)) sttModel = configData.SttModel;
        if (!string.IsNullOrEmpty(configData.TtsUrl)) ttsUrl = configData.TtsUrl;
        if (!string.IsNullOrEmpty(configData.TtsModel)) ttsModel = configData.TtsModel;
        if (!string.IsNullOrEmpty(configData.TtsVoice)) ttsVoice = configData.TtsVoice;
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
    public string objectClassifications = "";
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

    [HideInInspector]
    public string trustGuideline = "As you guide the player, inform them of your own uncertainty and mistakes so they can gauge whether to trust your advice.";

    [HideInInspector] public string text;
    [HideInInspector] public GameObject targetForGuidance;
    [HideInInspector] public string modeOfTransportation;
    [HideInInspector] public GameObject targetForModification;
    [HideInInspector] public string modeOfModification;

    public string query;
    public string role;
    public AudioSource audioSource;

    private AudioClip humanApology;
    private AudioClip robotApology;
    private AudioClip dogApology;
    private AudioClip caneApology;
    private AudioClip birdApology;
    private AudioClip invisibleApology;

    private void Start()
    {
        m_AIGuideScript = GetComponent<AIGuide>();
        audioSource = GameObject.Find("Human Model").GetComponent<AudioSource>();
        LoadRoomDescriptions();
        LoadPredeterminedAudio();
    }

    private void Update()
    {
        getAvatarVoice();
        getSharedMovement();
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
        switch (audioCue)
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
        if (m_AIGuideScript == null)
            m_AIGuideScript = GetComponent<AIGuide>();

        int index = m_AIGuideScript.role - 1;

        if (index < 0 || index >= roles.Count)
            return;

        role = roles[index];
    }

    public GameObject GetClosestObjectByName(string name)
    {
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
            string jsonContent = File.ReadAllText(jsonFilePath);
            Dictionary<string, string> descriptionDataDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string sceneDescriptionKey = currentSceneName;
            string sceneObjectsKey = currentSceneName + "_Objects";

            objectClassifications = descriptionDataDict[sceneDescriptionKey];
            objectNames = descriptionDataDict[sceneObjectsKey];

            if (objectClassifications == null || objectNames == null)
            {
                Debug.LogWarning("Description for the current scene not found in RoomDescriptions.json.");
            }
            else
            {
                List<string> filteredObjectClassificationsList = new List<string>();
                string[] objectClassificationsArray = objectClassifications.Split('|');
                foreach (string str in objectClassificationsArray)
                {
                    if (str.Contains(':'))
                    {
                        filteredObjectClassificationsList.Add(str);
                    }
                }
                objectClassificationsArray = filteredObjectClassificationsList.ToArray();
                objectClassifications = string.Join(" | ", objectClassificationsArray);
            }
        }
        else
        {
            Debug.LogError("RoomDescriptions.json file not found in Resources folder.");
        }
    }
}