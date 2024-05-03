using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    // Scripts to inherit
    private SharedMovement m_SharedMovementScript;

    public string screenshotFileName = "birdEyeCapture.png";
    private int captureWidth = 1920;
    private int captureHeight = 1080;
    public Camera birdEyeCamera;
    public Camera viewpointCamera;

    // Start is called before the first frame update
    void Start()
    {
        // Pulls the viewpointCamera automatically from whichever GameObject is thePlayer - if there is no camera on the player,
        // viewpointCamera will be blank, which tells us something is wrong
        m_SharedMovementScript = gameObject.AddComponent(typeof(SharedMovement)) as SharedMovement;
        viewpointCamera = m_SharedMovementScript.thePlayer.GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
