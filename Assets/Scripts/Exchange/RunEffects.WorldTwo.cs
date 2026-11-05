using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T2 = Convergence.Core.Tuning.Overhaul;

namespace Convergence.Exchange
{
    /// <summary>
    /// THE WORLD COSTS, SECOND BATCH (2026-11-04, the user's "finish what was left out"): Plated,
    /// Reinforced, Volley and Twisted act through the enemies (LedgerWorld, GameBootstrap); here are
    /// the room's - Encroach, Hazard Bloom, Narrowing, Crumble, Dim, Tide, Spite Spires, Dead Ground -
    /// and every one's Albedo. Pits and water the ledger lays sit under the floor's hazard root, so
    /// the floor's teardown takes them with it.
    /// </summary>
    public partial class RunEffects
    {
        float _encroachTimer, _crumbleTimer, _narrowTick;
        bool _touchedPit;
        readonly List<Hazards.Column> _columns = new();
        readonly HashSet<EnemyArmor> _platedExposed = new();
        readonly Dictionary<Art.Lighting.Glow, float> _lightBase = new();

        // ---------------------------------------------------------------- what the pits ask

        /// <summary>How hard pits bite the player (Hazard Bloom, Dead Ground) - Hazards.FloorPits asks.</summary>
        public float PitDamageMul
            => (1f + T2.BloomTaken * N("co_bloom")) * (Has("co_dead_ground") ? T2.DeadGroundMul : 1f);

        /// <summary>Spite Spires: a spire's boon was just taken.</summary>
        public void OnSpireTaken()
        {
            int n = N("co_spite");
            if (n <= 0 || Hp == null) return;
            Pay(Hp.Max * T2.SpiteHealth * n);
            if (AtMax("co_spite")) _mods.Pending.Forced = true;
        }

        // ---------------------------------------------------------------- the floor

        void WorldTwoFloorStarted()
        {
            _encroachTimer = T2.EncroachEvery;
            _crumbleTimer = T2.CrumbleEvery;
            _touchedPit = false;
            _platedExposed.Clear();
            _columns.Clear();
            foreach (var c in FindObjectsByType<Hazards.Column>()) if (c != null && !c.Broken) _columns.Add(c);
            if (Has("co_tide")) LayTide();
        }

        /// <summary>DEAD GROUND (an oath): a floor cleared without touching a pit keeps it.</summary>
        void WorldTwoFloorCleared()
        {
            if (Has("co_dead_ground") && !_touchedPit) _mods.Fulfil("co_dead_ground");
        }

        void LayTide()
        {
            var root = GameBootstrap.FloorRoot;
            var player = _pc != null ? _pc.transform : null;
            for (int i = 0; i < T2.TidePools; i++)
            {
                Vector2 at = new Vector2((i == 0 ? -1f : 1f) * Arena.Inner.x * 0.35f, 0f);
                if (!FreeGround(at, T2.TideSize)) continue;
                var rect = new Rect(at - Vector2.one * T2.TideSize * 0.5f, Vector2.one * T2.TideSize);
                Hazards.WaterPit.Spawn(rect, player, root);
            }
            Enemies.NavField.MarkDirty();
        }

        /// <summary>Open floor, clear of pits, columns and the player, for a pit the ledger lays.</summary>
        bool FreeGround(Vector2 at, float size)
        {
            if (!Arena.OnFloor(at, size * 0.6f)) return false;
            float h = size * 0.5f + 0.2f;
            foreach (var p in new[] { at, at + new Vector2(h, h), at + new Vector2(-h, h), at + new Vector2(h, -h), at + new Vector2(-h, -h) })
                if (Hazards.FloorPits.At(p) != null) return false;
            foreach (var c in _columns) if (c != null && !c.Broken && Vector2.Distance(c.transform.position, at) < c.Radius + h + 0.3f) return false;
            return true;
        }

        // ---------------------------------------------------------------- the tick

        void TickWorldTwo(float dt)
        {
            if (_pc == null || _mods == null) return;
            Vector2 me = _pc.transform.position;
            bool inPit = Hazards.FloorPits.At(me) != null;
            if (inPit) _touchedPit = true;
            // PIT DANCER (Hazard Bloom's Albedo): a pit is a place to fight from.
            if (inPit && Has("al_bloom")) _trapRemaining = Mathf.Max(_trapRemaining, 0.25f);

            // ENCROACH: a spike pit opens near you, warned first.
            if (Has("co_encroach"))
            {
                _encroachTimer -= dt * (AtMax("co_encroach") ? 2f : 1f);
                if (_encroachTimer <= 0f)
                {
                    _encroachTimer = T2.EncroachEvery;
                    StartCoroutine(OpenPit(me));
                }
            }

            // NARROWING: past its time, the band along the room's edges burns you.
            float after = AtMax("co_narrowing") ? T2.NarrowingAfter * 0.5f : T2.NarrowingAfter;
            if ((Has("co_narrowing") || Has("al_narrowing")) && _floorSeconds >= after)
            {
                _narrowTick -= dt;
                float band = T2.NarrowingBand + T2.NarrowingBandPerStack * Mathf.Max(0, N("co_narrowing") - 1);
                if (_narrowTick <= 0f)
                {
                    _narrowTick = 0.5f;
                    if (Has("co_narrowing") && NearEdge(me, band) && Hp != null)
                        Hp.Take(new DamageInfo(Hp.Max * T2.NarrowingHealth * 0.5f, ElementType.Fire, null) { Entry = "co_narrowing", Tick = true });
                    if (Has("al_narrowing"))
                        foreach (var hp in EnemiesWithin(me, 30f))
                            if (NearEdge(hp.transform.position, band)) StatusEffects.Get(hp.gameObject).ApplySlow(0.6f, T2.NarrowingSlow);
                    DrawEdge(band, Has("co_narrowing") ? TexelFx.Danger : CoatRamp);
                }
            }

            // CRUMBLE: a column falls; LANDSLIP (its Albedo): a column that breaks strikes.
            if (Has("co_crumble"))
            {
                _crumbleTimer -= dt * (AtMax("co_crumble") ? 2f : 1f);
                if (_crumbleTimer <= 0f)
                {
                    _crumbleTimer = T2.CrumbleEvery;
                    Hazards.Column pick = null; float best = float.MaxValue;
                    foreach (var c in _columns)
                    {
                        if (c == null || c.Broken) continue;
                        float d = Vector2.Distance(c.transform.position, me);
                        if (d < best) { best = d; pick = c; }
                    }
                    if (pick != null) pick.Break();
                }
            }
            for (int i = _columns.Count - 1; i >= 0; i--)
            {
                var c = _columns[i];
                if (c != null && !c.Broken) continue;
                Vector2 at = c != null ? (Vector2)c.transform.position : _lastColumnAt.TryGetValue(i, out var p) ? p : me;
                _columns.RemoveAt(i);
                if (Has("al_crumble"))
                {
                    foreach (var hp in EnemiesWithin(at, T2.CrumbleRadius))
                        hp.Take(new DamageInfo(T2.CrumbleHitUnits * _pc.HitUnit * _mods.Potency("al_crumble"), ElementType.Earth, _pc.gameObject) { Entry = "al_crumble" });
                    LedgerFx.Stuff(at, TexelFx.Stone, T2.CrumbleRadius, true);
                }
            }
            _lastColumnAt.Clear();
            for (int i = 0; i < _columns.Count; i++) if (_columns[i] != null) _lastColumnAt[i] = _columns[i].transform.position;

            // DIM: your light is smaller.
            TickLight();
        }

        readonly Dictionary<int, Vector2> _lastColumnAt = new();

        IEnumerator OpenPit(Vector2 near)
        {
            Vector2 at = near;
            bool found = false;
            for (int tries = 0; tries < 12 && !found; tries++)
            {
                float a = (Time.time * 2.3f + tries * 1.7f) % (Mathf.PI * 2f);   // a hash of the clock, not Random
                float d = Mathf.Lerp(T2.EncroachNear, T2.EncroachFar, (tries % 4) / 3f);
                at = near + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                found = FreeGround(at, T2.EncroachSize);
            }
            if (!found) yield break;
            TexelFx.Ring(at, TexelFx.Danger, T2.EncroachSize * 0.5f, T2.EncroachSize * 0.7f, T2.EncroachWarning, 1.2f, true, SortingOrders.Telegraph);
            yield return new WaitForSeconds(T2.EncroachWarning);
            if (_pc == null || !FreeGround(at, T2.EncroachSize)) yield break;
            var rect = new Rect(at - Vector2.one * T2.EncroachSize * 0.5f, Vector2.one * T2.EncroachSize);
            Hazards.SpikePit.Spawn(rect, _pc.transform, GameBootstrap.FloorRoot);
            Enemies.NavField.MarkDirty();
        }

        static bool NearEdge(Vector2 p, float band)
        {
            var inner = Arena.Inner;
            return Mathf.Abs(p.x) > inner.x - band || Mathf.Abs(p.y) > inner.y - band;
        }

        void DrawEdge(float band, Color32[] ramp)
        {
            var inner = Arena.Inner;
            float x = inner.x - band, y = inner.y - band;
            LedgerFx.Crest(new Vector2(-x, -y), Vector2.up, 2f * y, ramp);
            LedgerFx.Crest(new Vector2(x, -y), Vector2.up, 2f * y, ramp);
            LedgerFx.Crest(new Vector2(-x, -y), Vector2.right, 2f * x, ramp);
            LedgerFx.Crest(new Vector2(-x, y), Vector2.right, 2f * x, ramp);
        }

        /// <summary>DIM: the room's lights shrink (a pit's glow - its warning - stays). Each light's own
        /// radius is remembered the first time it is seen, so the cost leaving gives it back.</summary>
        void TickLight()
        {
            int n = N("co_dim");
            if (n <= 0 && _lightBase.Count == 0) return;
            float mul = n <= 0 ? 1f : Mathf.Max(0.3f, 1f - T2.DimShare * n - (AtMax("co_dim") ? T2.DimShare : 0f));
            foreach (var g in Art.Lighting.Glow.All)
            {
                if (g == null || g.GetComponent<Hazards.FloorPit>() != null) continue;
                if (!_lightBase.TryGetValue(g, out var r)) { r = g.Radius; _lightBase[g] = r; }
                g.Radius = r * mul;
            }
            if (n <= 0) _lightBase.Clear();
            else if (_lightBase.Count > 128)
                foreach (var k in new List<Art.Lighting.Glow>(_lightBase.Keys)) if (k == null) _lightBase.Remove(k);
        }

        /// <summary>LANTERN: the target stands inside one of the room's lights.</summary>
        static bool InLight(Vector2 p)
        {
            foreach (var g in Art.Lighting.Glow.All)
                if (g != null && ((Vector2)g.transform.position - p).sqrMagnitude <= g.Radius * g.Radius) return true;
            return false;
        }

        // ---------------------------------------------------------------- the Albedos on hits and deaths

        /// <summary>CRACKED PLATE (Plated's Albedo) and LANTERN (Dim's): what they add to one hit.</summary>
        float WorldTwoOutgoing(Health target, float damage)
        {
            if (_pc == null || target == null) return damage;
            if (Has("al_dim") && InLight(target.transform.position))
                damage = Boost("al_dim", damage, 1f + T2.LanternTaken * _mods.Potency("al_dim"));
            return damage;
        }

        void WorldTwoHit(Health target)
        {
            if (!Has("al_plated") || target == null || target.IsDead) return;
            var armour = target.GetComponent<EnemyArmor>();
            if (armour == null || armour.Max <= 0f || armour.Current > 0f || !_platedExposed.Add(armour)) return;
            StatusEffects.Get(target.gameObject).ApplyExposed(T2.PlatedExposedSeconds, 1f + T2.PlatedExposed * _mods.Potency("al_plated"));
            LedgerFx.StatusBurst(target, TexelFx.Steel);
        }

        void WorldTwoKill(Health h)
        {
            var ec = h != null ? h.GetComponent<Enemies.EnemyController>() : null;
            if (ec == null || !ec.Elite || !Has("al_reinforced")) return;
            foreach (var hp in EnemiesWithin(h.transform.position, T2.ReinforcedRadius))
                Staggers(hp, T2.ReinforcedStagger * _mods.Potency("al_reinforced"));
            LedgerFx.Force(h.transform.position, LedgerFx.Lilac, T2.ReinforcedRadius);
        }
    }
}
