using System;
using UnityEngine;

// The shape of the route a hover target flies around its anchor.
public enum HoverPathKind {
    // Back and forth between anchor - extent and anchor + extent, easing in and out at both ends.
    Patrol,
    // An ellipse around the anchor, half as wide and high as the extent.
    Loop,
    // A figure eight lying on its side: once across and back in x for every two swings up and down.
    FigureEight,
}

// Everything tunable about a hover target. A class with initializers, so a scene saved before a field existed keeps its
// default.
[Serializable]
public class HoverTargetSettings {
    [Header("Board")]
    [Tooltip("Side of the square wooden board, in world units.")]
    public float boardSize = 1.5f;
    [Tooltip("Hits that break the board.")]
    public int hitsToBreak = 3;
    [Tooltip("Points popped up for a hit that does not break it.")]
    public int hitPoints = 10;
    [Tooltip("Points popped up for the hit that breaks it.")]
    public int breakPoints = 50;

    [Header("Path")]
    public HoverPathKind path = HoverPathKind.Patrol;
    [Tooltip("Half the span of the route in x and y, around the anchor (the object's position in the scene).")]
    public Vector2 extent = new Vector2(4f, 0f);
    [Tooltip("Seconds for one trip around the route (a patrol: there and back).")]
    public float period = 8f;
    [Tooltip("Where on its route it starts, as a share of a trip (0 to 1); different values keep neighbours apart.")]
    public float startShare;
    [Tooltip("Height of the slow vertical bob it adds to the route.")]
    public float bobHeight = 0.18f;
    [Tooltip("Seconds per bob.")]
    public float bobPeriod = 1.7f;
    [Tooltip("Degrees the board leans into its motion at a horizontal speed of 1 u/s.")]
    public float tiltPerSpeed = 3.5f;
    [Tooltip("Most the board leans, in degrees.")]
    public float maxTilt = 14f;

    [Header("Rotor")]
    [Tooltip("Turns per second of the rotor blades.")]
    public float rotorSpeed = 2.6f;
    [Tooltip("Seconds the rotor takes to run down once the board is broken, and to spin up on its return.")]
    public float rotorRunDown = 0.9f;

    [Header("Hit")]
    public Color flashColor = new Color(1f, 0.55f, 0.35f, 1f);
    public float flashDuration = 0.12f;
    [Tooltip("Speed (degrees per second) a hit sets the board turning, away from the side it was hit on.")]
    public float wobbleKick = 260f;
    public SpringSettings wobbleSpring = new SpringSettings(3.5f, 0.2f, 0f);

    [Header("Break and return")]
    [Tooltip("Gravity of the falling board, in u/s squared.")]
    public float fallGravity = 24f;
    [Tooltip("Degrees per second the falling board tumbles.")]
    public float tumbleSpeed = 380f;
    [Tooltip("The longest the board falls before it is gone, in seconds.")]
    public float maxFallTime = 1.4f;
    [Tooltip("Seconds after the break before the board comes back.")]
    public float respawnDelay = 4f;
    [Tooltip("Seconds the board takes to rise back in, with the rotor spinning up (it cannot be hit meanwhile).")]
    public float riseTime = 1.1f;
    [Tooltip("How far below its place on the route the board starts rising from.")]
    public float riseDistance = 2.5f;
}
