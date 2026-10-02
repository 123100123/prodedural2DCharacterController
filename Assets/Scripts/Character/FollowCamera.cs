using System;
using UnityEngine;

// Follows the character with spring lag and a little look-ahead, and takes small downward kicks on heavy foot plants
// and landings so impacts are felt through the camera.
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
    private (SecondOrderDynamics x, SecondOrderDynamics y, SecondOrderDynamics kick) springs;
    private Vector2 previousBodyPosition;

    public void Initialize(Transform followedBody, CharacterEvents events) {
        body = followedBody;
        targetCamera = targetCamera != null ? targetCamera : Camera.main;
        Vector2 start = FollowPoint(0f);
        springs = (new SecondOrderDynamics(start.x), new SecondOrderDynamics(start.y), new SecondOrderDynamics(0f));
        previousBodyPosition = body.position;

        events.Landed += landing => springs.kick.AddVelocity(-landing.impactSpeed * landingKickPerSpeed);
        events.FootPlanted += KickOnHeavyPlant;
    }

    public void Tick(float deltaTime) {
        if (!followsCharacter || targetCamera == null || deltaTime <= 0f) {
            return;
        }

        Vector2 target = FollowPoint(BodySpeedX(deltaTime));
        float x = springs.x.Update((target.x, deltaTime), horizontalSpring);
        float y = springs.y.Update((target.y, deltaTime), verticalSpring);
        float kick = springs.kick.Update((0f, deltaTime), kickSpring);

        Transform cameraTransform = targetCamera.transform;
        cameraTransform.position = new Vector3(x, y + kick, cameraTransform.position.z);
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
