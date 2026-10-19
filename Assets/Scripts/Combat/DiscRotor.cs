using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Gyre's rotor (the user's calls, 2026-10-16 - "helicopter style", IN FRONT of the character):
    /// both discs leave the outstretched hands and spin half a turn apart as rotor blades at chest
    /// height out along the aim, in the plane FACING the aim - its upright axis up the screen, its
    /// level axis across the aim - so aimed to the side it is seen edge-on, aimed at the camera as
    /// the whole circle. While the pull runs, a DRAFT
    /// of pale streaks blows through the cone toward the player. <see cref="Land"/> drops the discs
    /// back into the hands (the cut). Drawing only - the pull and the cut are PlayerController's
    /// (GyreStrike). Both discs are out at once, so a weapon with a combined picture and its relic
    /// shows it (DiscPictures).
    /// </summary>
    public class DiscRotor : MonoBehaviour
    {
        Player.PlayerController _pc;
        Transform[] _discs = new Transform[2];
        Vector2[] _from = new Vector2[2];
        Vector2 _aim;
        float _clock, _angle, _landAt = -1f, _streakClock;
        bool _drafting, _handsHidden;

        public static DiscRotor Spin(Player.PlayerController pc, Vector2 aim, Vector2 mainHand, Vector2 offHand)
        {
            var go = new GameObject("disc.rotor");
            var r = go.AddComponent<DiscRotor>();
            r._pc = pc;
            r._aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.right;
            r._from[0] = mainHand;
            r._from[1] = offHand;
            r._angle = Mathf.Atan2(r._aim.y, r._aim.x) * Mathf.Rad2Deg;

            var rig = pc.Rig;
            for (int i = 0; i < 2; i++)
            {
                var d = new GameObject("rotor" + i);
                d.transform.SetParent(go.transform, false);
                d.transform.position = r._from[i];
                var sr = DiscVisual.Add(d);
                Vector2 want = Vector2.one * 0.45f;
                if (rig != null && rig.TryGetDiscVisual(i, out var sprite, out var tint, out var size))
                {
                    sr.sprite = sprite;
                    sr.color = tint;
                    want = size * ThrownDisc.FlightScale;
                }
                else sr.sprite = Spr.Circle;
                var native = sr.sprite.bounds.size;
                d.transform.localScale = new Vector3(
                    native.x > 1e-4f ? want.x / native.x : want.x,
                    native.y > 1e-4f ? want.y / native.y : want.y, 1f);
                DiscPictures.ShowCombined(sr, pc);   // both discs out at once
                DiscVisual.Centre(sr);
                sr.sortingOrder = SortingOrders.Fx - 1;
                LightCycleTrail.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                LightningArcs.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                Art.Gear.HeldGlow.Follow(rig, d.transform, sr);
                r._discs[i] = d.transform;
            }
            // Both hands empty while the rotor turns.
            rig?.SetWeaponVisible(false);
            rig?.SetWeaponVisible(false);
            r._handsHidden = true;
            return r;
        }

        /// <summary>The pull is running: the draft blows through the cone.</summary>
        public bool Drafting { set => _drafting = value; }

        /// <summary>Bring the discs down into the hands over <paramref name="seconds"/>, then go.</summary>
        public void Land(float seconds)
        {
            if (_landAt >= 0f) return;
            _landAt = _clock;
            _landSeconds = Mathf.Max(0.02f, seconds);
            _drafting = false;
            for (int i = 0; i < 2; i++) if (_discs[i] != null) _from[i] = _discs[i].position;
        }

        float _landSeconds;

        void Update()
        {
            if (_pc == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _clock += dt;
            _angle += Tuning.Gyre.RotorDegreesPerSecond * dt;

            Vector2 body = _pc.transform.position;
            // In front, at chest height, spinning in the plane facing the aim.
            Vector2 hub = body + Vector2.up * Tuning.Gyre.RotorHeight + _aim * Tuning.Gyre.RotorAhead;
            var across = new Vector2(-_aim.y, _aim.x);

            for (int i = 0; i < 2; i++)
            {
                var d = _discs[i];
                if (d == null) continue;
                float a = (_angle + i * 180f) * Mathf.Deg2Rad;
                Vector2 blade = hub + (across * Mathf.Cos(a) + Vector2.up * Mathf.Sin(a)) * Tuning.Gyre.RotorRadius;
                Vector2 at;
                if (_landAt >= 0f)
                {
                    // Down into the hands.
                    float u = Mathf.Clamp01((_clock - _landAt) / _landSeconds);
                    Vector2 hand = i == 0 ? MainHand() : OffHand();
                    at = Vector2.Lerp(_from[i], hand, u * u);
                }
                else
                {
                    // Up out of the hands onto the rotor.
                    float u = Mathf.Clamp01(_clock / Tuning.Gyre.RiseSeconds);
                    at = Vector2.Lerp(_from[i], blade, 1f - (1f - u) * (1f - u));
                }
                d.position = at;
                d.Rotate(0f, 0f, 900f * dt);
            }

            if (_drafting) Draft(dt, body);

            if (_landAt >= 0f && _clock - _landAt >= _landSeconds) Finish();
        }

        Vector2 MainHand()
        {
            var rig = _pc.Rig;
            if (rig != null && rig.Transform != null && rig.TryGetSwordHand(out var local))
                return rig.Transform.TransformPoint(local);
            return _pc.transform.position;
        }

        Vector2 OffHand()
        {
            var off = _pc.Rig?.OffhandRenderer;
            return off != null ? (Vector2)off.transform.position : MainHand();
        }

        /// <summary>Pale streaks blown through the cone toward the player.</summary>
        void Draft(float dt, Vector2 body)
        {
            _streakClock -= dt;
            while (_streakClock <= 0f)
            {
                _streakClock += Tuning.Gyre.StreakEvery;
                float half = Tuning.Gyre.HalfAngle;
                float a = (Mathf.Atan2(_aim.y, _aim.x) * Mathf.Rad2Deg + Random.Range(-half, half)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float dist = Random.Range(Tuning.Gyre.DragStopAt + 0.4f, Tuning.Gyre.Reach);
                DraftStreak.Blow(body + dir * dist, -dir);
            }
        }

        void Finish()
        {
            if (_handsHidden)
            {
                _pc?.Rig?.SetWeaponVisible(true);
                _pc?.Rig?.SetWeaponVisible(true);
                _handsHidden = false;
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Every exit gives the hands back - SetWeaponVisible is reference counted.
            if (_handsHidden)
            {
                _pc?.Rig?.SetWeaponVisible(true);
                _pc?.Rig?.SetWeaponVisible(true);
                _handsHidden = false;
            }
        }
    }
}
