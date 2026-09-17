using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Normal.Realtime;
using System;
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
    [Tooltip("Model name your STT server expects -- check its docs, this is a placeholder.")]
    public string sttModel = "whisper-1";

    [Tooltip("OpenAI-compatible speech endpoint, e.g. from Speaches or Kokoro-FastAPI.")]
    public string ttsUrl = "http://localhost:8000/v1/audio/speech";
    [Tooltip("Model name your TTS server expects -- check its docs, this is a placeholder.")]
    public string ttsModel = "tts-1";
    [Tooltip("Voice name your TTS server expects.")]
    public string ttsVoice = "alloy";

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
    private float _silenceTimer = 0f;
    private float _silenceThreshold = 1.2f; // Seconds of silence before auto-stopping
    private float _volumeThreshold = 0.05f; // Minimum volume to be considered talking
    private bool _hasSpoken = false;

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
        "from the Navigation Registry. Do not mention or describe the tag -- it is silently removed before your words are spoken.";

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
            Debug.LogError($"[Ollama] Could not reach server at {ollamaHealthUrl}: {e.Message}");
        }
    }

    public void StartRecording()
    {
        if (!_isConnected) return;
        Debug.Log("Recording started");

        _hasSpoken = false;
        _silenceTimer = 0f;

        outputSource.Stop();
        _audioPlaybackQueue = new ConcurrentQueue<float[]>();
        _isAiSpeaking = false;

        _pendingRecordingSamples.Clear();

        _isRecording = true;
        _lastMicPos = 0;
        _totalSamplesSent = 0;
        _micClip = Microphone.Start(_micDevice, true, 20, SAMPLE_RATE);
    }

    public async Task StopRecordingAndCommit(string screenshotUrl = null)
    {
        Debug.Log("Recording stopped");
        HandleMicStreaming(); // flush the final bit of the mic buffer

        _isRecording = false;
        Microphone.End(_micDevice);

        // Require at least ~0.25s of audio before bothering to transcribe -- avoids empty-clip API errors
        if (_pendingRecordingSamples.Count > SAMPLE_RATE / 4)
        {
            byte[] wavBytes = EncodeWav(_pendingRecordingSamples.ToArray(), SAMPLE_RATE);
            _pendingRecordingSamples.Clear();

            string transcript = await TranscribeAudio(wavBytes);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                _openAIQueriesScript.PlayPredeterminedAudio("emptyQuery");
            }
            else
            {
                await SendUserMessageAndRespond(transcript);
            }
        }
        else
        {
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

        if (!_guideAudioSourceFound)
        {
            outputSource = GameObject.Find("Human Model").GetComponent<AudioSource>();
            _guideAudioSourceFound = true;
        }
        else
        {
            if (!outputSource.isPlaying && _audioPlaybackQueue.TryDequeue(out float[] nextChunk))
            {
                PlayAudioChunk(nextChunk);
            }
            else if (_isAiSpeaking && !outputSource.isPlaying && _audioPlaybackQueue.IsEmpty)
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

        if (maxVol > _volumeThreshold)
        {
            _hasSpoken = true;
            _silenceTimer = 0f;
        }
        else if (_hasSpoken)
        {
            _silenceTimer += (float)samples.Length / SAMPLE_RATE;

            if (_silenceTimer > _silenceThreshold)
            {
                _hasSpoken = false;
                _silenceTimer = 0f;

                OnAutoStopRecording?.Invoke();
                _ = StopRecordingAndCommit();
            }
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

        var requestBody = new JObject
        {
            ["model"] = ollamaModel,
            ["messages"] = messages,
            ["stream"] = false
        };

        string responseText;
        try
        {
            var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(ollamaChatUrl, content);
            string raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[Ollama] HTTP {(int)response.StatusCode}: {raw}");
                _isAiSpeaking = false;
                return;
            }

            JObject parsed = JObject.Parse(raw);
            responseText = parsed["message"]?["content"]?.ToString() ?? "";
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ollama] Chat request failed: {e.Message}");
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
            HandleParsedAction(action);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Action] Failed to parse action tag: {e.Message}");
            return (spoken, false); // fall back to just speaking the text if the tag was malformed
        }

        return (spoken, true);
    }

    private void HandleParsedAction(JObject action)
    {
        string name = (string)action["name"];
        string targetName = (string)action["target_object"];
        _isProcessingCommand = true;

        if (name == "trigger_guidance")
        {
            _openAIQueriesScript.modeOfTransportation = "guide";
            _openAIQueriesScript.targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

            if (_openAIQueriesScript.targetForGuidance != null)
            {
                string[] options = {
                    $"Of course. Press the grip button to confirm, and I will take you to the {targetName}.",
                    $"Sure. Squeeze the grip button to confirm and I'll lead the way to the {targetName}."
                };
                _ = SpeakCustomText(options[UnityEngine.Random.Range(0, options.Length)]);
            }
        }
        else if (name == "trigger_teleportation")
        {
            _openAIQueriesScript.modeOfTransportation = "teleport";
            _openAIQueriesScript.targetForGuidance = _openAIQueriesScript.GetClosestObjectByName(targetName);

            if (_openAIQueriesScript.targetForGuidance != null)
            {
                string[] options = {
                    $"Sure. If you'd like me to teleport us to the {targetName}, just press the grip button.",
                    $"Of course. I'm ready to teleport us to the {targetName}. Just confirm with the grip button."
                };
                _ = SpeakCustomText(options[UnityEngine.Random.Range(0, options.Length)]);
            }
        }
        else if (name == "trigger_modification")
        {
            _openAIQueriesScript.modeOfModification = "modify";
            _openAIQueriesScript.targetForModification = _openAIQueriesScript.GetClosestObjectByName(targetName);

            if (_openAIQueriesScript.targetForModification != null)
            {
                _ = SpeakCustomText($"I have added an audio beacon to the {targetName}.");
            }
        }
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

        try
        {
            var ttsBody = new JObject
            {
                ["model"] = ttsModel,
                ["input"] = text,
                ["voice"] = ttsVoice,
                ["response_format"] = "wav" // GuideAudioSync assumes 24kHz mono -- configure your TTS server to output that
            };

            var content = new StringContent(ttsBody.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(ttsUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[TTS] HTTP {(int)response.StatusCode}: {err}");
                _isAiSpeaking = false;
                return;
            }

            byte[] wavBytes = await response.Content.ReadAsByteArrayAsync();
            float[] samples = DecodeWav(wavBytes);

            if (samples.Length > 0)
                _audioPlaybackQueue.Enqueue(samples);
            else
                _isAiSpeaking = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTS] Request failed: {e.Message}");
            _isAiSpeaking = false;
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
            Debug.LogError($"[STT] Request failed: {e.Message}");
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
        if (outputSource.isPlaying)
            outputSource.Stop();

        _audioPlaybackQueue.Clear();
    }

    public void ResetCommandLock()
    {
        _isProcessingCommand = false;
    }

    // Queues a visual-context turn into conversation history so the model sees it on its next
    // reply, mirroring how the old Realtime API injected images silently without forcing an
    // immediate response. Requires ollamaModel to be a vision-capable model (check with
    // `ollama show <model>` that "vision" is listed under Capabilities).
    public void SendVisualContext(string viewpointBase64, string birdsEyeBase64)
    {
        if (!_isConnected) return;

        _conversationHistory.Add(new ChatTurn
        {
            Role = "user",
            Content = "[Visual Context] You are looking at two views of the player's VR scene. Image 1 is the " +
                      "player's view, Image 2 is a bird's eye of the scene. If you ever see a person's avatar " +
                      "with a gray hoodie, black hair, and glasses, that is YOUR avatar, the guide.",
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