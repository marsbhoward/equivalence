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
    /// THE ARSENAL'S SHARED KINDS (RELEASES.md): Guard, Mobility and Combo for every element, on the
    /// element's own meter (<see cref="ElementalResource.Fill01"/>, spent through
    /// <see cref="ElementalResource.Spend"/>). Blast and Buff stay each element's own.
    ///
    /// GUARD - a full meter for <see cref="R.GuardCharges"/> charges; each use of the defensive ability
    /// spends one and throws the element round the body. It adds an effect; it never moves the parry
    /// window or the cooldown (gear-tuned thresholds).
    ///
    /// MOBILITY - half a meter for one move. No immunity, and nothing that looks like it: the body
    /// stays drawn and hittable all the way.
    ///
    /// COMBO - three strikes for a third each, the next within <see cref="R.ComboChainSeconds"/> of
    /// the last, or the chain starts over (ReleaseArsenal.Combos). Stopping after one or two is fine.
    ///
    /// Bows never close in: a move or a strike that steps in steps away on a bow.
    /// </summary>
    public partial class ReleaseArsenal : MonoBehaviour
    {
        PlayerController _pc;
        ElementalResource _r;

        PlayerController Pc
        {
            get
            {
                if (_pc == null) _pc = GetComponent<PlayerController>();
                return _pc;
            }
        }

        /// <summary>Guard charges left; a Guard is not fired again until they are spent.</summary>
        public int GuardCharges { get; private set; }

        bool _moving, _striking;
        /// <summary>The next combo strike (0..2) and until when it chains.</summary>
        int _strike;
        float _chainUntil;

        public bool CanFire(ElementalResource r, ReleaseKind kind)
        {
            if (Pc == null || Pc.Health == null || Pc.Health.IsDead) return false;
            float fill = r.Fill01 + 0.001f;
            return kind switch
            {
                ReleaseKind.Guard => GuardCharges == 0 && fill >= 1f,
                ReleaseKind.Mobility => !_moving && !Pc.Dashing && fill >= R.MobilityCost,
                ReleaseKind.Combo => !_striking && fill >= R.ComboStrikeCost,
                _ => false,
            };
        }

        public void Fire(ElementalResource r, ReleaseKind kind)
        {
            _r = r;
            switch (kind)
            {
                case ReleaseKind.Guard:
                    r.Spend(1f);
                    GuardCharges = R.GuardCharges;
                    TexelFx.Ring(Feet, Ramp, 0.2f, 0.75f, 0.35f, 1.6f);
                    TexelFx.Rise(Feet, Ramp, 1.4f, ReleaseCanvas.UnderBody(transform));
                    break;
                case ReleaseKind.Mobility:
                    r.Spend(R.MobilityCost);
                    StartCoroutine(Move());
                    break;
                case ReleaseKind.Combo:
                    r.Spend(R.ComboStrikeCost);
                    int i = Time.time <= _chainUntil ? _strike : 0;
                    _strike = (i + 1) % 3;
                    StartCoroutine(Strike(i));
                    break;
            }
        }

        /// <summary>A repeat of a Guard (Twin Spark, Wellspring): one more charge.</summary>
        public void RepeatGuard() => GuardCharges++;

        /// <summary>The seal's tag while the arsenal has something to say: the Guard's charges, or the
        /// combo's next strike and its time left to chain (first - it is short).</summary>
        public string Tag(ElementalResource r)
        {
            if (_strike > 0 && Time.time <= _chainUntil)
                return $"{StrikeName(r.Element, Weapon, _strike).ToUpperInvariant()}  {_chainUntil - Time.time:0.0}s";
            if (GuardCharges > 0) return $"{ReleaseInfo.Name(r.Element, ReleaseKind.Guard)}  {new string('|', GuardCharges)}";
            return null;
        }

        // ---- shared ----

        ElementType Element => _r != null ? _r.Element : ElementType.Fire;
        WeaponClass Weapon => Pc != null ? Pc.Weapon : WeaponClass.Greatsword;
        bool Bow => Weapon == WeaponClass.Bow;
        Vector2 Me => transform.position;
        Vector2 Feet => ReleaseCanvas.Feet(transform);
        /// <summary>From where a body stands to where its picture goes on the floor.</summary>
        Vector2 Drawn(Vector2 p) => p + (Feet - Me);
        float Area => Pc != null ? Pc.AreaScaleNow : 1f;
        float Unit => (Pc != null ? Pc.HitUnit : Tuning.Player.BaseDamage) * (_r != null ? _r.ReleaseScaleNow : 1f);

        public static Color32[] RampOf(ElementType e) => e switch
        {
            ElementType.Fire => TexelFx.Fire,
            ElementType.Water => Tuning.ReleaseFx.Water,
            ElementType.Earth => TexelFx.Stone,
            _ => Tuning.ReleaseFx.Gust,
        };
        Color32[] Ramp => RampOf(Element);

        /// <summary>Where the player is asking to go: the aim stick, else the move stick, else the facing.</summary>
        Vector2 Asked()
        {
            var aim = Controls.Aim;
            if (aim.HasValue && aim.Value.sqrMagnitude > 0.01f) return aim.Value.normalized;
            var move = Controls.Move;
            if (move.sqrMagnitude > 0.01f) return move.normalized;
            return Pc != null ? Pc.Facing : Vector2.right;
        }

        /// <summary>Every living body (not the player) within <paramref name="r"/> of <paramref name="c"/>, once each.</summary>
        List<Health> Bodies(Vector2 c, float r)
        {
            var list = new List<Health>();
            foreach (var col in Physics2D.OverlapCircleAll(c, r))
            {
                if (col == null || col.gameObject == gameObject) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead || list.Contains(hp) || hp.GetComponent<PlayerController>() != null) continue;
                list.Add(hp);
            }
            return list;
        }

        Health Nearest(Vector2 from, float reach, ICollection<Health> skip = null)
        {
            Health best = null;
            float bd = float.MaxValue;
            foreach (var h in Bodies(from, reach))
            {
                if (skip != null && skip.Contains(h)) continue;
                float d = ((Vector2)h.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        /// <summary>The middle of the nearest knot of enemies, else a point ahead of the player.</summary>
        Vector2 Crowd(float reach, float ahead = 2.4f)
        {
            var near = Nearest(Me, reach);
            if (near == null) return Me + Asked() * ahead;
            Vector2 sum = Vector2.zero;
            int n = 0;
            foreach (var h in Bodies(near.transform.position, 1.6f)) { sum += (Vector2)h.transform.position; n++; }
            return n > 0 ? sum / n : (Vector2)near.transform.position;
        }

        /// <summary>Toward the nearest enemy in reach, else where the player asks.</summary>
        Vector2 Toward(float reach)
        {
            var near = Nearest(Me, reach);
            if (near == null) return Asked();
            var d = (Vector2)near.transform.position - Me;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Asked();
        }

        static float DistToSegment(Vector2 x, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(x - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(x, a + ab * t);
        }

        List<Health> InLine(Vector2 a, Vector2 b, float half)
        {
            var list = new List<Health>();
            foreach (var h in Bodies((a + b) * 0.5f, Vector2.Distance(a, b) * 0.5f + half + 0.5f))
                if (DistToSegment(h.transform.position, a, b) <= half) list.Add(h);
            return list;
        }

        List<Health> InCone(Vector2 from, Vector2 dir, float length, float halfAngleDeg)
        {
            var list = new List<Health>();
            foreach (var h in Bodies(from, length))
            {
                var d = (Vector2)h.transform.position - from;
                if (d.sqrMagnitude < 0.36f || Vector2.Angle(dir, d) <= halfAngleDeg) list.Add(h);
            }
            return list;
        }

        /// <summary>A release hit, in hit units, through the player (PlayerController.ReleaseHit), and
        /// the element's mark on it: burn, soak, stagger - Air's is the shove.</summary>
        void Deal(Health h, float units, Vector2 knockDir = default, float knock = 0f, bool mark = true)
        {
            if (h == null || h.IsDead || Pc == null) return;
            float share = StrikeShare(h);
            var info = new DamageInfo(units * share * Unit, Element, gameObject);
            if (knock > 0f && knockDir.sqrMagnitude > 1e-4f) info.Knockback = knockDir.normalized * knock;
            Pc.ReleaseHit(h, info);
            TexelFx.Hit(h.transform.position, Element, knockDir.sqrMagnitude > 1e-4f ? knockDir : Vector2.up);
            if (mark && !h.IsDead) Mark(h, share);
        }

        void Mark(Health h, float strength = 1f)
        {
            switch (Element)
            {
                case ElementType.Fire: Pc.Burn(h, R.ComboBurnHitUnits * Unit * strength, R.ComboBurnSeconds); break;
                // Soak and stagger are not damage: whole on everything the strike catches.
                case ElementType.Water: Pc.Soak(h, R.ComboSoakSeconds, Tuning.Elements.Water.SoakVulnerability); break;
                case ElementType.Earth: Pc.Stagger(h, R.ComboStagger); break;
            }
        }

        /// <summary>A scripted step through the dash's own path (chasms, walls) - no immunity.</summary>
        void Step(Vector2 dir, float distance, float seconds)
        {
            if (Pc == null || distance <= 0.01f) return;
            Pc.Dash(dir, distance / seconds, seconds);
        }

        /// <summary>A disc or an arrow carrying the element to <paramref name="to"/>; a blade's strike
        /// needs none. Waits for it to arrive.</summary>
        IEnumerator Deliver(Vector2 to)
        {
            if (Weapon == WeaponClass.Greatsword) yield break;
            float life = Mathf.Clamp(Vector2.Distance(Me, to) * 0.045f, 0.08f, 0.22f);
            TexelFx.Mote(Me + Vector2.up * 0.35f, to, Ramp, life, null, TexelFx.Air);
            if (Weapon == WeaponClass.Disc) StartCoroutine(DiscAway(to, life));
            yield return new WaitForSeconds(life);
        }

        // ---- GUARD ----

        /// <summary>The defensive ability was used (PlayerController.ActivateDefensiveAbility): a charge
        /// spent, the element thrown round the body - after a dash, where it ends.</summary>
        public void OnDefence()
        {
            if (GuardCharges <= 0 || _r == null) return;
            GuardCharges--;
            if (Pc != null && Pc.Dashing) StartCoroutine(GuardAfter(Tuning.Defense.DashSeconds));
            else GuardBurst();
        }

        IEnumerator GuardAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            GuardBurst();
        }

        void GuardBurst()
        {
            if (Pc == null || Pc.Health == null || Pc.Health.IsDead) return;
            var feet = Feet;
            switch (Element)
            {
                case ElementType.Fire:   // EMBER GUARD: a burst of flame, attackers set burning
                {
                    float r = R.GuardRadius * Area;
                    foreach (var h in Bodies(Me, r))
                    {
                        Deal(h, R.EmberGuardHitUnits, (Vector2)h.transform.position - Me, 2f, mark: false);
                        if (!h.IsDead) Pc.Burn(h, R.EmberGuardBurnHitUnits * Unit, R.EmberGuardBurnSeconds);
                    }
                    TexelFx.FlameRing(feet, TexelFx.Fire, r);
                    TexelFx.Spray(feet, TexelFx.Fire, r * 0.8f, 16, 0.4f, falls: false, order: TexelFx.Ground);
                    break;
                }
                case ElementType.Water:   // MIST VEIL: a mist that soaks attackers, and a little heal
                {
                    float r = R.GuardRadius * Area;
                    foreach (var h in Bodies(Me, r)) Pc.Soak(h, R.MistVeilSoakSeconds, Tuning.Elements.Water.SoakVulnerability);
                    Pc.HealWithinLimit(Pc.Health.Max * R.MistVeilHeal);
                    TexelFx.Spray(feet, Tuning.ReleaseFx.Water, r, 26, 0.6f, falls: false, order: TexelFx.Ground);
                    TexelFx.FaintRing(feet, Tuning.ReleaseFx.Water, 0.3f, r, 0.6f);
                    TexelFx.Motes(feet, TexelFx.Heal, 1.2f, ReleaseCanvas.UnderBody(transform), 10);
                    break;
                }
                case ElementType.Earth:   // STONE THORNS: a ring of spikes round where you stand
                {
                    float r = R.StoneThornsRadius * Area;
                    foreach (var h in Bodies(Me, r + 0.3f))
                    {
                        Deal(h, R.StoneThornsHitUnits, (Vector2)h.transform.position - Me, 1.5f, mark: false);
                        if (!h.IsDead) Pc.Stagger(h, R.StoneThornsStagger);
                    }
                    ArsenalFx.SpikeRing(feet, r, 10, TexelFx.Stone, ReleaseCanvas.UnderBody(transform));
                    TexelFx.Puff(feet, TexelFx.Stone, 10, r * 20f, 0.45f, TexelFx.Ground);
                    break;
                }
                default:   // WINDBREAK: a gust that shoves every attacker back
                {
                    float r = R.WindbreakRadius * Area;
                    foreach (var h in Bodies(Me, r)) DragToward.Push(h, Me, R.WindbreakPush);
                    TexelFx.Ring(feet, Tuning.ReleaseFx.Gust, 0.3f, r, 0.35f, 1.4f, dotted: true);
                    TexelFx.Swirl(feet, Tuning.ReleaseFx.Gust, r * 0.6f, 0.4f, TexelFx.Ground);
                    break;
                }
            }
        }

        // ---- MOBILITY ----

        /// <summary>A bow never carries the archer at the enemy: a move pointed at the nearest one
        /// goes the other way.</summary>
        Vector2 MoveDir()
        {
            var dir = Asked();
            if (!Bow) return dir;
            var near = Nearest(Me, 8f);
            if (near == null) return dir;
            var to = ((Vector2)near.transform.position - Me).normalized;
            return Vector2.Dot(dir, to) > 0.3f ? -to : dir;
        }

        /// <summary>Which way the body faces on a move: the way it goes, except a retreat - a bow's hop
        /// back, Mudslide - which keeps its eyes on the nearest enemy.</summary>
        Vector2 MoveFace(Vector2 dir)
        {
            if (!Bow && Element != ElementType.Earth) return dir;
            var near = Nearest(Me, 8f);
            return near != null ? ((Vector2)near.transform.position - Me).normalized : dir;
        }

        IEnumerator Move()
        {
            _moving = true;
            try
            {
                // The move's motion plays as the body goes (Zephyr swings again on each blink).
                Pc?.PlayReleaseMotion(MoveMotion(Element, Weapon), R.MotionWindowHeavy, MoveFace(MoveDir()));
                switch (Element)
                {
                    case ElementType.Fire: yield return Comet(); break;
                    case ElementType.Water: yield return Wavecrest(); break;
                    case ElementType.Earth: yield return Mudslide(); break;
                    default: yield return Zephyr(); break;
                }
            }
            finally { _moving = false; }
        }

        /// <summary>COMET: a leap trailing fire; the landing bursts, knocking back and burning. On a bow
        /// it carries you away and the burst is where you left.</summary>
        IEnumerator Comet()
        {
            var dir = Bow ? MoveDir() : (Nearest(Me, 6f) != null && Controls.Aim == null && Controls.Move.sqrMagnitude < 0.01f ? Toward(6f) : MoveDir());
            var start = Me;
            Step(dir, R.CometDistance, R.CometSeconds);
            for (float t = 0f; t < R.CometSeconds; t += 0.04f)
            {
                TexelFx.Puff(Feet, TexelFx.Fire, 5, 4f, 0.35f, TexelFx.Ground);
                yield return new WaitForSeconds(0.04f);
            }
            var at = Bow ? start : Me;
            float r = R.CometRadius * Area;
            foreach (var h in Bodies(at, r))
            {
                Deal(h, R.CometHitUnits, (Vector2)h.transform.position - at, R.CometKnockback, mark: false);
                if (!h.IsDead) Pc.Burn(h, R.CometBurnHitUnits * Unit, R.CometBurnSeconds);
            }
            TexelFx.Shock(Drawn(at), TexelFx.Fire, TexelFx.RubbleOf(), r, 0.4f);
            TexelFx.FlameRing(Drawn(at), TexelFx.Fire, r * 0.8f);
            FightMarks.Scorch(at, r * 0.6f);
        }

        /// <summary>WAVECREST: ride a wave along a line, soaking and shoving aside what you pass; the
        /// line stays wet.</summary>
        IEnumerator Wavecrest()
        {
            var dir = MoveDir();
            Vector2 start = Me, side = new(-dir.y, dir.x);
            var hit = new HashSet<Health>();
            Step(dir, R.WavecrestDistance, R.WavecrestSeconds);
            ArsenalFx.Line(ArsenalFx.Band.Wave, Feet, dir, R.WavecrestDistance, R.WavecrestHalfWidth, Tuning.ReleaseFx.Water,
                           R.WavecrestSeconds, R.WavecrestSeconds + 0.3f);
            for (float t = 0f; t <= R.WavecrestSeconds; t += Time.deltaTime)
            {
                foreach (var h in Bodies(Me, R.WavecrestHalfWidth + 0.3f))
                {
                    if (!hit.Add(h)) continue;
                    Pc.Soak(h, R.WavecrestSoakSeconds, Tuning.Elements.Water.SoakVulnerability);
                    float s = Vector2.Dot((Vector2)h.transform.position - Me, side);
                    DragToward.Push(h, (Vector2)h.transform.position - side * (s >= 0f ? 1f : -1f), R.WavecrestPush);
                }
                yield return null;
            }
            Vector2 end = Me;
            ArsenalFx.Line(ArsenalFx.Band.Wet, Drawn(start), dir, Vector2.Distance(start, end), R.WavecrestHalfWidth,
                           Tuning.ReleaseFx.Water, 0f, R.WavecrestWetSeconds);
            var pc = Pc;
            ArsenalField.Line(start, end, R.WavecrestHalfWidth, R.WavecrestWetSeconds, 0.5f,
                h => { if (pc != null) pc.Soak(h, R.WavecrestSoakSeconds, Tuning.Elements.Water.SoakVulnerability); });
        }

        /// <summary>MUDSLIDE: slide away on a surge of mud; it sets behind you as a short wall that
        /// stops enemies and their shots.</summary>
        IEnumerator Mudslide()
        {
            // A retreat: with no stick held it slides away from the nearest enemy, whatever the weapon.
            var dir = MoveDir();
            var near = Nearest(Me, 8f);
            if (near != null && Controls.Aim == null && Controls.Move.sqrMagnitude < 0.01f)
                dir = (Me - (Vector2)near.transform.position).normalized;
            Step(dir, R.MudslideDistance, R.MudslideSeconds);
            for (float t = 0f; t < R.MudslideSeconds; t += 0.05f)
            {
                TexelFx.Flicks(Feet, ArsenalFx.Mud, -dir, TexelFx.Ground);
                TexelFx.Puff(Feet, ArsenalFx.Mud, 4, 4f, 0.4f, TexelFx.Ground);
                yield return new WaitForSeconds(0.05f);
            }
            var side = new Vector2(-dir.y, dir.x);
            MudWall.Raise(Me - dir * R.MudWallGap, side, R.MudWallHalfWidth, R.MudWallSeconds, Feet - dir * R.MudWallGap);
        }

        /// <summary>ZEPHYR: three short blinks, each toward wherever the stick points at that moment,
        /// a gust thrown off each landing.</summary>
        IEnumerator Zephyr()
        {
            for (int i = 0; i < R.ZephyrBlinks; i++)
            {
                var from = Feet;
                var dir = MoveDir();
                if (i > 0) Pc?.PlayReleaseMotion(MoveMotion(Element, Weapon), R.MotionWindowQuick, MoveFace(dir), alt: i % 2 == 1);
                Step(dir, R.ZephyrDistance, R.ZephyrSeconds);
                ArsenalFx.Line(ArsenalFx.Band.Cord, from, dir, R.ZephyrDistance, 0.1f, Tuning.ReleaseFx.Gust, R.ZephyrSeconds, 0.25f);
                yield return new WaitForSeconds(R.ZephyrSeconds);
                foreach (var h in Bodies(Me, R.ZephyrRadius)) DragToward.Push(h, Me, R.ZephyrPush);
                TexelFx.Ring(Feet, Tuning.ReleaseFx.Gust, 0.15f, R.ZephyrRadius, 0.25f, 1.2f, dotted: true);
                if (i < R.ZephyrBlinks - 1) yield return new WaitForSeconds(R.ZephyrBetween);
            }
        }

        // ---- TREMOR STANCE's crack (EarthResource), and Stomp's ----

        /// <summary>A crack running <paramref name="length"/> along <paramref name="dir"/> from the
        /// player (or from <paramref name="start"/> - a bow's, where its arrow lands), striking each body it
        /// reaches once for <paramref name="units"/> hit units.</summary>
        public static void Crack(PlayerController pc, Vector2 dir, float length, float halfWidth, float units,
                                 Vector2? start = null)
        {
            if (pc == null) return;
            var a = pc.GetComponent<ReleaseArsenal>();   // never ?? - a missing component isn't C# null
            if (a == null) a = pc.gameObject.AddComponent<ReleaseArsenal>();
            a.StartCoroutine(a.RunCrack(dir, length, halfWidth, units, R.TremorCrackSeconds, start));
        }

        IEnumerator RunCrack(Vector2 dir, float length, float halfWidth, float units, float seconds, Vector2? start = null)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Vector2 from = start ?? Me;
            ArsenalFx.Line(ArsenalFx.Band.Crack, Drawn(from), dir, length, halfWidth, TexelFx.Stone, seconds, seconds + 0.5f);
            var hit = new HashSet<Health>();
            for (float t = 0f; t <= seconds; t += Time.deltaTime)
            {
                float reach = length * Mathf.Clamp01(t / seconds);
                foreach (var h in InLine(from, from + dir * reach, halfWidth))
                {
                    if (!hit.Add(h)) continue;
                    var info = new DamageInfo(units * (Pc != null ? Pc.HitUnit : Tuning.Player.BaseDamage), ElementType.Earth, gameObject);
                    Pc.ReleaseHit(h, info);
                    if (!h.IsDead) Pc.Stagger(h, R.ComboStagger * 0.5f);
                    TexelFx.Hop(ReleaseCanvas.Feet(h.transform), TexelFx.Stone, 0.3f, 5, TexelFx.Ground);
                }
                yield return null;
            }
            TexelFx.Hop(Drawn(from + dir * length), TexelFx.Stone, 0.35f, 6, TexelFx.Ground);
        }
    }
}
