# Gameplay expansion (branch `feature/gameplay-expansion`)

Second feature batch after `ExpansionPlan.md`. Everything stays procedural (no Animator clips, no imported audio or
VFX assets: clips, particles and sprites are generated in code). Each feature is one phase, committed separately on
this branch. `main` is not touched until the user approves the whole batch.

## Shared rules for every phase

- Read `AGENTS.md` first (Unity MCP usage, staging rules, serialized-field rules, scene-save rules).
- Gameplay code lives in `Assets/Scripts` (assembly `ProceduralCharacter.Runtime`). It never references
  `ProceduralCharacter.Diagnostics`. Expose new collaborators to the F1 panel through `CharacterParts` like the
  existing ones.
- Composition happens in `CharacterDriver.ComposeCharacter`. Settings are `[Serializable]` classes with field
  initializers (never add fields to an existing `[Serializable] struct`). New serialized fields on `CharacterDriver`
  keep defaults in code; the saved scene will pick up the defaults because the fields are new.
- New moments go through `CharacterEvents` (add events, never change existing signatures).
- Input: add actions in `PlayerInputReader` (built in code) and fields in `PlayerCommands`. Keyboard + gamepad.
- Tuning: add the feature's settings to `MovementPreset` and a tab or rows in `MovementTuningSchema` following the
  existing pattern (bump the preset version the way the existing code does, keep old presets loading).
- Tests: pure logic gets EditMode tests (`Assets/Tests/EditMode`), behaviour gets a PlayMode scenario
  (`Assets/Tests/PlayMode`, reuse `ScenarioFixture`/`ScenarioStage`; spawn any test geometry inside the test, do not
  depend on new scene objects).
- Gate per phase: compile clean in Unity (`refresh_unity` + `read_console`, no errors and no new warnings), then
  `python Tools/unity_mcp.py test EditMode` and `python Tools/unity_mcp.py test PlayMode` all green, then commit with
  only `Assets/Scripts`, `Assets/Tests`, `Docs` (and `Assets/Editor` when touched) staged. Never stage
  `Packages/*`, `ProjectSettings/*`, or the scene (except phase 10, hunk-only).
- Allocation: no per-frame GC allocations in steady state (pool particles, tracers, casings, audio voices).

## Phases

1. **Weapon** — real pistol: hitscan shot along the aim with a fading procedural tracer; impacts on colliders;
   `IShotReceiver` (hit point, normal, direction, damage, impulse); impulse applied to dynamic `Rigidbody2D`s;
   magazine (e.g. 12 rounds) with a Reload action (R / gamepad west), auto-reload on empty trigger pull, an empty
   click event; procedural reload motion (support hand goes to the grip/magazine and back, pistol tilts) blocking
   fire while reloading; `ShootingTarget` component (health, hit flash + spring wobble, knock back, respawn after a
   delay). Events: `ShotHit`, `ReloadStarted`, `ReloadFinished`, `DryFired`.
2. **Camera** — while aiming, the follow target blends toward a point between the character and the pointer
   (clamped); trauma-based screen shake (Perlin, trauma² falloff) fed by firing and by hard landings. Tunable.
3. **Procedural VFX** — pooled, code-configured particle bursts with generated textures: foot-plant dust scaled by
   impact speed, landing dust ring scaled by impact, jump kick-off dust, roll dust, muzzle smoke + spark, ejected
   shell casings (small pooled physics sprites that settle and fade), bullet impact sparks along the hit normal.
   Correct sorting layer and URP 2D lit/unlit choice consistent with the level.
4. **Procedural audio** — clips synthesized at startup with `AudioClip.Create` (filtered noise bursts and simple
   oscillators): footstep (walk/run/crouch loudness), landing thump scaled by impact, jump, roll, gunshot, dry click,
   reload clicks, casing tinkle, target hit. Pooled `AudioSource` voices with pitch/volume jitter, master volume and
   mute in the F1 panel.
5. **Wall slide and wall jump** — new state: airborne, falling, pressing into a wall (use `WallSpans`/motor contacts)
   → slide with a capped fall speed, front hand braced on the wall (IK), feet against the wall, body facing away.
   Jump → kick off away from the wall with tunable velocity and a brief steering lockout. Releasing toward-wall or
   reaching the ground ends it. Coyote window for wall jumps.
6. **Running slide** — crouch pressed while running on the ground → slide state: low hull (reuse crouch hull), speed
   decays with friction, legs extended forward, back arm trailing, ends into crouch-walk or stand (stays crouched under
   a low ceiling); jump out of a slide keeps momentum. Cooldown.
7. **Vertical input and ledges** — vertical axis (W/S, up/down arrows, left stick y). Ledge hang becomes holdable:
   Down drops from the hang, Up or toward climbs. One-way platforms (`OneWayPlatform` component or
   `PlatformEffector2D`) the character lands on from above and passes through from below; Down + Jump drops through.
8. **Physics push** — walking into a dynamic `Rigidbody2D` crate pushes it (force limited by mass), the walk slows,
   the upper body leans in and both hands reach to the crate face (IK); backs off cleanly; no jitter.
9. **Roll and climb tuning** — roll and climb settings in the F1 panel (new tabs), enable roll by default in the scene
   only if the user has not disabled it on purpose (leave scene value; add the toggle in F1).
10. **Playground and docs** — idempotent Editor menu `Tools/Procedural Character/Build Playground` that adds a
    showcase area to `SampleScene` (wall-jump shaft, slide gap, one-way platforms, crates, shooting targets) following
    `EnvironmentSetup` conventions (sorting layer, lit sprites, `ShadowCaster2D`). Run it, save the scene, stage only
    its hunks. Update `AGENTS.md` layout notes and this doc's status.

## Status

| Phase | Status | Commit |
|---|---|---|
| 1 Weapon | done | 8169009 |
| 2 Camera | done | 0bd45e2 |
| 3 VFX | done | 2521edf |
| 4 Audio | done | 13ce077 |
| 5 Wall slide / jump | done | fe3603a |
| 6 Running slide | done | 86366c9 |
| 7 Vertical input / ledges / one-way | done | 1962896 |
| 8 Physics push | done | f54ee41 |
| 9 Roll / climb tuning | done | 276ddf7 |
| 10 Playground / docs | done | this commit |
| 11 Roll on Left Alt, roll on by default, slide crouch-latch fix | done | |
| 12 Reachable one-way platform staircase | done | |
| 13 45° slopes and a hill | done | |

Also on the branch: `e54982c` (test seam: scenario tests aim at world points, independent of the Game view) and `f463c6d` (fixes from the verifier pass: disposal of recomposed collaborators, casing, audio and aim edge cases).
