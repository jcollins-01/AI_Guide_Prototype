using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Press F12 in Play mode to save figures that show what the guide gets told, to DebugCaptures/presentation/<time>/:
//   viewpoint.png          what the player is looking at
//   map_close.png          top-down map, player facing up, with lines to every object the scene context mentions
//   map_wide.png           the same further out, with walking routes
//   scene_context.txt      the exact text the guide receives for this spot
// Lines: blue = within reach, green = in view, orange = not in view (dashed route = NavMesh walking route), purple = guide.
// Installs itself in every scene; nothing has to be added to the scene.
public class PresentationCapture : MonoBehaviour
{
    public KeyCode captureKey = KeyCode.F12;
    [Tooltip("Half the height of the close map in metres.")]
    public float closeMapSize = 4f;
    public float wideMapSize = 11f;
    public int width = 1600, height = 1200;

    private static readonly Color Reach = new Color(0.2f, 0.55f, 1f);
    private static readonly Color InView = new Color(0.15f, 0.8f, 0.3f);
    private static readonly Color NotInView = new Color(1f, 0.55f, 0.1f);
    private static readonly Color GuideColor = new Color(0.65f, 0.35f, 0.95f);

    private readonly List<GameObject> temporary = new List<GameObject>();
    private Material lineMaterial;
    private Font font;
    private bool busy = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("Presentation Capture");
        DontDestroyOnLoad(go);
        go.AddComponent<PresentationCapture>();
    }

    void Update()
    {
        if (Input.GetKeyDown(captureKey) && !busy) StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        busy = true;
        Camera head = PlayerHead.Camera;
        if (head == null) { Debug.LogWarning("[Presentation] No main camera yet"); busy = false; yield break; }

        SceneContextBuilder builder = FindObjectOfType<SceneContextBuilder>();
        if (builder == null) builder = gameObject.AddComponent<SceneContextBuilder>();
        string context = builder.Build("");
        if (context == null) { Debug.LogWarning("[Presentation] Scene context is empty (no registry objects?)"); busy = false; yield break; }

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DebugCaptures", "presentation", DateTime.Now.ToString("yyyyMMdd_HHmmss")));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "scene_context.txt"), context);

        yield return new WaitForEndOfFrame();

        // The player's view first, before any annotation exists in the world
        var cam = new GameObject("Presentation Camera").AddComponent<Camera>();
        cam.enabled = false;
        cam.CopyFrom(head);
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.fieldOfView = 80f;
        Save(cam, Path.Combine(dir, "viewpoint.png"));

        Vector3 headPos = builder.LastHeadPosition;
        Vector3 facing = builder.LastFacing;
        float drawHeight = headPos.y + 0.25f; // above counters, below the (hidden) ceiling

        // Everything above head height (ceilings, lamps, roofs) is hidden while the maps render
        var hidden = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.bounds.min.y > headPos.y + 0.4f && Vector3.Distance(Flat(r.bounds.center), Flat(headPos)) < wideMapSize * 2f).ToList();
        foreach (Renderer r in hidden) r.enabled = false;

        try
        {
            DrawAnnotations(builder, headPos, facing, drawHeight, labelScale: closeMapSize);
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            // Looking straight down with the player's facing direction pointing up in the image
            cam.transform.SetPositionAndRotation(headPos + Vector3.up * 25f, Quaternion.LookRotation(Vector3.down, facing));
            cam.orthographicSize = closeMapSize;
            Save(cam, Path.Combine(dir, "map_close.png"));

            ClearAnnotations();
            DrawAnnotations(builder, headPos, facing, drawHeight, labelScale: wideMapSize * 0.7f);
            cam.orthographicSize = wideMapSize;
            Save(cam, Path.Combine(dir, "map_wide.png"));
        }
        finally
        {
            ClearAnnotations();
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            Destroy(cam.gameObject);
        }

        Debug.Log($"[Presentation] Saved viewpoint, maps and scene context to {dir}\n{context}");
        busy = false;
    }

    private void DrawAnnotations(SceneContextBuilder builder, Vector3 headPos, Vector3 facing, float y, float labelScale)
    {
        Vector3 origin = new Vector3(headPos.x, y, headPos.z);
        float lineWidth = labelScale * 0.012f;

        // Player: reach circle, facing arrow and "12 o'clock" mark
        Circle(origin, builder.nextToRadius, Reach, lineWidth * 0.7f);
        Line(new[] { origin, origin + facing * 1.2f }, Color.red, lineWidth * 2.5f);
        Line(new[] { origin + facing * 1.2f, origin + facing * 0.9f + Vector3.Cross(Vector3.up, facing) * 0.2f }, Color.red, lineWidth * 2.5f);
        Line(new[] { origin + facing * 1.2f, origin + facing * 0.9f - Vector3.Cross(Vector3.up, facing) * 0.2f }, Color.red, lineWidth * 2.5f);
        Label("YOU", origin - facing * 0.35f, Color.white, labelScale * 0.9f, facing);
        Label("12", origin + facing * 1.45f, Color.red, labelScale * 0.8f, facing);

        foreach (var a in builder.LastAnnotations)
        {
            Color c = a.kind == SceneContextBuilder.AnnotationKind.WithinReach ? Reach
                    : a.kind == SceneContextBuilder.AnnotationKind.InView ? InView
                    : a.kind == SceneContextBuilder.AnnotationKind.Guide ? GuideColor : NotInView;
            Vector3 target = new Vector3(a.point.x, y, a.point.z);

            if (a.route != null && a.route.Length > 1)
            {
                Line(new[] { origin, target }, c * 0.6f, lineWidth * 0.5f);
                Dashed(a.route.Select(p => new Vector3(p.x, y + 0.01f, p.z)).ToArray(), c, lineWidth);
            }
            else
                Line(new[] { origin, target }, c, lineWidth);

            Dot(target, c, lineWidth * 4f);
            Label(a.label, target + facing * 0.15f, c, labelScale * 0.6f, facing);
        }
    }

    // --- Drawing helpers: temporary world objects that only exist while the map camera renders

    private Material LineMaterial()
    {
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        return lineMaterial;
    }

    private void Line(Vector3[] points, Color color, float width)
    {
        var go = new GameObject("Annotation Line");
        temporary.Add(go);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = LineMaterial();
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = width;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 2;
    }

    private void Dashed(Vector3[] points, Color color, float width)
    {
        float dash = width * 6f;
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            int n = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(a, b) / (dash * 2f)));
            for (int k = 0; k < n; k++)
            {
                Vector3 s = Vector3.Lerp(a, b, (float)k / n);
                Line(new[] { s, Vector3.MoveTowards(s, b, dash) }, color, width);
            }
        }
    }

    private void Circle(Vector3 center, float radius, Color color, float width)
    {
        var pts = Enumerable.Range(0, 49).Select(i => center + Quaternion.Euler(0, i * 7.5f, 0) * Vector3.forward * radius).ToArray();
        Line(pts, color, width);
    }

    private void Dot(Vector3 at, Color color, float size)
    {
        var pts = Enumerable.Range(0, 13).Select(i => at + Quaternion.Euler(0, i * 30f, 0) * Vector3.forward * size * 0.25f).ToArray();
        Line(pts, color, size * 0.5f);
    }

    private void Label(string text, Vector3 at, Color color, float scale, Vector3 facing)
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // Dark copy slightly offset as a shadow, so labels stay readable on any floor
        foreach (bool shadow in new[] { true, false })
        {
            var go = new GameObject("Annotation Label");
            temporary.Add(go);
            go.transform.position = at + (shadow ? new Vector3(0.02f, -0.01f, -0.02f) * scale : Vector3.zero);
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, facing); // lies flat, readable from the map camera
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = scale * 0.012f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = shadow ? new Color(0, 0, 0, 0.85f) : color;
            go.GetComponent<MeshRenderer>().material = font.material;
        }
    }

    private void ClearAnnotations()
    {
        // Immediately, not at the end of the frame: the wide map is rendered right after the close one is cleared
        foreach (GameObject go in temporary) if (go != null) DestroyImmediate(go);
        temporary.Clear();
    }

    private void Save(Camera cam, string path)
    {
        var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.ResetAspect();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Destroy(rt);
        Destroy(tex);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
}
