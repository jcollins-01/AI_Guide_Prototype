using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// The carrot soup task: which steps are done, what can be done next, and what the guide should know about it.
// The guide, as the player's chef, suggests the recipe order: pot, water, stove; the carrot while the water heats;
// carrots and spices into the pot; simmer. Steps only depend on each other where cooking really needs it (water before
// the stove, chopping before adding the carrots), so the player can also do the carrot first.
public class SoupTask : MonoBehaviour
{
    public static SoupTask Instance { get; private set; }

    public SoupPot pot;
    public StoveBurner burner;
    public StoveKnob knob;
    public SinkTap tap;
    public CarrotCrate crate;
    public CuttingBoard board;
    public SpiceJar spiceJar;

    [Tooltip("Close the game after the closing words, once everything is in the pot.")]
    public bool quitWhenDone = true;
    [TextArea(2, 5)]
    public string endingLine = "Congratulations, chef! Everything is in the pot, and now the soup just has to simmer. Our carrot soup " +
        "is done! Thank you so much for taking part in this research project. The game will close now. Goodbye!";

    public enum TeleportMode { Off, InsideKitchen, Anywhere }
    [Tooltip("The kitchen is small enough to walk: teleporting is off by default, or can be kept inside the kitchen.")]
    public TeleportMode teleport = TeleportMode.Off;
    [Tooltip("The guide speaks on its own when a step is done or something goes wrong.")]
    public bool proactiveGuide = true;
    [Tooltip("Number keys 1-9 and 0 complete the steps, for testing without a headset.")]
    public bool enableDebugKeys = true;
    public bool showOverlay = true;
    [Tooltip("Writes step times, mistakes and guide replies to DebugCaptures/soup_runs.")]
    public bool logRun = true;

    public enum Step { PickUpPot, FillPot, PotOnStove, HeatMedium, TakeCarrot, CarrotOnBoard, ChopCarrot, CarrotsInPot, AddSpices, Simmer }

    private static readonly string[] StepText =
    {
        "pick up the Soup Pot",
        "fill the pot with water at the Sink (hold it in the sink and turn on the Sink Tap)",
        "put the full pot on the Stove Burner",
        "turn the Stove Knob to medium heat",
        "take a Carrot from the Crate of Carrots",
        "put the carrot on the Cutting Board",
        "chop the carrot with a Knife",
        "put the Chopped Carrots into the pot",
        "season the soup: hold the Spice Jar over the pot and shake it",
        "let the soup simmer (happens by itself once everything is in the pot)"
    };

    private static readonly Step[][] Needs =
    {
        new Step[0],
        new[] { Step.PickUpPot },
        new[] { Step.FillPot },
        new[] { Step.PotOnStove },
        new Step[0],
        new[] { Step.TakeCarrot },
        new[] { Step.CarrotOnBoard },
        new[] { Step.ChopCarrot },
        new[] { Step.PotOnStove },
        new[] { Step.HeatMedium, Step.CarrotsInPot, Step.AddSpices }
    };

    public const string PromptRules =
        "\n\nCOOKING TASK: In this kitchen you are also the player's chef for today: warm, encouraging and practical, like a " +
        "cooking teacher standing next to them. Together you cook carrot soup, in this order: pick up the Soup Pot; fill it with " +
        "water at the Sink (hold the pot in the sink and turn on the Sink Tap); put it on the Stove Burner; turn the Stove Knob to " +
        "medium. The water then takes a while to heat, so while it heats, have the player prepare the carrot: take a Carrot from " +
        "the Crate of Carrots; put it on the Cutting Board; chop it with a Knife; put the Chopped Carrots into the pot. Then " +
        "season it with the Spice Jar. Once everything is in the pot the soup simmers by itself and the game ends. " +
        "If the player does things in a different order, that is fine. " +
        "Coach the player one step at a time and let them do it themselves. The player is ALWAYS inside this kitchen: never " +
        "say they are outside, on the roof or need to go outside; the top-down map image is taken from above the roof and is not " +
        "where they are. When they ask where they are, use the 'Position in the room' line: say if they are in the middle of " +
        "the kitchen or against a wall or counter, and on which side. Use the [Task] block for what is done and what is " +
        "next, and the [Scene Context] block for where things are: say the direction and distance, and when hand directions " +
        "are given, guide the hand the last bit (e.g. 'move your right hand 20 centimetres to the left'). " +
        "HOW THINGS WORK: grab objects with the grip button; a controller vibrates faster as it gets close to something it can " +
        "grab, and buzzes steadily once it's close enough. B (right) or Y (left) makes you say the name of the object next to " +
        "that hand. Letting go of the pot anywhere over the burner or the sink makes it click into place. To use the Stove Knob, " +
        "squeeze grip on it or pull the trigger with an empty hand next to it: each turn is one click (off, low, medium, high, " +
        "then off again; one click sound for low, two for medium, three for high). To turn the water on or off, pull the " +
        "trigger with a hand anywhere at the sink (the hand holding the pot works too) or squeeze grip on the Sink Tap; the " +
        "pot fills in a few seconds and the water sound rises as it fills. To take a carrot, put a hand into the Crate of Carrots and " +
        "squeeze grip. To chop, hold a Knife and bring it down onto the carrot on the board several times. To season, hold " +
        "the Spice Jar over the pot and shake it up and down (or pull the trigger); each shake rattles, and it takes exactly " +
        "three shakes over the pot, which the [Task] block counts. The soup is only ready when everything is in the pot. " +
        "The player is blind: never say 'you see' or ask them to look; describe things by direction, distance and touch. " +
        "The bracketed blocks ([Task], [Scene Context], [Game Event]) are for you only: never read them out or mention them. " +
        "Warn the player about the hot burner and the knife blade. A message starting with [Game Event] is something that just " +
        "happened in the game, not the player speaking: react in one or two short sentences, confirm it and say what to do next.";

    private static readonly int StepCount = Enum.GetValues(typeof(Step)).Length;
    private readonly bool[] done = new bool[StepCount];
    private readonly float[] doneAt = new float[StepCount];
    private float startTime;
    private SoupItem looseCarrot;
    private SoupItem choppedPile;
    private RealtimeGuideClient guide;
    private VRHandling vr;
    private bool introQueued = false;
    private bool wasAskPressed = false;
    private int questions = 0, mistakes = 0;
    private float lastHazardWarning = -100f, highHeatSince = -1f;
    private bool warnedEmptyPot = false, warnedBoiling = false;
    private string logPath;

    public bool IsDone(Step s) => done[(int)s];
    public bool Finished => done[(int)Step.Simmer];

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        startTime = Time.time;
        if (crate != null) crate.CarrotTaken += c => looseCarrot = c;
        if (board != null) board.CarrotFinished += p => choppedPile = p;
        if (knob != null) knob.LevelChanged += OnKnobChanged;
        if (spiceJar == null) spiceJar = FindObjectOfType<SpiceJar>();
        if (HandAssist.Instance == null) gameObject.AddComponent<HandAssist>(); // name button, proximity vibration, grab assist
        if (pot != null) pot.Boiling += () => { Log("event", "water boiling"); Announce("The water in the pot is boiling now."); };
        SetUpTeleport();

        if (logRun)
        {
            string dir = Path.Combine(Application.dataPath, "..", "DebugCaptures", "soup_runs");
            Directory.CreateDirectory(dir);
            logPath = Path.GetFullPath(Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
            File.WriteAllText(logPath, "seconds,type,detail\n");
            Debug.Log($"[Soup] Logging this run to {logPath}");
        }
    }

    void Update()
    {
        FindGuide();
        CheckSteps();
        CheckHazards();
        CountQuestions();
        if (enableDebugKeys) HandleDebugKeys();
    }

    // --- Steps -----------------------------------------------------------------------------------------------

    private void CheckSteps()
    {
        if (pot == null) return;
        bool onStove = burner != null && burner.PotOnBurner;

        if (pot.Item.everHeld) Complete(Step.PickUpPot);
        if (pot.IsFull) Complete(Step.FillPot);
        if (onStove && pot.HasWater && IsDone(Step.FillPot)) Complete(Step.PotOnStove);
        if (onStove && knob != null && knob.level == 2 && IsDone(Step.PotOnStove)) Complete(Step.HeatMedium);
        if (looseCarrot != null || (board != null && board.Carrot != null) || choppedPile != null || pot.carrotsIn) Complete(Step.TakeCarrot);
        if ((board != null && board.Carrot != null) || choppedPile != null || pot.carrotsIn) Complete(Step.CarrotOnBoard);
        if (choppedPile != null || pot.carrotsIn) Complete(Step.ChopCarrot);
        if (pot.carrotsIn) Complete(Step.CarrotsInPot);
        if (pot.spicesIn) Complete(Step.AddSpices);
        if (OnlySimmerLeft) Complete(Step.Simmer); // everything is in the pot: the soup simmers by itself, and we're done

        // Cooking: the water heats up on a lit burner, then bubbles louder with the heat
        int heat = onStove && knob != null ? knob.level : 0;
        pot.HeatUp(Time.deltaTime, heat);
        pot.SetBubbling(!pot.HasWater || heat == 0 ? 0f : pot.IsBoiling ? heat * 0.7f : pot.temperature * 0.3f);
    }

    private bool OnlySimmerLeft => Enumerable.Range(0, StepCount).All(i => done[i] || i == (int)Step.Simmer);

    private void Complete(Step step)
    {
        if (done[(int)step]) return;
        done[(int)step] = true;
        doneAt[(int)step] = Time.time - startTime;
        Debug.Log($"[Soup] Step done: {step} at {doneAt[(int)step]:F1}s");
        Log("step", step.ToString());

        if (step == Step.Simmer)
        {
            if (pot != null) SoupFeedback.Play(pot.GetComponent<AudioSource>(), SoupFeedback.Done);
            StartCoroutine(EndGame());
        }
        else if (OnlySimmerLeft)
        {
            // The last ingredient: no "step done" reply, the closing words come right after
        }
        else if (step == Step.HeatMedium && !pot.IsBoiling)
            Announce($"Step done: {StepText[(int)step]}. The water needs about {pot.heatSeconds:F0} seconds to heat up; " +
                     "suggest preparing the carrot in the meantime.");
        else
            Announce($"Step done: {StepText[(int)step]}.");
    }

    // Fixed closing words (every participant hears the same), then the game closes
    private IEnumerator EndGame()
    {
        proactiveGuide = false; // no more step replies over the goodbye
        Log("event", "game finished");
        yield return new WaitForSeconds(1f); // let the "done" chime play first

        if (guide != null && guide._isConnected)
        {
            _ = guide.SpeakCustomText(endingLine);
            yield return null;
            float waited = 0f;
            while (guide._isAiSpeaking && waited < 60f) { waited += Time.deltaTime; yield return null; }
        }
        else
            Debug.LogWarning("[Soup] Guide not connected; closing without the spoken goodbye");

        if (!quitWhenDone) yield break;
        yield return new WaitForSeconds(2f);
        Debug.Log("[Soup] Soup finished, closing the game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetUpTeleport()
    {
        Bounds kitchen = default;
        bool haveKitchen = false;
        GameObject building = GameObject.Find("Rosie's Kitchen");
        if (building != null)
            foreach (Renderer r in building.GetComponentsInChildren<Renderer>())
            {
                if (!haveKitchen) { kitchen = r.bounds; haveKitchen = true; }
                else kitchen.Encapsulate(r.bounds);
            }
        if (haveKitchen) kitchen.Expand(new Vector3(-1f, 0f, -1f)); // keep landing spots half a metre off the walls

        foreach (TeleportationProvider provider in FindObjectsByType<TeleportationProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (provider is CustomTeleportationProvider custom)
            {
                custom.teleportEnabled = teleport != TeleportMode.Off;
                custom.limitToArea = teleport == TeleportMode.InsideKitchen && haveKitchen;
                custom.allowedArea = kitchen;
            }
            else if (teleport == TeleportMode.Off)
                provider.enabled = false;
        }
        if (teleport == TeleportMode.InsideKitchen && !haveKitchen)
            Debug.LogWarning("[Soup] No 'Rosie's Kitchen' object to limit teleporting to; teleporting is unlimited");
        Debug.Log($"[Soup] Teleporting: {teleport}");
    }

    private void OnKnobChanged(int level)
    {
        Log("knob", StoveKnob.LevelNames[level]);
        if (level != 3) { highHeatSince = -1f; warnedBoiling = false; }
        if (level == 0) warnedEmptyPot = false;
    }

    // --- Hazards and mistakes --------------------------------------------------------------------------------

    private void CheckHazards()
    {
        if (burner == null || knob == null || pot == null) return;
        bool onStove = burner.PotOnBurner;

        // Heat on under an empty pot
        if (knob.level > 0 && onStove && !pot.HasWater && !warnedEmptyPot)
        {
            warnedEmptyPot = true;
            Mistake("heat on with an empty pot", "Careful, the pot on the stove is empty. Turn the knob back to off, then fill the pot at the sink first.");
        }

        // Too hot: boils over after a few seconds on high
        if (knob.level == 3 && onStove && pot.HasWater)
        {
            if (highHeatSince < 0) highHeatSince = Time.time;
            if (!warnedBoiling && Time.time - highHeatSince > 4f)
            {
                warnedBoiling = true;
                Mistake("boiling over on high heat", "It's boiling over, that's too hot. Squeeze the knob once to turn it off, then twice more for medium.");
            }
        }

        // A hand right above a hot, empty burner
        if (knob.level > 0 && !onStove && Time.time - lastHazardWarning > 8f)
        {
            foreach (Transform hand in Hands())
            {
                Vector3 d = hand.position - burner.transform.position;
                if (new Vector2(d.x, d.z).magnitude < 0.2f && d.y > -0.05f && d.y < 0.3f)
                {
                    lastHazardWarning = Time.time;
                    Mistake("hand over hot burner", $"Careful, your {SoupItem.HandName(hand)} is right above the hot burner.");
                    break;
                }
            }
        }
    }

    // Warnings are spoken straight away from a fixed sentence: a 2-3 s model round trip is too slow for "hot burner"
    private void Mistake(string what, string warning)
    {
        mistakes++;
        Debug.Log($"[Soup] Mistake: {what}");
        Log("mistake", what);
        if (guide != null && guide._isConnected) _ = guide.SpeakCustomText(warning);
    }

    // --- Guide ------------------------------------------------------------------------------------------------

    private void FindGuide()
    {
        if (guide == null)
        {
            guide = FindObjectOfType<RealtimeGuideClient>();
            if (guide != null) guide.OnTextReceived += text => Log("guide", text);
        }
        if (vr == null) vr = FindObjectOfType<VRHandling>();

        // Introduce the task once the guide is connected, the player has arrived and the welcome (controls) is over
        if (!introQueued && guide != null && guide._isConnected && PlayerHead.Camera != null && Time.time - startTime > 3f && GuideWelcome.Done)
        {
            introQueued = true;
            // Fixed text computed from where the player stands, so the first directions are always right
            if (pot != null && !IsDone(Step.PickUpPot))
            {
                Log("event", "first step instructions");
                _ = guide.SpeakCustomText(FirstStepText(PlayerHead.Camera.transform));
            }
            else
                Announce("You have just welcomed the player, explained the controls and given the recipe. As their chef, tell them the " +
                         "first step and where to go, in one or two sentences.");
        }
    }

    // "Let's start! First we need the pot. It's 3 metres away, on the counter next to the stove. Push the right stick to the
    // left twice to face it. Then push the left stick forward and walk about 2 metres. ..."
    private string FirstStepText(Transform head)
    {
        Vector3 target = pot.Item.Bounds().center;
        Vector3 to = Vector3.ProjectOnPlane(target - head.position, Vector3.up);
        float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward, Vector3.up), to, Vector3.up);
        float distance = to.magnitude;
        string side = angle >= 0 ? "right" : "left";
        string where = burner != null && Vector3.Distance(target, burner.transform.position) < 1.5f ? ", on the counter next to the stove" : "";
        string Metres(float m) { int n = Mathf.Max(1, Mathf.RoundToInt(m)); return n == 1 ? "1 metre" : $"{n} metres"; }

        var sb = new StringBuilder("Let's start! First we need the pot. ");
        if (distance < 0.8f)
            sb.Append($"It's right next to you, {(Mathf.Abs(angle) < 30f ? "in front of you" : $"on your {side}")}{where}. ");
        else
        {
            sb.Append($"It's {Metres(distance)} away{where}. ");
            if (Mathf.Abs(angle) < 25f) sb.Append("It's straight ahead of you. ");
            else
            {
                var snap = FindObjectOfType<SnapTurnProviderBase>();
                int clicks = snap != null && snap.isActiveAndEnabled && snap.turnAmount > 0 ? Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(angle) / snap.turnAmount)) : 0;
                string times = clicks == 1 ? "once" : clicks == 2 ? "twice" : $"{clicks} times";
                sb.Append(clicks > 0 ? $"Push the right stick to the {side} {times} to face it. " : $"Turn to your {side} with the right stick until it's in front of you. ");
            }
            sb.Append($"Then push the left stick forward and walk about {Metres(distance - 0.5f)}. ");
        }
        sb.Append("Reach out with a hand: the controller ticks faster as you get close, and buzzes when you can grab it. " +
                  "Then squeeze the grip button to pick up the pot. If you get lost, press A and ask me.");
        return sb.ToString();
    }

    private void Announce(string gameEvent)
    {
        if (proactiveGuide && guide != null) guide.QueueGameEvent(gameEvent);
    }

    private void CountQuestions()
    {
        bool pressed = Input.GetKeyDown(KeyCode.Space) || (vr != null && vr.isButtonPressed && !wasAskPressed);
        wasAskPressed = vr != null && vr.isButtonPressed;
        if (pressed) { questions++; Log("question", questions.ToString()); }
    }

    // The [Task] block that SceneContextBuilder adds to every request
    public string ContextText(Transform head)
    {
        var sb = new StringBuilder();
        int count = done.Count(d => d);
        sb.AppendLine($"[Task] Cooking carrot soup: {count} of {done.Length} steps done.");

        var doneSteps = Enumerable.Range(0, done.Length).Where(i => done[i]).Select(i => StepText[i]).ToList();
        var now = Enumerable.Range(0, done.Length).Where(i => !done[i] && Needs[i].All(IsDone)).Select(i => StepText[i]).ToList();
        var later = Enumerable.Range(0, done.Length).Where(i => !done[i] && !Needs[i].All(IsDone)).Select(i => StepText[i]).ToList();
        if (doneSteps.Count > 0) sb.AppendLine("Done: " + string.Join("; ", doneSteps));
        if (now.Count > 0) sb.AppendLine("Can do now: " + string.Join("; ", now));
        if (later.Count > 0) sb.AppendLine("Later: " + string.Join("; ", later));

        sb.AppendLine("State: " + StateText());

        string hand = HandGuidance(head);
        if (hand != null) sb.AppendLine(hand);
        return sb.ToString().TrimEnd();
    }

    private string StateText()
    {
        var parts = new List<string>();
        var held = new List<SoupItem> { pot != null ? pot.Item : null, looseCarrot, choppedPile, spiceJar != null ? spiceJar.GetComponent<SoupItem>() : null }.Where(i => i != null && i.IsHeld).ToList();
        parts.Add(held.Count == 0 ? "player is holding nothing from the recipe" : string.Join(", ", held.Select(i => $"holding the {i.name} in the {i.HeldBy()}")));

        if (pot != null)
        {
            string where = burner != null && burner.PotOnBurner ? "on the stove burner" : tap != null && tap.PotInSink ? "in the sink" : pot.Item.IsHeld ? "in the player's hand" : "not on the stove";
            string water = pot.IsFull ? "full of water" : pot.water > 0.01f ? $"{Mathf.RoundToInt(pot.water * 100)}% full of water" : "empty";
            string heat = !pot.HasWater ? "" : pot.IsBoiling ? ", water boiling" : pot.temperature > 0.01f ? $", water heating up ({Mathf.RoundToInt(pot.temperature * 100)}%)" : ", water cold";
            parts.Add($"Soup Pot is {where}, {water}{heat}{(pot.carrotsIn ? ", carrots in" : "")}{(pot.spicesIn ? ", spices in" : "")}");
        }
        if (tap != null) parts.Add($"Sink Tap is {(tap.Running ? "running" : "off")}");
        if (knob != null) parts.Add($"Stove Knob is at {knob.LevelName}");
        if (board != null && board.Carrot != null) parts.Add($"a carrot is on the Cutting Board, chopped {board.Chops} of {board.chopsNeeded} times");
        else if (looseCarrot != null && !looseCarrot.IsHeld) parts.Add("a Carrot is lying loose, not on the board");
        if (choppedPile != null && !choppedPile.IsHeld) parts.Add("the Chopped Carrots are waiting on the Cutting Board");
        if (spiceJar != null && pot != null && !pot.spicesIn && spiceJar.Pinches > 0) parts.Add($"spices shaken in {spiceJar.Pinches} of {spiceJar.pinchesNeeded} times ({spiceJar.pinchesNeeded - spiceJar.Pinches} more needed)");
        return string.Join(". ", parts) + ".";
    }

    // Where the next object is relative to the nearest hand, once it is within reach: the last half metre is where a
    // blind player needs the most help, and "on your right" isn't precise enough to close a hand on a knob
    private string HandGuidance(Transform head)
    {
        var (name, point) = NextTarget();
        if (name == null || head == null) return null;

        Transform best = null;
        float bestDist = 1.2f;
        foreach (Transform hand in Hands())
        {
            float d = Vector3.Distance(hand.position, point);
            if (d < bestDist) { bestDist = d; best = hand; }
        }
        if (best == null) return null;

        Vector3 v = point - best.position;
        Vector3 facing = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        var parts = new List<string>();
        AddOffset(parts, Vector3.Dot(v, facing), "forward", "back towards you");
        AddOffset(parts, Vector3.Dot(v, right), "to the right", "to the left");
        AddOffset(parts, v.y, "higher", "lower");
        string offset = parts.Count == 0 ? "right at" : string.Join(", ", parts) + " from";
        return $"Hand directions: the {name} is {offset} the player's {SoupItem.HandName(best)}.";
    }

    private static void AddOffset(List<string> parts, float metres, string positive, string negative)
    {
        int cm = Mathf.RoundToInt(Mathf.Abs(metres) * 20f) * 5; // nearest 5 cm
        if (cm >= 5) parts.Add($"{cm} cm {(metres > 0 ? positive : negative)}");
    }

    // A short sentence about an object's state for the hand's name button ("This is the soup pot. It's empty."),
    // with how to use it where that isn't obvious; null for objects the task doesn't know
    public string Describe(GameObject obj)
    {
        if (pot != null && obj == pot.gameObject)
        {
            string water = pot.IsFull ? "full of water" : pot.water > 0.01f ? "partly filled with water" : "empty";
            string extra = (pot.IsBoiling ? ", boiling" : "") + (pot.carrotsIn ? ", with carrots in" : "") + (pot.spicesIn ? ", seasoned" : "");
            return $"It's {water}{extra}.";
        }
        if (knob != null && obj == knob.gameObject) return $"It's set to {knob.LevelName}. Pull the trigger to turn it one click.";
        if (tap != null && obj == tap.gameObject) return $"The water is {(tap.Running ? "running" : "off")}. Pull the trigger to turn it {(tap.Running ? "off" : "on")}.";
        if (burner != null && obj == burner.gameObject) return burner.IsOn ? $"It's on, at {knob.LevelName}. Careful, it's hot." : "It's off.";
        if (crate != null && (obj == crate.gameObject || obj.name == "Crate of Carrots")) return "Squeeze grip inside it to take a carrot.";
        if (board != null && board.Carrot != null && obj == board.Carrot.gameObject) return $"It's on the cutting board, chopped {board.Chops} of {board.chopsNeeded} times.";
        if (spiceJar != null && obj == spiceJar.gameObject) return pot != null && pot.spicesIn ? "The soup is already seasoned." : "Hold it over the pot and shake it three times.";
        return null;
    }

    // The object the player most likely needs next, pot chain first
    private (string, Vector3) NextTarget()
    {
        Vector3 Near(Component c) => c.GetComponentInChildren<Renderer>() is Renderer r ? r.bounds.center : c.transform.position;

        if (pot != null && !IsDone(Step.PickUpPot)) return ("Soup Pot", Near(pot));
        if (pot != null && !IsDone(Step.FillPot) && tap != null)
            return pot.Item.IsHeld && !tap.PotInSink ? ("Sink", tap.sinkZone.center) : ("Sink Tap", Near(tap));
        if (!IsDone(Step.PotOnStove) && burner != null) return ("Stove Burner", burner.transform.position);
        if (!IsDone(Step.HeatMedium) && knob != null) return ("Stove Knob", Near(knob));
        if (!IsDone(Step.TakeCarrot) && crate != null) return ("Crate of Carrots", crate.transform.position);
        if (!IsDone(Step.CarrotOnBoard) && board != null) return ("Cutting Board", board.boardZone.center);
        if (!IsDone(Step.ChopCarrot) && board != null)
        {
            if (board.KnifeHeld || board.Carrot == null) return ("carrot on the Cutting Board", board.boardZone.center);
            GameObject knife = NearestKnife(board.boardZone.center);
            return knife != null ? ("Knife", knife.transform.position) : (null, Vector3.zero);
        }
        if (!IsDone(Step.CarrotsInPot) && choppedPile != null)
            return choppedPile.IsHeld && pot != null ? ("Soup Pot", Near(pot)) : ("Chopped Carrots", Near(choppedPile));
        if (!IsDone(Step.AddSpices) && spiceJar != null)
            return spiceJar.GetComponent<SoupItem>().IsHeld && pot != null ? ("Soup Pot", Near(pot)) : ("Spice Jar", Near(spiceJar));
        return (null, Vector3.zero);
    }

    private static GameObject NearestKnife(Vector3 near) =>
        FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).Where(g => g.name.StartsWith("Knife"))
            .OrderBy(g => Vector3.Distance(g.transform.position, near)).Select(g => g.gameObject).FirstOrDefault();

    // Controller transforms, from the XR rig the player spawns with
    private static IEnumerable<Transform> Hands() =>
        FindObjectsByType<XRBaseController>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && SoupItem.HandName(c.transform) != "hand").Select(c => c.transform);

    // --- Logging and testing -------------------------------------------------------------------------------------

    private void Log(string type, string detail)
    {
        if (!logRun || logPath == null) return;
        detail = detail.Replace("\"", "'").Replace("\n", " ");
        File.AppendAllText(logPath, $"{Time.time - startTime:F2},{type},\"{detail}\"\n");
    }

    private void HandleDebugKeys()
    {
        for (int i = 0; i < StepCount; i++)
        {
            // Keys 1-9, then 0 for the tenth step
            if (!Input.GetKeyDown(i < 9 ? KeyCode.Alpha1 + i : KeyCode.Alpha0)) continue;
            Debug.Log($"[Soup] Debug key {(i + 1) % 10}: {(Step)i}");
            switch ((Step)i)
            {
                case Step.PickUpPot: pot.Item.everHeld = true; break;
                case Step.FillPot: pot.Fill(100f); break;
                case Step.PotOnStove: pot.Item.everHeld = true; pot.Item.PlaceAt(burner.transform.position); break;
                case Step.HeatMedium: knob.Turn(); break;
                case Step.TakeCarrot: crate.Spawn(crate.transform.position + Vector3.up * 0.4f); break;
                case Step.CarrotOnBoard: if (looseCarrot != null) board.PlaceCarrot(looseCarrot); break;
                case Step.ChopCarrot: board.Chop(); break;
                case Step.CarrotsInPot: if (choppedPile != null) pot.AddCarrots(choppedPile); break;
                case Step.AddSpices: pot.AddSpices(); break;
                case Step.Simmer: Complete(Step.Simmer); break;
            }
        }
    }

    void OnGUI()
    {
        if (!showOverlay) return;
        var sb = new StringBuilder("Soup task" + (enableDebugKeys ? "  (keys 1-9, 0 = steps)" : "") + "\n");
        for (int i = 0; i < done.Length; i++)
            sb.AppendLine($"{(done[i] ? "[x]" : Needs[i].All(IsDone) ? "[ ]" : " - ")} {(i + 1) % 10}. {((Step)i)}{(done[i] ? $"  {doneAt[i]:F0}s" : "")}");
        sb.Append($"questions {questions}  mistakes {mistakes}\n{StateText()}");
        GUI.Box(new Rect(Screen.width - 410, 10, 400, 300), "");
        GUI.Label(new Rect(Screen.width - 400, 15, 385, 290), sb.ToString());
    }
}
