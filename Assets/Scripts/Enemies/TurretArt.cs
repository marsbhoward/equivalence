using System.Text;
using UnityEngine;
using Convergence.Art;

namespace Convergence.Enemies
{
    /// <summary>
    /// Procedural pixel art for Turret's two coexisting looks - see <see cref="EnemyLooks"/>.
    /// Four sprites per look, in EnemyStage order (Idle/Mid/Full/Cooling), read by
    /// EnemyStageCycle off EnemyController.TurretChargeProgress01/Recovering.
    ///
    /// THE EYE WIDENS AND BRIGHTENS, THE SAME DIRECTION BOMB'S OWN CRACK TAKES - and deliberately
    /// does NOT copy the ground-level charge ring, which already CONTRACTS as a turret arms
    /// ("closing rather than growing, so 'about to fire' reads as the ring arriving at the turret"
    /// - see UpdateTurretChargeVisual). A shrinking body aperture was tried and rendered first: it
    /// put the SMALLEST, least noticeable dot on the stage that most needs to read as alarming,
    /// which is backwards. Widening reads as an eye dilating rather than restating the ring's own
    /// "expanding = a blast already happened" grammar, so the two cues stay complementary instead
    /// of duplicating each other. Cooling shrinks the SAME aperture back down, dim - the eye
    /// relaxing to rest, not a new mechanism.
    ///
    /// Both looks share Turret's own cyan (the tint EnemyDef has always coloured its placeholder
    /// circle) for the eye/gem itself, and diverge only in what the eye sits in: a riveted
    /// mechanical housing for the automaton, a faceted mineral spire for the construct - the same
    /// "shared warning colour, different carrier" split Bomb's two looks already use.
    /// </summary>
    public static class TurretArt
    {
        static readonly Palette.Ramp Eye      = new(new Color(0.30f, 0.85f, 0.95f)); // Turret's own tint
        static readonly Palette.Ramp Gunmetal = new(new Color(0.42f, 0.46f, 0.52f));
        static readonly Palette.Ramp Crystal  = new(new Color(0.40f, 0.36f, 0.52f));

        /// <summary>
        /// The automaton's second material - the tracked base's turntable ring, the collar the
        /// barrels bolt to, and the strut lashing them together. A MATERIAL rather than a tier
        /// swatch (see Palette.Tier's own warning), chosen to read as riveted copper fittings
        /// against the gunmetal housing, the same two-metal read the reference mech turret uses.
        /// </summary>
        static readonly Palette.Ramp Bronze = new(new Color(0.68f, 0.47f, 0.24f));

        /// <summary>
        /// A look's art, split into the piece that stays put and the piece that AIMS. Every
        /// Turret can now swivel to face whoever it's shooting - see <see cref="Attach"/> - so
        /// each look has to say which part of its own picture is the gun.
        ///
        /// <see cref="Base"/> is null for a look with no fixed mount (Construct - see below):
        /// the whole body IS the aiming piece there, pivoted at its own root instead of split.
        /// <see cref="BaseFrac"/>/<see cref="HeadFrac"/> are the WORLD-HEIGHT shares the two
        /// pieces occupy stacked on top of each other, summing to 1 - not texel counts, so a
        /// look's total silhouette still fills exactly EnemyDef.Size whichever way it's split.
        /// </summary>
        public readonly struct Rig
        {
            public readonly Sprite Base;
            public readonly Sprite[] Head;
            public readonly float BaseFrac, HeadFrac;
            public Rig(Sprite @base, Sprite[] head, float baseFrac, float headFrac)
            {
                Base = @base; Head = head; BaseFrac = baseFrac; HeadFrac = headFrac;
            }
        }

        static Rig _construct, _automaton;
        static bool _constructBuilt, _automatonBuilt;

        public static Rig Construct
        {
            get { if (!_constructBuilt) { _construct = BuildConstruct(); _constructBuilt = true; } return _construct; }
        }
        public static Rig Automaton
        {
            get { if (!_automatonBuilt) { _automaton = BuildAutomaton(); _automatonBuilt = true; } return _automaton; }
        }

        public static Rig For(EnemyLook look) => look == EnemyLook.Construct ? Construct : Automaton;

        /// <summary>
        /// Wires a spawned Turret's visuals from its <see cref="Rig"/>: the fixed piece (if any)
        /// goes on the already-built <paramref name="baseVisual"/> from ArtBinder, and the aiming
        /// piece gets its own child - <see cref="EnemyController.SetTurretHead"/> hands the
        /// controller that child's Transform so it can swivel it toward the target every frame,
        /// the same rotation math <c>UpdateBeamVisual</c> already points the beam itself with.
        ///
        /// Both pieces are placed so that, unrotated, they reproduce exactly what a single
        /// un-split sprite centred on the enemy's own position used to look like: the head's
        /// pivot (its own root, wherever <see cref="Rig"/> baked it) lands on the SEAM between the
        /// two fractions, and the base - centred pivot, ordinary sprite - sits in the remaining
        /// span below it. A look with no split (Construct) has no seam to find: its "base" is the
        /// enemy's own feet, so the whole piece is grounded at -size/2 instead.
        /// </summary>
        public static void Attach(GameObject enemyRoot, GameObject baseVisual, EnemyController ec,
                                  EnemyKind kind, EnemyLook look, float size)
        {
            var rig = For(look);
            var baseSr = baseVisual.GetComponent<SpriteRenderer>();
            int baseOrder = baseSr != null ? baseSr.sortingOrder : 0;

            Transform headTransform;
            if (rig.Base != null)
            {
                baseSr.sprite = rig.Base;
                float baseNative = rig.Base.bounds.size.y;
                if (baseNative > 0.0001f)
                    baseVisual.transform.localScale = Vector3.one * (size * rig.BaseFrac / baseNative);
                baseVisual.transform.localPosition = new Vector3(0f, size * (rig.BaseFrac / 2f - 0.5f), 0f);

                var headGo = new GameObject("turret.head");
                headGo.transform.SetParent(enemyRoot.transform, false);
                headGo.transform.localPosition = new Vector3(0f, size * (rig.BaseFrac - 0.5f), 0f);
                var headSr = headGo.AddComponent<SpriteRenderer>();
                // Above the hull it's bolted to, always - the same "everything drawn stands in
                // front of what it stands ON" rule SortingOrders states for the depth band.
                headSr.sortingOrder = baseOrder + 1;

                EnemyStageCycle.Attach(headGo, rig.Head, size * rig.HeadFrac, ec, kind);
                headTransform = headGo.transform;
            }
            else
            {
                // No split: the whole body is the aiming piece, rooted at the enemy's own feet
                // rather than centred, so it swivels about where it's actually planted.
                baseVisual.transform.localPosition = new Vector3(0f, -size / 2f, 0f);
                EnemyStageCycle.Attach(baseVisual, rig.Head, size * rig.HeadFrac, ec, kind);
                headTransform = baseVisual.transform;
            }

            ec.SetTurretHead(headTransform);
        }

        // Idle, Mid, Full, Cooling - same order as EnemyStage.
        //
        // THE EYE WIDENS; ONLY THE GROUND RING CLOSES. Both were tried shrinking together and
        // rendered to compare - shrinking the body's own aperture put the SMALLEST, least
        // noticeable dot on the stage that most needs to read as alarming, which is backwards:
        // "about to fire" should be the loudest frame, not the quietest. Widening and brightening
        // (the same direction Bomb's own crack takes) fixes that and still avoids restating the
        // ground ring's cue, because a filled disc growing reads as an eye dilating, not as the
        // "expanding ring = a blast already happened" grammar the ring itself exists to avoid.
        static readonly float[] StageRadius = { 1.0f, 2.1f, 3.4f, 1.0f };
        static readonly char[] StageTone    = { 'B', 'L', 'H', 'D' }; // uppercase: Construct's 2nd material (Eye)

        // Automaton is now a 3-material grid (Gunmetal/Bronze/Eye - see AutomatonRows), so its own
        // stage tone rides the DIGIT ramp (the third material) instead of uppercase, which Bronze
        // now owns. Same Base/Light/Glow/Dark progression as StageTone above, different alphabet.
        static readonly char[] AutomatonStageTone = { '4', '5', '6', '3' };

        // ================================================================== automaton: tracked mech turret

        // Ground up: tracks, turntable ring, gunmetal housing, barrel collar, the barrel cluster
        // itself. Modelled on a reference tri-barrel mech turret on tank treads - bronze fittings
        // banding a gunmetal hull, three barrels bundled by a strut with the centre one leading.
        const int AutoW = 32, AutoH = 34;
        const int TrackRows = 6, RingRows = 3, BodyRows = 9, CollarRows = 3;
        const int RingTop   = TrackRows + RingRows;               // 9
        const int BodyTop   = RingTop + BodyRows;                 // 18
        const int CollarTop = BodyTop + CollarRows;                // 21 - barrels start here
        const float BarrelHalf   = 2.4f;
        const float BarrelSpread = 6.6f;
        const int SideBarrelTip   = AutoH - 3;   // flanking barrels are shorter
        const int CentreBarrelTip = AutoH - 1;   // the centre barrel leads the cluster
        const int StrutRowLit  = CollarTop + 5;  // the bronze band lashing all three barrels
        const int StrutRowDark = CollarTop + 6;

        const int HeadRows = AutoH - BodyTop; // 16 - collar + barrels, everything that swivels

        static Rig BuildAutomaton()
        {
            var palette = Palette.Of(Gunmetal, Bronze, Eye);
            var baseSprite = PixelSprite.From("enemy.turret.automaton.base", AutomatonBaseRows(), palette, outline: true);

            var head = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var rows = AutomatonHeadRows(StageRadius[i], AutomatonStageTone[i]);
                head[i] = PixelSprite.From($"enemy.turret.automaton.head.{i}", rows, palette,
                                           outline: true, pivotTexel: new Vector2Int(AutoW / 2, HeadRows - 1));
            }
            return new Rig(baseSprite, head, BodyTop / (float)AutoH, HeadRows / (float)AutoH);
        }

        /// <summary>Tank tracks -> a bronze turntable ring -> a riveted gunmetal housing. The hull
        /// never turns - only the gun on top of it does (see <see cref="AutomatonHeadRows"/>),
        /// the same way a real tank's tracks hold a fixed heading while its turret swivels.</summary>
        static string[] AutomatonBaseRows()
        {
            const int w = AutoW, h = BodyTop;
            float cx = (w - 1) / 2f;

            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var sb = new StringBuilder(w);
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;

                    // ---- tank tracks ----
                    if (y < TrackRows)
                    {
                        float t = TrackRows <= 1 ? 0f : y / (float)(TrackRows - 1);
                        float halfW = Mathf.Lerp(15f, 13f, t);
                        if (Mathf.Abs(dx) > halfW) { sb.Append('.'); continue; }
                        bool link = ((x + (y % 2)) % 4) < 2; // alternating tread-link stripes
                        sb.Append(y == 0 ? 'd' : link ? 's' : 'd');
                        continue;
                    }

                    // ---- turntable ring (bronze) ----
                    if (y < RingTop)
                    {
                        if (Mathf.Abs(dx) > 12f) { sb.Append('.'); continue; }
                        bool rivet = y == RingTop - 1 && x % 4 == 0;
                        sb.Append(rivet ? 'S' : y == TrackRows ? 'D' : 'L');
                        continue;
                    }

                    // ---- gunmetal housing ----
                    {
                        float t = (y - RingTop) / (float)(BodyRows - 1);
                        float halfW = Mathf.Lerp(11f, 9f, t);
                        if (y >= BodyTop - 2)
                            halfW *= Mathf.Lerp(1f, 0.72f, y - (BodyTop - 2));
                        if (Mathf.Abs(dx) > halfW) { sb.Append('.'); continue; }
                        bool rivet = ((x + 2) % 4 == 0) && ((y - RingTop) % 3 == 1);
                        bool lit = dx <= 0f && y >= RingTop + 2;
                        sb.Append(rivet ? 'd' : y == RingTop ? 'd' : lit ? 'l' : 'b');
                    }
                }
                rows[h - 1 - y] = sb.ToString();
            }
            return rows;
        }

        /// <summary>
        /// The bronze collar and the three barrels it clamps - the whole gun, as one rigid piece
        /// pivoted at its own base (see the <c>pivotTexel</c> passed in <see cref="BuildAutomaton"/>)
        /// so <see cref="Attach"/> can swivel it to face whoever the turret is shooting at,
        /// exactly the way the beam itself already rotates to reach the target.
        ///
        /// The bore glow at each barrel's own tip is the shared "eye" - all three widen and
        /// brighten together off the same apertureR/apertureTone StageCycle already drives every
        /// other stage in the file with, so a charging turret reads as one weapon arming rather
        /// than three unrelated dots doing it independently.
        /// </summary>
        static string[] AutomatonHeadRows(float apertureR, char apertureTone)
        {
            const int w = AutoW;
            float cx = (w - 1) / 2f;

            var rows = new string[HeadRows];
            for (int ly = 0; ly < HeadRows; ly++)
            {
                int y = BodyTop + ly;
                var sb = new StringBuilder(w);
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;

                    // ---- barrel collar (bronze) ----
                    if (y < CollarTop)
                    {
                        if (Mathf.Abs(dx) > 10.5f) { sb.Append('.'); continue; }
                        bool rivet = y == CollarTop - 1 && x % 3 == 0;
                        sb.Append(rivet ? 'S' : y == BodyTop ? 'D' : 'L');
                        continue;
                    }

                    // ---- the bronze strut lashing all three barrels together ----
                    if ((y == StrutRowLit || y == StrutRowDark) && Mathf.Abs(dx) <= BarrelSpread + BarrelHalf)
                    {
                        sb.Append(y == StrutRowLit ? 'L' : 'D');
                        continue;
                    }

                    // ---- the barrel cluster: centre barrel leads, two flank it ----
                    char c = '.';
                    foreach (float centre in Centres)
                    {
                        int tip = centre == 0f ? CentreBarrelTip : SideBarrelTip;
                        if (y > tip) continue;
                        float bdx = x - (cx + centre);

                        float half = BarrelHalf;
                        if (y > tip - 2)
                            half = Mathf.Lerp(BarrelHalf, 0.6f, y - (tip - 2));
                        if (Mathf.Abs(bdx) > half) continue;

                        if (y >= tip - 2)
                        {
                            // The bore glow - the shared "eye" - clamped to the barrel's own
                            // rounded muzzle so it can never draw wider than the metal around it.
                            float r = Mathf.Min(apertureR, half);
                            c = Mathf.Abs(bdx) <= r ? apertureTone : (y == tip ? 'd' : 's');
                        }
                        else
                        {
                            c = bdx <= 0f ? 'l' : 'b';
                        }
                        break;
                    }
                    sb.Append(c);
                }
                rows[HeadRows - 1 - ly] = sb.ToString();
            }
            return rows;
        }

        static readonly float[] Centres = { -BarrelSpread, 0f, BarrelSpread };

        // ================================================================== construct: crystal spire

        const int CrystalW = 18, CrystalH = 24;
        const float WidestAt = 0.45f;       // fraction up the spire where it is broadest
        const float BaseHalf = 1.6f, PeakHalf = 7.2f, TipHalf = 0.6f;

        static Rig BuildConstruct()
        {
            var palette = Palette.Of(Crystal, Eye);
            var sprites = new Sprite[4];
            // Pivoted at its own ground row rather than split into a base/head pair - a spire has
            // no natural joint the way the automaton's gun does, so Attach swivels the WHOLE thing
            // about where it roots into the floor, the same "planted, swinging its reach" read a
            // reed leaning toward light gets. Base is null and HeadFrac is 1 for exactly that
            // reason: there is no fixed piece underneath it.
            var pivot = new Vector2Int(CrystalW / 2, CrystalH - 1);
            for (int i = 0; i < 4; i++)
            {
                var rows = ConstructRows(StageRadius[i], StageTone[i]);
                sprites[i] = PixelSprite.From($"enemy.turret.construct.{i}", rows, palette, outline: true, pivotTexel: pivot);
            }
            return new Rig(null, sprites, 0f, 1f);
        }

        /// <summary>
        /// A faceted spire - LINEAR tapers rather than Bomb's sqrt-eased dome, so it reads angular
        /// rather than round: narrow at the ground, broadest at WidestAt, then a long straight
        /// taper to a point at the crown. Two small triangular shards flank the base for a
        /// cluster read. The gem sits at the spire's own widest row - "head height," where there
        /// is room for it and where the eye is naturally led.
        /// </summary>
        static string[] ConstructRows(float gemR, char gemTone)
        {
            const int w = CrystalW, h = CrystalH;
            float cx = (w - 1) / 2f;
            int peakRow = Mathf.RoundToInt(WidestAt * (h - 1));
            float gemCy = peakRow;

            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                float halfW = y <= peakRow
                    ? Mathf.Lerp(BaseHalf, PeakHalf, peakRow == 0 ? 1f : y / (float)peakRow)
                    : Mathf.Lerp(PeakHalf, TipHalf, (y - peakRow) / (float)(h - 1 - peakRow));

                // Two low companion shards, each a small triangle shrinking to nothing by
                // ShardTip - flanking the main spire rather than merged into its own taper.
                const int shardTip = 6;
                float shardHalf = y < shardTip ? Mathf.Lerp(1.3f, 0f, y / (float)shardTip) : 0f;
                float shardOffset = PeakHalf * 0.85f;

                var sb = new StringBuilder(w);
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    bool inSpire = Mathf.Abs(dx) <= halfW;
                    bool inShard = shardHalf > 0f &&
                                   (Mathf.Abs(dx - shardOffset) <= shardHalf || Mathf.Abs(dx + shardOffset) <= shardHalf);
                    if (!inSpire && !inShard) { sb.Append('.'); continue; }

                    if (inSpire)
                    {
                        float dgx = x - cx, dgy = y - gemCy;
                        if (Mathf.Sqrt(dgx * dgx + dgy * dgy) <= gemR) { sb.Append(gemTone); continue; }
                    }

                    // Directional shading: lit toward the upper-left, a dark band at the ground -
                    // the same cheap rule Bomb-Construct's own boulder shading uses.
                    bool grounded = y < 2;
                    bool lit = y >= h - 10 && dx <= 0f;
                    sb.Append(grounded ? 'd' : lit ? 'l' : 'b');
                }
                rows[h - 1 - y] = sb.ToString();
            }
            return rows;
        }
    }
}
