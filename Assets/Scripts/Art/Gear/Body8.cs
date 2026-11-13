using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// EIGHT DIRECTIONS, PHASE 3 (RIG.md): the whole figure below the head as ONE picture per pose - the legs and arms
    /// posed on the 3D skeleton (<see cref="Locomotion8"/>, the arms swinging as the approved mock8 has them) and drawn as
    /// capsules, and every worn torso piece from its baked view (<see cref="Body8Art"/>), all in one depth buffer: the
    /// nearest texel wins, then each part is outlined and stacked by its mean depth exactly as the mock's compose()
    /// does. So any mix of slots, and any swing of the arms past the body, is the mock's own picture.
    ///
    /// The baked pieces lean with the gait (pivoting at the pelvis) and ride its bob. Pictures are cached per pose
    /// (direction, gait, frame, stride size, what is worn) and the oldest are dropped past <see cref="CacheSize"/>.
    /// </summary>
    public static partial class Body8
    {
        public const int CanvasW = 128, CanvasH = 216, GroundX = 64, GroundY = 200;
        /// <summary>The pelvis at rest (texels above the soles): the baked pieces lean about it.</summary>
        const float Pelvis = 66f, RestHip = 62f;
        /// <summary>The arms, from the mock: shoulder 41 above the hips, 15 out; upper arm 21, forearm 17.</summary>
        const float ShoulderUp = 41f, ShoulderOut = 15f, UpperArm = 21f, ForeArm = 17f;
        const int LimbOutline = 2;
        /// <summary>Limb gear is worn ON the limb: its depth leads the limb's by this much (texels), so a trouser leg a
        /// shade thinner than the drawn thigh still covers it.</summary>
        const float StripOnLimb = 1.5f;
        /// <summary>The head's nearest surface toward the camera (texels; the mock's head is ~14 deep about the neck):
        /// a swing's weapon nearer than this is laid over the head (<see cref="LastOver"/>).</summary>
        const float HeadFront = 9f;
        /// <summary>The last uncached (swing) Draw's weapon texels in front of the head, or null - the caller's.</summary>
        public static Sprite LastOver;
        /// <summary>The cape at rest: its top and its hem (texels above the soles), and its flutter at the hem.</summary>
        const float CapeTop = 104f, CapeHem = 30f, CapeFlutter = 1.5f;
        const int CacheSize = 192;

        static readonly Color Outline = new Color32(0x14, 0x12, 0x1C, 0xFF);

        /// <summary>Where the gait's lean carries a point <paramref name="height"/> texels above the soles, in screen
        /// texels (x right, y UP) - for the head, which the rig places itself.</summary>
        public static Vector2 LeanAt(Locomotion8.Pose pose, float height)
        {
            float s = Mathf.Max(0f, height - Pelvis) * Mathf.Tan(pose.Lean * Mathf.Deg2Rad);
            return new Vector2(pose.Forward.x * s, -pose.Forward.z * Locomotion8.DepthY * s);
        }

        // ------------------------------------------------------------- the cache
        static Dictionary<string, Sprite> _cache;
        /// <summary>A cached picture's overlay (what lies in front of the head), when it has one.</summary>
        static Dictionary<string, Sprite> _cacheOver;
        /// <summary>A cached picture's arm piece segments (<see cref="LastArm"/>).</summary>
        static Dictionary<string, Frame[]> _cacheArm;
        static Frame[] _drawnArm;
        static LinkedList<string> _order;

        /// <summary>The cached picture for <paramref name="key"/> (and its overlay in <see cref="LastOver"/>), or null.</summary>
        public static Sprite Cached(string key)
        {
            if (key == null || _cache == null || !_cache.TryGetValue(key, out var s) || s == null) return null;
            _order.Remove(key); _order.AddLast(key);
            LastOver = _cacheOver != null && _cacheOver.TryGetValue(key, out var o) ? o : null;
            LastArm = _cacheArm != null && _cacheArm.TryGetValue(key, out var a) ? a : null;
            return s;
        }

        static void Keep(string key, Sprite sprite, Sprite over, Frame[] arm)
        {
            _cache ??= new Dictionary<string, Sprite>();
            _cacheOver ??= new Dictionary<string, Sprite>();
            _cacheArm ??= new Dictionary<string, Frame[]>();
            if (arm != null) _cacheArm[key] = arm;
            _order ??= new LinkedList<string>();
            _cache[key] = sprite; _order.AddLast(key);
            if (over != null) _cacheOver[key] = over;
            while (_order.Count > CacheSize)
            {
                string old = _order.First.Value; _order.RemoveFirst(); _statEvicted++;
                if (_cache.TryGetValue(old, out var s) && s != null) { Object.Destroy(s.texture); Object.Destroy(s); }
                if (_cacheOver.TryGetValue(old, out var o) && o != null) { Object.Destroy(o.texture); Object.Destroy(o); }
                _cache.Remove(old); _cacheOver.Remove(old); _cacheArm.Remove(old);
            }
        }

        // ------------------------------------------------------------- drawing
        sealed class Part { public int OutR; public double DepthSum; public int Count; public float Bias, Alpha = 1f; public bool OverHead; public List<int> Won; }

        static float[] _z;
        static int[] _owner;
        static Color[] _col;
        /// <summary>The composed picture and its over-the-head overlay, reused draw to draw (FromPixels copies them).</summary>
        static Color[] _out, _over;
        /// <summary>The draw's parts, their order and their texel lists, reused draw to draw.</summary>
        static List<Part> _parts, _order8;
        static List<List<int>> _won;
        /// <summary>This picture's canvas: the plain one, or a bigger one for a weapon (a greatsword reaches 130 texels).</summary>
        static int _W = CanvasW, _H = CanvasH, _GX = GroundX, _GY = GroundY;
        const int HeldW = 288, HeldH = 320, HeldGX = 144, HeldGY = 290;

        /// <summary>The figure for <paramref name="pose"/>: <paramref name="view"/> the baked view (Body8Art.Front ..
        /// Back), <paramref name="mirrored"/> the character's mirror image (facing left: the rig mirrors the picture).
        /// The pivot is the ground point under the hips.</summary>
        public static Sprite Draw(string key, Locomotion8.Pose pose, Locomotion8.Gait gait, float phase, float size, int view,
                                  bool mirrored, IReadOnlyList<Body8Art.Piece> pieces, IReadOnlyList<Body8Art.Strip> strips,
                                  Dictionary<char, Color> suit, float ppu, Weapon8.Held held = null, Extras extras = null)
        {
            var hit = Cached(key);
            if (hit != null) { _statReused++; return hit; }
            if (key != null) _statDrawn++; else _statFrames++;
            LastOver = null;
            _drawnArm = null;

            bool big = held?.Art != null;
            _W = big ? HeldW : CanvasW; _H = big ? HeldH : CanvasH; _GX = big ? HeldGX : GroundX; _GY = big ? HeldGY : GroundY;

            int n = _W * _H;
            if (_z == null || _z.Length != n) { _z = new float[n]; _owner = new int[n]; _col = new Color[n]; }
            for (int i = 0; i < n; i++) { _z[i] = float.NegativeInfinity; _owner[i] = -1; }
            var parts = _parts ??= new List<Part>(); parts.Clear();

            // ---- the worn pieces, leaning and bobbing with the gait
            float lift = pose.HipY - RestHip, lean = Mathf.Tan(pose.Lean * Mathf.Deg2Rad);
            var f = pose.Forward;
            float c = -f.z * Locomotion8.DepthY * lean;           // screen height gained per texel above the pelvis
            // the cape's trail at its hem (mock8: the gait's trail and a flutter at twice the stride), none at the shoulders
            float hem = (gait.Trail + CapeFlutter * Mathf.Sin(4f * Mathf.PI * phase)) * size;
            float trailAt(float h) => hem * Mathf.Clamp01((CapeTop - h) / (CapeTop - CapeHem));
            // an arm piece takes its arm's pauldron off: the right one, which facing left (the figure drawn mirrored) is
            // the baked LEFT one's place unless the slot has its own mirror pictures
            foreach (var piece in pieces)
            {
                if (extras?.Arm != null && piece.Slot == GearSlot.Shoulders
                    && piece.Name == (!mirrored || piece.Mirror != null ? "pauldronR" : "pauldronL")) continue;
                var v = piece.For(view, mirrored);
                if (v == null || v.Depth == null) continue;
                int id = parts.Count; parts.Add(new Part { OutR = piece.OutR, Bias = piece.DepthBias, OverHead = piece.OverHead });
                bool trails = piece.Trails && size > 0f;
                float hTop = Body8Art.Top - v.Y0, hBot = Body8Art.Top - (v.Y0 + v.H);
                if (trails) hBot -= 6f;   // room for the hem to swing down into (trailing toward the camera)
                float push = piece.PushBack * Mathf.Abs(f.z) * Locomotion8.DepthY;
                hTop += push; hBot -= push;   // and the rows a pushed piece moves into
                float Map(float hb) => hb + lift + (hb > Pelvis ? c * (hb - Pelvis) : 0f);
                int rTop = Mathf.FloorToInt(_GY - Map(hTop)) - 1, rBot = Mathf.CeilToInt(_GY - Map(hBot)) + 1;
                for (int R = Mathf.Max(0, rTop); R <= Mathf.Min(_H - 1, rBot); R++)
                {
                    float hd = _GY - R - 0.5f - lift;
                    float hb = hd > Pelvis ? Pelvis + (hd - Pelvis) / (1f + c) : hd;
                    // a cape trails behind the stride, more toward its hem: back along the facing, which lifts it on
                    // the screen as it goes away from the camera (solved for the source row, one step)
                    float trail = 0f;
                    if (trails)
                    {
                        trail = trailAt(hb);
                        trail = trailAt(hb - f.z * Locomotion8.DepthY * trail);
                    }
                    trail += piece.PushBack;   // hung on a pack: back along the facing, as the cape's trail goes
                    hb -= f.z * Locomotion8.DepthY * trail;
                    int r = Mathf.FloorToInt(Body8Art.Top - hb) - v.Y0;
                    if (r < 0 || r >= v.H) continue;
                    float s = hb > Pelvis ? (hb - Pelvis) * lean : 0f;
                    int dx = Mathf.RoundToInt(f.x * s - f.x * trail);
                    float dz = f.z * s - f.z * trail;
                    for (int x = 0; x < v.W; x++)
                    {
                        char ch = v.Cols[r * v.W + x];
                        if (ch == '.') continue;
                        int X = _GX + v.X0 + x + dx;
                        if (X < 0 || X >= _W) continue;
                        float z = v.Depth[r * v.W + x] * 0.5f - 64f + dz;
                        Color col;
                        if (piece.Suit) { if (!suit.TryGetValue(ch, out col)) continue; }
                        else if (!piece.Palette.TryGetValue(ch, out col)) continue;
                        Put(X, R, z, col, id);
                    }
                }
            }

            // ---- the limbs on the skeleton (the undersuit)
            char Tone(int i) => "ksdblh"[Mathf.Clamp(i, 0, 5)];
            int Shade(float s) => s < -0.45f ? 4 : s > 0.5f ? 2 : 3;
            void Limb(Vector3 a, Vector3 b, float rad, System.Func<float, float, char> tone)
            {
                int id = parts.Count; parts.Add(new Part { OutR = LimbOutline });
                Capsule(a, b, rad, (u, s) => suit.TryGetValue(tone(u, s), out var col) ? col : Color.magenta, id);
            }
            foreach (var leg in new[] { pose.A, pose.B })
            {
                Limb(leg.Hip, leg.Knee, 5f, (u, s) => Tone(Shade(s) + (u < 0.15f ? -1 : 0)));
                Limb(leg.Knee, leg.AnkleJoint, 4.6f, (u, s) => Tone(Shade(s)));
                Limb(leg.Heel, leg.Toe, 2.6f, (u, s) => Tone(1 + (s < 0f ? 0 : 1)));
            }
            var r3 = pose.Right; var up = Vector3.up;
            var pelvis = new Vector3(0f, pose.HipY + 4f, 0f);
            Vector3 Up(float h) => pelvis + up * h + f * (h * lean);
            float swing = gait.Arm * size * Mathf.Sin(2f * Mathf.PI * phase);
            var fore = new (Vector3, Vector3)[2];

            // ---- the weapon at rest (phase 3c): the mock's body frame (x the character's LEFT) into the drawn frame,
            // riding the lean and bob like the torso; mirrored, the left hand is the drawn figure's right
            Weapon8.Grip grip = default;
            if (big)
            {
                var left = mirrored ? r3 : -r3;
                Vector3 Body(Vector3 q) => Up(q.y - Pelvis) + left * q.x + f * q.z;
                Vector3 Vec(Vector3 q) => left * q.x + (up + f * lean) * q.y + f * q.z;   // sheared with the lean, as Up() is
                grip = held.Live ?? Weapon8.Pose(held, Body, Vec, mirrored ? 0 : 1);
                if (held.Quiver != null) grip.Cards.Add(Weapon8.QuiverCard(held.Quiver, Body, Vec, mirrored));
            }

            var reachShort = new Vector3[2];   // where a held hand fell short of its target (a swing's card follows it)
            var shoulders = new Vector3[2];
            // an arm piece replaces the arm it is worn on: the character's right, the drawn figure's index 0 (1 mirrored)
            int pieceArm = extras?.Arm != null ? (mirrored ? 1 : 0) : -1;
            foreach (int side in new[] { 1, -1 })
            {
                float sw = side * swing * Mathf.Deg2Rad;
                var sh = Up(ShoulderUp - 4f) + r3 * (side * ShoulderOut);
                Vector3 el, wr, d2;
                var target = grip.Hand?[side > 0 ? 0 : 1];
                if (target.HasValue)
                {
                    // the arm reaches the hand's place, the elbow out and back (views.js arm())
                    wr = target.Value;
                    var d = wr - sh; float L = Mathf.Min(d.magnitude, UpperArm + ForeArm - 0.01f);
                    var dn = d.normalized; wr = sh + dn * L;
                    reachShort[side > 0 ? 0 : 1] = wr - target.Value;
                    float a = (UpperArm * UpperArm - ForeArm * ForeArm + L * L) / (2f * L);
                    float h = Mathf.Sqrt(Mathf.Max(0f, UpperArm * UpperArm - a * a));
                    var hint = r3 * (side * 0.6f) - up * 0.1f - f;
                    var pole = (hint - dn * Vector3.Dot(hint, dn)).normalized;
                    el = sh + dn * a + pole * h;
                    d2 = (wr - el).normalized;
                }
                else
                {
                    var d1 = -up * Mathf.Cos(sw) + f * Mathf.Sin(sw);
                    el = sh + d1 * UpperArm + r3 * (side * 1.5f);
                    float b = sw + gait.Elbow * size * Mathf.Deg2Rad;
                    d2 = -up * Mathf.Cos(b) + f * Mathf.Sin(b);
                    wr = el + d2 * ForeArm + r3 * (-side * 2.5f);
                }
                if (pieceArm != (side > 0 ? 0 : 1))
                {
                    Limb(sh, el, 4.2f, (u, s) => Tone(Shade(s)));
                    Limb(el, wr + d2 * 2f, 3.9f, (u, s) => Tone(Shade(s) + (u > 0.85f ? -1 : 0)));
                }
                fore[side > 0 ? 0 : 1] = (el, wr);
                shoulders[side > 0 ? 0 : 1] = sh;
            }

            // ---- the limb gear, swept along the posed limbs (phase 3b)
            if (strips != null)
                foreach (var st in strips)
                {
                    var sv = st.For(view);
                    if (sv == null) continue;
                    switch (st.Limb)
                    {
                        case Limb8.Thigh: foreach (var leg in new[] { pose.A, pose.B }) Sweep(st, sv, leg.Hip, leg.Knee, null, parts); break;
                        case Limb8.Shin: foreach (var leg in new[] { pose.A, pose.B }) Sweep(st, sv, leg.Knee, leg.AnkleJoint, null, parts); break;
                        case Limb8.Fore: for (int k = 0; k < 2; k++) if (k != pieceArm) Sweep(st, sv, fore[k].Item1, fore[k].Item2, null, parts); break;
                        case Limb8.Foot:
                            foreach (var leg in new[] { pose.A, pose.B })
                            {
                                var toe = (leg.Toe - leg.Heel).normalized;
                                var footUp = (Vector3.up - toe * Vector3.Dot(Vector3.up, toe)).normalized;
                                // the strip's heel and toe are the mock's (3 behind the ankle, 9 ahead), the game's the same
                                Sweep(st, sv, leg.Heel, leg.Toe, footUp, parts);
                            }
                            break;
                    }
                }

            // ---- the extras: the scarf's tail, the wraps' tails, an arm piece (Body8.Extras.cs)
            if (extras != null)
            {
                var leftV = mirrored ? r3 : -r3;
                Vector3 BodyX(Vector3 q) => Up(q.y - Pelvis) + leftV * q.x + f * q.z;
                if (extras.ScarfTones != null) ScarfTail(extras, BodyX, leftV, f, gait, size, size > 0f ? phase : extras.ScarfStand, parts);
                if (extras.WrapTones != null) WrapTails(extras, fore, r3, leftV, f, parts);
                if (extras.Live != null) DrawLive(extras.Live, BodyX, leftV, f, parts);
                if (extras.Arm != null) ArmPiece(extras, BodyX, leftV, f, shoulders[pieceArm], fore[pieceArm].Item1, fore[pieceArm].Item2, parts);
            }

            // ---- the weapon cards: a part of their own, laid on LAST (the mock's z 999) with no outline of their own -
            // the game art carries it - so no body outline is drawn over a blade it lies in front of
            Part cardPart = null;
            if (grip.Cards != null && grip.Cards.Count > 0)
            {
                cardPart = new Part { OutR = 0 };
                int id = parts.Count; parts.Add(cardPart);
                foreach (var cd in grip.Cards)
                {
                    var placed = cd;
                    if (placed.FollowsHand > 0) placed.O += reachShort[placed.FollowsHand - 1];
                    DrawCard(placed, id);
                }
            }

            // ---- each part outlined and laid on in the order of its mean depth (compose())
            // each part's texels go in a list kept from draw to draw (a swing draws a picture a frame)
            _won ??= new List<List<int>>();
            for (int j = 0; j < parts.Count; j++)
            {
                if (j == _won.Count) _won.Add(new List<int>());
                parts[j].Won = _won[j]; _won[j].Clear();
            }
            for (int i = 0; i < n; i++)
            {
                int o = _owner[i]; if (o < 0) continue;
                var p = parts[o]; p.Won.Add(i); p.DepthSum += _z[i]; p.Count++;
            }
            var order = _order8 ??= new List<Part>(); order.Clear(); order.AddRange(parts);
            order.RemoveAll(p => p.Count == 0);
            order.Sort((p, q) => (p.DepthSum / p.Count + p.Bias).CompareTo(q.DepthSum / q.Count + q.Bias));
            if (cardPart != null && order.Remove(cardPart)) order.Add(cardPart);
            // the picture's buffers are kept between draws (a swing draws one a frame): cleared, not reallocated
            if (_out == null || _out.Length != n) _out = new Color[n]; else System.Array.Clear(_out, 0, n);
            var outCol = _out;
            foreach (var p in order)
            {
                // a part's own texels are those it owns on the canvas (its Won)
                int own = _owner[p.Won[0]];
                int R = p.OutR, R2 = R * R + 2;
                foreach (int i in p.Won)
                {
                    int x = i % _W, y = i / _W;
                    for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        if (dx * dx + dy * dy > R2) continue;
                        int X = x + dx, Y = y + dy;
                        if (X < 0 || Y < 0 || X >= _W || Y >= _H) continue;
                        int k = Y * _W + X;
                        if (_owner[k] != own) outCol[k] = p.Alpha < 1f ? Over(outCol[k], Outline, p.Alpha) : Outline;
                    }
                }
                if (p.Alpha < 1f) foreach (int i in p.Won) outCol[i] = Over(outCol[i], _col[i], p.Alpha);
                else foreach (int i in p.Won) outCol[i] = _col[i];
            }

            var sprite = PixelSprite.FromPixels(outCol, _W, _H, new Vector2Int(_GX, _GY), ppu, crop: true);

            // A swing's weapon nearer the camera than the front of the head: the head is its own sprite ABOVE this
            // picture, so those texels go again on an overlay the rig sorts over the head (else a sweep passing
            // before the face vanishes under it and reads as cutting through the neck). An accessory's texels in
            // front of the head (the banner's pole and the hourglass's finials seen from behind) likewise.
            LastOver = null;
            Color[] overPx = null;
            Color[] OverPx()
            {
                if (overPx != null) return overPx;
                if (_over == null || _over.Length != n) _over = new Color[n]; else System.Array.Clear(_over, 0, n);
                return overPx = _over;
            }
            if (key == null && cardPart != null)
                foreach (int i in cardPart.Won) if (_z[i] > HeadFront) { OverPx(); overPx[i] = outCol[i]; }
            var headC = Locomotion8Screen(Up(HeadCentre - Pelvis));
            float headCz = Up(HeadCentre - Pelvis).z;
            foreach (var p in order)
            {
                if (!p.OverHead) continue;
                foreach (int i in p.Won)
                {
                    int x = i % _W, y = i / _W;
                    if (!InFrontOfHead(x, y, _z[i], headC, headCz)) continue;
                    OverPx();
                    int R = p.OutR, R2 = R * R + 2;
                    overPx[i] = outCol[i];
                    for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        if (dx * dx + dy * dy > R2) continue;
                        int X = x + dx, Y = y + dy;
                        if (X < 0 || Y < 0 || X >= _W || Y >= _H) continue;
                        int k = Y * _W + X;
                        if (outCol[k] == Outline && _owner[k] != _owner[i]) overPx[k] = Outline;
                    }
                }
            }
            Sprite overSprite = overPx != null ? PixelSprite.FromPixels(overPx, _W, _H, new Vector2Int(_GX, _GY), ppu, crop: true) : null;
            if (key != null) Keep(key, sprite, overSprite, _drawnArm);
            LastArm = _drawnArm;   // a swing's frames (key null) are the caller's, not cached
            LastOver = overSprite;
            return sprite;
        }

        /// <summary>A strip of limb gear along <paramref name="a"/>..<paramref name="b"/> (3D): a disc per half step, at the
        /// strip's radius and centre there, coloured across it - across the screen for a limb, top to sole for the foot
        /// (<paramref name="footUp"/>, the foot's own up).</summary>
        static void Sweep(Body8Art.Strip st, Body8Art.StripView sv, Vector3 a, Vector3 b, Vector3? footUp, List<Part> parts)
        {
            int id = parts.Count; parts.Add(new Part { OutR = st.OutR });
            int K = Body8Art.StripK;
            for (int j = 0; j < sv.N * 2; j++)
            {
                int i = j / 2;
                float r = sv.Rad[i];
                if (r <= 0f || sv.Cols[i * K] == '.') continue;
                float t = st.T0 + (st.T1 - st.T0) * (j + 0.5f) / (sv.N * 2);
                var P = Vector3.LerpUnclamped(a, b, t);
                Vector2 C;
                if (footUp.HasValue) { P += footUp.Value * (sv.Off[i] - 5f); C = Locomotion8Screen(P); }
                else C = Locomotion8Screen(P) + new Vector2((sv.Off[i] - 5f), 0f);
                for (int y = Mathf.FloorToInt(C.y - r); y <= Mathf.CeilToInt(C.y + r); y++)
                for (int x = Mathf.FloorToInt(C.x - r); x <= Mathf.CeilToInt(C.x + r); x++)
                {
                    if (x < 0 || y < 0 || x >= _W || y >= _H) continue;
                    float dx = x + 0.5f - C.x, dy = y + 0.5f - C.y, d2 = dx * dx + dy * dy;
                    if (d2 > r * r) continue;
                    float s = Mathf.Clamp((footUp.HasValue ? dy : dx) / r, -1f, 1f);
                    char ch = sv.Cols[i * K + Mathf.RoundToInt((s + 1f) * 0.5f * (K - 1))];
                    if (!st.Palette.TryGetValue(ch, out var col)) continue;
                    Put(x, y, P.z + Mathf.Sqrt(r * r - d2) + StripOnLimb, col, id);
                }
            }
        }

        /// <summary>A weapon card (views.js card()), flat - one with a form has its thickness (DrawSlab): each texel of the canvas finds the card texel under it through the
        /// card's projected axes; seen nearly edge-on the card is its middle column walked down its length, a strip
        /// ~2 texels wide.</summary>
        static void DrawCard(Weapon8.Card cd, int id)
        {
            // a weapon with its thickness (Body8.Thick); an overlay, or art with no form, stays a flat card
            if (!cd.Blend && DrawSlab(cd, id)) return;
            var art = cd.Art;
            var O = Locomotion8Screen(cd.O);
            Vector2 Us = new Vector2(cd.U.x, -cd.U.y + cd.U.z * Locomotion8.DepthY), Vs = new Vector2(cd.V.x, -cd.V.y + cd.V.z * Locomotion8.DepthY);
            float det = Us.x * Vs.y - Us.y * Vs.x;
            bool edge = Mathf.Abs(det) < 0.09f * Us.magnitude * Vs.magnitude;
            // the screen box of the card's corners
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var (a, b) in new[] { (0f, 0f), (art.W, 0f), (0f, art.H), (art.W, art.H) })
            {
                var c = O + Us * (a - art.Pivot.x) + Vs * (b - art.Pivot.y);
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }
            float vl2 = Mathf.Max(1e-6f, Vs.sqrMagnitude), vl = Mathf.Sqrt(vl2);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(minY) - 2); y <= Mathf.Min(_H - 1, Mathf.CeilToInt(maxY) + 2); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(minX) - 2); x <= Mathf.Min(_W - 1, Mathf.CeilToInt(maxX) + 2); x++)
            {
                float px = x + 0.5f - O.x, py = y + 0.5f - O.y;
                Color32 c; float z;
                if (!edge)
                {
                    float a = (px * Vs.y - py * Vs.x) / det, b = (Us.x * py - Us.y * px) / det;
                    c = art.At(Mathf.FloorToInt(art.Pivot.x + a), Mathf.FloorToInt(art.Pivot.y + b));
                    z = cd.O.z + cd.U.z * a + cd.V.z * b;
                    if (cd.Blend)
                    {
                        // over the card beneath, where it is what shows here
                        int k = y * _W + x;
                        if (c.a > 0 && _owner[k] == id && Mathf.Abs(_z[k] - z) < 1.5f + Weapon8.MaxHalf) _col[k] = Over(_col[k], c, c.a / 255f);
                        continue;
                    }
                    if (c.a < 128) continue;
                }
                else
                {
                    if (cd.Blend) continue;
                    float b = (px * Vs.x + py * Vs.y) / vl2, side = (px * Vs.y - py * Vs.x) / vl;
                    if (Mathf.Abs(side) > 1.2f) continue;
                    int v = Mathf.FloorToInt(art.Pivot.y + b);
                    c = default;
                    for (int u = art.Mid; u >= 0; u--) { c = art.At(u, v); if (c.a >= 128) break; }
                    if (c.a < 128) continue;
                    z = cd.O.z + cd.V.z * b;
                }
                Put(x, y, z, c, id);
            }
        }

        /// <summary>The head with a helm on (views.js: Hoplite's helm, centre 122.5, half 19 x 16 x 17.5, rounded) as an
        /// ellipsoid: is a texel at (x, y), depth z, in front of it? Beside it (the ray misses), it is when nearer the camera
        /// than the head's centre: the helm's picture is wider than this ellipsoid, and would hide the hourglass's posts
        /// standing beside it seen from behind, which the mock's depth shows.</summary>
        const float HeadCentre = 122f, HeadRx = 20f, HeadRy = 17f, HeadRz = 18f;
        static bool InFrontOfHead(int x, int y, float z, Vector2 c, float cz)
        {
            // the ray through the texel: drawn (px, py, pz) with py = DepthY * pz - sy (sy the screen offset, down)
            float u = (x + 0.5f - c.x) / HeadRx, sy = y + 0.5f - c.y, k = Locomotion8.DepthY;
            // the centre's height on the ray at the centre's depth is 0, so solve about it: dy = k * dz - sy
            float A = (k / HeadRy) * (k / HeadRy) + 1f / (HeadRz * HeadRz), B = -2f * k * sy / (HeadRy * HeadRy), C = (sy / HeadRy) * (sy / HeadRy) + u * u - 1f;
            float disc = B * B - 4f * A * C;
            if (disc < 0f) return z > cz;
            float dz = (-B + Mathf.Sqrt(disc)) / (2f * A);
            return z > cz + dz;
        }

        static Color Over(Color under, Color c, float a)
        {
            float oa = a + under.a * (1f - a);
            if (oa <= 0f) return Color.clear;
            var rgb = ((Vector4)c * a + (Vector4)under * (under.a * (1f - a))) / oa;
            return new Color(rgb.x, rgb.y, rgb.z, oa);
        }

        static void Put(int x, int y, float z, Color col, int id)
        {
            int k = y * _W + x;
            if (z <= _z[k]) return;
            _z[k] = z; _col[k] = col; _owner[k] = id;
        }

        /// <summary>A limb from <paramref name="a"/> to <paramref name="b"/> (3D, texels), its depth the axis's plus the
        /// round of the limb; coloured by (along 0..1, across -1..1 lit from the left).</summary>
        static void Capsule(Vector3 a, Vector3 b, float rad, System.Func<float, float, Color> col, int id)
        {
            var A = Locomotion8Screen(a); var B = Locomotion8Screen(b);
            float dx = B.x - A.x, dy = B.y - A.y, l2 = Mathf.Max(1e-6f, dx * dx + dy * dy);
            for (int y = Mathf.FloorToInt(Mathf.Min(A.y, B.y) - rad); y <= Mathf.CeilToInt(Mathf.Max(A.y, B.y) + rad); y++)
            for (int x = Mathf.FloorToInt(Mathf.Min(A.x, B.x) - rad); x <= Mathf.CeilToInt(Mathf.Max(A.x, B.x) + rad); x++)
            {
                if (x < 0 || y < 0 || x >= _W || y >= _H) continue;
                float px = x + 0.5f - A.x, py = y + 0.5f - A.y;
                float u = Mathf.Clamp01((px * dx + py * dy) / l2);
                float qx = px - u * dx, qy = py - u * dy, d2 = qx * qx + qy * qy;
                if (d2 > rad * rad) continue;
                float s = (x + 0.5f - (A.x + u * dx)) / rad;
                float z = Mathf.Lerp(a.z, b.z, u) + Mathf.Sqrt(rad * rad - d2);
                Put(x, y, z, col(u, s), id);
            }
        }

        static Vector2 Locomotion8Screen(Vector3 p) => new Vector2(_GX + p.x, _GY - p.y + p.z * Locomotion8.DepthY);
    }
}
