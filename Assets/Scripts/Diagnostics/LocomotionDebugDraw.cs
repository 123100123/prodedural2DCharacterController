using System;
using UnityEngine;

// Optional lines over the character, drawn from the telemetry with Debug.DrawLine: visible in the Scene view, and in
// the Game view while its Gizmos toggle is on. Nothing is drawn unless asked for.
public static class LocomotionDebugDraw {
    [Flags]
    public enum Layers {
        None = 0,
        FootTargets = 1 << 0,
        IkTargets = 1 << 1,
        PlannedFootholds = 1 << 2,
        BodyTarget = 1 << 3,
        StrideAnchor = 1 << 4,
        SupportRange = 1 << 5,
        Pelvis = 1 << 6,
        PostureForward = 1 << 7,
        GroundProbes = 1 << 8,
        SupportLeg = 1 << 9,
        GaitPhase = 1 << 10,
        PelvisPath = 1 << 11,
        SupportOffset = 1 << 12,
    }

    private const float MarkSize = 0.08f;
    private const float PostureLineLength = 1.5f;
    private const float PhaseBarWidth = 1.2f;
    private const float PhaseBarHeight = 3.6f;
    private const float PelvisPathSeconds = 1.5f;
    private static readonly Color SupportColor = Color.green;
    private static readonly Color SurgeColor = new Color(1f, 0.6f, 0.1f);
    private static readonly Color FrontColor = new Color(0.3f, 0.85f, 1f);
    private static readonly Color BackColor = new Color(1f, 0.4f, 0.85f);

    public static void Draw((LocomotionTelemetry telemetry, Layers layers) frame) {
        LocomotionTelemetry t = frame.telemetry;
        Layers layers = frame.layers;
        if (layers == Layers.None) {
            return;
        }
        DrawLegs((t, layers));
        DrawBody((t, layers));
        if (Has(layers, Layers.GroundProbes)) {
            foreach (Vector2 candidate in t.FootholdCandidates) {
                Debug.DrawLine(candidate, candidate + Vector2.up * MarkSize, Color.gray);
            }
        }
        if (Has(layers, Layers.GaitPhase)) {
            DrawPhaseBar(t);
        }
    }

    private static void DrawLegs((LocomotionTelemetry t, Layers layers) frame) {
        (LocomotionTelemetry.Leg leg, Color color)[] legs = { (frame.t.Front, FrontColor), (frame.t.Back, BackColor) };
        foreach ((LocomotionTelemetry.Leg leg, Color color) foot in legs) {
            if (Has(frame.layers, Layers.FootTargets)) {
                Cross((foot.leg.FootTarget, foot.color));
            }
            if (Has(frame.layers, Layers.IkTargets)) {
                Cross((foot.leg.Solver, Color.white));
            }
            if (Has(frame.layers, Layers.PlannedFootholds) && !foot.leg.IsGrounded) {
                Debug.DrawLine(foot.leg.Solver, foot.leg.FootTarget, foot.color);
            }
        }
        if (Has(frame.layers, Layers.SupportLeg)) {
            DrawSupportLeg(frame.t);
        }
    }

    // The support foot to its hip in full colour; another planted foot (double support) dimmer.
    private static void DrawSupportLeg(LocomotionTelemetry t) {
        SupportStance support = t.Support;
        if (!support.HasSupport || !support.IsPlanted) {
            return;
        }
        bool frontCarries = support.SupportLeg == SupportStance.Leg.Front;
        LocomotionTelemetry.Leg carrying = frontCarries ? t.Front : t.Back;
        LocomotionTelemetry.Leg other = frontCarries ? t.Back : t.Front;
        Debug.DrawLine(carrying.FootTarget, carrying.Hip, SupportColor);
        if (other.IsGrounded) {
            Debug.DrawLine(other.FootTarget, other.Hip, SupportColor * 0.5f);
        }
    }

    private static void DrawBody((LocomotionTelemetry t, Layers layers) frame) {
        LocomotionTelemetry t = frame.t;
        if (Has(frame.layers, Layers.BodyTarget)) {
            Cross((t.BodyTarget, Color.yellow));
        }
        if (Has(frame.layers, Layers.Pelvis)) {
            Cross((t.BodyPosition, Color.red));
        }
        if (Has(frame.layers, Layers.StrideAnchor)) {
            Debug.DrawLine(new Vector2(t.StrideAnchorX, t.StanceHeight), new Vector2(t.StrideAnchorX, t.StanceHeight + 0.5f), Color.yellow);
        }
        if (Has(frame.layers, Layers.SupportRange)) {
            Debug.DrawLine(new Vector2(t.Front.FootTarget.x, t.StanceHeight), new Vector2(t.Back.FootTarget.x, t.StanceHeight), Color.green);
        }
        if (Has(frame.layers, Layers.PostureForward)) {
            Debug.DrawLine(t.BodyPosition, t.BodyPosition + t.PostureUp * PostureLineLength, SurgeColor);
        }
        if (Has(frame.layers, Layers.PelvisPath)) {
            Debug.DrawLine(t.PreviousBodyPosition, t.BodyPosition, Color.red, PelvisPathSeconds);
        }
        if (Has(frame.layers, Layers.SupportOffset)) {
            DrawPelvisOffsets(t);
        }
    }

    // At the pelvis target's height: the anchor, the support offset from it (green) and the surge on top (orange),
    // ending at the target.
    private static void DrawPelvisOffsets(LocomotionTelemetry t) {
        float y = t.BodyTarget.y;
        Vector2 anchor = new Vector2(t.StrideAnchorX, y);
        Vector2 supported = anchor + Vector2.right * t.PelvisOffsets.support;
        Debug.DrawLine(anchor + Vector2.down * MarkSize, anchor + Vector2.up * MarkSize, Color.yellow);
        Debug.DrawLine(anchor, supported, SupportColor);
        Debug.DrawLine(supported, supported + Vector2.right * t.PelvisOffsets.surge, SurgeColor);
    }

    // A bar above the character filling with the gait phase; ticks mark the front leg's lift (0) and the back leg's (0.5).
    private static void DrawPhaseBar(LocomotionTelemetry t) {
        Vector2 start = t.BodyPosition + new Vector2(-PhaseBarWidth / 2f, PhaseBarHeight);
        Debug.DrawLine(start, start + Vector2.right * PhaseBarWidth, Color.gray);
        Debug.DrawLine(start, start + Vector2.right * (PhaseBarWidth * t.GaitPhase), Color.white);
        Debug.DrawLine(start + Vector2.down * MarkSize, start + Vector2.up * MarkSize, FrontColor);
        Vector2 backTick = start + Vector2.right * (PhaseBarWidth * GaitCycle.BackLegLiftPhase);
        Debug.DrawLine(backTick + Vector2.down * MarkSize, backTick + Vector2.up * MarkSize, BackColor);
    }

    private static void Cross((Vector2 at, Color color) mark) {
        Debug.DrawLine(mark.at + new Vector2(-MarkSize, -MarkSize), mark.at + new Vector2(MarkSize, MarkSize), mark.color);
        Debug.DrawLine(mark.at + new Vector2(-MarkSize, MarkSize), mark.at + new Vector2(MarkSize, -MarkSize), mark.color);
    }

    private static bool Has(Layers layers, Layers layer) => (layers & layer) != 0;
}
