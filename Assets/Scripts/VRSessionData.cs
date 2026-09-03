using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TrackedEvent
{
    public string objectName;
    public bool passedInVR;
    public bool verifiedInEditor; // The toggle you will click after the session
}

[System.Serializable]
public class TaskData
{
    public string taskName;
    public bool isSuccessful;
    public double timeSpentSeconds;

    // Internal timer variables (Not saved to CSV)
    [System.NonSerialized] public bool isTimerRunning;
    [System.NonSerialized] public double timerStartTime;
}

[CreateAssetMenu(fileName = "NewSessionData", menuName = "VR Logging/Session Data")]
public class VRSessionData : ScriptableObject
{
    public string participantID = "P001";

    // Two data lists, one for data specifically attached to tasks, one for the events we track and check after
    public List<TaskData> tasks = new List<TaskData>();
    public List<TrackedEvent> trialEvents = new List<TrackedEvent>();

    // Automatically populates the five tasks into the session object
    private void OnEnable()
    {
        if (tasks.Count == 0)
        {
            tasks.Add(new TaskData { taskName = "Scene Understanding" });
            tasks.Add(new TaskData { taskName = "Navigation" });
            tasks.Add(new TaskData { taskName = "Object Location" });
            tasks.Add(new TaskData { taskName = "Visual Description" });
            tasks.Add(new TaskData { taskName = "Object Grabbing" });
        }
    }

    public void ClearSession()
    {
        trialEvents.Clear();
        foreach (var task in tasks)
        {
            task.isSuccessful = false;
            task.timeSpentSeconds = 0;
            task.isTimerRunning = false;
        }
    }
}