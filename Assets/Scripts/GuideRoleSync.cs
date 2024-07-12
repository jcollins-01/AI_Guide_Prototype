using Normal.Realtime;
using System.Collections;
using UnityEngine;

public class GuideRoleSync : RealtimeComponent<GuideRoleModel>
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

    protected override void OnRealtimeModelReplaced(GuideRoleModel previousModel, GuideRoleModel currentModel)
    {
        if (previousModel != null)
            previousModel.roleDidChange -= RoleDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.role = _changeAvatarRuntime.GetCurrentRole();

            currentModel.roleDidChange += RoleDidChange;
        }
    }

    private void RoleDidChange(GuideRoleModel model, int value)
    {
        //Debug.Log("Detected a role change over the network");
        _changeAvatarRuntime.SetRole(value);
    }

    public void SetRole(int role)
    {
        //Debug.Log("Reached SetRole in GuideRoleSync");
        if (model != null)
            model.role = role;
        else
            Debug.LogError("Model is not initialized.");
    }
}
