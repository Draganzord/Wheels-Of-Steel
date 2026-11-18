using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FourCorners.Tests
{
    public sealed class VehicleHandlingTests
    {
        private static readonly Vector3 CarStart = new(120f, 0.74f, -24f);
        private static readonly Vector3 TruckStart = new(210f, 0.88f, -24f);
        private static readonly WaitForFixedUpdate PhysicsTick = new();

        private ArcadeVehicle car;
        private ArcadeVehicle truck;

        [UnitySetUp]
        public IEnumerator OpenYardWithClearTestLanes()
        {
            yield return SceneManager.LoadSceneAsync("FourCornersSalvageYard", LoadSceneMode.Single);
            var encounter = Object.FindFirstObjectByType<SalvageEncounter>();
            Assert.That(encounter, Is.Not.Null);
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Truck.GetComponent<TruckDriver>().enabled = false;
            encounter.Player.DamageEnabled = false;
            encounter.Truck.DamageEnabled = false;
            car = encounter.Player.Vehicle;
            truck = encounter.Truck.Vehicle;

            SurfaceProfile asphalt = FindAsphalt();
            CreateLane(CarStart, asphalt);
            CreateLane(TruckStart, asphalt);
            Place(car, CarStart);
            Place(truck, TruckStart);
            for (int step = 0; step < 30; step++)
                yield return PhysicsTick;
            Assert.That(car.GroundedWheelCount, Is.EqualTo(4));
            Assert.That(truck.GroundedWheelCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator MuscleCarTurnsPromptlyAndLoadsItsOutsideSuspension()
        {
            BeginRoll(car, 11f);
            BeginRoll(truck, 11f);
            car.SetInput(0.22f, 0.75f, false);
            truck.SetInput(0.22f, 0.75f, false);
            float outwardRoll = 0f;
            float initialSteerAngle = 0f;
            for (int step = 0; step < 15; step++)
            {
                yield return PhysicsTick;
                outwardRoll = Mathf.Max(outwardRoll, AngleAboveGround(car.transform.right));
                if (step == 2)
                    initialSteerAngle = car.SteeringAngle;
            }

            float carYaw = HeadingChange(car);
            float truckYaw = HeadingChange(truck);
            Debug.Log($"Steering check: {initialSteerAngle:F1}° of wheel steering after 3 physics steps, car {carYaw:F1}° yaw and truck {truckYaw:F1}° yaw after 15, outside roll {outwardRoll:F1}°.");
            Assert.That(initialSteerAngle, Is.GreaterThan(14f), "The front wheels should turn quickly when steering begins.");
            Assert.That(carYaw, Is.GreaterThan(4f), "The car should visibly change heading within 15 physics steps.");
            Assert.That(carYaw, Is.GreaterThan(truckYaw + 0.25f), "The truck should turn in more slowly than the car at the same starting speed.");
            Assert.That(outwardRoll, Is.GreaterThan(0.35f), "As the car turns right, its body should lean toward the outside wheels.");
        }

        [UnityTest]
        public IEnumerator HandbrakeBuildsRearSlipAndReleaseCatchesItWhileMoving()
        {
            BeginRoll(car, 14f);
            car.SetInput(0.25f, 0f, false);
            for (int step = 0; step < 6; step++)
                yield return PhysicsTick;
            float straightSlip = RearLateralSpeed();

            car.SetInput(0.35f, 0.72f, true);
            yield return PhysicsTick;
            float initialBlend = car.HandbrakeBlend;
            float slideSlip = 0f;
            for (int step = 0; step < 20; step++)
            {
                yield return PhysicsTick;
                slideSlip = Mathf.Max(slideSlip, RearLateralSpeed());
            }

            Assert.That(initialBlend, Is.InRange(0.01f, 0.85f), "Pulling the handbrake should ramp in over several physics steps.");
            Assert.That(car.HandbrakeBlend, Is.GreaterThan(0.85f));
            Assert.That(slideSlip, Is.GreaterThan(straightSlip + 1.3f));

            car.SetInput(0.52f, 0f, false);
            float recoveredSlip = float.PositiveInfinity;
            for (int step = 0; step < 52; step++)
            {
                yield return PhysicsTick;
                if (step == 8)
                    Assert.That(car.HandbrakeBlend, Is.LessThan(0.15f), "The tires should regain grip promptly.");
                if (step > 8 && car.GroundedWheelCount == 4 && Vector3.Dot(car.Body.linearVelocity, car.transform.forward) > 4f)
                    recoveredSlip = Mathf.Min(recoveredSlip, RearLateralSpeed());
            }

            Debug.Log($"Rear slide check: {straightSlip:F1} m/s straight, {slideSlip:F1} m/s during handbrake, {recoveredSlip:F1} m/s after release while moving forward.");
            Assert.That(recoveredSlip, Is.LessThan(slideSlip * 0.58f), "The rear should catch the slide while the car is still driving forward.");
        }

        private float RearLateralSpeed()
        {
            float rearSlip = 0f;
            for (int index = 2; index < 4; index++)
            {
                WheelContact contact = car.Contacts[index];
                if (contact.Grounded)
                    rearSlip += Mathf.Abs(Vector3.Dot(contact.Velocity, Vector3.Cross(contact.Normal, contact.Forward)));
            }
            return rearSlip * 0.5f;
        }

        [UnityTest]
        public IEnumerator HardBrakingPitchesTheNoseDownAndSlowsTheCar()
        {
            float restingPitch = AngleAboveGround(car.transform.forward);
            BeginRoll(car, 16f);
            car.SetInput(-1f, 0f, false);
            float noseDive = 0f;
            for (int step = 0; step < 21; step++)
            {
                yield return PhysicsTick;
                noseDive = Mathf.Max(noseDive, restingPitch - AngleAboveGround(car.transform.forward));
            }

            float speed = car.Body.linearVelocity.z;
            Debug.Log($"Braking check after 21 physics steps: {noseDive:F1}° nose dive, 16.0 to {speed:F1} m/s.");
            Assert.That(noseDive, Is.GreaterThan(0.45f));
            Assert.That(speed, Is.LessThan(13f));
        }

        [UnityTest]
        public IEnumerator CarsGetUpToSpeedQuicklyFromRest()
        {
            car.SetInput(1f, 0f, false);
            truck.SetInput(1f, 0f, false);
            for (int step = 0; step < 50; step++)
                yield return PhysicsTick;

            Debug.Log($"One-second launch: muscle car {car.Speed:F1} m/s, truck {truck.Speed:F1} m/s.");
            Assert.That(car.Speed, Is.GreaterThan(10f));
            Assert.That(truck.Speed, Is.GreaterThan(8f));
            Assert.That(car.Speed, Is.GreaterThan(truck.Speed));
        }

        [UnityTest]
        public IEnumerator HoldingDriveAfterAWallImpactPeelsTheCarOff()
        {
            var wall = new GameObject("Crash test wall");
            wall.transform.position = CarStart + Vector3.forward * 5f;
            wall.AddComponent<BoxCollider>().size = new Vector3(16, 2, 0.45f);
            Physics.SyncTransforms();
            BeginRoll(car, 8f);
            car.SetInput(1f, 0f, false);
            bool hit = false;
            float clearance = 0;
            for (int step = 0; step < 100; step++)
            {
                yield return PhysicsTick;
                if (!hit && car.Body.position.z > CarStart.z + 1.8f && car.Speed < 1.5f)
                    hit = true;
                if (hit)
                    clearance = Mathf.Max(clearance, 2.65f - (car.Body.position.z - CarStart.z));
            }

            Assert.That(hit, Is.True);
            Assert.That(clearance, Is.GreaterThan(0.5f));
            Assert.That(Mathf.Abs(HeadingChange(car)), Is.GreaterThan(16f));
        }

        private static float HeadingChange(ArcadeVehicle vehicle)
        {
            Vector3 heading = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up);
            return Vector3.SignedAngle(Vector3.forward, heading, Vector3.up);
        }

        private static float AngleAboveGround(Vector3 axis) => Mathf.Asin(Mathf.Clamp(axis.y, -1f, 1f)) * Mathf.Rad2Deg;

        private static void BeginRoll(ArcadeVehicle vehicle, float speed)
        {
            vehicle.Body.linearVelocity = Vector3.forward * speed;
            vehicle.Body.WakeUp();
        }

        private static void Place(ArcadeVehicle vehicle, Vector3 position)
        {
            vehicle.SetInput(0f, 0f, false);
            Rigidbody body = vehicle.Body;
            body.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }

        private static SurfaceProfile FindAsphalt()
        {
            foreach (var zone in Object.FindObjectsByType<SurfaceZone>(FindObjectsSortMode.None))
            {
                SurfaceProfile surface = SurfaceZone.Resolve(zone.GetComponent<Collider>(), zone.transform.position, out _);
                if (surface != null && surface.Kind == SurfaceKind.Asphalt)
                    return surface;
            }

            Assert.Fail("The yard must have a configured asphalt surface.");
            return null;
        }

        private static void CreateLane(Vector3 center, SurfaceProfile asphalt)
        {
            var lane = new GameObject("Handling test lane");
            lane.transform.position = new Vector3(center.x, -0.5f, center.z + 18f);
            lane.AddComponent<BoxCollider>().size = new Vector3(68f, 1f, 100f);
            lane.AddComponent<SurfaceZone>().Configure(asphalt);
        }
    }
}
