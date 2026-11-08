using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Reaching into the carrot crate and squeezing grip puts a fresh carrot straight into that hand
[RequireComponent(typeof(XRSimpleInteractable))]
public class CarrotCrate : MonoBehaviour
{
    [Tooltip("Inactive carrot in the scene that gets copied.")]
    public SoupItem carrotTemplate;
    public int maxCarrots = 3;

    public event Action<SoupItem> CarrotTaken;

    private XRSimpleInteractable interactable;
    private AudioSource source;
    private int spawned = 0;

    void Awake()
    {
        source = SoupFeedback.AddSource(gameObject);
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(args => StartCoroutine(HandOver(args.interactorObject)));
    }

    // Swapping the selection from the crate to the carrot inside selectEntered itself confuses the interaction
    // manager, so wait a frame
    private IEnumerator HandOver(IXRSelectInteractor hand)
    {
        yield return null;
        SoupItem carrot = Spawn(hand.transform.position);
        if (carrot == null) yield break;

        var manager = interactable.interactionManager;
        if (manager != null)
        {
            if (interactable.isSelected) manager.SelectExit(hand, interactable);
            yield return null; // the carrot's Awake runs on activation; give the grab component a frame to register
            manager.SelectEnter(hand, carrot.Grab);
        }
    }

    public SoupItem Spawn(Vector3 position)
    {
        if (carrotTemplate == null || spawned >= maxCarrots) return null;
        spawned++;

        SoupItem carrot = Instantiate(carrotTemplate, position, carrotTemplate.transform.rotation);
        carrot.name = "Carrot";
        carrot.gameObject.SetActive(true);
        SoupFeedback.Play(source, SoupFeedback.Pop);
        Debug.Log("[Soup] Carrot taken from the crate");
        CarrotTaken?.Invoke(carrot);
        return carrot;
    }
}
