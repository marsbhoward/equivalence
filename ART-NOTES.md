# Art and rig notes

Per-item and per-mechanism art notes split out of `CLAUDE.md`: what each piece was built to look
like, what failed at this size, and which calls were the user's. Read the section for a piece
before changing it.

## The Hellspawn Cape: Spawn's cape

`Art/Gear/DemoGear.Hellspawn.cs`. A Diamond, power-0 Back piece after Spawn's cape - everything
the other cloaks are not: FLOOR-LENGTH and flared to about twice the body's width at the hem (at
the torso's width it showed from the front as a strip down each side), TATTERED (a seeded hem of
V-cut points, ragged lower sides, four rips), and a HIGH COLLAR of two big outward-sweeping points
a side - the one part seen above the shoulders from the front. One field in the torso's own frame
(cells, y up from the waist), at BodyPpu, auto-outlined.

- **Blood red by hand**, not a Ramp: a ramp lifts toward white and a lit red goes pink.
- **Broad fan folds** (`HellspawnFolds`) - whole planes of cloth that widen with the flare, the
  light/dark boundary dipping under each ridge. Not crease marks: CapeRows' rule against dashes
  stands. A straight light/dark line across the cape read as two cloths sewn together.
- **Rips must be big** (about 4 x 8 cells): at 3 x 4 the outline closed them into dark runes.
- **The cape spring is per item now**: `GearItem.CapeHingeCells` hinges it that far below the
  sprite's DRAWN top (outline included) - the collar stands up past the neck, and a top-edge hinge
  would slide the shoulders about a point over the head; `CapeSwingScale` (0.45 here) - a
  floor-length hem at the full 34 degrees sweeps half a body sideways. Both default to the old
  behaviour for every other cape.
- The spring reads `Body.linearVelocity`, which the HUB avatar does not have - capes only flutter
  there. Judge the swing in a run.

## Nocturne, Orichalc and Vermilion: reference-built full sets

`Art/Gear/DemoGear.Nocturne.cs`, `DemoGear.Orichalc.cs`, `DemoGear.Vermilion.cs`. Seven Diamond,
power-0 pieces each:
Torso, Shoulders, Gloves, Belt, Legs, Boots, Back. Fields like Sovereign/Revenant; the long
Back pieces in torso-local TEXELS (not the Hellspawn's cells).

**Nocturne** follows the user's four-view turnaround (a 3D render came first and was misread
twice): black plate edged in GOLD (one texel, always an edge), a closed ankle-length black coat
piped in WHITE, a GREY cape. Two trims on purpose - gold for armour, white for cloth.
- The piping and tassels are palette letters `w`/`v` outside the three ramps (`NocWithWhite`) -
  the derived menu art shades only ramps, and a white line has no surface.
- The coat is the LEGS slot on `RigLayer.Tasset`, closed like the Shogun robe; its two front
  lines (`NocFrontEdge`) start on the cuirass under the chevron and run to the hem, and a line
  each side branches out at the knee. Gold-edged notched hip plates sit over it.
- `NocFrontEdge` is 3, not 4: the NEAR ARM hangs in front of the torso, and a cuff wider than
  the couter put its outline over the near line. Keep the gauntlet's widest point at 6.
- Pauldron = a box (lit top face, outer corner `NocPauldronRise` above the chin) over ONE band
  that juts further out - the widest point from front and back. A white tassel hangs from a
  dark gold-rimmed knot (solid gold read as a candle flame).
- The couter (heart-shaped) is drawn BELOW the elbow cut so it bends with the forearm; the rig
  repeats arm-local row -12 to lengthen the upper arm, so the sleeve above stays uniform.

**Orichalc** is the sister design RECOLOURED by the user: silver -> COPPER, blue -> YELLOW, the
coat's light trim included. Its coat panels hang OUTSIDE the legs (over the thighs their outline
swallowed the leg harness). Copper on yellow separates by VALUE; yellow's shadows are hand-placed
toward ochre (`OchreShadows`; lerped to black, yellow goes olive). Pauldron = one plate rising to
a POINTED peak at its outer top (`OriPauldronRise`; a rounded fall read as a hump), cloth, then a
band round the arm ending on the elbow.

**Vermilion**: red plate trimmed in white (one texel, always an edge), lit toward orange so it
never goes pink; big DOME pauldrons standing `VerPauldronRise` above the chin; a tabard on the
Legs slot (hip band, a centre panel to a point, FLARING hip plates - square, they read as a row
of boxes beside the centre plate - and side panels with SWALLOWTAIL hems, two small teeth a side
read as torn); tall boots whose toes are HORNS curling out and up, on two grids (BootsFront's
out is +X); a grey cape whose white lining is turned back along the -X edge. The reference's
tabard crosses are left off, following the user's call on the other two sets. `VerRim` (a
shape's own one-texel rim, by neighbour test) is how its plates get their white.

What failed at this size, all three sets: straps crossed steeply at the centre read as a BOW TIE; a
ring round a small emblem closes up (`$`), a star beside a line reads as `H`, and a four-pointed
star reads as a cross (so no emblems - the user removed the chest crosses); banding every plate
in lames made the figure stripes. **Pauldrons were first built in shapes the file already had**
(Revenant's stepped slabs, Sovereign's dome-on-lames) and sent back - judge a reference
pauldron's SILHOUETTE before reaching for an existing one.

Facing away, torso/belt/leg pieces still draw their FRONT art, like every armour set (no armour
has a back view yet); a full-length Back piece hides most of it.

Judging a back view with `GearSheet`: after `Setup`, call `SetFacingAway(true)` on the row-`.1`
rigs before `Capture`. `SetDetailArt(true)` on each rig shows the menu-density versions.

## Talon: one arm in plate, one arm bare

`Art/Gear/DemoGear.Talon.cs`. Seven Diamond, power-0 pieces after the user's three references
(one set): Jerkin, Pauldron, Gauntlet, Belt, Skirts, Greaves, Coat. The first ASYMMETRIC set,
and the asymmetry is the silhouette:
- **The FAR arm (+X) is the plated one**, the character's LEFT. That is the user's call against
  the references, whose plate is on the right arm. The rest carry raises the NEAR arm, and with
  the plate there the fan swung up with the sword. Now the bare hand carries the sword and the
  plate hangs still.
- **Turned away it stays on the LEFT** (`GearItem.Lopsided`). Turned away, the rig keeps the carry
  in the anatomical right hand by moving it to arm.back. So a lopsided piece's arm and shoulder
  layers TRADE sides with it, each mirrored (`PixelSprite.Mirror`, a real sprite so the elbow cut
  keeps it), and a lopsided cape turns over in place. `SyncLopsided` repaints only those layers on
  a turn, never a full Apply: that would reset the cape spring and put back Prism's base gem. It
  ends a running flash or stone around the repaint, because both restore the sprites they saved.
  Also flagged: the Wraithguard Pauldron (cap and mantle shape) and the Vermilion Cape (its lining
  stays on the right edge). The Wraithguard Cloak's drape and the saya already followed the carry
  (`SyncDrapeSide`, `SyncTrinketSide`). An audit of every pair found no other true lopsided piece.
  The Pauldron's far layer is a FAN of jagged blades along the shoulder's ARC, the top one rising
  past the cheek; its near layer is fur. Blades sprung from one point under a round cap read as a
  FLOWER, so keep the roots spread along the shoulder. The plated arm's shapes are written
  outward toward -x and sampled mirrored (x = -X); only the light-dependent tones read real X.
- **The far arm is BARE**: `GearItem.ShowsSkin`. Skin is authored in `BodyLook.GearSkin` (the
  default swatch, so cards and NFT images show a plausible arm) or `GearSkinShaded` on a far limb,
  and `BodyLook.Reskin` -> `PixelSprite.Recolour` repaints those texels in the wearer's tone at
  `Apply`, before the elbow split. `SetAppearance` re-applies when only the skin changes. The armour
  stand repaints it in its WOOD. A layer with no skin texels comes back as its own source.
- **The far arm hangs half behind the torso**, so a jerkin, belt or pouch a texel past the body
  (plus its outline) hid it entirely. That was found when the BARE arm was the far one. On the +X
  side the Jerkin and Belt keep to the body's own edge (`TalBodyEdge`), and the pouch sits inboard,
  so the plated arm's clawed hand shows.
- A red sleeve at the elbow read as BLOOD, and a clasp across the seam under the plastron's point
  made a CROSS. Both are gone.
- The Coat (Back) is the fur mantle plus the coat's back and split tails. From the front only the
  tails past the legs show, and the mantle covers the back view.

## Geode: diamond plate over stone, black glass

`Art/Gear/DemoGear.Geode.cs`. Seven Diamond, power-0 pieces after the user's armoured-Mewtwo
references (plus three crops they isolated: helm, chest/shoulder, hip). Only the armour and visor
are taken. The user's material map, which took three passes:

- the reference's METAL -> DIAMOND, and only the metal. Faceted by `GeoDiamond`: rhombic facets
  over the plate's own form tone, one in four lifted a tone. Facets nudged DOWN as well read as
  camouflage blotches; drawn facet lines made every plate plaid. The ramp is shaded deep (0.46) -
  all-pale, the plates lost their form.
- the reference's BODY between plates (belly, upper arms, thighs) -> STONE, a grained warm granite
  (`GeoRock`), matte. Warm so it never reads as Medusa's petrify grey.
- the black parts and the visor -> BLACK GLASS, Shadow's ramp and alphas on the digits.
- seams between plates -> BLACK (`GeoSeam`).

The first pass had it the other way round (stone plate, diamond in the gaps) and read as rubble.

- **The visor is a CRESCENT low on a teardrop dome**, not a band across the eyes: the dome covers
  the whole face (`SealsHead`). Pale prongs either side, a sickle off the back (-X).
- **Glass over a face must be SOLID.** The project blends in linear space, so an alpha reads far
  clearer over bright skin than over the dark floor Shadow's numbers were set against: at 0.85 and
  0.95 the face came through as brown. The glass LINE tone is 1.0 in this set, and the visor's
  highlights sit on Glow (0.92). The see-through middle tones are only used over the undersuit.
- The tasset hangs from the BELT (torso-local, to the knee), so it stays one piece while the legs
  walk under it; a glossy glass dome sits over the band at the front. Either side, a ROUNDED
  shard, shorter and splayed out (the user's ask for structure there, not borrowed from
  Vermilion's cloth panels). Hard because BEVELLED: a one-texel ridge, a flat lit face and a flat
  shaded face, black seams where shards touch - no facet scatter. Two shards a side was tried
  first: about five texels each, the ridges and seams cut them into strips and the fan read as
  ICICLES.
- The chest vent sits inboard (x -6..-3): further out, the near arm covers it.

## Weapon parts that are not in the sprite

Lumen's live wire, Saint's halo and Prism's gem glow are part of how those weapons READ, not effects, so they are
present wherever the weapon is drawn: in the world as components (`CharacterRigFactory` for the rig
- arena, hub, character preview - and `GearDisplay.ApplyEffects` for the rack and armoury wall),
and on the flat UI pictures (gear picker cards, the rack close-up) through `UI/WeaponExtras`, which
places them from the SAME derived numbers the world uses. Its animation runs on UNSCALED time -
every screen it appears on pauses the game. A new weapon with such a part must go in both places.

The Secret Fire's marks (the Aether set and greatsword) are the one such part that IS in the sprite - black
texels - with only the glow laid over it, by `KindledMarks` in the world and `UI/KindledImage` on
cards; they need the same both-places care.

Prism's LIT GEM follows the same rule through `Art/Gear/Attunement`: one current element, set by
the run (its element) and by the hub's sigil selector (`HubRoom.Attune`, which also repaints the
avatar, rack and armoury wall). `CharacterRigFactory.Paint` applies it on EVERY paint via
`ApplyAttunement` - before, a mid-run repaint (appearance edit, helm toggle) put the base sprite's
fire gem back under another element's glow - and `GearDisplay.Represent` hands every display and UI
card the lit variant (and `WeaponExtras.AddGemGlow` the glow at it). The rig's `DetailArt` picks the MENU variant (`GearItem.MenuElementBlades`):
the swap does not rescale, so an arena variant in the character screen drops it to arena density.
Gem heights (`PrismGemAlongs`) convert cells with `FineUpscale / FinePpu` - by ppu alone every glow
sat at half its gem's height.

## The elbow and the right-hand carry

Each arm has an elbow pivot (`elbow.front`/`elbow.back`, 14 body texels below the shoulder, in the
pinch `Limb` always had). The forearm, lower glove, ring and weapon hang from it; authored offsets
stay SHOULDER-relative and `PivotShift` converts at placement. Nothing authors the four
`*Lower` RigLayers — `PaintElbowSplit` cuts the arm and glove sprites at the elbow line when they
are painted, both halves overlapping by two identical texels, so a straight arm composites to the
exact old picture and every glove bends without being redrawn. `WithForearms` ranks each lower
layer directly above its upper half in every sort table. Elbows are 0 except in the rest carry
and the walk's arm swing (free hands only - see The walk). The wrist (four hands) and the knee (six
shins) followed: 43 layers now, `DepthStride` 50, `DepthSteps` 471 to stay under StatusOverlay.

**The rest carry FOLLOWS THE MIRROR** (`CarryInBackArm` is `_facingAway`): facing left is facing
right reflected, carry, drape (`SyncDrapeSide`) and left-hip saya included. It used to stay in
the character's anatomical right hand (`_facingLeft != _facingAway`), swapping arms on every turn
so the picture on screen never changed - with a symmetric torso, only the head visibly turned.
The carry angles are written for arm.back and sign-flipped for arm.front. Pose from the reference: upper arm 30° above horizontal, forearm 20° off
vertical leaning in, blade 50° off vertical down across the back; the grip moves into the fist
(`CarryGrip`). The carrying arm ignores aim.

**Walking sideways, the blade is SHOULDERED** (the march carry, `_march`, the user's call - the walk
itself was never the problem; walking into a fight with the blade down across the back was).
Facing the camera and moving left or right, the blade lies back over the TRAILING shoulder at
`MarchBladeDegrees` (17 above horizontal), resting on top of the shoulder by the neck (where the
user drew it), the hilt in front of the chest, the OTHER edge up from the rest carry's.

- **From the power stance it is the elbow dropping and the wrist turning**: the forearm keeps
  nearly its rest angle (200 -> `MarchForearmDegrees` 210, an ABSOLUTE angle so the blend doesn't
  spin it a turn), the upper arm swings down (`MarchShoulderDegrees` -20) so the elbow comes in
  front of the body under the fist, and the fist lands on the upper chest. Forced by geometry, not
  taste: for the blade to rest on the shoulder by the neck the fist must be INWARD of that
  shoulder, and the only elbow that reaches there is down in front (measured: shoulder joint 3.5
  body units from the neck, arm 5.0 + 3.45 to the grip). Elbow and weapon angles are derived.
- **Second pass, the user's call: elbow DOWN, blade a little FLATTER, ROLLED.** It was shoulder
  -33 / blade 25; now -20 / 17 (clockwise walking left, counter-clockwise walking right - the same
  picture mirrored), forearm held at 210. 37 (steeper) was tried first and sent back; at shoulder
  -30 the fist rose and the guard climbed toward the head. The wrist turns as it comes:
  `_marchRolled` mirrors the weapon pivot across the blade's long axis (scale.x exactly -1,
  latched at 0.55/0.45 of the blend, only while the carrying fist holds it) - a 2D rotation can't
  show a turn about the blade's own axis, and a scale through 0 would resample point-filtered art.
  The swap lands mid-sweep, where it reads as the wrist rolling. Read the blade's tip and highlight
  to see it.
- **The carrying forearm, glove and hand draw OVER the shoulder pieces** in the march
  (`ForearmOverShoulders`: over pauldrons, mantle, drape and shroud, the user's call). The forearm
  comes up in front of the chest there; capped by `PauldronsCapArms` the fist on the hilt was lost
  under the pauldron and cloak. The upper arm stays capped.
- **Its own layer order** (`MarchCarryOrder`, taken past `MarchOrderFrom` 0.85 of the blend): the
  weapon over torso/belt/amulet, under the carrying arm, pauldrons and head - the hilt shows in
  the fist, the blade passes behind the shoulder and reads as resting on it.
- **It is a READY position: it flows into the opener.** The march blade (70-74 degrees) already
  lies where a Chop cocks (75-87), so the hand-off is the hands rising with the blade held still.
  Three things had broken that (the user's call): the elbow unfolded the LONG way from 230 (the
  blade spun past the hip and back up - `CarryElbowHeld` takes it as -130, latched while the carry
  is fully held); the grip left the carry at the RETURN's speed, so a fifth of the carry was still
  in the arm as the strike began (`GripToSwingSharpness` 30, closed by the end of the cock); and
  the swing's own entry blend fought the grip blend (a swing out of the carry now starts on its
  own first frame). Getting there is a flick too: `MarchInSharpness` 18 (~0.15s), reading the
  walk's TARGET size rather than the eased `_gaitWeight` - at 6 on top of that ease it took 0.67s.
- Standing, walking straight up/down (`MarchSidewaysFrom`/`Full`), or turned away keeps the rest
  carry; the walk's size scales it, so a stop settles back (`MarchOutSharpness` 8). The blend swings the tip DOWN and out
  behind the body, never up across the head.
- Wrong turns, in order: the rest arm with only the blade turned (fist overhead - BRANDISHING); an
  arm folded up beside the shoulder (fist away from the head - holding the sword OUT); the power
  stance with the elbow dropped 15 degrees (blade floating ABOVE the shoulder).

**The upper arm is drawn 6 texels longer than authored** (`UpperArmExtraTexels`, elbow joint at
20 texels, art cut at 14). At 14 the pauldron's half-width covered the whole segment and the
carry had no visible elbow. `CutAtElbow` repeats the row above the elbow band into the upper
half; everything below the elbow keeps its art and hangs lower, so offsets stay authored against
the unlengthened arm (`ElbowArtRest`, not `ElbowRest`, is what `PivotShift` subtracts). Hands
hang 0.04 units lower in every pose.

**The pauldron over the carrying shoulder follows the arm** (`FollowPauldron`, 0.25 of the
raise = 30°, so the plate lies along the upper arm). That is a CAP's number: a plate that already
HANGS DOWN the upper arm sets `GearItem.HangsAlongArm` and turns with the whole raise - at a
quarter it hung off the shoulder while the arm lifted out from under it. Two do: the Shogun sode
(chin to elbow) and Sovereign's stepped plates (hem on the elbow; as a cap they jutted off the
raised shoulder like a shelf). The other six are caps, wider than they are tall, and a cap turned the
whole raise stands up beside the arm as a slab - Revenant is the borderline one, kept a cap because
its upper slab sits flat across the shoulder. Judge a new pauldron in the carry both ways. Matched to a shoulder by the SIGN of its x,
not by layer: every pair puts `Shoulders` on -X except the Wraithguard's, whose steel cap is on +X.
Only in the carry.

**The pauldrons cap the arms in every stack** (`PrimitiveCharacterRig.PauldronsCapArms`, applied
to every sorting table after `WithForearms`): both arms' UPPER halves - arm and glove above the
elbow - rank under both pauldrons; only the forearm, fist and ring rise to grip a weapon. The disc,
two-handed and turned-away carry stacks used to promote the whole glove for the grip, which was
harmless while gloves were hands and drew every full arm harness over its pauldron.

**Every pauldron is placed like the gold pair** — body density, `±ShoulderX`, `PlateY(rows)` (top
edge on the chin). The rest used to sit on literal offsets at gear density and stood 2–3x gold's size.

**Every Gloves piece is a FULL ARM HARNESS** on Silver's frame (`GloveRows`): 14 x 28 at body
density on `ArmHarnessY`, shoulder to fingertips - upper-arm plate, an elbow break at row 9 (where
the elbow split cuts), forearm plate, a wrist break at row 18, the gauntlet. Revenant and Sovereign
were built that way; Black Steel, Conclave and Seraph were hand-only gauntlets sat on a literal -12
at the old 75 ppu (grids for an earlier, bigger arm: 16 cells wide on a 5-cell arm, and in the hand
a block that hid a disc's whole hollow). Each harness follows its set's own language - Black
Steel's black seams, Conclave's black -> gold bracer -> white glove, Seraph's leg order on the arm.
A metal forearm needs a HIGHLIGHT: flat, Seraph's gilt read as a bare forearm and Conclave's
electrum as khaki. Far arms need a COMPLETE palette - a far ramp with no second material draws
every accent texel as a hole (an unmapped letter is transparent).

That leftover (75 ppu, literal offsets) is gone from every armour piece now - each was judged
against the body rather than blanket-halved, and the rules below are how. `CapeRows` (the
diamond, hood and Wraithguard cloaks) still says GearPpu and is NOT a leftover: it is laid out one
texel per cell and already fitted to this body's shoulder line.

- **Torso pieces go through `DemoGear.Reproportioned`**, the move CuirassRows made by hand: the
  body NARROWED and kept its height, so rows double (same world height) and width goes x0.8
  (20 texels at 75 -> 32 at BodyPpu, the tracer's width), offsets unchanged. The Vanguard
  cuirass had been doubled once more than its siblings (twice every cuirass's width, a barrel
  past both arms) and is `Undoubled` first.
- **The Seraph Mantle and the Conclave gorget are the exception**: both are laid out against the
  HEAD (x +/-16, the chin line, horns at cheek height), and the head is the same 32 x 28 grid
  drawn at half its old size - so their grids go to BodyPpu UNCHANGED (exactly half size) and
  hang from `ChinYCells`. Still oversized against the body by design, now by their own amount.
- **Belts** sit on `BeltY` like Silver/Sovereign: Black Steel is Silver's 28 x 6 strap with a
  black buckle; Seraph's native-75 grid halves to exactly 28, strap on `BeltY` (it hung low only
  to clear the old mantle's medallion); the Vanguard sash keeps its 2x2 blocks with its
  waistband on `BeltY`.
- **Leg pieces and boots** (Black Steel, Conclave, Seraph) were 16 texels at 75 PER LEG - four
  legs wide, so each pair drew as one block. `PerLeg` resamples them to `LegArmourWidth` (12:
  the leg's 8 plus two a side, under Silver's 10-14 boot), rows doubled. Leg pieces keep their
  hip-relative offsets; boots stand on the sole through `SoleY` (`BootY`'s rule for any grid) -
  on their old literals they stopped five cells above the foot. Seraph's leggings were the old
  leg's full height, so `KneeBandOnKnee` lengthens the white thigh until the black band ENDS on
  the knee joint (centred, the boot's outline left one cell of gilt where the original had two).
- **Scarves** keep their 2x2 blocks (the amulet's pixel size), collar top on the chin
  (`PlateY`). The tail halved in WIDTH but not length (`ScarfTailRows` 9 -> 18): halved both
  ways it never cleared the torso even at the full 46-degree swing.

**A hood encloses the helm, as it does the hair.** With a hooded cloak worn, the helmet is cut to
the cowl's outline at Apply (`PrimitiveCharacterRig.EncloseHelmInHood` -> `PixelSprite.Enclosed`;
the armour stand does the same). Inside = between the cowl's outermost texels on each row, so the
face opening shows whatever is under it; above the crown is cut; below the hem is kept, as hair
flows out there. A texel is kept only if ALL of it is inside (quarter-point samples), so a helm
coarser than the cowl can't leave half a texel hanging past the outline. Crowns, horns and brims
(Geode, Vanguard, Blacksteel, Seraph) no longer poke through the cloth.

**A cloak covers the pauldrons.** `RigLayer.BackOver` (the Back slot's front piece) is re-ranked by
`DrapeOverShoulders` to directly above `TorsoOver` in every front-facing stack. The poncho's yoke
covers both shoulders and the upper chest, and its back (`PonchoBackRows`) is the yoke's exact width
and shoulder curve so the two read as one cloth. The Wraithguard Cloak drapes ONE shoulder - the one
OPPOSITE the sword arm - then falls in a panel outside the arm — a shoulder cap alone read as a
detached pad. Reworked after the user's hooded-knight reference (crimson and the shared hood kept,
the user's calls; `DemoGear.WraithCloak.cs`): a two-roll WRAP gathered round the throat, crossing
the centre line to the bare shoulder's collar; the cap and panel shaded in broad DIAGONAL planes
(a 3.6-cell period came out as thin parallel stripes - 5.5 reads as folds); the panel ending at
the hip in torn points, and the cloak's OWN calf-length cape (no longer `CapeRows`) with a seeded
torn hem, hinged at the neck, `CapeSwingScale` 0.7. Its back view is DERIVED from the two fields
(the drape strip outside the cape, in the tone just inside the cape's lit edge). Authored on -X; `SyncDrapeSide` mirrors it by `!CarryInBackArm`, and
because `GearItem.DrapeSwingsWithCape` is set it is BENT by the cape's own `ClothBend` state (same
hinge, length and springs) so the two swing as one cloth (the poncho's yoke leaves it off and sits still). Turned away it
swaps to `GearItem.DrapeBack`, ranked over the cape (`DrapeOverCape`): ONLY the strip past the
cape's edge, in the cape's own tones, stroked on its outer edges only — the front art laid over the
cape drew a seam through what must read as one cloth. Cloaks carry NO crease marks — `CapeRows` (every
cape) and the poncho's back are shaded top to bottom only; from behind, dashed creases read as
scattered squares, not folds. `CapeRows`' top edge SLOPES — full height only across the collar
(behind the head), down to the body's shoulder line (y 21) at the edge. Flat at y 25, its corners
read from behind as shoulders three cells above the body's, and every pauldron looked slipped. The
pauldrons' own height is fixed by the chin (the head is wider than the shoulders); fit cloth to
them, not the other way round. The set's asymmetry lives on the
CLOAK, not its pauldron (whose far piece is steel). Both
drapes are sized to the largest pauldron's envelope (|x| ≤ 13.5 cells, down to y 12 cells); a
pauldron grown past that will poke out.

## The walk: one clock, feet first, knees

`Art/Gear/PrimitiveCharacterRig.Gait.cs` (the rig is `partial` now). History: the first gait was a
walk driven by one sine (legs +-26 degrees as straight stilts, straight-stick arms, bob and dip all
peaking on the same frame) and read as a metronome. It was rebuilt as a RUN (feet, knees, one
clock), and the running didn't pan out - the user's call. It is a WALK again on the run's
machinery, so it keeps the knees and isn't robotic. What makes it a walk:

- **Stance over half the cycle** (`WalkStanceFraction` 0.6): both feet down for a moment each
  step, never neither.
- **The body vaults over the planted leg**: `GaitBob` is HIGHEST at mid-stance and lowest in the
  double support (a run is the reverse). Small (`WalkBob` 0.005) so it reads grounded.
  `WalkCrouch` 0.0035 gives soft, ready knees; it is sensitive (at 0.007 the stance knee sat at
  25-45 degrees, a squat). Measured knee through a cycle: 0 at footfall, ~15 at mid-stance, ~30
  at toe-off, ~60 peak in the swing.
- **TACTICAL, a warrior walking into battle** (the user's call): the arms barely sway
  (`WalkArmDegrees` 5 - at 16 the loose swing read as SILLY), held a little bent and ready
  (`WalkElbowDegrees` 18); a 4-degree lean into the walk; the head rides steady
  (`WalkHeadLagShare` 0.3); the foot only clears the floor (`WalkFootLift`).

- **The body is drawn nearly FACE-ON with the hips side by side**, so a leg swinging forward moves
  SIDEWAYS on screen: a wide swing is a jumping-jack scissor (A, then X). The stride is narrow
  (`WalkStride`) on purpose; the walk reads from the knees, the bob, the lean and the arms.
- **One clock** (`_stride`, radians, wrapped; `cycle` 0..1, 0 = front leg's footfall, 0.5 the
  back's). Every curve is a function of the cycle alone. Cadence `WalkStrideHz` x speed^0.5 (faster
  mostly lengthens the stride). The rate is EASED and SIGNED: a stop lets the legs finish the step
  while the walk shrinks (`_gaitWeight`), and moving away from the facing runs the clock backwards
  - required once the gait is asymmetric, or backpedalling moonwalks.
- **Legs are posed by their FEET** (`PoseLegs` -> `SolveLeg`, a two-bone reach, knee always
  forward): stance plants the foot and slides it back at a constant rate, the swing is a Hermite
  that leaves and lands at that same rate (no hitch, a little follow-through/reach) and lifts the
  foot in an arc peaking early. The hips just MOVE (bob, `WalkCrouch`, lean plant, landing) and the knees
  take up the difference - a landing now bends the knees with the feet planted instead of sinking
  the rigid legs into the floor. Standing, the hips sit exactly a leg's length up and the legs are
  EXACTLY straight (the tiny margin in `SolveLeg` - the bend grows as the square root of the
  shortfall, so a 0.5mm margin popped the knee 7 degrees).
- **The knee is the elbow's mechanism** (`PaintKneeSplit`, `CutAtElbow`, two texels of overlap, no
  lengthening): `KneeTexels` 24 of the leg's 48, the middle of the pinch the leg art already had.
  Leg, greave and boot are cut; a boot wholly below the knee goes to the shin whole. `WithShins`
  (called by `WithForearms`) ranks each shin directly above its thigh, so a straight leg composites
  exactly. `ArmourStand.PivotFor` maps the shins to its (never-bending) legs.
- **Torso and hips share the bob** (`GaitBody`) so the waist never stretches; the head trails it a
  beat (`GaitHeadLag`, small - up against the torso is a neck appearing). A steady lean into the
  walk (`_walkLean`, kept apart from the acceleration spring, negated on a mirror flip; the hip plant
  reads only the spring's).
- **Arms swing on FREE hands only** (`GaitArmShares`): full for an empty hand or the bow's drawing
  hand, half for a hand holding a disc, none for the carrying arm, the bow arm, or two fists on a
  hilt (whose shoulder swing drops to `WalkHiltSwingShare`). Zero outright during a swing/charge -
  the arcs are tuned for a straight arm.
- TopDown keeps rotating the whole leg (`WalkLegDegrees`) - its capsule legs have no knee.
- **Cloth over the legs** (every Tasset and Belt) hangs from the hips and is pushed by the legs -
  rules in CLAUDE.md ("Cloth over the legs moves with them"). Judged on the Aether skirt: before,
  the swing knee's greave came out through the side of the dress on every step. A ClothBend's
  `LateUpdate` must be invoked too in a reflection harness, or the cloth draws its last frame.

**Judging it**: stills don't show a gait. The scratch harness that worked: build a fresh rig far
from the hub (x=500) with its own camera on flat grey, copy the hub profile's look and gear,
invoke `Update` by reflection with `rb.linearVelocity` set, and either pin `_stride` per frame (a
cycle strip) or step `Update` at the frame's `deltaTime` through a velocity plan (start/stop/turn,
captured every 40ms into a GIF). The real hub floor is too dark to read dark legs against.

**The chain harness** (swings, hand-offs, layering): in Play mode in the hub, ONE `eval_file`
that sets `Time.captureDeltaTime = 1/60`, disables `HubAvatar`, holds the avatar's
`rb.linearVelocity`, and loops the rig's `Update`/`LateUpdate` by reflection - `Time.deltaTime`
is a fixed 1/60 inside a single eval, and no physics step runs to move the body. Fire
`PlayAttack`/`PlayCharge` at the frame each step of a REAL chain starts (opener, lead-in,
finisher at their intervals), render a camera on the avatar to a PNG every frame, log the arm,
elbow, wrist and stack flags, then grid the PNGs. Paused `EditorApplication.Step` does NOT work
here (deltaTime reads 0 and the velocity never moves the body). Start a motion from the carry only
if the game can (openers: Chop, Sweep, Thrust, Jab) - Rise, Slam and Spin from the carry show
poses no chain ever reaches.

## Errant: a hooded knight, silver on one side

`Art/Gear/DemoGear.Errant.cs`. Six Diamond, power-0 pieces after the user's hooded-swordsman
reference: Plastron, Pauldron, Gauntlets, Belt, Faulds, Greaves. **The hood is NOT new** - the
user's call: the Hooded Cloak (`black_hood`) already is the reference's hood and back drape, and
the cowl under the chin is a fifth scarf dye, `ashen_scarf` (a step above the hood's own cloth so
it reads as a second layer). The full look is those two plus the six.

- **Silver on the FAR arm**, the Talon rule again by the user's call (the reference has it on the
  sword arm): the pauldron and gauntlets are `Lopsided`, silver on the left, black on the right.
- **The arm under the silver pauldron is NOT plated** (the user's call, after the second
  reference): a black sleeve to the elbow, a black forearm wrap of three bands with a rivet each,
  and the HAND BARE. The rig's own hand is the undersuit's DARK fist, so an empty glove row shows
  no hand at all: the hand is drawn in `BodyLook.GearSkinShaded` and repainted in the wearer's tone
  (`ShowsSkin`, Talon's mechanism).
  It replaced a silver couter, vambrace and lamed fingers.
- **The silver pauldron is three SEPARATE shield plates** (the user's second reference, a stack of
  nested plates; it replaced a cap over four lames whose lower edges striped into each other). The
  front plate hangs over the upper arm, its point OUT past the arm (the user's call on the
  character - pointed in toward the body it read backwards for this arm); the two behind step UP and
  a little IN toward the neck and shrink a touch. What makes them read as separate rather than one piece of
  metal: each back plate shows ~4 rows of its own face (rim, shading), a black gap plus a shadow row
  under the plate in front, and the step is NOT along the plate's slanted outer edge, so the sides
  notch the outline. Stepped along that edge, or showing only a sliver, the three merged. The near
  one is still a small black cap and two lames, so the silver one owns the silhouette.
- **One bandolier**, near shoulder to far hip, over a ridged breastplate with a plackart - a single
  strap is fine where two crossing read as a bow tie.
- **The faulds' splints sit a texel below the belt**: the belt's outline covers the top of the
  Tasset, and at the belt's own height the first row was swallowed. The mail runs to mid-thigh.
- **The knee cop is wider than the leg and points DOWN the shin** (Talon's points up), the lame
  above tucked under its dome. At the leg's width, with a dark row and a gap above it, the cop read
  as an "E".
- Coat panels hang outside the legs (Orichalc's finding), so the cops stay in view.

## Survivor: a white tabard dyed by the cape

`Art/Gear/DemoGear.Survivor.cs`. Six Diamond, power-0 pieces after the user's "Lone Survivor"
reference: Tabard, Pauldrons, Gauntlets, Belt, Tassets, Greaves. **No hood or cape** - the user's
call. Instead the tabard's cloth takes the colour of whatever Back piece is worn.

- **The cloth is dyed by the cape** (`GearItem.DyedByBack`, `Art/Gear/ClothDye`). Authored in
  `ClothDye.Undyed` (warm white) and repainted at Apply in a ramp built round the cape sprite's
  MASS colour - its commonest interior texel, measured, so every cloak dyes it with nothing added
  to the cloak. Built by HUE (value scaled, saturation eased toward the light), not lerped to
  white: a lit red goes pink that way. Steel and silver never change - only the cloth ramp's six
  tones are matched. No Back piece: white, which is also what cards and NFT images show.
- **Two pieces carry the cloth** (Tabard on TorsoArmor, Tassets' skirt on Tasset), both flagged,
  so the panel is one colour collar to hem; the pauldrons' metal is a third dyed piece (below). Any new tone on either piece must be a cloth tone or
  a non-cloth ramp - an off-ramp white would stay white under every cape.
- **The chain is the TABARD's**, running under both pauldrons' inner edges to a ring on each.
  On the pauldrons it would swing loose: each turns with its own arm.
- **One chain, no engraving.** The head and arms leave about a dozen texels of chest. Two swags
  plus a studded yoke read as three rows of dots. Chain links are MID steel with one dark texel
  in three: lit steel is the white cloth's own value and vanished into it.
- **Pauldrons are ONE rounded plate** (the user's templar reference, cross left off; they were
  dual plated before): a big smooth dome over the shoulder, its inner-lower side cut by a slope
  down the chest, the outer side rounding over the upper arm, a dark steel lame peeking out under
  it. Both shoulders alike, not Lopsided.
- **The plate is REACTIVE METAL**: its face is in the cloth ramp and `DyedByBack`, so it takes
  the cape's colour with the tabard, shaded as a dome with a hard specular SPOT toward the light
  (glow straight against base - a crescent band of glow read as a stripe, and cloth is soft).
- **The silver trim is thicker at the top, the extra on the neck side** (the user's call):
  `SurTrimWidth` is 1 texel outside, 2 over the crown, 3 on the inner half and down the inner
  edge's UPPER half. Measuring the inner edge below the dome's middle thickened the slope down the
  chest too. A thick band is a raised rim: lit at its outer edge, a dark texel against the face.
  Undyed it is white plate with a grey-silver edge.
- **The far pauldron is dyed too**: it is drawn at `ClothDye.Far` (0.85), and `ClothDye.Dye`
  matches those shaded cloth tones as well, mapping them to the dyed ramp at the same shade. A
  far-side dyed layer in any set must use that factor, or its cloth stays white.
- The skirt's tears are big (the Hellspawn rule): one V torn up on the lit side, one long tail
  on the near side.
- Under the Hooded Cloak the tabard goes black against the undersuit. That's what following the
  cape means; it was left that way.

## Herald: gunmetal plate, the wearer's sigil on one shoulder

`Art/Gear/DemoGear.Herald.cs`. Six Diamond, power-0 pieces after the user's armoured-knight
reference (spiky hair, a beast-faced pauldron, red cloth): Cuirass, Pauldrons, Gauntlets, Belt,
Tassets, Greaves. **No cloak** - the user's call. The leg cloth is dyed by the worn cape instead
(`DyedByBack`, the Survivor mechanism), so it is WHITE with no Back piece, on cards and NFT
images too. The swords on its back and hip are weapons and were left out. Built first as
"Lionheart" with a lion's face on the pauldron; the user swapped the beast for the element sigil,
and the set was renamed for it.

- **Gunmetal**, a green-grey steel lit toward pale mint, edged in GOLD, with the gold kept to rims:
  the pec line, the pauldron's lip and sigil, the wrist band, the fauld's lower lame, the knee cops.
- **The big pauldron bears the WEARER'S element sigil** (`GearItem.BearsSigil`, the user's call).
  Worn, it shows the attuned element (`Attunement` - the run's element, or the hub selector's),
  exactly as a Prism lights its gem. The rig paints the variant in `PaintGearLayer` and repaints
  only that layer on a change (`SyncAttunement`, called from `CharacterRigFactory.ApplyAttunement`).
  The layer's OWN sprite - cards, the armour stand, NFT images - is all four COMBINED.
- **Combined is the bare six-pointed star.** It already holds all four marks: each triangle's flat
  edge is the other one's bar (Earth's crossed ▽, Air's crossed △). The Armillary's extra bar
  through the centre closed the middle into a "B" at this size. The marks are the sigil door's
  triangles, drawn by `ArmSigil`'s measure (edge normals), radius 7.6 - at 6.6 the star's lines
  ran together.
- **The sigil pauldron is on the FAR arm** (Talon's rule, and the reference's own split - its
  beast arm is not the sword arm). Pauldrons and gauntlets are both `Lopsided`.
- **The chain hangs from the far FIST**, on the gauntlet grid, which runs past the hand
  (`HerChainBottom`). The wrist cut puts it on the hand layer, so it swings with the fist.
- **Everything on the far side of the Tasset layer stops at the body's edge** (`HerFarEdge`). The
  far arm draws behind it, and with the fist at the belt, a far panel or fauld out past the hip
  buried both the hand and the chain (first pass).
- **The cloth is a parted FRONT panel**, not side panels. Hung outside the legs it was two thin
  strips; the reference's red is across the front of the thighs. It parts at the middle where the
  dark trousers (the undersuit) show, has gold piping down each parted edge, and its hems are torn
  ABOVE the knee so the cops stay in view. The near panel's outer part runs on as one long torn
  tail outside the leg.

## Tepes: black plate scrolled in gold, a torn navy cape

`Art/Gear/DemoGear.Tepes.cs`. Seven Diamond, power-0 pieces after the user's "General of Tepes"
reference, asked for as close to one-to-one as possible, minus its red under-glow: Cuirass,
Pauldrons, Gauntlets, Belt, Tassets, Greaves, Cape. Near-black plate with a violet cast, antique
gold, indigo cloth.

- **The reference's split is kept, and it is the only one that works**: the spiked pauldron is on
  the SWORD arm (near, `Lopsided` so it follows the carry when turned away) and the cape's fur
  mantle lies over the other shoulder. Talon/Errant/Herald put their showpiece on the far arm, but
  the rig always mirrors a Back item's drape opposite the sword arm (`SyncDrapeSide`), so a far
  pauldron would be buried under the fur. Facing the camera this is also the reference's picture:
  pauldron on the viewer's left, mantle on the right.
- **Shoulders kept close to the body** (the user's call - the first pass was bulky): a dome 15
  texels wide, one tall spike off its crown and one off its outer rim, two gilt-hemmed lames. The
  first pass reached 37 texels from the centre line with spikes, more than twice the head's
  half-width. The tall spike's root must sit outboard of the head (o >= ~5) or the head hides it.
- What failed on the dome, in order: a gold COIL from the centre read as a snail shell; a vine
  across the face closed with the gilt foot into a MOUTH; a scalloped arc plus a gilt foot closed
  into a RING. Now: the scalloped arc under the crown only, no gold at the foot, and a highlight
  inside the arc (without it the dome was a flat black disc). Three spikes along the rim read as
  a CROWN; keep the outer one lying out sideways.
- **The chest**: the yoke's gilt rim runs in a V down to the medallion (the reference's necklace
  line) and a plain spine tapers from it to the waist. A lozenge on the spine made medallion +
  spine a chess pawn.
- **The mantle** (`BackOver`, `DrapeSwingsWithCape`, back view derived like the Wraithguard's):
  fur reads by its EDGES - a lumpy top, a tufted hem - and is shaded in broad planes; a hatch of
  short locks read as knitting. Its panel ends above the waist so the far forearm comes out at
  the hip, as in the reference.
- **The legs** (the user's second look): tassets FLARE and part downward into a V over the
  loincloth, shaded ACROSS the plate so they wrap the thigh, and stop at MID-THIGH. Straight-sided
  with a lame line they read as boxes with lids, and run lower they buried the knee cops. The cops
  carry no gold (round their lower half it read as a U, a ridge as bars across both knees) and are
  lit in a band, not a spot; the gold hems the lame below. The centre plate is lit only along a
  ridge - lit all across it read as glare.
- **The belt is the cuirass's width at the waist** (13 near, 9.5 far), level and one tone end
  to end; past the cuirass on one side, stepped on the other and lit lighter there, it read as
  uneven plates. The gauntlet cuffs carry NO gold: the fists hang at belt height, and a gilt cuff
  a texel under the belt's gold line read as more belt plates beside it. The cuirass has no lame
  line at its foot - on the belt's top edge it widened the black outline into a gap.
- **The plates under the belt are one width on both sides** (the user's circled issue): Herald's
  rule of stopping the far side at the body's edge made the far tasset a gold-edged sliver beside
  a broad near plate. Both now flare from the belt's width to just past it, overlapping the far
  fist's inner edge a little. Gold only on the tassets' OUTER edge and foot - gilt inner edges
  slanting beside the centre plate's V made a gold zigzag. The dark line between belt and plates
  is the belt's own outline (one art pixel, four texels at BodyPpu), the house seam every set has.
- Cape tears sit inboard of the body's edge: torn at the visible edge they read from the front
  as star-shaped gaps. At about 2.8 x 5 cells they read as holes from behind; at 2 x 4 they
  closed into runes (the Hellspawn rule).

## Aether: black plate, a curse burning in it

`Art/Gear/DemoGear.Aether.cs`. Seven Diamond, power-0 pieces after the user's reference (a
corrupted knight-queen in blue-black plate veined with red, a long dark dress, a visor), crossed
with a CURSE MARK (a three-tomoe seal whose flame-shaped marks spread across the body): Helm,
Pauldrons, Cuirass, Gauntlets, Faulds (Belt), Skirt (Legs), Greaves (Boots); and the Aether Greatsword, its
Black Diamond sword (below). Named Aether by the user (built as "Corvus", for the raven of the nigredo). Every mark
is the Secret Fire's (CLAUDE.md, "Marks that burn in the wearer's element"): black on the piece,
lit in the attuned element.

- **Marks are STROKES** (`CorStroke`): a polyline with a half-width tapering root to tip, Ember on
  its last stretch so a tendril dies into the plate. The curse's tongues are wide at the root and
  pointed, with BARBS - smooth, they read as piping; the reference's veins are one texel all along.
- **The curse spreads ONE WAY.** The seal (three hooked tongues round a hot heart, `CorSeal`) sits
  under the collar a little toward the far side, and the marks sweep from it down across the
  sternum to the near ribs, down the far side, up the collar toward the neck; the FAR pauldron and
  FAR gauntlet carry more of it than the near. Symmetric round the sternum it read as an emblem -
  a spider, then a sun. Above y 26 both pauldrons draw over the chest's corners: keep chest marks
  inside x -6..6 there.
- **The helm is the user's Magneto references** (a comic panel, then a black prop helm with
  silver trim; it replaced a visor, 2026-10-06): a smooth round dome (`SealsHead`), the face in
  an M - a rounded ARCH over each eye meeting at a POINT on the nose, each arch's OUTER end a sharp
  point past the guard's edge - and cheek guards closing in toward the mouth, the foot LEVEL on the
  chestplate's top (at the jaw it left a strip of neck). The prop's silver band is the SECRET FIRE:
  one thickness (`CorTrimWidth`, 2 - tried at 3 and trimmed back by the user) round the whole opening and along the whole foot, measured as a
  disc of neighbours so it holds round the arches and slants. At each outer point the band is MITRED (`CorTrimMitre`, the point's two edges carried
  outward) - the disc alone rounded the outside of the point into a nub - and stopped short of the
  side, or it runs out to the edge as a horn. The far point has almost no room; it stays small. The
  helm's OUTER edge is tested before the band, or the band pushes through it. No flares off it (the prop's horns
  at the brow and barbs on the guards) - the user's call. The foot is LEVEL: slanted
  up under the guards it stepped the band and left the guards' tips as lumps; and the foot's own
  edge is tested before the band, or the band pokes through it at each tip. Its own edge, no
  auto-outline (it would eat the eyes). The first pass (a ridge, a pale side plate, two-plane
  shading) was replaced wholesale; what it learned: in this near-black a dark side plane reads as
  a bob, a rounded panel as a lock, a vertical sheen streak as strands, an oval sheen as a blob.
- **Pauldrons stay close**: one swept plate rising to a point just above the shoulder line, two
  lames. The first pass stood half a head above the shoulders and read as wings.
- **BLACKENED STEEL, not black diamond** (the user's call): a near-neutral dark grey whose sheen
  is a low grey. The first pass lit a blue-black toward a bright, cold steel blue, with glow-tone
  edges everywhere, and read as black diamond - a crystal texture this density cannot carry. Keep
  tone 5 off the plate's faces; edges at 4. The greatsword shares the steel.
- **The skirt is the dress, CLOSED** (the user's call): a FRONT PANEL of the same cloth hangs from
  the faulds' split, widening to the legs' own span, to a hem at mid-shin, so the greaves (whose
  cracks climb from the heel) and sabatons show beneath it; the sides run to the floor, torn. The
  first pass was open from the crotch down and read as a piece of fabric MISSING - the reference
  has the panel. Narrow, the panel left both legs under the side panels. The far side holds to the
  body's edge down past the fist (the Herald's rule) before it flares.
- **The faulds SPLIT at the front into THREE PLATES a side** (the user's call, after the
  reference's hip; Shogun's kusazuri is the nearest thing in the file, panels round the hip, but
  these are three long plates, not lames): long over the front of the thigh, shorter over the hip,
  shorter again turning away behind, each overlapping the one behind it to a shallow point. The
  plates are the long part and the band the short one - the first pass had one short plate a side
  and a pointed plate over the middle, and the ratio read wrong. Each plate a separate curve: a
  lit leading edge, a dark seam behind - shaded only a step darker plate by plate, the three ran
  together. The vein runs down the FRONT plate's edge and along each foot; traced round every
  plate, the U-shapes read as tubes. The far side shows two plates, inside the body's edge until
  the fist is passed.

**The Aether Greatsword**: after the reference's black sword - a black blade with a long point, a fuller
line burning from a row of four RINGS above the guard to an ARROWHEAD short of the point (its tip
hot), a cross-guard whose own line crosses the blade's at a hot heart, a black wrapped grip, a kite
pommel with a spark. One field over authored cells sampled for the arena and (x4) the menu, so the
two never disagree; the family height through `BladeRowsFor`. A 9-row point read stubby - 14.
Black Diamond with no relic: a signature Weapon Art would be its own design.

## Chibi proportions

Rebuilt from realistic proportions (body taller than head, visible neck) to anime/chibi (head
bigger than the whole torso, no neck — closed by texel OVERLAP between head-bottom and torso-top,
not by nudging offsets closer). Pivots moved with the layout: torso pivots at the waist, head/arms
pivot at the torso's new top edge.

## Menu-density hair, eyes, and faces

A face built at menu density needs STRUCTURE, not just a scaled-up flat block: a lash line, a real
iris the lids visibly clip, a wide aperture — evaluated as continuous per-texel fields (distance-
to-edge ramps), never assembled from axis-aligned rectangles (an assembled eye reads as mechanical
however the parts are curved). The digit ramp order is NOT brightness order (`Deep < Line < Dark <
Base`, since Line lerps toward a near-black TINT rather than pure black) — using it as if it were
produces a visible dark band mid-iris.

Hair reads by OUTLINE, not shading: locks leaving the head, an uneven fringe with skin showing
through — never scored grooves into a solid cap (reads as a helmet regardless of shading). Gaps
between locks are TRANSPARENT, never a dark tone (dark-on-white is confetti, not hair).

## Hair: two views, at the head's design density

Each hairstyle has a TURNED view (authored facing right; the rig mirrors it) and a BACK view in
`BodyLook.HairArt.cs` (format in `BodyLook.HairViews`). Nothing is shown face-on any more.

- **Start from the style's face-on original** (`Tools/hair/legacy/BodyLook_faceon.cs`) and change
  only what the view needs. A from-scratch redraw read as twenty different haircuts and was thrown
  away. `python3 Tools/hair/compare.py` puts every original beside its two views.
- **Draw at the head's DESIGN density** (16-cell grids, doubled on load), not the body's texels: the
  face under the hair is the 16x14 design doubled, and finer hair is two pixel sizes on one head.
- The cap is composed into the head and goes through `FaceDetail.Build` like the rest of the face;
  the back of the head is the bare skull plus the back cap, through the same passes.
- **A K line inside hair must not touch the outline or the sprite's edge** - `ThinOutline` erases
  any K it can reach from the border.
- From behind, the mane draws over the cape and torso (`ManeOverCape`); turned, behind the body.

**The turned face** (`BodyLook.Gaze`): the near half of the face (eye, brow, expression marks)
moves `GazeShift + NearEyeLead`, the far half `GazeShift`, and the far eye loses one texel off its
outer edge (`FarEyeTrim`). Moving both eyes equally kept the face-on spacing and read as a front view.

**Hair wears its swatch** (`HairRamp`, lift 0): the hair pass paints in Light/Dark/Deep, never Base,
so a lifted Light made every colour paler than its chip - and red, pink.

Known, not fixed: helms have no back view, so from behind a helmeted character shows full hair.

## Relics on the hip: the pouch is the standard

Every worn relic goes through `DemoGear.HipRelic`: drawn at the BODY's density (the pouch's), hung
so its outlined top meets the pouch's on the belt, centred on the pouch's hip (`RelicHipX`). The
Black Diamond marks and seals were drawn as weapons (`FinePpu` + `upscale2x`, 2x2 texels a cell) and
came out twice the pouch, hanging in open air; the grids themselves are untouched. Sizes are
around the pouch, not uniform (9-15 texels against its 10x12).

**The saya is the outlier, and is WORN, not hung**: its mouth is placed on the belt at the pouch's
hip (`SayaMouthAt`), leaning in toward the buckle with the tip out past the hip, at body density.
Its bore is the TSUKA's width (`SwordGripWidth`), because the finisher leaves the handle standing
on it. It sits on the character's LEFT hip in both facings (`GearItem.WornOnLeftHip` - the rig
mirrors it with the carry, as it does the Wraithguard's drape).

**The right hand stays on the sword through Crosscut.** `KatanaSheathe` is a separate sprite while
the rig's weapon is hidden, so it drives `ICharacterRig.SetHandTarget` every frame: a two-bone reach
in the rig's `LateUpdate` (the coroutine moving the blade resumes after `Update`), the fist behind
the guard on the way in and just above the mouth once home. It starts from `TryGetSwordHand`, so
there is no snap out of the combat grip; facing left, `arm.back` reaches, drawn in front of the body,
its shoulder sliding toward `ArmBackGripRest` only as far as the reach needs. **The reach never opens
the grip** - it poses the free arm itself (`offHand`, eased off and back on) - because opening it
into the carry left the draw-cut starting from the carry: blade turned 70 degrees in the fist and
sunk behind the body. The copy sorts at the rig's WEAPON layer, not "one above the saya" (which put
the drawn blade behind the pauldron and head while the fist stayed in front). The saya's geometry
numbers used to divide grid cells by `FinePpu` without the 2x upscale and were all HALF - derive
them from the grid at the density it is drawn at.

## Cosmetic weapons: lessons from building them

Recurring lessons worth remembering when building the next one:

- **`StrokeOutline` erodes one texel off every side of a transparent gap bordering opaque art** —
  a hollow, tear, or fork narrower than ~5 texels closes up completely under the auto-outline.
  Measured directly more than once (the disc's grip hollow, the Rift Blade's tear, Sniper's prong
  gap) rather than assumed.
- **A single mirrored wave/wobble reads as constructed**; real turbulence/asymmetry (the Rift
  Blade's tear edges, Lumen's lightning vein, Crossblade's arm shading) needs two independent
  waves at different frequencies/phases per side.
- **A weapon's pivot is always its grip**, never its geometric centre, or it visibly orbits the
  wrong point mid-swing.
- **World size must match to the texel across skin/menu/rack variants** or the character changes
  shape when a screen opens — checked as a standing cross-weapon rule (`## Every sword the same
  height` below).
- **A ramp's `lift`/alpha weighting decides whether "glass"/"energy" reads as material or as a
  bright cartoon rim** — weight alpha to the LIT tones (`ShadowAlphas`-style), and keep lift low
  enough that the edge doesn't blow out past what the material should look like.

## Every sword the same height — and every blade measured against the BODY

All the greatswords measure **1.0533 world units** tall (`DemoGear.SwordHeightTexels`, 158
texels at `FinePpu`), against a character 0.7733 tall — so the weapon stands about 1.36× the
figure. (It was 148 / 1.28× until the handles were lengthened - see below.) Fixed by lengthening the GRID (more rows), never by hand-touching `Size`/offset/pivot,
which would reintroduce the non-uniform stretch `SizeOf` exists to prevent.

**The rule is now a mechanism, not a convention.** Each weapon declares the rows it spends on
everything that is NOT blade (tip, wings, guard, collar, grip, pommel) and `BladeRowsFor` works
out the rest, so moving `BladeRows` carries the whole set. It was ten separate literals that all
had to be edited together and never were — several carried comments describing a length their
sword had stopped having. `BladeRowsForNoOutline` is the same budget for the two weapons drawn
without the auto-outline, which must make the stroke's thickness up out of their own grid.

**Widths come off the body, not off each other** (`SwordBladeWidth` / `SwordGuardWidth`, in the
same LAYOUT CELLS the rig counts joints in, so they follow a reproportion):

    the blade AND its outline are never wider than the TORSO       13 cells -> grid 10
    the guard AND its outline are never wider than the SHOULDERS   19 cells -> grid 16

rounded down to even (PixelSprite mirrors about a texel BOUNDARY, so an odd width has nothing at
its middle to mirror about). The outline is subtracted because the eye measures the DRAWN edge:
a blade authored to exactly the torso comes out a cell wider either side, and looks it.

They were 18 and 28 — a blade wider than the character's own head under a guard nearly twice their
shoulders, which read as a PLANK rather than a sword. Narrowing alone was not the fix: at the old
length the sword was still the character's own height and still read short and thick, so
`BladeRows` went 34 → 50 in the same move. **Treat the two as a pair.**

**One envelope, five builders.** `BuildGreatsword`, `BuildGreatswordDetail`, `FireBlade`,
`BuildRiftSword` and `BuildShadowSword`/`Detail` all draw the shared silhouette, and each used to
retype "blade over cols 5..22, guard the full 28" by hand — seven copies counting the menu grids.
They now share `SwordTipRows`/`SwordBladeRow`/`SwordGuardRows`/`SwordCollarRow`/`SwordGripRow`/
`SwordPommelRows` and the `SwordDetail*` family, which are the same sections at twice the size.

**The grip does NOT follow the blade** — what closes on it is a fist, and a fist is the size the
body says whatever the sword does. `SwordGripWidth` is 4 (was 8, then 6 - thinned with the
lengthening, since a long grip at six read as a second blade stub). Odd-width grids (Crossblade,
Lumen, Obsidian - they centre on a column) use the family width plus one.

**LONG HANDLES, and the fists at their CENTRE.** `SwordGripRows` is 12 (was 7): a two-hander's
grip is a fifth to a quarter of its length, and at seven the fists sat hard against the guard and
hid every hilt (the Sniper's spinning cylinder was the trigger). The five rows were ADDED to the
sword - `SwordHeightRows` grows with `SwordGripRows` - not taken from the blade, so every blade kept
its length. Swords with their own grip constant got the same +5 (Zanmato 17, Sniper 17, Prism 15).
The pivot is `GripCentre(gripTop, gripRows)` for EVERY greatsword - the grip's centre, not its 2nd
row - so the guard stands clear above the hands and the pommel below. Any grip written as literal
rows must use `SwordGripRows` or that sword comes out short (Blood did once; Tria Prima's grip and
pommel were literal rows until this change). Effects placed up the blade must be DERIVED from the
sword's own grid (SaintHaloAlong, PrismGemAlongs, RiftShardsAlong) - the Rift's shards were a
literal 0.52 until this change.

**Measurements taken INSIDE a blade do not scale with it for free.** Emberline's glass lens,
Shadow's hollow fuller and the Rift Blade's tear are absolute texel half-widths tuned to leave a
specific amount of flat either side; `SwordBladeScale` moves them with the blade. But the OUTLINE's
bite is absolute, not proportional, so anything that has to survive it needs a clamp of its own as
well — the flats are served first and the groove takes the middle (`ShadowMinFlat`, `RiftMinFlat`).
Scaled by width alone, Shadow's fuller closed to a scored line.

**Three weapons earn a documented exception**, and only because their own idea will not fit:

    Sniper    a gunblade on a COLT PYTHON: stainless (bright highlights on DARK reflection bands),
              a barrel at a Python's proportion to its frame (18 rows) - vented rib left, round
              barrel ending in a crown and bore, full underlug right, front sight + orange insert
              - and past the muzzle the rib and underlug carry on as TWIN BLADES, the gap between
              them the barrel's own width (4 cells, safe from the outline). The CYLINDER is side
              on, six flutes, cut off from the frame by dark SEAMS so it reads as its own part;
              after a finisher, if NO attack follows, an idle flourish (CylinderSpin): the gun is
              held out at arm's length (ICharacterRig.SetPresent - barrel forward, hammer up), the
              cylinder turns all six chambers (GearItem.SpinFrames), the arm comes down. Any attack
              cancels it; it used to play ON the next attack and was lost inside the swing. Hammer spur (checkered pad) on the rib side, trigger guard loop the other:
              the one asymmetric hilt. Walnut grip CURVED back like a revolver's, checkered panel,
              gold medallion, rounded butt. One field (SniperTexel) for arena, menu and spin frames
    Phantom   ~20 at the gas  a dark GLASS body (8 cells, inside budget) carrying the face, in a
               cloud of round gas PUFFS (union of circles, lit at the rim) - lumps read as gas
               boiling off something, sine-wobbled edges read as a ribbon. Glass reads by its
               EDGES: dense rims, a clear-ish middle, a highlight down the lit side; flat alpha read
               as a ghost of a blade. A slimmer body under a bigger cloud was tried and rejected -
               the body is the object. Metallic purple hilt: hooked guard arms, gem in the face's
               glow, lobed langet, wrapped grip, crowned spire pommel. One field (PhantomTexel)
               for arena, menu and the 8 haze frames; the frames go through StageSprite - built
               from the raw grid they were HALF the resting sword's size
    Saint / Crossblade / Blood   wings and arms may pass the guard — they ARE those weapons
    Emberline  crescent horns reach ~21 cells, curling up beside the blade; the guard's BODY
               stays inside SwordGuardWidth. Horn-to-blade gap kept at 3 cells so the outline
               can't close it
    Silver     X-shaped gunmetal guard (arrowhead arms, 42 deg up / 28 deg down) past the shoulder
               budget; gunmetal cone pommel takes 5 rows, so the blade gives up 2 and the grip/pivot
               sits 2 rows higher (SilverGrip, derived). Same continuous-field approach as Emberline
    Blood      a BAT WING (~12.5 cells at the claws): spine up the lit edge, four finger ribs,
               scalloped membrane. The MEMBRANE is the vial meter - gunmetal empty, blood rising
               from the guard per stage, a bright surface line on the fill. Arena ribs are thinner
               and uncreased, or the wing reads as a screw thread
    Rift       an "Aztec sun" hilt in the SIGIL DOOR's stone (SigilDoor's own colours): a 22-cell
               HALF-DISC guard, dome up into the blade, rays CUT as one-cell grooves measured in
               cells (angle-spaced wedges read as blobs); wood grip; a half-disc pommel hung the
               other way round a core of the tear's light. Guard 9 rows + pommel 5, so the blade
               gives up 4 and the pivot sits 2 rows higher (RiftGrip, derived). ONE field
               (RiftTexel) for arena and menu, tear included; the menu cuts finer grooves, not new
               features. RiftTearEdges clamps its sine - Pow of sin(PI)'s tiny negative is NaN,
               which fails every edge test open and fills the row
    Lumen      a CIRCUIT-BOARD hilt: an upright CYLINDER of board, the pommel's width (9 cells) and
               about as tall, in line with the rest of the hilt. The pattern is laid out in SURFACE
               coordinates (arc length round it, and height) and projected: the big chip faces
               front, the two small chips sit low on the sides at 72 degrees and wrap out of sight
               past the silhouette - that squeeze is what makes it read round. Shaded by facing
               (lit left, shade right) plus a specular stripe in the menu. It was the reference's
               triangle, then a flat disc (both past the hilt's width). Traces run small chips ->
               big chip -> a cyan trace up
               into the blade; gold contact fingers for a grip, a pin-header pommel, and the
               tail a LIVE WIRE: red insulation, frayed copper, sparks flickering at random
               (LumenWireFrames). The tail hangs from the POMMEL'S END (worked out from the weapon
               sprite's own pivot and bottom edge) with gravity, not from the fist - hung at the
               anchor it lay over the grip. It was also built undoubled, a tenth of the character's
               height; it goes through StageSprite now. Menu art is a
               FIELD for the blade too (LumenBladeTexel: same outline, LumenBand resampled, the same
               vein waves plus a fast jitter) - the derived Smooth pass on a grid this regular was
               just the arena picture at 4x - with the board drawn over it from the hilt field.
               Arena board edge is ONE dark tone: a lit diagonal at cell size is a checker. It
               read as two pictures stacked until the blade was tied INTO the board: the blade
               pinches into a STEEL socket (the core's own metal, not chip black - black read as a
               hole) on the cylinder's top, every trace glows the blade's cyan and
               funnels small chips -> big chip -> socket, and the blade's light spills (OPAQUE
               tones) onto the board's top and rims
    Saint      the reference's CLOVER hilt: three open gold rings (side rings at the grip's end,
               the centre one above them holding the blade's end and a small Y), the Saint's wings
               sweeping up from the side rings, a long ivory grip, and a pommel of three small
               rings with little wings turned DOWN. Ring holes are sized to survive the arena
               outline (radius 3.6; at 3.2 they closed to pinholes). The GLASS reads by its edges:
               nearly clear middle ('b' 0.28), a darker refraction line inside each gold edge, a
               highlight streak, diagonal glints in the menu - grey bands at 38-88% alpha read as
               steel. One field (SaintTexel) for arena and menu. The halo's height AND size are
               DERIVED (DemoGear.SaintHaloAlong / SaintHaloSize = 2.4 blade widths) - literals
               0.48/0.60 counted off the old grid left a hoop round the shoulder. SaintHalo.Attach
               now re-places an existing halo too, not just re-sizes it
    Prism      the reference's crossguard carved in Prism's own violet-grey STONE (silver was tried
               and read as a second, unrelated material on a glass-and-gem weapon): 21-cell arms
               flaring into notched fishtail ends, a lozenge boss (knotwork cut in the menu) and a
               langet up the blade; a BLACK AND STONE handle - black wrap by the guard, a stone
               diamond ring, a stone lattice sleeve - 10 rows long, and a forked pommel. The arena
               blade and gem rows are unchanged (PrismGemAlongs still scans them); the menu blade
               is a field reading the gem positions back off the arena grid
    Shadow     the reference's CRESCENTS round an ECLIPSE: two arcs a side (outer ~16 cells - at
               radius 6.2 they lay over the blade's dark base and vanished), tapering to horns either
               side of the blade, a dark disc with a bright rim at their centre, a crescent cupped
               under the grip. Iron lifted well past the blade's lift so its edges separate from it.
               The blade is SHADOW-GLASS: dense at every edge (rims and the hollow's walls), clearest
               mid-flat ('s' 0.28) - flat alpha read as a grey sheet. Family grip/pivot unchanged.
               One field (ShadowTexel) for arena and menu
               SHADOW, WHOLE: the Shadow's look DRAWN (a skin counts) with its relic SOCKETED and
               the character casts NO contact shadow (CharacterRigFactory.Paint -> rig
               SetContactShadow). Either half alone keeps it. It still shows while AIRBORNE - there
               it is a leap finisher's landing marker, not decoration
    Zanmato    a RED BRAIDED tsuka (two cords crossing into the diamond lattice, dark red in the
               diamonds) and a GOLD CLAW kashira (a cap, then a talon hooking down and round to
               the edge side), after the user's reference. The claw's 5 rows came off the tsuka
               (14 -> 12), NOT the blade, so the blade, the pivot and everything KatanaSheathe
               reads (ZanmatoBladeAboveGrip, the saya geometry) are unchanged. Blade/tsuba/saya
               untouched. STRAIGHT, by decision: a 2-cell sori (whole length, toward the spine,
               rounded into a proper run) was built and at arena size its two one-cell steps read
               as a BROKEN blade. The arena cannot be smoother (cells are 1.5 screen px at 1080p;
               finer is sub-pixel), and a menu-only curve would move the silhouette 2 cells between
               screens. ZanmatoCurve/ZanmatoSori are kept at 0 for the day the arena gains density.
               The menu keeps a wavy hamon. Read as a katana from its OTHER cues instead: the
               kissaki the right way round (spine straight to the point, the EDGE curving up to it
               - it had a Western point) with the YOKOTE line across its base; a GOLD habaki (the
               claw's gold, a bright stripe at the blade's base); an OVAL tsuba, 5 rows (a round
               guard seen from a little above - the flat pointed bar was a Western crossguard; the
               2 rows came off the blade). Menu art = blade from
               ZanmatoBladeTexel, habaki/tsuba from the derived Enrich pass, braid and claw from
               ZanmatoHiltTexel
    Obsidian   handle and pommel brought IN LINE with the family (grip SwordGripRows, a 3-row
               knob pommel; they were 15 and 9, a third of the sword) and the 14 rows given into
               the SHAFT. Pivot is the grip's centre like everyone else's (GripCentre).
               The HEAD is one continuous field now (ObsidianHalf + a TRUE, slope-corrected
               distance for the black-glass rim - the old chessboard distance made the band lumpy
               round the horns), with an engraved border in the menu. The HILT is the reference
               sheet's "elvish" #3 in BLACKENED steel (dark tones only - gunmetal at the brightest):
               the GUARD IS TWO STRANDS, each from a pointed arm tip beside the grip, crossing at
               the blade's base (left over right) and running on up the blade as one side of a long
               pointed loop - not a crossbar with decoration on it; a cloth-wrapped grip; a tall
               oval RING pommel with an inner ring. No collar. Arena shading is SOLID gunmetal
               (lit edge lighter, shaded darker): with no outline, a menu-style rim round a
               near-black body broke thin pieces into loose pixels. An ornate swept guard was
               built first and dropped as too close to Phantom's. Menu art from the same fields
    Crossblade a FLANGED MACE HEAD on a sword's tip, after the user's photo: a finial, concave edges
               to sharp flange corners high up, a long concave taper into a blued collar - TALLER
               than wide (wider read as a squat fan). Flanges told by LINE WORK only: the front one
               edge-on down the centre (ridge between dark lines), the others as curves nested
               inside the silhouette; the faces between them in the blade's DAMASCUS (one steel),
               a plain steel collar. A ringed
               cross and then a spiked ball with a front pyramid were built first and both read as
               TOO BUSY. Ringed guard (quillons through a ring, a boss), a leather-wrapped grip, a
               pommel that GROWS out of the grip (an oval under it read as stuck on) with a cross
               cut in it. DAMASCUS blade (sparse dark strata in the arena - equal two-tone read as
               camo) and HEAT-BLUED fittings, neither used elsewhere. Family grip/pivot and height.
               One field (CrossbladeTexel) for arena and menu
    Gilded     straight solid-gold quillons with sharp antique-gold ends, ~25 cells tip to tip; a braid up the blade
               from the boss. Arena twist/braid are TWO-tone - three tones at cell size read as a
               checkerboard

**Lumen and Obsidian were half-height and nobody noticed**, because they build their `LayerSprite`
by hand (an auto-outline is wrong for a beam of light, and would draw a line outside Obsidian's
see-through edge) and so bypassed the one function that applies `upscale2x`. `OutlinelessWeapon` is
that path now; anything else skipping the outline goes through it.

**Emberline's hilt is solidified lava** (`EmberCrustSd` / `EmberTexel`): cooled basalt with
domain-warped Voronoi cooling cracks, hottest where the mass is thickest, drawn in the HEAT RAMP's
tones so the hilt runs the heat cycle with the blade. Both densities sample ONE continuous field
(arena at cell centres, menu at quarter cells), so the silhouette can't drift between them. Its
menu art (`EmberlineDetail`) is the first HAND-AUTHORED NFT-standard piece at TRUE 300 ppu, 4x the
arena grid (112x288) — never pass it upscale2x. The arena grid cracks only the hot core and keeps
the gem clear: at cell size a crack is a whole texel and cracking everywhere drowned the gem.

**Swappable stage sprites go through `StageSprite`**, which applies the same 2x upscale as
`Pixels`. Built straight from the half-density grid they rendered at HALF the base layer's size
the moment the rig swapped to them (heat, prism gems, blood vial all shipped this way; only a
stage sharing the base layer's cache key escaped).

## The Armillary: the disc Forge set, one half in each hand

`Art/Gear/DemoGear.Armillary.cs`. Four Diamond discs - **Ignis, Aer, Aqua, Terra Band** - are the
same brass armillary frame (hub hoop, outer hoop, four spokes, grip across the hollow) with ONE
element band filled, each at its own radius: Fire round the hub, Air vanes, Water tube, Earth the
notched stone rim (the only band that changes the outline). ONE field (`ArmTexel`) with bands
switched on or off, so no part can drift from its fused self. Fused at the Forge
(`GearForge.Fusions`, `armillary`) into the Black Diamond **Armillary**, whose pair is SPLIT by
alchemy's grouping: RISING (Fire + Air) in the main hand, FALLING (Water + Earth) in the off hand.
All bands animate on one clock (vanes a twelfth of a turn per loop, the wave one wavelength, the
flames on whole cycles); Earth holds still.

- **A disc's off hand can hold a different picture**: `GearItem.OffhandLayer` /
  `OffhandMenuLayer` / `OffhandIdleFrames`, handed to the rig by `ICharacterRig.SetOffhandPicture`
  before `SetWeaponSplit`. Null for every ordinary disc (the off hand copies the main).
  `SetWeaponSprite` steps the off-hand flipbook to the SAME frame index. The pair displays (rack,
  armoury wall, rack close-up) take the twin from `GearDisplay.RepresentOffhand`, so the whole
  armillary shows where the two halves overlap.
- **Quintessence** (the Seal's signature, Medium; `AttackStep.ReverseCones`,
  `PlayerController.QuintessenceStrike`, `Combat/QuintessenceVisual`): both hands empty, the halves
  rise and join OVERHEAD into the whole armillary (`GearItem.CombinedFrames`, all four bands), it
  holds while four REVERSE CONES strike round the character - widest AT the character, pointed
  outward, one per side from the facing in Fire/Air/Water/Earth order - then it parts and the
  hands fill again. Each cone is `ResolveEcho` at a quarter of the step (four quarters = one
  Medium, Separatio's arithmetic); `GatherTargets` cuts the cone out of a reach-circle. The cones
  draw ON THE GROUND (`Spr.ReverseConeBlast`, above pits, under characters) - over the character
  they washed it out. The cone shape is a placeholder for the move still being designed.
- With Earth AND Fire (the whole armillary) the flames burn OUTSIDE the stone rim; the sigil draws
  BOTH triangles (the six-pointed star) with the bar.
- **A fusion's relic may be null** until its finisher exists.
- Discs that leave the hands take the half matching WHICH disc they are (`ICharacterRig.TryGetDiscVisual`): the first in flight is the main hand's, the second the off hand's, the rest alternate - thrown, volley, orbiting and suspended alike.

## Rai: lightning in disc form, arcing a greatsword's length

`Art/Gear/DemoGear.Rai.cs` + `Combat/LightningArcs.cs`. A Diamond, power-0 disc pair with no
metal in it: a band of four jagged strands (one white, one pale, two blue - a second pale one
speckled the arena band until the white current stopped reading as a line) over a steady
translucent glow, short tendrils crackling out of it, and a white-hot knot at the centre for the
fist, with spokes out to the band. OUTLINELESS (a black line round lightning frames every bolt as
a tube); its deep-blue edge is the outline. The glow is what keeps it a disc - the strands re-jag
every frame and alone would read as a scribble. ONE field (`RaiTexel`) per cell / quarter cell.

- **Every frame is a fresh seeded crackle** (16 at 0.06s), and the off hand has its OWN crackle
  through the Armillary's `OffhandLayer`/`OffhandIdleFrames` - an unmirrored copy flickering in
  lockstep read as one sprite pasted twice.
- **The long bolts are an EFFECT** (`GearItem.LightningArcs` -> `LightningArcs`), wired like the
  Rift Disc's trail: `SetOn` for both held discs (`CharacterRigFactory.Paint`) and displays
  (`GearDisplay.ApplyEffects`), `Follow` for thrown/orbiting/suspended discs. Random strikes that
  flash, often RE-STRIKE down the same channel, and fade; reach up to `RaiArcReachCells` (a
  greatsword's drawn height), measured in the disc's own CELLS so it rides any scale.
- **PIXEL ART, not a LineRenderer**: bolts are rasterised into a per-disc canvas at the disc's cell
  size (menu art -> half-cell lines). The canvas is UNPARENTED and never rotates - parented it
  would alias with every swing, and forcing a world rotation under the rig's mirrored chain invites
  skew. Sprite mesh must be `FullRect` (a Tight mesh is cut to the empty canvas it starts as).
- Unscaled time (every screen showing a held disc pauses the game); skipped while the disc is on
  no camera (`isVisible` - the armoury wall's bay would otherwise redraw off-screen forever).
- Sorted just behind the disc it leaves, like the light-cycle wall.

## Horologe: a clock's gear train, ticking in mesh

`Art/Gear/DemoGear.Horologe.cs`. A Diamond, power-0 disc pair in clock brass: a RING gear (teeth
inside, and a 12-tooth cog outside that is the silhouette) driving a CHAIN of three wheels -
LARGE (14 teeth, four crossings, like a clock's centre wheel) -> MEDIUM (8) -> SMALL (6) - under
a FIXED blued-steel bridge with ruby jewel bearings, which is what the hand holds.

- **Why a chain, not the planetary set it was built as first**: any gear meshing both the ring
  and a centre gear is forced to (ring - sun) / 2, so every planet came out the same size. And
  **no loops**: a gear meshing the ring and another gear that meshes the ring locks solid (an
  inside mesh keeps the sense of turn, an outside one reverses it), so only the large wheel
  touches the ring and the others keep clear of it.
- **Positions are solved** (`HorologeCentres`): each wheel sits exactly its pitch radius plus its
  driver's from the driver, where that circle crosses a set reach from the disc's centre (the most
  its ring clearance allows, so the chain spreads round the fist). 14 is the largest the large
  wheel can be and still let the medium one reach it clear of the ring.
- **The mesh is real.** One module (0.75) for the train; `HorologeAngles` puts a tooth in a gap at
  every contact and turns each wheel d Zr / Z, alternating sense down the chain. Proven by
  sampling gear-on-gear overlap across the rotation: every meshing pair 0-1 samples, every pair
  that must not touch 0, against ~350 with the medium wheel knocked half a tooth out - re-run that
  after changing any tooth count, reach or angle.
- **Stub teeth** (`HorologeAdd`/`HorologeDed`, 0.6/0.8 modules): standard depth on a six-leaf
  pinion is longer than the gear's own body and reads as a star. At module 0.62 ten-tooth wheels
  had single-cell teeth (snowflakes in the arena); 0.92 left no room for three sizes.
- **Wheels are the BRIGHTER brass.** One brass ran the train into a jumble; darker sank the
  wheels into the dark gaps round them. The large wheel's windows are a deep-brass RECESS, not a
  hole - the outline would close a hole that small.
- **It TICKS** (hold, then a two-frame step, `HorologeTickCurve`) one ring tooth a tick, so every
  wheel advances one of its own teeth. `HorologeTicksPerLoop` is DERIVED - the ticks until the
  outer cog and the large wheel's crossings have both come round (14 today). Hold first, so frame 0
  is the still. The off hand runs the same ticks backwards through `OffhandIdleFrames`.
- **Sized to the standard**: every frame of both hands draws 32 x 32, opaque area 673-698
  (the other discs run 604-740).
- **In the hand, a big gauntlet hides the train.** Blacksteel Gauntlets' lower piece is a solid
  18 x 12-cell block over the grip - the same happens to every disc's hollow. Bare-handed, or on the
  rack, the armoury wall and the character screen without it, the whole train shows.

## Singularity: a ring of light falling into a black core

`Art/Gear/DemoGear.Singularity.cs`. A Diamond, power-0 disc pair: a black hole's accretion ring in
the hand. A TRANSLUCENT black horizon (the fist closes over it), a thin bright photon ring on its
edge, and a band of particle light whose motes each spiral IN - faster, stretching into streaks -
and vanish at the horizon, over a SEMI-TRANSPARENT white ring glow (half alpha, so it still counts as
the disc's edge) that keeps the silhouette a disc. The motes are WARM - warm white, pale gold, soft
amber - between the two builds the user turned down: all saturated amber (too saturated) and all
white (washed out). Outlineless like Rai, so its grid makes up the outline's pad (32). It GLOWS on
its holder, from the ring (see "Weapons that cast light").

- **A NARROW ring near the rim** (the band 12.5-15, by the user's call - it was 9.5-15), so a dark gap
  opens between the light and the horizon for the falling motes to cross.
- **The drawing-in is the animation.** Each mote's position is a function of one fall phase, and
  every mote falls a whole number of times a loop, so the loop closes. A mote ORBITS in the band
  for most of its phase (`SingOrbitShare`, 0.86) and then PLUNGES, quickly. Easing one curve from
  band to horizon kept a third of the motes mid-fall at once - a white donut by the horizon, and a
  gap full of gold once the band moved out.
- **Streaks follow the path**, drawn in five pieces along the spiral: one straight segment per
  streak cut across the spiral as a chord and the whole disc read as a polygon. A streak's tail
  cools a step, so it reads as a comet with its head in front.
- **The glow is the silhouette** and has to reach the standard: the outermost cells' centres are
  15.5 out, so the solid glow runs to 15.7 (at 14.7 it drew 30 x 30). Every frame of both hands draws
  32 x 32; the opaque area is ~440-530, under the family's ~600-740 because the ring is narrow.
- The off hand has its own motes and turns the other way.

## Weapons that cast light

`Art/Gear/HeldGlow.cs`, driven by `GearItem.GlowColor`/`GlowIntensity`/`GlowFromRim`: a soft 2D
point light at the weapon's centre, on both held discs, displays and in flight.

- **THE NEUTRAL GLOBAL LIGHT.** URP's 2D renderer draws a lit sprite as its own colour only while
  the scene has NO 2D lights; once any exists, every lit sprite is its colour x the sum of the
  multiply lights (+ the additive ones) - a lone light turns the rest of the scene BLACK.
  `HeldGlow.EnsureNeutralGlobal` adds a white global light at intensity 1 first (static scene
  captured with and without it: pixel-identical). Anything else that ever adds a 2D light needs it.
- **ADDITIVE, not multiply**: a multiply light scales a sprite's own colours, and this game's
  floors, cloth and undersuit are dark - at 1.5 it barely registered. Additive 0.3 is soft; 0.5
  fogs the character.
- **From the rim** (`GlowFromRim`): a ring-shaped cookie, dark over the middle and brightest at the
  edge - a centred glow greyed Singularity's black core, and a black hole's core gives no light.
- The runtime assembly references `Unity.RenderPipelines.Universal.2D.Runtime` (where `Light2D`
  lives - not the main URP runtime assembly).

## Tria Prima: three Diamond swords that forge into one

`Art/Gear/DemoGear.TriaPrima.cs` (DemoGear is now `partial`). Three Diamond, power-0 swords, each a
principle in its own METAL: **Sulfur Ripsaw** (soul - a WIDE STEEL bar with a clipped point and a
groove, and round its rim a CONTINUOUS chain loop in YELLOW sulfur - up the left, over the clipped
tip past a nose sprocket, down the right - a small L-shaped cutter every third link, the MOTOR (an
engine: spark-plug boot, cooling fins, recoil-starter disc, muffler grille) fixed over the bar at
its base. What reads as a chainsaw is the closed loop of links; big fin-teeth on a thin chain read
as a serrated sword. The bar is wide so small cutters still stand past the Pacemaker's edge on the
fused sword. The chain ANIMATES round the loop (`SawChainPerLoop` is a whole number of cutter
spacings so the loop closes)), **Salt Pacemaker**
(body, MULTICOLOURED salt crystal - a Voronoi mosaic of white/pink/red/grey/black/blue facets - a
wide HOLLOW blade, a big quicksilver bead running the opening's rim, half in the hollow; the guard an
OUROBOROS in a figure 8 - a lemniscate of Gerono, head at the right end biting its tail, lit steel
not black (black on a dark background was only its outline), lobes tall enough to keep open eyes.
The mosaic is weighted to the COLOURS (pink/red/blue, white/grey/black as accents) and runs to the
edges with small facets: on the fused sword only the tip and thin side strips of it show, and a
white-weighted mosaic with a white rim left the fused blade colourless), **Mercury Reactor** (spirit, MIRROR silver armour plates, bands,
collar and core ring round a slim purple light blade, the REACTOR CORE at its base, left of centre;
it ANIMATES - soft two-row pulses climb the light and the core, bulb and collar node throb. A hard
one-row pulse read as one more coupling).

All three animated parts - bead, chain, Reactor - run on ONE clock: the bead's 36-frame flipbook
(`BeadFrames`), each with a whole number of cycles per circuit, so Tria Prima's frames loop clean.
Separatio's figures take them in that order (Sulfur, Salt, Mercury).

All three share ONE hilt at the same rows and pivot (black leather engraved with circles, a
dark-steel pommel with a violet crystal) and all three are the family's full height. **Tria Prima
NESTS them** (the user's own sketch). Front to back: the bead (over the
Reactor's edge so it stays visible); the Reactor, down the Pacemaker's opening, its point OVER the
Pacemaker's point and its plates and core OVER the Ripsaw's bar (the fused Reactor is exactly the
lone one); the Ripsaw's bar and chain; the Pacemaker outermost. In the GUARD rows the order is
its own: the Reactor's violet collar node, then the ouroboros OVER the Reactor's collar, then the
collar and the Pacemaker's block - and the Ripsaw's chain and cutters stop at the guard. Only the
Ripsaw is set DEEPER (`RipsawFusedTip` moves only where its point starts) - never shortened or
squashed. The MOTOR is on the lone Ripsaw ONLY (`RipsawAt(..., motor: false)` on the fused sword -
the user's call; the bar runs on under the Reactor without it, and the Reactor's core shows in
full). The rule the other way still holds: nothing may appear on the fusion from nowhere. Every
sword is ONE continuous field (arena per cell, menu per quarter cell); Tria Prima samples the SAME
fields front to back, so a part cannot drift from its fused self.

- A mirror reads by CONTRAST and is mostly LIGHT: a wide dark reflection band read as obsidian.
- Purple lit far toward white washes to lavender - the light ramp's lift is kept low.
- Don't name a DemoGear member `Core` - it shadows the `Core` namespace for the whole partial class.
- The bead is a flipbook (`GearItem.IdleFrames`) on Phantom's ticker (`PhantomHaze`, now taking a
  frame time; `GearItem.WeaponFlipbook` picks haze or idle frames). Still pictures rest it up the
  opening's right side (`BeadRestPhase`), where the fused sword shows it too.
- Static row arrays in the partial file are LAZY: C# does not order static initialisers across
  files, and eager ones could run before `SwordTipTaper`/`SwordBladeWidth` exist.

## Floor pit art (`PitArt`)

Sand and fire need POINT FILTERING and TILING — bilinear resolves grain to mush, and tiling at one
sprite per world unit keeps grain the same apparent size regardless of pit footprint. Sand is three
layers (shadowed bulk, drift noise, lit grain). Fire's disengaged state is a different material (a
cracked ash crust via thresholded noise contours) with embers glowing through the cracks, not just a
dimmed version of lit fire. Flames are several independently-timed tongues pivoted at their base,
not one animated sheet. Jacks (spikes) sit on a jittered lattice with cells skipped, drawn twice
(dark offset copy under steel) so they read as objects lying in the pit rather than a decal.

Sunken art (lip, walls, shadow) is SHARED by `FloorPit` regardless of kind — building it three times
would be three chances for one to read as a decal. Far wall (top-down camera's view into the hole)
carries the deepest shadow; near wall catches light.

Water is deep blue under a LOW-contrast shallows mottle (at 0.75 alpha the one-unit tile repeated
visibly and the basin read as a patterned rug) and shimmering point-sampled glints (`PitArt.Glints`,
short dashes - a lone texel reads as sand grain). Its recess is drawn at `WaterRecessDepth` (a
filled basin, not a hole). Ripples are a pooled set of `Spr.Ring`s - `ThinRing` is a hairline
built for 4-unit circles and vanishes at ripple size - shrunk near the rim so none spills over the
lip; a moving body throws a WAKE of them, the slide's visible readout.

## Tornado art (`Tornado`)

Procedural, from `Spr` soft shapes: 13 flattened ellipse bands (a grey `Glow` body + a faint white
`Ring` edge) narrow at the foot and flaring up, bodies tall enough to overlap (at 9 bands they
read as separate rings - a slinky). White wind streaks orbit each band, brighter on the near
(lower) half of the ellipse so the circling reads as going round. The body is mid-grey, not white:
a white haze over light stone floors has no edge. Base dust is pale grey - tan dust was the
floor's own colour and vanished. The top sways more than the foot. Funnel depth-sorts at its foot;
shadow and hitbox ring are ground (`Enemy - 1`), like the mire.
