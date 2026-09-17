// OllamaConnectionTest.cs
// Standalone test: verifies Unity can talk to a local Ollama server over plain HTTP.
// Drop this on any empty GameObject in a test scene, press Play, right-click the
// component in the Inspector and choose "Test Ollama Connection" from the context menu
// (or call TestConnection() from a UI Button's OnClick).
//
// Requires Newtonsoft.Json (already used elsewhere in this project, e.g. OpenAIQueries.cs).

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class OllamaConnectionTest : MonoBehaviour
{
    [Header("Ollama Settings")]
    [SerializeField] private string ollamaUrl = "http://localhost:11434/api/chat";
    [SerializeField] private string modelName = "llama3.2";
    [SerializeField] private string testPrompt = "Say hello in one short sentence.";

    private static readonly HttpClient httpClient = new HttpClient();

    [ContextMenu("Test Ollama Connection")]
    public async void TestConnection()
    {
        Debug.Log("[OllamaTest] Sending request to " + ollamaUrl);

        // Ollama's /api/chat expects a "messages" array, same shape as OpenAI's chat format.
        var requestBody = new JObject
        {
            ["model"] = modelName,
            ["messages"] = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = testPrompt
                }
            },
            // "stream": false gives one single JSON response instead of
            // newline-delimited streaming chunks -- simplest for a first test.
            ["stream"] = false
        };

        string jsonBody = requestBody.ToString();
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await httpClient.PostAsync(ollamaUrl, content);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[OllamaTest] HTTP {(int)response.StatusCode}: {responseText}");
                return;
            }

            // Expected shape from /api/chat (stream:false):
            // { "model": "...", "message": { "role": "assistant", "content": "..." }, "done": true, ... }
            JObject parsed = JObject.Parse(responseText);
            string replyText = parsed["message"]?["content"]?.ToString();

            if (string.IsNullOrEmpty(replyText))
            {
                Debug.LogWarning("[OllamaTest] Got a response but couldn't find message.content. Raw response:\n" + responseText);
            }
            else
            {
                Debug.Log("[OllamaTest] Ollama replied: " + replyText);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.LogError("[OllamaTest] Connection failed -- is 'ollama serve' running? " + ex.Message);
        }
        catch (Exception ex)
        {
            Debug.LogError("[OllamaTest] Unexpected error: " + ex.Message);
        }
    }
}
