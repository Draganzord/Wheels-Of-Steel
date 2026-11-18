using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FourCorners.Editor
{
    public static class SliceBuilder
    {
        public const string ScenePath = "Assets/Scenes/FourCornersSalvageYard.unity";
        private const string Generated = "Assets/FourCorners/Encounter/Generated";

        [MenuItem("Four Corners/Rebuild Playable Scene")]
        private static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeLighting();

            SurfaceProfile[] profiles = SurfaceAuthoring.CreateProfiles();
            ArenaLayout arena = ArenaAuthoring.Build(new GameObject("Four Corners Salvage Yard").transform, profiles);
            CombatVehicle car = MakeVehicle(arena.PlayerSpawn, false);
            CombatVehicle truck = MakeVehicle(arena.TruckSpawn, true);

            GameObject session = new("Encounter");
            CombatEffects effects = MakeCombatFeedback(session.transform);
            SalvageEncounter encounter = session.AddComponent<SalvageEncounter>();
            encounter.Configure(car, truck, arena, effects, MakeMine());
            DesktopDriver input = session.AddComponent<DesktopDriver>();
            input.Configure(encounter, AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            TruckDriver ai = truck.gameObject.AddComponent<TruckDriver>();
            ai.Configure(encounter);
            ExpandEncounter(encounter, arena, effects);

            GameObject cameraObject = new("Yard Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 22.5f;
            camera.nearClipPlane = 0.35f;
            camera.farClipPlane = 185;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.61f, 0.43f, 0.29f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<YardCamera>().Configure(car.GetComponent<ArcadeVehicle>());
            session.AddComponent<YardHud>().Configure(encounter, ai, camera);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save {ScenePath}.");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(entry => entry.path != ScenePath)).ToArray();
            PlayerSettings.productName = "Four Corners Salvage Yard";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 810;
            AssetDatabase.SaveAssets();
            Debug.Log($"Four Corners playable scene saved to {ScenePath}.");
        }

        internal static void ExpandEncounter(SalvageEncounter encounter, ArenaLayout arena, CombatEffects effects)
        {
            RocketAuthoring.ConfigureBlastEffects(effects);
            encounter.ConfigureExpansion(RivalAuthoring.AddRivals(arena, encounter), RocketAuthoring.CreatePrefab(),
                BlastBarrelAuthoring.AddBarrels(arena.transform, encounter));
        }

        [MenuItem("Four Corners/Build Windows Player")]
        public static void BuildWindows()
        {
            string output = Path.GetFullPath("Builds/FourCorners");
            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(output, "Four Corners Salvage Yard.exe"),
                target = BuildTarget.StandaloneWindows64
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows player build {report.summary.result}.");

            Debug.Log($"Four Corners Windows build saved to {output}.");
        }

        private static CombatVehicle MakeVehicle(Transform marker, bool truck)
        {
            GameObject prefab = VehicleAuthoring.CreatePrefab(truck);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(marker.position, marker.rotation);
            SurfaceAuthoring.ConfigureEffects(instance.AddComponent<VehicleSurfaceEffects>());
            CombatVehicle combat = instance.AddComponent<CombatVehicle>();
            combat.Configure(truck ? 155 : 100, new Vector3(0, 0.53f, truck ? 3.15f : 2.55f));
            return combat;
        }

        private static void MakeLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.72f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.57f, 0.49f, 0.41f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.27f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.58f, 0.4f);
            RenderSettings.fogStartDistance = 92;
            RenderSettings.fogEndDistance = 165;
            GameObject sun = new("Late Afternoon Sun");
            sun.transform.rotation = Quaternion.Euler(44, -45, 0);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.84f, 0.66f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.87f;
            RenderSettings.sun = light;
        }

        private static CombatEffects MakeCombatFeedback(Transform parent)
        {
            GameObject root = new("Combat Feedback");
            root.transform.SetParent(parent, false);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.maxParticles = 144;
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            Material sparks = MakeMaterial("Sparks", "Universal Render Pipeline/Particles/Unlit", Color.white);
            sparks.SetFloat("_Surface", 1);
            sparks.SetFloat("_ZWrite", 0);
            sparks.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            sparks.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            sparks.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sparks.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(sparks);
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparks;

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            CombatEffects effects = root.AddComponent<CombatEffects>();
            effects.Configure(MakeMaterial("Tracer", "Universal Render Pipeline/Unlit", new Color(1, 0.78f, 0.22f)),
                particles, audio, MakeSound("Gun", 0.13f, 157, 40), MakeSound("Explosion", 0.52f, 56, 9));
            return effects;
        }

        private static YardMine MakeMine()
        {
            GameObject root = new("Yard Mine");
            YardMine mine = root.AddComponent<YardMine>();
            Material oxide = MakeMaterial("Mine Oxide", "Universal Render Pipeline/Lit", new Color(0.31f, 0.13f, 0.064f));
            Material steel = MakeMaterial("Mine Steel", "Universal Render Pipeline/Lit", new Color(0.14f, 0.15f, 0.14f));
            Material lamp = MakeMaterial("Mine Lamp", "Universal Render Pipeline/Unlit", new Color(1f, 0.15f, 0.042f));
            MinePiece(root.transform, "Rim", new Vector3(0, 0.05f, 0), new Vector3(0.64f, 0.065f, 0.64f), steel);
            MinePiece(root.transform, "Oxide cap", new Vector3(0, 0.13f, 0), new Vector3(0.49f, 0.047f, 0.49f), oxide);
            GameObject light = MinePiece(root.transform, "Arming lamp", new Vector3(0, 0.18f, 0), new Vector3(0.15f, 0.028f, 0.15f), lamp);
            mine.SetWarningLight(light);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Generated}/Yard Mine.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<YardMine>();
        }

        private static GameObject MinePiece(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        internal static Material MakeMaterial(string name, string shaderName, Color color)
        {
            string path = $"{Generated}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AudioClip MakeSound(string name, float length, float pitch, float decay)
        {
            string path = $"{Generated}/{name}.wav";
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * length);
            int bytes = samples * sizeof(short);
            System.Random random = new(358);
            using (BinaryWriter wave = new(File.Create(path)))
            {
                wave.Write("RIFF".ToCharArray());
                wave.Write(36 + bytes);
                wave.Write("WAVEfmt ".ToCharArray());
                wave.Write(16);
                wave.Write((short)1);
                wave.Write((short)1);
                wave.Write(sampleRate);
                wave.Write(sampleRate * sizeof(short));
                wave.Write((short)sizeof(short));
                wave.Write((short)16);
                wave.Write("data".ToCharArray());
                wave.Write(bytes);
                for (int index = 0; index < samples; index++)
                {
                    float time = index / (float)sampleRate;
                    float noise = (float)random.NextDouble() * 2 - 1;
                    float thump = Mathf.Sin(2 * Mathf.PI * pitch * time / (1 + 4 * time));
                    float sample = (noise * 0.38f + thump * 0.62f) * Mathf.Exp(-time * decay);
                    wave.Write((short)(sample * short.MaxValue * 0.82f));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
