using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// Recorded sound effects. Any slot left empty falls back to the synthesized placeholder (SfxSynth),
    /// so audio can be replaced one clip at a time.
    /// </summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public AudioClip Glug, Clunk, Ding, Horn, Brake, Bonk, EngineShort, EngineLong, Whoosh, Win, PourLoop;

        [Header("Mix")]
        [Range(0, 1)] public float SfxVolume = 0.8f;
        [Range(0, 1)] public float PourLoopVolume = 0.12f;
        [Tooltip("Glug pitch maps from this frequency (empty truck) to MaxGlugHz (full), like the prototype.")]
        public float MinGlugHz = 170f;
        public float MaxGlugHz = 690f;
    }
}
