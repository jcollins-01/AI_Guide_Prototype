using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class CameraSystem : MonoBehaviour
{
    // Variables to set camera parameters
    private float birdHeight = 30f; // 15f for test scene
    private float birdZOffset = -10f; // Moves the camera back from directly over the player to get a better angle
    private float fieldOfView = 80f; // 60f is default
    private Vector3 birdRotation = new Vector3(65f, 0f, 0f);
    private Vector3 handLocalPos = new Vector3(0, 0.1f, 0.3f);
    private Vector3 handRotation = new Vector3(30, 0, 0);
    private Vector3 bodyLocalPos = new Vector3(1.0f, 0.5f, 0.5f);
    private Vector3 bodyRotation = new Vector3(10, -90, 0);
    private Transform head;

    // Public camera variables for AIGuide script to access
    public Camera birdEyeCamera;
    public Camera viewpointCamera;
    public Camera handCam;
    public Camera bodyCam;

    // Variables for monitoring
    private bool calledCamerasToStart = false;
    public bool converted = false;

    // Start is called before the first frame update
    void Start()
    {
        // Pulls the viewpointCamera automatically from the Main Camera under XR Origin
        viewpointCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        createBirdEyeCamera();
    }

    private void Update()
    {
        // If there is a guide in the scene, pull the bird eye camera from it, then begin sending screenshots
        if (GetComponent<SharedMovement>().theGuide != null && !calledCamerasToStart)
        {
            birdEyeCamera = GameObject.Find("Bird's Eye Camera").GetComponent<Camera>();

            if (!calledCamerasToStart)
            {
                // Capture once from viewpoint + bird's eye cameras so guide sees start of scene
                CaptureScreenshot();
                calledCamerasToStart = true;
            }
        }
    }

    // LateUpdate runs AFTER the XR Rig moves the camera - use this to move hand/body cams
    void LateUpdate()
    {
        if (head == null) return;

        if (handCam != null)
        {
            handCam.transform.position = head.position + (head.rotation * handLocalPos);
            handCam.transform.rotation = head.rotation * Quaternion.Euler(handRotation);
        }

        if (bodyCam != null)
        {
            bodyCam.transform.position = head.position + (head.rotation * bodyLocalPos);
            bodyCam.transform.rotation = head.rotation * Quaternion.Euler(bodyRotation);
        }
    }

    private void createBirdEyeCamera()
    {
        GameObject newCamera = new GameObject("Bird's Eye Camera");
        birdEyeCamera = newCamera.AddComponent<Camera>();

        // If we're in the test scene, alter the birdHeight variable to be closer since the scene isn't as big
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("Tutorial")) // was GuideTest_Networked
            birdHeight = 15f;

        // Camera has specified height it goes above the guide to get bird's eye view + rotation + widened field of view to look down at the scene
        birdHeight = birdHeight + transform.position.y;
        birdEyeCamera.transform.position = new Vector3(transform.position.x, birdHeight, transform.position.z + birdZOffset);
        birdEyeCamera.transform.eulerAngles = birdRotation;
        birdEyeCamera.fieldOfView = fieldOfView;
    }

    // Writes every capture to <project>/DebugCaptures so you can check what the vision model actually received
    public bool saveCapturesToDisk = true;

    // Plain (non-XR) camera that copies the head pose before each capture. Rendering the XR camera itself into a
    // RenderTexture is unreliable because the XR system drives its pose and projection.
    private Camera captureCam;

    public void CaptureScreenshot()
    {
        converted = false;

        if (viewpointCamera == null || !viewpointCamera.isActiveAndEnabled)
            viewpointCamera = Camera.main;

        if (captureCam == null)
        {
            captureCam = new GameObject("Guide Viewpoint Capture Camera").AddComponent<Camera>();
            captureCam.enabled = false; // only renders when we call Render()
        }
        captureCam.CopyFrom(viewpointCamera); // copies settings AND the current world position/rotation of the head
        captureCam.stereoTargetEye = StereoTargetEyeMask.None;
        captureCam.fieldOfView = fieldOfView; // a bit wider than the headset's mono FOV so the guide sees what's around

        // Keep the map centred on where the player is now instead of where the scene started
        Vector3 head = viewpointCamera.transform.position;
        birdEyeCamera.transform.position = new Vector3(head.x, birdHeight, head.z + birdZOffset);
        capturedHeadPos = head;
        capturedHeadForward = Vector3.ProjectOnPlane(viewpointCamera.transform.forward, Vector3.up).normalized;

        Debug.Log($"[Vision] Capturing from \"{viewpointCamera.name}\" at {head}, facing yaw {viewpointCamera.transform.eulerAngles.y:F0}°");

        StartCoroutine(CaptureScreenshotCoroutine(captureCam));
        StartCoroutine(CaptureScreenshotCoroutine(birdEyeCamera));
    }
    private IEnumerator CaptureScreenshotCoroutine(Camera camera)
    {
        yield return new WaitForEndOfFrame();

        // Fixed 4:3 size: independent of the Game window's shape (which gave thin 768x300 strips), and small enough
        // that the local vision model stays fast
        int width = 768;
        int height = 576;

        RenderTexture renderTexture = new RenderTexture(width, height, 24);
        camera.targetTexture = renderTexture;
        camera.ResetAspect(); // CopyFrom carries over the headset's aspect; use the render texture's

        // Roofs are hidden only for the map render, so the player's own view is unchanged
        List<Renderer> hidden = new List<Renderer>();
        if (camera == birdEyeCamera)
        {
            foreach (string name in hideFromBirdsEye)
            {
                GameObject roof = GameObject.Find(name);
                Renderer r = roof != null ? roof.GetComponent<Renderer>() : null;
                if (r != null && r.enabled) { r.enabled = false; hidden.Add(r); }
            }
        }

        camera.Render();
        foreach (Renderer r in hidden) r.enabled = true;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        if (camera == birdEyeCamera)
            DrawPlayerArrow(texture, camera);
        texture.Apply();

        byte[] bytes = texture.EncodeToJPG(75);

        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);
        Destroy(texture);

        // Convert images to base 64 string
        if (camera == captureCam)
        {
            //Debug.Log("Converting viewpoint screenshot to base 64");
            viewpointImageBase64 = GetBase64FromBytes(bytes);
            SaveCapture("viewpoint.jpg", bytes);
        }
        else if (camera == birdEyeCamera)
        {
            //Debug.Log("Converting birds eye screenshot to base 64");
            birdsEyeImageBase64 = GetBase64FromBytes(bytes);
            SaveCapture("birdseye.jpg", bytes);
        }

        converted = true;
    }

    [HideInInspector] public string viewpointImageBase64;
    [HideInInspector] public string birdsEyeImageBase64;

    [Tooltip("Objects whose own renderer is hidden while the bird's-eye map is rendered (roofs), so the map shows inside buildings.")]
    public List<string> hideFromBirdsEye = new List<string> { "Kitchen Ceiling", "Rosie's Kitchen" };

    private Vector3 capturedHeadPos;
    private Vector3 capturedHeadForward;

    // Red dot at the player's position with a line pointing the way they face, so the model can read the map relative to them
    private void DrawPlayerArrow(Texture2D texture, Camera cam)
    {
        Vector3 p = cam.WorldToViewportPoint(capturedHeadPos);
        Vector3 tip = cam.WorldToViewportPoint(capturedHeadPos + capturedHeadForward * 3f);
        if (p.z <= 0) return; // behind the camera

        Vector2 center = new Vector2(p.x * texture.width, p.y * texture.height);
        Vector2 dir = new Vector2(tip.x * texture.width, tip.y * texture.height) - center;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.up;

        for (int x = -10; x <= 10; x++)
            for (int y = -10; y <= 10; y++)
                if (x * x + y * y <= 100) SetPixelSafe(texture, (int)center.x + x, (int)center.y + y);

        for (int i = 0; i < 40; i++)
        {
            Vector2 q = center + dir * i;
            for (int t = -2; t <= 2; t++)
                SetPixelSafe(texture, (int)(q.x - dir.y * t), (int)(q.y + dir.x * t));
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y)
    {
        if (x >= 0 && y >= 0 && x < texture.width && y < texture.height) texture.SetPixel(x, y, Color.red);
    }

    private void SaveCapture(string fileName, byte[] bytes)
    {
        if (!saveCapturesToDisk) return;

        // Next to Assets/ (not inside it) so Unity doesn't import the images
        string dir = Path.Combine(Application.dataPath, "..", "DebugCaptures");
        Directory.CreateDirectory(dir);
        string path = Path.GetFullPath(Path.Combine(dir, fileName));
        File.WriteAllBytes(path, bytes);
        Debug.Log($"[Vision] Saved {fileName} -> {path}");
    }

    private string GetBase64FromBytes(byte[] imageBytes)
    {
        string base64 = Convert.ToBase64String(imageBytes);
        return $"data:image/jpeg;base64,{base64}";
    }
}