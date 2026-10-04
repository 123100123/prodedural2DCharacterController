using System;
using UnityEngine;

// Follows the character with spring lag and a little look-ahead, leans toward the pointer while aiming, and takes small
// downward kicks on heavy foot plants and landings so impacts are felt through the camera. Shots and hard landings also
// shake it, on top of the followed position (the shake never feeds back into the springs).
[Serializable]
public class FollowCamera {
    [Tooltip("Camera to move; falls back to the main camera. Disable to leave the camera where it is.")]
    [SerializeField] private bool followsCharacter = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f);
    [Tooltip("Distance the camera leads ahead at full walking speed.")]
    [SerializeField] private float lookAhead = 1.5f;
    [SerializeField] private float lookAheadFullSpeed = 10f;
    [SerializeField] private SpringSettings horizontalSpring = new SpringSettings(1.4f, 0.9f, 0f);
    [SerializeField] private SpringSettings verticalSpring = new SpringSettings(1.2f, 1f, 0f);

    [Header("Impact kicks")]
    [SerializeField] private SpringSettings kickSpring = new SpringSettings(5f, 0.35f, 0f);
    [Tooltip("Downward camera velocity per unit of landing impact speed.")]
    [SerializeField] private float landingKickPerSpeed = 0.12f;
    [Tooltip("Downward camera velocity per unit of foot impact speed, for plants harder than footKickThreshold.")]
    [SerializeField] private float footKickPerSpeed = 0.02f;
    [SerializeField] private float footKickThreshold = 6f;

    private Transform body;
    private CameraFeelSettings feel;
    private CameraShake shake;
    private (SecondOrderDynamics x, SecondOrderDynamics y, SecondOrderDynamics kick, SecondOrderDynamics aim) springs;
    private Vector2 previousBodyPosition;
    private Vector2 framedOffset;
    private float baseRoll;

    // The camera's shake, for tools and tests.
    public CameraShake Shake => shake;
    // How far the aim currently leans the camera from where it would follow the character.
    public Vector2 AimOffset => springs.aim == null ? Vector2.zero : framedOffset * springs.aim.Value;

    public void Initialize(Transform followedBody, (CharacterEvents events, CameraFeelSettings feel) parts) {
        body = followedBody;
        feel = parts.feel;
        shake = new CameraShake(feel);
        targetCamera = targetCamera != null ? targetCamera : Camera.main;
        Vector2 start = FollowPoint(0f);
        springs = (new SecondOrderDynamics(start.x), new SecondOrderDynamics(start.y), new SecondOrderDynamics(0f), new SecondOrderDynamics(0f));
        previousBodyPosition = body.position;
        baseRoll = targetCamera != null ? targetCamera.transform.eulerAngles.z : 0f;

        parts.events.Landed += landing => {
            springs.kick.AddVelocity(-landing.impactSpeed * landingKickPerSpeed);
            shake.AddTrauma(CameraShake.LandingTrauma(landing.impactSpeed, feel));
        };
        parts.events.FootPlanted += KickOnHeavyPlant;
        parts.events.Fired += _ => shake.AddTrauma(feel.firedTrauma);
        parts.events.ShotHit += _ => shake.AddTrauma(feel.shotHitTrauma);
    }

    // The aim: whether the character is aiming, and where the pointer is in the world.
    public void Tick(float deltaTime, (bool isAiming, Vector2 pointerWorld) aim) {
        if (!followsCharacter || targetCamera == null || deltaTime <= 0f) {
            return;
        }

        float aimWeight = FrameAim((aim.isAiming, aim.pointerWorld, deltaTime));
        Vector2 target = FollowPoint(BodySpeedX(deltaTime)) + framedOffset * aimWeight;
        float x = springs.x.Update((target.x, deltaTime), horizontalSpring);
        float y = springs.y.Update((target.y, deltaTime), verticalSpring);
        float kick = springs.kick.Update((0f, deltaTime), kickSpring);

        shake.Tick(deltaTime);
        Vector2 shaken = shake.Offset;
        Transform cameraTransform = targetCamera.transform;
        cameraTransform.position = new Vector3(x + shaken.x, y + kick + shaken.y, cameraTransform.position.z);
        cameraTransform.rotation = Quaternion.Euler(0f, 0f, baseRoll + shake.Roll);
    }

    // Where the aim pulls the camera from the character: a fraction of the way to the pointer, held within a maximum.
    public static Vector2 AimLean((Vector2 toPointer, float fraction, Vector2 maxOffset) aim) {
        return new Vector2(
            Mathf.Clamp(aim.toPointer.x * aim.fraction, -aim.maxOffset.x, aim.maxOffset.x),
            Mathf.Clamp(aim.toPointer.y * aim.fraction, -aim.maxOffset.y, aim.maxOffset.y));
    }

    // Eases the aim framing in while aiming and out when it ends (it fades from where the pointer last was).
    private float FrameAim((bool isAiming, Vector2 pointerWorld, float deltaTime) frame) {
        if (frame.isAiming) {
            framedOffset = AimLean((frame.pointerWorld - (Vector2)body.position, feel.aimFraction, feel.maxAimOffset));
        }
        return springs.aim.Update((frame.isAiming ? 1f : 0f, frame.deltaTime), feel.aimBlendSpring);
    }

    private void KickOnHeavyPlant((Vector2 position, float impactSpeed) plant) {
        if (plant.impactSpeed > footKickThreshold) {
            springs.kick.AddVelocity(-plant.impactSpeed * footKickPerSpeed);
        }
    }

    private Vector2 FollowPoint(float bodySpeedX) {
        float lead = Mathf.Clamp(bodySpeedX / lookAheadFullSpeed, -1f, 1f) * lookAhead;
        return (Vector2)body.position + offset + Vector2.right * lead;
    }

    private float BodySpeedX(float deltaTime) {
        float speed = (body.position.x - previousBodyPosition.x) / deltaTime;
        previousBodyPosition = body.position;
        return speed;
    }
}
