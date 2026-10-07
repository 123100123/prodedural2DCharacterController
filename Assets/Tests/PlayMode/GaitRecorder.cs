using System.Collections.Generic;
using UnityEngine;

// Samples the character once per frame through its public surface and the rig's bones, so the same measurements
// describe any gait implementation: foot lifts and plants, phase continuity, posture and head steadiness.
public sealed class GaitRecorder {
    public readonly struct Sample {
        public readonly float Time;
        public readonly float GroundSpeed;
        public readonly float Phase;
        public readonly float ExpectedPhaseAdvance;
        public readonly bool IsDrivingGait;
        public readonly bool FrontGrounded;
        public readonly bool BackGrounded;
        public readonly float BodyAboveStance;
        public readonly float BodyLineLean;
        public readonly float HeadAngle;
        public readonly float ChestAngle;
        public readonly float PlantedFootGap;
        public readonly int BalanceSteps;
        public readonly int PhaseResyncs;
        public readonly int FootholdFailures;
        public readonly float CatchUp;
        // Pelvis ahead of its planted support foot along the motion, at the stance's progress (NaN without support).
        public readonly float PelvisOverSupport;
        public readonly float SupportProgress;
        // Hip-to-foot distance of the planted support leg as a share of the leg's length.
        public readonly float SupportExtension;
        // Where each foot's target is (its foothold while planted).
        public readonly Vector2 FrontFoot;
        public readonly Vector2 BackFoot;
        // Largest hip-to-foot distance (share of the leg) of a planted foot behind its hip along the motion (NaN: none).
        public readonly float TrailingExtension;
        // Largest horizontal distance a planted foot is behind its hip along the motion (NaN: none).
        public readonly float TrailingLag;

        public Sample(GaitRecorder recorder, float time) {
            StrideController stride = recorder.stride;
            Time = time;
            GroundSpeed = stride.GroundSpeed;
            Phase = stride.GaitPhase;
            float strideLength = GaitTiming.StrideLength(stride.GroundSpeed, stride.CurrentGait);
            ExpectedPhaseAdvance = Mathf.Abs(stride.GroundSpeed) * ScenarioStage.FrameTime / strideLength;
            IsDrivingGait = stride.IsDrivingGait;
            FrontGrounded = stride.FrontLeg.IsGrounded;
            BackGrounded = stride.BackLeg.IsGrounded;
            BodyAboveStance = stride.transform.position.y - stride.StanceHeight;
            BodyLineLean = recorder.BodyLineLean();
            HeadAngle = recorder.BoneAngle(recorder.head);
            ChestAngle = recorder.BoneAngle(recorder.chest);
            PlantedFootGap = Mathf.Max(recorder.PlantedGap((stride.FrontLeg, recorder.frontEffector)), recorder.PlantedGap((stride.BackLeg, recorder.backEffector)));
            BalanceSteps = stride.Diagnostics.BalanceSteps;
            PhaseResyncs = stride.Diagnostics.PhaseResyncs;
            FootholdFailures = stride.Diagnostics.FootholdFailures;
            CatchUp = stride.Diagnostics.CatchUp;
            SupportStance support = stride.Support;
            bool isSupported = support.HasSupport && support.IsPlanted && stride.IsMoving;
            SupportProgress = isSupported ? support.Progress : float.NaN;
            PelvisOverSupport = isSupported ? (stride.transform.position.x - support.FootX) * support.Direction : float.NaN;
            LegStepper supportLeg = support.SupportLeg == SupportStance.Leg.Back ? stride.BackLeg : stride.FrontLeg;
            SupportExtension = isSupported ? Vector2.Distance(supportLeg.HipPosition, supportLeg.FootTarget) / supportLeg.LegLength : float.NaN;
            FrontFoot = stride.FrontLeg.FootTarget;
            BackFoot = stride.BackLeg.FootTarget;
            float direction = Mathf.Sign(stride.GroundSpeed);
            (float extension, float lag) front = Trailing(stride.FrontLeg, direction, stride.IsMoving);
            (float extension, float lag) back = Trailing(stride.BackLeg, direction, stride.IsMoving);
            TrailingExtension = float.IsNaN(front.extension) ? back.extension : float.IsNaN(back.extension) ? front.extension : Mathf.Max(front.extension, back.extension);
            TrailingLag = float.IsNaN(front.lag) ? back.lag : float.IsNaN(back.lag) ? front.lag : Mathf.Max(front.lag, back.lag);
        }

        private static (float extension, float lag) Trailing(LegStepper leg, float direction, bool moving) {
            float lag = (leg.HipPosition.x - leg.FootTarget.x) * direction;
            if (!moving || !leg.IsGrounded || lag <= 0f) {
                return (float.NaN, float.NaN);
            }
            return (Vector2.Distance(leg.HipPosition, leg.FootTarget) / leg.LegLength, lag);
        }
    }

    public readonly struct FootEvent {
        public readonly float Time;
        public readonly bool Front;
        public readonly bool IsLift;
        // The foot's target at the event: its foothold when it plants.
        public readonly Vector2 Foot;

        public FootEvent((float time, bool front, bool isLift) footEvent, Vector2 foot = default) {
            Time = footEvent.time;
            Front = footEvent.front;
            IsLift = footEvent.isLift;
            Foot = foot;
        }
    }

    private readonly StrideController stride;
    private readonly Transform characterRoot;
    private readonly Transform chest;
    private readonly Transform head;
    private readonly Transform frontEffector;
    private readonly Transform backEffector;
    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<FootEvent> footEvents = new List<FootEvent>();
    private float time;

    public GaitRecorder((CharacterDriver character, StrideController stride) rig) {
        stride = rig.stride;
        characterRoot = rig.character.transform;
        chest = FindBone("bone_chest");
        head = FindBone("bone_head");
        frontEffector = FindBone("effectorLegF");
        backEffector = FindBone("effectorLegB");
    }

    public float Now => time;
    public IReadOnlyList<Sample> Samples => samples;
    public IReadOnlyList<FootEvent> FootEvents => footEvents;

    // Called after the IK has solved this frame, so feet and bones are where they are drawn.
    public void Record() {
        time += ScenarioStage.FrameTime;
        Sample sample = new Sample(this, time);
        if (samples.Count > 0) {
            RecordFootChanges((samples[samples.Count - 1], sample));
        }
        samples.Add(sample);
    }

    private void RecordFootChanges((Sample previous, Sample current) frames) {
        if (frames.previous.FrontGrounded != frames.current.FrontGrounded) {
            footEvents.Add(new FootEvent((time, true, !frames.current.FrontGrounded), frames.current.FrontFoot));
        }
        if (frames.previous.BackGrounded != frames.current.BackGrounded) {
            footEvents.Add(new FootEvent((time, false, !frames.current.BackGrounded), frames.current.BackFoot));
        }
    }

    // Forward lean of the line from the body pivot to the head, in degrees toward the facing direction.
    private float BodyLineLean() {
        Vector2 line = head.position - stride.transform.position;
        return Mathf.Atan2(line.x * FacingSign(), line.y) * Mathf.Rad2Deg;
    }

    private int FacingSign() => characterRoot.localScale.x < 0f ? -1 : 1;

    // A bone's visible world direction (mirroring included) in degrees, measured toward the facing side so turning
    // around does not read as rotation.
    private float BoneAngle(Transform bone) {
        Vector3 direction = bone.TransformVector(Vector3.right);
        return Mathf.Atan2(direction.y, direction.x * FacingSign()) * Mathf.Rad2Deg;
    }

    // How far the IK foot is from its planted target: a planted foot that drifts or cannot reach shows up here.
    private float PlantedGap((LegStepper leg, Transform effector) foot) {
        return foot.leg.IsGrounded ? Vector2.Distance(foot.effector.position, foot.leg.FootTarget) : 0f;
    }

    private Transform FindBone(string boneName) {
        foreach (Transform bone in characterRoot.GetComponentsInChildren<Transform>()) {
            if (bone.name == boneName) {
                return bone;
            }
        }
        throw new KeyNotFoundException(boneName);
    }
}
