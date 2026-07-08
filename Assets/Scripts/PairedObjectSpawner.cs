using UnityEngine;
using System;

public class PrefabPairSpawner : MonoBehaviour
{
    [Serializable]
    public class PrefabPair
    {
        public GameObject prefabLeft;
        public GameObject prefabRight;
    }

    public enum PairSelection
    {
        None,
        PairOne,
        PairTwo,
        PairThree,
        PairFour
    }

    [Header("Settings")]
    public Transform xrOriginCamera; // Assign the Main Camera/Camera Offset
    public float spawnDistance = 2.0f;
    public float sideOffset = 0.5f;

    [Header("Prefab Pairs")]
    public PrefabPair[] prefabPairs = new PrefabPair[4];

    [Header("Active Selection")]
    [SerializeField]
    private PairSelection _currentPairType = PairSelection.None;

    private GameObject currentLeft;
    private GameObject currentRight;

    void Start()
    {
        // Initialize whatever state is currently selected in the inspector
        UpdateSpawnedPairs();
    }

    private void UpdateSpawnedPairs()
    {
        // Always clear existing objects first
        if (currentLeft != null) Destroy(currentLeft);
        if (currentRight != null) Destroy(currentRight);

        // If 'None' is selected, stop here (leaves the scene clear)
        if (_currentPairType == PairSelection.None) return;

        // Calculate array index. Enum value 'None' is 0, 'PairOne' is 1, so subtract 1.
        int index = (int)_currentPairType - 1;

        // Safety check to ensure we don't go out of bounds if the array size changes
        if (index < 0 || index >= prefabPairs.Length) return;

        var pair = prefabPairs[index];

        // Safety check to ensure prefabs are assigned
        if (pair.prefabLeft == null || pair.prefabRight == null)
        {
            Debug.LogWarning($"[PrefabPairSpawner] Missing prefabs for {_currentPairType}");
            return;
        }

        // Calculate spawn position
        Vector3 forward = xrOriginCamera.forward;
        forward.y = 0; // Keep objects level with the floor
        forward.Normalize();

        Vector3 spawnCenter = xrOriginCamera.position + forward * spawnDistance;

        // Spawn the paired objects
        currentLeft = Instantiate(pair.prefabLeft, spawnCenter - xrOriginCamera.right * sideOffset, Quaternion.identity);
        currentRight = Instantiate(pair.prefabRight, spawnCenter + xrOriginCamera.right * sideOffset, Quaternion.identity);
    }

#if UNITY_EDITOR
    // Safely catch Inspector dropdown changes during play mode
    private PairSelection _lastValidatedPairType;

    private void OnValidate()
    {
        // Only trigger the spawn logic if the game is actually running and the value has changed
        if (Application.isPlaying && _currentPairType != _lastValidatedPairType)
        {
            _lastValidatedPairType = _currentPairType;
            Debug.Log($"[PrefabPairSpawner] Switched to: {_currentPairType}");
            UpdateSpawnedPairs();
        }
    }
#endif
}