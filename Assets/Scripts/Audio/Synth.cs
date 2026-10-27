using UnityEngine;

namespace Convergence.Audio
{
    /// <summary>
    /// The placeholder sounds, synthesised in code the way <c>Spr</c> draws the placeholder
    /// sprites: no asset in the project, nothing to import, the same on a clean checkout. A real
    /// recording dropped into <see cref="GameSound"/> replaces any of them; until then these keep
    /// the game from being silent.
    ///
    /// Everything is mono at <see cref="Rate"/> (22 050 Hz is plenty for thuds, wind and drones,
    /// and halves the memory and the build time). Each builder is deterministic from its seed, so
    /// the variants of a sound are the same every session.
    /// </summary>
    public static partial class Synth
    {
        public const int Rate = 22050;

        // ------------------------------------------------------------------ building blocks

        /// <summary>A small fast PRNG - System.Random would do, but this one is cheap per sample
        /// and never touches UnityEngine.Random (seeded rolls elsewhere depend on its state).</summary>
        public struct Noise
        {
            uint _s;
            public Noise(int seed) { _s = (uint)seed * 2654435761u + 1013904223u; if (_s == 0) _s = 1; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / (float)0x800000 - 1f;
            }
            public float Range(float a, float b) => a + (Next() * 0.5f + 0.5f) * (b - a);
        }

        /// <summary>A state-variable filter (the trapezoidal "TPT" form): low, band and high pass
        /// from one state, stable at any cutoff and Q - the Chamberlin form it replaced blew up
        /// near the top of the range and clipped. Cutoff may move every sample (sweeps), which is
        /// why it is not a fixed biquad.</summary>
        public struct Svf
        {
            float _ic1, _ic2;
            public float Low, Band, High;
            public void Run(float x, float cutoffHz, float q)
            {
                float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(cutoffHz, 10f, Rate * 0.45f) / Rate);
                float k = 1f / Mathf.Max(0.3f, q);
                float a1 = 1f / (1f + g * (g + k)), a2 = g * a1, a3 = g * a2;
                float v3 = x - _ic2;
                float v1 = a1 * _ic1 + a2 * v3;
                float v2 = _ic2 + a2 * _ic1 + a3 * v3;
                _ic1 = 2f * v1 - _ic1;
                _ic2 = 2f * v2 - _ic2;
                Low = v2; Band = v1; High = x - k * v1 - v2;
            }
        }

        /// <summary>Attack, then an exponential fall to a thousandth over <paramref name="decay"/>.</summary>
        public static float Env(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return Mathf.Exp(-6.9f * (t - attack) / Mathf.Max(0.001f, decay));
        }

        static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * Rate))];

        /// <summary>Scales the buffer so its loudest sample sits at <paramref name="peak"/>. Every
        /// sound is normalised here and its loudness set in Tuning.Audio, so a builder's internal
        /// gains never decide how loud anything plays.</summary>
        static float[] Normalise(float[] s, float peak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
            if (max > 0f) { float k = peak / max; for (int i = 0; i < s.Length; i++) s[i] *= k; }
            // A few samples' fade at each end, so no clip starts or stops with a click.
            int edge = Mathf.Min(32, s.Length / 4);
            for (int i = 0; i < edge; i++) { float g = i / (float)edge; s[i] *= g; s[s.Length - 1 - i] *= g; }
            return s;
        }

        public static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>A falling sine - the body of every impact. Phase is integrated, so the sweep
        /// never clicks.</summary>
        static void Thump(float[] s, float from, float to, float sweep, float decay, float gain, float start = 0f)
        {
            float phase = 0f;
            int s0 = (int)(start * Rate);
            for (int i = s0; i < s.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float f = to + (from - to) * Mathf.Exp(-t / Mathf.Max(0.001f, sweep));
                phase += 2f * Mathf.PI * f / Rate;
                s[i] += Mathf.Sin(phase) * Env(t, 0.002f, decay) * gain;
            }
        }

        /// <summary>Filtered noise under an envelope. <paramref name="mode"/> 0 low, 1 band, 2 high.</summary>
        static void Hiss(float[] s, int seed, int mode, float cutFrom, float cutTo, float q,
                         float attack, float decay, float gain, float start = 0f)
        {
            var n = new Noise(seed);
            var f = new Svf();
            int s0 = (int)(start * Rate);
            float span = Mathf.Max(0.001f, attack + decay);
            for (int i = s0; i < s.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float cut = Mathf.Lerp(cutFrom, cutTo, Mathf.Clamp01(t / span));
                f.Run(n.Next(), cut, q);
                float v = mode == 0 ? f.Low : mode == 1 ? f.Band : f.High;
                s[i] += v * Env(t, attack, decay) * gain;
            }
        }

        /// <summary>Inharmonic partials - metal, bells, a struck blade.</summary>
        static void Ring(float[] s, float[] freqs, float[] gains, float attack, float decay, float start = 0f)
        {
            int s0 = (int)(start * Rate);
            for (int k = 0; k < freqs.Length; k++)
            {
                float w = 2f * Mathf.PI * freqs[k] / Rate;
                float d = decay / (1f + k * 0.35f);   // higher partials die first, as a bell's do
                for (int i = s0; i < s.Length; i++)
                {
                    float t = (i - s0) / (float)Rate;
                    s[i] += Mathf.Sin(w * (i - s0)) * Env(t, attack, d) * gains[k];
                }
            }
        }

        // ------------------------------------------------------------------ footsteps

        public static float[] StepStone(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.14f);
            Hiss(s, seed, 0, n.Range(1400, 2200), 500, 0.8f, 0.001f, 0.035f, 0.9f);
            Thump(s, n.Range(150, 190), 70, 0.02f, 0.05f, 0.7f);
            // the heel, then the ball of the foot a hair later
            Hiss(s, seed + 7, 1, n.Range(2500, 3400), 2000, 1.2f, 0.001f, 0.02f, 0.35f, n.Range(0.018f, 0.03f));
            return Normalise(s);
        }

        public static float[] StepSand(int seed)
        {
            var s = Buffer(0.22f);
            var n = new Noise(seed);
            var f = new Svf();
            // a scrunch: grains (sparse clicks) through a band, under a soft swell
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = n.Next() > 0.82f ? n.Next() : n.Next() * 0.15f;
                f.Run(grain, 2600f, 0.9f);
                s[i] += f.Band * Env(t, 0.03f, 0.13f);
            }
            Thump(s, 120, 60, 0.03f, 0.05f, 0.25f);
            return Normalise(s);
        }

        public static float[] StepWater(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.3f);
            Hiss(s, seed, 1, 700, n.Range(2600, 3400), 1.4f, 0.004f, 0.16f, 0.9f);
            // two or three bloops - a bubble's pitch RISES as it collapses
            int drops = 2 + (seed & 1);
            for (int d = 0; d < drops; d++)
            {
                float start = n.Range(0.01f, 0.12f), f0 = n.Range(380, 700);
                float phase = 0f;
                for (int i = (int)(start * Rate); i < s.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    float f = f0 * (1f + 1.6f * Mathf.Min(1f, t / 0.06f));
                    phase += 2f * Mathf.PI * f / Rate;
                    s[i] += Mathf.Sin(phase) * Env(t, 0.002f, 0.06f) * 0.35f;
                }
            }
            return Normalise(s);
        }

        public static float[] StepAsh(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.18f);
            Hiss(s, seed, 0, 900, 300, 0.7f, 0.002f, 0.06f, 0.5f);
            // crackle: a handful of bright ticks
            for (int c = 0; c < 6; c++)
            {
                int at = (int)(n.Range(0.0f, 0.12f) * Rate);
                for (int i = 0; i < 40 && at + i < s.Length; i++)
                    s[at + i] += n.Next() * Mathf.Exp(-i / 6f) * 0.8f;
            }
            return Normalise(s);
        }

        public static float[] StepMetal(int seed)
        {
            var n = new Noise(seed);
            var s = StepStone(seed);
            float[] ring = new float[s.Length + Rate / 6];
            System.Array.Copy(s, ring, s.Length);
            Ring(ring, new[] { n.Range(1150, 1300), n.Range(2850, 3100), n.Range(4300, 4700) },
                 new[] { 0.25f, 0.18f, 0.1f }, 0.001f, 0.12f, 0.005f);
            return Normalise(ring);
        }

        // ------------------------------------------------------------------ combat

        /// <summary>A landed hit. Weight 0 basic, 1 Light, 2 Medium, 3 Heavy - each heavier,
        /// lower and longer, so the ear tells the weight classes apart as the eye does.</summary>
        public static float[] Hit(int weight, int seed)
        {
            var n = new Noise(seed);
            float len = 0.12f + weight * 0.09f;
            var s = Buffer(len + 0.05f);
            // the edge: a bright cut of noise falling in pitch
            Hiss(s, seed, 1, n.Range(3200, 4200) - weight * 400, 900, 0.9f, 0.001f, 0.03f + weight * 0.015f, 0.8f);
            // the body
            Thump(s, 190 - weight * 30, 55 - weight * 6, 0.025f + weight * 0.01f, 0.06f + weight * 0.06f,
                  0.7f + weight * 0.15f);
            if (weight >= 2)   // a crunch under the medium and heavy
                Hiss(s, seed + 3, 0, 1200, 250, 0.7f, 0.002f, 0.06f + weight * 0.05f, 0.5f, 0.004f);
            return Normalise(s);
        }

        /// <summary>The crit's ring, laid over the hit - a struck-metal shimmer.</summary>
        public static float[] Crit(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.35f);
            float f = n.Range(2050, 2300);
            Ring(s, new[] { f, f * 1.51f, f * 2.27f }, new[] { 0.5f, 0.3f, 0.18f }, 0.001f, 0.28f);
            return Normalise(s);
        }

        /// <summary>Two blows meeting: a bright metallic clang - inharmonic partials over a hard
        /// attack, a scrape of noise and a small knock under it. Distinct from the crit's clean ring.</summary>
        public static float[] Clash(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.4f);
            float f = n.Range(1180, 1380);
            Ring(s, new[] { f, f * 1.37f, f * 2.11f, f * 2.93f }, new[] { 0.45f, 0.32f, 0.22f, 0.12f }, 0.0005f, 0.32f);
            Hiss(s, seed, 1, n.Range(3800, 4800), 2200, 1.5f, 0.0005f, 0.06f, 0.7f);
            Thump(s, 260, 170, 0.01f, 0.05f, 0.35f);
            return Normalise(s);
        }

        public static float[] PlayerHurt(int seed)
        {
            var s = Buffer(0.22f);
            Thump(s, 140, 60, 0.03f, 0.12f, 1f);
            Hiss(s, seed, 0, 700, 200, 0.6f, 0.002f, 0.09f, 0.6f);
            return Normalise(s);
        }

        public static float[] EnemyDeath(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.55f);
            Thump(s, n.Range(210, 250), 45, 0.18f, 0.4f, 0.8f);
            Hiss(s, seed, 0, 1600, 150, 0.6f, 0.005f, 0.4f, 0.6f);
            return Normalise(s);
        }

        public static float[] PlayerDeath()
        {
            var s = Buffer(2.2f);
            Thump(s, 90, 30, 0.6f, 1.6f, 1f);
            Hiss(s, 99, 0, 600, 80, 0.6f, 0.01f, 1.8f, 0.5f);
            Ring(s, new[] { 110f, 164.8f, 233f }, new[] { 0.25f, 0.18f, 0.12f }, 0.3f, 1.8f);
            return Normalise(s);
        }

        // ------------------------------------------------------------------ the timing bar

        /// <summary>PERFECT: a bell, three partials over a soft attack - the sound worth chasing.</summary>
        public static float[] Perfect()
        {
            var s = Buffer(1.3f);
            Ring(s, new[] { 880f, 1318.5f, 1760f, 2637f }, new[] { 0.5f, 0.35f, 0.25f, 0.12f }, 0.004f, 1.1f);
            Ring(s, new[] { 883f, 1322f }, new[] { 0.2f, 0.12f }, 0.004f, 1.1f);   // the beat that makes it shimmer
            return Normalise(s, 0.8f);
        }

        public static float[] Good()
        {
            var s = Buffer(0.5f);
            Ring(s, new[] { 659.3f, 1318.5f }, new[] { 0.5f, 0.15f }, 0.003f, 0.38f);
            return Normalise(s, 0.6f);
        }

        /// <summary>EARLY or MISSED: a dull wooden knock - not a punishment, just no reward.</summary>
        public static float[] Miss()
        {
            var s = Buffer(0.12f);
            Thump(s, 320, 240, 0.02f, 0.05f, 1f);
            return Normalise(s, 0.5f);
        }

        // ------------------------------------------------------------------ ambience

        /// <summary>
        /// A seamless loop: <paramref name="render"/> fills seconds+fade, and the tail is
        /// crossfaded into the head so the last sample runs straight into the first.
        /// </summary>
        static float[] Loop(float seconds, float fade, System.Action<float[]> render)
        {
            int len = Mathf.CeilToInt(seconds * Rate), x = Mathf.CeilToInt(fade * Rate);
            var raw = new float[len + x];
            render(raw);
            var s = new float[len];
            for (int i = 0; i < len; i++)
            {
                if (i < x) { float g = i / (float)x; s[i] = raw[i] * g + raw[i + len] * (1f - g); }
                else s[i] = raw[i];
            }
            float max = 0f;
            for (int i = 0; i < len; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
            if (max > 0f) for (int i = 0; i < len; i++) s[i] *= 0.9f / max;
            return s;
        }

        /// <summary>Wind: band-passed noise whose centre and loudness wander on slow sines.</summary>
        static void Wind(float[] s, int seed, float lo, float hi, float q, float gain, float gustHz)
        {
            var n = new Noise(seed);
            var f = new Svf();
            float p1 = seed * 0.37f, p2 = seed * 1.13f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float sway = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * gustHz * t + p1)
                                    * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * gustHz * 2.71f * t + p2));
                f.Run(n.Next(), Mathf.Lerp(lo, hi, sway), q);
                s[i] += f.Band * gain * (0.35f + 0.65f * sway);
            }
        }

        /// <summary>A drone: sines at the given pitches, each slowly swelling on its own period.</summary>
        static void Drone(float[] s, float[] freqs, float[] gains, float swellHz)
        {
            for (int k = 0; k < freqs.Length; k++)
            {
                float w = 2f * Mathf.PI * freqs[k] / Rate, sp = 2f * Mathf.PI * swellHz * (1f + k * 0.37f) / Rate;
                for (int i = 0; i < s.Length; i++)
                    s[i] += Mathf.Sin(w * i) * gains[k] * (0.6f + 0.4f * Mathf.Sin(sp * i + k));
            }
        }

        /// <summary>A breathy pad: detuned saws, low-passed - the far choir of the deepest floors.</summary>
        static void Choir(float[] s, float[] freqs, float gain, float cutoff, float swellHz)
        {
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float ph = freqs[k] * t;
                    v += 2f * (ph - Mathf.Floor(ph)) - 1f;
                    ph = freqs[k] * 1.004f * t;
                    v += 2f * (ph - Mathf.Floor(ph)) - 1f;
                }
                f.Run(v, cutoff, 0.7f);
                s[i] += f.Low * gain * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * swellHz * t));
            }
        }

        /// <summary>A chorus of insects: narrow high bands, each pulsing at its own rate.</summary>
        static void Insects(float[] s, int seed)
        {
            var n = new Noise(seed);
            for (int k = 0; k < 3; k++)
            {
                var f = new Svf();
                float hz = 3800f + k * 900f, rate = 7f + k * 3.3f, ph = k * 1.7f;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = i / (float)Rate;
                    f.Run(n.Next(), hz, 14f);
                    float chirp = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * rate * t + ph));
                    float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (0.05f + k * 0.02f) * t + ph);
                    s[i] += f.Band * chirp * chirp * swell * 0.25f;
                }
            }
        }

        /// <summary>Water lapping at a shore: soft low bloops at an uneven pace.</summary>
        static void Lapping(float[] s, int seed)
        {
            var n = new Noise(seed);
            float t = 0.3f;
            while (t < s.Length / (float)Rate - 0.5f)
            {
                var hiss = new Svf();
                float f0 = n.Range(180, 320), phase = 0f;
                int i0 = (int)(t * Rate);
                for (int i = i0; i < s.Length && i < i0 + Rate / 2; i++)
                {
                    float u = (i - i0) / (float)Rate;
                    phase += 2f * Mathf.PI * f0 * (1f + 0.6f * u) / Rate;
                    hiss.Run(n.Next(), 700f, 0.8f);
                    s[i] += (Mathf.Sin(phase) * 0.5f + hiss.Low * 0.6f) * Env(u, 0.06f, 0.35f) * 0.25f;
                }
                t += n.Range(0.9f, 2.4f);
            }
        }

        public static float[] Bed(Bed bed)
        {
            switch (bed)
            {
                case Audio.Bed.Hub:   // a still room: low air, a faint warm hum
                    return Loop(14f, 2f, s =>
                    {
                        Wind(s, 11, 180, 420, 0.8f, 0.5f, 0.05f);
                        Drone(s, new[] { 65.4f, 98f }, new[] { 0.08f, 0.04f }, 0.06f);
                    });
                case Audio.Bed.Crypt:   // floors 1-25: wind through stone
                    return Loop(18f, 3f, s =>
                    {
                        Wind(s, 21, 280, 900, 1.6f, 1f, 0.07f);
                        Wind(s, 22, 120, 260, 0.9f, 0.5f, 0.03f);
                        Drone(s, new[] { 55f }, new[] { 0.05f }, 0.04f);
                    });
                case Audio.Bed.Deep:   // 26-50: lower wind, a drone with a fifth
                    return Loop(18f, 3f, s =>
                    {
                        Wind(s, 31, 160, 520, 1.4f, 0.8f, 0.05f);
                        Drone(s, new[] { 49f, 73.4f, 98.5f }, new[] { 0.12f, 0.07f, 0.03f }, 0.05f);
                    });
                case Audio.Bed.Abyss:   // 51-75: the drone beats against itself, the air goes dark
                    return Loop(20f, 3f, s =>
                    {
                        Wind(s, 41, 90, 300, 1.1f, 0.6f, 0.04f);
                        Drone(s, new[] { 41.2f, 41.9f, 61.7f, 87.3f }, new[] { 0.14f, 0.12f, 0.06f, 0.035f }, 0.04f);
                    });
                case Audio.Bed.Throne:   // 76-100: a far choir, dissonant, under the drone
                    return Loop(20f, 3f, s =>
                    {
                        Wind(s, 51, 90, 260, 1f, 0.45f, 0.03f);
                        Drone(s, new[] { 36.7f, 55f }, new[] { 0.12f, 0.06f }, 0.035f);
                        Choir(s, new[] { 146.8f, 155.6f, 220f }, 0.05f, 900f, 0.05f);
                    });
                case Audio.Bed.Forest:   // leaves in a breeze, insects, water lapping somewhere close
                    return Loop(20f, 3f, s =>
                    {
                        Wind(s, 61, 1800, 4200, 0.9f, 0.35f, 0.06f);   // leaves: high, soft
                        Wind(s, 62, 200, 500, 0.8f, 0.25f, 0.04f);
                        Insects(s, 63);
                        Lapping(s, 64);
                    });
                case Audio.Bed.Sky:   // high open wind, gusting; nothing to echo off
                    return Loop(18f, 3f, s =>
                    {
                        Wind(s, 71, 500, 1800, 1.3f, 1f, 0.08f);
                        Wind(s, 72, 1600, 3800, 1.6f, 0.45f, 0.13f);
                        Wind(s, 73, 140, 300, 0.7f, 0.3f, 0.05f);
                    });
                case Audio.Bed.Salt:   // a vast still: low wind over the flat, a faint high ringing
                    return Loop(20f, 3f, s =>
                    {
                        Wind(s, 81, 300, 700, 0.9f, 0.55f, 0.03f);
                        Drone(s, new[] { 2093f, 2637f, 3136f }, new[] { 0.010f, 0.007f, 0.005f }, 0.03f);
                    });
                case Audio.Bed.Nigredo:   // laid OVER the floor's bed near the top: a furnace roar, a slow swelling throb
                    return Loop(12.8f, 2f, s =>
                    {
                        Wind(s, 91, 40, 150, 0.9f, 1.2f, 0.06f);    // the roar: low, heavy
                        Wind(s, 92, 150, 420, 1.1f, 0.35f, 0.11f);  // its rough upper edge
                        // four swells a loop, 3.2 s apart - the crystals' own pace
                        for (int i = 0; i < s.Length; i++)
                        {
                            float t = i / (float)Rate, u = (t % 3.2f) / 3.2f, env = Mathf.Sin(Mathf.PI * u);
                            s[i] += Mathf.Sin(2f * Mathf.PI * 33f * t) * env * env * 0.22f;
                        }
                    });
                case Audio.Bed.Boss:   // laid OVER the floor's bed on a boss floor: a slow heartbeat
                    return Loop(2.4f, 0.05f, s =>
                    {
                        Thump(s, 70, 38, 0.04f, 0.18f, 1f, 0.05f);
                        Thump(s, 64, 36, 0.04f, 0.14f, 0.6f, 0.36f);
                    });
            }
            return new float[Rate];
        }

        // ------------------------------------------------------------------ distant sounds

        /// <summary>The occasional far-off sound that says the place goes on past the walls.
        /// 0 a drip, 1 stone settling, 2 a chain, 3 a far groan.</summary>
        public static float[] Distant(int kind, int seed)
        {
            var n = new Noise(seed);
            switch (kind)
            {
                case 0:
                {
                    var s = Buffer(0.9f);
                    float f0 = n.Range(900, 1400), phase = 0f;
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        phase += 2f * Mathf.PI * f0 * (1f + 0.8f * Mathf.Min(1f, t / 0.03f)) / Rate;
                        s[i] += Mathf.Sin(phase) * Env(t, 0.001f, 0.05f);
                    }
                    Echo(s, 0.11f, 0.4f, 4);
                    return Normalise(s);
                }
                case 1:
                {
                    var s = Buffer(1.4f);
                    for (int g = 0; g < 7; g++)
                        Hiss(s, seed + g, 0, n.Range(600, 1400), 200, 0.7f, 0.002f, n.Range(0.04f, 0.15f),
                             n.Range(0.3f, 1f), n.Range(0f, 0.6f));
                    Echo(s, 0.17f, 0.35f, 3);
                    return Normalise(s);
                }
                case 2:
                {
                    var s = Buffer(1.2f);
                    for (int l = 0; l < 5; l++)
                    {
                        float f = n.Range(1800, 2600);
                        Ring(s, new[] { f, f * 1.37f }, new[] { 0.4f, 0.25f }, 0.001f, 0.12f, 0.08f * l + n.Range(0, 0.03f));
                    }
                    Echo(s, 0.13f, 0.3f, 3);
                    return Normalise(s);
                }
                default:
                {
                    var s = Buffer(2.4f);
                    float phase = 0f, f0 = n.Range(48, 62);
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        phase += 2f * Mathf.PI * f0 * (1f + 0.12f * Mathf.Sin(t * 2.3f)) / Rate;
                        s[i] += (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2.01f)) * Env(t, 0.6f, 1.6f);
                    }
                    return Normalise(s);
                }
            }
        }

        /// <summary>A few decaying repeats - the room the distant sound happened in.</summary>
        static void Echo(float[] s, float delay, float feedback, int repeats)
        {
            int d = (int)(delay * Rate);
            for (int r = 0; r < repeats; r++)
                for (int i = s.Length - 1; i >= d; i--)
                    s[i] += s[i - d] * feedback * 0.6f;
        }
    }
}
