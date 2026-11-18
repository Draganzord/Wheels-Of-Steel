using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace FourCorners
{
    internal sealed class WheelTrackMesh : IDisposable
    {
        private const MeshUpdateFlags UpdateFlags = MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds;
        private static readonly Bounds WorldBounds = new(Vector3.zero, Vector3.one * 1024f);
        private static readonly int TimeProperty = Shader.PropertyToID("_EvidenceTime");

        [StructLayout(LayoutKind.Sequential)]
        private struct TrackVertex
        {
            public Vector3 Position;
            public Color32 Tint;
            public Vector2 UV;
            public Vector3 Details;
        }

        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new();
        private readonly TrackVertex[] quad = new TrackVertex[4];
        private readonly int capacity;
        private int cursor;
        private int visibleCount;

        public int Count { get; private set; }

        public WheelTrackMesh(Transform root, Material material, int capacity)
        {
            this.capacity = capacity;
            var display = new GameObject("Wheel Evidence");
            display.transform.SetParent(root, false);
            renderer = display.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            mesh = new Mesh { name = "Wheel stamps" };
            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(capacity * 4,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3));
            mesh.SetIndexBufferParams(capacity * 6, IndexFormat.UInt16);
            var indices = new ushort[capacity * 6];
            for (int stamp = 0; stamp < capacity; stamp++)
            {
                int vertex = stamp * 4;
                int index = stamp * 6;
                indices[index] = (ushort)vertex;
                indices[index + 1] = (ushort)(vertex + 1);
                indices[index + 2] = (ushort)(vertex + 2);
                indices[index + 3] = (ushort)(vertex + 2);
                indices[index + 4] = (ushort)(vertex + 1);
                indices[index + 5] = (ushort)(vertex + 3);
            }

            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, UpdateFlags);
            mesh.bounds = WorldBounds;
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0) { bounds = WorldBounds }, UpdateFlags);
            display.AddComponent<MeshFilter>().sharedMesh = mesh;
        }

        public void Add(Vector3 center, Vector3 normal, Vector3 heading, Vector2 size, Color color,
            SurfaceKind kind, float slide)
        {
            Vector3 forward = Vector3.ProjectOnPlane(heading, normal).normalized * (size.y * 0.5f);
            Vector3 side = Vector3.Cross(normal, forward).normalized * (size.x * 0.5f);
            Vector3 details = new((float)kind, slide, Time.time);
            SetCorner(0, center - side - forward, 0f, 0f, color, details);
            SetCorner(1, center + side - forward, 1f, 0f, color, details);
            SetCorner(2, center - side + forward, 0f, 1f, color, details);
            SetCorner(3, center + side + forward, 1f, 1f, color, details);
            mesh.SetVertexBufferData(quad, 0, cursor * 4, quad.Length, 0, UpdateFlags);
            cursor = (cursor + 1) % capacity;
            Count = Mathf.Min(Count + 1, capacity);
        }

        public void Present()
        {
            if (Count != visibleCount)
            {
                visibleCount = Count;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, visibleCount * 6)
                {
                    bounds = WorldBounds,
                    vertexCount = visibleCount * 4
                }, UpdateFlags);
            }

            properties.SetFloat(TimeProperty, Time.time);
            renderer.SetPropertyBlock(properties);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
        }

        private void SetCorner(int index, Vector3 position, float u, float v, Color32 tint, Vector3 details)
        {
            quad[index] = new TrackVertex { Position = position, UV = new Vector2(u, v), Tint = tint, Details = details };
        }
    }
}
