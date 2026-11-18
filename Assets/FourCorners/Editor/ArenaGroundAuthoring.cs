using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FourCorners.Editor
{
    public static partial class ArenaAuthoring
    {
        private const float RoadHalfWidth = 5.7f;
        private const float CreekSouth = -23f;
        private const float CreekNorth = 19f;
        private const float CreekHalfWidth = 3.8f;
        private const float GrainUvScale = 0.14f;

        private static void BuildGround(Transform parent, SurfaceProfile[] profiles, Vector2[] road)
        {
            Transform ground = Group(parent, "00 | Roadbed, creek and surfaces");
            Material[] materials =
            {
                Lit("Coal Asphalt", "515459", 0.14f, Grain("Asphalt Aggregate", 0)),
                Lit("Sage Grass Soil", "68753C", 0.11f, Grain("Grass Soil", 1)),
                Lit("Waterlogged Clay", "77513F", 0.53f, Grain("Mud Clods", 2)),
                Lit("Copper Sand", "C39261", 0.13f, Grain("Windblown Sand", 3)),
                Lit("Creek Gravel", "576953", 0.42f, Grain("Creek Silt", 4))
            };
            Material terrainMaterial = BakeTerrainAppearance(materials, road);

            var builders = new TerrainMesh[profiles.Length];
            for (int i = 0; i < builders.Length; i++)
                builders[i] = new TerrainMesh();

            const float cell = 1.2f;
            for (float z = -54; z < 54; z += cell)
            {
                for (float x = -65; x < 65; x += cell)
                {
                    Vector2 center = new(x + cell * 0.5f, z + cell * 0.5f);
                    SurfaceKind surface = SurfaceAt(center, road);
                    float nextX = x + cell;
                    float nextZ = z + cell;
                    builders[(int)surface].Quad(
                        XZ(new(x, z), GroundHeight(new(x, z))),
                        XZ(new(x, nextZ), GroundHeight(new(x, nextZ))),
                        XZ(new(nextX, nextZ), GroundHeight(new(nextX, nextZ))),
                        XZ(new(nextX, z), GroundHeight(new(nextX, z))));
                }
            }

            for (int i = 0; i < builders.Length; i++)
            {
                var kind = (SurfaceKind)i;
                Mesh mesh = SaveAsset(builders[i].Mesh($"Ground {kind}"), "asset");
                var tile = Group(ground, $"Wheel contact | {kind}").gameObject;
                tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                tile.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
                tile.AddComponent<MeshCollider>().sharedMesh = mesh;
                if (kind == SurfaceKind.Water)
                    tile.AddComponent<SurfaceZone>().Configure(profiles[i], WaterLevel);
                else
                    tile.AddComponent<SurfaceZone>().Configure(profiles[i]);
            }

            Box(ground, "Desert horizon bed", new(0, -0.65f, 0), new(275, 0.3f, 240), materials[(int)SurfaceKind.Sand]);
            BuildWater(ground);
            PaintRoad(ground, road);
        }

        private static Material BakeTerrainAppearance(Material[] swatches, Vector2[] road)
        {
            const int width = 1152;
            const int height = 960;
            const float worldWidth = 132f;
            const float worldHeight = 110f;
            var palette = new TerrainSwatch[swatches.Length];
            for (int i = 0; i < palette.Length; i++)
                palette[i] = new TerrainSwatch(swatches[i]);

            var pixels = new Color32[width * height];
            var bake = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                float z = ((y + 0.5f) / height - 0.5f) * worldHeight;
                float creekX = CreekCenter(z);
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(((x + 0.5f) / width - 0.5f) * worldWidth, z);
                    pixels[y * width + x] = TerrainAppearance(point, creekX, palette, road);
                }
            }

            bake.SetPixels32(pixels);
            bake.Apply(false);
            string path = $"{ArtPath}/Yard Ground Albedo.png";
            File.WriteAllBytes(path, bake.EncodeToPNG());
            Object.DestroyImmediate(bake);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            Material material = Lit("Yard Terrain Blended", "FFFFFF", 1f, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetTextureScale("_BaseMap", new Vector2(1f / (worldWidth * GrainUvScale), 1f / (worldHeight * GrainUvScale)));
            material.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0.5f));
            material.SetFloat("_SmoothnessTextureChannel", 1f);
            material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Color TerrainAppearance(Vector2 point, float creekX, TerrainSwatch[] swatches, Vector2[] road)
        {
            Vector2 uv = point * GrainUvScale;
            float asphalt = Feather(RoadHalfWidth - DistanceToRoute(point, road, true), 0.32f);
            if (asphalt >= 1f)
                return swatches[(int)SurfaceKind.Asphalt].At(uv);

            float creek = Feather(Mathf.Min(CreekHalfWidth - Mathf.Abs(point.x - creekX),
                Mathf.Min(point.y - CreekSouth, CreekNorth - point.y)), 0.35f);
            float forecourt = Feather(Mathf.Min(Mathf.Min(point.x + 10, 15 - point.x),
                Mathf.Min(point.y - 2.2f, 25 - point.y)), 0.28f);

            Color color = swatches[(int)SurfaceKind.Sand].At(uv);
            if (point.x > -30.5f && point.x < 9.5f && point.y < 2.8f && point.y > -24.5f)
            {
                float noise = (Mathf.PerlinNoise(point.x * 0.105f + 10, point.y * 0.105f + 10) - 0.5f) * 3f;
                float cutDistance = DistanceToRoute(point, ShortcutCenters, false);
                float grass = Feather(Mathf.Min(Mathf.Min(point.x + 29, 8 - point.x),
                    Mathf.Min(Mathf.Min(1.2f - point.y, point.y + 23), 10.5f + noise - cutDistance)), 0.85f);
                float mud = Feather(Mathf.Min(Mathf.Min(point.x + 15 - noise, creekX - point.x),
                    Mathf.Min(1.3f - point.y, 6.6f + noise - cutDistance)), 0.85f);
                ApplySwatch(ref color, swatches[(int)SurfaceKind.Grass], uv, grass);
                ApplySwatch(ref color, swatches[(int)SurfaceKind.Mud], uv, mud);
            }

            ApplySwatch(ref color, swatches[(int)SurfaceKind.Asphalt], uv, forecourt * (1f - creek));
            ApplySwatch(ref color, swatches[(int)SurfaceKind.Water], uv, creek);
            ApplySwatch(ref color, swatches[(int)SurfaceKind.Asphalt], uv, asphalt);
            return color;
        }

        private static float Feather(float signedInside, float width) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-width, width, signedInside));

        private static void ApplySwatch(ref Color color, TerrainSwatch swatch, Vector2 uv, float opacity)
        {
            if (opacity > 0f)
                color = Color.Lerp(color, swatch.At(uv), opacity);
        }

        private readonly struct TerrainSwatch
        {
            private readonly Texture2D grain;
            private readonly Color tint;
            private readonly float smoothness;

            public TerrainSwatch(Material material)
            {
                grain = (Texture2D)material.GetTexture("_BaseMap");
                tint = material.GetColor("_BaseColor");
                smoothness = material.GetFloat("_Smoothness");
            }

            public Color At(Vector2 uv)
            {
                Color color = tint * grain.GetPixelBilinear(uv.x, uv.y);
                color.a = smoothness;
                return color;
            }
        }

        private static SurfaceKind SurfaceAt(Vector2 point, Vector2[] road)
        {
            if (DistanceToRoute(point, road, true) < RoadHalfWidth)
                return SurfaceKind.Asphalt;

            float distanceFromCreek = Mathf.Abs(point.x - CreekCenter(point.y));
            if (point.y > CreekSouth && point.y < CreekNorth && distanceFromCreek < CreekHalfWidth)
                return SurfaceKind.Water;

            if (point.x > -10 && point.x < 15 && point.y > 2.2f && point.y < 25)
                return SurfaceKind.Asphalt;

            float noise = (Mathf.PerlinNoise(point.x * 0.105f + 10, point.y * 0.105f + 10) - 0.5f) * 3f;
            float cutDistance = DistanceToRoute(point, ShortcutCenters, false);
            if (point.x > -15 + noise && point.x < CreekCenter(point.y) && point.y < 1.3f && cutDistance < 6.6f + noise)
                return SurfaceKind.Mud;

            if (point.x > -29 && point.x < 8 && point.y < 1.2f
                && point.y > -23 && cutDistance < 10.5f + noise)
                return SurfaceKind.Grass;

            return SurfaceKind.Sand;
        }

        private static float GroundHeight(Vector2 point)
        {
            float distance = Mathf.Abs(point.x - CreekCenter(point.y));
            float channel = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.25f, 4.35f, distance));
            float waterEnd = Mathf.Min(
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CreekSouth - 1f, CreekSouth + 2f, point.y)),
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CreekNorth + 1f, CreekNorth - 2f, point.y)));
            return -0.31f * channel * waterEnd;
        }

        private static float CreekCenter(float z) =>
            12f + (z + 4f) * 0.16f + 11f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 18f, z));

        private static void BuildWater(Transform parent)
        {
            var water = new TerrainMesh(true);
            const int sections = 58;
            for (int i = 0; i < sections; i++)
            {
                float z0 = Mathf.Lerp(CreekSouth, CreekNorth, (float)i / sections);
                float z1 = Mathf.Lerp(CreekSouth, CreekNorth, (i + 1f) / sections);
                float x0 = CreekCenter(z0);
                float x1 = CreekCenter(z1);
                water.Quad(
                    new(x0 - CreekHalfWidth, WaterLevel, z0), new(x1 - CreekHalfWidth, WaterLevel, z1),
                    new(x1 + CreekHalfWidth, WaterLevel, z1), new(x0 + CreekHalfWidth, WaterLevel, z0));
            }

            var material = new Material(Shader.Find("FourCorners/Shallow Water")) { name = "Creek Water" };
            material = SaveAsset(material, "mat");
            var sheet = Group(parent, "Shallow water | Surface separate from gravel bed").gameObject;
            sheet.AddComponent<MeshFilter>().sharedMesh = SaveAsset(water.Mesh("Creek Water Surface"), "asset");
            sheet.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void PaintRoad(Transform ground, Vector2[] road)
        {
            Transform paint = Group(ground, "Painted lane markings");
            Material ochre = Lit("Road Paint Amber", "BA8F4B", 0.16f);
            Material cream = Lit("Road Paint Chalk", "D9CEAA", 0.18f);
            for (int i = 0; i < road.Length; i++)
            {
                Vector2 a = road[i];
                Vector2 b = road[(i + 1) % road.Length];
                Vector2 direction = (b - a).normalized;
                Vector2 lateral = new(-direction.y, direction.x);
                Vector2 mid = (a + b) * 0.5f;
                bool usable = Vector2.Distance(mid, ShortcutCenters[0]) > 8.5f
                    && Vector2.Distance(mid, ShortcutCenters[^1]) > 7.5f;
                if (i % 2 == 0 && usable)
                    Beam(paint, "Amber road dash", XZ(a + direction * 0.4f, 0.015f),
                        XZ(b - direction * 0.22f, 0.015f), 0.17f, 0.012f, ochre);

                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (sign == -1 && !usable)
                        continue;

                    Vector2 offset = lateral * (sign * 4.5f);
                    Beam(paint, "Road shoulder line", XZ(a + offset, 0.012f), XZ(b + offset, 0.012f),
                        0.10f, 0.012f, cream);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Beam(paint, "Bank merge chevron", new(28.7f + i * 1.08f, 0.018f, -4.65f),
                    new(29.45f + i * 1.08f, 0.018f, -3.05f), 0.15f, 0.016f, cream);
                Beam(paint, "Bank merge chevron", new(29.43f + i * 1.08f, 0.018f, -3.08f),
                    new(28.7f + i * 1.08f, 0.018f, -1.48f), 0.15f, 0.016f, cream);
            }
        }

        private static Texture2D Grain(string name, int style)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(2170 + style);
            for (int y = 0; y < size; y++)
            {
                float v = y * (Mathf.PI * 2f / size);
                for (int x = 0; x < size; x++)
                {
                    float u = x * (Mathf.PI * 2f / size);
                    float fieldX = 20f + Mathf.Sin(v) * 3f + Mathf.Cos(u) * 2.8f;
                    float fieldY = 20f + Mathf.Cos(v) * 2.6f + Mathf.Sin(u) * 3f;
                    float mottling = Mathf.PerlinNoise(fieldX, fieldY);
                    float grain = Pick(random, -0.10f, 0.10f);
                    float tone = 0.86f + (mottling - 0.5f) * (style == 1 ? 0.4f : 0.22f) + grain;
                    if (style == 0 && Mathf.Abs(mottling - 0.54f) < 0.016f && Mathf.Cos(v) > -0.42)
                        tone *= 0.81f;
                    if (style == 3)
                        tone += Mathf.Sin(v * 9f + Mathf.Sin(u) * 3f) * 0.032f;
                    if (style == 2 && grain < -0.05f)
                        tone -= 0.09f;
                    pixels[y * size + x] = new Color(tone, tone, tone);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return SaveAsset(texture, "asset");
        }

        private sealed class TerrainMesh
        {
            private readonly bool water;
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector2> uv = new();
            private readonly List<int> triangles = new();

            public TerrainMesh(bool water = false) => this.water = water;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                uv.Add(water ? new Vector2(0, a.z) : new Vector2(a.x, a.z) * GrainUvScale);
                uv.Add(water ? new Vector2(0, b.z) : new Vector2(b.x, b.z) * GrainUvScale);
                uv.Add(water ? new Vector2(1, c.z) : new Vector2(c.x, c.z) * GrainUvScale);
                uv.Add(water ? new Vector2(1, d.z) : new Vector2(d.x, d.z) * GrainUvScale);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                return mesh;
            }
        }
    }
}
