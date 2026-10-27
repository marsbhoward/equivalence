using UnityEngine;
using Convergence.Core;
using F = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// A spinning knot of wind: pulls whatever is caught in it toward its own centre and damages
    /// harder the closer a body sits to that centre, for as long as it keeps spinning.
    ///
    /// Built at runtime like everything else in this project - no prefab, so it survives the
    /// scene being regenerated from the CLI.
    ///
    /// Modelled directly on <see cref="FirePool"/>'s own shape (spawn, tick, fade, self-destroy),
    /// with the two differences that are the whole idea: damage is weighted by DISTANCE rather
    /// than flat across the radius, and every tick also pulls - a pool is something you fight
    /// standing in, a vortex is something that keeps dragging you back to the worst part of it.
    /// </summary>
    public class AirVortex : MonoBehaviour
    {
        float _radius, _pullForce, _damageNearPerTick, _damageFarPerTick, _tickInterval;
        float _tick, _remaining, _life;
        GameObject _owner;

        const float Ppu = Art.FloorArt.PixelsPerUnit;

        // The dust devil (the user's pick, 2026-10-25): a standing funnel sorted by depth with the
        // bodies, and grit on the ground drawn in across the pull's radius.
        SpriteRenderer _funnel, _ground;
        Texture2D _fTex, _gTex;
        Color32[] _fPx, _gPx;
        int _fw, _fh, _gw, _seed;

        /// <summary>
        /// Drop a vortex at a world position.
        ///
        /// <paramref name="owner"/> is excluded from the pull and the damage, the same reason
        /// <see cref="FirePool.Spawn"/> excludes it - these land around the player's own feet, and
        /// a vortex that sucked its caster in would make reaching the second ability a way to hurt
        /// yourself.
        /// </summary>
        public static AirVortex Spawn(Vector2 pos, float radius, float pullForce,
                                      float damageNearPerSecond, float damageFarPerSecond,
                                      float lifetime, GameObject owner, Transform parent = null)
        {
            var go = new GameObject("airvortex");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var v = go.AddComponent<AirVortex>();
            v._radius = Mathf.Max(0.1f, radius);
            v._pullForce = pullForce;
            v._tickInterval = 0.2f;
            v._damageNearPerTick = damageNearPerSecond * v._tickInterval;
            v._damageFarPerTick = damageFarPerSecond * v._tickInterval;
            v._life = Mathf.Max(0.1f, lifetime);
            v._remaining = v._life;
            v._owner = owner;
            v._tick = v._tickInterval;

            v._seed = Mathf.Abs(Mathf.RoundToInt(pos.x * 37.5f) * 91 + Mathf.RoundToInt(pos.y * 37.5f) * 57) % 997;
            v.Build();
            return v;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _remaining -= dt;

            if (_remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Grows up out of the floor, and dies back down over the last stretch - the same tell
            // FirePool uses, so it announces its own expiry instead of vanishing on whoever it
            // just dragged into the middle.
            float open = Mathf.Clamp01((_life - _remaining) / F.DevilInSeconds)
                       * Mathf.Min(1f, Mathf.Clamp01(_remaining / _life) * 3f);
            Draw(open);

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = _tickInterval;

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, _radius))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                var delta = (Vector2)col.transform.position - (Vector2)transform.position;
                float dist = delta.magnitude;

                // 1 at the centre, 0 at the rim - damage and pull both key off this, so a body
                // dragged closer keeps paying more the whole time it is caught.
                float inner = Mathf.Clamp01(1f - dist / _radius);
                float dmg = Mathf.Lerp(_damageFarPerTick, _damageNearPerTick, inner);
                var pull = dist > 0.0001f ? -(delta / dist) : Vector2.zero;

                Player.PlayerController.ReleaseHitFrom(_owner, hp, new DamageInfo(dmg, ElementType.Air, _owner)
                {
                    Knockback = pull * _pullForce,

                    // The whole point of a vortex is to move what it catches - the same opt-in
                    // Undertow's own pull already uses, and for the same reason: an ordinary enemy
                    // is Immovable by default, and Displaces is what overrides that. An elite's
                    // Anchored flag still refuses it, exactly as it refuses Undertow.
                    Displaces = true,
                });
            }
        }

        void Build()
        {
            int half = Mathf.CeilToInt(F.DevilTopWidth * Ppu * 0.5f) + 4;
            _fw = half * 2 + 1; _fh = Mathf.CeilToInt(F.DevilHeight * Ppu) + 3;
            _funnel = Canvas("funnel", _fw, _fh, new Vector2(half / (float)_fw, 1f / _fh), out _fTex, out _fPx);
            DepthSorted.Attach(_funnel.gameObject, 0f, false, _funnel);

            int g = Mathf.CeilToInt(_radius * Ppu) + 2;
            _gw = g * 2 + 1;
            _ground = Canvas("ground", _gw, _gw, new Vector2(0.5f, 0.5f), out _gTex, out _gPx);
            _ground.sortingOrder = SortingOrders.GroundLasting;

            // The centre on a whole floor texel.
            var p = transform.position;
            transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, p.z);
        }

        SpriteRenderer Canvas(string name, int w, int h, Vector2 pivot, out Texture2D tex, out Color32[] px)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            px = new Color32[w * h];
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            return sr;
        }

        void Draw(float open)
        {
            if (_fPx == null || _gPx == null) return;
            float t = Time.time;
            System.Array.Clear(_fPx, 0, _fPx.Length);
            System.Array.Clear(_gPx, 0, _gPx.Length);

            // The funnel: four strands winding up a cone pinched at the floor, leaning as it turns.
            int cx = _fw / 2, h = Mathf.RoundToInt((_fh - 3) * open);
            float top = F.DevilTopWidth * Ppu * 0.5f;
            for (int i = 0; i < h; i++)
            {
                float u = i / (float)Mathf.Max(1, h);
                float w = 1.5f + u * top, lean = Mathf.Sin(t * 2f + _seed) * i * 0.08f;
                for (int k = 0; k < 4; k++)
                {
                    float a = t * F.DevilSpin + i * 0.3f + k * 1.57f + _seed;
                    bool front = Mathf.Sin(a) > 0f;
                    var c = ReleaseCanvas.At(F.Air, front ? 0.05f + u * 0.3f : 0.4f + u * 0.3f);
                    c.a = (byte)(front ? 230 : 140);
                    int x = cx + Mathf.RoundToInt(Mathf.Cos(a) * w + lean);
                    PutF(x, i + 1, c);
                    if (front) PutF(x + (Mathf.Cos(a) > 0f ? -1 : 1), i + 1, c);   // the near strands two texels wide
                }
            }
            // Dust kicked up round its foot.
            for (int k = 0; k < 5; k++)
            {
                float life = 0.6f, age = Mathf.Repeat(t + FireArt.Hash(k, _seed) * life, life), u = age / life;
                var c = ReleaseCanvas.At(F.Stone, 0.25f + u * 0.4f);
                c.a = (byte)(200f * open * (1f - u));
                PutF(cx + Mathf.RoundToInt((FireArt.Hash(k, _seed, 2) - 0.5f) * 12f * (0.4f + u)), 1 + Mathf.RoundToInt(u * 5f), c);
            }

            // The ground: a dark smudge under its foot, and grit drawn in across the radius.
            int g = _gw / 2;
            for (int dy = -5; dy <= 5; dy++)
                for (int dx = -5; dx <= 5; dx++)
                    if (dx * dx + dy * dy * 4 <= 25) PutG(g + dx, g + dy, new Color32(11, 9, 12, (byte)(90 * open)));
            float r = _radius * Ppu;
            for (int k = 0; k < F.DevilGrit; k++)
            {
                float P = 0.7f, age = Mathf.Repeat(t + FireArt.Hash(k, _seed, 3) * P, P), u = age / P;
                float a = FireArt.Hash(k, _seed, 4) * Mathf.PI * 2f - u * 4f, rr = r * (1f - u);
                var c = ReleaseCanvas.At(F.Stone, 0.3f);
                c.a = (byte)(220f * open);
                PutG(g + Mathf.RoundToInt(Mathf.Cos(a) * rr), g + Mathf.RoundToInt(Mathf.Sin(a) * rr), c);
            }
            _fTex.SetPixels32(_fPx); _fTex.Apply();
            _gTex.SetPixels32(_gPx); _gTex.Apply();
        }

        void PutF(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < _fw && y < _fh) _fPx[y * _fw + x] = c; }
        void PutG(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < _gw && y < _gw) _gPx[y * _gw + x] = c; }

        void OnDestroy()
        {
            if (_funnel != null && _funnel.sprite != null) Destroy(_funnel.sprite);
            if (_ground != null && _ground.sprite != null) Destroy(_ground.sprite);
            if (_fTex != null) Destroy(_fTex);
            if (_gTex != null) Destroy(_gTex);
        }
    }
}
