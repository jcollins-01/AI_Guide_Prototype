using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectCollision : MonoBehaviour
{
    private VRScreenreader m_VRScreenreaderScript;

    private void Start()
    {
        m_VRScreenreaderScript = FindObjectOfType<VRScreenreader>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_VRScreenreaderScript != null)
        {
            // If this collider object is one of the teleportation reticles
            if (gameObject.name == "Left Reticle" || gameObject.name == "Right Reticle")
                m_VRScreenreaderScript.ReticleTouchingReaderReference(other);

            // If this collider object is one of the reader reticles
            if (gameObject.name == "Left Reader Reticle" || gameObject.name == "Right Reader Reticle")
                m_VRScreenreaderScript.CheckReferenceAndPlayAudio(other);
        }
    }
}
