using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Draws what <see cref="StatusEffects"/> is doing to a target.
    ///
    /// Burn, soak and stagger previously rendered nothing at all, which hit fire hardest: four of
    /// its five stack tiers are about burn, so the element's whole identity was happening with no
    /// sign of it on screen.
    ///
    /// Kept separate from the sim so authored VFX can replace it without touching the rules - the
    /// same seam ArtBinder gives the characters. It draws into its OWN child sprite rather than
    /// tinting the target's, because <see cref="Health"/>'s hit-flash already writes that colour
    /// every frame and the two would fight over it.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatusVisuals : MonoBehaviour
    {
        static readonly Color BurnTint    = new(1f,    0.42f, 0.10f);
        static readonly Color SoakTint    = new(0.28f, 0.60f, 1f);
        static readonly Color StaggerTint = new(0.78f, 0.66f, 0.40f);
        static readonly Color PetrifyTint = new(0.62f, 0.45f, 0.95f);

        StatusEffects _status;
        SpriteRenderer _halo;
        float _emberTimer;
        float _size = 1.1f;

        void Awake()
        {
            _status = GetComponent<StatusEffects>();

            var go = new GameObject("status.halo");
            go.transform.SetParent(transform, false);
            _halo = go.AddComponent<SpriteRenderer>();
            _halo.sprite = Spr.Circle;
            _halo.sortingOrder = SortingOrders.StatusOverlay;
            _halo.enabled = false;

            // Wrap whatever body this is - enemies are not all one size.
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == _halo) continue;
                _size = Mathf.Max(0.6f, sr.bounds.size.magnitude * 0.8f);
                break;
            }
            go.transform.localScale = Vector3.one * _size;
        }

        void Update()
        {
            if (_status == null) { _halo.enabled = false; return; }

            // Blend everything active rather than picking a winner: a soaked-and-burning target
            // would otherwise silently drop half its state, and those two stack on purpose.
            Color sum = Color.clear;
            int n = 0;
            float alpha = 0f;
            if (_status.Burning)   { sum += BurnTint;    n++; alpha = Mathf.Max(alpha, 0.50f); }
            if (_status.Soaked)    { sum += SoakTint;    n++; alpha = Mathf.Max(alpha, 0.38f); }
            if (_status.Staggered) { sum += StaggerTint; n++; alpha = Mathf.Max(alpha, 0.34f); }
            // The one status this project ever puts on the PLAYER, so it is also the one whose
            // absence would be worst - a silent root reads as the controls having broken.
            if (_status.Petrified) { sum += PetrifyTint; n++; alpha = Mathf.Max(alpha, 0.46f); }

            if (n == 0)
            {
                _halo.enabled = false;
                return;
            }

            // Burn flickers fast and unevenly; the others breathe. Motion separates these at a
            // glance far better than hue does, especially at this camera distance.
            //
            // Perlin output clusters hard around 0.5 - raw, it only covered 0.77..1.14, a wobble
            // rather than a flame. Stretching it away from the midpoint is what makes it read as
            // fire; the offset seeds each target so a burning crowd does not pulse in unison.
            float pulse;
            if (_status.Burning)
            {
                float noise = Mathf.PerlinNoise(Time.time * 9f, transform.position.x * 3.1f);
                noise = Mathf.Clamp01((noise - 0.5f) * 2.6f + 0.5f);
                pulse = 0.55f + noise * 0.85f;
            }
            else
            {
                pulse = 0.85f + Mathf.Sin(Time.time * 3.2f) * 0.15f;
            }

            var c = sum / n;
            c.a = alpha * pulse;
            _halo.color = c;
            _halo.transform.localScale = Vector3.one * _size * (1f + (pulse - 0.85f) * 0.12f);
            _halo.enabled = true;

            // Burn is the only status that deals damage, so it gets motion of its own rather than
            // just a tint - embers rising off the target while it ticks.
            if (_status.Burning)
            {
                _emberTimer -= Time.deltaTime;
                if (_emberTimer <= 0f)
                {
                    _emberTimer = 0.16f;
                    var p = transform.position + new Vector3(
                        Random.Range(-0.28f, 0.28f), Random.Range(-0.05f, 0.40f), 0f);
                    Spr.Flash(p, 0.16f, new Color(1f, 0.6f, 0.2f, 0.9f), 0.32f, false);
                }
            }
        }
    }
}
