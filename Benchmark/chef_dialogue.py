"""Plays a whole soup run through the guide's real prompt, without Unity, and saves what the chef says.

The system prompt is the one RealtimeGuideClient sends (taken from a logged Kitchen request) with the SoupKitchen registry
and SoupTask.PromptRules swapped in. Game events and the [Task] blocks follow SoupTask's wording; the [Scene Context]
positions are a made-up but consistent kitchen layout, so this tests what the chef says, not the position maths.

Usage: python -I -u Benchmark/chef_dialogue.py [--model gemma4:e4b] [--no-audio]
Writes Benchmark/results/chef_<time>/transcript.md plus one WAV per guide line (Speaches TTS).
"""
import argparse, json, re, sys, time, urllib.request
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "Assets" / "Scripts"
LOGGED_REQUEST = ROOT / "DebugCaptures" / "interactions" / "20261026_020449_Kitchen" / "request.json"
OLLAMA = "http://localhost:11434/api/chat"
TTS = "http://localhost:8000/v1/audio/speech"


def csharp_string(source, start_pattern):
    """Joins the string literals of a C# expression like  X = "a" + "b";  (stops at the first ; outside a string)."""
    i = re.search(start_pattern, source).end()
    out, j = [], i
    while source[j] != ";":
        if source[j] == '"':
            k = j + 1
            while source[k] != '"' or source[k - 1] == "\\":
                k += 1
            out.append(source[j + 1:k])
            j = k
        j += 1
    return "".join(out).replace('\\"', '"').replace("\\n", "\n")


def csharp_list(source, start_pattern):
    block = source[re.search(start_pattern, source).end():]
    block = block[:block.index("};")]
    return [m.replace('\\"', '"') for m in re.findall(r'"((?:[^"\\]|\\.)*)"', block)]


def system_prompt():
    rooms = json.loads((ROOT / "Assets" / "Resources" / "RoomDescriptions.json").read_text(encoding="utf-8"))
    logged = json.loads(LOGGED_REQUEST.read_text(encoding="utf-8"))["messages"][0]["content"]

    # Same filtering as OpenAIQueries.LoadRoomDescriptions
    def classifications(scene):
        return " | ".join(s for s in rooms[scene].split("|") if ":" in s)

    s = logged.replace(classifications("Kitchen"), classifications("SoupKitchen"))
    s = s.replace(rooms["Kitchen_Objects"], rooms["SoupKitchen_Objects"])
    assert "Warehouse Parking Lot" not in s, "registry swap failed"
    rules = csharp_string((SCRIPTS / "Soup" / "SoupTask.cs").read_text(encoding="utf-8"), r"PromptRules\s*=")
    i = s.index("\n\nACTIONS:")  # AIGuide adds the rules to the prompt; RealtimeGuideClient appends ActionInstructions after
    return s[:i] + rules + s[i:]


def welcome_text():
    src = (SCRIPTS / "GuideWelcome.cs").read_text(encoding="utf-8")
    lines = csharp_list(src, r"public List<string> lines = new List<string>\s*\{")
    chef = csharp_string(src, r"public string chefLine\s*=")
    return " ".join(lines[:1] + [chef] + lines[1:])


# --- A scripted run: (what happens, scene context, task block). Positions are invented but consistent.
def scene(*lines):
    return ("[Scene Context] Exact positions computed by the game engine at the moment the player asked. Directions are "
            "relative to where the player is facing (12 o'clock = straight ahead). Trust this over the images for what is "
            "where; use the images for anything not listed here.\nPlayer is inside: Rosie's Kitchen\n" + "\n".join(lines))


STEPS = ["pick up the Soup Pot", "fill the pot with water at the Sink (hold it in the sink and turn on the Sink Tap)",
         "put the full pot on the Stove Burner", "turn the Stove Knob to medium heat", "take a Carrot from the Crate of Carrots",
         "put the carrot on the Cutting Board", "chop the carrot with a Knife", "put the Chopped Carrots into the pot",
         "season the soup: hold the Spice Jar over the pot and shake it", "let the soup simmer at medium heat"]
NEEDS = [[], [0], [1], [2], [], [4], [5], [6], [2], [3, 7, 8]]


def task(done, state, hand=None):
    now = [STEPS[i] for i in range(10) if i not in done and all(n in done for n in NEEDS[i])]
    later = [STEPS[i] for i in range(10) if i not in done and not all(n in done for n in NEEDS[i])]
    out = [f"[Task] Cooking carrot soup: {len(done)} of 10 steps done."]
    if done: out.append("Done: " + "; ".join(STEPS[i] for i in sorted(done)))
    if now: out.append("Can do now: " + "; ".join(now))
    if later: out.append("Later: " + "; ".join(later))
    out.append("State: " + state)
    if hand: out.append("Hand directions: " + hand)
    return "\n".join(out)


RUN = [
    ("event", "You have just welcomed the player, explained the controls and given the recipe. As their chef, tell them the first step and where to go, in one or two sentences.",
     scene("In view, nearest first:", "- Kitchen Counter: straight ahead (12 o'clock), 1 m", "- Soup Pot: ahead to the left (11 o'clock), 2 m", "- Oven: ahead to the left (10 o'clock), 2 m",
           "Not in view:", "- Sink: behind to the right (5 o'clock), 3 m"),
     task([], "player is holding nothing from the recipe. Soup Pot is not on the stove, empty. Sink Tap is off. Stove Knob is at off.")),
    ("event", "Step done: pick up the Soup Pot.",
     scene("Right next to the player (within reach):", "- Oven Countertop: in front of you, slightly to your left (11 o'clock), within 1 m",
           "Not in view:", "- Sink: behind to the right (5 o'clock), 3 m; walking route 4 m, start by heading to the right (3 o'clock)"),
     task([0], "holding the Soup Pot in the right hand. Soup Pot is in the player's hand, empty. Sink Tap is off. Stove Knob is at off.")),
    ("player", "Where is the sink?",
     scene("In view, nearest first:", "- Sink: ahead to the right (1 o'clock), 2 m", "- Dish Rack: ahead to the right (2 o'clock), 2 m"),
     task([0], "holding the Soup Pot in the right hand. Soup Pot is in the player's hand, empty. Sink Tap is off. Stove Knob is at off.")),
    ("event", "Step done: fill the pot with water at the Sink (hold it in the sink and turn on the Sink Tap).",
     scene("Right next to the player (within reach):", "- Sink: directly in front of you (12 o'clock), within 1 m", "- Sink Tap: directly in front of you (12 o'clock), within 1 m",
           "Not in view:", "- Stove Burner: behind to the left (7 o'clock), 3 m"),
     task([0, 1], "holding the Soup Pot in the right hand. Soup Pot is in the sink, full of water, water cold. Sink Tap is running. Stove Knob is at off.")),
    ("event", "Step done: put the full pot on the Stove Burner.",
     scene("Right next to the player (within reach):", "- Oven: directly in front of you (12 o'clock), within 1 m", "- Stove Knob: in front of you, slightly to your right (1 o'clock), within 1 m"),
     task([0, 1, 2], "player is holding nothing from the recipe. Soup Pot is on the stove burner, full of water, water cold. Sink Tap is off. Stove Knob is at off.",
          "the Stove Knob is 25 cm forward, 15 cm to the right, 10 cm lower from the player's right hand.")),
    ("event", "Step done: turn the Stove Knob to medium heat. The water needs about 20 seconds to heat up; suggest preparing the carrot in the meantime.",
     scene("Right next to the player (within reach):", "- Oven: directly in front of you (12 o'clock), within 1 m",
           "Not in view:", "- Crate of Carrots: to the left (9 o'clock), 3 m", "- Cutting Board: behind to the left (8 o'clock), 2 m"),
     task([0, 1, 2, 3], "player is holding nothing from the recipe. Soup Pot is on the stove burner, full of water, water heating up (10%). Sink Tap is off. Stove Knob is at medium.")),
    ("event", "Step done: take a Carrot from the Crate of Carrots.",
     scene("Right next to the player (within reach):", "- Crate of Carrots: directly in front of you (12 o'clock), within 1 m",
           "In view, nearest first:", "- Kitchen Prep Table: ahead to the right (2 o'clock), 2 m", "- Cutting Board: ahead to the right (2 o'clock), 2 m"),
     task([0, 1, 2, 3, 4], "holding the Carrot in the left hand. Soup Pot is on the stove burner, full of water, water heating up (35%). Sink Tap is off. Stove Knob is at medium.")),
    ("event", "Step done: put the carrot on the Cutting Board.",
     scene("Right next to the player (within reach):", "- Cutting Board: directly in front of you (12 o'clock), within 1 m", "- Knife: in front of you, slightly to your right (1 o'clock), within 1 m"),
     task([0, 1, 2, 3, 4, 5], "player is holding nothing from the recipe. Soup Pot is on the stove burner, full of water, water heating up (55%). Sink Tap is off. Stove Knob is at medium. a carrot is on the Cutting Board, chopped 0 of 4 times.",
          "the Knife is 20 cm forward, 25 cm to the right from the player's right hand.")),
    ("event", "Step done: chop the carrot with a Knife.",
     scene("Right next to the player (within reach):", "- Cutting Board: directly in front of you (12 o'clock), within 1 m",
           "Not in view:", "- Stove Burner: behind to the right (4 o'clock), 2 m"),
     task([0, 1, 2, 3, 4, 5, 6], "holding the Knife in the right hand. Soup Pot is on the stove burner, full of water, water heating up (80%). Sink Tap is off. Stove Knob is at medium. the Chopped Carrots are waiting on the Cutting Board.")),
    ("event", "The water in the pot is boiling now.",
     scene("Right next to the player (within reach):", "- Cutting Board: directly in front of you (12 o'clock), within 1 m",
           "Not in view:", "- Stove Burner: behind to the right (4 o'clock), 2 m"),
     task([0, 1, 2, 3, 4, 5, 6], "player is holding nothing from the recipe. Soup Pot is on the stove burner, full of water, water boiling. Sink Tap is off. Stove Knob is at medium. the Chopped Carrots are waiting on the Cutting Board.")),
    ("event", "Step done: put the Chopped Carrots into the pot.",
     scene("Right next to the player (within reach):", "- Oven: directly in front of you (12 o'clock), within 1 m", "- Spice Jar: on your right (3 o'clock), within 1 m"),
     task([0, 1, 2, 3, 4, 5, 6, 7], "player is holding nothing from the recipe. Soup Pot is on the stove burner, full of water, water boiling, carrots in. Sink Tap is off. Stove Knob is at medium.",
          "the Spice Jar is 10 cm forward, 35 cm to the right from the player's right hand.")),
    ("player", "Did I add enough spices?",
     scene("Right next to the player (within reach):", "- Oven: directly in front of you (12 o'clock), within 1 m"),
     task([0, 1, 2, 3, 4, 5, 6, 7], "holding the Spice Jar in the right hand. Soup Pot is on the stove burner, full of water, water boiling, carrots in. Sink Tap is off. Stove Knob is at medium. spices shaken in 1 of 3 times (2 more needed).")),
    ("event", "Step done: season the soup: hold the Spice Jar over the pot and shake it.",
     scene("Right next to the player (within reach):", "- Oven: directly in front of you (12 o'clock), within 1 m"),
     task([0, 1, 2, 3, 4, 5, 6, 7, 8], "holding the Spice Jar in the right hand. Soup Pot is on the stove burner, full of water, water boiling, carrots in, spices in. Sink Tap is off. Stove Knob is at medium. simmering 2 of 15 s.")),
    ("event", "The soup is ready! The player finished all steps in 214 seconds. Congratulate them.",
     scene("Right next to the player (within reach):", "- Oven: directly in front of you (12 o'clock), within 1 m"),
     task(list(range(10)), "player is holding nothing from the recipe. Soup Pot is on the stove burner, full of water, water boiling, carrots in, spices in. Sink Tap is off. Stove Knob is at medium.")),
]

TOWN = ["Warehouse", "Police Station", "Hospital", "Courthouse", "Mount Oreo", "Bakery", "Bank", "Parking Lot", "Music Store"]


def post(url, body, timeout=180):
    req = urllib.request.Request(url, json.dumps(body).encode(), {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="gemma4:e4b")
    ap.add_argument("--no-audio", action="store_true")
    a = ap.parse_args()

    out = ROOT / "Benchmark" / "results" / f"chef_{datetime.now():%Y%m%d_%H%M%S}"
    out.mkdir(parents=True)
    system = system_prompt()
    welcome = welcome_text()
    history = [{"role": "assistant", "content": welcome}]  # GuideWelcome speaks via SpeakCustomText, which adds it to history
    lines = [("Welcome (fixed text)", welcome, None)]
    notes = []

    for n, (kind, text, ctx, task_block) in enumerate(RUN, 1):
        user = ("[Game Event] " + text) if kind == "event" else text
        history.append({"role": "user", "content": user})
        # Like SendUserMessageAndRespond: context goes right before the newest message and isn't kept in history
        messages = [{"role": "system", "content": system}] + history[:-1] + [{"role": "user", "content": ctx + "\n" + task_block}] + history[-1:]
        t = time.time()
        reply = json.loads(post(OLLAMA, {"model": a.model, "messages": messages, "stream": False, "think": False}))["message"]["content"]
        dt = time.time() - t
        history.append({"role": "assistant", "content": reply})
        flags = []

        # Same clean-up as RealtimeGuideClient.ProcessResponseForActions before speaking
        spoken = re.sub(r"\[(?:Task|Scene Context|Game Event|Visual Context)\][^\n.!?]*[.!?]?\s*", "", reply)
        if spoken != reply: flags.append("quoted an input block (removed before speaking)")
        spoken = re.sub(r"\[ACTION:\{.*?\}\]", "", spoken).strip()
        sentences = len([s for s in re.split(r"(?<=[.!?])\s+", spoken) if s])
        if sentences > 3: flags.append(f"{sentences} sentences")
        if re.search(r"[*#]|^\s*-", spoken, re.M): flags.append("markdown")
        if "[ACTION" in reply: flags.append("action tag")
        if re.search(r"\b(you see|you can see|look at|looking)\b", spoken, re.I): flags.append("visual wording")
        flags += [f"mentions {w}" for w in TOWN if w.lower() in spoken.lower()]
        label = ("Player: " if kind == "player" else "Event: ") + text
        print(f"[{n:2}] {dt:4.1f}s {label[:70]}\n     -> {spoken}" + (f"   !! {', '.join(flags)}" if flags else ""), flush=True)
        lines.append((label, spoken, dt))
        if flags: notes.append(f"{n}: {', '.join(flags)}")

    if not a.no_audio:
        for i, (_, spoken, _) in enumerate(lines):
            wav = post(TTS, {"model": "speaches-ai/Kokoro-82M-v1.0-ONNX", "voice": "af_heart", "input": spoken, "response_format": "wav"}, timeout=300)
            (out / f"{i:02d}.wav").write_bytes(wav)
        print(f"Saved {len(lines)} WAV files", flush=True)

    md = [f"# Chef dialogue test ({a.model}, {datetime.now():%Y-%m-%d %H:%M})", "",
          "Scene positions are scripted, not from Unity; the prompt, rules and welcome are the real ones.", ""]
    for i, (label, spoken, dt) in enumerate(lines):
        md += [f"**{i:02d}. {label}**" + (f"  _({dt:.1f} s)_" if dt else ""), "", f"> {spoken}", ""]
    md += ["## Flags", ""] + ([f"- {x}" for x in notes] or ["- none"])
    (out / "transcript.md").write_text("\n".join(md), encoding="utf-8")
    print(f"Transcript: {out / 'transcript.md'}")


if __name__ == "__main__":
    main()
