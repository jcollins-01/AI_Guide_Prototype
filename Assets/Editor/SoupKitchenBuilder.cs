using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

// Tools > Soup Kitchen > Create Soup Kitchen Scene
// Copies Kitchen.unity to SoupKitchen.unity and adds the soup task to the copy: a grabbable pot, a burner and heat knob on
// the stove, a tap on the sink, a carrot crate that hands out carrots, a cutting board where they get chopped, and a
// spice jar for seasoning.
// Everything new sits under "===== SOUP TASK =====" and is placed relative to the existing kitchen objects, so it can
// be moved by hand afterwards. Kitchen.unity itself is not changed.
public static class SoupKitchenBuilder
{
    private const string SourceScene = "Assets/Scenes/Kitchen.unity";
    private const string TargetScene = "Assets/Scenes/SoupKitchen.unity";
    private const string MaterialFolder = "Assets/Materials/Soup";
    private const int InteractableLayer = 7; // same layer as the knives
    private const int IgnoreRaycastLayer = 2;

    private static List<GameObject> sceneObjects;

    [MenuItem("Tools/Soup Kitchen/Create Soup Kitchen Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(TargetScene))
        {
            if (!EditorUtility.DisplayDialog("Soup Kitchen", $"{TargetScene} already exists. Rebuild it from {SourceScene}? " +
                                             "Any changes you made to SoupKitchen.unity will be lost.", "Rebuild", "Cancel"))
                return;
            AssetDatabase.DeleteAsset(TargetScene);
        }
        if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
        {
            Debug.LogError($"[SoupBuilder] Could not copy {SourceScene} to {TargetScene}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        try
        {
            Build(scene);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Soup Kitchen", "Building the soup task failed: " + e.Message + "\nSee the Console.", "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(TargetScene);
        Debug.Log($"[SoupBuilder] Saved {TargetScene}. Press Play; keys 1-9 and 0 complete the steps for testing without a headset.");
    }

    private static void Build(Scene scene)
    {
        sceneObjects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();

        GameObject board = Pick("Cutting Board", null) ?? throw new Exception("No 'Cutting Board' with a renderer in the scene");
        GameObject oven = Pick("Oven", Center(board)) ?? throw new Exception("No 'Oven' with a renderer in the scene");
        GameObject sink = Pick("Sink", Center(oven)) ?? throw new Exception("No 'Sink' with a renderer in the scene");
        GameObject crate = Pick("Crate of Carrots", Center(board)) ?? throw new Exception("No 'Crate of Carrots' in the scene");
        GameObject potSource = Pick("Cooking Pots", Center(oven), exact: false);
        GameObject table = Pick("Kitchen Prep Table", Center(board));

        // Rough middle of the kitchen: "front" of the stove and sink is the side facing it
        var landmarks = new[] { oven, sink, board, crate, table }.Where(g => g != null).ToList();
        Vector3 roomCenter = landmarks.Aggregate(Vector3.zero, (sum, g) => sum + Center(g)) / landmarks.Count;

        GameObject root = new GameObject("===== SOUP TASK =====");
        Transform parent = root.transform;

        // --- Stove: burner on top, knob at the front edge
        Bounds ovenBounds = SurfaceBounds(oven);
        Vector3 ovenFront = SnapToAxis(oven.transform, roomCenter - ovenBounds.center);
        Vector3 ovenTop = new Vector3(ovenBounds.center.x, ovenBounds.max.y, ovenBounds.center.z);
        float ovenDepth = Mathf.Abs(Vector3.Dot(ovenBounds.extents, Abs(ovenFront)));

        var burnerObj = new GameObject("Stove Burner");
        burnerObj.transform.SetParent(parent);
        burnerObj.transform.position = ovenTop - ovenFront * Mathf.Min(0.05f, ovenDepth * 0.2f);
        burnerObj.layer = IgnoreRaycastLayer;
        GameObject glow = Prim(PrimitiveType.Cylinder, "Burner Ring", burnerObj.transform, burnerObj.transform.position + Vector3.up * 0.003f,
                               new Vector3(0.24f, 0.003f, 0.24f), Mat("Burner", new Color(0.15f, 0.15f, 0.15f)));
        var burner = burnerObj.AddComponent<StoveBurner>();
        burner.glow = glow.GetComponent<Renderer>();

        var knobObj = new GameObject("Stove Knob");
        knobObj.transform.SetParent(parent);
        knobObj.transform.position = ovenTop + ovenFront * Mathf.Max(0.02f, ovenDepth - 0.07f) + Vector3.up * 0.02f;
        knobObj.layer = InteractableLayer;
        knobObj.AddComponent<SphereCollider>().radius = 0.06f; // bigger than the knob so it's easy to find by touch
        GameObject dial = Prim(PrimitiveType.Cylinder, "Dial", knobObj.transform, knobObj.transform.position, new Vector3(0.07f, 0.02f, 0.07f), Mat("Knob", new Color(0.85f, 0.85f, 0.85f)));
        Prim(PrimitiveType.Cube, "Pointer", dial.transform, knobObj.transform.position + ovenFront * 0.025f + Vector3.up * 0.022f,
             new Vector3(0.012f, 0.008f, 0.03f), Mat("Knob Pointer", Color.red)).transform.rotation = Quaternion.LookRotation(ovenFront);
        knobObj.AddComponent<XRSimpleInteractable>();
        var knob = knobObj.AddComponent<StoveKnob>();
        knob.dial = dial.transform;
        burner.knob = knob;

        // --- Sink: tap at the back edge, filling zone above the basin
        Bounds sinkBounds = SurfaceBounds(sink);
        Vector3 sinkBack = -SnapToAxis(sink.transform, roomCenter - sinkBounds.center);
        float sinkDepth = Mathf.Abs(Vector3.Dot(sinkBounds.extents, Abs(sinkBack)));
        Vector3 sinkTop = new Vector3(sinkBounds.center.x, sinkBounds.max.y, sinkBounds.center.z);

        var tapObj = new GameObject("Sink Tap");
        tapObj.transform.SetParent(parent);
        tapObj.transform.position = sinkTop + sinkBack * Mathf.Max(0.02f, sinkDepth - 0.06f) + Vector3.up * 0.2f;
        tapObj.layer = InteractableLayer;
        tapObj.AddComponent<SphereCollider>().radius = 0.07f;
        Material metal = Mat("Tap", new Color(0.75f, 0.77f, 0.8f));
        Prim(PrimitiveType.Cylinder, "Tap Column", tapObj.transform, tapObj.transform.position - Vector3.up * 0.1f, new Vector3(0.035f, 0.1f, 0.035f), metal);
        Vector3 spout = tapObj.transform.position - sinkBack * 0.1f;
        Prim(PrimitiveType.Cube, "Spout", tapObj.transform, tapObj.transform.position - sinkBack * 0.05f, new Vector3(0.03f, 0.03f, 0.03f) + Abs(sinkBack) * 0.1f, metal);
        float streamLength = spout.y - sinkBounds.min.y;
        GameObject stream = Prim(PrimitiveType.Cylinder, "Water Stream", tapObj.transform, spout - Vector3.up * streamLength / 2f,
                                 new Vector3(0.02f, streamLength / 2f, 0.02f), Mat("Water", new Color(0.35f, 0.6f, 1f)));
        tapObj.AddComponent<XRSimpleInteractable>();
        var tap = tapObj.AddComponent<SinkTap>();
        tap.waterStream = stream;
        tap.sinkZone = new Bounds(new Vector3(sinkBounds.center.x, (sinkBounds.min.y + sinkBounds.max.y + 0.45f) / 2f, sinkBounds.center.z),
                                  new Vector3(sinkBounds.size.x + 0.1f, sinkBounds.size.y + 0.45f, sinkBounds.size.z + 0.1f));

        // --- Pot: a grabbable copy of the kitchen's cooking pot (or a plain cylinder if that mesh is a whole group of pots)
        SoupPot pot = BuildPot(parent, potSource, oven);
        burner.pot = pot;
        tap.pot = pot;

        // --- Carrots: template carrot handed out by the crate, chopped pile made by the board
        SoupItem carrot = BuildCarrot(parent, Center(crate) + Vector3.up * 0.5f);
        SoupItem chopped = BuildChoppedCarrots(parent, Center(board) + Vector3.up * 0.5f);

        Bounds crateBounds = SurfaceBounds(crate);
        var crateObj = new GameObject("Crate of Carrots Grab Area");
        crateObj.transform.SetParent(parent);
        crateObj.transform.position = new Vector3(crateBounds.center.x, crateBounds.max.y - 0.03f, crateBounds.center.z);
        crateObj.layer = InteractableLayer;
        crateObj.AddComponent<BoxCollider>().size = new Vector3(crateBounds.size.x * 0.9f, 0.1f, crateBounds.size.z * 0.9f);
        crateObj.AddComponent<XRSimpleInteractable>();
        var crateComp = crateObj.AddComponent<CarrotCrate>();
        crateComp.carrotTemplate = carrot;

        Bounds boardBounds = SurfaceBounds(board);
        var boardObj = new GameObject("Cutting Board Zone");
        boardObj.transform.SetParent(parent);
        boardObj.transform.position = new Vector3(boardBounds.center.x, boardBounds.max.y, boardBounds.center.z);
        boardObj.layer = IgnoreRaycastLayer;
        var boardComp = boardObj.AddComponent<CuttingBoard>();
        boardComp.boardZone = new Bounds(new Vector3(boardBounds.center.x, boardBounds.max.y + 0.08f, boardBounds.center.z),
                                         new Vector3(boardBounds.size.x, 0.16f, boardBounds.size.z));
        boardComp.choppedTemplate = chopped;

        // --- Task manager
        var task = new GameObject("Soup Task").AddComponent<SoupTask>();
        task.transform.SetParent(parent);
        task.pot = pot;
        task.burner = burner;
        task.knob = knob;
        task.tap = tap;
        task.crate = crateComp;
        task.board = boardComp;
        task.spiceJar = BuildSpiceJar(parent, pot, burner);

        Selection.activeGameObject = root;
        Debug.Log($"[SoupBuilder] Stove: '{NameOf(oven)}' (front faces {ovenFront}), sink: '{NameOf(sink)}', board: '{NameOf(board)}', " +
                  $"crate: '{NameOf(crate)}', pot copied from: '{(potSource != null ? NameOf(potSource) : "none, built a cylinder")}'. " +
                  "If something sits in the wrong place, move it under ===== SOUP TASK =====.");
    }

    // Tools > Soup Kitchen > Add Spice Jar To Open Scene: for a soup scene built before the spice step existed, so it
    // doesn't have to be rebuilt (which would undo any objects moved by hand)
    [MenuItem("Tools/Soup Kitchen/Add Spice Jar To Open Scene")]
    public static void AddSpiceJarToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        SoupTask task = UnityEngine.Object.FindObjectOfType<SoupTask>();
        if (task == null || task.pot == null || task.burner == null)
        {
            EditorUtility.DisplayDialog("Soup Kitchen", "Open the soup scene first (it needs a Soup Task with a pot and a burner).", "OK");
            return;
        }
        if (UnityEngine.Object.FindObjectOfType<SpiceJar>() != null)
        {
            EditorUtility.DisplayDialog("Soup Kitchen", "This scene already has a Spice Jar.", "OK");
            return;
        }

        sceneObjects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
        task.spiceJar = BuildSpiceJar(task.transform.parent, task.pot, task.burner);
        EditorUtility.SetDirty(task);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = task.spiceJar.gameObject;
        Debug.Log("[SoupBuilder] Added a Spice Jar next to the stove. Save the scene (Ctrl+S) to keep it.");
    }

    // A small jar on the counter next to the stove, so seasoning happens right where the pot is
    private static SpiceJar BuildSpiceJar(Transform parent, SoupPot pot, StoveBurner burner)
    {
        Vector3 spot;
        GameObject counter = Pick("Oven Countertop", burner.transform.position);
        if (counter != null)
        {
            Bounds cb = SurfaceBounds(counter);
            spot = new Vector3(cb.center.x, cb.max.y, cb.center.z);
        }
        else
            spot = burner.transform.position + Vector3.ProjectOnPlane(burner.transform.right, Vector3.up).normalized * 0.35f;

        GameObject jar = Prim(PrimitiveType.Cylinder, "Spice Jar", parent, spot + Vector3.up * 0.06f, new Vector3(0.06f, 0.05f, 0.06f),
                              Mat("Spice Jar", new Color(0.7f, 0.25f, 0.12f)), keepCollider: true);
        Prim(PrimitiveType.Cylinder, "Lid", jar.transform, spot + Vector3.up * 0.115f, new Vector3(0.065f, 0.008f, 0.065f), Mat("Spice Lid", new Color(0.8f, 0.8f, 0.78f)));
        jar.layer = InteractableLayer;
        MakeGrabbable(jar, 0.2f);
        jar.AddComponent<SoupItem>().kind = SoupItem.Kind.SpiceJar;
        var spiceJar = jar.AddComponent<SpiceJar>();
        spiceJar.pot = pot;

        // Dark specks on the soup surface once seasoned
        if (pot.spicesVisual == null)
        {
            Bounds b = pot.GetComponent<Renderer>().bounds;
            var specks = new GameObject("Spices In Pot");
            specks.transform.SetParent(pot.transform, false);
            Material brown = Mat("Spice", new Color(0.35f, 0.18f, 0.08f));
            for (int i = 0; i < 8; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Min(b.size.x, b.size.z) * 0.3f;
                Prim(PrimitiveType.Cube, "Speck", specks.transform, new Vector3(b.center.x + r.x, b.min.y + b.size.y * 0.88f, b.center.z + r.y), Vector3.one, brown);
            }
            foreach (Transform speck in specks.transform) SetWorldScale(speck, new Vector3(0.012f, 0.004f, 0.012f));
            specks.SetActive(false);
            pot.spicesVisual = specks;
            EditorUtility.SetDirty(pot);
        }
        return spiceJar;
    }

    private static SoupPot BuildPot(Transform parent, GameObject source, GameObject oven)
    {
        GameObject pot;
        Bounds sb = source != null ? source.GetComponent<Renderer>().bounds : default;
        if (source != null && Mathf.Max(sb.size.x, sb.size.y, sb.size.z) < 0.7f)
        {
            pot = UnityEngine.Object.Instantiate(source, parent);
            pot.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            pot.transform.localScale = source.transform.lossyScale;
            source.SetActive(false); // the grabbable copy replaces it
        }
        else
        {
            Bounds ob = SurfaceBounds(oven);
            pot = Prim(PrimitiveType.Cylinder, "Pot", parent, new Vector3(ob.center.x, ob.max.y + 0.1f, ob.center.z) + oven.transform.right * 0.5f,
                       new Vector3(0.26f, 0.09f, 0.26f), Mat("Pot", new Color(0.55f, 0.55f, 0.6f)), keepCollider: true);
        }
        pot.name = "Soup Pot";
        SetLayer(pot, InteractableLayer);
        if (pot.GetComponent<Collider>() == null) pot.AddComponent<BoxCollider>();

        MakeGrabbable(pot, 1.5f);
        pot.AddComponent<SoupItem>().kind = SoupItem.Kind.Pot;
        var soupPot = pot.AddComponent<SoupPot>();

        // Water surface inside the pot, raised as it fills
        Bounds b = pot.GetComponent<Renderer>().bounds;
        float width = Mathf.Min(b.size.x, b.size.z) * 0.8f;
        GameObject water = Prim(PrimitiveType.Cylinder, "Water", pot.transform, b.center, Vector3.one, Mat("Pot Water", new Color(0.3f, 0.55f, 0.95f)));
        SetWorldScale(water.transform, new Vector3(width, 0.005f, width));
        soupPot.waterVisual = water.transform;
        soupPot.waterEmptyLocal = pot.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.1f, b.center.z));
        soupPot.waterFullLocal = pot.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.85f, b.center.z));

        var carrots = new GameObject("Carrots In Pot");
        carrots.transform.SetParent(pot.transform, false);
        Material orange = Mat("Carrot", new Color(1f, 0.5f, 0.1f));
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * width * 0.35f;
            Prim(PrimitiveType.Cylinder, "Piece", carrots.transform, new Vector3(b.center.x + r.x, b.min.y + b.size.y * 0.87f, b.center.z + r.y), Vector3.one, orange);
        }
        foreach (Transform piece in carrots.transform) SetWorldScale(piece, new Vector3(0.03f, 0.006f, 0.03f));
        carrots.SetActive(false);
        soupPot.carrotsVisual = carrots;
        return soupPot;
    }

    private static SoupItem BuildCarrot(Transform parent, Vector3 position)
    {
        GameObject carrot = Prim(PrimitiveType.Capsule, "Carrot (template)", parent, position, new Vector3(0.045f, 0.07f, 0.045f), Mat("Carrot", new Color(1f, 0.5f, 0.1f)), keepCollider: true);
        carrot.transform.rotation = Quaternion.Euler(0, 0, 90); // lying down
        Prim(PrimitiveType.Cube, "Leaves", carrot.transform, position + Vector3.right * 0.075f, new Vector3(0.025f, 0.03f, 0.025f), Mat("Carrot Leaves", new Color(0.2f, 0.65f, 0.2f)));
        carrot.layer = InteractableLayer;
        MakeGrabbable(carrot, 0.15f);
        carrot.AddComponent<SoupItem>().kind = SoupItem.Kind.Carrot;
        carrot.SetActive(false);
        return carrot.GetComponent<SoupItem>();
    }

    private static SoupItem BuildChoppedCarrots(Transform parent, Vector3 position)
    {
        var pile = new GameObject("Chopped Carrots (template)");
        pile.transform.SetParent(parent);
        pile.transform.position = position;
        pile.layer = InteractableLayer;
        var box = pile.AddComponent<BoxCollider>();
        box.size = new Vector3(0.12f, 0.03f, 0.09f);
        box.center = new Vector3(0, 0.015f, 0);
        Material orange = Mat("Carrot", new Color(1f, 0.5f, 0.1f));
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Cylinder, "Piece", pile.transform, position + new Vector3(-0.045f + i * 0.018f, 0.008f, (i % 2) * 0.03f - 0.015f), new Vector3(0.03f, 0.006f, 0.03f), orange);
        MakeGrabbable(pile, 0.2f);
        pile.AddComponent<SoupItem>().kind = SoupItem.Kind.ChoppedCarrots;
        pile.SetActive(false);
        return pile.GetComponent<SoupItem>();
    }

    // Same setup as the kitchen's knives: Rigidbody + XRGrabInteractable + GrabRequest (grab sound)
    private static void MakeGrabbable(GameObject obj, float mass)
    {
        var body = obj.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var grab = obj.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true; // held where the hand closed on it, not snapped to its centre
        grab.throwOnDetach = false;   // letting go puts it down instead of flinging it
        obj.AddComponent<GrabRequest>();
    }

    // --- Helpers ---------------------------------------------------------------------------------------------

    private static GameObject Pick(string name, Vector3? near, bool exact = true) =>
        sceneObjects.Where(g => (exact ? g.name == name : g.name.StartsWith(name)) && g.activeInHierarchy && g.GetComponent<Renderer>() != null)
            .OrderBy(g => near.HasValue ? Vector3.Distance(Center(g), near.Value) : 0f).FirstOrDefault();

    private static Vector3 Center(GameObject g) => g.GetComponent<Renderer>().bounds.center;

    // Collider bounds are what things physically rest on; fall back to the mesh
    private static Bounds SurfaceBounds(GameObject g)
    {
        Collider c = g.GetComponent<Collider>();
        return c != null && c.enabled ? c.bounds : g.GetComponent<Renderer>().bounds;
    }

    // The object's own horizontal axis closest to a direction, so the knob and tap line up with the furniture
    private static Vector3 SnapToAxis(Transform t, Vector3 direction)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        var axes = new[] { t.forward, -t.forward, t.right, -t.right }.Select(a => Vector3.ProjectOnPlane(a, Vector3.up).normalized);
        return axes.OrderByDescending(a => Vector3.Dot(a, direction)).First();
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 worldScale, Material material, bool keepCollider = false)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        if (!keepCollider) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, true);
        g.transform.position = position;
        SetWorldScale(g.transform, worldScale);
        g.GetComponent<Renderer>().sharedMaterial = material;
        g.layer = parent != null ? parent.gameObject.layer : 0;
        return g;
    }

    private static void SetWorldScale(Transform t, Vector3 worldScale)
    {
        Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(worldScale.x / p.x, worldScale.y / p.y, worldScale.z / p.z);
    }

    private static void SetLayer(GameObject g, int layer)
    {
        foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // Saved as assets: materials created in code and only referenced by a scene would be lost on reload
    private static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Soup");
        string path = $"{MaterialFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    private static string NameOf(GameObject g) => g.transform.parent != null ? $"{g.transform.parent.name}/{g.name}" : g.name;

    private static void AddToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
