using System.Collections.Generic;
using UnityEngine;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        public static void ConfigureDestruction(ArenaLayout layout)
        {
            Transform ground = layout.transform.Find("00 | Roadbed, creek and surfaces");
            var dryGround = new List<MeshCollider>();
            foreach (MeshCollider tile in ground.GetComponentsInChildren<MeshCollider>())
            {
                SurfaceProfile profile = SurfaceZone.Resolve(tile, Vector3.zero, out _);
                if (profile && profile.Kind != SurfaceKind.Water)
                    dryGround.Add(tile);
            }

            var exclusions = new List<Bounds>();
            const float waterStep = 1.2f;
            for (float z = CreekSouth - waterStep; z < CreekNorth + waterStep; z += waterStep)
            {
                float x0 = CreekCenter(z);
                float x1 = CreekCenter(z + waterStep);
                const float margin = 0.95f;
                exclusions.Add(new Bounds(new Vector3((x0 + x1) * 0.5f, 0, z + waterStep * 0.5f),
                    new Vector3(Mathf.Abs(x0 - x1) + 2 * (CreekHalfWidth + margin), 1, waterStep)));
            }

            Physics.SyncTransforms();
            foreach (Transform ramp in layout.Ramps)
            {
                var deck = ramp.GetComponentInChildren<MeshCollider>();
                Bounds bounds = deck.bounds;
                bounds.Encapsulate(ramp.position - ramp.forward * 9f);
                bounds.Encapsulate(ramp.Find("Runout").position + ramp.forward * 6f);
                bounds.Expand(new Vector3(2.4f, 0, 0));
                exclusions.Add(bounds);
            }

            foreach (BoxCollider foundation in layout.GetComponentsInChildren<BoxCollider>())
            {
                if (!foundation.enabled || foundation.attachedRigidbody)
                    continue;

                Bounds bounds = foundation.bounds;
                bounds.Expand(new Vector3(0.85f, 0, 0.85f));
                exclusions.Add(bounds);
            }

            if (!layout.TryGetComponent(out DestructibleTerrain destructible))
                destructible = layout.gameObject.AddComponent<DestructibleTerrain>();
            var soil = new Material(Shader.Find("Four Corners/Disturbed Soil")) { name = "Crater Soil Overlay" };
            destructible.Configure(dryGround.ToArray(), exclusions.ToArray(),
                new Bounds(new Vector3(0.5f, 0, -0.5f), new Vector3(106f, 1f, 82f)), SaveAsset(soil, "mat"),
                ground.Find("Painted lane markings"));
        }
    }
}
