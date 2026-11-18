using UnityEngine;

namespace FourCorners
{
    public sealed class CombatEffects : MonoBehaviour
    {
        public const int TracerCapacity = 12;
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shotSound;
        [SerializeField] private AudioClip blastSound;

        private readonly LineRenderer[] tracers = new LineRenderer[TracerCapacity];
        private readonly float[] expires = new float[TracerCapacity];
        private int nextTracer;

        public void Configure(Material material, ParticleSystem particles, AudioSource source,
            AudioClip shot, AudioClip blast)
        {
            tracerMaterial = material;
            sparks = particles;
            audioSource = source;
            shotSound = shot;
            blastSound = blast;
        }

        private void Awake()
        {
            sparks.Play();
            for (int index = 0; index < tracers.Length; index++)
            {
                GameObject tracer = new("Tracer");
                tracer.transform.SetParent(transform, false);
                LineRenderer line = tracer.AddComponent<LineRenderer>();
                line.sharedMaterial = tracerMaterial;
                line.positionCount = 2;
                line.startWidth = 0.105f;
                line.endWidth = 0.015f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                tracers[index] = line;
            }
        }

        private void Update()
        {
            for (int index = 0; index < tracers.Length; index++)
                if (tracers[index].enabled && Time.time >= expires[index])
                    tracers[index].enabled = false;
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
            Emit(from, 5, 2.8f, 0.16f, new Color(1, 0.64f, 0.16f), 0.15f);
            if ((to - from).sqrMagnitude < 52 * 52 - 1)
                Emit(to, 7, 4, 0.32f, new Color(1, 0.73f, 0.32f), 0.12f);
            audioSource.PlayOneShot(shotSound, 0.53f);
        }

        public void Blast(Vector3 position)
        {
            Emit(position, 36, 10, 0.65f, new Color(1, 0.42f, 0.08f), 0.24f);
            Emit(position, 23, 4, 1.35f, new Color(0.31f, 0.24f, 0.21f), 0.72f);
            audioSource.PlayOneShot(blastSound, 0.85f);
        }

        private void Emit(Vector3 position, int count, float speed, float lifetime, Color color, float size)
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
                sparks.Emit(particle, 1);
            }
        }
    }
}
