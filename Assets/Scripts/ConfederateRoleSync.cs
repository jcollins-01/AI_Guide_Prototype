using Normal.Realtime;
using System.Collections;
using UnityEngine;

public class ConfederateRoleSync : RealtimeComponent<ConfederateRoleModel>
{
    private ChangeAvatarRuntime _changeAvatarRuntime;

    // Monitoring bools
    private bool changeAvatarFound = false;

    private void Awake()
    {
        _changeAvatarRuntime = GetComponentInChildren<ChangeAvatarRuntime>();
        if (_changeAvatarRuntime == null)
            Debug.LogError("ChangeAvatarRuntime component missing from this GameObject.");
    }

    void Update()
    {
        // Call until the component is found
        if (!changeAvatarFound)
            getChangeAvatarRuntime();
    }

    private void getChangeAvatarRuntime()
    {
        if (_changeAvatarRuntime == null)
            _changeAvatarRuntime = FindObjectOfType<ChangeAvatarRuntime>();
        else
            changeAvatarFound = true;
    }

    protected override void OnRealtimeModelReplaced(ConfederateRoleModel previousModel, ConfederateRoleModel currentModel)
    {
        if (previousModel != null)
            previousModel.roleDidChange -= RoleDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.role = _changeAvatarRuntime.GetConfederateCurrentRole();

            currentModel.roleDidChange += RoleDidChange;
        }
    }

    private void RoleDidChange(ConfederateRoleModel model, int value)
    {
        Debug.Log("Detected a confederate role change over the network, role is " + value);
        _changeAvatarRuntime.SetConfederateRole(value);
    }

    public void SetConfederateRole(int role)
    {
        //Debug.Log("Reached SetRole in GuideRoleSync, role is " + role);
        if (model != null)
            model.role = role;
        else
            Debug.LogError("Model is not initialized.");
    }
}
