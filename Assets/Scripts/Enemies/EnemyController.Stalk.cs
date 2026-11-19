using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>
    /// THE BRUTE STALKS (Tuning.EnemyAi.BruteStalks, the user's design 2026-11-15): a full-sized
    /// Brute - normal or elite; a Dross keeps the old rush - no longer runs in to swing. Without a
    /// turn it holds the STANDOFF just past its reach and HERDS: it takes the side of the player
    /// with the most open floor, away from its pack, so the room closes from both sides. On its
    /// turn it comes in for the overhead. Crowd it - inside ShoveRange - and it SHOVES: a LOW tell
    /// (the overhead's is the tallest it stands), then its shoulder drives the player back out.
    /// </summary>
    public partial class EnemyController
    {
        /// <summary>This Brute stalks, herds and shoves (never a Dross).</summary>
        bool Stalks => Kind == EnemyKind.Chaser && !Dross && Tuning.EnemyAi.BruteStalks;

        bool _shoveTell;
        float _herdPickAt = -10f;
        Vector2 _herdDir;

        /// <summary>The shove's tell is running - ChaserVisual's crouch.</summary>
        public bool ShoveTelling => _shoveTell && _telegraphTimer > 0f;

        /// <summary>When (Time.time) the last shove landed - ChaserVisual's drive frame.</summary>
        [System.NonSerialized] public float ShoveAt = -10f;

        /// <summary>
        /// Where a stalking Brute without a turn goes: to its side of the player at the standoff
        /// radius, round the band rather than through the player's reach, then holding the band
        /// there as any standoff does.
        /// </summary>
        Vector2 Herd(Vector2 pos, Vector2 to, float dist, float speed)
        {
            Vector2 player = _target.position;
            if (Time.time >= _herdPickAt)
            {
                _herdPickAt = Time.time + Tuning.EnemyAi.HerdRepickSeconds;
                _herdDir = HerdSide(pos, player);
            }
            float r = StandoffRadius;
            var spot = player + _herdDir * r;
            if ((spot - pos).sqrMagnitude < 0.36f) return Standoff(pos, to, dist, speed);

            // Inside the band's reach and off its side: round the circle toward it, keeping the radius.
            float bearing = Mathf.Atan2(pos.y - player.y, pos.x - player.x) * Mathf.Rad2Deg;
            float side = Mathf.Atan2(_herdDir.y, _herdDir.x) * Mathf.Rad2Deg;
            float off = Mathf.DeltaAngle(bearing, side);
            if (Mathf.Abs(off) > 25f && dist < r + 1.5f)
            {
                var out_ = dist > 1e-3f ? (pos - player) / dist : Vector2.up;
                var round = new Vector2(-out_.y, out_.x) * Mathf.Sign(off);
                var want = round + out_ * Mathf.Clamp(r - dist, -1f, 1f);
                return Steer(want, float.PositiveInfinity) * speed;
            }
            // Farther out: straight to the spot where the line is clear, down the route where not.
            var toSpot = spot - pos;
            var way = NavField.ClearLine(pos, spot) ? toSpot : NavField.RouteDirection(pos, spot);
            return Steer(way, toSpot.magnitude) * speed;
        }

        /// <summary>
        /// The side to hold: of sixteen around the player at the standoff radius, on standing floor,
        /// the one most AWAY from the rest of the pack, with the most open floor beyond it (so never
        /// the lip side), leaning to the side it is already on.
        /// </summary>
        Vector2 HerdSide(Vector2 pos, Vector2 player)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;
            var all = EnemyRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e == this || e._health == null || e._health.IsDead) continue;
                sum += (Vector2)e.transform.position;
                n++;
            }
            var mine = pos - player;
            mine = mine.sqrMagnitude > 1e-4f ? mine.normalized : Vector2.right;
            var away = n > 0 ? player - sum / n : mine;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : mine;

            float r = StandoffRadius, best = float.NegativeInfinity;
            var pick = mine;
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (!Arena.OnFloor(player + d * r, BodyRadius)) continue;
                int open = 0;
                for (int k = 1; k <= 3; k++)
                    if (Arena.OnFloor(player + d * (r + k * 0.8f), BodyRadius)) open++;
                float score = Vector2.Dot(d, away) + open * 0.25f + Vector2.Dot(d, mine) * Tuning.EnemyAi.HerdStickiness;
                if (score > best) { best = score; pick = d; }
            }
            return pick;
        }

        /// <summary>The shove's tell: crouched, the eye lit - held still for it.</summary>
        void BeginShoveTell()
        {
            Audio.Sfx.Play(Audio.Sound.Growl, transform.position);
            _shoveTell = true;
            _telegraphTimer = Tuning.EnemyAi.ShoveTellSeconds;
        }

        /// <summary>The shove lands: half a swing's damage and the player driven back out of reach.
        /// Parried or across a spire's wall, nothing; the clash hooks read the tell like a swing's.</summary>
        void LandShove()
        {
            CancelTelegraph();
            _cooldown = AttackInterval;
            ShoveAt = Time.time;
            ReleaseToken();
            BeginRecover(Tuning.Enemy.ChaserRecoverSeconds);
            Audio.Sfx.Play(Audio.Sound.Thud, transform.position);

            if (_targetHealth == null || _targetHealth.IsDead || _target == null) return;
            var d = (Vector2)_target.position - (Vector2)transform.position;
            if (d.magnitude > Tuning.EnemyAi.ShoveRange + Tuning.EnemyAi.ShoveReachSlack) return;
            if (Spire.BubbleSeparates(transform.position, _target.position)) return;
            if (_targetController != null && _targetController.TryParry(transform.position, gameObject)) return;

            var dir = d.sqrMagnitude > 1e-4f ? d.normalized : (Vector2)(transform.right);
            _targetHealth.Take(new DamageInfo(Damage * Tuning.EnemyAi.ShoveDamageMul, ElementType.Earth, gameObject));
            if (_targetController != null && !_targetHealth.IsDead)
                _targetController.Shove(dir, Tuning.EnemyAi.ShoveDistance, Tuning.EnemyAi.ShoveSeconds);
        }
    }
}
