using UnityEngine;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        private static void BuildWayfinding(Transform parent)
        {
            Material timber = Lit("Wayfinding Timber", "85634A");
            Material asphalt = Lit("Wayfinding Asphalt Board", "434A44");
            Material teal = Lit("Wayfinding Creek Enamel", "375F59");
            Material cream = Lit("Wayfinding Bone Reflector", "E5D4A3");
            Material amber = Lit("Wayfinding Bank Amber", "DBA74F");
            Mesh arrow = SaveAsset(WayfindingArrow(), "asset");

            Transform fork = Group(parent, "Loop and creek fork", new Vector3(-26.6f, 0, -14.8f));
            fork.localRotation = Quaternion.Euler(0, -6, 0);
            for (int side = -1; side <= 1; side += 2)
                Box(fork, "Split sign post", new Vector3(side * 1.75f, 1.14f, 0), new Vector3(0.21f, 2.28f, 0.24f), timber);

            DirectionBoard(fork, "LOOP", 2.36f, 26f, asphalt, cream, arrow);
            DirectionBoard(fork, "CREEK", 1.19f, -72f, teal, cream, arrow);
            FloorArrow(parent, "Asphalt loop arrow", new Vector2(-34.9f, -20.7f), Vector3.forward, arrow, cream, 1.65f);
            FloorArrow(parent, "Asphalt loop confirmation", new Vector2(-35f, -17.5f), Vector3.forward, arrow, cream, 1.35f);
            Vector3 creekHeading = new Vector3(0.84f, 0, 0.54f);
            FloorArrow(parent, "Creek turn arrow", new Vector2(-23.7f, -18.1f), creekHeading, arrow, teal, 1.75f, cream);
            FloorArrow(parent, "Creek cut confirmation", new Vector2(-20.4f, -15.9f), creekHeading, arrow, teal, 1.4f, cream);
            Waypost(parent, "Ford south stake", new Vector2(7.3f, -7.8f), timber, teal, cream);
            Waypost(parent, "Ford north stake", new Vector2(8.6f, -1.4f), timber, teal, cream);

            Transform bypass = Group(parent, "Narrow bypass marking");
            Vector2 bypassCenter = new Vector2(20.35f, -8.6f);
            Text(bypass, "Approach stencil", "BYPASS", XZ(bypassCenter, 0.028f), new Vector3(90, 0, 0), 0.105f,
                new Color32(100, 63, 36, 255));
            FloorArrow(bypass, "Enter the gap", new Vector2(22.75f, -8.6f), Vector3.right, arrow, amber, 1.30f);
            FloorArrow(bypass, "Exit the gap", new Vector2(25.8f, -8.6f), Vector3.right, arrow, amber, 1.40f);

            foreach (float z in new[] { -6.34f, -10.86f })
            {
                Transform finger = Group(bypass, "Amber gap edge", new Vector3(24.0f, 0.045f, z));
                Box(finger, "Rail end ground bed", Vector3.zero, new Vector3(2.4f, 0.09f, 0.65f), asphalt);
                for (int dash = -1; dash <= 1; dash++)
                {
                    GameObject stripe = Box(finger, "Diagonal reflector", new Vector3(dash * 0.61f, 0.053f, 0),
                        new Vector3(0.28f, 0.03f, 0.6f), amber);
                    stripe.transform.localRotation = Quaternion.Euler(0, 25, 0);
                }
                Box(finger, "Capped gate post", new Vector3(0.19f, 1.45f, 0),
                    new Vector3(0.28f, 0.12f, 0.28f), cream);
            }
        }

        private static void DirectionBoard(Transform parent, string label, float height, float turn,
            Material panel, Material cream, Mesh arrow)
        {
            Transform face = Group(parent, label + " panel", new Vector3(0, height, -0.1f));
            face.localRotation = Quaternion.Euler(26, 0, 0);
            Box(face, "Painted plank edge", Vector3.zero, new Vector3(4.42f, 0.93f, 0.18f), cream);
            Box(face, "Enamel sign face", new Vector3(0, 0, -0.104f), new Vector3(4.24f, 0.77f, 0.065f), panel);
            Text(face, label + " lettering", label, new Vector3(-0.34f, 0, -0.146f), Vector3.zero,
                0.11f, new Color32(248, 231, 189, 255));
            Quaternion pointing = Quaternion.AngleAxis(turn, Vector3.forward) * Quaternion.Euler(-90, 0, 0);
            MeshShape(face, "Direction arrow", arrow, new Vector3(1.51f, 0, -0.146f),
                Vector3.one * 0.44f, pointing, cream);
        }

        private static void FloorArrow(Transform parent, string name, Vector2 location, Vector3 direction,
            Mesh arrow, Material color, float size, Material border = null)
        {
            float height = GroundHeight(location);
            Quaternion heading = Quaternion.LookRotation(direction);
            if (border)
                MeshShape(parent, name + " outline", arrow, XZ(location, height + 0.015f),
                    Vector3.one * (size * 1.21f), heading, border);
            MeshShape(parent, name, arrow, XZ(location, height + 0.03f), Vector3.one * size, heading, color);
        }

        private static void Waypost(Transform parent, string name, Vector2 location,
            Material timber, Material teal, Material cream)
        {
            Transform post = Group(parent, name, XZ(location, GroundHeight(location)));
            Box(post, "Post shank", new Vector3(0, 0.53f, 0), new Vector3(0.19f, 1.07f, 0.20f), timber);
            Box(post, "Blue enamel sleeve", new Vector3(0, 0.81f, 0), new Vector3(0.31f, 0.35f, 0.32f), teal);
            Box(post, "Pale reflector cap", new Vector3(0, 1.0f, 0), new Vector3(0.40f, 0.10f, 0.41f), cream);
        }

        private static Mesh WayfindingArrow()
        {
            var mesh = new Mesh { name = "Painted Route Arrow" };
            mesh.vertices = new[]
            {
                new Vector3(-0.16f, 0, -0.67f), new Vector3(-0.16f, 0, 0.15f),
                new Vector3(0.16f, 0, 0.15f), new Vector3(0.16f, 0, -0.67f),
                new Vector3(-0.50f, 0, 0.15f), new Vector3(0, 0, 0.85f), new Vector3(0.50f, 0, 0.15f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
