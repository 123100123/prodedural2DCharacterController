using UnityEngine;

// Shooting mode while aim is held: faces the pointer, points the pistol arm at it, follows it with the head, fires on click.
public sealed class ShootingStance {
    private readonly CharacterRig rig;
    private readonly Pistol pistol;
    private readonly ShootingSettings settings;

    public ShootingStance(CharacterRig rig, (Pistol pistol, ShootingSettings settings) weapon) {
        this.rig = rig;
        pistol = weapon.pistol;
        settings = weapon.settings;
    }

    public void Tick(PlayerCommands commands, float now) {
        if (!commands.AimHeld) {
            return;
        }

        Vector2 aimPoint = PointerInWorld(commands.PointerScreenPosition);
        rig.Facing.FaceToward(aimPoint.x - rig.Body.position.x);

        Vector2 aimDirection = AimDirectionFromShoulder(aimPoint);
        rig.FrontArm.ReachToward(rig.FrontArm.ShoulderPosition + aimDirection * (rig.FrontArm.Reach * settings.armExtension));
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

    private Vector2 AimDirectionFromShoulder(Vector2 aimPoint) {
        Vector2 toAim = aimPoint - rig.FrontArm.ShoulderPosition;
        return toAim.sqrMagnitude > 0f ? toAim.normalized : Vector2.right * rig.Facing.Sign;
    }
}
