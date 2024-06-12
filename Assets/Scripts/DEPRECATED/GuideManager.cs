using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuideManager : MonoBehaviour
{
    public GameObject guidePrefab;

    private Realtime _realtime;
    private RealtimeTransform _guideRealtimeTransform;
    private GameObject guideInstance;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InstantiateGuide()
    {
        // Find the Realtime component
        _realtime = FindObjectOfType<Realtime>();

        if (_realtime == null)
        {
            Debug.LogError("Realtime component not found in the scene.");
            return;
        }

        // Ensure the guide prefab is registered
        if (guidePrefab == null)
        {
            Debug.LogError("Guide prefab is not assigned.");
            return;
        }

        // Instantiate the guide avatar as a networked object
        guideInstance = Realtime.Instantiate(guidePrefab.name, Realtime.InstantiateOptions.defaults);

        _guideRealtimeTransform = guideInstance.GetComponent<RealtimeTransform>();

        // Request ownership of the guide avatar
        /*if (_guideRealtimeTransform != null && !_guideRealtimeTransform.isOwnedLocallyInHierarchy)
        {
            _guideRealtimeTransform.RequestOwnership();
        }*/
    }
}
