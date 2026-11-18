using UnityEngine;

namespace FourCorners
{
    public sealed class YardMine : MonoBehaviour
    {
        public const float ArmDelay = 0.7f;
        private const float TriggerRadius = 2.4f;
        private const float BlastRadius = 4.7f;

        [SerializeField] private GameObject warningLight;
        private SalvageEncounter encounter;
        private float armedAt;

        public bool IsArmed => Time.time >= armedAt;

        public void Deploy(SalvageEncounter session)
        {
            encounter = session;
            armedAt = Time.time + ArmDelay;
        }

        private void Update()
        {
            if (!IsArmed)
                return;

            warningLight.SetActive(Mathf.Repeat(Time.time, 0.4f) < 0.23f);
            foreach (CombatVehicle target in encounter.Participants)
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
            encounter.Detonate(transform.position + Vector3.up * 0.16f, BlastRadius, 68f, 4800f);
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
