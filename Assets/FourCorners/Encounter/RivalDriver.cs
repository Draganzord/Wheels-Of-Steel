using UnityEngine;

namespace FourCorners
{
    public enum RivalKind { Gunner, Interceptor }

    [RequireComponent(typeof(CombatVehicle))]
    public sealed class RivalDriver : MonoBehaviour
    {
        [SerializeField] private SalvageEncounter encounter;
        [SerializeField] private RivalKind kind;
        [SerializeField] private string callsign;
        [SerializeField, Min(1f)] private float alertDistance = 23f;
        [SerializeField, Min(0f)] private float deploymentDelay;

        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private CombatVehicle combat;
        private ArcadeVehicle vehicle;
        private float readyAt;
        private float aimSince = -1f;
        private float fireAt;
        private int burstRemaining;
        private int routeIndex = -1;
        private int routeDirection;
        private float nextRouteAt;
        private float stalledFor;
        private float reverseUntil;
        private float reverseSteer;
        private Vector3 interceptHeading;
        private float interceptUntil;
        private float nextInterceptAt;

        public RivalKind Kind => kind;
        public string Callsign => callsign;
        public bool IsEngaged { get; private set; }

        public void Configure(SalvageEncounter session, RivalKind role, string label, float wakeDistance, float delay)
        {
            encounter = session;
            kind = role;
            callsign = label;
            alertDistance = wakeDistance;
            deploymentDelay = delay;
        }

        private void Start()
        {
            combat = GetComponent<CombatVehicle>();
            vehicle = combat.Vehicle;
            readyAt = Time.fixedTime + deploymentDelay;
        }

        private void OnDisable()
        {
            if (vehicle != null)
                vehicle.SetInput(0f, 0f, false);
        }

        private void FixedUpdate()
        {
            if (combat.Destroyed || encounter.Result != EncounterResult.Active)
            {
                vehicle.SetInput(0f, 0f, false);
                return;
            }

            Rigidbody player = encounter.Player.Vehicle.Body;
            Vector3 separation = Planar(player.position - vehicle.Body.position);
            float distance = separation.magnitude;
            bool canSee = LaneClear(player.position, player);
            if (!IsEngaged)
            {
                if (Time.fixedTime >= readyAt && ((canSee && distance < alertDistance)
                    || combat.ArmorFraction < 0.999f || encounter.Truck.Destroyed))
                    IsEngaged = true;
                else
                {
                    vehicle.SetInput(0f, 0f, true);
                    return;
                }
            }

            if (kind == RivalKind.Interceptor && Time.fixedTime < interceptUntil)
            {
                Vector3 through = vehicle.Body.position + interceptHeading * 10f;
                if (LaneClear(through, player))
                {
                    DriveTo(through, 23f);
                    return;
                }
                interceptUntil = 0f;
            }

            if (!canSee)
            {
                TrackAim(false);
                DriveTo(Detour(player.position), kind == RivalKind.Gunner ? 12f : 15f);
                return;
            }

            routeIndex = -1;
            if (kind == RivalKind.Gunner)
                DriveGunner(player, separation, distance);
            else
                DriveInterceptor(player, separation, distance);
        }

        private void DriveGunner(Rigidbody player, Vector3 separation, float distance)
        {
            float alignment = Vector3.Angle(Forward, separation);
            float aimTolerance = Mathf.Atan2(0.65f, distance) * Mathf.Rad2Deg;
            float preferredSpeed = Mathf.Clamp((distance - 12f) * 0.94f, 0f, 16f);
            if (distance < 7f && alignment < 35f && LaneClear(vehicle.Body.position - Forward * 5f))
                preferredSpeed = -3.8f;
            else if (alignment > aimTolerance * 0.8f)
                preferredSpeed = Mathf.Max(preferredSpeed, 4.2f);

            DriveTo(player.position, preferredSpeed);
            TrackAim(distance <= 24f && alignment <= aimTolerance && Time.fixedTime >= reverseUntil);
        }

        private void TrackAim(bool aligned)
        {
            float now = Time.fixedTime;
            if (!aligned)
            {
                aimSince = -1f;
                if (burstRemaining > 0 && now > fireAt + 0.32f)
                {
                    burstRemaining = 0;
                    fireAt = now + 0.68f;
                }
                return;
            }

            if (aimSince < 0f)
                aimSince = now;
            if (now - aimSince < 0.26f || now < fireAt)
                return;

            if (burstRemaining == 0)
                burstRemaining = 3;
            if (encounter.TryFireOpponent(combat))
            {
                burstRemaining--;
                fireAt = now + (burstRemaining > 0 ? 0.23f : 1.12f);
            }
        }

        private void DriveInterceptor(Rigidbody player, Vector3 separation, float distance)
        {
            Vector3 target = player.position + Planar(player.linearVelocity) * Mathf.Clamp(distance / 26f, 0.1f, 0.66f);
            if (Time.fixedTime >= nextInterceptAt && distance < 10f && distance > 3f
                && Vector3.Dot(vehicle.Body.linearVelocity, Forward) > 8f && Vector3.Angle(Forward, separation) < 22f)
            {
                Vector3 heading = Planar(target - vehicle.Body.position).normalized;
                if (Vector3.Dot(Forward, heading) > 0.89f && LaneClear(vehicle.Body.position + heading * 11f, player))
                {
                    interceptHeading = heading;
                    interceptUntil = Time.fixedTime + 0.55f;
                    nextInterceptAt = Time.fixedTime + 1.45f;
                }
            }
            DriveTo(target, 22f);
        }

        private void DriveTo(Vector3 aim, float targetSpeed)
        {
            Vector3 position = vehicle.Body.position;
            Vector3 forward = Forward;
            float angle = Vector3.SignedAngle(forward, Planar(aim - position), Vector3.up);
            float speed = Vector3.Dot(vehicle.Body.linearVelocity, forward);
            float steer = Mathf.Clamp(angle / 32f, -1f, 1f);
            Rigidbody player = encounter.Player.Vehicle.Body;
            bool blocked = !LaneClear(position + forward * 5.8f, player);
            if (targetSpeed >= 0f)
            {
                targetSpeed = Mathf.Lerp(targetSpeed, Mathf.Min(4.2f, targetSpeed),
                    Mathf.InverseLerp(16f, 95f, Mathf.Abs(angle)));
                if (blocked)
                    targetSpeed = Mathf.Min(3f, targetSpeed);
            }

            bool stuck = Mathf.Abs(speed) < 1.15f && (blocked || Mathf.Abs(angle) > 97f);
            stalledFor = stuck ? stalledFor + Time.fixedDeltaTime : 0f;
            if (stalledFor > 0.42f && Time.fixedTime >= reverseUntil && LaneClear(position - forward * 4f))
            {
                reverseUntil = Time.fixedTime + 0.82f;
                reverseSteer = steer > 0f ? -0.8f : 0.8f;
                stalledFor = 0f;
            }

            if (Time.fixedTime < reverseUntil)
                vehicle.SetInput(-0.78f, reverseSteer, false);
            else
                vehicle.SetInput(Mathf.Clamp((targetSpeed - speed) * 0.44f, -0.88f, 1f),
                    targetSpeed < 0f ? -steer : steer, false);
        }

        private Vector3 Detour(Vector3 destination)
        {
            Transform[] loop = encounter.Arena.Loop;
            Vector3 position = vehicle.Body.position;
            if (routeIndex >= 0 && Time.fixedTime < nextRouteAt)
                return loop[routeIndex].position;

            nextRouteAt = Time.fixedTime + 0.28f;
            int goal = Nearest(loop, destination);
            if (routeIndex >= 0 && routeIndex != goal && Planar(loop[routeIndex].position - position).sqrMagnitude < 32f)
            {
                int onward = (routeIndex + routeDirection + loop.Length) % loop.Length;
                if (LaneClear(loop[onward].position))
                {
                    routeIndex = onward;
                    return loop[routeIndex].position;
                }
            }

            routeIndex = Nearest(loop, position);
            float best = float.PositiveInfinity;
            for (int index = 0; index < loop.Length; index++)
            {
                Vector3 offset = Planar(loop[index].position - position);
                float score = offset.magnitude + Mathf.Min(RouteLength(loop, index, goal, 1), RouteLength(loop, index, goal, -1));
                if (offset.magnitude > 4f && score < best && LaneClear(loop[index].position))
                {
                    best = score;
                    routeIndex = index;
                }
            }

            routeDirection = RouteLength(loop, routeIndex, goal, 1) <= RouteLength(loop, routeIndex, goal, -1) ? 1 : -1;
            return loop[routeIndex].position;
        }

        private static int Nearest(Transform[] loop, Vector3 point)
        {
            int best = 0;
            float distance = float.PositiveInfinity;
            for (int index = 0; index < loop.Length; index++)
            {
                float candidate = Planar(loop[index].position - point).sqrMagnitude;
                if (candidate < distance)
                {
                    best = index;
                    distance = candidate;
                }
            }
            return best;
        }

        private static float RouteLength(Transform[] loop, int entry, int goal, int direction)
        {
            float length = 0f;
            while (entry != goal)
            {
                int next = (entry + direction + loop.Length) % loop.Length;
                length += Vector3.Distance(loop[entry].position, loop[next].position);
                entry = next;
            }
            return length;
        }

        private bool LaneClear(Vector3 destination, Rigidbody permitted = null)
        {
            Vector3 origin = vehicle.Body.position + Vector3.up * 0.16f;
            Vector3 path = Planar(destination - origin);
            float distance = path.magnitude;
            if (distance < 1.7f)
                return true;

            int count = Physics.RaycastNonAlloc(origin, path / distance, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Rigidbody hit = sightHits[index].rigidbody;
                if (hit != vehicle.Body && (permitted == null || hit != permitted) && (hit == null || hit.mass >= 100f))
                    return false;
            }
            return true;
        }

        private Vector3 Forward => Planar(vehicle.Body.rotation * Vector3.forward).normalized;
        private static Vector3 Planar(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
