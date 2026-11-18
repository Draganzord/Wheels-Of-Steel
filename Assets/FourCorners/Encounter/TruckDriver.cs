using UnityEngine;

namespace FourCorners
{
    public enum TruckIntent { Patrolling, Pursuing, LiningUp, Ramming, Recovering, Wrecked }

    [RequireComponent(typeof(CombatVehicle))]
    public sealed class TruckDriver : MonoBehaviour
    {
        [SerializeField] private SalvageEncounter encounter;
        [SerializeField, Min(1)] private float alertDistance = 25;
        [SerializeField, Min(1)] private float patrolSpeed = 8;
        [SerializeField, Min(1)] private float ramSpeed = 22;
        [SerializeField, Min(0.1f)] private float lineupSeconds = 0.42f;
        [SerializeField, Min(0.1f)] private float ramSeconds = 0.82f;
        [SerializeField, Min(0.1f)] private float recoverySeconds = 0.95f;

        private ArcadeVehicle vehicle;
        private CombatVehicle combat;
        private Vector3[] nodes;
        private float[,] routeLength;
        private int[,] nextHop;
        private int patrolIndex;
        private TruckIntent intent;
        private float intentUntil;
        private Vector3 ramHeading;
        private float nextSearch;
        private Vector3 searchAim;
        private float stalledFor;
        private float reverseUntil;
        private float reverseSteering;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        public bool IsChasing { get; private set; }
        public TruckIntent Intent => combat != null && combat.Destroyed ? TruckIntent.Wrecked : intent;

        public void Configure(SalvageEncounter session) => encounter = session;

        private void Start()
        {
            combat = GetComponent<CombatVehicle>();
            vehicle = combat.Vehicle;
            BakeRoute(encounter.Arena.Loop, encounter.Arena.Shortcut);
            patrolIndex = ClosestNode(transform.position, 0, encounter.Arena.Loop.Length);
        }

        private void FixedUpdate()
        {
            if (combat.Destroyed || encounter.Result != EncounterResult.Active)
            {
                vehicle.SetInput(0, 0, false);
                return;
            }

            Rigidbody target = encounter.Player.Vehicle.Body;
            Vector3 separation = Planar(target.position - transform.position);
            float distance = separation.magnitude;
            bool clear = PathIsClear(target.position, true);
            IsChasing |= (distance < alertDistance && clear) || combat.ArmorFraction < 0.999f;

            if (!IsChasing)
            {
                DriveTo(Patrol(), patrolSpeed);
                return;
            }

            Pursue(target, separation, distance, clear);
        }

        private void Pursue(Rigidbody target, Vector3 separation, float distance, bool clear)
        {
            Vector3 intercept = target.position + Planar(target.linearVelocity) * Mathf.Clamp(distance / 24, 0.15f, 0.65f);
            if ((intent == TruckIntent.LiningUp || intent == TruckIntent.Ramming) && DriveAttack(intercept, clear))
                return;

            Vector3 aim = clear ? intercept : SearchFor(target.position);
            if (intent == TruckIntent.Recovering && Time.fixedTime < Mathf.Max(intentUntil, reverseUntil))
            {
                DriveTo(aim, Mathf.Min(ramSpeed, 10f));
                return;
            }

            intent = TruckIntent.Pursuing;
            float forwardSpeed = Vector3.Dot(vehicle.Body.linearVelocity, transform.forward);
            Vector3 forward = Planar(transform.forward).normalized;
            Vector3 aimHeading = Planar(intercept - transform.position).normalized;
            if (clear && distance >= 5f && distance < 17f && forwardSpeed > 8f && Time.fixedTime >= reverseUntil
                && Vector3.Dot(forward, separation.normalized) > 0.89f && Vector3.Dot(forward, aimHeading) > 0.9f
                && Planar(intercept - target.position).sqrMagnitude < 25f
                && PathIsClear(transform.position + aimHeading * AttackProbeLength(forwardSpeed), true, ignorePlayer: true))
            {
                ramHeading = aimHeading;
                intent = TruckIntent.LiningUp;
                intentUntil = Time.fixedTime + lineupSeconds;
            }

            DriveTo(aim, ramSpeed);
        }

        private bool DriveAttack(Vector3 intercept, bool clear)
        {
            float forwardSpeed = Vector3.Dot(vehicle.Body.linearVelocity, transform.forward);
            bool blocked = !PathIsClear(transform.position + ramHeading * AttackProbeLength(forwardSpeed),
                true, ignorePlayer: true);
            if (blocked || (intent == TruckIntent.Ramming && (Time.fixedTime >= intentUntil || forwardSpeed < 4f)))
            {
                BeginRecovery();
                return false;
            }

            if (intent == TruckIntent.LiningUp)
            {
                Vector3 aimHeading = Planar(intercept - transform.position).normalized;
                if (clear && Vector3.Dot(ramHeading, aimHeading) > 0.8f)
                    ramHeading = Vector3.RotateTowards(ramHeading, aimHeading, 0.55f * Time.fixedDeltaTime, 0f);

                if (Time.fixedTime >= intentUntil)
                {
                    intent = TruckIntent.Ramming;
                    intentUntil = Time.fixedTime + ramSeconds;
                }
            }

            DriveTo(transform.position + ramHeading * 12f, ramSpeed);
            return true;
        }

        private static float AttackProbeLength(float speed) => Mathf.Clamp(speed * 0.55f, 5.4f, 12f);

        private void BeginRecovery()
        {
            intent = TruckIntent.Recovering;
            intentUntil = Time.fixedTime + recoverySeconds;
        }

        private Vector3 SearchFor(Vector3 position)
        {
            if (Time.fixedTime >= nextSearch)
            {
                searchAim = RouteToward(position);
                nextSearch = Time.fixedTime + 0.23f;
            }
            return searchAim;
        }

        private Vector3 Patrol()
        {
            if (Planar(nodes[patrolIndex] - transform.position).sqrMagnitude < 63)
                patrolIndex = (patrolIndex + 1) % encounter.Arena.Loop.Length;
            return nodes[patrolIndex];
        }

        private void DriveTo(Vector3 aim, float targetSpeed)
        {
            Vector3 direction = Planar(aim - transform.position);
            float angle = Vector3.SignedAngle(Planar(transform.forward), direction, Vector3.up);
            float speed = Vector3.Dot(vehicle.Body.linearVelocity, transform.forward);
            float steering = Mathf.Clamp(angle / 36, -1, 1);
            float cornerSpeed = Mathf.Lerp(targetSpeed, 4, Mathf.InverseLerp(18, 96, Mathf.Abs(angle)));
            bool blockedClose = !PathIsClear(transform.position + transform.forward * 6.4f, IsChasing);
            if (blockedClose)
                cornerSpeed = Mathf.Min(cornerSpeed, 5);

            stalledFor = Mathf.Abs(speed) < 1.4f && (blockedClose || Mathf.Abs(angle) > 69)
                ? stalledFor + Time.fixedDeltaTime : 0;
            if (stalledFor > 0.34f && Time.fixedTime >= reverseUntil)
            {
                reverseUntil = Time.fixedTime + 1.22f;
                reverseSteering = steering > 0 ? -0.9f : 0.9f;
                stalledFor = 0;
                if (IsChasing && intent != TruckIntent.Recovering)
                    BeginRecovery();
            }

            if (Time.fixedTime < reverseUntil)
                vehicle.SetInput(-0.85f, reverseSteering, false);
            else
                vehicle.SetInput(Mathf.Clamp((cornerSpeed - speed) * 0.31f, -0.86f, 1), steering, false);
        }

        private bool PathIsClear(Vector3 destination, bool allowFence, bool ignorePlayer = false)
        {
            Vector3 origin = transform.position + Vector3.up * 0.18f;
            Vector3 direction = Planar(destination - origin);
            float distance = direction.magnitude;
            if (distance < 2.9f)
                return true;

            Rigidbody player = encounter.Player.Vehicle.Body;
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, sightHits, distance, ~0,
                QueryTriggerInteraction.Ignore);
            RaycastHit obstacle = default;
            float closest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = sightHits[index];
                Rigidbody body = hit.rigidbody;
                if (hit.distance >= closest || body == vehicle.Body || (ignorePlayer && body == player)
                    || (body != null && body.mass < 100))
                    continue;

                obstacle = hit;
                closest = hit.distance;
            }

            return obstacle.collider == null || obstacle.rigidbody == player
                || (allowFence && obstacle.collider.GetComponentInParent<BreakableFence>() != null);
        }

        private Vector3 RouteToward(Vector3 destination)
        {
            int goal = ClosestNode(destination, 0, nodes.Length);
            int entry = ClosestNode(transform.position, 0, nodes.Length);
            float best = float.PositiveInfinity;
            for (int index = 0; index < nodes.Length; index++)
            {
                float cost = Planar(nodes[index] - transform.position).magnitude + routeLength[index, goal];
                if (cost < best && PathIsClear(nodes[index], true))
                {
                    best = cost;
                    entry = index;
                }
            }

            int next = nextHop[entry, goal];
            if (Planar(nodes[entry] - transform.position).sqrMagnitude < 49 && next >= 0)
                return nodes[next];
            return nodes[entry];
        }

        private void BakeRoute(Transform[] loop, Transform[] shortcut)
        {
            int count = loop.Length + shortcut.Length;
            nodes = new Vector3[count];
            routeLength = new float[count, count];
            nextHop = new int[count, count];
            for (int row = 0; row < count; row++)
            {
                nodes[row] = (row < loop.Length ? loop[row] : shortcut[row - loop.Length]).position;
                for (int column = 0; column < count; column++)
                {
                    routeLength[row, column] = row == column ? 0 : float.PositiveInfinity;
                    nextHop[row, column] = -1;
                }
            }

            for (int index = 0; index < loop.Length; index++)
                Link(index, (index + 1) % loop.Length);
            for (int index = loop.Length + 1; index < count; index++)
                Link(index - 1, index);
            Link(loop.Length, ClosestNode(nodes[loop.Length], 0, loop.Length));
            Link(count - 1, ClosestNode(nodes[count - 1], 0, loop.Length));

            for (int via = 0; via < count; via++)
            for (int from = 0; from < count; from++)
            for (int to = 0; to < count; to++)
            {
                float distance = routeLength[from, via] + routeLength[via, to];
                if (distance < routeLength[from, to])
                {
                    routeLength[from, to] = distance;
                    nextHop[from, to] = nextHop[from, via];
                }
            }
        }

        private void Link(int left, int right)
        {
            routeLength[left, right] = routeLength[right, left] = Vector3.Distance(nodes[left], nodes[right]);
            nextHop[left, right] = right;
            nextHop[right, left] = left;
        }

        private int ClosestNode(Vector3 position, int start, int end)
        {
            int closest = start;
            float minimum = float.PositiveInfinity;
            for (int index = start; index < end; index++)
            {
                float distance = Planar(nodes[index] - position).sqrMagnitude;
                if (distance < minimum)
                {
                    closest = index;
                    minimum = distance;
                }
            }
            return closest;
        }

        private static Vector3 Planar(Vector3 vector) => new(vector.x, 0, vector.z);
    }
}
