using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;

public class QueryDescription : MonoBehaviour
{
    public bool VRMode = false;

    // Start is called before the first frame update
    void Start()
    {
        //Debug.Log("Ready to query for CV description - press space or right primary button to capture and upload an image for querying!");
    }

    // Update is called once per frame
    void Update()
    {
        /*
        // Logic to pull and assign the XR controllers
        if (VRMode)
            getControllers();

        // Capture screenshot and process it through Astica
        if (Input.GetKeyDown("space") || (rightXRController.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButtonValue) && primaryButtonValue))
        {
            CaptureScreenshot();
        }
        */
    }

    public void QueryLeft()
    {
        // Rotates the game object this script is attached to to the left
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y - 90f, transform.eulerAngles.z);
        CaptureScreenshot();
    }

    public IEnumerator QueryEast()
    {
        // Rotates the game object to the left according to the world origin (east)
        transform.rotation = Quaternion.Euler(0, -90, 0);
        yield return new WaitForEndOfFrame();
        CaptureScreenshot();
        while (!uploaded)
        {
            yield return null; // Wait for the next frame
        }
    }

    public void QueryRight()
    {
        // Rotates the game object this script is attached to to the right
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y + 90f, transform.eulerAngles.z);
        // transform.rotation = Quaternion.Euler(0, 90, 0); - this version would set a TRUE RIGHT, and always turn to the initial right direction (west)
        CaptureScreenshot();
    }

    public IEnumerator QueryWest()
    {
        // Rotates the game object to the right according to the world origin (west)
        transform.rotation = Quaternion.Euler(0, 90, 0);
        yield return new WaitForEndOfFrame();
        CaptureScreenshot();
        while (!uploaded)
        {
            yield return null; // Wait for the next frame
        }
    }

    public void QueryFront()
    {
        // Rotates the game object this script is attached to the front
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z);
        CaptureScreenshot();
    }

    public IEnumerator QueryNorth()
    {
        // Rotates the game object to the front according to the world origin (north)
        transform.rotation = Quaternion.Euler(0, 0, 0);
        yield return new WaitForEndOfFrame();
        CaptureScreenshot();
        while (!uploaded)
        {
            yield return null; // Wait for the next frame
        }
    }

    public void QueryBehind()
    {
        // Rotates the game object this script is attached to the back 
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y + 180f, transform.eulerAngles.z);
        CaptureScreenshot();
    }

    public IEnumerator QuerySouth()
    {
        // Rotates the game object to the back according to the world origin (south)
        transform.rotation = Quaternion.Euler(0, 180, 0);
        yield return new WaitForEndOfFrame();
        CaptureScreenshot();
    }

    public IEnumerator QueryScene()
    {
        // Rotates the game object in all directions and takes pictures + describes each (uses cardinal, instead of relate directions)
        yield return StartCoroutine(QueryEast());
        yield return StartCoroutine(QueryNorth());
        yield return StartCoroutine(QueryWest());
        yield return StartCoroutine(QuerySouth());
    }

    // Keep track of asset refresh
    private bool refreshed = false;
    private bool uploaded = false;

    public void CaptureScreenshot()
    {
        ScreenCapture.CaptureScreenshot(Application.dataPath + "/Resources/Screenshots/capture.png");
        Debug.Log("Screenshot captured!");
        refreshed = false;
        uploaded = false;
        RefreshAssets();
    }

    void RefreshAssets()
    {
        UnityEditor.AssetDatabase.Refresh();

        if (!refreshed) // Run this coroutine to make sure we refresh before moving on
            StartCoroutine(WaitForRefresh());
        else
        {
            Debug.Log("Assets refreshed!");
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
        Debug.Log("Uploading screenshot to Image Shack");
        // Loads the screenshot (Unity considers it a texture) from Resources
        Texture2D capturedScreenshot = Resources.Load<Texture2D>("Screenshots/capture");
        // Decompresses the screenshot texture to work with encoding, encodes texture to a byte array in PNG format, then converts that array to a base64 string
        Texture2D preppedScreenshot = capturedScreenshot.DeCompress();
        string imageString = System.Convert.ToBase64String(ImageConversion.EncodeToPNG(preppedScreenshot));

        // Takes the byte array of the imageData and passed it to IMGUR for upload
        byte[] imageData = ImageConversion.EncodeToPNG(preppedScreenshot);
        StartCoroutine(UploadImage(imageData));
    }

    // Image Shack API Key, requested from "https://imageshack.com/contact/api"
    private string imageApiKey = "468CGIVYeba088be6297f37babc219efe571c8bd";
    private string imageShackLink;

    IEnumerator UploadImage(byte[] imageData)
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
                Debug.Log("Upload successful!");
                Debug.Log("Response: " + responseText);
                string imageLink = ParseXmlResponse(responseText);
                Debug.Log("image_link: " + imageLink);
                imageShackLink = imageLink;
                uploaded = true;

                // Query Astica with uploaded image
                Debug.Log("Sent request to Query Astica");
                //QueryAstica();
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
        {
            Debug.LogError("image_link not found in the XML.");
        }

        return imageLink;
    }

    async Task QueryAstica() // was a static method
    {
        Debug.Log("Querying Astica AI");

        string asticaAPI_key = "762A2AC4-411B-43B5-A4DC-49D4602B87C3CCFF077C-9401-4AE7-9EDA-8E828D671526"; // visit https://astica.org
        string asticaAPI_timeout = "35"; // seconds

        string asticaAPI_endpoint = "https://vision.astica.ai/describe";
        string asticaAPI_modelVersion = "2.1_full"; // '1.0_full', '2.0_full', or (was) '2.1_full'

        string asticaAPI_input = imageShackLink; // Sample tests: "https://astica.ai/example/asticaVision_sample.jpg", "https://usapple.org/wp-content/uploads/2019/10/apple-pink-lady.png", "https://i.postimg.cc/VLp2sVMn/test.png", imageString (FAIL), "https://live.staticflickr.com/65535/53542353338_14b2062afc_h.jpg"
        string asticaAPI_visionParams = "description"; // comma separated options; leave blank for all; note "gpt" and "gpt_detailed" are slow. // Original: "gpt,description,objects,faces";

        Dictionary<string, string> asticaAPI_payload = new Dictionary<string, string>
        {
            { "tkn", asticaAPI_key },
            { "modelVersion", asticaAPI_modelVersion },
            { "visionParams", asticaAPI_visionParams },
            { "input", asticaAPI_input }
        };

        var asticaAPI_result = await AsticaAPI(asticaAPI_endpoint, asticaAPI_payload, asticaAPI_timeout);

        Debug.Log("\nastica API Output:");
        Debug.Log(JsonConvert.SerializeObject(asticaAPI_result, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("=================");
        Debug.Log("=================");
        Debug.Log(asticaAPI_result["caption"]);

        if (asticaAPI_result.ContainsKey("status"))
        {
            if (asticaAPI_result["status"].ToString() == "error") // Cast to string
            {
                Debug.Log("Output:\n" + asticaAPI_result["error"]);
            }
            else if (asticaAPI_result["status"].ToString() == "success") // Cast to string
            {
                var captionGPTS = asticaAPI_result.ContainsKey("caption_GPTS") ? asticaAPI_result["caption_GPTS"].ToString() : null; // Cast to string
                var caption = asticaAPI_result.ContainsKey("caption") ? asticaAPI_result["caption"] as Dictionary<string, object> : null; // Cast to dictionary
                var captionDetailed = asticaAPI_result.ContainsKey("CaptionDetailed") ? asticaAPI_result["CaptionDetailed"] as Dictionary<string, object> : null; // Cast to dictionary
                var objects = asticaAPI_result.ContainsKey("objects") ? asticaAPI_result["objects"] as string : null; // Cast to string

                if (!string.IsNullOrEmpty(captionGPTS))
                {
                    Debug.Log("=================");
                    Debug.Log("GPT Caption: " + captionGPTS);
                }
                if (caption != null && caption.ContainsKey("text") && !string.IsNullOrEmpty(caption["text"].ToString())) // Cast to string
                {
                    Debug.Log("=================");
                    Debug.Log("Caption: " + caption["text"]);
                }
                if (captionDetailed != null && captionDetailed.ContainsKey("text") && !string.IsNullOrEmpty(captionDetailed["text"].ToString())) // Cast to string
                {
                    Debug.Log("=================");
                    Debug.Log("CaptionDetailed: " + captionDetailed["text"]);
                }
                if (!string.IsNullOrEmpty(objects))
                {
                    Debug.Log("=================");
                    Debug.Log("Objects: " + objects);
                }
            }
        }
        else
        {
            Debug.Log("Invalid response");
        }
    }

    static async Task<Dictionary<string, object>> AsticaAPI(string endpoint, Dictionary<string, string> payload, string timeout)
    {
        using (HttpClient client = new HttpClient())
        {
            client.Timeout = System.TimeSpan.FromSeconds(System.Convert.ToDouble(timeout));
            var content = new FormUrlEncodedContent(payload);
            HttpResponseMessage response = await client.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
            }
            else
            {
                return new Dictionary<string, object>
                {
                    { "status", "error" },
                    { "error", "Failed to connect to the API." }
                };
            }
        }
    }

    private bool rightControllerGrabbed = false;
    private bool leftControllerGrabbed = false;
    private InputDevice rightXRController;
    private InputDevice leftXRController;

    // Check for and assign XR controllers
    public void getControllers()
    {
        if (!rightControllerGrabbed || !leftControllerGrabbed)
        {
            // Makes a list for input devices + fills it with devices that match the characteristics we give in the Unity editor
            // Narrows devices list using characteristics to just the controller we want to use
            List<InputDevice> devices = new List<InputDevice>();

            InputDeviceCharacteristics rightController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Right;
            InputDevices.GetDevicesWithCharacteristics(rightController, devices);

            InputDeviceCharacteristics leftController = InputDeviceCharacteristics.HeldInHand & InputDeviceCharacteristics.Left;
            InputDevices.GetDevicesWithCharacteristics(leftController, devices);

            Debug.Log("Grabbing devices");
            Debug.Log("Found devices " + devices);

            if (!rightControllerGrabbed)
                rightXRController = devices[2]; //attached to right controller
            if (!leftControllerGrabbed)
                leftXRController = devices[1]; // attached to left controller

            if (devices[2] != null) // rightXRController
            {
                Debug.Log("Grabbed right controller successfully");
                rightControllerGrabbed = true;
            }

            if (devices[1] != null) // leftXRController
            {
                Debug.Log("Grabbed left controller successfully");
                leftControllerGrabbed = true;
            }
        }
    }
}

public static class ExtensionMethod
{
    public static Texture2D DeCompress(this Texture2D source)
    {
        RenderTexture renderTex = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        Texture2D readableText = new Texture2D(source.width, source.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }
}