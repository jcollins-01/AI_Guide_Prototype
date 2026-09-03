using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(VRSessionData))]
public class VRSessionDataEditor : Editor
{
    [MenuItem("Tools/Create VR Session Data File")]
    public static void CreateSessionDataAsset()
    {
        VRSessionData asset = ScriptableObject.CreateInstance<VRSessionData>();
        AssetDatabase.CreateAsset(asset, "Assets/CurrentSession.asset");
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
        Debug.Log("Created CurrentSession.asset in your Assets folder!");
    }

    public override void OnInspectorGUI()
    {
        VRSessionData data = (VRSessionData)target;

        GUILayout.Label("Participant Settings", EditorStyles.boldLabel);
        data.participantID = EditorGUILayout.TextField("Participant ID", data.participantID);

        GUILayout.Space(10);
        GUILayout.Label("Post-Session Verification Checklist", EditorStyles.boldLabel);

        // Display a toggle for every object passed during the session
        for (int i = 0; i < data.trialEvents.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(data.trialEvents[i].objectName, GUILayout.Width(150));
            data.trialEvents[i].verifiedInEditor = EditorGUILayout.Toggle(data.trialEvents[i].verifiedInEditor);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(20);

        if (GUILayout.Button("SAVE TO CSV", GUILayout.Height(40)))
        {
            SaveToCSV(data);
        }

        // Force Unity to save the ScriptableObject changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(data);
        }
    }

    private void SaveToCSV(VRSessionData data)
    {
        string directory = Application.dataPath + "/Logs/";
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string filePath = directory + data.participantID + "_Log.csv";
        bool fileExists = File.Exists(filePath);

        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            // Write standard headers if creating a new file
            if (!fileExists)
            {
                writer.WriteLine("ParticipantID,ObjectName,PassedInVR,VerifiedInEditor");
            }

            // Append each logged event
            foreach (var evt in data.trialEvents)
            {
                writer.WriteLine($"{data.participantID},{evt.objectName},{evt.passedInVR},{evt.verifiedInEditor}");
            }
        }

        Debug.Log($"Data successfully appended for {data.participantID} at {filePath}");
        data.ClearSession(); // Ready for the next trial
    }
}