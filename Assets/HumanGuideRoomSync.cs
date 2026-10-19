using Normal.Realtime;
using Unity.Burst.CompilerServices;
using UnityEngine;

public class HumanGuideRoomSync : RealtimeComponent<HumanGuideRoomModel>
{
    public enum HumanGuideRole
    {
        Unassigned,
        Participant,
        Guide
    }

    public HumanGuideRole LocalRole { get; private set; } = HumanGuideRole.Unassigned;
    private RealtimeAvatarManager playerAvatarManager;
    private RealtimeAvatarManager aiGuideAvatarManager;
    private RealtimeAvatarManager humanGuideAvatarManager;
    protected override void OnRealtimeModelReplaced(
    HumanGuideRoomModel previousModel,
    HumanGuideRoomModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.participantClientIDDidChange -= ParticipantClientIDDidChange;
            previousModel.guideClientIDDidChange -= GuideClientIDDidChange;
        }

        if (currentModel != null)
        {
            currentModel.participantClientIDDidChange += ParticipantClientIDDidChange;
            currentModel.guideClientIDDidChange += GuideClientIDDidChange;

            UpdateLocalRole();
        }
    }

    private void ParticipantClientIDDidChange(
        HumanGuideRoomModel model,
        int clientID)
    {
        UpdateLocalRole();
    }

    private void GuideClientIDDidChange(
        HumanGuideRoomModel model,
        int clientID)
    {
        UpdateLocalRole();
    }
    
    private void UpdateLocalRole()
    {
        if (model == null || realtime == null)
            return;

        int localClientID = realtime.clientID;

        if (localClientID < 0)
        {
            LocalRole = HumanGuideRole.Unassigned;
            return;
        }

        if (localClientID == model.participantClientID)
        {
            LocalRole = HumanGuideRole.Participant;
        }
        else if (localClientID == model.guideClientID)
        {
            LocalRole = HumanGuideRole.Guide;
        }
        else
        {
            LocalRole = HumanGuideRole.Unassigned;
        }

        Debug.Log(
            $"Human Guide Mode: Client {localClientID} currently has role {LocalRole}"
        );

        ConfigureAvatarManagers();
    }
    private void TryAssignLocalRole()
    {
        if (model == null || realtime == null || !realtime.connected)
            return;

        int localClientID = realtime.clientID;

        // This client already has a role.
        if (model.participantClientID == localClientID ||
            model.guideClientID == localClientID)
        {
            UpdateLocalRole();
            return;
        }

        // No participant yet, so this client becomes the participant.
        if (model.participantClientID == -1)
        {
            model.participantClientID = localClientID;

            Debug.Log(
                $"Human Guide Mode: Client {localClientID} assigned as PARTICIPANT."
            );

            UpdateLocalRole();
            return;
        }

        // A participant exists but no guide exists,
        // so this client becomes the human guide.
        if (model.guideClientID == -1)
        {
            model.guideClientID = localClientID;

            Debug.Log(
                $"Human Guide Mode: Client {localClientID} assigned as GUIDE."
            );

            UpdateLocalRole();
        }
    }
    private void Start()
    {
        if (realtime == null)
        {
            Debug.LogError("HumanGuideRoomSync: No Realtime instance found.");
            return;
        }

        Debug.Log("HumanGuideRoomSync: Start called.");

        realtime.didConnectToRoom += DidConnectToRoom;

        // If Realtime connected before this script subscribed to the event,
        // handle the connection immediately.
        if (realtime.connected)
        {
            Debug.Log("HumanGuideRoomSync: Already connected to room.");
            DidConnectToRoom(realtime);
        }
    }

    private void DidConnectToRoom(Realtime realtime)
    {
        Debug.Log("HumanGuideRoomSync: DidConnectToRoom was called.");

        SwitchTools switchTools = GetComponent<SwitchTools>();

        if (switchTools == null)
        {
            Debug.LogError("HumanGuideRoomSync: SwitchTools was not found on RoomManager.");
            return;
        }

        Debug.Log("HumanGuideRoomSync: HumanGuideActive = " + switchTools.HumanGuideActive);

        if (!switchTools.HumanGuideActive)
        {
            Debug.Log("Human Guide Mode is disabled. Using the normal AI guide.");
            return;
        }

        TryAssignLocalRole();
    }

    private void FindAvatarManagers()
    {
        RealtimeAvatarManager[] managers = GetComponents<RealtimeAvatarManager>();

        foreach (RealtimeAvatarManager manager in managers)
        {
            if (manager.localAvatarPrefab == null)
                continue;

            string prefabName = manager.localAvatarPrefab.name;

            if (prefabName == "Player Avatar")
            {
                playerAvatarManager = manager;
            }
            else if (prefabName == "Guide Avatar")
            {
                aiGuideAvatarManager = manager;
            }
            else if (prefabName == "Human Guide Avatar")
            {
                humanGuideAvatarManager = manager;
            }
        }
    }

    private void ConfigureAvatarManagers()
    {
        if (playerAvatarManager == null ||
            aiGuideAvatarManager == null ||
            humanGuideAvatarManager == null)
        {
            FindAvatarManagers();
        }

        if (LocalRole == HumanGuideRole.Participant)
        {
            playerAvatarManager.enabled = true;
            aiGuideAvatarManager.enabled = false;
            humanGuideAvatarManager.enabled = false;

            Debug.Log("Human Guide Mode: Configured avatars for PARTICIPANT.");
        }
        else if (LocalRole == HumanGuideRole.Guide)
        {
            playerAvatarManager.enabled = false;
            aiGuideAvatarManager.enabled = false;
            humanGuideAvatarManager.enabled = true;

            Debug.Log("Human Guide Mode: Configured avatars for GUIDE.");
        }
    }
}