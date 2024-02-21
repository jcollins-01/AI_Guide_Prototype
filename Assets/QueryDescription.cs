using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class QueryDescription : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Ready to query for CV description");
    }

    // Update is called once per frame
    void Update()
    {
        // Future method: take screenshot
        
        // Upload image to Imgur
        if (Input.GetKeyDown("space"))
        {
            Debug.Log("Uploading screenshot to Imgur");
            // Loads the screenshot (Unity considers it a texture) from Resources
            Texture2D capturedScreenshot = Resources.Load<Texture2D>("Screenshots/red");
            // Decompresses the screenshot texture to work with encoding, encodes texture to a byte array in PNG format, then converts that array to a base64 string
            Texture2D preppedScreenshot = capturedScreenshot.DeCompress();
            string imageString = System.Convert.ToBase64String(ImageConversion.EncodeToPNG(preppedScreenshot));

            // Takes the byte array of the imageData and passed it to IMGUR for upload
            byte[] imageData = ImageConversion.EncodeToPNG(preppedScreenshot);
            StartCoroutine(UploadImageToImgur(imageData, OnUploadComplete));
        }

        // Query Astica on uploaded image
        if (Input.GetKeyDown("q"))
        {
            Debug.Log("Querying Astica AI");
            QueryAstica();
        }
    }

    async Task QueryAstica() // was a static method
    {
        string asticaAPI_key = "762A2AC4-411B-43B5-A4DC-49D4602B87C3CCFF077C-9401-4AE7-9EDA-8E828D671526"; // visit https://astica.org
        string asticaAPI_timeout = "35"; // seconds

        string asticaAPI_endpoint = "https://vision.astica.ai/describe";
        string asticaAPI_modelVersion = "2.1_full"; // '1.0_full', '2.0_full', or (was) '2.1_full'

        //imgurImageLink
        string asticaAPI_input = imgurImageLink; // Sample tests: "https://astica.ai/example/asticaVision_sample.jpg", "https://usapple.org/wp-content/uploads/2019/10/apple-pink-lady.png", "https://i.postimg.cc/VLp2sVMn/test.png", imageString (FAIL), "https://live.staticflickr.com/65535/53542353338_14b2062afc_h.jpg"
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
        Debug.Log(JsonConvert.SerializeObject(asticaAPI_result, Formatting.Indented));
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

    // Flickr API key
    private string apiKey = "4149eff9725358e6dd3443b3cfb48358";
    public string imgurImageLink;

    // OAuth callback link to Postman: https://www.getpostman.com/oauth2/callback

    // Function to upload image to Imgur
    public IEnumerator UploadImageToImgur(byte[] imageData, System.Action<string> onComplete) //was string imagePath
    {
        // Convert byte array to base64 string
        string base64Image = System.Convert.ToBase64String(imageData);

        // Construct parameters for upload
        WWWForm form = new WWWForm();
        form.AddField("api_key", apiKey);
        form.AddField("photo", base64Image);

        // Upload image to Flickr using UnityWebRequest
        using (UnityWebRequest www = UnityWebRequest.Post("https://up.flickr.com/services/upload/", form))
        {
            www.chunkedTransfer = false; // Flickr doesn't support chunked transfer

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Image upload successful!");
                string responseText = www.downloadHandler.text;
                Debug.Log("Response: " + responseText);

                // Parse response to get photo ID or other relevant information
                string photoId = GetPhotoIdFromResponse(responseText);
                onComplete?.Invoke(photoId);
            }
            else
            {
                Debug.LogError("Error uploading image: " + www.error);
                onComplete?.Invoke(null);
            }
        }
    }

    // Callback function when upload is complete
    void OnUploadComplete(string photoId)
    {
        if (photoId != null)
        {
            // Handle the photo ID here
            Debug.Log("Upload successful! Photo ID: " + photoId);
        }
        else
        {
            Debug.LogError("Upload failed!");
        }
    }

    // Function to parse response and extract photo ID
    private string GetPhotoIdFromResponse(string responseText)
    {
        // Implement parsing logic to extract photo ID from Flickr API response
        // For simplicity, we'll just return the entire response text
        return responseText;
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

// Class to represent the Imgur API response
[System.Serializable]
public class ImgurResponse
{
    public ImgurData data;
    public bool success;
    public int status;
}

// Class to represent the data field in the Imgur API response
[System.Serializable]
public class ImgurData
{
    public string link;
}