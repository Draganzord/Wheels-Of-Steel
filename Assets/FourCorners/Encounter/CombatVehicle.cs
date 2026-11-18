using UnityEngine;

namespace FourCorners
{
    [RequireComponent(typeof(ArcadeVehicle))]
    public sealed class CombatVehicle : MonoBehaviour
    {
        [SerializeField, Min(1)] private float maxArmor = 100;
        [SerializeField] private Vector3 muzzle = new(0, 0.58f, 2.45f);
        [SerializeField, Min(0.04f)] private float shotInterval = 0.145f;
        [SerializeField, Min(1)] private float gunDamage = 9;

        private float nextShot;
        private float nextImpact;

        public ArcadeVehicle Vehicle { get; private set; }
        public float Armor { get; private set; }
        public float ArmorFraction => Armor / maxArmor;
        public bool Destroyed => Armor <= 0;
        public bool DamageEnabled { get; set; } = true;

        public void Configure(float armor, Vector3 muzzlePosition)
        {
            maxArmor = armor;
            muzzle = muzzlePosition;
        }

        private void Awake()
        {
            Vehicle = GetComponent<ArcadeVehicle>();
            Armor = maxArmor;
        }

        public bool TryFire(CombatEffects effects)
        {
            if (Destroyed || Time.time < nextShot)
                return false;

            nextShot = Time.time + shotInterval;
            Rigidbody body = Vehicle.Body;
            Vector3 origin = body.position + body.rotation * muzzle;
            Vector3 forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
            Vector3 endpoint = origin + forward * 52;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, 52, ~0, QueryTriggerInteraction.Ignore))
            {
                endpoint = hit.point;
                CombatVehicle target = hit.collider.GetComponentInParent<CombatVehicle>();
                if (target != null && target != this)
                    target.TakeDamage(gunDamage);
            }

            effects.Shot(origin, endpoint);
            return true;
        }

        public void TakeDamage(float amount)
        {
            if (Destroyed || !DamageEnabled || amount <= 0)
                return;

            Armor = Mathf.Max(0, Armor - amount);
            if (Destroyed)
                Vehicle.BecomeWreck();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Destroyed || Time.time < nextImpact)
                return;

            float normalSpeed = 0;
            Vector3 impactPoint = transform.position;
            for (int index = 0; index < collision.contactCount; index++)
            {
                ContactPoint contact = collision.GetContact(index);
                if (Mathf.Abs(contact.normal.y) > 0.68f)
                    continue;

                float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
                if (speed > normalSpeed)
                {
                    normalSpeed = speed;
                    impactPoint = contact.point;
                }
            }

            if (normalSpeed < 4.2f)
                return;

            Rigidbody other = collision.rigidbody;
            float ownMass = Vehicle.Body.mass;
            float impulseSpeed = collision.impulse.magnitude / ownMass;
            if (impulseSpeed < 0.8f)
                return;

            float massFactor = other != null ? Mathf.Clamp(Mathf.Sqrt(other.mass / ownMass), 0.22f, 1.6f) : 0.95f;
            Vector3 localImpact = transform.InverseTransformPoint(impactPoint);
            float armorFactor = localImpact.z > 0.95f ? 0.72f : localImpact.z < -0.65f ? 1.26f : 1f;
            float severity = Mathf.Min(normalSpeed - 3.5f, impulseSpeed * 1.9f);
            nextImpact = Time.time + 0.34f;
            TakeDamage(Mathf.Min(43, severity * 2.5f * massFactor * armorFactor));
        }
    }
}
