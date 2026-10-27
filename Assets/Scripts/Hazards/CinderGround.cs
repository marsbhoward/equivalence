using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A patch of burning ground shed by a CINDERED elite (Enemies.EliteAffix, Tuning.EliteAffixes):
    /// lands dim as a warning, burns for a few seconds, fades. It hurts ONLY THE PLAYER - FirePool's
    /// look turned the other way (a Fire player's pools hurt enemies only). A tick, so it makes no
    /// hit sound (DamageInfo.Tick) and goes through heat like every enemy blow.
    /// </summary>
    public class CinderGround : MonoBehaviour
    {
        const float TickInterval = 0.35f;
        /// <summary>For the picture: how far it has armed (0..1, 0 once lit) and how strongly it
        /// burns (0 while arming, fading at the end).</summary>
        internal float Arming, Burning;
        internal float Radius => _radius;

        // Plain serialised fields - see the domain-reload notes in CLAUDE.md.
        [SerializeField] float _radius, _damagePerTick, _age, _tick;
        [SerializeField] Transform _target;
        [SerializeField] GameObject _owner;
        [SerializeField] CinderPicture _pic;
        [SerializeField] Art.Lighting.Glow _glow;

        public static CinderGround Spawn(Vector2 at, Transform parent, Transform target, GameObject owner, float damagePerSecond)
        {
            var go = new GameObject("cinders");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;

            var g = go.AddComponent<CinderGround>();
            g._radius = Tuning.EliteAffixes.CinderRadius;
            g._damagePerTick = damagePerSecond * TickInterval;
            g._target = target;
            g._owner = owner;
            g._tick = 0f;

            // On the ground over the pits, under every body (the user's pick, group 4 - "flame carpet").
            int r = Mathf.CeilToInt(g._radius * TexelFx.Body) + 2;
            g._pic = TexelPicture.Attach<CinderPicture>(go.transform, "picture", Vector2.zero, r, r, r + 5, SortingOrders.GroundLasting, clip: true);

            g._glow = go.AddComponent<Art.Lighting.Glow>();
            g._glow.Radius = g._radius * 2.2f;
            g._glow.Intensity = 0f;
            g._glow.Color = new Color(1f, 0.45f, 0.15f);
            g._glow.Flicker = 0.3f;
            return g;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float arm = Tuning.EliteAffixes.CinderArmSeconds, life = arm + Tuning.EliteAffixes.CinderSeconds;
            if (_age >= life) { Destroy(gameObject); return; }

            bool armed = _age >= arm;
            float fade = Mathf.Min(1f, (life - _age) / (Tuning.EliteAffixes.CinderSeconds * 0.3f));
            Arming = armed ? 0f : _age / arm;
            Burning = armed ? fade : 0f;
            if (_glow != null) _glow.Intensity = armed ? 0.55f * fade : 0f;

            if (!armed || _target == null) return;
            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            if (Vector2.Distance(_target.position, transform.position) > _radius + 0.15f) return;
            _tick = TickInterval;
            var hp = _target.GetComponent<Health>();
            if (hp == null || hp.IsDead) return;
            hp.Take(new DamageInfo(_damagePerTick, ElementType.Fire, _owner) { Tick = true });
        }
    }

    /// <summary>
    /// Cinder ground's picture (the user's pick, group 4 - "flame carpet"): the patch burning with
    /// low flames inside a HARD RED EDGE - the rule for ground that hurts the player. A Fire
    /// player's own coals and Erupt's pools are ragged with no edge. While it arms only the edge
    /// shows, dark and fading in.
    /// </summary>
    public class CinderPicture : TexelPicture
    {
        [SerializeField] CinderGround _g;

        protected override bool Draw()
        {
            if (_g == null) _g = GetComponentInParent<CinderGround>();
            if (_g == null) return false;
            float r = _g.Radius * Ppu, t = Age, burn = _g.Burning;
            if (burn > 0f)
            {
                Ring(0, 0, 0, r - 1.8f, (x, y, d, ang) =>
                {
                    Over(x, y, TexelFx.Danger[5], 0.7f * burn);
                    float v = FireArt.Noise(x * 0.5f, (y - t * 14f) * 0.45f);
                    if (v > 0.45f) Over(x, y, FireArt.At(1.1f - v), burn);
                });
                // Low tongues standing along the patch.
                for (int x = -Mathf.FloorToInt(r) + 2; x <= r - 2; x += 2)
                {
                    int foot = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, r * r - x * x))) - 1;
                    for (int i = 1; i < 5; i++)
                        if (FireArt.Noise(x * 0.5f + 3f, i - t * 12f) > 0.35f + i * 0.12f) Over(x, foot + i, FireArt.At(0.2f + i * 0.18f), burn);
                }
            }
            float edge = burn > 0f ? burn : _g.Arming * 0.6f;
            Ring(0, 0, r - 1.8f, r, (x, y, d, ang) => Over(x, y, burn > 0f && d <= r - 0.9f ? TexelFx.Danger[3] : TexelFx.Danger[4], edge));
            return true;
        }
    }
}
