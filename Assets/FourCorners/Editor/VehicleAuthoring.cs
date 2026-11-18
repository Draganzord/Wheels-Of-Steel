using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FourCorners.Editor
{
    public static class VehicleAuthoring
    {
        private const string AssetRoot = "Assets/FourCorners/Vehicles";
        private const string TuningRoot = AssetRoot + "/Tuning";
        private const string ArtRoot = AssetRoot + "/Art";

        private readonly struct Section
        {
            public readonly float Z, Width, Bottom, Top;

            public Section(float z, float width, float bottom, float top)
            {
                Z = z;
                Width = width;
                Bottom = bottom;
                Top = top;
            }

            public Vector3[] Outline()
            {
                float bevel = Mathf.Min(0.15f, (Top - Bottom) * 0.3f);
                return new[]
                {
                    new Vector3(-Width + bevel, Bottom, Z), new Vector3(Width - bevel, Bottom, Z),
                    new Vector3(Width, Bottom + bevel, Z), new Vector3(Width, Top - bevel, Z),
                    new Vector3(Width - bevel, Top, Z), new Vector3(-Width + bevel, Top, Z),
                    new Vector3(-Width, Top - bevel, Z), new Vector3(-Width, Bottom + bevel, Z)
                };
            }
        }

        public static GameObject CreatePrefab(bool truck)
        {
            EnsureFolder(TuningRoot);
            EnsureFolder(ArtRoot);
            string name = truck ? "TowTruck" : "MuscleCar";
            VehicleSettings settings = GetSettings(name, truck);
            var root = new GameObject(name);
            var body = root.AddComponent<Rigidbody>();
            body.mass = settings.Mass;
            body.linearDamping = truck ? 0.055f : 0.045f;
            body.angularDamping = truck ? 1.17f : 1.02f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var hull = root.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, 0.12f, truck ? -0.04f : 0f);
            hull.size = truck ? new Vector3(1.98f, 0.87f, 4.84f) : new Vector3(1.73f, 0.64f, 3.92f);
            hull.sharedMaterial = ChassisContact();
            var bumpers = new Collider[2];
            for (int i = 0; i < bumpers.Length; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var bumper = new GameObject(i == 0 ? "Rear impact rail" : "Front impact rail");
                bumper.transform.SetParent(root.transform, false);
                bumper.transform.localPosition = new Vector3(0f, -0.035f, sign * (truck ? 2.55f : 2.06f));
                var collider = bumper.AddComponent<BoxCollider>();
                collider.size = truck ? new Vector3(2.28f, 0.53f, 0.52f) : new Vector3(1.82f, 0.34f, 0.32f);
                collider.sharedMaterial = hull.sharedMaterial;
                bumpers[i] = collider;
            }

            var paintedParts = new List<Renderer>();
            Transform damagePanel = truck ? ModelTruck(root.transform, paintedParts) : ModelCar(root.transform, paintedParts);
            var wheels = new ArcadeVehicle.WheelMount[4];
            for (int i = 0; i < wheels.Length; i++)
                wheels[i] = Wheel(root.transform, settings, truck, i);

            root.AddComponent<ArcadeVehicle>().Configure(settings, wheels, bumpers, paintedParts.ToArray(), damagePanel);
            string path = $"{AssetRoot}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static VehicleSettings GetSettings(string name, bool truck)
        {
            string path = $"{TuningRoot}/{name}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<VehicleSettings>(path);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            if (truck)
            {
                settings.Mass = 2880f;
                settings.CenterOfMass = new Vector3(0f, -0.04f, -0.08f);
                settings.AntiRoll = 7800f;
                settings.WreckHeight = 1.72f;
                settings.WheelRadius = 0.53f;
                settings.RestLength = 0.58f;
                settings.Extension = 0.22f;
                settings.Spring = 57000f;
                settings.Damper = 4800f;
                settings.ReboundDamper = 5600f;
                settings.DriveForce = 27600f;
                settings.TopSpeed = 21.5f;
                settings.ReverseSpeed = 5.8f;
                settings.FrontDriveBias = 0.30f;
                settings.BrakeForce = 32000f;
                settings.HandbrakeForce = 6600f;
                settings.LowSpeedSteer = 37f;
                settings.HighSpeedSteer = 17f;
                settings.SteerRate = 205f;
                settings.LateralStiffness = 2650f;
                settings.TireGrip = 1.02f;
                settings.SlidingGrip = 0.82f;
                settings.HandbrakeRearGrip = 0.55f;
                settings.HandbrakeEngageRate = 6f;
                settings.HandbrakeReleaseRate = 6.6f;
                settings.GravityScale = 1.42f;
                settings.AirLevelStrength = 20f;
                settings.AirLevelDamping = 8f;
                settings.RecoveryDelay = 1.15f;
            }

            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        private static PhysicsMaterial ChassisContact()
        {
            string path = $"{ArtRoot}/ChassisContact.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material != null)
                return material;

            material = new PhysicsMaterial("Chassis Contact")
            {
                dynamicFriction = 0.26f,
                staticFriction = 0.35f,
                bounciness = 0.03f
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform ModelCar(Transform root, List<Renderer> painted)
        {
            var cobalt = Paint("Cobalt lacquer", 0x2457AF, 0.27f, 0.57f);
            var blueTrim = Paint("Blue trim", 0x5478BD, 0.21f, 0.46f);
            var ivory = Paint("Ivory racing stripes", 0xEBD8A6, 0.12f, 0.46f);
            var glass = Paint("Smoked glass", 0x263D4D, 0.36f, 0.76f);
            var dark = Paint("Dark steel", 0x29282A, 0.53f, 0.34f);
            var chrome = Paint("Brushed alloy", 0xA4AAA8, 0.75f, 0.65f);
            var lamp = Paint("Warm headlamps", 0xFFE19E, 0.11f, 0.58f, true);
            var tail = Paint("Red lenses", 0xDB3B27, 0.13f, 0.52f, true);

            painted.Add(Loft(root, "Muscle car body", cobalt, new Section(-2.11f, 0.79f, -0.12f, 0.40f),
                new Section(-1.69f, 0.92f, -0.24f, 0.46f), new Section(-0.60f, 0.86f, -0.30f, 0.43f),
                new Section(0.70f, 0.88f, -0.30f, 0.46f), new Section(1.67f, 0.92f, -0.22f, 0.48f),
                new Section(2.12f, 0.79f, -0.10f, 0.35f)).GetComponent<Renderer>());
            Loft(root, "Fastback windows", glass, new Section(-1.43f, 0.74f, 0.40f, 0.50f),
                new Section(-0.63f, 0.62f, 0.45f, 0.95f), new Section(0.23f, 0.61f, 0.46f, 0.95f),
                new Section(0.90f, 0.73f, 0.43f, 0.53f));
            painted.Add(Loft(root, "Roof cap", cobalt, new Section(-0.70f, 0.61f, 0.92f, 0.94f),
                new Section(-0.50f, 0.63f, 0.93f, 0.99f), new Section(0.14f, 0.62f, 0.93f, 0.99f),
                new Section(0.31f, 0.60f, 0.91f, 0.94f)).GetComponent<Renderer>());

            var hood = new GameObject("Hood and hardware").transform;
            hood.SetParent(root, false);
            hood.localPosition = new Vector3(0f, 0.48f, 1.31f);
            painted.Add(Box(hood, "Raised hood", Vector3.zero, new Vector3(1.24f, 0.065f, 1.02f), cobalt).GetComponent<Renderer>());
            foreach (float side in new[] { -1f, 1f })
            {
                float stripe = side * 0.166f;
                Box(hood, "Hood stripe", new Vector3(stripe, 0.039f, 0.019f), new Vector3(0.205f, 0.012f, 1.005f), ivory);
                Box(root, "Roof stripe", new Vector3(stripe, 0.995f, -0.17f), new Vector3(0.205f, 0.012f, 0.70f), ivory);
                Box(root, "Deck stripe", new Vector3(stripe, 0.467f, -1.72f), new Vector3(0.205f, 0.014f, 0.43f), ivory);
                Box(root, "Window pillar", new Vector3(side * 0.638f, 0.70f, -0.46f), new Vector3(0.045f, 0.46f, 0.090f), cobalt);
                Box(root, "Mirror arms", new Vector3(side * 0.925f, 0.54f, 0.61f), new Vector3(0.18f, 0.08f, 0.17f), blueTrim);
                Box(root, "Rockers", new Vector3(side * 0.868f, -0.18f, 0f), new Vector3(0.10f, 0.12f, 1.95f), dark);
                Box(root, "Tail lens", new Vector3(side * 0.55f, 0.22f, -2.118f), new Vector3(0.37f, 0.12f, 0.045f), tail);
                Box(root, "Side beltline", new Vector3(side * 0.876f, 0.23f, -0.07f), new Vector3(0.040f, 0.05f, 2.01f), blueTrim);
            }

            Box(root, "Inset grille", new Vector3(0f, 0.15f, 2.12f), new Vector3(1.39f, 0.325f, 0.075f), dark);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int pair = 0; pair < 2; pair++)
                    Cylinder(root, "Round headlamp", new Vector3(i * (0.48f + pair * 0.165f), 0.21f, 2.17f),
                        0.135f, 0.055f, new Vector3(90f, 0f, 0f), lamp);
            }
            Box(root, "Front steel rail", new Vector3(0f, -0.047f, 2.18f), new Vector3(1.74f, 0.104f, 0.167f), chrome);
            Box(root, "Rear steel rail", new Vector3(0f, -0.047f, -2.14f), new Vector3(1.74f, 0.101f, 0.14f), chrome);
            Box(hood, "Gun saddle", new Vector3(0f, 0.105f, 0.19f), new Vector3(0.34f, 0.19f, 0.35f), dark);
            Box(hood, "Gun shroud", new Vector3(0f, 0.221f, 0.24f), new Vector3(0.205f, 0.15f, 0.36f), dark);
            Cylinder(hood, "Gun muzzle silhouette", new Vector3(0f, 0.224f, 0.455f), 0.09f, 0.23f,
                new Vector3(90f, 0f, 0f), chrome);
            return hood;
        }

        private static Transform ModelTruck(Transform root, List<Renderer> painted)
        {
            var rust = Paint("Tow oxide red", 0x973D28, 0.26f, 0.31f);
            var edge = Paint("Tow worn edges", 0xC87143, 0.27f, 0.4f);
            var boom = Paint("Crane ochre", 0xD09527, 0.31f, 0.35f);
            var dark = Paint("Dark steel", 0x29282A, 0.53f, 0.34f);
            var chrome = Paint("Brushed alloy", 0xA4AAA8, 0.75f, 0.65f);
            var glass = Paint("Smoked glass", 0x263D4D, 0.36f, 0.76f);
            var lamp = Paint("Warm headlamps", 0xFFE19E, 0.11f, 0.58f, true);
            var beacon = Paint("Amber beacons", 0xF8AA36, 0.15f, 0.5f, true);

            painted.Add(Loft(root, "Tow frame and armor", rust, new Section(-2.63f, 0.95f, -0.17f, 0.37f),
                new Section(-2.00f, 1.05f, -0.28f, 0.43f), new Section(0.50f, 1.04f, -0.34f, 0.50f),
                new Section(1.68f, 1.04f, -0.29f, 0.59f), new Section(2.57f, 0.94f, -0.15f, 0.52f)).GetComponent<Renderer>());
            painted.Add(Loft(root, "Box cab", rust, new Section(-0.69f, 0.92f, 0.25f, 1.33f),
                new Section(-0.43f, 0.95f, 0.25f, 1.43f), new Section(0.75f, 0.94f, 0.30f, 1.43f),
                new Section(1.17f, 0.89f, 0.30f, 1.23f)).GetComponent<Renderer>());
            Box(root, "Wide windshield", new Vector3(0f, 1.055f, 1.174f), new Vector3(1.57f, 0.36f, 0.047f), glass);
            Box(root, "Split windshield post", new Vector3(0f, 1.055f, 1.204f), new Vector3(0.071f, 0.37f, 0.045f), edge);
            Box(root, "Roof light bar", new Vector3(0f, 1.47f, 0.41f), new Vector3(1.30f, 0.10f, 0.19f), dark);
            for (int i = -2; i <= 2; i++)
                Box(root, "Amber light", new Vector3(i * 0.28f, 1.55f, 0.41f), new Vector3(0.115f, 0.12f, 0.12f), beacon);

            var hood = new GameObject("Tow hood").transform;
            hood.SetParent(root, false);
            hood.localPosition = new Vector3(0f, 0.65f, 1.80f);
            painted.Add(Loft(hood, "Tow hood shell", rust, new Section(-0.62f, 0.70f, -0.16f, 0.21f),
                new Section(0.39f, 0.63f, -0.15f, 0.15f), new Section(0.68f, 0.61f, -0.16f, 0.025f)).GetComponent<Renderer>());
            Box(root, "Tow front grille", new Vector3(0f, 0.40f, 2.58f), new Vector3(1.24f, 0.50f, 0.07f), dark);
            for (int i = -3; i <= 3; i++)
                Box(root, "Grille rib", new Vector3(i * 0.155f, 0.40f, 2.62f), new Vector3(0.022f, 0.43f, 0.03f), chrome);
            Box(root, "Tow ram plate", new Vector3(0f, -0.015f, 2.73f), new Vector3(2.28f, 0.51f, 0.23f), dark);
            for (int i = -3; i <= 3; i++)
                Box(root, "Ram tooth", new Vector3(i * 0.34f, -0.057f, 2.86f), new Vector3(0.105f, 0.59f, 0.24f),
                    chrome, new Vector3(-18f, 0f, 0f));

            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, "Door glass", new Vector3(side * 0.953f, 1.08f, 0.33f), new Vector3(0.034f, 0.32f, 0.89f), glass);
                Box(root, "Door panel", new Vector3(side * 0.957f, 0.69f, 0.33f), new Vector3(0.036f, 0.36f, 0.93f), edge);
                Box(root, "Door handle", new Vector3(side * 0.986f, 0.80f, -0.01f), new Vector3(0.023f, 0.038f, 0.16f), dark);
                Box(root, "Cab steps", new Vector3(side * 1.08f, -0.10f, 0.39f), new Vector3(0.26f, 0.17f, 0.91f), dark);
                Box(root, "Utility box", new Vector3(side * 0.81f, 0.46f, -1.69f), new Vector3(0.46f, 0.39f, 1.45f), edge);
                Box(root, "Square headlamp", new Vector3(side * 0.79f, 0.38f, 2.59f), new Vector3(0.28f, 0.195f, 0.09f), lamp);
                Cylinder(root, "Upright exhaust stack", new Vector3(side * 0.99f, 0.90f, -0.60f), 0.12f, 1.46f, Vector3.zero, dark);
                Box(root, "Rear hazard guard", new Vector3(side * 0.73f, 0.40f, -2.66f), new Vector3(0.42f, 0.25f, 0.09f), boom);
                for (int i = 0; i < 3; i++)
                    Box(root, "Hazard stripe", new Vector3(side * 0.73f + (i - 1) * 0.12f, 0.40f, -2.72f),
                        new Vector3(0.048f, 0.24f, 0.016f), dark, new Vector3(0f, 0f, -28f));
            }

            Box(root, "Winch base", new Vector3(0f, 0.64f, -1.2f), new Vector3(1.28f, 0.37f, 1.06f), dark);
            Cylinder(root, "Sheave housing", new Vector3(0f, 0.87f, -1.2f), 0.48f, 0.66f,
                new Vector3(0f, 0f, 90f), chrome);
            Vector3 craneFoot = new Vector3(0f, 0.8f, -0.96f);
            Vector3 craneHead = new Vector3(0f, 2.05f, -2.78f);
            BoxBetween(root, "Crane main boom", craneFoot, craneHead, 0.36f, 0.26f, boom);
            BoxBetween(root, "Crane supporting ram", new Vector3(0f, 0.62f, -1.88f),
                Vector3.Lerp(craneFoot, craneHead, 0.71f), 0.17f, 0.15f, chrome);
            BoxBetween(root, "Winch cable", craneHead, new Vector3(0f, 1.42f, -2.82f), 0.032f, 0.033f, dark);
            BoxBetween(root, "Hook spine", new Vector3(0f, 1.43f, -2.82f), new Vector3(0f, 1.20f, -2.90f), 0.10f, 0.09f, dark);
            BoxBetween(root, "Hook curl", new Vector3(0f, 1.20f, -2.90f), new Vector3(0f, 1.33f, -3.02f), 0.10f, 0.09f, dark);
            return hood;
        }

        private static ArcadeVehicle.WheelMount Wheel(Transform root, VehicleSettings settings, bool truck, int index)
        {
            float side = (index & 1) == 0 ? -1f : 1f;
            float axle = index < 2 ? 1f : -1f;
            var anchor = new Vector3(side * (truck ? 1.015f : 0.86f), 0.065f, axle * (truck ? 1.86f : 1.48f));
            var knuckle = new GameObject(index < 2 ? (side < 0 ? "Front left" : "Front right") :
                (side < 0 ? "Rear left" : "Rear right")).transform;
            knuckle.SetParent(root, false);
            knuckle.localPosition = anchor - Vector3.up * (settings.RestLength - 0.10f);
            var hub = new GameObject("Rolling assembly").transform;
            hub.SetParent(knuckle, false);
            var rubber = Paint("Tire rubber", 0x262526, 0.07f, 0.28f);
            var tread = Paint("Tire shoulder", 0x393635, 0.07f, 0.24f);
            var alloy = Paint("Wheel alloy", 0x8F8A79, 0.63f, 0.42f);
            var dark = Paint("Dark steel", 0x29282A, 0.53f, 0.34f);
            float diameter = 2f * settings.WheelRadius;
            float width = truck ? 0.38f : 0.28f;
            var rotation = new Vector3(0f, 0f, 90f);
            Cylinder(hub, "Tire", Vector3.zero, diameter, width, rotation, rubber);
            Cylinder(hub, "Shoulder", Vector3.zero, diameter * 0.95f, width * 1.08f, rotation, tread);
            Cylinder(hub, "Rim", new Vector3(side * width * 0.54f, 0f, 0f), diameter * 0.66f, 0.035f, rotation, alloy);
            Cylinder(hub, "Rim inset", new Vector3(side * width * 0.60f, 0f, 0f), diameter * 0.53f, 0.023f, rotation, dark);
            Cylinder(hub, "Hubcap", new Vector3(side * width * 0.66f, 0f, 0f), diameter * 0.23f, 0.046f, rotation, alloy);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var radial = Quaternion.AngleAxis(angle, Vector3.right) * Vector3.up;
                Box(hub, "Spoke", new Vector3(side * width * 0.643f, 0f, 0f) + radial * (diameter * 0.175f),
                    new Vector3(0.031f, diameter * 0.14f, diameter * 0.042f), alloy, new Vector3(angle, 0f, 0f));
            }

            return new ArcadeVehicle.WheelMount { Anchor = anchor, Knuckle = knuckle, Hub = hub };
        }

        private static GameObject Loft(Transform root, string name, Material material, params Section[] sections)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();
            Vector3[] ring = sections[0].Outline();
            AddCap(vertices, triangles, normals, ring, -Vector3.forward);
            for (int i = 1; i < sections.Length; i++)
            {
                Vector3[] next = sections[i].Outline();
                for (int side = 0; side < ring.Length; side++)
                {
                    int afterward = (side + 1) % ring.Length;
                    int first = vertices.Count;
                    Vector3 normal = Vector3.Cross(ring[afterward] - ring[side], next[side] - ring[side]).normalized;
                    vertices.AddRange(new[] { ring[side], ring[afterward], next[side], next[afterward] });
                    normals.AddRange(new[] { normal, normal, normal, normal });
                    triangles.AddRange(new[] { first, first + 1, first + 2, first + 1, first + 3, first + 2 });
                }
                ring = next;
            }
            AddCap(vertices, triangles, normals, ring, Vector3.forward);

            var authored = new Mesh { name = name };
            authored.SetVertices(vertices);
            authored.SetTriangles(triangles, 0);
            authored.SetNormals(normals);
            authored.RecalculateBounds();
            string path = $"{ArtRoot}/{name.Replace(' ', '_')}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                EditorUtility.CopySerialized(authored, mesh);
                EditorUtility.SetDirty(mesh);
                Object.DestroyImmediate(authored);
            }
            else
            {
                mesh = authored;
                AssetDatabase.CreateAsset(mesh, path);
            }

            var part = new GameObject(name);
            part.transform.SetParent(root, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static void AddCap(List<Vector3> vertices, List<int> triangles, List<Vector3> normals,
            Vector3[] ring, Vector3 normal)
        {
            int first = vertices.Count;
            foreach (Vector3 vertex in ring)
            {
                vertices.Add(vertex);
                normals.Add(normal);
            }

            for (int i = 1; i < ring.Length - 1; i++)
            {
                triangles.Add(first);
                triangles.Add(first + (normal.z > 0f ? i : i + 1));
                triangles.Add(first + (normal.z > 0f ? i + 1 : i));
            }
        }

        private static GameObject Box(Transform root, string name, Vector3 position, Vector3 size, Material material,
            Vector3 angles = default)
        {
            return Primitive(root, PrimitiveType.Cube, name, position, size, Quaternion.Euler(angles), material);
        }

        private static void BoxBetween(Transform root, string name, Vector3 start, Vector3 end, float width, float height,
            Material material)
        {
            Vector3 axis = end - start;
            Primitive(root, PrimitiveType.Cube, name, (start + end) * 0.5f, new Vector3(width, height, axis.magnitude),
                Quaternion.LookRotation(axis), material);
        }

        private static void Cylinder(Transform root, string name, Vector3 position, float diameter, float length,
            Vector3 angles, Material material)
        {
            Primitive(root, PrimitiveType.Cylinder, name, position, new Vector3(diameter, length * 0.5f, diameter),
                Quaternion.Euler(angles), material);
        }

        private static GameObject Primitive(Transform root, PrimitiveType kind, string name, Vector3 position, Vector3 size,
            Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(kind);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.SetLocalPositionAndRotation(position, rotation);
            part.transform.localScale = size;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static Material Paint(string name, uint rgb, float metal, float smoothness, bool glow = false)
        {
            string path = $"{ArtRoot}/{name.Replace(' ', '_')}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            if (glow)
            {
                material.SetColor("_EmissionColor", (Color)color * 0.32f);
                material.EnableKeyword("_EMISSION");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separator = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..separator], path[(separator + 1)..]);
        }
    }
}
