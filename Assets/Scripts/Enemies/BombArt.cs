using System.Text;
using UnityEngine;
using Convergence.Art;

namespace Convergence.Enemies
{
    /// <summary>
    /// Procedural pixel art for Bomb's two coexisting looks - see <see cref="EnemyLooks"/> for
    /// why there are two and why the floor, not the spawn, picks one. The four sprites returned
    /// per look are in EnemyStage order (Idle/Mid/Full/Cooling) - EnemyStageCycle is what actually
    /// picks between them at runtime, reading EnemyController's telegraph state so the CREATURE
    /// says the same thing the ground ring already says, rather than the ring carrying the whole
    /// read alone.
    ///
    /// Built the same way every other piece of art in this project is: a PixelSprite grid,
    /// rasterised once and cached by key, never an imported asset - the scene stays
    /// CLI-rebuildable. FOUR FRAMES PER VARIANT rather than one static body plus a separate
    /// telegraph-only cue, because a bomb that looks identical whether it is idle or a third of a
    /// second from detonating is relying entirely on the ground ring to say so.
    ///
    /// Both variants are YELLOW - the Bomb's colour, body and light alike, matching its yellow
    /// blast ring - and share their WARNING material (a pale lamp-yellow, brighter than the body
    /// so it still reads against it), diverging only in what carries it: cracks in an ochre stone
    /// for the construct, a rising plunger on a hazard-yellow barrel for the automaton.
    ///
    /// THE COLOUR IS BAKED HERE, and the renderer stays white (EnemyFactory). It used to be the
    /// enemy's orange Tint multiplying grey art - which is why the barrel read brown and its lamp
    /// red - and a multiply cannot make a yellow lamp out of anything but a yellow tint.
    /// </summary>
    public static class BombArt
    {
        /// <summary>
        /// The Bomb's BLAST colour - its warning ring, its explosion flash, and an elite's cluster
        /// shards. Yellow, so a Bomb's blast reads apart from the Mortar's red-orange shell and the
        /// body's own ember: the colour belongs to the danger zone, not to the thing carrying it.
        /// </summary>
        public static readonly Color Blast = new(1f, 0.86f, 0.2f);

        // ---- shared materials ----
        //
        // The body carries the Bomb's yellow; Lamp, the warning material (the automaton's light,
        // the construct's cracks), is paler and brighter so it still stands out on a yellow body
        // as it heats - Dark at idle, Glow at the brink.
        static readonly Palette.Ramp Ochre  = new(new Color(0.80f, 0.64f, 0.20f));
        static readonly Palette.Ramp Lamp   = new(new Color(1.00f, 0.93f, 0.45f));
        static readonly Palette.Ramp Hazard = new(new Color(0.92f, 0.72f, 0.14f));

        // The automaton's COOLING stage swaps hue rather than just dimming - a machine has a
        // status light that can change colour; a mineral only loses its glow. See BuildAutomaton.
        static readonly Palette.Ramp Safety = new(new Color(0.35f, 0.85f, 0.75f));

        static Sprite[] _construct, _automaton;

        public static Sprite[] Construct => _construct ??= BuildConstruct();
        public static Sprite[] Automaton => _automaton ??= BuildAutomaton();

        public static Sprite[] For(EnemyLook look) => look == EnemyLook.Construct ? Construct : Automaton;

        // ================================================================== construct

        const int ConstructW = 22, ConstructH = 16;

        static Sprite[] BuildConstruct()
        {
            var body = ConstructBody(out var crack);
            var wideCrack = Dilate(body, crack);
            var palette = Palette.Of(Ochre, Lamp);

            // Idle, Mid, Full, Cooling - same order as EnemyStage. UPPERCASE: the crack is
            // the SECOND material (Lamp) in Palette.Of(Ochre, Lamp) - lowercase would draw it
            // from the stone's own ramp instead and it would read as more stone rather than as
            // the light showing through it.
            //
            // Colour alone was not enough to tell the four stages apart at this size - measured
            // against a live capture, a one-texel line four brightnesses apart barely moved. Only
            // ArmFull widens (the dilated mask): "the cracks visibly widen" was the whole pitch
            // for this look, and colour on its own could not deliver it. Cooling reuses the THIN
            // mask rather than a still-wider one - it is the same crack sealing back down, not a
            // new and different one - so only its tone (Dark) needs to say "this is cooling."
            var mask = new[] { crack, crack, wideCrack, crack };
            char[] crackTone = { 'B', 'L', 'H', 'D' };

            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var rows = ConstructRows(body, mask[i], crackTone[i]);
                sprites[i] = PixelSprite.From($"enemy.bomb.construct.{i}", rows, palette, outline: true);
            }
            return sprites;
        }

        /// <summary>Every body cell 4-adjacent to a crack cell, crack cells included - one texel
        /// of growth in every direction the crack can widen without leaving the silhouette.</summary>
        static bool[,] Dilate(bool[,] body, bool[,] mask)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            var out_ = new bool[w, h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!body[x, y]) continue;
                bool near = mask[x, y]
                    || (x > 0 && mask[x - 1, y]) || (x < w - 1 && mask[x + 1, y])
                    || (y > 0 && mask[x, y - 1]) || (y < h - 1 && mask[x, y + 1]);
                out_[x, y] = near;
            }
            return out_;
        }

        /// <summary>
        /// A squat boulder: a flat resting base (rows 0..2, full width) rising into a quarter-
        /// ellipse dome, the same flattened-ellipse technique Obsidian's own head silhouette uses
        /// for a curve that isn't a hand-typed grid. Three crack lines are walked out from a point
        /// near the crown toward the base, each stepping with a small alternating sideways wobble
        /// so they read as jagged rather than as ruled diagonals - the same reasoning the Rift
        /// Blade's own tear edges are never a single straight wave.
        /// </summary>
        static bool[,] ConstructBody(out bool[,] crack)
        {
            const int w = ConstructW, h = ConstructH, baseRows = 3;
            var body = new bool[w, h];
            float cx = (w - 1) / 2f;
            float half = w / 2f - 0.5f;

            for (int y = 0; y < h; y++)
            {
                float hw;
                if (y < baseRows)
                {
                    hw = half;
                }
                else
                {
                    float v = (y - baseRows) / (float)(h - 1 - baseRows);
                    hw = Mathf.Max(1.2f, half * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)));
                }
                for (int x = 0; x < w; x++)
                    if (Mathf.Abs(x - cx) <= hw) body[x, y] = true;
            }

            var cr = new bool[w, h];
            void Crack(float x0, float y0, float dx, float dy, int steps, float wobble)
            {
                float x = x0, y = y0;
                for (int i = 0; i < steps; i++)
                {
                    int xi = Mathf.RoundToInt(x), yi = Mathf.RoundToInt(y);
                    if (xi >= 0 && xi < w && yi >= 0 && yi < h && body[xi, yi]) cr[xi, yi] = true;
                    x += dx + (i % 2 == 0 ? wobble : -wobble);
                    y += dy;
                }
            }

            // All three start near the crown and run down toward the base, the way a shell cracks
            // outward from the point of impact - long branch left, shorter branch right, one short
            // hairline so the pattern doesn't read as perfectly bilateral.
            Crack(cx, h - 2f, -0.9f, -0.80f, 7, 0.60f);
            Crack(cx, h - 2f, 0.75f, -0.90f, 6, 0.50f);
            Crack(cx - 1f, h - 3f, 0.10f, -1.00f, 5, 0.70f);

            crack = cr;
            return body;
        }

        static string[] ConstructRows(bool[,] body, bool[,] crack, char crackTone)
        {
            const int w = ConstructW, h = ConstructH;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var sb = new StringBuilder(w);
                for (int x = 0; x < w; x++)
                {
                    if (!body[x, y]) { sb.Append('.'); continue; }
                    if (crack[x, y]) { sb.Append(crackTone); continue; }

                    // Cheap directional shading rather than true lighting - the same "brighter
                    // toward the upper-left, darker along the ground" rule PixelDetail's own
                    // chamfer pass uses. A silhouette that is one flat tone reads as a sticker;
                    // this is the minimum that reads as a rounded, resting mass.
                    bool grounded = y < 2;
                    bool lit = y >= h - 6 && x <= (w - 1) / 2f;
                    sb.Append(grounded ? 'd' : lit ? 'l' : 'b');
                }
                rows[h - 1 - y] = sb.ToString(); // PixelSprite reads rows[0] as the TOP row.
            }
            return rows;
        }

        // ================================================================== automaton

        const int AutomatonW = 16, AutomatonH = 22;
        const int BarrelRows = 13;
        const int RodMaxRows = 9;

        static Sprite[] BuildAutomaton()
        {
            var palette = Palette.Of(Hazard, Lamp, Safety);

            return new[]
            {
                PixelSprite.From("enemy.bomb.automaton.0", AutomatonRows(2, 'D'), palette, outline: true),
                PixelSprite.From("enemy.bomb.automaton.1", AutomatonRows(5, 'L'), palette, outline: true),
                PixelSprite.From("enemy.bomb.automaton.2", AutomatonRows(RodMaxRows, 'H'), palette, outline: true),
                // Cooling: the plunger retracts to its idle height but the tip goes to the SAFETY
                // ramp (digits) instead of a dim warning tone - a status light changing colour
                // reads as "stood down" more clearly than the same red merely getting darker,
                // which is easy to mistake for "still arming, just dim."
                PixelSprite.From("enemy.bomb.automaton.3", AutomatonRows(2, '4'), palette, outline: true),
            };
        }

        /// <summary>
        /// A riveted barrel (rows 0..BarrelRows-1, corners clipped rather than a true ellipse -
        /// this is a stubby cylinder seen face-on, not a dome) with a thin plunger rod rising out
        /// of reserved headroom above it. The barrel's own row count and width never change
        /// between stages - only how far the rod climbs into that headroom, and what tone its tip
        /// takes - so every stage bakes to the identical canvas size and a driver can swap between
        /// them without ever touching the renderer's scale.
        /// </summary>
        static string[] AutomatonRows(int rodRows, char tipTone)
        {
            const int w = AutomatonW, h = AutomatonH;
            int rodX0 = w / 2 - 1, rodX1 = w / 2;   // the two centre columns of an EVEN-width canvas
            var rows = new string[h];

            for (int y = 0; y < h; y++)
            {
                var sb = new StringBuilder(w);

                if (y < BarrelRows)
                {
                    int inset = y == 0 || y == BarrelRows - 1 ? 3 : y == 1 || y == BarrelRows - 2 ? 1 : 0;
                    bool band = y == 4 || y == 9;   // riveted hoop bands
                    for (int x = 0; x < w; x++)
                    {
                        if (x < inset || x >= w - inset) { sb.Append('.'); continue; }
                        bool rivet = band && (x == 3 || x == w - 4);
                        bool rim = x == inset || x == w - inset - 1 || y == 0 || y == BarrelRows - 1;
                        sb.Append(rivet ? 'l' : band ? 'd' : rim ? 'd' : 'b');
                    }
                }
                else
                {
                    int rodY = y - BarrelRows;
                    bool hasRod = rodY < rodRows;
                    // The tip is the top two rungs of whatever height the rod currently reaches -
                    // a fixed FRACTION of the rod would make the idle nub's tip nearly invisible.
                    bool tip = hasRod && rodY >= rodRows - 2;
                    for (int x = 0; x < w; x++)
                    {
                        bool onRod = hasRod && (x == rodX0 || x == rodX1);
                        sb.Append(!onRod ? '.' : tip ? tipTone : 'd');
                    }
                }

                rows[h - 1 - y] = sb.ToString();
            }
            return rows;
        }
    }
}
