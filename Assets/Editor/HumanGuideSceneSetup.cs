using System;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the Human Guide setup (as used in the Kitchen scene) to other scenes:
// a RealtimeView + HumanGuideRoomSync and a disabled RealtimeAvatarManager for the
// Human Guide Avatar on RoomManager, and turns on SwitchTools.HumanGuideActive.
// The human guide's avatar follows the same rig (root/head/hands) as the Player Avatar.
public static class HumanGuideSceneSetup
{
    private const string HumanGuideAvatarPath = "Assets/Resources/Human Guide Avatar.prefab";

    private static readonly string[] Scenes =
    {
        "Assets/Scenes/Alien Spaceship Repair Shop.unity",
        "Assets/Scenes/Flower Shop.unity",
        "Assets/Scenes/Guide Parks/GuidePark1_Networked.unity",
        "Assets/Scenes/Guide Parks/GuidePark2_Networked.unity",
        "Assets/Scenes/Guide Parks/GuidePark3_Networked.unity",
        "Assets/Scenes/Monster Pet Shop.unity",
        "Assets/Scenes/Pharmacy.unity",
    };

    [MenuItem("Tools/Human Guide/Add Human Guide Setup To Scenes")]
    public static void SetUpScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int failures = SetUpAll();
        EditorUtility.DisplayDialog("Human Guide Setup",
            failures == 0 ? "All scenes set up. See the Console for details." : $"{failures} scene(s) failed. See the Console.",
            "OK");
    }

    // Entry point for running from the command line (-batchmode -executeMethod HumanGuideSceneSetup.SetUpScenesBatch)
    public static void SetUpScenesBatch()
    {
        int failures = SetUpAll();
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int SetUpAll()
    {
        GameObject humanGuideAvatar = AssetDatabase.LoadAssetAtPath<GameObject>(HumanGuideAvatarPath);
        if (humanGuideAvatar == null)
        {
            Debug.LogError($"[HumanGuideSetup] Could not load {HumanGuideAvatarPath}");
            return Scenes.Length;
        }

        int failures = 0;
        foreach (string scenePath in Scenes)
        {
            try
            {
                if (!SetUpScene(scenePath, humanGuideAvatar))
                    failures++;
            }
            catch (Exception e)
            {
                Debug.LogError($"[HumanGuideSetup] {scenePath}: {e}");
                failures++;
            }
        }

        Debug.Log($"[HumanGuideSetup] Done. {Scenes.Length - failures} of {Scenes.Length} scenes OK.");
        return failures;
    }

    private static bool SetUpScene(string scenePath, GameObject humanGuideAvatar)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // RoomManager is the object with SwitchTools, Realtime and the avatar managers
        SwitchTools[] switchTools = UnityEngine.Object.FindObjectsOfType<SwitchTools>(true);
        if (switchTools.Length != 1)
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: expected 1 SwitchTools, found {switchTools.Length}.");
            return false;
        }

        GameObject roomManager = switchTools[0].gameObject;
        Realtime realtime = roomManager.GetComponent<Realtime>();
        if (realtime == null)
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: {roomManager.name} has no Realtime component.");
            return false;
        }

        RealtimeAvatarManager playerManager = FindManager(roomManager, "Player Avatar");
        if (playerManager == null)
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: no RealtimeAvatarManager with the Player Avatar prefab on {roomManager.name}.");
            return false;
        }

        // Room-wide role sync (needs a RealtimeView on the same object)
        RealtimeView view = roomManager.GetComponent<RealtimeView>();
        if (view == null)
            view = roomManager.AddComponent<RealtimeView>();

        HumanGuideRoomSync roomSync = roomManager.GetComponent<HumanGuideRoomSync>();
        if (roomSync == null)
            roomSync = roomManager.AddComponent<HumanGuideRoomSync>();

        ConfigureView(view, realtime, roomSync);

        // Avatar manager for the human guide, off until HumanGuideRoomSync assigns the guide role
        RealtimeAvatarManager humanGuideManager = FindManager(roomManager, "Human Guide Avatar");
        if (humanGuideManager == null)
        {
            humanGuideManager = roomManager.AddComponent<RealtimeAvatarManager>();
            humanGuideManager.localAvatarPrefab = humanGuideAvatar;
        }
        humanGuideManager.enabled = false;
        CopyLocalPlayer(playerManager, humanGuideManager);

        SerializedObject switchToolsObject = new SerializedObject(switchTools[0]);
        switchToolsObject.FindProperty("HumanGuideActive").boolValue = true;
        switchToolsObject.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: failed to save.");
            return false;
        }

        SerializedProperty localPlayer = new SerializedObject(humanGuideManager).FindProperty("_localPlayer");
        Debug.Log($"[HumanGuideSetup] {scenePath}: set up on {roomManager.name}. Human guide avatar follows " +
                  $"root={NameOf(localPlayer, "root")}, head={NameOf(localPlayer, "head")}, " +
                  $"leftHand={NameOf(localPlayer, "leftHand")}, rightHand={NameOf(localPlayer, "rightHand")}.");
        return true;
    }

    private static RealtimeAvatarManager FindManager(GameObject roomManager, string prefabName)
    {
        foreach (RealtimeAvatarManager manager in roomManager.GetComponents<RealtimeAvatarManager>())
        {
            if (manager.localAvatarPrefab != null && manager.localAvatarPrefab.name == prefabName)
                return manager;
        }
        return null;
    }

    // Match the scene view settings used in the Kitchen scene and register HumanGuideRoomSync with the view
    private static void ConfigureView(RealtimeView view, Realtime realtime, HumanGuideRoomSync roomSync)
    {
        SerializedObject viewObject = new SerializedObject(view);
        viewObject.FindProperty("_realtime").objectReferenceValue = realtime;

        SerializedProperty viewUUID = viewObject.FindProperty("_viewUUID");
        if (string.IsNullOrEmpty(viewUUID.stringValue))
            viewUUID.stringValue = Guid.NewGuid().ToString();

        SetBool(viewObject, "_sceneViewPreventOwnershipTakeover", false);
        SetBool(viewObject, "_sceneViewDestroyWhenOwnerOrLastClientLeaves", true);
        SetBool(viewObject, "_sceneViewDestroyWhenLastClientLeaves", true);

        SerializedProperty components = viewObject.FindProperty("_components");
        var usedIDs = new HashSet<int>();
        bool registered = false;
        for (int i = 0; i < components.arraySize; i++)
        {
            SerializedProperty entry = components.GetArrayElementAtIndex(i);
            usedIDs.Add(entry.FindPropertyRelative("componentID").intValue);
            if (entry.FindPropertyRelative("component").objectReferenceValue == roomSync)
                registered = true;
        }

        if (!registered)
        {
            int id = 1;
            while (usedIDs.Contains(id))
                id++;

            components.InsertArrayElementAtIndex(components.arraySize);
            SerializedProperty entry = components.GetArrayElementAtIndex(components.arraySize - 1);
            entry.FindPropertyRelative("componentID").intValue = id;
            entry.FindPropertyRelative("component").objectReferenceValue = roomSync;
        }

        viewObject.ApplyModifiedProperties();
    }

    private static void CopyLocalPlayer(RealtimeAvatarManager from, RealtimeAvatarManager to)
    {
        SerializedProperty source = new SerializedObject(from).FindProperty("_localPlayer");
        SerializedObject targetObject = new SerializedObject(to);
        SerializedProperty target = targetObject.FindProperty("_localPlayer");

        foreach (string field in new[] { "root", "head", "leftHand", "rightHand" })
            target.FindPropertyRelative(field).objectReferenceValue = source.FindPropertyRelative(field).objectReferenceValue;

        targetObject.ApplyModifiedProperties();
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
        else
            Debug.LogWarning($"[HumanGuideSetup] RealtimeView has no {propertyName} field, left as is.");
    }

    private static string NameOf(SerializedProperty localPlayer, string field)
    {
        UnityEngine.Object value = localPlayer.FindPropertyRelative(field).objectReferenceValue;
        return value != null ? value.name : "NONE";
    }
}
