// Implemented by a component on a collider's object (or on its rigidbody's object) that wants to know when a bullet
// strikes it. The weapon already applies the impulse to a dynamic body; a receiver only reacts.
public interface IShotReceiver {
    void OnShot(ShotHit hit);
}
