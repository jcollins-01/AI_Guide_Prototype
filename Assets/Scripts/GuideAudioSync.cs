using Normal.Realtime;
using Normal.Realtime.Native;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    [SerializeField] private AudioSource _outputAudioSource; // Set to Remote Guide Voice Proxy in Unity Editor

    private AudioInputStream _inputStream;
    private AudioOutputStream _outputStream;

    // OpenAI standard sample rate
    private int _sampleRate = 24000;
    private int _channels = 1;

    // Buffers to prevent crashing from memory access violation (sending too many packets too quickly)
    private ConcurrentQueue<float[]> _threadSafeQueue = new ConcurrentQueue<float[]>();
    private List<float> _sendBuffer = new List<float>();
    private const int BufferThreshold = 480;
    // Caching this avoids calling RealtimeView from the Audio Thread
    private bool _isHost = false;

    private void Start()
    {
        _isHost = realtimeView.isOwnedLocallySelf;

        // If we are a remote client, we need this AudioSource to be "Playing" a dummy clip so that OnAudioFilterRead actually fires
        if (!_isHost)
        {
            _outputAudioSource.clip = AudioClip.Create("Silence", 1, _channels, _sampleRate, false);
            _outputAudioSource.loop = true;
            _outputAudioSource.Play();
        }
    }

    private void Update()
    {
        // Safety check: Only the owner of the Guide should broadcast its audio - others just receive
        if (model == null || !_isHost) return;

        //Debug.Log("Reached Broadcast Audio Chunk");

        while (_threadSafeQueue.TryDequeue(out float[] pcmData))
        {
            _sendBuffer.AddRange(pcmData);
        }

        // Only send to Normcore once we have enough data for a stable packet
        while (_sendBuffer.Count >= BufferThreshold)
        {
            // Create an array to hold the chunk data up to the size of our buffer
            float[] chunk = new float[BufferThreshold];
            _sendBuffer.CopyTo(0, chunk, 0, BufferThreshold);
            _sendBuffer.RemoveRange(0, BufferThreshold);

            SendToNormcore(chunk);
        }
    }

    // --- SENDING (Called locally on the Host) ---
    public void BroadcastAudioChunk(float[] pcmData)
    {
        if (pcmData == null) return;
        _threadSafeQueue.Enqueue(pcmData);
    }

    private void SendToNormcore(float[] chunk)
    {
        // Initialize the stream if it doesn't exist yet
        if (_inputStream == null)
        {
            _inputStream = realtime.room.CreateAudioInputStream(true, _channels, _sampleRate);
            // Sync the IDs so remote clients know where to listen
            model.streamID = _inputStream.StreamID();
            model.clientID = realtime.room.clientID;
            //Debug.Log($"[GuideAudio] Created Stream ID: {model.streamID}");
        }

        // Feed the raw PCM data into Normcore's compressed voice network
        _inputStream.SendRawAudioData(chunk);
        //Debug.Log("Sending raw audio data via Normcore");
    }

    // --- RECEIVING (Called on Remote Clients) ---
    protected override void OnRealtimeModelReplaced(GuideAudioSyncModel previousModel, GuideAudioSyncModel currentModel)
    {
        if (previousModel != null) previousModel.streamIDDidChange -= StreamIDDidChange;

        if (currentModel != null)
        {
            currentModel.streamIDDidChange += StreamIDDidChange;

            // If they join mid-sentence, catch the active stream immediately
            if (currentModel.streamID != -1) //&& currentModel.clientID != -1)
            {
                StreamIDDidChange(currentModel, currentModel.streamID);
            }
        }
    }

    private void StreamIDDidChange(GuideAudioSyncModel model, int value)
    {
        // If I'm the host sending the audio, I don't need to listen to the network stream
        if (_isHost) return;

        // Ensure we don't try to grab the stream before the clientID is synced
        if (model.clientID == -1 || value == -1) return;

        // Fetch the corresponding audio stream from the network
        _outputStream = realtime.room.GetAudioOutputStream(model.clientID, value);
        //Debug.Log($"[GuideAudio] Connected to Remote Stream: {value}");
    }

    // --- PLAYBACK (Native Unity Audio Pipeline) ---

    // Unity automatically calls this every frame on any object with an AudioSource
    private void OnAudioFilterRead(float[] data, int channels)
    {
        // If we are the sender, or we don't have a stream yet, do nothing and output silence
        if (_isHost)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        // Pull the decompressed network audio directly into Unity's audio playback buffer
        if (_outputStream != null)
        {
            _outputStream.GetAudioData(data);
            //Debug.Log("Playing audio data received remotely via Normcore");
        }
        else
        {
            // If no audio is coming in, we must explicitly clear the buffer to silence - not doing so makes Unity loop/replay last buffer
            System.Array.Clear(data, 0, data.Length);
        }
    }
}