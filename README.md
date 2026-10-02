## Procedural animation showcase
_Unity 2D humanoid procedural animation (Unity 6000.3, 2D Animation IK, Input System)_

## 1. Overview
  This project explains a simple approach to procedural 2D leg animation for a bipedal character. It uses inverse kinematics (IK) to place each leg, so the legs adapt to the terrain. Walking, jumping, rolling, ledge climbing and aiming are all procedural: there are no animation clips.

  ![Alt Text](https://media.giphy.com/media/Y3S9FdlkHIynsJgPaj/giphy.gif)

_Results_

Open `Assets/Scenes/SampleScene.unity` and press Play.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Walk | A / D (or ← / →) | Left stick |
| Run (while walking, not while aiming) | Hold Left Shift | Press left stick |
| Jump (climbs instead when a too-high ledge is ahead) | Space | South button |
| Roll toward the facing direction (disabled by default) | C | East button |
| Shooting mode: face and aim at the pointer | Hold right mouse button | Left trigger |
| Fire (in shooting mode; logs a debug message) | Left mouse button | Right trigger |

## Project layout
```
Assets/
  Art/Characters/      inverseRobo.psb (rigged PSB character)
  Art/Sprites/         debug icons and the ground tile
  Scenes/              SampleScene
  Scripts/             ProceduralCharacter.Runtime assembly
    Character/         CharacterDriver (composition root), facing, kinematic motor, rig
    Character/States/  Walk, JumpWindup, Airborne, Roll, Climb and their settings
    Climbing/          ledge detection and grab rules
    Input/             PlayerInputReader (actions defined in code), PlayerCommands
    Locomotion/        stride gait, leg steppers, foothold planning
    UpperBody/         two-bone arm IK, head look, shooting stance, pistol
  Tests/EditMode/      tests for the pure policies
```

### How a frame runs
`CharacterDriver` (on the `inverseRobo` root) is the only component with an `Update`:
1. It reads one `PlayerCommands` snapshot from `PlayerInputReader`.
2. It ticks the active **full-body state**: Walk, JumpWindup, Airborne, Roll or Climb.
3. If that state allows it, it ticks the **shooting stance** on top: aiming, head look and firing.
4. In `LateUpdate`, it applies this frame's arm and head pose requests. Any limb nobody posed eases back to its authored rest pose.

The full-body states switch through `CharacterStateMachine`. Each state receives a `StateContext` holding the machine, the `CharacterRig` (stride, facing, motor, arms, head, chest) and its sibling states.

| Piece | Responsibility |
|---|---|
| [StrideController](Assets/Scripts/Locomotion/StrideController.cs) | Walking gait: moves the stride target, plans the next foothold, steps one leg at a time and keeps the body over the feet. |
| [LegStepper](Assets/Scripts/Locomotion/LegStepper.cs) | Moves one leg's IK target: a lift-then-settle step arc, settling after a landing, or a free swing while airborne. |
| [FootholdPlanner](Assets/Scripts/Locomotion/FootholdPlanner.cs) / [FootholdProbe](Assets/Scripts/Locomotion/FootholdProbe.cs) | Pure scoring of foothold candidates / the physics queries that collect them. |
| [KinematicBodyMotor](Assets/Scripts/Character/KinematicBodyMotor.cs) | Moves the body by velocity while a hips-to-head capsule slides along walls and ceilings. |
| [CharacterFacing](Assets/Scripts/Character/CharacterFacing.cs) | Turns the character by mirroring the rig root in place. World-space targets keep their positions. |
| [ArmLimb](Assets/Scripts/UpperBody/ArmLimb.cs) / [TwoBoneIk](Assets/Scripts/UpperBody/TwoBoneIk.cs) | Per-frame arm reach requests solved by analytic two-bone IK. |
| [LedgeDetector](Assets/Scripts/Climbing/LedgeDetector.cs) / [LedgeGrabPolicy](Assets/Scripts/Climbing/LedgeGrabPolicy.cs) | Finding a climbable ledge / the pure grab windows and climb keyframes. |

## 2. Idea
  Each leg of the rigged character is a chain of two bones. Its end (the foot) reaches for a planned foothold once the step target moves out of range. To avoid unnatural, direct point-to-point movement of both legs, extra logic controls and syncs leg and body movement based on the environment and where the feet currently stand.

## 3. Character rig
  The leg rig is simplified to keep the calculations easy, and each bone drives one sprite. The legs use Unity's `LimbSolver2D`. The arms are posed by this project's own two-bone IK, and the head and chest bones are rotated directly.

  ![Alt Text](ikCharacterRig.PNG)

## 4. Planning footholds
  A foothold is chosen once, when a step starts, and the leg keeps it for the whole step.
  - **Collecting candidates:** `FootholdProbe` samples ground tops from the stride target back to the swinging foot, and a little beyond.
  - **Rejecting columns:** a column is dropped if its ray starts inside geometry, or if the swing path to it would pass through terrain (for example, the floor on the far side of a stair block).
  - **Validating:** `FootholdPlanner` accepts a candidate only if it is inside the step window around the planted foot, and only if it is within leg reach of where the hip will settle.
  - **Scoring:** the cheapest candidate wins, where cost = distance to the desired spot + height change. So:
    - narrow gaps are stepped across;
    - the floor deep under a gap is ignored;
    - a far, high ledge never beats a near foothold.

| `FootholdLimits` field | Default | Meaning |
|---|---|---|
| `maxStepUp` / `maxStepDown` | 1.0 / 1.3 | Step window around the planted foot |
| `maxReachStretch` | 1.2 | Allowed hip-to-foothold distance, as a multiple of leg length |
| `searchRadius` / `searchStep` | 0.8 / 0.05 | How far past the desired spot to sample, and how densely |
| `heightWeight` | 0.5 | Cost of height change relative to sideways shift |
| `minStepLength` | 0.15 | Shorter steps are skipped instead of shuffling |

  When no foothold exists, a fall starts if the body leans past its feet by more than `edgeOverhang`. A fall also starts if neither foot stands on ground.

![Alt Text](https://media.giphy.com/media/Q7ds6FdBQB03IzmXaO/giphy.gif)

## 5. Inverse kinematics
  IK computes the bone rotations that let a foot reach its target point.

  ![Alt Text](ikFeet.gif)

  The simplified bone structure and the reduction to two dimensions remove nearly all the complex calculation. Basic trigonometry is enough, with no iterative matrix operations. The arms use the same idea in [TwoBoneIk](Assets/Scripts/UpperBody/TwoBoneIk.cs).

  ![Alt Text](ikTrig.png)

  _We know the lengths and positions of all the bones and line segments, so we can calculate the angle between the red and blue bones that reaches the target point._

## 6. Walking and running
  The body leads and the feet follow, timed by one gait cycle ([GaitCycle](Assets/Scripts/Locomotion/GaitCycle.cs)):
  - **One clock for both legs:** a phase in [0, 1) advances by distance travelled over a stride. The front leg lifts at phase 0 and the back leg at 0.5, so steps always alternate evenly. Without this shared clock, independent legs drift into a limp of alternating long and short steps.
  - **Speed sets step length and cadence:** both grow with the square root of speed ([GaitTiming](Assets/Scripts/Locomotion/GaitTiming.cs)), as people keep step length over cadence roughly constant (the "walk ratio"). That gives short, slow steps when starting and stopping, about two steps a second walking and about three running.
  - **Stance and swing:** each foot spends a duty-factor share of the cycle on the ground: about 0.6 walking, so a foot is always down, and a third running, so both feet leave the ground between steps. A foot lands where the hip will be at touchdown, plus half its stance travel ahead, so the stance is centred under the body.
  - **Setting off and catching up:** the first step is planned for most of the intended speed, so it isn't a shuffle. If the body outruns a planted foot while speeding up, that foot takes a balance step and the cycle re-syncs to it.
  - **Standing:** the robot stands tall on nearly straight legs (`bodyHeight`). Walking sinks the pelvis a little and running more (`pelvisDrop`). When it stops, the feet take settling steps into a stance centred under the torso, slightly ahead of the pelvis (`stanceLead`).
  - **Edges:** a foot that finds no foothold ahead leaves the body leaning past its feet, and it falls. Stopping near a ledge keeps it on the ledge.

  Walk and run are each a [GaitProfile](Assets/Scripts/Locomotion/GaitProfile.cs) on `StrideController`, with their own acceleration and braking: walking eases in, running starts and stops sharply. Holding run blends from the walk profile to the run profile over about 0.4 s (`runBlendRate`), and every motion scales with the current speed, so idle, walking and running flow into each other.

  ![Alt Text](https://media.giphy.com/media/UWcRcLwcw8LyuvL7qL/giphy.gif)

## 7. Moves
  - **Jump:**
    - A short crouch (`JumpWindupState`), then a launch tuned by apex height.
    - In the air, the character can steer, and the legs tuck while rising and reach while falling.
    - Coyote time and a jump buffer make timing forgiving.
    - Landing is swept over the whole frame's fall, so a long frame cannot tunnel through the ground.
  - **Roll** (a dive roll in four procedural phases, each easing from where the previous one left off):
    1. **Dive:** forward momentum, the torso pitches forward and drops, both arms stretch ahead and the trailing leg pushes back.
    2. **Hand contact:** the hands reach for ground probed ahead of the shoulders. The phase ends when the hand actually reaches it (with a timeout).
    3. **Roll:** the hands stay planted briefly, then tuck. The body turns the rest of the way to 360° while its lowest point (head, shoulders, back or hips) stays on the ground, so it rolls like a ball over uneven steps. The chest curls and the chin tucks only while the body is inverted, which swings the head up and away from the ground.
    4. **Recovery:** the feet reach for ground within a step of where the robot rolled, sliding toward the body near edges. The body rises into a slight crouch that the walking gait stands out of.

    The roll stops at walls and turns into a fall if it leaves a ledge.
  - **Climb:**
    - **Trigger:** pressing jump next to a ledge too high to step onto (up to `maxGrabRise`) starts a climb. So does reaching a ledge corner in the air.
    - **Sequence:** the character reaches the corner, hangs, pulls up while the feet scramble on the wall face, mounts a foot on top and stands.
    - **Detection:** a ledge needs standing room above it and at least `minTopDepth` of top.
  - **Shooting mode:**
    - Holding aim turns the character toward the pointer (it can backpedal), extends the front arm toward it and tilts the head to follow.
    - Fire calls `Pistol.TryFire`. For now that logs a debug message and draws a debug ray from the muzzle.
    - Shooting is suppressed while rolling and climbing.

  All tunables are serialized on `CharacterDriver`, grouped by move. Ease values are fractions per 60 Hz frame and are converted to the actual frame time by `FrameRateEase`.

  ![Alt Text](https://media.giphy.com/media/H62vdwLXLRo3sMCKaL/giphy.gif)

## 8. Weight and feel
  The body and upper body move in step with the legs, all from the same gait phase ([GaitWaves](Assets/Scripts/Locomotion/GaitWaves.cs)):
  - **Bob:** twice per stride. When walking, the body is lowest just after each foot lands (the knee takes the weight) and highest passing over the supporting leg. When running, the bounce comes from the foot plants and push-offs: lowest mid-stance, highest in flight. The leg-reach limit stops the hips rising beyond what the planted legs reach.
  - **Surge:** the body runs slightly ahead coming through each low point and drops back over the top.
  - **Torso** ([TorsoMotion](Assets/Scripts/UpperBody/TorsoMotion.cs)):
    - the chest flexes forward as weight is taken and extends upright over the supporting leg;
    - it leans with speed (more when running) and into starts, and rocks back on stops;
    - when standing still it breathes ([Breathing](Assets/Scripts/UpperBody/Breathing.cs)): each in-breath raises the chest along the spine and extends it, lifting the shoulders, arms and head, and the out-breath is slower;
    - it kicks back with recoil.
  - **Arms** ([ArmSwing](Assets/Scripts/UpperBody/ArmSwing.cs)):
    - they swing opposite to the legs, the hand dragging a little behind the shoulder;
    - the elbows flex on the forward swing; when running they stay bent and pump;
    - winding up a jump they swing back, and in the air they rise for balance.
  - **Mass:** the body follows its target through [SecondOrderDynamics](Assets/Scripts/Locomotion/SecondOrderDynamics.cs) springs ([BodyCarriage](Assets/Scripts/Locomotion/BodyCarriage.cs)), with three perceptual parameters (t3ssel8r's formulation): `frequency`, `damping` and `response`. Foot plants push it down, and a landing's speed becomes the squash and rebound.
  - **Steps:** the foot follows [StepArc](Assets/Scripts/Locomotion/StepArc.cs) curves, one set for walking and one for running:
    - walking, the toe clears early in the swing, the foot passes under the hip near the top, then lowers gently and comes to rest as it touches down;
    - running, the foot stays low until it's under the hip, so the shin never folds back flat behind the body, then rises with the knee drive and comes down hard.
  - **Air:** falls are heavier than rises, with a brief hang at the apex.
  - **Camera and events:** [FollowCamera](Assets/Scripts/Character/FollowCamera.cs) follows with spring lag and look-ahead and kicks on landings. [CharacterEvents](Assets/Scripts/Character/CharacterEvents.cs) (`FootPlanted`, `Landed`, `Jumped`, `Fired`) are the place to attach sound and VFX.

## 9. Tests
  Open the Test Runner (Window ▸ General ▸ Test Runner) and run the **EditMode** tests. They cover the pure policies: foothold planning, two-bone IK, ledge grab rules, jump math, frame-rate easing, the gait cycle and timing, step arcs and breathing.

## 10. Todo
  - [ ] Character and ground prefabs
  - [ ] Footstep and landing sounds and dust on `CharacterEvents`
  - [ ] Projectiles and hit reactions for the pistol
  - [ ] Foot alignment to slopes
  - [ ] Step curves as `AnimationCurve`s
  - [ ] Allow the character to fall over when losing balance
  - [ ] Remove the legacy `RaycastTarget` components and their `rayTargetLeg*` marker objects from the scene
