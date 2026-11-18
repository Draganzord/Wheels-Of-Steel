using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FourCorners.Tests
{
    public sealed class EncounterTests
    {
        private const string SceneName = "FourCornersSalvageYard";
        private SalvageEncounter encounter;

        [UnitySetUp]
        public IEnumerator OpenScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            encounter = Object.FindAnyObjectByType<SalvageEncounter>();
            Assert.That(encounter, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TruckNoticesThePlayerAndRamsOpenTheBankGate()
        {
            encounter.GetComponent<DesktopDriver>().enabled = false;
            TruckDriver driver = encounter.Truck.GetComponent<TruckDriver>();
            yield return WaitFixed(20);
            Assert.That(driver.IsChasing, Is.True, "The truck should acquire the player at the opening of the encounter.");
            driver.enabled = false;
            encounter.Truck.DamageEnabled = false;
            Rigidbody truck = encounter.Truck.Vehicle.Body;
            BreakableFence fence = encounter.Arena.Fence;
            Vector3 gate = fence.transform.position;
            Vector3 through = Vector3.right;

            Place(truck, gate - through * 3.5f, through, 0.87f, 2.4f);
            yield return WaitFixed(26);
            Assert.That(fence.IsBroken, Is.False, "A slow bump should leave the gate intact.");
            Assert.That(fence.StandingBlocker.enabled, Is.True);

            Place(truck, gate - through * 8, through, 0.88f, 15.5f);
            yield return WaitFixed(30);
            Assert.That(fence.IsBroken, Is.True, "A perpendicular truck ram should breach the gate.");
            Assert.That(fence.StandingBlocker.enabled, Is.False);
            Assert.That(truck.position.x, Is.GreaterThan(gate.x), "The truck should retain enough momentum to pass through.");
            Assert.That(fence.ActiveDebrisCount, Is.InRange(2, 20));

            Place(truck, new Vector3(-10, 0, -31), through, 0.86f);
            Rigidbody car = encounter.Player.Vehicle.Body;
            Place(car, gate - through * 5, through, 0.72f, 10);
            yield return WaitFixed(46);
            Assert.That(car.position.x, Is.GreaterThan(gate.x + 1), "A later pass must clear the gate without repeating the break.");
            Assert.That(fence.IsBroken, Is.True);

            SalvageEncounter old = encounter;
            old.RequestRestart();
            yield return new WaitUntil(() => old == null);
            encounter = Object.FindAnyObjectByType<SalvageEncounter>();
            Assert.That(encounter.Arena.Fence.IsBroken, Is.False);
            Assert.That(encounter.Arena.Fence.StandingBlocker.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator PursuitCrossesTheCreekAndBreachesTheGateUnderAiControl()
        {
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Player.DamageEnabled = false;
            encounter.Truck.DamageEnabled = false;
            Place(encounter.Player.Vehicle.Body, new Vector3(31, 0, -3), Vector3.right, 0.74f);
            Place(encounter.Truck.Vehicle.Body, new Vector3(6.5f, 0, -4), Vector3.right, 0.83f);

            bool sawWater = false;
            BreakableFence fence = encounter.Arena.Fence;
            WheelContact[] contacts = encounter.Truck.Vehicle.Contacts;
            for (int step = 0; step < 330 && !fence.IsBroken; step++)
            {
                yield return new WaitForFixedUpdate();
                foreach (WheelContact wheel in contacts)
                    sawWater |= wheel.Grounded && wheel.Surface != null && wheel.Surface.Kind == SurfaceKind.Water;
            }

            Assert.That(encounter.Truck.GetComponent<TruckDriver>().IsChasing, Is.True);
            Assert.That(sawWater, Is.True, "The truck should pursue through the solid creek bed.");
            Assert.That(fence.IsBroken, Is.True, $"Truck stopped at {encounter.Truck.Vehicle.Body.position} rather than breaching.");
        }

        [UnityTest]
        public IEnumerator MovingWreckSettlesAndBlocksFireThenTwoRestartsResetTheRound()
        {
            DisableDrivers();
            CombatVehicle player = encounter.Player;
            CombatVehicle truck = encounter.Truck;
            Rigidbody wreck = truck.Vehicle.Body;
            Place(player.Vehicle.Body, new Vector3(-13, 0, -31), Vector3.right, 0.73f);
            Place(wreck, new Vector3(-2, 0, -31), Vector3.right, 0.86f, 8);
            yield return WaitFixed(2);
            Assert.That(encounter.TryFire(), Is.True);
            Assert.That(truck.ArmorFraction, Is.LessThan(1), "A clear forward shot should hit the truck.");
            float speedBefore = wreck.linearVelocity.magnitude;
            Assert.That(speedBefore, Is.GreaterThan(5));
            truck.TakeDamage(truck.Armor);
            Assert.That(truck.Vehicle.IsWreck, Is.True);
            Assert.That(wreck.linearVelocity.magnitude, Is.EqualTo(speedBefore).Within(0.5f));
            yield return WaitFixed(190);
            Assert.That(encounter.Result, Is.EqualTo(EncounterResult.Won));
            Assert.That(wreck.linearVelocity.magnitude, Is.LessThan(0.7f));

            Vector3 muzzle = player.transform.TransformPoint(0, 0.53f, 2.55f);
            Vector3 aim = wreck.position - muzzle;
            aim.y = 0;
            Assert.That(Physics.Raycast(muzzle, aim.normalized, out RaycastHit hit, 52, ~0, QueryTriggerInteraction.Ignore));
            Assert.That(hit.rigidbody, Is.SameAs(wreck), "A settled wreck must stop shot raycasts.");
            Vector3 settled = wreck.position;
            player.Vehicle.SetInput(0.8f, 0, false);
            yield return WaitFixed(25);
            Assert.That(player.Vehicle.Speed, Is.GreaterThan(2), "The winner retains driving control.");
            Assert.That(Vector3.Distance(wreck.position, settled), Is.LessThan(0.3f));

            Assert.That(encounter.TryDropMine(), Is.False, "Combat is over after the win.");
            for (int restart = 0; restart < 2; restart++)
            {
                SalvageEncounter old = encounter;
                old.RequestRestart();
                old.RequestRestart();
                yield return null;
                yield return new WaitUntil(() => old == null);
                yield return null;
                encounter = Object.FindAnyObjectByType<SalvageEncounter>();
                Assert.That(Object.FindObjectsByType<ArcadeVehicle>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
                Assert.That(encounter.Result, Is.EqualTo(EncounterResult.Active));
                Assert.That(encounter.Mines, Is.EqualTo(3));
                Assert.That(encounter.Arena.Fence.IsBroken, Is.False);
                Assert.That(encounter.Arena.Fence.ActiveDebrisCount, Is.Zero);
                Assert.That(Object.FindObjectsByType<YardMine>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(encounter.Player.GetComponent<VehicleSurfaceEffects>().ActiveTrackCount, Is.Zero);
                Assert.That(encounter.Truck.GetComponent<TruckDriver>().isActiveAndEnabled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator MineArmsBeforeItCanHurtAChasingVehicle()
        {
            DisableDrivers();
            Place(encounter.Player.Vehicle.Body, new Vector3(-15, 0, -31), Vector3.right, 0.74f);
            Place(encounter.Truck.Vehicle.Body, new Vector3(-27, 0, -31), Vector3.right, 0.87f);
            Physics.SyncTransforms();
            Assert.That(encounter.TryDropMine(), Is.True);
            Assert.That(encounter.Mines, Is.EqualTo(2));
            YardMine mine = Object.FindAnyObjectByType<YardMine>();
            Assert.That(mine.IsArmed, Is.False);
            Place(encounter.Truck.Vehicle.Body, mine.transform.position, Vector3.right, 0.87f);
            yield return WaitFixed(13);
            Assert.That(encounter.Truck.ArmorFraction, Is.EqualTo(1));
            yield return WaitFixed(38);
            Assert.That(encounter.Truck.ArmorFraction, Is.LessThan(0.9f));
            Assert.That(mine == null, Is.True);
        }

        [UnityTest]
        public IEnumerator DesktopBindingsDriveDeployAndRestart()
        {
            encounter.Truck.GetComponent<TruckDriver>().enabled = false;
            Place(encounter.Player.Vehicle.Body, new Vector3(-16, 0, -31), Vector3.right, 0.74f);
            InputSettings settings = InputSystem.settings;
            InputSettings.BackgroundBehavior background = settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode editorInput = settings.editorInputBehaviorInPlayMode;
            // Batch Play Mode has no focused Game view. Route synthetic device input to the player.
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null;
                Assert.That(keyboard.wKey.isPressed, Is.True, "The synthetic keyboard event should reach the player.");
                yield return WaitFixed(32);
                Assert.That(encounter.Player.Vehicle.Speed, Is.GreaterThan(3));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                yield return null;
                yield return null;
                Assert.That(encounter.Mines, Is.EqualTo(2));

                SalvageEncounter old = encounter;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                float deadline = Time.realtimeSinceStartup + 5;
                while (old != null && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(old == null, Is.True, "R should reload the playable scene.");
                Assert.That(Object.FindAnyObjectByType<SalvageEncounter>().Mines, Is.EqualTo(3));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        private void DisableDrivers()
        {
            encounter.GetComponent<DesktopDriver>().enabled = false;
            encounter.Truck.GetComponent<TruckDriver>().enabled = false;
        }

        private static IEnumerator WaitFixed(int steps)
        {
            for (int index = 0; index < steps; index++)
                yield return new WaitForFixedUpdate();
        }

        private static void Place(Rigidbody body, Vector3 position, Vector3 heading, float height, float speed = 0)
        {
            position.y = height;
            body.transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading));
            body.position = position;
            body.rotation = Quaternion.LookRotation(heading);
            body.linearVelocity = heading * speed;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }
    }
}
