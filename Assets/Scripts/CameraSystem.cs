using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CameraSystem : MonoBehaviour
{
    // Variables to set camera parameters
    //private int captureWidth = 1920;
    //private int captureHeight = 1080;
    private float birdHeight = 15f;
    private Vector3 birdRotation = new Vector3(65f, 0f, 0f);

    // Public camera variables for AIGuide script to access
    public Camera birdEyeCamera;
    public Camera viewpointCamera;
    public string screenshotFileName = "birdEyeCapture.png";

    // Variables for monitoring
    private bool refreshed = false;

    // Start is called before the first frame update
    void Start()
    {
        // Pulls the viewpointCamera automatically from the Main Camera under XR Origin
        viewpointCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

        // Creates a Game Object to hold a new camera for a bird's eye view
        GameObject newCamera = new GameObject("Bird's Eye Camera");
        birdEyeCamera = newCamera.AddComponent<Camera>();

        // Camera has specified height it goes above the player to get bird's eye view + rotation to look down at the scene
        birdHeight = birdHeight + transform.position.y;
        birdEyeCamera.transform.position = new Vector3(transform.position.x, birdHeight, transform.position.z);
        birdEyeCamera.transform.eulerAngles = birdRotation;

        // Begin capturing screenshots every 10 secs to keep guide updated on scene
        //InvokeRepeating("CaptureScreenshot", 0f, 10f);
        CaptureScreenshot();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CaptureScreenshot()
    {
        // Captures screenshots from both cameras in system
        CaptureSpecificCamera(viewpointCamera, "/Resources/Screenshots/viewpointCapture.png");
        CaptureSpecificCamera(birdEyeCamera, "/Resources/Screenshots/birdEyeCapture.png");

        //Debug.Log("Screenshots captured!");
        refreshed = false;
        RefreshAssets();
    }

    void CaptureSpecificCamera(Camera camera, string path)
    {
        // Creates a RenderTexture so we can render the Camera's output
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        camera.targetTexture = renderTexture;
        camera.Render();

        // Creates a new Texture2D and reads the RenderTexture into it
        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        texture.Apply();

        // Saves the texture as an image and cleans up
        byte[] bytes = texture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + path, bytes);
        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);
        Destroy(texture);
    }

    void RefreshAssets()
    {
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh(); // This function is only available in the editor; can't be used in builds to the headset
#endif
        if (!refreshed) // Run this coroutine to make sure we refresh before moving on
            StartCoroutine(WaitForRefresh());
        else
        {
            //Debug.Log("Assets refreshed!");
            string viewpointPath = Application.dataPath + "/Resources/Screenshots/viewpointCapture.png";
            string birdEyePath = Application.dataPath + "/Resources/Screenshots/birdEyeCapture.png";

            ImageUploader image = gameObject.AddComponent<ImageUploader>();
            image.UploadImage(viewpointPath);
        }
    }

    IEnumerator WaitForRefresh()
    {
        yield return new WaitForSeconds(2);
        RefreshAssets();
        refreshed = true;
    }
}

public class ImageUploader : MonoBehaviour
{
    //https://postimages.org/json/rr
    public string uploadUrl = "https://postimg.cc/json";

    public void UploadImage(string imageFilePath)
    {
        StartCoroutine(UploadImageCoroutine(imageFilePath));
    }

    IEnumerator UploadImageCoroutine(string imageFilePath)
    {
        if (string.IsNullOrEmpty(imageFilePath))
        {
            Debug.LogError("Image file path is not specified.");
            yield break;
        }

        // Load the image bytes
        byte[] imageBytes = System.IO.File.ReadAllBytes(imageFilePath);

        // Create a UnityWebRequest to upload the image
        using (UnityWebRequest www = new UnityWebRequest(uploadUrl, "POST"))
        {
            // Set the content type to "multipart/form-data"
            www.SetRequestHeader("Content-Type", "multipart/form-data");

            // Create a multipart form data
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", imageBytes, "image.png", "image/png");

            // Set the form data to the UnityWebRequest
            www.uploadHandler = new UploadHandlerRaw(form.data);
            www.uploadHandler.contentType = "multipart/form-data";

            // Send the request
            yield return www.SendWebRequest();

            // Check for errors
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogError("Error uploading image: " + www.error);
            }
            else
            {
                // Image uploaded successfully
                Debug.Log("Image uploaded successfully. Response: " + www.downloadHandler.text);
            }
        }
    }
}
