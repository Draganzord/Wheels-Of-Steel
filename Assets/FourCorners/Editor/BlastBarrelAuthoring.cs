using UnityEditor;
using UnityEngine;

namespace FourCorners.Editor
{
    internal static class BlastBarrelAuthoring
    {
        private static readonly Vector2[] Positions =
        {
            new(-17, -22), new(-14.65f, -22.2f), new(-12.15f, -22),
            new(34.5f, -18.5f), new(32.2f, -18.8f),
            new(8.5f, 29), new(11.0f, 29.25f)
        };

        public static BlastBarrel[] AddBarrels(Transform arena, SalvageEncounter encounter)
        {
            GameObject prefab = CreatePrefab();
            GameObject parent = new("Explosive powder drums");
            parent.transform.SetParent(arena, false);
            BlastBarrel[] barrels = new BlastBarrel[Positions.Length];
            for (int index = 0; index < Positions.Length; index++)
            {
                Vector2 p = Positions[index];
                var drum = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                drum.name = $"Powder Drum {index + 1}";
                drum.transform.position = new Vector3(p.x, 0.02f, p.y);
                barrels[index] = drum.GetComponent<BlastBarrel>();
                barrels[index].Configure(encounter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(drum.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(barrels[index]);
            }
            return barrels;
        }

        private static GameObject CreatePrefab()
        {
            const string path = "Assets/FourCorners/Encounter/Generated/Powder Drum.prefab";
            GameObject root = new("Powder Drum");
            Material body = SliceBuilder.MakeMaterial("Powder Drum Red", "Universal Render Pipeline/Lit", new Color(0.61f, 0.085f, 0.036f));
            Material rim = SliceBuilder.MakeMaterial("Powder Drum Graphite", "Universal Render Pipeline/Lit", new Color(0.19f, 0.174f, 0.13f));
            Material stripe = SliceBuilder.MakeMaterial("Powder Drum Band", "Universal Render Pipeline/Lit", new Color(0.99f, 0.65f, 0.15f));
            Cylinder(root.transform, "Steel shell", 0.58f, 0.91f, 0.56f, body);
            Cylinder(root.transform, "Hazard band", 0.62f, 0.93f, 0.13f, stripe);
            Cylinder(root.transform, "Bottom ring", 0.15f, 0.99f, 0.06f, rim);
            Cylinder(root.transform, "Drum lid", 1.10f, 0.99f, 0.055f, rim);
            Cylinder(root.transform, "Filler cap", 1.18f, 0.36f, 0.026f, stripe);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.57f, 0);
            collider.size = new Vector3(0.89f, 1.13f, 0.89f);
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 86;
            rigidbody.linearDamping = 0.35f;
            rigidbody.angularDamping = 0.7f;
            root.AddComponent<BlastBarrel>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Cylinder(Transform parent, string name, float y, float diameter, float halfHeight, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = new Vector3(0, y, 0);
            piece.transform.localScale = new Vector3(diameter, halfHeight, diameter);
            piece.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
        }
    }
}
