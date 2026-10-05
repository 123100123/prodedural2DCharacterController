// The set of full-body states, filled once after construction so states can name each other as transition targets.
public sealed class CharacterStates {
    public WalkState Walk { get; private set; }
    public JumpWindupState JumpWindup { get; private set; }
    public AirborneState Airborne { get; private set; }
    public RollState Roll { get; private set; }
    public ClimbState Climb { get; private set; }
    public WallSlideState WallSlide { get; private set; }

    public void Register((WalkState walk, JumpWindupState jumpWindup, AirborneState airborne, RollState roll, ClimbState climb, WallSlideState wallSlide) states) {
        Walk = states.walk;
        JumpWindup = states.jumpWindup;
        Airborne = states.airborne;
        Roll = states.roll;
        Climb = states.climb;
        WallSlide = states.wallSlide;
    }
}
