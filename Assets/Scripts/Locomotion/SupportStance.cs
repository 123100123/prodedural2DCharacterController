// The leg carrying the body and how far the body has travelled over its planted foot: progress runs from 0 at contact
// (the hip behind the foot) through 0.5 at mid-stance (the hip over it) to 1 at toe-off (the hip ahead of it), and past
// 1 while the body flies on after the foot has left the ground.
public readonly struct SupportStance {
    public enum Leg { None, Front, Back }

    public static readonly SupportStance Unsupported = new SupportStance((Leg.None, false), (0f, 1f, 0f, 0f));

    public SupportStance((Leg leg, bool isPlanted) support, (float footX, float direction, float stanceTravel, float progress) stance) {
        SupportLeg = support.leg;
        IsPlanted = support.isPlanted;
        FootX = stance.footX;
        Direction = stance.direction;
        StanceTravel = stance.stanceTravel;
        Progress = stance.progress;
    }

    public Leg SupportLeg { get; }
    // False in a running flight phase: the last support foot has left the ground.
    public bool IsPlanted { get; }
    public float FootX { get; }
    // +1 moving toward world right, -1 toward world left.
    public float Direction { get; }
    // How far the body travels over the foot from contact to toe-off.
    public float StanceTravel { get; }
    public float Progress { get; }
    public bool HasSupport => SupportLeg != Leg.None;
}
