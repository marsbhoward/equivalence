using System.Text;
using UnityEngine;
using Convergence.Art;
using static Convergence.Enemies.PixelGeometry;

namespace Convergence.Enemies
{
    /// <summary>
    /// Procedural pixel art for Ranged's two coexisting looks - see <see cref="EnemyLooks"/>.
    /// Four sprites per look, in EnemyStage order (Idle/Mid/Full/Cooling), read by
    /// EnemyStageCycle off EnemyController.TelegraphProgress01/Recovering - the shared
    /// _telegraphTimer Bomb's and Chaser's own telegraphs already use.
    ///
    /// A POSED PROBE, THE SAME TECHNIQUE CHASER'S ARM USES - swept through four angles rather than
    /// told apart by colour or radius alone - but a different ARC for a different job. Chaser
    /// swings sideways through a strike; Ranged AIMS: the probe rises from tucked low, through
    /// levelling off, to dead level and glowing at the moment it fires, then kicks UP into a recoil
    /// on the recover beat - the same rise-then-kick shape a real barrel makes, and legible as
    /// "aim, fire, jolt back" rather than as a repeat of Chaser's swipe.
    ///
    /// SMALL AND SPINDLY ON PURPOSE - Ranged is the fragile one (37 HP against Bomb's 65 and
    /// Chaser's 185), so the body is the smallest and thinnest silhouette among the kinds built so
    /// far: a low, quiet body a supporting frame stands on, all the visual weight in the one thing
    /// that matters, the aiming probe.
    ///
    /// THE TRIPOD LEGS (automaton) AND THE URCHIN SPIKES (construct) are each kind's own flavour of
    /// "small quiet base, one raised weapon" rather than a repeat of either earlier look's
    /// silhouette technique - a tripod is a sentry stance, an urchin's spikes are a passive
    /// defence, and neither is the smooth dome or the swinging-limb shape the other three kinds
    /// already used. Both share Ranged's own violet as the probe tip's glow, the same
    /// shared-accent rule every kind built so far follows.
    /// </summary>
    public static class RangedArt
    {
        static readonly Palette.Ramp Gunmetal = new(new Color(0.60f, 0.60f, 0.66f));
        static readonly Palette.Ramp Mineral  = new(new Color(0.42f, 0.48f, 0.50f));
        static readonly Palette.Ramp Glow     = new(new Color(0.55f, 0.45f, 0.95f)); // Ranged's own tint

        static Sprite[] _construct, _automaton;

        public static Sprite[] Construct => _construct ??= BuildConstruct();
        public static Sprite[] Automaton => _automaton ??= BuildAutomaton();

        public static Sprite[] For(EnemyLook look) => look == EnemyLook.Construct ? Construct : Automaton;

        // ---- shared geometry ----

        const int W = 24, H = 20;
        // BaseHalf came down from 5.5 - the body was wide enough at the base that legs hanging
        // off a low hip point stayed almost entirely inside its own silhouette and read as a
        // couple of stray bumps rather than legs, the same "limb buried in the torso" trap
        // Chaser's own idle arm hit first. A narrower body leaves real clearance either side.
        const float Cx = 10f, BaseHalf = 4.2f, BodyBaseRows = 2f, BodyTopRow = 11f, BodyMinHalf = 1.2f;
        static readonly Vector2 Pivot = new(Cx + 3f, 9f);
        const float ProbeLen = 8f, ProbeHalf = 1.3f, TipGlowHalf = 1.5f, TipGlowFrom = 0.68f;

        // Idle, Mid, Full, Cooling - same order as EnemyStage. Rises from tucked-low, through
        // levelling off, to dead level at the moment it fires, then KICKS UP into a recoil rather
        // than returning to Idle's own angle the way Chaser's cooling does - Ranged's recover beat
        // is a retreat (kiting back), not a stance reset in place, so the pose says "just jolted by
        // its own shot" rather than "settled."
        static readonly float[] StageAngle = { -65f, -20f, 5f, 55f };
        static readonly char[] StageTone = { 'B', 'L', 'H', 'D' }; // second-material slot: Base/Light/Glow/Dark

        static float BodyHalfWidthAt(float y)
        {
            if (y < BodyBaseRows) return BaseHalf;
            float v = (y - BodyBaseRows) / (BodyTopRow - BodyBaseRows);
            return Mathf.Max(BodyMinHalf, BaseHalf * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)));
        }

        static bool BodyMask(float x, float y)
        {
            float hw = BodyHalfWidthAt(y);
            return y <= BodyTopRow && Mathf.Abs(x - Cx) <= hw;
        }

        // ================================================================== automaton: tripod sentry

        // Hip raised to row 7, well above the base, where the body's own taper has already
        // narrowed it - a leg only has to clear a few texels of body rather than the base's full
        // width, so most of its outer length reads as a distinct limb rather than a buried one.
        static readonly Vector2 Hip = new(Cx, 7f);
        static readonly (Vector2 a, Vector2 b)[] Legs =
        {
            (Hip, new Vector2(Cx - 9f, 0f)),
            (Hip, new Vector2(Cx + 9f, 0f)),
        };

        static Sprite[] BuildAutomaton()
        {
            var palette = Palette.Of(Gunmetal, Glow);
            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var tip = PointAt(Pivot, StageAngle[i], ProbeLen);
                var rows = Rows(tip, hasLegs: true, hasSpikes: false, StageTone[i]);
                sprites[i] = PixelSprite.From($"enemy.ranged.automaton.{i}", rows, palette, outline: true);
            }
            return sprites;
        }

        // ================================================================== construct: needle urchin

        // (row, side, angleDeg, len) - side is -1/+1 for left/right of centre. The start point is
        // computed OFF THE BODY'S OWN EDGE FUNCTION at that row rather than an eyeballed
        // coordinate, so a spike always roots exactly on the surface and points radially outward
        // from it - the first pass hand-picked points that landed just inside the edge with barely
        // 0.15 texels of taper left showing past it, which is why the fix here is systematic
        // (root ON the edge, aim directly away from centre) rather than another set of guesses.
        static readonly (float row, float side, float angle, float len)[] SpikeDef =
        {
            (3f, -1f, 195f, 4.4f),
            (1f, 1f, -25f, 4.2f),
            (8f, 1f, 25f, 3.8f),
            (9.5f, -1f, 165f, 3.6f),
        };

        static (Vector2 start, float angle, float len)[] BuildSpikes()
        {
            var spikes = new (Vector2, float, float)[SpikeDef.Length];
            for (int i = 0; i < SpikeDef.Length; i++)
            {
                var (row, side, angle, len) = SpikeDef[i];
                var start = new Vector2(Cx + side * BodyHalfWidthAt(row), row);
                spikes[i] = (start, angle, len);
            }
            return spikes;
        }

        static readonly (Vector2 start, float angle, float len)[] Spikes = BuildSpikes();

        static Sprite[] BuildConstruct()
        {
            var palette = Palette.Of(Mineral, Glow);
            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var tip = PointAt(Pivot, StageAngle[i], ProbeLen);
                var rows = Rows(tip, hasLegs: false, hasSpikes: true, StageTone[i]);
                sprites[i] = PixelSprite.From($"enemy.ranged.construct.{i}", rows, palette, outline: true);
            }
            return sprites;
        }

        static string[] Rows(Vector2 tip, bool hasLegs, bool hasSpikes, char tipTone)
        {
            var glowFrom = Vector2.Lerp(Pivot, tip, TipGlowFrom);
            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var sb = new StringBuilder(W);
                for (int x = 0; x < W; x++)
                {
                    float fx = x, fy = y;

                    bool onTipGlow = InWedge(fx, fy, glowFrom, tip, ProbeHalf, TipGlowHalf);
                    bool onProbe = InCapsule(fx, fy, Pivot, tip, ProbeHalf);
                    bool onLeg = hasLegs && AnyCapsule(fx, fy, Legs, 0.9f);
                    bool onSpike = hasSpikes && AnySpike(fx, fy);
                    bool onBody = BodyMask(fx, fy);

                    if (onTipGlow) { sb.Append(tipTone); continue; }
                    if (onProbe) { sb.Append('d'); continue; }
                    if (onLeg) { sb.Append('d'); continue; }
                    if (onSpike) { sb.Append('d'); continue; }
                    if (onBody)
                    {
                        bool grounded = y < 2;
                        bool lit = y >= BodyTopRow - 5 && x <= Cx;
                        sb.Append(grounded ? 'd' : lit ? 'l' : 'b');
                        continue;
                    }
                    sb.Append('.');
                }
                rows[H - 1 - y] = sb.ToString(); // PixelSprite reads rows[0] as the TOP row.
            }
            return rows;
        }

        static bool AnyCapsule(float x, float y, (Vector2 a, Vector2 b)[] segs, float halfWidth)
        {
            foreach (var (a, b) in segs)
                if (InCapsule(x, y, a, b, halfWidth)) return true;
            return false;
        }

        static bool AnySpike(float x, float y)
        {
            foreach (var (start, angle, len) in Spikes)
            {
                var tip = PointAt(start, angle, len);
                if (InWedge(x, y, start, tip, 1.3f, 0.2f)) return true;
            }
            return false;
        }
    }
}
