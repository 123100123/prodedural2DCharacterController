using System;
using UnityEngine;

[Serializable]
public class ShootingSettings {
    [Tooltip("Camera that converts the pointer to world space; falls back to the main camera.")]
    public Camera aimCamera;
    [Tooltip("Share of full arm length used when aiming, keeping a slight elbow bend.")]
    [Range(0.5f, 1f)] public float armExtension = 0.92f;
    [Tooltip("Aim is kept within this angle above and below horizontal in front of the character, so the arm never swings over the head.")]
    [Range(10f, 89f)] public float maxAimElevation = 80f;
    [Tooltip("How far behind the front hand, along the aim, the support hand grips.")]
    public float supportHandOffset = 0.1f;
}
