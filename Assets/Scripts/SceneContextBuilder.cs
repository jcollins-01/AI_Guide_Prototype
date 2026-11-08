using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Describes where the Navigation Registry objects are relative to the player, using what the engine knows exactly:
// positions and bounds, line of sight, and NavMesh walking routes. The text is sent to the guide with every question,
// so the vision model no longer has to work out positions from the screenshots (which small local models do badly).
public class SceneContextBuilder : MonoBehaviour
{
    [Tooltip("Half of the horizontal angle that counts as 'in view' (matches roughly what the player sees).")]
    public float viewHalfAngle = 50f;
    public int maxVisible = 8;
    public int maxNotVisible = 6;
    [Tooltip("Objects further than this are only listed if the player mentions them.")]
    public float nearbyRadius = 20f;
    [Tooltip("Objects closer than this are listed as 'right next to you' in any direction, even behind the player.")]
    public float nextToRadius = 1.5f;

    // Avatars and the rig shouldn't block line of sight: Ignore Raycast, Player, XR Rig, Person, NPC
    private const int LineOfSightMask = ~((1 << 2) | (1 << 3) | (1 << 6) | (1 << 11) | (1 << 12));
    // Words too common to identify an object when matching the player's question against registry names
    private static readonly HashSet<string> CommonWords = new HashSet<string> { "the", "kitchen", "city", "near", "area", "of", "by", "a" };

    // What the last Build() described, with positions, so PresentationCapture can draw it on a map
    public enum AnnotationKind { WithinReach, InView, NotInView, Guide }
    public struct Annotation
    {
        public string label;
        public Vector3 point;
        public AnnotationKind kind;
        public Vector3[] route; // walking route corners, when the context mentions one
    }
    public readonly List<Annotation> LastAnnotations = new List<Annotation>();
    public Vector3 LastHeadPosition { get; private set; }
    public Vector3 LastFacing { get; private set; }

    private OpenAIQueries m_OpenAIQueriesScript;
    private Dictionary<string, List<GameObject>> registryObjects;
    private bool loggedRegistry = false;

    private struct Entry
    {
        public string name;
        public GameObject obj;
        public float distance;
        public float angle; // signed yaw from the player's facing direction, + is right
        public float nearAngle; // same, but to the object's closest point (a long counter beside you is "on your right")
        public bool visible;
    }

    // Called by RealtimeGuideClient right before each question goes to Ollama
    public string Build(string playerQuestion)
    {
        Camera head = PlayerHead.Camera;
        if (head == null) return null;
        ResolveRegistry(); // every time: objects can appear during play (a carrot taken from the crate)
        if (registryObjects.Count == 0) return null;

        Vector3 headPos = head.transform.position;
        Vector3 facing = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
        string question = (playerQuestion ?? "").ToLowerInvariant();
        LastAnnotations.Clear();
        LastHeadPosition = headPos;
        LastFacing = facing;

        var entries = new List<Entry>();
        foreach (var pair in registryObjects)
        {
            // Several objects can share a name (e.g. trees); describe the closest one, like GetClosestObjectByName does
            GameObject closest = pair.Value.Where(o => o != null)
                .OrderBy(o => Vector3.Distance(GetBounds(o).ClosestPoint(headPos), headPos)).FirstOrDefault();
            if (closest == null) continue;

            Bounds b = GetBounds(closest);
            Vector3 nearest = b.ClosestPoint(headPos);
            Vector3 toCenter = Vector3.ProjectOnPlane(b.center - headPos, Vector3.up);
            entries.Add(new Entry
            {
                name = pair.Key,
                obj = closest,
                distance = Vector3.Distance(new Vector3(nearest.x, 0, nearest.z), new Vector3(headPos.x, 0, headPos.z)),
                angle = toCenter.sqrMagnitude < 0.01f ? 0f : Vector3.SignedAngle(facing, toCenter, Vector3.up),
                nearAngle = Vector3.SignedAngle(facing, Vector3.ProjectOnPlane(nearest - headPos, Vector3.up), Vector3.up),
                visible = IsVisible(closest, b, headPos, facing)
            });
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Scene Context] Exact positions computed by the game engine at the moment the player asked. " +
                      "Directions are relative to where the player is facing (12 o'clock = straight ahead). " +
                      "Trust this over the images for what is where; use the images for anything not listed here.");

        string inside = ContainingObject(entries, headPos);
        // The cooking task never leaves the kitchen; the building's bounds can miss a player standing against its wall
        if (inside == null && SoupTask.Instance != null && registryObjects.ContainsKey("Rosie's Kitchen")) inside = "Rosie's Kitchen";
        if (inside != null) sb.AppendLine($"Player is inside: {inside}" + (SoupTask.Instance != null ? " (the player is always inside the kitchen, never outside)" : ""));
        sb.AppendLine(RoomPosition(head.transform));

        GuideFollow guide = FindObjectOfType<GuideFollow>();
        if (guide != null)
        {
            Vector3 toGuide = Vector3.ProjectOnPlane(guide.transform.position - headPos, Vector3.up);
            sb.AppendLine($"You (the guide) are standing: {Direction(Vector3.SignedAngle(facing, toGuide, Vector3.up))}, {Meters(toGuide.magnitude)}");
            Annotate("Guide", guide.transform.position, Vector3.SignedAngle(facing, toGuide, Vector3.up), toGuide.magnitude, AnnotationKind.Guide);
        }

        // Within arm's reach, in any direction: replaces static labels like "Near kitchen prepping table" with
        // where it actually is ("on your right"). Things overhead (ceiling lights) are left to the lists below.
        var nextTo = entries.Where(e => e.name != inside && e.distance <= nextToRadius && GetBounds(e.obj).min.y < headPos.y)
            .OrderBy(e => e.distance).ToList();
        if (nextTo.Count > 0)
        {
            sb.AppendLine("Right next to the player (within reach):");
            foreach (var e in nextTo)
            {
                sb.AppendLine($"- {e.name}: {Beside(e.nearAngle)}, {Meters(e.distance)}");
                Annotate(e.name, GetBounds(e.obj).ClosestPoint(headPos), e.nearAngle, e.distance, AnnotationKind.WithinReach);
            }
        }

        var visible = entries.Where(e => e.visible && e.name != inside && !nextTo.Contains(e)).OrderBy(e => e.distance).Take(maxVisible).ToList();
        sb.AppendLine("In view, nearest first:");
        if (visible.Count == 0) sb.AppendLine("- nothing from the Registry");
        foreach (var e in visible)
        {
            sb.AppendLine($"- {e.name}: {Direction(e.angle)}, {Meters(e.distance)}");
            Annotate(e.name, GetBounds(e.obj).center, e.angle, e.distance, AnnotationKind.InView);
        }

        // Out of view: the nearest ones, plus anything the player mentioned no matter how far (with a walking route)
        var mentioned = entries.Where(e => Mentions(question, e.name)).ToList();
        var notVisible = entries.Where(e => !e.visible && e.name != inside && !nextTo.Contains(e) && e.distance <= nearbyRadius)
            .OrderBy(e => e.distance).Take(maxNotVisible)
            .Union(mentioned.Where(e => !e.visible && !nextTo.Contains(e)))
            .ToList();
        if (notVisible.Count > 0)
        {
            sb.AppendLine("Not in view:");
            foreach (var e in notVisible)
            {
                lastRoute = null;
                sb.AppendLine($"- {e.name}: {Direction(e.angle)}, {Meters(e.distance)}{Route(e, headPos, facing)}");
                Annotate(e.name, GetBounds(e.obj).center, e.angle, e.distance, AnnotationKind.NotInView, lastRoute);
            }
        }

        // Cooking task progress and hand directions, in scenes that have the soup task
        if (SoupTask.Instance != null)
            sb.AppendLine(SoupTask.Instance.ContextText(head.transform));

        return sb.ToString().TrimEnd();
    }

    // Every registry object with its name and bounds (for the hand's name button)
    public IEnumerable<(string name, GameObject obj, Bounds bounds)> RegistryObjects()
    {
        ResolveRegistry();
        foreach (var pair in registryObjects)
            foreach (GameObject obj in pair.Value)
                if (obj != null && obj.activeInHierarchy) yield return (pair.Key, obj, GetBounds(obj));
    }

    // Registry names come from the scene's entry in RoomDescriptions.json; names with no matching GameObject
    // (e.g. old location labels like "Near kitchen prepping table") are skipped and logged once.
    private void ResolveRegistry()
    {
        registryObjects = new Dictionary<string, List<GameObject>>();
        if (m_OpenAIQueriesScript == null) m_OpenAIQueriesScript = FindObjectOfType<OpenAIQueries>();
        if (m_OpenAIQueriesScript == null) return;

        var names = new HashSet<string>();
        foreach (string n in (m_OpenAIQueriesScript.objectNames ?? "").Split(','))
            if (n.Trim().Length > 0) names.Add(n.Trim());
        foreach (string entry in (m_OpenAIQueriesScript.objectClassifications ?? "").Split('|'))
            if (entry.Contains(':')) names.Add(entry.Split(':')[0].Trim());

        var all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in all)
        {
            if (!names.Contains(obj.name)) continue;
            if (!registryObjects.TryGetValue(obj.name, out var list)) registryObjects[obj.name] = list = new List<GameObject>();
            list.Add(obj);
        }

        if (loggedRegistry) return;
        loggedRegistry = true;
        var missing = names.Where(n => !registryObjects.ContainsKey(n)).ToList();
        Debug.Log($"[SceneContext] {registryObjects.Count} registry objects found in the scene" +
                  (missing.Count > 0 ? $"; no GameObject for: {string.Join(", ", missing)}" : ""));
    }

    private static Bounds GetBounds(GameObject obj)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null) return r.bounds;
        Collider c = obj.GetComponent<Collider>();
        if (c != null) return c.bounds;

        // Groups (e.g. a crate area) may only have renderers on their children
        Renderer[] children = obj.GetComponentsInChildren<Renderer>();
        if (children.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 0.2f);
        Bounds b = children[0].bounds;
        foreach (Renderer child in children) b.Encapsulate(child.bounds);
        return b;
    }

    // In view = inside the horizontal view angle AND a line from the eyes reaches the object without hitting a wall
    private bool IsVisible(GameObject obj, Bounds b, Vector3 headPos, Vector3 facing)
    {
        Vector3[] points = { b.center, b.ClosestPoint(headPos), b.center + Vector3.up * b.extents.y * 0.8f };
        foreach (Vector3 p in points)
        {
            Vector3 flat = Vector3.ProjectOnPlane(p - headPos, Vector3.up);
            if (flat.sqrMagnitude > 0.01f && Vector3.Angle(facing, flat) > viewHalfAngle) continue;

            if (!Physics.Linecast(headPos, p, out RaycastHit hit, LineOfSightMask, QueryTriggerInteraction.Ignore))
                return true;
            if (hit.transform.IsChildOf(obj.transform) || obj.transform.IsChildOf(hit.transform))
                return true;
        }
        return false;
    }

    // The smallest large registry object whose bounds contain the player, e.g. "Rosie's Kitchen"
    private static string ContainingObject(List<Entry> entries, Vector3 headPos)
    {
        string best = null;
        float bestVolume = float.MaxValue;
        foreach (var e in entries)
        {
            Bounds b = GetBounds(e.obj);
            float volume = b.size.x * b.size.y * b.size.z;
            if (volume > 30f && volume < bestVolume && b.Contains(headPos))
            {
                best = e.name;
                bestVolume = volume;
            }
        }
        return best;
    }

    // Where in the room the player stands: the nearest wall or furniture straight ahead, right, behind and left, at waist
    // height (counters) and chest height (walls, fridge), so the guide can say "you're in the middle" or "the wall is
    // right behind you"
    private string RoomPosition(Transform head)
    {
        Vector3 facing = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        var sides = new[] { ("ahead", facing), ("on the right", right), ("behind", -facing), ("on the left", -right) };
        const float range = 8f;

        var parts = new List<string>();
        string closestSide = null;
        float nearest = range;
        foreach (var (word, dir) in sides)
        {
            float d = range;
            foreach (float drop in new[] { 0.8f, 0.3f }) // waist and chest, below a standing player's eyes
                if (Physics.Raycast(head.position - Vector3.up * drop, dir, out RaycastHit hit, range, LineOfSightMask, QueryTriggerInteraction.Ignore))
                    d = Mathf.Min(d, hit.distance);
            parts.Add(d >= range ? $"{word} open" : $"{word} {(d < 1f ? $"{d:0.0} m" : Meters(d))}");
            if (d < nearest) { nearest = d; closestSide = word; }
        }

        string summary = nearest < 0.6f ? $"the player is at the edge of the room, right against something {closestSide}"
                       : nearest < 1.5f ? $"the player is near the side of the room, closest {closestSide}"
                       : "the player is in the open middle of the room";
        return $"Position in the room: {summary}. Nearest wall or furniture: {string.Join(", ", parts)}.";
    }

    // Walking route along the NavMesh, only mentioned when it is noticeably longer than the straight line
    // (walls in the way) -- then the first leg tells the guide which way to start walking.
    private string Route(Entry e, Vector3 headPos, Vector3 facing)
    {
        if (!NavMesh.SamplePosition(headPos, out NavMeshHit start, 2.5f, NavMesh.AllAreas)) return "";
        Bounds b = GetBounds(e.obj);
        if (!NavMesh.SamplePosition(b.ClosestPoint(headPos), out NavMeshHit end, Mathf.Max(3f, b.extents.magnitude), NavMesh.AllAreas)) return "";

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.corners.Length < 2) return "";

        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        if (length < e.distance * 1.3f + 1f) return "";

        lastRoute = path.corners;
        Vector3 firstLeg = Vector3.ProjectOnPlane(path.corners[1] - path.corners[0], Vector3.up);
        return $"; walking route {Meters(length)}, start by heading {Direction(Vector3.SignedAngle(facing, firstLeg, Vector3.up))}" +
               (path.status == NavMeshPathStatus.PathPartial ? " (route incomplete)" : "");
    }

    private Vector3[] lastRoute;

    private void Annotate(string name, Vector3 point, float angle, float distance, AnnotationKind kind, Vector3[] route = null)
    {
        string clock = Direction(angle);
        clock = clock.Substring(clock.IndexOf('(') + 1).TrimEnd(')');
        LastAnnotations.Add(new Annotation { label = name + "\n" + $"{Meters(distance)}, {clock}", point = point, kind = kind, route = route });
    }

    private static bool Mentions(string question, string name)
    {
        if (question.Length == 0) return false;
        string lower = name.ToLowerInvariant();
        if (question.Contains(lower)) return true;
        var words = lower.Split(' ', '-', '\'').Where(w => w.Length >= 3 && !CommonWords.Contains(w)).ToList();
        return words.Count > 0 && words.All(w => question.Contains(w));
    }

    // Word direction plus clock position (12 = straight ahead), the convention used in sighted guiding
    private static string Direction(float angle)
    {
        int clock = Mathf.RoundToInt(angle / 30f);
        clock = ((clock % 12) + 12) % 12;
        if (clock == 0) clock = 12;

        float a = Mathf.Abs(angle);
        string side = angle >= 0 ? "right" : "left";
        string word = a <= 20 ? "straight ahead"
                    : a <= 70 ? $"ahead to the {side}"
                    : a <= 110 ? $"to the {side}"
                    : a <= 160 ? $"behind to the {side}"
                    : "behind";
        return $"{word} ({clock} o'clock)";
    }

    // Wording for things within reach: "on your right" reads better than "to the right" when it is right there
    private static string Beside(float angle)
    {
        float a = Mathf.Abs(angle);
        string side = angle >= 0 ? "right" : "left";
        string word = a <= 20 ? "directly in front of you"
                    : a <= 70 ? $"in front of you, slightly to your {side}"
                    : a <= 110 ? $"on your {side}"
                    : a <= 160 ? $"behind you, to your {side}"
                    : "directly behind you";
        return $"{word} ({Direction(angle).Split('(')[1]}";
    }

    private static string Meters(float d) => d < 1f ? "within 1 m" : $"{Mathf.RoundToInt(d)} m";
}
