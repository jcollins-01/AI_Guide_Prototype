using Normal.Realtime;
using System.Linq;
using UnityEngine;

public class RoomManager : RealtimeComponent<RoomAttributesModel>
{
    public string playerName;
    public string audioClipName;

    private RealtimeUserState[] foundUserStates;
    private Realtime _realtime;
    private bool _rolesAssigned;
    private bool _localPlayerCounted;
    private bool _pendingRoleAssignment;

    private void Awake()
    {
        _realtime = GetComponent<Realtime>();
        if (_realtime == null)
            _realtime = FindObjectOfType<Realtime>();

        if (_realtime != null)
            _realtime.didConnectToRoom += DidConnectToRoom;
    }

    private void OnDestroy()
    {
        if (_realtime != null)
            _realtime.didConnectToRoom -= DidConnectToRoom;
    }

    private void DidConnectToRoom(Realtime room)
    {
        if (!gameObject.activeInHierarchy || !enabled)
            return;

        AssignNetworkRoles();
    }

    public void AssignNetworkRoles()
    {
        if (model == null)
            return;

        GuideModeController controller = FindObjectOfType<GuideModeController>();
        if (controller == null || !controller.humanGuideOn)
            return;

        RealtimeUserState localUserState = GetLocalUserState();
        if (localUserState == null)
        {
            _pendingRoleAssignment = true;
            return;
        }

        _pendingRoleAssignment = false;

        if (!_localPlayerCounted)
        {
            model.totalPlayers = model.totalPlayers + 1;
            _localPlayerCounted = true;
        }

        switch (controller.humanNetworkRole)
        {
            case GuideModeController.HumanNetworkRole.Guide:
                localUserState.SetName("guide");
                Debug.Log("[RoomManager] Local client assigned guide (from GuideModeController).");
                return;
            case GuideModeController.HumanNetworkRole.Participant:
                localUserState.SetName("participant");
                Debug.Log("[RoomManager] Local client assigned participant (from GuideModeController).");
                return;
        }

        if (model.totalPlayers == 1)
        {
            localUserState.SetName("participant");
            Debug.Log("[RoomManager] Only one player in room; assigned participant.");
            return;
        }

        foundUserStates = FindObjectsOfType<RealtimeUserState>()
            .OrderBy(state => state.realtimeView.ownerIDSelf)
            .ToArray();

        if (foundUserStates.Length >= 2)
        {
            foundUserStates[0].SetName("participant");
            foundUserStates[1].SetName("guide");
            Debug.Log("[RoomManager] Two players present; assigned participant and guide.");
        }
        else
        {
            localUserState.SetName("participant");
            Debug.LogWarning("[RoomManager] Expected two RealtimeUserState instances but found " + foundUserStates.Length);
        }

        _rolesAssigned = true;
    }

    public void NewUserJoined()
    {
        if (model == null)
            return;

        model.totalPlayers = model.totalPlayers + 1;

        if (!_rolesAssigned && model.totalPlayers >= 2)
            AssignNetworkRoles();
    }

    public void ApplyLocalRole(string roleName)
    {
        playerName = roleName;
        ApplyMovementSetup(roleName);
        AssignNetworkedGuideReference(roleName);
    }

    private void Update()
    {
        if (_pendingRoleAssignment)
            AssignNetworkRoles();

        if (!string.IsNullOrEmpty(playerName))
            ApplyMovementSetup(playerName);
    }

    private void ApplyMovementSetup(string roleName)
    {
        bool isGuide = roleName == "guide";
        bool isParticipant = roleName == "participant";

        GameObject guideRig = FindGuideRig();
        GameObject participantRig = FindParticipantRig();

        if (guideRig != null)
            guideRig.SetActive(isGuide);

        if (participantRig != null)
            participantRig.SetActive(isParticipant);

        FindAvatarManagers(out RealtimeAvatarManager guideAvatarManager, out RealtimeAvatarManager participantAvatarManager);

        if (guideAvatarManager != null)
            guideAvatarManager.enabled = isGuide;

        if (participantAvatarManager != null)
            participantAvatarManager.enabled = isParticipant;
    }

    private void AssignNetworkedGuideReference(string roleName)
    {
        if (roleName != "participant")
            return;

        SharedMovement sharedMovement = FindObjectOfType<SharedMovement>();
        if (sharedMovement == null)
            return;

        foreach (RealtimeUserState userState in FindObjectsOfType<RealtimeUserState>())
        {
            if (userState.playerName != "guide")
                continue;

            if (userState.realtimeView == null || userState.realtimeView.isOwnedLocallySelf)
                continue;

            sharedMovement.theGuide = userState.gameObject;
            return;
        }
    }

    private static GameObject FindGuideRig()
    {
        GuideFollow guideFollow = FindObjectOfType<GuideFollow>();
        if (guideFollow != null)
            return guideFollow.gameObject;

        return GameObject.Find("XR Origin (Guide Rig)");
    }

    private static GameObject FindParticipantRig()
    {
        VRScreenreader screenreader = FindObjectOfType<VRScreenreader>();
        if (screenreader != null)
            return screenreader.gameObject;

        return GameObject.Find("XR Origin (Player Rig)");
    }

    private static void FindAvatarManagers(out RealtimeAvatarManager guideManager, out RealtimeAvatarManager participantManager)
    {
        guideManager = null;
        participantManager = null;

        foreach (RealtimeAvatarManager manager in FindObjectsOfType<RealtimeAvatarManager>())
        {
            if (manager.localAvatarPrefab == null)
                continue;

            string prefabName = manager.localAvatarPrefab.name;
            if (prefabName == "Guide Avatar")
                guideManager = manager;
            else if (prefabName == "Player Avatar")
                participantManager = manager;
        }
    }

    private RealtimeUserState GetLocalUserState()
    {
        foreach (RealtimeUserState userState in FindObjectsOfType<RealtimeUserState>())
        {
            if (userState.realtimeView != null && userState.realtimeView.isOwnedLocallySelf)
                return userState;
        }

        return null;
    }
}
