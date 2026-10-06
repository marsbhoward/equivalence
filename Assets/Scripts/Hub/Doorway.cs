using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// An open doorway in a wall face: a stone frame around a black entryway, and nothing else.
    ///
    /// No door, no light, no mark - deliberately the opposite of the sigil door. That one is SHUT
    /// and glows because what is behind it is a choice; this one is simply a way into the next
    /// room, and a black opening is the whole message. The same builder draws both sides of the
    /// armoury passage, so walking through reads as one doorway seen from either room.
    ///
    /// Drawn on the wall face only. Every wall stays solid - like the sigil door, it is used from
    /// the floor in front of it, and interacting is what takes the player through.
    /// </summary>
    public class Doorway : MonoBehaviour
    {
        static readonly Color Stone = new(0.30f, 0.31f, 0.37f);
        static readonly Color StoneLit = new(0.40f, 0.41f, 0.48f);
        static readonly Color Step = new(0.24f, 0.25f, 0.30f);
        static readonly Color Dark = new(0.012f, 0.012f, 0.018f);

        SpriteRenderer _frame;
        float _lit;
        bool _focused;

        /// <summary>Where the player stands to use it: on the floor, just in front of the wall.</summary>
        public Vector2 Anchor { get; private set; }

        /// <param name="x">Position along the wall.</param>
        /// <param name="wallY">The wall line - the doorway stands up the face from here.</param>
        public static Doorway Build(Transform parent, string name, float x, float wallY,
                                    float halfWidth, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, wallY, 0f);

            var d = go.AddComponent<Doorway>();
            d.Anchor = (Vector2)parent.TransformPoint(new Vector3(x, wallY - 0.45f, 0f));

            // Based on GroundDecal for the same reason the sigil door is - see its own note.
            d._frame = Quad(go.transform, "frame", halfWidth * 2f + 0.20f, height + 0.10f,
                            Stone, SortingOrders.GroundDecal + 1, new Vector2(0f, (height + 0.10f) * 0.5f));
            Quad(go.transform, "entry", halfWidth * 2f, height,
                 Dark, SortingOrders.GroundDecal + 2, new Vector2(0f, height * 0.5f));

            // A worn step on the floor at the foot of the opening - the one thing that says
            // "walk up to this" on a top-down floor, where the opening itself is up the wall.
            Quad(go.transform, "threshold", halfWidth * 2f + 0.12f, 0.12f,
                 Step, SortingOrders.GroundDecal + 1, new Vector2(0f, -0.06f));
            return d;
        }

        public void SetFocus(bool on) => _focused = on;

        void Update()
        {
            if (_frame == null) return;
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _frame.color = Color.Lerp(Stone, StoneLit, _lit);
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h,
                                   Color c, int order, Vector2 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
