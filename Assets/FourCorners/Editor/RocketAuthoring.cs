using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FourCorners.Editor
{
    public static class RocketAuthoring
    {
        private const string EncounterPath = "Assets/FourCorners/Encounter";
        private const string Generated = EncounterPath + "/Generated";
        private const string FeedbackShader = "Four Corners/Combat Feedback";

        public static YardRocket CreatePrefab()
        {
            EnsureFolder();
            Material shape = Surface("Rocket Ivory", "Universal Render Pipeline/Lit", new Color(0.68f, 0.63f, 0.48f));
            Material burnt = Surface("Rocket Rust", "Universal Render Pipeline/Lit", new Color(0.46f, 0.094f, 0.035f));
            Material jet = Surface("Rocket Glow", "Universal Render Pipeline/Unlit", new Color(1f, 0.68f, 0.15f));
            Material streak = FeedbackMaterial("Blast Streak", 1f);

            GameObject model = new("Yard Rocket");
            model.AddComponent<YardRocket>();
            Part(model.transform, "Ivory shell", PrimitiveType.Cylinder, new Vector3(0, 0, 0.02f),
                new Vector3(0.34f, 0.52f, 0.34f), Quaternion.Euler(90, 0, 0), shape);
            Part(model.transform, "Orange cap", PrimitiveType.Sphere, new Vector3(0, 0, 0.53f),
                new Vector3(0.34f, 0.34f, 0.46f), Quaternion.identity, burnt);
            Part(model.transform, "Tail collar", PrimitiveType.Cylinder, new Vector3(0, 0, -0.45f),
                new Vector3(0.41f, 0.11f, 0.41f), Quaternion.Euler(90, 0, 0), burnt);
            Part(model.transform, "Lower tail vane", PrimitiveType.Cube, new Vector3(0, 0, -0.38f),
                new Vector3(0.68f, 0.04f, 0.32f), Quaternion.Euler(0, 0, 45), burnt);
            Part(model.transform, "Upper tail vane", PrimitiveType.Cube, new Vector3(0, 0, -0.38f),
                new Vector3(0.68f, 0.04f, 0.32f), Quaternion.Euler(0, 0, -45), burnt);
            Part(model.transform, "Amber flare", PrimitiveType.Sphere, new Vector3(0, 0, -0.72f),
                new Vector3(0.21f, 0.21f, 0.63f), Quaternion.identity, jet);

            ConfigureTrail(model.transform, "Rocket smoke ribbon", streak, 0.40f, 0.78f,
                new Color(0.52f, 0.41f, 0.35f, 0.49f), new Color(0.30f, 0.24f, 0.22f, 0f),
                new AnimationCurve(new Keyframe(0, 0.36f), new Keyframe(0.34f, 1f), new Keyframe(1, 0.29f)));
            ConfigureTrail(model.transform, "Rocket exhaust ribbon", streak, 0.24f, 0.49f,
                new Color(1f, 0.88f, 0.44f, 0.98f), new Color(1f, 0.27f, 0.045f, 0f),
                AnimationCurve.EaseInOut(0, 1, 1, 0));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, $"{Generated}/Yard Rocket.prefab");
            Object.DestroyImmediate(model);
            return prefab.GetComponent<YardRocket>();
        }

        public static void ConfigureBlastEffects(CombatEffects effects)
        {
            EnsureFolder();
            effects.ConfigureBlastMaterials(FeedbackMaterial("Blast Flare", 0f), FeedbackMaterial("Blast Streak", 1f));
            EditorUtility.SetDirty(effects);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Generated))
                AssetDatabase.CreateFolder(EncounterPath, "Generated");
        }

        private static void Part(Transform root, string label, PrimitiveType type, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = label;
            part.transform.SetParent(root, false);
            part.transform.SetLocalPositionAndRotation(position, rotation);
            part.transform.localScale = scale;
            var display = part.GetComponent<MeshRenderer>();
            display.sharedMaterial = material;
            display.shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static void ConfigureTrail(Transform root, string label, Material material, float age,
            float width, Color start, Color end, AnimationCurve taper)
        {
            GameObject tail = new(label);
            tail.transform.SetParent(root, false);
            tail.transform.localPosition = new Vector3(0, 0, -0.68f);
            var trail = tail.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = age;
            trail.minVertexDistance = 0.14f;
            trail.widthCurve = taper;
            trail.widthMultiplier = width;
            trail.startColor = start;
            trail.endColor = end;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 3;
        }

        private static Material Surface(string name, string shader, Color color)
        {
            Material material = MaterialAsset(name, shader);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material FeedbackMaterial(string name, float streak)
        {
            Material material = MaterialAsset(name, FeedbackShader);
            material.SetFloat("_Streak", streak);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MaterialAsset(string name, string shaderName)
        {
            string path = $"{Generated}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Missing rocket shader {shaderName}.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
