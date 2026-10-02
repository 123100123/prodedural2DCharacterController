using UnityEngine;

// Moves the body pivot toward its target as a mass on springs, keeps it out of walls, and restarts from wherever it is
// when something else moved it (a respawn, a teleport) or another state hands it over.
public sealed class BodyCarriage {
    // A body found further than this from where the springs last put it was moved by something else.
    private const float ExternalMoveDistance = 0.5f;

    private readonly Transform body;
    private SecondOrderDynamics x;
    private SecondOrderDynamics y;
    private Vector2 lastWrittenPosition;

    public BodyCarriage(Transform carriedBody) {
        body = carriedBody;
    }

    public Vector2 Velocity => x == null ? Vector2.zero : new Vector2(x.Velocity, y.Velocity);

    // True before the first carry and after the body was moved by something else.
    public bool NeedsRestart => x == null || Vector2.Distance(body.position, lastWrittenPosition) > ExternalMoveDistance;

    // Continues from the body's current position with the given momentum.
    public void Restart(Vector2 velocity) {
        Vector2 position = body.position;
        x = x ?? new SecondOrderDynamics(position.x);
        y = y ?? new SecondOrderDynamics(position.y);
        x.ResetWithVelocity((position.x, velocity.x));
        y.ResetWithVelocity((position.y, velocity.y));
        lastWrittenPosition = position;
    }

    // An instantaneous vertical push, e.g. a foot taking the weight or pushing off.
    public void Push(float verticalVelocity) => y?.AddVelocity(verticalVelocity);

    public void Carry((Vector2 target, float deltaTime) frame, (SpringSettings horizontal, SpringSettings vertical, (float min, float max) span) motion) {
        float carriedX = KeepWithinSpan(x.Update((frame.target.x, frame.deltaTime), motion.horizontal), motion.span);
        float carriedY = y.Update((frame.target.y, frame.deltaTime), motion.vertical);
        body.position = new Vector3(carriedX, carriedY, body.position.z);
        lastWrittenPosition = body.position;
    }

    // A spring overshooting into a wall stops there and loses its speed toward it.
    private float KeepWithinSpan(float carriedX, (float min, float max) span) {
        float clamped = Mathf.Clamp(carriedX, Mathf.Min(span.min, span.max), Mathf.Max(span.min, span.max));
        if (!Mathf.Approximately(clamped, carriedX)) {
            x.ResetWithVelocity((clamped, 0f));
        }
        return clamped;
    }
}
