using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// The player's own states as small icons over the head (the user's pick, 2026-10-26 -
    /// "icons"): a cracked red shield while EXPOSED (after a leap - damage taken is up), a water
    /// drop while SOAKED by standing in water. Side by side when both are on. They replaced red
    /// and pale rings pulsing off the body. Texel-drawn at body density, above the head, so they
    /// never cover the player.
    ///
    /// RETROGRADE MOTION (the user's pick, group 3 - "clock over the head") is a third: a dial whose
    /// hand ticks BACKWARDS through the warning, quicker as the flip nears, red and still while
    /// the controls are inverted (Contrary's inversion shows it too).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class StateIcons : MonoBehaviour
    {
        const float Ppu = TexelFx.Body;
        const int IconW = 7, IconH = 7, Gap = 2;

        [SerializeField] PlayerController _player;
        [SerializeField] SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _px;

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color32 RedEdge = C("#ff4a3a"), RedFill = C("#701010"), RedCrack = C("#ffe9e4");
        static readonly Color32 DropEdge = C("#56c6f2"), DropHi = C("#a8ecff");
        static readonly Color32 Dial = C("#a07ff2"), DialHand = C("#f6eeff"), DialRed = C("#ff4a3a"), DialRedHand = C("#ffe9e4");
        const int Icons = 3, CanvasW = IconW * Icons + Gap * (Icons - 1);
        float _handAngle;

        public static StateIcons Attach(PlayerController player)
        {
            var go = new GameObject("player.states");
            var s = go.AddComponent<StateIcons>();
            s._player = player;
            s._sr = go.AddComponent<SpriteRenderer>();
            s._sr.enabled = false;
            return s;
        }

        void LateUpdate()
        {
            if (_player == null) { Destroy(gameObject); return; }
            bool exposed = _player.ExposedSeconds > 0f, soaked = _player.Soaked;
            var fx = _player.Effects;
            bool inverted = fx != null && fx.InputsInverted, coming = fx != null && fx.InversionComing;
            bool clock = inverted || coming;
            int count = (exposed ? 1 : 0) + (soaked ? 1 : 0) + (clock ? 1 : 0);
            if (count == 0) { _sr.enabled = false; return; }

            int w = CanvasW, h = IconH;
            if (_tex == null)
            {
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[w * h];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_px, 0, _px.Length);
            int x0 = (w - (count * IconW + (count - 1) * Gap)) / 2;
            if (exposed) { Shield(x0); x0 += IconW + Gap; }
            if (soaked) { Drop(x0); x0 += IconW + Gap; }
            if (clock)
            {
                // Backwards, from one turn every 1.5 s to four a second as the flip nears.
                if (!inverted) _handAngle -= Time.deltaTime * Mathf.Lerp(4f, 25f, fx.InversionWarning);
                Clock(x0, inverted);
            }
            _tex.SetPixels32(_px);
            _tex.Apply();

            var rig = _player.Rig as MonoBehaviour;
            var top = rig != null ? PlayerController.HeadTop(rig) : (Vector2)_player.transform.position + Vector2.up;
            transform.position = new Vector3(Mathf.Round(top.x * Ppu) / Ppu, Mathf.Round((top.y + 0.12f) * Ppu) / Ppu, 0f);
            _sr.sortingOrder = ReleaseCanvas.UnderBody(_player.transform);
            _sr.enabled = true;
        }

        /// <summary>A heater shield, cracked down the middle - rows from the top.</summary>
        void Shield(int x0)
        {
            for (int row = 0; row < IconH; row++)
            {
                int half = row < 4 ? 3 : 3 - (row - 3);
                for (int dx = -half; dx <= half; dx++)
                {
                    bool edge = Mathf.Abs(dx) == half || row == 0;
                    Put(x0 + 3 + dx, IconH - 1 - row, edge ? RedEdge : RedFill);
                }
            }
            Put(x0 + 3, IconH - 2, RedCrack); Put(x0 + 4, IconH - 3, RedCrack); Put(x0 + 3, IconH - 4, RedCrack);
        }

        /// <summary>A water drop: a point at the top swelling to a round foot.</summary>
        void Drop(int x0)
        {
            int[] widths = { 0, 0, 1, 1, 2, 2, 1 };
            for (int row = 0; row < IconH; row++)
            {
                int half = widths[row];
                for (int dx = -half; dx <= half; dx++)
                    Put(x0 + 3 + dx, IconH - 1 - row, dx == -half && row >= 3 ? DropHi : DropEdge);
            }
        }

        /// <summary>A dial with one hand at <see cref="_handAngle"/>.</summary>
        void Clock(int x0, bool red)
        {
            var rim = red ? DialRed : Dial;
            for (int y = 0; y < IconH; y++)
                for (int x = 0; x < IconW; x++)
                {
                    float dx = x - 3, dy = y - 3, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 2.5f && d <= 3.5f) Put(x0 + x, y, rim);
                }
            float a = red ? 0f : _handAngle;
            for (int i = 0; i <= 2; i++)
                Put(x0 + 3 + Mathf.RoundToInt(Mathf.Sin(a) * i), 3 + Mathf.RoundToInt(Mathf.Cos(a) * i), red ? DialRedHand : DialHand);
        }

        void Put(int x, int y, Color32 c)
        {
            int w = CanvasW;
            if (x < 0 || y < 0 || x >= w || y >= IconH) return;
            _px[y * w + x] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
