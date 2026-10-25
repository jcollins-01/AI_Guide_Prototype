using System.Collections;
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

        Debug.Log($"[Vision] Capturing from \"{viewpointCamera.name}\" at {head}, facing yaw {viewpointCamera.transform.eulerAngles.y:F0}°");

        StartCoroutine(CaptureScreenshotCoroutine(captureCam));
        StartCoroutine(CaptureScreenshotCoroutine(birdEyeCamera));
    }
    private IEnumerator CaptureScreenshotCoroutine(Camera camera)
    {
        yield return new WaitForEndOfFrame();

        // Capped at 768px wide: the local vision model's time grows with image size and full-screen shots gain it nothing
        int width = Mathf.Min(Screen.width, 768);
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * (width / (float)Screen.width)));

        RenderTexture renderTexture = new RenderTexture(width, height, 24);
        camera.targetTexture = renderTexture;
        camera.Render();

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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