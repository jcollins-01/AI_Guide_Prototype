using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using Unity.VisualScripting;

public class RandomObjectSpawner : MonoBehaviour
{
    // Variables to share with other scripts
    [HideInInspector]
    public GameObject spawnedObject;
    [HideInInspector]
    public GameObject spawnSource;

    // Components for audio
    [HideInInspector]
    public AudioSource audioSource;
    [HideInInspector]
    public AudioClip unloaded;
    private AudioClip preparing;

    // Scripts we need access to
    private ShortTaskController m_ShortTaskControllerScript;

    void Start()
    {
        // Assign spawnSource to be the GameObject this script is on
        spawnSource = this.gameObject;
        Debug.Log("Spawn source is " + this.gameObject.name);
        m_ShortTaskControllerScript = FindObjectOfType<ShortTaskController>();

        // Assign audio components for indicating an object has been unloaded / prepared
        audioSource = this.gameObject.AddComponent<AudioSource>();
        unloaded = Resources.Load<AudioClip>("Audio/completion");
        string sceneName = SceneManager.GetActiveScene().name;
        switch (sceneName)
        {
            case "Kitchen":
                preparing = Resources.Load<AudioClip>("Audio/chop");
                break;
            case "Alien Spaceship Repair Shop":
                preparing = Resources.Load<AudioClip>("Audio/repair");
                break;
            case "Flower Shop":
                preparing = Resources.Load<AudioClip>("Audio/watering");
                break;
            case "Monster Pet Shop":
                preparing = Resources.Load<AudioClip>("Audio/brush");
                break;
            case "Witch Cottage":
                preparing = Resources.Load<AudioClip>("Audio/cast");
                break;
            case "Pharmacy":
                preparing = Resources.Load<AudioClip>("Audio/scan");
                break;
        }
    }

    private void Update()
    {
        // CheckObjectUnloaded(); // debugging function for unloading portion
        CheckObjectPrepared();
    }

    public void SpawnRandomObject()
    {
        Debug.Log("Spawning object");

        // Extra check since the first time this script is added, this function is called, occasionally before Start runs
        if (spawnSource == null)
            spawnSource = this.gameObject;
        if (m_ShortTaskControllerScript == null)
            m_ShortTaskControllerScript = FindObjectOfType<ShortTaskController>();

        // Get the current scene name
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Load all prefabs from Resources/Environment/{currentSceneName}
        Object[] objects = Resources.LoadAll($"Environments/{currentSceneName}", typeof(GameObject));

        // Check if any objects were found
        if (objects.Length == 0)
        {
            Debug.LogWarning($"No objects found in Resources/Environments/{currentSceneName}");
            return;
        }

        // Select a random prefab from the loaded objects
        GameObject randomPrefab = (GameObject)objects[Random.Range(0, objects.Length)];

        // Get the position and rotation of the spawnSource
        Vector3 spawnPosition = spawnSource.transform.position + new Vector3(0, 1.5f, 0); // Can adjust Y position to make it fall from a higher place above spawnSource
        Quaternion spawnRotation = Quaternion.identity;

        // Instantiate the random prefab on top of the spawnSource
        spawnedObject = Instantiate(randomPrefab, spawnPosition, spawnRotation);

        if (spawnedObject.GetComponent<XRGrabInteractable>() != null &&
            spawnedObject.GetComponent<GrabRequest>() != null)
        {
            XRGrabInteractable m_XRGrabInteractableScript = spawnedObject.GetComponent<XRGrabInteractable>();
            GrabRequest m_GrabRequestScript = spawnedObject.GetComponent<GrabRequest>();
            m_XRGrabInteractableScript.enabled = false;
            m_GrabRequestScript.enabled = false;
            StartCoroutine(StartGrabCheckAfterDelay(m_XRGrabInteractableScript, m_GrabRequestScript));
        }

        m_ShortTaskControllerScript.unloadingBag.layer = 9;
        spawnedObject.AddComponent<UnloadObject>();
        spawnedObject.GetComponent<UnloadObject>().AssignBag(spawnSource);
    }

    public void ResetUnloadingPortion()
    {
        if (spawnedObject != null)
        {
            // Debug.Log("reset unloading portion of task");
            Destroy(spawnedObject); // Destroy object to ensure only one exists at a given time
            SpawnRandomObject();
        }
    }

    public void StartPreparationPart()
    {
        spawnedObject.AddComponent<PrepareObject>();
        spawnedObject.GetComponent<PrepareObject>().AssignTable(spawnSource);
    }

    void CheckObjectUnloaded()
    {
        // If the object has an UnloadObject component added to it by the task controller
        if (spawnedObject != null)
        {
            if (spawnedObject.GetComponent<UnloadObject>())
            {
                if (spawnedObject.GetComponent<UnloadObject>().playerUnloadedObject)
                {
                    Debug.Log("Player unloaded object - moved on to prep portion of task");
                }
            }
        }
    }

    void CheckObjectPrepared()
    {
        // If the object has a PrepareObject component added to it by the task controller
        if (spawnedObject != null)
        {
            if (spawnedObject.GetComponent<PrepareObject>())
            {
                // If player is mid-preparation
                if (spawnedObject.GetComponent<PrepareObject>().playerMidPreparation)
                {
                    Debug.Log("Player starting preparing object");
                    if (!audioSource.isPlaying)
                    {
                        audioSource.clip = preparing;
                        audioSource.Play();
                    }
                }

                // If player finishes preparation
                if (spawnedObject.GetComponent<PrepareObject>().playerPreparedObject)
                {
                    Debug.Log("Player prepared object - destroying object and spawning a new one");
                    spawnedObject.GetComponent<PrepareObject>().prepTool.GetComponent<XRGrabInteractable>().enabled = false;
                    Destroy(spawnedObject.GetComponent<PrepareObject>());
                    Destroy(spawnedObject); // Destroy object to ensure only one exists at a given time
                    m_ShortTaskControllerScript.preparationTaskScore++;
                    audioSource.clip = unloaded;
                    audioSource.Play();
                    ResetUnloadingPortion();
                }
                else if (!spawnedObject.GetComponent<PrepareObject>().playerMidPreparation && !spawnedObject.GetComponent<PrepareObject>().playerPreparedObject)
                {
                    if (audioSource.isPlaying && audioSource.clip != unloaded)
                    {
                        audioSource.Stop();
                    }
                }
            }
        }
    }

    private IEnumerator StartGrabCheckAfterDelay(XRGrabInteractable m_XRGrabInteractableScript, GrabRequest m_GrabRequestScript)
    {
        yield return new WaitForSeconds(0.6f);
        if (m_GrabRequestScript != null)
        {
            m_GrabRequestScript.enabled = true;
        }
        if (m_XRGrabInteractableScript != null)
        {
            m_XRGrabInteractableScript.enabled = true;
        }
    }
}
