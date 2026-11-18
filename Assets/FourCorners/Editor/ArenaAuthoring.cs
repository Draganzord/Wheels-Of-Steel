using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        private const string ArtPath = "Assets/FourCorners/Arena/Art";
        private const float WaterLevel = -0.055f;

        private static readonly Vector2[] LoopCenters =
        {
            new(-34, -21), new(-35, -7), new(-35, 12), new(-28, 24),
            new(-11, 29), new(16, 29), new(32, 23), new(38, 12),
            new(38, -3), new(37, -17), new(31, -26), new(19, -31),
            new(-11, -31), new(-27, -29)
        };

        private static readonly Vector2[] ShortcutCenters =
        {
            new(-29, -22), new(-20, -15), new(-10, -9), new(1, -6),
            new(12, -4), new(19, -3), new(24, -3), new(38, -3)
        };

        public static ArenaLayout Build(Transform parent, SurfaceProfile[] profiles)
        {
            if (!AssetDatabase.IsValidFolder(ArtPath))
                AssetDatabase.CreateFolder("Assets/FourCorners/Arena", "Art");

            Transform yard = Group(parent, "Four Corners Salvage Yard | Arena");
            var layout = yard.gameObject.AddComponent<ArenaLayout>();
            Vector2[] road = SmoothLoop(LoopCenters);
            BuildGround(yard, profiles, road);
            BuildStation(Group(yard, "01 | Gas station and forecourt"));
            BuildSalvage(Group(yard, "02 | Fork and salvage inventory"));
            layout.Fence = BuildBreach(Group(yard, "03 | Sandy bank and breach"));
            BuildScenery(Group(yard, "04 | Desert perimeter"), road);
            BuildWayfinding(Group(yard, "Route signs and markers"));
            AddSalvageRamps(layout, profiles);
            ConfigureDestruction(layout);

            Transform routes = Group(yard, "05 | Race lines and spawn markers");
            layout.PlayerSpawn = Marker(routes, "Player spawn", new(-29, -23), 0.71f, 35f);
            layout.TruckSpawn = Marker(routes, "Truck spawn", new(-33.5f, -31), 0.86f, 22f);
            layout.Loop = Waypoints(Group(routes, "Loop | Asphalt"), LoopCenters);
            layout.Shortcut = Waypoints(Group(routes, "Cut | Creek"), ShortcutCenters);
            return layout;
        }

        private static Transform[] Waypoints(Transform parent, Vector2[] points)
        {
            var markers = new Transform[points.Length];
            for (int index = 0; index < points.Length; index++)
                markers[index] = Marker(parent, $"Waypoint {index + 1:00}", points[index], GroundHeight(points[index]), 0f);
            return markers;
        }

        private static Transform Marker(Transform parent, string name, Vector2 xz, float y, float yaw)
        {
            Transform marker = Group(parent, name, XZ(xz, y));
            marker.localRotation = Quaternion.Euler(0, yaw, 0);
            return marker;
        }

        private static Transform Group(Transform parent, string name, Vector3 localPosition = default)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            return root;
        }

        private static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 size, Material material, bool solid = false)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = size;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
                Object.DestroyImmediate(shape.GetComponent<Collider>());
            return shape;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool solid = false) =>
            Shape(parent, name, PrimitiveType.Cube, position, size, material, solid);

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, float diameter,
            float height, Material material, bool solid = false) =>
            Shape(parent, name, PrimitiveType.Cylinder, position, new Vector3(diameter, height * 0.5f, diameter), material, solid);

        private static GameObject Beam(Transform parent, string name, Vector3 from, Vector3 to, float width,
            float height, Material material, bool solid = false)
        {
            Vector3 axis = (to - from).normalized;
            GameObject beam = Box(parent, name, (from + to) * 0.5f,
                new Vector3(width, height, Vector3.Distance(from, to)), material, solid);
            beam.transform.localRotation = Quaternion.LookRotation(axis,
                Mathf.Abs(axis.y) > 0.95f ? Vector3.forward : Vector3.up);
            return beam;
        }

        private static Material Lit(string name, string hex, float smoothness = 0.23f, Texture2D texture = null)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (texture)
                material.SetTexture("_BaseMap", texture);
            return SaveAsset(material, "mat");
        }

        private static T SaveAsset<T>(T asset, string extension) where T : Object
        {
            string path = $"{ArtPath}/{asset.name}.{extension}";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static float DistanceToRoute(Vector2 point, Vector2[] centers, bool closed)
        {
            float closest = float.PositiveInfinity;
            for (int i = 0; i < centers.Length - (closed ? 0 : 1); i++)
            {
                Vector2 start = centers[i];
                Vector2 segment = centers[(i + 1) % centers.Length] - start;
                float fraction = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
                closest = Mathf.Min(closest, (point - start - segment * fraction).sqrMagnitude);
            }
            return Mathf.Sqrt(closest);
        }

        private static Vector2[] SmoothLoop(Vector2[] vertices)
        {
            const int sections = 6;
            var path = new Vector2[vertices.Length * sections];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 a = vertices[(i + vertices.Length - 1) % vertices.Length];
                Vector2 b = vertices[i];
                Vector2 c = vertices[(i + 1) % vertices.Length];
                Vector2 d = vertices[(i + 2) % vertices.Length];
                for (int j = 0; j < sections; j++)
                {
                    float t = (float)j / sections;
                    path[i * sections + j] = 0.5f * (2f * b + (c - a) * t
                        + (2f * a - 5f * b + 4f * c - d) * t * t
                        + (-a + 3f * b - 3f * c + d) * t * t * t);
                }
            }
            return path;
        }

        private static Vector3 XZ(Vector2 point, float y) => new(point.x, y, point.y);

        private static float Pick(System.Random random, float min, float max) =>
            Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
