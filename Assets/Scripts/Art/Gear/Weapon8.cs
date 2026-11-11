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
    public static class Weapon8
    {
        public enum Kind { Sword, DiscWings, DiscStowed, Bow, BowReady }

        /// <summary>A weapon's picture: its texels (row 0 the TOP) and the grip, in texels from the top-left.</summary>
        public sealed class Art
        {
            public Color32[] Px; public int W, H; public Vector2 Pivot; public float Scale;
            /// <summary>The middle of the drawn columns: an edge-on card shows the column nearest it.</summary>
            public int Mid;
            public Color32 At(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? default : Px[y * W + x];
        }

        /// <summary>What the hands hold this pose: the art, how it is carried, and a key for the picture cache.</summary>
        public sealed class Held
        {
            public Art Art; public Kind Kind; public string Key; public float StowZ = StowZ0;
            /// <summary>Phase 4: a swing's hands and cards, already in the drawn frame (they replace the rest pose).</summary>
            public Grip? Live;
            /// <summary>A bow's quiver (worn on the back, rest and swing) and arrow (nocked at the ready), or null.</summary>
            public Art Quiver, Arrow;
            /// <summary>A bow: 0 out of combat (O1) .. 1 at the ready (C1), eased by the rig.</summary>
            public float Ready;
        }

        /// <summary>How far behind the body stowed discs ride (the mock's z, texels) for what is worn on the back: a pack
        /// stands out past the shoulder blades (Athanor's, views.js: its back face 23.5 behind).</summary>
        public static float StowDepthFor(string backSet) => backSet == "athanor" ? -25.5f : StowZ0;

        static Dictionary<Sprite, Art> _arts;

        /// <summary>The card for a weapon sprite drawn <paramref name="texelsPerPixel"/> body texels a texture texel.</summary>
        public static Art ArtOf(Sprite s, float texelsPerPixel)
        {
            if (s == null || s.texture == null || !s.texture.isReadable) return null;
            _arts ??= new Dictionary<Sprite, Art>();
            if (_arts.TryGetValue(s, out var hit) && hit != null) return hit;
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
            _arts[s] = art;
            return art;
        }

        /// <summary>A card placed in the drawn frame: its grip at <see cref="O"/>, one texture texel along U (across)
        /// and V (down the grid).</summary>
        public struct Card
        {
            public Art Art; public Vector3 O, U, V;
            /// <summary>A swing's card follows this hand (Body8's arm index) where the drawn arm cannot reach as far as
            /// today's rig holds it; 0 = none (rest cards are placed for the drawn arm already).</summary>
            public int FollowsHand;
        }

        /// <summary>Where the hands go (null: the arm swings free), indexed as Body8's arms: 0 the character's right.</summary>
        public struct Grip { public Vector3?[] Hand; public List<Card> Cards; }

        static Vector3 Norm(Vector3 v) => v.normalized;

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
                {
                    // the rest carry, in the character's RIGHT hand as the game holds it (the user, 2026-11-11; the mock
                    // had it in the left): the fist up by the right shoulder, the blade hanging diagonally down behind
                    // the back toward the other side. The art keeps its own face (U from the cross, not a mirror).
                    var hand = new Vector3(-21f, 118f, 3f);
                    var tip = Norm(new Vector3(0.72f, -0.62f, -0.3f));
                    var u = Norm(Vector3.Cross(tip, Vector3.forward));
                    // views.js's cross is right-handed in its (x left, y up, z forward) frame: Unity's Cross is the same sum
                    g.Hand[1 - leftArm] = body(hand);
                    g.Cards.Add(new Card { Art = art, O = body(hand + new Vector3(0f, 0f, -3f)), U = vec(u) * k, V = vec(-tip) * k });
                    break;
                }
                case Kind.DiscWings:
                    // the fists up before the chest, a disc standing behind each side of the body, facing out
                    foreach (int s in new[] { 1, -1 })
                    {
                        g.Hand[s > 0 ? leftArm : 1 - leftArm] = body(new Vector3(s * 8.5f, 95f, 10f));
                        g.Cards.Add(new Card { Art = art, O = body(new Vector3(s * 29f, 84f, -7f)), U = vec(Vector3.right) * k, V = vec(Vector3.down) * k });
                    }
                    break;
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
                    var restHand = new Vector3(18f, 70f, 3f) + new Vector3(1.5f, 0f, 0f);
                    var restDown = -Norm(new Vector3(0f, 1f, 0.5f));
                    float o = 25f * Mathf.Deg2Rad;
                    var restU = new Vector3(Mathf.Sin(o), 0f, Mathf.Cos(o)); restU = Norm(restU - restDown * Vector3.Dot(restU, restDown));
                    var readyGrip = new Vector3(4f, 72f, 15f);
                    var readyDown = -Norm(new Vector3(0f, 0.85f, 0.5f));
                    var readyU = Norm(Vector3.forward - readyDown * Vector3.Dot(Vector3.forward, readyDown));
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
        /// <summary>How far behind the bow's grip its string runs (texels), where the right hand holds it at the ready.</summary>
        public static float BowStringOff = 13f;
        const float StowZ0 = -13f;
    }
}
