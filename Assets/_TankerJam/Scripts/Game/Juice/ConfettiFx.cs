using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// Win confetti: a code-configured particle system (no prefab) that bursts candy-colored flakes over the
    /// lot. Particles are emitted from a pool owned by the system, so a burst allocates nothing.
    /// </summary>
    public sealed class ConfettiFx : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] Shader particleShader;
        [SerializeField] int burstCount = 140;

        ParticleSystem system;
        ParticleSystem.EmitParams emit;
        Color[] colors;

#if UNITY_EDITOR
        public void EditorWire(GameController controller, Shader shader)
        {
            game = controller;
            particleShader = shader;
        }
#endif

        void Awake()
        {
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var rot = system.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.2f;

            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = SharedMeshes.Box;
            r.alignment = ParticleSystemRenderSpace.World;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (particleShader != null) r.sharedMaterial = new Material(particleShader) { name = "Confetti" };
        }

        void OnEnable()
        {
            if (game != null) game.Cue += OnCue;
        }

        void OnDisable()
        {
            if (game != null) game.Cue -= OnCue;
        }

        void OnCue(GameCue cue, float arg)
        {
            if (cue == GameCue.Win) Burst();
        }

        public void Burst()
        {
            if (colors == null)
            {
                var pal = game.Config.Palette;
                colors = new Color[pal.Oils.Length + 2];
                for (int i = 0; i < pal.Oils.Length; i++) colors[i] = pal.Oils[i].Color;
                colors[pal.Oils.Length] = Color.white;
                colors[pal.Oils.Length + 1] = pal.Vip;
            }
            var L = game.Layout;
            var center = L != null ? new Vector3(0f, 6f, L.LotCenter.Z - 2f) : new Vector3(0f, 6f, 0f);
            for (int i = 0; i < burstCount; i++)
            {
                emit.position = center + new Vector3(Random.Range(-3f, 3f), Random.Range(-0.5f, 0.5f), Random.Range(-3f, 3f));
                emit.velocity = new Vector3(Random.Range(-5f, 5f), Random.Range(4f, 10f), Random.Range(-5f, 5f));
                emit.startSize3D = new Vector3(Random.Range(0.12f, 0.22f), 0.02f, Random.Range(0.06f, 0.12f));
                emit.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                emit.startColor = colors[Random.Range(0, colors.Length)];
                system.Emit(emit, 1);
            }
        }
    }
}
