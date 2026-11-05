using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Art.Gear;
using R = Convergence.Core.Tuning.Releases;

namespace Convergence.Player
{
    /// <summary>
    /// THE COMBOS (the user's picks, 2026-11-05 - RELEASES.md): three strikes per element and weapon.
    /// The ELEMENT owns the attack and the body's motion; the WEAPON its personality - a greatsword's
    /// blade throws the element from the body, discs carry it out and back, a bow looses it from
    /// range and never steps in. Each strike is a third of the meter; the third is the payoff.
    /// </summary>
    public partial class ReleaseArsenal
    {
        static readonly string[,,] Names =
        {
            // Fire
            { { "Flare Jab", "Backdraft", "Detonate" }, { "Flame Fan", "Ember Spiral", "Pyre Drop" }, { "Flame Fan", "Backdraft", "Detonate" } },
            // Water
            { { "Spray", "Eddy", "Breaker" }, { "Draw In", "Rolling Wave", "Geyser Line" }, { "Lash", "Rolling Wave", "Tidal Wall" } },
            // Earth
            { { "Spike Line", "Tremor", "Monolith" }, { "Stomp", "Pillar Shove", "Fault Rupture" }, { "Stomp", "Tremor", "Anvil Drop" } },
            // Air
            { { "Feather Cut", "Whirl Step", "Gale Barrage" }, { "Crescent", "Whirl Step", "Cyclone" }, { "Crescent", "Whirl Step", "Downburst" } },
        };

        static int Row(ElementType e) => e switch { ElementType.Fire => 0, ElementType.Water => 1, ElementType.Earth => 2, _ => 3 };
        static int Col(WeaponClass w) => w switch { WeaponClass.Disc => 1, WeaponClass.Bow => 2, _ => 0 };

        /// <summary>A strike's name - the seal shows the next one while the chain is open.</summary>
        public static string StrikeName(ElementType e, WeaponClass w, int strike) => Names[Row(e), Col(w), Mathf.Clamp(strike, 0, 2)];

        float Units(int strike)
            => (strike switch { 0 => R.Strike1HitUnits, 1 => R.Strike2HitUnits, _ => R.Strike3HitUnits }) * R.ComboScale(Element);

        IEnumerator Strike(int i)
        {
            _striking = true;
            try
            {
                float u = Units(i);
                _current = i;
                _quarry = Nearest(Me, Reach + 1f);
                string name = StrikeName(Element, Weapon, i);
                // The swing first; its hit lands on the drawn strike. A strike that travels (a hop, a
                // glide, a leap) swings as it goes instead of before. The payoff swings heavier.
                float window = i == 2 ? R.MotionWindowHeavy : i == 0 ? R.MotionWindowQuick : R.MotionWindow;
                if (name is "Feather Cut" or "Whirl Step" or "Pyre Drop")
                    Pc?.PlayReleaseMotion(StrikeMotion(name, Weapon), window, Toward(Reach));
                else yield return SwingStrike(Toward(Reach), window);
                switch (name)
                {
                    case "Flare Jab": yield return FlareJab(u); break;
                    case "Flame Fan": yield return FlameFan(u); break;
                    case "Backdraft": yield return Backdraft(u); break;
                    case "Ember Spiral": yield return EmberSpiral(u); break;
                    case "Detonate": yield return Detonate(u); break;
                    case "Pyre Drop": yield return PyreDrop(u); break;
                    case "Spray": yield return Spray(u); break;
                    case "Eddy": yield return Eddy(u); break;
                    case "Breaker": yield return Breaker(u); break;
                    case "Draw In": yield return DrawIn(u); break;
                    case "Rolling Wave": yield return RollingWave(u); break;
                    case "Geyser Line": yield return GeyserLine(u); break;
                    case "Lash": yield return Lash(u); break;
                    case "Tidal Wall": yield return TidalWall(u); break;
                    case "Spike Line": yield return SpikeLine(u); break;
                    case "Tremor": yield return Tremor(u); break;
                    case "Monolith": yield return Monolith(u); break;
                    case "Stomp": yield return Stomp(u); break;
                    case "Pillar Shove": yield return PillarShove(u); break;
                    case "Fault Rupture": yield return FaultRupture(u); break;
                    case "Anvil Drop": yield return AnvilDrop(u); break;
                    case "Feather Cut": yield return FeatherCut(u); break;
                    case "Crescent": yield return Crescent(u); break;
                    case "Whirl Step": yield return WhirlStep(u); break;
                    case "Gale Barrage": yield return GaleBarrage(u); break;
                    case "Cyclone": yield return Cyclone(u); break;
                    default: yield return Downburst(u); break;
                }
            }
            finally
            {
                _striking = false;
                _quarry = null;
                // The third ends the chain; the first two hold it open.
                _chainUntil = i < 2 ? Time.time + R.ComboChainSeconds : 0f;
                if (i >= 2) _strike = 0;
            }
        }

        float Reach => R.ComboReach;

        /// <summary>The body a strike is for (Strike) - it takes the strike whole, the rest a share.</summary>
        Health _quarry;

        /// <summary>A strike's weight on <paramref name="h"/>: whole on its quarry, a share on the rest.</summary>
        float StrikeShare(Health h) => !_striking || _quarry == null || h == _quarry ? 1f : R.ComboSpillShare;

        void HitAround(Vector2 at, float radius, float units, float knock = 0f)
        {
            foreach (var h in Bodies(at, radius)) Deal(h, units, (Vector2)h.transform.position - at, knock);
        }

        // ================================================================ FIRE

        /// <summary>FLARE JAB: two quick jabs (disc punches, snap shots), each popping a puff of flame.</summary>
        IEnumerator FlareJab(float u)
        {
            for (int k = 0; k < 2; k++)
            {
                var t = Nearest(Me, Bow ? Reach : 2.4f);
                var dir = t != null ? ((Vector2)t.transform.position - Me).normalized : Asked();
                Vector2 at = t != null ? (Vector2)t.transform.position : Me + dir * 1.3f;
                if (!Bow && t != null && Vector2.Distance(Me, at) > 1.6f) Step(dir, 0.5f, 0.08f);
                yield return Deliver(at);
                foreach (var h in Bodies(at, 0.8f)) Deal(h, u * 0.5f, dir, 1.2f);
                TexelFx.Puff(Drawn(at), TexelFx.Fire, 10, 8f, 0.35f, TexelFx.Air);
                TexelFx.Star(at, TexelFx.Fire, 4, 0.12f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        /// <summary>FLAME FAN: the swing sheds a wide fan of flame; a disc sprays it; a bow looses three
        /// burning arrows 20 degrees apart, each striking the first body on its line.</summary>
        IEnumerator FlameFan(float u)
        {
            var dir = Toward(Reach);
            float len = (Bow ? R.BowStrikeReach : 3f) * Area;
            if (Bow)
                for (int k = -1; k <= 1; k++)
                    TexelFx.Mote(Me + Vector2.up * 0.35f, Me + (Vector2)(Quaternion.Euler(0, 0, k * 20f) * dir) * len, TexelFx.Fire, 0.16f, null, TexelFx.Air);
            else if (Weapon == WeaponClass.Disc) TexelFx.Mote(Me + Vector2.up * 0.35f, Me + dir * 1.2f, TexelFx.Fire, 0.1f, null, TexelFx.Air);
            yield return new WaitForSeconds(Bow ? 0.12f : 0.06f);
            if (Bow)
            {
                // Arrows, not a fan: a bow's combo is for one enemy, and a fan past its standoff swept the crowd.
                for (int k = -1; k <= 1; k++)
                {
                    var d = (Vector2)(Quaternion.Euler(0, 0, k * 20f) * dir);
                    Health first = null;
                    float fd = float.MaxValue;
                    foreach (var h in InLine(Me, Me + d * len, 0.4f))
                    {
                        float dd = ((Vector2)h.transform.position - Me).sqrMagnitude;
                        if (dd < fd) { fd = dd; first = h; }
                    }
                    if (first == null) continue;
                    Deal(first, u, d, 1.5f);
                    TexelFx.Spray(ReleaseCanvas.Feet(first.transform), TexelFx.Fire, 0.5f, 8, 0.3f);
                }
                yield break;
            }
            ArsenalFx.Fan(Feet, TexelFx.Fire, dir, len, 40f * Mathf.Deg2Rad);
            foreach (var h in InCone(Me, dir, len, 40f)) Deal(h, u, dir, 1.5f);
        }

        /// <summary>BACKDRAFT: fire sucked into one point in the crowd, dragging them with it, then it pops.</summary>
        IEnumerator Backdraft(float u)
        {
            var at = Crowd(Reach);
            yield return Deliver(at);
            float r = 2.4f * Area;
            foreach (var h in Bodies(at, r)) DragToward.Pull(h, at, 1.2f);
            TexelFx.Ring(Drawn(at), TexelFx.Fire, r, 0.2f, 0.32f, 1.6f);
            yield return new WaitForSeconds(0.34f);
            HitAround(at, 1.4f * Area, u, 2.5f);
            TexelFx.Shock(Drawn(at), TexelFx.Fire, TexelFx.RubbleOf(), 1.4f * Area, 0.35f);
            TexelFx.Spray(Drawn(at), TexelFx.Fire, 1.2f, 18, 0.4f);
        }

        /// <summary>EMBER SPIRAL: a spin throws a spiral of fire outward (discs whip round; a bow's arrow
        /// into the floor at the feet).</summary>
        IEnumerator EmberSpiral(float u)
        {
            float r = 2.2f * Area;
            if (Bow) { TexelFx.Mote(Me + Vector2.up * 0.6f, Me, TexelFx.Fire, 0.1f, null, TexelFx.Air); yield return new WaitForSeconds(0.1f); }
            ArsenalFx.Spiral(Feet, TexelFx.Fire, r);
            yield return new WaitForSeconds(0.12f);
            HitAround(Me, r, u, 1.8f);
        }

        /// <summary>DETONATE: a blast leaps from body to body, each bursting in turn (a disc carries it;
        /// an arrow sets off the first).</summary>
        IEnumerator Detonate(float u)
        {
            var hit = new HashSet<Health>();
            var t = Nearest(Me, Reach);
            if (t == null)
            {
                var at = Me + Asked() * 2f;
                TexelFx.Shock(Drawn(at), TexelFx.Fire, TexelFx.RubbleOf(), 0.9f, 0.3f);
                yield break;
            }
            yield return Deliver(t.transform.position);
            Vector2 last = t.transform.position;
            for (int k = 0; k < 5 && t != null; k++)
            {
                hit.Add(t);
                Vector2 at = t.transform.position;
                if (k > 0) TexelFx.Mote(last, at, TexelFx.Fire, 0.1f, null, TexelFx.Air);
                foreach (var h in Bodies(at, 0.9f)) Deal(h, u, h.transform.position - (Vector3)last, 2f);
                TexelFx.Shock(Drawn(at), TexelFx.Fire, TexelFx.RubbleOf(), 0.9f, 0.3f);
                last = at;
                yield return new WaitForSeconds(0.12f);
                t = Nearest(at, 2.8f, hit);
            }
        }

        /// <summary>PYRE DROP: a leap into them and a pillar of fire where you land; a bow hops in place
        /// and lobs the arrow that raises it.</summary>
        IEnumerator PyreDrop(float u)
        {
            var at = Crowd(Reach);
            int order = TexelFx.Air;
            if (Bow)
            {
                TexelFx.Mote(Me + Vector2.up * 0.4f, at, TexelFx.Fire, 0.3f, null, TexelFx.Air);
                yield return new WaitForSeconds(0.3f);
            }
            else
            {
                var d = at - Me;
                float go = Mathf.Max(0f, Mathf.Min(3.2f, d.magnitude - 0.4f));
                if (go > 0.1f) { Step(d.normalized, go, 0.26f); yield return new WaitForSeconds(0.26f); }
                at = Me;
                order = ReleaseCanvas.UnderBody(transform);
            }
            float r = 1.5f * Area;
            HitAround(at, r, u, 3f);
            ArsenalFx.Pillar(Drawn(at), TexelFx.Fire, 2.4f, 0.45f, order);
            TexelFx.FlameRing(Drawn(at), TexelFx.Fire, r);
            FightMarks.Scorch(at, r * 0.6f);
        }

        // ================================================================ WATER

        /// <summary>SPRAY: a flowing sweep (the discs skim out; a loose bursts) flinging a wide spray.</summary>
        IEnumerator Spray(float u)
        {
            var dir = Toward(Reach);
            float len = 3f * Area;
            if (!Bow) yield return Deliver(Me + dir * 1.2f);
            else { TexelFx.Mote(Me + Vector2.up * 0.35f, Me + dir * 1.6f, Tuning.ReleaseFx.Water, 0.1f, null, TexelFx.Air); yield return new WaitForSeconds(0.1f); }
            ArsenalFx.Fan(Feet, Tuning.ReleaseFx.Water, dir, len, 45f * Mathf.Deg2Rad, 0.45f);
            TexelFx.Spray(Feet + dir * len * 0.5f, Tuning.ReleaseFx.Water, len * 0.5f, 20, 0.45f);
            foreach (var h in InCone(Me, dir, len, 45f)) Deal(h, u, dir, 1.2f);
        }

        /// <summary>EDDY: a swirl in the middle of them drags them round (both discs circling; an arrow
        /// into the middle).</summary>
        IEnumerator Eddy(float u)
        {
            var at = Crowd(Reach);
            yield return Deliver(at);
            float r = 2.2f * Area;
            var bodies = Bodies(at, r);
            var drawn = new List<Vector2>();
            foreach (var h in bodies) { DragToward.Pull(h, at, 0.9f); drawn.Add(ReleaseCanvas.Feet(h.transform)); }
            WaterWhirl.Play(Drawn(at), r, drawn);
            yield return new WaitForSeconds(0.25f);
            foreach (var h in bodies) Deal(h, u);
        }

        /// <summary>BREAKER: a great wave raised and crashing down on them.</summary>
        IEnumerator Breaker(float u)
        {
            var at = Crowd(Reach);
            if (Bow) { TexelFx.Mote(Me + Vector2.up * 0.4f, at, Tuning.ReleaseFx.Water, 0.25f, null, TexelFx.Air); yield return new WaitForSeconds(0.25f); }
            var dir = (at - Me).sqrMagnitude > 0.01f ? (at - Me).normalized : Asked();
            var start = Bow ? at - dir * 1.4f : Me;
            float len = Vector2.Distance(start, at) + 1.4f;
            ArsenalFx.Line(ArsenalFx.Band.Wave, Drawn(start), dir, len, 1.3f * Area, Tuning.ReleaseFx.Water, 0.3f, 0.65f);
            yield return new WaitForSeconds(0.28f);
            foreach (var h in InLine(start, start + dir * len, 1.3f * Area))
            {
                Deal(h, u, dir, 4f);
                TexelFx.Splash(ReleaseCanvas.Feet(h.transform), Tuning.ReleaseFx.Water, 1.2f);
            }
        }

        /// <summary>DRAW IN: a current (a disc on a cord, an arrow on a line) hooks the nearest and hauls
        /// it to your feet.</summary>
        IEnumerator DrawIn(float u)
        {
            var t = Nearest(Me, Reach + 1f);
            var to = t != null ? (Vector2)t.transform.position : Me + Asked() * 3f;
            var dir = (to - Me).sqrMagnitude > 0.01f ? (to - Me).normalized : Asked();
            ArsenalFx.Line(ArsenalFx.Band.Cord, Feet, dir, Vector2.Distance(Me, to), 0.1f, Tuning.ReleaseFx.Water, 0.12f, 0.45f);
            yield return Deliver(to);
            if (t == null) yield break;
            DragToward.Pull(t, Me, Mathf.Max(0f, Vector2.Distance(Me, to) - (Bow ? 2.5f : 0.9f)));
            Deal(t, u);
            TexelFx.Splash(ReleaseCanvas.Feet(t.transform), Tuning.ReleaseFx.Water, 1f);
        }

        /// <summary>ROLLING WAVE: a wave sent rolling forward that shoves them back.</summary>
        IEnumerator RollingWave(float u)
        {
            var dir = Toward(Reach);
            float len = (Bow ? R.BowStrikeReach : 3.8f) * Area, half = 1f * Area;
            ArsenalFx.Line(ArsenalFx.Band.Wave, Feet, dir, len, half, Tuning.ReleaseFx.Water, 0.4f, 0.75f);
            var hit = new HashSet<Health>();
            for (float t = 0f; t <= 0.4f; t += Time.deltaTime)
            {
                float reach = len * Mathf.Clamp01(t / 0.4f);
                foreach (var h in InLine(Me, Me + dir * reach, half))
                {
                    if (!hit.Add(h)) continue;
                    Deal(h, u);
                    DragToward.Push(h, (Vector2)h.transform.position - dir, 1.8f);
                }
                yield return null;
            }
        }

        /// <summary>GEYSER LINE: a curve cut (skimmed, skipped) toward them, geysers erupting along it
        /// one by one.</summary>
        IEnumerator GeyserLine(float u)
        {
            var dir = Toward(Reach);
            var side = new Vector2(-dir.y, dir.x);
            for (int k = 1; k <= 4; k++)
            {
                var at = Me + dir * (k * 1.05f * Area) + side * Mathf.Sin(k * 1.3f) * 0.7f;
                foreach (var h in Bodies(at, 0.85f * Area)) Deal(h, u * 0.55f, Vector2.up, 1f);
                ArsenalFx.Pillar(Drawn(at), Tuning.ReleaseFx.Water, 1.4f, 0.22f, TexelFx.Air, 0.45f);
                TexelFx.Splash(Drawn(at), Tuning.ReleaseFx.Water, 0.8f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>LASH: a long whip of water (a disc on a cord, an arrow's ribbon) sweeping across them.</summary>
        IEnumerator Lash(float u)
        {
            var dir = Toward(Reach);
            float r = (Bow ? R.BowStrikeReach : 3.4f) * Area;
            TexelFx.Arc(Feet, Tuning.ReleaseFx.Water, dir, r, TexelFx.Ground, 1.2f);
            TexelFx.Arc(Feet, Tuning.ReleaseFx.Water, dir, r * 0.85f, TexelFx.Ground, 1.1f);
            yield return new WaitForSeconds(0.1f);
            foreach (var h in InCone(Me, dir, r, 70f)) Deal(h, u, dir, 1.5f);
        }

        /// <summary>TIDAL WALL: an arrow raises a wall of water along its line that rolls over them -
        /// Fire's column as a wave (the user's swap).</summary>
        IEnumerator TidalWall(float u)
        {
            var dir = Toward(Reach + 1f);
            float len = 5f * Area, half = 1.1f * Area;
            TexelFx.Mote(Me + Vector2.up * 0.35f, Me + dir * len, Tuning.ReleaseFx.Water, 0.18f, null, TexelFx.Air);
            ArsenalFx.Line(ArsenalFx.Band.Wave, Feet + dir * 0.6f, dir, len, half, Tuning.ReleaseFx.Water, 0.5f, 0.9f);
            var hit = new HashSet<Health>();
            for (float t = 0f; t <= 0.5f; t += Time.deltaTime)
            {
                var a = Me + dir * 0.6f;
                foreach (var h in InLine(a, a + dir * len * Mathf.Clamp01(t / 0.5f), half))
                {
                    if (!hit.Add(h)) continue;
                    Deal(h, u, dir, 2f);
                    DragToward.Push(h, (Vector2)h.transform.position - dir, 1.6f);
                    TexelFx.Splash(ReleaseCanvas.Feet(h.transform), Tuning.ReleaseFx.Water, 1.2f);
                }
                yield return null;
            }
        }

        // ================================================================ EARTH

        /// <summary>SPIKE LINE: the blade (both discs) driven down and a line of spikes bursting forward;
        /// a bow's arrow lands past them and the spikes burst back toward them from it.</summary>
        IEnumerator SpikeLine(float u)
        {
            var dir = Toward(Reach);
            float len = 3.6f * Area;
            Vector2 from = Me, along = dir;
            if (Bow)
            {
                var t = Nearest(Me, Reach);
                var end = t != null ? (Vector2)t.transform.position + dir * 1.2f : Me + dir * len;
                TexelFx.Mote(Me + Vector2.up * 0.35f, end, TexelFx.Stone, 0.16f, null, TexelFx.Air);
                yield return new WaitForSeconds(0.16f);
                from = end; along = -dir; len = Mathf.Min(len, Vector2.Distance(Me, end) - 1f);
            }
            if (len <= 0.3f) yield break;
            ArsenalFx.Spikes(Drawn(from), along, len, 7, TexelFx.Stone, 0.3f, TexelFx.Air);
            var hit = new HashSet<Health>();
            for (float t = 0f; t <= 0.3f; t += Time.deltaTime)
            {
                foreach (var h in InLine(from, from + along * len * Mathf.Clamp01(t / 0.3f), 0.6f * Area))
                    if (hit.Add(h)) Deal(h, u, Vector2.up, 0.5f);
                yield return null;
            }
        }

        /// <summary>TREMOR: a heavy stamp (discs slammed) sends a ring of shock through the floor; a bow's
        /// arrow drives into the floor among them and the ring goes out from there.</summary>
        IEnumerator Tremor(float u)
        {
            float r = 2.4f * Area;
            var at = Me;
            if (Bow)
            {
                var t = Nearest(Me, Reach);
                if (t != null) at = Vector2.MoveTowards(t.transform.position, Me, R.BowBlastShort);
                yield return DeliverAlways(at);
            }
            TexelFx.Shock(Drawn(at), TexelFx.Stone, TexelFx.RubbleOf(), r, 0.4f);
            TexelFx.Hop(Drawn(at), TexelFx.Stone, r * 0.7f, 12, TexelFx.Ground);
            yield return new WaitForSeconds(0.08f);
            HitAround(at, r, u, 1.2f);
        }

        /// <summary>MONOLITH: a slab of stone falls out of the air onto them.</summary>
        IEnumerator Monolith(float u)
        {
            var at = Crowd(Reach);
            if (Bow || Weapon == WeaponClass.Disc) { TexelFx.Mote(Me + Vector2.up * 0.4f, Me + Vector2.up * 3f, TexelFx.Stone, 0.15f, null, TexelFx.Air); yield return new WaitForSeconds(0.1f); }
            ArsenalFx.Slab(Drawn(at), TexelFx.Stone, 3f, 0.75f, TexelFx.Air);
            yield return new WaitForSeconds(0.2f);
            HitAround(at, 1.4f * Area, u, 2.5f);
            foreach (var h in Bodies(at, 1.4f * Area)) if (!h.IsDead) Pc.Stagger(h, R.ComboStagger);
            TexelFx.Rubble(Drawn(at), TexelFx.RubbleOf(), 0.4f, 10, 1.2f);
            FightMarks.Crack(at, 1f);
        }

        /// <summary>STOMP: rise and stamp (discs slammed, the bow planted); a crack runs out and spikes
        /// burst under the first.</summary>
        IEnumerator Stomp(float u)
        {
            var t = Nearest(Me, Reach);
            var to = t != null ? (Vector2)t.transform.position : Me + Asked() * 3f;
            var dir = (to - Me).sqrMagnitude > 0.01f ? (to - Me).normalized : Asked();
            float len = Vector2.Distance(Me, to);
            TexelFx.Puff(Feet, TexelFx.Stone, 8, 8f, 0.35f, TexelFx.Ground);
            ArsenalFx.Line(ArsenalFx.Band.Crack, Feet, dir, len, 0.5f, TexelFx.Stone, 0.2f, 0.8f);
            yield return new WaitForSeconds(0.2f);
            ArsenalFx.SpikeRing(Drawn(to), 0.5f, 6, TexelFx.Stone, TexelFx.Air);
            HitAround(to, 0.9f * Area, u, 0.5f);
        }

        /// <summary>PILLAR SHOVE: pillars punch up in front (where the discs strike, where the arrow
        /// lands) and shove them back.</summary>
        IEnumerator PillarShove(float u)
        {
            var dir = Toward(Reach);
            var side = new Vector2(-dir.y, dir.x);
            float at = Bow ? 2.2f : 1.3f;
            if (Bow) { TexelFx.Mote(Me + Vector2.up * 0.35f, Me + dir * at, TexelFx.Stone, 0.12f, null, TexelFx.Air); yield return new WaitForSeconds(0.12f); }
            for (int k = -1; k <= 1; k++)
                ArsenalFx.Pillar(Drawn(Me + dir * at + side * k * 0.7f), TexelFx.Stone, 1.1f, 0.22f, TexelFx.Air, 0.5f);
            yield return new WaitForSeconds(0.06f);
            foreach (var h in InCone(Me, dir, (at + 1.4f) * Area, 55f))
            {
                Deal(h, u);
                DragToward.Push(h, Me, 2f);
            }
        }

        /// <summary>FAULT RUPTURE: the floor split (scored by a disc or an arrow); a trench tears open
        /// with shards along it.</summary>
        IEnumerator FaultRupture(float u)
        {
            var dir = Toward(Reach);
            float len = 4.4f * Area, half = 0.7f * Area;
            ArsenalFx.Line(ArsenalFx.Band.Crack, Feet, dir, len, half * 1.8f, TexelFx.Stone, 0.35f, 1.4f);
            ArsenalFx.Spikes(Feet + dir * 0.4f, dir, len - 0.4f, 9, TexelFx.Stone, 0.35f, TexelFx.Air);
            var hit = new HashSet<Health>();
            for (float t = 0f; t <= 0.35f; t += Time.deltaTime)
            {
                foreach (var h in InLine(Me, Me + dir * len * Mathf.Clamp01(t / 0.35f), half))
                    if (hit.Add(h)) Deal(h, u, Vector2.up, 1f);
                yield return null;
            }
            FightMarks.Crack(Me + dir * len * 0.5f, len * 0.4f);
        }

        /// <summary>ANVIL DROP: rise tall and bring everything down (loose straight down at them); a
        /// crater opens under them.</summary>
        IEnumerator AnvilDrop(float u)
        {
            var at = Crowd(Reach);
            TexelFx.Mote(Me + Vector2.up * 0.4f, at, TexelFx.Stone, 0.22f, null, TexelFx.Air);
            yield return new WaitForSeconds(0.22f);
            float r = 1.6f * Area;
            HitAround(at, r, u, 1.5f);
            foreach (var h in Bodies(at, r)) if (!h.IsDead) Pc.Stagger(h, R.ComboStagger);
            TexelFx.Shock(Drawn(at), TexelFx.Stone, TexelFx.RubbleOf(), r, 0.45f);
            TexelFx.Rubble(Drawn(at), TexelFx.RubbleOf(), 0.3f, 12, 1.4f);
            FightMarks.Crack(at, r * 0.8f);
        }

        // ================================================================ AIR

        /// <summary>FEATHER CUT: hop in, cut, and hop back out; a bow hops back and looses as it lands.</summary>
        IEnumerator FeatherCut(float u)
        {
            var t = Nearest(Me, 3.4f);
            var dir = t != null ? ((Vector2)t.transform.position - Me).normalized : Asked();
            if (Bow)
            {
                Step(-dir, 1.4f, 0.14f);
                yield return new WaitForSeconds(0.14f);
                var to = t != null ? (Vector2)t.transform.position : Me + dir * 3f;
                yield return DeliverAlways(to);
                foreach (var h in Bodies(to, 0.8f)) Deal(h, u, dir, 1.4f);
                ArsenalFx.Crescent(Drawn(to) - dir * 0.6f, dir, 0.6f, 0.45f, Tuning.ReleaseFx.Gust, 0.08f);
                yield break;
            }
            float go = t != null ? Mathf.Clamp(Vector2.Distance(Me, t.transform.position) - 0.8f, 0f, 2.2f) : 1f;
            Step(dir, go, 0.1f);
            yield return new WaitForSeconds(0.1f);
            foreach (var h in InCone(Me, dir, 1.6f, 70f)) Deal(h, u, dir, 1.4f);
            TexelFx.Cut(Me + dir * 0.6f, Tuning.ReleaseFx.Gust, new Vector2(-dir.y, dir.x), 0.6f);
            yield return new WaitForSeconds(0.08f);
            Step(-dir, go, 0.12f);
            yield return new WaitForSeconds(0.12f);
        }

        IEnumerator DeliverAlways(Vector2 to)
        {
            float life = Mathf.Clamp(Vector2.Distance(Me, to) * 0.045f, 0.08f, 0.22f);
            TexelFx.Mote(Me + Vector2.up * 0.35f, to, Ramp, life, null, TexelFx.Air);
            yield return new WaitForSeconds(life);
        }

        /// <summary>CRESCENT: a light step (back, on a bow) and a crescent of wind thrown, a disc or an
        /// arrow riding inside it.</summary>
        IEnumerator Crescent(float u)
        {
            var dir = Toward(Reach);
            Step(Bow ? -dir : dir, 0.5f, 0.08f);
            yield return new WaitForSeconds(0.08f);
            float len = (Bow ? R.BowStrikeReach : 4f) * Area;
            ArsenalFx.Crescent(Feet, dir, len, 0.7f, Tuning.ReleaseFx.Gust, 0.3f);
            var hit = new HashSet<Health>();
            for (float t = 0f; t <= 0.3f; t += Time.deltaTime)
            {
                float reach = len * EaseOut(t / 0.3f);
                foreach (var h in InLine(Me, Me + dir * reach, 0.8f))
                    if (hit.Add(h)) { Deal(h, u); DragToward.Push(h, Me, 0.8f); }
                yield return null;
            }
        }

        static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x); }

        /// <summary>WHIRL STEP: glide sideways spinning, cutting all round; a bow glides loosing as it turns.</summary>
        IEnumerator WhirlStep(float u)
        {
            var dir = Toward(Reach);
            var side = new Vector2(-dir.y, dir.x);
            if (Controls.Move.sqrMagnitude > 0.01f && Vector2.Dot(Controls.Move, side) < 0f) side = -side;
            Step(side, 1.8f, 0.28f);
            var hit = new HashSet<Health>();
            for (int k = 0; k < 3; k++)
            {
                if (Bow)
                {
                    // An arrow a turn at the nearest - three of them, a third of the strike each.
                    var t = Nearest(Me, Reach);
                    if (t != null)
                    {
                        TexelFx.Mote(Me + Vector2.up * 0.35f, t.transform.position, Tuning.ReleaseFx.Gust, 0.1f, null, TexelFx.Air);
                        Deal(t, u / 3f, (Vector2)t.transform.position - Me, 1f);
                    }
                }
                else
                {
                    TexelFx.Swirl(Feet, Tuning.ReleaseFx.Gust, 0.9f, 0.2f, TexelFx.Ground);
                    foreach (var h in Bodies(Me, 1.4f * Area))
                        if (hit.Add(h)) Deal(h, u, (Vector2)h.transform.position - Me, 1.2f);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>GALE BARRAGE: a flurry of cuts (throws, arrows), each riding a small crescent.</summary>
        IEnumerator GaleBarrage(float u)
        {
            var hits = new List<Health>();
            for (int k = 0; k < 6; k++)
            {
                var t = Nearest(Me, Reach);
                var dir = t != null ? ((Vector2)t.transform.position - Me).normalized : Asked();
                ArsenalFx.Crescent(Feet, dir, t != null ? Vector2.Distance(Me, t.transform.position) : 2.5f, 0.35f, Tuning.ReleaseFx.Gust, 0.12f);
                if (t != null) Deal(t, u / 4f, dir, 0.6f);
                yield return new WaitForSeconds(0.08f);
            }
        }

        /// <summary>CYCLONE: a spinning cut (the discs, a spun arrow) looses a cyclone that travels into
        /// them, drawing them in and lifting them.</summary>
        IEnumerator Cyclone(float u)
        {
            var dir = Toward(Reach + 1f);
            float len = 4.6f * Area, travel = 0.9f;
            ArsenalFx.Cyclone(Feet, dir, len, Tuning.ReleaseFx.Gust, travel, TexelFx.Air);
            var hit = new HashSet<Health>();
            Vector2 from = Me;
            for (float t = 0f; t <= travel; t += Time.deltaTime)
            {
                var c = from + dir * len * (t / travel);
                foreach (var h in Bodies(c, 1.3f * Area))
                {
                    DragToward.Pull(h, c, 0.3f);
                    if (hit.Add(h)) { Deal(h, u); if (!h.IsDead) Pc.Stagger(h, R.ComboStagger); }
                }
                yield return null;
            }
        }

        /// <summary>DOWNBURST: the blade raised (discs thrown high, an arrow straight up); wind slams down
        /// on them and flattens them.</summary>
        IEnumerator Downburst(float u)
        {
            var at = Crowd(Reach);
            TexelFx.Mote(Me + Vector2.up * 0.4f, Me + Vector2.up * 3f, Tuning.ReleaseFx.Gust, 0.15f, null, TexelFx.Air);
            float r = 1.8f * Area;
            TexelFx.Ring(Drawn(at), Tuning.ReleaseFx.Gust, r * 1.6f, 0.2f, 0.22f, 1.6f, dotted: true);
            yield return new WaitForSeconds(0.24f);
            foreach (var h in Bodies(at, r))
            {
                Deal(h, u);
                if (!h.IsDead) { Pc.Stagger(h, R.ComboStagger); DragToward.Push(h, at, 1f); }
            }
            TexelFx.Dust(Drawn(at), Tuning.ReleaseFx.Gust, r);
            TexelFx.Ring(Drawn(at), Tuning.ReleaseFx.Gust, 0.2f, r, 0.3f, 1.4f);
        }
    }
}
