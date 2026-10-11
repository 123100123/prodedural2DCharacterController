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

**Phase 15, Shift as a run intent** (after the plan above): Shift held with a direction (`RunIntent.Resolve`, filled into `PlayerCommands.RunIntent`
by the driver) means "run as soon as the character can"; Shift alone does nothing. Each full-body state answers
`ICharacterState.RunReadinessFor(commands)` itself (a `RunReadiness` with a `RunBlock` reason: the walk names aiming,
a held or latched crouch, a low ceiling or a push; the other states name themselves) and acts on the intent at its
own legal exit: the roll's recovery keeps the roll's speed and hands over part way through
(`RollRunExitSettings`), the climb stands up already moving, landings and slides carry their speed into the walk,
which then runs. The intent cancels a tap-latched crouch (the driver drops the latch when the walk reports
`CrouchLatched`) and ignores a Ctrl press still short enough to be a tap (`CrouchTap`); Ctrl held past the tap time and
a low ceiling keep the crouch, and the slide still starts on the press edge. The walk's gait still runs at the stick's
share of the run speed below the intent's dead zone (`RunIntent.GaitRuns`). The roll hands over only once the pelvis is
near its standing height (`maxStandGap`), rising toward it when running out, so the walk does not jerk it up. The state machine records the last switch, and the F1 LIVE
tab has a "Run intent" section.

**Phase 16, Aim-side running, climb run-intent re-sample:** while aiming, Shift runs when the move axis points the way the character faces (the way it aims); backpedalling while aiming stays a walk (`RunIntent.AimingBackward`, the readiness reason `RunBlock.AimingBackward`; `RunIntent.GaitRuns` and `SlidePolicy.IsRunIntent` take the facing sign). The climb already re-reads the intent every tick until it stands on top, so Shift pressed mid-climb is honoured; a test pins it.

**Phase 17, Roll momentum and forward throw:** the roll carries the speed it starts with (`RollMomentum`, `RollMomentumSettings` on the character). It reads the incoming velocity along the roll before clearing the motor, starts at that speed (u_in), and over `throwTime` rises to `inheritShare`·u_in plus a forward throw that blends from `standingThrow` to `runningThrow` with the run share, capped at `maxEntrySpeed`. Then per-phase drag and brakes decay it with no step at any phase change (`diveDrag`, `contactBrake`, `rollDrag`, `recoverDrag`), floored at `minRollSpeed` until the recovery. The recovery only drags with the run intent along the roll, brakes to a stop with no input (`settleBrake`), and pulls toward the walk speed with walk input. The speed it ends at is handed to the walk through the motor, so Shift runs on from it. A running dive lifts the pelvis higher (`standingLift`/`runningLift`) and pitches forward sooner; phase durations stay time-based. The roll also carries the body on the frame the walk hands over, so there is no stalled frame. The legacy per-phase speeds (`RollSettings.diveSpeed`/`contactSpeed`/`rollSpeed`/`recoverSpeed`/`speedEase`) are unused and hidden. F1: the ROLL tab has momentum and run-exit rows in place of the phase speeds, presets save `rollMomentum` and `rollRunExit` (version 13), and the LIVE tab has a "Roll" section.

**Phase 18, Slope-aware gait:** the stance is bounded by the legs' reach on the ground under the gait (`SlopeReach`), not by a scalar shortening. For a hip H above the ground under it, a planted foot d behind (or ahead) on ground of signed gradient g (positive where the foot is lower) is reachable while sqrt(d² + (H + g·d)²) ≤ reach (leg length × `legExtension`). `MaxLag` (push-off, with the pelvis allowed to sink by the reach reserve `maxReachDrop`, at the bob of the push-off) and `MaxLead` (landing, at the gait's own height) bound the stance travel, so the step is min(nominal, bound) and the cadence follows from cycle = stride / speed. Only where that would make a step shorter than its swing can be timed (`minSwingDuration`) does the pelvis ride lower (the derived slope drop, at most the crouch depth). The hip height is measured each gait step (its depth below the pivot changes with the lean, and on a slope the ground under hips swung behind the pivot is lower going up). The cap is applied to the gait's step range, so a swing timed for another speed is bounded the same way. The bound engages wherever the gait reads a slope; flat ground keeps the flat gait exactly as designed (its steady push-off is at the reach limit, within 1%, so the bound would only clip a run blend lagging a hard acceleration). A planted trailing foot stretched to `releaseExtension` (default 1.0, measured with the pelvis at its reach reserve after this tick's travel) steps as its normal next step when the other foot carries the body or lets it lift; the clock cue it answered is used up, so it is neither a balance step nor a resync. The default is 1.0, not the 0.985 the approved plan named: the flat run peaks at 1.0017 extension (pinned in `FlatGaitRegressionTests`), so 0.985 would fire releases on flat ground. The pelvis reserve floor in `ExtensionAtLowestPelvis` (`StrideController`) lets the measured planted extension reach about 1.012 on a 45 degree walk up; that is about 1 px of foot lift, and the bones are rigid. The foothold planner prefers footholds that keep the planted foot within reach until it may lift (falling back to the best foothold rather than none), and on a slope predicts the landing hip from the ground under it instead of the mean height of the feet (a step off a crest no longer finds every ramp foothold out of reach). The torso leans toward the uphill side by `slopeLean` per degree. `strideShortening` and `bodyDrop` are legacy (kept for presets, unused, hidden). F1: SLOPE rows for the release extension and slope lean, a LIVE "Slope gait" section and an EXTENSION RELEASE flag; presets are version 14. `FlatGaitRegressionTests` pins the flat walk and run measured before the change (3%); `SlopeGaitScenarioTests` runs 15°/30°/45° hills and writes `Temp/LocomotionScenarios/slope-run-{up,down}-<angle>.png`.

**Phase 19, Pistol and holster on the rig:** `PistolView` builds the pistol, its holster and the hand's `WeaponPivot` at runtime (`PistolTextures` draws them), placed by `PistolHandlingSettings`; shots, the flash, smoke and casings leave the barrel's `Muzzle` and `EjectionPort`, and a shot twists the barrel up (`recoilTwist`). Presets save `pistolHandling` (version 15).

**Phase 20, Procedural draw and holster:** the pure `DrawCycle` runs Holstered → Drawing → Equipped → Holstering → Holstered, each move with a progress 0–1 (`drawDuration` 0.35 s, `holsterDuration` 0.4 s); `PistolHolder` drives it from aim and fire and only asks the arms where to reach. Drawing: the front hand travels from its swing to the holster along an arc (`handArcLift`), grips at `gripShare` (the pistol moves to the `WeaponPivot` keeping its world pose, then eases into the grip), carries the pistol up through the front to the aim grip the `ShootingStance` works out, the support hand joining from `supportJoinShare`; at 1 the stance takes both hands. Holstering is the same path backwards: the support hand lets go first, the pistol goes into the holster at `insertShare`, the hand returns to the swing. The holster now hangs from the front thigh (`bone_thighF`, a drop-leg holster), so its offset and angle are in that bone's space. Interruptions (plan §13): one fire press during the draw is buffered and fires the frame the pistol arrives (dropped if aim is released; presses do not stack); aim released before the grip goes straight back to Holstered, after it holsters from the mirrored progress; aim pressed while holstering draws again from the mirrored progress before the insert, and restarts from the hand's place after it; facing flips, jumps and falls carry on. A state that takes the arms (roll, climb, wall slide) or a crate push quick-holsters (`quickHolsterDuration` 0.2 s) on the front arm only, over the state's own request, which the path hands the arm back to; the pistol is drawn again when it ends if aim is still held, and fire is ignored meanwhile. The magazine timer always runs; the reload motion shows only for a reload begun while Equipped. Sliding draws and fires as walking does. F1: AIM rows for the durations and shares, a LIVE "Weapon" section (state and progress, parent, front and support grip errors, muzzle, buffered fire); presets are version 16 (a version 15 holster offset and angle were in the pelvis' space and load at their defaults). `PistolDrawScenarioTests` covers every rule and writes `Temp/LocomotionScenarios/pistol-draw-{draw,holster}-{right,left}.png`; scenario tests that need a shot on a given frame draw first with `ScenarioStage.DrawAt`.

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
| 14 Helicopter hover targets with points popups | done | |
| 15 Shift as a run intent | done | |
| 16 Aim-side running, climb run-intent re-sample | done | |
| 17 Roll momentum and forward throw | done | |
| 18 Slope-aware gait: stance bounded by leg reach (SlopeReach), trailing-foot release at full extension, slope lean | done | |
| 19 Pistol and holster on the rig, muzzle from the barrel tip | done | 4155b75 |
| 20 Procedural draw/holster cycle and its interruptions | done | |

Also on the branch: `e54982c` (test seam: scenario tests aim at world points, independent of the Game view) and `f463c6d` (fixes from the verifier pass: disposal of recomposed collaborators, casing, audio and aim edge cases).
