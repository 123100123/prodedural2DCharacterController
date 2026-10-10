using UnityEngine;

// Follows the pistol from frame to frame, at the very end of each frame (after the arms, the leg IK and the pistol's own
// pose), in the space of what carries it. Motion the carrier gives it (the thigh swinging the holster, the hand carrying
// it) is not a jump; moving within the holster, or a change of carrier that moves it, is. Motion within the hand is the
// carry itself and is not measured (plan §27).
[DefaultExecutionOrder(10000)]
public sealed class PistolTrack : MonoBehaviour {
    private PistolView view;
    private Transform parent;
    private Vector3 world;
    private Vector3 local;
    private Matrix4x4 holster;
    private Matrix4x4 pivot;

    public float WorstInHolster { get; private set; }
    public float WorstSwap { get; private set; }
    public int Swaps { get; private set; }
    // The largest move of the pistol in the world in one frame while it was in the holster, carried motion included.
    public float FastestInHolster { get; private set; }

    public static PistolTrack Follow(PistolView view) {
        PistolTrack track = new GameObject("pistolTrack").AddComponent<PistolTrack>();
        track.view = view;
        track.Remember();
        return track;
    }

    private void LateUpdate() {
        if (view == null || view.Pistol == null) {
            return;
        }
        Transform now = view.Parent;
        if (now != parent) {
            // Carried by either: the new parent from where it let go, or the old one (a pistol seated in the holster as
            // the hand grips it stays with the holster that frame).
            Transform old = now == view.Holster ? view.WeaponPivot : view.Holster;
            Matrix4x4 newBefore = now == view.Holster ? holster : pivot;
            Matrix4x4 oldBefore = now == view.Holster ? pivot : holster;
            Vector3 withNew = now.TransformPoint(newBefore.inverse.MultiplyPoint3x4(world));
            Vector3 withOld = old.TransformPoint(oldBefore.inverse.MultiplyPoint3x4(world));
            float jump = Mathf.Min(Vector2.Distance(withNew, view.Pistol.position), Vector2.Distance(withOld, view.Pistol.position));
            WorstSwap = Mathf.Max(WorstSwap, jump);
            Swaps++;
        } else if (now == view.Holster) {
            WorstInHolster = Mathf.Max(WorstInHolster, Vector2.Distance(now.TransformPoint(local), view.Pistol.position));
            FastestInHolster = Mathf.Max(FastestInHolster, Vector2.Distance(world, view.Pistol.position));
        }
        Remember();
    }

    private void Remember() {
        parent = view.Parent;
        world = view.Pistol.position;
        local = view.Pistol.localPosition;
        holster = view.Holster.localToWorldMatrix;
        pivot = view.WeaponPivot.localToWorldMatrix;
    }

    public override string ToString() => $"swaps {Swaps}, worst swap {WorstSwap:F4}, worst within the holster {WorstInHolster:F4}";
}
