using System.Collections.Generic;
using UnityEngine;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        private static void BuildScenery(Transform parent, Vector2[] road)
        {
            Material wood = Lit("Perimeter Sun-Bleached Timber", "967351");
            Material steel = Lit("Perimeter Steel", "64635A", 0.31f);
            Material signRed = Lit("Perimeter Brick", "9F4C35");
            Material cream = Lit("Perimeter Sign Ivory", "D8C398");
            Material clay = Lit("Sandstone Copper", "AA694C");
            Material ochre = Lit("Sandstone Gold", "BC8E62");
            Material darkClay = Lit("Sandstone Umber", "86513B");
            Material sage = Lit("Desert Scrub Sage", "7A8247");
            Material dried = Lit("Desert Scrub Dried", "B4A267");
            Material cactus = Lit("Saguaro Green", "607343", 0.31f);
            sage.SetFloat("_Cull", 0);
            dried.SetFloat("_Cull", 0);

            var rock = SaveAsset(RockMesh(), "asset");
            var grass = SaveAsset(GrassMesh(), "asset");
            var random = new System.Random(50307);
            Transform scatter = Group(parent, "Brush, gravel and bank plants");

            for (int i = 0; i < 180; i++)
            {
                Vector2 point = new(Pick(random, -58, 58), Pick(random, -43, 42));
                var surface = SurfaceAt(point, road);
                if (surface != SurfaceKind.Sand && surface != SurfaceKind.Grass)
                    continue;
                if (DistanceToRoute(point, ShortcutCenters, false) < 4f || point.x > 12f && point.x < 36 && Mathf.Abs(point.y + 6) < 8f)
                    continue;

                float y = GroundHeight(point);
                if (surface == SurfaceKind.Grass || i % 3 != 2)
                {
                    float scale = Pick(random, 0.65f, surface == SurfaceKind.Grass ? 1.2f : 1.7f);
                    MeshShape(scatter, "Low clump", grass, XZ(point, y), Vector3.one * scale,
                        Quaternion.Euler(0, Pick(random, 0, 180), 0), surface == SurfaceKind.Grass || i % 4 == 0 ? sage : dried);
                }
                else
                {
                    float scale = Pick(random, 0.29f, 0.8f);
                    MeshShape(scatter, "Loose gravel", rock, XZ(point, y + scale * 0.10f),
                        new(scale * 1.57f, scale * 0.46f, scale), Quaternion.Euler(0, Pick(random, 0, 180), 0),
                        i % 2 == 0 ? clay : ochre);
                }
            }

            for (int i = 0; i < 24; i++)
            {
                float z = Mathf.Lerp(CreekSouth + 1.7f, CreekNorth - 1.7f, (float)i / 23f);
                if (z > -10.3f && z < 2.7f)
                    continue;
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 bank = new(CreekCenter(z) + (CreekHalfWidth + Pick(random, 0.2f, 1f)) * side, z);
                float scale = Pick(random, 0.8f, 1.85f);
                MeshShape(scatter, "Bank sedge", grass, XZ(bank, GroundHeight(bank)), new(scale, scale * 1.5f, scale),
                    Quaternion.Euler(0, Pick(random, 0, 180), 0), sage);
                Vector2 stone = bank - new Vector2(side * 0.4f, 0.23f);
                MeshShape(scatter, "Creek cobble", rock, XZ(stone, GroundHeight(stone) + 0.09f),
                    new(scale * 0.53f, scale * 0.28f, scale * 0.35f), Quaternion.Euler(0, z * 23, 0), ochre);
            }

            Vector2[] cactusPatches =
            {
                new(-45, -35), new(-47, 25), new(46, 31), new(49, -14),
                new(44, -36), new(26, -37), new(-51, -16), new(30, 34), new(-17, 37)
            };
            for (int i = 0; i < cactusPatches.Length; i++)
            {
                Vector2 point = cactusPatches[i];
                var plant = Group(scatter, "Saguaro", XZ(point, GroundHeight(point)));
                plant.localRotation = Quaternion.Euler(0, i * 35, 0);
                float size = Pick(random, 1.3f, 2.4f);
                Cylinder(plant, "Tall trunk", new(0, size * 0.5f, 0), 0.32f, size, cactus);
                Shape(plant, "Trunk tip", PrimitiveType.Sphere, new(0, size, 0), new(0.32f, 0.32f, 0.32f), cactus);
                for (int side = -1; side <= 1; side += 2)
                {
                    float height = size * (side == -1 ? 0.41f : 0.61f);
                    Beam(plant, "Bent arm", new(0, height, 0), new(0.43f * side, height + 0.23f, 0), 0.19f, 0.19f, cactus);
                    Cylinder(plant, "Saguaro arm", new(0.43f * side, height + 0.37f, 0), 0.2f, 0.46f, cactus);
                    Shape(plant, "Rounded arm tip", PrimitiveType.Sphere, new(0.43f * side, height + 0.63f, 0),
                        new(0.2f, 0.2f, 0.2f), cactus);
                }
            }

            Transform boundary = Group(parent, "Physical yard boundary and low approach rails");
            Boundary(boundary, new(-53, -42), new(54, -42), wood, steel);
            Boundary(boundary, new(54, -42), new(54, 41), wood, steel);
            Boundary(boundary, new(54, 41), new(-53, 41), wood, steel);
            Boundary(boundary, new(-53, 41), new(-53, -42), wood, steel);

            Transform skyline = Group(parent, "Desert silhouettes");
            Vector2[] ridges = { new(-45, 55), new(-18, 62), new(18, 64), new(51, 54), new(68, 30), new(-71, 16) };
            for (int i = 0; i < ridges.Length; i++)
            {
                Vector2 center = ridges[i];
                for (int j = 0; j < 4; j++)
                {
                    float size = Pick(random, 5.5f, 10.5f);
                    var offset = new Vector2(Pick(random, -7, 7), Pick(random, -4, 4));
                    MeshShape(skyline, "Eroded mesa boulder", rock, XZ(center + offset, size * 0.27f - 0.6f),
                        new(size * 1.8f, size * 0.85f, size), Quaternion.Euler(0, Pick(random, -19, 19), 0),
                        j % 3 == 0 ? darkClay : j % 3 == 1 ? clay : ochre);
                }
            }

            Transform shed = Group(parent, "Utility bay | Background dressing", new(-45.7f, 0, 12.8f));
            Box(shed, "Rusted utility bay", new(0, 1.23f, 0), new(4.5f, 2.46f, 8.8f), signRed, true);
            Box(shed, "Ribbed steel roof", new(0, 2.58f, 0), new(4.8f, 0.23f, 9.08f), steel);
            Box(shed, "Bay opening", new(2.26f, 1.0f, 0.5f), new(0.1f, 1.94f, 3.72f), wood);
            Text(shed, "Shed roof label", "PARTS", new(0, 2.72f, 1.3f), new(90, 0, 90),
                0.18f, new Color32(236, 220, 173, 255));

            Transform forkPlacard = Group(parent, "Fork direction placard", new(-24.7f, 0, -15.31f));
            forkPlacard.localRotation = Quaternion.Euler(0, 29, 0);
            Box(forkPlacard, "Sign upright", new(0, 0.82f, 0), new(0.17f, 1.64f, 0.17f), wood);
            Box(forkPlacard, "Guide panel", new(0, 1.6f, 0), new(2.8f, 0.86f, 0.12f), signRed);
            Text(forkPlacard, "Sign text", "CREEK  > ", new(0, 1.6f, -0.069f), Vector3.zero, 0.085f,
                new Color32(250, 224, 172, 255));

            for (int i = 0; i < 3; i++)
                Box(boundary, "Reflective guard panel", new(35.6f + i * 4.1f, 0.7f, -41.8f),
                    new(0.48f, 0.16f, 0.14f), cream);
        }

        private static void Boundary(Transform parent, Vector2 start, Vector2 end, Material wood, Material steel)
        {
            Vector3 a = XZ(start, 0.62f);
            Vector3 b = XZ(end, 0.62f);
            Beam(parent, "Closed boundary crash rail", a, b, 0.22f, 0.42f, steel, true);
            Beam(parent, "Lower rail", a - Vector3.up * 0.3f, b - Vector3.up * 0.3f, 0.13f, 0.16f, wood);
            int count = Mathf.CeilToInt(Vector2.Distance(start, end) / 5.3f);
            for (int i = 0; i < count; i++)
                Box(parent, "Roadside fence post", XZ(Vector2.Lerp(start, end, (float)i / count), 0.53f),
                    new(0.27f, 1.06f, 0.27f), wood);
        }

        private static void MeshShape(Transform parent, string name, Mesh mesh, Vector3 position,
            Vector3 size, Quaternion rotation, Material material)
        {
            Transform item = Group(parent, name, position);
            item.localScale = size;
            item.localRotation = rotation;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh RockMesh()
        {
            var points = new Vector3[3, 7];
            var random = new System.Random(371);
            for (int ring = 0; ring < 3; ring++)
            {
                for (int i = 0; i < 7; i++)
                {
                    float direction = i * Mathf.PI * 2f / 7f;
                    float radius = Pick(random, 0.74f, 1.07f) * (ring == 2 ? 0.68f : ring == 1 ? 1f : 0.83f);
                    points[ring, i] = new Vector3(Mathf.Cos(direction) * radius,
                        (ring - 1) * 0.53f + Pick(random, -0.12f, 0.12f), Mathf.Sin(direction) * radius);
                }
            }

            var verts = new List<Vector3>();
            for (int i = 0; i < 7; i++)
            {
                int next = (i + 1) % 7;
                for (int ring = 0; ring < 2; ring++)
                {
                    verts.Add(points[ring, i]);
                    verts.Add(points[ring + 1, i]);
                    verts.Add(points[ring + 1, next]);
                    verts.Add(points[ring, i]);
                    verts.Add(points[ring + 1, next]);
                    verts.Add(points[ring, next]);
                }
                verts.Add(new Vector3(0, 0.50f, 0));
                verts.Add(points[2, next]);
                verts.Add(points[2, i]);
            }
            return FaceMesh("Faceted Sandstone", verts);
        }

        private static Mesh GrassMesh()
        {
            var vertices = new List<Vector3>();
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 2.4f;
                Vector3 tip = new(Mathf.Sin(angle) * (0.22f + i * 0.023f), 0.12f + (i % 3) * 0.14f,
                    Mathf.Cos(angle) * (0.22f + i * 0.023f));
                Vector3 basePoint = new(tip.x * 0.20f, 0, tip.z * 0.20f);
                Vector3 cross = new(-tip.z * 0.14f, 0, tip.x * 0.14f);
                vertices.Add(basePoint - cross);
                vertices.Add(tip);
                vertices.Add(basePoint + cross);
            }
            return FaceMesh("Desert Brush Cluster", vertices);
        }

        private static Mesh FaceMesh(string name, List<Vector3> points)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points);
            var triangles = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
                triangles[i] = i;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
