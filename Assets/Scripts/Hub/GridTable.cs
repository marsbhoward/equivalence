using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// A table with the sphere grid drawn on a sheet of paper.
    ///
    /// The mastery board used to be reachable only by knowing that [M] does something. That is
    /// fine for a prototype and wrong for a hub: the room is supposed to be where the game's
    /// systems live as PLACES, so the board gets a table and the paper on it is a real picture of
    /// the real grid - see <see cref="Progression.GridSketch"/>.
    /// </summary>
    public class GridTable : MonoBehaviour
    {
        SpriteRenderer _paper, _sketch, _edge;
        float _lit;
        bool _focused;

        static readonly Color Wood = new(0.32f, 0.23f, 0.16f);
        static readonly Color Paper = new(0.84f, 0.78f, 0.64f);

        public static GridTable Build(Transform parent, Vector2 centre, float width, float depth)
        {
            var go = new GameObject("grid-table");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var t = go.AddComponent<GridTable>();

            // Legs/apron, low and slightly narrower than the top - every other fixture in the
            // room reads as standing up because its parts are stacked vertically (the crate's lid
            // over its body, the forge's hearth over its base); this table used to draw its top,
            // inlay and paper all at the same local (0,0), which is exactly what a shape painted
            // flat on the floor and viewed from directly overhead looks like. The apron gives it
            // a base to stand on and the top (below) sits up off of it.
            float legY = -depth * 0.5f + 0.11f;
            var legs = Quad(go.transform, "legs", width * 0.82f, 0.22f,
                            new Color(0.20f, 0.14f, 0.09f), SortingOrders.FloorDetail + 1,
                            new Vector2(0f, legY));

            // Table top, offset UP off the apron rather than sharing its centre - the same
            // vertical stacking convention every other fixture in the room already uses.
            const float topOffset = 0.14f;
            t._edge = Quad(go.transform, "top", width, depth,
                           Wood, SortingOrders.FloorDetail + 2, new Vector2(0f, topOffset));
            var inlay = Quad(go.transform, "inlay", width - 0.14f, depth - 0.14f,
                 new Color(0.38f, 0.28f, 0.19f), SortingOrders.FloorDetail + 3, new Vector2(0f, topOffset));

            // The sheet, laid at a slight angle - a page squared to the table looks printed on it.
            float sheet = Mathf.Min(width, depth) * 0.78f;
            t._paper = Quad(go.transform, "paper", sheet, sheet,
                            Paper, SortingOrders.FloorDetail + 4, new Vector2(0f, topOffset));
            t._paper.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);

            var sk = new GameObject("sketch");
            sk.transform.SetParent(t._paper.transform, false);
            sk.transform.localScale = Vector3.one * 0.86f;
            t._sketch = sk.AddComponent<SpriteRenderer>();
            t._sketch.sprite = Progression.GridSketch.Sheet;
            t._sketch.sortingOrder = SortingOrders.FloorDetail + 5;

            // Movable like every other fixture in the room, so it needs the same dynamic
            // y-sort the others already get - without it the table would always draw at a
            // fixed depth regardless of where it's dragged to, and stop correctly ducking
            // behind (or in front of) the player walking near it.
            DepthSorted.Attach(go, legY - 0.11f, false, legs, t._edge, inlay, t._paper, t._sketch);

            t.Apply(0f);
            return t;
        }

        public void SetFocus(bool on) => _focused = on;

        void Update()
        {
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            // Only the PAPER lights. Brightening the whole table would say "this table is
            // interactive"; brightening the page says "this page is what you are about to read".
            _paper.color = Color.Lerp(Paper, Color.Lerp(Paper, Color.white, 0.45f), lit);
            _edge.color = Color.Lerp(Wood, Wood * 1.3f, lit);
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Color color,
                                   int order, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
