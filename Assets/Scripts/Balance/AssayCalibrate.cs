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
            // Fire: heat stacks into damage and speed - five stacks must still be felt; Erupt is
            // area denial.
            new("FireStackDamagePoints", 5f, 8f, 1f),
            new("FireHastePoints", 15f, 30f, 4f),
            new("FirePoolHitUnitsPerSecond", 2f, 14f, 1f),
            // Water: the decision-maker whose release carries it. Everything else it has IS its
            // release, so with the release held to the band its only standing lever is a small
            // damage head start (never more than Earth's least).
            new("WaterDamagePoints", -15f, 15f, 3f),
            new("WaterSurgePoints", 30f, 60f, 5f),
            new("WaterBurstHitUnits", 4f, 30f, 2f),
            // Earth: heavy and clearly slower - the biggest damage head start of the four.
            new("EarthDamagePoints", 25f, 45f, 5f),
            new("EarthAttackSpeedPoints", -30f, -10f, 5f),
            new("EarthShockHitUnits", 1.5f, 4f, 0.4f),
            new("EarthShockHitUnitsPerCharge", 2.5f, 8f, 0.8f),
            // Air: light hits, fast swings - momentum must still make it fast.
            new("AirDamagePoints", -50f, -20f, 5f),
            new("AirAttackSpeedPoints", 0f, 30f, 5f),
            new("AirAttackSpeedPointsPerMomentum", 60f, 100f, 10f),
            new("AirCritDamagePoints", 10f, 40f, 5f),
            new("AirGustDamagePoints", 0f, 90f, 10f),
            // Fire's fourth rung: a splash, never less than a quarter of the hit.
            new("FireSplashFraction", 0.25f, 0.5f, 0.05f),
            // The survival half. Earth the toughest, Air the most fragile, always.
            new("EarthHp", 125f, 160f, 5f),
            new("EarthStillDr", 0.15f, 0.25f, 0.025f),
            new("EarthMovingDr", 0.05f, 0.12f, 0.01f),
            new("AirHp", 80f, 105f, 5f),
            // The board's rules (BoardNumbers): the Area branch's salted ground, the Weight branch's
            // shockwave (Striker and Bulwark), the Strikes branch's two (Striker and Tempo), and the
            // Element branch's two (the Elementalist).
            new("FermentationHitFractionPerSecond", 0.10f, 0.35f, 0.05f),
            new("PrecipitationFraction", 0.25f, 0.40f, 0.05f),
            new("VitriolMax", 0.05f, 0.10f, 0.01f),
            new("CohobationPerBasic", 0.04f, 0.08f, 0.01f),
            new("ExaltationDamagePoints", 25f, 40f, 5f),
            new("AlkahestRefund", 0.20f, 0.30f, 0.05f),
        };

        static FieldInfo NumberField(string name)
            => typeof(ElementNumbers).GetField(name, BindingFlags.Public | BindingFlags.Static)
               ?? typeof(BoardNumbers).GetField(name, BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// The balance rule, as one number to make small, every term on WORTH - sqrt(damage x
        /// survival), Max kit, no ledger:
        ///   the worst build's spread across the four elements, plus half the average build's;
        ///   the spread across the four builds;
        ///   a fifth of the average DAMAGE spread, so parity comes from moderate trades rather than
        ///     an element that cannot kill and another that cannot live;
        ///   how far a fresh character's release strays outside 3-5 s, or a Max Elementalist's
        ///     past its ceiling (Targets);
        ///   how far the average worth has moved from <see cref="_level"/> - the start's, held
        ///     inside the Max character's band - so the weak are lifted as much as the strong are cut.
        /// </summary>
        static float ParityCost(out float worstSpread, out float meanSpread) => ParityCost(out worstSpread, out meanSpread, out _);

        /// <summary>The average worth (x unbuffed) the search holds to; 0 while unset.</summary>
        static float _level;

        static float ParityCost(out float worstSpread, out float meanSpread, out float level)
        {
            _builds.Clear();   // the gear is rolled around the element's head starts, which just moved
            var none = new Exchange.Mods();
            var reference = Evaluate(Reference(), none, _ => 0, 1);
            worstSpread = 0f; meanSpread = 0f; level = 0f;
            float damageSpread = 0f;
            var buildWorth = new float[Archetypes.Length];
            for (int i = 0; i < Archetypes.Length; i++)
            {
                var w = Elements.Select(e => Evaluate(Make(Archetypes[i], e, Kit.Max), none, _ => 0, 1)).ToArray();
                var v = w.Select(x => Mathf.Sqrt(x.Dps / reference.Dps * (x.Ehp / reference.Ehp))).ToArray();
                float s = Spread(v);
                worstSpread = Mathf.Max(worstSpread, s);
                meanSpread += s / Archetypes.Length;
                damageSpread += Spread(w.Select(x => x.Dps).ToArray()) / Archetypes.Length;
                buildWorth[i] = v.Average();
                level += buildWorth[i] / Archetypes.Length;
            }

            float release = 0f;
            foreach (var e in Elements)
            {
                float fresh = ReleaseSeconds(Reference(e));
                release += Mathf.Max(0f, Targets.ReleaseSecondsMin - fresh) + Mathf.Max(0f, fresh - Targets.ReleaseSecondsMax);
                float invested = ReleaseSeconds(Make(Archetype.Elementalist, e, Kit.Max));
                release += Mathf.Max(0f, invested - Targets.ReleaseSecondsInvestedMax);
            }

            float drift = _level > 0f ? Mathf.Abs(level / _level - 1f) : 0f;
            return worstSpread + 0.5f * meanSpread + Spread(buildWorth) + 0.2f * damageSpread + 0.05f * release + drift;
        }

        /// <summary>
        /// Searches the element numbers (Assay.ElementNumbers, the model's copy of Tuning.Elements)
        /// and the board's crowd rules (BoardNumbers, of Tuning.Board) for the best balance the
        /// knobs' ranges allow: coordinate descent, each knob nudged up and down by its step, the
        /// step halved when nothing improves. Leaves the winning numbers in the copies and prints
        /// them - write them into Tuning, and the two Drift()s confirm when they agree again.
        ///
        /// A whole search outlasts eval's 5s limit, so it RESUMES: <see cref="CalibrateStart"/>
        /// once, then <see cref="CalibrateStep"/> until it returns the report.
        ///
        ///     unity command eval --code 'return Convergence.Balance.Assay.CalibrateStart(8);'
        ///     unity command eval --code 'return Convergence.Balance.Assay.CalibrateStep();'   # repeat
        /// </summary>
        public static string CalibrateElements(int rounds = 6)
        {
            CalibrateStart(rounds);
            return CalibrateStep(int.MaxValue);
        }

        /// <summary>The last search's report, null while one runs.</summary>
        public static string LastCalibration;

        // The search in flight - plain statics, so a domain reload simply ends it.
        static float[] _steps;
        static int _rounds, _round, _knob, _dir;
        static bool _searching, _improved;
        static float _best, _startWorst, _startMean;

        public static string CalibrateStart(int rounds = 6)
        {
            ElementNumbers.Reset();   // from Tuning's numbers, never from the last search's
            BoardNumbers.Reset();
            _steps = Knobs.Select(k => k.Step).ToArray();
            _level = 0f;
            ParityCost(out _, out _, out float start);
            _level = Mathf.Clamp(start, Targets.Character * (1f - Targets.CharacterBand),
                                        Targets.Character * (1f + Targets.CharacterBand));
            _best = ParityCost(out _startWorst, out _startMean);
            _rounds = rounds; _round = 0; _knob = 0; _dir = 0; _improved = false;
            _searching = true;
            LastCalibration = null;
            return $"searching {Knobs.Length} knobs, {rounds} rounds, cost {_best:0.0000}";
        }

        /// <summary>Runs the search for about <paramref name="millis"/> and says where it got to -
        /// or, when it is done, returns the report.</summary>
        public static string CalibrateStep(int millis = 3500)
        {
            if (!_searching) return LastCalibration ?? "no search started - CalibrateStart first";
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < millis)
            {
                if (_round >= _rounds)
                {
                    _searching = false;
                    LastCalibration = CalibrationReport();
                    return LastCalibration;
                }

                var knob = Knobs[_knob];
                var field = NumberField(knob.Field);
                float now = (float)field.GetValue(null);
                float next = Mathf.Clamp(now + (_dir == 0 ? 1f : -1f) * _steps[_knob], knob.Min, knob.Max);
                if (!Mathf.Approximately(next, now))
                {
                    field.SetValue(null, next);
                    float cost = ParityCost(out _, out _);
                    if (cost < _best - 1e-5f) { _best = cost; _improved = true; }
                    else field.SetValue(null, now);
                }

                if (++_dir > 1)
                {
                    _dir = 0;
                    if (++_knob >= Knobs.Length)
                    {
                        _knob = 0;
                        if (!_improved) for (int i = 0; i < _steps.Length; i++) _steps[i] *= 0.5f;
                        _improved = false;
                        _round++;
                    }
                }
            }
            return $"round {_round + 1}/{_rounds}, knob {_knob + 1}/{Knobs.Length}, cost {_best:0.0000}";
        }

        static string CalibrationReport()
        {
            float startWorst = _startWorst, startMean = _startMean;
            ParityCost(out float worst, out float mean);
            var sb = new StringBuilder();
            sb.Append($"element parity: worst build spread {startWorst:P0} -> {worst:P0}, mean {startMean:P0} -> {mean:P0}\n");
            foreach (var k in Knobs)
                sb.Append($"  {k.Field,-32} {(float)NumberField(k.Field).GetValue(null),7:0.##}\n");
            sb.Append(ElementTable());
            var gear = Archetypes.Select(GearMultiplier).ToArray();
            sb.Append($"gear's multiplier by build: {string.Join("  ", Archetypes.Select((a, i) => $"{a} x{gear[i]:0.00}"))}\n");
            return sb.ToString();
        }
    }
}
