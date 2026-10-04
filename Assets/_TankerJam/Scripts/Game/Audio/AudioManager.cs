// Plays GameController cues. Pooled one-shot sources (no runtime AudioSource creation), a dedicated
// glug source re-pitched per glug, and one looping pour hiss whose volume follows pumping.
// Mute is saved in PlayerPrefs and survives sessions.
using UnityEngine;

namespace TankerJam.Game
{
    public sealed class AudioManager : MonoBehaviour
    {
        const string MuteKey = "tj.sound.muted";
        const int PoolSize = 8;

        [SerializeField] GameController game;
        [SerializeField] AudioCatalog catalog;

        AudioSource[] pool;
        AudioSource glugSource, loopSource;
        int next;
        float minGlug = 170f, maxGlug = 690f, sfxVolume = 0.8f, loopVolume = 0.12f;
        AudioClip glug, clunk, ding, horn, brake, bonk, engineShort, engineLong, whoosh, win;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 0f : 1f;
            }
        }

#if UNITY_EDITOR
        public void EditorWire(GameController controller, AudioCatalog audioCatalog)
        {
            game = controller;
            catalog = audioCatalog;
        }
#endif

        void Awake()
        {
            if (catalog != null)
            {
                minGlug = catalog.MinGlugHz;
                maxGlug = catalog.MaxGlugHz;
                sfxVolume = catalog.SfxVolume;
                loopVolume = catalog.PourLoopVolume;
            }
            glug = Pick(catalog ? catalog.Glug : null, SfxSynth.Glug);
            clunk = Pick(catalog ? catalog.Clunk : null, SfxSynth.Clunk);
            ding = Pick(catalog ? catalog.Ding : null, SfxSynth.Ding);
            horn = Pick(catalog ? catalog.Horn : null, SfxSynth.Horn);
            brake = Pick(catalog ? catalog.Brake : null, SfxSynth.Brake);
            bonk = Pick(catalog ? catalog.Bonk : null, SfxSynth.Bonk);
            engineShort = catalog && catalog.EngineShort ? catalog.EngineShort : SfxSynth.Engine(1.4f);
            engineLong = catalog && catalog.EngineLong ? catalog.EngineLong : SfxSynth.Engine(1.8f);
            whoosh = Pick(catalog ? catalog.Whoosh : null, SfxSynth.Whoosh);
            win = Pick(catalog ? catalog.Win : null, SfxSynth.Win);

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource($"Sfx{i}");
            glugSource = NewSource("Glug");
            loopSource = NewSource("PourLoop");
            loopSource.clip = catalog && catalog.PourLoop ? catalog.PourLoop : SfxSynth.PourHissLoop();
            loopSource.loop = true;
            loopSource.volume = 0f;
            loopSource.Play();

            AudioListener.volume = Muted ? 0f : 1f;
        }

        static AudioClip Pick(AudioClip recorded, System.Func<AudioClip> synth) => recorded != null ? recorded : synth();

        AudioSource NewSource(string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void OnEnable()
        {
            if (game != null) game.Cue += OnCue;
        }

        void OnDisable()
        {
            if (game != null) game.Cue -= OnCue;
        }

        void Update()
        {
            float target = game != null && game.IsPouring ? loopVolume : 0f;
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, Time.unscaledDeltaTime * 0.6f);
        }

        void OnCue(GameCue cue, float arg)
        {
            switch (cue)
            {
                case GameCue.Bonk: Play(bonk); break;
                case GameCue.Depart: Play(engineShort); break;
                case GameCue.Brake: Play(brake); break;
                case GameCue.Glug:
                    // Pitch rises with fill: 170 Hz empty -> 690 Hz full, with a little randomness.
                    float hz = Mathf.Lerp(minGlug, maxGlug, arg) + Random.value * 30f;
                    glugSource.pitch = Mathf.Clamp(hz / SfxSynth.GlugBaseHz, 0.1f, 3f);
                    glugSource.PlayOneShot(glug, sfxVolume);
                    break;
                case GameCue.TruckFull:
                    Play(clunk);
                    Play(ding, 0.14f);
                    break;
                case GameCue.Leave:
                    Play(horn);
                    Play(engineLong);
                    break;
                case GameCue.VipLift: Play(whoosh); break;
                case GameCue.ExtraBayOpened: Play(ding); break;
                case GameCue.Win: Play(win); break;
                case GameCue.Jam: Play(bonk); break;
            }
        }

        void Play(AudioClip clip, float delay = 0f)
        {
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.Stop();
            s.pitch = 1f;
            s.clip = clip;
            s.volume = sfxVolume;
            if (delay > 0f) s.PlayDelayed(delay);
            else s.Play();
        }
    }
}
