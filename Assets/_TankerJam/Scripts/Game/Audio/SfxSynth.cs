// Placeholder sound effects synthesized once at boot, porting the web prototype's WebAudio recipes
// (oscillators with exponential sweeps and envelopes, filtered noise bursts). Replace any of them with a
// recorded clip in the AudioCatalog asset; these only fill the gaps.
using System;
using UnityEngine;

namespace TankerJam.Game
{
    public static class SfxSynth
    {
        public const int Rate = 44100;
        /// <summary>Glug is generated at this pitch; AudioManager re-pitches it per glug.</summary>
        public const float GlugBaseHz = 300f;

        enum Wave { Sine, Square, Triangle, Saw }
        enum Filter { LowPass, HighPass, BandPass }

        public static AudioClip Glug() => Make("sfx_glug", 0.14f, d =>
        {
            Tone(d, 0f, Wave.Sine, GlugBaseHz * 1.25f, GlugBaseHz * 0.8f, 0.09f, 0.22f);
            Noise(d, 0f, 0.06f, Filter.BandPass, GlugBaseHz * 3f, 4f, 0.06f, 11);
        });

        public static AudioClip Clunk() => Make("sfx_clunk", 0.25f, d =>
        {
            Tone(d, 0f, Wave.Sine, 120f, 60f, 0.18f, 0.5f);
            Noise(d, 0f, 0.04f, Filter.HighPass, 2500f, 1f, 0.15f, 12);
        });

        public static AudioClip Ding() => Make("sfx_ding", 1.25f, d =>
        {
            Tone(d, 0.05f, Wave.Sine, 1046f, 1046f, 1.1f, 0.16f);
            Tone(d, 0.05f, Wave.Sine, 1568f, 1568f, 0.8f, 0.08f);
            Tone(d, 0.17f, Wave.Sine, 1318f, 1318f, 1.0f, 0.12f);
        });

        public static AudioClip Horn() => Make("sfx_horn", 0.34f, d =>
        {
            foreach (float t in new[] { 0f, 0.16f })
            {
                Tone(d, t, Wave.Square, 392f, 392f, 0.12f, 0.05f);
                Tone(d, t, Wave.Square, 494f, 494f, 0.12f, 0.04f);
            }
        });

        public static AudioClip Brake() => Make("sfx_brake", 0.4f, d => Noise(d, 0f, 0.35f, Filter.HighPass, 3000f, 0.7f, 0.08f, 13));

        public static AudioClip Bonk() => Make("sfx_bonk", 0.28f, d =>
        {
            Tone(d, 0f, Wave.Triangle, 140f, 70f, 0.15f, 0.35f);
            Tone(d, 0.05f, Wave.Square, 330f, 330f, 0.18f, 0.05f);
        });

        public static AudioClip Engine(float duration) => Make($"sfx_engine_{duration:0.0}", duration + 0.05f, d =>
        {
            // Sawtooth 42 -> 85 Hz through a low-pass, swelling in then out.
            int n = (int)(duration * Rate);
            double phase = 0;
            var lp = new Biquad(Filter.LowPass, 380f, 0.7f);
            for (int i = 0; i < n && i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(42f, 85f, t / duration);
                phase += f / Rate;
                float saw = (float)(2.0 * (phase - Math.Floor(phase + 0.5)));
                float g = t < 0.1f ? t / 0.1f * 0.1f : Mathf.Lerp(0.1f, 0f, (t - 0.1f) / (duration - 0.1f));
                d[i] += lp.Process(saw) * g;
            }
        });

        public static AudioClip Whoosh() => Make("sfx_whoosh", 0.95f, d => Noise(d, 0f, 0.9f, Filter.BandPass, 600f, 1.2f, 0.12f, 14));

        public static AudioClip Win() => Make("sfx_win", 0.95f, d =>
        {
            float[] notes = { 523f, 659f, 784f, 1046f };
            for (int i = 0; i < notes.Length; i++) Tone(d, i * 0.12f, Wave.Triangle, notes[i], notes[i], 0.5f, 0.12f);
        });

        /// <summary>Seamless band-passed noise loop; volume follows pumping.</summary>
        public static AudioClip PourHissLoop()
        {
            const float seconds = 2f;
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var bp = new Biquad(Filter.BandPass, 900f, 0.7f);
            var rng = new System.Random(21);
            // Run the filter twice over the buffer so the loop point has settled filter state.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                    d[i] = bp.Process((float)(rng.NextDouble() * 2 - 1));
            var clip = AudioClip.Create("sfx_pour_loop", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // ---------------- building blocks ----------------

        const float Gain = 2.2f; // WebAudio levels were mixed quietly; bring them up to a usable level

        static AudioClip Make(string name, float seconds, Action<float[]> fill)
        {
            var data = new float[(int)(seconds * Rate)];
            fill(data);
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * Gain, -1f, 1f);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Oscillator with an exponential frequency sweep, 10 ms attack and exponential decay (prototype tn()).</summary>
        static void Tone(float[] d, float start, Wave wave, float f0, float f1, float dur, float vol)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = f1 > 0f && f1 != f0 ? f0 * Mathf.Pow(f1 / f0, t / dur) : f0;
                phase += f / Rate;
                float x = (float)(phase - Math.Floor(phase));
                float w;
                switch (wave)
                {
                    case Wave.Square: w = x < 0.5f ? 1f : -1f; break;
                    case Wave.Triangle: w = 4f * Mathf.Abs(x - 0.5f) - 1f; break;
                    case Wave.Saw: w = 2f * x - 1f; break;
                    default: w = Mathf.Sin(2f * Mathf.PI * x); break;
                }
                d[s0 + i] += w * Envelope(t, dur, vol);
            }
        }

        /// <summary>Filtered noise burst with the same envelope (prototype nb()).</summary>
        static void Noise(float[] d, float start, float dur, Filter type, float freq, float q, float vol, int seed)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var f = new Biquad(type, freq, q);
            var rng = new System.Random(seed);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[s0 + i] += f.Process((float)(rng.NextDouble() * 2 - 1)) * Envelope(t, dur, vol);
            }
        }

        static float Envelope(float t, float dur, float vol)
        {
            const float attack = 0.005f;
            if (t < attack) return vol * t / attack;
            float u = (t - attack) / Mathf.Max(1e-4f, dur - attack);
            return vol * Mathf.Pow(0.001f / vol, u);
        }

        /// <summary>RBJ cookbook biquad.</summary>
        sealed class Biquad
        {
            readonly float b0, b1, b2, a1, a2;
            float x1, x2, y1, y2;

            public Biquad(Filter type, float freq, float q)
            {
                float w0 = 2f * Mathf.PI * freq / Rate, cos = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / (2f * q);
                float nb0, nb1, nb2;
                switch (type)
                {
                    case Filter.LowPass: nb0 = (1 - cos) / 2; nb1 = 1 - cos; nb2 = (1 - cos) / 2; break;
                    case Filter.HighPass: nb0 = (1 + cos) / 2; nb1 = -(1 + cos); nb2 = (1 + cos) / 2; break;
                    default: nb0 = alpha; nb1 = 0; nb2 = -alpha; break;
                }
                float a0 = 1 + alpha;
                b0 = nb0 / a0; b1 = nb1 / a0; b2 = nb2 / a0;
                a1 = -2 * cos / a0; a2 = (1 - alpha) / a0;
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
