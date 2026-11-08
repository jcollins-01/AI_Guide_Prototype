using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Heat knob: every grip squeeze on it (or trigger pull with an empty hand near it) turns it one click,
// Off -> Low -> Medium -> High -> Off, with one click sound per step so the player can count the setting without seeing it
[RequireComponent(typeof(XRSimpleInteractable))]
public class StoveKnob : MonoBehaviour
{
    public static readonly string[] LevelNames = { "off", "low", "medium", "high" };

    [Range(0, 3)] public int level = 0;
    [Tooltip("The visible part of the knob, rotated a quarter turn per click.")]
    public Transform dial;
    [Tooltip("A trigger pull with an empty hand this close (m) to the knob turns it.")]
    public float triggerReach = 0.3f;

    public event Action<int> LevelChanged;
    public string LevelName => LevelNames[level];

    private AudioSource source;

    void Awake()
    {
        source = SoupFeedback.AddSource(gameObject);
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(args => Turn(args.interactorObject));
        // Easier to find by touch than the scene's 6 cm sphere
        if (TryGetComponent(out SphereCollider sphere)) sphere.radius = Mathf.Max(sphere.radius, 0.1f);
    }

    void Update()
    {
        // Only an empty hand: the hand holding the spice jar over the pot uses the trigger to shake it
        foreach (HandAssist.Hand hand in HandAssist.Hands)
            if (hand.TriggerPressed && hand.Held == null && Vector3.Distance(hand.Position, transform.position) < triggerReach)
            {
                Turn(hand.interactor);
                break;
            }
    }

    public void Turn(IXRInteractor hand = null)
    {
        level = (level + 1) % LevelNames.Length;
        if (dial != null) dial.localRotation = Quaternion.Euler(0, 90f * level, 0);

        // Clicks = new level (1 click for low, 2 for medium, 3 for high); off is one low clunk
        if (level == 0) SoupFeedback.Play(source, SoupFeedback.Clunk, 0.6f);
        for (int i = 0; i < level; i++) Invoke(nameof(ClickOnce), i * 0.12f);
        SoupFeedback.Pulse(hand, 0.25f + 0.2f * level, 0.08f);

        Debug.Log($"[Soup] Stove knob -> {LevelName}");
        LevelChanged?.Invoke(level);
    }

    private void ClickOnce() => SoupFeedback.Play(source, SoupFeedback.Click);
}
