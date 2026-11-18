using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FourCorners.Editor
{
    public static class SurfaceAuthoring
    {
        private const string Root = "Assets/FourCorners/Surfaces";
        private const string Tuning = Root + "/Tuning";
        private const string Materials = Root + "/Materials";
        private const string Audio = Root + "/Audio";

        public static SurfaceProfile[] CreateProfiles()
        {
            EnsureFolder("Tuning");
            EnsureFolder("Audio");
            var profiles = new SurfaceProfile[5];
            foreach (SurfaceKind kind in Enum.GetValues(typeof(SurfaceKind)))
            {
                string profilePath = $"{Tuning}/{kind}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<SurfaceProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<SurfaceProfile>();
                    profile.Kind = kind;
                    SetDefaults(profile);
                    AssetDatabase.CreateAsset(profile, profilePath);
                }

                if (profile.RollingAudio == null)
                {
                    profile.RollingAudio = CreateRollingAudio(kind);
                    EditorUtility.SetDirty(profile);
                }
                profiles[(int)kind] = profile;
            }

            return profiles;
        }

        public static void ConfigureEffects(VehicleSurfaceEffects effects)
        {
            EnsureFolder("Materials");
            effects.Configure(
                LoadOrCreateMaterial("Wheel Evidence", "Four Corners/Wheel Evidence"),
                LoadOrCreateMaterial("Wheel Spray", "Four Corners/Wheel Spray"),
                CreateProfiles());
            EditorUtility.SetDirty(effects);
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder($"{Root}/{folder}"))
                AssetDatabase.CreateFolder(Root, folder);
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Missing surface shader {shaderName}.");

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetDefaults(SurfaceProfile profile)
        {
            switch (profile.Kind)
            {
                case SurfaceKind.Asphalt:
                    profile.Grip = 1.18f;
                    profile.RollingResistance = 0.016f;
                    profile.DriveScale = 1f;
                    profile.MarkTint = new Color(0.065f, 0.045f, 0.034f, 0.57f);
                    profile.SlideTint = new Color(0.024f, 0.020f, 0.018f, 0.87f);
                    profile.MarkSpacing = 0.35f;
                    profile.MarkWidth = 0.31f;
                    profile.MinimumMarkSlip = 2.25f;
                    profile.SprayTint = new Color(0.73f, 0.69f, 0.62f, 0.37f);
                    profile.SprayRate = 11f;
                    profile.AudioLevel = 0.38f;
                    break;
                case SurfaceKind.Grass:
                    profile.Grip = 0.92f;
                    profile.RollingResistance = 0.045f;
                    profile.DriveScale = 0.92f;
                    profile.MarkTint = new Color(0.23f, 0.21f, 0.088f, 0.69f);
                    profile.SlideTint = new Color(0.20f, 0.104f, 0.046f, 0.88f);
                    profile.MarkSpacing = 0.34f;
                    profile.MarkWidth = 0.35f;
                    profile.SprayTint = new Color(0.42f, 0.48f, 0.154f, 0.89f);
                    profile.SprayRate = 16f;
                    profile.AudioLevel = 0.41f;
                    break;
                case SurfaceKind.Mud:
                    profile.Grip = 0.59f;
                    profile.RollingResistance = 0.138f;
                    profile.DriveScale = 0.76f;
                    profile.MarkTint = new Color(0.14f, 0.071f, 0.040f, 0.86f);
                    profile.SlideTint = new Color(0.094f, 0.046f, 0.023f, 0.94f);
                    profile.MarkSpacing = 0.36f;
                    profile.MarkWidth = 0.36f;
                    profile.SprayTint = new Color(0.29f, 0.123f, 0.056f, 0.95f);
                    profile.SprayRate = 23f;
                    profile.AudioLevel = 0.55f;
                    break;
                case SurfaceKind.Sand:
                    profile.Grip = 0.79f;
                    profile.RollingResistance = 0.081f;
                    profile.DriveScale = 0.82f;
                    profile.MarkTint = new Color(0.41f, 0.248f, 0.135f, 0.74f);
                    profile.SlideTint = new Color(0.32f, 0.204f, 0.107f, 0.78f);
                    profile.MarkSpacing = 0.37f;
                    profile.MarkWidth = 0.34f;
                    profile.SprayTint = new Color(0.84f, 0.63f, 0.36f, 0.37f);
                    profile.SprayRate = 19f;
                    profile.AudioLevel = 0.48f;
                    break;
                case SurfaceKind.Water:
                    profile.Grip = 0.83f;
                    profile.RollingResistance = 0.189f;
                    profile.DriveScale = 0.73f;
                    profile.MarkTint = new Color(0.59f, 0.91f, 0.86f, 0.82f);
                    profile.SlideTint = new Color(0.78f, 0.95f, 0.91f, 0.89f);
                    profile.MarkSpacing = 0.48f;
                    profile.MarkWidth = 0.96f;
                    profile.SprayTint = new Color(0.71f, 0.92f, 1f, 0.94f);
                    profile.SprayRate = 26f;
                    profile.AudioLevel = 0.67f;
                    break;
            }
        }

        private static AudioClip CreateRollingAudio(SurfaceKind kind)
        {
            string path = $"{Audio}/{kind} Roll.wav";
            if (!File.Exists(path))
            {
                WriteRollingWave(path, kind);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void WriteRollingWave(string path, SurfaceKind kind)
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            var random = new System.Random(9020 + (int)kind);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)random.NextDouble() * 2f - 1f;

            using var writer = new BinaryWriter(File.Create(path));
            int length = samples.Length * sizeof(short);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(length + 36);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(length);

            float low = 0f;
            float medium = 0f;
            for (int i = -768; i < samples.Length; i++)
            {
                float noise = samples[(i + samples.Length) % samples.Length];
                low = Mathf.Lerp(low, noise, 0.042f);
                medium = Mathf.Lerp(medium, noise, 0.22f);
                if (i < 0)
                    continue;

                float t = (float)i / sampleRate * Mathf.PI * 2f;
                float sample = kind switch
                {
                    SurfaceKind.Asphalt => low * 0.50f + Sine(t, 76) * 0.048f + Sine(t, 115) * 0.023f,
                    SurfaceKind.Grass => medium * 0.36f + (noise - medium) * (0.055f + 0.028f * Sine(t, 17)),
                    SurfaceKind.Mud => low * (0.71f + 0.28f * Sine(t, 11)) + medium * 0.15f + Sine(t, 43) * 0.032f,
                    SurfaceKind.Sand => medium * (0.51f + 0.15f * Sine(t, 19)) + noise * 0.045f,
                    SurfaceKind.Water => medium * (0.46f + 0.15f * Sine(t, 7) + 0.085f * Sine(t, 13)) + noise * 0.077f,
                    _ => 0f
                };
                writer.Write((short)(Mathf.Clamp(sample, -0.99f, 0.99f) * short.MaxValue));
            }
        }

        private static float Sine(float t, int frequency) => Mathf.Sin(t * frequency);
    }
}
