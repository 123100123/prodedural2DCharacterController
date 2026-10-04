using UnityEngine;

// Shooting mode while aiming (aim held or tap-latched): faces the pointer, points the pistol with both hands,
// follows it with the head, fires on click. While the weapon reloads, the support hand fetches the magazine and the
// pistol tilts up; the reload itself runs in the weapon whether or not the character is aiming.
public sealed class ShootingStance {
    private readonly CharacterRig rig;
    private readonly WeaponController weapon;
    private readonly ShootingSettings settings;
    private readonly WeaponSettings weaponSettings;
    private readonly SecondOrderDynamics recoil = new SecondOrderDynamics(0f);

    // Where the pointer was in the world at the last aiming tick; the camera frames the aim with it.
    public Vector2 AimPoint { get; private set; }

    public ShootingStance(CharacterRig rig, (WeaponController weapon, ShootingSettings settings, WeaponSettings weaponSettings) parts) {
        this.rig = rig;
        weapon = parts.weapon;
        settings = parts.settings;
        weaponSettings = parts.weaponSettings;
    }

    // Called only while aiming.
    public void Tick(PlayerCommands commands, float now) {
        Vector2 aimPoint = PointerInWorld(commands.PointerScreenPosition);
        AimPoint = aimPoint;
        rig.Facing.FaceToward(aimPoint.x - rig.Body.position.x);

        Vector2 aimDirection = ClampElevation(AimDirectionFromShoulder(aimPoint));
        float reloadProgress = weapon.ReloadProgress(now);
        Vector2 gripDirection = ReloadMotion.TiltedUp(aimDirection, (rig.Facing.Sign, weaponSettings.reloadTilt * ReloadMotion.Tilt(reloadProgress)));
        Vector2 gripPoint = rig.FrontArm.ShoulderPosition + gripDirection * (rig.FrontArm.Reach * settings.armExtension)
            + RecoilOffset((aimDirection, Time.deltaTime));
        rig.FrontArm.ReachToward(gripPoint);
        rig.BackArm.ReachToward(SupportHandTarget((gripPoint - gripDirection * settings.supportHandOffset, reloadProgress)));
        rig.Head.LookToward(aimDirection);

        if (commands.FirePressed) {
            FirePistol((aimDirection, now));
        }
    }

    private void FirePistol((Vector2 aimDirection, float now) shot) {
        Vector2 muzzle = weapon.MuzzleFor((rig.FrontArm.HandPosition, shot.aimDirection));
        if (weapon.PullTrigger((muzzle, shot.aimDirection), shot.now) == TriggerResult.Fired) {
            recoil.AddVelocity(settings.recoilKick);
        }
    }

    // The support hand rests at its grip, and drops to the magazine below it during a reload.
    private Vector2 SupportHandTarget((Vector2 grip, float reloadProgress) support) {
        Vector2 magazine = support.grip + Vector2.down * weaponSettings.reloadHandDrop;
        return Vector2.Lerp(support.grip, magazine, ReloadMotion.HandDrop(support.reloadProgress));
    }

    // The grip is thrown back along the aim and up, then springs back.
    private Vector2 RecoilOffset((Vector2 aimDirection, float deltaTime) frame) {
        float kick = recoil.Update((0f, frame.deltaTime), settings.recoilSpring);
        return (-frame.aimDirection + Vector2.up * settings.recoilRise) * kick;
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
