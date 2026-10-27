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
        [SerializeField] HubFixture _art;
        [SerializeField] SpriteRenderer _sketch;
        float _lit;
        bool _focused;


        /// <summary>The oak map table (the user's pick, group 6): a heavy table on turned legs, parchment
        /// across its top, the board's own sketch laid on it - drawn flat, foreshortened, as a page
        /// lying on a table is seen at this angle.</summary>
        public static GridTable Build(Transform parent, Vector2 centre, float width, float depth)
        {
            var go = new GameObject("grid-table");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var t = go.AddComponent<GridTable>();
            int w = Mathf.RoundToInt(width * HubArt.Ppu), d = Mathf.RoundToInt(depth * HubArt.Ppu * 0.6f);
            float foot = -depth * 0.5f;
            t._art = HubFixture.Attach(go, HubArt.MapTable(w, d), new Vector2(0f, foot));

            // The sketch on the parchment, squashed to lie flat on the top.
            var sk = new GameObject("sketch");
            sk.transform.SetParent(t._art.Art.transform, false);
            sk.transform.localPosition = new Vector3(0f, (14f + d * 0.5f) / HubArt.Ppu, 0f);
            t._sketch = sk.AddComponent<SpriteRenderer>();
            t._sketch.sprite = Progression.GridSketch.Sheet;
            t._sketch.sortingOrder = 3;
            var native = t._sketch.sprite != null ? (Vector2)t._sketch.sprite.bounds.size : Vector2.one;
            float fitW = (w - 10) / HubArt.Ppu, fitH = (d - 5) / HubArt.Ppu;
            sk.transform.localScale = new Vector3(fitW / Mathf.Max(0.01f, native.x), fitH / Mathf.Max(0.01f, native.y), 1f);

            DepthSorted.Attach(go, foot, false, t._art.Art, t._art.RimRenderer, t._sketch);
            return t;
        }

        public void SetFocus(bool on) { _focused = on; if (_art != null) _art.SetFocus(on); }

        void Update()
        {
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            // Only the page lights - it is what you are about to read.
            if (_sketch != null) _sketch.color = Color.Lerp(new Color(0.8f, 0.78f, 0.72f), Color.white, _lit);
        }
    }
}
