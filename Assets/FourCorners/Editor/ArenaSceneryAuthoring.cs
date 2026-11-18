using UnityEditor;
using UnityEngine;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        private static void BuildStation(Transform station)
        {
            Material plaster = Lit("Gas Station Whitewash", "D8C9A7");
            Material turquoise = Lit("Gas Station Turquoise", "4E8A82", 0.33f);
            Material cream = Lit("Gas Station Trim", "EADABD");
            Material crimson = Lit("Gas Station Oxide Red", "A43F2C", 0.34f);
            Material concrete = Lit("Gas Station Concrete", "AA9B88", 0.26f);
            Material charcoal = Lit("Gas Station Dark Metal", "393C3D", 0.3f);
            Material glass = Lit("Gas Station Dark Glass", "31464A", 0.85f);
            Material yellow = Lit("Gas Station Safety Yellow", "D5A247", 0.3f);

            Box(station, "Cinderblock service station", new(1.4f, 2.21f, 15), new(18.8f, 4.42f, 9.4f), plaster, true);
            Box(station, "Weathered turquoise skirt", new(1.4f, 0.52f, 15), new(18.86f, 1.04f, 9.45f), turquoise);
            Box(station, "Roof slab", new(1.4f, 4.45f, 15), new(19.5f, 0.28f, 10.0f), concrete);
            Box(station, "Parapet color band", new(1.4f, 4.44f, 15), new(19.67f, 0.26f, 10.17f), crimson);
            Box(station, "Roof coping", new(1.4f, 4.65f, 15), new(19.72f, 0.18f, 10.22f), plaster);
            Box(station, "Flat roof cap", new(1.4f, 4.755f, 15), new(18.9f, 0.045f, 9.4f), concrete);
            Text(station, "Roof lettering", "FOUR CORNERS", new(1.25f, 4.81f, 16.2f), new(90, 0, 0), 0.26f, new Color32(81, 54, 39, 255));
            Text(station, "Roof lettering lower", "S A L V A G E   Y A R D", new(1.25f, 4.812f, 13.8f), new(90, 0, 0), 0.145f, new Color32(115, 47, 33, 255));
            Box(station, "Service sidewalk", new(1.4f, 0.09f, 9.55f), new(19.4f, 0.18f, 1.8f), cream);
            for (int i = 0; i < 12; i++)
                Box(station, "Sidewalk joint", new(-7.4f + i * 1.6f, 0.182f, 9.53f), new(0.03f, 0.01f, 1.68f), concrete);

            Box(station, "Office window frame", new(-3.2f, 2.08f, 10.24f), new(6.5f, 1.88f, 0.13f), charcoal);
            for (int i = 0; i < 4; i++)
            {
                Box(station, "Office pane", new(-5.6f + i * 1.6f, 2.09f, 10.155f), new(1.44f, 1.57f, 0.1f), glass);
                Box(station, "High pane reflection", new(-5.6f + i * 1.6f, 2.61f, 10.099f), new(1.42f, 0.11f, 0.011f), turquoise);
            }
            Box(station, "Office door", new(1.2f, 1.39f, 10.21f), new(1.45f, 2.42f, 0.23f), crimson);
            Box(station, "Door window", new(1.2f, 1.9f, 10.077f), new(1.10f, 1.08f, 0.10f), glass);
            Box(station, "Office door handle", new(1.72f, 1.20f, 10.013f), new(0.22f, 0.1f, 0.11f), yellow);
            Box(station, "Service bay frame", new(6.9f, 1.78f, 10.23f), new(6.6f, 3.25f, 0.22f), charcoal);
            Material rollUp = Lit("Roll-up Door Steel", "7A7D73", 0.46f);
            for (int i = 0; i < 13; i++)
                Box(station, "Rolled steel slat", new(6.9f, 0.37f + i * 0.225f, 10.09f), new(6.05f, 0.19f, 0.16f), rollUp);

            Box(station, "Canopy slab", new(-0.4f, 3.97f, 6.69f), new(20.2f, 0.48f, 6.1f), crimson);
            Box(station, "Ivory canopy fascia", new(-0.4f, 3.89f, 6.65f), new(20.27f, 0.13f, 6.24f), cream);
            Box(station, "Canopy coping", new(-0.4f, 4.22f, 6.67f), new(20.33f, 0.12f, 6.15f), cream);
            Text(station, "Canopy gas sign", "G A S", new(-0.4f, 4.027f, 3.51f), Vector3.zero, 0.108f, new Color32(250, 228, 168, 255));
            for (int i = 0; i < 3; i++)
            {
                float x = -7.65f + i * 7.25f;
                Box(station, "Canopy column", new(x, 1.94f, 6.44f), new(0.44f, 3.86f, 0.44f), turquoise, true);
                Box(station, "Canopy column base", new(x, 0.25f, 6.44f), new(0.68f, 0.48f, 0.68f), concrete);
                Pump(station, new(x + 2.35f, 0, 5.42f), cream, crimson, charcoal, yellow);
            }

            Box(station, "Rooftop AC unit", new(8.5f, 5.09f, 18.0f), new(1.9f, 0.71f, 1.35f), rollUp);
            for (int i = 0; i < 7; i++)
                Box(station, "Rooftop AC grille", new(7.83f + i * 0.22f, 5.45f, 18.0f), new(0.085f, 0.055f, 0.98f), charcoal);
            Cylinder(station, "Vent duct", new(-6.5f, 5.12f, 17.5f), 0.43f, 0.76f, rollUp);
            Cylinder(station, "Vent cap", new(-6.5f, 5.54f, 17.5f), 0.74f, 0.18f, charcoal);
        }

        private static void Pump(Transform parent, Vector3 position, Material cream, Material crimson,
            Material dark, Material yellow)
        {
            Transform island = Group(parent, "Vintage fuel pump", position);
            Box(island, "Pump island bumper", new(0, 0.12f, 0), new(3.1f, 0.25f, 1.58f), cream, true);
            Box(island, "Pump body", new(0, 1.04f, 0), new(0.90f, 1.79f, 0.54f), crimson, true);
            Box(island, "Pump meter", new(0, 1.37f, -0.30f), new(0.63f, 0.63f, 0.11f), cream);
            Box(island, "Pump readout", new(0, 1.48f, -0.36f), new(0.43f, 0.20f, 0.04f), dark);
            Cylinder(island, "Pump cap lamp", new(0, 2.0f, 0), 0.45f, 0.18f, cream);
            Cylinder(island, "Pump bollard", new(-1.15f, 0.69f, -0.40f), 0.19f, 1.15f, yellow, true);
            Cylinder(island, "Pump bollard", new(1.15f, 0.69f, -0.40f), 0.19f, 1.15f, yellow, true);
        }

        private static void BuildSalvage(Transform parent)
        {
            Material rust = Lit("Inventory Rust Red", "8C4A39", 0.34f);
            Material iron = Lit("Inventory Gunmetal", "474642");
            Material tyre = Lit("Scrap Tire Rubber", "2B2928", 0.36f);
            Material oxblood = Lit("Inventory Oxblood", "613D33");
            Material cream = Lit("Inventory Painted Ivory", "D6BB87");
            Material pale = Lit("Inventory Dust Blue", "66818B", 0.31f);
            Material dark = Lit("Inventory Deep Shade", "373A35");

            Transform forklift = Group(parent, "Solid obstacle | Stacked scrap at fork", new(-17, 0, 1));
            Box(forklift, "Rack collision and base", new(0, 1.25f, 0), new(11.4f, 2.5f, 6.6f), iron, true);
            Box(forklift, "Rack second shelf", new(0, 2.55f, 0), new(11.7f, 0.26f, 6.7f), oxblood);
            for (int i = 0; i < 4; i++)
            {
                float x = -5.55f + i * 3.7f;
                Box(forklift, "Rack upright", new(x, 1.78f, -3.16f), new(0.22f, 3.56f, 0.24f), rust);
                Box(forklift, "Rack slot", new(x + 1.8f, 1.25f, -3.33f), new(3.3f, 1.72f, 0.11f), dark);
                if (i < 3)
                {
                    Shell(forklift, new(x + 1.8f, 1.24f, -3.25f), i % 2 == 0 ? rust : pale, tyre, cream);
                    Shell(forklift, new(x + 1.8f, 3.06f, 0.13f), i % 2 == 1 ? rust : pale, tyre, cream);
                }
            }
            Box(forklift, "Scrap rack warning placard", new(0, 2.51f, -3.47f), new(7.6f, 0.38f, 0.12f), cream);
            Text(forklift, "Scrap rack warning", "S C R A P   I N V E N T O R Y", new(0, 2.52f, -3.54f), Vector3.zero, 0.065f, new Color32(73, 44, 37, 255));

            for (int i = 0; i < 6; i++)
            {
                Vector3 position = i < 3
                    ? new Vector3(-23.7f + i * 1.05f, 0, -3.88f)
                    : new Vector3(24.9f + (i - 3) * 1.02f, 0, -12.38f);
                Barrel(parent, position, i % 2 == 0 ? rust : pale, iron);
            }

            for (int i = 0; i < 3; i++)
            {
                Vector3 tirePosition = new(-7.9f, 0.54f + 0.37f * i, 9.74f);
                Cylinder(parent, "Used tire", tirePosition, 0.91f, 0.32f, tyre);
                Cylinder(parent, "Tire recess", tirePosition + Vector3.up * 0.17f, 0.5f, 0.03f, dark);
            }

            Transform sign = Group(parent, "Roadside Four Corners marque", new(-14.1f, 0, 10.1f));
            Box(sign, "Marque sign post", new(-2.24f, 1.37f, 0), new(0.24f, 2.73f, 0.24f), iron, true);
            Box(sign, "Marque sign post", new(2.24f, 1.37f, 0), new(0.24f, 2.73f, 0.24f), iron, true);
            Box(sign, "Marque board", new(0, 3.12f, 0), new(6.7f, 2.25f, 0.22f), rust);
            Box(sign, "Marque stripe", new(0, 2.45f, -0.125f), new(6.65f, 0.58f, 0.08f), cream);
            Text(sign, "Marque title", "FOUR CORNERS", new(0, 3.45f, -0.128f), Vector3.zero, 0.15f, new Color32(255, 226, 183, 255));
            Text(sign, "Marque subtitle", "SALVAGE YARD", new(0, 2.46f, -0.184f), Vector3.zero, 0.076f, new Color32(74, 46, 40, 255));
        }

        private static void Shell(Transform parent, Vector3 position, Material body, Material rubber, Material plate)
        {
            Transform shell = Group(parent, "Crushed inventory car", position);
            Box(shell, "Chassis shell", Vector3.zero, new(3.28f, 0.52f, 1.54f), body);
            Box(shell, "Cab shell", new(-0.22f, 0.39f, 0), new(1.36f, 0.47f, 1.27f), body);
            Box(shell, "Window cavity", new(-0.22f, 0.4f, -0.64f), new(1.1f, 0.27f, 0.035f), rubber);
            Box(shell, "Bumper", new(1.59f, -0.10f, -0.03f), new(0.18f, 0.12f, 1.52f), plate);
            for (int i = 0; i < 2; i++)
            {
                GameObject wheel = Cylinder(shell, "Torn tire", new(-0.96f + i * 2.05f, -0.24f, -0.72f), 0.57f, 0.16f, rubber);
                wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
        }

        private static void Barrel(Transform parent, Vector3 position, Material paint, Material dark)
        {
            Transform root = Group(parent, "Light prop | Loose oil drum", position + Vector3.up * 0.48f);
            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.7f, 0.92f, 0.7f);
            Rigidbody body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = 21f;
            body.linearDamping = 0.17f;
            body.angularDamping = 0.48f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Cylinder(root, "Drum shell", Vector3.zero, 0.72f, 0.89f, paint);
            for (int i = 0; i < 4; i++)
                Cylinder(root, "Drum rib", new(0, -0.42f + i * 0.28f, 0), 0.755f, 0.064f, dark);
            Cylinder(root, "Bung", new(0.16f, 0.46f, 0.06f), 0.15f, 0.036f, dark);
        }

        private static BreakableFence BuildBreach(Transform parent)
        {
            Material cedar = Lit("Fence Sun-Bleached Cedar", "8B6650");
            Material face = Lit("Fence Aged Corrugation", "83847A", 0.31f);
            Material rust = Lit("Fence Rust Corrugation", "8D4B38", 0.38f);
            Material warning = Lit("Fence Warning Amber", "D1A456", 0.22f);
            Material steel = Lit("Fence Steel Frame", "454943");
            Transform fence = Group(parent, "Bank gate | Breakable", new(24, 0, -3));
            fence.localRotation = Quaternion.Euler(0, 90, 0);
            var blocker = fence.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, 0.9f, 0);
            blocker.size = new Vector3(5.75f, 1.8f, 0.23f);

            Transform standing = Group(fence, "Standing panels");
            Transform broken = Group(fence, "Authored broken posts");
            for (int side = -1; side <= 1; side += 2)
            {
                Box(standing, "Standing edge post", new(side * 2.95f, 1.0f, 0), new(0.27f, 1.99f, 0.3f), cedar);
                Box(broken, "Snapped post stump", new(side * 2.95f, 0.27f, 0), new(0.28f, 0.52f, 0.32f), cedar);
                Beam(broken, "Cracked post remnant", new(side * 2.95f, 0.2f, -0.045f),
                    new(side * 3.055f, 0.74f, 0.15f), 0.15f, 0.14f, cedar);
            }

            var panels = new Rigidbody[2];
            for (int i = 0; i < panels.Length; i++)
            {
                float center = -1.39f + i * 2.78f;
                Transform visible = Group(standing, "Intact gate leaf", new(center, 0.89f, 0));
                Corrugated(visible, 2.77f, 1.57f, face, rust, steel);
                Transform detached = Group(fence, "Ejected gate leaf", new(center, 0.95f, 0.07f));
                Corrugated(detached, 2.72f, 1.5f, face, rust, steel);
                panels[i] = DebrisBody(detached, new(2.72f, 1.5f, 0.15f), 19f);
            }
            Text(standing, "Gate caution stencil", "KEEP CLEAR", new(0, 1.12f, -0.135f), Vector3.zero, 0.09f, new Color32(254, 219, 125, 255));
            Box(standing, "Gate latch", new(0, 0.87f, -0.15f), new(0.50f, 0.17f, 0.16f), warning);

            var splinters = new Rigidbody[8];
            for (int i = 0; i < splinters.Length; i++)
            {
                float x = Mathf.Lerp(-2.7f, 2.7f, (float)i / (splinters.Length - 1));
                Vector3 size = new(0.08f + (i % 3) * 0.047f, 0.28f + (i % 3) * 0.13f, 0.089f);
                Transform fragment = Group(fence, "Capped wood and corrugation debris", new(x, 0.74f + (i % 3) * 0.2f, -0.1f));
                fragment.localRotation = Quaternion.Euler(i * 23, i * 12, 37);
                Box(fragment, "Loose fragment", Vector3.zero, size, i % 2 == 0 ? cedar : rust);
                splinters[i] = DebrisBody(fragment, size, 0.65f);
            }
            broken.gameObject.SetActive(false);
            BreakableFence result = fence.gameObject.AddComponent<BreakableFence>();
            result.Configure(blocker, standing.gameObject, broken.gameObject, panels, splinters);
            PrefabUtility.SaveAsPrefabAssetAndConnect(fence.gameObject, $"{ArtPath}/Bank Gate.prefab", InteractionMode.AutomatedAction);

            Transform edge = Group(parent, "Fixed fence and narrow south bypass", new(24, 0, -3));
            edge.localRotation = fence.localRotation;
            BarrierBay(edge, -5.94f, 5.78f, face, rust, steel);
            BarrierBay(edge, -10.26f, 2.42f, face, rust, steel);
            BarrierBay(edge, 10.42f, 4.45f, face, rust, steel);
            Box(edge, "Exit bypass marker", new(7.86f, 0.72f, 0.19f), new(0.22f, 1.44f, 0.22f), warning, true);
            Box(edge, "Exit bypass marker", new(3.34f, 0.66f, 0.19f), new(0.22f, 1.32f, 0.22f), warning, true);
            return result;
        }

        private static void Corrugated(Transform parent, float width, float height, Material face, Material rust, Material steel)
        {
            int ribs = Mathf.CeilToInt(width / 0.205f);
            float stride = width / ribs;
            for (int rib = 0; rib < ribs; rib++)
            {
                float x = -width * 0.5f + (rib + 0.5f) * stride;
                Box(parent, "Corrugated rib", new(x, 0, rib % 2 * 0.018f), new(stride * 0.98f, height, 0.093f),
                    rib % 6 == 2 || rib % 6 == 3 ? rust : face);
            }
            Box(parent, "Lower brace", new(0, -height * 0.36f, 0.04f), new(width, 0.09f, 0.11f), steel);
            Box(parent, "Upper brace", new(0, height * 0.36f, 0.04f), new(width, 0.09f, 0.11f), steel);
        }

        private static void BarrierBay(Transform parent, float x, float width, Material face, Material rust, Material steel)
        {
            Transform bay = Group(parent, "Solid fence return", new(x, 0.86f, 0));
            BoxCollider collider = bay.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, 1.72f, 0.29f);
            Corrugated(bay, width, 1.64f, face, rust, steel);
        }

        private static Rigidbody DebrisBody(Transform parent, Vector3 size, float mass)
        {
            parent.gameObject.AddComponent<BoxCollider>().size = size;
            Rigidbody body = parent.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.angularDamping = 0.45f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            parent.gameObject.SetActive(false);
            return body;
        }

        private static void Text(Transform parent, string name, string label, Vector3 position,
            Vector3 rotation, float size, Color color)
        {
            Transform item = Group(parent, name, position);
            item.localRotation = Quaternion.Euler(rotation);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TextMesh text = item.gameObject.AddComponent<TextMesh>();
            text.font = font;
            text.text = label;
            text.fontSize = 62;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            item.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
