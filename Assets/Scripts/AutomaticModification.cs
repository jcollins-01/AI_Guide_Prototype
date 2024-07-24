using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutomaticModification : MonoBehaviour
{
    public GameObject m_targetObject; // The target game object to modify
    public AudioClip beaconClip;
    private float lifespan = 10f; // Time in seconds before the audioSource is destroyed
    //private int numAudioSources = 0;

    // Update is called once per frame
    void Start()
    {
        beaconClip = Resources.Load<AudioClip>("Audio/beacon");

        if (beaconClip == null)
            Debug.LogError("Failed to load beacon audio clip. Please ensure an audio file 'beacon' is located in the Resources folder.");
    }

    public void AddAudioBeacon(GameObject targetObject)
    {
        m_targetObject = targetObject;
        //Debug.Log(targetObject + ", with audio sources: " + numAudioSources);
        if (targetObject != null)
        {
            AudioSource audioSource = targetObject.AddComponent<AudioSource>();
            audioSource.clip = beaconClip;
            audioSource.loop = true;
            audioSource.spatialBlend = 1;
            //audioSource.maxDistance = 10;
            audioSource.Play();
            DestroyAfterTime(audioSource);
            //numAudioSources++;
        }
        else
        {
            Debug.LogWarning("Target not assigned.");
        }
    }

    // Calls for the modification audio beacon to be destroyed
    private void DestroyAfterTime(AudioSource audioSource)
    {
        Debug.Log("Destroying audio source");
        Destroy(audioSource, lifespan);
        //numAudioSources = 0;
    }
}
