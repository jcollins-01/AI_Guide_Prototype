using Normal.Realtime;
using UnityEngine;

public class RequestOwnership : MonoBehaviour
{
    private RealtimeTransform _realtimeTransform;

    void Start()
    {
        _realtimeTransform = GetComponent<RealtimeTransform>();
    }

    private void Update()
    {
        // Request ownership of the RealtimeTransform component
        if (_realtimeTransform != null && !_realtimeTransform.isOwnedLocallyInHierarchy)
        {
            _realtimeTransform.RequestOwnership();
        }
    }
}