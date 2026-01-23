using System.Collections.Generic;
using UnityEngine;

public class RandomTarget : MonoBehaviour
{
    // Variables for spawning target positions
    public GameObject prefabToSpawn;       // The prefab to spawn in open areas
    public GameObject areaReferenceObject; // The object to define the center and bounds of the area
    public LayerMask obstacleLayer;        // Layer mask for obstacles to detect
    private Vector3 areaSize = new Vector3(10, 1, 10); // Size of the area to search (width, height, depth)
    private Vector3 cellSize = new Vector3(1, 1, 1);   // Size of each cell to check
    private Vector3 areaCenter;

    // Variables for short tasks with targets
    private List<BoxCollider> targetColliders = new List<BoxCollider>();
    public List<GameObject> randomTargets = new List<GameObject>();
    private SelectedTarget m_SelectedTargetScript;
    public int timesTargetReached = 0;
    private int previousTargetIndex = -1;

    void Start()
    {
        if (areaReferenceObject != null)
        {
            areaCenter = areaReferenceObject.transform.position; // Use the center of the areaReferenceObject as the area center
            //SetUpRandomTargets(); // Set-up now called from the ShortTaskController
        }
        else
        {
            Debug.LogError("Area Reference Object is not assigned! Cannot begin navigation task.");
        }
    }

    public void SetUpRandomTargets()
    {
        // Set up all possible destinations for random target points
        FindObstaclesAndAddColliders();
        FindAndSpawnInOpenSpaces();
        FindObstaclesAndRemoveColliders(); // Might remove later if we need to keep the obstacles for VR mode

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

    void FindObstaclesAndAddColliders()
    {
        Debug.Log("Adding colliders");
        // Get all objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        // Loop through each object
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == 13) // KeyItems
            {
                // Add collider to parent object
                if (!obj.GetComponentInChildren<BoxCollider>())
                {
                    BoxCollider targetCollider = obj.AddComponent<BoxCollider>();
                    targetColliders.Add(targetCollider);
                }

                // Get the child objects and add colliders 
                foreach (Transform child in obj.transform)
                {
                    if (child.gameObject.layer != 13 && child.gameObject.layer != 7 && child.gameObject.layer != 10) // If the child is not a key item or on another important layer
                    {
                        child.gameObject.layer = 8; // Obstacles, children must be set to this layer or their colliders will be ignored when considering spawn positions
                        if (!child.gameObject.GetComponentInChildren<BoxCollider>())
                        {
                            BoxCollider targetCollider = child.gameObject.AddComponent<BoxCollider>();
                            targetColliders.Add(targetCollider);
                        }
                    }
                }
            }
        }
    }

    void FindObstaclesAndRemoveColliders()
    {
        Debug.Log("Removing colliders");
        // Get all colliders in the targetColliders list
        foreach (BoxCollider targetCollider in targetColliders)
        {
            Destroy(targetCollider);
        }

        targetColliders.Clear();
    }

    void FindAndSpawnInOpenSpaces()
    {
        // Calculate bounds of the area based on areaReferenceObject's position
        Vector3 areaMin = areaCenter - areaSize / 2;
        Vector3 areaMax = areaCenter + areaSize / 2;

        // Loop through the area using the cell size
        for (float x = areaMin.x; x < areaMax.x; x += cellSize.x)
        {
            for (float z = areaMin.z; z < areaMax.z; z += cellSize.z)
            {
                Vector3 positionToCheck = new Vector3(x, areaCenter.y, z);

                // Check if the cell is open
                if (IsPositionOpen(positionToCheck))
                {
                    // Spawn the prefab at this open position
                    Instantiate(prefabToSpawn, positionToCheck, Quaternion.identity);
                    Debug.Log("Spawned prefab at: " + positionToCheck);
                }
            }
        }
    }

    bool IsPositionOpen(Vector3 position)
    {
        // Check for obstacles within the cell using OverlapBox
        Collider[] colliders = Physics.OverlapBox(position, cellSize / 2, Quaternion.identity, obstacleLayer);

        // If no colliders found, the position is open
        return colliders.Length == 0;
    }

    // Draw gizmos to visualize the area and cell size in the Scene view
    private void OnDrawGizmos()
    {
        if (areaReferenceObject != null)
        {
            Gizmos.color = Color.green;
            Vector3 areaCenter = areaReferenceObject.transform.position;
            Gizmos.DrawWireCube(areaCenter, areaSize);

            if (cellSize != Vector3.zero)
            {
                Vector3 areaMin = areaCenter - areaSize / 2;
                Vector3 areaMax = areaCenter + areaSize / 2;

                Gizmos.color = Color.cyan;
                for (float x = areaMin.x; x < areaMax.x; x += cellSize.x)
                {
                    for (float z = areaMin.z; z < areaMax.z; z += cellSize.z)
                    {
                        Vector3 positionToCheck = new Vector3(x, areaCenter.y, z);
                        Gizmos.DrawWireCube(positionToCheck, cellSize);
                    }
                }
            }
        }
    }
}