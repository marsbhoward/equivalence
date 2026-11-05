using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Balance
{
    /// <summary>
    /// THE ARSENAL SIDE BY SIDE (RELEASES.md "Single target and the crowd"): what a FULL METER of each
    /// kind adds, on a fresh character carrying that kind alone, in seconds of its own damage - on
    /// ONE target, and on the PACK (the target and <see cref="Assay.Assume.CrowdPack"/> round it,
    /// every body counted whole). Read with <c>Assay.Arsenal()</c>.
    ///
    /// The rule it checks (the user's, 2026-11-05): the single-target kinds (Combo, Buff) out-damage
    /// the crowd kind (Blast) on one target, and the Blast out-damages them on the pack, each by
    /// <see cref="Targets.RoleMargin"/>. Guard and Mobility pay for defence and movement: their damage
    /// stays under both.
    /// </summary>
    public static partial class Assay
    {
        public static partial class Targets
        {
            /// <summary>How far a kind leads in its own fight: x1.25 or more.</summary>
            public const float RoleMargin = 1.25f;
        }

        static readonly ReleaseKind[] Kinds = { ReleaseKind.Blast, ReleaseKind.Buff, ReleaseKind.Combo, ReleaseKind.Guard, ReleaseKind.Mobility };

        /// <summary>One full meter of <paramref name="kind"/> on a fresh character, in seconds of its own
        /// single-target damage: (on one target fought alone, on the whole pack).</summary>
        public static (float one, float pack) KindSeconds(ElementType e, ReleaseKind kind, WeaponClass w = WeaponClass.Greatsword)
        {
            _weapon = w;
            try { return KindSecondsNow(e, kind); }
            finally { _weapon = WeaponClass.Greatsword; }
        }

        static (float one, float pack) KindSecondsNow(ElementType e, ReleaseKind kind)
        {
            var b = Carrying(e, kind);
            var none = new Exchange.Mods();
            var on = Evaluate(b, none, _ => 0, 1);
            _alone = true;
            Worth alone;
            try { alone = Evaluate(b, none, _ => 0, 1); }
            finally { _alone = false; }
            _releasesOff = true;
            Worth off;
            try { off = Evaluate(b, none, _ => 0, 1); }
            finally { _releasesOff = false; }
            float gain = StatCurves.Character(StatKind.ElementGrowth, b.GainRatePoints);
            float every = 1f / Mathf.Max(1e-4f, Profile(e, gain, _ => 0, false, b).ReleasesPerSecond);
            float unit = Mathf.Max(1e-3f, off.TargetDps) / every;
            return ((alone.TargetDps - off.TargetDps) / unit,
                    (on.TargetDps + on.CrowdDps - off.TargetDps - off.CrowdDps) / unit);
        }

        public static string Arsenal()
        {
            var sb = new StringBuilder("A FULL METER, fresh character, seconds of its own damage: one target alone / the pack (target + "
                                       + $"{Assume.CrowdPack:0} round it), the shapes read for each weapon on the one chain\n");
            bool stAhead = true, mtAhead = true;
            foreach (var w in new[] { WeaponClass.Greatsword, WeaponClass.Disc, WeaponClass.Bow })
            foreach (var e in new[] { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air })
            {
                if (e == ElementType.Fire) sb.Append($"{w,-10}  blast        buff         combo        guard        mobility\n");
                var v = Kinds.Select(k => KindSeconds(e, k, w)).ToArray();
                sb.Append($"  {e,-7}");
                foreach (var (one, pack) in v) sb.Append($"  {one,4:0.0} / {pack,4:0.0}");
                var (blast, buff, combo) = (v[0], v[1], v[2]);
                bool st = Mathf.Min(buff.one, combo.one) >= Targets.RoleMargin * blast.one;
                bool mt = blast.pack >= Targets.RoleMargin * Mathf.Max(buff.pack, combo.pack);
                stAhead &= st;
                mtAhead &= mt;
                sb.Append(st ? "" : "  <- blast ahead on one").Append(mt ? "" : "  <- blast behind on the pack").Append('\n');
            }
            sb.Append($"single target ahead on one (x{Targets.RoleMargin}): {(stAhead ? "OK" : "NO")}; ")
              .Append($"blast ahead on the pack: {(mtAhead ? "OK" : "NO")}\n");

            sb.Append("\nhit units a full meter, alone / pack, by weapon (AssayShapes - a Buff's is Tremor's crack a swing)\n");
            foreach (var w in new[] { WeaponClass.Greatsword, WeaponClass.Disc, WeaponClass.Bow })
            {
                sb.Append($"{w}\n");
                foreach (var e in new[] { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air })
                {
                    sb.Append($"  {e,-7}");
                    foreach (var k in Kinds)
                    {
                        var h = Shape(e, k, w);
                        sb.Append($"  {h.Alone,4:0.0} / {h.Pack,4:0.0}");
                    }
                    sb.Append('\n');
                }
            }
            sb.Append("not counted: knockback and pulls, soak's and stagger's follow-ups beyond the soak's damage, Windbreak, ")
              .Append("Mudslide's wall, Wavecrest's line, Zephyr's gusts; Mist Veil's heal is survival, not here.");
            return sb.ToString();
        }
    }
}
