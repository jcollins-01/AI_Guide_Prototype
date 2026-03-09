using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Measures pointing/orientation performance before navigating to a random target.
/// When the right-controller trigger is pressed, this script:
/// - Finds the current active navigation target (the one with SelectedTarget).
/// - Casts a ray from the controller forward.
/// - Computes angular error (between ray and target direction).
/// - Computes distance error (between ray hit point and target position).
/// - Plays success / failure sounds based on whether the ray actually hits the target.
/// - Logs trial data to a CSV file.
/// </summary>
public class OrientationTask : MonoBehaviour
{
    [Header("References")]
    public ShortTaskController shortTaskController;
    public Transform rightControllerTransform;

    [Header("Audio Feedback")]
    public AudioClip successClip;
    public AudioClip failureClip;

    [Header("Raycast Settings")]
    public float maxRaycastDistance = 10f;
    public LayerMask raycastLayers = ~0;

    [Header("Logging")]
    public string csvFileName = "orientation_results_scene_xx.csv";

    [Header("Debug")]
    public bool showDebugPrimitive = false;

    private VRHandling _vrHandling;
    private InputDevice _rightController;
    private bool _triggerWasPressedLastFrame = false;
    private int _trialIndex = 0;
    private float _trialStartTime;

    private void Start()
    {
        _trialStartTime = Time.time;
        _vrHandling = FindObjectOfType<VRHandling>();
    }

    private void Update()
    {
        // Only run when the navigation task is active, if a controller is provided
        bool navigationTaskIsActive = shortTaskController != null && shortTaskController.navigationTaskActive;

        // Block teleportation while the navigation task is running so the trigger is reserved
        // for the orientation measurement step.
        TeleportationHandler.teleportationBlocked = navigationTaskIsActive;

        if (shortTaskController != null && !shortTaskController.navigationTaskActive)
        {
            return;
        }

        // Ensure we have access to VRHandling and its right controller
        if (_vrHandling == null)
        {
            _vrHandling = FindObjectOfType<VRHandling>();
            if (_vrHandling == null)
                return;
        }

        // Make sure VRHandling has grabbed the devices
        _vrHandling.getControllers();
        _rightController = _vrHandling.rightXRController;

        if (!_rightController.isValid)
        {
            return;
        }

        bool triggerPressed;
        if (_rightController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed))
        {
            // Detect rising edge of trigger press so we only log once per press
            if (triggerPressed && !_triggerWasPressedLastFrame)
            {
                HandleTriggerPress();
            }

            _triggerWasPressedLastFrame = triggerPressed;
        }
    }

    private bool TryGetRightControllerPose(out Vector3 position, out Quaternion rotation)
    {
        if (rightControllerTransform != null)
        {
            position = rightControllerTransform.position;
            rotation = rightControllerTransform.rotation;
            return true;
        }

        if (_rightController.isValid &&
            _rightController.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
            _rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
        {
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    private void HandleTriggerPress()
    {
        // Get right-controller pose first so we can always show the primitive when trigger is pressed
        Vector3 controllerPosition;
        Quaternion controllerRotation;
        if (!TryGetRightControllerPose(out controllerPosition, out controllerRotation))
        {
            return;
        }

        Vector3 controllerForward = controllerRotation * Vector3.forward;

        // Find the currently selected navigation target (the one with SelectedTarget)
        SelectedTarget activeTarget = FindObjectOfType<SelectedTarget>();
        GameObject targetObject = activeTarget != null ? activeTarget.gameObject : null;

        Vector3 targetPosition = targetObject != null ? targetObject.transform.position : controllerPosition + controllerForward * 2f;
        Vector3 toTarget = targetPosition - controllerPosition;
        float trueDistanceToTarget = toTarget.magnitude;

        // Horizontal (XZ-plane) angular error (only meaningful when we have a target)
        float horizontalAngularError = 0f;
        if (targetObject != null && trueDistanceToTarget > Mathf.Epsilon)
        {
            Vector3 controllerForwardHorizontal = new Vector3(controllerForward.x, 0f, controllerForward.z);
            Vector3 toTargetHorizontal = new Vector3(toTarget.x, 0f, toTarget.z);
            if (controllerForwardHorizontal.sqrMagnitude > Mathf.Epsilon &&
                toTargetHorizontal.sqrMagnitude > Mathf.Epsilon)
            {
                horizontalAngularError = Vector3.Angle(controllerForwardHorizontal.normalized, toTargetHorizontal.normalized);
            }
        }

        // Cast a ray to find where the player is actually pointing in space
        RaycastHit hit;
        bool hasHit = Physics.Raycast(controllerPosition, controllerForward, out hit, maxRaycastDistance, raycastLayers, QueryTriggerInteraction.Collide);

        if (showDebugPrimitive)
        {
            float rayLength = hasHit ? (hit.point - controllerPosition).magnitude : Mathf.Min(trueDistanceToTarget, maxRaycastDistance);
            if (rayLength < Mathf.Epsilon)
                rayLength = 2f;
            Vector3 cubePosition = hasHit
                ? hit.point
                : controllerPosition + controllerForward.normalized * rayLength;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = cubePosition;
            cube.transform.localScale = Vector3.one * 0.15f;
            Destroy(cube, 2f);
        }

        // If no active target, we're done after (optionally) showing the cube
        if (activeTarget == null || targetObject == null)
            return;
        if (trueDistanceToTarget <= Mathf.Epsilon)
            return;

        Vector3 d = controllerForward.normalized;
        Vector3 perpendicular = toTarget - Vector3.Dot(toTarget, d) * d;
        float distanceError = perpendicular.magnitude;

        bool hitTarget = hasHit &&
                         (hit.collider.gameObject == targetObject ||
                          hit.collider.transform.IsChildOf(targetObject.transform));
        PlayFeedback(hitTarget, controllerPosition);

        int targetIndex = -1;
        if (shortTaskController != null)
        {
            RandomTarget randomTarget = shortTaskController.GetComponent<RandomTarget>();
            if (randomTarget != null && randomTarget.randomTargets != null)
            {
                int listIndex = randomTarget.randomTargets.IndexOf(targetObject);
                targetIndex = (listIndex >= 0) ? (listIndex + 1) : -1;
            }
        }

        float timeSinceTrialStart = Time.time - _trialStartTime;
        _trialIndex++;

        LogToCsv(_trialIndex, targetIndex, timeSinceTrialStart, hitTarget, horizontalAngularError, distanceError);

        _trialStartTime = Time.time;
    }

    private void PlayFeedback(bool passed, Vector3 controllerPosition)
    {
        AudioClip clipToPlay = passed ? successClip : failureClip;
        if (clipToPlay != null)
        {
            // Play the clip at the controller position so feedback is spatially meaningful
            AudioSource.PlayClipAtPoint(clipToPlay, controllerPosition);
        }
    }

    private void LogToCsv(int trial, int targetNumber, float timeSeconds, bool hit, float horizontalAngularError, float distanceError)
    {
        string resultFolder = Path.Combine(Application.dataPath, "Orientation Task Result");
        if (!Directory.Exists(resultFolder))
            Directory.CreateDirectory(resultFolder);
        string path = Path.Combine(resultFolder, csvFileName);
        bool fileExists = File.Exists(path);

        try
        {
            using (var writer = new StreamWriter(path, append: true))
            {
                if (!fileExists)
                    writer.WriteLine("trial,target_number,time_seconds,hit,horizontal_angular_error_degrees,distance_error_3d_meters");

                writer.WriteLine(string.Format("{0},{1},{2:F3},{3},{4:F3},{5:F3}", trial, targetNumber, timeSeconds, hit ? 1 : 0, horizontalAngularError, distanceError));
            }
        }
        catch (IOException)
        {
        }
    }
}

