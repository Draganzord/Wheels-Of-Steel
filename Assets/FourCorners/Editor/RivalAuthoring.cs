using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FourCorners.Editor
{
    public static class RivalAuthoring
    {
        private const string VehicleRoot = "Assets/FourCorners/Vehicles";
        private const string ArtRoot = VehicleRoot + "/Rivals";

        public static CombatVehicle[] AddRivals(ArenaLayout arena, SalvageEncounter encounter)
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
                AssetDatabase.CreateFolder(VehicleRoot, "Rivals");

            GameObject muscle = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleRoot + "/MuscleCar.prefab");
            if (muscle == null)
                muscle = VehicleAuthoring.CreatePrefab(false);

            var rivals = new GameObject("Rivals").transform;
            rivals.SetParent(arena.transform.parent, false);
            return new[]
            {
                Spawn(muscle, rivals, encounter, RivalKind.Gunner, arena.Loop[5], arena.Loop[6]),
                Spawn(muscle, rivals, encounter, RivalKind.Interceptor, arena.Loop[4], arena.Loop[3])
            };
        }

        private static CombatVehicle Spawn(GameObject muscle, Transform parent, SalvageEncounter encounter,
            RivalKind role, Transform approach, Transform exit)
        {
            bool gunner = role == RivalKind.Gunner;
            string callsign = gunner ? "Rivet" : "Jackal";
            GameObject variant = MakeVariant(muscle, callsign, gunner);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(variant, parent);
            Vector3 position = Vector3.Lerp(approach.position, exit.position, gunner ? 0.7f : 0.64f);
            Vector3 heading = exit.position - approach.position;
            heading.y = 0f;
            instance.transform.SetPositionAndRotation(position + Vector3.up * 0.74f, Quaternion.LookRotation(heading));
            instance.AddComponent<RivalDriver>().Configure(encounter, role, callsign, gunner ? 23f : 20f,
                gunner ? 1.2f : 3.0f);
            return instance.GetComponent<CombatVehicle>();
        }

        private static GameObject MakeVariant(GameObject muscle, string label, bool gunner)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(muscle);
            instance.name = label;
            try
            {
                var tuning = Tune(label, muscle.GetComponent<ArcadeVehicle>().Settings, gunner);
                var vehicle = new SerializedObject(instance.GetComponent<ArcadeVehicle>());
                vehicle.FindProperty("settings").objectReferenceValue = tuning;
                var bumpers = vehicle.FindProperty("bumperColliders");
                int cabinIndex = bumpers.arraySize;
                bumpers.arraySize = cabinIndex + 1;
                bumpers.GetArrayElementAtIndex(cabinIndex).objectReferenceValue = Cabin(instance.transform);
                vehicle.ApplyModifiedPropertiesWithoutUndo();

                var body = instance.GetComponent<Rigidbody>();
                body.mass = tuning.Mass;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                Dress(instance.transform, label, gunner);
                SurfaceAuthoring.ConfigureEffects(instance.AddComponent<VehicleSurfaceEffects>());
                instance.AddComponent<CombatVehicle>().Configure(gunner ? 65f : 70f, new Vector3(0f, 0.31f, 2.52f),
                    0.19f, 5f);

                GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, $"{ArtRoot}/{label}.prefab");
                if (variant == null)
                    throw new InvalidOperationException($"Could not save rival prefab {label}.");
                return variant;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static VehicleSettings Tune(string label, VehicleSettings muscle, bool gunner)
        {
            string path = $"{ArtRoot}/{label}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<VehicleSettings>(path);
            if (settings != null)
                return settings;

            settings = Object.Instantiate(muscle);
            settings.name = label;
            settings.Mass = gunner ? 1190f : 1070f;
            settings.Spring = gunner ? 27000f : 24500f;
            settings.DriveForce = gunner ? 14100f : 15100f;
            settings.TopSpeed = gunner ? 22.5f : 26.5f;
            settings.LowSpeedSteer = gunner ? 42f : 50f;
            settings.HighSpeedSteer = gunner ? 20f : 25f;
            settings.SteerRate = gunner ? 360f : 470f;
            settings.LateralStiffness = gunner ? 2290f : 2510f;
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        private static BoxCollider Cabin(Transform parent)
        {
            var root = new GameObject("Cabin collision");
            root.transform.SetParent(parent, false);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.69f, -0.13f);
            collider.size = new Vector3(1.19f, 0.51f, 1.49f);
            collider.sharedMaterial = parent.GetComponent<BoxCollider>().sharedMaterial;
            return collider;
        }

        private static void Dress(Transform parent, string label, bool gunner)
        {
            Material paint = MaterialFor(label + " Body", gunner ? new Color32(217, 131, 39, 255) : new Color32(57, 153, 116, 255), 0.38f);
            Material accent = MaterialFor(label + " Stripe", gunner ? new Color32(42, 36, 32, 255) : new Color32(235, 219, 175, 255), 0.29f);
            Material metal = MaterialFor("Rival Chassis Trim", new Color32(53, 50, 46, 255), 0.46f);
            foreach (var renderer in parent.GetComponentsInChildren<MeshRenderer>())
            {
                switch (renderer.gameObject.name)
                {
                    case "Muscle car body":
                    case "Roof cap":
                    case "Raised hood":
                    case "Window pillar":
                    case "Mirror arms":
                    case "Side beltline":
                        SetFinish(renderer, paint);
                        break;
                    case "Hood stripe":
                    case "Roof stripe":
                    case "Deck stripe":
                        SetFinish(renderer, accent);
                        break;
                    case "Gun saddle":
                    case "Gun shroud":
                    case "Gun muzzle silhouette":
                        if (!gunner)
                        {
                            renderer.gameObject.SetActive(false);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
                        }
                        break;
                }
            }

            if (gunner)
            {
                Block(parent, "Armor lip", new Vector3(0f, -0.012f, 2.24f), new Vector3(1.69f, 0.19f, 0.14f), metal);
                Block(parent, "Roof radio box", new Vector3(-0.34f, 1.039f, -0.38f), new Vector3(0.25f, 0.15f, 0.29f), metal);
                Block(parent, "Radio whip", new Vector3(-0.37f, 1.30f, -0.43f), new Vector3(0.035f, 0.48f, 0.034f), metal,
                    new Vector3(-16f, 0f, -7f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Block(parent, "Gun-car wheel guard", new Vector3(side * 0.84f, 0.34f, 1.49f),
                        new Vector3(0.18f, 0.14f, 0.75f), metal, new Vector3(0f, side * -8f, 0f));
                }
            }
            else
            {
                Block(parent, "Tail wing", new Vector3(0f, 0.72f, -1.87f), new Vector3(1.68f, 0.08f, 0.31f), accent,
                    new Vector3(-9f, 0f, 0f));
                Block(parent, "Front air dam", new Vector3(0f, -0.13f, 2.20f), new Vector3(1.63f, 0.091f, 0.21f), metal);
                for (int side = -1; side <= 1; side += 2)
                {
                    Block(parent, "Wing pillar", new Vector3(side * 0.49f, 0.59f, -1.88f), new Vector3(0.09f, 0.24f, 0.19f), metal);
                    Block(parent, "Side intake", new Vector3(side * 0.92f, 0.26f, -0.69f), new Vector3(0.10f, 0.18f, 0.37f), metal);
                }
            }
        }

        private static void Block(Transform parent, string name, Vector3 center, Vector3 size, Material material,
            Vector3 rotation = default)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(center, Quaternion.Euler(rotation));
            part.transform.localScale = size;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void SetFinish(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        private static Material MaterialFor(string name, Color32 color, float smoothness)
        {
            string path = $"{ArtRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0.18f);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
