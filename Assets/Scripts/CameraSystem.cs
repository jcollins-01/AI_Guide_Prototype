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
    private Vector3 birdRotation = new Vector3(65f, 0f, 0f);
    private float fieldOfView = 80f; // 60f is default

    // Public camera variables for AIGuide script to access
    public Camera birdEyeCamera;
    public Camera viewpointCamera;
    public string screenshotFileName = "birdEyeCapture.png";

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
                // Begin capturing screenshots every 10 secs to keep guide updated on scene
                // InvokeRepeating("CaptureScreenshot", 0f, 10f);
                // Begin capturing screenshots
                CaptureScreenshot(); // capture once from both cameras
                calledCamerasToStart = true;
            }
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

    public void CaptureScreenshot()
    {
        converted = false;
        StartCoroutine(CaptureScreenshotCoroutine(viewpointCamera, "view"));
        StartCoroutine(CaptureScreenshotCoroutine(birdEyeCamera, "bird"));
    }

    private IEnumerator CaptureScreenshotCoroutine(Camera camera, string cameraType)
    {
        yield return new WaitForEndOfFrame();

        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        camera.targetTexture = renderTexture;
        camera.Render();

        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        texture.Apply();

        byte[] bytes = texture.EncodeToJPG(75);
        // Don't need to save the images to the application for debugging anymore
        //string path = Path.Combine(Application.persistentDataPath, "VR_Capture.png");
        //File.WriteAllBytes(path, bytes);
        //Debug.Log("Screenshot saved to: " + path);

        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);
        Destroy(texture);

        // Convert images to base 64 string
        if (camera == viewpointCamera)
        {
            //Debug.Log("Converting viewpoint screenshot to base 64");
            viewpointImageBase64 = GetBase64FromBytes(bytes);
        }
            
        if (camera == birdEyeCamera)
        {
            //Debug.Log("Converting birds eye screenshot to base 64");
            birdsEyeImageBase64 = GetBase64FromBytes(bytes);
        }

        converted = true;
    }

    [HideInInspector] public string viewpointImageBase64;
    [HideInInspector] public string birdsEyeImageBase64;

    private string GetBase64FromBytes(byte[] imageBytes)
    {
        string base64 = Convert.ToBase64String(imageBytes);
        return $"data:image/jpeg;base64,{base64}";
    }
}