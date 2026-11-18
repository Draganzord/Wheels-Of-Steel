using System;
using UnityEngine;

namespace FourCorners
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeVehicle : MonoBehaviour
    {
        [Serializable]
        public struct WheelMount
        {
            public Vector3 Anchor;
            public Transform Knuckle;
            public Transform Hub;
        }

        [SerializeField] private VehicleSettings settings;
        [SerializeField] private WheelMount[] wheels;
        [SerializeField] private Collider[] bumperColliders;
        [SerializeField] private Renderer[] paintRenderers;
        [SerializeField] private Transform damagePanel;
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

        private readonly WheelContact[] contacts = new WheelContact[4];
        private readonly float[] compression = new float[4];
        private readonly float[] wheelRotation = new float[4];
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private float throttle;
        private float steering;
        private float steerAngle;
        private float handbrakeBlend;
        private bool handbrake;

        public Rigidbody Body { get; private set; }
        public VehicleSettings Settings => settings;
        public bool IsWreck { get; private set; }
        public WheelContact[] Contacts => contacts;
        public int GroundedWheelCount { get; private set; }
        public float Speed => Body.linearVelocity.magnitude;

        public void Configure(VehicleSettings tuning, WheelMount[] mounts, Collider[] bumpers,
            Renderer[] bodyPaint, Transform panel)
        {
            settings = tuning;
            wheels = mounts;
            bumperColliders = bumpers;
            paintRenderers = bodyPaint;
            damagePanel = panel;
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = settings.Mass;
            Body.centerOfMass = settings.CenterOfMass;
        }

        public void SetInput(float throttle, float steer, bool handbrake)
        {
            this.throttle = Mathf.Clamp(throttle, -1f, 1f);
            steering = Mathf.Clamp(steer, -1f, 1f);
            this.handbrake = handbrake;
        }

        public void BecomeWreck()
        {
            if (IsWreck)
                return;

            IsWreck = true;
            SetInput(0f, 0f, false);
            Array.Clear(contacts, 0, contacts.Length);
            GroundedWheelCount = 0;
            foreach (Collider bumper in bumperColliders)
                bumper.enabled = false;
            var hull = GetComponent<BoxCollider>();
            Vector3 size = hull.size;
            hull.center += Vector3.up * ((settings.WreckHeight - size.y) * 0.5f);
            size.y = settings.WreckHeight;
            hull.size = size;

            Body.linearDamping = 0.55f;
            Body.angularDamping = 1.45f;

            var finish = new MaterialPropertyBlock();
            foreach (Renderer paint in paintRenderers)
            {
                paint.GetPropertyBlock(finish);
                var color = paint.sharedMaterial.GetColor(BaseColor);
                finish.SetColor(BaseColor, Color.Lerp(color, new Color(0.12f, 0.085f, 0.068f), 0.76f));
                paint.SetPropertyBlock(finish);
            }

            if (damagePanel != null)
                damagePanel.localRotation *= Quaternion.Euler(-13f, 9f, 8f);
        }

        private void FixedUpdate()
        {
            if (IsWreck)
                return;

            float delta = Time.fixedDeltaTime;
            float forwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);
            float speedBlend = Mathf.InverseLerp(4f, settings.TopSpeed * 0.88f, Mathf.Abs(forwardSpeed));
            float steeringLimit = Mathf.Lerp(settings.LowSpeedSteer, settings.HighSpeedSteer, speedBlend);
            steerAngle = Mathf.MoveTowards(steerAngle, steering * steeringLimit, settings.SteerRate * delta);
            float handbrakeRate = handbrake ? settings.HandbrakeEngageRate : settings.HandbrakeReleaseRate;
            handbrakeBlend = Mathf.MoveTowards(handbrakeBlend, handbrake ? 1f : 0f, handbrakeRate * delta);
            bool braking = Mathf.Abs(forwardSpeed) > 1.25f && forwardSpeed * throttle < 0f;

            GroundedWheelCount = 0;
            for (int index = 0; index < wheels.Length; index++)
                StepWheel(index, braking, delta);

            ApplyAntiRoll(0, 1);
            ApplyAntiRoll(2, 3);
        }

        private void StepWheel(int index, bool braking, float delta)
        {
            WheelMount wheel = wheels[index];
            bool isFront = index < 2;
            float angle = isFront ? steerAngle : 0f;
            float reach = settings.RestLength + settings.Extension;
            Vector3 origin = transform.TransformPoint(wheel.Anchor);
            if (!FindGround(origin, reach + settings.WheelRadius, out RaycastHit hit))
            {
                contacts[index] = default;
                compression[index] = 0f;
                DrawWheel(wheel, index, reach, angle, 0f, delta);
                return;
            }

            GroundedWheelCount++;
            float length = Mathf.Max(0.02f, hit.distance - settings.WheelRadius);
            float jounce = settings.RestLength - length;
            compression[index] = Mathf.Max(0f, jounce);
            float suspensionVelocity = Vector3.Dot(Body.GetPointVelocity(origin), transform.up);
            float springLoad = Mathf.Clamp(jounce * settings.Spring - suspensionVelocity * settings.Damper,
                0f, settings.Mass * Physics.gravity.magnitude * 0.95f);
            Body.AddForceAtPosition(transform.up * springLoad, hit.point);

            var surface = SurfaceZone.Resolve(hit.collider, hit.point, out float surfaceHeight);
            Vector3 steeredForward = Quaternion.AngleAxis(angle, transform.up) * transform.forward;
            Vector3 wheelForward = Vector3.ProjectOnPlane(steeredForward, hit.normal).normalized;
            Vector3 wheelRight = Vector3.Cross(hit.normal, wheelForward);
            Vector3 velocity = Vector3.ProjectOnPlane(Body.GetPointVelocity(hit.point), hit.normal);
            float longitudinalSpeed = Vector3.Dot(velocity, wheelForward);
            float lateralSpeed = Vector3.Dot(velocity, wheelRight);
            float rearGrip = isFront ? 1f : Mathf.Lerp(1f, settings.HandbrakeRearGrip, handbrakeBlend);
            float surfaceGrip = surface != null ? surface.Grip : 1f;
            float resistance = surface != null ? surface.RollingResistance : 0.018f;
            float driveScale = surface != null ? surface.DriveScale : 1f;
            float traction = springLoad * settings.TireGrip * surfaceGrip;

            float axleBias = isFront ? settings.FrontDriveBias : 1f - settings.FrontDriveBias;
            float speedLimit = throttle >= 0f ? settings.TopSpeed : settings.ReverseSpeed;
            float engineScale = 1f - Mathf.InverseLerp(speedLimit * 0.77f, speedLimit, longitudinalSpeed * Mathf.Sign(throttle));
            float engineForce = braking ? 0f : throttle * settings.DriveForce * axleBias * 0.5f * engineScale * driveScale;
            if (!isFront)
                engineForce *= Mathf.Lerp(1f, 0.26f, handbrakeBlend);

            float brakeDemand = braking ? Mathf.Abs(throttle) * settings.BrakeForce * (isFront ? 0.29f : 0.21f) : 0f;
            if (!isFront)
                brakeDemand += settings.HandbrakeForce * handbrakeBlend * 0.5f;

            float stoppingForce = Mathf.Abs(longitudinalSpeed) * settings.Mass / (4f * delta);
            float rollingForce = springLoad * resistance * Mathf.Clamp01(Mathf.Abs(longitudinalSpeed) * 2f);
            float longitudinal = engineForce - Mathf.Sign(longitudinalSpeed) * Mathf.Min(stoppingForce, brakeDemand + rollingForce);
            float lateral = -lateralSpeed * settings.LateralStiffness * surfaceGrip;
            lateral = Mathf.Clamp(lateral, -traction * rearGrip, traction * rearGrip);
            Vector2 tireForce = Vector2.ClampMagnitude(new Vector2(longitudinal, lateral), traction);
            Body.AddForceAtPosition(wheelForward * tireForce.x + wheelRight * tireForce.y, hit.point);

            float brakeSlip = Mathf.InverseLerp(traction * 0.65f, traction * 1.4f, brakeDemand) * Mathf.Abs(longitudinalSpeed);
            float driveSlip = Mathf.Clamp01((Mathf.Abs(engineForce) - traction) / Mathf.Max(traction, 1f)) * 2f;
            contacts[index] = new WheelContact
            {
                Grounded = true,
                Point = hit.point,
                Normal = hit.normal,
                Forward = wheelForward,
                Velocity = velocity,
                Slip = Mathf.Sqrt(lateralSpeed * lateralSpeed + brakeSlip * brakeSlip) + driveSlip,
                Surface = surface,
                SurfaceHeight = surfaceHeight
            };

            DrawWheel(wheel, index, length, angle, longitudinalSpeed * (1f - 0.86f * handbrakeBlend * (isFront ? 0f : 1f)), delta);
        }

        private bool FindGround(Vector3 origin, float reach, out RaycastHit ground)
        {
            ground = default;
            float closest = reach;
            int count = Physics.RaycastNonAlloc(origin, -transform.up, groundHits, reach, groundLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = groundHits[index];
                if (candidate.rigidbody != null || candidate.distance >= closest ||
                    Vector3.Dot(candidate.normal, transform.up) < 0.35f)
                    continue;

                closest = candidate.distance;
                ground = candidate;
            }

            return ground.collider != null;
        }

        private void ApplyAntiRoll(int left, int right)
        {
            float balance = (compression[left] - compression[right]) * settings.AntiRoll;
            if (contacts[left].Grounded)
                Body.AddForceAtPosition(transform.up * balance, contacts[left].Point);
            if (contacts[right].Grounded)
                Body.AddForceAtPosition(-transform.up * balance, contacts[right].Point);
        }

        private void DrawWheel(WheelMount wheel, int index, float length, float angle, float speed, float delta)
        {
            float turn = speed / settings.WheelRadius * Mathf.Rad2Deg * delta;
            wheelRotation[index] = Mathf.Repeat(wheelRotation[index] + turn, 360f);
            wheel.Knuckle.localPosition = wheel.Anchor - Vector3.up * length;
            wheel.Knuckle.localRotation = Quaternion.Euler(0f, angle, 0f);
            wheel.Hub.localRotation = Quaternion.Euler(wheelRotation[index], 0f, 0f);
        }
    }
}
