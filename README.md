# Talk Out

A 3D comedy persuasion game: type anything, talk your way out of (or into) trouble.
NPCs are driven by a **local LLM** (no cloud, no API keys) acting as a *Director* that
can only pick from engine-approved actions — the game engine is the *Referee* that
owns all state and decides outcomes.

**Scenario 001: Traffic Stop** — convince Officer Glazer not to write the ticket,
while your friend Benny panics in the passenger seat.

## Setup (fresh clone)

1. Open with **Unity 2022.3.62f3** (packages restore automatically, including
   [LLMUnity](https://github.com/undreamai/LLMUnity) and Newtonsoft JSON).
2. Download the model — see `Assets/StreamingAssets/Models/README.md` (~2.3 GB GGUF,
   gitignored).
3. In Unity: **Tools → TalkOut → Build Everything (Textures + Assets + Scenes)**.
   This generates placeholder face textures, authors all scenario data assets, and
   builds `Assets/Scenes/TrafficStop.unity` + `MainMenu.unity`.
4. Open `Assets/Scenes/TrafficStop.unity` and press **Play**.

## Playing / testing

- Type in the input field, press Enter. Talk your way out.
- **F1** — debug overlay showing the hidden Referee state (suspicion, patience,
  sympathy, amusement, flags).
- **F9** — run the scripted test corpus (`Assets/GameData/TestCorpus.txt`) through
  the full turn loop; a log lands in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project\`.
- The `Systems` object's **GameManager** has `useMockDirector` (default ON): a
  keyword-based fake director for instant iteration. Turn it OFF to use the real
  local LLM (first turn warms the model up).

## Architecture (see the TDD for details)

- `Scripts/Core` — TurnController (the spine), SceneStateModel (authoritative state),
  OutcomeEvaluator (win/lose by code, never by AI).
- `Scripts/Director` — IDirector: MockDirector | LlmDirector (LLMUnity + per-turn
  **GBNF grammar** so invalid JSON / hallucinated actions are structurally impossible),
  PromptBuilder, GrammarBuilder, DirectorValidator, FallbackLibrary.
- `Scripts/Data` — ScriptableObject scenario definitions: NPCs, action catalog
  (with engine-side effects + availability conditions), props, outcome rules.
  New scenarios = new data assets, no new code.
- `Scripts/Actors|Props|World|CameraRig` — blocky characters, face-texture
  expressions, pose micro-tweens, interactable props, camera focus, police lights.
- `Scripts/Editor` — `Tools/TalkOut/*` menu: face texture generator, scenario asset
  builder, scene builder. All idempotent; re-run any time.

## Swapping the model

Edit `Assets/GameData/LlmConfig.asset` (file name or absolute override path) and the
`LLM` component's model field in the TrafficStop scene. Any GGUF chat model works —
check its license before shipping, and remember Steam's AI-content disclosure.
