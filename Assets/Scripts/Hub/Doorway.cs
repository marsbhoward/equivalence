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

        [SerializeField] HubFixture _art;
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

            // The raised portcullis (the user's pick, group 6): an iron-framed opening, black - it is
            // a way through - its gate drawn up into the top. On the wall's band like the sigil door.
            d._art = HubFixture.Attach(go, HubArt.Portcullis(Mathf.RoundToInt(halfWidth * HubArt.Ppu), Mathf.RoundToInt(height * HubArt.Ppu)),
                                       Vector2.zero, SortingOrders.GroundDecal + 2);
            return d;
        }

        public void SetFocus(bool on) => _focused = on;

        void Update()
        {
            if (_art != null) _art.SetFocus(_focused);
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
