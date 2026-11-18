using UnityEngine;

namespace FourCorners
{
    [CreateAssetMenu(menuName = "Four Corners/Vehicle Settings")]
    public sealed class VehicleSettings : ScriptableObject
    {
        [Header("Chassis")]
        [Min(1f)] public float Mass = 1280f;
        public Vector3 CenterOfMass = new Vector3(0f, -0.045f, -0.12f);
        [Min(0f)] public float AntiRoll = 3600f;
        [Min(0.1f)] public float WreckHeight = 1.27f;

        [Header("Suspension")]
        [Min(0.01f)] public float WheelRadius = 0.40f;
        [Min(0.01f)] public float RestLength = 0.51f;
        [Min(0.01f)] public float Extension = 0.20f;
        [Min(1f)] public float Spring = 28800f;
        [Min(0f)] public float Damper = 2700f;
        [Min(0f)] public float ReboundDamper = 3300f;

        [Header("Powertrain")]
        [Min(0f)] public float DriveForce = 16400f;
        [Min(0.1f)] public float TopSpeed = 25f;
        [Min(0.1f)] public float ReverseSpeed = 8f;
        [Range(0f, 1f)] public float FrontDriveBias;
        [Min(0f)] public float BrakeForce = 23500f;
        [Min(0f)] public float HandbrakeForce = 7200f;

        [Header("Tires and steering")]
        [Range(0f, 65f)] public float LowSpeedSteer = 46f;
        [Range(0f, 40f)] public float HighSpeedSteer = 22f;
        [Min(1f)] public float SteerRate = 425f;
        [Min(0f)] public float LateralStiffness = 2450f;
        [Min(0.1f)] public float TireGrip = 1.12f;
        [Range(0.5f, 1f)] public float SlidingGrip = 0.74f;
        [Range(0.05f, 1f)] public float HandbrakeRearGrip = 0.27f;
        [Min(0.1f)] public float HandbrakeEngageRate = 8.5f;
        [Min(0.1f)] public float HandbrakeReleaseRate = 9.8f;

        [Header("Air and recovery")]
        [Min(1f)] public float GravityScale = 1.58f;
        [Min(0f)] public float AirLevelStrength = 28f;
        [Min(0f)] public float AirLevelDamping = 9f;
        [Min(0.1f)] public float RecoveryDelay = 0.95f;
    }
}
