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
    private bool _wasLocallyOwned;
    private readonly Queue<float[]> _playbackQueue = new Queue<float[]>();
    private float[] _receiveBuffer;
    private const int ReceiveChunkSize = 4800;
    private const float SilenceThreshold = 0.0005f;

    private void Start()
    {
        if (_outputAudioSource == null)
            _outputAudioSource = GetComponent<AudioSource>();

        _receiveBuffer = new float[ReceiveChunkSize];
        _wasLocallyOwned = IsLocallyOwned();
        RefreshPlaybackMode(_wasLocallyOwned);
    }

    private void Update()
    {
        bool isLocallyOwned = IsLocallyOwned();
        if (isLocallyOwned != _wasLocallyOwned)
        {
            _wasLocallyOwned = isLocallyOwned;
            RefreshPlaybackMode(isLocallyOwned);
        }

        if (!isLocallyOwned)
        {
            PumpRemoteAudio();
            return;
        }

        // Safety check: Only the owner of the Guide should broadcast its audio - others just receive
        if (model == null || realtime == null || realtime.room == null) return;

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
        if (pcmData == null || pcmData.Length == 0 || !IsLocallyOwned()) return;
        _threadSafeQueue.Enqueue(pcmData);
    }

    private void SendToNormcore(float[] chunk)
    {
        if (chunk == null || chunk.Length == 0 || model == null || realtime == null || realtime.room == null)
            return;

        if (!isOwnedLocallySelf)
        {
            RequestOwnership();
            return;
        }

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
        if (previousModel != null)
        {
            previousModel.streamIDDidChange -= StreamIDDidChange;
            previousModel.clientIDDidChange -= ClientIDDidChange;
        }

        if (currentModel != null)
        {
            currentModel.streamIDDidChange += StreamIDDidChange;
            currentModel.clientIDDidChange += ClientIDDidChange;

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
        if (IsLocallyOwned()) return;

        // Ensure we don't try to grab the stream before the clientID is synced
        if (realtime == null || realtime.room == null || model.clientID == -1 || value == -1) return;

        // Fetch the corresponding audio stream from the network
        _outputStream = realtime.room.GetAudioOutputStream(model.clientID, value);
        //Debug.Log($"[GuideAudio] Connected to Remote Stream: {value}");
    }

    private void ClientIDDidChange(GuideAudioSyncModel model, int value)
    {
        if (IsLocallyOwned()) return;

        if (realtime == null || realtime.room == null || value == -1 || model.streamID == -1) return;
        _outputStream = realtime.room.GetAudioOutputStream(value, model.streamID);
    }

    private void PumpRemoteAudio()
    {
        if (_wasLocallyOwned || _outputStream == null || _outputAudioSource == null || _receiveBuffer == null)
            return;

        try
        {
            _outputStream.GetAudioData(_receiveBuffer);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"GuideAudioSync failed to pull remote audio: {exception.Message}");
            return;
        }

        if (IsEffectivelySilent(_receiveBuffer))
            return;

        float[] chunk = new float[_receiveBuffer.Length];
        System.Array.Copy(_receiveBuffer, chunk, _receiveBuffer.Length);
        _playbackQueue.Enqueue(chunk);

        if (!_outputAudioSource.isPlaying)
            PlayNextChunk();
    }

    private bool IsLocallyOwned()
    {
        return realtimeView != null && realtimeView.isOwnedLocallySelf;
    }

    private void RefreshPlaybackMode(bool isLocallyOwned)
    {
        if (_outputAudioSource == null)
            return;

        if (isLocallyOwned)
        {
            if (_outputAudioSource.isPlaying)
                _outputAudioSource.Stop();
            return;
        }

        _outputAudioSource.loop = false;
    }

    private void PlayNextChunk()
    {
        if (_outputAudioSource == null || _playbackQueue.Count == 0)
            return;

        float[] nextChunk = _playbackQueue.Dequeue();
        AudioClip clip = AudioClip.Create("GuideRemoteChunk", nextChunk.Length, _channels, _sampleRate, false);
        clip.SetData(nextChunk, 0);
        _outputAudioSource.clip = clip;
        _outputAudioSource.Play();
    }

    private void LateUpdate()
    {
        if (_wasLocallyOwned || _outputAudioSource == null)
            return;

        if (!_outputAudioSource.isPlaying && _playbackQueue.Count > 0)
            PlayNextChunk();
    }

    private bool IsEffectivelySilent(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (Mathf.Abs(data[i]) > SilenceThreshold)
                return false;
        }

        return true;
    }
}
