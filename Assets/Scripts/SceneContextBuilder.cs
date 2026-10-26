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

    private OpenAIQueries m_OpenAIQueriesScript;
    private Dictionary<string, List<GameObject>> registryObjects;

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
        Camera head = Camera.main;
        if (head == null) return null;
        if (registryObjects == null) ResolveRegistry();
        if (registryObjects.Count == 0) return null;

        Vector3 headPos = head.transform.position;
        Vector3 facing = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
        string question = (playerQuestion ?? "").ToLowerInvariant();

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
        if (inside != null) sb.AppendLine($"Player is inside: {inside}");

        GuideFollow guide = FindObjectOfType<GuideFollow>();
        if (guide != null)
        {
            Vector3 toGuide = Vector3.ProjectOnPlane(guide.transform.position - headPos, Vector3.up);
            sb.AppendLine($"You (the guide) are standing: {Direction(Vector3.SignedAngle(facing, toGuide, Vector3.up))}, {Meters(toGuide.magnitude)}");
        }

        // Within arm's reach, in any direction: replaces static labels like "Near kitchen prepping table" with
        // where it actually is ("on your right"). Things overhead (ceiling lights) are left to the lists below.
        var nextTo = entries.Where(e => e.name != inside && e.distance <= nextToRadius && GetBounds(e.obj).min.y < headPos.y)
            .OrderBy(e => e.distance).ToList();
        if (nextTo.Count > 0)
        {
            sb.AppendLine("Right next to the player (within reach):");
            foreach (var e in nextTo)
                sb.AppendLine($"- {e.name}: {Beside(e.nearAngle)}, {Meters(e.distance)}");
        }

        var visible = entries.Where(e => e.visible && e.name != inside && !nextTo.Contains(e)).OrderBy(e => e.distance).Take(maxVisible).ToList();
        sb.AppendLine("In view, nearest first:");
        if (visible.Count == 0) sb.AppendLine("- nothing from the Registry");
        foreach (var e in visible)
            sb.AppendLine($"- {e.name}: {Direction(e.angle)}, {Meters(e.distance)}");

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
                sb.AppendLine($"- {e.name}: {Direction(e.angle)}, {Meters(e.distance)}{Route(e, headPos, facing)}");
        }

        return sb.ToString().TrimEnd();
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

        Vector3 firstLeg = Vector3.ProjectOnPlane(path.corners[1] - path.corners[0], Vector3.up);
        return $"; walking route {Meters(length)}, start by heading {Direction(Vector3.SignedAngle(facing, firstLeg, Vector3.up))}" +
               (path.status == NavMeshPathStatus.PathPartial ? " (route incomplete)" : "");
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
