using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectedTarget : MonoBehaviour
{
    public bool playerReachedTarget = false;
    private Material previousMaterial;
    
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("A new target was selected and SelectedTarget has been added");

        previousMaterial = gameObject.GetComponent<Renderer>().material;
        gameObject.GetComponent<Renderer>().material = Resources.Load<Material>("Screenreader/Glow");
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Player collided with the target in SelectedTarget");
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.clip = Resources.Load<AudioClip>("Audio/completion");
        audioSource.Play();
        gameObject.GetComponent<Renderer>().material = previousMaterial;

        playerReachedTarget = true;
    }
}
