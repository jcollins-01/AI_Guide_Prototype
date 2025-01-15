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
    public bool preparationTaskActive;
    private bool previousPrepTaskState;

    // Scripts we need access to
    private RandomTarget m_RandomTargetScript;
    private RandomObjectSpawner m_UnloadSpawnerScript;
    private RandomObjectSpawner m_PrepareSpawnerScript;

    // Variables to track scores
    public bool checkScores;
    private bool previousCheckScoreState;
    public int navigationTaskScore = 0;
    public int unloadingTaskScore = 0;
    public int preparationTaskScore = 0;

    // Start is called before the first frame update
    void Start()
    {
        // All desired components should be on the Task Manager prefab with this controller
        m_RandomTargetScript = gameObject.GetComponent<RandomTarget>();

        // Set up state variables for detecting changes
        previousNavTaskState = navigationTaskActive;
        previousUnloadTaskState = unloadingTaskActive;
        previousPrepTaskState = preparationTaskActive;
        previousCheckScoreState = checkScores;
    }

    // Update is called once per frame
    void Update()
    {
        // Constantly check + update scores from tasks
        if (m_RandomTargetScript != null && m_UnloadSpawnerScript != null && m_PrepareSpawnerScript != null)
            CheckScoreUpdates();

        // Check if navigation task is active or inactive
        if (m_RandomTargetScript != null)
            CheckNavigationTaskActive();

        // Check if unloading task is active or inactive -- needs to be run to set up m_UnloadSpawnerScript
        CheckUnloadingTaskActive();

        // Check if prerpation task is active or inactive -- needs to be run to set up m_PrepareSpawnerScript
        CheckPreparationTaskActive();
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

    private void CheckPreparationTaskActive()
    {
        if (preparationTaskActive != previousPrepTaskState)
        {
            if (preparationTaskActive)
            {
                Debug.Log("Setting up preparation task");
                SetUpPrepareSpawner();
            }
            else
            {
                Debug.Log("Taking down preparation task");
                TakeDownPrepareSpawner();
            }

            // Update previousPrepTaskState to match the new state of preparationTaskActive
            previousPrepTaskState = preparationTaskActive;
        }
    }

    private void SetUpPrepareSpawner()
    {
        if (interactionTable != null)
        {
            taskName = "Preparation"; // Set task name as Preparatino to guide Spawner

            if (m_PrepareSpawnerScript == null) // First time running the preparation task
            {
                interactionTable.AddComponent<RandomObjectSpawner>(); // Add a spawner
                m_PrepareSpawnerScript = interactionTable.GetComponent<RandomObjectSpawner>();
                m_PrepareSpawnerScript.SpawnRandomObject();
            }
            else
                m_PrepareSpawnerScript.SpawnRandomObject(); // All other times, the table should already have the spawner added
        }
    }

    private void TakeDownPrepareSpawner()
    {
        if (interactionTable != null)
        {
            if (m_PrepareSpawnerScript != null) // If the table has had a RandomObjectSpawner added (the task had begun at some point)
            {
                if (m_PrepareSpawnerScript.spawnedObject != null) // Destroy any lingering spawnedObjects from preparation
                    Destroy(m_PrepareSpawnerScript.spawnedObject);
            }
        }
    }

    private void SetUpUnloadSpawner()
    {
        if (unloadingBag != null)
        {
            taskName = "Unloading"; // Set task name as Unloading to guide Spawner

            if (m_UnloadSpawnerScript == null) // First time running the unloading task
            {
                unloadingBag.AddComponent<RandomObjectSpawner>(); // Add a spawner
                m_UnloadSpawnerScript = unloadingBag.GetComponent<RandomObjectSpawner>();
                m_UnloadSpawnerScript.SpawnRandomObject();
            }
            else
                m_UnloadSpawnerScript.SpawnRandomObject(); // All other times, the bag should already have the spawner added
        }
    }

    private void TakeDownUnloadSpawner()
    {
        if (unloadingBag != null)
        {
            if (m_UnloadSpawnerScript != null) // If the bag has had a RandomObjectSpawner added (the task had begun at some point)
            {
                if (m_UnloadSpawnerScript.spawnedObject != null) // Destroy any lingering spawnedObjects from unloading
                    Destroy(m_UnloadSpawnerScript.spawnedObject);
            }
        }
    }

    void CheckScoreUpdates()
    {
        // Pull latest scores from scripts
        navigationTaskScore = m_RandomTargetScript.timesTargetReached;
        unloadingTaskScore = m_UnloadSpawnerScript.timesObjectUnloaded;
        preparationTaskScore = m_PrepareSpawnerScript.timesObjectPrepared;

        // Display scores in editor if checkScores is true
        if (checkScores != previousCheckScoreState)
        {
            if (checkScores)
            {
                Debug.Log("Navigation task score is: " + navigationTaskScore);
                Debug.Log("Unloading task score is: " + unloadingTaskScore);
                Debug.Log("Preparation task score is: " + preparationTaskScore);
            }
            previousCheckScoreState = checkScores;
        }
    }
}
