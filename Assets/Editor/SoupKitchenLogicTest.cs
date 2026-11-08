using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Plays the whole soup recipe in SoupKitchen.unity in Play mode, using the same calls as the debug keys, and checks
// that every step registers, in order, without errors from the soup scripts. Writes Benchmark/results/logic_<time>.md.
//   In the Editor: Tools > Soup Kitchen > Run Logic Test (the scene is not saved).
//   Headless (Unity closed):  Unity.exe -batchmode -projectPath <project> -executeMethod SoupKitchenLogicTest.RunBatch -logFile <log>
// Entering Play mode reloads all scripts, so the test state lives in SessionState and the runner restarts itself
// after the reload through [InitializeOnLoad].
[InitializeOnLoad]
public static class SoupKitchenLogicTest
{
    private const string ScenePath = "Assets/Scenes/SoupKitchen.unity";
    private const string RunningKey = "SoupLogicTest.Running", BatchKey = "SoupLogicTest.Batch", JarKey = "SoupLogicTest.AddedJar";

    private static int stage;
    private static double stageStart;
    private static SoupItem carrot, pile;
    private static readonly List<string> report = new List<string>();
    private static readonly List<string> ourErrors = new List<string>(), otherErrors = new List<string>();
    private static bool failed;

    static SoupKitchenLogicTest()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Begin();
        };
        if (EditorApplication.isPlaying) Begin();
    }

    [MenuItem("Tools/Soup Kitchen/Run Logic Test")]
    public static void RunInEditor() => Start(batch: false);

    public static void RunBatch() => Start(batch: true);

    private static void Start(bool batch)
    {
        if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);

        // Scenes built before the spice step have no jar yet: add one for this run only (the scene isn't saved)
        bool addedJar = UnityEngine.Object.FindObjectOfType<SpiceJar>() == null;
        if (addedJar) SoupKitchenBuilder.AddSpiceJarToOpenScene();

        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(BatchKey, batch);
        SessionState.SetBool(JarKey, addedJar);
        Debug.Log("[SoupTest] Entering Play mode");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        stage = 0;
        stageStart = EditorApplication.timeSinceStartup;
        report.Clear(); ourErrors.Clear(); otherErrors.Clear();
        failed = false;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        Debug.Log("[SoupTest] Running");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string line = message.Split('\n')[0];
        // Errors from the soup task or the guide's context code fail the test; others (network, XR, avatars) are listed
        bool ours = new[] { "Soup", "SceneContext", "GuideWelcome", "SpiceJar", "StoveBurner", "StoveKnob", "SinkTap", "CarrotCrate", "CuttingBoard" }
            .Any(n => stack.Contains(n) || message.Contains(n));
        (ours ? ourErrors : otherErrors).Add(line);
    }

    private static double Elapsed => EditorApplication.timeSinceStartup - stageStart;

    private static void Next(string passed)
    {
        report.Add($"- PASS {passed}");
        stage++;
        stageStart = EditorApplication.timeSinceStartup;
    }

    private static void Fail(string what)
    {
        report.Add($"- FAIL {what}");
        failed = true;
        Finish();
    }

    // Waits up to `timeout` seconds for `condition`, then passes or fails the stage
    private static void Expect(bool condition, string description, double timeout = 3)
    {
        if (condition) Next(description);
        else if (Elapsed > timeout) Fail(description);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        SoupTask task = SoupTask.Instance;
        try
        {
            switch (stage)
            {
                case 0:
                    if (task == null) { if (Elapsed > 10) Fail("SoupTask present in the scene"); return; }
                    if (Elapsed < 2) return; // let every Start() run
                    if (task.pot == null || task.burner == null || task.knob == null || task.tap == null || task.crate == null || task.board == null)
                    { Fail("SoupTask has all its references (pot, burner, knob, tap, crate, board)"); return; }
                    if (task.spiceJar == null) { Fail("a Spice Jar is in the scene"); return; }
                    task.pot.heatSeconds = 2f; // speed up the waits
                    task.quitWhenDone = false; // the test ends Play mode itself after writing the report
                    Next($"scene loaded with all soup objects{(SessionState.GetBool(JarKey, false) ? " (Spice Jar added for this run; the saved scene has none yet)" : "")}");
                    task.pot.Item.everHeld = true;
                    break;
                case 1: Expect(task.IsDone(SoupTask.Step.PickUpPot), "1 pick up the pot"); if (stage == 2) task.pot.Fill(100f); break;
                case 2: Expect(task.IsDone(SoupTask.Step.FillPot) && task.pot.IsFull, "2 fill the pot"); if (stage == 3) task.pot.Item.PlaceAt(task.burner.transform.position); break;
                case 3:
                    Expect(task.burner.PotOnBurner && task.IsDone(SoupTask.Step.PotOnStove), "3 pot snaps onto the burner");
                    if (stage == 4) { task.knob.Turn(); task.knob.Turn(); }
                    break;
                case 4:
                    Expect(task.knob.level == 2 && task.IsDone(SoupTask.Step.HeatMedium), "4 knob at medium");
                    if (stage == 5) carrot = task.crate.Spawn(task.crate.transform.position + Vector3.up * 0.4f);
                    break;
                case 5:
                    Expect(carrot != null && task.IsDone(SoupTask.Step.TakeCarrot), "5 carrot from the crate");
                    if (stage == 6) task.board.PlaceCarrot(carrot);
                    break;
                case 6:
                    Expect(task.board.Carrot != null && task.IsDone(SoupTask.Step.CarrotOnBoard), "6 carrot locks onto the cutting board");
                    if (stage == 7) for (int i = 0; i < task.board.chopsNeeded; i++) task.board.Chop();
                    break;
                case 7:
                    pile = UnityEngine.Object.FindObjectsByType<SoupItem>(FindObjectsSortMode.None).FirstOrDefault(s => s.kind == SoupItem.Kind.ChoppedCarrots);
                    Expect(pile != null && task.IsDone(SoupTask.Step.ChopCarrot), $"7 chopping makes a pile ({task.board.chopsNeeded} chops)");
                    if (stage == 8) task.pot.AddCarrots(pile);
                    break;
                case 8:
                    Expect(task.pot.carrotsIn && task.IsDone(SoupTask.Step.CarrotsInPot), "8 carrots into the pot");
                    if (stage == 9) task.pot.AddSpices();
                    break;
                case 9: Expect(task.pot.spicesIn && task.IsDone(SoupTask.Step.AddSpices), "9 spices in"); break;
                case 10: Expect(task.Finished, "10 everything in the pot: soup done, game ends", timeout: 3); break;
                case 11: Expect(task.pot.IsBoiling, "water boils on medium", timeout: 8); break;
                case 12:
                    string ctx = task.ContextText(PlayerHead.Camera != null ? PlayerHead.Camera.transform : null);
                    report.Add("");
                    report.Add("Final [Task] block:");
                    report.Add("```");
                    report.Add(ctx);
                    report.Add("```");
                    if (!ctx.Contains("10 of 10 steps done")) { Fail("[Task] block reports 10 of 10 steps"); return; }

                    var builder = UnityEngine.Object.FindObjectOfType<SceneContextBuilder>();
                    if (builder == null) builder = task.gameObject.AddComponent<SceneContextBuilder>();
                    string scene = builder.Build("where is the spice jar?");
                    report.Add("");
                    report.Add(scene == null ? "Scene context: none (no main camera in this run)" : "Scene context at the end:");
                    if (scene != null) { report.Add("```"); report.Add(scene); report.Add("```"); }
                    Next("[Task] block and scene context build");
                    Finish();
                    break;
            }
        }
        catch (Exception e)
        {
            ourErrors.Add($"{e.GetType().Name}: {e.Message}");
            Fail($"stage {stage} threw {e.GetType().Name}");
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (ourErrors.Count > 0) failed = true;

        var md = new List<string> { $"# Soup kitchen logic test, {DateTime.Now:yyyy-MM-dd HH:mm}", "", $"**{(failed ? "FAILED" : "PASSED")}**", "" };
        md.AddRange(report);
        md.Add("");
        md.Add($"## Errors from the soup / guide scripts ({ourErrors.Count})");
        md.AddRange(ourErrors.Count == 0 ? new[] { "- none" } : ourErrors.Distinct().Select(e => "- " + e));
        md.Add("");
        md.Add($"## Other errors during the run ({otherErrors.Count}, not counted as failures)");
        md.AddRange(otherErrors.Count == 0 ? new[] { "- none" } : otherErrors.Distinct().Take(30).Select(e => "- " + e));

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Benchmark", "results"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"logic_{DateTime.Now:yyyyMMdd_HHmmss}.md");
        File.WriteAllText(path, string.Join("\n", md));
        Debug.Log($"[SoupTest] {(failed ? "FAILED" : "PASSED")}. Report: {path}");

        bool batch = SessionState.GetBool(BatchKey, false);
        SessionState.EraseBool(RunningKey);
        if (batch) EditorApplication.Exit(failed ? 1 : 0);
        else EditorApplication.isPlaying = false;
    }
}
