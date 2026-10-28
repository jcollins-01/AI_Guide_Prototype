using Normal.Realtime;
using System.Collections.Generic;
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
    private SwitchTools switchTools;
    private RealtimeAvatarManager playerAvatarManager;
    private RealtimeAvatarManager aiGuideAvatarManager;
    private RealtimeAvatarManager humanGuideAvatarManager;

    // Client ID that owns each avatar in the room, recorded when the avatar is created
    // (by the time an avatar is destroyed, its owner may already be cleared).
    private readonly Dictionary<RealtimeAvatar, int> avatarOwners = new Dictionary<RealtimeAvatar, int>();

    private void Awake()
    {
        switchTools = GetComponent<SwitchTools>();
        FindAvatarManagers();

        // Every avatar registers with a RealtimeAvatarManager on this object,
        // so listening to all of them tells us when anyone's avatar appears or disappears.
        foreach (RealtimeAvatarManager manager in GetComponents<RealtimeAvatarManager>())
        {
            manager.avatarCreated += AvatarCreated;
            manager.avatarDestroyed += AvatarDestroyed;
        }

        // In Human Guide mode, no avatar should spawn until this client knows its role.
        // Otherwise every enabled manager spawns an avatar on connect and they get swapped out
        // afterwards ("RealtimeAvatar registered more than once for the same clientID").
        if (IsHumanGuideMode())
            SetAvatarManagersEnabled(false, false, false);
    }

    private bool IsHumanGuideMode()
    {
        return switchTools != null && switchTools.HumanGuideActive;
    }

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
        ClaimRoleIfUnassigned();
    }

    private void GuideClientIDDidChange(
        HumanGuideRoomModel model,
        int clientID)
    {
        UpdateLocalRole();
        ClaimRoleIfUnassigned();
    }

    // Covers two cases: both clients joined at the same moment and this one lost the
    // participant role to the other, or a role was freed because someone left.
    private void ClaimRoleIfUnassigned()
    {
        if (LocalRole == HumanGuideRole.Unassigned && IsHumanGuideMode())
            TryAssignLocalRole();
    }

    private void AvatarCreated(
        RealtimeAvatarManager avatarManager,
        RealtimeAvatar avatar,
        bool isLocalAvatar)
    {
        avatarOwners[avatar] = avatar.GetComponent<RealtimeView>().ownerIDSelf;
    }

    // When someone leaves, Normcore destroys their avatar. Free the role they held so that
    // the person can claim it again when they rejoin (they get a new client ID).
    private void AvatarDestroyed(
        RealtimeAvatarManager avatarManager,
        RealtimeAvatar avatar,
        bool isLocalAvatar)
    {
        if (!avatarOwners.TryGetValue(avatar, out int ownerID))
            return;

        avatarOwners.Remove(avatar);

        if (model == null || realtime == null || ownerID == realtime.clientID || !IsHumanGuideMode())
            return;

        // Only the avatar that matches the role counts. A client that loses a role race
        // destroys its old avatar but keeps the role it switched to.
        if (avatar.name.StartsWith("Player Avatar") && model.participantClientID == ownerID)
        {
            Debug.Log($"Human Guide Mode: Participant (client {ownerID}) left. Role is free again.");
            model.participantClientID = -1;
        }
        else if (avatar.name.StartsWith("Human Guide Avatar") && model.guideClientID == ownerID)
        {
            Debug.Log($"Human Guide Mode: Guide (client {ownerID}) left. Role is free again.");
            model.guideClientID = -1;
        }
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
            return;
        }

        Debug.LogWarning(
            $"Human Guide Mode: Both roles are taken (participant = client {model.participantClientID}, " +
            $"guide = client {model.guideClientID}). Client {localClientID} will not get an avatar."
        );
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
        // Outside Human Guide mode, SwitchTools decides which avatars are used
        if (!IsHumanGuideMode())
            return;

        if (playerAvatarManager == null ||
            aiGuideAvatarManager == null ||
            humanGuideAvatarManager == null)
        {
            FindAvatarManagers();
        }

        if (LocalRole == HumanGuideRole.Participant)
        {
            SetAvatarManagersEnabled(true, false, false);

            Debug.Log("Human Guide Mode: Configured avatars for PARTICIPANT.");
        }
        else if (LocalRole == HumanGuideRole.Guide)
        {
            SetAvatarManagersEnabled(false, false, true);

            Debug.Log("Human Guide Mode: Configured avatars for GUIDE.");
        }
        else
        {
            // No role, so no avatar (also removes the avatar of a role this client just lost)
            SetAvatarManagersEnabled(false, false, false);
        }
    }

    // Enabling a manager spawns this client's avatar for it, disabling it destroys that avatar
    private void SetAvatarManagersEnabled(bool player, bool aiGuide, bool humanGuide)
    {
        if (playerAvatarManager != null)
            playerAvatarManager.enabled = player;
        else
            Debug.LogError("HumanGuideRoomSync: No RealtimeAvatarManager with the Player Avatar prefab on RoomManager.");

        if (aiGuideAvatarManager != null)
            aiGuideAvatarManager.enabled = aiGuide;

        if (humanGuideAvatarManager != null)
            humanGuideAvatarManager.enabled = humanGuide;
        else
            Debug.LogError("HumanGuideRoomSync: No RealtimeAvatarManager with the Human Guide Avatar prefab on RoomManager.");
    }
}