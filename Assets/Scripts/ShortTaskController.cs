using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShortTaskController : MonoBehaviour
{
    // Variables to hold important gameobjects for controlling tasks
    public GameObject unloadingBag;
    public GameObject interactionTable;
    [HideInInspector]
    public string taskName;
    
    // Bools to control set-up and take down of short tasks from editor
    public bool navigationTaskActive;
    private bool previousNavTaskState;
    public bool unloadingTaskActive;
    private bool previousUnloadTaskState;

    // Scripts we need access to
    private RandomTarget m_RandomTargetScript;
    private RandomObjectSpawner m_RandomObjectSpawnerScript;

    // Variables to track scores
    public bool checkScores;
    private bool previousCheckScoreState;
    private int navigationTaskScore = 0;
    private int unloadingTaskScore = 0;

    // Start is called before the first frame update
    void Start()
    {
        // All desired components should be on the Task Manager prefab with this controller
        m_RandomTargetScript = gameObject.GetComponent<RandomTarget>();

        // Set up state variables for detecting changes
        previousNavTaskState = navigationTaskActive;
        previousUnloadTaskState = unloadingTaskActive;
        previousCheckScoreState = checkScores;
    }

    // Update is called once per frame
    void Update()
    {
        // Constantly check + update scores from tasks
        if (m_RandomTargetScript != null && m_RandomObjectSpawnerScript != null)
            CheckScoreUpdates();

        // Check if navigation task is active or inactive
        if (m_RandomTargetScript != null)
            CheckNavigationTaskActive();

        // Check if unloading task is active or inactive -- needs to be run to set up m_RandomObjectSpawnerScript
        CheckUnloadingTaskActive();
    }

    private void CheckNavigationTaskActive()
    {
        if (navigationTaskActive != previousNavTaskState)
        {
            if (navigationTaskActive)
            {
                Debug.Log("Setting up navigation task");
                m_RandomTargetScript.SetUpRandomTargets();
            }
            else
            {
                Debug.Log("Taking down navigation task");
                m_RandomTargetScript.TakeDownRandomTargets();
            }

            // Update previousNavTaskState to match the new state of navigationTaskActive
            previousNavTaskState = navigationTaskActive;
        }
    }

    private void CheckUnloadingTaskActive()
    {
        if (unloadingTaskActive != previousUnloadTaskState)
        {
            if (unloadingTaskActive)
            {
                Debug.Log("Setting up unloading task");
                SetUpUnloadSpawner();
            }
            else
            {
                Debug.Log("Taking down unloading task");
                TakeDownUnloadSpawner();
            }

            // Update previousUnloadTaskState to match the new state of unloadingTaskActive
            previousUnloadTaskState = unloadingTaskActive;
        }
    }

    private void SetUpUnloadSpawner()
    {
        if (unloadingBag != null)
        {
            taskName = "Unloading"; // Set task name as Unloading to guide Unload Spawner

            if (m_RandomObjectSpawnerScript == null) // First time running the unloading task
            {
                unloadingBag.AddComponent<RandomObjectSpawner>(); // Add an unload spawner
                m_RandomObjectSpawnerScript = unloadingBag.GetComponent<RandomObjectSpawner>();
                m_RandomObjectSpawnerScript.SpawnRandomObject();
            }
            else
                m_RandomObjectSpawnerScript.SpawnRandomObject(); // All other times, the bag should already have the spawner added
        }
    }

    private void TakeDownUnloadSpawner()
    {
        if (unloadingBag != null)
        {
            if (m_RandomObjectSpawnerScript != null) // If the bag has had a RandomObjectSpawner added (the task had begun at some point)
            {
                if (m_RandomObjectSpawnerScript.spawnedObject != null) // Destroy any lingering spawnedObjects from unloading
                    Destroy(m_RandomObjectSpawnerScript.spawnedObject);
            }
        }
    }

    void CheckScoreUpdates()
    {
        // Pull latest scores from scripts
        navigationTaskScore = m_RandomTargetScript.timesTargetReached;
        unloadingTaskScore = m_RandomObjectSpawnerScript.timesObjectUnloaded;

        // Display scores in editor if checkScores is true
        if (checkScores != previousCheckScoreState)
        {
            if (checkScores)
            {
                Debug.Log("Navigation task score is: " + navigationTaskScore);
                Debug.Log("Unloading task score is: " + unloadingTaskScore);
            }
            previousCheckScoreState = checkScores;
        }
    }
}
