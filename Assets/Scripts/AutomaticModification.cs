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

        // If the target object is one of the people avatars, add the beacon to the gameobject of one of the people, so it doesn't get placed somewhere weird
        switch (targetObject.name)
        {
            case "Couple By Fountain":
                targetObject = GameObject.Find("6_m_Talking1");
                break;
            case "Couple By Southern Gazebo":
                targetObject = GameObject.Find("5_m_Talking2");
                break;
            case "Huddle of People by Gazebo":
                targetObject = GameObject.Find("3_f_Talking");
                break;
            case "Dancing People":
                targetObject = GameObject.Find("3_f@House Dancing");
                break;
            case "Western Huddle of People":
                targetObject = GameObject.Find("3_f_Talking");
                break;
            case "Couple by the Platform":
                targetObject = GameObject.Find("4_m_Talking1");
                break;
            case "Couple By Puffy Tree":
                targetObject = GameObject.Find("6_m_Talking1");
                break;
            case "Northwest Huddle of People":
                targetObject = GameObject.Find("2_f_Talking2");
                break;
            case "Couple By the Yellow-Roofed Gazebo":
                targetObject = GameObject.Find("4_m_Talking1");
                break;
            case "Couple By the Fountain":
                targetObject = GameObject.Find("6_m_Talking1");
                break;
        }

        Debug.Log(targetObject + ", added audio source");
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
