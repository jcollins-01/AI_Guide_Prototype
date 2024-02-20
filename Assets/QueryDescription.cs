using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
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
        if (Input.GetKeyDown("space"))
        {
            Debug.Log("Querying Astica AI");
            QueryAstica();
        }
    }

    static async Task QueryAstica()
    {
        string asticaAPI_key = "762A2AC4-411B-43B5-A4DC-49D4602B87C3CCFF077C-9401-4AE7-9EDA-8E828D671526"; // visit https://astica.org
        string asticaAPI_timeout = "35"; // seconds

        string asticaAPI_endpoint = "https://vision.astica.ai/describe";
        string asticaAPI_modelVersion = "2.1_full"; // '1.0_full', '2.0_full', or '2.1_full'

        // Loads the screenshot (Unity considers it a texture) from Resources
        Texture2D capturedScreenshot = Resources.Load<Texture2D>("Screenshots/red");
        // Decompresses the screenshot texture to work with encoding, encodes texture to a byte array in PNG format, then converts that array to a base 64 string
        Texture2D preppedScreenshot = capturedScreenshot.DeCompress();
        string imageString = System.Convert.ToBase64String(ImageConversion.EncodeToPNG(preppedScreenshot));

        string asticaAPI_input = "https://i.postimg.cc/Yq3m9Mcm/red.png"; // Sample tests: "https://astica.ai/example/asticaVision_sample.jpg", "https://usapple.org/wp-content/uploads/2019/10/apple-pink-lady.png", "https://i.postimg.cc/VLp2sVMn/test.png"
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