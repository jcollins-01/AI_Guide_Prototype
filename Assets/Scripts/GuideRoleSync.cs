using Normal.Realtime;
using System.Collections;
using UnityEngine;

public class GuideRoleSync : RealtimeComponent<GuideRoleModel>
{
    private ChangeAvatarRuntime _changeAvatarRuntime;

    private void Awake()
    {
        _changeAvatarRuntime = GetComponent<ChangeAvatarRuntime>();
        if (_changeAvatarRuntime == null)
            Debug.LogError("ChangeAvatarRuntime component missing from this GameObject.");
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
        Debug.Log("Detected a role change over the network");
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
