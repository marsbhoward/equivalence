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
        /// <summary>THE LETTER (UI/SupportCard): sealed, on the sheet's corner after a first escape,
        /// its gold rim glinting until it is read.</summary>
        [SerializeField] SpriteRenderer _letter, _letterGlint;
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
            // Dressed in the character's table theme (group 8); the Cloister's is the oak map table. The
            // board's own sketch lies on whatever top the theme has, in the theme's ink.
            var theme = HubDress.Of(HubItem.Table);
            var top = HubThemeArt.Top(theme, w, d);
            t._art = HubFixture.Attach(go, HubThemeArt.Table(theme, w, d), new Vector2(0f, foot));

            var sk = new GameObject("sketch");
            sk.transform.SetParent(t._art.Art.transform, false);
            sk.transform.localPosition = new Vector3(top.Centre.x / HubArt.Ppu, top.Centre.y / HubArt.Ppu, 0f);
            t._sketch = sk.AddComponent<SpriteRenderer>();
            t._sketch.sprite = theme == HubTheme.Cloister ? Progression.GridSketch.Sheet : Progression.GridSketch.SheetIn(top.Ink);
            t._sketch.sortingOrder = 3;
            var native = t._sketch.sprite != null ? (Vector2)t._sketch.sprite.bounds.size : Vector2.one;
            float fitW = top.Size.x / HubArt.Ppu, fitH = top.Size.y / HubArt.Ppu;
            sk.transform.localScale = new Vector3(fitW / Mathf.Max(0.01f, native.x), fitH / Mathf.Max(0.01f, native.y), 1f);

            // The letter on the sheet's near right corner, hidden until there is one.
            var lt = new GameObject("letter");
            lt.transform.SetParent(t._art.Art.transform, false);
            lt.transform.localPosition = new Vector3((top.Centre.x + top.Size.x * 0.26f) / HubArt.Ppu,
                                                     (top.Centre.y - top.Size.y * 0.18f) / HubArt.Ppu, 0f);
            t._letterGlint = lt.AddComponent<SpriteRenderer>();
            t._letterGlint.sprite = LetterGlint;
            t._letterGlint.sortingOrder = 4;
            var face = new GameObject("face");
            face.transform.SetParent(lt.transform, false);
            t._letter = face.AddComponent<SpriteRenderer>();
            t._letter.sprite = Letter;
            t._letter.sortingOrder = 5;
            t.ShowLetter(false);

            DepthSorted.Attach(go, foot, false, t._art.Art, t._art.RimRenderer, t._sketch, t._letterGlint, t._letter);
            return t;
        }

        public void ShowLetter(bool on)
        {
            if (_letter != null) _letter.enabled = on;
            if (_letterGlint != null) _letterGlint.enabled = on;
        }

        // ---- the letter's art: 10 x 8 texels at the hub's density, its glint a ring one texel out
        const int LetterW = 10, LetterH = 8;
        static Sprite _letterSprite, _glintSprite;
        static Sprite Letter => _letterSprite != null ? _letterSprite : _letterSprite = DrawLetter(false);
        static Sprite LetterGlint => _glintSprite != null ? _glintSprite : _glintSprite = DrawLetter(true);

        static Sprite DrawLetter(bool glint)
        {
            int w = LetterW + 2, h = LetterH + 2;
            var px = new Color32[w * h];
            var paper = new Color32(232, 220, 192, 255);
            var shade = new Color32(196, 180, 148, 255);
            var edge = new Color32(92, 72, 46, 255);
            var wax = new Color32(156, 43, 34, 255);
            var waxLit = new Color32(196, 70, 52, 255);
            var gold = new Color32(240, 204, 106, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int lx = x - 1, ly = y - 1;
                    bool inside = lx >= 0 && lx < LetterW && ly >= 0 && ly < LetterH;
                    if (glint) { if (!inside) px[y * w + x] = gold; continue; }
                    if (!inside) continue;
                    bool rim = lx == 0 || ly == 0 || lx == LetterW - 1 || ly == LetterH - 1;
                    // the flap: a V from the top corners to the middle
                    int flap = LetterH - 1 - Mathf.Min(lx, LetterW - 1 - lx);
                    px[y * w + x] = rim ? edge : ly == flap ? shade : paper;
                }
            // the seal where the flap meets, 2 x 2, lit on its upper left
            int sx = 1 + LetterW / 2 - 1, sy = 1 + LetterH / 2 - 1;
            px[sy * w + sx] = wax; px[sy * w + sx + 1] = wax;
            px[(sy + 1) * w + sx] = waxLit; px[(sy + 1) * w + sx + 1] = wax;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), HubArt.Ppu);
        }

        public void SetFocus(bool on) { _focused = on; if (_art != null) _art.SetFocus(on); }

        void Update()
        {
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            // Only the page lights - it is what you are about to read.
            if (_sketch != null) _sketch.color = Color.Lerp(new Color(0.8f, 0.78f, 0.72f), Color.white, _lit);
            // The letter glints: its gold rim comes and goes.
            if (_letterGlint != null && _letterGlint.enabled)
            {
                float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / Tuning.Hub.LetterGlintSeconds);
                _letterGlint.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 0.95f, k));
            }
        }
    }
}
