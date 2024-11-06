using UnityEngine;

public class SpawnInOpenAreas : MonoBehaviour
{
    public GameObject prefabToSpawn;       // The prefab to spawn in open areas
    public GameObject areaReferenceObject; // The object to define the center and bounds of the area
    public LayerMask obstacleLayer;        // Layer mask for obstacles to detect
    private Vector3 areaSize = new Vector3(10, 1, 10); // Size of the area to search (width, height, depth)
    private Vector3 cellSize = new Vector3(1, 1, 1);   // Size of each cell to check
    private Vector3 areaCenter;

    void Start()
    {
        if (areaReferenceObject != null)
        {
            // Use the center of the areaReferenceObject as the area center
            areaCenter = areaReferenceObject.transform.position;
            FindObstaclesAndAddColliders();
            FindAndSpawnInOpenSpaces();
            //FindObstaclesAndRemoveColliders();
        }
        else
        {
            Debug.LogError("Area Reference Object is not assigned!");
        }
    }

    void FindObstaclesAndAddColliders()
    {
        // Get all objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        // Loop through each object
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == 13) // KeyItems
            {
                // Add collider to parent object
                if (!obj.GetComponentInChildren<BoxCollider>())
                    obj.AddComponent<BoxCollider>();

                // Get the child objects and add colliders 
                foreach (Transform child in obj.transform)
                {
                    if (child.gameObject.layer != 13 || child.gameObject.layer != 7 || child.gameObject.layer != 10) // If the child is not a key item or on another important layer
                    {
                        child.gameObject.layer = 8; // Obstacles
                        if (!child.gameObject.GetComponentInChildren<BoxCollider>())
                            child.gameObject.AddComponent<BoxCollider>();
                    }
                }
            }
        }
    }

    void FindObstaclesAndRemoveColliders()
    {
        // Get all objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        // Loop through each object
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == 13) // KeyItems
            {
                if (obj.GetComponentInChildren<BoxCollider>())
                    Destroy(obj.GetComponentInChildren<BoxCollider>());

                // Get the child objects and destroy colliders 
                foreach (Transform child in obj.transform)
                {
                    if (child.gameObject.GetComponentInChildren<BoxCollider>())
                        Destroy(child.gameObject.GetComponentInChildren<BoxCollider>());
                }
            }
        }
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