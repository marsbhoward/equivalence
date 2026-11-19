using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// EIGHT DIRECTIONS, PHASE 3c (RIG.md): the weapon at rest as a FLAT CARD in the hand - the approved views.js mock's
    /// card() and restPose(), ported. The weapon keeps its game art exactly (the equipped sprite, outline and all, one
    /// texture texel a body texel as the game draws it); the card is placed in 3D in the body's frame, the hand reaches
    /// it, and Body8 lays it into the figure's depth buffer, so it passes behind the back or before the chest as the
    /// view turns and, facing left, sits in the same hand on the far side (its other face, not a mirror).
    ///
    /// Rest carries only (attacks are phase 4): the greatsword's fist up by the right shoulder with the blade hanging
    /// diagonally down behind the back, the disc pair's wings (a disc behind each side, facing out, fists before the
    /// chest) or both stowed on the back out of combat, the bow in the hand at the side, turned 25 degrees out.
    /// </summary>
    public static partial class Weapon8
    {
        /// <summary>Appended only. SwordShoulder: the rest carry as it was on 2026-11-17, kept for the "Shoulder Carry"
        /// portrait pose whatever the greatsword's rest becomes.</summary>
        public enum Kind { Sword, DiscWings, DiscStowed, Bow, BowReady, DiscDraw, SwordShoulder }

        /// <summary>A greatsword rest stance in the mock's body frame (x the character's LEFT, y up from the soles, z
        /// forward): the right fist, the way the blade's tip points, the left hand on the grip too or not, slung on the
        /// back with the hands free, and whether the EDGE leads along the facing (a guard, ready to cut) rather than the
        /// flat facing the foe.</summary>
        public struct SwordStance
        {
            public Vector3 Hand, Tip; public bool TwoHands, Slung, EdgeLeads;
            public SwordStance(Vector3 hand, Vector3 tip, bool twoHands = false, bool slung = false, bool edgeLeads = false) { Hand = hand; Tip = tip; TwoHands = twoHands; Slung = slung; EdgeLeads = edgeLeads; }
        }

        /// <summary>The rest carry (2026-11-11): the fist up by the right shoulder, the blade down behind the back.</summary>
        public static readonly SwordStance ShoulderCarry = new(new Vector3(-21f, 118f, 3f), new Vector3(0.72f, -0.62f, -0.3f));

        /// <summary>The greatsword's stances (the user's picks, 2026-11-17): SLUNG on the back out of combat, the HIGH GUARD
        /// in it, eased between by the rig's readiness clock (held.Ready, as the bow's) - the right hand reaches the hilt,
        /// the blade comes over into the guard, the left hand joins. Placed clear of the head ellipsoid (Body8: centre 122,
        /// radii 20/17/18) in every facing; the guard's edge leads along the facing.</summary>
        public static SwordStance SwordRest => SwordDrafts["O2 Slung"];
        public static SwordStance SwordGuard => SwordDrafts["C1 High guard"];
        /// <summary>The draw's middle key: the hilt out at the right shoulder, the blade down and back, so the blade never
        /// crosses behind the head on its way over (a straight turn from slung to guard swung it level through the helm).</summary>
        static readonly SwordStance SwordLift = new(new Vector3(-30f, 120f, -6f), new Vector3(-0.15f, -0.5f, -0.85f));
        /// <summary>The readiness at which the right hand has reached the slung hilt, and from which the left comes to the
        /// grip (once the blade is coming forward: behind the shoulder it is out of the left arm's reach).</summary>
        const float SwordGrab = 0.4f, SwordJoin = 0.8f;

        /// <summary>The DRAFTS the picks came from, kept for comparing (dev): <see cref="SwordDraft"/> names one to show at
        /// rest in place of the eased pair, null the pair. Each guard sits on one greatsword basic's opening key
        /// (PrimitiveCharacterRig.Combos.cs), so the first swing starts from it.</summary>
        public static string SwordDraft;
        public static readonly Dictionary<string, SwordStance> SwordDrafts = new()
        {
            // OUT OF COMBAT
            ["O1 Shoulder rest"] = new(new Vector3(-13f, 96f, 11f), new Vector3(0.12f, 0.55f, -0.83f)),           // the flat laid on the shoulder, fist at the chest
            ["O2 Slung"] = new(new Vector3(-24f, 138f, -14f), new Vector3(0.5f, -0.86f, -0.06f), slung: true),   // across the back, hilt over the right shoulder, hands free; the grip high and out so the hilt clears the head and the tip the floor
            ["O3 Trailing"] = new(new Vector3(-22f, 76f, 6f), new Vector3(0.05f, -0.5f, 0.86f)),                 // low in the right hand, the point down ahead, clear of the floor
            // IN COMBAT
            // Return's opener (blade 30 back, fist 0.04 out and 0.12 up of the right shoulder): both fists at the right
            // shoulder at head height, the blade up and back over it, the edge leading along the facing (the user,
            // 2026-11-17). A little out from the key's shoulder line so the fists and blade clear the head in every facing.
            ["C1 High guard"] = new(new Vector3(-28f, 97f, 10f), new Vector3(-0.12f, 0.86f, -0.5f), twoHands: true, edgeLeads: true),
            // (Ascent's opener, blade 120 with the fist at the shoulder, is the carry itself - ShoulderCarry)
            ["C2 Middle guard"] = new(new Vector3(-12f, 97f, 15f), new Vector3(0.12f, 0.5f, 0.86f), twoHands: true),   // Undertow's opener (blade -60, fist 0.10 out, 0.02 down): point at the foe
            ["C3 Back guard"] = new(new Vector3(-19f, 108f, 0f), new Vector3(0.1f, 0.5f, -0.86f), twoHands: true),      // Cyclone's opener (blade 60, fist 0.05 up): the tip high behind
        };


        /// <summary>A weapon's picture: its texels (row 0 the TOP) and the grip, in texels from the top-left.</summary>
        public sealed class Art
        {
            public Color32[] Px; public int W, H; public Vector2 Pivot; public float Scale;
            /// <summary>The middle of the drawn columns: an edge-on card shows the column nearest it.</summary>
            public int Mid;
            /// <summary>What it is read as for its thickness (Weapon8.Depth), and the slab built for it.</summary>
            public Form Shape; public Slab Thick;
            public Color32 At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? default : Px[y * W + x];
        }

        /// <summary>What the hands hold this pose: the art, how it is carried, and a key for the picture cache.</summary>
        public sealed class Held
        {
            public Art Art; public Kind Kind; public string Key; public float StowZ = StowZ0;
            /// <summary>Phase 4: a swing's hands and cards, already in the drawn frame (they replace the rest pose).</summary>
            public Grip? Live;
            /// <summary>A swing leaving the rest stance: the rest it left (its pose is built in the swing's frame) and how much
            /// of it still shows (1 the stance, 0 the swing) - the guard's edge-on blade and fists ease into the swing's
            /// instead of jumping (<see cref="Blend"/>).</summary>
            public Held From; public float FromW;
            /// <summary>A bow's quiver (worn on the back, rest and swing) and arrow (nocked at the ready), or null.</summary>
            public Art Quiver, Arrow;
            /// <summary>A bow: 0 out of combat (O1) .. 1 at the ready (C1), eased by the rig. The disc pair's draw
            /// (<see cref="Kind.DiscDraw"/>): 0 stowed .. 1 in the wings, the rig's _discDraw clock.</summary>
            public float Ready;
            /// <summary>The RUN STANCE (the user's picks off the Pace Lab, 2026-11-18): 0 the weapon's guard .. 1 its run
            /// carry, eased by the rig while it runs in combat between swings - the greatsword TRAILING, the discs SWEPT
            /// BACK, the bow's low ready RIDING the stride. <see cref="RunGait"/> at <see cref="RunPhase"/> gives the
            /// stride's bounce and step the carry follows.</summary>
            public float Run, RunPhase;
            public Locomotion8.Gait RunGait;
            /// <summary>The free-run arms tucked (Body8.Tuck, out of combat, 0..1): the bow's hand is the tucked left fist,
            /// riding <see cref="RunGait"/> at <see cref="RunPhase"/> as the empty hand does.</summary>
            public float Tuck;
        }

        // ---------------------------------------------------------------- the run stances
        /// <summary>How a run carry rides the stride: the hands lag the body's rise and fall (lifted by <paramref name="k"/>
        /// of the bounce <paramref name="lag"/> of a cycle late, against the body's own), texels.</summary>
        /// <summary>How far the tucked bow's lower limb stays off the floor at the least (texels).</summary>
        const float BowClear = 6f;
        static float RunLift(Held h, float k, float lag) =>
            h.RunGait == null || !h.RunGait.Planted ? 0f : k * h.RunGait.Height(h.RunPhase - lag) - h.RunGait.Height(h.RunPhase);
        /// <summary>The step's rock, -1..1, <paramref name="lag"/> of a cycle late.</summary>
        static float RunRock(Held h, float lag) => h.RunGait == null ? 0f : Mathf.Sin(2f * Mathf.PI * (h.RunPhase - lag));

        /// <summary>TRAILING: the right fist low at the hip, the blade dragged back and down behind, the flat to the side;
        /// the left hand lets go and its arm runs (mock frame: x the character's LEFT).</summary>
        static void SwordTrail(Held h, out Vector3 hand, out Vector3 tip, out Vector3 across)
        {
            hand = new Vector3(-19f, 66f + RunLift(h, 1.2f, 0.06f), -6f + 4f * RunRock(h, 0.05f));
            tip = Norm(new Vector3(-0.14f, -0.25f + 0.05f * RunRock(h, 0.25f), -0.94f));   // the point clear of the floor
            across = Norm(Vector3.up - tip * Vector3.Dot(Vector3.up, tip));
        }

        /// <summary>How far behind the body stowed discs ride (the mock's z, texels) for what is worn on the back: a pack
        /// stands out past the shoulder blades (Athanor's, views.js: its back face 23.5 behind).</summary>
        public static float StowDepthFor(string backSet) => backSet == "athanor" ? -25.5f : StowZ0;
        /// <summary>How much further back than a cape a pack's back face is (texels): what is hung on the back goes there.</summary>
        public static float PackDepth(string backSet) => StowZ0 - StowDepthFor(backSet);

        static Dictionary<Sprite, Art> _arts;

        /// <summary>The card for a weapon sprite drawn <paramref name="texelsPerPixel"/> body texels a texture texel.
        /// <paramref name="fresh"/>: read now and not cached (a picture repainted every frame, the element overlay).</summary>
        public static Art ArtOf(Sprite s, float texelsPerPixel, bool fresh = false)
        {
            if (s == null || s.texture == null || !s.texture.isReadable) return null;
            _arts ??= new Dictionary<Sprite, Art>();
            if (!fresh && _arts.TryGetValue(s, out var hit) && hit != null) return hit;
            var r = s.rect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var src = s.texture.GetPixels32();
            int tw = s.texture.width;
            var px = new Color32[w * h];
            int x0 = w, x1 = -1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = src[(Mathf.RoundToInt(r.y) + (h - 1 - y)) * tw + Mathf.RoundToInt(r.x) + x];
                px[y * w + x] = c;
                if (c.a > 127) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); }
            }
            var art = new Art { Px = px, W = w, H = h, Pivot = new Vector2(s.pivot.x, h - s.pivot.y), Scale = texelsPerPixel, Mid = (x0 + x1) / 2 };
            if (!fresh) _arts[s] = art;
            return art;
        }

        /// <summary>A card placed in the drawn frame: its grip at <see cref="O"/>, one texture texel along U (across)
        /// and V (down the grid).</summary>
        public struct Card
        {
            /// <summary>Laid OVER the card drawn just before it where that card shows, by its alpha (the element overlay).</summary>
            public bool Blend;
            public Art Art; public Vector3 O, U, V;
            /// <summary>A swing's card follows this hand (Body8's arm index) where the drawn arm cannot reach as far as
            /// today's rig holds it; 0 = none (rest cards are placed for the drawn arm already).</summary>
            public int FollowsHand;
        }

        /// <summary>Where the hands go (null: the arm swings free), indexed as Body8's arms: 0 the character's right.</summary>
        /// <summary>Where each hand goes (null: it hangs free) and how far it has got there from its hang (HandW, 0..1;
        /// null: all the way).</summary>
        public struct Grip { public Vector3?[] Hand; public float[] HandW; public List<Card> Cards; }

        static Vector3 Norm(Vector3 v) => v.normalized;

        /// <summary>Ease a swing's grip <paramref name="live"/> back toward the rest grip it left, <paramref name="w"/> of the
        /// way (1 = the rest), IN PLACE (the swing's lists are its own, read again after the draw): the main card's place,
        /// and its axes turned (not shrunk) between the two; each hand between where the two have it.</summary>
        public static void Blend(Grip live, Grip rest, float w)
        {
            if (w <= 0f || live.Cards == null || live.Cards.Count == 0 || rest.Cards == null || rest.Cards.Count == 0) return;
            var c = live.Cards[0]; var r = rest.Cards[0];
            c.O = Vector3.Lerp(c.O, r.O, w);
            c.U = Vector3.Slerp(c.U, r.U, w);
            c.V = Vector3.Slerp(c.V, r.V, w);
            live.Cards[0] = c;
            for (int i = 0; i < 2 && live.Hand != null && i < live.Hand.Length; i++)
            {
                var a = live.Hand[i]; var b = rest.Hand?[i];
                if (!a.HasValue || !b.HasValue) continue;   // a hand only one of them holds goes where that one has it
                float rw = w * (rest.HandW?[i] ?? 1f);
                live.Hand[i] = Vector3.Lerp(a.Value, b.Value, rw);
            }
        }

        /// <summary>The way a stance's blade runs ACROSS (its width), square to the blade: along the facing when the edge
        /// leads (the flat to the sides: edge-on from ahead, flat from the side), else across the body (the flat to the
        /// front, or against the back slung). views.js's cross is right-handed in its (x left, y up, z forward) frame:
        /// Unity's Cross is the same sum.</summary>
        static Vector3 SwordAcross(SwordStance st, Vector3 tip) => st.EdgeLeads
            ? Norm(Vector3.forward - tip * Vector3.Dot(Vector3.forward, tip))
            : Norm(Vector3.Cross(tip, Mathf.Abs(tip.z) > 0.9f ? Vector3.up : Vector3.forward));

        /// <summary>
        /// The rest pose in the drawn frame. <paramref name="body"/> maps a point of the mock's body frame (x the
        /// character's LEFT, y up from the soles, z forward) into the drawn frame; <paramref name="vec"/> a direction.
        /// <paramref name="leftArm"/> is the Body8 arm index the mock's left hand lands on (it trades sides mirrored).
        /// </summary>
        public static Grip Pose(Held held, System.Func<Vector3, Vector3> body, System.Func<Vector3, Vector3> vec, int leftArm)
        {
            var g = new Grip { Hand = new Vector3?[2], Cards = new List<Card>() };
            var art = held.Art; float k = art.Scale;
            switch (held.Kind)
            {
                case Kind.Sword:
                case Kind.SwordShoulder:
                {
                    // the portrait pose's carry (Kind.SwordShoulder) or a draft (dev), in the character's RIGHT hand as the
                    // game holds it (the user, 2026-11-11). The art keeps its own face (U from the cross, not a mirror).
                    if (held.Kind == Kind.SwordShoulder || SwordDraft != null)
                    {
                        var st = held.Kind == Kind.Sword && SwordDrafts.TryGetValue(SwordDraft, out var d) ? d : ShoulderCarry;
                        var tip = Norm(st.Tip);
                        if (!st.Slung) g.Hand[1 - leftArm] = body(st.Hand);
                        // two hands: the left one on the grip just below the right (toward the pommel)
                        if (st.TwoHands) g.Hand[leftArm] = body(st.Hand - tip * 6f);
                        g.Cards.Add(new Card { Art = art, O = body(st.Hand + new Vector3(0f, 0f, st.Slung ? 0f : -3f)), U = vec(SwordAcross(st, tip)) * k, V = vec(-tip) * k });
                        break;
                    }
                    // slung out of combat, the high guard in it (SwordRest/SwordGuard), eased by the readiness clock:
                    // the right hand up to the slung hilt, then the blade over into the guard, the left hand joining
                    // through SwordLift: slung -> lift over the first half, lift -> guard over the second
                    float r = Mathf.Clamp01(held.Ready);
                    float grab = Mathf.SmoothStep(0f, 1f, r / SwordGrab);
                    float t = Mathf.InverseLerp(SwordGrab, 1f, r);
                    bool first = t < 0.5f;
                    var a = first ? SwordRest : SwordLift; var b = first ? SwordLift : SwordGuard;
                    float s = Mathf.SmoothStep(0f, 1f, first ? t * 2f : t * 2f - 1f);
                    var ta = Norm(a.Tip); var tb = Norm(b.Tip);
                    var tipT = Vector3.Slerp(ta, tb, s).normalized;
                    var uT = Vector3.Slerp(SwordAcross(a, ta), SwordAcross(b, tb), s);
                    uT = Norm(uT - tipT * Vector3.Dot(uT, tipT));   // square to the blade again
                    var handT = Vector3.Lerp(a.Hand, b.Hand, s);
                    // running in combat: the guard eases into the trailing carry, the left hand letting go
                    float run = Mathf.SmoothStep(0f, 1f, held.Run) * Mathf.Clamp01((r - SwordJoin) / (1f - SwordJoin));
                    if (run > 0f)
                    {
                        SwordTrail(held, out var trailHand, out var trailTip, out var trailU);
                        handT = Vector3.Lerp(handT, trailHand, run);
                        tipT = Vector3.Slerp(tipT, trailTip, run).normalized;
                        uT = Vector3.Slerp(uT, trailU, run);
                        uT = Norm(uT - tipT * Vector3.Dot(uT, tipT));
                    }
                    g.HandW = new float[2];
                    // slung, the hand takes the grip a little below its middle: the middle is just out of the arm's reach
                    if (grab > 0f) { g.Hand[1 - leftArm] = body(handT + tipT * (4f * (1f - t))); g.HandW[1 - leftArm] = grab; }
                    float join = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SwordJoin, 1f, r)) * (1f - run);
                    if (join > 0f) { g.Hand[leftArm] = body(handT - tipT * 6f); g.HandW[leftArm] = join; }
                    g.Cards.Add(new Card { Art = art, O = body(handT + new Vector3(0f, 0f, -3f * t)), U = vec(uT) * k, V = vec(-tipT) * k });
                    break;
                }
                case Kind.DiscWings:
                {
                    // the fists up before the chest, a disc standing behind each side of the body, facing out; running in
                    // combat, SWEPT BACK - the arms back and out, the discs trailing at the hips like fins, face to the side
                    // The pair STAGGERED (the user, 2026-11-18: side on, the two fins lay one over the other and read as
                    // one disc drawn twice): the left fin rides higher and closer, the right lower and further back, both
                    // canted out at the top, so from any side the two read as two.
                    float run = Mathf.SmoothStep(0f, 1f, held.Run), lift = RunLift(held, 1.3f, 0.08f), rock = RunRock(held, 0.05f);
                    foreach (int s in new[] { 1, -1 })
                    {
                        var swept = new Vector3(s * 21f, 72f + lift + s * FinStaggerUp, -18f + s * 3f * rock + (s > 0 ? 0f : -FinStaggerBack));
                        var hand = Vector3.Lerp(new Vector3(s * 8.5f, 95f, 10f), swept, run);
                        var centre = Vector3.Lerp(new Vector3(s * 29f, 84f, -7f), swept + new Vector3(s * 6f, 2f, -24f), run);
                        var u = Vector3.Slerp(Vector3.right, Vector3.forward, run);
                        var v = Vector3.Slerp(Vector3.down, Norm(new Vector3(-s * FinCant, -1f, 0f)), run);
                        g.Hand[s > 0 ? leftArm : 1 - leftArm] = body(hand);
                        g.Cards.Add(new Card { Art = art, O = body(centre), U = vec(u) * k, V = vec(v) * k });
                    }
                    break;
                }
                case Kind.DiscDraw:
                {
                    // between the two, on the flat rig's clock (PrimitiveCharacterRig.DiscStance): the arms go up over the
                    // shoulders across the first half, the discs come off the back into the fists across its middle
                    // (DiscGrab), the arms come down into the wings across the second half; stowing runs it backwards
                    float d = Mathf.Clamp01(held.Ready);
                    float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.5f, d)), down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, d));
                    float grab = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DiscGrabFrom, DiscGrabTo, d));
                    foreach (int s in new[] { 1, -1 })
                    {
                        Vector3 hang = new Vector3(s * 18f, 66f, 1f), over = new Vector3(s * 12f, 114f, -5f), fist = new Vector3(s * 8.5f, 95f, 10f);
                        var hand = d <= 0.5f ? Vector3.Lerp(hang, over, up) : Vector3.Lerp(over, fist, down);
                        if (d > 0.04f) g.Hand[s > 0 ? leftArm : 1 - leftArm] = body(hand);
                        var stow = new Vector3(s * StowX, StowY, s > 0 ? held.StowZ : held.StowZ - 1.5f);
                        var carried = Vector3.Lerp(over + new Vector3(0f, 0f, -4f), new Vector3(s * 29f, 84f, -7f), down);
                        // face out behind on the back, turning through edge-on to face out at the side
                        float a = grab * Mathf.PI;
                        var u = new Vector3(-Mathf.Cos(a), 0f, -s * Mathf.Sin(a));
                        g.Cards.Add(new Card { Art = art, O = body(Vector3.Lerp(stow, carried, grab)), U = vec(u) * k, V = vec(Vector3.down) * k });
                    }
                    break;
                }
                case Kind.DiscStowed:
                    // out of combat: both on the back, face out behind, overlapping near the spine, the hands free (the approved
                    // stowed stance, https://claude.ai/artifact/DNXik8ZUaP3z1Kad2Wac32)
                    foreach (int s in new[] { 1, -1 })
                        g.Cards.Add(new Card { Art = art, O = body(new Vector3(s * StowX, StowY, s > 0 ? held.StowZ : held.StowZ - 1.5f)), U = vec(Vector3.left) * k, V = vec(Vector3.down) * k });
                    break;
                case Kind.Bow:
                case Kind.BowReady:
                {
                    // the user's picks (2026-11-11, https://claude.ai/artifact/GhKYh8vGz98jG2WYtjhWqF), eased between by
                    // the rig over 0.45 s: O1 out of combat - in the left hand, lowered at the side, tipped forward,
                    // turned a little out; C1 in combat - low ready ahead at the hip, angled down and forward, the right
                    // hand on the string (the art's string side is behind its belly, against U), an arrow nocked
                    float t = Mathf.SmoothStep(0f, 1f, held.Kind == Kind.BowReady ? 1f : held.Ready);
                    // raised 4 and tipped further than the mock (the user, 2026-11-11): the lower limb clears the ground
                    // by about 4 texels instead of reaching below the soles, the bow kept at its size
                    var restHand = new Vector3(18f, 74f, 3f) + new Vector3(1.5f, 0f, 0f);
                    var restDown = -Norm(new Vector3(0f, 1f, 0.65f));
                    if (held.Tuck > 0f)
                    {
                        // running out of combat (the free-run arms tucked, the user's pick 2026-11-18): the bow in the
                        // tucked left fist, pumping with it, tipped forward from the WORLD's upright (not the lean's) just
                        // enough for the lower limb to clear the floor - upright at the tucked fist's height
                        int drawnSide = leftArm == 0 ? 1 : -1;
                        float sw = held.RunGait != null ? held.RunGait.ArmAt(held.RunPhase) : 0f;
                        var tuckGrip = new Vector3(Body8.TuckOut, Body8.TuckUp + RunLift(held, Body8.TuckKeep, Body8.TuckLag),
                                                   Body8.TuckFwd + Body8.TuckPump * drawnSide * sw / Body8.TuckArm);
                        float lean = Vector3.Dot(vec(Vector3.up), vec(Vector3.forward));   // the shear Vec carries
                        float c = Mathf.Clamp((body(tuckGrip).y - BowClear) / Mathf.Max(1f, art.H * k * 0.5f), 0.3f, 0.95f);
                        float tip = Mathf.Sqrt(1f / (c * c) - 1f);                          // forward per unit down, in the world
                        float w = Mathf.SmoothStep(0f, 1f, held.Tuck);
                        restHand = Vector3.Lerp(restHand, tuckGrip, w);
                        restDown = Norm(Vector3.Slerp(restDown, -Norm(new Vector3(0f, 1f, tip - lean)), w));
                    }
                    float o = 25f * Mathf.Deg2Rad;
                    var restU = new Vector3(Mathf.Sin(o), 0f, Mathf.Cos(o)); restU = Norm(restU - restDown * Vector3.Dot(restU, restDown));
                    var readyGrip = new Vector3(4f, 72f, 15f);
                    var readyDown = -Norm(new Vector3(0f, 0.85f, 0.5f));
                    var readyU = Norm(Vector3.forward - readyDown * Vector3.Dot(Vector3.forward, readyDown));
                    // running in combat the low ready RIDES the stride: the grip lags the bounce and rocks with the step
                    float run = Mathf.SmoothStep(0f, 1f, held.Run) * t;
                    if (run > 0f) readyGrip += new Vector3(0f, RunLift(held, 1.3f, 0.08f), 2.5f * RunRock(held, 0.08f)) * run;
                    var grip = Vector3.Lerp(restHand, readyGrip, t);
                    var down = Norm(Vector3.Slerp(restDown, readyDown, t));
                    var u = Norm(Vector3.Slerp(restU, readyU, t)); u = Norm(u - down * Vector3.Dot(u, down));
                    g.Hand[leftArm] = body(grip);
                    // the string hand comes up from its side over the second half
                    if (t > 0.5f)
                    {
                        Vector3 hang = new Vector3(-18f, 66f, 1f), onString = grip - u * BowStringOff;
                        g.Hand[1 - leftArm] = body(Vector3.Lerp(hang, onString, (t - 0.5f) * 2f));
                    }
                    g.Cards.Add(new Card { Art = art, O = body(grip), U = vec(u) * k, V = vec(down) * k });
                    if (held.Arrow != null && t > 0.75f)
                    {
                        // nocked: from the string hand through the grip, along the bow's belly; the art's point is +x
                        var nock = grip - u * BowStringOff;
                        float ka = held.Arrow.Scale, len = held.Arrow.W * ka;
                        var side = Norm(Vector3.Cross(u, down));
                        g.Cards.Add(new Card { Art = held.Arrow, O = body(nock + u * (len * 0.5f)), U = vec(u) * ka, V = vec(Norm(Vector3.Cross(side, u))) * ka });
                    }
                    break;
                }
            }
            return g;
        }

        /// <summary>The quiver on the back, its mouth over the LEFT shoulder (as the game wears it: the far shoulder facing
        /// right), leaning down to the right hip, face out behind. Its art's pivot is the mouth.</summary>
        public static Card QuiverCard(Art quiver, System.Func<Vector3, Vector3> body, System.Func<Vector3, Vector3> vec, bool mirrored)
        {
            float k = quiver.Scale;
            var mouth = new Vector3(8f, 104f, -11f);
            var down = Norm(new Vector3(-15f, -30f, -1f));
            var across = Norm(new Vector3(-down.y, down.x, 0f));
            // facing left the mock's left is the drawn figure's right: the quiver goes with the mock's left, so it stays
            // over the same shoulder of the character
            return new Card { Art = quiver, O = body(mouth), U = vec(across) * k, V = vec(down) * k };
        }

        /// <summary>Stowed discs: each centre this far out, up from the soles, and behind (texels, the mock's frame).</summary>
        public static float StowX = 5f, StowY = 92f;
        /// <summary>Where on the draw clock the discs leave the back and reach the fists (the flat rig's DiscGrabFrom / To).</summary>
        const float DiscGrabFrom = 0.35f, DiscGrabTo = 0.6f;
        /// <summary>The disc run stance's stagger (texels): the left fin this much higher, the right this much further
        /// back - and both canted out at the top by this much a unit down.</summary>
        const float FinStaggerUp = 7f, FinStaggerBack = 10f, FinCant = 0.3f;
        /// <summary>How far behind the bow's grip its string runs (texels), where the right hand holds it at the ready.</summary>
        public static float BowStringOff = 13f;
        const float StowZ0 = -13f;
    }
}
