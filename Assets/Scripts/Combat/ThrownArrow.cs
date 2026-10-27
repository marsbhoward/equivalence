using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Combat
{
    /// <summary>
    /// A single arrow, fired at whatever the player is currently locked onto.
    ///
    /// Unlike <see cref="ThrownDisc"/> this never bounces and never returns - the bow itself
    /// stays in the character's hand the whole time, so there is no weapon to hand back and no
    /// reason to hide it. It is also why this needs its own small projectile sprite rather than
    /// borrowing the held weapon's own art the way a thrown disc does.
    /// </summary>
    public class ThrownArrow : MonoBehaviour
    {
        public float Speed = 15f;

        /// <summary>A HEAVY weapon art's arrow moves what it hits (the user's call, 2026-10-15):
        /// this knockback along its flight, displacing. 0 for everything else.</summary>
        public float Knockback;
        Vector2 _travel = Vector2.right;

        GameObject _owner;
        Player.PlayerController _player;
        float _damage;
        ElementType _element;
        float _nearFraction;
        Vector2 _origin;
        float _range;
        Health _target;

        /// <summary>Whether this shot came off a finisher rather than a basic - see
        /// DamageInfo.IsFinisher.</summary>
        bool _isFinisher;
        bool _crit;

        /// <summary>CLASH (Combat.Clash): whether this arrow beats an enemy's strike, and where it
        /// was last frame - the line it cuts enemy shots along.</summary>
        bool _wins;
        Vector2 _last;

        /// <summary>Gear's Splash and (bow-only) Pierce, as fractions of this arrow's hit.</summary>
        public float SplashFraction, PierceFraction;

        /// <summary>A Prima Materia art shot: a kill comes apart (DamageInfo.Disintegrates), and no
        /// hit-stop - a volley under bullet time would otherwise stutter at every arrow.</summary>
        public bool Disintegrates, SuppressHitstop;

        /// <summary>A Four Birds spectral arrow: its hit never spends a Mark (DamageInfo.SparesMark).</summary>
        public bool SparesMark;

        /// <summary>Basics forms stamped at the loose (RunEffects.ArmArrow): PATIENT STRING bleeds a share
        /// of the hit over its seconds; LONG EMBER burns what it strikes past a share of the reach.</summary>
        public float BleedShare, BleedSeconds;
        public float EmberFrom = 2f, EmberShare, EmberSeconds;

        /// <summary>The damage it carries (set after Fire - the twin shot halves it).</summary>
        public float Damage { get => _damage; set => _damage = value; }

        /// <summary>How many basics this arrow's hit counts as (AttackStep.BasicsWorth) - the shot of
        /// a pair whose other basic was a move counts twice.</summary>
        public int BasicsWorth = 1;

        /// <summary>Called with each body this arrow strikes, after the hit (the Four Birds' swan
        /// arrow splits, its peacock arrow marks).</summary>
        public System.Action<Health, DamageInfo> OnHit;

        /// <summary>A point the arrow flies THROUGH before it homes on its target (the Prima Materia
        /// art's stone), and what to do as it passes. Null for an ordinary shot.</summary>
        public System.Func<Vector2> Through;
        public System.Action<ThrownArrow> OnThrough;

        /// <summary>Tint for the shaft; the element's own when left clear.</summary>
        public Color? Tint
        {
            set { var sr = GetComponent<SpriteRenderer>(); if (sr != null && value.HasValue) sr.color = value.Value; }
        }

        /// <summary>Set once and kept for the rest of the flight - a Red force field crossed on
        /// the way in doubles whatever this arrow eventually lands for.</summary>
        float _damageMul = 1f;

        public static ThrownArrow Fire(GameObject owner, Health target, Vector2 aim, float damage,
                                       ElementType element, float range, float nearFraction,
                                       bool isFinisher = false, bool crit = false,
                                       Vector2? from = null, Sprite sprite = null)
        {
            var go = new GameObject("thrown-arrow");
            // FROM: where it is loosed (a Four Birds spectral bow, a split off a struck body).
            go.transform.position = from ?? (Vector2)owner.transform.position;

            var a = go.AddComponent<ThrownArrow>();
            a._owner = owner;
            a._player = owner.GetComponent<Player.PlayerController>();
            a._damage = damage;
            a._element = element;
            a._nearFraction = Mathf.Clamp01(nearFraction);
            a._isFinisher = isFinisher;
            a._crit = crit;
            a._origin = go.transform.position;
            a._range = Mathf.Max(0.01f, range);
            a._target = target;
            // A Heavy art's arrow beats an enemy's strike (Combat.Clash) - the art being loosed now.
            a._wins = a._player != null && Clash.Wins(isFinisher, a._player.LastArtWeight);
            a._last = go.transform.position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrders.Fx - 1;
            var arrow = sprite != null ? sprite : a._player != null ? a._player.ArrowSprite : null;
            if (arrow != null)
            {
                // The drawn bow's OWN arrow (the user's call, 2026-10-16 - DemoGear.BowKit), at the
                // rig's scale, with a faint streak of the element behind it so the element still
                // reads the way the plain capsule said it - or, inside Ignite, a tail of flame.
                sr.sprite = arrow;
                sr.color = Color.white;
                float scale = Core.Tuning.Player.ArenaVisualScale;
                go.transform.localScale = new Vector3(scale, scale, 1f);
                if (Art.Gear.SecretFire.HasMarks(arrow)) Art.Gear.KindledMarks.On(sr);
                float len = arrow.bounds.size.x;
                // Inside Fire's Ignite the streak is a flame tail from the fletching instead
                // (Combat.FlameTrail - the sword's smear, laid along the shot).
                if (a._player != null && a._player.Igniting)
                    FlameTrail.Behind(go.transform, sr, new Vector2(-len * 0.5f, 0f), Core.Tuning.FireFx.TrailArrowWindow,
                                      Core.Tuning.FireFx.TrailArrowRadius, SortingOrders.Fx - 2);
                else
                {
                    var streak = new GameObject("arrow-streak");
                    streak.transform.SetParent(go.transform, false);
                    var ssr = streak.AddComponent<SpriteRenderer>();
                    ssr.sprite = Spr.Capsule;
                    var tint = ElementInfo.Tint(element); tint.a = 0.35f;
                    ssr.color = tint;
                    ssr.sortingOrder = SortingOrders.Fx - 2;
                    streak.transform.localPosition = new Vector3(-len * 0.55f, 0f, 0f);
                    streak.transform.localScale = new Vector3(len * 1.1f, arrow.bounds.size.y * 0.5f, 1f);
                }
            }
            else
            {
                sr.sprite = Spr.Capsule;
                sr.color = ElementInfo.Tint(element);
                go.transform.localScale = new Vector3(0.5f, 0.11f, 1f);
            }
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

            return a;
        }

        // ---- PIERCING (the Four Birds' crow arrow): straight down a line, striking every body it
        // passes once, each a swing's falloff below the one before; walls stop it. No target. ----

        bool _piercing;
        Vector2 _dir;
        float _falloff = 1f, _floor;
        int _pierced;
        readonly System.Collections.Generic.HashSet<Health> _struck = new();
        static readonly Collider2D[] _touch = new Collider2D[16];

        /// <summary>Send this arrow straight along <paramref name="dir"/>, piercing. Call right after Fire.</summary>
        public void Pierce(Vector2 dir, float falloff, float floor)
        {
            _piercing = true;
            _dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            _travel = _dir;
            _falloff = falloff;
            _floor = floor;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
        }

        void UpdatePierce()
        {
            float step = Speed * Time.deltaTime;
            if (step <= 0f) return;   // a hit-stop
            Vector2 pos = transform.position, next = pos + _dir * step;
            switch (HazardQuery.CheckCrossing(pos, next))
            {
                case SightResult.Nulled:
                case SightResult.Blocked: Destroy(gameObject); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            transform.position = next;
            int n = Physics2D.OverlapCircleNonAlloc(next, 0.3f, _touch);
            for (int i = 0; i < n; i++)
            {
                var col = _touch[i];
                if (col == null) continue;
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || _struck.Contains(hp)) continue;
                if (col.GetComponentInParent<Enemies.EnemyController>() == null
                    && col.GetComponentInParent<Bosses.Boss>() == null) continue;
                _struck.Add(hp);
                HitBody(hp, Mathf.Max(_floor, Mathf.Pow(_falloff, _pierced)), _pierced == 0);
                _pierced++;
            }
            if (Vector2.Distance(next, _origin) >= _range) Destroy(gameObject);
        }

        void Update()
        {
            if (_owner == null) { Destroy(gameObject); return; }
            if (_piercing) { UpdatePierce(); return; }
            if (_target == null || _target.IsDead) { Miss(); return; }
            if (_lob > 0f) { UpdateLob(); return; }

            if (Through != null)
            {
                Vector2 via = Through(), at = transform.position;
                var leg = via - at;
                float stepVia = Speed * Time.deltaTime;
                if (leg.magnitude <= stepVia)
                {
                    transform.position = via;
                    Through = null;
                    OnThrough?.Invoke(this);
                }
                else
                {
                    transform.position = at + leg.normalized * stepVia;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(leg.y, leg.x) * Mathf.Rad2Deg);
                }
                return;
            }

            Vector2 to = (Vector2)_target.transform.position - (Vector2)transform.position;
            float dist = to.magnitude;
            if (dist > 1e-4f) _travel = to / dist;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);

            float step = Speed * Time.deltaTime;
            Vector2 prevPos = transform.position;
            Vector2 nextPos = dist <= step ? (Vector2)_target.transform.position : prevPos + to / dist * step;

            switch (HazardQuery.CheckCrossing(prevPos, nextPos))
            {
                // A Blue force field nulls the shot outright - it vanishes here, dealing nothing.
                case SightResult.Nulled: Miss(); return;
                case SightResult.Blocked: Miss(); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            // Charnel (the exchange): a corpse in the way stops the arrow.
            if (Corpse.StopsShot(prevPos, nextPos, out _)) { Miss(); return; }

            if (dist <= step) { Hit(); return; }
            transform.position = nextPos;
        }

        /// <summary>The arrow ends without striking anything - its target gone, or a wall, a field or
        /// a corpse in the way. A basic's miss tells the ledger (Thornfield, a basics form).</summary>
        void Miss()
        {
            if (!_isFinisher && _player != null) _player.Effects?.OnArrowMissed(transform.position);
            Destroy(gameObject);
        }

        // ---- LOBBED (the bow's High pair, 2026-10-17): the shot homes along the GROUND as any
        // other - hits, force fields and range judged there, but it flies OVER walls - while the
        // drawn arrow rises and falls over it on a parabola, turned along the arc. ----

        float _lob, _lobDone;
        Vector2 _ground;

        /// <summary>Lob this arrow: its peak <paramref name="height"/> above the ground track,
        /// over the distance to its target. Call right after Fire.</summary>
        public void Lob(float height)
        {
            _lob = Mathf.Max(0f, height);
            _ground = transform.position;
            _lobDone = 0f;
        }

        void UpdateLob()
        {
            Vector2 to = (Vector2)_target.transform.position - _ground;
            float dist = to.magnitude;
            if (dist > 1e-4f) _travel = to / dist;
            float step = Speed * Time.deltaTime;
            if (step <= 0f) return;   // a hit-stop
            Vector2 next = dist <= step ? (Vector2)_target.transform.position : _ground + to / dist * step;
            // OVER WALLS (the user's call, 2026-10-17), as a mortar shell is lobbed over them - and
            // over a corpse in the way. A force field still nulls or amplifies it.
            switch (HazardQuery.CheckCrossing(_ground, next))
            {
                case SightResult.Nulled: Destroy(gameObject); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            if (dist <= step) { transform.position = next; Hit(); return; }

            float moved = (next - _ground).magnitude;
            _lobDone += moved;
            _ground = next;
            // The arc over the whole flight as it stands: what is flown plus what is left.
            float total = _lobDone + (dist - moved);
            float u = total > 1e-4f ? Mathf.Clamp01(_lobDone / total) : 1f;
            float h = _lob * 4f * u * (1f - u);
            float slope = _lob * 4f * (1f - 2f * u) / Mathf.Max(0.5f, total);   // dh per unit of ground
            var drawn = _ground + Vector2.up * h;
            var tangent = _travel + Vector2.up * slope;
            transform.position = drawn;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
        }

        void Hit()
        {
            HitBody(_target, 1f, true);
            Destroy(gameObject);
        }

        /// <summary>CLASH: an enemy shot on this frame's flight is cut. A basic's arrow is spent on
        /// it; an art's flies on.</summary>
        void LateUpdate()
        {
            Vector2 now = transform.position;
            int cut = Clash.CutShots(_last, now, Core.Tuning.Clash.ShotCutRadius);
            _last = now;
            if (cut > 0 && !_isFinisher) Destroy(gameObject);
        }

        void HitBody(Health body, float scale, bool first)
        {
            // CLASH: the body's own strike is about to land - the arrow is batted away and its blow
            // is void, unless this is a Heavy art's arrow.
            if (Clash.AtBody(body, (Vector2)body.transform.position - _travel, _wins)) return;

            // Measured against the ORIGIN (where the shot was released), not the player's live
            // position - the same rule ThrownDisc's own FarFraction/NearFraction curves use, so
            // a recoil or a dash mid-flight cannot cheapen or inflate a shot already in the air.
            float t = Mathf.Clamp01(Vector2.Distance(body.transform.position, _origin) / _range);
            float near = _nearFraction >= 1f ? 1f : Mathf.Lerp(_nearFraction, 1f, t);

            float dmg = _damage * near * _damageMul * scale;
            if (_player != null) dmg = _player.ScaleThrownHit(body, dmg, _isFinisher, _crit, t, first);
            var info = new DamageInfo(dmg, _element, _owner)
                { Thrown = true, IsFinisher = _isFinisher, Crit = _crit,
                  Disintegrates = Disintegrates, SuppressHitstop = SuppressHitstop, SparesMark = SparesMark,
                  Knockback = _travel * Knockback, Displaces = Knockback > 0f };
            var where = body.transform.position;
            body.Take(info);

            // Direction of travel, not the aim at release: the arrow homes, so the line it
            // carries on along is the one it was actually flying when it struck.
            if (first)
            {
                CrowdHits.Splash(body, info.Amount, SplashFraction, _element, _owner);
                CrowdHits.Pierce(body, (Vector2)body.transform.position - _origin, info.Amount,
                                 PierceFraction, _element, _owner);
            }

            if (_player != null && !body.IsDead)
            {
                if (BleedShare > 0f && BleedSeconds > 0f)
                    _player.Bleed(body, info.Amount * BleedShare / BleedSeconds, BleedSeconds);
                if (EmberShare > 0f && t >= EmberFrom)
                    _player.Burn(body, info.Amount * EmberShare, EmberSeconds);
            }

            _player?.Resource?.OnHitLanded(body, info);
            _player?.NotifyThrownHit(body, info, t, first, BasicsWorth);
            OnHit?.Invoke(body, info);

            TexelFx.Hit(where, _element, _travel);
        }
    }
}
