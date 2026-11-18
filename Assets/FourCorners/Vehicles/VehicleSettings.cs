using UnityEngine;

namespace FourCorners
{
    [CreateAssetMenu(menuName = "Four Corners/Vehicle Settings")]
    public sealed class VehicleSettings : ScriptableObject
    {
        [Header("Chassis")]
        [Min(1f)] public float Mass = 1280f;
        public Vector3 CenterOfMass = new Vector3(0f, -0.12f, -0.12f);
        [Min(0f)] public float AntiRoll = 6600f;
        [Min(0.1f)] public float WreckHeight = 1.27f;

        [Header("Suspension")]
        [Min(0.01f)] public float WheelRadius = 0.40f;
        [Min(0.01f)] public float RestLength = 0.44f;
        [Min(0.01f)] public float Extension = 0.15f;
        [Min(1f)] public float Spring = 33000f;
        [Min(0f)] public float Damper = 4150f;

        [Header("Powertrain")]
        [Min(0f)] public float DriveForce = 12300f;
        [Min(0.1f)] public float TopSpeed = 25f;
        [Min(0.1f)] public float ReverseSpeed = 8f;
        [Range(0f, 1f)] public float FrontDriveBias;
        [Min(0f)] public float BrakeForce = 18500f;
        [Min(0f)] public float HandbrakeForce = 5300f;

        [Header("Tires and steering")]
        [Range(0f, 65f)] public float LowSpeedSteer = 38f;
        [Range(0f, 40f)] public float HighSpeedSteer = 15f;
        [Min(1f)] public float SteerRate = 195f;
        [Min(0f)] public float LateralStiffness = 1300f;
        [Min(0.1f)] public float TireGrip = 1.22f;
        [Range(0.05f, 1f)] public float HandbrakeRearGrip = 0.31f;
        [Min(0.1f)] public float HandbrakeEngageRate = 4.3f;
        [Min(0.1f)] public float HandbrakeReleaseRate = 2.9f;
    }
}
