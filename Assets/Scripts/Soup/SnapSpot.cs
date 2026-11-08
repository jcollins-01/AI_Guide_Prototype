using System;
using UnityEngine;

// A spot an item clicks into when it is let go nearby: the pot onto the burner, or into the sink under the tap. A blind
// player can't line the pot up by eye, so releasing it anywhere above or around the spot is enough; it lands upright on
// the spot with a clunk and a pulse in the hand that let go.
public class SnapSpot : MonoBehaviour
{
    public SoupItem item;
    [Tooltip("Snaps when the item's bottom is released within this horizontal distance (m) of the spot.")]
    public float radius = 0.4f;
    [Tooltip("...and at most this high (m) above it.")]
    public float height = 0.6f;
    [Tooltip("Once on the spot, the item can't be pushed off by other objects, only picked up.")]
    public bool lockInPlace = true;

    public bool Occupied { get; private set; }
    public event Action<bool> OccupiedChanged;

    private AudioSource source;

    void Awake() => source = SoupFeedback.AddSource(gameObject);

    void Update()
    {
        bool on = false;
        // Only things the player has handled: the pot starts out near the stove and shouldn't jump onto the burner
        if (item != null && item.isActiveAndEnabled && item.everHeld && !item.IsHeld)
        {
            Bounds b = item.Bounds();
            Vector3 offset = new Vector3(b.center.x, b.min.y, b.center.z) - transform.position;
            if (new Vector2(offset.x, offset.z).magnitude < radius && offset.y > -0.15f && offset.y < height)
            {
                // Snap once when it arrives (not every frame, so physics can settle it)
                if (!Occupied)
                {
                    item.PlaceAt(transform.position);
                    if (lockInPlace && item.Body != null) item.Body.isKinematic = true; // SoupItem frees it when picked up and let go
                    SoupFeedback.Play(source, SoupFeedback.Clunk);
                    SoupFeedback.Pulse(item.LastHand, 0.6f, 0.15f);
                }
                on = true;
            }
        }
        if (on != Occupied)
        {
            Occupied = on;
            OccupiedChanged?.Invoke(on);
        }
    }
}
