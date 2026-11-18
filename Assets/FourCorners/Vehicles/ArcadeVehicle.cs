using System;
using UnityEngine;

namespace FourCorners
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
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
        private readonly Collider[] recoveryHits = new Collider[24];
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Vector3[] RecoveryOffsets =
        {
            Vector3.zero, Vector3.right * 1.35f, Vector3.left * 1.35f,
            Vector3.forward * 1.35f, Vector3.back * 1.35f
        };

        private BoxCollider hull;
        private Bounds recoveryBounds;
        private float throttle;
        private float steering;
        private float steerAngle;
        private float handbrakeBlend;
        private bool handbrake;
        private float lastChassisSupport = float.NegativeInfinity;
        private float nextRecoveryAttempt;
        private float lastBlockingContact = float.NegativeInfinity;
        private Vector3 blockingNormal;
        private float blockedTime;
        private float releaseUntil;
        private float releasedThrottle;

        public Rigidbody Body { get; private set; }
        public VehicleSettings Settings => settings;
        public bool IsWreck { get; private set; }
        public WheelContact[] Contacts => contacts;
        public int GroundedWheelCount { get; private set; }
        public float Speed => Body.linearVelocity.magnitude;
        public float SteeringAngle => steerAngle;
        public float HandbrakeBlend => handbrakeBlend;
        public bool IsAirborne { get; private set; }
        public float OverturnedTime { get; private set; }
        public int RecoveryCount { get; private set; }

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
            hull = GetComponent<BoxCollider>();
            recoveryBounds = new Bounds(hull.center, hull.size);
            foreach (Collider bumper in bumperColliders)
            {
                if (bumper is BoxCollider box)
                {
                    Vector3 center = transform.InverseTransformPoint(box.transform.TransformPoint(box.center));
                    recoveryBounds.Encapsulate(new Bounds(center, box.size));
                }
            }
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
            IsAirborne = false;
            OverturnedTime = 0f;
            foreach (Collider bumper in bumperColliders)
                bumper.enabled = false;
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

            if (Body.useGravity)
                Body.AddForce(Physics.gravity * (settings.GravityScale - 1f), ForceMode.Acceleration);

            float delta = Time.fixedDeltaTime;
            float forwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);
            float speedBlend = Mathf.InverseLerp(5f, settings.TopSpeed * 0.94f, Mathf.Abs(forwardSpeed));
            float steeringLimit = Mathf.Lerp(settings.LowSpeedSteer, settings.HighSpeedSteer, speedBlend);
            steerAngle = Mathf.MoveTowards(steerAngle, steering * steeringLimit, settings.SteerRate * delta);
            float handbrakeRate = handbrake ? settings.HandbrakeEngageRate : settings.HandbrakeReleaseRate;
            handbrakeBlend = Mathf.MoveTowards(handbrakeBlend, handbrake ? 1f : 0f, handbrakeRate * delta);
            float drive = Time.fixedTime < releaseUntil && throttle * releasedThrottle > 0f ? 0f : throttle;
            bool braking = Mathf.Abs(forwardSpeed) > 0.9f && forwardSpeed * drive < 0f;
            float launch = 1f - Mathf.InverseLerp(3f, 11f, Mathf.Abs(forwardSpeed));

            GroundedWheelCount = 0;
            for (int index = 0; index < wheels.Length; index++)
                StepWheel(index, drive, launch, braking, delta);

            ApplyAntiRoll(0, 1);
            ApplyAntiRoll(2, 3);
            UpdateAttitude(delta);
            ReleaseFromCollision(delta);
        }

        private void StepWheel(int index, float drive, float launch, bool braking, float delta)
        {
            WheelMount wheel = wheels[index];
            bool isFront = index < 2;
            float angle = isFront ? steerAngle : 0f;
            float reach = settings.RestLength + settings.Extension;
            Vector3 origin = transform.TransformPoint(wheel.Anchor);
            if (!FindGround(origin, transform.up, reach + settings.WheelRadius, out RaycastHit hit))
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
            float damping = suspensionVelocity < 0f ? settings.Damper : settings.ReboundDamper;
            float bumpStopForce = Mathf.Max(0f, 0.10f - length) * settings.Spring * 2f;
            float springLoad = Mathf.Clamp(jounce * settings.Spring - suspensionVelocity * damping + bumpStopForce,
                0f, settings.Mass * Physics.gravity.magnitude * settings.GravityScale);
            Body.AddForceAtPosition(hit.normal * springLoad, hit.point);

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

            float frontBias = Mathf.Lerp(settings.FrontDriveBias, Mathf.Max(0.38f, settings.FrontDriveBias), launch * (1f - handbrakeBlend));
            float axleBias = isFront ? frontBias : 1f - frontBias;
            float speedLimit = drive >= 0f ? settings.TopSpeed : settings.ReverseSpeed;
            float engineScale = 1f - Mathf.InverseLerp(speedLimit * 0.77f, speedLimit, longitudinalSpeed * Mathf.Sign(drive));
            float engineForce = braking ? 0f : drive * settings.DriveForce * axleBias * 0.5f * engineScale * driveScale;
            float launchLimit = traction * 0.96f;
            engineForce = Mathf.Lerp(engineForce, Mathf.Clamp(engineForce, -launchLimit, launchLimit), launch * (1f - handbrakeBlend));
            if (!isFront)
                engineForce *= Mathf.Lerp(1f, 0.26f, handbrakeBlend);

            float brakeDemand = braking ? Mathf.Abs(drive) * settings.BrakeForce * (isFront ? 0.29f : 0.21f) : 0f;
            if (!isFront)
                brakeDemand += settings.HandbrakeForce * handbrakeBlend * 0.5f;

            float stoppingForce = Mathf.Abs(longitudinalSpeed) * settings.Mass / (4f * delta);
            float rollingForce = springLoad * resistance * Mathf.Clamp01(Mathf.Abs(longitudinalSpeed) * 2f);
            float longitudinal = engineForce - Mathf.Sign(longitudinalSpeed) * Mathf.Min(stoppingForce, brakeDemand + rollingForce);
            float lateral = -lateralSpeed * settings.LateralStiffness * surfaceGrip;
            float demand = new Vector2(longitudinal, lateral / rearGrip).magnitude;
            float saturation = Mathf.InverseLerp(traction, traction * 2.35f, demand);
            float availableGrip = Mathf.SmoothStep(traction, traction * settings.SlidingGrip, saturation);
            float forceScale = Mathf.Min(1f, availableGrip / Mathf.Max(demand, 0.01f));
            Vector2 tireForce = new Vector2(longitudinal, lateral) * forceScale;
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

        private bool FindGround(Vector3 origin, Vector3 up, float reach, out RaycastHit ground)
        {
            ground = default;
            float closest = reach;
            int count = Physics.RaycastNonAlloc(origin, -up, groundHits, reach, groundLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = groundHits[index];
                if (candidate.rigidbody != null || candidate.distance >= closest ||
                    Vector3.Dot(candidate.normal, up) < 0.35f)
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
                Body.AddForceAtPosition(contacts[left].Normal * balance, contacts[left].Point);
            if (contacts[right].Grounded)
                Body.AddForceAtPosition(-contacts[right].Normal * balance, contacts[right].Point);
        }

        private void OnCollisionEnter(Collision collision) => TrackContacts(collision);

        private void OnCollisionStay(Collision collision) => TrackContacts(collision);

        private void TrackContacts(Collision collision)
        {
            if (IsWreck)
                return;

            for (int index = 0; index < collision.contactCount; index++)
            {
                Vector3 normal = collision.GetContact(index).normal;
                if (collision.rigidbody == null && normal.y >= 0.4f)
                    lastChassisSupport = Time.fixedTime;
                if (Mathf.Abs(normal.y) > 0.45f || Mathf.Abs(throttle) < 0.4f
                    || Vector3.Dot(normal, transform.forward) * Mathf.Sign(throttle) > -0.30f)
                    continue;

                if (lastBlockingContact < Time.fixedTime)
                    blockingNormal = Vector3.zero;
                lastBlockingContact = Time.fixedTime;
                blockingNormal += Vector3.ProjectOnPlane(normal, Vector3.up);
            }
        }

        private void ReleaseFromCollision(float delta)
        {
            bool pinned = Time.fixedTime >= releaseUntil && Time.fixedTime - lastBlockingContact < 0.065f
                && GroundedWheelCount >= 2 && transform.up.y > 0.6f && !handbrake
                && Mathf.Abs(throttle) >= 0.4f && Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up).sqrMagnitude < 2.25f
                && Vector3.Dot(blockingNormal.normalized, transform.forward) * Mathf.Sign(throttle) < -0.30f;
            blockedTime = pinned ? blockedTime + delta : 0f;
            if (blockedTime < 0.30f)
                return;

            Vector3 away = blockingNormal.normalized;
            float side = Mathf.Abs(steering) > 0.15f
                ? Mathf.Sign(steering * throttle) : Vector3.Dot(away, transform.right) * throttle < -0.05f ? -1f : 1f;
            Body.AddForce((away + Vector3.ProjectOnPlane(transform.right, Vector3.up) * side * 0.25f) * 2.6f,
                ForceMode.VelocityChange);
            Body.AddTorque(Vector3.up * side * 1.9f, ForceMode.VelocityChange);
            releasedThrottle = throttle;
            releaseUntil = Time.fixedTime + 0.27f;
            blockedTime = 0f;
        }

        private void UpdateAttitude(float delta)
        {
            Vector3 up = Body.rotation * Vector3.up;
            IsAirborne = GroundedWheelCount == 0 && Time.fixedTime - lastChassisSupport > 0.07f;
            if (IsAirborne)
            {
                Quaternion correction = Quaternion.FromToRotation(up, Vector3.up);
                correction.ToAngleAxis(out float angle, out Vector3 axis);
                Vector3 tiltVelocity = Vector3.ProjectOnPlane(Body.angularVelocity, Vector3.up);
                Vector3 torque = axis * (angle * Mathf.Deg2Rad * settings.AirLevelStrength)
                    - tiltVelocity * settings.AirLevelDamping;
                Body.AddTorque(Vector3.ClampMagnitude(torque, 35f), ForceMode.Acceleration);
            }

            bool stranded = !IsAirborne && up.y < 0.18f && Body.linearVelocity.sqrMagnitude < 12.25f
                && Body.angularVelocity.sqrMagnitude < 2.56f;
            OverturnedTime = stranded ? OverturnedTime + delta : 0f;
            if (OverturnedTime < settings.RecoveryDelay || Time.fixedTime < nextRecoveryAttempt)
                return;

            nextRecoveryAttempt = Time.fixedTime + 0.2f;
            TryRecover();
        }

        private void TryRecover()
        {
            Vector3 heading = Vector3.ProjectOnPlane(Body.rotation * Vector3.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.04f)
                heading = Vector3.Cross(Body.rotation * Vector3.right, Vector3.up);
            Quaternion upright = Quaternion.LookRotation(heading, Vector3.up);

            foreach (Vector3 offset in RecoveryOffsets)
            {
                Vector3 position = Body.position + upright * offset;
                if (!TrySetRecoveryHeight(ref position, upright) || !HasRecoveryClearance(position, upright))
                    continue;

                Body.position = position;
                Body.rotation = upright;
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                Body.WakeUp();
                Array.Clear(contacts, 0, contacts.Length);
                Array.Clear(compression, 0, compression.Length);
                GroundedWheelCount = 0;
                lastChassisSupport = float.NegativeInfinity;
                OverturnedTime = 0f;
                RecoveryCount++;
                return;
            }
        }

        private bool TrySetRecoveryHeight(ref Vector3 position, Quaternion upright)
        {
            float highestGround = float.NegativeInfinity;
            float lowestGround = float.PositiveInfinity;
            foreach (WheelMount wheel in wheels)
            {
                Vector3 footprint = new Vector3(wheel.Anchor.x, 0f, wheel.Anchor.z);
                Vector3 probe = position + upright * footprint + Vector3.up * 0.85f;
                if (!FindGround(probe, Vector3.up, 2.8f, out RaycastHit ground)
                    || ground.normal.y < 0.72f || ground.point.y > Body.position.y + 0.25f)
                    return false;

                highestGround = Mathf.Max(highestGround, ground.point.y);
                lowestGround = Mathf.Min(lowestGround, ground.point.y);
            }

            if (highestGround - lowestGround > 0.45f)
                return false;

            float load = settings.Mass * Physics.gravity.magnitude * settings.GravityScale / wheels.Length;
            float compressionAtRest = Mathf.Min(load / settings.Spring, settings.RestLength * 0.68f);
            position.y = highestGround + settings.RestLength + settings.WheelRadius
                - compressionAtRest - wheels[0].Anchor.y + 0.06f;
            return true;
        }

        private bool HasRecoveryClearance(Vector3 position, Quaternion upright)
        {
            Vector3 center = position + upright * recoveryBounds.center;
            Vector3 padding = new(0.15f, 0.04f, 0.08f);
            int count = Physics.OverlapBoxNonAlloc(center, recoveryBounds.extents + padding, recoveryHits,
                upright, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                if (recoveryHits[index].attachedRigidbody != Body)
                    return false;
            }

            return count < recoveryHits.Length;
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
