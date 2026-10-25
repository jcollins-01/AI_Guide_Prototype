"""Benchmark local Ollama models as the AI guide's brain.

Two parts:
  1. Vision: replays every interaction logged by Unity (DebugCaptures/interactions/*/request.json -- the exact
     prompt, history and screenshots the guide sent) with the model swapped, and scores the answer against
     that folder's label.json.
  2. Actions: 16 spoken-style requests in GuidePark1; checks the [ACTION:...] tag (right action, right object,
     no false triggers).

Labelling: the first run writes a label.json template into every interaction folder. Fill it in, e.g.
    {"expect_any": ["fridge", "refrigerator"], "forbid": ["oven", "prep table"]}
An answer counts as correct if it mentions at least one expect_any word and no forbid word (case-insensitive).
Unlabelled interactions still run (for latency) but are not scored.

Usage (from the AI_Guide_Prototype folder, Ollama running):
    python -I Benchmark/benchmark.py gemma4:e4b qwen3-vl:8b --reps 3
    python -I Benchmark/benchmark.py gemma4:e4b --skip-actions
Results go to Benchmark/results/<timestamp>/ (results.csv with every answer, summary.md with the table).
"""
import argparse, csv, json, re, statistics, sys, time, urllib.error, urllib.request
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent  # AI_Guide_Prototype/
INTERACTIONS = ROOT / "DebugCaptures" / "interactions"
ROOM_JSON = ROOT / "Assets" / "Resources" / "RoomDescriptions.json"
OLLAMA = "http://localhost:11434"
ACTION_RE = re.compile(r"\[ACTION:(\{.*?\})\]")


def post(path, body, timeout=600):
    req = urllib.request.Request(OLLAMA + path, json.dumps(body).encode(), {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.load(r)


MAX_TOKENS = 1024  # some models ignore think:false and think for minutes; cap it so one bad model can't stall the run


def chat(body):
    """Returns (reply text, seconds, tokens generated, hidden thinking chars). Retries without 'think' for models that reject it."""
    body = dict(body, options=dict(body.get("options", {}), num_predict=MAX_TOKENS))
    t = time.time()
    try:
        resp = post("/api/chat", body)
    except urllib.error.HTTPError as e:
        if e.code == 400 and "think" in body:
            body = {k: v for k, v in body.items() if k != "think"}
            t = time.time()
            resp = post("/api/chat", body)
        else:
            raise RuntimeError(f"HTTP {e.code}: {e.read().decode(errors='replace')[:300]}")
    msg = resp["message"]
    return msg.get("content", ""), time.time() - t, resp.get("eval_count", 0), len(msg.get("thinking") or "")


def capabilities(model):
    try:
        return post("/api/show", {"model": model}).get("capabilities", [])
    except Exception as e:
        sys.exit(f"Model '{model}' not available ({e}). Pull it first: ollama pull {model}")


# ---------- Part 1: vision replay ----------

def load_interactions():
    import base64
    cases = []
    for d in sorted(INTERACTIONS.glob("*/")):
        req_file = d / "request.json"
        if not req_file.exists():
            continue
        request = json.loads(req_file.read_text(encoding="utf-8"))
        for m in request["messages"]:  # file names -> base64, as Unity sent them
            if "images" in m:
                m["images"] = [base64.b64encode((d / n).read_bytes()).decode() for n in m["images"]]
        meta = json.loads((d / "meta.json").read_text(encoding="utf-8"))
        label_file = d / "label.json"
        if not label_file.exists():
            label_file.write_text(json.dumps({"expect_any": [], "forbid": [],
                                              "note": f"Question: {meta['question']}"}, indent=2), encoding="utf-8")
        label = json.loads(label_file.read_text(encoding="utf-8"))
        cases.append({"id": d.name, "request": request, "question": meta["question"], "label": label})
    return cases


def score_vision(text, label):
    if not label.get("expect_any"):
        return None  # unlabelled
    low = text.lower()
    hit = any(w.lower() in low for w in label["expect_any"])
    bad = any(w.lower() in low for w in label.get("forbid", []))
    return hit and not bad


# ---------- Part 2: action tags (same prompt construction as AIGuide.GetFormattedPrompt, GuidePark1, role 1) ----------

def guide_prompt(scene="GuidePark1_Networked"):
    d = json.loads(ROOM_JSON.read_text(encoding="utf-8"))
    names = d[scene + "_Objects"]
    classes = " | ".join(s for s in d[scene].split("|") if ":" in s)
    base = ("You are Giddy, a warm, friendly, but still professional sighted guide. You are a sighted guide for a blind player. "
            "YOUR EYES (Visual Context): You will receive periodic text updates labeled 'VISUAL CONTEXT'. "
            "This is your current reality. If you see a new person, a new object (like a cylinder), or a change in the scene, mention it naturally."
            "As you respond to the player, speak as though you're in the scene with them - refrain from mentioning aspects of your internal architecture."
            " THE NAVIGATION REGISTRY: Names and descriptions of objects in the scene. When following navigation or modification commands, use ONLY these names: "
            + classes +
            "PRIORITY: If the 'VISUAL CONTEXT' shows new avatars or objects not in the Registry, alert the player immediately. "
            "Use the Registry " + names + " for technical commands (teleport/modify/guide). "
            "For everything else, rely on the Visual Context provided in the chat history. "
            "If a user asks 'What's around me?', synthesize the Registry and the Visual Context into a spatial summary."
            "GUIDANCE RULES: If a new object/avatar appears that is NOT in the Registry, describe it spatially (e.g., 'A new player just joined, standing to your left'). Never mention 'photos' or 'images.'Limit: 150 words.")
    # Keep in sync with RealtimeGuideClient.ActionInstructions
    action = ("\n\nACTIONS: You can trigger three actions by including a special tag anywhere in your reply: "
              "[ACTION:{\"name\":\"trigger_guidance\",\"target_object\":\"<exact object name>\"}] to walk the player to an object, "
              "[ACTION:{\"name\":\"trigger_teleportation\",\"target_object\":\"<exact object name>\"}] to teleport there instantly, or "
              "[ACTION:{\"name\":\"trigger_modification\",\"target_object\":\"<exact object name>\"}] to add an audio beacon to it. "
              "Only include this tag when the player has clearly asked for one of these three things, using the exact object name "
              "from the Navigation Registry. Do not mention or describe the tag -- it is silently removed before your words are spoken. "
              "If you offered to take the player somewhere and they answer yes, include the tag for that object in this reply. "
              "\n\nSPOKEN REPLIES: Everything you write is read aloud to a blind player, so answer in at most 2-3 short sentences "
              "(this overrides any longer word limit above). No lists, no markdown. Give the most useful information first.")
    return base + action


ACTION_CASES = [
    ("Can you take me to the bench?", "trigger_guidance", "Bench"),
    ("Guide me to the red gazebo please.", "trigger_guidance", "Red-Roofed Gazebo"),
    ("I want to go to the boat.", "trigger_guidance", "Boat"),
    ("Lead me to the picnic table.", "trigger_guidance", "Picnic Table"),
    ("Walk me over to the eastern bridge.", "trigger_guidance", "Eastern Bridge"),
    ("Teleport me to the dock.", "trigger_teleportation", "Dock"),
    ("Can you teleport us to the yellow roofed gazebo?", "trigger_teleportation", "Yellow-Roofed Gazebo"),
    ("Put a sound beacon on the northern fountain.", "trigger_modification", "Northern Fountain"),
    ("Add an audio beacon to the boat so I can hear it.", "trigger_modification", "Boat"),
    ("What's around me?", None, None),
    ("Describe the park to me.", None, None),
    ("Where is the river?", None, None),
    ("Is there anyone near the fountain?", None, None),
    ("Thanks, that's helpful.", None, None),
    ("What does the bench look like?", None, None),
    ("Take me to the ice cream stand.", None, None),  # not in the registry: must NOT fire
]


def score_action(text, exp_act, exp_tgt):
    m = ACTION_RE.search(text)
    act = tgt = None
    if m:
        try:
            j = json.loads(m.group(1)); act, tgt = j.get("name"), j.get("target_object")
        except Exception:
            act = "MALFORMED"
    return act == exp_act and (exp_tgt is None or tgt == exp_tgt), act, tgt


# ---------- Runner ----------

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("models", nargs="+")
    ap.add_argument("--reps", type=int, default=2, help="repetitions per case (answers vary run to run)")
    ap.add_argument("--skip-actions", action="store_true")
    ap.add_argument("--skip-vision", action="store_true")
    args = ap.parse_args()

    vision_cases = [] if args.skip_vision else load_interactions()
    if not args.skip_vision:
        labelled = sum(1 for c in vision_cases if c["label"].get("expect_any"))
        print(f"Vision: {len(vision_cases)} logged interactions, {labelled} labelled (edit label.json files in {INTERACTIONS})")

    out_dir = ROOT / "Benchmark" / "results" / datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir.mkdir(parents=True)
    summary = []
    system = guide_prompt()

    # Rows are written as they come in, so a stopped or crashed run keeps everything measured so far
    csv_file = open(out_dir / "results.csv", "w", newline="", encoding="utf-8")
    w = csv.writer(csv_file)
    w.writerow(["model", "part", "case", "rep", "question", "seconds", "tokens", "thinking_chars",
                "correct", "action", "target", "response"])

    def record(*row):
        w.writerow(row); csv_file.flush()

    for model in args.models:
        caps = capabilities(model)
        print(f"\n=== {model}  (capabilities: {', '.join(caps)}) ===", flush=True)
        post("/api/chat", {"model": model, "messages": [], "keep_alive": "30m"})  # load it so load time isn't counted
        v_ok = v_n = a_ok = a_n = 0
        v_lat, a_lat = [], []

        if "vision" in caps:
            for c in vision_cases:
                for rep in range(args.reps):
                    body = dict(c["request"], model=model, stream=False)
                    text, dt, tok, think = chat(body)
                    s = score_vision(text, c["label"])
                    v_lat.append(dt)
                    if s is not None:
                        v_n += 1; v_ok += s
                    print(f"  [vision] {'--' if s is None else ('OK' if s else 'XX')} {dt:5.1f}s {tok:4d} tok"
                          f"{f' (thinking {think} chars)' if think else ''} {c['id']}: {text[:100]!r}", flush=True)
                    record(model, "vision", c["id"], rep, c["question"], f"{dt:.2f}", tok, think,
                           "" if s is None else int(s), "", "", text)
        elif vision_cases:
            print("  (no 'vision' capability -- skipping vision part)", flush=True)

        if not args.skip_actions:
            for utt, exp_act, exp_tgt in ACTION_CASES:
                for rep in range(args.reps):
                    text, dt, tok, think = chat({"model": model, "stream": False, "think": False, "messages": [
                        {"role": "system", "content": system}, {"role": "user", "content": utt}]})
                    ok, act, tgt = score_action(text, exp_act, exp_tgt)
                    a_n += 1; a_ok += ok; a_lat.append(dt)
                    print(f"  [action] {'OK' if ok else 'XX'} {dt:5.1f}s {utt!r} -> {act} / {tgt}", flush=True)
                    record(model, "action", utt, rep, utt, f"{dt:.2f}", tok, think, int(ok), act or "", tgt or "", text)

        med = lambda xs: f"{statistics.median(xs):.1f}s" if xs else "-"
        p90 = lambda xs: f"{sorted(xs)[int(0.9 * (len(xs) - 1))]:.1f}s" if xs else "-"
        summary.append([model,
                        f"{v_ok}/{v_n}" if v_n else "-", med(v_lat), p90(v_lat),
                        f"{a_ok}/{a_n}" if a_n else "-", med(a_lat)])
        post("/api/chat", {"model": model, "messages": [], "keep_alive": 0})  # free VRAM for the next model

    csv_file.close()
    header = ["Model", "Vision correct", "Vision median", "Vision p90", "Actions correct", "Actions median"]
    lines = ["| " + " | ".join(header) + " |", "|" + "---|" * len(header)]
    lines += ["| " + " | ".join(r) + " |" for r in summary]
    (out_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n" + "\n".join(lines))
    print(f"\nFull answers: {out_dir / 'results.csv'}")


if __name__ == "__main__":
    main()
