using System;
using UnityEngine;

// Moments other systems react to (camera kicks and effects now; sound later), raised by the locomotion and upper body.
public sealed class CharacterEvents {
    public event Action<(Vector2 position, float impactSpeed)> FootPlanted;
    public event Action<(Vector2 position, float impactSpeed)> Landed;
    public event Action<Vector2> Jumped;
    public event Action<(Vector2 muzzle, Vector2 direction)> Fired;
    // A bullet struck a collider.
    public event Action<ShotHit> ShotHit;
    // The trigger was pulled on an empty magazine (a reload starts with it).
    public event Action<(Vector2 muzzle, Vector2 direction)> DryFired;
    // A reload began, with its duration in seconds.
    public event Action<float> ReloadStarted;
    // A reload ended, with the rounds now in the magazine.
    public event Action<int> ReloadFinished;
    // A roll's hands met the ground: where, and which way (+1 right, -1 left) the roll travels.
    public event Action<(Vector2 position, int direction)> Rolled;
    // A jump kicked off a wall: where the feet left it, and which way (+1 right, -1 left) the character flies.
    public event Action<(Vector2 position, int direction)> WallJumped;
    // The character caught a wall and began sliding down it: the wall face beside the body, and the wall's side (+1 right
    // of the body, -1 left).
    public event Action<(Vector2 contact, int wallSide)> WallSlideStarted;
    // The slide ended (a wall jump, dropping off, landing or climbing a ledge).
    public event Action WallSlideStopped;
    // A running slide began: the ground under the body, and which way (+1 right, -1 left) it slides.
    public event Action<(Vector2 position, int direction)> SlideStarted;
    // The running slide ended (getting up, jumping out, leaving a ledge or stopping against a wall).
    public event Action SlideStopped;
    // The character let go of a ledge it hung from: the corner its hands left, and the ledge's side (+1 right of the body).
    public event Action<(Vector2 corner, int climbDirection)> LedgeDropped;
    // Down + jump dropped the character through the one-way platform it stood on: where its feet left the platform.
    public event Action<Vector2> DroppedThroughPlatform;
    // The walking character took hold of a crate (a dynamic body) to push it: its collider, and which way (+1 right, -1
    // left) the push goes.
    public event Action<(Collider2D crate, int direction)> PushStarted;
    // The push let go (walking away, aiming, jumping, the crate getting away or the walk ending).
    public event Action PushStopped;

    public void RaiseFootPlanted((Vector2 position, float impactSpeed) plant) => FootPlanted?.Invoke(plant);

    public void RaiseLanded((Vector2 position, float impactSpeed) landing) => Landed?.Invoke(landing);

    public void RaiseJumped(Vector2 launchVelocity) => Jumped?.Invoke(launchVelocity);

    public void RaiseFired((Vector2 muzzle, Vector2 direction) shot) => Fired?.Invoke(shot);

    public void RaiseShotHit(ShotHit hit) => ShotHit?.Invoke(hit);

    public void RaiseDryFired((Vector2 muzzle, Vector2 direction) shot) => DryFired?.Invoke(shot);

    public void RaiseReloadStarted(float duration) => ReloadStarted?.Invoke(duration);

    public void RaiseReloadFinished(int rounds) => ReloadFinished?.Invoke(rounds);

    public void RaiseRolled((Vector2 position, int direction) roll) => Rolled?.Invoke(roll);

    public void RaiseWallJumped((Vector2 position, int direction) kick) => WallJumped?.Invoke(kick);

    public void RaiseWallSlideStarted((Vector2 contact, int wallSide) slide) => WallSlideStarted?.Invoke(slide);

    public void RaiseWallSlideStopped() => WallSlideStopped?.Invoke();

    public void RaiseSlideStarted((Vector2 position, int direction) slide) => SlideStarted?.Invoke(slide);

    public void RaiseSlideStopped() => SlideStopped?.Invoke();

    public void RaiseLedgeDropped((Vector2 corner, int climbDirection) drop) => LedgeDropped?.Invoke(drop);

    public void RaiseDroppedThroughPlatform(Vector2 feet) => DroppedThroughPlatform?.Invoke(feet);

    public void RaisePushStarted((Collider2D crate, int direction) push) => PushStarted?.Invoke(push);

    public void RaisePushStopped() => PushStopped?.Invoke();
}
