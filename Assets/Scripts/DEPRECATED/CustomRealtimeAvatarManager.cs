using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

public class CustomRealtimeAvatarManager : MonoBehaviour
{
    public Realtime realtime;
    public GameObject playerAvatarPrefab;
    public GameObject guideAvatarPrefab;

    private RealtimeAvatarManager _realtimeAvatarManager;

    private void Awake()
    {
        if (realtime == null)
        {
            realtime = GetComponent<Realtime>();
            Debug.Log("Got Realtime");
            if (realtime == null)
                Debug.LogError("Realtime component not found.");
        }

        _realtimeAvatarManager = GetComponent<RealtimeAvatarManager>();
        if (_realtimeAvatarManager == null)
        {
            Debug.LogError("RealtimeAvatarManager component not found on the same GameObject.");
            return;
        }

        realtime.didConnectToRoom += DidConnectToRoom;
        Debug.Log("Awake function");
    }

    private void DidConnectToRoom(Realtime room)
    {
        Debug.Log("Checking for connecting to room");


        if (!gameObject.activeInHierarchy || !enabled)
            return;

        // Instantiate avatars
        InstantiateAvatar(playerAvatarPrefab, "PlayerAvatar");
        InstantiateAvatar(guideAvatarPrefab, "GuideAvatar");
    }

    private void InstantiateAvatar(GameObject prefab, string avatarType)
    {
        if (prefab == null)
        {
            Debug.LogError($"{avatarType} prefab is not assigned.");
            return;
        }

        GameObject avatarObject = Realtime.Instantiate(prefab.name, new Realtime.InstantiateOptions
        {
            ownedByClient = true,
            preventOwnershipTakeover = true,
            destroyWhenOwnerLeaves = true,
            destroyWhenLastClientLeaves = true,
            useInstance = realtime
        }) ;

        Debug.Log("Instantiated avatar");

        RealtimeAvatar realtimeAvatar = avatarObject.GetComponent<RealtimeAvatar>();
        if (realtimeAvatar == null)
        {
            Debug.LogError($"{avatarType} prefab does not have a RealtimeAvatar component.");
            return;
        }

        _realtimeAvatarManager._RegisterAvatar(realtime.clientID, realtimeAvatar);
    }
}