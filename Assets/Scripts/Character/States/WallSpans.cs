// How far the grounded body and its feet may travel sideways before geometry stops them.
public static class WallSpans {
    // The body may travel as far as its hull is free; feet may reach the wall face itself, a hull half-width further.
    public static ((float min, float max) feet, (float min, float max) body) Within(CharacterRig rig) {
        float bodyX = rig.Body.position.x;
        float lookAhead = rig.Stride.WallProbeDistance;
        KinematicBodyMotor motor = rig.Motor;

        (float min, float max) body = (bodyX - motor.FreeDistance(-1f, lookAhead), bodyX + motor.FreeDistance(1f, lookAhead));
        return ((body.min - motor.HullHalfWidth, body.max + motor.HullHalfWidth), body);
    }
}
