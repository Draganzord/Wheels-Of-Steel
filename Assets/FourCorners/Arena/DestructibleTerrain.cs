using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FourCorners
{
    [DisallowMultipleComponent]
    public sealed class DestructibleTerrain : MonoBehaviour
    {
        public const int CraterCapacity = 12;
        private const float MaxDepth = 0.95f;
        private const float MaxRim = 0.13f;

        [SerializeField] private MeshCollider[] dryGround = Array.Empty<MeshCollider>();
        [SerializeField] private Bounds[] protectedAreas = Array.Empty<Bounds>();
        [SerializeField] private Bounds playableYard;
        [SerializeField] private Material disturbedSoil;
        [SerializeField] private Transform lanePaint;

        private GroundZone[] zones;

        public int CraterCount { get; private set; }

        public void Configure(MeshCollider[] terrain, Bounds[] exclusions, Bounds yard, Material soil, Transform paint)
        {
            dryGround = terrain;
            protectedAreas = exclusions;
            playableYard = yard;
            disturbedSoil = soil;
            lanePaint = paint;
        }

        public bool ApplyBlast(Vector3 center, float radius, float power)
        {
            if (CraterCount >= CraterCapacity || !(power > 0f) || !(radius > 0f)
                || !float.IsFinite(center.x) || !float.IsFinite(center.y) || !float.IsFinite(center.z))
                return false;

            radius = Mathf.Clamp(radius, 1.8f, 5f);
            if (!CanCrater(center, radius))
                return false;

            zones ??= new GroundZone[dryGround.Length];
            float depth = MaxDepth * Mathf.Clamp01(power);
            bool changed = false;
            for (int i = 0; i < dryGround.Length; i++)
            {
                MeshCollider ground = dryGround[i];
                if (!ground || !IntersectsCircle(ground.bounds, center, radius))
                    continue;

                zones[i] ??= new GroundZone(ground, disturbedSoil);
                changed |= zones[i].Deform(center, radius, depth);
            }

            if (!changed)
                return false;

            CraterCount++;
            EraseLanePaint(center, radius);
            return true;
        }

        private void EraseLanePaint(Vector3 center, float radius)
        {
            if (!lanePaint)
                return;

            foreach (Transform mark in lanePaint)
                if (mark.gameObject.activeSelf && mark.TryGetComponent(out Renderer renderer)
                    && IntersectsCircle(renderer.bounds, center, radius * 0.88f))
                    mark.gameObject.SetActive(false);
        }

        private bool CanCrater(Vector3 center, float radius)
        {
            Vector3 min = playableYard.min;
            Vector3 max = playableYard.max;
            if (center.x - radius < min.x || center.x + radius > max.x
                || center.z - radius < min.z || center.z + radius > max.z)
                return false;

            foreach (Bounds area in protectedAreas)
                if (IntersectsCircle(area, center, radius))
                    return false;

            Ray probe = new Ray(center + Vector3.up * 1.4f, Vector3.down);
            foreach (MeshCollider ground in dryGround)
                if (ground && ground.Raycast(probe, out RaycastHit hit, 3.1f)
                    && Mathf.Abs(hit.point.y - center.y) <= 1.7f)
                    return true;
            return false;
        }

        private static bool IntersectsCircle(Bounds bounds, Vector3 center, float radius)
        {
            float x = center.x - Mathf.Clamp(center.x, bounds.min.x, bounds.max.x);
            float z = center.z - Mathf.Clamp(center.z, bounds.min.z, bounds.max.z);
            return x * x + z * z <= radius * radius;
        }

        private void OnDestroy()
        {
            if (zones == null)
                return;

            foreach (GroundZone zone in zones)
                zone?.Release();
        }

        private sealed class GroundZone
        {
            private readonly MeshCollider collider;
            private readonly MeshFilter rendererMesh;
            private readonly Mesh source;
            private readonly Material soil;
            private readonly Vector3[] initial;
            private readonly Vector3[] vertices;
            private readonly Color32[] colors;
            private Mesh runtimeMesh;

            public GroundZone(MeshCollider target, Material soil)
            {
                collider = target;
                rendererMesh = collider.GetComponent<MeshFilter>();
                source = target.sharedMesh;
                this.soil = soil;
                initial = source.vertices;
                vertices = (Vector3[])initial.Clone();
                colors = new Color32[initial.Length];
            }

            public bool Deform(Vector3 worldCenter, float radius, float depth)
            {
                Vector3 center = collider.transform.InverseTransformPoint(worldCenter);
                float radiusSquared = radius * radius;
                bool changed = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 delta = vertices[i] - center;
                    float distanceSquared = delta.x * delta.x + delta.z * delta.z;
                    if (distanceSquared >= radiusSquared)
                        continue;

                    float distance = Mathf.Sqrt(distanceSquared) / radius;
                    float bowl = 1f - SmoothRange(0.09f, 0.90f, distance);
                    float rim = SmoothRange(0.65f, 0.84f, distance) * (1f - SmoothRange(0.84f, 1f, distance));
                    float oldY = vertices[i].y;
                    float baseY = initial[i].y;
                    float newY = Mathf.Clamp(oldY - depth * (bowl - rim * 0.11f), baseY - MaxDepth, baseY + MaxRim);
                    if (Mathf.Abs(oldY - newY) < 0.0001f)
                        continue;

                    vertices[i].y = newY;
                    colors[i] = new Color(
                        Mathf.Clamp01((baseY - newY) / 0.65f),
                        Mathf.Clamp01((newY - baseY) / 0.088f), 0f);
                    changed = true;
                }

                if (changed)
                    RefreshMesh();
                return changed;
            }

            private static float SmoothRange(float begin, float end, float distance) =>
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(begin, end, distance));

            private void RefreshMesh()
            {
                if (!runtimeMesh)
                {
                    runtimeMesh = Instantiate(source);
                    runtimeMesh.name = source.name + " Deformed";
                    rendererMesh.sharedMesh = runtimeMesh;
                    if (soil)
                    {
                        GameObject overlay = new GameObject("Exposed crater soil");
                        overlay.transform.SetParent(collider.transform, false);
                        overlay.transform.localPosition = Vector3.up * 0.015f;
                        overlay.AddComponent<MeshFilter>().sharedMesh = runtimeMesh;
                        MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = soil;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }

                runtimeMesh.vertices = vertices;
                runtimeMesh.colors32 = colors;
                runtimeMesh.RecalculateNormals();
                runtimeMesh.RecalculateBounds();
                collider.sharedMesh = null;
                collider.sharedMesh = runtimeMesh;
            }

            public void Release()
            {
                if (runtimeMesh)
                    Destroy(runtimeMesh);
            }
        }
    }
}
