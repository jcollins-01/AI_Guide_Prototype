using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
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
    private bool refreshed = false;
    private bool calledCamerasToStart = false;

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
                //InvokeRepeating("CaptureScreenshot", 0f, 10f);
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
        if (currentSceneName.Equals("GuideTest_Networked"))
            birdHeight = 15f;

        // Camera has specified height it goes above the guide to get bird's eye view + rotation + widened field of view to look down at the scene
        birdHeight = birdHeight + transform.position.y;
        birdEyeCamera.transform.position = new Vector3(transform.position.x, birdHeight, transform.position.z + birdZOffset);
        birdEyeCamera.transform.eulerAngles = birdRotation;
        birdEyeCamera.fieldOfView = fieldOfView;
    }

    private void CaptureScreenshot()
    {
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

        byte[] bytes = texture.EncodeToPNG();
        string path = Path.Combine(Application.persistentDataPath, "VR_Capture.png");
        File.WriteAllBytes(path, bytes);

        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);
        Destroy(texture);

        Debug.Log("Screenshot saved to: " + path);

        // Upload the image
        StartCoroutine(UploadImage(bytes, cameraType));
    }

    private string imageApiKey = "6EHKLMNTd1353fef85ed809f9acb93b2e33f0ead";

    [HideInInspector]
    public string viewpointImageLink;
    [HideInInspector]
    public string birdsEyeImageLink;

    IEnumerator UploadImage(byte[] imageData, string type)
    {
        WWWForm form = new WWWForm();
        form.AddField("key", imageApiKey);
        form.AddBinaryData("fileupload", imageData, "image.png", "image/png");

        using (UnityWebRequest www = UnityWebRequest.Post("https://post.imageshack.us/upload_api.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                if (type == "view")
                {
                    viewpointImageLink = ParseXmlResponse(responseText);
                }
                else
                {
                    birdsEyeImageLink = ParseXmlResponse(responseText);
                }
            }
            else
            {
                Debug.LogError("Upload failed: " + www.error);
            }
        }
    }

    string ParseXmlResponse(string xmlResponse)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlResponse);

        XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("ns", "http://ns.imageshack.us/imginfo/8/");

        string imageLink = "";
        XmlNode imageLinkNode = xmlDoc.SelectSingleNode("//ns:links/ns:image_link", nsManager);

        if (imageLinkNode != null)
        {
            imageLink = imageLinkNode.InnerText;
        }
        else
        {
            Debug.LogError("image_link not found in the XML.");
        }

        return imageLink;
    }
}

/*using System.Collections;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;

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
    private bool refreshed = false;
    private bool calledCamerasToStart = false;

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
                //InvokeRepeating("CaptureScreenshot", 0f, 10f);
                CaptureScreenshot(); // capture once from both cameras
                //InvokeRepeating("CaptureWrapper", 0f, 10f);
                //destroyBirdEyeCamera();
                calledCamerasToStart = true;
            }
        }
    }
    
    private void destroyBirdEyeCamera()
    {
        Destroy(birdEyeCamera.gameObject);
    }

    private void createBirdEyeCamera()
    {
        GameObject newCamera = new GameObject("Bird's Eye Camera");

        birdEyeCamera = newCamera.AddComponent<Camera>();

        // If we're in the test scene, alter the birdHeight variable to be closer since the scene isn't as big
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentSceneName.Equals("GuideTest_Networked"))
            birdHeight = 15f;

        // Camera has specified height it goes above the guide to get bird's eye view + rotation + widened field of view to look down at the scene
        birdHeight = birdHeight + transform.position.y;
        birdEyeCamera.transform.position = new Vector3(transform.position.x, birdHeight, transform.position.z + birdZOffset);
        birdEyeCamera.transform.eulerAngles = birdRotation;
        birdEyeCamera.fieldOfView = fieldOfView;
    }

    private void CaptureWrapper()
    {
        CaptureSpecificCamera(viewpointCamera, "/Resources/Screenshots/viewpointCapture.png");

        refreshed = false;
        RefreshAssets();
    }

    private void CaptureScreenshot()
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
            UploadImage();
        }
    }

    IEnumerator WaitForRefresh()
    {
        yield return new WaitForSeconds(2);
        RefreshAssets();
        refreshed = true;
    }

    void UploadImage()
    {
        // Debug.Log("Uploading screenshot to Image Shack");
        // Loads the screenshots (Unity considers it a texture) from Resources
        Texture2D viewCapturedScreenshot = Resources.Load<Texture2D>("Screenshots/viewpointCapture");
        Texture2D birdCapturedScreenshot = Resources.Load<Texture2D>("Screenshots/birdEyeCapture");
        // Decompresses the textures to work with encoding, encodes textures to byte arrays in PNG format, then converts those arrays to base64 strings
        Texture2D viewPreppedScreenshot = viewCapturedScreenshot.DeCompress();
        Texture2D birdPreppedScreenshot = birdCapturedScreenshot.DeCompress();

        // Takes the byte arrays of the imageData and passes it to IMGUR for upload
        byte[] viewImageData = ImageConversion.EncodeToPNG(viewPreppedScreenshot);
        byte[] birdImageData = ImageConversion.EncodeToPNG(birdPreppedScreenshot);
        string viewType = "view";
        string birdType = "bird";
        StartCoroutine(UploadImage(viewImageData, viewType));
        StartCoroutine(UploadImage(birdImageData, birdType));
    }

    // Image Shack API Key, requested from "https://imageshack.com/contact/api", website link is: https://oauth.pstmn.io/v1/callback
    // For resetting Image Shack account, go to Settings > Basic > Manage Exceptions > find/add imageshack.com > Delete Data
    private string imageApiKey = "6EHKLMNTd1353fef85ed809f9acb93b2e33f0ead";

    [HideInInspector]
    public string viewpointImageLink;
    [HideInInspector]
    public string birdsEyeImageLink;

    IEnumerator UploadImage(byte[] imageData, string type)
    {
        // Set up form data
        WWWForm form = new WWWForm();
        form.AddField("key", imageApiKey);
        form.AddBinaryData("fileupload", imageData, "image.png", "image/png"); // was "image.jpg", "image/jpeg"

        // Send the POST request
        using (UnityWebRequest www = UnityWebRequest.Post("https://post.imageshack.us/upload_api.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // Parse the response
                string responseText = www.downloadHandler.text;
                // Debug.Log("Upload successful!");
                // Debug.Log("Response: " + responseText);
                if (type == "view")
                {
                    viewpointImageLink = ParseXmlResponse(responseText);
                    //Debug.Log("view_image_link: " + viewpointImageLink);
                }
                else
                {
                    birdsEyeImageLink = ParseXmlResponse(responseText);
                    //Debug.Log("bird_image_link: " + birdsEyeImageLink);
                }
            }
            else
            {
                Debug.LogError("Upload failed: " + www.error);
            }
        }
    }

    // Parse XML response and extract xmlns data
    string ParseXmlResponse(string xmlResponse)
    {
        // Create XML document and load the XML string
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlResponse);

        // Create an XmlNamespaceManager for resolving namespaces
        XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("ns", "http://ns.imageshack.us/imginfo/8/");

        // Placeholder for link to return
        string imageLink = "";

        // Get the image_link element using the namespace manager
        XmlNode imageLinkNode = xmlDoc.SelectSingleNode("//ns:links/ns:image_link", nsManager);

        // Check if imageLinkNode is not null
        if (imageLinkNode != null)
        {
            // Get the value of the image_link element
            imageLink = imageLinkNode.InnerText;
        }
        else
            Debug.LogError("image_link not found in the XML.");

        return imageLink;
    }
}
*/