using System;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the Human Guide setup (as used in the Kitchen scene) to every scene under Assets/Scenes
// (except Old Scenes) that has a RoomManager: a RealtimeView + HumanGuideRoomSync and a disabled
// RealtimeAvatarManager for the Human Guide Avatar on RoomManager, and turns on SwitchTools.HumanGuideActive.
// The human guide's avatar follows the same rig (root/head/hands) as the Player Avatar.
// Scenes that are already set up are left untouched.
public static class HumanGuideSceneSetup
{
    private const string HumanGuideAvatarPath = "Assets/Resources/Human Guide Avatar.prefab";
    private const string ScenesFolder = "Assets/Scenes";
    private const string OldScenesFolder = "Assets/Scenes/Old Scenes/";

    private enum Result { SetUp, AlreadySetUp, NoRoomManager, Failed }

    [MenuItem("Tools/Human Guide/Add Human Guide Setup To All Scenes")]
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
            return 1;
        }

        var counts = new Dictionary<Result, int>();
        foreach (string scenePath in FindScenes())
        {
            Result result;
            try
            {
                result = SetUpScene(scenePath, humanGuideAvatar);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HumanGuideSetup] {scenePath}: {e}");
                result = Result.Failed;
            }
            counts.TryGetValue(result, out int count);
            counts[result] = count + 1;
        }

        counts.TryGetValue(Result.SetUp, out int setUp);
        counts.TryGetValue(Result.AlreadySetUp, out int alreadySetUp);
        counts.TryGetValue(Result.NoRoomManager, out int noRoomManager);
        counts.TryGetValue(Result.Failed, out int failures);
        Debug.Log($"[HumanGuideSetup] Done. Set up: {setUp}, already set up: {alreadySetUp}, " +
                  $"no RoomManager (skipped): {noRoomManager}, failed: {failures}.");
        return failures;
    }

    private static List<string> FindScenes()
    {
        var scenes = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(OldScenesFolder))
                scenes.Add(path);
        }
        scenes.Sort();
        return scenes;
    }

    private static Result SetUpScene(string scenePath, GameObject humanGuideAvatar)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // RoomManager is the object with SwitchTools, Realtime and the avatar managers
        SwitchTools[] switchTools = UnityEngine.Object.FindObjectsOfType<SwitchTools>(true);
        if (switchTools.Length == 0)
        {
            Debug.Log($"[HumanGuideSetup] {scenePath}: no RoomManager (SwitchTools), skipped.");
            return Result.NoRoomManager;
        }
        if (switchTools.Length > 1)
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: expected 1 SwitchTools, found {switchTools.Length}.");
            return Result.Failed;
        }

        GameObject roomManager = switchTools[0].gameObject;
        Realtime realtime = roomManager.GetComponent<Realtime>();
        if (realtime == null)
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: {roomManager.name} has no Realtime component.");
            return Result.Failed;
        }

        RealtimeAvatarManager playerManager = FindManager(roomManager, "Player Avatar");
        if (playerManager == null)
        {
            Debug.LogError($"[HumanGuideSetup] {scenePath}: no RealtimeAvatarManager with the Player Avatar prefab on {roomManager.name}.");
            return Result.Failed;
        }

        // Don't re-save scenes that are already done (saving re-serializes unrelated scene data)
        if (IsSetUp(switchTools[0], playerManager))
        {
            Debug.Log($"[HumanGuideSetup] {scenePath}: already set up, left unchanged.");
            return Result.AlreadySetUp;
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
            return Result.Failed;
        }

        SerializedProperty localPlayer = new SerializedObject(humanGuideManager).FindProperty("_localPlayer");
        Debug.Log($"[HumanGuideSetup] {scenePath}: set up on {roomManager.name}. Human guide avatar follows " +
                  $"root={NameOf(localPlayer, "root")}, head={NameOf(localPlayer, "head")}, " +
                  $"leftHand={NameOf(localPlayer, "leftHand")}, rightHand={NameOf(localPlayer, "rightHand")}.");
        return Result.SetUp;
    }

    private static bool IsSetUp(SwitchTools switchTools, RealtimeAvatarManager playerManager)
    {
        GameObject roomManager = switchTools.gameObject;
        RealtimeAvatarManager humanGuideManager = FindManager(roomManager, "Human Guide Avatar");
        if (!switchTools.HumanGuideActive ||
            roomManager.GetComponent<HumanGuideRoomSync>() == null ||
            roomManager.GetComponent<RealtimeView>() == null ||
            humanGuideManager == null ||
            humanGuideManager.enabled)
            return false;

        SerializedProperty playerRig = new SerializedObject(playerManager).FindProperty("_localPlayer");
        SerializedProperty humanGuideRig = new SerializedObject(humanGuideManager).FindProperty("_localPlayer");
        foreach (string field in new[] { "root", "head", "leftHand", "rightHand" })
        {
            if (playerRig.FindPropertyRelative(field).objectReferenceValue != humanGuideRig.FindPropertyRelative(field).objectReferenceValue)
                return false;
        }
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
