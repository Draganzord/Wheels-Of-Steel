using System.Collections.Generic;
using UnityEngine;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        private static readonly Vector2[] LaunchDeck =
        {
            new(0f, -0.025f), new(0.6f, 0f), new(1.35f, 0.075f),
            new(3.7f, 0.365f), new(7.7f, 0.90f)
        };

        private static readonly Vector2[] BounceDeck =
        {
            new(0f, -0.025f), new(0.65f, 0.01f), new(1.55f, 0.15f), new(3.15f, 0.45f),
            new(5.5f, 0.99f), new(6.15f, 1.14f), new(6.50f, 1.15f),
            new(6.92f, 1.06f), new(7.9f, 0.68f), new(9.8f, 0.14f), new(11f, -0.025f)
        };

        public static void AddSalvageRamps(ArenaLayout layout, SurfaceProfile[] profiles)
        {
            Transform group = Group(layout.transform, "Salvage ramps");
            Material deck = Lit("Ramp Plate Steel", "54636A", 0.36f);
            Material frame = Lit("Ramp Oxide Frame", "865641");
            Material amber = Lit("Ramp Warning Ochre", "C88838");
            Material cream = Lit("Ramp Worn Ivory", "DDD0A5");
            Mesh arrow = SaveAsset(WayfindingArrow(), "asset");
            SurfaceProfile surface = profiles[(int)SurfaceKind.Asphalt];
            layout.Ramps = new[]
            {
                BuildRamp(group, "Scrap Launch", "JUMP", new(-48.4f, -29f), 4.7f, LaunchDeck, 4, 20.2f,
                    deck, frame, amber, cream, arrow, surface),
                BuildRamp(group, "Steel Bounce", "BOUNCE", new(46.6f, -9.4f), 4.5f, BounceDeck, 6, 20.9f,
                    deck, frame, amber, cream, arrow, surface)
            };
        }

        private static Transform BuildRamp(Transform parent, string name, string stencil, Vector2 toe, float width,
            Vector2[] profile, int crestIndex, float runout, Material deck, Material frame, Material amber,
            Material cream, Mesh arrow, SurfaceProfile surface)
        {
            Transform ramp = Group(parent, name, XZ(toe, 0));
            Transform platform = Group(ramp, "Driveable steel deck");
            Mesh mesh = SaveAsset(RampMesh(name + " Deck", width, profile), "asset");
            platform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            platform.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { deck, frame };
            platform.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            platform.gameObject.AddComponent<SurfaceZone>().Configure(surface);

            Group(ramp, "Toe");
            Vector2 crest = profile[crestIndex];
            Group(ramp, "Crest", new Vector3(0, crest.y, crest.x));
            Group(ramp, "Runout", new Vector3(0, 0, runout));

            float edge = width * 0.5f - 0.18f;
            for (int i = 1; i < profile.Length; i++)
            {
                Vector2 a = profile[i - 1];
                Vector2 b = profile[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = edge * side;
                    Beam(ramp, "Painted deck edge", new Vector3(x, a.y + 0.023f, a.x),
                        new Vector3(x, b.y + 0.023f, b.x), 0.19f, 0.02f, amber);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float z = crest.x - (i + 0.65f) * 0.42f;
                Box(ramp, "Crest warning stripe", new Vector3(0, DeckHeight(profile, z) + 0.031f, z),
                    new Vector3(width * 0.73f, 0.022f, 0.14f), i == 0 ? cream : amber);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float x = width * 0.5f * side;
                Beam(ramp, "Salvage truss brace", new Vector3(x, 0.12f, crest.x - 2.5f),
                    new Vector3(x, crest.y - 0.11f, crest.x - 0.2f), 0.11f, 0.11f, amber);
                Waypost(parent, name + " entry marker", toe + new Vector2(x + side * 0.28f, -0.68f), frame, amber, cream);
                Waypost(parent, name + " landing marker", toe + new Vector2(x + side * 0.37f, runout), frame, amber, cream);
            }

            Text(ramp, "Approach stencil", stencil, new Vector3(0, 0.028f, -4.1f),
                new Vector3(90, 0, 0), 0.11f, new Color32(85, 56, 43, 255));
            FloorArrow(parent, name + " approach arrow", toe + Vector2.down * 1.85f, Vector3.forward, arrow, amber, 1.6f, cream);
            return ramp;
        }

        private static float DeckHeight(Vector2[] profile, float distance)
        {
            for (int index = 1; index < profile.Length; index++)
                if (distance <= profile[index].x)
                    return Mathf.Lerp(profile[index - 1].y, profile[index].y,
                        Mathf.InverseLerp(profile[index - 1].x, profile[index].x, distance));
            return profile[^1].y;
        }

        private static Mesh RampMesh(string name, float width, Vector2[] profile)
        {
            const float floor = -0.065f;
            float left = -width * 0.5f;
            float right = width * 0.5f;
            var vertices = new List<Vector3>();
            var deck = new List<int>();
            var sides = new List<int>();
            for (int index = 1; index < profile.Length; index++)
            {
                Vector2 a = profile[index - 1];
                Vector2 b = profile[index];
                Vector3 leftA = new(left, a.y, a.x);
                Vector3 leftB = new(left, b.y, b.x);
                Vector3 rightA = new(right, a.y, a.x);
                Vector3 rightB = new(right, b.y, b.x);
                RampQuad(vertices, deck, leftA, leftB, rightB, rightA);
                RampQuad(vertices, sides, new Vector3(left, floor, a.x), new Vector3(left, floor, b.x), leftB, leftA);
                RampQuad(vertices, sides, new Vector3(right, floor, b.x), new Vector3(right, floor, a.x), rightA, rightB);
            }
            Vector2 end = profile[^1];
            RampQuad(vertices, sides, new Vector3(left, floor, end.x), new Vector3(right, floor, end.x),
                new Vector3(right, end.y, end.x), new Vector3(left, end.y, end.x));

            var mesh = new Mesh { name = name, subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(deck, 0);
            mesh.SetTriangles(sides, 1);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void RampQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
