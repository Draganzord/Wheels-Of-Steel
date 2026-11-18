using UnityEngine;

namespace FourCorners
{
    [RequireComponent(typeof(CombatVehicle))]
    public sealed class TruckDriver : MonoBehaviour
    {
        [SerializeField] private SalvageEncounter encounter;
        [SerializeField, Min(1)] private float alertDistance = 25;
        [SerializeField, Min(1)] private float patrolSpeed = 8;
        [SerializeField, Min(1)] private float ramSpeed = 22;

        private ArcadeVehicle vehicle;
        private CombatVehicle combat;
        private Vector3[] nodes;
        private float[,] routeLength;
        private int[,] nextHop;
        private int patrolIndex;
        private float commitUntil;
        private Vector3 committedThrough;
        private float nextSearch;
        private Vector3 searchAim;
        private float stalledFor;
        private float reverseUntil;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        public bool IsChasing { get; private set; }

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

            Vector3 aim = IsChasing ? Chase(target, separation, distance, clear) : Patrol();
            DriveTo(aim, IsChasing ? ramSpeed : patrolSpeed);
        }

        private Vector3 Chase(Rigidbody target, Vector3 separation, float distance, bool clear)
        {
            if (Time.time < commitUntil)
                return committedThrough;

            if (!clear)
            {
                if (Time.time >= nextSearch)
                {
                    searchAim = RouteToward(target.position);
                    nextSearch = Time.time + 0.23f;
                }
                return searchAim;
            }

            Vector3 intercept = target.position + Planar(target.linearVelocity) * Mathf.Clamp(distance / 24, 0.15f, 0.65f);
            float alignment = Vector3.Dot(Planar(transform.forward).normalized, separation.normalized);
            if (distance < 13 && alignment > 0.89f && vehicle.Speed > 8 && Planar(intercept - target.position).magnitude < 5)
            {
                commitUntil = Time.time + 0.68f;
                committedThrough = intercept + Planar(transform.forward).normalized * 10;
            }
            return intercept;
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
            if (stalledFor > 0.55f && Time.time >= reverseUntil)
            {
                reverseUntil = Time.time + 1.22f;
                stalledFor = 0;
            }

            if (Time.time < reverseUntil)
                vehicle.SetInput(-0.85f, steering > 0 ? -0.9f : 0.9f, false);
            else
                vehicle.SetInput(Mathf.Clamp((cornerSpeed - speed) * 0.31f, -0.86f, 1), steering, false);
        }

        private bool PathIsClear(Vector3 destination, bool allowFence)
        {
            Vector3 origin = transform.position + Vector3.up * 0.18f;
            Vector3 direction = Planar(destination - origin);
            float distance = direction.magnitude;
            if (distance < 2.9f)
                return true;

            int count = Physics.RaycastNonAlloc(origin, direction.normalized, sightHits, distance, ~0,
                QueryTriggerInteraction.Ignore);
            RaycastHit obstacle = default;
            float closest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = sightHits[index];
                Rigidbody body = hit.rigidbody;
                if (hit.distance >= closest || body == vehicle.Body || (body != null && body.mass < 100))
                    continue;

                obstacle = hit;
                closest = hit.distance;
            }

            return obstacle.collider == null || obstacle.rigidbody == encounter.Player.Vehicle.Body
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
