using UnityEngine;
using System;

public class TableDropSpawner : MonoBehaviour
{
    public enum ItemSelection
    {
        None,
        ItemOne,
        ItemTwo,
        ItemThree,
        ItemFour
    }

    [Header("Settings")]
    public Transform xrOriginCamera;
    public float tableSpawnDistance = 1.5f;
    public float dropHeightOffset = 0.5f;
    public float edgeMargin = 0.1f;

    [Tooltip("How high the invisible walls around the table should be.")]
    public float bumperHeight = 1.0f;

    [Header("Prefabs")]
    public GameObject tablePrefab;
    public GameObject[] itemPrefabs = new GameObject[4];

    [Header("Active Selection")]
    [SerializeField]
    private ItemSelection _currentItemType = ItemSelection.None;

    private GameObject currentTable;
    private GameObject currentItem;

    // Sensor we need to access for clean-up
    SpatialPerceptionSensor perceptionSensor;

    void Start()
    {
        // Initialize whatever state is currently selected in the inspector
        UpdateSpawnedObjects();

        // Get the perception sensor
        perceptionSensor = FindObjectOfType<SpatialPerceptionSensor>();
    }

    private void UpdateSpawnedObjects()
    {
        // If 'None' is selected, destroy the item and the table, then stop
        if (_currentItemType == ItemSelection.None)
        {
            if (currentItem != null) Destroy(currentItem);
            if (currentTable != null) Destroy(currentTable);

            return;
        }

        // Spawn the table if it doesn't exist yet
        if (currentTable == null)
        {
            if (tablePrefab == null)
            {
                Debug.LogWarning("[TableDropSpawner] Table prefab is missing!");
                return;
            }

            Vector3 forward = xrOriginCamera.forward;
            forward.y = 0; // Keep flat
            forward.Normalize();

            // Position table in front of the player, matching the player's floor height
            Vector3 tableSpawnPos = xrOriginCamera.position + (forward * tableSpawnDistance);
            tableSpawnPos.y = xrOriginCamera.position.y;

            // Make the table face the player
            Quaternion tableRotation = Quaternion.LookRotation(-forward);

            currentTable = Instantiate(tablePrefab, tableSpawnPos, tableRotation);

            // Generate invisible walls to catch falling items
            BoxCollider tableCollider = currentTable.GetComponentInChildren<BoxCollider>();
            if (tableCollider != null)
            {
                CreateInvisibleBumpers(tableCollider);
            }
        }

        // Clear the previous item
        if (currentItem != null) Destroy(currentItem);

        // Validate the selected item prefab
        int index = (int)_currentItemType - 1;
        if (index < 0 || index >= itemPrefabs.Length || itemPrefabs[index] == null)
        {
            Debug.LogWarning($"[TableDropSpawner] Missing item prefab for {_currentItemType}");
            return;
        }

        // Calculate a random spawn point on the table's surface
        BoxCollider activeCollider = currentTable.GetComponentInChildren<BoxCollider>();
        if (activeCollider == null)
        {
            Debug.LogError("[TableDropSpawner] The table prefab needs a BoxCollider to calculate the spawn area.");
            return;
        }

        Bounds bounds = activeCollider.bounds;

        // Pick a random X and Z within the table's bounds (minus the safety margin)
        float randomX = UnityEngine.Random.Range(bounds.min.x + edgeMargin, bounds.max.x - edgeMargin);
        float randomZ = UnityEngine.Random.Range(bounds.min.z + edgeMargin, bounds.max.z - edgeMargin);

        // Spawn height is the top of the table's bounding box + the drop height offset
        Vector3 dropPosition = new Vector3(randomX, bounds.max.y + dropHeightOffset, randomZ);

        // Spawn the new item
        currentItem = Instantiate(itemPrefabs[index], dropPosition, UnityEngine.Random.rotation); // Spawns with random rotation so it tumbles slightly
    }

    private void CreateInvisibleBumpers(BoxCollider tableCollider)
    {
        float wallThickness = 0.1f;
        Vector3 size = tableCollider.size;
        Vector3 center = tableCollider.center;

        // Create a parent object for the walls to keep the hierarchy clean
        GameObject bumpersContainer = new GameObject("InvisibleBumpers");
        bumpersContainer.transform.SetParent(tableCollider.transform, false);

        // Front Wall
        CreateWall(bumpersContainer.transform,
            new Vector3(center.x, center.y + bumperHeight / 2, center.z + size.z / 2 + wallThickness / 2),
            new Vector3(size.x + wallThickness * 2, bumperHeight, wallThickness));

        // Back Wall
        CreateWall(bumpersContainer.transform,
            new Vector3(center.x, center.y + bumperHeight / 2, center.z - size.z / 2 - wallThickness / 2),
            new Vector3(size.x + wallThickness * 2, bumperHeight, wallThickness));

        // Right Wall
        CreateWall(bumpersContainer.transform,
            new Vector3(center.x + size.x / 2 + wallThickness / 2, center.y + bumperHeight / 2, center.z),
            new Vector3(wallThickness, bumperHeight, size.z));

        // Left Wall
        CreateWall(bumpersContainer.transform,
            new Vector3(center.x - size.x / 2 - wallThickness / 2, center.y + bumperHeight / 2, center.z),
            new Vector3(wallThickness, bumperHeight, size.z));
    }

    private void CreateWall(Transform parent, Vector3 localPos, Vector3 size)
    {
        GameObject wall = new GameObject("BumperWall");
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPos;

        // Layer 2 is Unity's built-in "Ignore Raycast" layer - ensures the invisible walls don't block VR pointers or teleport rays
        wall.layer = 2;

        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = size;
        // No MeshRenderer is added, so it remains completely invisible
    }

#if UNITY_EDITOR
    // Safely catch Inspector dropdown changes during play mode
    private ItemSelection _lastValidatedItemType;

    private void OnValidate()
    {
        if (Application.isPlaying && _currentItemType != _lastValidatedItemType)
        {
            _lastValidatedItemType = _currentItemType;
            Debug.Log($"[TableDropSpawner] Switched to: {_currentItemType}");
            UpdateSpawnedObjects();
        }
    }
#endif
}