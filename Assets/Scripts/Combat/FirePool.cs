using UnityEngine;
using Convergence.Core;
using F = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// A patch of burning ground: ticks damage and re-applies burn to anything standing in it,
    /// then fades and cleans itself up.
    ///
    /// Built at runtime like everything else in this project, so it needs no prefab and survives
    /// the scene being regenerated from the CLI.
    ///
    /// Deliberately a LINGERING AREA rather than a burst. Fire's whole identity is sustained
    /// pressure - stacks that decay unless you keep the offence up - and a pool asks the same
    /// question of the player's positioning that stacks ask of their aggression: the damage is
    /// there if you can keep the fight standing in it.
    /// </summary>
    public class FirePool : MonoBehaviour
    {
        const float Ppu = Art.FloorArt.PixelsPerUnit;

        float _damagePerTick;
        float _burnScale = 1f;
        float _tickInterval;
        float _radius;
        float _remaining;
        float _life;
        float _tick;
        float _delay, _lit, _redraw;
        GameObject _owner;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h, _cx, _cy, _seed;

        /// <summary>
        /// Drop a pool at a world position.
        ///
        /// <paramref name="owner"/> is excluded from the damage: fire's eruption goes off around
        /// the player's own feet, and a pool that cooked its caster would make the payoff for
        /// reaching max heat a self-inflicted wound.
        /// </summary>
        /// <param name="burnScale">What the burn a pool leaves on its victims is multiplied by -
        /// the player's Burn Power (the mastery board), so an Erupt pool burns like every other
        /// burn they apply. The pool's own ground damage is not a burn and is left alone.</param>
        public static FirePool Spawn(Vector2 pos, float radius, float damagePerSecond,
                                     float lifetime, GameObject owner, Transform parent = null,
                                     float burnScale = 1f, float delay = 0f)
        {
            var go = new GameObject("firepool");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var pool = go.AddComponent<FirePool>();
            pool._radius = Mathf.Max(0.1f, radius);
            pool._tickInterval = 0.35f;
            pool._damagePerTick = damagePerSecond * pool._tickInterval;
            pool._life = Mathf.Max(0.1f, lifetime);
            pool._remaining = pool._life;
            pool._owner = owner;
            pool._burnScale = burnScale;
            pool._tick = pool._tickInterval;

            pool._delay = Mathf.Max(0f, delay);
            pool._seed = Mathf.Abs(Mathf.RoundToInt(pos.x * 37.5f) * 73 + Mathf.RoundToInt(pos.y * 37.5f) * 151) % 997;

            // On the ground, over the pits and under every body: a pool is something fought OVER,
            // so it must never hide the enemies standing in it.
            pool._sr = go.AddComponent<SpriteRenderer>();
            pool._sr.sortingOrder = SortingOrders.GroundLasting;
            pool._sr.enabled = false;

            return pool;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Waiting for Erupt's ring of fire to reach it: nothing burns, nothing is drawn.
            if (_delay > 0f)
            {
                _delay -= dt;
                if (_delay > 0f) return;
                FightMarks.Scorch(transform.position, _radius);   // the ground it burns stays burnt
            }
            _lit += dt;
            _remaining -= dt;

            if (_remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Fade out over the last stretch so the pool announces its own expiry rather than
            // vanishing under someone who was standing in it on purpose.
            _redraw -= dt;
            if (_redraw <= 0f)
            {
                _redraw = 1f / F.PoolFps;
                float fade = Mathf.Min(1f, _remaining / _life * 3f) * Mathf.Clamp01(_lit / F.PoolInSeconds);
                DrawPool(fade);
            }

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = _tickInterval;

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, _radius))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                Player.PlayerController.ReleaseHitFrom(_owner, hp, new DamageInfo(_damagePerTick, ElementType.Fire, _owner) { Tick = true });
                StatusEffects.Get(hp.gameObject).ApplyBurn(_damagePerTick * 0.5f * _burnScale, 1.5f, _owner);
            }
        }

        /// <summary>
        /// The pool as texels on the floor's grid (the user's pick with Erupt's sigil blast,
        /// 2026-10-25): a ragged patch of burning ground - deep reds mottled and shifting, a lit
        /// orange edge - with small flames standing on it. Its edge never passes the radius that
        /// burns by more than a texel or two.
        /// </summary>
        void DrawPool(float fade)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(_radius * Ppu));
            if (_px == null)
            {
                int e = Mathf.CeilToInt(r * 1.15f) + 1;
                _w = e * 2 + 1; _cx = e; _cy = e; _h = e * 2 + F.PoolFlameMax + 2;
                _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[_w * _h];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), new Vector2(_cx / (float)_w, _cy / (float)_h), Ppu, 0, SpriteMeshType.FullRect);
                // On the floor's grid: the centre texel on a whole texel.
                var p = transform.position;
                transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, p.z);
            }
            System.Array.Clear(_px, 0, _px.Length);
            float t = Time.time;
            byte a = (byte)(230f * Mathf.Clamp01(fade));
            for (int dy = -_cy; dy <= _cy; dy++)
                for (int dx = -_cx; dx <= _cx; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float edge = r * (0.82f + FireArt.Noise(Mathf.Cos(ang) * 2.2f + _seed, Mathf.Sin(ang) * 2.2f + _seed) * 0.33f);
                    if (d > edge) continue;
                    float v = FireArt.Noise(dx * 0.3f + t * 0.8f, dy * 0.4f + _seed);
                    var c = d > edge - F.PoolEdgeTexels ? FireArt.At(0.25f + v * 0.3f) : FireArt.At(0.62f + v * 0.38f);
                    c.a = a;
                    Put(_cx + dx, _cy + dy, c);
                }
            int flames = Mathf.Max(3, Mathf.RoundToInt(r * F.PoolFlamesPerTexel));
            for (int k = 0; k < flames; k++)
            {
                float ang = FireArt.Hash(k, _seed, 7) * Mathf.PI * 2f, rr = Mathf.Sqrt(FireArt.Hash(k, _seed, 8)) * r * 0.8f;
                int x = _cx + Mathf.RoundToInt(Mathf.Cos(ang) * rr), y = _cy + Mathf.RoundToInt(Mathf.Sin(ang) * rr);
                int hgt = F.PoolFlameMin + Mathf.FloorToInt(FireArt.Hash(k, _seed, 9) * (F.PoolFlameMax - F.PoolFlameMin + 1));
                for (int i = 0; i < hgt; i++)
                {
                    float v = FireArt.Noise(k * 1.7f + _seed, (i - t * 14f) * 0.45f);
                    if (v <= 0.3f + i / (float)hgt * 0.55f) continue;
                    var c = FireArt.At(0.12f + i / (float)hgt * 0.7f);
                    c.a = a;
                    Put(x, y + i, c);
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply();
            _sr.enabled = true;
        }

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
