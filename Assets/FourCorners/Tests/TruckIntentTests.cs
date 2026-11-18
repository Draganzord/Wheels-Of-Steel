using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FourCorners.Tests
{
    public sealed class TruckIntentTests
    {
        [UnityTest]
        public IEnumerator TruckTelegraphsThenOvershootsADodgeAndRecovers()
        {
            yield return SceneManager.LoadSceneAsync("FourCornersSalvageYard");
            var encounter = Object.FindAnyObjectByType<SalvageEncounter>();
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Player.DamageEnabled = false;
            encounter.Truck.DamageEnabled = false;

            const float Lane = -31f;
            const float InitialTarget = -4f;
            Place(encounter.Player.Vehicle.Body, new Vector3(InitialTarget, 0.73f, Lane), 0f);
            Rigidbody truck = encounter.Truck.Vehicle.Body;
            Place(truck, new Vector3(-19f, 0.86f, Lane), 10.5f);
            TruckDriver driver = encounter.Truck.GetComponent<TruckDriver>();
            var step = new WaitForFixedUpdate();
            for (int tick = 0; tick < 25 && driver.Intent != TruckIntent.LiningUp; tick++)
                yield return step;
            Assert.That(driver.Intent, Is.EqualTo(TruckIntent.LiningUp));

            int warningSteps = 0;
            while (driver.Intent == TruckIntent.LiningUp && warningSteps < 80)
            {
                warningSteps++;
                yield return step;
            }
            Assert.That(warningSteps, Is.GreaterThanOrEqualTo(10), "The warning must last long enough to see.");
            Assert.That(driver.Intent, Is.EqualTo(TruckIntent.Ramming));

            Place(encounter.Player.Vehicle.Body, new Vector3(InitialTarget - 3f, 0.73f, Lane + 7f), 0f);
            float start = truck.position.x;
            for (int tick = 0; tick < 65 && driver.Intent == TruckIntent.Ramming; tick++)
                yield return step;
            Assert.That(driver.Intent, Is.EqualTo(TruckIntent.Recovering));
            Assert.That(truck.position.x, Is.GreaterThan(start + 6f), "The ram should carry the truck past the dodge.");
            Assert.That(truck.position.z, Is.EqualTo(Lane).Within(1.6f), "The truck must hold its line through the miss.");

            for (int tick = 0; tick < 150 && driver.Intent == TruckIntent.Recovering; tick++)
                yield return step;
            Assert.That(driver.Intent, Is.EqualTo(TruckIntent.Pursuing));
            Assert.That(driver.IsChasing, Is.True);

            encounter.Truck.DamageEnabled = true;
            encounter.Truck.TakeDamage(encounter.Truck.Armor);
            Assert.That(driver.Intent, Is.EqualTo(TruckIntent.Wrecked));
        }

        private static void Place(Rigidbody body, Vector3 position, float speed)
        {
            Quaternion heading = Quaternion.LookRotation(Vector3.right);
            body.transform.SetPositionAndRotation(position, heading);
            body.position = position;
            body.rotation = heading;
            body.linearVelocity = Vector3.right * speed;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }
    }
}
