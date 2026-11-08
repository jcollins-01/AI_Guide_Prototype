using System;
using UnityEngine;

// A carrot let go over the cutting board clicks into place and stays put. Bringing a held knife down onto it chops
// it (one chop sound per hit); after enough chops it turns into a pile of pieces that can be picked up.
public class CuttingBoard : MonoBehaviour
{
    [Tooltip("World-space box above the board where a released carrot gets placed.")]
    public Bounds boardZone;
    public int chopsNeeded = 4;
    [Tooltip("Inactive chopped-carrot pile in the scene that gets copied.")]
    public SoupItem choppedTemplate;

    public SoupItem Carrot { get; private set; } // the carrot lying on the board, if any
    public int Chops { get; private set; }
    public bool KnifeHeld { get; private set; }

    public event Action CarrotPlaced;
    public event Action<int> Chopped; // number of chops so far
    public event Action<SoupItem> CarrotFinished; // the pile of pieces

    private AudioSource source;
    private bool knifeTouching = false;
    private float lastChop = -1f;

    void Awake() => source = SoupFeedback.AddSource(gameObject);

    void Update()
    {
        if (Carrot == null) LookForCarrot();
        else CheckKnife();
    }

    private void LookForCarrot()
    {
        foreach (Collider c in Physics.OverlapBox(boardZone.center, boardZone.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            SoupItem item = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<SoupItem>() : null;
            if (item == null || item.kind != SoupItem.Kind.Carrot || item.IsHeld) continue;

            // Lock it to the board so knife hits can't knock it away
            Carrot = item;
            Chops = 0;
            item.Grab.enabled = false;
            item.Body.isKinematic = true;
            item.PlaceAt(new Vector3(boardZone.center.x, boardZone.min.y, boardZone.center.z));
            SoupFeedback.Play(source, SoupFeedback.Clunk, 0.5f);
            SoupFeedback.Pulse(null, 0.4f, 0.1f);
            Debug.Log("[Soup] Carrot placed on the cutting board");
            CarrotPlaced?.Invoke();
            return;
        }
    }

    private void CheckKnife()
    {
        Bounds b = Carrot.Bounds();
        bool touching = false;
        KnifeHeld = false;
        foreach (Collider c in Physics.OverlapBox(b.center, b.extents + Vector3.one * 0.02f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            Rigidbody body = c.attachedRigidbody;
            if (body == null || !body.name.StartsWith("Knife")) continue;
            var grab = body.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
            if (grab != null && grab.isSelected) { touching = true; KnifeHeld = true; }
        }

        // One chop per time the knife comes down onto the carrot (it has to lift off in between)
        if (touching && !knifeTouching && Time.time - lastChop > 0.25f)
            Chop();
        knifeTouching = touching;
    }

    public void Chop()
    {
        if (Carrot == null) return;
        lastChop = Time.time;
        Chops++;
        SoupFeedback.Play(source, SoupFeedback.Chop);
        SoupFeedback.Pulse(null, 0.7f, 0.06f);
        Debug.Log($"[Soup] Chop {Chops}/{chopsNeeded}");
        Chopped?.Invoke(Chops);

        if (Chops < chopsNeeded) return;

        Vector3 spot = Carrot.transform.position;
        Destroy(Carrot.gameObject);
        Carrot = null;
        Chops = 0;

        if (choppedTemplate == null) return;
        SoupItem pile = Instantiate(choppedTemplate, spot, choppedTemplate.transform.rotation);
        pile.name = "Chopped Carrots";
        pile.gameObject.SetActive(true);
        pile.PlaceAt(new Vector3(boardZone.center.x, boardZone.min.y, boardZone.center.z));
        SoupFeedback.Play(source, SoupFeedback.Pop);
        CarrotFinished?.Invoke(pile);
    }

    // Debug key path: put a carrot on the board without physics
    public void PlaceCarrot(SoupItem carrot)
    {
        carrot.PlaceAt(new Vector3(boardZone.center.x, boardZone.min.y, boardZone.center.z));
    }
}
