using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FourCorners.Tests
{
    public sealed class SurfacePlayModeTests
    {
        private SalvageEncounter encounter;
        private ArcadeVehicle car;
        private VehicleSurfaceEffects effects;
        private float originalTimeScale;

        [UnitySetUp]
        public IEnumerator LoadYard()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 4f;
            yield return SceneManager.LoadSceneAsync("FourCornersSalvageYard", LoadSceneMode.Single);
            yield return null;

            encounter = Object.FindFirstObjectByType<SalvageEncounter>();
            Assert.That(encounter, Is.Not.Null);
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Truck.GetComponent<TruckDriver>().enabled = false;
            encounter.Player.DamageEnabled = false;
            encounter.Truck.DamageEnabled = false;
            car = encounter.Player.Vehicle;
            effects = car.GetComponent<VehicleSurfaceEffects>();
            Assert.That(effects, Is.Not.Null);
        }

        [TearDown]
        public void ResetClock() => Time.timeScale = originalTimeScale;

        [UnityTest]
        public IEnumerator BrakingAndSlidesDifferBetweenAsphaltAndMud()
        {
            Vector3 asphalt = new(120f, 0.71f, -30f);
            Vector3 mud = new(205f, 0.71f, -30f);
            MakeTestPad(asphalt, Profile(SurfaceKind.Asphalt), new Vector3(64f, 1f, 110f));
            MakeTestPad(mud, Profile(SurfaceKind.Mud), new Vector3(64f, 1f, 110f));

            float asphaltStop = 0f;
            float mudStop = 0f;
            float asphaltSlide = 0f;
            float mudSlide = 0f;
            int baseline = effects.ActiveTrackCount;
            yield return MeasureBrake(asphalt, distance => asphaltStop = distance);
            yield return MeasureSlide(asphalt, angle => asphaltSlide = angle);
            int asphaltMarks = effects.ActiveTrackCount - baseline;
            baseline = effects.ActiveTrackCount;
            yield return MeasureBrake(mud, distance => mudStop = distance);
            yield return MeasureSlide(mud, angle => mudSlide = angle);
            int mudMarks = effects.ActiveTrackCount - baseline;

            Debug.Log($"Surface check: asphalt stop {asphaltStop:F2}m, slide {asphaltSlide:F1}°, {asphaltMarks} marks. " +
                $"Mud stop {mudStop:F2}m, slide {mudSlide:F1}°, {mudMarks} marks.");
            Assert.That(mudStop, Is.GreaterThan(asphaltStop + 0.8f));
            Assert.That(Mathf.Abs(mudSlide - asphaltSlide), Is.GreaterThan(1f));
            Assert.That(asphaltMarks, Is.GreaterThan(0));
            Assert.That(mudMarks, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator CreekWheelsStayIndependentAndOnlyMovingWheelsLeaveEvidence()
        {
            Vector3 creek = encounter.Arena.Shortcut[4].position;
            Vector3 exit = encounter.Arena.Shortcut[5].position;
            Vector3 heading = Vector3.ProjectOnPlane(exit - creek, Vector3.up).normalized;
            Quaternion rotation = Quaternion.LookRotation(heading);
            Warp(creek + Vector3.up * 0.75f, rotation);
            yield return FixedSteps(18);
            car.Body.linearVelocity = heading * 8f;
            car.SetInput(0.16f, 0f, false);

            int peakSpray = 0;
            bool split = false;
            for (int step = 0; step < 60; step++)
            {
                yield return new WaitForFixedUpdate();
                peakSpray = Mathf.Max(peakSpray, effects.ActiveParticleCount);
                WheelContact[] contacts = car.Contacts;
                split = contacts[0].Surface?.Kind == SurfaceKind.Sand && contacts[1].Surface?.Kind == SurfaceKind.Sand &&
                    contacts[2].Surface?.Kind == SurfaceKind.Water && contacts[3].Surface?.Kind == SurfaceKind.Water;
                if (split)
                    break;
            }

            Assert.That(split, Is.True, "The front wheels should reach sand while the rear wheels are still in water.");
            Assert.That(effects.ActiveWetContacts, Is.EqualTo(2));
            Assert.That(peakSpray, Is.GreaterThan(0));
            RigidbodyConstraints constraints = car.Body.constraints;
            car.SetInput(0f, 0f, false);
            car.Body.linearVelocity = Vector3.zero;
            car.Body.angularVelocity = Vector3.zero;
            car.Body.constraints = RigidbodyConstraints.FreezeAll;
            yield return FixedSteps(4);
            int parkedMarks = effects.ActiveTrackCount;
            yield return FixedSteps(70);
            Assert.That(effects.ActiveTrackCount, Is.EqualTo(parkedMarks));
            Assert.That(effects.ActiveParticleCount, Is.Zero);
            Assert.That(effects.ActiveWetContacts, Is.EqualTo(2));

            car.Body.constraints = constraints;
            car.SetInput(0.25f, 0f, false);
            car.Body.linearVelocity = heading * 5f;
            int previousWetPrints = effects.WetSandPrintsPlaced;
            for (int step = 0; step < 35 && effects.WetSandPrintsPlaced == previousWetPrints; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(effects.WetSandPrintsPlaced, Is.GreaterThan(previousWetPrints));
        }

        [UnityTest]
        public IEnumerator SustainedWheelEvidenceReusesItsGeometryAndEmitterBudgets()
        {
            var closeMarks = Object.Instantiate(Profile(SurfaceKind.Sand));
            closeMarks.MarkSpacing = 0.1f;
            Vector3 start = new(120f, 0.71f, -92f);
            MakeTestPad(new Vector3(start.x, start.y, 18f), closeMarks, new Vector3(28f, 1f, 248f));
            Warp(start, Quaternion.identity);
            yield return FixedSteps(20);

            int maxParticles = 0;
            int fullSteps = 0;
            for (int step = 0; step < 480; step++)
            {
                // Repeatable simulated travel fills the ring buffer inside the test lane.
                car.Body.linearVelocity = Vector3.forward * 23f + Vector3.up * car.Body.linearVelocity.y;
                yield return new WaitForFixedUpdate();
                maxParticles = Mathf.Max(maxParticles, effects.ActiveParticleCount);
                Assert.That(effects.ActiveTrackCount, Is.LessThanOrEqualTo(VehicleSurfaceEffects.TrackCapacity));
                if (effects.ActiveTrackCount == VehicleSurfaceEffects.TrackCapacity)
                    fullSteps++;
                if (fullSteps >= 20)
                    break;
            }

            Object.Destroy(closeMarks);
            Assert.That(fullSteps, Is.EqualTo(20), "Sustained travel should reuse full track geometry without growth.");
            Assert.That(maxParticles, Is.GreaterThan(0));
            Assert.That(maxParticles, Is.LessThanOrEqualTo(VehicleSurfaceEffects.ParticleCapacity));
            Debug.Log($"Evidence budget: {effects.ActiveTrackCount} track quads, peak {maxParticles} active particles.");
        }

        private IEnumerator MeasureBrake(Vector3 start, System.Action<float> report)
        {
            Warp(start, Quaternion.identity);
            yield return FixedSteps(20);
            Vector3 initialPosition = car.Body.position;
            car.Body.linearVelocity = Vector3.forward * 16f;
            car.SetInput(-1f, 0.26f, false);
            for (int step = 0; step < 175; step++)
            {
                yield return new WaitForFixedUpdate();
                if (Vector3.Dot(car.Body.linearVelocity, car.transform.forward) <= 1.75f)
                    break;
            }
            car.SetInput(0f, 0f, false);
            report(Vector3.ProjectOnPlane(car.Body.position - initialPosition, Vector3.up).magnitude);
        }

        private IEnumerator MeasureSlide(Vector3 start, System.Action<float> report)
        {
            Warp(start, Quaternion.identity);
            yield return FixedSteps(24);
            car.Body.linearVelocity = Vector3.forward * 14f;
            car.SetInput(0.7f, 0.74f, true);
            float measured = 0f;
            for (int step = 0; step < 55; step++)
            {
                yield return new WaitForFixedUpdate();
                Vector3 planar = Vector3.ProjectOnPlane(car.Body.linearVelocity, Vector3.up);
                if (planar.sqrMagnitude > 16f)
                    measured = Mathf.Max(measured, Vector3.Angle(planar, car.transform.forward));
            }
            report(measured);
        }

        private void Warp(Vector3 position, Quaternion rotation)
        {
            car.SetInput(0f, 0f, false);
            car.Body.position = position;
            car.Body.rotation = rotation;
            car.Body.linearVelocity = Vector3.zero;
            car.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static IEnumerator FixedSteps(int count)
        {
            var tick = new WaitForFixedUpdate();
            for (int i = 0; i < count; i++)
                yield return tick;
        }

        private static SurfaceProfile Profile(SurfaceKind kind)
        {
            SurfaceProfile profile = Object.FindObjectsByType<SurfaceZone>(FindObjectsSortMode.None)
                .Select(zone => SurfaceZone.Resolve(zone.GetComponent<Collider>(), zone.transform.position, out _))
                .FirstOrDefault(profile => profile != null && profile.Kind == kind);
            Assert.That(profile, Is.Not.Null, $"Expected a scene surface profile for {kind}.");
            return profile;
        }

        private static void MakeTestPad(Vector3 center, SurfaceProfile profile, Vector3 size)
        {
            var pad = new GameObject($"Controlled {profile.Kind} Test Lane");
            pad.transform.position = new Vector3(center.x, -0.5f, center.z);
            pad.AddComponent<BoxCollider>().size = size;
            pad.AddComponent<SurfaceZone>().Configure(profile);
        }
    }
}
