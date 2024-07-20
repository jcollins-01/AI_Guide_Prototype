using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckCurrentAudio : MonoBehaviour
{
    // GameObjects to play audio on
    public GameObject thePlayer;
    public GameObject theGuide;
    public GameObject confederateOne;
    public GameObject confederateTwo;

    // Audio components for playing audio
    public AudioSource playerAudio;
    public AudioSource guideAudio;
    public AudioSource confederateOneAudio;
    public AudioSource confederateTwoAudio;
    public AudioClip currentClip;

    // Audio clips for player sonification
    private AudioClip teleportEffect;
    private AudioClip walkEffect;
    private AudioClip woodEffect;
    private AudioClip waterEffect;
    private AudioClip grassEffect;
    private AudioClip turnEffect;
    private AudioClip woodCollisionEffect;
    private AudioClip collisionEffect;
    private AudioClip idleEffect;
    private AudioClip noEffect;

    private AudioClip _clip = default;
    private AudioClip _previousClip = default;

    private AudioClipSync _audioClipSync;

    // Variables to hold scripts we need access to
    private SharedMovement m_SharedMovementScript;
    private PlayAudio m_PlayAudioScript;

    // Monitoring bools
    private bool sharedMovementFound = false;
    private bool confederatesArrived = false;
    private bool audioAdded = false;

    // Bools to determine which client we're playing from
    private bool guideClient = false;
    private bool confederateOneClient = false;
    private bool confederateTwoClient = false;

    // Start is called before the first frame update
    void Start()
    {
        // Set default values for clips to be overwritten
        _clip = currentClip;
        _previousClip = null;

        // Determine which role we are playing as, and add local audio sources to each other one
        // If our audio is coming from the client with the guide, add audio sources to the confederates
        if (gameObject.tag == "Guide")
        {
            // Get the audioclipsync component for this client and set our audioclipsync variable to it
            _audioClipSync = GetComponent<AudioClipSync>(); //This is to make sure we grab the sync component from the guide and the player and access them separately
            m_PlayAudioScript = GameObject.FindWithTag("Guide Rig").GetComponent<PlayAudio>();
            guideClient = true;
            Debug.Log("Playing as the guide, audio clip sync is " + GetComponent<AudioClipSync>());
        }

        if (gameObject.tag == "Player")
        {
            // Get the audioclipsync component for this client and set our audioclipsync variable to it
            _audioClipSync = GetComponent<AudioClipSync>();
            m_PlayAudioScript = GameObject.FindWithTag("Player Rig").GetComponent<PlayAudio>();
            guideClient = true;
            Debug.Log("Playing as the player, audio clip sync is " + GetComponent<AudioClipSync>());
        }

        // If our audio is coming from the client with confed 1, add audio sources to the player and guide
        if (gameObject.tag == "Confederate_1")
        {
            // Get the audioclipsync component for this client and set our audioclipsync variable to it
            _audioClipSync = GetComponent<AudioClipSync>();
            m_PlayAudioScript = GameObject.FindWithTag("Confederate Rig").GetComponent<PlayAudio>();
            confederateOneClient = true;
            Debug.Log("Playing as the confederate_1, audio clip sync is " + GetComponent<AudioClipSync>());
        }

        // If our audio is coming from the client with confed 1, add audio sources to the player and guide
        if (gameObject.tag == "Confederate_2")
        {
            // Get the audioclipsync component for this client and set our audioclipsync variable to it
            _audioClipSync = GetComponent<AudioClipSync>();
            m_PlayAudioScript = GameObject.FindWithTag("EditorOnly").GetComponent<PlayAudio>(); // CHANGE TO CONFEDERATE RIG
            confederateTwoClient = true;
            Debug.Log("Playing as the confederate_2, audio clip sync is " + GetComponent<AudioClipSync>());
        }

        // Assign sounds from Resources
        teleportEffect = Resources.Load<AudioClip>("Audio/teleport");
        walkEffect = Resources.Load<AudioClip>("Audio/walk");
        woodEffect = Resources.Load<AudioClip>("Audio/wood-walk");
        waterEffect = Resources.Load<AudioClip>("Audio/water-walk");
        grassEffect = Resources.Load<AudioClip>("Audio/grass-walk");
        turnEffect = Resources.Load<AudioClip>("Audio/turn");
        woodCollisionEffect = Resources.Load<AudioClip>("Audio/wooden-collision");
        collisionEffect = Resources.Load<AudioClip>("Audio/general-collision");

        idleEffect = Resources.Load<AudioClip>("Audio/guide_idle");
        noEffect = Resources.Load<AudioClip>("Audio/nothing");
    }

    // Update is called once per frame
    void Update()
    {
        // Grab components we need access to
        if (!sharedMovementFound)
            getSharedMovement();
        if (!confederatesArrived)
            checkConfederateArrival();

        // Add audio sources to all other players that aren't from our client
        //if (!audioAdded)
            //addAudioSources();

        // Repeatedly check for the audio clip current set on each player
        //if (audioAdded)
            //getCurrentAudio();

        // If every player is present, determine our role and play local audio accordingly
        //if (sharedMovementFound && confederatesArrived)
            //determineLocalRole();
    }

    private void determineLocalRole()
    {
        //Debug.Log("Reached determineLocalRole");
        // Determine the role we are playing as, get the clip assigned to all other roles, and play that clip from the audio source we added to them
        if (gameObject.tag == "Guide") // This should play the correct audio for the Player too
        {
            // Add to confederates (since we already hear the player locally)
            Debug.Log("Determined to be a guide");
            
            playSyncedAudio(confederateTwo, confederateTwoAudio);
            playSyncedAudio(confederateOne, confederateOneAudio);
        }

        if (gameObject.tag == "Confederate_1")
        {
            // Add to other confederate, guide, and player
            Debug.Log("Determined to be confederate_1");
            playSyncedAudio(confederateTwo, confederateTwoAudio);
            playSyncedAudio(theGuide, guideAudio);
            playSyncedAudio(thePlayer, playerAudio);
        }

        if (gameObject.tag == "Confederate_2")
        {
            // Add to other confederate, guide, and player
            Debug.Log("Determined to be confederate_2");
            playSyncedAudio(confederateOne, confederateOneAudio);
            playSyncedAudio(theGuide, guideAudio);
            playSyncedAudio(thePlayer, playerAudio);
        }
    }

    private void playSyncedAudio(GameObject player, AudioSource audioSource)
    {
        Debug.Log("Reached playSyncAudio");
        Debug.Log("We have an audio sync clip component for "  + player + " which is " + player.GetComponent<AudioClipSync>());
        Debug.Log("The name of the clip is " + player.GetComponent<AudioClipSync>()._clipName);
        if (player.GetComponent<AudioClipSync>()._clipName == teleportEffect.name)
        {
            Debug.Log("Playing teleport for " + player);
            audioSource.clip = teleportEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == walkEffect.name)
        {
            Debug.Log("Playing walk for " + player);
            audioSource.clip = walkEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == woodEffect.name)
        {
            Debug.Log("Playing wood for " + player);
            audioSource.clip = woodEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == waterEffect.name)
        {
            Debug.Log("Playing water for " + player);
            audioSource.clip = waterEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == grassEffect.name)
        {
            Debug.Log("Playing grass for " + player);
            audioSource.clip = grassEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == turnEffect.name)
        {
            Debug.Log("Playing turn for " + player);
            audioSource.clip = turnEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == woodCollisionEffect.name)
        {
            Debug.Log("Playing wood collision for " + player);
            audioSource.clip = woodCollisionEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == collisionEffect.name)
        {
            Debug.Log("Playing general collision for " + player);
            audioSource.clip = collisionEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == idleEffect.name)
        {
            Debug.Log("Playing idle for " + player);
            audioSource.clip = idleEffect;
            audioSource.Play();
        }
        else if (player.GetComponent<AudioClipSync>()._clipName == noEffect.name)
        {
            Debug.Log("Playing no effect for " + player);
            audioSource.clip = noEffect;
            audioSource.Play();
        }
    }

    private void getCurrentAudio()
    {
        currentClip = m_PlayAudioScript.currentClip;
        _clip = currentClip;

        if (_clip != _previousClip)
        {
            Debug.Log("Audio clip has changed to " + currentClip.name);
            // For each audio sound effect, send the name of the current clip to the local audioclipsync component
            // This is how we'll share with the network which clip each player is playing
            if (currentClip == teleportEffect)
            {
                //Send currentClip.name to the AudioClipSync component
                _audioClipSync.SetClipName(teleportEffect.name);
            }
            else if (currentClip == walkEffect)
                _audioClipSync.SetClipName(walkEffect.name);
            else if (currentClip == woodEffect)
                _audioClipSync.SetClipName(woodEffect.name);
            else if (currentClip == waterEffect)
                _audioClipSync.SetClipName(waterEffect.name);
            else if (currentClip == grassEffect)
                _audioClipSync.SetClipName(grassEffect.name);
            else if (currentClip == turnEffect)
                _audioClipSync.SetClipName(turnEffect.name);
            else if (currentClip == woodCollisionEffect)
                _audioClipSync.SetClipName(woodCollisionEffect.name);
            else if (currentClip == collisionEffect)
                _audioClipSync.SetClipName(collisionEffect.name);
            else if (currentClip == idleEffect)
                _audioClipSync.SetClipName(idleEffect.name);
            else if (currentClip == noEffect)
                _audioClipSync.SetClipName(noEffect.name);

            _previousClip = _clip;
        }
    }

    private void addAudioSources()
    {
        // If we're on the guide client, add audio sources to the two confederates - wait to mark as true until both confederates are added
        if (guideClient && confederatesArrived)
        {
            confederateOneAudio = confederateOne.AddComponent<AudioSource>();
            confederateOneAudio.loop = true;
            confederateOneAudio.spatialBlend = 1f;
            confederateOneAudio.playOnAwake = false;

            confederateTwoAudio = confederateTwo.AddComponent<AudioSource>();
            confederateTwoAudio.loop = true;
            confederateTwoAudio.spatialBlend = 1f;
            confederateTwoAudio.playOnAwake = false;

            audioAdded = true;
        }

        if (confederateOneClient && sharedMovementFound)
        {
            playerAudio = thePlayer.AddComponent<AudioSource>();
            playerAudio.loop = true;
            playerAudio.spatialBlend = 1f;
            playerAudio.playOnAwake = false;

            guideAudio = theGuide.AddComponent<AudioSource>();
            guideAudio.loop = true;
            guideAudio.spatialBlend = 1f;
            guideAudio.playOnAwake = false;

            confederateTwoAudio = confederateTwo.AddComponent<AudioSource>();
            confederateTwoAudio.loop = true;
            confederateTwoAudio.spatialBlend = 1f;
            confederateTwoAudio.playOnAwake = false;

            audioAdded = true;
        }
    }

    private void getSharedMovement()
    {
        if (m_SharedMovementScript == null)
            m_SharedMovementScript = FindObjectOfType<SharedMovement>();
        else
        {
            theGuide = m_SharedMovementScript.theGuide;
            thePlayer = m_SharedMovementScript.thePlayer;
            if (theGuide != null && thePlayer != null)
            {
                // Assign playerAudio component after we have access to thePlayer
                if (playerAudio == null)
                    playerAudio = thePlayer.GetComponentInParent<AudioSource>();

                if (guideAudio == null)
                    guideAudio = theGuide.transform.parent.GetComponentInParent<AudioSource>();

                sharedMovementFound = true;
            }
        }
    }

    private void checkConfederateArrival()
    {
        if (!confederatesArrived)
        {
            // If both confederates have arrived in the scene
            if (GameObject.FindWithTag("Confederate_1") && GameObject.FindWithTag("Confederate_2"))
            {
                confederateOne = GameObject.FindWithTag("Confederate_1");
                confederateOneAudio = confederateOne.GetComponentInChildren<AudioSource>();

                confederateTwo = GameObject.FindWithTag("Confederate_2");
                confederateTwoAudio = confederateTwo.GetComponentInChildren<AudioSource>();

                confederatesArrived = true;
            }
        }
    }
}
