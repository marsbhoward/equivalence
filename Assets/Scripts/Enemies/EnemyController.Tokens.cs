using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>
    /// ATTACK TURNS, leading aim, and the few places an enemy READS THE PLAYER (2026-10-25).
    /// Numbers: Tuning.AttackTokens and Tuning.EnemyAi.
    /// </summary>
    public partial class EnemyController
    {
        /// <summary>The floor's ENEMY BAND (Core.RunFormat.EnemyBand), set by EnemyFactory - what
        /// every behaviour that arrives with depth asks, never the floor.</summary>
        [SerializeField] int _band;
        public int Band { get => _band; set => _band = value; }

        // ---------------------------------------------------------------- attack turns

        // Serialized so a domain reload keeps who holds a turn. The pool's count is never stored -
        // it is summed off the registry each time it is asked, so nothing can drift out of step.
        [SerializeField] float _tokenUntil;

        // When a tell last STARTED, of any kind. A reload resets it to the past, which only lets one
        // tell through the stagger early.
        static float _lastTellStart = -10f;

        bool HoldsToken => Time.time < _tokenUntil && _health != null && !_health.IsDead;

        int TokenCost => Elite ? Tuning.AttackTokens.EliteCost : 1;

        /// <summary>Whether this body may START a tell now - taking a turn from the ONE shared pool
        /// if so (Tuning.AttackTokens). Asked every frame a ready enemy waits; a refused one holds.</summary>
        bool TryTakeToken()
        {
            if (!Tuning.AttackTokens.Enabled) return true;
            float now = Time.time;
            if (HoldsToken)
            {
                _tokenUntil = now + Tuning.AttackTokens.MaxHoldSeconds;
                return true;
            }

            // A last start in the FUTURE is a stale static from before Time restarted - ignored.
            if (_lastTellStart <= now && now - _lastTellStart < Tuning.AttackTokens.StaggerSeconds) return false;

            int used = 0;
            var all = EnemyRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e == this || !e.HoldsToken) continue;
                used += e.TokenCost;
            }
            var table = Tuning.AttackTokens.Turns;
            int max = table[Mathf.Clamp(_band, 0, table.Length - 1)];
            // The weapon art moves the turns a RUSHER finds: fewer while one is loaded, more just after.
            if (Rusher)
            {
                if (InPunishWindow) max += Tuning.EnemyAi.PunishBonusTokens;
                else if (ArtLoaded) max = Mathf.Max(1, max - Tuning.EnemyAi.LoadedTokenCut);
            }
            // An empty pool always gives a turn, so an elite costing more than the pool holds is
            // never locked out.
            if (used > 0 && used + TokenCost > max) return false;

            _tokenUntil = now + Tuning.AttackTokens.MaxHoldSeconds;
            _lastTellStart = now;
            return true;
        }

        /// <summary>Gives the turn back <paramref name="after"/> seconds from now - the blow has
        /// resolved (with the hold), or it was denied (0). A no-op for a body holding none.</summary>
        void ReleaseToken(float after = Tuning.AttackTokens.HoldAfterSeconds)
        {
            if (!HoldsToken) return;
            _tokenUntil = Time.time + after;
        }

        // ---------------------------------------------------------------- leading aim

        [SerializeField] int _aimedShots;
        Rigidbody2D _targetRb;

        /// <summary>Counts an aimed shot and says whether it LEADS: from LeadFromBand, every Nth for
        /// an ordinary Stinger or Salamander, every one for an elite.</summary>
        bool NextShotLeads()
        {
            if (_band < Tuning.EnemyAi.LeadFromBand) return false;   // band 0 aims plainly
            _aimedShots++;
            return Elite || _aimedShots % Tuning.EnemyAi.LeadEvery == 0;
        }

        Vector2 TargetVelocity()
        {
            if (_target == null) return Vector2.zero;
            if (_targetRb == null) _targetRb = _target.GetComponent<Rigidbody2D>();
            return _targetRb != null ? _targetRb.linearVelocity : Vector2.zero;
        }

        /// <summary>
        /// Where the player will be when a shot fired from <paramref name="from"/> after
        /// <paramref name="delay"/> seconds, at <paramref name="speed"/>, arrives - if they keep
        /// moving as they are now. Capped (Tuning.EnemyAi.LeadMaxDistance) and kept on the floor.
        /// A standing player is not led.
        /// </summary>
        Vector2 LeadPoint(Vector2 from, float delay, float speed)
        {
            Vector2 p = _target.position;
            var v = TargetVelocity();
            if (v.magnitude < Tuning.EnemyAi.LeadMinSpeed) return p;
            float travel = speed > 0.01f ? Vector2.Distance(from, p) / speed : 0f;
            var lead = Vector2.ClampMagnitude(v * (delay + travel), Tuning.EnemyAi.LeadMaxDistance);
            var at = Arena.Clamp(p + lead, 0.3f);
            return Arena.OnFloor(at, 0.2f) ? at : p;
        }

        // ---------------------------------------------------------------- reading the player

        /// <summary>The kinds that RUSH IN - the ones that respect a loaded art and punish a spent one.</summary>
        bool Rusher => Kind is EnemyKind.Chaser or EnemyKind.Dasher;

        /// <summary>The player has a weapon art banked and ready.</summary>
        bool ArtLoaded => _targetController != null && _targetController.FinisherNext && !InPunishWindow;

        /// <summary>The player's last weapon art was done less than PunishSeconds ago.</summary>
        bool InPunishWindow => _targetController != null
            && Time.time - _targetController.ArtEndedAt < Tuning.EnemyAi.PunishSeconds;

        [SerializeField] float _punishSeenAt = -10f;

        /// <summary>The first frame this rusher, within PunishRadius, sees a spent art: true once
        /// per art. The Brute's swing is made ready on it.</summary>
        bool PunishBegins(float dist)
        {
            if (!Rusher || !InPunishWindow || dist > Tuning.EnemyAi.PunishRadius) return false;
            float at = _targetController.ArtEndedAt;
            if (at <= _punishSeenAt) return false;
            _punishSeenAt = at;
            return true;
        }

        /// <summary>How much faster a rusher moves now: hurrying at a pinned player, or rushing in
        /// after a spent art (the larger).</summary>
        float RushSpeedMul(float dist)
        {
            if (!Rusher) return 1f;
            float m = 1f;
            if (TargetPinned()) m = Tuning.EnemyAi.HurrySpeedMul;
            if (InPunishWindow && dist <= Tuning.EnemyAi.PunishRadius) m = Mathf.Max(m, Tuning.EnemyAi.PunishSpeedMul);
            return m;
        }

        /// <summary>Where a Brute WITHOUT A TURN stands while the player's art is loaded: just past
        /// its reach, circling - giving ground if closer, closing if farther.</summary>
        float StandoffRadius => AttackRange + Tuning.EnemyAi.StandoffMargin;

        Vector2 Standoff(Vector2 pos, Vector2 to, float dist, float speed)
        {
            float r = StandoffRadius;
            if (dist < r - 0.2f) return Flee(to) * (speed * Tuning.EnemyAi.StandoffBackOffFraction);
            if (dist > r + 0.2f) return MeleeApproach(pos, dist, r) * speed;
            return Strafe(to) * (speed * Tuning.EnemyAi.StandoffCircleFraction);
        }

        StatusEffects _targetStatus;

        /// <summary>The player cannot walk away: petrified, stone, or slowed hard (a mire, a cost).
        /// Brutes and Hounds HURRY toward them.</summary>
        bool TargetPinned()
        {
            if (_target == null) return false;
            if (_targetStatus == null) _targetStatus = _target.GetComponent<StatusEffects>();
            if (_targetStatus == null) return false;
            return _targetStatus.Petrified || _targetStatus.Stone
                || _targetStatus.MoveMultiplier < Tuning.EnemyAi.HurryWhenSlowerThan;
        }

        bool _sawBar;

        /// <summary>
        /// A Stinger in reach that sees the player's weapon-art bar OPEN (the edge, once per bar):
        /// a shot nearly ready comes now; otherwise it gives ground. Asked every frame at the top of
        /// UpdateRanged (so the edge is never missed); a punish still needs a turn like any shot.
        /// </summary>
        void ReadStrikeBar()
        {
            bool bar = _targetController != null && _targetController.StrikePending;
            bool opened = bar && !_sawBar;
            _sawBar = bar;
            // Mid-tell it is already committed; mid-kite it is already giving ground.
            if (!opened || _telegraphTimer > 0f || Recovering) return;
            if (Vector2.Distance(transform.position, _target.position) > AttackRange) return;
            if (_cooldown <= Tuning.EnemyAi.BarPunishWithin) _cooldown = 0f;
            else BeginRecover(Tuning.EnemyAi.BarKiteSeconds);
        }

        /// <summary>
        /// The Brimstone Heart's self-destruct rush runs at where the player is GOING - a cut-off,
        /// not a chase. Straight there while the line is clear, by the route otherwise.
        /// </summary>
        Vector2 BombCutOff(Vector2 pos, float dist, float speed)
        {
            if (dist <= AttackRange * 0.9f) return Vector2.zero;
            var lead = Vector2.ClampMagnitude(TargetVelocity() * Tuning.EnemyAi.BombCutOffSeconds,
                                              Tuning.EnemyAi.BombCutOffMax);
            var cut = Arena.Clamp((Vector2)_target.position + lead, 0.3f);
            if (lead.sqrMagnitude > 0.01f && Arena.OnFloor(cut, BodyRadius) && NavField.ClearLine(pos, cut)
                && (cut - pos).sqrMagnitude > 0.04f)
                return Steer(cut - pos, dist) * speed;
            return MeleeApproach(pos, dist, AttackRange) * speed;
        }
    }
}
