## Procedural animation showcase
_Unity 2D humanoid procedural animation (Unity 6000.3, 2D Animation IK, Input System)_

## 1. Overview
  This is the general explanation of the idea of simple 2D procedural leg animation for bipedal character. Project uses inverse kinematics (IK) to calculate position of the leg, which adapts to the terrain.

  ![Alt Text](https://media.giphy.com/media/Y3S9FdlkHIynsJgPaj/giphy.gif)

_Results_

Open `Assets/Scenes/SampleScene.unity`, press Play and walk with **A** / **D**.

## Project layout
```
Assets/
  Art/Characters/      inverseRobo.psb (rigged PSB character)
  Art/Sprites/         debug icons and the ground tile
  Scenes/              SampleScene
  Scripts/Locomotion/  BodyControl, MoveTarget, RaycastTarget, FootholdProbe, FrameRateEase
```

| Script | Responsibility |
|---|---|
| [BodyControl](Assets/Scripts/Locomotion/BodyControl.cs) | Moves the stride target from input, decides which leg steps next, keeps the body centred over the feet. |
| [MoveTarget](Assets/Scripts/Locomotion/MoveTarget.cs) | Moves one leg's IK solver toward its foot target along a lift-then-settle arc and reports landing. |
| [RaycastTarget](Assets/Scripts/Locomotion/RaycastTarget.cs) | Keeps a desired target on reachable ground, holding the last valid foothold when none is found. |
| [FootholdProbe](Assets/Scripts/Locomotion/FootholdProbe.cs) | Finds reachable ground in a vertical window; searches sideways for the nearest ledge over gaps. |
| [FrameRateEase](Assets/Scripts/Locomotion/FrameRateEase.cs) | Makes the ease-out motion independent of frame rate. |

Update order is fixed: `BodyControl` → `RaycastTarget` → `MoveTarget`.

## 2. Idea
  Legs of the rigged character are chain of the two bones, end of which (feet) are always trying to reach for the raycasted target after it reaches certain range. To prevent unnatural, direct movement from point to point of both legs, there is added logic, which controls and syncs leg and body movement depending on the environment and current placement of the feet.

## 3. Character rig
  Bone rig for legs is simplified to make later calculations easier and each bone affects one sprite assigned to it. Top of the body could have no bones, but those will be important in further developement for arms and head animations.

  ![Alt Text](ikCharacterRig.PNG)

## 4. Raycasting desired position
  A raycaster on the body and on each thigh keeps its desired target (circle icon) on the ground. Only ground inside a reach window counts: from `maxRise` above the probing bone down to `maxDrop` below it. The floor deep beneath a gap and platforms overhead are therefore ignored. When nothing reachable is directly below, the probe searches sideways (forward first, up to `searchRadius`) for the nearest ledge, so feet step onto the far side of a gap instead of dropping into it. If no ledge exists, the last valid foothold is kept.

| `FootholdLimits` field | Default | Meaning |
|---|---|---|
| `maxRise` | 1.0 | Highest accepted surface above the probing bone |
| `maxDrop` | 3.2 | Lowest accepted surface below the probing bone |
| `searchRadius` | 0.8 | Sideways search extent over gaps |
| `searchStep` | 0.05 | Sideways search resolution |
| `groundMask` | Default raycast layers | Layers treated as ground |

The stride target's raycaster (on `bone_abs`) has `snapToFoundLedge` disabled. It takes only the ledge height, so input can keep pushing it across a gap.

![Alt Text](https://media.giphy.com/media/Q7ds6FdBQB03IzmXaO/giphy.gif)

## 5. Inverse kinematics
  It makes possible for us to calculate possible rotations of the bones, that will allow feet to reach desired point.

  ![Alt Text](ikFeet.gif)

  Nearly all complicated calculations are diminished thanks to the simplified bone structure and reduction of the problem to the two dimensions. It allows us to use basic trigonometry instead of the iterating over matrix operations.

  ![Alt Text](ikTrig.png)

  _We know lengths and positions of all the bones and all line segments, so it allows us to easily calculate what angle should be between red and blue bone, to reach designated point_

## 6. Controlling and syncing the body
  The body follows a stride target that A/D input moves around, clamped near the body. Each frame the body eases horizontally toward the mean of both desired footholds and the stride target.

  When both legs are grounded, the leg whose foot is farther from the stride target is chosen. If the stride target is also out of `reachRange` from the other (planted) foothold, that leg starts a step: its desired foothold jumps to the stride target and the leg is marked airborne. A leg counts as landed again once its solver is within 5% of `reachRange` of its foot target.

  ![Alt Text](https://media.giphy.com/media/UWcRcLwcw8LyuvL7qL/giphy.gif)

  The leg solver moves toward its target with `Vector2.MoveTowards()` combined with a frame-rate-independent ease-out.

## 7. Body and leg height adjustments
  During the first half of a step the foot is lifted by `1.5 × reachRange`; in the second half it settles onto the target. Half of the current lift is passed to the body, which eases toward the average foot height plus `bodyHeight`. This raises the body slightly during each step and when one leg stands higher than the other, for example on stairs.

  ![Alt Text](https://media.giphy.com/media/H62vdwLXLRo3sMCKaL/giphy.gif)

## 8. Todo
  - [ ] Add head animation
  - [ ] Add mouse controls
  - [ ] Allow character to fall when losing balance
  - [ ] Upgrade animation tween and curves
  - [ ] Add upper body animations
  - [ ] Add grabbing and physics interactions
  - [ ] Character and ground prefabs
  - [ ] EditMode tests for `FootholdProbe`
