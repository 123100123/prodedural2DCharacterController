using System;
using UnityEngine;

[Serializable]
public class ShootingSettings {
    [Tooltip("Camera that converts the pointer to world space; falls back to the main camera.")]
    public Camera aimCamera;
    [Tooltip("Share of full arm length used when aiming, keeping a slight elbow bend.")]
    [Range(0.5f, 1f)] public float armExtension = 0.92f;
}
