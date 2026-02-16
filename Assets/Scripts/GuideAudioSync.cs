using Normal.Realtime;
using UnityEngine;
using System.Collections;

public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    private RealtimeGuideClient _guideClient;

    private void Start()
    {
        // Find the main client script
        _guideClient = FindObjectOfType<RealtimeGuideClient>();
    }

    // Sending -- Called by the Host when OpenAI delivers audio
    public void BroadcastAudioChunk(string base64Audio)
    {
        if (model != null)
        {
            // Setting this property sends it to everyone
            model.audioChunk = base64Audio;
        }
    }

    // Receiving -- Called by Normcore

    protected override void OnRealtimeModelReplaced(GuideAudioSyncModel previousModel, GuideAudioSyncModel currentModel)
    {
        if (previousModel != null) previousModel.audioChunkDidChange -= AudioChunkDidChange;

        if (currentModel != null)
        {
            currentModel.audioChunkDidChange += AudioChunkDidChange;

            // If they join mid-sentence, try to catch the current chunk immediately
            if (!string.IsNullOrEmpty(currentModel.audioChunk))
            {
                AudioChunkDidChange(currentModel, currentModel.audioChunk);
            }
        }
    }

    private void AudioChunkDidChange(GuideAudioSyncModel model, string value)
    {
        Debug.Log("Detected that the realtime audio chunk changed");
        
        // If the string is empty, ignore
        if (string.IsNullOrEmpty(value)) return;

        // If I'm the owner (the client with the Guide), I've already heard it locally
        if (realtimeView.isOwnedLocallySelf) return;

        // Pass to the client to play
        if (_guideClient != null)
        {
            Debug.Log("Was passed a chunk from the player's guide to make my local guide play");
            _guideClient.ReceiveRemoteAudio(value);
        }
    }
}