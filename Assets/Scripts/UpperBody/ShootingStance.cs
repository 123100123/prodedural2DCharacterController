using UnityEngine;

// Shooting mode while aiming (aim held or tap-latched): faces the pointer, follows it with the head and, once the pistol
// holder has drawn the pistol to the aim, points it with both hands and fires on click (a click during the draw is
// buffered by the holder and fires when the pistol arrives). Until then it only works out the aim grip, which the holder
// carries the hand toward. While the weapon reloads, the support hand fetches the magazine and the pistol tilts up; the
// reload itself runs in the weapon whether or not the character is aiming, and is shown only when the holder says so.
public sealed class ShootingStance {
    // How far ahead of the body the aim point is held when there is no camera to read the pointer through.
    private const float NoCameraAimDistance = 5f;

    private readonly CharacterRig rig;
    private readonly WeaponController weapon;
    private readonly ShootingSettings settings;
    private readonly WeaponSettings weaponSettings;
    private readonly PistolView sidearm;
    private readonly PistolHolder holder;
    private readonly SecondOrderDynamics recoil = new SecondOrderDynamics(0f);
    private float recoilKick;

    // Where the pointer was in the world at the last aiming tick; the camera frames the aim with it.
    public Vector2 AimPoint { get; private set; }

    // Where the front hand holds the pistol at the aim this tick and the way the barrel points (reload tilt included).
    public (Vector2 grip, Vector2 direction) AimGrip { get; private set; }

    // Without a holder (a bare stance) the pistol counts as always drawn and fires on every click.
    public ShootingStance(CharacterRig rig, (WeaponController weapon, ShootingSettings settings, WeaponSettings weaponSettings) parts, (PistolView view, PistolHolder holder) sidearm = default) {
        this.rig = rig;
        this.sidearm = sidearm.view;
        holder = sidearm.holder;
        weapon = parts.weapon;
        settings = parts.settings;
        weaponSettings = parts.weaponSettings;
    }

    // Called only while aiming.
    public void Tick(PlayerCommands commands, float now) {
        Vector2 aimPoint = PointerInWorld(commands.PointerScreenPosition);
        AimPoint = aimPoint;
        if (!rig.HoldsFacing) {
            rig.Facing.FaceToward(aimPoint.x - rig.Body.position.x);
        }

        Vector2 aimDirection = ClampElevation(AimDirectionFromShoulder(aimPoint));
        float reloadProgress = holder == null || holder.ShowsReload ? weapon.ReloadProgress(now) : 0f;
        Vector2 gripDirection = ReloadMotion.TiltedUp(aimDirection, (rig.Facing.Sign, weaponSettings.reloadTilt * ReloadMotion.Tilt(reloadProgress)));
        Vector2 gripPoint = rig.FrontArm.ShoulderPosition + gripDirection * (rig.FrontArm.Reach * settings.armExtension)
            + RecoilOffset((aimDirection, Time.deltaTime));
        AimGrip = (gripPoint, gripDirection);
        rig.Head.LookToward(aimDirection);
        // Below full aim share the holder has the hands (it is drawing the pistol toward this grip).
        if (holder == null || holder.AimShare >= 1f) {
            rig.FrontArm.ReachToward(gripPoint);
            sidearm?.SetBarrelDirection(gripDirection, recoilKick);
            rig.BackArm.ReachToward(SupportHandTarget((gripPoint - gripDirection * settings.supportHandOffset, reloadProgress)));
        }

        bool fires = holder != null ? holder.FiresNow : commands.FirePressed;
        if (fires) {
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
        recoilKick = kick;
        return (-frame.aimDirection + Vector2.up * settings.recoilRise) * kick;
    }

    private Vector2 PointerInWorld(Vector2 screenPosition) {
        Camera camera = settings.aimCamera != null ? settings.aimCamera : Camera.main;
        if (camera == null) {
            return (Vector2)rig.Body.position + Vector2.right * (rig.Facing.Sign * NoCameraAimDistance);
        }
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
