using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetCurrentAudio : MonoBehaviour
{
    // Game objects for audio syncing
    private PlayAudio _guidePlayAudioScript;
    private PlayAudio _playerPlayAudioScript;
    private PlayAudio _confederateOnePlayAudioScript;
    private PlayAudio _confederateTwoPlayAudioScript;

    public string _guideClip;
    public string _playerClip;
    public string _confederateOneClip;
    public string _confederateTwoClip;

    // Start is called before the first frame update
    void Start()
    {
        // Find each appropriate rig based on our role and grab Play Audio
        if (gameObject.tag == "Guide") // Guide shares the audio of the confederates to the Player client
            _guidePlayAudioScript = FindObjectOfType<GuideFollow>().gameObject.GetComponent<PlayAudio>();

        if (gameObject.tag == "Player")
            _playerPlayAudioScript = GameObject.FindWithTag("Player Rig").gameObject.GetComponent<PlayAudio>();

        if (gameObject.tag == "Confederate_1" || gameObject.tag == "Confederate_2")
            _confederateOnePlayAudioScript = GameObject.FindWithTag("Confederate Rig").gameObject.GetComponent<PlayAudio>();
    }

    // Update is called once per frame
    void Update()
    {
        // Get the name of the current audio clips played by each player
        _guideClip = _guidePlayAudioScript.currentClip.name;
        _playerClip = _playerPlayAudioScript.currentClip.name;
        _confederateOneClip = _confederateOnePlayAudioScript.currentClip.name;
        _confederateTwoClip = _confederateTwoPlayAudioScript.currentClip.name;
    }
}
