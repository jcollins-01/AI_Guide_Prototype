using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShortTaskController : MonoBehaviour
{
    // Bools to control set-up and take down of short tasks from editor
    public bool navigationTaskActive;
    private bool previousNavTaskState;
    
    // Scripts we need access to
    private RandomTarget m_RandomTargetScript;

    // Variables to track scores
    public bool checkScores;
    private bool previousCheckScoreState;
    private int navigationTaskScore = 0;

    // Start is called before the first frame update
    void Start()
    {
        // All desired components should be on the Task Manager prefab with this controller
        m_RandomTargetScript = gameObject.GetComponent<RandomTarget>();

        // Set up state variables for detecting changes
        previousNavTaskState = navigationTaskActive;
        previousCheckScoreState = checkScores;
    }

    // Update is called once per frame
    void Update()
    {
        // Constantly check + update scores from tasks
        if (m_RandomTargetScript != null)
            CheckScoreUpdates();

        // Check if navigation task is active or inactive
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

    void CheckScoreUpdates()
    {
        // Pull latest scores from scripts
        navigationTaskScore = m_RandomTargetScript.timesTargetReached;

        // Display scores in editor if checkScores is true
        if (checkScores != previousCheckScoreState)
        {
            if (checkScores)
            {
                Debug.Log("Navigation task score is: " + navigationTaskScore);
            }
            previousCheckScoreState = checkScores;
        }
    }
}
