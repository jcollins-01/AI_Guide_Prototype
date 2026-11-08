using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

// Helps a blind player find and use objects with their hands:
// - B (right) / Y (left): the guide says the name of the object nearest that hand ("This is the soup pot. It's empty.")
// - Vibration that starts before the hand touches a grabbable or a handle and pulses faster the closer it gets, like a
//   parking sensor, then turns into a steady buzz once the object is close enough to grab
// - Grab assist: squeezing grip with an empty hand near a grabbable or handle takes the nearest one, even if the hand is
//   not quite touching it
// It also tracks the buttons of both hands so the soup handles (tap, knob) can react to the trigger nearby.
// Only objects on the Interactable layer (7) are grabbed or vibrate; Key Items keep their own CollisionHaptics.
[DefaultExecutionOrder(-50)] // before the scripts that read the button presses this frame
public class HandAssist : MonoBehaviour
{
    public class Hand
    {
        public string name;  // "left hand" / "right hand"
        public XRNode node;
        public XRBaseControllerInteractor interactor;
        public bool TriggerPressed { get; internal set; } // went down this frame
        public bool GripPressed { get; internal set; }
        public bool NamePressed { get; internal set; }
        internal bool triggerWas, gripWas, nameWas, inGrabRange;
        internal float nextPulse;

        public Vector3 Position => interactor.transform.position;
        public IXRSelectInteractable Held => interactor.hasSelection ? interactor.interactablesSelected[0] : null;
        public bool Holds<T>() where T : Component => Held != null && Held.transform.GetComponent<T>() != null;
    }

    public static HandAssist Instance { get; private set; }
    public static IReadOnlyList<Hand> Hands => Instance != null ? Instance.hands : (IReadOnlyList<Hand>)new Hand[0];

    [Tooltip("The name button only names things within this distance (m) of the hand.")]
    public float nameRange = 0.5f;
    [Tooltip("Vibration starts when a hand comes this close (m) to a grabbable or handle.")]
    public float hapticRange = 0.45f;
    [Tooltip("Squeezing grip takes an object this close (m), even without touching it.")]
    public float grabRange = 0.2f;
    public bool proximityHaptics = true;
    public bool grabAssist = true;

    private const int InteractableLayer = 7;
    private readonly List<Hand> hands = new List<Hand>();
    private List<XRBaseInteractable> targets = new List<XRBaseInteractable>();
    private float nextRefresh = 0f;
    private float lastNameTime = -10f;
    private RealtimeGuideClient guide;
    private SceneContextBuilder context;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (Time.time >= nextRefresh) Refresh(); // objects appear during play (carrots from the crate)

        foreach (Hand hand in hands)
        {
            if (hand.interactor == null || !hand.interactor.isActiveAndEnabled) { nextRefresh = 0f; continue; }
            ReadButtons(hand);

            if (hand.NamePressed) NameNearest(hand);
            if (hand.GripPressed && grabAssist) StartCoroutine(AssistGrab(hand));
            if (proximityHaptics) Buzz(hand);
        }
    }

    private void Refresh()
    {
        nextRefresh = Time.time + 1f;
        targets = FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None)
            .Where(i => i.gameObject.layer == InteractableLayer && i.colliders.Count > 0).ToList();

        if (hands.Count == 2 && hands.All(h => h.interactor != null && h.interactor.isActiveAndEnabled)) return;
        hands.Clear();
        // The direct (touch) interactor of each hand on the player rig; fall back to any interactor of that hand
        var interactors = FindObjectsByType<XRBaseControllerInteractor>(FindObjectsSortMode.None).Where(i => i.isActiveAndEnabled)
            .OrderByDescending(i => i is XRDirectInteractor).ToList();
        foreach (var (handName, node) in new[] { ("left hand", XRNode.LeftHand), ("right hand", XRNode.RightHand) })
        {
            var interactor = interactors.FirstOrDefault(i => SoupItem.HandName(i.transform) == handName);
            if (interactor != null) hands.Add(new Hand { name = handName, node = node, interactor = interactor });
        }
    }

    private static void ReadButtons(Hand hand)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand.node);
        device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
        device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
        device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool name); // B on the right controller, Y on the left
        hand.TriggerPressed = trigger && !hand.triggerWas;
        hand.GripPressed = grip && !hand.gripWas;
        hand.NamePressed = name && !hand.nameWas;
        hand.triggerWas = trigger; hand.gripWas = grip; hand.nameWas = name;
    }

    // --- Distances ---------------------------------------------------------------------------------------------

    private static float Distance(Collider c, Vector3 point)
    {
        // ClosestPoint only works on convex colliders
        bool exact = !(c is MeshCollider mesh) || mesh.convex;
        return Vector3.Distance(point, exact ? c.ClosestPoint(point) : c.bounds.ClosestPoint(point));
    }

    private static float Distance(XRBaseInteractable target, Vector3 point)
    {
        float best = float.MaxValue;
        foreach (Collider c in target.colliders)
            if (c != null && c.enabled && c.gameObject.activeInHierarchy) best = Mathf.Min(best, Distance(c, point));
        return best;
    }

    // Nearest grabbable or handle the hand could take now (not held by someone else, and not in the hand already)
    private XRBaseInteractable NearestTarget(Hand hand, out float distance)
    {
        XRBaseInteractable best = null;
        distance = float.MaxValue;
        foreach (XRBaseInteractable target in targets)
        {
            if (target == null || !target.isActiveAndEnabled) continue;
            if (target is IXRSelectInteractable s && s.isSelected) continue;
            if ((target.interactionLayers & hand.interactor.interactionLayers) == 0) continue;
            float d = Distance(target, hand.Position);
            if (d < distance) { distance = d; best = target; }
        }
        return best;
    }

    // --- Proximity vibration -----------------------------------------------------------------------------------

    private void Buzz(Hand hand)
    {
        if (hand.interactor.hasSelection) { hand.inGrabRange = false; return; } // holding something: stay quiet
        NearestTarget(hand, out float d);
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand.node);

        bool inGrab = d <= grabRange;
        if (inGrab && !hand.inGrabRange) device.SendHapticImpulse(0, 0.8f, 0.12f); // "you can grab it now"
        hand.inGrabRange = inGrab;
        if (d > hapticRange || Time.time < hand.nextPulse) return;

        if (inGrab)
        {
            device.SendHapticImpulse(0, 0.35f, 0.12f); // steady buzz, renewed before it ends
            hand.nextPulse = Time.time + 0.1f;
        }
        else
        {
            float t = (d - grabRange) / (hapticRange - grabRange); // 0 = almost in reach, 1 = edge of the range
            device.SendHapticImpulse(0, Mathf.Lerp(0.5f, 0.15f, t), 0.04f);
            hand.nextPulse = Time.time + Mathf.Lerp(0.08f, 0.45f, t);
        }
    }

    // --- Grab assist -------------------------------------------------------------------------------------------

    private IEnumerator AssistGrab(Hand hand)
    {
        // Give the interactor its own chance first (a frame for the grip to register, one for the selection)
        yield return null;
        yield return null;
        if (hand.interactor == null || hand.interactor.hasSelection) yield break;
        if (!InputDevices.GetDeviceAtXRNode(hand.node).TryGetFeatureValue(CommonUsages.gripButton, out bool held) || !held) yield break;

        XRBaseInteractable target = NearestTarget(hand, out float d);
        if (target == null || d > grabRange || !(target is IXRSelectInteractable selectable)) yield break;
        XRInteractionManager manager = target.interactionManager != null ? target.interactionManager : hand.interactor.interactionManager;
        if (manager == null) yield break;

        Debug.Log($"[HandAssist] {hand.name} takes {target.name} ({d:0.00} m away)");
        manager.SelectEnter((IXRSelectInteractor)hand.interactor, selectable);
    }

    // --- Naming ------------------------------------------------------------------------------------------------

    private void NameNearest(Hand hand)
    {
        if (Time.time - lastNameTime < 0.5f) return;
        lastNameTime = Time.time;
        InputDevices.GetDeviceAtXRNode(hand.node).SendHapticImpulse(0, 0.2f, 0.03f); // the press was noticed

        if (guide == null) guide = FindObjectOfType<RealtimeGuideClient>();
        if (guide == null) return;

        string text;
        GameObject obj = NearestNamed(hand.Position, out string name);
        if (obj == null)
            text = $"There's nothing within reach of your {hand.name}.";
        else
        {
            string state = SoupTask.Instance != null ? SoupTask.Instance.Describe(obj) : null;
            text = $"This is the {name}." + (string.IsNullOrEmpty(state) ? "" : " " + state);
        }
        Debug.Log($"[HandAssist] {hand.name} name button: \"{text}\"");
        _ = guide.SpeakCustomText(text);
    }

    // The nearest object to a point among the scene's registry objects and the grabbables, preferring small things
    // (with the hand on a counter next to the pot, that's the pot, not the counter)
    private GameObject NearestNamed(Vector3 point, out string name)
    {
        var candidates = new List<(GameObject obj, string name, float distance, float size)>();

        if (context == null) context = FindObjectOfType<SceneContextBuilder>();
        if (context != null)
            foreach (var (n, obj, bounds) in context.RegistryObjects())
                candidates.Add((obj, n, Vector3.Distance(point, bounds.ClosestPoint(point)), Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z)));

        foreach (XRBaseInteractable target in targets)
            if (target != null && target.isActiveAndEnabled && !candidates.Any(c => c.obj == target.gameObject))
            {
                Bounds b = target.colliders[0].bounds;
                candidates.Add((target.gameObject, CleanName(target.name), Distance(target, point), Mathf.Max(b.size.x, b.size.y, b.size.z)));
            }

        var best = candidates.Where(c => c.distance <= nameRange && c.size < 4f)
            .OrderBy(c => c.distance + 0.15f * Mathf.Min(c.size, 2f)).FirstOrDefault();
        name = best.obj != null ? Spoken(best.name) : null;
        return best.obj;
    }

    // "Knife (1)" -> "Knife", "Carrot(Clone)" -> "Carrot", "Crate of Carrots Grab Area" -> "Crate of Carrots"
    private static string CleanName(string n) => Regex.Replace(n, @"\s*\((?:\d+|Clone|template)\)|\s+Grab Area$", "").Trim();

    // "Soup Pot" -> "soup pot"; names with an apostrophe are proper names and keep their capitals
    private static string Spoken(string n) => n.Contains("'") ? n : n.ToLowerInvariant();
}
