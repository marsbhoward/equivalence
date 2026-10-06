using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Balance
{
    public static partial class Assay
    {
        /// <summary>
        /// One element number the calibration may move, and the range that keeps the element what
        /// it is - Earth stays heavy and slow, Air light and fast, a release never vanishes.
        /// </summary>
        readonly struct Knob
        {
            public readonly string Field;
            public readonly float Min, Max, Step;
            public Knob(string field, float min, float max, float step) { Field = field; Min = min; Max = max; Step = step; }
        }

        static readonly Knob[] Knobs =
        {
            // Fire: heat stacks into damage and speed; Erupt is area denial.
            new("FireStackDamagePoints", 2f, 8f, 1f),
            new("FireHastePoints", 10f, 30f, 4f),
            new("FirePoolHitUnitsPerSecond", 2f, 14f, 1f),
            // Water: not a damage element - the decision-maker whose release carries it.
            new("WaterDamagePoints", -15f, 0f, 3f),
            new("WaterSurgePoints", 30f, 60f, 5f),
            new("WaterBurstHitUnits", 4f, 30f, 2f),
            // Earth: heavy and clearly slower.
            new("EarthDamagePoints", 15f, 45f, 5f),
            new("EarthAttackSpeedPoints", -30f, -10f, 5f),
            new("EarthShockHitUnits", 1f, 4f, 0.4f),
            new("EarthShockHitUnitsPerCharge", 2f, 8f, 0.8f),
            // Air: light hits, fast swings - momentum must still make it fast.
            new("AirDamagePoints", -50f, -20f, 5f),
            new("AirAttackSpeedPoints", 0f, 30f, 5f),
            new("AirAttackSpeedPointsPerMomentum", 60f, 100f, 10f),
            new("AirCritDamagePoints", 10f, 40f, 5f),
            new("AirGustDamagePoints", 0f, 90f, 10f),
        };

        static FieldInfo NumberField(string name)
            => typeof(ElementNumbers).GetField(name, BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// The element parity rule, as one number to make small: the worst build's spread across the
        /// four elements (Max kit, no ledger), plus half the average build's, plus how far any
        /// element's release worth (on the Elementalist, who invests in it) strays outside 3.5-4.5s.
        /// </summary>
        static float ParityCost(out float worstSpread, out float meanSpread)
        {
            _builds.Clear();   // the gear is rolled around the element's head starts, which just moved
            var none = new Exchange.Mods();
            worstSpread = 0f; meanSpread = 0f;
            foreach (var a in Archetypes)
            {
                var d = Elements.Select(e => Evaluate(Make(a, e, Kit.Max), none, _ => 0, 1).Dps).ToArray();
                float s = Spread(d);
                worstSpread = Mathf.Max(worstSpread, s);
                meanSpread += s / Archetypes.Length;
            }
            float release = 0f;
            foreach (var e in Elements)
            {
                float r = ReleaseSeconds(Make(Archetype.Elementalist, e, Kit.Max));
                release += Mathf.Max(0f, 3.5f - r) + Mathf.Max(0f, r - 4.5f);
            }
            return worstSpread + 0.5f * meanSpread + 0.05f * release;
        }

        /// <summary>
        /// Searches the element numbers (Assay.ElementNumbers - the model's copy of
        /// Tuning.Elements) for the best parity the knobs' ranges allow: coordinate descent, each
        /// knob nudged up and down by its step, the step halved when nothing improves. Leaves the
        /// winning numbers in ElementNumbers and prints them - write them into Tuning.Elements, and
        /// <see cref="ElementNumbers.Drift"/> confirms when the two agree again.
        ///
        ///     unity command eval 'return Convergence.Balance.Assay.CalibrateElements();'
        /// </summary>
        public static string CalibrateElements(int rounds = 6)
        {
            var steps = Knobs.Select(k => k.Step).ToArray();
            float best = ParityCost(out float w0, out float m0);
            float startWorst = w0, startMean = m0;

            for (int round = 0; round < rounds; round++)
            {
                bool improved = false;
                for (int i = 0; i < Knobs.Length; i++)
                {
                    var field = NumberField(Knobs[i].Field);
                    float now = (float)field.GetValue(null);
                    foreach (float dir in new[] { 1f, -1f })
                    {
                        float next = Mathf.Clamp(now + dir * steps[i], Knobs[i].Min, Knobs[i].Max);
                        if (Mathf.Approximately(next, now)) continue;
                        field.SetValue(null, next);
                        float cost = ParityCost(out _, out _);
                        if (cost < best - 1e-5f) { best = cost; now = next; improved = true; }
                        else field.SetValue(null, now);
                    }
                }
                if (!improved) for (int i = 0; i < steps.Length; i++) steps[i] *= 0.5f;
            }

            ParityCost(out float worst, out float mean);
            var sb = new StringBuilder();
            sb.Append($"element parity: worst build spread {startWorst:P0} -> {worst:P0}, mean {startMean:P0} -> {mean:P0}\n");
            foreach (var k in Knobs)
                sb.Append($"  {k.Field,-32} {(float)NumberField(k.Field).GetValue(null),7:0.##}\n");
            sb.Append(ElementTable());
            return sb.ToString();
        }
    }
}
