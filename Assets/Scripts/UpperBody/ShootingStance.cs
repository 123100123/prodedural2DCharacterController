using UnityEngine;

// Shooting mode while aiming (aim held or tap-latched): faces the pointer, points the pistol with both hands,
// follows it with the head, fires on click.
public sealed class ShootingStance {
    private readonly CharacterRig rig;
    private readonly Pistol pistol;
    private readonly ShootingSettings settings;

    public ShootingStance(CharacterRig rig, (Pistol pistol, ShootingSettings settings) weapon) {
        this.rig = rig;
        pistol = weapon.pistol;
        settings = weapon.settings;
    }

    // Called only while aiming.
    public void Tick(PlayerCommands commands, float now) {
        Vector2 aimPoint = PointerInWorld(commands.PointerScreenPosition);
        rig.Facing.FaceToward(aimPoint.x - rig.Body.position.x);

        Vector2 aimDirection = ClampElevation(AimDirectionFromShoulder(aimPoint));
        Vector2 gripPoint = rig.FrontArm.ShoulderPosition + aimDirection * (rig.FrontArm.Reach * settings.armExtension);
        rig.FrontArm.ReachToward(gripPoint);
        rig.BackArm.ReachToward(gripPoint - aimDirection * settings.supportHandOffset);
        rig.Head.LookToward(aimDirection);

        if (commands.FirePressed) {
            Vector2 muzzle = pistol.MuzzleFor((rig.FrontArm.HandPosition, aimDirection));
            pistol.TryFire((muzzle, aimDirection), now);
        }
    }

    private Vector2 PointerInWorld(Vector2 screenPosition) {
        Camera camera = settings.aimCamera != null ? settings.aimCamera : Camera.main;
        float depth = rig.Body.position.z - camera.transform.position.z;
        return camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
    }

    // Keeps the aim in front of the character: steep pointer angles are held at maxAimElevation.
    private Vector2 ClampElevation(Vector2 aimDirection) {
        int facing = rig.Facing.Sign;
        float elevation = Mathf.Atan2(aimDirection.y, aimDirection.x * facing) * Mathf.Rad2Deg;
        float clamped = Mathf.Clamp(elevation, -settings.maxAimElevation, settings.maxAimElevation) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(clamped) * facing, Mathf.Sin(clamped));
    }

    private Vector2 AimDirectionFromShoulder(Vector2 aimPoint) {
        Vector2 toAim = aimPoint - rig.FrontArm.ShoulderPosition;
        return toAim.sqrMagnitude > 0f ? toAim.normalized : Vector2.right * rig.Facing.Sign;
    }
}
