using UnityEngine;

public class GuideController : MonoBehaviour
{
    private GuideAudioSync _guideAudioSync;

    private void Start()
    {
        _guideAudioSync = GetComponent<GuideAudioSync>();

        if (_guideAudioSync == null)
            Debug.LogError("GuideAudioSync missing from this GameObject");
    }

    /*public void SetNewAudioClip(AudioClip audioClip)
    {
        Debug.Log("reached SetNewAudioClip");
        if (_guideAudioSync != null)
            _guideAudioSync.SetAudioClip(audioClip);
        else
            Debug.LogError("GuideAudioSync is not initialized.");
    }*/
}