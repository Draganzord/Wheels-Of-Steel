using UnityEngine;

namespace FourCorners
{
    public sealed class YardRocket : MonoBehaviour
    {
        private enum FlightState { Ready, Flying, Detonated }

        [SerializeField, Min(1)] private float speed = 43f;
        [SerializeField, Min(1)] private float range = 68f;
        [SerializeField, Min(0.01f)] private float contactRadius = 0.16f;
        [SerializeField, Min(0.1f)] private float blastRadius = 6.4f;
        [SerializeField, Min(0)] private float blastDamage = 66f;
        [SerializeField, Min(0)] private float blastImpulse = 7200f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Collider[] overlaps = new Collider[12];
        private CombatVehicle owner;
        private Rigidbody ownerBody;
        private SalvageEncounter encounter;
        private CombatEffects feedback;
        private Vector3 direction;
        private float distanceLeft;
        private float nextExhaust;
        private FlightState state;

        public void Launch(CombatVehicle launchingVehicle, Vector3 heading, SalvageEncounter session,
            float travelDistance = float.PositiveInfinity)
        {
            if (state != FlightState.Ready)
                return;

            owner = launchingVehicle;
            ownerBody = owner.Vehicle.Body;
            encounter = session;
            feedback = encounter.GetComponentInChildren<CombatEffects>();
            direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : owner.transform.forward;
            Vector3 up = Mathf.Abs(direction.y) > 0.98f ? Vector3.forward : Vector3.up;
            transform.rotation = Quaternion.LookRotation(direction, up);
            distanceLeft = Mathf.Clamp(travelDistance, contactRadius, range);
            state = FlightState.Flying;
            feedback?.RocketExhaust(transform.position, direction);
        }

        private void FixedUpdate()
        {
            if (state != FlightState.Flying)
                return;

            Vector3 origin = transform.position;
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, contactRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlapCount; index++)
            {
                if (!IsOwner(overlaps[index]))
                {
                    Detonate(origin);
                    return;
                }
            }

            float travel = Mathf.Min(speed * Time.fixedDeltaTime, distanceLeft);
            int count = Physics.SphereCastNonAlloc(origin, contactRadius, direction, hits, travel, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit impact = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.distance >= nearest || IsOwner(hit.collider))
                    continue;
                nearest = hit.distance;
                impact = hit;
            }

            if (impact.collider != null)
            {
                Detonate(impact.point + impact.normal * 0.085f);
                return;
            }

            transform.position = origin + direction * travel;
            distanceLeft -= travel;
            if (distanceLeft <= 0.001f)
            {
                Detonate(transform.position);
                return;
            }

            if (Time.time >= nextExhaust)
            {
                nextExhaust = Time.time + 0.035f;
                feedback?.RocketExhaust(transform.position - direction * 0.64f, direction);
            }
        }

        private bool IsOwner(Collider collider) =>
            collider.attachedRigidbody == ownerBody || collider.transform.IsChildOf(owner.transform);

        private void Detonate(Vector3 position)
        {
            state = FlightState.Detonated;
            encounter.Detonate(position, blastRadius, blastDamage, blastImpulse);
            Destroy(gameObject);
        }
    }
}
