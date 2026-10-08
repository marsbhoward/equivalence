using Convergence.Chain;
using Convergence.Combat;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A humanoid the loadout can be painted onto. Two implementations:
    ///
    ///   PrimitiveCharacterRig    - procedural placeholder, works with zero art
    ///   SpriteLibraryCharacterRig - PSD Importer + 2D Animation, the destination for real art
    ///
    /// Gameplay only ever talks to this, so swapping implementations changes nothing outside
    /// <see cref="CharacterRigFactory"/>.
    /// </summary>
    public interface ICharacterRig
    {
        /// <summary>True when drawing MENU-density art (the character screen's preview) - what a
        /// sprite swapped into it has to match.</summary>
        bool DetailArt { get; }

        /// <summary>Repaint to match a loadout. Called on every equip, so it must be cheap.</summary>
        void Apply(Loadout loadout);

        /// <summary>
        /// Animate one swing. <paramref name="duration"/> is the real interval until the next
        /// attack, so a fast move looks fast: a fixed-length animation gets cut off mid-arc by
        /// quick movesets and leaves the arm idle after slow ones.
        ///
        /// <paramref name="alt"/> selects the motion's alternate variant, so the second swing of a
        /// chain is not the first one replayed. What that variant IS belongs to the motion: Chop
        /// stages the same downward stroke from the other shoulder, Sweep runs its arc the other
        /// way. Motions with a fixed sense - a thrust, a spin, an overhead slam - ignore it,
        /// because varying those produces a wrong move rather than an answering one.
        /// </summary>
        void PlayAttack(AttackMotion motion, float duration, bool alt);

        /// <summary>
        /// Move into a swing's FIRST frame and hold it there for <paramref name="seconds"/>,
        /// still tracking the aim - the finisher timing bar's wind-up. The strike itself is the
        /// ordinary <see cref="PlayAttack"/> that follows, which blends from this held pose to its
        /// own first frame (the same frame), so the swing leaves the hold without a seam.
        ///
        /// Held on the motion's own first frame rather than on <see cref="PlayCharge"/>'s raised
        /// blade because every lead-in is authored to END where its finisher STARTS
        /// (MovesetLibrary.CheckLeadIns); a different pose in between would break each one.
        /// </summary>
        void HoldSwingStart(AttackMotion motion, bool alt, float seconds);

        /// <summary>
        /// Hide or show the held weapon. The blade throw needs the hand to actually be empty while
        /// the weapon is in flight, or the move reads as spawning a second sword.
        /// </summary>
        /// <summary>
        /// Hold a wind-up pose for a charged strike. Separate from <see cref="PlayAttack"/> because
        /// a charge is not a swing on a stretched clock: it is a held pose the player reads as
        /// "something is coming", then a strike.
        /// </summary>
        void PlayCharge(float seconds);

        void SetWeaponVisible(bool visible);

        /// <summary>
        /// Draw a second copy of the weapon in the off hand. Only the disc class asks for it.
        /// </summary>
        void SetWeaponSplit(bool split);

        /// <summary>
        /// A DIFFERENT picture for the off hand than the main (GearItem.OffhandLayer) - arena and
        /// menu density, and a flipbook kept frame for frame with the main hand's. All null for an
        /// ordinary disc, whose off hand copies the main. Set before <see cref="SetWeaponSplit"/>.
        /// </summary>
        void SetOffhandPicture(UnityEngine.Sprite arena, UnityEngine.Sprite menu,
                               UnityEngine.Sprite[] mainFrames, UnityEngine.Sprite[] offFrames);

        /// <summary>Show or hide the contact shadow under the feet. Hidden, it still appears while
        /// airborne - there it is the landing marker, not decoration.</summary>
        void SetContactShadow(bool on);

        /// <summary>
        /// Show this many reagent vials on the hip opposite the relic - the bow class's own
        /// class-conditional decoration, the same shape SetWeaponSplit is for the disc. 0 hides
        /// the rack entirely, so a non-bow character draws nothing extra.
        /// </summary>
        void SetVialCount(int count);

        /// <summary>
        /// Repaint the weapon layer, keeping its size, offset and pivot.
        ///
        /// For art that changes DURING a run without the loadout changing - the fire blade's heat
        /// cycle. Going through Apply instead would repaint all twelve slots to change one, and
        /// would also put the helmet preference and every transmog through the mill on every
        /// finisher.
        ///
        /// Null restores whatever the equipped weapon's own art is.
        /// </summary>
        void SetWeaponSprite(UnityEngine.Sprite sprite);

        /// <summary>
        /// Lift the whole figure off the ground, in world units. 0 is standing.
        ///
        /// The rig root moves rather than the player: the player's transform is what the physics,
        /// the camera, the range rings and every attack's origin are measured from, so raising it
        /// would carry the hitbox and the camera into the sky along with the drawing. Only the
        /// picture leaves the ground; the character is still standing exactly where the shadow is.
        /// </summary>
        void SetAirborne(float height);

        /// <summary>
        /// Draw the whole character this many times its authored size, about the gameplay root.
        ///
        /// VISUAL ONLY - the collider, reach, range rings and every hit are measured from the
        /// root and do not move. It exists so the arena character can be drawn at a density that
        /// lands on whole screen pixels (4/3 turns the body's 150 ppu into 112.5: one screen
        /// pixel per texel at 1080p, two at 4K) without changing any art or any tuning. See
        /// Tuning.Player.ArenaVisualScale.
        /// </summary>
        void SetVisualScale(float scale);

        /// <summary>
        /// Offsets the DRAWING by this much from the player's own origin, in world units - never
        /// the player's real transform, the same contract SetAirborne already keeps. Phantom's
        /// poof is the first user: a random point inside the melee circle for the length of a
        /// swing, then back to zero. Stacks with SetAirborne and the swing hop, all three being
        /// nothing but where the picture is drawn.
        /// </summary>
        void SetPhantomOffset(UnityEngine.Vector2 offset);

        /// <summary>
        /// Whiten the whole figure for a hit flash, 0..1.
        ///
        /// Health used to do this by lerping one SpriteRenderer's colour toward white. That never
        /// worked properly - it resolved to whichever renderer came first depth-first, which is a
        /// LEG - and it stops working entirely once art carries baked colour, because tinting
        /// multiplies and cannot brighten.
        /// </summary>
        void SetFlash(float t);

        /// <summary>
        /// Draw the figure as a STATUE - Medusa's stone: every layer swapped for a grey stone copy
        /// (PixelSprite.Stone) and every motion frozen in the pose it was caught in. Off puts the
        /// exact sprites back and lets the animation resume from where it stopped. Composes with
        /// <see cref="SetFlash"/>: a statue still flashes white when it is hit.
        /// </summary>
        void SetStone(bool on);

        /// <summary>
        /// Hide (or show) the head-slot armour layer without touching what is equipped there -
        /// the item stays worn and its stats still apply. A cosmetic preference, not unequipping.
        /// </summary>
        void SetHelmHidden(bool hidden);

        /// <summary>
        /// Hide (or show) the weapon layer as a standing PREFERENCE, surviving repaint the same
        /// way <see cref="SetHelmHidden"/> does - unlike <see cref="SetWeaponVisible"/>, which is
        /// a reference-counted transient (the blade mid-throw), this is a flag one caller owns.
        /// The transmutation preview is currently the only caller, and deliberately never writes
        /// it anywhere persisted - it only ever hides the weapon on the rig it was called on.
        /// </summary>
        void SetWeaponHidden(bool hidden);

        /// <summary>
        /// Set the body underneath the gear: skin, hair, eyes, hairstyle, expression.
        ///
        /// Separate from <see cref="Apply"/> because the two change on completely different
        /// clocks. Gear churns every run as loot arrives; the body is chosen once at the
        /// transmutation circle and is the part of the character a holder's token actually
        /// depicts. Folding them together would mean repainting a face on every equip.
        /// </summary>
        void SetAppearance(Chain.Appearance look);

        /// <summary>
        /// Move every layer to a new base sorting order, keeping their relative stacking.
        ///
        /// Needed because a character that walks around a room has to re-sort against the props
        /// in it every time it moves - the base cannot be decided once at construction the way it
        /// can in the arena, where the player is simply always on top.
        /// </summary>
        void SetSortingBase(int order);

        /// <summary>
        /// Switch to a relaxed, at-ease idle used only by the loadout preview: the combat idle is
        /// dead square-on with the fists hanging over the waist, which buries the belt. The
        /// showcase stance eases the arms off the centre line so waist gear reads. No-op on a rig
        /// whose idle is authored art.
        /// </summary>
        void SetShowcasePose(bool on);

        /// <summary>
        /// Paint the higher-detail menu art where a piece has any. Set once when the rig is
        /// built, not per equip - it changes which sprites Apply reaches for.
        /// </summary>
        void SetDetailArt(bool on);

        /// <summary>
        /// Height in world units the NEXT swing should hop off the ground, 0 for none. Set
        /// immediately before <see cref="PlayAttack"/>; the rig times the hop against that
        /// swing's own phases so the landing coincides with the strike.
        ///
        /// Deliberately separate from <see cref="SetAirborne"/>: that one is a gameplay state
        /// (immune, mid-leap, blast pending) and this is pure animation weight.
        /// </summary>
        void SetSwingHop(float height);

        /// <summary>
        /// How the held weapon looks, so anything drawing it somewhere else draws THAT weapon.
        /// The thrown blade used a generic capsule tinted by the element, so the player's gold
        /// sword left the hand and an unrelated orange blob flew off - which reads as a spawned
        /// projectile, not as your sword.
        /// </summary>
        bool TryGetWeaponVisual(out UnityEngine.Sprite sprite, out UnityEngine.Color tint,
                                out UnityEngine.Vector2 size);

        /// <summary>
        /// How a disc that leaves the hands looks - <see cref="TryGetWeaponVisual"/>, except that a
        /// pair holding two different halves (GearItem.OffhandLayer) hands out the half that
        /// matches WHICH disc this is. Discs leave in the order the hands empty: the first in
        /// flight is the main hand's, the second the off hand's, and any beyond that alternate.
        /// <paramref name="index"/> is that position; pass -1 for "the next disc to leave", which
        /// the rig answers from how many are already out. Size is the main hand's either way.
        /// </summary>
        bool TryGetDiscVisual(int index, out UnityEngine.Sprite sprite, out UnityEngine.Color tint,
                              out UnityEngine.Vector2 size);

        /// <summary>
        /// Point the figure this way, for a rig that has nothing driving it.
        ///
        /// The rig normally takes its aim from the PlayerController it hangs under, falling back
        /// to its Rigidbody2D's velocity - and a rig with neither returns early and faces right
        /// forever. That is fine for the four rigs that have one of those, and wrong for the
        /// shadow echoes, which are loose figures placed round the arena and have to face outward
        /// from where they were put.
        ///
        /// Zero clears the override and hands aim back to whatever else is driving it.
        /// </summary>
        void SetFacing(UnityEngine.Vector2 aim);

        /// <summary>
        /// How far the ARMS may tilt toward the aim, in degrees, for a move that has to point the
        /// blade straight down its line - the Magnum Opus's slash, which must sweep THROUGH the
        /// crescent's direction even aimed up or down. 0 restores the rig's own limit (ArmAimRange,
        /// 45). A rig with no arms to tilt may ignore this.
        /// </summary>
        void SetAimRange(float degrees);

        /// <summary>
        /// Turn the figure to show its BACK - for a scripted beat (walking through a door) or for
        /// ordinary untargeted movement toward the top of the screen (see
        /// <c>PlayerController.UpdateTravelFacingAway</c>), never for combat. This is deliberately
        /// NOT derived from <see cref="SetFacing"/> or from aim INSIDE THE RIG: the 3/4 combat view
        /// exists because a body rotated to track a target has to stay legible mid-fight (see the
        /// reverted TopDown mode), and none of that applies once nothing is being fought. The rig
        /// itself must never read PlayerTargeting or wire this into FaceAim's own aim value - the
        /// decision belongs one layer up, in whichever caller can tell combat facing apart from
        /// travel facing, and PlayerController forces this back to front the instant a target is
        /// acquired or a finisher locks.
        ///
        /// Swaps the head for its back-of-skull layer, drops the accessories that have nothing to
        /// show from behind (Ring, Trinket, Neck), and reorders the rig so the BACK-slot cloak
        /// draws over the body instead of being hidden behind it - which is most of the reason
        /// this exists: a cape drawn behind a front-facing character is drawn almost entirely
        /// UNDER their own silhouette and barely shows at rest. The weapon stays visible rather
        /// than hidden, just low in the stack - it is bigger than the body in almost every case,
        /// so it still shows past the body's own silhouette the same way gear is meant to break it
        /// everywhere else in this rig, without needing to be drawn in front of anything.
        /// </summary>
        void SetFacingAway(bool away);

        /// <summary>
        /// Repaint any sigil-bearing piece (GearItem.BearsSigil) for <paramref name="element"/> -
        /// called by CharacterRigFactory.ApplyAttunement whenever the attunement changes. Apply
        /// itself reads Attunement.Current.
        /// </summary>
        void SyncAttunement(Core.ElementType element);

        /// <summary>
        /// Carry a two-handed weapon over the shoulder in one hand, off arm hanging, instead of
        /// holding it in both.
        ///
        /// A REST pose, not a combat one. The caller asks for it when the character is not in the
        /// middle of anything - the rig refuses it outright while a swing or charge is running,
        /// because the attack arcs are absolute angles that have to hit their real numbers.
        ///
        /// Ask for this off the state of the COMBO, not of the individual swing. The return to
        /// the rest carry has to fit inside the attack gate, and at high attack speed
        /// AttackMotions.SwingSeconds is clamped at its own floor - so a grip that re-formed
        /// after every swing would have no time to travel and would strobe exactly when the
        /// player is swinging fastest. Re-forming when the CHAIN lapses gives it a real pause to
        /// happen in, and reads better besides: nobody drops a greatsword off their shoulder
        /// between two beats of a combo.
        ///
        /// ON BY DEFAULT: every sword rests on the shoulder, so a rig nobody drives (the hub
        /// avatar, a couch figure) carries it without having to ask. Only a caller with combat to
        /// come out of - PlayerController - ever turns it off.
        ///
        /// A rig with no opinion about grips may ignore this entirely.
        /// </summary>
        void SetGripShouldered(bool shouldered);

        /// <summary>
        /// Send the character's RIGHT fist to a point, in the rig ROOT's local frame (so the
        /// mirror is the rig's business, not the caller's), or null to hand the arm back.
        ///
        /// For something the hand holds that the rig is not drawing - Zanmato's blade on its way
        /// into the saya, which is a separate sprite (KatanaSheathe) while the rig's own weapon is
        /// hidden. Without it the arm stayed up in the rest carry, empty, while the sword it was
        /// meant to be holding went into the scabbard on its own.
        ///
        /// While set, the reaching arm is drawn in front of the body, since it crosses it.
        /// <paramref name="offHand"/> is how far the OTHER hand has let go of the grip (0 still
        /// on it, 1 hanging free) - ease it back to 0 before clearing the target and the rig is
        /// handed back exactly the grip it had. Called every frame the point moves; start it from
        /// <see cref="TryGetSwordHand"/> and it leaves the pose without a snap. A rig with no
        /// joints to solve may ignore this.
        /// </summary>
        void SetHandTarget(UnityEngine.Vector2? rootLocal, float offHand = 1f);

        /// <summary>
        /// Hold the weapon out at arm's length, 0 (not at all) to 1 (fully out) - the Sniper's
        /// idle flourish after a finisher, showing the cylinder turn (see CylinderSpin). The
        /// caller eases the weight; the rig only lays the pose over whatever else it is doing,
        /// and keeps the grip closed while it is asked for. A rig with no joints may ignore this.
        /// </summary>
        void SetPresent(float weight);

        /// <summary>
        /// Where the fist <see cref="SetHandTarget"/> moves is right now, in the root's local
        /// frame - the right hand, on the sword. False if this rig cannot say.
        /// </summary>
        bool TryGetSwordHand(out UnityEngine.Vector2 rootLocal);

        UnityEngine.Transform Transform { get; }

        /// <summary>
        /// The transform the WEAPON layer hangs off, or null if this rig cannot say.
        ///
        /// For anything that has to travel WITH a swing rather than merely appear near one -
        /// Rift Blade's drifting shards are the first. Parenting to this means the effect follows the
        /// arc for free; a world-space effect placed at the weapon's position each frame lags it by
        /// exactly the frame that matters, which is every frame of a fast swing.
        ///
        /// Nullable on purpose. Only the primitive rig actually keeps a transform per layer; the
        /// SpriteLibrary rig is an authored hierarchy whose weapon is wherever the artist put it,
        /// so it answers null and callers simply do without.
        /// </summary>
        UnityEngine.Transform WeaponAnchor { get; }

        /// <summary>
        /// The WEAPON layer's renderer, or null if this rig cannot say.
        ///
        /// Beside <see cref="WeaponAnchor"/> because an effect hanging off the weapon needs both
        /// halves and they answer different questions: the anchor says WHERE the blade is, and
        /// this says WHAT ORDER IT DRAWS AT - which is not a constant and cannot be computed. The
        /// rig is y-sorted, so `SetSortingBase` moves every layer whenever the character moves,
        /// and `TwoHandedOrder` permutes which offset the weapon even holds. Anything wanting to
        /// draw in front of or behind the blade has to read this every frame; deriving it from
        /// `RigLayer.Weapon` gives an order the rig stopped using.
        /// </summary>
        UnityEngine.SpriteRenderer WeaponRenderer { get; }

        /// <summary>
        /// The TRINKET layer's renderer, or null if this rig cannot say.
        ///
        /// Same pair of questions as <see cref="WeaponRenderer"/>, for the worn hip slot: its
        /// transform says where the piece ended up (the sprite pivots on its centre, so the
        /// transform IS the centre) and its sorting order says what to draw behind. Zanmato's
        /// sheathing animation needs both - the blade slides along the scabbard's axis and has
        /// to pass BEHIND it, which is the whole reason going in reads as going in.
        /// </summary>
        UnityEngine.SpriteRenderer TrinketRenderer { get; }

        /// <summary>The OFF hand's disc while a disc pair is split (SetWeaponSplit), else null - a
        /// second weapon renderer, for anything that lights or hides the pair as one (the
        /// King and Queen art's gather).</summary>
        UnityEngine.SpriteRenderer OffhandRenderer { get; }
    }
}
