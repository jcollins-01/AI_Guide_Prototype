using Normal.Realtime;
using UnityEngine;

public class ConfederateHandlerSync : RealtimeComponent<ConfederateHandlerModel>
{
    private ConfederateHandler _confederateHandler;

    // Monitoring bools
    private bool confederateHandlerFound = false;

    private void Awake()
    {
        _confederateHandler = GetComponent<ConfederateHandler>();
        if (_confederateHandler == null)
            Debug.LogError("ConfederateHandler component missing from this GameObject.");
    }

    void Update()
    {
        // Call until the component is found
        if (!confederateHandlerFound)
            getConfederateHandler();
    }

    private void getConfederateHandler()
    {
        if (_confederateHandler == null)
            _confederateHandler = FindObjectOfType<ConfederateHandler>();
        else
            confederateHandlerFound = true;
    }

    protected override void OnRealtimeModelReplaced(ConfederateHandlerModel previousModel, ConfederateHandlerModel currentModel)
    {
        if (previousModel != null)
            previousModel.confederateVersionDidChange -= ConfederateVersionDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.confederateVersion = _confederateHandler.confederateVersion;

            currentModel.confederateVersionDidChange += ConfederateVersionDidChange;
        }
    }

    private void ConfederateVersionDidChange(ConfederateHandlerModel model, bool value)
    {
        Debug.Log("Detected a confederate version change " + value);
        _confederateHandler.confederateVersion = value;
    }

    public void SetConfederateVersion(bool value)
    {
        if (model != null)
            model.confederateVersion = value;
        else
            Debug.LogError("Model is not initialized.");
    }
}