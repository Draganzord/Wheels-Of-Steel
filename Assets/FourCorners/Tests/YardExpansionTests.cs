using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FourCorners.Tests
{
    public sealed class YardExpansionTests
    {
        private SalvageEncounter encounter;

        [UnitySetUp]
        public IEnumerator OpenYard()
        {
            yield return SceneManager.LoadSceneAsync("FourCornersSalvageYard");
            encounter = Object.FindAnyObjectByType<SalvageEncounter>();
            Assert.That(encounter.OpponentsRemaining, Is.EqualTo(3));
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Truck.GetComponent<TruckDriver>().enabled = false;
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator RocketCreatesACollisionCraterAndRestartRestoresTheRoad()
        {
            foreach (CombatVehicle opponent in encounter.Opponents)
                if (opponent.TryGetComponent(out RivalDriver rival))
                    rival.enabled = false;

            DestructibleTerrain terrain = encounter.Arena.GetComponent<DestructibleTerrain>();
            Vector3 aim = new(-10, 0, -31);
            Vector3 nextLane = new(-19, 0, -33);
            Place(encounter.Player.Vehicle, new Vector3(-27, 0.73f, -31), Vector3.right);
            float original = GroundAt(aim).point.y;
            float neighbor = GroundAt(nextLane).point.y;
            Assert.That(encounter.TryLaunchRocket(aim), Is.True);
            Assert.That(encounter.Rockets, Is.EqualTo(4));
            Assert.That(encounter.TryLaunchRocket(aim), Is.False, "Rocket cooldown must apply to the public firing entry point.");

            for (int step = 0; step < 75 && terrain.CraterCount == 0; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(terrain.CraterCount, Is.EqualTo(1), "The rocket should detonate once at the selected ground point.");
            RaycastHit crater = GroundAt(aim);
            float depth = original - crater.point.y;
            Assert.That(depth, Is.GreaterThan(0.4f), "Wheels and other raycasts need to hit the lowered roadbed.");
            Assert.That(SurfaceZone.Resolve(crater.collider, crater.point, out _).Kind, Is.EqualTo(SurfaceKind.Asphalt));
            Assert.That(GroundAt(nextLane).point.y, Is.EqualTo(neighbor).Within(0.06f));
            Debug.Log($"Rocket crater measured {depth:F2} m below the original road surface.");

            SalvageEncounter old = encounter;
            old.RequestRestart();
            yield return new WaitUntil(() => old == null);
            encounter = Object.FindAnyObjectByType<SalvageEncounter>();
            Assert.That(encounter.Rockets, Is.EqualTo(5));
            Assert.That(encounter.Arena.GetComponent<DestructibleTerrain>().CraterCount, Is.Zero);
            Assert.That(GroundAt(aim).point.y, Is.EqualTo(original).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator PowderDrumsChainAndABlastOpensTheBankGate()
        {
            BlastBarrel first = GameObject.Find("Powder Drum 1").GetComponent<BlastBarrel>();
            BlastBarrel second = GameObject.Find("Powder Drum 2").GetComponent<BlastBarrel>();
            BlastBarrel third = GameObject.Find("Powder Drum 3").GetComponent<BlastBarrel>();
            first.TakeDamage(100f);
            Assert.That(first.HasExploded && second.HasExploded && third.HasExploded, Is.True);
            Assert.That(first.gameObject.activeSelf || second.gameObject.activeSelf || third.gameObject.activeSelf, Is.False);

            BreakableFence fence = encounter.Arena.Fence;
            Assert.That(fence.IsBroken, Is.False);
            encounter.Detonate(fence.StandingBlocker.bounds.center - fence.transform.forward * 0.8f, 6, 66, 6800);
            Assert.That(fence.IsBroken, Is.True);
            Assert.That(fence.StandingBlocker.enabled, Is.False);
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator RivetFiresFromRangeAndJackalJoinsThePursuit()
        {
            RivalDriver rivet = encounter.Opponents[1].GetComponent<RivalDriver>();
            RivalDriver jackal = encounter.Opponents[2].GetComponent<RivalDriver>();
            Assert.That(rivet.Kind, Is.EqualTo(RivalKind.Gunner));
            Assert.That(jackal.Kind, Is.EqualTo(RivalKind.Interceptor));
            Place(encounter.Player.Vehicle, new Vector3(-1, 0.72f, -31), Vector3.right);
            Place(encounter.Opponents[1].Vehicle, new Vector3(-17, 0.72f, -31), Vector3.right);
            Vector3 interceptStart = new(17, 0.76f, -31);
            Place(encounter.Opponents[2].Vehicle, interceptStart, Vector3.left);

            for (int step = 0; step < 133 && encounter.Player.ArmorFraction >= 1; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(rivet.IsEngaged, Is.True);
            Assert.That(encounter.Player.ArmorFraction, Is.LessThan(1), "Rivet should land ranged damage before Jackal deploys.");
            Assert.That(jackal.IsEngaged, Is.False);
            encounter.Player.DamageEnabled = false;
            for (int step = 0; step < 180 && Vector3.Distance(interceptStart, jackal.transform.position) < 4f; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(jackal.IsEngaged, Is.True);
            Assert.That(Vector3.Distance(interceptStart, jackal.transform.position), Is.GreaterThanOrEqualTo(4f));
        }

        private static RaycastHit GroundAt(Vector3 sample)
        {
            Assert.That(Physics.Raycast(sample + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6, ~0,
                QueryTriggerInteraction.Ignore), Is.True);
            return hit;
        }

        private static void Place(ArcadeVehicle vehicle, Vector3 position, Vector3 heading)
        {
            Rigidbody body = vehicle.Body;
            Quaternion rotation = Quaternion.LookRotation(heading);
            body.transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }
    }
}
