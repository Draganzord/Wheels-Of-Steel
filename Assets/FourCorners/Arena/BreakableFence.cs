using UnityEngine;

namespace FourCorners
{
    [DisallowMultipleComponent]
    public sealed class BreakableFence : MonoBehaviour
    {
        [SerializeField] private BoxCollider standingBlocker;
        [SerializeField] private GameObject standingPanels;
        [SerializeField] private GameObject brokenPosts;
        [SerializeField] private Rigidbody[] loosePanels;
        [SerializeField] private Rigidbody[] splinters;
        [SerializeField, Min(1f)] private float breakSpeed = 9.6f;
        [SerializeField, Min(1f)] private float referenceMass = 1350f;
        [SerializeField, Range(0f, 1f)] private float carriedSpeed = 0.78f;

        public bool IsBroken { get; private set; }
        public BoxCollider StandingBlocker => standingBlocker;

        public int ActiveDebrisCount
        {
            get
            {
                if (!IsBroken)
                    return 0;

                int count = loosePanels.Length;
                foreach (Rigidbody fragment in splinters)
                    if (fragment)
                        count++;
                return count;
            }
        }

        public void Configure(BoxCollider blocker, GameObject standing, GameObject broken,
            Rigidbody[] panels, Rigidbody[] fragments)
        {
            standingBlocker = blocker;
            standingPanels = standing;
            brokenPosts = broken;
            loosePanels = panels;
            splinters = fragments;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsBroken || !collision.rigidbody || !collision.rigidbody.TryGetComponent(out ArcadeVehicle vehicle) || vehicle.IsWreck)
                return;

            Vector3 normal = transform.forward;
            float approachSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            float massScale = Mathf.Sqrt(vehicle.Body.mass / referenceMass);
            if (approachSpeed * massScale < breakSpeed)
                return;

            Vector3 relativePosition = vehicle.Body.worldCenterOfMass - standingBlocker.bounds.center;
            Vector3 direction = Vector3.Dot(relativePosition, normal) > 0 ? -normal : normal;
            Open(direction, approachSpeed, vehicle.Body);
        }

        private void Open(Vector3 direction, float speed, Rigidbody vehicle)
        {
            IsBroken = true;
            standingBlocker.enabled = false;
            standingPanels.SetActive(false);
            brokenPosts.SetActive(true);

            float nowForward = Vector3.Dot(vehicle.linearVelocity, direction);
            vehicle.linearVelocity += direction * Mathf.Max(0, speed * carriedSpeed - nowForward);

            for (int i = 0; i < loosePanels.Length; i++)
                Throw(loosePanels[i], direction, speed, i, true);
            for (int i = 0; i < splinters.Length; i++)
                Throw(splinters[i], direction, speed, i, false);
        }

        private void Throw(Rigidbody debris, Vector3 direction, float speed, int index, bool isPanel)
        {
            float spread = Mathf.Sin(index * 2.4f + 0.7f);
            debris.transform.position += direction * 0.55f;
            debris.gameObject.SetActive(true);
            debris.linearVelocity = direction * Mathf.Min(speed * (isPanel ? 0.57f : 0.33f), 8.5f)
                + transform.right * (spread * (isPanel ? 2.2f : 4f))
                + Vector3.up * (isPanel ? 1.25f : 1.5f + Mathf.Abs(spread) * 1.8f);
            debris.angularVelocity = new Vector3(spread * 4f, Mathf.Cos(index * 2.4f) * 3f, 2.8f);
            if (!isPanel)
                Destroy(debris.gameObject, 3.5f + 0.13f * index);
        }
    }
}
