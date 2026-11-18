using UnityEngine;
using UnityEngine.SceneManagement;

namespace FourCorners
{
    public enum EncounterResult { Active, Won, Lost }

    public sealed class SalvageEncounter : MonoBehaviour
    {
        [SerializeField] private CombatVehicle player;
        [SerializeField] private CombatVehicle truck;
        [SerializeField] private ArenaLayout arena;
        [SerializeField] private CombatEffects effects;
        [SerializeField] private YardMine minePrefab;
        [SerializeField, Range(1, 8)] private int startingMines = 3;

        private CombatVehicle[] targets;
        private bool restarting;

        public CombatVehicle Player => player;
        public CombatVehicle Truck => truck;
        public ArenaLayout Arena => arena;
        public int Mines { get; private set; }
        public EncounterResult Result { get; private set; }

        public void Configure(CombatVehicle car, CombatVehicle wrecker, ArenaLayout layout,
            CombatEffects feedback, YardMine mine)
        {
            player = car;
            truck = wrecker;
            arena = layout;
            effects = feedback;
            minePrefab = mine;
        }

        private void Awake()
        {
            targets = new[] { player, truck };
            Mines = startingMines;
        }

        private void Update()
        {
            if (Result != EncounterResult.Active)
                return;

            if (player.Destroyed)
            {
                Result = EncounterResult.Lost;
                truck.Vehicle.SetInput(0, 0, false);
                effects.Blast(player.transform.position);
            }
            else if (truck.Destroyed)
            {
                Result = EncounterResult.Won;
                player.DamageEnabled = false;
                effects.Blast(truck.transform.position);
            }
        }

        public bool TryFire() => player.TryFire(effects);

        public bool TryDropMine()
        {
            if (Mines == 0 || Result != EncounterResult.Active || player.Destroyed)
                return false;

            Vector3 position = player.transform.TransformPoint(0, 0.5f, -3.75f);
            if (!Physics.Raycast(position, Vector3.down, out RaycastHit hit, 4, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (SurfaceZone.Resolve(hit.collider, hit.point, out _) == null)
                return false;

            YardMine mine = Instantiate(minePrefab, hit.point + hit.normal * 0.055f,
                Quaternion.FromToRotation(Vector3.up, hit.normal));
            mine.Deploy(targets, effects);
            Mines--;
            return true;
        }

        public void RequestRestart()
        {
            if (restarting)
                return;

            restarting = true;
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex, LoadSceneMode.Single);
        }
    }
}
