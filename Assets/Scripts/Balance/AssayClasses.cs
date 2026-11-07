using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.Balance
{
    /// <summary>
    /// THE WEAPON CLASSES IN THE MODEL (the user's ask, 2026-11-06). Until now every build swung a
    /// greatsword: the chain, the crowd, the release shapes, the weapon's stat pool and the deals
    /// were a sword's, and the discs and the bow were unpriced.
    ///
    /// A class is read from the game's own rules, not a guess at its damage:
    /// - CADENCE: PlayerController.AttackInterval's class factor - the bow at Tuning.Bow.AttackIntervalMul,
    ///   the disc at its melee or its throw interval. A weapon art's lock is counted in those intervals,
    ///   so it slows with them.
    /// - EACH HIT: the bow's plant-to-fire (Tuning.Bow.MovingDamage on the share of a draw spent
    ///   moving) and its point-blank curve (NearFraction to 1 at the rim - ThrownArrow); the disc's
    ///   throw per-throw boost (ThrowDamagePerThrow) and its own curve (ThrowDamageNear - ThrownDisc).
    /// - THE CROWD: a sword's arc as before; a disc's arc at its shorter reach up close and a
    ///   ricochet at range; the bow strikes one body (its splash and pierce are the run's, priced
    ///   where they always were).
    /// - THE ARTS: still the model's one Medium art at the weight table's numbers - every class's
    ///   pool is declared to those totals (a disc's Ripple is four throws of 0.83, a Light's three
    ///   basics) - in the class's cadence and with its hits' rules.
    /// - SURVIVAL: <see cref="Assume.Exposure"/>, the one judged number here - the model counts how
    ///   many blows a build can take, never how often they come, and range is the bow's whole case.
    ///
    /// <c>Assay.Classes()</c> is the report.
    /// </summary>
    public static partial class Assay
    {
        public static partial class Assume
        {
            /// <summary>A disc's share of the fight swung up close (melee) rather than thrown -
            /// ThrowsInsteadOfSwinging reads the distance; a hybrid spends about half on each. Mine.</summary>
            public const float DiscMeleeShare = 0.5f;

            /// <summary>Where a disc's throw lands in its throw circle (0 the thrower, 1 the rim). The
            /// throw band starts at melee reach - half the circle - so the middle of the band. Mine.</summary>
            public const float DiscThrowAt = 0.75f;

            /// <summary>Where a bow's shot lands in its range circle - a bow player holds well out but
            /// is not always at the rim (the nearest-enemy lock drags targets in). Mine.</summary>
            public const float BowShotAt = 0.7f;

            /// <summary>Share of a bow's draw spent moving (plant to fire). Mine.</summary>
            public const float BowMovingDrawShare = 0.25f;

            /// <summary>
            /// Blows that reach a player of this class against a greatsword's, in the same fight.
            /// MINE, UNMEASURED: the bow holds the widest circle in the game and creates space, the
            /// disc spends half its fight at throwing range. Telemetry records hits per floor and the
            /// class (SHOWCASE.md "Telemetry") - move these to what played runs show once there are
            /// enough of them.
            /// </summary>
            public static float Exposure(WeaponClass w) => w switch
            {
                WeaponClass.Bow => 0.75f,
                WeaponClass.Disc => 0.9f,
                _ => 1f,
            };
        }

        public static partial class Targets
        {
            /// <summary>Worth spread across the classes, per build and element - the build spread's
            /// bar (PROVISIONAL, mine: no class target existed).</summary>
            public const float ClassSpread = 0.15f;
        }

        static readonly WeaponClass[] Classes3 = { WeaponClass.Greatsword, WeaponClass.Disc, WeaponClass.Bow };

        /// <summary>A class's chain against a sword's: the interval's factor, and what a basic and an
        /// art land for (the art's hits ride the same curves; the disc's throw boost is a basic's).</summary>
        public struct ClassChain
        {
            public float IntervalMul, BasicMul, ArtMul;
        }

        public static ClassChain ChainOf(WeaponClass w)
        {
            switch (w)
            {
                case WeaponClass.Bow:
                {
                    float shot = Mathf.Lerp(1f, Tuning.Bow.MovingDamage, Assume.BowMovingDrawShare)
                               * Mathf.Lerp(Tuning.Bow.NearFraction, 1f, Assume.BowShotAt);
                    return new ClassChain { IntervalMul = Tuning.Bow.AttackIntervalMul, BasicMul = shot, ArtMul = shot };
                }
                case WeaponClass.Disc:
                {
                    // Two cadences, blended by the time spent in each: the interval is the time-weighted
                    // one, and a hit's damage is set so damage a second is the blend of the two modes'.
                    float melee = Assume.DiscMeleeShare, thrown = 1f - melee;
                    float mI = Tuning.Disc.MeleeIntervalMul, tI = Tuning.Disc.ThrowIntervalMul;
                    float near = Mathf.Lerp(Tuning.Disc.ThrowDamageNear, 1f, Assume.DiscThrowAt);
                    float interval = melee * mI + thrown * tI;
                    float basicRate = melee * Tuning.Disc.MeleeDamageMul / mI + thrown * Tuning.Disc.ThrowDamagePerThrow * near / tI;
                    float artRate = melee * Tuning.Disc.MeleeDamageMul / mI + thrown * near / tI;
                    return new ClassChain { IntervalMul = interval, BasicMul = basicRate * interval, ArtMul = artRate * interval };
                }
                default:
                    return new ClassChain { IntervalMul = 1f, BasicMul = 1f, ArtMul = 1f };
            }
        }

        /// <summary>
        /// The chain's hits on the OTHER bodies, per unit of the chain on the target. A sword's arc
        /// (<paramref name="reached"/> bodies, Borax included, losing <paramref name="keep"/> a body);
        /// a disc's arc at its own reach up close and a ricochet at range (Tuning.Disc's bounces, the
        /// run's Ricochet added, each a share of the last); a bow's nothing.
        /// </summary>
        static float ClassThrough(WeaponClass w, float reached, float keep, Mods m)
        {
            switch (w)
            {
                case WeaponClass.Bow:
                    return 0f;
                case WeaponClass.Disc:
                {
                    float reach = Tuning.Player.BaseRange * Tuning.Disc.RangeMul;
                    float near = reached * BodiesWithin(reach) / Mathf.Max(1e-3f, Assume.CrowdBodies);
                    float arc = ChainThrough(near, keep);
                    float bounceTo = Mathf.Min(1f, BodiesWithin(reach * Tuning.Disc.BounceRangeMul));
                    int bounces = Tuning.Disc.BaseRicochets + (m?.ExtraRicochets ?? 0);
                    float ricochet = 0f, carry = 1f;
                    for (int i = 0; i < bounces; i++)
                    {
                        carry *= Tuning.Disc.BounceFalloff * bounceTo;
                        ricochet += carry;
                    }
                    return Assume.DiscMeleeShare * arc + (1f - Assume.DiscMeleeShare) * ricochet;
                }
                default:
                    return ChainThrough(reached, keep);
            }
        }

        // ------------------------------------------------------------------ the report

        /// <summary>Warms the class report one class at a time (eval's 5 s limit): 0 discs, 1 bow,
        /// then their sensible ledgers by element (2-9). Returns what it warmed, or "done".</summary>
        public static string ClassesWarm(int index)
        {
            var none = new Mods();
            if (index < 2)
            {
                var w = index == 0 ? WeaponClass.Disc : WeaponClass.Bow;
                foreach (var a in Archetypes) foreach (var e in Elements) Evaluate(Make(a, e, Kit.Max, w), none, _ => 0, 1);
                foreach (var a in Archetypes) GearMultiplier(a, w);
                return $"{index}: {w} builds";
            }
            int i = index - 2;
            if (i >= 2 * Elements.Length) return "done";
            var wc = i < Elements.Length ? WeaponClass.Disc : WeaponClass.Bow;
            var el = Elements[i % Elements.Length];
            Run(Make(Archetype.Striker, el, Kit.Max, wc), Policy.Sensible, 60);
            return $"{index}: {wc} {el} ledger";
        }

        /// <summary>
        /// The three classes side by side, on ONE scale (the greatsword's unbuffed Reference): each
        /// Max build's worth (damage / survival), the spread across classes per build and element,
        /// a fresh character, gear's multiplier, one release, the careful ledger, and Chaser hits to
        /// die deep. Heavy - warm with <see cref="ClassesWarm"/> first, or call again after a timeout.
        /// </summary>
        public static string Classes()
        {
            var none = new Mods();
            var reference = Evaluate(Reference(), none, _ => 0, 1);
            float D(Worth w) => w.Dps / reference.Dps;
            float H(Worth w) => w.Ehp / reference.Ehp;
            float X(Worth w) => Mathf.Sqrt(D(w) * H(w));

            var sb = new StringBuilder("THE CLASSES - worth x the sword's unbuffed Reference, sqrt(damage x survival); (damage / survival)\n");
            foreach (var w in Classes3)
            {
                var c = ChainOf(w);
                sb.Append($"  {w}: interval x{c.IntervalMul:0.00}, basic x{c.BasicMul:0.00}, art x{c.ArtMul:0.00}, ")
                  .Append($"exposure x{Assume.Exposure(w):0.00}\n");
            }

            var cells = new Dictionary<(Archetype, ElementType, WeaponClass), Worth>();
            foreach (var a in Archetypes) foreach (var e in Elements) foreach (var w in Classes3)
                cells[(a, e, w)] = Evaluate(Make(a, e, Kit.Max, w), none, _ => 0, 1);

            sb.Append("\nMax kit, no ledger\n");
            float worstSpread = 0f;
            string worstCell = "";
            foreach (var a in Archetypes)
            {
                sb.Append($"{a}\n");
                foreach (var e in Elements)
                {
                    sb.Append($"  {e,-6}");
                    foreach (var w in Classes3)
                    {
                        var v = cells[(a, e, w)];
                        sb.Append($"  {w,-10} {X(v),4:0.0} ({D(v),4:0.0} / {H(v),3:0.0})");
                    }
                    float spread = Spread(Classes3.Select(w => X(cells[(a, e, w)])).ToArray());
                    if (spread > worstSpread) { worstSpread = spread; worstCell = $"{a} {e}"; }
                    sb.Append($"   spread {spread:P0}\n");
                }
            }

            sb.Append("\nby class                 mean worth  dmg   surv  | fresh  | gear x  | one release | careful ledger @100 | Chaser hits to die @100\n");
            foreach (var w in Classes3)
            {
                var mine = cells.Where(kv => kv.Key.Item3 == w).Select(kv => kv.Value).ToList();
                var fresh = Elements.Select(e => X(Evaluate(Reference(e, w), none, _ => 0, 1))).Average();
                var gear = Archetypes.Select(a => GearMultiplier(a, w)).Average();
                var release = Elements.Select(e => ReleaseSeconds(Reference(e, w))).ToArray();
                var ledger = Elements.Average(e =>
                {
                    var r = Run(Make(Archetype.Striker, e, Kit.Max, w), Policy.Sensible, 60);
                    return r.Dps[LastFloor] / r.Dps[1];
                });
                var deep = Archetypes.SelectMany(a => Elements.Select(e =>
                    Evaluate(Make(a, e, Kit.Max, w), none, _ => 0, LastFloor).Ehp / ChaserHit(LastFloor))).ToArray();
                sb.Append($"  {w,-22} {mine.Average(X),5:0.0}x  {mine.Average(D),4:0.0}  {mine.Average(H),4:0.0}  | ")
                  .Append($"{fresh,4:0.00}x | x{gear:0.00}  | {release.Min():0.0}-{release.Max():0.0}s    | ")
                  .Append($"{ledger:0.00}x               | {deep.Min():0.0}-{deep.Max():0.0}\n");
            }

            sb.Append($"\nclass spread, worst build/element: {worstSpread:P0} ({worstCell}) - provisional bar <= {Targets.ClassSpread:P0}: ")
              .Append(worstSpread <= Targets.ClassSpread ? "PASS" : "over").Append('\n');
            sb.Append("judged, not measured: Assume.Exposure (blows that reach each class), DiscMeleeShare, DiscThrowAt, BowShotAt, ")
              .Append("BowMovingDrawShare. Not modelled: the pools' Light/Heavy mix (every class swings the Medium art), ")
              .Append("bespoke art rules (marks, tethers, volleys beyond their weight's total), the disc's catch timing.");
            return sb.ToString();
        }
    }
}
