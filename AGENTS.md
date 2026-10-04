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

## Layout

- `Assets/Scripts` — assembly `ProceduralCharacter.Runtime` (gameplay). `Character/` states and driver, `Locomotion/`
  gait, `UpperBody/` arms/torso/head/aim, `Climbing/`, `Input/`.
- `Assets/Scripts/Diagnostics` — assembly `ProceduralCharacter.Diagnostics`: the F1 panel, tuning domains, presets.
  Gameplay never references it.
- `Assets/Tests/EditMode` (pure logic) and `Assets/Tests/PlayMode` (scenario tests on `SampleScene`).
- `Docs/ExpansionPlan.md` is the approved plan and source of truth for crouch, momentum jumps, airborne pose, fall motion, landing, lighting
  and the F1 reorganisation.
