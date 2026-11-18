using UnityEngine;

namespace FourCorners
{
    public sealed class SurfaceZone : MonoBehaviour
    {
        [SerializeField] private SurfaceProfile profile;
        [SerializeField] private float waterHeight;

        public void Configure(SurfaceProfile surface, float waterLevel = 0f)
        {
            profile = surface;
            waterHeight = waterLevel;
        }

        public static SurfaceProfile Resolve(Collider hitCollider, Vector3 point, out float surfaceHeight)
        {
            surfaceHeight = point.y;
            SurfaceZone zone = hitCollider != null ? hitCollider.GetComponentInParent<SurfaceZone>() : null;
            if (zone == null || zone.profile == null)
                return null;

            if (zone.profile.Kind == SurfaceKind.Water)
                surfaceHeight = zone.waterHeight;
            return zone.profile;
        }
    }
}
