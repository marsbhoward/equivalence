using UnityEngine;

namespace Convergence.Audio
{
    /// <summary>
    /// The 2026-11-13 sound pass: what was silent. The player's own feedback (the four defensives, a
    /// blocked hit, a release's kind, the lock, a full meter, a level, a status landing), the world's
    /// (loot, the Rift, spikes, wind, a fall, the puzzles), the hub's fixtures and the menus' ticks.
    /// Placeholders like the rest - a recording in GameSound replaces any of them.
    /// </summary>
    public static partial class Synth
    {
        // ------------------------------------------------------------------ helpers

        /// <summary>A plain sine ping - menu ticks and the lock's two notes.</summary>
        static void Blip(float[] s, float hz, float start, float decay, float gain, float attack = 0.002f)
        {
            int s0 = (int)(start * Rate);
            float w = 2f * Mathf.PI * hz / Rate;
            for (int i = s0; i < s.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float e = Env(t, attack, decay);
                if (t > attack && e < 0.0005f) break;
                s[i] += Mathf.Sin(w * (i - s0)) * e * gain;
            }
        }

        /// <summary>A sine gliding from one pitch to another over the whole buffer, under its own swell.</summary>
        static void Glide(float[] s, float from, float to, float attack, float decay, float gain, float start = 0f)
        {
            int s0 = (int)(start * Rate);
            float phase = 0f, len = (s.Length - s0) / (float)Rate;
            for (int i = s0; i < s.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float f = Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.001f, len)));
                phase += 2f * Mathf.PI * f / Rate;
                s[i] += Mathf.Sin(phase) * Env(t, attack, decay) * gain;
            }
        }

        // ------------------------------------------------------------------ the player

        /// <summary>The Dash: a short whoosh that rises past the ear.</summary>
        public static float[] Dodge(int seed)
        {
            var s = Buffer(0.22f);
            var n = new Noise(seed);
            var f = new Svf();
            float top = n.Range(3200, 4200);
            for (int i = 0; i < s.Length; i++)
            {
                float u = i / (float)s.Length;
                float bell = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.5f));
                f.Run(n.Next(), Mathf.Lerp(700f, top, u), 1.6f);
                s[i] = f.Band * bell * bell;
            }
            return Normalise(s);
        }

        /// <summary>The Barrier going up (and a Guard release): glass partials swelling in over a rising hiss.</summary>
        public static float[] Ward()
        {
            var s = Buffer(0.9f);
            Ring(s, new[] { 880f, 1318f, 1975f, 2637f }, new[] { 0.3f, 0.22f, 0.15f, 0.1f }, 0.05f, 0.7f);
            Hiss(s, 41, 1, 1200, 5200, 2f, 0.12f, 0.35f, 0.15f);
            return Normalise(s, 0.8f);
        }

        /// <summary>The Bulwark: a planted thud with iron ringing in it.</summary>
        public static float[] BulwarkUp()
        {
            var s = Buffer(0.8f);
            Thump(s, 150f, 58f, 0.05f, 0.35f, 0.9f);
            Ring(s, new[] { 220f, 563f, 891f, 1240f }, new[] { 0.22f, 0.16f, 0.1f, 0.06f }, 0.004f, 0.55f);
            return Normalise(s);
        }

        /// <summary>Parry Stance (v3, the user 2026-11-13): the swing itself (<see cref="Swing"/>) with a chime on
        /// top - the guard is a motion, and the bell says it is set.</summary>
        public static float[] ParryReady(int seed)
        {
            var sw = Swing(false, seed);
            var s = Buffer(0.7f);
            for (int i = 0; i < sw.Length && i < s.Length; i++) s[i] = sw[i] * 0.8f;
            // The chime lands near the swing's peak, a clear bell rather than a metal ring.
            Ring(s, new[] { 1760f, 2637f, 3520f }, new[] { 0.3f, 0.14f, 0.07f }, 0.003f, 0.5f, 0.09f);
            return Normalise(s, 0.85f);
        }

        /// <summary>A hit stopped by the Barrier: a dull knock on something that rings a little.</summary>
        public static float[] Blocked(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.3f);
            Thump(s, n.Range(320, 360), 170f, 0.02f, 0.09f, 0.8f);
            Ring(s, new[] { 660f, 990f, 1500f }, new[] { 0.2f, 0.12f, 0.06f }, 0.002f, 0.18f);
            Hiss(s, seed, 1, 1800, 1200, 1.2f, 0.001f, 0.03f, 0.3f);
            return Normalise(s);
        }

        /// <summary>A Buff release: a tone climbing into a shimmer.</summary>
        public static float[] Empower()
        {
            var s = Buffer(0.8f);
            Glide(s, 300f, 920f, 0.04f, 0.6f, 0.5f);
            Glide(s, 450f, 1380f, 0.06f, 0.55f, 0.25f);
            Ring(s, new[] { 1840f, 2760f }, new[] { 0.15f, 0.08f }, 0.08f, 0.4f, 0.3f);
            return Normalise(s, 0.8f);
        }

        /// <summary>The lock taken (two notes up) or let go (down).</summary>
        public static float[] LockTick(bool on)
        {
            var s = Buffer(0.16f);
            Blip(s, on ? 1400f : 2100f, 0f, 0.05f, 0.6f);
            Blip(s, on ? 2100f : 1400f, 0.045f, 0.07f, 0.6f);
            return Normalise(s, 0.8f);
        }

        /// <summary>The element meter full: a soft three-note chord.</summary>
        public static float[] MeterFull()
        {
            var s = Buffer(1.0f);
            Ring(s, new[] { 1046.5f, 1318.5f, 1568f }, new[] { 0.3f, 0.25f, 0.22f }, 0.01f, 0.8f);
            return Normalise(s, 0.8f);
        }

        /// <summary>A level (v2, 2026-11-13 redo): no chiptune climb - a low bell struck, its fifth and octave
        /// blooming after it, and a shimmer rising out of the chord.</summary>
        public static float[] LevelUp()
        {
            var s = Buffer(2.4f);
            Thump(s, 160f, 73.4f, 0.04f, 0.5f, 0.35f);
            Ring(s, new[] { 146.8f, 293.7f, 440f, 587.3f }, new[] { 0.4f, 0.3f, 0.18f, 0.1f }, 0.003f, 2.0f);
            Ring(s, new[] { 147.6f }, new[] { 0.25f }, 0.003f, 2.0f);   // a beat against the root, as a real bell has
            Ring(s, new[] { 880f, 1174.7f }, new[] { 0.18f, 0.12f }, 0.25f, 1.6f, 0.15f);
            Hiss(s, 1313, 1, 1500, 6000, 3f, 0.6f, 0.9f, 0.08f, 0.2f);
            return Normalise(s, 0.85f);
        }

        /// <summary>A status landing: 0 a burn catching, 1 a soak, 2 a bleed, 3 a stagger.</summary>
        public static float[] Afflict(int kind, int seed)
        {
            var n = new Noise(seed);
            switch (kind)
            {
                case 0:   // fire catching (v2): a small whump as it takes, then crackle
                {
                    var s = Buffer(0.45f);
                    var f = new Svf();
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        f.Run(n.Next(), Mathf.Lerp(250f, 1800f, Mathf.Clamp01(t / 0.12f)), 0.8f);
                        s[i] = f.Low * Env(t, 0.03f, 0.22f) * 0.9f;
                    }
                    Thump(s, 110f, 60f, 0.03f, 0.12f, 0.5f);
                    for (int k = 0; k < 7; k++)
                    {
                        float at = n.Range(0.06f, 0.4f);
                        int i0 = (int)(at * Rate);
                        float g = n.Range(0.25f, 0.6f);
                        for (int i = i0; i < Mathf.Min(s.Length, i0 + 90); i++) s[i] += n.Next() * g * Mathf.Exp(-(i - i0) / 18f);
                    }
                    return Normalise(s);
                }
                case 1:   // a soak: a splash and two bubbles
                {
                    var s = Buffer(0.4f);
                    Hiss(s, seed, 0, 2400, 700, 0.8f, 0.004f, 0.18f, 0.8f);
                    Bloop(s, 0.08f, n.Range(500, 700), 0.35f);
                    Bloop(s, 0.16f, n.Range(700, 900), 0.25f);
                    return Normalise(s);
                }
                case 2:   // a bleed (v2): a quick slice, then a low pulse - the wound, not a drip
                {
                    var s = Buffer(0.45f);
                    var f = new Svf();
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        if (t > 0.07f) break;
                        f.Run(n.Next(), Mathf.Lerp(6000f, 2200f, t / 0.07f), 2.5f);
                        s[i] = f.Band * Mathf.Sin(Mathf.PI * t / 0.07f);
                    }
                    for (int beat = 0; beat < 2; beat++)
                        Thump(s, 95f, 55f, 0.02f, 0.09f, beat == 0 ? 0.55f : 0.35f, 0.1f + beat * 0.16f);
                    return Normalise(s);
                }
                default:   // a stagger: a dull wobbling knock
                {
                    var s = Buffer(0.4f);
                    float phase = 0f;
                    for (int i = 0; i < s.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float f = 120f + 25f * Mathf.Sin(t * 2f * Mathf.PI * 9f);
                        phase += 2f * Mathf.PI * f / Rate;
                        s[i] = Mathf.Sin(phase) * Env(t, 0.003f, 0.3f);
                    }
                    Hiss(s, seed, 0, 600, 300, 0.8f, 0.002f, 0.05f, 0.3f);
                    return Normalise(s);
                }
            }
        }

        // ------------------------------------------------------------------ the world

        /// <summary>A piece taken from the floor: three quick bright notes up.</summary>
        public static float[] Pickup(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.6f);
            float k = n.Range(0.97f, 1.03f);
            Ring(s, new[] { 1568f * k }, new[] { 0.3f }, 0.002f, 0.3f, 0f);
            Ring(s, new[] { 2093f * k }, new[] { 0.3f }, 0.002f, 0.3f, 0.06f);
            Ring(s, new[] { 2637f * k, 5274f * k }, new[] { 0.3f, 0.05f }, 0.002f, 0.4f, 0.12f);
            return Normalise(s, 0.8f);
        }

        /// <summary>A Rift Box taken (v3, the user 2026-11-13: it should sound like the Rift - they are one
        /// thing): the Rift's own tear (<see cref="RiftOpen"/>'s grain, swell and chord) small and quick, running
        /// INWARD - the fragment sealing itself into the pack - with a soft weight as it lands.</summary>
        public static float[] BoxPickup(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.85f);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                // The Rift's grain (the same modulation as RiftOpen), its cutoff falling instead of rising.
                float grain = n.Next() * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 63f + Mathf.Sin(t * 7f))));
                f.Run(grain, Mathf.Lerp(3400f, 300f, Mathf.Clamp01(t / 0.35f)), 1.4f);
                s[i] = f.Band * Env(t, 0.06f, 0.4f);
            }
            Glide(s, 82f, 55f, 0.05f, 0.5f, 0.5f);   // the Rift's swell, falling
            float k = n.Range(0.98f, 1.02f);
            Ring(s, new[] { 1244f * k, 1864f * k }, new[] { 0.16f, 0.1f }, 0.01f, 0.45f, 0.28f);   // the Rift's chord
            Thump(s, 120f, 70f, 0.02f, 0.15f, 0.4f, 0.3f);
            return Normalise(s, 0.85f);
        }

        /// <summary>A Rift tearing open (or holding): noise ripping upward over a low swell.</summary>
        public static float[] RiftOpen()
        {
            var s = Buffer(1.4f);
            var n = new Noise(311);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = n.Next() * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 63f + Mathf.Sin(t * 7f))));
                f.Run(grain, Mathf.Lerp(200f, 3400f, Mathf.Clamp01(t / 0.7f)), 1.4f);
                s[i] = f.Band * Env(t, 0.15f, 1.0f);
            }
            Glide(s, 55f, 82f, 0.2f, 1.1f, 0.6f);
            Ring(s, new[] { 1244f, 1864f }, new[] { 0.1f, 0.06f }, 0.3f, 0.8f, 0.4f);
            return Normalise(s, 0.85f);
        }

        /// <summary>A Rift or a Rift Box closing on itself: noise drawn inward, a pop at the end.</summary>
        public static float[] RiftClose(int seed)
        {
            var s = Buffer(0.9f);
            var n = new Noise(seed);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float u = Mathf.Clamp01(t / 0.7f);
                f.Run(n.Next(), Mathf.Lerp(3800f, 180f, u), 1.6f);
                s[i] = f.Band * u * u * (t < 0.7f ? 1f : Mathf.Exp(-(t - 0.7f) * 30f));
            }
            Thump(s, 220f, 60f, 0.02f, 0.18f, 0.8f, 0.68f);
            return Normalise(s);
        }

        /// <summary>A spike pit biting: a metal jab.</summary>
        public static float[] Spikes(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.25f);
            Hiss(s, seed, 2, 4500, 3000, 1f, 0.001f, 0.05f, 0.6f);
            float b = n.Range(1700, 1900);
            Ring(s, new[] { b, b * 1.5f, b * 2.3f }, new[] { 0.25f, 0.15f, 0.08f }, 0.001f, 0.12f);
            return Normalise(s);
        }

        /// <summary>Wind gathering: a tornado forming.</summary>
        public static float[] Gust(int seed)
        {
            var s = Buffer(1.6f);
            var n = new Noise(seed);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 1.6f;
                float swell = Mathf.Sin(Mathf.PI * u);
                f.Run(n.Next(), 300f + 1300f * swell + 200f * Mathf.Sin(t * 11f), 2.2f);
                s[i] = f.Band * swell;
            }
            return Normalise(s, 0.8f);
        }

        /// <summary>A body falling into the void: a whistle dropping away, a thud far below.</summary>
        public static float[] Fall(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(1.1f);
            Glide(s, n.Range(850, 950), 180f, 0.05f, 0.9f, 0.35f);
            Hiss(s, seed, 1, 2000, 400, 1.5f, 0.1f, 0.6f, 0.3f);
            Thump(s, 90f, 45f, 0.04f, 0.25f, 0.35f, 0.85f);
            return Normalise(s, 0.8f);
        }

        /// <summary>A body landing on the tier below.</summary>
        public static float[] Thud(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.4f);
            Thump(s, n.Range(105, 120), 48f, 0.04f, 0.25f, 1f);
            Hiss(s, seed, 0, 700, 250, 0.8f, 0.002f, 0.12f, 0.4f);
            return Normalise(s);
        }

        /// <summary>A puzzle stone stepped on: a stone click as it gives.</summary>
        public static float[] PlatePress(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.25f);
            Thump(s, n.Range(380, 420), 240f, 0.01f, 0.06f, 0.8f);
            Hiss(s, seed, 1, 1300, 900, 1.5f, 0.001f, 0.05f, 0.5f);
            return Normalise(s);
        }

        /// <summary>A puzzle solved: two notes up and a bright fifth held.</summary>
        public static float[] PuzzleSolved()
        {
            var s = Buffer(1.4f);
            Ring(s, new[] { 784f }, new[] { 0.35f }, 0.004f, 0.3f, 0f);
            Ring(s, new[] { 1175f }, new[] { 0.35f }, 0.004f, 0.3f, 0.1f);
            Ring(s, new[] { 1568f, 2349f, 3136f }, new[] { 0.35f, 0.2f, 0.08f }, 0.004f, 1.0f, 0.2f);
            return Normalise(s, 0.85f);
        }

        /// <summary>A puzzle failed: a low, beating buzz.</summary>
        public static float[] PuzzleFailed()
        {
            var s = Buffer(0.9f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float a = Mathf.Sin(2f * Mathf.PI * 110f * t), b = Mathf.Sin(2f * Mathf.PI * 116.5f * t);
                float sq = Mathf.Sign(a) * 0.3f + a * 0.7f;
                s[i] = (sq + b * 0.7f) * Env(t, 0.01f, 0.75f);
            }
            var f = new Svf();
            for (int i = 0; i < s.Length; i++) { f.Run(s[i], 900f, 0.8f); s[i] = f.Low; }
            return Normalise(s, 0.8f);
        }

        // ------------------------------------------------------------------ the hub and the menus

        /// <summary>A deal taken: a wax seal pressed, a coin's ring after.</summary>
        public static float[] DealTaken(int seed)
        {
            var s = Buffer(0.7f);
            Thump(s, 210f, 90f, 0.02f, 0.12f, 0.9f);
            Hiss(s, seed, 1, 2500, 1400, 1f, 0.001f, 0.05f, 0.4f);
            Ring(s, new[] { 1830f, 2950f, 4100f }, new[] { 0.2f, 0.12f, 0.06f }, 0.002f, 0.45f, 0.08f);
            return Normalise(s);
        }

        /// <summary>Gear put on: cloth and a buckle.</summary>
        public static float[] Equip(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.3f);
            Hiss(s, seed, 1, 900, 1800, 0.9f, 0.01f, 0.08f, 0.6f);
            float b = n.Range(1150, 1300);
            Ring(s, new[] { b, b * 1.6f }, new[] { 0.25f, 0.12f }, 0.002f, 0.12f, 0.06f);
            return Normalise(s);
        }

        /// <summary>The booth's camera: the shutter's two clicks.</summary>
        public static float[] Shutter()
        {
            var s = Buffer(0.25f);
            Hiss(s, 503, 2, 3500, 3500, 0.9f, 0.0005f, 0.012f, 0.9f);
            Hiss(s, 509, 2, 2800, 2800, 0.9f, 0.0005f, 0.018f, 0.7f, 0.07f);
            Thump(s, 600f, 400f, 0.01f, 0.03f, 0.3f, 0.07f);
            return Normalise(s);
        }

        /// <summary>The Forge: a hammer on the anvil.</summary>
        public static float[] Anvil(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(1.0f);
            float k = n.Range(0.98f, 1.02f);
            Ring(s, new[] { 880f * k, 1172f * k, 2350f * k, 3120f * k }, new[] { 0.3f, 0.2f, 0.12f, 0.07f }, 0.001f, 0.8f);
            Thump(s, 260f, 140f, 0.01f, 0.06f, 0.5f);
            Hiss(s, seed, 2, 6000, 4000, 0.8f, 0.0005f, 0.02f, 0.3f);
            return Normalise(s);
        }

        /// <summary>The crate: an old lid creaking up and set down.</summary>
        public static float[] Crate(int seed)
        {
            var s = Buffer(0.6f);
            var n = new Noise(seed);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = n.Next() * (Mathf.Sin(t * 2f * Mathf.PI * 38f) > 0.6f ? 1f : 0.15f);
                f.Run(grain, 450f + 200f * Mathf.Sin(t * 9f), 3f);
                s[i] = f.Band * Env(t, 0.05f, 0.35f);
            }
            Thump(s, 170f, 95f, 0.02f, 0.12f, 0.6f, 0.38f);
            return Normalise(s);
        }

        /// <summary>A frame or a piece of furniture set down (or lifted).</summary>
        public static float[] Place(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.25f);
            Thump(s, n.Range(170, 200), 110f, 0.02f, 0.09f, 0.9f);
            Hiss(s, seed, 1, 600, 400, 1.2f, 0.001f, 0.04f, 0.35f);
            return Normalise(s);
        }

        /// <summary>The transmutation circle: a slow chord swelling up with breath under it.</summary>
        public static float[] Transmute()
        {
            var s = Buffer(1.6f);
            Ring(s, new[] { 392f, 587f, 784f, 1175f, 1568f }, new[] { 0.25f, 0.2f, 0.18f, 0.12f, 0.08f }, 0.35f, 1.2f);
            Hiss(s, 919, 1, 600, 2600, 1.4f, 0.4f, 0.8f, 0.12f);
            return Normalise(s, 0.8f);
        }

        /// <summary>A menu's selection moving: the smallest tick.</summary>
        public static float[] UiTick()
        {
            var s = Buffer(0.06f);
            Blip(s, 1900f, 0f, 0.025f, 0.8f);
            return Normalise(s, 0.7f);
        }

        /// <summary>A menu's choice: two notes up; BACK: two notes down.</summary>
        public static float[] UiTwoTone(bool up)
        {
            var s = Buffer(0.14f);
            Blip(s, up ? 1180f : 1180f, 0f, 0.04f, 0.6f);
            Blip(s, up ? 1580f : 880f, 0.035f, 0.06f, 0.6f);
            return Normalise(s, 0.75f);
        }
    }
}
