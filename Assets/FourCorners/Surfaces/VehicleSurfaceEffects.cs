using System;
using UnityEngine;

namespace FourCorners
{
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(ArcadeVehicle))]
    public sealed class VehicleSurfaceEffects : MonoBehaviour
    {
        public const int TrackCapacity = 4096;
        public const int ParticleCapacity = 176;
        private const float MovingSpeed = 0.8f;
        private const float WetCarryDistance = 8f;

        [SerializeField] private Material trackMaterial;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private SurfaceProfile[] profiles;

        private struct WheelHistory
        {
            public SurfaceProfile MarkSurface;
            public Vector3 PreviousPoint;
            public Vector3 PreviousNormal;
            public float DistanceToNextMark;
            public float WetDistance;
            public float SprayBudget;
            public bool InWater;
        }

        private readonly WheelHistory[] histories = new WheelHistory[4];
        private readonly AudioSource[] rolls = new AudioSource[5];
        private readonly float[] rollingLevels = new float[5];
        private readonly float[] rollingPitches = new float[5];
        private ArcadeVehicle vehicle;
        private GameObject effectsRoot;
        private WheelTrackMesh tracks;
        private ParticleSystem plumes;
        private ParticleSystem spray;

        public int ActiveTrackCount => tracks?.Count ?? 0;
        public int ActiveParticleCount => plumes == null ? 0 : plumes.particleCount + spray.particleCount;
        public int ActiveWetContacts { get; private set; }
        public int WetSandPrintsPlaced { get; private set; }

        public void Configure(Material tracksMaterial, Material particlesMaterial, SurfaceProfile[] surfaceProfiles)
        {
            trackMaterial = tracksMaterial;
            particleMaterial = particlesMaterial;
            profiles = surfaceProfiles;
        }

        private void Start()
        {
            vehicle = GetComponent<ArcadeVehicle>();
            if (trackMaterial == null || particleMaterial == null)
            {
                enabled = false;
                return;
            }

            effectsRoot = new GameObject($"{gameObject.name} Surface Effects");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effectsRoot, gameObject.scene);
            tracks = new WheelTrackMesh(effectsRoot.transform, trackMaterial, TrackCapacity);
            plumes = CreateEmitter("Dust", 64, true);
            spray = CreateEmitter("Spray and clods", ParticleCapacity - 64, false);
            BuildRollingAudio();
        }

        private void FixedUpdate()
        {
            ActiveWetContacts = 0;
            Array.Clear(rollingLevels, 0, rollingLevels.Length);
            Array.Clear(rollingPitches, 0, rollingPitches.Length);

            WheelContact[] contacts = vehicle.Contacts;
            if (!vehicle.IsWreck && contacts != null)
            {
                for (int index = 0; index < Mathf.Min(contacts.Length, histories.Length); index++)
                    UpdateWheel(ref histories[index], contacts[index]);
            }
            UpdateRollingAudio();
        }

        private void LateUpdate()
        {
            tracks.Present();
        }

        private void OnDestroy()
        {
            tracks?.Dispose();
            if (effectsRoot != null)
                Destroy(effectsRoot);
        }

        private void UpdateWheel(ref WheelHistory history, WheelContact contact)
        {
            SurfaceProfile surface = contact.Surface;
            if (!contact.Grounded || surface == null)
            {
                history.MarkSurface = null;
                history.InWater = false;
                history.SprayBudget = 0f;
                return;
            }

            bool water = surface.Kind == SurfaceKind.Water;
            float speed = Vector3.ProjectOnPlane(contact.Velocity, contact.Normal).magnitude;
            float slide = Mathf.InverseLerp(0.8f, 9f, contact.Slip);
            if (water)
            {
                ActiveWetContacts++;
                history.WetDistance = WetCarryDistance;
                if (!history.InWater && speed > 1.4f)
                {
                    int splashDrops = Mathf.RoundToInt(Mathf.Lerp(4f, 13f, Mathf.InverseLerp(1.4f, 14f, speed)));
                    EmitSpray(contact, speed, slide, splashDrops, true);
                }
            }
            else
            {
                history.WetDistance = Mathf.Max(0f, history.WetDistance - speed * Time.fixedDeltaTime);
            }
            history.InWater = water;

            if (speed < MovingSpeed)
            {
                history.MarkSurface = null;
                history.SprayBudget = 0f;
                return;
            }

            int kind = (int)surface.Kind;
            float audioEnergy = Mathf.Lerp(0.75f, 1.6f, slide) * Mathf.InverseLerp(MovingSpeed, 17f, speed);
            rollingLevels[kind] += audioEnergy * surface.AudioLevel * 0.25f;
            rollingPitches[kind] = Mathf.Max(rollingPitches[kind], 0.72f + speed * 0.025f + 0.3f * slide);

            bool wetSand = surface.Kind == SurfaceKind.Sand && history.WetDistance > 0f;
            if (contact.Slip >= surface.MinimumMarkSlip || wetSand)
                LeaveMarks(ref history, contact, slide);
            else
                history.MarkSurface = null;

            float sprayEnergy = surface.Kind switch
            {
                SurfaceKind.Asphalt => slide * slide,
                SurfaceKind.Grass => slide * 0.75f,
                SurfaceKind.Mud => 0.3f + slide,
                SurfaceKind.Sand => 0.45f + slide,
                SurfaceKind.Water => 1f + 0.5f * slide,
                _ => 0f
            };
            history.SprayBudget += surface.SprayRate * sprayEnergy * Mathf.InverseLerp(MovingSpeed, 15f, speed) * Time.fixedDeltaTime;
            int sprayCount = Mathf.FloorToInt(history.SprayBudget);
            if (sprayCount > 0)
            {
                history.SprayBudget -= sprayCount;
                EmitSpray(contact, speed, slide, Mathf.Min(sprayCount, 3), false);
            }
        }

        private void LeaveMarks(ref WheelHistory history, WheelContact contact, float slide)
        {
            SurfaceProfile surface = contact.Surface;
            bool water = surface.Kind == SurfaceKind.Water;
            Vector3 point = contact.Point;
            if (water)
                point.y = contact.SurfaceHeight;
            Vector3 normal = water ? Vector3.up : contact.Normal;
            Vector3 travel = point - history.PreviousPoint;
            float distance = travel.magnitude;
            float spacing = Mathf.Max(0.1f, surface.MarkSpacing);
            if (history.MarkSurface != surface || distance > 4f)
            {
                history.MarkSurface = surface;
                history.DistanceToNextMark = spacing * 0.4f;
                history.PreviousPoint = point;
                history.PreviousNormal = normal;
                return;
            }

            while (history.DistanceToNextMark <= distance && distance > 0.001f)
            {
                float progress = history.DistanceToNextMark / distance;
                Vector3 stampNormal = Vector3.Lerp(history.PreviousNormal, normal, progress).normalized;
                Vector3 center = Vector3.Lerp(history.PreviousPoint, point, progress) + stampNormal * 0.023f;
                Color tint = Color.Lerp(surface.MarkTint, surface.SlideTint, slide);
                float width = surface.MarkWidth * (1f + slide * 0.75f);
                float length = spacing * (surface.Kind == SurfaceKind.Sand ? 0.97f : 1.17f);
                if (surface.Kind == SurfaceKind.Sand)
                {
                    Color wetTint = new(0.12f, 0.084f, 0.055f, 0.82f);
                    tint = Color.Lerp(tint, wetTint, history.WetDistance / WetCarryDistance);
                    if (history.WetDistance > 0f)
                        WetSandPrintsPlaced++;
                }
                if (water)
                {
                    length = 1.45f + slide * 0.4f;
                    width *= 1.25f;
                }

                tracks.Add(center, stampNormal, travel, new Vector2(width, length), tint, surface.Kind, slide);
                history.DistanceToNextMark += spacing;
            }

            history.DistanceToNextMark -= distance;
            history.PreviousPoint = point;
            history.PreviousNormal = normal;
        }

        private void EmitSpray(WheelContact contact, float speed, float slide, int count, bool entry)
        {
            SurfaceProfile surface = contact.Surface;
            SurfaceKind kind = surface.Kind;
            bool water = kind == SurfaceKind.Water;
            bool dusty = kind == SurfaceKind.Asphalt || kind == SurfaceKind.Sand;
            Vector3 origin = contact.Point + Vector3.up * 0.05f;
            if (water)
                origin.y = contact.SurfaceHeight + 0.03f;

            Vector3 direction = -Vector3.ProjectOnPlane(contact.Velocity, Vector3.up).normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, direction);
            for (int i = 0; i < count; i++)
            {
                bool cloud = dusty && (kind == SurfaceKind.Asphalt || UnityEngine.Random.value < 0.38f);
                float scatter = UnityEngine.Random.Range(-1f, 1f);
                float upward = entry ? UnityEngine.Random.Range(1.9f, 4.5f) : UnityEngine.Random.Range(0.55f, water ? 2.7f : 1.6f);
                float back = Mathf.Min(speed * (cloud ? 0.1f : 0.2f), cloud ? 1.55f : 3f);
                if (kind == SurfaceKind.Mud)
                    back *= 0.5f;

                Color tint = surface.SprayTint;
                if (kind == SurfaceKind.Sand && !cloud)
                    tint.a = Mathf.Min(1f, tint.a * 1.85f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = origin + lateral * (scatter * 0.12f),
                    velocity = direction * back + lateral * (scatter * (0.65f + slide)) + Vector3.up * upward,
                    startColor = tint,
                    startSize = cloud ? UnityEngine.Random.Range(0.37f, 0.78f) : UnityEngine.Random.Range(0.08f, water ? 0.16f : 0.19f),
                    startLifetime = cloud ? UnityEngine.Random.Range(0.35f, 0.7f) : UnityEngine.Random.Range(0.21f, entry ? 0.83f : 0.45f)
                };
                (cloud ? plumes : spray).Emit(particle, 1);
            }
        }

        private ParticleSystem CreateEmitter(string label, int cap, bool cloud)
        {
            var container = new GameObject(label);
            container.transform.SetParent(effectsRoot.transform, false);
            var emitter = container.AddComponent<ParticleSystem>();
            emitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = emitter.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = cap;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = cloud ? -0.08f : 0.95f;
            var emission = emitter.emission;
            emission.enabled = false;
            var shape = emitter.shape;
            shape.enabled = false;

            var fade = emitter.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[]
            {
                new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f)
            });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            if (cloud)
            {
                var expansion = emitter.sizeOverLifetime;
                expansion.enabled = true;
                expansion.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1.85f));
            }

            var display = container.GetComponent<ParticleSystemRenderer>();
            display.sharedMaterial = particleMaterial;
            display.renderMode = cloud ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            display.lengthScale = 1.4f;
            display.velocityScale = 0.08f;
            emitter.Play();
            return emitter;
        }

        private void BuildRollingAudio()
        {
            if (profiles == null)
                return;

            foreach (SurfaceProfile profile in profiles)
            {
                if (profile == null || profile.RollingAudio == null)
                    continue;

                var source = gameObject.AddComponent<AudioSource>();
                source.clip = profile.RollingAudio;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0.65f;
                source.minDistance = 7f;
                source.maxDistance = 32f;
                source.volume = 0f;
                rolls[(int)profile.Kind] = source;
            }
        }

        private void UpdateRollingAudio()
        {
            for (int i = 0; i < rolls.Length; i++)
            {
                AudioSource source = rolls[i];
                if (source == null)
                    continue;

                source.volume = Mathf.MoveTowards(source.volume, Mathf.Min(1f, rollingLevels[i]), Time.fixedDeltaTime * 3f);
                if (rollingPitches[i] > 0f)
                    source.pitch = Mathf.MoveTowards(source.pitch, rollingPitches[i], Time.fixedDeltaTime * 2f);
                if (source.volume > 0.002f && !source.isPlaying)
                    source.Play();
                else if (source.volume <= 0.002f && source.isPlaying)
                    source.Stop();
            }
        }
    }
}
