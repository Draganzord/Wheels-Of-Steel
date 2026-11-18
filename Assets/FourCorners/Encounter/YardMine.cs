using UnityEngine;

namespace FourCorners
{
    public sealed class YardMine : MonoBehaviour
    {
        public const float ArmDelay = 0.7f;
        private const float TriggerRadius = 2.4f;
        private const float BlastRadius = 4.7f;

        [SerializeField] private GameObject warningLight;
        private CombatVehicle[] targets;
        private CombatEffects effects;
        private float armedAt;

        public bool IsArmed => Time.time >= armedAt;

        public void Deploy(CombatVehicle[] vehicles, CombatEffects feedback)
        {
            targets = vehicles;
            effects = feedback;
            armedAt = Time.time + ArmDelay;
        }

        private void Update()
        {
            if (!IsArmed)
                return;

            warningLight.SetActive(Mathf.Repeat(Time.time, 0.4f) < 0.23f);
            foreach (CombatVehicle target in targets)
            {
                if (!target.Destroyed && DistanceTo(target) <= TriggerRadius)
                {
                    Detonate();
                    return;
                }
            }
        }

        private void Detonate()
        {
            effects.Blast(transform.position + Vector3.up * 0.2f);
            foreach (CombatVehicle target in targets)
            {
                float distance = DistanceTo(target);
                if (target.Destroyed || distance > BlastRadius)
                    continue;

                float strength = 1 - distance / BlastRadius;
                target.Vehicle.Body.AddExplosionForce(4800 * strength, transform.position, BlastRadius, 0.2f, ForceMode.Impulse);
                target.TakeDamage(37 + 31 * strength);
            }
            Destroy(gameObject);
        }

        private float DistanceTo(CombatVehicle target)
        {
            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0;
            return delta.magnitude;
        }

        public void SetWarningLight(GameObject lightObject) => warningLight = lightObject;
    }
}
