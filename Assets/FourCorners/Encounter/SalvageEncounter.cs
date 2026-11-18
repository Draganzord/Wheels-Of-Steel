using System;
using System.Collections.Generic;
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
        [SerializeField] private CombatVehicle[] rivals = Array.Empty<CombatVehicle>();
        [SerializeField] private YardRocket rocketPrefab;
        [SerializeField, Range(1, 8)] private int startingRockets = 5;
        [SerializeField] private BlastBarrel[] barrels = Array.Empty<BlastBarrel>();

        private const int BlastLimit = 24;
        private readonly Queue<Explosion> pendingBlasts = new(BlastLimit);
        private readonly Collider[] blastHits = new Collider[96];
        private readonly HashSet<Rigidbody> movedBodies = new();
        private CombatVehicle[] participants;
        private CombatVehicle[] opponents;
        private DestructibleTerrain terrain;
        private bool restarting;
        private bool resolvingBlasts;
        private float nextRocket;

        private readonly struct Explosion
        {
            public readonly Vector3 Position;
            public readonly float Radius;
            public readonly float Damage;
            public readonly float Impulse;

            public Explosion(Vector3 position, float radius, float damage, float impulse)
            {
                Position = position;
                Radius = radius;
                Damage = damage;
                Impulse = impulse;
            }
        }

        public CombatVehicle Player => player;
        public CombatVehicle Truck => truck;
        public ArenaLayout Arena => arena;
        public IReadOnlyList<CombatVehicle> Opponents => opponents;
        public IReadOnlyList<CombatVehicle> Participants => participants;
        public int OpponentsRemaining { get; private set; }
        public int Mines { get; private set; }
        public int Rockets { get; private set; }
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

        public void ConfigureExpansion(CombatVehicle[] drivers, YardRocket rocket, BlastBarrel[] drums)
        {
            rivals = drivers;
            rocketPrefab = rocket;
            barrels = drums;
        }

        private void Awake()
        {
            opponents = new CombatVehicle[rivals.Length + 1];
            opponents[0] = truck;
            Array.Copy(rivals, 0, opponents, 1, rivals.Length);
            participants = new CombatVehicle[opponents.Length + 1];
            participants[0] = player;
            Array.Copy(opponents, 0, participants, 1, opponents.Length);
            foreach (CombatVehicle combatant in participants)
                combatant.Wrecked += OnWrecked;

            terrain = arena.GetComponent<DestructibleTerrain>();
            OpponentsRemaining = opponents.Length;
            Mines = startingMines;
            Rockets = startingRockets;
        }

        private void OnDestroy()
        {
            if (participants == null)
                return;

            foreach (CombatVehicle combatant in participants)
                if (combatant != null)
                    combatant.Wrecked -= OnWrecked;
        }

        private void OnWrecked(CombatVehicle combatant)
        {
            if (combatant != player)
                OpponentsRemaining--;
            effects.Blast(combatant.transform.position, 3.1f);
        }

        private void Update()
        {
            if (Result != EncounterResult.Active)
                return;

            if (player.Destroyed)
            {
                Result = EncounterResult.Lost;
                foreach (CombatVehicle opponent in opponents)
                    opponent.Vehicle.SetInput(0, 0, false);
            }
            else if (OpponentsRemaining == 0)
            {
                Result = EncounterResult.Won;
                player.DamageEnabled = false;
            }
        }

        public bool TryFire() => player.TryFire(effects);

        public bool TryFireOpponent(CombatVehicle shooter)
        {
            return Result == EncounterResult.Active && Array.IndexOf(opponents, shooter) >= 0
                && shooter.TryFire(effects, player);
        }

        public bool TryLaunchRocket(Vector3 aimPoint)
        {
            if (rocketPrefab == null || Rockets == 0 || player.Destroyed || Time.time < nextRocket)
                return false;

            Rigidbody body = player.Vehicle.Body;
            Vector3 horizontal = Vector3.ProjectOnPlane(aimPoint - body.position, Vector3.up);
            if (horizontal.sqrMagnitude < 16)
            {
                horizontal = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized * 18;
                aimPoint = body.position + horizontal - Vector3.up * 0.64f;
            }

            Vector3 start = body.position + Vector3.up * 0.64f + horizontal.normalized * 2.40f;
            Vector3 flight = aimPoint - start;
            if (flight.sqrMagnitude < 0.01f)
                return false;

            YardRocket rocket = Instantiate(rocketPrefab, start, Quaternion.LookRotation(flight));
            rocket.Launch(player, flight, this, flight.magnitude);
            Rockets--;
            nextRocket = Time.time + 0.52f;
            return true;
        }

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
            mine.Deploy(this);
            Mines--;
            return true;
        }

        public void Detonate(Vector3 position, float radius, float damage, float impulse)
        {
            if (radius <= 0 || damage <= 0 || pendingBlasts.Count >= BlastLimit)
                return;

            pendingBlasts.Enqueue(new Explosion(position, radius, damage, impulse));
            if (resolvingBlasts)
                return;

            resolvingBlasts = true;
            try
            {
                while (pendingBlasts.Count > 0)
                    ApplyExplosion(pendingBlasts.Dequeue());
            }
            finally
            {
                resolvingBlasts = false;
            }
        }

        private void ApplyExplosion(Explosion blast)
        {
            effects.Blast(blast.Position, blast.Radius);
            foreach (CombatVehicle target in participants)
            {
                if (target.Destroyed)
                    continue;

                Rigidbody body = target.Vehicle.Body;
                float falloff = 1 - Vector3.Distance(blast.Position, body.worldCenterOfMass) / blast.Radius;
                if (falloff <= 0)
                    continue;

                float cover = HasCover(blast.Position, body) ? 0.28f : 1f;
                body.AddExplosionForce(blast.Impulse * cover, blast.Position, blast.Radius, 0.27f, ForceMode.Impulse);
                target.TakeDamage(blast.Damage * (0.25f + 0.75f * falloff) * cover);
            }

            foreach (BlastBarrel barrel in barrels)
            {
                if (barrel.HasExploded)
                    continue;

                float falloff = 1 - Vector3.Distance(blast.Position, barrel.transform.position) / blast.Radius;
                if (falloff > 0)
                    barrel.TakeDamage(blast.Damage * (0.25f + 0.75f * falloff));
            }

            int hits = Physics.OverlapSphereNonAlloc(blast.Position, blast.Radius, blastHits, ~0,
                QueryTriggerInteraction.Ignore);
            movedBodies.Clear();
            for (int index = 0; index < hits; index++)
            {
                Rigidbody body = blastHits[index].attachedRigidbody;
                if (body != null && !body.isKinematic && !body.TryGetComponent(out CombatVehicle _) && movedBodies.Add(body))
                    body.AddExplosionForce(blast.Impulse * 0.30f, blast.Position, blast.Radius, 0.21f, ForceMode.Impulse);
            }
            arena.Fence.ApplyBlast(blast.Position, blast.Radius, blast.Damage);
            float craterRadius = Mathf.Clamp(blast.Radius * 0.74f, 1.8f, 5f);
            if (terrain != null && terrain.ApplyBlast(blast.Position, craterRadius, Mathf.Clamp01(blast.Damage / 77f)))
                foreach (CombatVehicle participant in participants)
                    if (participant.TryGetComponent(out VehicleSurfaceEffects wheelEffects))
                        wheelEffects.EraseTracksNear(blast.Position, craterRadius);
        }

        private static bool HasCover(Vector3 center, Rigidbody target)
        {
            Vector3 source = center + Vector3.up * 0.20f;
            Vector3 delta = target.worldCenterOfMass - source;
            return Physics.Raycast(source, delta.normalized, out RaycastHit hit, delta.magnitude, ~0,
                QueryTriggerInteraction.Ignore) && hit.rigidbody != target;
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
