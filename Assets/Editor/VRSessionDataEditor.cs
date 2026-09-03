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

    // This forces the Unity Inspector to update every frame so you can see the timer tick visually
    public override bool RequiresConstantRepaint()
    {
        return true;
    }

    public override void OnInspectorGUI()
    {
        VRSessionData data = (VRSessionData)target;

        GUILayout.Label("Participant Settings", EditorStyles.boldLabel);
        data.participantID = EditorGUILayout.TextField("Participant ID", data.participantID);

        GUILayout.Space(15);
        GUILayout.Label("Task Speed and Completion Checks", EditorStyles.boldLabel);

        // Display each task with its own timer and success toggle
        foreach (var task in data.tasks)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();

            GUILayout.Label(task.taskName, GUILayout.Width(140));

            // Timer controls
            if (!task.isTimerRunning)
            {
                if (GUILayout.Button("Start", GUILayout.Width(50)))
                {
                    task.timerStartTime = EditorApplication.timeSinceStartup;
                    task.isTimerRunning = true;
                }
            }
            else
            {
                GUI.backgroundColor = Color.red; // Turns the stop button red
                if (GUILayout.Button("Stop", GUILayout.Width(50)))
                {
                    task.timeSpentSeconds += (EditorApplication.timeSinceStartup - task.timerStartTime);
                    task.isTimerRunning = false;
                }
                GUI.backgroundColor = Color.white;
            }

            // Calculate and display live time
            double displayTime = task.timeSpentSeconds;
            if (task.isTimerRunning)
            {
                displayTime += (EditorApplication.timeSinceStartup - task.timerStartTime);
            }
            GUILayout.Label($"{displayTime:F1}s", GUILayout.Width(45));

            // Success toggle
            GUILayout.Label("Completed?", GUILayout.Width(60));
            task.isSuccessful = EditorGUILayout.Toggle(task.isSuccessful, GUILayout.Width(20));

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        GUILayout.Space(15);
        GUILayout.Label("Task Comprehension Checklists", EditorStyles.boldLabel);

        if (data.trialEvents.Count == 0)
        {
            GUILayout.Label("No object zones entered yet.", EditorStyles.miniLabel);
        }

        for (int i = 0; i < data.trialEvents.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(data.trialEvents[i].objectName, GUILayout.Width(150));
            data.trialEvents[i].verifiedInEditor = EditorGUILayout.Toggle(data.trialEvents[i].verifiedInEditor);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(20);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("SAVE TO CSV", GUILayout.Height(40)))
        {
            SaveToCSV(data);
        }
        GUI.backgroundColor = Color.white;

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
            // Create headers
            if (!fileExists)
            {
                writer.WriteLine("ParticipantID,RecordType,ItemName,CompletedOrEncounteredVR,ComprehensionVerified,TimeSpentSeconds");
            }

            // Write the Tasks first
            foreach (var task in data.tasks)
            {
                writer.WriteLine($"{data.participantID},Task,{task.taskName},{task.isSuccessful},NA,{task.timeSpentSeconds:F2}");
            }

            // Write the CompItems (the comprehension checks that we do afterwards based on what the user encountered in VR) second
            foreach (var evt in data.trialEvents)
            {
                writer.WriteLine($"{data.participantID},CompItem,{evt.objectName},{evt.passedInVR},{evt.verifiedInEditor},NA");
            }
        }

        Debug.Log($"Data appended for {data.participantID} at {filePath}");
        data.ClearSession();
    }
}