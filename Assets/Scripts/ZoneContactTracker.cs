using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZoneContactTracker : MonoBehaviour
{
    public List<GameObject> touchingObjects = new List<GameObject>();

    /*private void OnTriggerEnter(Collider other)
    {
        // Check if it's a key items object, and ensure it isn't already added
        if (!touchingObjects.Contains(other.gameObject) && other.gameObject.layer == 13)
        {
            touchingObjects.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Remove the object when it stops touching this specific cube zone
        if (touchingObjects.Contains(other.gameObject))
        {
            touchingObjects.Remove(other.gameObject);
        }
    }*/
}
