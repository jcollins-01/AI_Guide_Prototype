using Normal.Realtime;
using System.Collections;
using UnityEngine;

public class GuideAudioSync : RealtimeComponent<GuideAudioSyncModel>
{
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponentInChildren<AudioSource>();
        if (_audioSource == null)
            Debug.LogError("AudioSource missing from this GameObject");
    }

    protected override void OnRealtimeModelReplaced(GuideAudioSyncModel previousModel, GuideAudioSyncModel currentModel)
    {
        if (previousModel != null)
            previousModel.audioClipDataDidChange -= AudioClipDataDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.audioClipData = null;
            currentModel.audioClipDataDidChange += AudioClipDataDidChange;
        }
    }

    private void AudioClipDataDidChange(GuideAudioSyncModel model, byte[] audioClipData)
    {
        Debug.Log("Detected that the audio clip data did change!");
        if (audioClipData != null)
        {
            Debug.Log("Should be playing audio clip");
            AudioClip clip = AudioClipFromByteArray(audioClipData);
            _audioSource.clip = clip;
            _audioSource.Play();
        }
    }

    public void SetAudioClip(AudioClip clip)
    {
        Debug.Log("Reached SetAudioClip in GuideAudioSync");
        byte[] audioClipData = AudioClipToByteArray(clip);
        if (model != null)
            model.audioClipData = audioClipData;
        else
            Debug.LogError("Model is not initialized.");
    }

    private byte[] AudioClipToByteArray(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        byte[] byteArray = new byte[samples.Length * sizeof(float)];
        System.Buffer.BlockCopy(samples, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }

    private AudioClip AudioClipFromByteArray(byte[] byteArray)
    {
        float[] samples = new float[byteArray.Length / sizeof(float)];
        System.Buffer.BlockCopy(byteArray, 0, samples, 0, byteArray.Length);
        AudioClip clip = AudioClip.Create("GuideVoice", samples.Length, 1, 44100, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Start is called before the first frame update
    void Start()
    {
        gameObject.AddComponent<GuideController>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
