using UnityEngine;

namespace FourCorners
{
    [RequireComponent(typeof(Camera))]
    public sealed class YardCamera : MonoBehaviour
    {
        [SerializeField] private ArcadeVehicle target;
        [SerializeField] private Vector3 viewAngles = new(57, -10, 0);
        [SerializeField] private Vector3 framingBias = new(3.3f, 0, 3.5f);
        [SerializeField, Min(1)] private float viewDistance = 38;
        [SerializeField, Min(0.01f)] private float followTime = 0.17f;
        [SerializeField, Min(0)] private float lookAheadTime = 0.32f;
        [SerializeField, Min(0)] private float maxLookAhead = 6;

        private Vector3 focus;
        private Vector3 followVelocity;

        public void Configure(ArcadeVehicle vehicle)
        {
            target = vehicle;
            focus = target.transform.position + framingBias;
            focus.y = 0;
            PositionCamera();
        }

        private void Start() => Configure(target);

        private void LateUpdate()
        {
            Vector3 velocity = target.Body.linearVelocity;
            velocity.y = 0;
            Vector3 ahead = Vector3.ClampMagnitude(velocity * lookAheadTime, maxLookAhead);
            Vector3 aim = target.transform.position + framingBias + ahead;
            aim.y = 0;
            focus = Vector3.SmoothDamp(focus, aim, ref followVelocity, followTime);
            PositionCamera();
        }

        private void PositionCamera()
        {
            Quaternion rotation = Quaternion.Euler(viewAngles);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * viewDistance, rotation);
        }
    }
}
