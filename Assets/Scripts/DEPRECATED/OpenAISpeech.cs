using OpenAI;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEngine;

public class OpenAISpeech
{
    public static async Task<HttpResponseMessage> CreateHttpResponseFromTuple(Tuple<string, AudioClip> speechResponse)
    {
        Debug.Log("Creating an Http response out of our tuple");
        var message = speechResponse.Item1;
        var audioClip = speechResponse.Item2;

        byte[] audioData = ConvertAudioClipToByteArray(audioClip);
        var content = new ByteArrayContent(audioData);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        var response = new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = content
        };

        response.Headers.Add("Message", message);

        return await Task.FromResult(response);
    }

    private static byte[] ConvertAudioClipToByteArray(AudioClip audioClip)
    {
        Debug.Log("Converting the audio clip from the tuple to a byte array");
        if (audioClip == null)
        {
            return new byte[0];
        }

        float[] samples = new float[audioClip.samples];
        audioClip.GetData(samples, 0);

        byte[] byteArray = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, byteArray, 0, byteArray.Length);

        return byteArray;
    }

    /*public async Task<AudioClip> CallAlloyTTS(string text, string voice = "alloy")
    {
        Debug.Log("Reached call alloy from open ai speech for streaming");
        var requestUri = "https://api.openai.com/v1/audio/speech"; // Replace with the actual OpenAI endpoint

        var requestBody = new
        {
            model = "tts-1",
            voice = voice,
            input = text
        };

        var requestJson = JsonUtility.ToJson(requestBody);
        var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var response = await client.PostAsync(requestUri, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Starting to read content as a stream");
                var stream = await response.Content.ReadAsStreamAsync();
                return await ConvertStreamToAudioClip(stream);
            }
            else
            {
                Debug.LogError($"Request failed: {response.StatusCode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception in CallAlloyTTS: {e}");
        }

        return null;
    }*/

    public static async Task<AudioClip> ConvertStreamToAudioClip(Stream stream)
    {
        Debug.Log("Converting stream to audio clip");
        // Convert stream to byte array
        byte[] audioData = await ConvertStreamToByteArray(stream);

        // Convert byte array to AudioClip
        return CreateAudioClipFromWav(audioData);
    }

    private static async Task<byte[]> ConvertStreamToByteArray(Stream stream)
    {
        Debug.Log("Converting stream to byte array");
        using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }

    private static AudioClip CreateAudioClipFromWav(byte[] wavData)
    {
        // Implement WAV to AudioClip conversion here
        // This will involve parsing the WAV header and extracting the audio data
        // For now, let's assume you have a method called ParseWav that does this

        return ParseWav(wavData);
    }

    private static AudioClip ParseWav(byte[] wavData)
    {
        Debug.Log("Starting to parse byte array and convert as wav AudioClip");
        // You need to implement this method to parse WAV data and create an AudioClip
        // Refer to Unity's AudioClip.Create and other resources to handle WAV parsing

        // Placeholder implementation
        // Assuming 16-bit PCM WAV file
        int sampleCount = wavData.Length / 2;
        float[] audioData = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(wavData, i * 2);
            audioData[i] = sample / 32768.0f; // Convert to float range -1.0f to 1.0f
        }

        AudioClip audioClip = AudioClip.Create("TTS_Audio", sampleCount, 1, 44100, false);
        audioClip.SetData(audioData, 0);

        return audioClip;
    }
}