using UnityEngine;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// The red that fills the arena while Medusa's gaze builds - everywhere EXCEPT the pillars'
    /// shadows. One low-resolution texture over the whole room, painted texel by texel from
    /// <see cref="PillarRing.Shadowed"/>: the same function the gaze itself tests, so the picture
    /// is the rule. Safe ground is simply the ground the red does not cover.
    ///
    /// Bilinear on purpose (unlike the pixel art): at <see cref="TexelsPerUnit"/> a hard edge
    /// would step in visible blocks along every shadow's slant, and a soft one reads as light.
    /// The blur is one texel wide (1/8 unit) straddling the rule's edge; a player standing where
    /// the red has fully faded is in shadow.
    ///
    /// Repainted only when the ring changes (a pillar breaking mid-telegraph), never per frame.
    /// </summary>
    public class GazeShade : MonoBehaviour
    {
        const int TexelsPerUnit = 8;

        /// <summary>Half the arena wall's thickness (GameBootstrap builds them 0.6 thick, centred
        /// on the boundary): the red stops at their inner face rather than staining the stone.</summary>
        const float WallInset = 0.3f;

        static Vector2 Half => Arena.HalfExtents - Vector2.one * WallInset;

        static readonly Color Red = new(0.95f, 0.10f, 0.12f);

        SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _px;
        int _paintedMask = -1;

        public static GazeShade Build(Transform parent)
        {
            var go = new GameObject("medusa.gaze");
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;

            var shade = go.AddComponent<GazeShade>();
            var half = Half;
            int w = Mathf.CeilToInt(half.x * 2f * TexelsPerUnit);
            int h = Mathf.CeilToInt(half.y * 2f * TexelsPerUnit);
            shade._tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            shade._px = new Color32[w * h];

            shade._sr = go.AddComponent<SpriteRenderer>();
            shade._sr.sprite = Sprite.Create(shade._tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                                             TexelsPerUnit, 0, SpriteMeshType.FullRect);
            // Over the floor, the pits and their decals - the red has to be read over all of it -
            // but under every body: the player has to be seen standing in it, or out of it.
            shade._sr.sortingOrder = SortingOrders.PitBase + 7;
            shade._sr.color = new Color(Red.r, Red.g, Red.b, 0f);
            shade._sr.enabled = false;
            return shade;
        }

        /// <summary>Paint the room as seen from <paramref name="eye"/>, if the ring changed since
        /// the last paint.</summary>
        public void Paint(PillarRing ring, Vector2 eye)
        {
            if (ring == null || _tex == null) return;
            int mask = ring.StandingMask;
            if (mask == _paintedMask) return;
            _paintedMask = mask;

            int w = _tex.width, h = _tex.height;
            _px ??= new Color32[w * h];
            var half = Half;
            for (int y = 0; y < h; y++)
            {
                float wy = -half.y + (y + 0.5f) / TexelsPerUnit;
                for (int x = 0; x < w; x++)
                {
                    float wx = -half.x + (x + 0.5f) / TexelsPerUnit;
                    bool lit = !ring.Shadowed(eye, new Vector2(wx, wy));
                    _px[y * w + x] = lit ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply();
        }

        /// <summary>The red's strength, 0..1 of its colour's alpha. Zero hides it outright.</summary>
        public void SetStrength(float a)
        {
            if (_sr == null) return;
            _sr.enabled = a > 0.001f;
            _sr.color = new Color(Red.r, Red.g, Red.b, Mathf.Clamp01(a));
        }

        /// <summary>Forget the last paint, so the next Paint redraws whatever the ring says.</summary>
        public void Invalidate() => _paintedMask = -1;

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
