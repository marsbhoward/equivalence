using UnityEngine;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The terminal in the corner of the room: every checkpoint written this session.
    ///
    /// Deliberately the one anachronism in a room full of alchemy. The game's writes are real -
    /// three checkpoints per run, an account record behind them - and hiding that behind a
    /// scrying bowl would make it decoration. A screen with a log on it says the thing it is.
    ///
    /// It glows harder while a write is in flight, so a player who steps away mid-transaction can
    /// see from across the room that something is still settling.
    /// </summary>
    public class TerminalStation : MonoBehaviour
    {
        SpriteRenderer _screen, _glow, _desk;
        /// <summary>
        /// NOT readonly - see TransmutationCircle for the full story. A domain reload cannot write
        /// back into a readonly field, so this came back as four nulls while every sibling field
        /// survived, and Apply threw once a frame from then on.
        /// </summary>
        SpriteRenderer[] _lines = new SpriteRenderer[4];
        float _lit;
        bool _focused;

        static readonly Color Screen = new(0.10f, 0.26f, 0.22f);
        static readonly Color Ink = new(0.36f, 0.92f, 0.66f);
        static readonly Color Desk = new(0.22f, 0.23f, 0.28f);

        public static TerminalStation Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("terminal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var t = go.AddComponent<TerminalStation>();

            t._desk = Quad(go.transform, "desk", 1.9f, 0.62f, Desk, 2, new Vector2(0f, -0.30f));
            Quad(go.transform, "lip", 1.9f, 0.10f, Desk * 1.35f, 3, new Vector2(0f, -0.02f));

            // Monitor: a dark bezel around a lit screen, standing on the desk.
            Quad(go.transform, "bezel", 1.22f, 0.92f, new Color(0.13f, 0.14f, 0.17f), 4,
                 new Vector2(0f, 0.50f));
            t._screen = Quad(go.transform, "screen", 1.04f, 0.74f, Screen, 5, new Vector2(0f, 0.50f));

            // Four short bars standing in for rows of text. Cheaper and steadier than real glyphs
            // at this size, and they animate as a log filling in.
            for (int i = 0; i < 4; i++)
            {
                float w = 0.74f - i * 0.10f;
                t._lines[i] = Quad(go.transform, $"row{i}", w, 0.075f, Ink, 6,
                                   new Vector2(-0.14f + (0.74f - w) * 0.5f, 0.74f - i * 0.16f));
            }

            // Sized to the monitor, not the desk: a halo wider than the object it comes off
            // stops reading as screen light and starts reading as a green blob on the floor.
            t._glow = Quad(go.transform, "glow", 1.30f, 1.00f, Ink, 1, new Vector2(0f, 0.50f), Spr.Circle);

            DepthSorted.Attach(go, -0.30f, false,
                               t._desk, t._screen, t._glow, t._lines[0], t._lines[1], t._lines[2], t._lines[3]);
            t.Apply(0f);
            return t;
        }

        public void SetFocus(bool on) => _focused = on;

        void Update()
        {
            // A domain reload can hand this back half-built. Skipping is far better than throwing
            // every frame; the terminal simply stops animating until the room is rebuilt.
            if (_screen == null || _glow == null || _desk == null || _lines == null) return;

            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            // A pending write pulses whether or not anyone is standing here. That is the one piece
            // of state worth reading from across the room.
            bool pending = TxLog.PendingCount > 0;
            float beat = pending ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;

            _screen.color = Color.Lerp(Screen, Screen * 1.6f, Mathf.Max(lit, beat * 0.8f));
            _glow.color = new Color(Ink.r, Ink.g, Ink.b,
                                    Mathf.Lerp(0.035f, 0.14f, Mathf.Max(lit, beat)));
            _desk.color = Color.Lerp(Desk, Desk * 1.3f, lit);

            // Rows fill in as the session accumulates writes, so the screen is empty at the start
            // of a session and busy by the end of one.
            int rows = Mathf.Clamp(TxLog.All.Count, 0, _lines.Length);
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] == null) continue;
                bool on = i < rows;
                float a = on ? Mathf.Lerp(0.55f, 1f, lit) : 0.10f;
                _lines[i].color = new Color(Ink.r, Ink.g, Ink.b, a);
            }
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Color color,
                                   int order, Vector2 offset, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : Spr.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
