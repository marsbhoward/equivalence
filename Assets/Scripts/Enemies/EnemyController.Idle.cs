using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;
using Convergence.Combat;

namespace Convergence.Enemies
{
    /// <summary>
    /// UNAWARE (the user's call, 2026-10-22): the bodies already in the room when the player
    /// arrives have not noticed them yet. They wander slowly near where they stood, stopping as if
    /// sniffing the air; a turret and a gargoyle hold still. Nothing is acted on - no telegraph,
    /// no attack, no roar - until one of these wakes it:
    ///
    ///   - the player within <c>NoticeRadius</c> with a clear line, or within <c>NoticeClose</c> at all;
    ///   - a hit;
    ///   - a packmate within <c>SpreadRadius</c> waking (a beat later);
    ///   - <c>MaxSeconds</c> passing - a floor never stalls on a pack that never looked up.
    ///
    /// Waking is MARKED: a "!" over the head (Combat-style overlay, never darkened), the kind's
    /// alert sound, and a short STARTLE in which it still does nothing. Only the first wave is
    /// placed unaware (GameBootstrap.BuildFight); bodies that trickle in later arrive alert.
    /// </summary>
    public partial class EnemyController
    {
        [SerializeField] bool _unaware;
        [SerializeField] float _unawareLeft, _startle, _wakeIn = -1f, _pause;
        [SerializeField] Vector2 _home, _wanderTo;
        System.Random _idleRng;

        public bool Unaware => _unaware;

        /// <summary>Place this body unaware - only as the floor's first wave comes in.</summary>
        public void BeginUnaware()
        {
            _unaware = true;
            _idleRng = new System.Random(GetEntityId().GetHashCode());
            _unawareLeft = Mathf.Lerp(Tuning.Idle.MaxSecondsMin, Tuning.Idle.MaxSecondsMax, (float)_idleRng.NextDouble());
            _home = transform.position;
            _wanderTo = _home;
            _pause = (float)_idleRng.NextDouble() * 1.5f;
        }

        /// <summary>The update gate: true while unaware or startled - nothing else runs.</summary>
        bool TickIdle()
        {
            if (_startle > 0f) { _startle -= Time.deltaTime; return true; }
            if (!_unaware) return false;

            if (_wakeIn >= 0f)
            {
                _wakeIn -= Time.deltaTime;
                if (_wakeIn < 0f) Wake(spread: false);
                return _unaware || _startle > 0f;
            }

            _unawareLeft -= Time.deltaTime;
            if (_unawareLeft <= 0f) { Wake(spread: true); return true; }

            if (_target != null)
            {
                Vector2 to = (Vector2)_target.position - (Vector2)transform.position;
                float d = to.magnitude;
                if (d <= Tuning.Idle.NoticeClose ||
                    (d <= Tuning.Idle.NoticeRadius &&
                     HazardQuery.Query(transform.position, _target.position, gameObject, _target.gameObject) != SightResult.Blocked))
                {
                    Wake(spread: true);
                    return true;
                }
            }
            return true;
        }

        /// <summary>Hit while unaware: awake at once.</summary>
        void WakeOnHurt(DamageInfo info)
        {
            if (_unaware && !info.Tick) Wake(spread: true);
        }

        void Wake(bool spread)
        {
            if (!_unaware) return;
            _unaware = false;
            _wakeIn = -1f;
            _startle = Tuning.Idle.StartleSeconds;
            AlertMark.Show(this);
            Audio.Sfx.Play(Kind switch
            {
                EnemyKind.Chaser => Audio.Sound.Growl,
                EnemyKind.Dasher => Audio.Sound.Snarl,
                _ => Audio.Sound.Alert,
            }, transform.position);

            if (!spread) return;
            foreach (var e in EnemyRegistry.All)
            {
                if (e == null || e == this || !e._unaware || e._wakeIn >= 0f) continue;
                if (((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude
                    > Tuning.Idle.SpreadRadius * Tuning.Idle.SpreadRadius) continue;
                e._wakeIn = Mathf.Lerp(Tuning.Idle.SpreadDelayMin, Tuning.Idle.SpreadDelayMax,
                                       (float)(e._idleRng ?? new System.Random(1)).NextDouble());
            }
        }

        /// <summary>The idle walk: to a point near home, a pause, another point. Statues stand.</summary>
        void IdleMove(Vector2 pos)
        {
            if (Kind is EnemyKind.Turret or EnemyKind.Gargoyle || _startle > 0f)
            {
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, 0.3f);
                return;
            }
            _idleRng ??= new System.Random(GetEntityId().GetHashCode());
            if (_pause > 0f)
            {
                _pause -= Time.fixedDeltaTime;
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, 0.3f);
                if (_pause <= 0f)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (float)_idleRng.NextDouble() * Mathf.PI * 2f, r = Tuning.Idle.WanderRadius * Mathf.Sqrt((float)_idleRng.NextDouble());
                        var p = _home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (Arena.OnFloor(p, 0.4f) && FloorPits.At(p) == null) { _wanderTo = p; break; }
                    }
                }
                return;
            }
            var to = _wanderTo - pos;
            if (to.magnitude < 0.15f)
            {
                _pause = Mathf.Lerp(Tuning.Idle.PauseMin, Tuning.Idle.PauseMax, (float)_idleRng.NextDouble());
                return;
            }
            Drive(to.normalized * MoveSpeed * Tuning.Idle.WanderSpeed, MoveSpeed * Tuning.Idle.WanderSpeed);
        }
    }

    /// <summary>
    /// The "!" over a body that has just noticed the player - texel-drawn at body density, in the
    /// overlay band (never darkened), following the body, popping in and fading out.
    /// </summary>
    public class AlertMark : MonoBehaviour
    {
        static Sprite _sprite;
        SpriteRenderer _sr;
        Transform _over;
        float _t;

        public static void Show(EnemyController e)
        {
            if (e == null) return;
            var go = new GameObject("alert");
            var m = go.AddComponent<AlertMark>();
            m._over = e.transform;
            m._sr = go.AddComponent<SpriteRenderer>();
            m._sr.sprite = Mark;
            m._sr.sortingOrder = SortingOrders.StatusOverlay + 30;
            m.Place();
        }

        static Sprite Mark
        {
            get
            {
                if (_sprite != null) return _sprite;
                const int W = 7, H = 15;
                var px = new Color[W * H];
                var ink = new Color(0.08f, 0.05f, 0.04f); var lit = new Color(1f, 0.86f, 0.32f); var hot = new Color(1f, 0.97f, 0.75f);
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool bar = x >= 2 && x <= 4 && y >= 5 && y <= 13;
                    bool dot = x >= 2 && x <= 4 && y >= 1 && y <= 3;
                    bool core = (bar || dot) && x == 3;
                    bool shape = bar || dot;
                    bool edge = !shape && (x >= 1 && x <= 5) && ((y >= 4 && y <= 14) || (y <= 4));
                    px[y * W + x] = core ? hot : shape ? lit : edge ? ink : Color.clear;
                }
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels(px);
                tex.Apply();
                _sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), Art.FloorArt.PixelsPerUnit);
                _sprite.hideFlags = HideFlags.HideAndDontSave;
                return _sprite;
            }
        }

        void Place()
        {
            if (_over == null) return;
            float top = _over.position.y + 0.6f;
            foreach (var r in _over.GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled && r.sprite != null && r.sortingOrder < SortingOrders.StatusOverlay) top = Mathf.Max(top, r.bounds.max.y);
            // a hop up as it appears, then it settles
            float hop = _t < 0.12f ? (0.12f - _t) * 1.2f : 0f;
            transform.position = new Vector3(_over.position.x, top + 0.12f + hop, 0f);
        }

        void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_over == null || _t > Tuning.Idle.MarkSeconds) { Destroy(gameObject); return; }
            Place();
            float fade = Mathf.Clamp01((Tuning.Idle.MarkSeconds - _t) / 0.25f);
            _sr.color = new Color(1f, 1f, 1f, fade);
        }
    }
}
