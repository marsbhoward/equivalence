using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using R = Convergence.Core.Tuning.Releases;
using E = Convergence.Core.Tuning.Elements;

namespace Convergence.Balance
{
    /// <summary>
    /// THE SHAPES (2026-11-05): what each damaging release does to ONE target and to a PACK, read
    /// from the game's own shapes. A sampled fight - the player, the target at the weapon's range,
    /// <see cref="Assay.Assume.CrowdPack"/> others round it spread as <see cref="Assay.BodiesWithin"/>
    /// spreads them - and each release replayed on it by the rules its code uses: the overlap tests
    /// (a body is caught by its edge, <see cref="ShapeAssume.BodyRadius"/>), the nearest and the
    /// knot of the crowd (ReleaseArsenal's Nearest and Crowd), cones and lines, Erupt's and Squall's
    /// scatter, Burst on everything it catches, the quake's shrinking aftershocks - a bow's Blasts a step
    /// short of the body it aims at.
    ///
    /// In hit units per FULL METER (a combo's three strikes, a Guard's three charges, two moves),
    /// before Elemental Power. Held still: knockback, pulls (bar Backdraft's, which sets up its own
    /// hit), statuses other than Fire's burn, and bodies walking into lasting ground after it lands -
    /// a body caught at the release spends <see cref="ShapeAssume.GroundStay"/> of the ground's life in it.
    /// </summary>
    public static partial class Assay
    {
        public static class ShapeAssume
        {
            /// <summary>Where the target stands from the player: a blade's reach, a bow's standoff.</summary>
            public const float MeleeRange = 1.4f, BowRange = 4.5f;
            /// <summary>An enemy's collider (EnemyFactory: half its size) - the game's overlap tests
            /// catch a body by its edge.</summary>
            public const float BodyRadius = 0.4f;
            public const int Samples = 600;
            /// <summary>Share of a lasting ground's life (Erupt's pools) a body caught in it at the
            /// release spends in it; a vortex holds what it catches longer (it pulls).</summary>
            public const float GroundStay = 0.6f, VortexStay = 0.8f;
            /// <summary>Seconds between combo strikes - how long a strike's burn runs on into the next.</summary>
            public const float StrikeGap = 1f;
            /// <summary>Share of a Guard's charges that catch an attacker beside the player (a dash
            /// can end away from them), and of a move's landing (Comet) that catches anyone.</summary>
            public const float GuardCatches = 0.6f, MoveCatches = 0.5f;
        }

        /// <summary>Hit units on the target in the pack, on the target and the pack together, and on
        /// the target fought ALONE - the single-target fight (a shared pool is all its own).</summary>
        public struct Hits
        {
            public float One, Pack, Alone;
            public float Others => Pack - One;
        }

        /// <summary>Not readonly: a domain reload re-creates it empty.</summary>
        static Dictionary<string, Hits> _shapes;

        /// <summary>One full meter of <paramref name="kind"/> on the sampled fight, at
        /// <paramref name="area"/> (PlayerController.AoeScale). A Buff deals nothing itself - except
        /// Tremor Stance, whose value here is ONE swing's crack (the window's count is the chain's).</summary>
        public static Hits Shape(ElementType e, ReleaseKind kind, WeaponClass w = WeaponClass.Greatsword, float area = 1f)
        {
            area = Mathf.Round(area * 20f) / 20f;
            string key = $"{e}/{kind}/{w}/{area:0.00}";
            _shapes ??= new Dictionary<string, Hits>();
            if (_shapes.TryGetValue(key, out var v)) return v;

            var rng = new System.Random(7919);
            var sim = new ShapeSim { Area = area, Weapon = w, Element = e };
            // A Guard answers whoever is on the player; everything else is fought at the weapon's range.
            float range = kind == ReleaseKind.Guard || w != WeaponClass.Bow ? ShapeAssume.MeleeRange : ShapeAssume.BowRange;
            double one = 0, pack = 0, alone = 0;
            for (int i = 0; i < ShapeAssume.Samples; i++)
            {
                sim.Lay(rng, range, Mathf.RoundToInt(Assume.CrowdPack));
                var got = sim.Run(kind, rng);
                one += got[0];
                foreach (var x in got) pack += x;
                sim.Lay(rng, range, 0);
                alone += sim.Run(kind, rng)[0];
            }
            int n = ShapeAssume.Samples;
            v = new Hits { One = (float)(one / n), Pack = (float)(pack / n), Alone = (float)(alone / n) };
            _shapes[key] = v;
            return v;
        }

        /// <summary>The sampled fight and the release code's shapes, replayed on it.</summary>
        sealed class ShapeSim
        {
            public float Area;
            public WeaponClass Weapon;
            public ElementType Element;

            Vector2[] _p;
            float[] _got;
            int[] _struck;
            float[] _burn;
            int _quarry = -1;
            bool[] _thisStrike;
            Vector2 _me;

            bool Bow => Weapon == WeaponClass.Bow;
            static float Reach => R.ComboReach;
            static float Br => ShapeAssume.BodyRadius;

            public void Lay(System.Random rng, float range, int others)
            {
                int n = 1 + others;
                if (_p == null || _p.Length != n)
                {
                    _p = new Vector2[n];
                    _got = new float[n];
                    _struck = new int[n];
                    _burn = new float[n];
                    _thisStrike = new bool[n];
                }
                _me = Vector2.zero;
                _p[0] = new Vector2(range, 0f);
                // BodiesWithin(r) = pack x (1 - exp(-(r/spread)^2)): a round Gaussian of sigma spread/sqrt2.
                float sigma = CrowdSpread / Mathf.Sqrt(2f);
                for (int i = 1; i < n; i++)
                {
                    Vector2 at;
                    int tries = 0;
                    do
                    {
                        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                        double mag = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * sigma;
                        at = _p[0] + new Vector2((float)(mag * System.Math.Cos(2 * System.Math.PI * u2)),
                                                 (float)(mag * System.Math.Sin(2 * System.Math.PI * u2)));
                    } while (at.magnitude < range && ++tries < 24);   // the target is the nearest - the one being fought
                    _p[i] = at;
                }
            }

            public float[] Run(ReleaseKind kind, System.Random rng)
            {
                System.Array.Clear(_got, 0, _got.Length);
                System.Array.Clear(_struck, 0, _struck.Length);
                switch (kind)
                {
                    case ReleaseKind.Blast: Blast(rng); break;
                    case ReleaseKind.Combo: Combo(); break;
                    case ReleaseKind.Guard:
                        for (int c = 0; c < R.GuardCharges; c++) Guard(ShapeAssume.GuardCatches);
                        break;
                    case ReleaseKind.Mobility:
                        for (int c = 0; c < Mathf.RoundToInt(1f / R.MobilityCost); c++) Move(ShapeAssume.MoveCatches);
                        break;
                    case ReleaseKind.Buff:
                        if (Element == ElementType.Earth) Crack();
                        break;
                }
                return _got;
            }

            // ---- the release code's helpers (ReleaseArsenal) ----

            IEnumerable<int> Bodies(Vector2 c, float r)
            {
                for (int i = 0; i < _p.Length; i++)
                    if ((_p[i] - c).magnitude <= r + Br) yield return i;
            }

            int Nearest(Vector2 from, float reach, HashSet<int> skip = null)
            {
                int best = -1;
                float bd = float.MaxValue;
                foreach (int i in Bodies(from, reach))
                {
                    if (skip != null && skip.Contains(i)) continue;
                    float d = (_p[i] - from).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                return best;
            }

            Vector2 Crowd(float reach)
            {
                int near = Nearest(_me, reach);
                if (near < 0) return _me + Vector2.right * 2.4f;
                Vector2 sum = Vector2.zero;
                int n = 0;
                foreach (int i in Bodies(_p[near], 1.6f)) { sum += _p[i]; n++; }
                return n > 0 ? sum / n : _p[near];
            }

            Vector2 Toward(float reach)
            {
                int near = Nearest(_me, reach);
                if (near < 0) return Vector2.right;
                var d = _p[near] - _me;
                return d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.right;
            }

            static float DistToSegment(Vector2 x, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(x - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return Vector2.Distance(x, a + ab * t);
            }

            IEnumerable<int> InLine(Vector2 a, Vector2 b, float half)
            {
                foreach (int i in Bodies((a + b) * 0.5f, Vector2.Distance(a, b) * 0.5f + half + 0.5f))
                    if (DistToSegment(_p[i], a, b) <= half) yield return i;
            }

            IEnumerable<int> InCone(Vector2 from, Vector2 dir, float length, float halfAngleDeg)
            {
                foreach (int i in Bodies(from, length))
                {
                    var d = _p[i] - from;
                    if (d.sqrMagnitude < 0.36f || Vector2.Angle(dir, d) <= halfAngleDeg) yield return i;
                }
            }

            /// <summary>A strike lands whole on its quarry, a share on the rest (ReleaseArsenal.StrikeShare).</summary>
            void Deal(int i, float units)
            {
                float share = _quarry < 0 || i == _quarry ? 1f : R.ComboSpillShare;
                _got[i] += units * share;
                _burn[i] = Mathf.Max(_burn[i], share);
                _thisStrike[i] = true;
            }

            void Around(Vector2 at, float r, float units)
            {
                foreach (int i in Bodies(at, r)) Deal(i, units);
            }

            // ---- BLAST ----

            void Blast(System.Random rng)
            {
                // Round the player - with a bow, a step short of the nearest body in its reach, where a
                // blade would stand (ElementalResource.BlastCentre).
                int aim = Bow ? Nearest(_me, R.BowBlastReach) : -1;
                var c = aim >= 0 ? Vector2.MoveTowards(_p[aim], _me, R.BowBlastShort) : _me;
                switch (Element)
                {
                    case ElementType.Fire:   // ERUPT: pools scattered round the centre (FireResource.Erupt)
                    {
                        float spread = E.Fire.EruptSpread * Area, radius = E.Fire.PoolRadius * Area;
                        float base0 = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float each = E.Fire.PoolHitUnitsPerSecond * E.Fire.PoolSeconds * ShapeAssume.GroundStay;
                        for (int k = 0; k < E.Fire.EruptPools; k++)
                        {
                            float angle = base0 + k / (float)E.Fire.EruptPools * Mathf.PI * 2f + Range(rng, -0.35f, 0.35f);
                            var pos = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spread * Range(rng, 0.35f, 1f);
                            foreach (int i in Bodies(pos, radius)) _got[i] += each;
                        }
                        break;
                    }
                    case ElementType.Water:   // BURST on everything caught, split by its falloff (WaterResource.Land)
                    {
                        var caught = new List<int>(Bodies(c, E.Water.Radius * Area));
                        if (caught.Count == 0) break;
                        float share = E.Water.BurstHitUnits / Mathf.Pow(caught.Count, Mathf.Clamp01(E.Water.BurstSplitFalloff));
                        foreach (int i in caught) _got[i] += share;
                        break;
                    }
                    case ElementType.Earth:   // QUAKE at full charge, then aftershocks (EarthResource.Erupt)
                    {
                        int after = Mathf.FloorToInt(E.Earth.ChargeSeconds / E.Earth.AftershockPerSeconds);
                        for (int k = 0; k <= after; k++)
                        {
                            float scale = k == 0 ? 1f : 0.55f - (k - 1) * 0.08f;
                            float radius = E.Earth.Radius * Area * Mathf.Max(0.4f, scale);
                            Around(c, radius, (E.Earth.ShockHitUnits + E.Earth.ShockHitUnitsPerCharge) * scale);
                        }
                        break;
                    }
                    default:   // SQUALL: vortices scattered round the centre (AirResource.ScatterVortices)
                    {
                        float spread = E.Air.VortexSpread * Area, radius = E.Air.VortexRadius * Area;
                        float base0 = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float each = (E.Air.VortexNearHitUnitsPerSecond + E.Air.VortexFarHitUnitsPerSecond) * 0.5f
                                   * E.Air.VortexSeconds * ShapeAssume.VortexStay;
                        for (int k = 0; k < E.Air.VortexCount; k++)
                        {
                            float angle = base0 + k / (float)E.Air.VortexCount * Mathf.PI * 2f + Range(rng, -0.35f, 0.35f);
                            var pos = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spread * Range(rng, 0.35f, 1f);
                            foreach (int i in Bodies(pos, radius)) _got[i] += each;
                        }
                        break;
                    }
                }
            }

            static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---- COMBO: the three strikes, as ReleaseArsenal.Combos plays them ----

            void Combo()
            {
                float k = R.ComboScale(Element);
                float[] units = { R.Strike1HitUnits * k, R.Strike2HitUnits * k, R.Strike3HitUnits * k };
                System.Array.Clear(_burn, 0, _burn.Length);
                for (int s = 0; s < 3; s++)
                {
                    System.Array.Clear(_thisStrike, 0, _thisStrike.Length);
                    _quarry = Nearest(_me, Reach + 1f);
                    Strike(Player.ReleaseArsenal.StrikeName(Element, Weapon, s), units[s]);
                    for (int i = 0; i < _p.Length; i++) if (_thisStrike[i]) _struck[i]++;
                }
                _quarry = -1;
                // Fire's mark: a burn, merged by max, refreshed by each strike that lands (Mark),
                // at the strike's share.
                if (Element == ElementType.Fire)
                    for (int i = 0; i < _p.Length; i++)
                        if (_struck[i] > 0)
                            _got[i] += _burn[i] * R.ComboBurnHitUnits * (R.ComboBurnSeconds + ShapeAssume.StrikeGap * (_struck[i] - 1));
            }

            void Strike(string name, float u)
            {
                float a = Area;
                switch (name)
                {
                    case "Flare Jab":
                        for (int k = 0; k < 2; k++)
                        {
                            int t = Nearest(_me, Bow ? Reach : 2.4f);
                            if (t < 0) continue;
                            if (!Bow && Vector2.Distance(_me, _p[t]) > 1.6f) _me += (_p[t] - _me).normalized * 0.5f;
                            Around(_p[t], 0.8f, u * 0.5f);
                        }
                        break;
                    case "Flame Fan":
                        if (Bow)   // three arrows 20 degrees apart, each the first body on its line
                            for (int k = -1; k <= 1; k++)
                            {
                                var d = (Vector2)(Quaternion.Euler(0, 0, k * 20f) * Toward(Reach));
                                int first = -1;
                                float fd = float.MaxValue;
                                foreach (int i in InLine(_me, _me + d * R.BowStrikeReach * a, 0.4f))
                                    if ((_p[i] - _me).sqrMagnitude < fd) { fd = (_p[i] - _me).sqrMagnitude; first = i; }
                                if (first >= 0) Deal(first, u);
                            }
                        else foreach (int i in InCone(_me, Toward(Reach), 3f * a, 40f)) Deal(i, u);
                        break;
                    case "Backdraft":
                    {
                        // Dragged 1.2 toward the point, then the pop.
                        var at = Crowd(Reach);
                        foreach (int i in Bodies(at, 2.4f * a))
                            if (Mathf.Max(0f, (_p[i] - at).magnitude - 1.2f) <= 1.4f * a + Br) Deal(i, u);
                        break;
                    }
                    case "Ember Spiral": Around(_me, 2.2f * a, u); break;
                    case "Detonate":
                    {
                        var hit = new HashSet<int>();
                        int t = Nearest(_me, Reach);
                        for (int k = 0; k < 5 && t >= 0; k++)
                        {
                            hit.Add(t);
                            Around(_p[t], 0.9f, u);
                            t = Nearest(_p[t], 2.8f, hit);
                        }
                        break;
                    }
                    case "Pyre Drop":
                    {
                        var at = Crowd(Reach);
                        if (!Bow)
                        {
                            var d = at - _me;
                            float go = Mathf.Max(0f, Mathf.Min(3.2f, d.magnitude - 0.4f));
                            if (go > 0.1f) _me += d.normalized * go;
                            at = _me;
                        }
                        Around(at, 1.5f * a, u);
                        break;
                    }
                    case "Spray":
                        foreach (int i in InCone(_me, Toward(Reach), 3f * a, 45f)) Deal(i, u);
                        break;
                    case "Eddy": Around(Crowd(Reach), 2.2f * a, u); break;
                    case "Breaker":
                    {
                        var at = Crowd(Reach);
                        var dir = (at - _me).sqrMagnitude > 0.01f ? (at - _me).normalized : Vector2.right;
                        var start = Bow ? at - dir * 1.4f : _me;
                        float len = Vector2.Distance(start, at) + 1.4f;
                        foreach (int i in InLine(start, start + dir * len, 1.3f * a)) Deal(i, u);
                        break;
                    }
                    case "Draw In":
                    {
                        int t = Nearest(_me, Reach + 1f);
                        if (t >= 0) Deal(t, u);
                        break;
                    }
                    case "Rolling Wave":
                    {
                        var dir = Toward(Reach);
                        foreach (int i in InLine(_me, _me + dir * (Bow ? R.BowStrikeReach : 3.8f) * a, 1f * a)) Deal(i, u);
                        break;
                    }
                    case "Geyser Line":
                    {
                        var dir = Toward(Reach);
                        var side = new Vector2(-dir.y, dir.x);
                        for (int k = 1; k <= 4; k++)
                            Around(_me + dir * (k * 1.05f * a) + side * Mathf.Sin(k * 1.3f) * 0.7f, 0.85f * a, u * 0.55f);
                        break;
                    }
                    case "Lash":
                        foreach (int i in InCone(_me, Toward(Reach), (Bow ? R.BowStrikeReach : 3.4f) * a, 70f)) Deal(i, u);
                        break;
                    case "Tidal Wall":
                    {
                        var dir = Toward(Reach + 1f);
                        var from = _me + dir * 0.6f;
                        foreach (int i in InLine(from, from + dir * 5f * a, 1.1f * a)) Deal(i, u);
                        break;
                    }
                    case "Spike Line":
                    {
                        var dir = Toward(Reach);
                        float len = 3.6f * a;
                        Vector2 from = _me, along = dir;
                        if (Bow)
                        {
                            int t = Nearest(_me, Reach);
                            var end = t >= 0 ? _p[t] + dir * 1.2f : _me + dir * len;
                            from = end; along = -dir; len = Mathf.Min(len, Vector2.Distance(_me, end) - 1f);
                        }
                        if (len > 0.3f) foreach (int i in InLine(from, from + along * len, 0.6f * a)) Deal(i, u);
                        break;
                    }
                    case "Tremor":   // a bow's ring goes out from its arrow, a step short of the nearest
                    {
                        int t = Bow ? Nearest(_me, Reach) : -1;
                        Around(t >= 0 ? Vector2.MoveTowards(_p[t], _me, R.BowBlastShort) : _me, 2.4f * a, u);
                        break;
                    }
                    case "Monolith": Around(Crowd(Reach), 1.4f * a, u); break;
                    case "Stomp":
                    {
                        int t = Nearest(_me, Reach);
                        if (t >= 0) Around(_p[t], 0.9f * a, u);
                        break;
                    }
                    case "Pillar Shove":
                        foreach (int i in InCone(_me, Toward(Reach), ((Bow ? 2.2f : 1.3f) + 1.4f) * a, 55f)) Deal(i, u);
                        break;
                    case "Fault Rupture":
                        foreach (int i in InLine(_me, _me + Toward(Reach) * 4.4f * a, 0.7f * a)) Deal(i, u);
                        break;
                    case "Anvil Drop": Around(Crowd(Reach), 1.6f * a, u); break;
                    case "Feather Cut":
                    {
                        int t = Nearest(_me, 3.4f);
                        var dir = t >= 0 ? (_p[t] - _me).normalized : Vector2.right;
                        if (Bow) { if (t >= 0) Around(_p[t], 0.8f, u); break; }
                        float go = t >= 0 ? Mathf.Clamp(Vector2.Distance(_me, _p[t]) - 0.8f, 0f, 2.2f) : 1f;
                        foreach (int i in InCone(_me + dir * go, dir, 1.6f, 70f)) Deal(i, u);
                        break;
                    }
                    case "Crescent":
                    {
                        var dir = Toward(Reach);
                        _me += (Bow ? -dir : dir) * 0.5f;
                        foreach (int i in InLine(_me, _me + dir * (Bow ? R.BowStrikeReach : 4f) * a, 0.8f)) Deal(i, u);
                        break;
                    }
                    case "Whirl Step":
                    {
                        var dir = Toward(Reach);
                        var side = new Vector2(-dir.y, dir.x);
                        var hit = new HashSet<int>();
                        var start = _me;
                        for (int k = 0; k < 3; k++)
                        {
                            var at = start + side * 1.8f * Mathf.Clamp01(k * 0.1f / 0.28f);
                            if (Bow)
                            {
                                _me = at;
                                int t = Nearest(_me, Reach);
                                if (t >= 0) Deal(t, u / 3f);
                            }
                            else foreach (int i in Bodies(at, 1.4f * a)) if (hit.Add(i)) Deal(i, u);
                        }
                        _me = start + side * 1.8f;
                        break;
                    }
                    case "Gale Barrage":
                        for (int k = 0; k < 6; k++)
                        {
                            int t = Nearest(_me, Reach);
                            if (t >= 0) Deal(t, u / 4f);
                        }
                        break;
                    case "Cyclone":
                    {
                        // The cyclone's middle travels the line; anything within its radius of the path.
                        var dir = Toward(Reach + 1f);
                        for (int i = 0; i < _p.Length; i++)
                            if (DistToSegment(_p[i], _me, _me + dir * 4.6f * a) <= 1.3f * a + Br) Deal(i, u);
                        break;
                    }
                    default:   // Downburst
                        Around(Crowd(Reach), 1.8f * a, u);
                        break;
                }
            }

            // ---- GUARD, MOBILITY, TREMOR STANCE ----

            void Guard(float catches)
            {
                switch (Element)
                {
                    case ElementType.Fire:
                        foreach (int i in Bodies(_me, R.GuardRadius * Area))
                            _got[i] += catches * (R.EmberGuardHitUnits + R.EmberGuardBurnHitUnits * R.EmberGuardBurnSeconds);
                        break;
                    case ElementType.Earth:
                        foreach (int i in Bodies(_me, R.StoneThornsRadius * Area + 0.3f))
                            _got[i] += catches * R.StoneThornsHitUnits;
                        break;
                }
            }

            void Move(float catches)
            {
                if (Element != ElementType.Fire) return;
                // Comet: a blade leaps onto the target and bursts there; a bow leaps away, the burst
                // where it stood.
                var at = Bow ? _me : _p[0];
                foreach (int i in Bodies(at, R.CometRadius * Area))
                    _got[i] += catches * (R.CometHitUnits + R.CometBurnHitUnits * R.CometBurnSeconds);
            }

            void Crack()
            {
                var dir = Toward(Reach);
                // A bow's crack starts where its arrow lands, a step short of the nearest (EarthResource.CrackAfter).
                int t = Bow ? Nearest(_me, Reach) : -1;
                var from = t >= 0 ? Vector2.MoveTowards(_p[t], _me, R.BowBlastShort) : _me;
                foreach (int i in InLine(from, from + dir * R.TremorCrackLength * Area, R.TremorCrackHalfWidth))
                    _got[i] += R.TremorCrackHitUnits;
            }
        }
    }
}
