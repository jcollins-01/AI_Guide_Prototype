using Normal.Realtime;
using UnityEngine;

public class RealtimeUserState : RealtimeComponent<PlayerType>
{
    public string playerName { get; private set; }
    public string audioClipName { get; private set; }

    protected override void OnRealtimeModelReplaced(PlayerType previousModel, PlayerType currentModel)
    {
        if (previousModel != null)
        {
            previousModel.nameDidChange -= NameDidChange;
            previousModel.audioClipNameDidChange -= AudioDidChange;
        }

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
            {
                RoomManager roomManager = FindObjectOfType<RoomManager>();
                if (roomManager != null)
                {
                    currentModel.name = roomManager.playerName;
                    currentModel.audioClipName = roomManager.audioClipName;
                }
            }

            UpdateName();
            UpdateAudio();

            currentModel.nameDidChange += NameDidChange;
            currentModel.audioClipNameDidChange += AudioDidChange;
        }
    }

    private void NameDidChange(PlayerType model, string value)
    {
        UpdateName();
    }

    private void AudioDidChange(PlayerType model, string value)
    {
        UpdateAudio();
    }

    private void UpdateName()
    {
        playerName = model.name;

        if (realtimeView != null && realtimeView.isOwnedLocallySelf)
        {
            RoomManager roomManager = FindObjectOfType<RoomManager>();
            if (roomManager != null)
                roomManager.ApplyLocalRole(playerName);
        }
    }

    private void UpdateAudio()
    {
        audioClipName = model.audioClipName;

        RoomManager roomManager = FindObjectOfType<RoomManager>();
        if (roomManager != null)
            roomManager.audioClipName = audioClipName;
    }

    public void SetName(string name)
    {
        if (model == null)
            return;

        model.name = name;

        if (realtimeView != null && realtimeView.isOwnedLocallySelf)
        {
            RoomManager roomManager = FindObjectOfType<RoomManager>();
            if (roomManager != null)
                roomManager.ApplyLocalRole(name);
        }
    }

    public void SetAudio(string clipName)
    {
        if (model == null)
            return;

        model.audioClipName = clipName;
    }
}
