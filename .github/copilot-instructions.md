## Purpose

This file gives succinct, actionable guidance for an AI coding agent working in this Unity project so it can be productive immediately. Focus on discoverable facts only (how the code is structured, important files, runtime dataflows, and exact places to change behavior).

## Big picture (what the project *is*)
- Unity 2022.3.20f1 LTS project (see `ProjectSettings/ProjectVersion.txt`).
- Core feature: an in-game AI “guide” that uses OpenAI (Whisper + GPT) and PlayHT for TTS, syncs audio over the network (Normcore), and controls avatar guidance/interaction.
- Major subsystems:
  - AI/voice flow: `Assets/Scripts/OpenAIQueries.cs` (calls Whisper, streams GPT responses, chunks text -> PlayHT).
  - Guide behavior: `Assets/Scripts/AIGuide.cs` (scene-based role assignment, input handling, high-level orchestration).
  - Networking/audio sync: Normcore + `GuideAudioSync`/`GuideRoleSync` (search for these scripts in `Assets/Scripts`).
  - Scene/object data: `Assets/Resources/RoomDescriptions.json` (object lists and human-readable scene descriptions used to build prompts).

## Key files & patterns (concrete examples to reference)
- `Assets/Scripts/AIGuide.cs`: uses `FindObjectOfType` heavily to wire components at runtime, maps scene names to `role` integers (examples: `Tutorial`, `GuidePark1_Networked`, `GuidePark2_Networked`, `GuidePark3_Networked`). When changing role logic, update this file.
- `Assets/Scripts/OpenAIQueries.cs`: constructs prompts, calls OpenAI client (`new OpenAIClient(apiKey)`), streams GPT responses and pushes chunks to PlayHT. It expects API keys in a Resources config (see Loading: `Resources.Load<TextAsset>("config")`) and also reads `RoomDescriptions.json`.
- `Assets/Resources/OpenAIConfiguration.asset`: project contains a serialized Unity asset for OpenAI config; production keys are likely kept out of source—check `Resources/` for `config.json` or the `OpenAIConfiguration` asset before editing.
- Audio & TTS: PlayHT calls in `OpenAIQueries.StreamTextToPlayHT` use `playHTApiKey`/`playHTUserId`. These live in `Resources` (not committed) or in `OpenAIConfiguration.asset`.
- Scenes: `Assets/Scenes/Guide Parks/*` contains the networked guide scenes referenced by runtime code.

## Conventions & gotchas (project-specific)
- Role mapping is index-based: `AIGuide.role` (int) maps to `OpenAIQueries.roles` list (index = role - 1). Be careful when adding/removing roles.
- The code assumes a GameObject named "Human Model" hosts the local `AudioSource` used as the recording/playback audio source (see `OpenAIQueries.Start`). Changing that GameObject name breaks audio wiring.
- Many components are attached at runtime via `gameObject.AddComponent<...>()` in `AIGuide.Start()`. Tests and static analysis should account for that (components won't appear in editors until runtime).
- `OpenAIQueries` splits GPT output into text chunks before sending to PlayHT. If you change chunking thresholds, test streaming latency and audio overlap (see `chunkSizeThreshold` and `ProcessChunkQueue`).

## Build / run / debug (concrete steps)
- Unity editor: use Unity Editor 2022.3.20f1. Open the project in Unity Hub or directly with the editor using `-projectPath`. Example PowerShell (replace Unity path):

```powershell
# Open project in Unity (example path, replace with your Unity install)
& "C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe" -projectPath "C:\Users\kheja\Videos\VEL\AI_Guide_Prototype"
```

- Play mode: run the relevant scene (e.g. `Assets/Scenes/Guide Parks/GuidePark1_Networked.unity` or `Assets/Scenes/Tutorial.unity`) and watch the Unity Console for debug logs (scripts use `Debug.Log` extensively).
- Headless builds / CI: this repo uses Unity; create platform-specific build commands using the editor CLI if needed. Confirm the team’s Unity install path and license before scripting builds.

## Secrets & configuration
- API keys are loaded at runtime from `Resources`. `OpenAIQueries.LoadConfig()` expects a `TextAsset` named `config` (JSON with fields `APIKey`, `PlayHTAPIKey`, `PlayHTUserID`). If absent, the code logs an error. Do not commit keys—use build-time secret injection or a local-only `Assets/Resources/config.json` ignored by git.
- `Assets/Resources/OpenAIConfiguration.asset` also exists and may be used for editor-time config—inspect it before changing keys.

## Integration points & dependencies
- Networking: Normcore (`Packages/com.normalvr.normcore@...`) is used for realtime audio and avatar sync. Look for `Realtime`, `RealtimeAvatarVoice`, and `GuideAudioSync` usages.
- External APIs: OpenAI (Whisper + GPT), PlayHT (TTS). Both are called via HTTP/client code in `OpenAIQueries.cs`.

## Quick tasks an agent can do safely
- Update inline prompt templates in `OpenAIQueries.cs` (keep 150-word constraint noted in `memoClassifications`).
- Make role strings more descriptive in `roles` list, but keep index mapping in sync with `AIGuide.role` usage.
- Add defensive null-checks around `FindObjectOfType` results to reduce runtime errors.

## Where not to guess
- Do not hard-code API keys into tracked assets. If a required config file is missing, ask the developer for the secure location or CI secrets policy.

---
If anything in this summary is unclear or you want a different focus (tests, CI, or a deep-dive into networking), tell me which area and I will iterate. Also tell me where you keep secret/config files so I can reference exact names in the docs.
