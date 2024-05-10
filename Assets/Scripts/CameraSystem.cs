using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    // Variables to set camera parameters
    private int captureWidth = 1920;
    private int captureHeight = 1080;
    private float birdHeight = 15f;
    private Vector3 birdRotation = new Vector3(65f, 0f, 0f);

    // Camera variables for AIGuide script to access
    public Camera birdEyeCamera;
    public Camera viewpointCamera;
    public string screenshotFileName = "birdEyeCapture.png";

    // Start is called before the first frame update
    void Start()
    {
        // Pulls the viewpointCamera automatically from the Main Camera under XR Origin
        viewpointCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

        // Creates a Game Object to hold a new camera for a bird's eye view
        GameObject newCamera = new GameObject("Bird's Eye Camera");
        birdEyeCamera = newCamera.AddComponent<Camera>();

        // Camera has specified height it goes above the player to get bird's eye view + rotation to look down at the scene
        birdHeight = birdHeight + transform.position.y;
        birdEyeCamera.transform.position = new Vector3(transform.position.x, birdHeight, transform.position.z);
        birdEyeCamera.transform.eulerAngles = birdRotation;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
