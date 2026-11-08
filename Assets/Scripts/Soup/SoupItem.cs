using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Marks a grabbable object that the soup task cares about and tracks who is holding it
[RequireComponent(typeof(XRGrabInteractable))]
public class SoupItem : MonoBehaviour
{
    public enum Kind { Pot, Carrot, ChoppedCarrots, SpiceJar }
    public Kind kind;

    [HideInInspector] public bool everHeld = false;
    public event Action<SoupItem, IXRSelectInteractor> Grabbed;
    public event Action<SoupItem> Released;

    public XRGrabInteractable Grab { get; private set; }
    public Rigidbody Body { get; private set; }

    public bool IsHeld => Grab != null && Grab.isSelected;
    public IXRSelectInteractor LastHand { get; private set; } // the hand holding it, or that let go of it last

    void Awake()
    {
        Grab = GetComponent<XRGrabInteractable>();
        Body = GetComponent<Rigidbody>();
        Grab.selectEntered.AddListener(args =>
        {
            everHeld = true;
            LastHand = args.interactorObject;
            SoupFeedback.Pulse(args.interactorObject, 0.3f, 0.05f);
            Grabbed?.Invoke(this, args.interactorObject);
        });
        Grab.selectExited.AddListener(args =>
        {
            // A SnapSpot locks items in place; let go anywhere else, it falls and can be pushed again. This runs after the
            // grab interactable restored the Rigidbody, so it wins; a spot locks it again if it was let go over one.
            if (Body != null) Body.isKinematic = false;
            Released?.Invoke(this);
        });
    }

    // "right hand" / "left hand", from the name of the interactor's hierarchy (e.g. "Right Controller")
    public string HeldBy()
    {
        if (!IsHeld) return null;
        return HandName(Grab.interactorsSelecting[0].transform);
    }

    public static string HandName(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("right")) return "right hand";
            if (n.Contains("left")) return "left hand";
        }
        return "hand";
    }

    // Puts the object down at a spot (bottom of its bounds on the point), upright, keeping its heading
    public void PlaceAt(Vector3 surfacePoint)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        Bounds b = Bounds();
        transform.position += surfacePoint - new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.up * 0.002f;
        if (Body != null && !Body.isKinematic)
        {
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
    }

    public Bounds Bounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.1f);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
