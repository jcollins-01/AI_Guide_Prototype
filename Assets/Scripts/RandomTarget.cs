using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class RandomTarget : MonoBehaviour
{
    public NavMeshSurface kitchenNavMeshSurface; // Assign this in the Inspector
    public GameObject prefabToSpawn;

    // Start is called before the first frame update
    void Start()
    {
        SpawnInKitchen();
        InvokeRepeating("MoveRandom", 0f, 10f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SpawnInKitchen()
    {
        Vector3 randomPoint = GetRandomPointInNavMeshBounds(kitchenNavMeshSurface);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas))
        {
            Instantiate(prefabToSpawn, hit.position, Quaternion.identity);
        }
    }

    private void MoveRandom()
    {
        Vector3 newPosition = GetRandomPointInNavMeshBounds(kitchenNavMeshSurface);
        prefabToSpawn.transform.position = newPosition;
    }

    private Vector3 GetRandomPointInNavMeshBounds(NavMeshSurface surface)
    {
        Bounds bounds = surface.navMeshData.sourceBounds;
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        return new Vector3(x, bounds.center.y, z);
    }
}
