using UnityEngine;
using UnityEngine.InputSystem;

namespace FourCorners
{
    public sealed class DesktopDriver : MonoBehaviour
    {
        [SerializeField] private SalvageEncounter encounter;
        [SerializeField] private InputActionAsset templateActions;
        [SerializeField] private InputAction dropMine = new("Drop mine", InputActionType.Button, "<Mouse>/rightButton");
        [SerializeField] private InputAction restart = new("Restart", InputActionType.Button, "<Keyboard>/r");

        private InputActionAsset controls;
        private InputAction move;
        private InputAction handbrake;
        private InputAction fire;

        public void Configure(SalvageEncounter session, InputActionAsset input)
        {
            encounter = session;
            templateActions = input;
        }

        private void Awake()
        {
            controls = Instantiate(templateActions);
            InputActionMap map = controls.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            handbrake = map.FindAction("Jump", true);
            fire = map.FindAction("Attack", true);
        }

        private void OnEnable()
        {
            move.Enable();
            handbrake.Enable();
            fire.Enable();
            dropMine.Enable();
            restart.Enable();
        }

        private void OnDisable()
        {
            move.Disable();
            handbrake.Disable();
            fire.Disable();
            dropMine.Disable();
            restart.Disable();
            if (encounter != null && encounter.Player != null && encounter.Player.Vehicle != null)
                encounter.Player.Vehicle.SetInput(0, 0, false);
        }

        private void OnDestroy()
        {
            dropMine.Dispose();
            restart.Dispose();
            Destroy(controls);
        }

        private void Update()
        {
            if (restart.WasPressedThisFrame())
                encounter.RequestRestart();

            if (encounter.Player.Destroyed)
                return;

            Vector2 drive = move.ReadValue<Vector2>();
            encounter.Player.Vehicle.SetInput(drive.y, drive.x, handbrake.IsPressed());
            if (fire.IsPressed())
                encounter.TryFire();
            if (dropMine.WasPressedThisFrame())
                encounter.TryDropMine();
        }
    }
}
