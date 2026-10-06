using System.Text;
using UnityEngine;
using Convergence.Art;

namespace Convergence.Enemies
{
    /// <summary>
    /// Procedural pixel art for Chaser's two coexisting looks - see <see cref="EnemyLooks"/>.
    /// Four sprites per look, in EnemyStage order (Idle/Mid/Full/Cooling), read by
    /// EnemyStageCycle off EnemyController.TelegraphProgress01/Recovering - the same shared
    /// _telegraphTimer Bomb's own telegraph uses, so this needed no new EnemyController getter
    /// the way Turret's charge did.
    ///
    /// A PHYSICAL SWING, NOT A COLOUR OR A RADIUS - the brief for this kind specifically. Bomb and
    /// Turret both tell their four stages apart with a fixed silhouette that changes colour or
    /// size; Chaser's whole identity is a presser that hits you with its own arm, so the four
    /// stages are four different ARM ANGLES swept through shoulder->hand->(weapon), the wind-up
    /// and the strike read as a POSE change the way the brief asked for, and colour is only the
    /// secondary cue on top of it. Body silhouette and position stay fixed across all four frames
    /// - only the limb moves - so nothing about the creature looks like it is teleporting between
    /// frames, only swinging.
    ///
    /// THE BLADE (automaton) AND THE CLAWS (construct) split the brief's own two phrasings -
    /// "swing blades" and "swipe arms" - into the two looks' actual difference, on top of the
    /// material split (gunmetal vs wet clay) every other kind's two looks already use. Both carry
    /// the SAME magenta - Chaser's own placeholder tint - as a charge line down the arm that
    /// brightens through the wind-up, the same "shared warning colour, different carrier" rule
    /// Bomb's and Turret's own two looks already follow.
    ///
    /// THE AUTOMATON IS A COMBAT UNIT NOW, NOT A RECOLOURED CONSTRUCT. It used to share the
    /// construct's row builder wholesale and differ only in material and a single blade tip - a
    /// gunmetal claw-swiper. It gets its own geometry (<see cref="AutomatonRow"/>) so the two
    /// looks can diverge past palette: a flat visor head reads as a sensor array rather than a
    /// face, a chest core sits where a heart would (the SAME per-stage charge tone the arm's own
    /// filament already brightens through, so head/chest/arm all "power up" together through the
    /// wind-up rather than one cue standing alone), and each arm's elbow is a visible ball-joint
    /// actuator that SPLITS into three forearms - folded flush against the upper arm at rest,
    /// extended and fanned open on the strike. Six identical straight blades total: one on each of
    /// three forearms, on both the swinging arm and a fixed, mirrored resting arm on the other
    /// shoulder. Identical rather than mismatched, deliberately - a mass-produced unit reads
    /// cleaner at this pixel count and costs one blade shape instead of six.
    ///
    /// THE CONSTRUCT KEEPS ITS OWN <see cref="Rows"/>/<see cref="IsClaw"/> UNTOUCHED. The two
    /// looks no longer share a row builder at all; only <see cref="BodyMask"/>, the stage tables
    /// and <see cref="PointAt"/>/<see cref="InCapsule"/>/<see cref="InWedge"/> are still common,
    /// because the torso silhouette and the swing timing are the property that must not drift
    /// between the two, not the limb geometry riding on top of it.
    /// </summary>
    public static class ChaserArt
    {
        // Cooled toward blue-gray rather than the warm/violet cast the flat recolour had - red
        // sits below green sits below blue on both ramps now, which is what actually reads as
        // "gunmetal" instead of "tinted stone" at this saturation.
        static readonly Palette.Ramp Gunmetal = new(new Color(0.24f, 0.28f, 0.36f));
        static readonly Palette.Ramp Steel    = new(new Color(0.76f, 0.82f, 0.90f)); // the blades
        static readonly Palette.Ramp Clay     = new(new Color(0.52f, 0.28f, 0.22f));
        static readonly Palette.Ramp Charge   = new(new Color(0.95f, 0.35f, 0.55f)); // Chaser's own tint

        static Sprite[] _construct, _automaton;

        public static Sprite[] Construct => _construct ??= BuildConstruct();
        public static Sprite[] Automaton => _automaton ??= BuildAutomaton();

        public static Sprite[] For(EnemyLook look) => look == EnemyLook.Construct ? Construct : Automaton;

        // ---- shared geometry: one fixed body, one arm that swings through four angles ----

        const int W = 36, H = 24;
        const float Cx = 13f, BaseHalf = 7f, BodyBaseRows = 3f, BodyTopRow = 17f, BodyMinHalf = 1.5f;
        static readonly Vector2 Shoulder = new(Cx + 5f, 12f);
        const float ArmLen = 10f, ArmHalf = 2.3f, ChargeHalf = 0.9f;

        // Idle, Mid, Full, Cooling - same order as EnemyStage. Degrees, standard math convention
        // (0 = facing right/+x, 90 = up). Mid winds the arm up and back over the shoulder; Full
        // sweeps it forward and down through the strike; Cooling drops back to Idle's own angle
        // exactly - the "recovers IN PLACE" rule means the stance resets rather than travelling
        // anywhere, so there is no separate pose to draw for it, only a dimmer tone.
        //
        // IDLE IS -60, NOT STRAIGHT DOWN, AND THAT WAS A REAL FIX. The shoulder sits right at the
        // body's own silhouette edge (by design - shoulders belong at the edge), so an arm hanging
        // near-vertical from there runs almost the whole way down INSIDE the torso's own wider
        // lower silhouette and all but disappears - rendered and caught exactly this way, only the
        // charge line survived visibly. Swinging the resting angle outward keeps the shaft mostly
        // outside the body's edge instead of parallel to and buried in it.
        static readonly float[] StageAngle = { -60f, 140f, -20f, -60f };

        // Idle, Mid, Full, Cooling tone letters - but WHICH CHARACTER SET depends on which
        // material slot a thing sits in, and that differs between the two looks: construct is a
        // two-material palette (Clay, Charge) so its claws AND its charge core both read the
        // SECOND material, uppercase; automaton is a three-material palette (Gunmetal, Steel,
        // Charge) so its blade reads the SECOND material (uppercase) but its charge core reads
        // the THIRD (digits). Reusing one array for both would silently draw the automaton's
        // charge line in STEEL instead of magenta - the exact lowercase/uppercase mistake Bomb's
        // own crack made on the first pass, one slot further along.
        static readonly char[] StageToneUpper = { 'B', 'L', 'H', 'D' }; // second-material slot
        static readonly char[] StageToneDigit = { '4', '5', '6', '3' }; // third-material slot: Base/Light/Glow/Dark

        static Vector2 PointAt(Vector2 from, float angleDeg, float dist) => PixelGeometry.PointAt(from, angleDeg, dist);
        static bool InCapsule(float x, float y, Vector2 a, Vector2 b, float hw) => PixelGeometry.InCapsule(x, y, a, b, hw);
        static bool InWedge(float x, float y, Vector2 a, Vector2 b, float hA, float hB) => PixelGeometry.InWedge(x, y, a, b, hA, hB);

        static bool BodyMask(float x, float y)
        {
            float hw;
            if (y < BodyBaseRows) hw = BaseHalf;
            else
            {
                float v = (y - BodyBaseRows) / (BodyTopRow - BodyBaseRows);
                hw = Mathf.Max(BodyMinHalf, BaseHalf * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)));
            }
            return y <= BodyTopRow && Mathf.Abs(x - Cx) <= hw;
        }

        // ================================================================== automaton

        // Shoulders sit at the torso's own edge, same rule as the construct's single arm - the
        // automaton just has one on each side. Back is a mirror of Front across Cx.
        static readonly Vector2 ShoulderFront = Shoulder;              // the swinging arm
        static readonly Vector2 ShoulderBack  = new(Cx - 5f, 12f);     // the resting arm

        // 180 - StageAngle[0] mirrors the idle pose across the vertical axis, so the resting arm
        // reads as the swinging arm's own idle stance seen from the other shoulder rather than an
        // unrelated pose. Written as a constant rather than derived, since the resting arm never
        // moves and has no reason to recompute this every call.
        const float BackAngle = -120f; // == 180 - (-60), wrapped

        const float UpperArmLen = 5f, ForearmLen = 5.5f, BladeLen = 4.5f;
        const float UpperArmHalf = 1.6f, ForearmHalf = 1.1f, JointR = 1f, WristR = 0.6f;
        const float BladeHalfBase = 1.1f, BladeHalfTip = 0.25f;

        static readonly Vector2 CoreCenter = new(Cx, 9f);
        const float CoreR = 1.6f;
        const float HeadHalf = 3f, HeadBase = BodyTopRow, HeadTop = BodyTopRow + 4f;

        // Idle, Mid, Full, Cooling - how far the three forearms fan apart at the elbow, and how
        // much of their own reach they extend to. Idle and Cooling both fold nearly flush against
        // the upper arm (small fan, short reach); Full opens all the way. Mid sits between them so
        // the fan reads as opening THROUGH the wind-up rather than snapping straight to full.
        static readonly float[] ForearmFan = { 3f, 14f, 28f, 4f };
        static readonly float[] ForearmReach = { 0.55f, 0.8f, 1f, 0.55f };

        // The resting arm never swings, so it gets one fixed fold rather than reading the stage
        // tables - it is always "at ease", the same reason its own angle is a constant above.
        const float BackFan = 3f, BackReach = 0.55f;

        static readonly float[] ForearmOffsets = { -1f, 0f, 1f };

        static bool InCircle(float x, float y, Vector2 c, float r) => (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y) <= r * r;

        /// <summary>
        /// One shoulder's whole rig: the upper arm, its elbow's ball-joint actuator, and the three
        /// forearms fanned out from it, each ending in a wrist actuator and a straight blade.
        /// A joint circle is geometrically a SUBSET of the wider capsule feeding it - every point
        /// within <see cref="JointR"/> of the elbow is also within <see cref="UpperArmHalf"/> of
        /// the same point on the upper-arm segment, since the elbow IS an endpoint of that segment.
        /// So each joint is tested BEFORE the capsule it sits on, or the capsule always wins those
        /// pixels first and the joint never draws - caught by rendering this and finding no joint
        /// dot anywhere on screen, not by reasoning about it.
        /// </summary>
        static bool OnArmRig(float x, float y, Vector2 shoulder, float angle, float fan, float reach,
                              char armTone, char jointTone, char bladeTone, out char tone)
        {
            var elbow = PointAt(shoulder, angle, UpperArmLen);
            if (InCircle(x, y, elbow, JointR)) { tone = jointTone; return true; }
            if (InCapsule(x, y, shoulder, elbow, UpperArmHalf)) { tone = armTone; return true; }

            foreach (var off in ForearmOffsets)
            {
                float a = angle + off * fan;
                var forearmTip = PointAt(elbow, a, ForearmLen * reach);
                if (InCircle(x, y, forearmTip, WristR)) { tone = jointTone; return true; }
                if (InCapsule(x, y, elbow, forearmTip, ForearmHalf)) { tone = armTone; return true; }
                var bladeTip = PointAt(forearmTip, a, BladeLen * reach);
                if (InWedge(x, y, forearmTip, bladeTip, BladeHalfBase, BladeHalfTip)) { tone = bladeTone; return true; }
            }
            tone = default;
            return false;
        }

        static bool OnHead(float x, float y) => y >= HeadBase && y <= HeadTop && Mathf.Abs(x - Cx) <= HeadHalf;

        // A one-texel slit rather than a pair of eyes - the whole point is that it reads as a
        // sensor bar, not a face. Sits in the lower half of the head block, like a visor rather
        // than a hairline.
        static bool OnVisor(float x, float y) =>
            y >= HeadBase + 1.3f && y <= HeadBase + 2.1f && Mathf.Abs(x - Cx) <= HeadHalf - 0.6f;

        static char HeadTone(float y) => y <= HeadBase + 0.6f ? 'd' : 'b'; // one dark seam row = the collar line

        static char BodyTone(float x, float y)
        {
            bool grounded = y < 2;
            // A panel seam every four rows - texture on the plating rather than a smooth shell,
            // checked before the lit highlight so a seam still reads inside the lit zone.
            bool seam = !grounded && Mathf.FloorToInt(y) % 4 == 3;
            bool lit = y >= H - 8 && x <= Cx;
            return grounded || seam ? 'd' : lit ? 'l' : 'b';
        }

        static Sprite[] BuildAutomaton()
        {
            var palette = Palette.Of(Gunmetal, Steel, Charge);
            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
                sprites[i] = PixelSprite.From($"enemy.chaser.automaton.{i}", AutomatonRow(i), palette, outline: true);
            return sprites;
        }

        static string[] AutomatonRow(int i)
        {
            float angle = StageAngle[i];
            float fan = ForearmFan[i];
            float reach = ForearmReach[i];
            char bladeTone = StageToneUpper[i];  // steel, brightens through the wind-up
            char coreTone = StageToneDigit[i];   // Chaser's own charge tint - head, chest and arm together
            const char armTone = 'd';            // dark gunmetal sleeve
            const char jointTone = 'l';          // lit gunmetal - a brushed-metal actuator against the dark sleeve

            var elbowFront = PointAt(ShoulderFront, angle, UpperArmLen);
            // Stops short of the elbow rather than running the filament's full length - the
            // rounded end of a capsule reaching all the way to the joint would bury the elbow
            // actuator under the accent stripe exactly the way the joint-vs-capsule bug above
            // did, just one drawing pass earlier. Left short, the joint dot reads as a socket the
            // conduit plugs into rather than disappearing into it.
            var filamentEnd = Vector2.Lerp(ShoulderFront, elbowFront, 0.7f);

            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var sb = new StringBuilder(W);
                for (int x = 0; x < W; x++)
                {
                    float fx = x, fy = y;

                    // A thin charge filament along the swinging arm's own upper segment, drawn
                    // BEFORE the arm rig so it sits on top of the sleeve rather than under it -
                    // the same "shaft carries a charge line" cue the construct's claws use.
                    if (InCapsule(fx, fy, ShoulderFront, filamentEnd, ChargeHalf)) { sb.Append(coreTone); continue; }

                    if (OnArmRig(fx, fy, ShoulderFront, angle, fan, reach, armTone, jointTone, bladeTone, out var t1))
                    { sb.Append(t1); continue; }
                    if (OnArmRig(fx, fy, ShoulderBack, BackAngle, BackFan, BackReach, armTone, jointTone, bladeTone, out var t2))
                    { sb.Append(t2); continue; }

                    if (OnVisor(fx, fy)) { sb.Append(coreTone); continue; }
                    if (OnHead(fx, fy)) { sb.Append(HeadTone(fy)); continue; }
                    if (InCircle(fx, fy, CoreCenter, CoreR)) { sb.Append(coreTone); continue; }
                    if (BodyMask(fx, fy)) { sb.Append(BodyTone(fx, fy)); continue; }
                    sb.Append('.');
                }
                rows[H - 1 - y] = sb.ToString(); // PixelSprite reads rows[0] as the TOP row.
            }
            return rows;
        }

        // ================================================================== construct

        static Sprite[] BuildConstruct()
        {
            var palette = Palette.Of(Clay, Charge);
            var sprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var hand = PointAt(Shoulder, StageAngle[i], ArmLen);
                var rows = Rows(hand, hand, hasBlade: false, StageToneUpper[i], StageToneUpper[i]);
                sprites[i] = PixelSprite.From($"enemy.chaser.construct.{i}", rows, palette, outline: true);
            }
            return sprites;
        }

        /// <summary>
        /// Shared row builder for both looks. <paramref name="hasBlade"/> selects which weapon
        /// tip to draw at the hand - a tapering steel WEDGE (automaton) or three short clawed
        /// capsules (construct, drawn in the SAME material as the arm's own charge core, since a
        /// claw here is bone/horn hardened by the same magic rather than a separate forged
        /// material - construct has no third material to give it).
        /// </summary>
        static string[] Rows(Vector2 hand, Vector2 tip, bool hasBlade, char weaponTone, char chargeTone)
        {
            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                var sb = new StringBuilder(W);
                for (int x = 0; x < W; x++)
                {
                    float fx = x, fy = y;

                    bool onBlade = hasBlade && InWedge(fx, fy, hand, tip, 1.9f, 0.3f);
                    bool onClaw = !hasBlade && IsClaw(fx, fy, hand);
                    bool onChargeCore = InCapsule(fx, fy, Shoulder, hand, ChargeHalf);
                    bool onArm = InCapsule(fx, fy, Shoulder, hand, ArmHalf);
                    bool onBody = BodyMask(fx, fy);

                    if (onBlade) { sb.Append(weaponTone); continue; }
                    if (onClaw) { sb.Append(weaponTone); continue; }
                    if (onChargeCore) { sb.Append(chargeTone); continue; }
                    // Dark rather than Base: the shaft still crosses the torso's own silhouette at
                    // some stages, and a shaded sleeve reads as a distinct limb where a same-tone
                    // one would melt into the body the way the idle pose originally did.
                    if (onArm) { sb.Append('d'); continue; }
                    if (onBody)
                    {
                        bool grounded = y < 2;
                        bool lit = y >= H - 8 && x <= Cx;
                        sb.Append(grounded ? 'd' : lit ? 'l' : 'b');
                        continue;
                    }
                    sb.Append('.');
                }
                rows[H - 1 - y] = sb.ToString(); // PixelSprite reads rows[0] as the TOP row.
            }
            return rows;
        }

        static bool IsClaw(float x, float y, Vector2 hand)
        {
            // Three short talons fanned around the hand's own approach angle - reusing the LAST
            // stage angle isn't available here, so claws are simply fanned around straight out
            // from the shoulder-to-hand line, computed per point rather than per stage.
            const float clawLen = 3.5f, clawHalf = 0.85f;
            var toHand = hand - Shoulder;
            float baseAngle = Mathf.Atan2(toHand.y, toHand.x) * Mathf.Rad2Deg;
            foreach (var da in new[] { -18f, 0f, 18f })
            {
                var tip = PointAt(hand, baseAngle + da, clawLen);
                if (InCapsule(x, y, hand, tip, clawHalf)) return true;
            }
            return false;
        }
    }
}
