using UnityEngine;

namespace FourCorners
{
    public sealed class CombatEffects : MonoBehaviour
    {
        public const int TracerCapacity = 12;
        public const int ShockwaveCapacity = 8;
        public const int ParticleCapacity = 360;
        private const float ShockwaveDuration = 0.43f;

        [SerializeField] private Material tracerMaterial;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shotSound;
        [SerializeField] private AudioClip blastSound;
        [SerializeField] private Material flareMaterial;
        [SerializeField] private Material streakMaterial;

        private readonly LineRenderer[] tracers = new LineRenderer[TracerCapacity];
        private readonly float[] expires = new float[TracerCapacity];
        private readonly LineRenderer[] shockwaves = new LineRenderer[ShockwaveCapacity];
        private readonly float[] shockStarted = new float[ShockwaveCapacity];
        private readonly float[] shockRadius = new float[ShockwaveCapacity];
        private ParticleSystem fire;
        private ParticleSystem smoke;
        private Light flash;
        private float flashUntil;
        private int nextTracer;
        private int nextShockwave;

        public int ActiveParticleCount => sparks.particleCount + (fire == null ? 0 : fire.particleCount + smoke.particleCount);
        public int ActiveShockwaveCount { get; private set; }

        public void Configure(Material material, ParticleSystem particles, AudioSource source,
            AudioClip shot, AudioClip blast)
        {
            tracerMaterial = material;
            sparks = particles;
            audioSource = source;
            shotSound = shot;
            blastSound = blast;
        }

        public void ConfigureBlastMaterials(Material flare, Material streak)
        {
            flareMaterial = flare;
            streakMaterial = streak;
        }

        private void Awake()
        {
            var sparkSettings = sparks.main;
            sparkSettings.maxParticles = 144;
            sparkSettings.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            if (flareMaterial != null)
                sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = flareMaterial;
            sparks.Play();
            for (int index = 0; index < tracers.Length; index++)
            {
                LineRenderer line = CreateLine("Tracer", tracerMaterial);
                line.positionCount = 2;
                line.startWidth = 0.105f;
                line.endWidth = 0.015f;
                tracers[index] = line;
            }

            if (flareMaterial == null || streakMaterial == null)
                return;

            fire = CreateEmitter("Blast fire", 96, -0.12f, 1.38f, 0.32f);
            smoke = CreateEmitter("Blast and rocket smoke", ParticleCapacity - 144 - 96, -0.06f, 1.96f, 0.66f);
            BuildShockwaves();

            GameObject lightObject = new("Blast flash");
            lightObject.transform.SetParent(transform, false);
            flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.shadows = LightShadows.None;
            flash.color = new Color(1f, 0.51f, 0.19f);
            flash.enabled = false;
        }

        private void Update()
        {
            for (int index = 0; index < tracers.Length; index++)
                if (tracers[index].enabled && Time.time >= expires[index])
                    tracers[index].enabled = false;

            if (flash == null)
                return;

            for (int index = 0; index < shockwaves.Length; index++)
            {
                if (!shockwaves[index].enabled)
                    continue;
                float phase = Mathf.Clamp01((Time.time - shockStarted[index]) / ShockwaveDuration);
                if (phase >= 1f)
                {
                    shockwaves[index].enabled = false;
                    ActiveShockwaveCount--;
                    continue;
                }
                DisplayShockwave(index, phase);
            }

            float flashRemaining = Mathf.Clamp01((flashUntil - Time.time) / 0.21f);
            flash.enabled = flashRemaining > 0f;
            if (flash.enabled)
                flash.intensity = flashRemaining * flashRemaining * 7.3f;
        }

        public void Shot(Vector3 from, Vector3 to)
        {
            int index = nextTracer;
            nextTracer = (nextTracer + 1) % tracers.Length;
            LineRenderer line = tracers[index];
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.enabled = true;
            expires[index] = Time.time + 0.047f;
            Emit(sparks, from, 5, 2.8f, 0.16f, new Color(1, 0.64f, 0.16f), 0.15f);
            if ((to - from).sqrMagnitude < 52 * 52 - 1)
                Emit(sparks, to, 7, 4, 0.32f, new Color(1, 0.73f, 0.32f), 0.12f);
            audioSource.PlayOneShot(shotSound, 0.53f);
        }

        public void Blast(Vector3 position, float radius = 4.7f)
        {
            float scale = Mathf.Clamp(radius / 4.7f, 0.35f, 1.7f);
            float boomVolume = Mathf.Lerp(0.54f, 0.98f, Mathf.InverseLerp(1.6f, 7.6f, radius));
            audioSource.PlayOneShot(blastSound, boomVolume);
            Emit(sparks, position, Mathf.RoundToInt(26 * scale), 11f * scale, 0.73f,
                new Color(1f, 0.57f, 0.14f), 0.24f * scale);
            if (fire == null)
            {
                Emit(sparks, position, 23, 3.8f, 1.23f, new Color(0.31f, 0.24f, 0.21f), 0.72f);
                return;
            }

            Vector3 core = position + Vector3.up * 0.28f;
            Emit(fire, core, 7, 2.2f * scale, 0.23f, new Color(1f, 0.91f, 0.49f), 1.25f * scale);
            Emit(fire, core, Mathf.RoundToInt(22 * scale), 5.8f * scale, 0.53f,
                new Color(1f, 0.34f, 0.045f, 0.92f), 0.9f * scale);
            Emit(smoke, core, Mathf.RoundToInt(20 * scale), 3.5f * scale, 1.65f,
                new Color(0.19f, 0.14f, 0.12f, 0.56f), 1.07f * scale);

            int index = nextShockwave;
            nextShockwave = (nextShockwave + 1) % shockwaves.Length;
            shockStarted[index] = Time.time;
            shockRadius[index] = Mathf.Clamp(radius, 1.6f, 8.6f);
            shockwaves[index].transform.position = position + Vector3.up * 0.16f;
            if (!shockwaves[index].enabled)
                ActiveShockwaveCount++;
            shockwaves[index].enabled = true;
            DisplayShockwave(index, 0f);

            flash.transform.position = core + Vector3.up * 0.45f;
            flash.range = shockRadius[index] * 1.5f;
            flashUntil = Time.time + 0.21f;
        }

        public void RocketExhaust(Vector3 position, Vector3 direction)
        {
            if (smoke == null)
                return;
            Vector3 scatter = Random.insideUnitSphere * 0.32f;
            smoke.Emit(new ParticleSystem.EmitParams
            {
                position = position + scatter,
                velocity = scatter * 2.4f - direction * 1.85f + Vector3.up * 0.35f,
                startColor = new Color(0.37f, 0.29f, 0.24f, 0.51f),
                startSize = Random.Range(0.39f, 0.7f),
                startLifetime = Random.Range(0.40f, 0.64f)
            }, 1);
        }

        private void BuildShockwaves()
        {
            var points = new Vector3[40];
            for (int point = 0; point < points.Length; point++)
            {
                float angle = Mathf.PI * 2f * point / points.Length;
                points[point] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            for (int index = 0; index < shockwaves.Length; index++)
            {
                LineRenderer wave = CreateLine("Blast shockwave", streakMaterial);
                wave.useWorldSpace = false;
                wave.loop = true;
                wave.positionCount = points.Length;
                wave.SetPositions(points);
                shockwaves[index] = wave;
            }
        }

        private void DisplayShockwave(int index, float phase)
        {
            LineRenderer wave = shockwaves[index];
            float progress = 1f - Mathf.Pow(1f - phase, 2.6f);
            float radius = shockRadius[index] * Mathf.Lerp(0.16f, 1.25f, progress);
            wave.transform.localScale = new Vector3(radius, 1f, radius);
            wave.widthMultiplier = Mathf.Lerp(0.34f, 0.07f, phase);
            Color color = Color.Lerp(new Color(1f, 0.82f, 0.48f), new Color(0.87f, 0.58f, 0.33f), phase);
            color.a = (1f - phase) * 0.76f;
            wave.startColor = color;
            wave.endColor = color;
        }

        private LineRenderer CreateLine(string label, Material material)
        {
            GameObject display = new(label);
            display.transform.SetParent(transform, false);
            LineRenderer line = display.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private ParticleSystem CreateEmitter(string label, int capacity, float gravity, float expansion, float lateFade)
        {
            var holder = new GameObject(label);
            holder.transform.SetParent(transform, false);
            var emitter = holder.AddComponent<ParticleSystem>();
            emitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = emitter.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = gravity;

            var emission = emitter.emission;
            emission.enabled = false;
            var shape = emitter.shape;
            shape.enabled = false;
            var size = emitter.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, expansion));

            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.83f, 0f), new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(lateFade, 0.58f), new GradientAlphaKey(0, 1f) });
            var effectsColor = emitter.colorOverLifetime;
            effectsColor.enabled = true;
            effectsColor.color = new ParticleSystem.MinMaxGradient(gradient);
            holder.GetComponent<ParticleSystemRenderer>().sharedMaterial = flareMaterial;
            emitter.Play();
            return emitter;
        }

        private static void Emit(ParticleSystem emitter, Vector3 position, int count, float speed, float lifetime,
            Color color, float size)
        {
            for (int index = 0; index < count; index++)
            {
                Vector3 velocity = Random.insideUnitSphere * speed;
                velocity.y = Mathf.Abs(velocity.y) + 0.4f;
                ParticleSystem.EmitParams particle = new()
                {
                    position = position,
                    velocity = velocity,
                    startSize = size * Random.Range(0.6f, 1.5f),
                    startLifetime = lifetime * Random.Range(0.65f, 1.2f),
                    startColor = color
                };
                emitter.Emit(particle, 1);
            }
        }
    }
}
