using Normal.Realtime;
using Unity.XR.CoreUtils;
using UnityEngine;

public class AudioClipSync : RealtimeComponent<AudioClipModel>
{
    // Game objects for native audio syncing
    public AudioSource _guideAudioSource;
    public AudioSource _playerAudioSource;
    public AudioSource _confederateOneAudioSource;
    public AudioSource _confederateTwoAudioSource;

    public PlayAudio _playAudioScript;
    public string _clipName;

    public GetCurrentAudio _guideAudio;
    private GetCurrentAudio _playerAudio;
    public GetCurrentAudio _confederateOneAudio;
    public GetCurrentAudio _confederateTwoAudio;
    

    // Monitoring bools
    private bool audioSourceFound = false;

    // Sound effects for player sonification
    private AudioClip teleportEffect;
    private AudioClip walkEffect;
    private AudioClip woodEffect;
    private AudioClip waterEffect;
    private AudioClip grassEffect;
    private AudioClip turnEffect;
    private AudioClip woodCollisionEffect;
    private AudioClip collisionEffect;
    private AudioClip noEffect; // For sharing sound properly

    // Sound effects for guide sonification
    private AudioClip robotWalkEffect;
    private AudioClip caneWalkEffect;
    private AudioClip dogWalkEffect;
    private AudioClip birdFlyEffect;

    private void Awake()
    {
        // Find each appropriate rig based on our role and grab Play Audio - ASSIGNS OUR OWN AUDIO TO SEND TO OTHERS
        if (gameObject.tag == "Guide") // Guide shares the audio of the confederates to the Player client
        {
            // Our audio to share
            _playAudioScript = FindObjectOfType<GuideFollow>().gameObject.GetComponent<PlayAudio>();

            // Others' audio we need
            _confederateOneAudio = GameObject.FindWithTag("Confederate_1").GetComponent<GetCurrentAudio>();
            _confederateTwoAudio = GameObject.FindWithTag("Confederate_2").GetComponent<GetCurrentAudio>();
        }

        // Our audio to share (guide handles grabbing others' audio for Player)
        if (gameObject.tag == "Player")
            _playAudioScript = GameObject.FindWithTag("Player Rig").gameObject.GetComponent<PlayAudio>();


        if (gameObject.tag == "Confederate_1" || gameObject.tag == "Confederate_2")
        {
            // Our audio to share
            _playAudioScript = GameObject.FindWithTag("Confederate Rig").gameObject.GetComponent<PlayAudio>();

            // Others' audio we need
            _guideAudio = GameObject.FindWithTag("Guide").GetComponent<GetCurrentAudio>();
            _playerAudio = GameObject.FindWithTag("Player").GetComponent<GetCurrentAudio>();
        }
            

        //_playAudioScript = FindObjectOfType<PlayAudio>();

        if (_playAudioScript == null)
            Debug.LogError("PlayAudio script missing from this GameObject.");

        // Assign sounds from Resources
        teleportEffect = Resources.Load<AudioClip>("Audio/teleport");
        walkEffect = Resources.Load<AudioClip>("Audio/walk");
        woodEffect = Resources.Load<AudioClip>("Audio/wood-walk");
        waterEffect = Resources.Load<AudioClip>("Audio/water-walk");
        grassEffect = Resources.Load<AudioClip>("Audio/grass-walk");
        turnEffect = Resources.Load<AudioClip>("Audio/turn");
        woodCollisionEffect = Resources.Load<AudioClip>("Audio/wooden-collision");
        collisionEffect = Resources.Load<AudioClip>("Audio/general-collision");
        noEffect = Resources.Load<AudioClip>("Audio/nothing");

        robotWalkEffect = Resources.Load<AudioClip>("Audio/robot-walk");
        caneWalkEffect = Resources.Load<AudioClip>("Audio/white-cane");
        dogWalkEffect = Resources.Load<AudioClip>("Audio/dog-walk");
        birdFlyEffect = Resources.Load<AudioClip>("Audio/bird-flap");
    }

    private void Update()
    {
        // If we are in a scene with Shared Movement (a guide and player) and at least one confederate
        if (FindObjectOfType<SharedMovement>() && GameObject.FindWithTag("Confederate"))
        {
            // Grab components we need access to
            if (!audioSourceFound)
                getAudioSources();

            // For each grabbed audio source, play the clip we've pulled from GetCurrentAudio in our local client
            playSyncedAudioLocally();
        }
    }

    private void playSyncedAudio(AudioSource _audioSource, string _clipName)
    {
        Debug.Log("The clip in AudioClipSync is " + _clipName);
        switch (_clipName)
        {
            case "teleport":
                _audioSource.clip = teleportEffect;
                break;
            case "walk":
                _audioSource.clip = walkEffect;
                break;
            case "wood-walk":
                _audioSource.clip = woodEffect;
                break;
            case "water-walk":
                _audioSource.clip = waterEffect;
                break;
            case "grass-walk":
                _audioSource.clip = grassEffect;
                break;
            case "turn":
                _audioSource.clip = turnEffect;
                break;
            case "wooden-collision":
                _audioSource.clip = woodCollisionEffect;
                break;
            case "general-collision":
                _audioSource.clip = collisionEffect;
                break;
            case "nothing":
                _audioSource.clip = noEffect;
                break;
            case "robot-walk":
                _audioSource.clip = robotWalkEffect;
                break;
            case "white-cane":
                _audioSource.clip = caneWalkEffect;
                break;
            case "dog-walk":
                _audioSource.clip = dogWalkEffect;
                break;
            case "bird-flap":
                _audioSource.clip = birdFlyEffect;
                break;
        }

        if (!_audioSource.isPlaying)
        {
            _audioSource.Play();
            Debug.Log("Audio source is playing from AudioClipSync");
        }
    }

    private void playSyncedAudioLocally()
    {
        // Play the audio that we need locally for each audio source we've grabbed
        if (gameObject.tag == "Guide")
        {
            if (_confederateOneAudioSource != null)
                playSyncedAudio(_confederateOneAudioSource, _confederateOneAudio._confederateOneClip);

            if (_confederateTwoAudioSource != null)
                playSyncedAudio(_confederateTwoAudioSource, _confederateTwoAudio._confederateTwoClip);
        }

        if (gameObject.tag == "Confederate_1")
        {
            if (_playerAudioSource != null)
                playSyncedAudio(_playerAudioSource, _playerAudio._playerClip);

            if (_guideAudioSource != null)
                playSyncedAudio(_guideAudioSource, _guideAudio._guideClip);

            if (_confederateTwoAudioSource != null)
                playSyncedAudio(_confederateTwoAudioSource, _confederateTwoAudio._confederateTwoClip);
        }

        if (gameObject.tag == "Confederate_2")
        {
            if (_playerAudioSource != null)
                playSyncedAudio(_playerAudioSource, _playerAudio._playerClip);

            if (_guideAudioSource != null)
                playSyncedAudio(_guideAudioSource, _guideAudio._guideClip);

            if (_confederateOneAudioSource != null)
                playSyncedAudio(_confederateOneAudioSource, _confederateOneAudio._confederateOneClip);
        }
    }

    private void getAudioSources()
    {
        /*if (_audioSource == null)
            _audioSource = GetComponentInChildren<AudioSource>(); // Grab audio source on root of object this script is on - the sound effect source
        else
            audioSourceFound = true;*/
        if (_guideAudioSource == null || _playerAudioSource == null || _confederateOneAudioSource == null || _confederateTwoAudioSource == null)
        {
            if (gameObject.tag == "Guide")
            {
                _confederateOneAudioSource = GameObject.FindWithTag("Confederate_1").GetComponentInChildren<AudioSource>();
                _confederateTwoAudioSource = GameObject.FindWithTag("Confederate_2").GetComponentInChildren<AudioSource>();
            }

            if (gameObject.tag == "Confederate_1")
            {
                _guideAudioSource = GameObject.FindWithTag("Guide").GetComponent<AudioSource>();
                _playerAudioSource = GameObject.FindWithTag("Player").GetComponent<AudioSource>();
                _confederateTwoAudioSource = GameObject.FindWithTag("Confederate_2").GetComponentInChildren<AudioSource>();
            }

            if (gameObject.tag == "Confederate_2")
            {
                _guideAudioSource = GameObject.FindWithTag("Guide").GetComponent<AudioSource>();
                _playerAudioSource = GameObject.FindWithTag("Player").GetComponent<AudioSource>();
                _confederateOneAudioSource = GameObject.FindWithTag("Confederate_1").GetComponentInChildren<AudioSource>();
            }
        }
    }

    protected override void OnRealtimeModelReplaced(AudioClipModel previousModel, AudioClipModel currentModel)
    {
        if (previousModel != null)
            previousModel.clipNameDidChange -= ClipNameDidChange;

        if (currentModel != null)
        {
            if (currentModel.isFreshModel)
                currentModel.clipName = _playAudioScript.currentClip != null ? _playAudioScript.currentClip.name : "nothing";

            currentModel.clipNameDidChange += ClipNameDidChange;
        }
    }

    private void ClipNameDidChange(AudioClipModel model, string value)
    {
        
    }

    public void SetClipName(string clipName)
    {
        Debug.Log("Reached SetClipName with " + clipName);
        if (model != null)
            model.clipName = clipName;
    }
}