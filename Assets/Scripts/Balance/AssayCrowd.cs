using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;
using Convergence.Progression;
using B = Convergence.Core.Tuning.Board;
using E = Convergence.Core.Tuning.Elements;

namespace Convergence.Balance
{
    /// <summary>
    /// The crowd: what a build's hits do to the OTHER bodies round the one being fought. Without
    /// it the model was blind to Cleave, Splash and Area, and to every release's radius - a board
    /// spent on Area was worth nothing, and the Elementalist read as the weakest build by a third.
    ///
    /// Round the fight stand <see cref="Assay.Assume.CrowdPack"/> other bodies, thinning with
    /// distance: <see cref="BodiesWithin"/> is that pack's share inside a radius, shaped so a
    /// sword's reach (Tuning.Player.BaseRange) finds <see cref="Assay.Assume.CrowdBodies"/>. Every
    /// effect reads the radius the GAME gives it, Area included, and hits those bodies by the
    /// game's own rule: the blade's per-body falloff, the Water burst's split pool, the quake's
    /// shrinking aftershocks.
    ///
    /// The result is raw damage a second (Worth.CrowdDps); Evaluate credits it into Dps at
    /// <see cref="Assay.Assume.CrowdCredit"/>. Not here: Range (a longer capsule), area weapon
    /// arts (the model's chain swings a plain Medium art), Air's Vortex and Fire's Ignite (the
    /// model prices each element's first ability), and the Burn and Soak branches' spreading rules
    /// (Cineration, Aqua Fortis) - no assayed build owns them.
    /// </summary>
    public static partial class Assay
    {
        /// <summary>Other enemies within <paramref name="radius"/> of the body being fought.</summary>
        public static float BodiesWithin(float radius)
        {
            float x = radius / CrowdSpread;
            return Assume.CrowdPack * (1f - Mathf.Exp(-x * x));
        }

        /// <summary>The pack's spread, from the two stated numbers: a sword's reach finds
        /// CrowdBodies of CrowdPack.</summary>
        static float CrowdSpread
            => Tuning.Player.BaseRange / Mathf.Sqrt(-Mathf.Log(1f - Assume.CrowdBodies / Assume.CrowdPack));

        /// <summary>
        /// Damage a second on the crowd. <paramref name="targetChain"/> is the chain on the body
        /// being fought (soak included); <paramref name="releaseScale"/> is Elemental Power times
        /// the ledger's release multiplier, as the release on the target is.
        /// </summary>
        static float CrowdDps(Build b, StatPercents s, Mods m, ElementProfile el, ChainShape c,
                              float targetChain, float hitUnit, float releaseScale, float releasesPerSecond)
        {
            float area = StatPercents.Apply(1f, s.AoeRadius) * m.AreaMul;   // PlayerController.AoeScale
            float crowd = 0f;

            // ---- the swing: a blade loses energy through a crowd (ResolveArc's chain), Cleave
            // shrinking the loss. Borax bunches the pack after a release, so more of it is in reach.
            float reached = Assume.CrowdBodies;
            if (Owns(b, Notable.Borax))
            {
                float bunched = Mathf.Min(1f, Assume.BoraxHoldSeconds * releasesPerSecond);
                reached = Mathf.Lerp(reached, BodiesWithin(Tuning.Player.BaseRange + B.BoraxPullDistance), bunched);
            }
            float keep = 1f - (1f - SwingFalloff) * StatPercents.ReductionFactor(s.Cleave + m.CleavePoints);
            crowd += targetChain * ChainThrough(reached, keep);

            // ---- Splash: a share of every hit on the designated target, to each body near it.
            crowd += targetChain * s.Splash / 100f * BodiesWithin(Tuning.Attack.SplashRadius);

            // ---- Fire's fourth rung: every hit splashes onto one more body. Average heat 3 is
            // never there, 5 always.
            if (b.Element == ElementType.Fire)
            {
                float rung = Mathf.Clamp01((el.FireHeat - 3f) / 2f);
                crowd += targetChain * ElementNumbers.FireSplashFraction * Mathf.Min(1f, BodiesWithin(E.Fire.SplashRadius)) * rung;
            }

            // ---- Precipitation: the weapon art's shockwave round the target.
            if (Owns(b, Notable.Precipitation) && c.Seconds > 0f)
                crowd += BoardNumbers.PrecipitationFraction * c.ArtHit / c.Seconds * BodiesWithin(B.PrecipitationRadius);

            // ---- the release, on everything else it reaches
            crowd += ReleaseCrowdHitUnits(b.Element, el, area) * hitUnit * releaseScale * releasesPerSecond;

            // ---- Fermentation: the salted ground under the rest of the pack.
            if (Owns(b, Notable.Fermentation))
                crowd += FermentationDps(b, hitUnit, releasesPerSecond) * BodiesWithin(B.FermentationRadius * area);

            return crowd;
        }

        /// <summary>CONCENTRATE: what a release from a full meter adds on the crowd by reaching
        /// further - the release's crowd at the wider radius less its crowd at the usual one.</summary>
        static float ConcentrateCrowd(Build b, StatPercents s, Mods m, ElementProfile el, float hitUnit,
                                      float releaseScale, float releasesPerSecond)
        {
            if (b.Element == null) return 0f;
            float area = StatPercents.Apply(1f, s.AoeRadius) * m.AreaMul;
            float full = b.Element == ElementType.Fire || b.Element == ElementType.Air ? 1f : Assume.FullReleaseShare;
            float more = ReleaseCrowdHitUnits(b.Element, el, area * Tuning.Exchange.ConcentrateFullRadiusMul)
                       - ReleaseCrowdHitUnits(b.Element, el, area);
            return Mathf.Max(0f, more) * hitUnit * releaseScale * releasesPerSecond * full;
        }

        /// <summary>What the bodies after the first keep of a swing, summed over
        /// <paramref name="bodies"/> of them (a fraction counts as part of a body), floored as
        /// ResolveArc floors it.</summary>
        static float ChainThrough(float bodies, float keep)
        {
            float sum = 0f, chain = 1f;
            for (int k = 1; k <= Mathf.CeilToInt(bodies); k++)
            {
                chain = Mathf.Max(Tuning.Attack.CleaveFalloffFloor, chain * keep);
                sum += chain * Mathf.Min(1f, bodies - (k - 1));
            }
            return sum;
        }

        /// <summary>
        /// One release on the bodies OTHER than the target, in hit units before Elemental Power -
        /// the counterpart of ElementProfile.ReleaseHitUnits, each element by its own shape.
        /// </summary>
        static float ReleaseCrowdHitUnits(ElementType? element, ElementProfile el, float area)
        {
            if (element == null || el.ReleaseHitUnits <= 0f) return 0f;
            switch (element.Value)
            {
                case ElementType.Fire:
                {
                    // Erupt's pools land round the player, spread and size both grown by Area, so
                    // the share of the ground under a pool holds. A body in the pack stands in one
                    // for that share of the time; the target, fought where the pools are, for
                    // FireTargetInPool.
                    float covered = E.Fire.EruptPools * E.Fire.PoolRadius * E.Fire.PoolRadius
                                  / (E.Fire.EruptSpread * E.Fire.EruptSpread);
                    float perBody = Mathf.Min(1f, covered / Assume.FireTargetInPool);
                    return el.ReleaseHitUnits * perBody * BodiesWithin(E.Fire.EruptSpread * area);
                }

                case ElementType.Water:
                {
                    // One pool shared by everything caught, softened so a crowd takes more in total
                    // (WaterResource.Cash): n bodies take n^(1 - falloff) of it. The model gives the
                    // target the whole pool, so the crowd is the rest.
                    float caught = 1f + BodiesWithin(E.Water.Radius * area);
                    return el.ReleaseHitUnits * (Mathf.Pow(caught, 1f - E.Water.BurstSplitFalloff) - 1f);
                }

                case ElementType.Earth:
                {
                    // The quake at full charge, then aftershocks each weaker and narrower
                    // (EarthResource.Erupt); every body inside a shock takes it whole.
                    float weights = 1f, reached = BodiesWithin(E.Earth.Radius * area);
                    int after = Mathf.FloorToInt(E.Earth.ChargeSeconds / E.Earth.AftershockPerSeconds);
                    for (int i = 0; i < after; i++)
                    {
                        float w = 0.55f - i * 0.08f;
                        weights += w;
                        reached += w * BodiesWithin(E.Earth.Radius * area * Mathf.Max(0.4f, w));
                    }
                    return el.ReleaseHitUnits * reached / weights;
                }

                default:
                    return 0f;   // Air's Gust deals nothing itself
            }
        }

        /// <summary>Fermentation on ONE body standing on the salted ground through its seconds -
        /// the target, as every release is set off beside it.</summary>
        static float FermentationDps(Build b, float hitUnit, float releasesPerSecond)
            => Owns(b, Notable.Fermentation)
                ? hitUnit * BoardNumbers.FermentationHitFractionPerSecond * B.FermentationSeconds * releasesPerSecond
                : 0f;
    }
}
