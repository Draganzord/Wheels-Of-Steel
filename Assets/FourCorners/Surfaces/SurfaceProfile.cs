using UnityEngine;

namespace FourCorners
{
    public enum SurfaceKind { Asphalt, Grass, Mud, Sand, Water }

    [CreateAssetMenu(menuName = "Four Corners/Surface Profile")]
    public sealed class SurfaceProfile : ScriptableObject
    {
        public SurfaceKind Kind;
        [Min(0f)] public float Grip = 1f;
        [Min(0f)] public float RollingResistance = 0.015f;
        [Min(0f)] public float DriveScale = 1f;

        [Header("Wheel evidence")]
        public Color MarkTint = new(0.12f, 0.09f, 0.07f, 0.65f);
        public Color SlideTint = new(0.12f, 0.09f, 0.07f, 0.8f);
        [Tooltip("Color of freshly wet prints on sand after leaving the creek.")]
        public Color WetMarkTint = new(0.073f, 0.062f, 0.049f, 0.92f);
        [Min(0.1f)] public float MarkSpacing = 0.42f;
        [Min(0.05f)] public float MarkWidth = 0.31f;
        [Min(0f)] public float MinimumMarkSlip;

        [Header("Sound and particles")]
        public AudioClip RollingAudio;
        [Range(0f, 1f)] public float AudioLevel = 0.5f;
        public Color SprayTint = Color.white;
        [Min(0f)] public float SprayRate = 3f;
    }

    public struct WheelContact
    {
        public bool Grounded;
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Forward;
        public Vector3 Velocity;
        public float Slip;
        public SurfaceProfile Surface;
        public float SurfaceHeight;
    }
}
