# Procedural Character Expansion — Implementation Plan

Planning only. No project code is touched until this plan is approved. The plan is grounded in a read of the repository (states, `StrideController`, gait/posture/arm systems, F1 panel, rendering setup) and in the grill-me decisions in §3. (The `grill-me` skill was not installed in this session, so the grilling was done as four focused question batches.)

---

## 0. Context

The character is a fully procedural 2D humanoid: a spring-carried pelvis over a gait-clock-driven pair of IK legs, with posture/torso/arm/head systems layered on top. Walk, run, idle, aim/shoot, roll and climb exist. This expansion adds: crouch locomotion, momentum-carrying jumps, a deliberate airborne pose (ascent → apex → descent), panicked long-fall arm motion, impact-scaled landing that flows back into the gait, URP 2D lighting (ambient, player, shadow-casting muzzle flash), and a reorganised, domain-owned F1 panel.

Direction preserved: no Animator clips; everything stays runtime-procedural and spring-based; new systems extend the existing seams instead of adding parallel ones.

Project conventions this plan follows (from the user's saved workflow notes): compile-check + commit per phase; the user tests in Unity themselves; Unity MCP package entries in `Packages/manifest.json` / `packages-lock.json` stay **unstaged** (stage only the URP lines with `git add -p`); verification runs through the Unity MCP HTTP endpoint (`run_tests` + `get_test_job`); scripted scene saves overwrite user edits, so scene edits are made once, deliberately, and re-read first; stale serialized values must be reset (see §14).

---

## 1. Current architecture (what exists today)

**Tick** — everything runs from `CharacterDriver` (`Assets/Scripts/Character/CharacterDriver.cs`). No FixedUpdate, no execution-order attributes.
- `Update`: `input.ReadCommands()` → `machine.Tick` → `posture.Tick` → if `Current.AllowsUpperBodyActions`: aim (`ShootingStance.Tick`) *or* (`Head.HoldLevel` + `ArmSwing.Tick`), then `TorsoMotion.Tick`.
- `LateUpdate`: `IKManager2D` (order −10) solves legs → `rig.ApplyUpperBodyPose` (arms, head) → `FollowCamera.Tick`.
- Composition is in `ComposeCharacter` (called from `OnEnable` only if `input == null`).

**States** (`Character/States/`): `WalkState` (grounded; owns the gait call), `JumpWindupState`, `AirborneState`, `RollState` (disabled in scene), `ClimbState`. `CharacterStateMachine.ChangeTo` = Exit → swap → Enter; a trigger site must `return` immediately.

**Grounded gait** — `StrideController` (`Locomotion/`). Ground speed ramps (`GroundSpeedRamp`); a distance-driven gait clock (`StepScheduler`/`GaitCycle`) cues legs half a cycle apart; `LegStepper` swings each foot to `FootholdPlanner` landings; `BodyCarriage` moves the pelvis as springs; `PelvisSupportMotion` offsets it over the support foot. Walk/run are two `GaitProfile` structs blended field-by-field by `Pace.Running` (`GaitProfile.Blend`). `Pace.Moving = |v| / walk.speed` scales bob/surge/lean/arm swing. The walk/run **pair is hard-wired** through a `(walk, run, transitions)` tuple in `LocomotionPace.RunningAt`, `SwingForecast.Request/GaitAt`, `StrideController.WallProbeDistance`, `AccelerateGround`, `GaitPreview`, the preset and the schema.

**Pelvis height** — `BaselineHeight = StanceHeight + bodyHeight − pelvisDrop·Moving − HeldCrouch` (`StrideController.cs:585`); `HeldCrouch` is set only by `JumpWindupState` **and is also read by `ArmSwing` as a "wind-up" flag** (`HeldCrouch > 0` → arms swing back 40°) — a hidden coupling a new crouch would trip.

**Air** — `AirborneState`: steering `MoveTowards(vx, axis·airSpeed, 30·dt)`; gravity via `KinematicBodyMotor.ApplyGravity` (×0.75 inside `|vy|<2`, ×1.6 falling); legs `Lerp(tucked, reaching, InverseLerp(4,−4,vy))` via `LegStepper.SwingFreelyToward`; arms `ArmSwing.AirbornePose` (raise 45–80° by `vy`); landing = swept ray + `TrySettleStance` (both feet snap-settle in 0.12 s at `±stanceHalfWidth`) + body put at `ground+bodyHeight−0.3` + 60 % of impact speed handed to the carriage spring as the squash, then `WalkState.Enter` → `BeginGroundedMotion` + `RestartGaitFromStance` (**the gait fully resets on every landing**).

**Jump velocity today** — `JumpWindupState.cs:43-45`: `launch = (moveAxis·airSpeed(6), LaunchSpeedForHeight(g, 2.4))`. Run speed (9) is discarded; ground speed is ramped to 0 during the 0.16 s windup (`TickWalk` with direction 0); `Walk.Exit` writes `Motor.Velocity = Stride.BodyVelocity` *after* `Airborne.Fall/Launch` already set it (so a walk-off-ledge keeps the raw carriage-spring velocity, not the clamped one). No variable jump height; no jump-held input.

**Input** — `PlayerInputReader` builds actions in code (no `.inputactions`): A/D, Space, **C = roll**, Shift run, RMB aim (wrapped in `HoldOrTapToggle`), LMB fire. No crouch, no S/Down.

**Hull** — virtual capsule in `KinematicBodyMotor` (`hullSize` 0.8×2.45, `hullOffset` y 0.93), no setter, no overlap/headroom query; no `Collider2D` on the character.

**Events** — `CharacterEvents.Fired((muzzle, direction))` is raised in `ShootingStance.FirePistol`; `Pistol.MuzzleFor = hand + aim·0.15` (no muzzle transform). Only subscriber today: torso kick.

**F1 panel** — UI Toolkit, schema-driven: `LocomotionTuningSchema.Build()` → `Section`s of `TuningParameter` (string group, lambdas over a `LocomotionPreset` value object). `LocomotionTuning.Capture/Apply` bridge to 5 owners (`StrideController`, `LocomotionPosture`, `TorsoMotion`, `HeadLook`, `ArmSwing`). Hand-written: LIVE (`LiveTelemetryView`, `PreviewControls`), DEBUG (`DebugDrawToggles`), `PresetToolbar`. Tabs: LIVE, IDLE, WALK, RUN, TRANSITIONS, BODY, UPPER BODY, DEBUG (105 rows). Not editable today: air, jump, roll, climb, shooting, foothold limits, arm limbs.
Presets: unversioned `JsonUtility` JSON in `persistentDataPath/LocomotionPresets`; **missing keys load as 0, not defaults**.

**Dependency direction** — all in one asmdef (`ProceduralCharacter.Runtime`). `CharacterDriver.BuildInspector()` constructs the tuning/telemetry objects, so gameplay *does* reference diagnostics (read-only by convention, not by assembly).

**Rendering** — **Built-in RP**. No URP package, `m_CustomRenderPipeline: {fileID: 0}` in Graphics and all 6 quality levels, Gamma color space, every sprite on `Sprites-Default` (unlit), no lights, one Default sorting layer, camera orthographic with solid blue-grey background. Package manifest diff is only the Unity MCP bridge.

---

## 2. Gap analysis (per feature)

| Feature | Exists / reusable | Missing | Coupled wrongly | Owner |
|---|---|---|---|---|
| Crouch | `HoldOrTapToggle`; `GaitProfile.Blend`; pelvis spring baseline; `IdleStanceSettings`; `TwoBoneIk` knees | input, crouch profile, share blend, hull shrink + headroom check, a 3rd gait profile in all (walk,run) tuples | `HeldCrouch` doubles as jump windup + arm flag; `Pace.Moving` normalised by `walk.speed` | new `CrouchController` + `CrouchSettings`; gait through new `GaitSet` |
| Jump momentum | `Stride.GroundSpeed` (clean ground speed), `JumpMath` | launch from ground velocity, speed-scaled windup, variable height, air steering that preserves momentum, `Fall` that carries full speed | `Walk.Exit` overwrites handoff velocity; launch uses *input*, not velocity | `JumpMomentum` (pure) + `JumpSettings`; steering in `AirSteering` (pure) |
| Airborne pose | `SwingFreelyToward`, `ArmSwing` springs, posture/chest springs | ascent/apex/descent weights, push-off, speed-dependent poses, pose reaching upper-body systems | posture lean/torso gait-flex go to 0 when not driving gait; arm raise is a loose two-number lerp | `AirPhase` (pure) + `AirPoseSettings` + `AirPoseFrame` on `CharacterRig` |
| Long-fall arms | `ArmSwing` 4 springs, `ArmLimb` eased weights | time-in-air/falling counters, smooth deterministic oscillators, aim suppression | `ArmSwing.Tick` is *not called* while aiming → springs go stale | `FallSecondaryMotion` (pure) + settings; consumed by `ArmSwing` |
| Landing | swept touchdown, squash via carriage spring, `Landed` event, `FollowCamera` kick | predicted-ground reach, impact scaling, in-motion foot placement, rhythm continuation, speed loss, torso fold | `TrySettleStance` always uses stationary stance columns; `WalkState.Enter` always restarts the gait | `LandingSettings` + `LandingForecast` (pure); `StrideController.TrySettleLanding` |
| Lighting | nothing | URP 2D, lit materials, Light2D rig, muzzle controller, settings | — | new `Lighting/` asmdef; `LightingSettings` |
| F1 refactor | schema/sections/rows, `UiInputCapture`, presets | generic domains, versioned overlay presets, tabs per domain, telemetry for new systems, asmdef boundary | Inspector built inside `CharacterDriver`; duplicated params in 3 tabs; panel edits via a whole-preset re-apply | `Diagnostics/` asmdef; one `TuningDomain<T>` per preset type |

**Things I challenge or correct**
1. **Phase order.** The request puts the F1 refactor at Phase 9. But every new feature needs tunable settings; building 6 new settings groups against the old flat 8-tab preset and then re-organising would mean doing the schema work twice. Recommendation (and plan): do the **preset/schema/asmdef foundation first (Phase 1)** and have each feature phase add *its own* tab + telemetry as it lands.
2. **Lighting requires a render-pipeline migration**, not a component. Built-in RP has no `Light2D`. It is the single riskiest, least reversible change; it is isolated in its own phase so locomotion work can ship without it.
3. **Muzzle flash casting shadows** is possible only with `ShadowCaster2D` on level geometry, and the light must not be occluded by the character's own sprites or sit inside a caster. Handled in §10; flagged as a risk.
4. **"Crouch inherits `HeldCrouch`"** would silently trigger the jump-windup arm swing → `HeldCrouch` is split (§5.3).
5. **Adding fields to existing `[Serializable] struct`s** (e.g. `GaitProfile`) deserialises as 0 in the saved scene. Plan never extends existing structs; new settings are new top-level serialized fields or `[Serializable] class`es with initializers (§14).
6. **"Aiming/shooting must be explicitly defined in the air"** — defined in §8.4.

---

## 3. Decisions (grill-me results)

**Crouch**
- Input: **Left Ctrl, hold-or-tap** (quick tap < 0.25 s latches on/off; hold = crouch while held). Gamepad: right-stick click. (`C` stays roll.)
- Speed: **one slow crouch gait, ~1.8 u/s. Shift ignored while crouched.** Run→crouch decelerates through the existing `GroundSpeedRamp` (no slide).
- Hull: **lower hull + blocked stand-up**. Pressing crouch shrinks the hull by `depth`; releasing under a low ceiling keeps you crouched until clear.
- Combos supported: **aim + shoot crouched, crouch → jump**. Not requested, defaulted by me: roll is standing-only (crouch is released when roll starts); landing with crouch still requested continues crouched (crouch is a persistent modifier, so no special case).

**Jump / momentum**
- Carry **100 % of ground velocity + small forward bonus** (≈ +1.2 u/s at full run, scaled by speed share).
- Air control: **keep today's steering** (30 u/s², `airSpeed` 6) — with one deliberate correction so momentum survives: neutral input applies only a gentle `neutralDrag` (default 2 u/s², settable to 30 to restore old behaviour); opposite input brakes at full `airAcceleration`; forward input accelerates only up to `airSpeed`, never trims carried speed above it.
- Windup: **speed-scaled, momentum-keeping.** Standing: full 0.16 s load; walking: shorter/shallower; running: ~0 (pushes off mid-stride). Ground speed is *not* ramped down during windup.
- Height: **variable** (release Space early → hop) and **crouch jump = 85 %** height.

**Airborne**
- Style: **athletic, weighty** (not cartoonish). Ascent has visible push-off extension; apex relaxes/tucks; descent reaches.
- Fall arms: **panicked windmill** — large, fast, asynchronous arm arcs that grow with fall time.
- Long fall legs/torso: **legs stay braced with slight loosening**; torso pitches a little forward; head stays stabilised.
- Aiming in the air: **aim always wins** (windmill blends out over ~0.15 s while aiming, back in on release).
- Landing: **impact-scaled squash + gait continuation** (no forced recovery state).

**Lighting**
- **Migrate to URP 2D Renderer** (keep Gamma colour space).
- Mood: **dusk** — cool dim ambient (~0.65, never darker than ~55 % anywhere) + warm soft player light.
- Muzzle flash: orange, instant attack, fast decay, **casts shadows**.
- Environment lights: **authored in the scene; one global lighting profile** (no area zones).

**F1 / presets**
- Presets: **separate Movement and Lighting presets**, both versioned, loading overlays onto code defaults.
- Tabs: **LIVE, IDLE, WALK, RUN, CROUCH, JUMP, FALL (with Landing group), AIM, LIGHTING, TRANSITIONS, ADVANCED, DEBUG**. `UPPER BODY` is dissolved into per-mode tabs + ADVANCED.

---

## 4. Target design

### 4.1 Mode architecture (what is a state, what is a modifier)

Existing states stay: `Walk` (grounded locomotion), `JumpWindup`, `Airborne`, `Roll`, `Climb`. **No `CrouchState`, no `LandingState`.**

- **Crouch = a persistent modifier layer** (`CrouchController`, share 0‥1), orthogonal to the state machine. Walk, JumpWindup and Airborne all allow it; Roll/Climb force target 0. This is why crouch→jump, crouch+aim, landing-into-crouch and turning-while-crouched need no transition code.
- **Idle/Walk/Run/Crouch-walk** are all points in one continuous gait space (`GaitSet`), not states.
- **Rising / Apex / Descent** are *not* states: they are three continuous weights from vertical velocity (`AirPhase`), all inside `AirborneState`.
- **Landing** is a ~0.25 s *prep* phase inside `AirborneState` (blend `landingPrep`) plus a *post-touchdown transient* inside the grounded gait (`LandingCompression` + squash), not a state.

### 4.2 State / behaviour diagram

```
                       ┌──────────── crouch modifier (share 0..1) applies in Walk, Windup, Air ───────────┐
 Idle ⇄ Walk ⇄ Run   (one gait space; GaitSet blends by ground speed; Idle=|v|<0.15)                       │
  │       │     │                                                                                         │
  │  Crouch held ⇒ share→1: Idle→CrouchIdle, Walk→CrouchWalk, Run→(decel via ramp)→CrouchWalk             │
  │  Crouch released (headroom clear) ⇒ share→0; if blocked ⇒ stays crouched                              │
  ▼                                                                                                       │
 Jump pressed (Walk) ──→ JumpWindup ──(windup scaled by speed; 0 when running)──→ Launch ─┐                │
 Ledge lost (Walk) ────────────────────────────────────→ Fall (carries full ground speed)─┤                │
                                                                                          ▼                │
                      Airborne ── vy>0: ASCENT ──vy→0 band──▶ APEX ──vy<0──▶ DESCENT ──▶ (timeFalling>0.5s)│
                                  push-off pose   relax/tuck     reach down      secondary arm motion grows│
                                  Space released early ⇒ hop (velocity cut)       aim ⇒ windmill suppressed │
                                                                                          │ landingPrep      │
                                                                                          ▼ (≈0.25 s out)    │
                                         TOUCHDOWN: place feet in motion, squash ∝ impact, speed loss ≤20% ──┘
                                                                                          │
                                  Walk (gait continues: lead foot = first plant, trail foot steps next)
 Walk/Air ──Climb──▶ Climb ──▶ Walk      Walk ──Roll(C)──▶ Roll ──▶ Walk (crouch released on roll)
```

### 4.3 Settings ownership

Each settings type is owned by exactly one runtime component and edited by exactly one F1 tab. **No new field is ever added to an existing struct** (§14).

```
StrideController ── walk, run (GaitProfile, unchanged), crouchGait (GaitProfile, NEW field), transitions,
                    balance, idle, body (BodyMassSettings), bodyHeight, footholdLimits, stepArc
CrouchController ── CrouchSettings (NEW class)  depth, enter/exit spring, hull drop, stance width scale,
                    idle lean, chest fold, idle arm bend, input tap window
CharacterDriver  ── AirborneSettings (airSpeed, airAcceleration, neutralDrag NEW, coyote, buffer)
                    JumpSettings (height, windup, crouchDepth, carry, forwardImpulse, minHopHeight,
                    crouchJumpScale, windup speed band)  — extended classes, initializers
                    AirPoseSettings (NEW class)  ascent/apex/descent pose sets, push-off, speed blend
                    FallSecondarySettings (NEW class)  onset/full time, windmill amp/freq, leg loosen
                    LandingSettings (NEW class)  prep time, impact range, sink, compression, speed loss, fold
                    ShootingSettings (existing, now tunable)  Pistol cooldown/muzzle offset
LocomotionPosture ─ PostureSettings   TorsoMotion ─ chest spring, BreathingSettings
HeadLook ───────── HeadMotionSettings ArmSwing ─── ArmSwingSettings (+ windupSwing)
Lighting (new asmdef) ─ LightingSettings (NEW class: ambient, player, muzzle) on SceneLightingController

Presets:  MovementPreset (v2)  ← every runtime settings group above except lighting
          LightingPreset (v1)  ← LightingSettings only
```

Dependency direction (enforced by asmdefs, §11):
`Runtime (gameplay; exposes settings properties + read-only snapshots)` ← `Lighting (Light2D; subscribes to events)` ; `Diagnostics (tuning facade + UI)` → `Runtime`, `Lighting`. Runtime references neither. The locomotion code runs identically with Diagnostics absent.

### 4.4 Tick order (final)

`CharacterDriver` gets `[DefaultExecutionOrder(-50)]` so `Events` and the rig exist before `Lighting`/`Diagnostics` `OnEnable`.

`Update`
1. `input.ReadCommands()` → `PlayerCommands` (+ `CrouchRequested`, `JumpHeld`).
2. `crouch.Tick(dt, commands, machine.Current.AllowsCrouch)` — resolve request vs headroom, advance share spring, push hull drop to motor, push `CrouchShare` to stride.
3. `machine.Tick`:
   - grounded: `TickWalk` (gait with `GaitSet` crouch weight) → pelvis spring → support offset.
   - JumpWindup: gait keeps ticking at *held* ground speed.
   - Airborne: steering → gravity → landing probe (one ray; `landingPrep`, `predictedGround`) → motor move → `AirPhase` weights → `AirPoseFrame` solve (incl. fall secondary motion) → leg pose.
4. `posture.Tick` (reads `AirPoseFrame` when not driving gait).
5. Upper body: aim (`ShootingStance`, raises `Fired`) **or** `ArmSwing.Tick` (always updates its springs; during aim it tracks without requesting reach) → `TorsoMotion.Tick`.

`LateUpdate`: IK solve (−10) → `rig.ApplyUpperBodyPose` → `FollowCamera.Tick` → `MuzzleFlashLight.LateUpdate` (applies envelope the same frame `Fired` was raised) → `PlayerLight` follows pelvis (child transform, no code).
`Update` (panel, visible only): telemetry read + debug draw.

---

## 5. Crouch — design

### 5.1 `GaitSet`: the one place a third gait lives
New `Locomotion/GaitSet.cs` replaces the `(walk, run, transitions)` tuple everywhere (`LocomotionPace.RunningAt`, `SwingForecast.Request/GaitAt`, `StrideController`, `GaitPreview.IntendedSpeed` params). It holds `walk, run, crouch, transitions, crouchShare` and exposes:
- `ProfileAt(speed, runningShare)` = `GaitProfile.Blend( Blend(walk, run, runningShare), crouch, CrouchWeight(speed) )`
- `CrouchWeight(speed) = crouchShare · (1 − SmoothStep(crouch.speed, walk.speed, |speed|))` — gait *timing* follows the ground speed (a runner who crouches decelerates through run→walk→crouch-walk timing instead of tiny fast steps), while pelvis height/lean follow `crouchShare` directly.
- `ReferenceSpeed(speed) = Lerp(walk.speed, crouch.speed, CrouchWeight(speed))` — fixes `Pace.Moving` so crouch walking at 1.8 is "moving = 1", not 0.5 of a walk. `LocomotionPace.MovingAt` takes this instead of `walk.speed`.
- `TargetSpeed(direction, running)` = `Lerp(direction·(running ? run.speed : walk.speed), direction·crouch.speed, crouchShare)`; replaces the `walk.speed/run.speed` pick in `AccelerateGround`.
Future gaits (e.g. a slide or sneak) add a field here and one blend term; nothing else changes.

### 5.2 `CrouchController` (new, `Character/CrouchController.cs`) + `CrouchSettings`
Pure/plain class owned by `CharacterRig`.
- `CrouchIntent.Resolve(requested, headroomBlocked, allowed)` (pure, tested): `allowed ? requested || (share>0.01 && headroomBlocked) : false`. Tap/hold latch lives in the existing `HoldOrTapToggle` inside `PlayerInputReader`.
- Share = `SecondOrderDynamics` (default ~3.5 Hz, damping 0.9) → feet stay planted (only the pelvis target changes; IK bends the knees), so entering/leaving crouch never snaps both legs.
- Headroom: while `share > 0.01`, one `Physics2D` capsule overlap (pre-allocated results array) of the *standing* hull at `(body.x, Stride.StanceHeight + BodyHeight + hullOffset.y)` on `solidMask`. Skipped when standing.
- Motor: new `KinematicBodyMotor.CrouchHullDrop { set; }` shrinks the hull from the top by `depth·share` (bottom fixed) and re-centres the offset; `FreeDistance`/wall spans use it automatically.
- Effects, all fed from `crouchShare`/`CrouchGaitWeight`:
  - pelvis: `BaselineHeight −= crouch.depth · share` (new term, separate from jump windup).
  - gait: `crouchGait` profile (below).
  - posture: `LocomotionPosture` base lean += `crouch.idleLean · share` (moving lean comes from the profile).
  - torso: `TorsoMotion` chest target pitch += `crouch.chestFold · share`.
  - arms: `ArmSwing` base elbow bend += `crouch.idleArmBend · share`; swing amplitude from the profile.
  - idle stance: `StanceHalfWidth` scaled by `Lerp(1, crouch.stanceWidthScale, share)` (default 1.15, inside `footTolerance` so no re-settling steps).
  - head: rides pelvis/posture; stabilisation unchanged.
  - terrain: foothold planner/limits unchanged (verified in tests; lower hips only make reach easier).
  - aim/shoot: unchanged code; shoulders sit lower, speed already ≤ crouch speed.

### 5.3 `HeldCrouch` split
`StrideController.HeldCrouch` → `WindupCrouch` (jump only). `ArmSwing` windup flag reads `WindupCrouch`. Crouch depth is the separate `crouchShare` term. Names change in `JumpWindupState`, `StrideController`, `ArmSwing`.

### 5.4 Default crouch gait (`GaitProfile.Crouch`, tunable live)
speed 1.8, accel 8, decel 12, duty 0.70, contactLeadShare 0.5, stepLengthPerRootSpeed 0.60, step 0.30–1.1, swing 0.30–0.70, liftHeight 0.22, pelvisDrop 0.05, bob 0.04, lowAfterContact 0.06, surge 0.03, plantImpulse 0.05, pushOff 0, lean 14°, pitchOscillation 0.8, armSwing 12°, armBend 0.45. `CrouchSettings`: depth 0.75, spring (3.5, 0.9, 0), hullDrop = depth, idleLean 10°, chestFold 8°, idleArmBend 0.35, stanceWidthScale 1.15.

---

## 6. Jump momentum — design

### 6.1 Equations (pure `JumpMomentum`, `JumpMath`, `AirSteering`)
```
groundV      = Stride.GroundSpeed                     (signed; NOT Carriage velocity — that carries spring noise)
speedShare   = clamp01(|groundV| / run.speed)
bonus        = jump.forwardImpulse · SmoothStep(0, 1, speedShare)        // default 1.2
launchX      = clamp( groundV · jump.carry + sign(groundV)·bonus,  ±(run.speed + jump.forwardImpulse) )   // carry = 1.0
height       = Lerp(jump.jumpHeight, jump.jumpHeight · jump.crouchJumpScale, crouchShare)   // 0.85
launchY      = JumpMath.LaunchSpeedForHeight(gravity, height)
variable height:  on Space release while vy > cutSpeed and launched by a jump:
                 vy = min(vy, JumpMath.LaunchSpeedForHeight(gravity, jump.minHopHeight))      // default 0.8
                 (if Space is already up at launch, the cut applies immediately → hop)
windup:  scale = 1 − SmoothStep(0.15, 0.85, speedShare)
         duration = max(jump.minWindup(0.02), jump.windupDuration · scale);  depth = crouchDepth · scale · (1 − crouchShare)
         during windup TickWalk keeps the held intent (direction = sign(groundV)) — ground speed is not ramped to 0.
Fall (walked off a ledge):  carries launch X = clamp(groundV, ±run.speed), Y = 0  (was clamped to ±airSpeed = 6).
```
Air steering (`AirSteering.Step(vx, axis, dt, air)`):
```
axis == 0                         → vx → 0 at neutralDrag (2)
sign(axis) == −sign(vx) (reverse) → vx → axis·airSpeed at airAcceleration (30)
sign(axis) == sign(vx) or vx≈0    → if |vx| < airSpeed: accelerate toward axis·airSpeed at airAcceleration; else leave unchanged
```
Coyote jump now also uses `JumpMomentum` (keeps current vx, new vy).

### 6.2 Bug fix included
`AirborneState.BeginFlight` sets `Motor.Velocity` *after* `Machine.ChangeTo(this)` (so `WalkState.Exit`'s write cannot overwrite it); `WalkState.Exit` stops writing velocity; Roll/Climb read `Stride.BodyVelocity` explicitly where they need it (audit these two call sites in Phase 3).

---

## 7. Airborne pose — design

### 7.1 `AirPhase` (pure): continuous weights from vertical velocity
```
t       = clamp(vy / airPose.apexBand, −1, 1)        // apexBand default 5 u/s (≈ existing poseBlendSpeed band, tunable)
ascent  = SmoothStep(0,1, t)    descent = SmoothStep(0,1, −t)    apex = 1 − max(ascent, descent)
```
Depends on `vy`, never a timer. Short hop → apex band reached quickly and no fall secondary motion; long fall → time-gated (§8).

### 7.2 `AirPoseFrame` and `AirPoseSettings`
`AirPoseFrame` (readonly struct on `CharacterRig.AirPose`, solved in `AirborneState.Tick`, zero outside the air): lead/trail foot offsets (body space), arm raise & elbow for each arm, `leanDeg`, `chestPitchDeg`, and an overall `weight`. Consumers read it: `AirborneState` (legs), `ArmSwing.AirbornePose`, `LocomotionPosture` (air lean target instead of 0), `TorsoMotion` (chest pitch). Each existing consumer keeps its own spring so the pose never snaps.
`AirPoseSettings` holds a *pose set* (`AirPose`: leadFoot, trailFoot, armFront, armBack, elbowBend, leanDeg, chestPitchDeg) for **Ascent**, **Apex**, **Descent**, each in a *standing* and a *running* variant, blended by `launchSpeedShare` (`|launchVx| / run.speed`, with a small `currentSpeedShare` term). Plus push-off: `pushOffDuration` 0.18 s, `pushOffExtension` (trail leg stays near straight/behind while the pelvis rises, `weight = 1 − SmoothStep(0, pushOffDuration, timeSinceLaunch)` on the ascent trail pose) so the character visibly *drives* off the ground rather than teleporting its feet.
Defaults (athletic, tunable): Ascent standing — lead knee up (0.7, −1.35), trail (−0.55, −2.3), arms raise 115°, chest −6° (extension); running — lead (1.0, −1.2), trail (−0.9, −2.1), front arm 110°/back arm 55°, lean +8°. Apex — both knees tucked (0.45, −1.45)/(0.05, −1.5), arms 70°, chest +4°. Descent — lead (0.3, −2.05), trail (−0.2, −2.15) (the existing reaching offsets), arms 45°, braced; running variant adds forward lean.
Legs keep going through `LegStepper.SwingFreelyToward` + `FootTargetAboveGround` (unchanged).

---

## 8. Long-fall secondary motion & aim in the air

### 8.1 Counters
`AirborneState` tracks `timeAirborne`, `timeFalling` (accumulates only while `vy < 0`), `timeSinceLaunch`, `launchSpeedShare`, and a per-airborne-entry `seed` (hash of a monotonically increasing jump counter — deterministic, not `Random` per frame).

### 8.2 `FallSecondaryMotion` (pure, allocation-free)
```
timeGate  = SmoothStep(onset 0.5 s, full 1.8 s, timeFalling)
speedGate = SmoothStep(6, 16, −vy)
S         = timeGate · Lerp(0.5, 1, speedGate) · (1 − 0.6·landingPrep) · (1 − aimSuppression)     ∈ [0,1]

Phases (deterministic, C∞-smooth — no Random, no per-frame noise):
  rate_i(t)  = Lerp(0.9, 2.4, S) · (1 + 0.2·sin(2π·0.31·t + ψ_i))        // Hz
  φ_i       += 2π · rate_i · dt                                            // φ_i seeded from `seed`
  armFront   = raise + A·S·( 0.7·sin φ_F + 0.3·sin(1.618·φ_F + ψ) ) + reactive
  armBack    = raise + A·S·( 0.7·sin(φ_B+π·0.85) … )   φ_B detuned ×1.13 from φ_F (left/right never lock in step)
  elbow      = 0.3 + 0.5·S·(0.5 + 0.5·sin(1.37·φ + ψ))
  reactive   = S · ( k_v·speedGate  −  k_a·clamp(accelX) )                 // arms rise with fall speed, trail horizontal acceleration
  legs:      foot offset += legLoosen·S·(0.12, 0.10)·sin(φ_leg)            // braced, slight dangle
  torso:     leanDeg += S·forwardPitch (≈6°); head: unchanged (HoldLevel)
Amplitude A default 75° around the descent raise (≈ 100°); output goes through the existing ArmSwing springs, so everything is low-passed.
```
Stage behaviour is continuous: 0 → 0.5 s no extra motion (a normal hop/jump is untouched), 0.5–1.8 s ramp, > 1.8 s full windmill; the last 0.25 s before touchdown damps it (`landingPrep`) so arms come in for balance.

### 8.3 Short fall must not over-animate
Typical jump (≈ 2.4 u) falls ≈ 0.3 s; `timeGate` is 0 below 0.5 s. A test asserts this.

### 8.4 Aiming / shooting in the air (explicit)
`aimSuppression` smooths 0↔1 over 0.15 s. Aiming still routes through `ShootingStance` exactly as on the ground (arms on the two-hand grip, recoil, `Fired`). `ArmSwing.Tick` is changed to *always* run its springs (during aim it advances them toward the current pose without calling `ReachToward`), so releasing aim blends back from live state instead of stale springs. Legs/torso fall pose still applies while aiming. Shooting while crouched uses the same path (no special casing).

---

## 9. Landing — design

### 9.1 Prediction (`LandingForecast`, pure)
One ray per airborne frame (already needed): `StrideController.TryFindGroundBelowBody(depth = bodyHeight + max(fallThisFrame, lookAhead))` with `lookAhead = max(0.5, −vy · prepTime)`. From the hit: `distance = origin.y − ground.y − bodyHeight` (clearance), `predictedGround` stored in the snapshot, `timeToImpact = (−v + √(v² + 2·g_eff·d)) / g_eff` with `v = −vy`, `g_eff = gravity · fallMultiplier`, `landingPrep = 1 − SmoothStep(0, prepTime(0.25), timeToImpact)`. Touchdown still fires when the clearance ≤ `fallThisFrame` (existing swept logic, same ray). Prep drives: feet reach to the predicted stance columns (feet blend from the descent pose to `predictedGround`), knees soften (descent pose weight), flail damping.

### 9.2 Touchdown
```
impactShare = SmoothStep(land.lightImpact(6), land.hardImpact(20), impactSpeed)
sink        = Lerp(land.sinkLight(0.15), land.sinkHard(0.7), impactShare)    // replaces AirborneSettings.touchdownSink (0.3)
carriedFall = min(land.maxCarried(9), impactSpeed · land.speedCarry(0.6))     // unchanged semantics, moved
vx_after    = vx · (1 − land.speedLossHard(0.20)·impactShare)
compression = impactShare · land.compressionHard(0.5) · (1 − 0.5·crouchShare)  // new transient, subtracted from BaselineHeight, decays with a spring (~0.35 s)
torso fold  = TorsoMotion.Kick( −impactShare·land.torsoFoldHard(25°) )
```

### 9.3 Continuing the gait (no reset)
- `StrideController.TrySettleLanding((x, facingSign, groundY), vx)` replaces `TrySettleStance` at touchdown. If `|vx| < 0.6·walk.speed` → existing stationary stance columns (unchanged behaviour). Otherwise columns come from the gait at that speed: lead foot at `hipX + dir·ContactLead`, trail foot at `hipX − dir·ToeOffLag` (`GaitTiming`), `dir = sign(vx)`; lead = the flight lead leg (`LegsOrderedByFacing`), so the foot that was reaching lands first.
- `StepScheduler.ContinueRhythm(frontStepsFirst)` (new, 3 lines): sets `lastLift = (!frontStepsFirst, 0)` so `FirstLegToStep` (which already honours `RhythmMemory`) cues the trail foot next, instead of `RestartFromStance` resetting the latch. `WalkState.Enter` calls `ContinueRhythm` for in-motion landings and `RestartFromStance` otherwise (Roll/Climb/stationary landings unchanged).
- `BeginGroundedMotion(velocity)` already seeds `groundSpeed`, `Pace` and `CurrentGait` and restarts the carriage with the downward velocity → squash; the new compression term extends it.
- Crouch continuity: nothing special; `crouchShare` carries through the air and landing.

---

## 10. Lighting — design

### 10.1 Pipeline migration (Phase 7)
1. `Packages/manifest.json`: add `com.unity.render-pipelines.universal` (the 17.x line that Unity 6000.3 resolves — confirm in Package Manager). Stage only that line (MCP entries stay unstaged).
2. Create `Assets/Settings/UniversalRP.asset` + `Renderer2D.asset`; assign in Graphics and **all six** Quality levels. Keep **Gamma** colour space.
3. Camera gets `UniversalAdditionalCameraData` (auto); keep solid-colour clear, no post-processing.
4. Materials: assign `Sprite-Lit-Default` to the 10 character sprites (SpriteSkin deformation works with it) and the ground sprites; leave debug icons/gizmo sprites unlit. Done once via a scripted pass using Unity MCP `execute_code` (C# 6), reading the scene first (scripted saves capture user edits).
5. Add a **Global Light 2D**, a **Player Light 2D** (child of `body`, symmetric so mirroring is harmless) and a disabled **Muzzle Light 2D** (shadow-casting). `ShadowCaster2D` on the 9 ground objects (`selfShadows = false`); **no casters on the character** so the flash lights it and cannot be occluded by it.
6. Verify the UI Toolkit panel still renders (it is an overlay `PanelSettings`, expected unaffected) and `Debug.DrawLine` layers still show.

### 10.2 Components (`Assets/Scripts/Lighting/`, asmdef `ProceduralCharacter.Lighting`, references `Runtime` + `Unity.RenderPipelines.Universal.Runtime`)
- `LightingSettings` ([Serializable] class, owned by `SceneLightingController`): `ambient {color, intensity}`, `player {enabled, color, intensity, innerRadius, outerRadius, falloff}`, `muzzle {color, peakIntensity, innerRadius, outerRadius, duration, decayExponent, radiusDecay, castShadows, shadowIntensity}`.
- `SceneLightingController` (MonoBehaviour): holds the three `Light2D` references and `LightingSettings`; `Apply()` on `OnValidate`/enable and from the tuning facade; exposes read-only `PlayerIntensity`/`MuzzleIntensity` for telemetry.
- `FlashEnvelope` (pure struct): `Trigger()` sets `t = 0`; `Tick(dt)`; `Level = peak·(1 − t/duration)^k`, hard `0` after `duration` (default 0.09 s, k = 3). Retrigger **resets to peak, never accumulates** → rapid fire is safe and deterministic.
- `MuzzleFlashLight` (MonoBehaviour): `OnEnable` subscribes to `driver.Events.Fired` (driver exposes `Events`; execution order −50 guarantees it exists); `OnFired` moves the light to `muzzle`, `Trigger()`s; `LateUpdate` ticks the envelope, sets `light.intensity`, `pointLightOuterRadius = Lerp(outer·(1−radiusDecay), outer, level01)`, toggles `light.enabled` off at 0. Weapon code only raises `Fired`; it knows nothing about lights.
- Player light: plain `Light2D` on a child of `body` (position follows with the pelvis); no script needed beyond settings application.
Defaults — ambient `#9FB4DC` @ 0.65 (slider min 0.4, so nothing can be darker than ~55 %), player `#FFD9A0` @ 0.6, outer 6, inner 0.5, falloff 0.7, muzzle `#FF9A3C` @ 2.5, outer 5, inner 0.4, 0.09 s, shadows on @ 0.6.

---

## 11. F1 / tuning architecture

### 11.1 Assemblies
- `ProceduralCharacter.Runtime` — gameplay. Exposes settings *properties* on owners (already the pattern), plus small read-only snapshot structs: `AirSnapshot` (phase weights, timeAirborne, timeFalling, launch velocity, flail strength, landingPrep, predictedGround, impactSpeed), `CrouchSnapshot` (share, target/current depth, headroomBlocked). `CharacterDriver` no longer builds the inspector: remove `BuildInspector()`/`Inspector`; keep a public `LatestCommands` and public read access to `Rig`/`States` snapshots. `GaitPreview` stays in Runtime as a tiny `IntentOverride` seam (needed by `AccelerateGround`).
- `ProceduralCharacter.Diagnostics` — `LocomotionTuning`, telemetry, schema, presets, panel, debug draw. References Runtime + Lighting. `DebugPanelBootstrap` (scene MonoBehaviour, serialised ref to `CharacterDriver`/`SceneLightingController`) builds the domains. Tests asmdefs reference it.
- `UiInputCapture` stays in Runtime (interface only).
Delete the "re-apply the whole preset on every edit" behaviour: `TuningDomain<TPreset>.Apply(preset)` stays as the full apply used by load/reset; single-field edits write the owner for *that* field via the same `Apply` of the `working` preset (kept for simplicity, but the working copy is re-captured when the panel opens or when presets load so another system's change is never silently overwritten).

### 11.2 Domains, schema, presets
- `TuningDomain<TPreset>`: `Capture()`, `Apply(preset)`, `Defaults`, `Sections`, `Store` (folder + version). Two domains: `Movement` (→ `MovementPreset`) and `Lighting` (→ `LightingPreset`). `TuningParameter` becomes generic over the preset (same lambdas, same min/max/unit). `TuningEditor<T>` and `PresetToolbar` are generic and instantiated per domain; the toolbar shows the active domain's presets (LIGHTING tab → lighting presets, every other tuning tab → movement presets).
- `MovementPreset` = today's `LocomotionPreset` fields (same names so legacy JSON loads) + `crouchGait`, `crouch`, `air`, `jump`, `airPose`, `fallSecondary`, `landing`, `shooting`, `windupSwing`, plus `int version = 2`. `FromJson(json)` = `CodeDefaults()` then `JsonUtility.FromJsonOverwrite(json, defaults)` — **missing keys keep code defaults** (old files and files from before a new field both load safely). `LightingPreset` likewise (`version = 1`, own `LightingPresets` folder). A v1 movement file (no `version`) loads unchanged.
- **Tabs (all schema-driven except LIVE/DEBUG)** — each parameter appears in exactly one tab:
  - IDLE: stance (half width, lead, tolerance, settle step/lift), idle lean, breathing (period, pitch, rise).
  - WALK / RUN: speed, timing, pelvis/body, upper body (existing groups).
  - CROUCH: height (depth, hull drop, stance width), gait (the `crouchGait` profile via the same `Gait()` helper), posture (idle lean, chest fold, idle arm bend), transitions (enter/exit spring).
  - JUMP: windup, launch/height, momentum (carry, forward impulse, windup speed band), air control (airSpeed, airAcceleration, neutralDrag, coyote, buffer), ascent pose, apex pose, push-off.
  - FALL: descent pose, prolonged-fall secondary motion (onset/full time, windmill amp/freq, leg loosen, forward pitch), **landing** group (prep time, impact range, sink, compression, speed loss, torso fold).
  - AIM: arm extension, support hand offset, max aim elevation, recoil (kick, spring, rise, torso kick), pistol cooldown/muzzle offset.
  - LIGHTING: ambient, player, muzzle (domain = Lighting).
  - TRANSITIONS: idle thresholds (start/stop — removed from IDLE), walk↔run blend, lift overlap, balance recovery, acceleration lean / posture blend speed (removed from the old UPPER BODY duplicate).
  - ADVANCED: body springs & reach (ex-BODY), posture springs, chest/head/arm springs, head stabilisation, `bentArm*` (previously unexposed), foothold limits.
  - DEBUG: draw layers (existing enum-driven toggles; add `CrouchHull`, `LandingPrediction`, `AirPose`, `FlailTargets`).
  - LIVE: hand-written telemetry; adds sections below.
- LIVE additions (read-only snapshots, no gameplay pollution): GROUNDED — mode (Idle/Walk/Run/CrouchWalk/Transition), velocity, gait phase, support leg, pelvis target. CROUCH — share, target/current height, headroom blocked. AIRBORNE — vx/vy, launch momentum, time airborne/falling, phase (ascent/apex/descent + weights), pose blend, flail strength. LANDING — landing prep, predicted ground, last impact speed. LIGHTING — player/muzzle intensity.
- Reset: per section (existing) and all (existing) reset to `Defaults` captured from the scene at panel creation; "Code defaults" uses `CodeDefaults()`.
- Hidden panel affects nothing: the panel is a separate component; no gameplay type references it; `UiInputCapture` is the only crossing (interface).

---

## 12. Performance
Hot paths: per-frame in the air — one ray (`TryFindGroundBelowBody`, already existed) + leg ray (`FootTargetAboveGround`, exists) ; crouched only — one `OverlapCapsule` (pre-allocated array, skipped when standing). `FallSecondaryMotion` and `FlashEnvelope` are structs of floats (no `Random`, no allocation). `GaitSet.ProfileAt` is struct-by-value (no allocation). Lighting: 3 `Light2D`; muzzle disabled when idle; only the muzzle casts shadows (9 casters). UI: panel rows are built once; telemetry labels refresh every 0.1 s only while visible; no `Find*` calls, no reflection per frame (debug-layer enum reflection is built once). `FindObjectOfType` is not used; references are serialized.

---

## 13. Phases

Every phase: compile-check via MCP, EditMode tests green, commit (MCP package lines unstaged), user playtests before the next dependent phase. Phases run back-to-back without approval pauses.

### Phase 0 — Groundwork (small, no behaviour change except bug fixes)
- **Goal:** safe seams.
- **Affected:** `CharacterDriver.cs` (`[DefaultExecutionOrder(-50)]`, public `Events`/`LatestCommands`), `AirborneState.cs` (velocity set after `ChangeTo`; `Fall` keeps full ground speed up to `run.speed`), `WalkState.cs` (`Exit` stops writing velocity; Roll/Climb audited to read `Stride.BodyVelocity`), `PlayerCommands.cs` / `PlayerInputReader.cs` (`CrouchRequested` via `HoldOrTapToggle` on Left Ctrl + stick-click, `JumpHeld`), `StrideController.cs` (`HeldCrouch` → `WindupCrouch`), `ArmSwing.cs`, `JumpWindupState.cs`.
- **Tests:** EditMode `AirborneVelocityHandoffTests` is not possible (state is Unity-bound) → PlayMode: walking off a ledge at run speed keeps vx ≈ run speed; existing tests unchanged. `PlayerInputReaderTests` for Ctrl hold/tap (via the existing `HoldOrTapToggleTests` pattern).
- **Done:** all existing EditMode + PlayMode tests pass; fall-off-ledge momentum visible in telemetry.

### Phase 1 — Settings/preset/F1 foundation
- **Goal:** domain-owned settings and the new panel skeleton, before features need it.
- **New:** `Diagnostics/MovementPreset.cs` (rename of `LocomotionPreset`, + `version`, overlay `FromJson`), `LightingPreset.cs` (empty for now), `TuningDomain.cs`, generic `TuningParameter<T>`/`TuningEditor<T>`/`PresetToolbar<T>`, `ProceduralCharacter.Diagnostics.asmdef`, `DebugPanelBootstrap.cs`. **Modified:** all `Diagnostics/*` files, `CharacterDriver.cs` (remove inspector construction), tests asmdef references.
- **Algorithm:** schema regrouped into IDLE/WALK/RUN/TRANSITIONS/ADVANCED (UPPER BODY and BODY dissolved; duplicates removed so each field lives in exactly one tab); LIVE/DEBUG kept; empty placeholder tabs added only when their phase lands.
- **Tests (EditMode):** `MovementPresetTests` — legacy v1 JSON loads and missing keys equal `CodeDefaults`; round-trip; `Copy` independence. `TuningSchemaTests` — tab order, defaults inside ranges, units non-empty, **each parameter writes only itself** (stricter than today, since duplicates are gone), no two parameters bound to the same field. PlayMode `LocomotionInspectorTests` updated; add "panel hidden/absent → identical gait" (run the walk scenario with the bootstrap disabled; metrics equal).
- **Done:** F1 opens, tabs as listed, every previous parameter reachable once, presets save/load, assemblies compile with Runtime free of Diagnostics references.

### Phase 2 — Crouch locomotion
- **Goal:** crouch idle/walk/turn/aim/shoot/jump-from, with a distinct gait.
- **New:** `Locomotion/GaitSet.cs`, `Character/CrouchController.cs`, `Character/CrouchSettings.cs`. **Modified:** `GaitProfile.cs` (+`Crouch` static, no new fields), `LocomotionPace.cs`, `SwingForecast.cs`, `StrideController.cs` (`crouchGait` field, `GaitSet`, baseline term, stance width scale, `CrouchShare` setter), `KinematicBodyMotor.cs` (`CrouchHullDrop`, `HasHeadroom(standingHull)`), `CharacterRig.cs`, `CharacterDriver.cs` (tick order), `WalkState.cs` (crouch-aware `GaitIntent`), `LocomotionPosture.cs`, `TorsoMotion.cs`, `ArmSwing.cs`, `GaitPreview.cs`, `LocomotionTelemetry` + schema (CROUCH tab, LIVE section, Debug layer `CrouchHull`).
- **Algorithm:** §5. Tap/hold from `HoldOrTapToggle`; `CrouchIntent.Resolve`; share spring; `GaitSet.CrouchWeight/ReferenceSpeed/TargetSpeed`.
- **Tests (EditMode):** `GaitSetTests` (weights at speeds 0/1.8/3.5/9, blend continuity, `ReferenceSpeed` makes `Moving`=1 at crouch speed), `CrouchIntentTests` (hold, tap latch, blocked headroom, roll/climb force release), `LocomotionPaceTests` updated, `SwingForecastTests` updated for `GaitSet`. **PlayMode:** idle→crouch (pelvis lowers ≥ depth·0.9, feet do not slide), crouch walk (speed ≈1.8, no foot skid, step length < walk), walk→crouch while moving (speed decays via ramp, no double-step), crouch→walk, run→crouch (no slide, decelerates ≤ ramp), turn while crouched, uneven terrain while crouched (no foothold failure), aim + shoot crouched (aim direction respected, recoil works), low ceiling (stays crouched, stands after clear), crouch→jump.
- **Done:** all of the above green; manual: crouch walking reads as weight carried low (not a lowered walk).

### Phase 3 — Momentum-preserving jump mechanics
- **Goal:** running jump ≫ standing jump; controllable; variable height.
- **New:** `Character/JumpMomentum.cs`, `Character/AirSteering.cs`. **Modified:** `JumpMath.cs` (`CutVelocity`), `JumpSettings.cs`, `AirborneSettings.cs` (+`neutralDrag`, class initializer), `JumpWindupState.cs` (scaled windup, held intent, `JumpMomentum` launch, crouch-jump height), `AirborneState.cs` (steering via `AirSteering`, variable-height cut, coyote via `JumpMomentum`), `PlayerCommands`/reader (`JumpHeld`), JUMP tab, LIVE (launch momentum, time airborne), snapshot.
- **Algorithm:** §6.
- **Tests (EditMode):** `JumpMomentumTests` (standing = 0 x; walk = ground; run = ground+bonus, cap; negative direction; crouch scaling), `AirSteeringTests` (neutral drag, reverse brake, forward no-trim above `airSpeed`, continuity), `JumpMathTests` (cut, hop height reached analytically), windup-scale tests. **PlayMode:** standing/walking/running/crouch jump distances (running ≥ 1.8× standing), jump while accelerating/decelerating/reversing, short hop vs long jump heights, ledge-walk-off keeps speed, coyote.
- **Done:** distances ordered stand < walk < run; hop apex ≈ `minHopHeight`; no jump regressions.

### Phase 4 — Procedural ascent/apex/descent
- **New:** `Character/AirPhase.cs`, `AirPoseSettings.cs` (+`AirPoseFrame`), `AirbornePoseSolver.cs`. **Modified:** `AirborneState.cs` (solver, push-off timing, legs), `CharacterRig.cs` (`AirPose`), `ArmSwing.cs` (`AirbornePose` reads frame), `LocomotionPosture.cs` (air lean), `TorsoMotion.cs` (air chest pitch), JUMP tab groups (ascent/apex/push-off), FALL tab (descent), `AirPose` debug layer, telemetry.
- **Algorithm:** §7.
- **Tests (EditMode):** `AirPhaseTests` (weights sum correctly, apex at vy=0, symmetry, monotone), `AirPoseBlendTests` (running vs standing blend, push-off decay). **PlayMode:** pose weights from a standing vs running jump (running lean greater), ascent trail-leg extension while within `pushOffDuration`, apex weight peak near top, no pose snap (max per-frame foot-target delta bounded).
- **Done:** playtest: push-off visible, apex relaxes, descent reaches.

### Phase 5 — Long-fall secondary motion
- **New:** `UpperBody/FallSecondaryMotion.cs`, `FallSecondarySettings.cs`. **Modified:** `AirborneState.cs` (counters, seed, `S`), `ArmSwing.cs` (always-tick springs, flail inputs, aim suppression), `AirPoseSolver` (legs loosen, torso pitch), FALL tab group, telemetry `flailStrength`.
- **Algorithm:** §8.
- **Tests (EditMode):** `FallSecondaryMotionTests` — `S`=0 for `timeFalling` < onset; monotone ramp; saturates; deterministic for equal seeds, different for different seeds; **smoothness** (max second difference of arm angles over 10 000 simulated 60 Hz frames below a bound — the "no jitter" test); left/right never phase-lock (phase difference drifts); aim suppression → 0. **PlayMode:** short jump (arm deviation ≈ 0), long drop (deviation grows), aiming mid-fall (arms on aim, `Fired` still raised), release aim (no snap).
- **Done:** playtest style = panicked windmill, smooth.

### Phase 6 — Landing integration
- **New:** `Character/LandingSettings.cs`, `LandingForecast.cs`. **Modified:** `AirborneState.cs` (single ray → prep + touchdown, `Landed` impact fields), `StrideController.cs` (`TrySettleLanding`, `LandingCompression` transient + spring), `StepScheduler.cs` (`ContinueRhythm`), `WalkState.cs` (`Enter` chooses continue/restart), `TorsoMotion.cs`, FALL tab Landing group, telemetry (prep, predicted ground, impact), `LandingPrediction` debug layer; `AirborneSettings.touchdownSink`/`landingSpeedCarry`/`maxCarriedLandingSpeed` move to `LandingSettings` (§14).
- **Algorithm:** §9.
- **Tests (EditMode):** `LandingForecastTests` (time-to-impact vs brute-force integration), `LandingImpactTests` (impactShare, sink, speed loss), `StepSchedulerTests` (`ContinueRhythm` picks the requested first leg). **PlayMode:** light vs hard landing (compression & dip depth monotone in impact), run-jump → run-landing (speed after ≥ 0.8× before, first plant of the trail foot within ~0.35 s, no gait-phase reset: `GaitRecorder` shows no stationary stance), standing landing unchanged, landing on slope/step, landing into crouch.
- **Done:** run jumps flow back into the run; hard long-fall landings visibly absorb.

### Phase 7 — Lighting infrastructure (URP 2D)
Pure setup (§10.1). **Affected:** `Packages/manifest.json`/`packages-lock.json` (URP lines only staged), `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset`, new `Assets/Settings/*.asset`, `SampleScene.unity` (materials, Global Light, Player Light, Muzzle Light, ShadowCasters), `Lighting/ProceduralCharacter.Lighting.asmdef`.
- **Tests:** PlayMode smoke: scene loads under URP, camera renders (no null pipeline), the panel UIDocument present, all existing PlayMode tests pass unchanged (physics/gameplay are render-independent). Manual: scene readable, character sprites lit, no black frames, panel visible.
- **Done:** all previous tests green under URP; ambient-only scene is readable.

### Phase 8 — Player / ambient / muzzle lighting + LIGHTING tab
- **New:** `LightingSettings.cs`, `SceneLightingController.cs`, `FlashEnvelope.cs`, `MuzzleFlashLight.cs`, `Diagnostics/LightingPreset.cs` fill-in, `LightingTuningSchema.cs`.
- **Algorithm:** §10.2.
- **Tests (EditMode):** `FlashEnvelopeTests` (instant peak, monotone decay, zero at duration, retrigger resets to peak, rapid fire never exceeds peak, `dt` independence ±), `LightingPresetTests` (overlay, round-trip), `LightingSchemaTests`. **PlayMode:** `Fired` ⇒ muzzle intensity > 0 within one frame; every successful shot (cooldown-respecting) triggers once; position equals the event's muzzle; ≈ 0 after 0.2 s; rapid fire holds consistent state; no flash when cooldown blocks; light works while moving/jumping/crouching; scene mean luminance ≥ threshold (readability), player light does not exceed radius.
- **Done:** playtest mood = dusk, readable; flash lights character/ground and casts shadows from ground casters.

### Phase 9 — Polish, docs, regression
Debug-draw layers finished, README sections (architecture, crouch, jump, airborne, landing, lighting, F1/presets; update §9 preset docs), the full test matrix of §15 run, performance spot-check (no per-frame allocations: Unity Profiler GC alloc 0 B in air/crouch frames), migration notes verified.

---

## 14. Migration of serialized settings
- **Rule:** never add a field to an existing `[Serializable] struct` (it deserialises as 0 from the saved scene). New settings live in new `[SerializeField]` fields on the owner component (field initializer applies) or in `[Serializable] class`es (class initializers apply to missing fields). `GaitProfile.Crouch` is a new *field on* `StrideController`, not a new field *in* `GaitProfile`.
- `AirborneSettings`/`JumpSettings` are classes → new members (`neutralDrag`, `carry`, `forwardImpulse`, `minHopHeight`, `crouchJumpScale`, …) get their initializers in existing scenes.
- Moved/removed: `AirborneSettings.touchdownSink`, `landingSpeedCarry`, `maxCarriedLandingSpeed` → `LandingSettings` (same defaults 0.3/0.6/9; the saved scene holds the defaults, so nothing is lost; the removed serialized values are dropped on the next save). `StrideController.HeldCrouch` → `WindupCrouch` (runtime property, not serialized). `ArmSwing.windupSwing` stays serialized; `airborneRaise/airborneElbowBend` are superseded by `AirPoseSettings` (defaults reproduce the old descent values) and removed.
- Scene: new serialized components (`CrouchSettings` holder, `AirPose`, `FallSecondary`, `Landing`, `SceneLightingController`, lights) are added in a single deliberate scripted pass that first **re-reads the saved scene** (so user edits are not clobbered) and then saves; stale serialized values are reset by assigning defaults explicitly.
- Presets: v1 files (flat, no `version`) load via overlay onto code defaults; `version` is written from v2 on. Old files in `LocomotionPresets/` keep working; lighting presets use a new folder.

---

## 15. Verification (end-to-end)

Automated (EditMode, via MCP `run_tests` + `get_test_job`): `GaitSetTests`, `CrouchIntentTests`, `JumpMomentumTests`, `AirSteeringTests`, `JumpMathTests`, `AirPhaseTests`, `AirPoseBlendTests`, `FallSecondaryMotionTests`, `LandingForecastTests`, `LandingImpactTests`, `StepSchedulerTests` (extended), `FlashEnvelopeTests`, `MovementPresetTests`, `LightingPresetTests`, `TuningSchemaTests` (+ lighting schema). Existing suites updated where signatures change (`LocomotionPaceTests`, `SwingForecastTests`, `PostureLeanTests` tuples, `LocomotionTuningSchemaTests.OffersTheTabsInOrder`).

PlayMode (`ScenarioStage`, virtual keyboard/mouse, 60 fps): crouch matrix (idle, walk, walk→crouch, crouch→walk, run→crouch, turn, uneven terrain, aim, shoot, low ceiling, crouch→jump), jump matrix (standing/walking/running/crouch/accelerating/decelerating/reversing/hop/long/apex/fall/long fall/landing/run→run landing), fall motion (short fall quiet, long fall grows, smooth, aim suppression), lighting (readability, local radius, flash per shot, position, decay, rapid fire, with motion/jump/crouch), F1 (tabs, live apply, hidden-panel equivalence, reset, presets, no duplicate owners), plus the existing regression scenarios (walk/run/aim/climb/uneven/stand-still).

Manual (user in Unity): crouch-walk reads as weight low (not lowered walk); push-off visible; apex relaxation; panicked windmill smooth, no jitter; landing absorption proportional; running landing continues; dusk mood readable; flash warm & fast; F1 tab usability.

---

## 16. Risks / regression areas
1. **URP migration** (Phase 7): materials, sorting, shadow-caster artefacts (light inside a caster; `selfShadows=false`; no character casters), Gamma colour. Isolated phase, revert = unassign URP asset.
2. **`GaitSet` refactor** touches tuple signatures in gait/forecast code → most-tested area; update tests first, keep walk/run numeric output byte-identical when `crouchShare = 0` (golden test: identical `CurrentGait` and gait metrics to the pre-change run).
3. **`IsDrivingGait` one-frame lag**: posture/arm systems switch to air targets one frame late; the air-pose weights are springed so it is invisible; verify at takeoff/landing.
4. **Aim/ArmSwing stale springs**: fixed by always-ticking `ArmSwing` (§8.4).
5. **Landing foot placement** at run speeds with `FootholdProbe` failing on one column (ledges): `TrySettleLanding` falls back to the stationary stance (current behaviour) when either column lacks ground.
6. **Hull shrink** under low ceilings can overlap geometry if the headroom probe disagrees with the motor's `solidMask`; both use `solidMask`.
7. **Scene serialization**: scripted scene saves overwrite user edits — read-before-write, one pass.
8. **asmdef split** can break test asmdef references and `UiInputCapture` registration; do it in Phase 1 with tests as the safety net.
9. **Preset overlay** relies on `JsonUtility.FromJsonOverwrite` semantics for nested structs; covered by `MovementPresetTests` before anything depends on it.

---

## 17. Files

**Modify (representative):** `Character/CharacterDriver.cs`, `CharacterRig.cs`, `JumpMath.cs`, `KinematicBodyMotor.cs`, `States/{WalkState,JumpWindupState,AirborneState,AirborneSettings,JumpSettings}.cs`, `Input/{PlayerCommands,PlayerInputReader}.cs`, `Locomotion/{StrideController,GaitProfile,LocomotionPace,SwingForecast,StepScheduler,GaitPreview}.cs`, `UpperBody/{ArmSwing,LocomotionPosture,TorsoMotion,ShootingSettings}.cs`, `Diagnostics/*` (rename/generics), `ProjectSettings/{Graphics,Quality}Settings.asset`, `Packages/manifest.json`, `Assets/Scenes/SampleScene.unity`, `README.md`, existing tests listed in §15.

**Add:** `Locomotion/GaitSet.cs`; `Character/{CrouchController,CrouchSettings,JumpMomentum,AirSteering,AirPhase,AirPoseSettings,AirbornePoseSolver,LandingSettings,LandingForecast}.cs`; `UpperBody/{FallSecondaryMotion,FallSecondarySettings}.cs`; `Lighting/{LightingSettings,SceneLightingController,FlashEnvelope,MuzzleFlashLight,ProceduralCharacter.Lighting.asmdef}`; `Diagnostics/{MovementPreset,LightingPreset,TuningDomain,LightingTuningSchema,DebugPanelBootstrap,ProceduralCharacter.Diagnostics.asmdef}` (+ generic row/editor/toolbar); `Assets/Settings/{UniversalRP,Renderer2D}.asset`; EditMode/PlayMode test files per §15.

## 18. Recommended order
0 groundwork → 1 tuning foundation → 2 crouch → 3 jump momentum → 4 air pose → 5 long-fall motion → 6 landing → 7 URP → 8 lights → 9 polish. Locomotion (2–6) is independent of lighting (7–8), so lighting can slip without blocking character work.
