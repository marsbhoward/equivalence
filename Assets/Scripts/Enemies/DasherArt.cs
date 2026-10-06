using System.Text;
using UnityEngine;
using Convergence.Art;
using static Convergence.Enemies.PixelGeometry;

namespace Convergence.Enemies
{
    /// <summary>
    /// Procedural pixel art for Dasher's two coexisting looks - see <see cref="EnemyLooks"/>.
    /// Four sprites per look, in EnemyStage order (Idle/Mid/Full/Cooling), read by
    /// EnemyStageCycle off EnemyController.CurrentDasherPhase - a discrete phase switch rather
    /// than the shared telegraph ramp every other kind reads, because Dasher's own state machine
    /// (Approach/Telegraph/Rushing/Combo/Evading) doesn't fit that shape - see EnemyStageCycle's
    /// own remarks.
    ///
    /// THE WHOLE BODY IS THE MOVING PART, NOT A LIMB - the fourth technique this project's enemy
    /// roster has needed (Bomb's crack, Turret's eye, Chaser/Ranged's pivoting limb), because
    /// Dasher's identity isn't a weapon, it's VELOCITY: the fastest, most fragile kind, whose whole
    /// attack is a coiled wind-up releasing into a straight-line blur. A limb swinging says
    /// "I am hitting you with this"; a body compressing then stretching says "I am about to move
    /// very fast" and then "I am moving very fast" - which is Dasher's actual threat. Each stage
    /// is its own SQUASH/STRETCH/LEAN on the same base silhouette function rather than a fixed body
    /// plus a moving part, because the body itself is what has to read as coiling and releasing.
    ///
    /// Idle stands neutral; Mid crouches low and wide, coiling back slightly like a spring loading;
    /// Full stretches long and low with a hard forward lean plus a trailing motion streak - the one
    /// stage that gets an extra flourish, because it covers BOTH Rushing and Combo (the charge and
    /// the hits that follow it read as one continuous strike, not two); Cooling leans back, braced,
    /// the shape of skidding to a stop before peeling away - Dasher's recover beat is Evading, a
    /// real retreat, not a stance reset in place the way Chaser's own cooling is.
    ///
    /// Both looks carry Dasher's own acid yellow-green as a core glow that brightens Idle->Mid->
    /// Full then dims on Cooling, the same shared-accent rule every kind's two looks already
    /// follow - deliberately NOT red, the same reason EnemyDef's own tint avoids it: red is what
    /// the ground telegraph glows, and a body that turned red too would stop reading as a second,
    /// agreeing cue and start reading as a duplicate of the first.
    /// </summary>
    public static class DasherArt
    {
        static readonly Palette.Ramp Gunmetal = new(new Color(0.56f, 0.58f, 0.62f));
        static readonly Palette.Ramp Dust     = new(new Color(0.58f, 0.52f, 0.40f));
        static readonly Palette.Ramp Core     = new(new Color(0.75f, 0.95f, 0.25f)); // Dasher's own tint

        static Sprite[] _construct, _automaton;

        public static Sprite[] Construct => _construct ??= Build(Dust, "construct");
        public static Sprite[] Automaton => _automaton ??= Build(Gunmetal, "automaton");

        public static Sprite[] For(EnemyLook look) => look == EnemyLook.Construct ? Construct : Automaton;

        // ---- shared geometry ----

        const int W = 30, H = 15;
        const float Cx = 14f, BaseHalf = 5f, BodyBaseRows = 2f, BodyTopRow = 11f, BodyMinHalf = 1.3f;
        const float LeanScale = 6f; // texels of shear at the crown per unit of "lean"

        // Idle, Mid, Full, Cooling - same order as EnemyStage. (widthMul, heightMul, lean).
        // Mid squashes low and wide with a slight BACKWARD lean - coiling, not yet moving. Full
        // stretches long and low with a hard FORWARD lean - the release. Cooling is a shorter,
        // gentler version of Mid's own compression leaning the OTHER way - braced against its own
        // momentum rather than winding up again.
        static readonly (float w, float h, float lean)[] StageBody =
        {
            (1.00f, 1.00f,  0.00f),
            (1.20f, 0.72f, -0.16f),
            (2.00f, 0.55f,  0.42f),
            (1.35f, 0.80f, -0.22f),
        };
        static readonly char[] StageTone = { 'B', 'L', 'H', 'D' }; // second-material slot: Base/Light/Glow/Dark

        static float HalfWidthAt(float y, float topRow, float widthMul)
        {
            if (y < BodyBaseRows) return BaseHalf * widthMul;
            float v = (y - BodyBaseRows) / Mathf.Max(0.01f, topRow - BodyBaseRows);
            return Mathf.Max(BodyMinHalf, BaseHalf * widthMul * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)));
        }

        static float RowCenterX(float y, float topRow, float lean) =>
            Cx + lean * (topRow > 0f ? Mathf.Clamp01(y / topRow) : 0f) * LeanScale;

        static Sprite[] Build(Palette.Ramp body, string key)
        {
            var palette = Palette.Of(body, Core);
            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var (w, h, lean) = StageBody[i];
                float topRow = BodyTopRow * h;
                var rows = Rows(topRow, w, lean, StageTone[i], streak: i == (int)EnemyStage.Full);
                sprites[i] = PixelSprite.From($"enemy.dasher.{key}.{i}", rows, palette, outline: true);
            }
            return sprites;
        }

        static string[] Rows(float topRow, float widthMul, float lean, char coreTone, bool streak)
        {
            float midY = topRow * 0.5f;
            var core = new Vector2(RowCenterX(midY, topRow, lean), midY);

            // The streak trails from the body's own BACK edge (opposite the lean, i.e. opposite
            // the direction of travel) at three heights through the squashed body, each stretching
            // further back and thinning - the same "afterimage" reasoning Shadow's echoes and the
            // Rift Blade's shard trail use elsewhere in this project, at a pixel-art enemy's scale.
            (Vector2 from, Vector2 to)[] streaks = null;
            if (streak)
            {
                streaks = new (Vector2, Vector2)[3];
                float[] heights = { midY * 0.4f, midY, midY * 1.5f };
                float[] lens = { 3.5f, 5.5f, 3f };
                for (int i = 0; i < 3; i++)
                {
                    float hy = Mathf.Min(heights[i], topRow);
                    float backEdge = RowCenterX(hy, topRow, lean) - HalfWidthAt(hy, topRow, widthMul);
                    streaks[i] = (new Vector2(backEdge, hy), new Vector2(backEdge - lens[i], hy));
                }
            }

            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var sb = new StringBuilder(W);
                for (int x = 0; x < W; x++)
                {
                    float fx = x, fy = y;

                    bool onCore = InCapsule(fx, fy, core, core, 1.4f);
                    bool onStreak = streaks != null && AnyWedge(fx, fy, streaks);
                    float rowCx = RowCenterX(fy, topRow, lean);
                    bool onBody = fy <= topRow && Mathf.Abs(fx - rowCx) <= HalfWidthAt(fy, topRow, widthMul);

                    if (onCore) { sb.Append(coreTone); continue; }
                    if (onStreak) { sb.Append(coreTone); continue; }
                    if (onBody)
                    {
                        bool grounded = y < 2;
                        bool lit = y >= topRow - topRow * 0.4f && fx <= rowCx;
                        sb.Append(grounded ? 'd' : lit ? 'l' : 'b');
                        continue;
                    }
                    sb.Append('.');
                }
                rows[H - 1 - y] = sb.ToString(); // PixelSprite reads rows[0] as the TOP row.
            }
            return rows;
        }

        static bool AnyWedge(float x, float y, (Vector2 from, Vector2 to)[] segs)
        {
            foreach (var (from, to) in segs)
                if (InWedge(x, y, from, to, 1.0f, 0.15f)) return true;
            return false;
        }
    }
}
