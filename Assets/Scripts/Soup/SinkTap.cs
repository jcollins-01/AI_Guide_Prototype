using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Sink tap: turns the water on or off with a grip squeeze on the tap, or a trigger pull with a hand anywhere at the sink
// (even the hand holding the pot), so it doesn't have to be found exactly. A pot let go in or above the sink snaps
// into place under the spout. While the water runs, a pot in the sink fills up; the water sound rises in pitch as it
// fills, so the player can hear when it is nearly full.
[RequireComponent(typeof(XRSimpleInteractable))]
public class SinkTap : MonoBehaviour
{
    public SoupPot pot;
    [Tooltip("World-space box above the sink basin where the pot gets filled.")]
    public Bounds sinkZone;
    public GameObject waterStream;
    [Tooltip("A trigger pull this close (m) to the tap, or over the sink, turns the water on or off.")]
    public float triggerReach = 0.5f;

    public bool Running { get; private set; }
    public bool PotInSink { get; private set; }
    public event Action<bool> RunningChanged;

    private AudioSource source;
    private AudioSource waterSource;
    private SnapSpot spot;

    void Awake()
    {
        source = SoupFeedback.AddSource(gameObject);
        waterSource = SoupFeedback.AddSource(gameObject, loop: true);
        waterSource.clip = SoupFeedback.Water;
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(args => Toggle(args.interactorObject));

        // Easier to find by touch than the scene's 7 cm sphere
        if (TryGetComponent(out SphereCollider sphere)) sphere.radius = Mathf.Max(sphere.radius, 0.12f);

        var spotObj = new GameObject("Sink Pot Spot");
        spotObj.transform.SetParent(transform, true);
        spotObj.transform.position = PotSpot();
        spot = spotObj.AddComponent<SnapSpot>();
        spot.radius = Mathf.Max(0.3f, Mathf.Min(sinkZone.extents.x, sinkZone.extents.z));

        if (waterStream != null) waterStream.SetActive(false);
    }

    void Start()
    {
        if (pot != null) spot.item = pot.Item;
    }

    // Straight below the spout, on the bottom of the basin (raycast down, ignoring the pot, the hands and the player)
    private Vector3 PotSpot()
    {
        Vector3 above = sinkZone.center;
        if (waterStream != null && waterStream.TryGetComponent(out Renderer r))
            above = new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z);
        int mask = ~((1 << 2) | (1 << 3) | (1 << 6) | (1 << 7));
        float floor = sinkZone.min.y;
        if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, 2f, mask, QueryTriggerInteraction.Ignore) && hit.point.y > floor)
            return hit.point;
        return new Vector3(above.x, floor, above.z);
    }

    public void Toggle(IXRInteractor hand = null)
    {
        Running = !Running;
        SoupFeedback.Play(source, SoupFeedback.Click);
        SoupFeedback.Pulse(hand, 0.3f, 0.08f);
        if (waterStream != null) waterStream.SetActive(Running);
        if (Running) waterSource.Play(); else waterSource.Stop();
        Debug.Log($"[Soup] Tap turned {(Running ? "on" : "off")}");
        RunningChanged?.Invoke(Running);
    }

    private bool InReach(Vector3 p)
    {
        Bounds sink = sinkZone;
        sink.Expand(new Vector3(0.3f, 0.2f, 0.3f));
        return Vector3.Distance(p, transform.position) < triggerReach || sink.Contains(p);
    }

    void Update()
    {
        foreach (HandAssist.Hand hand in HandAssist.Hands)
            if (hand.TriggerPressed && !hand.Holds<SpiceJar>() && InReach(hand.Position)) // the spice jar uses the trigger to shake
            {
                Toggle(hand.interactor);
                break;
            }

        PotInSink = pot != null && sinkZone.Contains(pot.Item.Bounds().center);
        if (!Running) return;

        if (PotInSink)
        {
            pot.Fill(Time.deltaTime);
            waterSource.pitch = 0.9f + 0.3f * pot.water; // fuller pot = higher pitch, like a real one
            waterSource.volume = pot.IsFull ? 0.15f : 0.25f;
        }
        else
        {
            waterSource.pitch = 0.9f;
            waterSource.volume = 0.2f;
        }
    }
}
