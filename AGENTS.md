# Agent notes — procedural character animation (Unity 6000.3, 2D)

Read this first. `CLAUDE.md` imports it.

## The Unity Editor and its MCP server are normally available

The user keeps the Unity Editor open on this project with the Unity MCP bridge (CoplayDev `unity-mcp`) running at
**`http://127.0.0.1:8080/mcp`** (streamable HTTP, JSON-RPC). Do not ask the user whether it is available and do not
conclude it is down from one failed call: try it first.

- Client: `python Tools/unity_mcp.py tools | call <tool> '<json>' | code '<C# body>'`. It creates a session, activates
  the `scripting_ext` (`execute_code`) and `testing` (`run_tests`, `get_test_job`) tool groups (they are off by
  default and per session) and retries while the server answers `no_unity_session` (Unity is compiling, reloading or
  busy; it usually clears within a minute).
- `refresh_unity` (`compile: request`) to compile, then poll until `EditorApplication.isCompiling` is false;
  `read_console` for errors.
- `execute_code` runs a C# 6 method body in the Editor (no tuples; use `UnityEngine.Object.FindAnyObjectByType`).
- `run_tests` (`mode`: EditMode/PlayMode, optional `test_names`) then `get_test_job` with `wait_timeout`. A job can fail
  to initialise right after a scene save or domain reload; retry.
- Batch-mode Unity cannot run while the Editor has the project open.

If the server really is unreachable, fall back to a `dotnet build` of generated globbing copies of the
`ProceduralCharacter.*.csproj` files for a compile check, and say that nothing was tested in Unity.

## Working rules

- Compile-check and commit each phase locally; the user tests in Unity themselves and says "stop" while testing.
- Never stage the user's `Assets/Scenes/SampleScene.unity` edits, `Packages/manifest.json`,
  `Packages/packages-lock.json` or `ProjectSettings/PackageManagerSettings.asset` unless a phase itself changes them
  (then stage only that hunk with `git add -p`). Stage `Assets/Scripts` and `Assets/Tests` explicitly, not `Assets`.
- A scripted `EditorSceneManager.SaveScene` also saves the user's unsaved scene edits: check `scene.isDirty` first and
  diff the scene after. An EditMode test run restores its own scene snapshot and can silently revert a scripted
  scene edit; reset serialized values before running tests, then read them back.
- Serialized settings keep their old scene values when code defaults change. Never add fields to an existing
  `[Serializable] struct` (it deserialises as 0 in the saved scene); add a new serialized field or a `[Serializable]`
  class with initializers.
- New assets need `.meta` files (the Editor creates them when it focuses; otherwise write them).
- The rig `inverseRobo` faces right (+1); a mirrored root reports bone angles as 360 minus the authored angle.
- Everything stays procedural: no Animator clips for locomotion.
- The world's look is set up by `EnvironmentSetup.SetUp()` (Editor, idempotent, refuses a dirty scene): sorting layer `Background`, the dot-grid background, lit level sprites, `ShadowCaster2D` on level blocks (authored in the scene, not at runtime), the lamp prefab. To look at the lit scene, run `python Tools/unity_mcp.py test PlayMode EnvironmentShadowTests`: it writes `Temp/LocomotionScenarios/lit-*.png`.
- Staging the scene: the user's own unsaved-then-saved edits may sit in `Assets/Scenes/SampleScene.unity` (e.g. the camera size). Stage only your hunks (filter the diff and `git apply --cached`), never `git add` the scene whole.

## Layout

- `Assets/Scripts` — assembly `ProceduralCharacter.Runtime` (gameplay). `Character/` states and driver, `Locomotion/`
  gait, `UpperBody/` arms/torso/head/aim, `Climbing/` (grab and holdable hang), `Input/`, `Weapon/` (pistol, magazine,
  `ShootingTarget`, the flying `HoverTarget` and its `IScoreSource` points), `Effects/` (pooled procedural particles), `Audio/` (clips synthesized in code, pooled voices),
  `Platforms/` (`OneWayPlatform`), `Props/` (`PushableCrate`).
- `Assets/Editor` — assembly `ProceduralCharacter.Editor`: `EnvironmentSetup` and `Tools/Procedural Character/Build
  Playground` (`PlaygroundSetup`: idempotent, refuses a dirty scene; a "Playground" root right of the furthest level
  block with a wall-jump pit, slide gap, ledge, one-way platform staircase, a 45° hill, two crates, three static targets,
  four flying `HoverTarget`s and a lamp). `python
  Tools/unity_mcp.py test PlayMode SavesPicturesOfThePlayground` writes `Temp/LocomotionScenarios/playground*.png`.
- `Assets/Scripts/Diagnostics` — assembly `ProceduralCharacter.Diagnostics`: the F1 panel, tuning domains, presets.
  Gameplay never references it.
- `Assets/Tests/EditMode` (pure logic) and `Assets/Tests/PlayMode` (scenario tests on `SampleScene`). `ScenarioStage`
  swaps the level for a flat track (it deactivates every active `BoxCollider2D`, the playground included; `Load(true)`
  keeps the level) and aims the character's pointer at a world point with `ScenarioStage.AimAt` / `AimAndFire`
  (scenario pointer seam: no Game view needed).
- `Docs/ExpansionPlan.md` is the approved plan and source of truth for crouch, momentum jumps, airborne pose, fall motion, landing, lighting
  and the F1 reorganisation.
