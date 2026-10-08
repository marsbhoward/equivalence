using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.Riposte;

namespace Convergence.Combat
{
    /// <summary>
    /// Riposte's guard, drawn: a heater shield of light formed in front of the character while
    /// the guard is up (AttackStep.Guards - the whole timing bar). It FORMS outward from its
    /// centre, holds with a glint running across it, FLARES white on every parry, and fades as the
    /// thrust lands. Purely a picture: PlayerController owns the guard and tells this what happened.
    ///
    /// Drawn texel by texel into one point-filtered texture at body density, like the StrikeBar,
    /// so it sits on the character's own grid. Outline-only (a dark rim, a bright core line, a
    /// faint fill and a blade emblem) so the character stays readable through it. On an OVERLAY
    /// order (<see cref="SortingOrders.RiposteWard"/>).
    ///
    /// Domain reload: texture and renderer are [SerializeField] Object references; the pixel buffer
    /// is a plain array rebuilt on demand (see CLAUDE.md).
    /// </summary>
    public class RiposteWard : MonoBehaviour
    {
        public const string ObjectName = "riposte.ward";

        const float Ppu = 37.5f;

        // The shield, in texels: even width so it mirrors on a texel boundary.
        const int W = 18;
        const int H = 22;
        const int Pad = 1;
        const int TexW = W + Pad * 2;
        const int TexH = H + Pad * 2;

        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Texture2D _texture;

        Color32[] _pixels;

        enum State { Hidden, Forming, Held, Fading }
        State _state;
        float _t;
        float _flash;

        /// <summary>The ward beside this player, made on first use and found by NAME after that,
        /// so a domain reload re-attaches instead of welding on a second one.</summary>
        public static RiposteWard For(Transform player)
        {
            var found = player.Find(ObjectName);
            if (found != null && found.TryGetComponent(out RiposteWard existing)) return existing;

            var go = new GameObject(ObjectName);
            go.transform.SetParent(player, false);
            var ward = go.AddComponent<RiposteWard>();
            ward.Build();
            return ward;
        }

        void Build()
        {
            _texture = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "riposte.ward",
            };
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), Ppu);
            _renderer.sortingOrder = SortingOrders.RiposteWard;
            _renderer.enabled = false;
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>The guard went up: form the ward in front of <paramref name="facingX"/>.</summary>
        public void Raise(float facingX)
        {
            _state = State.Forming;
            _t = 0f;
            _flash = 0f;
            transform.localScale = Vector3.one;
            Face(facingX);
            _renderer.enabled = true;
        }

        /// <summary>Keep the ward in front of the body as the facing tracks the aim.</summary>
        public void Face(float facingX)
        {
            float side = facingX >= 0f ? 1f : -1f;
            transform.localPosition = new Vector3(side * T.WardSideOffset, T.WardCentreY, 0f);
        }

        /// <summary>A hit was turned: flare white and pop.</summary>
        public void Flash()
        {
            if (_state == State.Hidden || _state == State.Fading) return;
            if (_state == State.Forming) { _state = State.Held; _t = 0f; }   // a parry completes it
            _flash = 1f;
            transform.localScale = new Vector3(1.14f, 1.14f, 1f);
        }

        /// <summary>The strike landed (or the guard ended): fade out.</summary>
        public void Drop()
        {
            if (_state == State.Hidden || _state == State.Fading) return;
            _state = State.Fading;
            _t = 0f;
        }

        void Update()
        {
            if (_renderer == null || !_renderer.enabled) return;
            float dt = Time.deltaTime;
            _t += dt;

            if (_state == State.Forming && _t >= T.WardFormSeconds) { _state = State.Held; _t = 0f; }
            if (_state == State.Fading && _t >= T.WardFadeSeconds)
            {
                _state = State.Hidden;
                _renderer.enabled = false;
                return;
            }
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt / T.WardFlashSeconds);

            var s = transform.localScale;
            if ((s - Vector3.one).sqrMagnitude > 0.00001f)
                transform.localScale = Vector3.Lerp(s, Vector3.one, 1f - Mathf.Exp(-18f * dt));

            Repaint();
        }

        // ---- the shape ----

        /// <summary>Half-width of the shield at row <paramref name="r"/> counted from the TOP:
        /// square shoulders with clipped corners, straight sides, then tapering to a point.</summary>
        static float HalfWidth(int r)
        {
            const float half = W * 0.5f;
            if (r == 0) return half - 1.5f;
            if (r == 1) return half - 0.5f;
            float straight = H * 0.42f;
            if (r < straight) return half;
            float t = (r - straight) / (H - 1 - straight);
            return half * Mathf.Pow(Mathf.Clamp01(1f - t), 0.65f) + 0.5f;
        }

        static bool Inside(int x, int y)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return false;
            int r = H - 1 - y;
            return Mathf.Abs(x + 0.5f - W * 0.5f) < HalfWidth(r);
        }

        static bool Rim(int x, int y)
            => Inside(x, y) && (!Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1));

        static bool Core(int x, int y)
            => Inside(x, y) && !Rim(x, y)
               && (Rim(x - 1, y) || Rim(x + 1, y) || Rim(x, y - 1) || Rim(x, y + 1));

        /// <summary>The blade emblem down the middle - this is a sword's guard, not a chest's.</summary>
        static bool Emblem(int x, int y)
        {
            int r = H - 1 - y;
            bool column = x == W / 2 - 1 || x == W / 2;
            if (column && r >= 4 && r <= H - 7) return true;
            return r == 7 && x >= W / 2 - 3 && x <= W / 2 + 2;   // the cross-guard
        }

        void Repaint()
        {
            if (_pixels == null || _pixels.Length != TexW * TexH) _pixels = new Color32[TexW * TexH];
            System.Array.Clear(_pixels, 0, _pixels.Length);

            float alpha = _state == State.Fading ? 1f - Mathf.Clamp01(_t / T.WardFadeSeconds) : 1f;
            // Forming: revealed outward from the centre, the front drawn bright.
            float reveal = _state == State.Forming ? Mathf.Clamp01(_t / T.WardFormSeconds) : 1f;
            float maxR = Mathf.Sqrt(W * W * 0.25f + H * H * 0.25f);
            // The glint: a bright diagonal band crossing the face while it is held.
            float glint = _state == State.Held ? Mathf.Repeat(Time.time * 70f, W + H + 24f) - 12f : -999f;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!Inside(x, y)) continue;
                    float d = Mathf.Sqrt((x + 0.5f - W * 0.5f) * (x + 0.5f - W * 0.5f)
                                         + (y + 0.5f - H * 0.5f) * (y + 0.5f - H * 0.5f)) / maxR;
                    if (d > reveal) continue;

                    Color c = Rim(x, y) ? T.WardRim
                            : Core(x, y) || Emblem(x, y) ? T.WardCore
                            : T.WardFill;
                    if (Emblem(x, y) && !Core(x, y)) c.a *= 0.55f;

                    if (_state == State.Forming && reveal - d < 0.12f && !Rim(x, y)) c = T.WardCore;
                    if (!Rim(x, y) && Mathf.Abs((x + (H - y)) - glint) < 1.5f)
                        c = Color.Lerp(c, T.WardCore, 0.7f);
                    if (_flash > 0f && !Rim(x, y)) c = Color.Lerp(c, T.WardFlash, _flash);

                    c.a *= alpha;
                    _pixels[(y + Pad) * TexW + x + Pad] = c;
                }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }
    }
}
