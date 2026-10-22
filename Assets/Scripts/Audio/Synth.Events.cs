using UnityEngine;

namespace Convergence.Audio
{
    /// <summary>
    /// The second set of placeholders: the player's swings, each enemy kind's TELL and its blow,
    /// and the run's big moments (a release, a floor cleared, the door). An enemy's tell is a
    /// gameplay cue as much as its picture is - a growl from off screen says a Chaser is winding
    /// up - so every tell is distinct in PITCH and SHAPE, not only in loudness.
    /// </summary>
    public static partial class Synth
    {
        // ------------------------------------------------------------------ the player

        /// <summary>A blade through air: band-passed noise whose centre rises and falls with the
        /// swing. Heavy is longer and lower.</summary>
        public static float[] Swing(bool heavy, int seed)
        {
            var n = new Noise(seed);
            float len = heavy ? 0.32f : 0.17f;
            var s = Buffer(len);
            var f = new Svf();
            float top = heavy ? n.Range(1300, 1700) : n.Range(2200, 2900);
            for (int i = 0; i < s.Length; i++)
            {
                float u = i / (float)s.Length;
                float bell = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f));
                f.Run(n.Next(), Mathf.Lerp(top * 0.3f, top, bell), 2.2f);
                s[i] = f.Band * bell * bell;
            }
            return Normalise(s);
        }

        public static float[] Release(Core.ElementType e, int seed)
        {
            var n = new Noise(seed);
            switch (e)
            {
                case Core.ElementType.Fire:   // a WHOOMP: noise opening up as it ignites
                {
                    var s = Buffer(0.9f);
                    Hiss(s, seed, 0, 300, 2600, 0.7f, 0.06f, 0.7f, 1f);
                    Thump(s, 120, 45, 0.08f, 0.4f, 0.8f);
                    return Normalise(s);
                }
                case Core.ElementType.Water:   // a wave: rising, breaking, bubbles after
                {
                    var s = Buffer(1.1f);
                    var f = new Svf();
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float cut = t < 0.3f ? Mathf.Lerp(400, 3200, t / 0.3f) : Mathf.Lerp(3200, 900, (t - 0.3f) / 0.8f);
                        f.Run(n.Next(), cut, 1.1f);
                        s[i] += f.Band * Env(t, 0.25f, 0.8f);
                    }
                    for (int d = 0; d < 6; d++) Bloop(s, n.Range(0.35f, 0.9f), n.Range(400, 900), 0.25f);
                    return Normalise(s);
                }
                case Core.ElementType.Earth:   // a rumble with a crack in it
                {
                    var s = Buffer(1.3f);
                    Hiss(s, seed, 0, 220, 90, 0.8f, 0.05f, 1.1f, 1f);
                    Thump(s, 85, 32, 0.15f, 0.9f, 1f);
                    Hiss(s, seed + 1, 1, 2400, 900, 1.2f, 0.001f, 0.08f, 0.5f, 0.02f);
                    return Normalise(s);
                }
                default:   // Air: a gust that swells and passes
                {
                    var s = Buffer(1f);
                    var f = new Svf();
                    for (int i = 0; i < s.Length; i++)
                    {
                        float u = i / (float)s.Length, bell = Mathf.Sin(Mathf.PI * u);
                        f.Run(n.Next(), Mathf.Lerp(400, 3400, bell), 2.6f);
                        s[i] = f.Band * bell;
                    }
                    return Normalise(s);
                }
            }
        }

        // ------------------------------------------------------------------ enemy tells

        /// <summary>The Chaser's (and the Dasher's, higher) wind-up: a throaty growl.</summary>
        public static float[] Growl(float pitch, int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.5f);
            var f = new Svf();
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = pitch * (1f + 0.06f * Mathf.Sin(t * 31f)) * (1f + 0.15f * t);
                phase += hz / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float flutter = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 23f * t);
                f.Run(saw + n.Next() * 0.4f, 520f, 1.4f);
                s[i] = f.Low * flutter * Env(t, 0.09f, 0.4f);
            }
            return Normalise(s);
        }

        /// <summary>A bolt gathering: a tone climbing with a waver - the Stinger's, the Eye's.</summary>
        public static float[] Charge(float from, float to, float seconds)
        {
            var s = Buffer(seconds);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, u = t / seconds;
                float hz = Mathf.Lerp(from, to, u * u) * (1f + 0.03f * Mathf.Sin(t * 60f));
                phase += 2f * Mathf.PI * hz / Rate;
                s[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * Mathf.Min(1f, u * 4f) * (0.4f + 0.6f * u);
            }
            return Normalise(s);
        }

        /// <summary>A bolt loosed: a falling buzz.</summary>
        public static float[] Zap(int seed)
        {
            var s = Buffer(0.18f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(1900f, 260f, t / 0.18f) / Rate;
                s[i] = (phase - Mathf.Floor(phase) < 0.5f ? 1f : -1f) * Env(t, 0.002f, 0.14f) * 0.6f;
            }
            Hiss(s, seed, 2, 4000, 2000, 0.8f, 0.001f, 0.05f, 0.5f);
            return Normalise(s);
        }

        /// <summary>A shell leaving the mortar: a hollow thoomp.</summary>
        public static float[] Lob(int seed)
        {
            var s = Buffer(0.3f);
            Thump(s, 210, 95, 0.04f, 0.18f, 1f);
            Hiss(s, seed, 0, 900, 200, 0.6f, 0.003f, 0.12f, 0.5f);
            return Normalise(s);
        }

        public static float[] Explosion(int seed)
        {
            var s = Buffer(1.1f);
            Hiss(s, seed, 0, 3000, 90, 0.6f, 0.002f, 0.9f, 1f);
            Thump(s, 95, 28, 0.12f, 0.7f, 1f);
            var n = new Noise(seed + 9);
            for (int c = 0; c < 10; c++)   // debris pattering after
            {
                int at = (int)(n.Range(0.15f, 0.8f) * Rate);
                for (int i = 0; i < 60 && at + i < s.Length; i++) s[at + i] += n.Next() * Mathf.Exp(-i / 10f) * 0.25f;
            }
            return Normalise(s);
        }

        /// <summary>A fuse or a heart arming: hiss swelling.</summary>
        public static float[] Hissing(int seed)
        {
            var s = Buffer(0.7f);
            Hiss(s, seed, 2, 3500, 5000, 0.9f, 0.5f, 0.15f, 1f);
            return Normalise(s);
        }

        /// <summary>The Eye's beam: crackling static over a mains buzz.</summary>
        public static float[] Crackle(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.6f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float buzz = Mathf.Sin(2f * Mathf.PI * 118f * t) > 0f ? 0.25f : -0.25f;
                s[i] = buzz * Env(t, 0.02f, 0.5f);
            }
            for (int c = 0; c < 40; c++)
            {
                int at = (int)(n.Range(0f, 0.55f) * Rate);
                for (int i = 0; i < 25 && at + i < s.Length; i++) s[at + i] += n.Next() * Mathf.Exp(-i / 4f);
            }
            return Normalise(s);
        }

        /// <summary>The Watcher's slam: stone on stone, heavy.</summary>
        public static float[] Slam(int seed)
        {
            var s = Buffer(0.9f);
            Thump(s, 80, 26, 0.1f, 0.6f, 1f);
            Hiss(s, seed, 0, 1800, 160, 0.7f, 0.001f, 0.35f, 0.8f);
            Hiss(s, seed + 2, 1, 900, 400, 1.5f, 0.02f, 0.5f, 0.3f, 0.05f);
            return Normalise(s);
        }

        /// <summary>The Watcher's tell: stone grinding.</summary>
        public static float[] Grind(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.6f);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = n.Next() * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 70f + n.Next() * 0.3f)));
                f.Run(grain, 500f, 1.2f);
                s[i] = f.Band * Env(t, 0.15f, 0.45f);
            }
            return Normalise(s);
        }

        /// <summary>The Green Lion's roar: a voiced saw through two moving formants.</summary>
        public static float[] Roar(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(1.1f);
            Svf a = new(), b = new();
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                phase += (95f + 25f * Mathf.Sin(t * 3f)) * (1f + 0.04f * Mathf.Sin(t * 37f)) / Rate;
                float src = 2f * (phase - Mathf.Floor(phase)) - 1f + n.Next() * 0.5f;
                a.Run(src, 650f + 200f * Mathf.Sin(t * 2.4f), 3f);
                b.Run(src, 1150f + 300f * t, 3.5f);
                s[i] = (a.Band + 0.7f * b.Band) * Env(t, 0.15f, 0.9f);
            }
            return Normalise(s);
        }

        /// <summary>The Drowned Bell blowing a bubble: bloops climbing.</summary>
        public static float[] Bubbling(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.6f);
            for (int d = 0; d < 7; d++) Bloop(s, d * 0.065f + n.Range(0, 0.02f), 300 + d * 70 + n.Range(-20, 20), 0.5f);
            return Normalise(s);
        }

        public static float[] Pop(int seed)
        {
            var s = Buffer(0.15f);
            Bloop(s, 0f, 700f, 1f);
            Hiss(s, seed, 2, 3000, 2000, 0.8f, 0.001f, 0.02f, 0.6f);
            return Normalise(s);
        }

        /// <summary>The fuse's blink - a short high beep the ear can count.</summary>
        public static float[] FuseTick()
        {
            var s = Buffer(0.07f);
            Ring(s, new[] { 1500f, 3000f }, new[] { 0.6f, 0.15f }, 0.002f, 0.05f);
            return Normalise(s);
        }

        /// <summary>The Salamander's flame: a roaring blowtorch.</summary>
        public static float[] Flame(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(1.2f);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                f.Run(n.Next(), 900f + 300f * Mathf.Sin(t * 13f), 0.8f);
                s[i] = f.Low * (0.75f + 0.25f * Mathf.Sin(t * 47f)) * Env(t, 0.12f, 1f);
            }
            return Normalise(s);
        }

        /// <summary>An enemy's blow landing on the player is the PlayerHurt sound; this is the
        /// swipe itself, so a whiffed claw is heard too.</summary>
        public static float[] Swipe(int seed)
        {
            var s = Swing(false, seed + 50);
            Thump(s, 160, 90, 0.02f, 0.06f, 0.4f, 0.04f);
            return Normalise(s);
        }

        // ------------------------------------------------------------------ the run

        /// <summary>The floor cleared: a low gong, slow to arrive and long to leave.</summary>
        public static float[] FloorClear()
        {
            var s = Buffer(3.2f);
            Ring(s, new[] { 98f, 147f, 233f, 311f, 466f }, new[] { 0.5f, 0.35f, 0.22f, 0.15f, 0.08f }, 0.02f, 2.8f);
            Ring(s, new[] { 99.2f }, new[] { 0.3f }, 0.02f, 2.8f);
            return Normalise(s, 0.85f);
        }

        /// <summary>The sigil door parting: stone grinding open, light chiming through.</summary>
        public static float[] DoorOpen()
        {
            var s = Buffer(1.8f);
            var n = new Noise(77);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = n.Next() * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 41f)));
                f.Run(grain, 350f, 1f);
                s[i] = f.Band * Env(t, 0.2f, 1.1f);
            }
            Ring(s, new[] { 1046.5f, 1568f, 2093f }, new[] { 0.18f, 0.12f, 0.07f }, 0.3f, 1.2f, 0.4f);
            return Normalise(s, 0.85f);
        }

        /// <summary>The forest's far-off sounds: 0 an owl, 1 a branch creaking, 2 a frog.</summary>
        public static float[] DistantForest(int kind, int seed)
        {
            var n = new Noise(seed);
            switch (kind)
            {
                case 0:
                {
                    var s = Buffer(1.6f);
                    float f0 = n.Range(360, 420);
                    foreach (var (at, len, k) in new[] { (0f, 0.35f, 1f), (0.55f, 0.6f, 0.94f) })
                    {
                        float phase = 0f;
                        for (int i = (int)(at * Rate); i < s.Length && i < (int)((at + len) * Rate); i++)
                        {
                            float t = i / (float)Rate - at;
                            phase += 2f * Mathf.PI * f0 * k * (1f - 0.04f * t / len) / Rate;
                            s[i] += Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t / len);
                        }
                    }
                    return Normalise(s);
                }
                case 1:
                {
                    var s = Buffer(1.3f);
                    var f = new Svf();
                    float phase = 0f;
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        phase += (70f + 25f * Mathf.Sin(t * 2.1f)) / Rate;
                        float pulse = (phase - Mathf.Floor(phase)) < 0.08f ? 1f : 0f;   // the wood's stick-slip
                        f.Run(pulse + n.Next() * 0.1f, 750f, 3f);
                        s[i] = f.Band * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 1.3f));
                    }
                    return Normalise(s);
                }
                default:
                {
                    var s = Buffer(0.7f);
                    var f = new Svf();
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float burst = Mathf.Sin(2f * Mathf.PI * 22f * t) > 0.3f ? 1f : 0f;
                        f.Run(Mathf.Sin(2f * Mathf.PI * 280f * t) * burst, 600f, 2f);
                        s[i] = f.Band * Env(t, 0.03f, 0.55f);
                    }
                    return Normalise(s);
                }
            }
        }

        /// <summary>The sky's far-off sounds: 0 a bell somewhere below, 1 a bird's cry, 2 a gust.</summary>
        public static float[] DistantSky(int kind, int seed)
        {
            var n = new Noise(seed);
            switch (kind)
            {
                case 0:
                {
                    var s = Buffer(2.6f);
                    float f = n.Range(196f, 262f);
                    Ring(s, new[] { f, f * 2f, f * 2.4f, f * 3f }, new[] { 0.5f, 0.28f, 0.18f, 0.1f }, 0.004f, 2.2f);
                    return Normalise(s);
                }
                case 1:
                {
                    var s = Buffer(0.6f);
                    float phase = 0f;
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        phase += 2f * Mathf.PI * Mathf.Lerp(2400f, 1300f, t / 0.6f) * (1f + 0.04f * Mathf.Sin(t * 60f)) / Rate;
                        s[i] = Mathf.Sin(phase) * Env(t, 0.02f, 0.5f);
                    }
                    return Normalise(s);
                }
                default:
                    return Release(Core.ElementType.Air, seed + 90);
            }
        }

        /// <summary>The salt's far-off sounds: 0 the crust cracking, 1 crystals chiming, 2 a whistle of wind.</summary>
        public static float[] DistantSalt(int kind, int seed)
        {
            var n = new Noise(seed);
            switch (kind)
            {
                case 0:
                {
                    var s = Buffer(0.8f);
                    for (int c = 0; c < 5; c++) Hiss(s, seed + c, 1, n.Range(2500, 4500), 1500, 2f, 0.001f, 0.02f, n.Range(0.4f, 1f), n.Range(0f, 0.25f));
                    return Normalise(s);
                }
                case 1:
                {
                    var s = Buffer(1.8f);
                    for (int c = 0; c < 4; c++)
                    {
                        float f = n.Range(2200f, 3800f);
                        Ring(s, new[] { f, f * 2.76f }, new[] { 0.4f, 0.12f }, 0.002f, 0.9f, c * n.Range(0.08f, 0.2f));
                    }
                    return Normalise(s);
                }
                default:
                {
                    var s = Buffer(1.4f);
                    var f = new Svf();
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        f.Run(n.Next(), 1100f + 500f * Mathf.Sin(t * 2.2f), 12f);
                        s[i] = f.Band * Mathf.Sin(Mathf.PI * t / 1.4f);
                    }
                    return Normalise(s);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A bubble: a sine whose pitch climbs as it closes.</summary>
        static void Bloop(float[] s, float start, float f0, float gain)
        {
            float phase = 0f;
            for (int i = (int)(start * Rate); i < s.Length; i++)
            {
                float t = i / (float)Rate - start;
                if (t > 0.12f) break;
                phase += 2f * Mathf.PI * f0 * (1f + 1.5f * Mathf.Min(1f, t / 0.05f)) / Rate;
                s[i] += Mathf.Sin(phase) * Env(t, 0.002f, 0.05f) * gain;
            }
        }
    }
}
