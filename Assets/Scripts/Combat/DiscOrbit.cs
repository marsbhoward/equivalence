using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Revolve's ring (Tuning.Revolve; the user's calls, 2026-10-15): FOUR discs - the pair from
    /// the hands and two more - turn close round the body, a revolution every
    /// <see cref="Tuning.Revolve.Seconds"/>, following the player, until a chain has spent them:
    /// a basic knocks out ONE, the weapon art after the chain's two basics takes the last TWO to the
    /// hands (EndNow) - or Tuning.Revolve.MaxSeconds.
    ///
    ///   an enemy bolt or a mortar shell that reaches the ring   destroyed, a puff of smoke
    ///   an enemy a disc runs into                              a light hit, once per disc
    ///   a basic attack while it turns                          the swing goes on unarmed and
    ///                                                          KNOCKS the disc nearest the facing
    ///                                                          out of the ring as a throw (Knock)
    ///
    /// When the last disc is knocked out - or the time is up - what is left flies home to the
    /// hands. Scaled time throughout.
    /// </summary>
    public class DiscOrbit : MonoBehaviour
    {
        /// <summary>The ring now turning round the player, if any - there is one player.</summary>
        public static DiscOrbit Current { get; private set; }

        /// <summary>Whether a ring is turning with a disc still in it.</summary>
        public static bool Live => Current != null && Current.isActiveAndEnabled && Current._phase == Phase.Turn
                                   && Current.Remaining > 0;

        enum Phase { Turn, Home }

        Player.PlayerController _pc;
        Phase _phase;
        float _age, _startAngle, _homeAge;
        Action<Health> _onTouch;

        Disc[] _discs = new Disc[Tuning.Revolve.Discs];   // non-readonly, null-guarded (domain reload)
        int _revolution;

        sealed class Disc
        {
            public SpriteRenderer Sr;
            public bool InRing = true, Hidden = true;
            public Vector2 Pos, HomeFrom;   // Pos: where it is DRAWN
            public Vector2 Ground;          // where it is for the game: on the ground circle
            public Vector3 Scale;
            public float Offset;
            public HashSet<Health> Touched = new();
        }

        /// <summary>Discs knocked out of the ring so far - the knock-outs alternate the pair's
        /// two hits by it.</summary>
        public int Knocked => (_discs?.Length ?? 0) - Remaining;

        int Remaining
        {
            get
            {
                int n = 0;
                foreach (var d in _discs) if (d != null && d.InRing) n++;
                return n;
            }
        }

        public static DiscOrbit Begin(Player.PlayerController pc, Vector2 facing, Action<Health> onTouch)
        {
            if (Current != null) Current.EndNow();
            var go = new GameObject("disc.orbit");
            var o = go.AddComponent<DiscOrbit>();
            o._pc = pc;
            o._onTouch = onTouch;
            // Start at the facing, so the discs leave the hands forward and go round from there.
            o._startAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

            var rig = pc.Rig;
            for (int i = 0; i < o._discs.Length; i++)
            {
                var dgo = new GameObject("disc" + i);
                dgo.transform.SetParent(go.transform, false);
                var sr = DiscVisual.Add(dgo);
                Vector2 want = Vector2.one * 0.45f;
                if (rig != null && rig.TryGetDiscVisual(i % 2, out var sprite, out var tint, out var size))
                {
                    sr.sprite = sprite;
                    sr.color = tint;
                    want = size * ThrownDisc.FlightScale;
                }
                else sr.sprite = Spr.Circle;
                var native = sr.sprite.bounds.size;
                dgo.transform.localScale = new Vector3(
                    native.x > 1e-4f ? want.x / native.x : want.x,
                    native.y > 1e-4f ? want.y / native.y : want.y, 1f);
                DiscPictures.ShowCombined(sr, pc);   // all four out at once: the combined picture
                DiscVisual.Centre(sr);
                // The first two are the hands' own (each holds a hand empty); the rest are conjured.
                o._discs[i] = new Disc { Sr = sr, Offset = i * 360f / o._discs.Length,
                                         Scale = dgo.transform.localScale, Hidden = i < 2 };
            }
            // Both hands are empty while the discs turn.
            rig?.SetWeaponVisible(false);
            rig?.SetWeaponVisible(false);
            Current = o;
            o.Lay(0f);
            return o;
        }

        /// <summary>The ring's centre for the GAME (the player's origin) and for the PICTURE (the
        /// torso).</summary>
        Vector2 Centre => _pc.transform.position;
        Vector2 DrawnCentre => (Vector2)_pc.transform.position + Vector2.up * Tuning.Revolve.CentreUp;

        void Update()
        {
            if (_pc == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;

            if (_phase == Phase.Turn)
            {
                _age += dt;
                // Each revolution a disc may strike each enemy again.
                int rev = Mathf.FloorToInt(_age / Tuning.Revolve.Seconds);
                if (rev != _revolution)
                {
                    _revolution = rev;
                    foreach (var d in _discs) d?.Touched.Clear();
                }
                Lay(_age);
                Stop();
                Touch();
                if (_age >= Tuning.Revolve.MaxSeconds || Remaining == 0) GoHome();
            }
            else
            {
                _homeAge += dt;
                float u = Mathf.Clamp01(_homeAge / Tuning.Revolve.HomeSeconds);
                var hand = Hand();
                bool all = true;
                foreach (var d in _discs)
                {
                    if (d == null || !d.InRing) continue;
                    d.Pos = Vector2.Lerp(d.HomeFrom, hand, u * u);
                    d.Sr.transform.parent.position = d.Pos;
                    if (u < 1f) all = false;
                }
                if (all) Finish();
            }
        }

        /// <summary>The discs evenly round the one circle, a revolution every Seconds - eased in
        /// over the first quarter turn so they leave the hands rather than snapping round.</summary>
        void Lay(float age)
        {
            float ease = Tuning.Revolve.Seconds * 0.25f;
            float turn = 360f / Tuning.Revolve.Seconds
                         * (age < ease ? age * age / (2f * ease) : age - ease * 0.5f);
            var c = Centre;
            var dc = DrawnCentre;
            foreach (var d in _discs)
            {
                if (d == null || !d.InRing) continue;
                float a = (_startAngle + d.Offset + turn) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                d.Ground = c + new Vector2(cos, sin) * Tuning.Revolve.Radius;
                d.Pos = dc + new Vector2(cos, sin * Tuning.Revolve.Squash) * Tuning.Revolve.Radius;
                var t = d.Sr.transform.parent;
                t.position = d.Pos;
                t.rotation = Quaternion.Euler(0f, 0f, _age * 900f);
                // The near half (sin < 0, toward the camera) passes IN FRONT of the body and a
                // little larger; the far half behind it and a little smaller.
                t.localScale = d.Scale * (1f - sin * Tuning.Revolve.DepthScale);
                d.Sr.sortingOrder = sin < 0f ? SortingOrders.Fx : SortingOrders.GroundDecal + 3;
            }
        }

        /// <summary>Every enemy bolt and mortar shell inside the ring: gone, in a puff of smoke.</summary>
        void Stop()
        {
            var c = Centre;
            float r = Tuning.Revolve.Radius + Tuning.Revolve.StopMargin;
            foreach (var p in FindObjectsByType<EnemyProjectile>())
            {
                if (p == null || !p.Hostile) continue;
                if (((Vector2)p.transform.position - c).sqrMagnitude > r * r) continue;
                SmokePuff.At(p.transform.position);
                Destroy(p.gameObject);
            }
            foreach (var s in FindObjectsByType<Enemies.MortarShell>())
            {
                if (s == null || !s.Hostile) continue;
                if ((s.DrawnPosition - c).sqrMagnitude > r * r) continue;
                SmokePuff.At(s.DrawnPosition);
                Destroy(s.gameObject);
            }
        }

        static readonly Collider2D[] _overlap = new Collider2D[16];

        /// <summary>A disc running into an enemy hits it - once per disc per enemy.</summary>
        void Touch()
        {
            foreach (var d in _discs)
            {
                if (d == null || !d.InRing) continue;
                int n = Physics2D.OverlapCircleNonAlloc(d.Ground, Tuning.Revolve.TouchRadius, _overlap);
                for (int i = 0; i < n; i++)
                {
                    var col = _overlap[i];
                    if (col == null) continue;
                    var hp = col.GetComponentInParent<Health>();
                    if (hp == null || hp.IsDead || d.Touched.Contains(hp)) continue;
                    if (col.GetComponentInParent<Enemies.EnemyController>() == null
                        && col.GetComponentInParent<Bosses.Boss>() == null) continue;
                    d.Touched.Add(hp);
                    _onTouch?.Invoke(hp);
                }
            }
        }

        /// <summary>
        /// A basic attack while the ring turns: the disc in the ring nearest the facing leaves it,
        /// handed to <paramref name="throwIt"/> (an ordinary basic throw) and placed where it was
        /// in the ring. False when the ring has none left.
        /// </summary>
        public bool Knock(Vector2 facing, Func<ThrownDisc> throwIt)
        {
            if (_phase != Phase.Turn) return false;
            Disc pick = null;
            float best = float.MinValue;
            var c = Centre;
            foreach (var d in _discs)
            {
                if (d == null || !d.InRing) continue;
                float dot = Vector2.Dot((d.Ground - c).normalized, facing);
                if (dot > best) { best = dot; pick = d; }
            }
            if (pick == null) return false;

            pick.InRing = false;
            pick.Sr.enabled = false;
            // A hand's own disc lets go of its hold on the hand; the throw takes it again at once.
            if (pick.Hidden)
            {
                _pc.Rig?.SetWeaponVisible(true);
                pick.Hidden = false;
            }
            var thrown = throwIt?.Invoke();
            if (thrown != null) thrown.transform.position = pick.Pos;
            return true;
        }

        void GoHome()
        {
            _phase = Phase.Home;
            _homeAge = 0f;
            foreach (var d in _discs) if (d != null && d.InRing) d.HomeFrom = d.Pos;
        }

        Vector2 Hand()
        {
            var rig = _pc != null ? _pc.Rig : null;
            if (rig != null && rig.Transform != null && rig.TryGetSwordHand(out var local))
                return rig.Transform.TransformPoint(local);
            return Centre;
        }

        /// <summary>Called when a weapon art fires mid-ring: what is left goes straight home.</summary>
        public void EndNow() => Finish();

        void Finish()
        {
            foreach (var d in _discs)
            {
                if (d == null || !d.Hidden) continue;
                d.Hidden = false;
                _pc?.Rig?.SetWeaponVisible(true);
            }
            if (Current == this) Current = null;
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy()
        {
            foreach (var d in _discs)
            {
                if (d == null || !d.Hidden) continue;
                d.Hidden = false;
                _pc?.Rig?.SetWeaponVisible(true);
            }
            if (Current == this) Current = null;
        }
    }
}
