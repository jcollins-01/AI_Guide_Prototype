using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;

// The player's head camera. Camera.main only finds cameras tagged "MainCamera", and the rig in the XR Accessibility
// Set-Up prefab isn't tagged, so in the headset Camera.main is null. Falls back to the camera of the player's XR Origin
// (the one named "Player", not the guide's rig).
public static class PlayerHead
{
    private static Camera cached;

    public static Camera Camera
    {
        get
        {
            if (cached != null && cached.isActiveAndEnabled) return cached;
            cached = Camera.main;
            if (cached != null) return cached;

            XROrigin origin = Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None)
                .Where(o => o.isActiveAndEnabled && o.Camera != null && o.Camera.isActiveAndEnabled)
                .OrderByDescending(o => o.name.Contains("Player"))
                .FirstOrDefault();
            cached = origin != null ? origin.Camera : null;
            return cached;
        }
    }
}
