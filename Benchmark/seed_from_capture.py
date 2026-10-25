"""Turn the current DebugCaptures/viewpoint.jpg + birdseye.jpg into a benchmark interaction.

For captures taken before Unity logged full requests. Rebuilds the request the way RealtimeGuideClient does
(system prompt + visual-context turn + question, think off), so it matches what the game would have sent
apart from any earlier conversation history.

Usage: python -I Benchmark/seed_from_capture.py <scene> "<question>" <name> [--expect a,b] [--forbid c,d]
  e.g. python -I Benchmark/seed_from_capture.py Kitchen "What's in front of me?" fridge --expect fridge,refrigerator --forbid oven,"prep table"
"""
import argparse, json, shutil, sys
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from benchmark import ROOT, INTERACTIONS, guide_prompt

# Keep in sync with RealtimeGuideClient.SendVisualContext
VISUAL_CONTEXT = ("[Visual Context] Two silent reference images for your next answer. "
                  "Image 1 is exactly what the player is facing right now; use it for anything in front of them. "
                  "Image 2 is a top-down map taken from high above, centred on the player (the player stands roughly in the "
                  "middle of it); use it ONLY to work out where things are relative to the player. "
                  "Image 2 is NOT the player's view: never describe it as a view, never say the view "
                  "changed or shifted, and do not offer to go to things only visible in it unless they are in the Registry. "
                  "If you see a person's avatar with a gray hoodie, black hair, and glasses, that is YOUR avatar, the guide.")

ap = argparse.ArgumentParser()
ap.add_argument("scene"); ap.add_argument("question"); ap.add_argument("name")
ap.add_argument("--expect", default=""); ap.add_argument("--forbid", default="")
a = ap.parse_args()

caps = ROOT / "DebugCaptures"
d = INTERACTIONS / f"{datetime.now():%Y%m%d_%H%M%S}_{a.scene.replace(' ', '_')}_{a.name}"
d.mkdir(parents=True)
shutil.copy(caps / "viewpoint.jpg", d / "image_0.jpg")
shutil.copy(caps / "birdseye.jpg", d / "image_1.jpg")

request = {"model": "gemma4:e4b", "stream": False, "think": False, "messages": [
    {"role": "system", "content": guide_prompt(a.scene)},
    {"role": "user", "content": VISUAL_CONTEXT, "images": ["image_0.jpg", "image_1.jpg"]},
    {"role": "user", "content": a.question}]}
(d / "request.json").write_text(json.dumps(request, indent=2), encoding="utf-8")
(d / "meta.json").write_text(json.dumps({"scene": a.scene, "model": "seeded", "question": a.question,
                                         "response": "", "ollama_ms": 0}, indent=2), encoding="utf-8")
split = lambda s: [w.strip() for w in s.split(",") if w.strip()]
(d / "label.json").write_text(json.dumps({"expect_any": split(a.expect), "forbid": split(a.forbid),
                                          "note": f"Question: {a.question}"}, indent=2), encoding="utf-8")
print(f"Seeded {d}")
