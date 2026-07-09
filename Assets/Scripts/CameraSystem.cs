using System.Collections;
using System.IO;
using UnityEngine;
using System;
using System.Collections.Generic;
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

    private Vector3 overheadLocalPos = new Vector3(0, 5.0f, 0); // 5 meters directly above the head
    private Vector3 overheadRotation = new Vector3(90, 0, 0);   // Looking straight down

    private Transform head;

    // Public camera variables for AIGuide script to access
    [HideInInspector] public Camera birdEyeCamera;
    [HideInInspector] public Camera viewpointCamera;
    [HideInInspector] public Camera overheadCamera;
    [HideInInspector] public Camera handCam;
    [HideInInspector] public Camera bodyCam;

    // Variables for monitoring
    private bool calledCamerasToStart = false;
    public bool converted = false;

    [Header("Instance Masking Settings")]
    public Camera maskCamera;
    public Material unlitMaskMaterialBase; // the material we'll alter to mask things
    private int maskLayer = 17;

    // Start is called before the first frame update
    void Start()
    {
        // Pulls the viewpointCamera automatically from the Main Camera under XR Origin
        viewpointCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        createBirdEyeCamera();
        createOverheadCamera();

        createHandCam();
        createBodyCam();

        createMaskCamera();

        // Grabs the user's headset transform from AI guide script to guide hand + body cam positioning
        head = viewpointCamera.transform;
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

        // Keep the overhead camera centered on the player, but pointing straight down
        if (overheadCamera != null)
        {
            // We only take the head position, not rotation, so the camera doesn't tilt when the player looks up/down
            overheadCamera.transform.position = head.position + overheadLocalPos;
            overheadCamera.transform.rotation = Quaternion.Euler(overheadRotation);
        }
    }

    private void createMaskCamera()
    {
        GameObject newCamera = new GameObject("Mask Camera");
        maskCamera = newCamera.AddComponent<Camera>();
        SetupCamera(maskCamera);

        // This camera ONLY sees the mask layer and renders a black background
        maskCamera.cullingMask = 1 << maskLayer;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = Color.black;
        maskCamera.enabled = false; // Only render when explicitly told to
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

    private void createOverheadCamera()
    {
        GameObject newCamera = new GameObject("Overhead Camera");
        overheadCamera = newCamera.AddComponent<Camera>();

        SetupCamera(overheadCamera);
        overheadCamera.fieldOfView = 80f; // Wide enough to see the immediate vicinity
    }

    private void createHandCam()
    {
        GameObject newCamera = new GameObject("Hand Camera");
        handCam = newCamera.AddComponent<Camera>();
        //handCam.transform.SetParent(head); // Parent to headset so it moves with the player

        // Camera needs to have specified height and angle to be pointed at user's hands at all times
        SetupCamera(handCam); // Point 30 degrees down and forward (not 90 straight down)

        // Don't enable at the start - only activate during StartGrabbing and deactivate on StopGrabbing
        handCam.enabled = false;
    }

    private void createBodyCam()
    {
        GameObject newCamera = new GameObject("Body Camera");
        bodyCam = newCamera.AddComponent<Camera>();
        //bodyCam.transform.SetParent(head);

        // Camera needs to have specified height and angle to be showing user's whole body from the side at all times/profile view to assist in grabbing
        SetupCamera(bodyCam); // Point 10 degrees down and towards the player

        // Don't enable at the start - only activate during StartGrabbing and deactivate on StopGrabbing
        bodyCam.enabled = false;
    }

    private void SetupCamera(Camera cam)
    {
        // DO NOT PARENT IT TO THE HEAD
        // Leave it completely unparented in the hierarchy so the XR rig can't mess with it
        cam.transform.SetParent(null);

        // Set Near Clip Plane very low so it doesn't clip hands
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 90f;
    }

    // Public method for SpatialPerceptionSensor to call
    public void CaptureObjectMask(GameObject targetObj, Color maskColor, Action<string> onComplete)
    {
        StartCoroutine(CaptureObjectMaskCoroutine(targetObj, maskColor, onComplete));
    }

    private IEnumerator CaptureObjectMaskCoroutine(GameObject targetObj, Color maskColor, Action<string> onComplete)
    {
        yield return new WaitForEndOfFrame();

        // Save original layer and materials
        int originalLayer = targetObj.layer;
        Renderer[] renderers = targetObj.GetComponentsInChildren<Renderer>();
        Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        // Apply unique color mask and move to isolation layer
        targetObj.layer = maskLayer;
        foreach (Renderer r in renderers)
        {
            originalMaterials[r] = r.materials;
            r.gameObject.layer = maskLayer;

            Material[] solidMats = new Material[r.materials.Length];
            for (int i = 0; i < solidMats.Length; i++)
            {
                solidMats[i] = new Material(unlitMaskMaterialBase);
                solidMats[i].color = maskColor;
            }
            r.materials = solidMats;
        }

        // Position mask camera (from player's perspective looking at the object)
        maskCamera.transform.position = viewpointCamera.transform.position;
        maskCamera.transform.LookAt(targetObj.transform);

        // Render to texture
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        maskCamera.targetTexture = renderTexture;
        maskCamera.Render();

        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        texture.Apply();

        // Convert to Base64
        byte[] bytes = texture.EncodeToJPG(75);
        string base64 = GetBase64FromBytes(bytes);

        // Cleanup Memory
        maskCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);
        Destroy(texture);

        // Restore original object state
        targetObj.layer = originalLayer;
        foreach (Renderer r in renderers)
        {
            r.materials = originalMaterials[r];
            r.gameObject.layer = originalLayer;
        }

        // Return the base64 string to the caller
        onComplete?.Invoke(base64);
    }

    public void CaptureScreenshot()
    {
        converted = false;
        StartCoroutine(CaptureScreenshotCoroutine(viewpointCamera));
        StartCoroutine(CaptureScreenshotCoroutine(birdEyeCamera));
        StartCoroutine(CaptureScreenshotCoroutine(overheadCamera));
    }

    public Coroutine CaptureHandScreenshots()
    {
        return StartCoroutine(CaptureHandAndBody());
    }

    private IEnumerator CaptureHandAndBody()
    {
        converted = false;
        yield return StartCoroutine(CaptureScreenshotCoroutine(handCam));
        yield return StartCoroutine(CaptureScreenshotCoroutine(bodyCam));
    }

    private IEnumerator CaptureScreenshotCoroutine(Camera camera)
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
        else if (camera == birdEyeCamera)
        {
            //Debug.Log("Converting birds eye screenshot to base 64");
            birdsEyeImageBase64 = GetBase64FromBytes(bytes);
        }
        else if (camera == overheadCamera)
        {
            overheadImageBase64 = GetBase64FromBytes(bytes);
        }
        else if (camera == handCam)
        {
            Debug.Log("Converting hand screenshot to base 64");
            handImageBase64 = GetBase64FromBytes(bytes);
        }
        else if (camera == bodyCam)
        {
            Debug.Log("Converting body screenshot to base 64");
            bodyImageBase64 = GetBase64FromBytes(bytes);
        }

        converted = true;
    }

    [HideInInspector] public string viewpointImageBase64;
    [HideInInspector] public string birdsEyeImageBase64;
    [HideInInspector] public string overheadImageBase64;
    [HideInInspector] public string handImageBase64;
    [HideInInspector] public string bodyImageBase64;

    private string GetBase64FromBytes(byte[] imageBytes)
    {
        string base64 = Convert.ToBase64String(imageBytes);
        return $"data:image/jpeg;base64,{base64}";
    }
}