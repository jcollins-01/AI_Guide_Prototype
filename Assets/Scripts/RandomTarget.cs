using System.Collections.Generic;
using UnityEngine;

public class RandomTarget : MonoBehaviour
{
    // Variables for spawning target positions
    public GameObject prefabToSpawn;       // The prefab to spawn in open areas

    // Variables for short tasks with targets
    public List<GameObject> spawnPoints = new List<GameObject>();
    public List<GameObject> randomTargets = new List<GameObject>();
    private SelectedTarget m_SelectedTargetScript;
    public int timesTargetReached = 0;
    private int previousTargetIndex = -1;

    public void SetUpRandomTargets()
    {
        // Set up all possible destinations for random target points
        SpawnAtSelectedLocations();

        // Assign targets to be a random target for the navigation task
        GetNumberOfPossibleTargets();
        RandomTargetSelection();
    }

    public void TakeDownRandomTargets()
    {
        // Destroy all random targets that were created during set-up
        foreach (GameObject obj in randomTargets)
        {
            Destroy(obj);
        }

        randomTargets.Clear();
    }

    private void Update()
    {
        CheckTargetReached();
    }

    void RandomTargetSelection()
    {
        //Debug.Log("Select new random target");
        int totalTargets = randomTargets.Count;
        if (totalTargets == 0)
        {
            Debug.Log("RandomTargetSelection: No targets found!");
            return;
        }
        int randomTargetIndex = Random.Range(0, totalTargets);
        while (randomTargetIndex == previousTargetIndex)
        {
            randomTargetIndex = Random.Range(0, totalTargets);
        }
        previousTargetIndex = randomTargetIndex;
        GameObject target = randomTargets[randomTargetIndex];

        // Set the active target name
        target.name = "Target Destination";

        // Add the component to the target that is the script which determines if a player enters it
        target.AddComponent<SelectedTarget>();
        m_SelectedTargetScript = target.GetComponent<SelectedTarget>();
    }

    void CheckTargetReached()
    {
        if (m_SelectedTargetScript != null)
        {
            if (m_SelectedTargetScript.playerReachedTarget)
            {
                //Debug.Log("Player reached target - destroying SelectedTarget and choosing a new one");

                // Revert the name back to inactive
                m_SelectedTargetScript.gameObject.name = "Target Destination Inactive";

                Destroy(m_SelectedTargetScript);
                timesTargetReached++;
                RandomTargetSelection();
            }
        }
    }

    void GetNumberOfPossibleTargets()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.tag == "Travel Target")
                randomTargets.Add(obj);
        }
    }

    void SpawnAtSelectedLocations()
    {
        foreach (GameObject obj in spawnPoints)
        {
            Vector3 spawnPosition = obj.transform.position;
            GameObject spawned = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

            // Set the initial inactive name
            spawned.name = "Target Destination Inactive";

            Debug.Log("Spawned prefab at: " + spawnPosition);
        }
    }
}