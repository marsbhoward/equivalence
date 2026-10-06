using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    /// <summary>
    /// One stone set into a puzzle floor, pressed by standing on it. Every puzzle is built from
    /// these, so a stone looks and answers the same whichever puzzle it is in.
    ///
    /// A stone is told apart by its MARK as well as its colour (a glyph, or a count of pips) - the
    /// same rule the weapon classes live by: colour is the half that dies for a colourblind player.
    ///
    /// Pressed on the step ONTO it (an edge), never while stood on - standing still must not keep
    /// answering. Cached state is [SerializeField] and non-readonly (see "Domain reload traps").
    /// </summary>
    public class PuzzlePlate : MonoBehaviour
    {
        public enum Look { Dim, Lit, Dead, Wrong }

        [SerializeField] SpriteRenderer _base, _rim, _glyph, _glow;
        [SerializeField] List<SpriteRenderer> _pips = new();
        [SerializeField] Color _tint;
        [SerializeField] float _radius;
        [SerializeField] bool _inside;
        [SerializeField] Look _look;
        float _pulse;

        public Look Current => _look;
        public Color Tint => _tint;

        static readonly Color Stone = new(0.17f, 0.17f, 0.21f);
        static readonly Color DeadGrey = new(0.26f, 0.26f, 0.29f);
        static readonly Color WrongRed = new(1f, 0.25f, 0.2f);

        const int Order = SortingOrders.GroundDecal;

        public static PuzzlePlate Make(Transform parent, Vector2 at, float radius, Color tint,
                                       Sprite glyph = null, int pips = 0)
        {
            var go = new GameObject("plate");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var p = go.AddComponent<PuzzlePlate>();
            p._tint = tint;
            p._radius = radius;

            p._glow = Quad(go.transform, "glow", radius * 4.4f, Spr.Glow, Order);
            p._base = Quad(go.transform, "base", radius * 2f, Spr.Circle, Order + 1);
            p._rim = Quad(go.transform, "rim", radius * 2.1f, Spr.ThinRing, Order + 2);
            if (glyph != null) p._glyph = Quad(go.transform, "glyph", radius * 1.15f, glyph, Order + 3);

            // Pips on a small ring, the first at the top - a count reads at a glance where a
            // shape has to be learned.
            for (int i = 0; i < pips; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / pips;
                float r = pips == 1 ? 0f : radius * 0.42f;
                var pip = Quad(go.transform, $"pip{i}", radius * 0.26f, Spr.Circle, Order + 3);
                pip.transform.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                p._pips.Add(pip);
            }
            p.Apply(Look.Dim);
            return p;
        }

        /// <summary>True on the frame the player steps ONTO this stone.</summary>
        public bool Entered(Vector2 player)
        {
            bool inside = (player - (Vector2)transform.position).sqrMagnitude <= _radius * _radius;
            bool edge = inside && !_inside;
            _inside = inside;
            return edge;
        }

        public void Set(Look look) { _look = look; _pulse = 0f; Apply(look); }

        /// <summary>Lit for a moment, then back to whatever it was.</summary>
        public void Pulse(float seconds) { _pulse = seconds; Apply(Look.Lit); }

        void Update()
        {
            if (_pulse <= 0f) return;
            _pulse -= Time.deltaTime;
            if (_pulse <= 0f) Apply(_look);
        }

        void Apply(Look look)
        {
            Color rim, mark, face; float glow;
            switch (look)
            {
                case Look.Lit:
                    rim = _tint; mark = Color.Lerp(_tint, Color.white, 0.35f);
                    face = Color.Lerp(Stone, _tint, 0.28f); glow = 0.5f; break;
                case Look.Dead:
                    rim = DeadGrey; mark = new Color(DeadGrey.r, DeadGrey.g, DeadGrey.b, 0.5f);
                    face = Stone * 0.8f; glow = 0f; break;
                case Look.Wrong:
                    rim = WrongRed; mark = WrongRed;
                    face = Color.Lerp(Stone, WrongRed, 0.25f); glow = 0.45f; break;
                default:
                    rim = new Color(_tint.r * 0.55f, _tint.g * 0.55f, _tint.b * 0.55f, 0.85f);
                    mark = new Color(_tint.r, _tint.g, _tint.b, 0.55f);
                    face = Stone; glow = 0f; break;
            }
            face.a = 1f;
            var g = look == Look.Wrong ? WrongRed : _tint;
            if (_base) _base.color = face;
            if (_rim) _rim.color = rim;
            if (_glyph) _glyph.color = mark;
            foreach (var pip in _pips) if (pip) pip.color = mark;
            if (_glow) _glow.color = new Color(g.r, g.g, g.b, glow);
        }

        static SpriteRenderer Quad(Transform parent, string name, float size, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
