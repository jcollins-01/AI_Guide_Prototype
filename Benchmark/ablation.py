"""Which input misleads the guide? Replays one logged interaction with one thing changed at a time.

Scoring is stricter than benchmark.py: the answer must place the target object in front of the player
(a sentence that names the object AND says front / ahead / straight).

Usage: python -I -u Benchmark/ablation.py <interaction folder name> <object word[,synonym]> [--model gemma4:e4b] [--reps 5]
  e.g. python -I -u Benchmark/ablation.py 20261025_213505_Kitchen_fridge fridge,refrigerator
"""
import argparse, copy, csv, json, re, sys
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from benchmark import ROOT, INTERACTIONS, ROOM_JSON, chat, load_interactions

FRONT = re.compile(r"\b(in front|ahead|straight|directly before|facing)\b", re.I)


def placed_in_front(text, words):
    for sentence in re.split(r"(?<=[.!?])\s+", text):
        low = sentence.lower()
        if any(w in low for w in words) and FRONT.search(sentence) and not re.search(r"\b(left|right|behind)\b", low):
            return True
    return False


def registry_descriptions(scene):
    d = json.loads(ROOM_JSON.read_text(encoding="utf-8"))
    return " | ".join(s for s in d[scene].split("|") if ":" in s)


def variants(req, scene):
    msgs = req["messages"]
    sys_i = next(i for i, m in enumerate(msgs) if m["role"] == "system")
    vis_i = next(i for i, m in enumerate(msgs) if "images" in m)
    q_i = len(msgs) - 1

    def v(fn):
        r = copy.deepcopy(req); fn(r["messages"]); return r

    def no_birdseye(m):
        m[vis_i]["images"] = m[vis_i]["images"][:1]
        m[vis_i]["content"] = "[Visual Context] This image is exactly what the player is facing right now."

    def no_descriptions(m):  # keep the object NAMES (needed for actions), drop the long text descriptions
        m[sys_i]["content"] = m[sys_i]["content"].replace(registry_descriptions(scene), "")

    def images_on_question(m):  # attach the image to the question itself instead of a separate earlier turn
        m[q_i]["images"] = m[vis_i].pop("images")
        m[q_i]["content"] = m[vis_i]["content"] + "\n\n" + m[q_i]["content"]
        del m[vis_i]

    def look_first(m):
        m[q_i]["content"] += ("\n(Answer from what is visible in the image. The Registry is only for exact object names; "
                              "never use it to decide where the player is or what is in front of them.)")

    def combined(m):
        no_birdseye(m); no_descriptions(m); look_first(m); images_on_question(m)

    return {
        "as logged (baseline)": req,
        "no bird's-eye image": v(no_birdseye),
        "no registry descriptions": v(no_descriptions),
        "image attached to question": v(images_on_question),
        "'look at the image' rule": v(look_first),
        "all of the above": v(combined),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("case"); ap.add_argument("words")
    ap.add_argument("--model", default="gemma4:e4b"); ap.add_argument("--reps", type=int, default=5)
    a = ap.parse_args()
    words = [w.strip().lower() for w in a.words.split(",")]

    case = next(c for c in load_interactions() if c["id"] == a.case)
    scene = json.loads((INTERACTIONS / a.case / "meta.json").read_text(encoding="utf-8"))["scene"]
    out = ROOT / "Benchmark" / "results" / f"ablation_{datetime.now():%Y%m%d_%H%M%S}.csv"
    rows, table = [], []

    for name, req in variants(case["request"], scene).items():
        ok = 0
        for rep in range(a.reps):
            text, dt, tok, _ = chat(dict(req, model=a.model, stream=False))
            hit = placed_in_front(text, words)
            ok += hit
            print(f"[{name}] {'OK' if hit else 'XX'} {dt:4.1f}s {text[:110]!r}", flush=True)
            rows.append([a.model, name, rep, f"{dt:.2f}", int(hit), text])
        table.append(f"| {name} | {ok}/{a.reps} |")

    with open(out, "w", newline="", encoding="utf-8") as f:
        csv.writer(f).writerows([["model", "variant", "rep", "seconds", "object_in_front", "response"]] + rows)
    print(f"\n| Variant ({a.model}) | '{words[0]}' placed in front |\n|---|---|\n" + "\n".join(table))
    print(f"\nFull answers: {out}")


if __name__ == "__main__":
    main()
