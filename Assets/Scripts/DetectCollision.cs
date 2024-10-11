using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectCollision : MonoBehaviour
{
    private VRScreenreader m_VRScreenreaderScript;

    private void Start()
    {
        Debug.Log("Detect collision is added to " + gameObject.name);
        m_VRScreenreaderScript = FindObjectOfType<VRScreenreader>();
        if (m_VRScreenreaderScript != null)
            Debug.Log("A detect collision on " + gameObject.name + " found the screenreader");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("A detect collision on " + gameObject.name + " entered a trigger");
        if (m_VRScreenreaderScript != null)
        {
            // If this collider object is one of the teleportation reticles
            //if (gameObject.name == "Left Reticle" || gameObject.name == "Right Reticle")
                //m_VRScreenreaderScript.ReticleTouchingReaderReference(other);

            // If this collider object is one of the reader reticles
            //if (gameObject.name == "Left Reader Reticle" || gameObject.name == "Right Reader Reticle")
                //m_VRScreenreaderScript.CheckReferenceAndPlayAudio(other);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        /*Debug.Log("A detect collision on " + gameObject.name + " entered a collision");
        if (m_VRScreenreaderScript != null)
        {
            // If this collider object is one of the teleportation reticles
            if (gameObject.name == "Left Reticle" || gameObject.name == "Right Reticle")
                m_VRScreenreaderScript.ReticleTouchingReaderReference(collision);

            // If this collider object is one of the reader reticles
            if (gameObject.name == "Left Reader Reticle" || gameObject.name == "Right Reader Reticle")
                m_VRScreenreaderScript.CheckReferenceAndPlayAudio(collision);
        }*/
    }
}
