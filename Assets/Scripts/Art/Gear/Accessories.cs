using System.Collections.Generic;
using UnityEngine;
using static Convergence.Art.Gear.TexelKit;

namespace Convergence.Art.Gear
{
    /// <summary>The accessory a GearSlot.Ring item wears (GearItem.Accessory). APPENDED, never
    /// reordered - an item's kind is read off the catalogue, but keep it stable all the same.</summary>
    public enum AccessoryKind
    {
        None,
        Bandolier,
        VialHarness,
        FeatherCollar,
        BannerPole,
        HourglassPack,
        ChainedTome,
        WristWraps,
        OrbitingStones,
        // the ARM PIECES (ArmPieces.cs): each replaces the whole near arm, shoulder to fingertips
        SalamanderArm,
        QuicksilverArm,
        SaltArm,
        CrucibleArm,
        VerdigrisArm,
        RubedoArm,
        SanguisArm,
        FulmenArm,
        PenumbraArm,
        HomunculusPlatedArm,
        HomunculusHydraulicArm,
    }

    /// <summary>What an accessory is drawn from this frame - filled by the rig
    /// (PrimitiveCharacterRig.Accessory.cs) from the wearer's phial, the run clock and its motion.</summary>
    public sealed class AccessoryState
    {
        public float T;
        public bool Walk;
        public Core.ElementType Element;
        public int Charges = 3, MaxCharges = 3;
        /// <summary>0 none, 1 a drink, 2 a spill; <see cref="P"/> its phase (0..1 over 0.9 s), and
        /// which phial it is (the bandolier's - the charge just taken).</summary>
        public int Anim;
        public float P = -1f;
        public int AnimIdx;
        public readonly bool[] Cracked = new bool[8];
        /// <summary>The run clock as a share of the hour (the hourglass's sand).</summary>
        public float Run;
        /// <summary>0..1, eased: the wraps stream out behind on a dash.</summary>
        public float Dash;
        /// <summary>The worn relic is a sheath (GearItem.IsSheath): the tome is not drawn.</summary>
        public bool RelicSheath;
        /// <summary>The piece's variant (GearItem.AccessoryLook): the collar's bird - Crow, Swan, Peacock,
        /// Phoenix - or the banner's and the wraps' cloth - Crimson, Linen, Ink, Navy, Element. Empty is the
        /// first of each.</summary>
        public string Look;
    }

    /// <summary>A soft light over the piece (the mock's additive glows): mock coordinates, a radius in
    /// texels; a ring is a band at the rim, the heal spreading out from the gulp.</summary>
    public struct AccessoryGlow
    {
        public Vector2 At;
        public float R, A;
        public Color32 C;
        public bool Ring;
        /// <summary>What the light rides: 0 the torso (every accessory), 1 an arm piece's shoulder, 2 its
        /// upper arm, 3 its forearm (<see cref="At"/> then in the forearm picture's unstretched rows).</summary>
        public int Joint;
    }

    /// <summary>
    /// THE ACCESSORIES, drawn live (the user's eight picks, mocked 2026-11-08 - Tools/tier-art/accessories.js;
    /// the mock is the source, this its port, number for number). Each draws up to TWO pictures per view,
    /// <paramref name="a"/> and <paramref name="b"/> in <see cref="Draw"/>, which the rig shows on its two
    /// accessory layers (RigLayer.Accessory / Accessory2), ranked and parented per piece and view by
    /// <see cref="Placement"/>. Mock coordinates: body texels, y down, the rig root at (19, 56); a torso
    /// picture is pivoted on the torso's joint (19, 72), the wraps on each shoulder (5, 30) / (33, 30) with the
    /// rig's elbow stretch taken out (rows from 50 up 6, as bake.js does for gloves).
    ///
    /// FROM BEHIND (the user's call): only the collar, the hourglass and the stones are worn over a cape or
    /// cloak; the rest go under it. The banner's pole goes under too, but what stands above the shoulders
    /// draws over the head.
    /// </summary>
    public static class Accessories
    {
        /// <summary>Where a picture ranks in the rig's stack - see PrimitiveCharacterRig.PlaceAccessories.</summary>
        public enum Rank
        {
            /// <summary>Over the torso and belt, under the relic (Trinket), the arms, the pauldrons and,
            /// turned away, the cape.</summary>
            Under,
            /// <summary>Over the pauldrons, the arms and the head's lower edge; turned away, over the cape and
            /// its drape, under the head.</summary>
            Over,
            /// <summary>Behind the body, over the cape (facing the camera).</summary>
            Behind,
            /// <summary>Over everything.</summary>
            Top,
            /// <summary>Under everything.</summary>
            Bottom,
            /// <summary>Over the near hand (the wraps).</summary>
            NearHand,
            /// <summary>Over the far hand.</summary>
            FarHand,
        }

        public enum Joint { Torso, NearForearm, FarForearm }

        /// <summary>The torso's joint in mock coordinates (bake.js PIVOT).</summary>
        public static readonly Vector2 TorsoPivot = new(19f, 72f);
        public static readonly Vector2 NearShoulder = new(5f, 30f), FarShoulder = new(33f, 30f);
        /// <summary>Mock rows from here down are below the rig's elbow stretch (bake.js ELBOW_ART + EXTRA).</summary>
        public const int ElbowArt = 44, ElbowExtra = 6;

        /// <summary>The two pictures' joints, and where each ranks, per view.</summary>
        public static (Joint a, Joint b, Rank ra, Rank rb) Placement(AccessoryKind kind, bool away) => kind switch
        {
            AccessoryKind.Bandolier      => (Joint.Torso, Joint.Torso, away ? Rank.Under : Rank.Over, Rank.Under),
            AccessoryKind.VialHarness    => (Joint.Torso, Joint.Torso, Rank.Under, Rank.Over),
            AccessoryKind.FeatherCollar  => (Joint.Torso, Joint.Torso, Rank.Over, Rank.Under),
            AccessoryKind.BannerPole     => (Joint.Torso, Joint.Torso, away ? Rank.Under : Rank.Behind, Rank.Top),
            AccessoryKind.HourglassPack  => (Joint.Torso, Joint.Torso, away ? Rank.Top : Rank.Behind, Rank.Under),
            AccessoryKind.ChainedTome    => (Joint.Torso, Joint.Torso, Rank.Under, Rank.Under),
            AccessoryKind.WristWraps     => (Joint.NearForearm, Joint.FarForearm, Rank.NearHand, Rank.FarHand),
            AccessoryKind.OrbitingStones => (Joint.Torso, Joint.Torso, Rank.Top, Rank.Bottom),
            _                            => (Joint.Torso, Joint.Torso, Rank.Under, Rank.Under),
        };

        // ---------------------------------------------------------------- materials

        static Tones _glass, _cork, _wood, _sand, _stone, _tome, _paper;
        static Tones _bronze, _bronzeDk, _leather, _leatherDk, _steel, _gold, _navy, _teal;
        static Tones Glass => _glass ??= Ramp("#7f97a6", "#ffffff", 0.5f);
        static Tones Cork => _cork ??= Ramp("#9a7048", "#f0c890", 0.32f);
        static Tones Wood => _wood ??= Ramp("#5b3d26", "#c89868", 0.30f);
        static Tones Sand => _sand ??= Ramp("#d9b46a", "#fff2c8", 0.40f);
        static Tones Stone => _stone ??= Ramp("#b3122e", "#ff9a7a", 0.45f);
        static Tones TomeCloth => _tome ??= Ramp("#5a1e2a", "#d88a8a", 0.30f);
        static Tones Paper => _paper ??= Ramp("#cdbb90", "#fffbe8", 0.40f);
        // the tier sets' own (sets.js M)
        static Tones Bronze => _bronze ??= Ramp("#B87333", "#ffd98a", 0.36f);
        static Tones BronzeDk => _bronzeDk ??= Ramp("#7f4c22", "#e9a65a");
        static Tones Leather => _leather ??= Ramp("#7a4a2a", "#d9a070", 0.28f);
        static Tones LeatherDk => _leatherDk ??= Ramp("#4a2e1c", "#a8774f", 0.28f);
        static Tones Steel => _steel ??= Ramp("#a3a9b5", "#f4f7ff", 0.40f);
        static Tones Gold => _gold ??= Ramp("#dba73a", "#fff2b0", 0.40f);
        static Tones Navy => _navy ??= Ramp("#2c3a5c", "#8fa8d8");
        static Tones Teal => _teal ??= Ramp("#2a6a68", "#9fe8d8");

        /// <summary>The collar's feathers (the mock's Crow; Swan, Peacock and Phoenix are its variants).</summary>
        static Tones _crow, _crimson;
        static Tones Crow => _crow ??= Ramp("#363a50", "#a8bcf4", 0.40f);
        /// <summary>The banner's and the wraps' cloth (the mock's Crimson).</summary>
        static Tones Crimson => _crimson ??= Ramp("#8a1a2a", "#ff8a8a");

        static readonly Dictionary<Core.ElementType, Tones> _liquid = new();

        /// <summary>The draught, in the wearer's element.</summary>
        static Tones Liquid(Core.ElementType e)
        {
            if (_liquid.TryGetValue(e, out var t)) return t;
            return _liquid[e] = Ramp(ElementHex(e), null, 0.42f, 0.34f);
        }

        static string ElementHex(Core.ElementType e) => e switch
        {
            Core.ElementType.Water => "#409EFF",
            Core.ElementType.Earth => "#8CC75C",
            Core.ElementType.Air   => "#D9D9F2",
            _                      => "#FF6B2E",
        };

        static Color32 ElementColour(Core.ElementType e) => Hex(ElementHex(e));

        // ---------------------------------------------------------------- the variants

        static Tones _swan, _peacock, _phoenix, _phoenixTip, _linen, _ink, _navyCloth;
        static readonly Dictionary<Core.ElementType, Tones> _elementCloth = new();

        /// <summary>The collar's bird: its ramp, the peacock's eyes on the last row, the phoenix's
        /// flame-gold tips.</summary>
        static (Tones r, bool eye, Tones tip) Bird(string look) => look switch
        {
            "Swan"    => (_swan ??= Ramp("#cfccc4", "#ffffff", 0.40f), false, null),
            "Peacock" => (_peacock ??= Ramp("#1f5f5a", "#8fe8c8", 0.34f), true, null),
            "Phoenix" => (_phoenix ??= Ramp("#a8261c", "#ffd070", 0.40f), false, _phoenixTip ??= Ramp("#e8a02a", "#fff2b0", 0.40f)),
            _         => (Crow, false, null),
        };

        /// <summary>EIGHT DIRECTIONS (Body8Art.Extras.cs): a baked accessory's ROLE colours for the worn item -
        /// "pal" the collar's bird or the banner's / wraps' cloth, "tip" the Phoenix's tips, "liq" the phials' liquid
        /// (the wearer's element) - as six tones, line .. glow; null for a role the piece has none of.</summary>
        public static Color[] Ramp8(AccessoryKind kind, string role, string look, Core.ElementType e)
        {
            Tones t = role switch
            {
                "pal" => kind == AccessoryKind.FeatherCollar ? Bird(look).r : Cloth(look, e),
                "tip" => Bird(look).tip,
                "liq" => Liquid(e),
                "stone" => Stone,
                _ => null,
            };
            if (t == null) return null;
            var o = new Color[6];
            for (int i = 0; i < 6; i++) o[i] = t[i];
            return o;
        }

        /// <summary>The banner's and the wraps' cloth; Element is the wearer's own colour.</summary>
        static Tones Cloth(string look, Core.ElementType e)
        {
            switch (look)
            {
                case "Linen": return _linen ??= Ramp("#c2ad7e", "#fff6dc", 0.30f);
                case "Ink": return _ink ??= Ramp("#2a2c3a", "#8a90b0", 0.30f);
                case "Navy": return _navyCloth ??= Ramp("#2c3a5c", "#8fa8d8");
                case "Element":
                    if (!_elementCloth.TryGetValue(e, out var t)) _elementCloth[e] = t = Ramp(ElementHex(e), null, 0.3f, 0.4f);
                    return t;
                default: return Crimson;
            }
        }

        static float Ease(float p) => p < 0f ? 0f : p > 1f ? 1f : p * p * (3f - 2f * p);
        static int MirrorX(int x) => 37 - x;
        static float Abs(float v) => Mathf.Abs(v);
        static float Sin(float v) => Mathf.Sin(v);

        // ---------------------------------------------------------------- the entry point

        /// <summary>Draw <paramref name="kind"/> for this frame into its two pictures' layer lists.</summary>
        public static void Draw(AccessoryKind kind, AccessoryState st, bool away, List<Layer> a, List<Layer> b,
                                List<AccessoryGlow> glows = null)
        {
            a.Clear(); b.Clear();
            glows?.Clear();
            _glows = glows;
            switch (kind)
            {
                case AccessoryKind.Bandolier:
                    if (away) a.Add(BandolierBack()); else a.Add(BandolierFront(st));
                    break;
                case AccessoryKind.VialHarness:
                    if (away) a.Add(HarnessBack()); else { var (straps, flask) = HarnessFront(st); a.Add(straps); b.Add(flask); }
                    break;
                case AccessoryKind.FeatherCollar:
                    a.AddRange(FeatherLayers(st, away));
                    break;
                case AccessoryKind.BannerPole:
                    if (!away) a.Add(BannerFront(st));
                    else
                    {
                        // the pole's split: what stands above the shoulders over the head, the rest under the cape
                        var all = BannerBack(st);
                        Layer lo = new(8.6f), hi = new(24f);
                        foreach (var kv in all.C) (KY(kv.Key) < 29 ? hi : lo).C[kv.Key] = kv.Value;
                        a.Add(lo); b.Add(hi);
                    }
                    break;
                case AccessoryKind.HourglassPack:
                    if (away) a.Add(HourglassBack(st)); else { var (back, over) = HourglassFront(); a.Add(back); b.Add(over); }
                    break;
                case AccessoryKind.ChainedTome:
                    if (st.RelicSheath) break;   // not worn with a sheath (the user's call) - see GearItem.IsSheath
                    a.Add(away ? TomeLayer(st, MirrorX(36), 63, -1f, true) : TomeLayer(st, 36, 63, 1f, false));
                    break;
                case AccessoryKind.WristWraps:
                    a.Add(Unstretched(WrapsFor(true, st, 13.5f)));
                    b.Add(Unstretched(WrapsFor(false, st, 2.5f)));
                    break;
                case AccessoryKind.OrbitingStones:
                {
                    var (front, behind) = StonesLayers(st, away);
                    a.Add(front); b.Add(behind);
                    break;
                }
            }
            _glows = null;
        }

        /// <summary>Where this Draw's glows go (null: not wanted - a card's still picture).</summary>
        static List<AccessoryGlow> _glows;

        static void Glow(float x, float y, float r, Color32 c, float a, bool ring = false)
        {
            if (_glows == null || a <= 0.01f || r <= 0.5f) return;
            _glows.Add(new AccessoryGlow { At = new Vector2(x, y), R = r, C = c, A = a, Ring = ring });
        }

        /// <summary>The drink's light: a swell over the glass as it is drunk, then the heal as a ring
        /// spreading from the chest at the gulp.</summary>
        static void DrinkGlows(float x, float y, float r0, float rk, float a0, float p, Core.ElementType e)
        {
            var c = ElementColour(e);
            float k = Sin(Mathf.PI * Mathf.Min(1f, p / 0.5f));
            Glow(x, y, r0 + rk * k, c, a0 * k);
            if (p > 0.45f && p < 0.8f)
            {
                float q = (p - 0.45f) / 0.35f;
                Glow(19f, 52f, 18f + 40f * q, c, 0.35f * (1f - q), ring: true);
            }
        }

        /// <summary>A forearm piece drawn on the mock's stretched arm, with the stretch taken out.</summary>
        static Layer Unstretched(Layer l)
        {
            var o = new Layer(l.Depth) { Far = l.Far, Outlined = l.Outlined, OutR = l.OutR };
            foreach (var kv in l.C)
            {
                int x = KX(kv.Key), y = KY(kv.Key);
                if (y >= ElbowArt && y < ElbowArt + ElbowExtra) continue;
                o.C[K(x, y >= ElbowArt + ElbowExtra ? y - ElbowExtra : y)] = kv.Value;
            }
            return o;
        }

        // ---------------------------------------------------------------- straps

        internal static Mask StrapMask(float ax, float ay, float bx, float by, float w)
        {
            float dx = bx - ax, dy = by - ay, L = Mathf.Sqrt(dx * dx + dy * dy), nx = -dy / L * w / 2f, ny = dx / L * w / 2f;
            return Poly(new Vector2(ax + nx, ay + ny), new Vector2(bx + nx, by + ny),
                        new Vector2(bx - nx, by - ny), new Vector2(ax - nx, ay - ny));
        }

        internal static void Strap(Layer l, float ax, float ay, float bx, float by, float w, Tones r)
        {
            float dx = bx - ax, dy = by - ay, L = Mathf.Sqrt(dx * dx + dy * dy);
            l.Fill(StrapMask(ax, ay, bx, by, w), (x, y, _) =>
            {
                float u = ((x + .5f - ax) * dx + (y + .5f - ay) * dy) / L, v = ((x + .5f - ax) * -dy + (y + .5f - ay) * dx) / L / (w / 2f);
                bool lit = dx * dy < 0 ? v > 0.45f : v < -0.45f, shade = dx * dy < 0 ? v < -0.45f : v > 0.45f;
                if (lit) return r[4];
                if (shade) return r[2];
                return r[((Mathf.FloorToInt(u) % 4) + 4) % 4 == 0 ? 2 : 3];
            });
        }

        // ---------------------------------------------------------------- the phial (shared)

        /// <summary>A phial 4 wide: cork 2 rows, neck 1, body 7 (corners cut). Returns its mouth.</summary>
        static Vector2 Phial(Layer l, int cx, int top, float level, AccessoryState st,
                             bool cork = true, int corkDy = 0, int corkDx = 0, bool cracked = false)
        {
            int x0 = cx - 2, x1 = cx + 1, bot = top + 9;
            var liq = Liquid(st.Element);
            var body = Rect(x0, top + 3, x1, bot); body.Del(x0, bot); body.Del(x1, bot);
            var neck = Rect(x0 + 1, top + 2, x1 - 1, top + 2);
            var glass = U(body, neck);
            // a dark rim parts it from armour of its own colour
            for (int y = top + 2; y <= bot + 1; y++) { l.Set(x0 - 1, y, Outline); l.Set(x1 + 1, y, Outline); }
            l.Fill(Rect(x0, bot + 1, x1, bot + 1), Outline);
            l.Fill(glass, (x, y, _) => Glass[x == x0 ? 4 : x == x1 ? 1 : 2]);
            int rows = R(level * 5f);
            for (int y = bot - rows + 1; y <= bot; y++)
                for (int x = x0; x <= x1; x++)
                    if (body.Has(x, y)) l.Set(x, y, liq[y == bot - rows + 1 ? 5 : x == x0 ? 4 : x == x1 ? 2 : 3]);
            l.Dot(x0, top + 4, Glass[5]);
            if (cracked) foreach (var (dx, dy) in new[] { (1, 4), (2, 5), (1, 6), (2, 7) }) l.Dot(x0 + dx, top + dy, Outline);
            if (cork)
            {
                int cy = top + corkDy, ccx = cx + corkDx;
                l.Fill(Rect(ccx - 1, cy, ccx, cy + 1), (x, y, _) => Cork[y == cy ? 4 : x == ccx ? 2 : 3]);
            }
            return new Vector2(cx - 0.5f, top + 2);
        }

        /// <summary>The spilled draught, flung out of the mouth and falling.</summary>
        static void SprayDrops(Layer l, float mx, float my, float p, AccessoryState st, float seed)
        {
            var liq = Liquid(st.Element);
            for (int i = 0; i < 7; i++)
            {
                float q = (p - 0.2f - i * 0.02f) / 0.6f;
                if (q <= 0f || q >= 1f) continue;
                float vx = Sin(seed + i * 2.4f) * 14f, vy = -10f - (i % 3) * 4f;
                int x = R(mx + vx * q), y = R(my + vy * q + 46f * q * q);
                l.Set(x, y, liq[4]);
                if (i % 2 == 1) l.Set(x, y + 1, liq[2]);
            }
        }

        // ---------------------------------------------------------------- 1. BANDOLIER

        /// <summary>Shoulder to opposite hip, a phial per drink: they empty as the phial is drunk, the
        /// one being drunk uncorks and drains, a spilled one cracks and sprays.</summary>
        static Layer BandolierFront(AccessoryState st)
        {
            var l = new Layer(14.2f);
            Vector2 A = new(31, 31), B = new(10, 63);
            Strap(l, A.x, A.y, B.x, B.y, 4f, LeatherDk);
            float[] S = { 0.30f, 0.50f, 0.70f };
            for (int i = 0; i < S.Length; i++)
            {
                float px = A.x + (B.x - A.x) * S[i], py = A.y + (B.y - A.y) * S[i];
                int sway = st.Walk ? R(Sin(st.T * 7f + i * 1.9f) * 1.2f) : 0;
                bool active = st.Anim != 0 && st.AnimIdx == i;
                float level = i < st.Charges ? 1f : 0f;
                bool cork = i < st.Charges, cracked = st.Cracked[i];
                int corkDy = 0, corkDx = 0;
                float p = st.P;
                if (active)
                {
                    level = st.Anim == 1 ? 1f - Ease((p - 0.15f) / 0.3f) : 1f - Ease((p - 0.2f) / 0.15f);
                    if (st.Anim == 1)
                    {
                        corkDy = -R(Ease(p / 0.35f) * 10f); corkDx = R(Ease(p / 0.35f) * 4f); cork = p < 0.55f;
                    }
                    else { cracked = p > 0.2f; cork = true; }
                }
                int cx = R(px) + sway, top = R(py) - 4;
                var mouth = Phial(l, cx, top, level, st, cork, corkDy, corkDx, cracked);
                // the loop that holds it, a leather band across the strap and the glass
                l.Fill(Rect(cx - 3, top + 4, cx + 2, top + 4), Leather[3]);
                if (active && st.Anim == 1) DrinkGlows(cx, top + 7, 4f, 8f, 0.55f, p, st.Element);
                if (active && st.Anim == 2) SprayDrops(l, mouth.x, mouth.y + 4, p, st, i);
            }
            return l;
        }

        /// <summary>The strap alone, from the other shoulder's side.</summary>
        static Layer BandolierBack()
        {
            var l = new Layer(15f);
            Strap(l, MirrorX(31), 31, MirrorX(10), 63, 4f, LeatherDk);
            l.Fill(Rect(MirrorX(10) - 2, 60, MirrorX(10) + 1, 63), Bevel(Bronze, 1, 3));
            return l;
        }

        // ---------------------------------------------------------------- 4. VIAL HARNESS

        /// <summary>Crossed straps, a round flask on the sternum holding the phial's drinks as thirds; its
        /// surface sloshes with the motion; a drink lifts the cork, drains a third and bubbles; a spill
        /// cracks the glass and sprays.</summary>
        static (Layer straps, Layer flask) HarnessFront(AccessoryState st)
        {
            Layer straps = new(8.6f), flask = new(14.2f);
            var liq = Liquid(st.Element);
            Strap(straps, 9, 31, 30, 62, 3f, Leather); Strap(straps, 30, 31, 9, 62, 3f, Leather);
            const float cx = 19.5f; const int cy = 47, Rr = 5;
            float p = st.P;
            bool active = st.Anim != 0, drink = st.Anim == 1;
            float max = Mathf.Max(1, st.MaxCharges);
            // the charge being drunk was already taken (Phial.Begin): it drains from what was there
            float level = st.Charges / max;
            if (active) level = (st.Charges + 1 - (drink ? Ease((p - 0.15f) / 0.3f) : Ease((p - 0.2f) / 0.15f))) / max;
            var bulb = Ell(cx, cy, Rr, Rr);
            flask.Fill(bulb, Dome(Glass, cx - 2, cy - 2, Rr + 1, Rr + 1, -0.35f));
            flask.Fill(Rect(19, cy - Rr - 2, 20, cy - Rr + 1), (x, y, _) => Glass[x == 19 ? 4 : 1]);
            float tilt = Sin(st.T * (st.Walk ? 5.2f : 2.4f)) * (st.Walk ? 0.32f : 0.14f) + (active && !drink ? 0.4f : 0f);
            var inner = Ell(cx, cy, Rr - 1, Rr - 1);
            float surfY0 = cy + (Rr - 1) - level * 2f * (Rr - 1);
            foreach (var (x, y) in inner.Pts())
            {
                float s = surfY0 + (x + 0.5f - cx) * tilt;
                if (level > 0.01f && y + 0.5f >= s)
                    flask.Set(x, y, liq[y + 0.5f - s < 1.2f ? 5 : x < cx - 2 ? 4 : x > cx + 2 ? 2 : 3]);
            }
            flask.Dot(Mathf.FloorToInt(cx) - 2, cy - 2, Glass[5]);
            // the brass cage: a band round the waist of the flask and the collar at the neck
            flask.Over(Rect(-40, cy, 80, cy), (x, y, _) => Bronze[x < cx ? 5 : 2]);
            flask.Fill(Rect(18, cy - Rr - 1, 21, cy - Rr - 1), (x, y, _) => Bronze[x < 20 ? 5 : 2]);
            // the cork lifts and reseats on a drink
            int cdy = active && drink ? -R(Sin(Mathf.PI * Mathf.Min(1f, p / 0.6f)) * 4f) : 0;
            flask.Fill(Rect(19, cy - Rr - 4 + cdy, 20, cy - Rr - 3 + cdy), (x, y, _) => Cork[y == cy - Rr - 4 + cdy ? 4 : 3]);
            bool cracked = false;
            for (int i = 0; i < st.MaxCharges && i < st.Cracked.Length; i++) cracked |= st.Cracked[i];
            if (cracked || (active && !drink && p > 0.2f))
                foreach (var (dx, dy) in new[] { (-1, -2), (0, -1), (-1, 0), (0, 1) }) flask.Dot(R(cx) + dx, cy + dy, Outline);
            if (active)
            {
                if (drink)
                {
                    // bubbles rising through what is left
                    for (int i = 0; i < 4; i++)
                    {
                        float q = (p * 3f + i * 0.27f) % 1f;
                        int bx = R(cx - 1 + (i * 1.7f) % 3f), by = R(cy + Rr - 1 - q * 7f);
                        if (inner.Has(bx, by) && by + 0.5f >= surfY0 - 1f) flask.Set(bx, by, liq[5]);
                    }
                    DrinkGlows(cx, cy, 5f, 7f, 0.6f, p, st.Element);
                }
                else SprayDrops(flask, cx, cy - Rr - 3, p, st, 3f);
            }
            return (straps, flask);
        }

        static Layer HarnessBack()
        {
            var l = new Layer(15f);
            Strap(l, 7, 31, 30, 62, 3f, Leather); Strap(l, 30, 31, 7, 62, 3f, Leather);
            l.Fill(Ell(18.5f, 46, 2.5f, 2.5f), Dome(Bronze, 17, 45, 3, 3));
            return l;
        }

        // ---------------------------------------------------------------- 5. FEATHER COLLAR

        /// <summary>Rows of feathers SHINGLED down from the shoulder line, tips hanging down and out, never up
        /// past the chin: the lower rows drawn first so each covers the roots of the one below. A ruffle runs
        /// through them, quicker at a walk.</summary>
        static void Shingles(Layer l, AccessoryState st, (Tones r, bool eye, Tones tip) bird, float xs0, float xs1,
                             System.Func<float, float> topY, int rows, System.Func<float, float> lenAt, float side)
        {
            const float Step = 5f;
            var r = bird.r;
            var list = new List<(int row, int fx)>();
            for (int row = rows - 1; row >= 0; row--)
            {
                float stagger = row % 2 == 1 ? Step / 2f : 0f;
                for (float fx = xs0 + stagger; fx <= xs1 + 0.01f; fx += Step) list.Add((row, R(fx)));
            }
            foreach (var (row, fx) in list)
            {
                float k = lenAt(fx);
                if (k <= 0f) continue;
                float lean = Mathf.Clamp((19.5f - fx) * 1.4f, -30f, 30f) * side;
                float ruff = Sin(st.T * (st.Walk ? 6f : 2.2f) + fx * 0.7f + row) * (st.Walk ? 4f : 2f) * (0.6f + row * 0.4f);
                float deg = lean + ruff, a = deg * Mathf.Deg2Rad;
                float ry = 5.5f * k, rx = 2.7f;
                float rootY = topY(fx) + row * 5f * k;
                float ux = -Sin(a), uy = Mathf.Cos(a);   // root -> tip
                float cx = fx + ux * ry * 0.85f, cy = rootY + uy * ry * 0.85f;
                var m = Rell(cx, cy, rx, ry, deg);
                bool last = row == rows - 1;
                (float u, float v) At(int x, int y)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    return ((dx * ux + dy * uy) / ry, (dx * uy - dy * ux) / rx);
                }
                foreach (var (x, y) in m.Pts())
                {
                    var (u, v) = At(x, y);
                    Color32 c;
                    if (bird.eye && last && u > 0.3f)                   // the peacock's eye
                        c = u > 0.62f && Abs(v) < 0.45f ? Navy[2] : u > 0.48f ? Gold[4] : Teal[4];
                    else if (bird.tip != null && last && u > 0.25f)     // the phoenix's flame tip
                        c = bird.tip[v < -0.3f ? 5 : v > 0.3f ? 3 : 4];
                    else if (Abs(v) < 0.22f && u < 0.6f) c = r[2];     // the quill
                    else if (v < -0.25f) c = r[u > 0.5f ? 5 : 4];       // the lit vane, its tip catching the sheen
                    else c = r[v > 0.5f ? 2 : 3];
                    l.Set(x, y, c);
                }
                // a dark seam round the lower half and the shaded side, so each feather stands apart
                foreach (var (x, y) in Band(m, 1).Pts())
                {
                    var (u, v) = At(x, y);
                    if ((u > 0f && !m.Has(x, y + 1)) || (v > 0.4f && !m.Has(x + 1, y))) l.Set(x, y, r[0]);
                }
            }
        }

        static IEnumerable<Layer> FeatherLayers(AccessoryState st, bool away)
        {
            var r = Bird(st.Look);
            if (away)
            {
                // shoulder to shoulder and out over the pauldrons; the top where the armour begins (behind the
                // neck, up under the head), the hem dipping to a point at the spine
                var l = new Layer(14.6f);
                float Top(float x) => 22f + Mathf.Min(5f, Abs(x - 19f) * 0.24f);
                float Hem(float x) => 26f + Abs(x - 19f) * 0.12f + 20.2f * (1.15f - Abs(x - 19f) / 21f * 0.4f);
                Shingles(l, st, r, -6, 44, Top, 3, x => (Hem(x) - Top(x)) / 20.2f, -1f);
                yield return l;
                yield break;
            }
            // one piece on each shoulder from the side of the neck out past the pauldron, the chest left open
            Layer near = new(14.6f), far = new(14.5f) { Far = true };
            Shingles(near, st, r, -3, 11, _ => 31f, 2, x => 1f - Abs(x - 4f) / 12f * 0.35f, 1f);
            Shingles(far, st, r, 27, 40, _ => 31f, 2, x => 1f - Abs(x - 33f) / 10f * 0.35f, 1f);
            yield return near;
            yield return far;
        }

        // ---------------------------------------------------------------- 6. BANNER POLE

        /// <summary>A vertical banner hung from a crossbar, its free edge rippling; the sigil a circumpunct.</summary>
        static void BannerFlag(Layer l, AccessoryState st, int x0, int dir, int top)
        {
            var r = Cloth(st.Look, st.Element);
            const int w = 12, h = 27;
            float rate = st.Walk ? 6.5f : 3.4f;
            var m = new Mask();
            for (int y = top; y < top + h; y++)
            {
                int ripple = R(Sin(st.T * rate - (y - top) * 0.32f) * (st.Walk ? 1.6f : 1.0f));
                int ext = w + (y - top > 3 ? ripple : 0), notch = y > top + h - 5 ? y - (top + h - 5) : -1;
                for (int i = 0; i < ext; i++)
                {
                    if (notch >= 0 && Abs(i - w / 2f) < notch) continue;
                    m.Add(x0 + dir * i, y);
                }
            }
            l.Fill(m, (x, y, _) =>
            {
                int i = (x - x0) * dir;
                float sl = Sin(st.T * rate - (y - top) * 0.32f + 1.2f);
                return r[i == 0 ? 2 : i == 1 ? 4 : sl > 0.4f ? 4 : sl < -0.4f ? 2 : 3];
            });
            int gx = x0 + dir * 6, gy = top + 11;
            l.Over(Band(Ell(gx, gy, 4.2f, 4.2f), 1), Gold[4]);
            l.Over(Rect(gx - 1, gy - 1, gx, gy), Gold[5]);
            l.Over(Rect(x0 - 40, top + h - 9, x0 + 40, top + h - 9), Gold[3]);
        }

        static void Finial(Layer l, int X)
            => l.Fill(Poly(new Vector2(X + .5f, -49), new Vector2(X + 2.5f, -44), new Vector2(X + 1.5f, -40),
                           new Vector2(X - .5f, -40), new Vector2(X - 1.5f, -44)), (x, y, _) => Bronze[x <= X ? 5 : 2]);

        static Layer BannerFront(AccessoryState st)
        {
            var l = new Layer(0.5f);
            const int X = 34;
            l.Fill(Rect(X, -40, X + 1, 40), (x, y, _) => Wood[x == X ? 4 : 2]);
            l.Fill(Rect(X, -39, X + 14, -38), (x, y, _) => Wood[y == -39 ? 4 : 2]);
            Finial(l, X);
            BannerFlag(l, st, X + 2, 1, -37);
            return l;
        }

        static Layer BannerBack(AccessoryState st)
        {
            var l = new Layer(15f);
            const int X = 2;
            l.Fill(Rect(X, -40, X + 1, 58), (x, y, _) => Wood[x == X ? 4 : 2]);
            l.Fill(Rect(X - 13, -39, X + 1, -38), (x, y, _) => Wood[y == -39 ? 4 : 2]);
            Finial(l, X);
            BannerFlag(l, st, X - 1, -1, -37);
            // its socket on the back: two bronze collars and a strap to the other shoulder
            Strap(l, X, 34, 33, 30, 3f, LeatherDk); Strap(l, X, 54, 30, 60, 3f, LeatherDk);
            foreach (int y0 in new[] { 33, 53 }) l.Fill(Rect(X - 1, y0, X + 2, y0 + 2), (x, y, _) => Bronze[y == y0 ? 5 : 2]);
            return l;
        }

        // ---------------------------------------------------------------- 8. HOURGLASS PACK

        /// <summary>The glass between the shoulder blades: its sand is the RUN CLOCK - the top drains toward
        /// the neck in a funnel, the bottom heaps into a mound, a thread of sand falls between.</summary>
        static void Hourglass(Layer l, AccessoryState st, int cx, int top)
        {
            float f = Mathf.Clamp01(st.Run);
            const int H = 34;
            void Plate(int y) => l.Fill(Rect(cx - 10, y, cx + 9, y + 2), Bevel(Bronze, 1, 3));
            Plate(top); Plate(top + H - 2);
            foreach (int x0 in new[] { cx - 9, cx + 7 }) l.Fill(Rect(x0, top + 3, x0 + 1, top + H - 3), (x, y, _) => BronzeDk[x == x0 ? 4 : 2]);
            var topB = Ell(cx, top + 9.5f, 6, 7).Clip(Rect(-40, top + 3, 80, top + 15));
            var botB = Ell(cx, top + H - 10.5f, 6, 7).Clip(Rect(-40, top + 17, 80, top + H - 3));
            var neck = Rect(cx - 1, top + 15, cx, top + 17);
            l.Fill(U(topB, botB, neck), (x, y, _) => Glass[x <= cx - 4 ? 4 : x >= cx + 3 ? 1 : 2]);
            float sTop = top + 15 - (1f - f) * 11f;
            foreach (var (x, y) in topB.Pts())
                if (y + 0.5f > sTop + Mathf.Max(0f, 2.2f - Abs(x + 0.5f - cx)) * (f > 0.02f ? 0.9f : 0f) && Abs(x + 0.5f - cx) < 5.5f)
                    l.Set(x, y, Sand[x < cx - 1 ? 4 : 3]);
            float pile = top + H - 3 - f * 11f;
            foreach (var (x, y) in botB.Pts())
            {
                float edge = pile + Abs(x + 0.5f - cx) * 0.55f;
                if (y + 0.5f > edge && Abs(x + 0.5f - cx) < 5.5f) l.Set(x, y, Sand[y + 0.5f < edge + 1.2f ? 4 : 3]);
            }
            if (f > 0f && f < 1f)
                for (int y = top + 17; y < pile; y++)
                    if (Sin(st.T * 23f + y * 2.7f) > -0.3f) l.Set(y % 2 != 0 ? cx - 1 : cx, y, Sand[5]);
            l.Dot(cx - 4, top + 6, Glass[5]); l.Dot(cx - 4, top + 7, Glass[5]); l.Dot(cx - 4, top + H - 13, Glass[5]);
        }

        static void Yoke(Layer l, int X, int stemBottom)
        {
            l.Fill(Rect(X, 16, X + 1, stemBottom), (x, y, _) => BronzeDk[x == X ? 4 : 2]);
            l.Fill(Ell(X + 1, 14, 3, 3), Dome(Bronze, X, 13, 3.5f, 3.5f));
        }

        /// <summary>From the front: the brass yoke's finials stand up behind the shoulders, the straps come
        /// down the chest.</summary>
        static (Layer back, Layer over) HourglassFront()
        {
            Layer back = new(0.6f), over = new(8.6f);
            foreach (int X in new[] { -4, 41 }) Yoke(back, X, 31);
            foreach (int x0 in new[] { 10, 27 }) over.Fill(Rect(x0, 29, x0 + 1, 46), (x, y, _) => LeatherDk[x == x0 ? 4 : 2]);
            foreach (int x0 in new[] { 10, 27 }) over.Fill(Rect(x0 - 1, 44, x0 + 2, 45), (x, y, _) => Bronze[y == 44 ? 5 : 2]);
            return (back, over);
        }

        static Layer HourglassBack(AccessoryState st)
        {
            var l = new Layer(15f);
            l.Fill(Rect(-4, 29, 42, 31), (x, y, _) => Bronze[y == 29 ? 5 : 2]);   // the yoke over the shoulders
            foreach (int X in new[] { -4, 41 }) Yoke(l, X, 28);
            Hourglass(l, st, 19, 32);
            return l;
        }

        // ---------------------------------------------------------------- 9. CHAINED TOME

        /// <summary>A book on a chain from the belt, on the RELIC'S hip (the far one), hung behind the relic
        /// and further out, so it fans out from behind whichever relic is worn; it swings with the walk.</summary>
        static Layer TomeLayer(AccessoryState st, int ax, int ay, float side, bool back)
        {
            var l = new Layer(back ? 15f : 8.4f);
            float swing = Sin(st.T * (st.Walk ? 5.5f : 2.2f)) * (st.Walk ? 10f : 4f) * side;
            int bob = st.Walk ? R(Abs(Sin(st.T * 5.5f)) * 1.5f) : 0;
            float a = swing * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Sin(a);
            Vector2 ToW(float lx, float ly) => new(ax + lx * c - ly * s, ay + bob + lx * s + ly * c);
            Vector2 ToL(int x, int y)
            {
                float dx = x + 0.5f - ax, dy = y + 0.5f - ay - bob;
                return new Vector2(dx * c + dy * s, -dx * s + dy * c);
            }
            // the chain: alternating links from the belt to the book's top corner
            for (int i = 0; i <= 7; i++)
            {
                var w = ToW(i % 2 == 1 ? 0f : -0.5f, i + 0.5f);
                int fx = Mathf.FloorToInt(w.x), fy = Mathf.FloorToInt(w.y);
                l.Set(fx, fy, Steel[i % 2 == 1 ? 4 : 2]);
                if (i % 2 == 0) l.Set(fx + 1, fy, Steel[1]);
            }
            l.Fill(Rect(ax - 1, ay - 1, ax + 1, ay + 1), (x, y, _) => Steel[x == ax - 1 ? 5 : 2]);
            // the book: hanging from the chain by its top centre
            var m = new Mask();
            for (int y = ay - 4; y <= ay + 36; y++)
                for (int x = ax - 18; x <= ax + 18; x++)
                {
                    var p = ToL(x, y);
                    if (p.x >= -8f && p.x < 8f && p.y >= 8f && p.y < 27f) m.Add(x, y);
                }
            l.Fill(m, (x, y, _) =>
            {
                var p = ToL(x, y);
                float lx = p.x, ly = p.y;
                bool fore = back ? lx < -6.5f : lx >= 6.5f, spine = back ? lx >= 6.6f : lx < -6.6f;
                bool corner = (lx < -4.5f || lx >= 4f) && (ly < 11f || ly >= 24f);
                if (fore) return Paper[Mathf.FloorToInt(ly) % 2 != 0 ? 4 : 2];
                if (corner) return Gold[lx < 0f && ly < 15f ? 5 : 2];
                if (spine) return TomeCloth[1];
                if (!back)
                {
                    float d = Mathf.Sqrt((lx + 0.5f) * (lx + 0.5f) + (ly - 17.5f) * (ly - 17.5f));
                    if (d > 3.2f && d < 4.5f) return Gold[lx < 0f ? 5 : 3];
                    if (d < 1.5f) return Gold[4];
                    if (lx >= 3.5f && lx < 6.5f && ly >= 16f && ly < 19f) return Bronze[ly < 17f ? 5 : 3];   // the clasp
                }
                return TomeCloth[lx < -3f ? 4 : lx > 3f ? 2 : 3];
            });
            return l;
        }

        // ---------------------------------------------------------------- 10. WRIST WRAPS

        /// <summary>A cloth band on each wrist and two tails from its outer edge: hanging and swaying, or
        /// streaming out behind on a dash.</summary>
        static Layer WrapsFor(bool near, AccessoryState st, float depth)
        {
            var r = Cloth(st.Look, st.Element);
            var l = new Layer(depth) { Far = !near };
            var band = near ? Rect(-1, 50, 10, 60) : Rect(37 - 10, 50, 37 + 1, 60);
            l.Fill(band, (x, y, _) =>
            {
                int xx = near ? x : 37 - x;
                return r[(((xx + y) % 4) + 4) % 4 < 2 ? (xx < 2 ? 4 : 3) : (xx > 7 ? 1 : 2)];
            });
            float x0 = near ? -0.5f : 38.5f, y0 = 58f, dirOut = near ? -1f : 1f;
            var tails = new[] { (24f, 0f), (18f, 1.7f) };
            for (int ti = 0; ti < tails.Length; ti++)
            {
                var (len, ph) = tails[ti];
                for (float s = 0f; s <= len; s += 0.5f)
                {
                    float wob = Sin(st.T * (st.Walk ? 7f : 3.2f) - s * 0.3f + ph) * Mathf.Min(1f, s / 6f) * (st.Walk ? 2.2f : 1.3f);
                    float hx = x0 + dirOut * (s * 0.18f + ti * 1.5f) + wob, hy = y0 + s;
                    float sx2 = x0 + dirOut * (s + 1 + ti);
                    float sy2 = y0 + s * 0.2f + Sin(st.T * 14f - s * 0.5f + ph) * Mathf.Min(1f, s / 5f) * 2f - ti * 2f;
                    float x = Mathf.Lerp(hx, sx2, st.Dash), y = Mathf.Lerp(hy, sy2, st.Dash);
                    int w = s < len - 6f ? 3 : 2;
                    for (int k = 0; k < w; k++)
                    {
                        int px = Mathf.FloorToInt(x) + k * (st.Dash > 0.5f ? 0 : 1);
                        int py = Mathf.FloorToInt(y) + k * (st.Dash > 0.5f ? 1 : 0);
                        l.Set(px, py, r[k == 0 ? 4 - ti : 2 - ti]);
                    }
                }
            }
            return l;
        }

        // ---------------------------------------------------------------- 19. ORBITING STONES

        /// <summary>Three red stones circling the waist, spinning as they go: over everything on the near
        /// half of the orbit, under everything on the far half.</summary>
        public static readonly Color32 StoneLight = new(0xff, 0x3a, 0x3a, 0xff);

        static (Layer front, Layer behind) StonesLayers(AccessoryState st, bool away)
        {
            Layer front = new(away ? 99f : 16f) { OutR = 2 }, behind = new(-1f) { Far = true, OutR = 2 };
            for (int i = 0; i < 3; i++)
            {
                float a = st.T * 0.95f + i * Mathf.PI * 2f / 3f;
                bool nearCam = Sin(a) > 0f;
                float cx = 19f + Mathf.Cos(a) * 28f * (away ? -1f : 1f);
                float cy = 46f + Sin(a) * 7f + Sin(st.T * 2f + i * 2f) * 1.5f;
                float spin = Abs(Mathf.Cos(st.T * 2.2f + i));
                int hw = Mathf.Max(1, R(spin * 4.5f));
                const float H = 8f;
                var l = nearCam ? front : behind;
                var m = Poly(new Vector2(cx, cy - H), new Vector2(cx + hw, cy - 1), new Vector2(cx + hw * 0.6f, cy + H * 0.7f),
                             new Vector2(cx - hw * 0.6f, cy + H * 0.7f), new Vector2(cx - hw, cy - 1));
                l.Fill(m, (x, y, _) => Stone[y < cy - H + 3 ? 5 : x + 0.5f < cx ? 4 : 2]);
                Glow(cx, cy, 12f, StoneLight, nearCam ? 0.42f : 0.18f);
            }
            return (front, behind);
        }
    }
}
