using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FourCorners.Tests
{
    public sealed class ArcadeAirTests
    {
        private SalvageEncounter encounter;

        [UnitySetUp]
        public IEnumerator OpenYard()
        {
            yield return SceneManager.LoadSceneAsync("FourCornersSalvageYard");
            encounter = Object.FindAnyObjectByType<SalvageEncounter>();
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Truck.GetComponent<TruckDriver>().enabled = false;
            foreach (CombatVehicle opponent in encounter.Opponents)
                if (opponent.TryGetComponent(out RivalDriver rival))
                    rival.enabled = false;
            encounter.Player.DamageEnabled = false;
            encounter.Truck.DamageEnabled = false;
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator BothAuthoredRampsLaunchTheCarAndAllowAWheelsDownLanding()
        {
            ArcadeVehicle car = encounter.Player.Vehicle;
            Assert.That(encounter.Arena.Ramps.Length, Is.EqualTo(2));
            foreach (Transform ramp in encounter.Arena.Ramps)
            {
                Transform toe = ramp.Find("Toe");
                Transform crest = ramp.Find("Crest");
                Assert.That(toe, Is.Not.Null, ramp.name);
                Assert.That(crest, Is.Not.Null, ramp.name);

                Vector3 forward = ramp.forward;
                Vector3 approach = toe.position - forward * 6.2f + Vector3.up * 0.72f;
                Place(car.Body, approach, ramp.rotation, forward * 17.2f);
                car.SetInput(0.65f, 0, false);
                float peak = approach.y;
                float airborneFor = 0;
                bool landed = false;
                for (int step = 0; step < 155; step++)
                {
                    yield return new WaitForFixedUpdate();
                    if (Vector3.Dot(car.Body.position - crest.position, forward) < -2)
                        continue;

                    peak = Mathf.Max(peak, car.Body.position.y);
                    if (car.GroundedWheelCount == 0)
                        airborneFor += Time.fixedDeltaTime;
                    else if (airborneFor >= 0.06f && car.GroundedWheelCount >= 3)
                    {
                        landed = true;
                        break;
                    }
                }

                Debug.Log($"{ramp.name}: {airborneFor:F2}s airborne; chassis gained {peak - approach.y:F2}m.");
                Assert.That(airborneFor, Is.InRange(0.06f, 1.35f), ramp.name);
                Assert.That(peak - approach.y, Is.GreaterThan(0.42f), ramp.name);
                Assert.That(landed, Is.True, $"{ramp.name} needs a reachable landing.");
                Assert.That(Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.81f), ramp.name);
            }
        }

        [UnityTest]
        public IEnumerator AirAssistLevelsAStumblingCarAndShortensTheDrop()
        {
            ArcadeVehicle car = encounter.Player.Vehicle;
            Quaternion attitude = Quaternion.Euler(28f, 90f, 37f);
            Place(car.Body, new Vector3(-3, 4.4f, -31), attitude, Vector3.right * 2);
            yield return WaitFixed(2);
            float verticalSpeed = car.Body.linearVelocity.y;
            float initialUp = Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up);
            Assert.That(car.IsAirborne, Is.True);

            const int sampleSteps = 10;
            yield return WaitFixed(sampleSteps);
            Assert.That(car.IsAirborne, Is.True);
            float dropAcceleration = (verticalSpeed - car.Body.linearVelocity.y) / (sampleSteps * Time.fixedDeltaTime);
            Assert.That(dropAcceleration, Is.GreaterThan(1.2f * Physics.gravity.magnitude));
            Assert.That(Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up), Is.GreaterThan(initialUp + 0.10f));
            Assert.That(car.RecoveryCount, Is.Zero, "Air leveling should work through torque without invoking recovery.");

            yield return WaitFixed(34);
            Assert.That(car.GroundedWheelCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.83f));
            Debug.Log($"Air assist measured {dropAcceleration / Physics.gravity.magnitude:F2}× scene gravity.");
        }

        [UnityTest]
        public IEnumerator BothLiveVehiclesRightThemselvesAfterAWaitAndTheWreckStaysDown()
        {
            ArcadeVehicle car = encounter.Player.Vehicle;
            ArcadeVehicle truck = encounter.Truck.Vehicle;
            Quaternion upsideDown = Quaternion.Euler(0, 90, 180);
            Vector3 carSpot = new(-15, 0.92f, -31);
            Vector3 truckSpot = new(7, 1.05f, -31);
            Place(car.Body, carSpot, upsideDown, Vector3.zero);
            Place(truck.Body, truckSpot, upsideDown, Vector3.zero);
            yield return WaitFixed(18);
            Assert.That(car.RecoveryCount, Is.Zero, "Recovery should give a real rollover time to settle.");
            Assert.That(truck.RecoveryCount, Is.Zero);

            for (int step = 0; step < 145 && (car.RecoveryCount == 0 || truck.RecoveryCount == 0); step++)
                yield return new WaitForFixedUpdate();

            Assert.That(car.RecoveryCount, Is.EqualTo(1));
            Assert.That(truck.RecoveryCount, Is.EqualTo(1));
            yield return WaitFixed(25);
            Assert.That(car.GroundedWheelCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(truck.GroundedWheelCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(Vector3.ProjectOnPlane(car.Body.position - carSpot, Vector3.up).magnitude, Is.LessThan(1.5f));

            encounter.Truck.DamageEnabled = true;
            encounter.Truck.TakeDamage(encounter.Truck.Armor);
            Place(truck.Body, truckSpot, upsideDown, Vector3.zero);
            yield return WaitFixed(110);
            Assert.That(truck.IsWreck, Is.True);
            Assert.That(truck.RecoveryCount, Is.EqualTo(1), "A wreck must never auto-right itself.");
        }

        private static IEnumerator WaitFixed(int count)
        {
            for (int step = 0; step < count; step++)
                yield return new WaitForFixedUpdate();
        }

        private static void Place(Rigidbody body, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            body.transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }
    }
}
