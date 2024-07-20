using Normal.Realtime;
using Unity.XR.CoreUtils;
using UnityEngine;

public class AudioClipSync : RealtimeComponent<AudioClipModel>
{
    private AudioSource _audioSource;
    private PlayAudio _playAudioScript;
    public string _clipName;

    // Monitoring bools
    private bool audioSourceFound = false;

    private void Awake()
    {
        // Find each appropriate rig based on our role and grab Play Audio
        if (gameObject.tag == "Guide")
        {
            _playAudioScript = FindObjectOfType<GuideFollow>().gameObject.GetComponent<PlayAudio>();
            Debug.Log("ACS getting play audio from Guide " + FindObjectOfType<GuideFollow>().gameObject.GetComponent<PlayAudio>());
        }
            

        if (gameObject.tag == "Player")
        {
            _playAudioScript = GameObject.FindWithTag("Player Rig").gameObject.GetComponent<PlayAudio>();
            Debug.Log("ACS getting play audio from Player " + GameObject.FindWithTag("Player Rig").gameObject.GetComponent<PlayAudio>());
            Debug.Log("Player rig is " + GameObject.FindWithTag("Player Rig"));
        }
            

        if (gameObject.tag == "Confederate_1")// || gameObject.tag == "Confederate_2")
        {
            _playAudioScript = GameObject.FindWithTag("Confederate Rig").gameObject.GetComponent<PlayAudio>();
            Debug.Log("ACS getting play audio from a confederate " + GameObject.FindWithTag("Confederate Rig").gameObject.GetComponent<PlayAudio>());
        }
            

        // REMOVE AFTER TESTING
        if (gameObject.tag == "Confederate_2")
            _playAudioScript = GameObject.FindWithTag("EditorOnly").gameObject.GetComponent<PlayAudio>();

        _playAudioScript = FindObjectOfType<PlayAudio>();

        if (_playAudioScript == null)
            Debug.LogError("PlayAudio script missing from this GameObject.");
    }

    private void Update()
    {
        if (!audioSourceFound)
            getAudioSource();
    }

    private void getAudioSource()
    {
        if (_audioSource == null)
            GetComponent<AudioSource>(); // Grab audio source on root of object this script is on - each player should have one
        else
            audioSourceFound = true;
    }

    protected override void OnRealtimeModelReplaced(AudioClipModel previousModel, AudioClipModel currentModel)
    {
        if (previousModel != null)
            previousModel.clipNameDidChange -= ClipNameDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.clipName = _playAudioScript.currentClip != null ? _playAudioScript.currentClip.name : "nothing";

            currentModel.clipNameDidChange += ClipNameDidChange;
        }
    }

    private void ClipNameDidChange(AudioClipModel model, string value)
    {
        //_playAudioScript.PlayFootstepSoundByName(value);
    }

    public void SetClipName(string clipName)
    {
        Debug.Log("Reached SetClipName with " + clipName);
        if (model != null)
            model.clipName = clipName;

        _clipName = clipName;
    }
}