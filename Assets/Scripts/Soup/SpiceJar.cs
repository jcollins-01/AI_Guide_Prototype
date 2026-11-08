using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Seasoning: hold the jar over the pot and shake it (a quick up-and-down flick), or pull the trigger. Every shake
// rattles; over the pot it also counts as a pinch with a pulse in the hand, and after a few pinches the spices are in.
[RequireComponent(typeof(SoupItem))]
public class SpiceJar : MonoBehaviour
{
    public SoupPot pot;
    public int pinchesNeeded = 3;
    [Tooltip("Vertical hand speed (m/s) that counts as a shake.")]
    public float shakeSpeed = 0.5f;

    public int Pinches { get; private set; }
    public bool OverPot { get; private set; }
    public event Action<int> Pinched;

    private SoupItem item;
    private AudioSource source;
    private Vector3 lastPosition;
    private int lastDirection = 0; // +1 moving up, -1 moving down
    private float lastShake = -1f;

    void Awake()
    {
        item = GetComponent<SoupItem>();
        source = SoupFeedback.AddSource(gameObject);
        // Directly, not via item.Grab: SoupItem.Awake may not have run yet
        GetComponent<XRGrabInteractable>().activated.AddListener(_ => Shake());
        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 position = transform.position;
        float vy = (position.y - lastPosition.y) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = position;

        if (!item.IsHeld || pot == null) { OverPot = false; lastDirection = 0; return; }

        Bounds b = pot.Item.Bounds();
        Vector3 d = position - new Vector3(b.center.x, b.max.y, b.center.z);
        OverPot = new Vector2(d.x, d.z).magnitude < Mathf.Max(b.extents.x, b.extents.z) + 0.1f && d.y > -0.02f && d.y < 0.45f;

        // An up-then-down flick is one shake
        if (vy > shakeSpeed) lastDirection = 1;
        else if (vy < -shakeSpeed)
        {
            if (lastDirection == 1) Shake();
            lastDirection = -1;
        }
    }

    public void Shake()
    {
        if (Time.time - lastShake < 0.2f) return;
        lastShake = Time.time;
        SoupFeedback.Play(source, SoupFeedback.Shake);
        if (!OverPot || pot == null || pot.spicesIn) return;

        Pinches++;
        SoupFeedback.Pulse(item.IsHeld ? item.Grab.interactorsSelecting[0] : null, 0.4f, 0.06f);
        Debug.Log($"[Soup] Spice pinch {Pinches}/{pinchesNeeded}");
        Pinched?.Invoke(Pinches);
        if (Pinches >= pinchesNeeded) pot.AddSpices();
    }
}
