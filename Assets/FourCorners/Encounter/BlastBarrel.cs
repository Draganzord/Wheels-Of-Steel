using UnityEngine;

namespace FourCorners
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class BlastBarrel : MonoBehaviour
    {
        [SerializeField] private SalvageEncounter encounter;
        [SerializeField, Min(1)] private float durability = 18;
        private float remaining;

        public bool HasExploded { get; private set; }

        public void Configure(SalvageEncounter session) => encounter = session;

        private void Awake() => remaining = durability;

        public void TakeDamage(float amount)
        {
            if (HasExploded || amount <= 0)
                return;

            remaining -= amount;
            if (remaining > 0)
                return;

            HasExploded = true;
            Vector3 position = GetComponent<Rigidbody>().worldCenterOfMass;
            gameObject.SetActive(false);
            encounter.Detonate(position, 4.6f, 53, 5200);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody != null && collision.rigidbody.GetComponent<ArcadeVehicle>() != null)
                TakeDamage(Mathf.Max(0, collision.relativeVelocity.magnitude - 5.8f) * 3.6f);
        }
    }
}
