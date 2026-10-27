using UnityEngine;

namespace Convergence.Audio
{
    /// <summary>
    /// The bosses' sounds, and stone breaking anywhere.
    ///
    /// THE WREATHE SINGS ITS PHRASE. Each of the eight slices has a NOTE (<see cref="SliceNote"/>,
    /// D natural minor up the ring, so walking round the arena climbs the scale), an inner half an
    /// octave down (<see cref="ZoneNote"/>): the CALL is sung on the slice's note. The ANSWER's beat
    /// tolls one NEUTRAL bell (it must never name the slice), and a landed step tolls the note back.
    /// </summary>
    public static partial class Synth
    {
        /// <summary>D3 to D4, natural minor - one note per slice.</summary>
        static readonly float[] Scale = { 146.83f, 164.81f, 174.61f, 196.00f, 220.00f, 233.08f, 261.63f, 293.66f };

        public static float SliceNote(int sector) => Scale[((sector % Scale.Length) + Scale.Length) % Scale.Length];

        /// <summary>A Wreathe zone's note: 0-7 a slice's, 8-15 the same an octave down (an inner
        /// half, enraged).</summary>
        public static float ZoneNote(int zone) => zone >= Scale.Length ? SliceNote(zone) * 0.5f : SliceNote(zone);

        /// <summary>The answer's clock: one low D, the same every beat, so it never names a slice.</summary>
        public const float NeutralBell = 73.42f;

        /// <summary>The Wreathe's sung note: a voiced saw with vibrato through "ah" formants.</summary>
        public static float[] Sing(float hz)
        {
            var n = new Noise((int)hz);
            var s = Buffer(0.75f);
            Svf a = new(), b = new(), c = new();
            float phase = 0f, phase2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float vib = 1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 5.2f * t) * Mathf.Min(1f, t / 0.25f);
                phase += hz * vib / Rate;
                phase2 += hz * 1.006f * vib / Rate;   // a second voice a hair sharp - a choir, not a synth
                float src = (2f * (phase - Mathf.Floor(phase)) - 1f) + (2f * (phase2 - Mathf.Floor(phase2)) - 1f)
                          + n.Next() * 0.15f;
                a.Run(src, 730f, 6f); b.Run(src, 1090f, 7f); c.Run(src, 2440f, 8f);
                s[i] = (a.Band + 0.6f * b.Band + 0.25f * c.Band) * Env(t, 0.07f, 0.65f);
            }
            return Normalise(s);
        }

        /// <summary>The answer's toll: the same note as a dark bell.</summary>
        public static float[] Toll(float hz)
        {
            var s = Buffer(1.4f);
            Ring(s, new[] { hz, hz * 2f, hz * 2.41f, hz * 3.02f, hz * 4.1f },
                 new[] { 0.55f, 0.3f, 0.2f, 0.12f, 0.06f }, 0.003f, 1.2f);
            return Normalise(s);
        }

        /// <summary>A slice erupting: fire bursting up out of the floor.</summary>
        public static float[] Erupt(int seed)
        {
            var s = Buffer(0.7f);
            Hiss(s, seed, 0, 4000, 400, 0.6f, 0.004f, 0.55f, 1f);
            Thump(s, 140, 55, 0.05f, 0.25f, 0.7f);
            return Normalise(s);
        }

        /// <summary>A teleport: a quick shimmer, rising.</summary>
        public static float[] Blink(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.25f);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, u = t / 0.25f;
                f.Run(n.Next(), Mathf.Lerp(1500, 6000, u), 6f);
                s[i] = f.Band * Mathf.Sin(Mathf.PI * u);
            }
            return Normalise(s);
        }

        /// <summary>A boss enraging: a dissonant cluster swelling up from the floor.</summary>
        public static float[] Enrage()
        {
            var s = Buffer(2f);
            float[] f = { 73.4f, 77.8f, 110f, 116.5f, 155.6f };
            var lp = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int k = 0; k < f.Length; k++) { float ph = f[k] * t; v += 2f * (ph - Mathf.Floor(ph)) - 1f; }
                lp.Run(v, Mathf.Lerp(200f, 1600f, Mathf.Min(1f, t / 1.2f)), 0.8f);
                s[i] = lp.Low * Env(t, 1.1f, 0.9f);
            }
            return Normalise(s);
        }

        /// <summary>The window opening: a long exhale falling, a soft chime in it - "now".</summary>
        public static float[] Vulnerable()
        {
            var s = Buffer(1.2f);
            Hiss(s, 61, 1, 2400, 500, 1.4f, 0.03f, 0.9f, 0.6f);
            Ring(s, new[] { 587.3f, 880f, 1174.7f }, new[] { 0.4f, 0.25f, 0.15f }, 0.01f, 0.9f);
            return Normalise(s);
        }

        /// <summary>The window closing: a short rising seal.</summary>
        public static float[] Seal()
        {
            var s = Buffer(0.45f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                phase += 2f * Mathf.PI * Mathf.Lerp(220f, 660f, Mathf.Min(1f, t / 0.25f)) / Rate;
                s[i] = Mathf.Sin(phase) * Env(t, 0.01f, 0.4f);
            }
            Thump(s, 120, 70, 0.03f, 0.1f, 0.5f, 0.22f);
            return Normalise(s);
        }

        /// <summary>A boss falls: a low boom with a choir dropping away under it.</summary>
        public static float[] BossDeath()
        {
            var s = Buffer(3.5f);
            Thump(s, 70, 22, 0.8f, 2.4f, 1f);
            Hiss(s, 71, 0, 2500, 60, 0.6f, 0.005f, 2.6f, 0.7f);
            var lp = new Svf();
            float[] f = { 146.8f, 220f, 293.7f };
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, v = 0f, fall = Mathf.Pow(0.5f, t / 2.5f);   // an octave down
                for (int k = 0; k < f.Length; k++) { float ph = f[k] * t * Mathf.Lerp(1f, fall, 0.5f); v += 2f * (ph - Mathf.Floor(ph)) - 1f; }
                lp.Run(v, 1100f, 0.8f);
                s[i] += lp.Low * 0.25f * Env(t, 0.2f, 3f);
            }
            return Normalise(s);
        }

        // ------------------------------------------------------------------ Medusa

        /// <summary>Her hair: a snake's rattle, one burst - played faster and faster through the
        /// gaze's tell, so the player hears the clock as well as seeing it.</summary>
        public static float[] Rattle(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.22f);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float clicks = 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 55f * t));
                f.Run(n.Next() * clicks, 5200f, 1.5f);
                s[i] = f.Band * Env(t, 0.01f, 0.18f);
            }
            return Normalise(s);
        }

        /// <summary>Her crown rearing: many serpents hissing at once, a sharp onset and a long
        /// sibilant tail - it wakes her statues.</summary>
        public static float[] SerpentHiss(int seed)
        {
            var s = Buffer(0.9f);
            Hiss(s, seed, 2, 7000, 5200, 1.2f, 0.03f, 0.75f, 1f);
            Hiss(s, seed + 5, 1, 4200, 3600, 1.5f, 0.06f, 0.6f, 0.5f);
            return Normalise(s);
        }

        /// <summary>Venom spat from her crown: a wet click and a short hiss.</summary>
        public static float[] Spit(int seed)
        {
            var s = Buffer(0.3f);
            Thump(s, 420, 180, 0.02f, 0.06f, 0.6f);
            Hiss(s, seed, 1, 5200, 2600, 1.4f, 0.002f, 0.18f, 0.8f);
            return Normalise(s);
        }

        /// <summary>She LOOKS: a piercing chord over a rush of air.</summary>
        public static float[] GazeLook()
        {
            var s = Buffer(1.3f);
            Ring(s, new[] { 1244.5f, 1318.5f, 1864.7f, 2637f }, new[] { 0.4f, 0.35f, 0.25f, 0.15f }, 0.005f, 1.1f);
            Hiss(s, 81, 2, 6000, 2500, 0.9f, 0.01f, 0.8f, 0.5f);
            Thump(s, 90, 40, 0.1f, 0.5f, 0.6f);
            return Normalise(s);
        }

        /// <summary>Caught: flesh turning to stone - a grinding crunch settling down.</summary>
        public static float[] Petrify(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(1.2f);
            var f = new Svf();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float grain = n.Next() * (n.Next() > 0.6f ? 1f : 0.2f);
                f.Run(grain, Mathf.Lerp(2200f, 300f, t / 1.2f), 1.3f);
                s[i] = f.Band * Env(t, 0.02f, 1f);
            }
            Thump(s, 100, 40, 0.2f, 0.7f, 0.6f);
            return Normalise(s);
        }

        /// <summary>Push or pull wind-up: a gust, played backwards for a pull (it sucks IN).</summary>
        public static float[] Rush(bool inward, int seed)
        {
            var s = Release(Core.ElementType.Air, seed + 30);
            if (inward) System.Array.Reverse(s);
            return s;
        }

        // ------------------------------------------------------------------ stone

        /// <summary>A hit on stone - a cracked column or wall taking a blow.</summary>
        public static float[] Chip(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(0.15f);
            Hiss(s, seed, 1, n.Range(2600, 3600), 1500, 2f, 0.001f, 0.05f, 1f);
            Thump(s, 230, 160, 0.02f, 0.05f, 0.5f);
            return Normalise(s);
        }

        /// <summary>Stone coming down: a column, a wall, a falling pillar.</summary>
        public static float[] Rubble(int seed)
        {
            var n = new Noise(seed);
            var s = Buffer(1.3f);
            Thump(s, 110, 38, 0.08f, 0.5f, 1f);
            Hiss(s, seed, 0, 2200, 200, 0.7f, 0.002f, 0.5f, 0.8f);
            for (int c = 0; c < 18; c++)   // chunks landing, thinning out
            {
                float at = n.Range(0.08f, 1.0f) * n.Range(0.4f, 1f);
                Hiss(s, seed + 10 + c, 1, n.Range(700, 2400), 500, 1.5f, 0.001f, n.Range(0.03f, 0.09f),
                     n.Range(0.2f, 0.6f), at);
            }
            return Normalise(s);
        }
    }
}
