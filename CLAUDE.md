# Coalescence — Unity project notes

Combat prototype for the arena game designed in `~/Development/code/project-convergence/the-seed/`.
Unity **6000.5.1f1**, 2D URP, Input System (new only — `activeInputHandler: 1`).

## Unity CLI workflow (confirmed working path)

The Pipeline package (`com.unity.pipeline` 0.5.0-exp.1) is installed, so `unity command` talks
to the running Editor. Verified facts about **CLI v1.0.0-beta.5**:

- The Editor must be **running and focused at least once** after a manifest change, or the
  package never resolves and `unity status` stays empty (`unity pipeline list` shows
  `Server Reachable = false`).
- `unity status` should list the Editor with a port (7800 here) and state `ready`.
- **Arguments are positional, not `name=value`.** `unity command capture_game_view source=screen`
  silently ignores the argument. Only `eval` reads cleanly because `code` is its first parameter.
- `eval` requires **statements**, not a bare expression: `return Foo();` works, `Foo()` does not.
- `eval_file` does **not** honour a `using` block — fully qualify every type.
- Prefer `FindAnyObjectByType` / `FindObjectsByType`; `FindFirstObjectByType` is deprecated in 6.5.

```bash
unity status                       # confirm the Editor is connected
unity command                      # list all 143 available commands

# Rebuild Arena.unity from scratch and add it to Build Settings
unity command eval 'return Convergence.EditorTools.ArenaSceneBuilder.BuildArenaScene();'

unity command recompile
unity command recompile_status     # SLEEP FIRST - see below - then poll until
                                   # {"status":"completed","failed":false}

unity command editor_play
unity command editor_stop
unity command console              # read Unity console output
```

Also in the Editor's **Convergence** menu: *Rebuild Arena Scene*, *Play Arena*.

### Two traps when iterating on ART from the CLI

Both fail by showing a plausible OLD picture rather than by erroring.

- **`recompile_status` answers about the LAST compile until the new one starts.** Polling it
  immediately after `recompile` returns `completed` from the previous run, so the loop exits at
  once and everything after it runs against stale assemblies. Sleep ~8s before polling.
- **A recompile during Play does NOT redraw what is already equipped.** The domain reload rebuilds
  `GearCatalog` and `PixelSprite._cache`, and `unity command eval` will happily report the NEW
  sprite sizes — while the `SpriteRenderer`s keep the old `Sprite` objects, because nothing called
  `Apply` again. Every screenshot looks like the edit did nothing. `editor_stop`, wait, then
  `editor_play`, and poll the renderer itself (`FindObjectsByType<SpriteRenderer>`, check
  `sprite.rect`) until it reports the size you expect before capturing.

The hub is a better place to judge a weapon than the arena: the character stands still, at rest,
with the weapon vertical beside them, which is the pose a reference image is usually in too.

## Art

Placeholders are procedural (`Core/Spr.cs`). Real art goes into the catalog at
`Assets/Resources/GameArt.asset` — every slot optional, blank falls back to the placeholder.
See **`ART.md`**. Rule: gameplay on the root at scale 1, visuals on a `visual` child.

Per-item art notes (each weapon, armour set and disc, hair, the rig's carry and gait) live in
**`ART-NOTES.md`** — read a piece's section there before changing its art.

**Cloth dyed by the cape** (`GearItem.DyedByBack`, `Art/Gear/ClothDye`): cloth authored in
`ClothDye.Undyed` is repainted at Apply in the worn Back piece's MEASURED colour - the
`ShowsSkin` mechanism, keyed off the cape instead of the skin. The rig keeps the colour as a plain
`Color` (a `Palette.Ramp` field would come back black after a domain reload); the armour stand
re-dyes when its Back changes. The Survivor set (tabard, skirt, and the pauldrons' metal inside
their silver trim), and the Herald's leg cloth. Far-side layers are drawn at `ClothDye.Far`, which
the dye also matches.

**Dyes** (`Art/Gear/Dye.cs`; design in progress - Forge tab, drops, chain write still unbuilt):
Diamond ARMOUR only, two channels per piece (`GearItem.DyeChannels`: [0] MAIN, [1] ACCENT),
declared per set with `DemoGear.Dyeable` at the end of each `AddX`. A channel is a RAMP plus the
exact SHADES it's drawn at (1, and the set's far factor) - matched on a palette group's BASE, never
inferred, because neutral greys are all proportional (Talon's jerkin is 0.72 of its steel). Dyeing
REBUILDS the layer from its own rows with the palette repainted (`DemoGear.Repaint`, keyed by
SPRITE via `_drawnFrom` since a minted instance's LayerSprites are copies) - never a texel match,
so derived menu art dyes identically. `DyeRecipe` works in OKLab: lightness structure from the
source ramp re-anchored at the dye's (clamped per `DyeMaterial`), hue from the dye, chroma from the
dye x a per-material profile. The SELF-DYE test (a set's own colour on its own ramp should look
like the authored piece) is what tuned Metal's chroma. Swatch ids (`DyeCatalog`) are permanent -
the item's datum names them. `GearDye.ReachReport()` lists channels that recolour nothing on a
piece (the Forge must only offer channels that reach); judge changes with
`EditorTools.DyeSheet` (Play mode, every swatch on a whole set). Not yet dyed: the Herald's sigil
variants, and the Hellspawn Cape (own palette, undeclared).

**Armour bearing the wearer's sigil** (`GearItem.BearsSigil`, the Herald pauldron): worn, one
layer shows the ATTUNED element's sigil (`Attunement`, as Prism's gem); its own sprite - every item
view that has no wearer - is all four combined. The rig repaints it through
`ICharacterRig.SyncAttunement`, called from `CharacterRigFactory.ApplyAttunement`, so anything that
changes the attunement must keep going through there.

**Marks that burn in the wearer's element - the Secret Fire** (`Art/Gear/SecretFire.cs`,
`GearItem.Kindled`; the Aether set and its greatsword): marks cut into a piece's art glow in the
ATTUNED element's colours and fade to black, every mark in the game on ONE beat.

- **The marks are texels of the piece's own sprite**, in three reserved grid letters (`@` core,
  `*` vein, `+` ember) painted in three reserved near-black colours (`SecretFire.Kindle`, called
  LAST on a palette - after any far-limb scaling, since they are matched to the byte). That is the
  unlit picture: what a card shows at the bottom of the beat, and what an item image would show.
  The overlay is DERIVED from whatever sprite is on screen (`SecretFire.Overlay`, cached per
  sprite and element), so the elbow/wrist/knee cuts, lopsided mirrors, the hood's enclosure, dyes
  and derived menu art all keep their marks for free - and a hit flash and Medusa's stone, which
  leave no reserved colour, put the fire out.
- **One clock**: brightness is a function of unscaled time only (`Tuning.SecretFire`), no
  per-instance phase. An element change dips every mark to black and swaps the colour in the dark
  (`Attunement.Previous`/`ChangedAt`), so nothing snaps and nothing drifts out of step.
- **Where it is drawn**: `KindledMarks` (a child renderer over any SpriteRenderer, LateUpdate
  order 1000, copying the source's order and nudged a THOUSANDTH toward the camera - the hub
  camera is tilted, and the glint's hundredth would move the picture up the screen by a texel) -
  on every gear layer of a rig wearing anything kindled (`PrimitiveCharacterRig.SyncKindled`, end
  of `Apply`), the armour stand's layers, the rack and armoury wall (`GearDisplay.ApplyEffects`),
  and weapons in flight (`ThrownBlade`, `WhirlingBlade`, `DiscVisual.Add`). On flat UI,
  `UI/KindledImage` over the card (gear picker, the rack close-up). A new surface that draws gear
  must add one too, or its marks stay black.
- Testing: `SecretFire.DevBrightness = 1f` (or 0) holds every mark lit (or dark) for a still;
  `BrightnessAt`/`ShownAt` answer for any time, so the swap can be checked to the frame.

## The terminal

`Chain/TxLog` is a SESSION-scoped list of every checkpoint written, shown by the terminal in the
hub (`Hub/TerminalStation` → `UI/TxScreen`). Never persisted, deliberately: it's a receipt roll,
not a ledger — the datum on disk is the record, and a saved copy here would be a second source of
truth that could disagree with it.

Records POST pending then CONFIRM separately even though the local store settles in the same
frame — a real CIP-68 store posts and hears back a block later, so this reconnects rather than
needing a rewrite. Local hashes are prefixed `local:`; the day a real store lands, every row still
saying that is a write that never left the machine.

## Two stores, deliberately

`IProfileStore` is the on-chain boundary: one CIP-68 reference NFT per CHARACTER, written at
exactly three checkpoints (run start, run end, permanent unlock).

`IAccountStore` is the PLAYER, keyed by wallet address — or `AccountProfile.LocalId` ("local")
when no wallet is connected, a normal state that must stay playable. It owns the hub room, because
a character is a save file you switch between and the room is where you switch between them.

`ConnectWallet` has three cases: an address with an existing record loads it; no record while
UNCONNECTED adopts the local one (a room arranged before connecting is kept); no record while
already on another wallet starts fresh (adopting there would clone one wallet's room onto another).

## The browser build and the chain connector

`Assets/Scripts/Chain/Web/` is the CIP-68 store and the ONLY part of the project that knows a
browser exists. `StoreFactory` picks it once at start-up; everything above it awaits
`IProfileStore` exactly as before, so the Editor and every native target keep the local JSON
stores and the `unity command eval` workflow untouched.

Unity's JS interop is one-way and synchronous: C# calls into JS but cannot be handed a promise
back, so every call carries a request id and the answer arrives later through `SendMessage`.
`WebChainBridge` keeps a `TaskCompletionSource` per id and converts that into the `async Task`
shape the interfaces already declare.

- **The receiver's NAME is the address** — `SendMessage` resolves by GameObject name
  (`"CoalescenceChainBridge"` on both the C# and jslib side). Renaming one side doesn't error;
  the reply never arrives and every request times out.
- `WebChainBridge._pending` is a **non-readonly, null-guarded** `Dictionary` — a reload empties it
  while everything around it survives.
- A request that never settles fails after 90s (the run-end checkpoint is awaited on the path back
  to the hub, so a lost response would otherwise strand the player).
- **An empty transaction hash is a FAILURE**, not a success.
- Both stores must agree about what a run DOES — `ChainProfileStore.CommitRunAsync` mutates the
  profile exactly as `LocalJsonProfileStore` does before serialising. If that rule changes in one
  it changes in both.
- **A failed checkpoint does not stop the game.** `FailRunOnCheckpointError` is false — the FAILED
  row in the terminal is the record instead. Turn it on once the connector is trusted.
- Transaction building is deliberately NOT in C# — Cardano needs CBOR/Plutus/coin-selection, the
  C# ecosystem is thin, and IL2CPP breaks a class of crypto libraries. `web/connector/` holds the
  TypeScript contract; Unity only ever passes strings.

### Who signs what

    the PLAYER's wallet    component art, and the assembled PFP. Signs TWICE: a message
                           proving the address, and the PFP mint.
    the SERVICE's wallet   progression (the three checkpoints) and room layout. No player
                           signature, ever.

Ordinary play never opens a wallet dialog — a player who can sign their own progression datum
can write whatever they like into it (kill counts, mastery, floors cleared are things the game
ASSERTS). If a checkpoint ever starts prompting, the split is broken —
`beginRun`/`commitRun`/`unlock`/`saveAccount` must never reach `signTx`.

`signData` ≠ `signTx`: proving an address is a signature over a MESSAGE (no fee, nothing
authorised). `ChainWallet.Proven` is kept separate from `Connected` for that reason — an unproven
wallet still plays but the service cannot write on its behalf.

The PFP is the one thing the player signs a TRANSACTION for, because it mints TO them from
components they already hold. `PfpRecipe` carries both the component keys and the composed look
(two characters can hold the same parts and wear them differently), plus the character's name and
deepest floor — the one field the SERVICE attests to.

**It is a TROPHY, not a live equip visualisation.** The datum updates only when the player asks;
it never updates itself. Nothing in the game may reach `TakeAsync` except a player pressing the
button: no auto-take on floor clear, no sync on equip, no refresh on load. `Revision` counts
distinct eras. `SaysTheSameAs` refuses a re-take that changes nothing (compares only what the
portrait DISPLAYS, ignoring Revision/TakenAtUtc).

**The player chooses WHEN; the service vouches for WHAT** — two signatures on an update, which is
why the reference token can be neither locked nor handed to either party alone:

    locked (immutable)          a bad first take is permanent
    player spends it alone      writes any MaxFloor they like
    service spends it alone     "player chooses when" stops being true
    BOTH                        player picks the moment, service attests the depth

`CurrentAsync` throws rather than returning null on a failed read — null means "no portrait" and
sends the caller down the (irreversible) MINT path.

Ownership is gated in the UI (`WalletInventory.Owns`) and ENFORCED at the connector — the cached
list can go stale the moment a token is sold in another tab. A failed inventory refresh KEEPS the
previous list rather than emptying it, so it doesn't grey out everything the player actually owns.

Wallet art is the one place textures aren't ours — `WalletShowcaseSource` fetches lazily,
downscales to `MaxEdge`, caps residency at `MaxCachedTextures`. A placement past the cap keeps its
spot and draws the placeholder.

**Landscape is enforced in the WebGL TEMPLATE, not only PlayerSettings.**
`Assets/WebGLTemplates/Coalescence/index.html` carries `viewport-fit=cover` and pads the canvas by
`env(safe-area-inset-*)` — `Screen.safeArea` is unreliable on WebGL, so the inset is applied to the
canvas rather than compensated for inside Unity.

## Floor pits: fire, sand, spikes and water on the column grid

`Hazards/FloorPit.cs` + `PitGrid` + `PitArt`. (`GameArt.LavaTile` is a dead slot, left blank.)

**Pits occupy the CELLS of the column lattice; columns occupy its POINTS.** A pit measured in
cells is bounded by four column points at its corners, so non-overlap is structural rather than a
clearance check. Cell centres are the column lattice offset by half a spacing, so pit and column
grids can't drift relative to each other.

Pits are placed BEFORE any column — `BuildColumnGrid` filters swallowed lattice points out ONCE, so
they're never in the pool. The exclusion is measured from the column's RIM, not its centre (a
large column overhangs its own body otherwise).

**One size tier per floor**, laid into that tier's own regions: one Large (centre), two Medium (one
per half), or four Small (one per quadrant), weighted 1/2/3 toward smaller layouts (more, smaller
pits leave more routes through the room). Below the first avatar boss (floor 25) every pit on a
floor shares one kind (fire/sand/spike/water); at and above it each pit rolls its own kind independently.
`PitKind` is append-only.

`PitGrid.TryRect` filters candidate cells by player clearance BEFORE choosing, measured to the
rect (the player spawns at the origin, so reject-after-placing killed the Large layout every time).

**What each does:**

    FIRE    cycles engaged/ashed on one clock. Damage rides the same value the art does. Each pit
            rolls its OWN cycle length (4.5-8.5s) and burn share (38-62%) as well as its start,
            so a floor's fires drift in and out of step rather than looping one pattern.
            Damage is scaled by `FloorDifficulty.Damage` (`FloorPit.ScaleDamage`, set at
            placement), like spikes, tornadoes and the spire's lines - every hazard that hurts
            hurts the way the floor's enemies do.
    SAND    always on. A pure movement cost — slows and does nothing else, deliberately: the
            crowd is a positioning problem, not a race, and sand argues with that without
            breaking it by making the ground you'd retreat across expensive.
    SPIKE   always on, damages only while moving. Cost is the seconds spent stationary with a
            pack arriving, not the damage. Measured DISPLACEMENT (not the stick) so being
            knocked in and slid across still hurts. Time-based while moving (not per distance)
            so it COMPOSES with sand — slowed means dragged longer. The tick resets rather than
            freezes when the player stops.
    WATER   always on. Takes GRIP, not speed: `FloorPit.Grip` caps how much of the gap to the
            wanted velocity one physics step may close (`WaterGrip` 0.05), so walking, turning and
            stopping all drift. The body's damping is RE-SOLVED with it
            (`FloorPits.DampingFor`) so top speed stays the dry one - capping the blend alone left
            damping 6 bleeding speed and the player WADED at ~30%; scaling damping by grip made
            enemies ~30% FASTER on water. Measured: top speed 3.03 dry / 2.93 water, ~1.1 units
            more slide on release. Surefooted (`NoSlide`) ignores the slide until the exchange
            rebuild cuts it. Enemies slide too (`Drive` and the flinch damping), so a Heavy
            finisher skates a body across. A wake of ripples at the feet is the readout.
            And it SOAKS the player (`FloorPit.Soaks`, asked through `FloorPits.SoaksAt`):
            `WaterSoakDamageTaken` (+25%) damage taken from EVERYTHING, held while standing in it
            and for `WaterSoakSeconds` (3) after - OUTSIDE the mitigation floor, like being
            exposed after a leap. A dash wades it (ticked before the dash's early return); a
            leap (`Airborne`) does not. Readout: pale rings shed at the feet
            (`PlayerController.Soaked`). Enemies are never soaked by ground.

**Enemies respect pits too** (they were once player-only): routes price each pit
(`FloorPit.RouteCost` - fire by the heat it will have 1.5s from now, so a pack crosses ash and not
flame), sand slows them (`FloorPits.EnemySpeedMultiplierAt`), and fire/spikes damage a body knocked
or caught in one as a FRACTION OF ITS MAX HP (`Tuning.Steering.*EnemyHpPerSecond` - the player's flat
numbers are sized for a player). The enemy ASKS the ground (`EnemyController.TickPits`); pits still
only ever damage the player themselves. Kinds that never walk are exempt (a turret spawned on a pit
can't step out).

The speed multiplier is ASKED FOR via `FloorPits.SpeedMultiplierAt`, never written onto the
player — one owner, same rule as `Controls.Screen`. Overlapping pits take the SLOWEST rather than
multiplying (a multiplier is a property of ground, which can't be layered).

**Art** (`PitArt`, kept out of `Spr`): POINT FILTERING and TILING at one sprite per world unit, so
grain stays the same size at any footprint. Sunken art (lip, walls, shadow) is SHARED by every kind.
Per-kind art notes are in `ART-NOTES.md`.

`SortingOrders.PitBase` is ABOVE `GroundDecal` — a pit REPLACES the floor rather than being painted
on it, but still below `Enemy`.

Every cached field on a pit is `[SerializeField]` and non-readonly (see Domain reload traps below) —
`FloorPit.Footprint` in particular is what every damage/slow check measures against, and losing it
silently moves the hazard to the world origin while the art stays put.

## Tornadoes: weather while the wave lives

`Hazards/TornadoStorm.cs` (the floor's spawner) + `Hazards/Tornado.cs` (one funnel),
`Tuning.Tornado`. Rolled by `HazardBuilder` last (it places nothing at build time) from
`FromFloor`; never a boss floor (`stormAllowed`, as `spireAllowed`). **As often as any one pit
kind**: `HazardBuilder.PitKindChance(floor)` derives that from the pit rules themselves (17.5% of
floors below floor 25, ~37% from 25 where each pit rolls its own kind) - Air's hazard turns up as
much as Fire's, Earth's and Water's, which the element trap boons rely on.

- **Forms only while the fight is on.** Waits for the first enemy, then forms a funnel every
  `FormInterval` up to `MaxAlive` (1, 2 from floor 20, 3 from 45). The floor clear calls
  `TornadoStorm.CalmAll()` beside the spire's Sink: no more funnels, and every live one starts
  dissipating, harmless from that frame. CalmAll FINDS storms rather than keeping a static list.
- **Three phases, only the middle hurts**: FORMING (1.3s, still - dust and the ring are the
  telegraph), SPINNING (`SpinSeconds` 8, wandering), DISSIPATING (0.9s).
- **The ring on the ground is the hitbox** (`Radius` at the foot, not the funnel's drawn width).
  Ticks the PLAYER only, `DamagePerSecond x FloorDifficulty.Damage` like the spire's lines, reset
  on stepping out like the pits.
- **Open floor only** (`Tornado.Open`, also the formation test): standing room for the ring, no
  pit footprint within it, nothing `NavField.Solid` inside it. Wanders on two incommensurate
  sines and turns to the nearest open heading at an obstacle. Slower than every enemy; it never
  chases. Forms at least `FormPlayerClearance` from the player.
- Testing: `Hazards.TornadoStorm.DevForceNext = true` before a floor builds (one-shot).

## Room shapes: interior walls

`Hazards/RoomShape.cs` (catalogue + rules) + `Hazards/RoomWalls.cs` (building), `Tuning.Rooms`.
Ten shapes: seven of walls (plaza, divide, crossroads, hall, chambers, baffles, bastion) and three
of chasms (maw, bridges, ledges).
Ordinary combat and Rift floors from floor 4, ~60% shaped (Open is still the single most common);
never a boss or puzzle floor (`BuildFight` passes null there). Seeded by `FloorPlanner.ShapeSeed`
(System.Random - a preview can name the shape without moving UnityEngine.Random); never the same
shape twice running, decided by re-drawing the previous floor from ITS seed, so no memory is kept.

- **Shapes are pictures in source**: 24x14, one cell per unit, north at the top. `.` floor, `#`
  wall, `%` CRACKED wall, `~` CHASM. Each shape and its three mirror images is VALIDATED at first use (door and
  arrival zones open, every floor cell reachable without breaking anything, nothing narrower than
  two cells); a failure is dropped with a warning naming it. Add a shape by drawing it.
- **`Arena` is the authority** - `OnFloor`/`RectOnFloor`/`NearestFloor`/`WallBetween` over the
  wall mask, `Clamp` is still only the outer rectangle. Anything that PLACES something must ask
  (spawns, pits, columns, the spire's whole ring, force fields, Rift tears and guards, Rift Boxes,
  fire pools, vortices, cluster shards, a deflected shell, the blade blink).
- **Force fields run STRAIGHT, between NEIGHBOURS** (`HazardBuilder`, `StandsBetween`): only two
  columns on the same lattice row or column with no third column between them - so a field never
  runs through a pillar or doubles back over another on the same line, and a column can carry one
  field in each of its four directions (an L, a T, a cross). Never diagonal (everything else in a
  room is horizontal or vertical, and a diagonal read as an arbitrary beam), never through a wall.
  NOTHING IS OWED: every eligible pair rolls `ForceFieldChancePerEligiblePair` (0.6) on its own,
  shuffled first so the floor's cap (`MaxForceFieldsPerFloor`, 4 - raised from 3 for junctions)
  isn't always spent on the first columns placed. At 3, measured over 90 floor-20 rooms: ~2.4
  fields a room, a pillar carrying two on ~43%; 4 simulates to ~2.8 and ~60%.
- **Walls stop SHOTS; columns don't.** `HazardQuery.CheckCrossing` returns `Blocked` for a wall: a
  bolt or arrow dies, a disc drops home, the thrown blade rebounds as off the outer wall, and a
  weapon RETURNING to the hand ignores it. Mortar shells lob over. Sight (`Query`) and routing
  (NavField asks physics) needed no code. Targeting skips enemies behind a wall and lets a walled
  lock go for one in the open.
- **The mask is published in `Build`, not only `OnEnable`** - AddComponent runs OnEnable before the
  mask exists (the first build shipped a room the placement couldn't see). It is ALSO serialized on
  the component and re-published from OnEnable, for the domain reload. `Apply` switches the old
  walls off on the same frame (Destroy is deferred and the next floor places before it lands).
- **Cracked segments** are a connected run of `%` with ONE collider over its bounds (melee adds a
  candidate per collider) and a Health of `CrackedHpPerCell` x cells; a Column's rules otherwise.
  Breaking one opens its cells in the mask and re-routes the pack. Drawn lighter and warmer than
  the wall, which is the outer walls' dark stone - mid-value stone read as a raised rug.
- One block per cell, pivoted at its foot and depth-sorted like a column, with a front face where
  the cell to the south is open and a contact shadow on the decal layer.
- **Chasms** (`~`, `Tuning.Chasm`; shapes maw, bridges, ledges): void nothing STANDS on, but
  shots and sight cross it (no collider - it lives only in `Arena`'s mask, which NavField asks to
  close the cells near it). Not standing room for any placement.
  - **The PLAYER's walk is held at the edge** (`PlayerController.HoldChasmEdge` trims the part of
    the velocity heading over the lip, so they slide along it). A DASH or LEAP is not held - a
    two-deep rift is crossable - and where it ENDS is judged (`SettleOverChasm`): within
    `Forgiveness` of the lip (measured to the LIP, not the set-down point) it's set down there;
    past that it FALLS - `PlayerFallFraction` of max HP through mitigation, back to the last ground
    stood on. Enemies never knock the player back, so a fall is always the player's own movement.
  - **An ENEMY goes in by being THROWN** (`EnemyController.ThrowIfChasm`, on `Health.Damaged`): a
    hit that DISPLACES, aimed at a chasm within `knockback x ThrowReachPerKnockback` (clamped
    0.6-2.0) along its own direction, glides it over the lip and drops it. Physics alone can't:
    a flinched body's velocity is damped hard each step, so a Heavy finisher only carries it
    0.2-0.6 units - measured. Anything that already displaces qualifies (Heavy finishers, Air's
    vortex pull, Undertow toward a player across a hole); Finisher Knockback lengthens the reach;
    a wall on the line stops it; Anchored elites are moved by nothing, so never fall.
  - The fall is an ordinary death (`Health.Kill`): wave credit, the spire, a Rift Box at the lip,
    and the player's on-kill effects credited at the lip. A Bomb that falls doesn't explode.
    A body that DRIFTS over the void without a recent shove (`Health.ShovedAt`) is put back.
- Testing: `Hazards.RoomShape.DevForceNext = "hall"` before a floor builds (one-shot);
  `EditorTools.RoomSheet.Show(name, variant)` swaps the live walls only, `Capture(path)` renders the
  arena from above.

## Spires: a capture point paying one boon

`Hazards/Spire.cs` (+ `SpireBoons`), placed by `HazardBuilder` after pits and before columns,
`Tuning.Spire`. ~1 eligible floor in 5 from floor 2, never a boss or puzzle floor, never more than
one. Stand in the ring `CaptureSeconds` (7s); leaving DRAINS progress at the rate it fills rather
than resetting it. Enemies never pause it - they make standing there expensive instead. A spire MAY
stand in a pit: its ring and lines draw above the pit band (`PitBase+5/+6`), and the pit's hazard is
one more price of the capture. Columns still give way to the ring.

- **ONE boon per spire, and its COLOUR is the boon**: the core circle at the shaft's middle and the
  grooves running up and down from it (which fill as the capture climbs). The name is flashed the
  first time the player steps in, so colour is never the only channel. Heal/Repair land once at
  capture (weighted toward whichever would help); Might/Haste/Swiftness/Precision/Aegis last the
  FLOOR through `RunModifiers.SetFloorBoon`, folded into `Modifiers.Current` so every existing
  reader gets it - dropped at the top of `NextFloor`, never on the ledger strip.
- **Named for the seven planetary metals** (`SpireBoons.NameOf`; the enum keeps what the boon
  does): Copper = Heal (verdigris green), Lead = Repair (steel), Iron = Might (red), Quicksilver =
  Haste (cinnabar rose), Silver = Swiftness (cyan), Gold = Precision (gold), Tin = Aegis (violet).
- **The floor-long boons are RUN-LAYER POINTS** (`Tuning.Spire.*Points`), added the way the ledger
  adds them and bent through the same Vessel - a spire tops a run up, never past what the run may
  hold, and at mastery level 0 it is bent like any boon (Iron's 30 points give ~25). Haste and
  Swiftness were multipliers on top of the ledger, compounding with it, until 2026-10-05. Gold's
  crit joins the one crit pool; Tin is mitigation, inside the one floor.
- **A FLOOR EVENT, not a fixture.** Rolled and placed with the room (so columns give way to the
  ring) but built fully UNDER the floor; it RISES (`Rise`) once `RiseAtDefeatedFraction` (10%, at
  least one kill) of the floor's wave is dead - measured in wave COST killed (`_spireKilled` against
  `_spirePool`, set when the wave is queued in `BuildFight`), so five elites aren't one kill from it. Nothing about it is live (capture, lines, bubble,
  collider) until it is fully up. The boon is named as it rises.
- **It SINKS INTO THE FLOOR at floor clear** (`Sink`, taken or not; an untaken one goes dark first
  and can't be captured - or a Mending spire would be a free heal every floor it appears). The
  shaft slides down through a range-limited `SpriteMask` cut at its foot, so it goes INTO the
  ground rather than shrinking. The rise is the same animation run backwards (`MoveShaft`). Scaled time: the reward screens pause the clear, so it plays when
  the player is back in the room. The core GLOW is left unmasked and faded instead - a soft sprite
  under a mask is cut to a hard-edged block.
- **Challenges** are separate depth-scaled rolls, both ending at capture: rotating DAMAGE LINES
  (white-hot on red whatever the boon colour, so they never read as part of a green heal) and a
  BUBBLE - a Blue field bent round the ring. `HazardQuery` nulls any line/projectile crossing its
  wall, and enemy melee/bomb/flame check `Spire.BubbleSeparates` too. Mortar shells still land
  inside: lobbed over the wall, they are the answer to a player hiding in there.
- The obelisk is solid and blocks sight like a column (no special case in `HazardQuery`).
- Polled by `GameBootstrap.TickSpire` (`TryAnnounce`/`TryClaim`), never a callback.
- Testing: `Spire.DevForceNext = true` (plus optional `DevForceBoon`/`DevForceLines`/
  `DevForceBubble`), one-shot, before the next floor builds.

## Sorting

Everything in `Core/SortingOrders.cs` is a FIXED layer except the **depth band**
(`DepthBase` and up), used by hub and combat. Anything standing on a floor sorts by its y through
`Core/DepthSorted.cs`.

- `DepthStride` must exceed the rig's 44 layers plus the bow's 3 vials, or two characters at adjacent depths interleave
  limbs into each other.
- Anything at or above `StatusOverlay` is an OVERLAY and must never be re-based — `DepthSorted`
  filters those out itself (a halo re-based onto a depth of 11300 wraps past Unity's sorting-order
  short and disappears behind the floor instead of drawing slightly wrong).

The player carries `DepthSorted.Bias` so it wins close calls — a crowd closing from below covers
you exactly when you most need to see yourself.

## Equivalent exchange

`Assets/Scripts/Exchange/` — the paired boon/cost row shown between floors.

- `ExchangeCatalog` — 35 boons, 36 costs, copy AND effect declared together (drift-proofing).
  `Apply == null && OnTaken == null` means declared-but-inert (waiting on an event layer).
- `RunModifiers` — the ledger. Run-scoped, never persisted, cleared at run start.
- `PendingOffer` — one-shot effects reshaping the NEXT offer (Prima Materia, Caput Mortuum,
  Indenture, Debt). Kept OUT of `Mods` because they're events, not accumulated stats.
- `ExchangeOffers` — `boonWeight = clamp(costWeight + bonus, 1, 3)`, bonus drifting 1→0 as floors
  progress (the naive `boon = cost + 1` orphans weight-1 boons and weight-3 costs).
- `ExchangeGlyphs` — family silhouette + mark; a family must be a CLOSED shape or the mark has
  nowhere to sit.
- `RunEffects` — the conditional half needing a clock/counter/memory. One component, its public
  hooks are the complete list of moments a run can react to.
- `UI/LedgerStrip` — carried boons/costs as icons; repeats collapse to a count badge.

**Not all of it works (audited 2026-10-05; the exchange is being rebuilt).** Seven entries write a
`Mods` field nothing reads (Cleaving Habit, Attunement, Rich Vein, Curator, Leaky Vessel, Stubborn
Ore, Silent Chain); Twin Spark's repeat never fires, because every release empties the meter the
repeat needs, so it only scales the release to 60%; Second Wind, Ghostwalk, Mirror Nerve and Dead
Weight are offered a second time for a stack that changes nothing.

The entries that do work: the last ten needed their own small systems rather than the shared event
layer (`Combat/Corpse` for Dead Weight, `StatusEffects.ApplyBleed` for Deep Cut, `ElementalResource`
refund/release-scale hooks, `PlayerController.Slots` as a List for Fourth Sigil/Extra Sigil, a
reused floor-reward roll for Transmuter's Eye so the preview never lies). Scrying Glass shows the
next floor's kind and roster from the SAME calls that build it (`FloorPlanner.Peek`, `ComposeWave`).

`Health` gained three plain delegates (`ModifyIncoming`, `ModifyHeal`, `SurviveLethal`) so it stays
ignorant of the exchange system. `SurviveLethal` fires BEFORE the subtraction — reviving after
would fire `Died` and everything behind it.

## The sigil door

`Hub/SigilDoor.cs` + `Hub/WallButton.cs`, on the NORTH wall (the one wall with a drawn face — the
gallery band). Two stone slabs, SHUT, parting only when the player uses them — that's what makes
the carved marks read as carved rather than floating. Each sigil is cut ACROSS the seam and each
half is a child of its own door leaf (`Half()` slices the glyph with a half-width `Sprite.Create`,
pivoted on the cut edge), so shut it's one whole mark and opening tears it in two.

Marks are CUT (a dark groove + lit offset wall), not laid on — a flat glyph on the surface floats.
Light through the gap is BLINDING WHITE (the sketch's "white opening"), not the element's colour —
tinted, it reads as a lit coloured panel rather than light. The selected mark stays lit whether
doors are open or shut, since it's the only thing carrying the element once the light is white.
Slabs are deliberately NOT tinted by element — even 10% toward the tint reads as muddy, not stone.

Sorting is based off `GroundDecal` (not `FloorDetail`), fixing a z-fight with floor decals. Doors
never open onto anything walkable — parting is light escaping, not a passage; every wall stays
solid.

`WallButton`'s four rings interlock at `r > 0.707d` (opposite compass points at distance `d`); the
mark inside (not the ring) shows which element is selected, since dimming the ring would read as
"three switched off." Priority 10 on both door and button, placed 2.10 apart against the door's own
1.9 prompt radius so neither steals the other's focus.

## The loft

`HubRoom.BuildLoft` — deliberately the SAME-PLANE cheat the gallery band already is, no real
elevation. Occupies the corner the sigil-door move freed up (north = gallery, east = solid since
the door move), needing only a skirt/shadow/blocker on the open west and south edges. One gap in
the south riser (`Tuning.Hub.LoftStairGapWidth`) with four decorative fading bars standing in for
stairs.

The mastery table sits `LoftBounds()`-relative. The couch stays on the ground floor until the
loft has real elevation (a per-actor layer, stair triggers, its own sort order - none exist yet).

## The armoury: weapon rack and armour stand

`Hub/WeaponRack.cs` / `Hub/ArmourStand.cs`, sharing `Hub/GearDisplay.cs` and
`UI/GearDisplayScreen.cs`. West wall. The rack holds ONE weapon; the stand wears a full set.

**PURELY COSMETIC** — neither reads the loadout, grants anything, or wears down (a display that
mirrors the loadout is a status bar).

**The stand is a STRIPPED RIG**, not a picture of one — its pivot tree is
`PrimitiveCharacterRig`'s own at the same texel positions, so gear hangs on the mannequin exactly
where it hangs on the character. The two `PivotFor` switches must stay identical or a layer lands
on the wrong joint. It's deliberately NOT the real rig (no walk cycle, no face) — six wooden blocks
at the character's own texel sizes, plus a blank oval head (helms are authored relative to the neck
joint and need something to cap). The joint tree hangs off one `figure` child scaled by
`ArenaVisualScale` and pinned at the feet, matching the hub avatar; plinth, shadow and collider
stay at 1.

**`SetBody` vs `SetBodyPixel`**: `Size` applied straight to `localScale` is
only correct for a `Spr` sprite (always 1×1 world units); gear art is PPU-matched and already its
own size, so doing that shrinks it ~3x. `GearDisplay.ScaleFor` (`Size / native`) is what both
fixtures must use.

**A weapon's pivot is its GRIP, not its middle** — pieces are placed by geometric CENTRE on the
peg. Only a BLADE flips (keyed off `WeaponClass.Greatsword`); applying the flip slot-wide hangs a
bow upside down.

The rack shows ONE weapon (not three — a case of three is a shop display, and there's no
non-arbitrary answer to which to evict when full) and ONE layer per weapon; the mannequin shows
EVERY layer (a set, not an object). A disc shows BOTH halves of the pair, overlapping (not side by
side — two rings in a row reads as a barbell).

`RoomLayout.Display` is additive, keyed `"weaponrack:0"` / `"armourstand:Torso"`, storing only an
item id (cosmetic displays have no durability/equipped state). Choosing again takes a piece OFF
(no slot picker — an item already knows its slot). **A full rack REFUSES rather than evicting.**

`RefreshArmoury` is explicit, NOT called from `RepaintLive` — the armoury has nothing to do with
what the character is wearing, so an equip must leave it alone.

## The armoury room

`Hub/ArmouryRoom.cs` + `Hub/Doorway.cs` — a separate hall holding every authored weapon DESIGN on
one wall, reached through a black doorway on the hub's north wall (on the loft, where the couch used
to stand; `Tuning.Hub.ArmouryDoorFromEast`). The rack stays in the hub as the one FAVOURITE; a
held bay's `[ E ]` hangs that weapon on it.

**It is a separate room because of the ZOOM.** The hub gives a world unit ~150 screen px at 1080p
and menu art is 300 texels/unit, so the room is built far along X (`Tuning.Armoury.OriginX`, X not Y
so depth sorting is untouched) and `GameBootstrap.UpdateHubCamera` swaps to its own camera while
`HubRoom.InArmoury`: half-height `Tuning.Armoury.ViewHalfHeight` (a WHOLE number of screen px per
menu texel — 1.8 at 1080p and 2160p, 2.4 at 1440p), strict top-down (the hub's tilt squashes sprite
height by cos 32°), following the player along the wall. Wall pieces are drawn at fit 1 ALWAYS — a
fit-scaled piece is off the texel grid the whole room exists for.

**Owned = exactly what the gear picker offers** (`Art/Gear/GearOwnership.Owned`, which `GearPicker`
now reads). Today that is every authored piece, so the wall is fully lit; the day equippable items
are imported from the wallet, that function narrows and the wall follows. Unowned bays show a dark
`PixelSprite.Silhouette`. `GearOwnership.DevUnowned` is a session-only switch to see silhouettes
now. Minted instances (non-empty `StackKey`) get no bay — no art, no design.

The room's interactables join `HubRoom._points`, so one prompt card and one focus rule serve both
rooms.

**The couch is in the crate** (`RoomLayout.CouchOut`, false by default and for old saves). In edit
mode, `[ R ]` at the crate takes it out straight into the player's hands; `[ R ]` on the couch, or
`[ Q ]` while carrying it, packs it away. Cancelling a carry fresh from the crate returns it to the
crate, since it has no spot to go back to.

## The room editor

The crate opens edit mode via `[ Q ]`/Alt (not `[ E ]`, which the crate already uses for opening
the collection). In edit mode `[ E ]` on any other fixture picks it up.

**Movable**: circle, couch, terminal, mastery table, crate, photo booth. **Never movable**: the
sigil door and its button — structurally absent from `_movables`, not merely gated.

A fixture's carry flow (`_carryFixtureKey` + `MovableFixture`) deliberately mirrors the trophy carry
system's shape (follow avatar, tint green/red, commit/cancel) without sharing its code — a trophy is
backed by a showcase key, a fixture is a live component with real behaviour.

**A fixture's `HubInteractable` anchors must move WITH it on commit**, and the delta must be
measured against the ORIGIN captured at pickup, not the fixture's live position at commit time —
by the normal flow the transform has already been overwritten to the drop point by the time the
accept branch runs, so measuring against it collapses "total distance carried" to only the last
frame's movement.

Positions are additive on `RoomLayout` (`FurniturePlacement` keyed by fixed fixture name), read
through `DefaultOr` so old saves load with built-in defaults.

## The photo booth

`Hub/PhotoBooth.cs` — independent of the couch and roster size. Not per-character and not on the
couch, because the couch shows characters OTHER than the one being played
(`p.ProfileId != _profile.ProfileId`) — nowhere to put whoever is actually standing in the room.

Holds no chain state — `CachedLine`/`Actionable` are a plain string/bool, refreshed explicitly via
`HubRoom.RefreshBooth()` rather than an event subscription (same domain-reload discipline as
everywhere else). `HubInteractable.Describe` is synchronous, so a live `await` would stall the
prompt panel — the booth caches the last chain-read answer.

**Placement must check door PRIORITY, not just distance** — a fixture inside a Priority-10 door's
catchment loses focus to the door regardless of how close the fixture itself is; `IsLegal`'s
clearance rule has no opinion about priority.

A canopy faked with a proximity sorting flip was built and removed: it read as a slab dropping onto
the player. Don't fake elevation the room doesn't have.

## The hub camera tilts

The hub camera pitches down `Tuning.Hub.CameraTiltDegrees` (32°) around local X; every other room
stays strict top-down. `HideHub()` resets rotation to identity (`TrackCamera` only ever writes
`.position`).

**On its own it does NOT buy an isometric look** — for an orthographic camera over flat z=0
geometry, tilt is exactly a vertical scale (`screen_y = (world_y - Cy)*cos(theta) + constant`), not
approximately. It cannot reveal a surface or create occlusion. It's a COMPANION effect: it costs
nothing (only the camera's transform moves — physics, `HubInteractable`, `DepthSorted` all stay in
flat world-XY), and it's what a compressed floor plane should accompany once wall faces are drawn.

Framing centres on what is DRAWN (the picture runs to the top of the gallery band), not the
walkable floor.

**The hub avatar is drawn at the arena's size** (`Tuning.Player.ArenaVisualScale`, 4/3) at the
same camera zoom, so the character is one size everywhere (zooming the CAMERA instead grew the
furniture too and was reverted). Inside the
armoury the avatar drops back to 1.0 — that hall's zoom puts a menu texel on whole screen pixels,
and 4/3 would land the body on 2.67 and shimmer. No billboarding — everything flat squashes by the same `cos(theta)`, which at 32°
reads as a shorter version of the same chunky pixel-art shape rather than distortion.

## Weapon parts that are not in the sprite

Lumen's live wire, Saint's halo, Prism's gem glow and Rai's arcs are how those weapons READ, so
they are present wherever the weapon is drawn: as components in the world
(`CharacterRigFactory` for the rig, `GearDisplay.ApplyEffects` for the rack and armoury wall) and
on flat UI through `UI/WeaponExtras`, from the SAME derived numbers, on UNSCALED time. A new
weapon with such a part must go in both places. Prism's lit gem goes through
`Art/Gear/Attunement` - see `ART-NOTES.md`.

## Weapon classes

`Art/Gear/WeaponClass` — how a weapon is FOUGHT WITH, separate from look or rolled stats. Within a
class, two weapons must differ by SILHOUETTE, not only ramp — colour is the half that dies in a
screenshot, a dark arena, or for a colourblind player.

**A tier is a RARITY, not a palette** — this holds at every tier. Reaching for `Palette.Tier` as an
item's own ramp is what makes "gold tier" mean "gold coloured"; new pieces name a MATERIAL instead.
Black diamond grants bespoke art per item plus a locked finisher, never a shared look.

A class owns three things: how a basic resolves, which finishers it may roll
(`MovesetLibrary.RandomExcluding(held, class)`), and the grip. Damage, tier, durability and
transmog stay per ITEM.

**Locked per run** — `PlayerController.LockWeaponClass` runs once in `BuildPlayer`; a mid-run
weapon swap that silently rewrote the chain/finisher pool/animation set would be a build reset three
floors in.

**Discs** are the hybrid: heavy melee inside reach, faster ricocheting throws outside it, switched
automatically by `ThrowsInsteadOfSwinging`. Neither half is best at what it does (melee ~69% of a
greatsword's DPS, throw capped below whatever the future ranged class lands on — see
`Tuning.Disc.MeleeDamageMul`/`ThrowDamageMul`). Throw range is `ThrowRangeMul` × melee reach (not a
fixed distance, so reach growth keeps the hybrid band alive).

The hybrid band reads off the target's rim (see "Reading a swing" below): SOLID inside melee reach
(it swings), DASHED in the throw band (it throws), brighter the more the throw deals at that range.

`PlayerController.Dash` is the horizontal-displacement primitive — three things had to be right for
the declared distance to match measured: suspend `linearDamping` for the duration, apply velocity
BEFORE decrementing the clock, and compare the clock against an epsilon (not zero).

`ICharacterRig.SetWeaponVisible` is REFERENCE COUNTED — as a flag it broke the moment two things hid
the weapon at once (a disc volley throws five simultaneously).

### How each element builds, per weapon

A thrown weapon attacks safely/constantly/without whiffing, which quietly removed the tension from
three of four elements. Each now keys off a cost the disc still charges:

| | Builds on | Demands |
|---|---|---|
| Fire | melee hits only | come **in** |
| Earth | stillness | stand **still** |
| Air | movement; bleeds while stationary | keep **moving** |
| Water | per hit, doubled on SOAKED | arrange the **crowd**, or spend to soak |

`WeaponClasses.HitMeterScale` (0.18) exists because discs land 3-5x as often as a greatsword —
unscaled, water's meter filled in under a second. Water's soak bonus lives on WATER, not any
weapon, so it applies to every class.

## Combat feel rules

- **Basics do not move enemies; HEAVY finishers do.** `Health.Immovable` overridden by
  `DamageInfo.Displaces`, set from the finisher's weight class (see Finisher weight classes below).
  Elemental releases currently do NOT displace.
- **Enemies never knock the player back** (`Tuning.Enemy.AttackKnockback` is 0).
- **No player/enemy collision** for basics and elites; enemy-to-enemy collision stays on so a pack
  spreads instead of stacking. Per-enemy in `EnemyFactory` (`bool solid`).

BOSSES will want the last two reversed — solid, with real attack knockback — decided per-enemy.

## Domain reload traps (read this before adding a field to a live MonoBehaviour)

A script edit during Play mode triggers a domain reload. Unity restores plain private fields but
**cannot restore three shapes**, and this project has been bitten by all three more than once:

- **Interface-typed fields** (`IProfileStore`, `ICharacterRig`) come back null while GameObjects
  around them survive intact — nothing *looks* broken, but any code depending on the interface
  silently stops working. Fix: a lazy property over a stateless implementation, or keep a
  `[SerializeField] MonoBehaviour` reference beside the interface and re-derive it on first use.
- **Dictionary fields** (`PrimitiveCharacterRig._layers`, `WebChainBridge._pending`,
  `TouchControls._buttons`) come back EMPTY while every referenced object survives. The rig keeps
  drawing perfectly until the first `Apply()` throws "key not present" — every subsequent equip then
  silently does nothing. Fix: re-attach by NAME to existing renderers (never rebuild — that welds a
  second set on top) whenever the dictionary might be empty; e.g. `PrimitiveCharacterRig.Apply`
  calls `EnsureLayers()` first.
- **`readonly` collections/arrays** come back freshly initialised. Dereferencing an ELEMENT of one
  (`TransmutationCircle`, `TerminalStation`, `FloorPit`) throws every frame for the rest of the
  session — looks like a permanent stale error, is actually the reload. Iterating or clear-and-refill
  lists (`_ordered`, UI hit lists) are harmless either way. Rule: never dereference an element of a
  `readonly` collection without a null guard; prefer non-readonly for anything read every frame.

Transform/MonoBehaviour fields survive fine (they're `UnityEngine.Object` references). When a rig
"looks right but refuses to change," check the collection first.

## Architecture

`Assets/Scenes/Arena.unity` contains only a camera and a `GameBootstrap` object. Everything —
arena, player, enemies, UI, sprites — is constructed at runtime, deliberately, so the scene has no
prefab/asset references and can be regenerated from the CLI at any time.

```
Assets/Scripts/
  Core/     ElementType, Spr (procedural sprites), GameBootstrap (scene + run lifecycle),
            Controls (THE input layer)
  Chain/    IProfileStore, CharacterProfile, RunSummary, LocalJsonProfileStore,
            Loadout, Durability, IAccountStore + AccountProfile + RoomLayout
  Combat/   Health, DamageInfo, StatusEffects, Moveset + MovesetLibrary, WeaponHeat,
            EchoChorus + ShadowEcho
  Player/   PlayerController, PlayerTargeting, ElementalResource + Fire/Water/Earth/Air
  Hub/      HubRoom, SigilDoor, ShowcaseFrame + IShowcaseSource, TransmutationCircle,
            TerminalStation, GridTable, CharacterCouch, HubInteractable, Trophy,
            DepthSorted, WeaponRack, ArmourStand, PhotoBooth
  Progression/ MasteryBoard, BoardState, BoardEffects, PrincipleEffects + Seasoning, GridSketch
  Art/      BodyLook, PixelSprite, Palette, FaceDetail, PitArt
  Enemies/  EnemyController, EnemyFactory, EnemyRegistry, EnemyType
  Art/Gear/ 12 gear slots (+ Relic), GearItem/GearCatalog, ICharacterRig +
            PrimitiveCharacterRig / SpriteLibraryCharacterRig
  UI/       UiKit, Hud, TouchControls + HintSwap, CharacterPreview, ShowcasePicker,
            GearPicker, CharacterScreen, TransmutationScreen, FloorRewardScreen
  Bosses/   Boss (shared base), Cantor + SectorHazards + ArenaSectors, Medusa + PillarRing + GazeShade
  Rifts/    Rift, RunLoot, HubGlimpse, RiftShards, RiftImplosion, FloorPlanner
  Puzzles/  PuzzleRoom, PuzzlePlate, EchoPuzzle, ElementsPuzzle, LightsPuzzle
Assets/Editor/
  ArenaSceneBuilder.cs   [MenuItem] + CLI entry point for scene generation
  GameArtSetup.cs        creates Assets/Resources/GameArt.asset
```

Legacy uGUI `Text` is used instead of TextMeshPro on purpose — TMP needs its Essential Resources
imported into `Assets/` before it renders, which would break clean-checkout rebuilds.

`Arena.unity` serializes `GameBootstrap`'s public fields — changing a C# default does NOT update
the saved scene. Rebuild via `ArenaSceneBuilder.BuildArenaScene()`.

## Attack timing rules

**The attack gate is never shorter than the animation, and tap beats hold.**
`AttackMotions.CooldownFor(interval)` is `Max(interval, SwingSeconds(interval))` — at extreme
attack-speed buffs the raw interval can fall below the animation's `MinSeconds` floor, so without
this the animation can outlast the gate meant to hold the door shut. Holding the attack button
settles into a slightly slower cadence than tapping on the beat: `_holdDelay` gates only the HELD
path on top of `_cooldown`; a fresh tap (`wasPressedThisFrame`) ignores it and fires the instant the
previous swing concludes.

`_tapBuffer` (`Tuning.Combo.TapBufferSeconds`, 0.05s) absorbs INPUT JITTER and nothing else — three
frames at 60fps. A buffer only ever moves a press LATER, never earlier. It's filled on the press
EDGE, so holding fills it exactly once and the hold path still pays `HoldExtraDelay`. A blink
(`Blade != null`) clears it outright, or a tap spent teleporting comes back as a swing on the frame
the blade returns to the hand. Absolute, never a fraction of the interval.

**There is no basic-attack recovery cancel any more.** A tap timed into a basic's recovery used to
cut it (`_cancelWindow`, `_cancelForfeit`, the white `CancelCue` metronome). It didn't land in play
and was REMOVED in favour of the finisher timing bar below — two timing systems on one button with
different rules (an invisible beat on basics, a visible meter on finishers) muddied both. Don't
bring it back alongside the bar.

## The finisher timing bar

`Tuning.StrikeTiming` (numbers), `Combat/StrikeJudge` (rules), `Combat/StrikeBar` (the picture),
and the "finisher timing bar" section of `PlayerController` (`BeginStrike` / `TickStrike` /
`SettleStrike`). Every finisher winds up while a slim ARC beside the character fills from the
bottom, like a basketball shot meter. The FIRST tap during it decides the strike:

    PERFECT  the green segment          +20%
    GOOD     either yellow segment      +10%
    EARLY    the red lead segment       -5%   (the same as no press - so mashing can't win)
    MISSED   nothing pressed            -5%

- **One attempt per bar**, taps only (holding attack counts as no press, so hold-to-attack pays 5%
  on finishers - tap beats hold). The press that STARTED the finisher is never judged
  (`_barOpenedFrame`), and a judged tap is consumed before the tap buffer so it can't become a swing.
- **The strike always lands at the bar's end.** Pressing never makes it come out sooner, so there's
  no tempo to win by pressing early - only damage to win by pressing well.
- **The bar is the last `BarSeconds` (0.36s) before the strike, on EVERY finisher** - four equal
  0.09s segments (widened from 0.075), same size and speed everywhere. A finisher that already delays its damage
  (charge, leap, Separatio's hold, the katana's sheathe, the bow's draw, Quintessence's merge) shows
  it at the end of that delay and gains no time; one that struck on the press (ordinary swings,
  disc volleys, thrown blades) gains a wind-up of the shortfall (`WindUpThenDispatch`).
  `StrikeDelay` MUST mirror `Dispatch`'s branch order and each path's own timing.
- **The wind-up holds the finisher's own FIRST FRAME** (`ICharacterRig.HoldSwingStart`: the swing
  with its clock stopped at k=0, plus a small tremble growing toward the strike), never
  `PlayCharge`'s raised blade - every lead-in is authored to end on that frame, so the hand-off
  stays seamless. Mobile and still aiming (the rig doesn't freeze facing during a hold, like a
  charge); the movement lock starts with the strike. The chain advances when the wind-up ENDS, so
  `NextStep`/`CurrentRange` still describe the finisher at dispatch.
- **Absolute seconds, never scaled by attack speed** - human timing is a fixed number of ms. Speed
  buffs still shorten the swing, just not the read.
- **Every finisher path calls `SettleStrike()` at the moment its judged hit resolves, before its
  damage is rolled** (11 call sites: plain swing/throw, volley, bow release or whiff, charge release,
  leap landing, Separatio, Quintessence, katana draw). A path that returns without settling is caught
  by `StrikeWatchdogSeconds`. Hits that land BEFORE the bar closes (the katana's two sheathed cuts)
  read `Pending`, which pays GOOD.
- **`_strikeMul` = parity x result**, applied on the same line as `FinisherDamageMul *
  FinisherPowerMul` at all four finisher damage sites. Basics never read it.
- **PARITY: GOOD = today's DPS.** `StrikeJudge.Parity` pays a finisher back for any wind-up it had to
  gain, computed LIVE from the chain actually being swung (real basics, real intervals, real
  finisher damage incl. boons) - a constant would drift with attack speed, since the wind-up is
  absolute. At base tempo that makes the effective finisher damage at GOOD Light 4.08 / Medium 6.27 /
  Heavy 7.66 basics (bases 3.71 / 5.70 / 6.97 x 1.10). A finisher that gained no time gets 1/1.10, so
  it lands GOOD exactly where it always did, PERFECT above, a miss below - "Good = today" applied
  evenly (otherwise long, easy-to-read wind-ups get a free +10%). `PlayerPower` models the wind-up
  priced at GOOD, never perfect play.
- **The meter** (`StrikeBar`) stands on the side AWAY from the facing (the off hand - the strike goes
  the other way), chosen when it appears and HELD for its run (the facing keeps tracking through a
  wind-up). Drawn texel by texel into one point-filtered texture at body density so it sits on the
  character's grid. The fill FREEZES where the press landed (the learning tool); the segment hit
  lights in ITS colour, fill tip included - never a white flash, which read as more fill. The green
  perfect segment is a texel wider than the yellow goods (colour is never the only channel). Overlay sort order
  (`SortingOrders.StrikeBar`).
- **Shadow's Echo runs no bar** (a basic in the finisher slot - see Shadow's section).
- **The PERFECT STREAK** (`PlayerController` "the perfect streak", `Tuning.StrikeTiming.Streak*`):
  consecutive PERFECT finishers that CONNECT add +2% crit chance each to EVERY hit, basics included
  (2 = +4%, 6 = +12%, 10 = +20%, capped; the count keeps climbing). GOOD, EARLY, MISSED or a perfect
  that hits nothing all reset it - air-swinging a cleared room must not build it. Lasts the run (it
  lives on the player). "Connected" is heard on the enemy's `Health.Damaged` (`Combat/FinisherHits`,
  attached beside `Hitstop`) for any `IsFinisher` hit from the player since the bar BEGAN; a perfect
  not yet connected at `SettleStrike` waits `StreakConnectSeconds`, or for as long as a thrown blade
  is out. Sublimate (`DiscSuspends`) is judged on timing alone - it deals nothing until the next
  finisher. The next bar beginning settles any perfect still waiting as air. Shown as PIPS on the
  meter's outer side (groups of five, mint, the perfect green at the cap, faint dots when empty) and
  a HUD counter beside the combo chain. `PlayerPower` ignores it, like PERFECT itself.

**A swing eases out, not just in.** `_poseArmSwing`/`_poseBackSwing`/`_poseBodySpin`/`_poseArmReach` carry the current pose between
frames; while a swing/charge is live they're hard-set from `Animate()` (exact keyframes), and only
once neither is running do they ease toward the walk-cycle target.

**A swing locks for its duration.** `FaceAim` freezes both the mirror and the aim offset while
`_attackTimer` is running — otherwise the arm's absolute angle kept moving mid-swing even though the
arc itself is a fixed number of degrees, and the mirror could flip the character mid-strike. Frozen
rather than latched into a copy, so the residual resumes easing from where the swing left it. A
CHARGE deliberately does NOT freeze this — it's supposed to keep tracking so the slam lands where
the player is facing when it goes.

## Sizing and range

`BaseRange` is 1.9 units. Reaching it with blade length alone would need a sword twice the
character's height, so `Spr.Slice` throws a crescent whose rim lands on the reach — the strike shows
the space it filled without an absurd blade. `Lunge` adds a small push at the peak of Chop
and Sweep; the arm reaches for it (see "The rig"), so past what a straight arm covers it becomes
a body slide - keep it small.

`Spr.ThinRing` has its OWN sprite rendered at 512 (not shared with `Spr.Ring`, which wants a soft
fat band for impact bursts). The leap's landing circle (`LandingZone`) uses it, and its flare's alpha
is independent of the colour's own alpha (scaling the peak by it would cap a quiet base forever — a
fade, not a flare).

## Reading a swing: no ground rings

The reach rings under the player are GONE. They claimed a circle while every strike is a capsule
along the facing (reach to the sides and behind that no swing has), and they sat on the ground under
you rather than on what you were looking at. Each job moved onto the thing it is about:

    does a press land        Player/TargetHighlight   a rim on the ONE locked enemy (replaced the
                                                      reticle). SOLID = a swing lands; DASHED = a
                                                      sword not yet in reach, or a disc that will
                                                      throw; bow/disc throw brighten with the
                                                      distance-damage curve. Shape, not colour.
    a finisher is banked     Combat/FinisherGlint     a light running up the weapon, first the
                                                      moment it banks, then every Period
    what the swing did       Combat/SwingSmear        greatswords: the blade's swept path, flat
                                                      two-tone at 37.5 world ppu. Basics smear the
                                                      outer blade; finishers most of it, warmer,
                                                      on top of WeaponTrail
    where a leap lands       Player/LandingZone       KEPT as a ring - a warning, not a readout

- **The rim's reach test mirrors the real ones** - capsule (`PendingRange` from the player) against
  the target's collider for a sword, `ThrowsInsteadOfSwinging` for a disc, ThrownDisc/ThrownArrow's
  `Lerp(near, 1, d / range)` for the curves. Change a hit rule, change the rim with it.
- **Rims are never parented to the enemy** (it can be destroyed any frame) - they copy its renderers'
  transforms in a late LateUpdate and sort ONE BELOW its lowest renderer, so internal seams between
  parts (a turret's head and base) are covered and only the outer edge shows. `PixelSprite.Rim`,
  cached per (sprite, pad, dashed); the pad is a WORLD thickness (`Tuning.Highlight.Thickness`, the
  character's own outline weight) converted per sprite.
- **The glint is an overlay**, never a sprite swap (flash/stone/flipbooks own the weapon's sprite),
  at the weapon's OWN sort order nudged toward the camera - weapon + 1 is some other layer in the
  rig's dense permutation (a gripping fist).
- **The smear is not the old crescent.** `Spr.Slice` on every swing was removed for reading as a
  wash; the smear covers only the blade's own path, hard-edged on the texel grid, and its size
  follows the blade's speed. It does NOT reach `BaseRange` (the blade is shorter than the reach) -
  the rim is what says a swing connects. It also smears the swing's 0.06s entry blend out of the
  previous pose.
- **Fog** (the Blindfold cost) hides the rim (`Mods.HideTargetHighlight`). Its old flag,
  `HideReachRing`, was set but never read - the card was inert until this.

**Bursts describe the shape of the hit.** Only `AreaOfEffect` steps draw `Spr.Flash(ring: true)` —
a swing already shows its reach via the blade; drawing a circular burst on every swing would
describe a hitbox shape that no longer exists.

**Swings alternate direction** (`PlayAttack(reversed)`, set from combo index) so each swing ends
where the next begins — only Chop and Sweep honour it (a thrust/spin/slam has no "backwards"
version that makes sense), and finishers always play forward (an "Overhand" reversed is a different
move).

**Strikes are swept capsules; area effects are circles.** A capsule laid along the facing replaced a
circle+cone that hit things well outside the animation's actual reach. Watch `size.x` on the
capsule — it's the TOTAL extent including end caps. Strikes fall off with number of bodies already
hit (`ChainFalloff`); area effects fall off with distance (`EdgeDamageFraction`) — a blade losing
energy through a crowd vs. a blast weakening with range. Targets are sorted nearest-first.

## Aim, cape, and the two-handed grip

**The torso does not rotate; arms carry the whole aim angle**, clamped to `ArmAimRange` (45°, well
under 90 — the swing arcs are absolute angles riding on top of this offset). The aim angle now has
to smooth ITSELF (`LerpAngle`) since it's no longer the leftover of an already-eased lean.

**Cape physics is a spring, not a curve driven off speed** — a curve freezes the cloth at exactly
what current velocity implies, so stopping snaps it straight; a spring lags and overshoots.
Driven by the forward component of velocity in the MIRRORED frame.

**The cape BENDS, it does not turn** (`Art/Gear/ClothBend`). Rows above
the hinge never move and each row below slides sideways by WHOLE texels, ~depth^2 (pinned at the
shoulders, most of the motion in the lower half), rising a little as it swings. TWO springs: the
cloth under the hinge chases the movement, the hem chases THAT (`CapeHemStiffness`/`Damping`, softer) -
so the hem lags as a run starts and whips past when it stops. The fixed flutter is a ripple running
down the cloth. The SCARF tail (`NeckBack`) bends the same way from its knot, on its own pair of
springs (`ScarfHem*`, quicker than the cape's - a strip, not a sheet); turned, it swung as a stick.
- **The layer's own renderer is never touched**, only stopped from drawing (`forceRenderingOff`,
  independent of `enabled`); a child renderer draws a bent per-character copy (same texture, its
  mesh rebuilt as one quad per run of rows) of WHATEVER the layer shows. That is why flash, stone,
  the drape's facing swap (an identity compare), lopsided capes and Apply all keep working
  unchanged. It copies the layer's sort order AFTER `DepthSorted` (`DefaultExecutionOrder(1000)`).
- The dash after-image (`RigSilhouette`) redraws sprites from their textures, so its cape is
  unbent - a brief monochrome ghost, left as is.

**Cloth over the legs moves with them** (`PrimitiveCharacterRig.AnimateLegCloth`, `ClothBend.OverLegs`;
EVERY Tasset and Belt layer - skirts, coats, tabards, faulds, plate tassets). They used to ride the
torso as boards: the walk's lean swung a floor-length hem back several texels while the hips stayed
put, and in the swing a knee comes ~10 texels forward - the leg stepped out through the cloth, and
it read as uncanny. Now, below the waist (torso y 0):
- **It hangs from the HIPS** - the torso's lean is undone in proportion to depth (`Plumb`), so it is
  continuous at the waist and the belt strap above it still rides the chest.
- **The legs PUSH it as a TUBE, not two sleeves**: its front goes where the furthest-forward point of
  EITHER leg goes, its back where the furthest-back one goes, spread across the middle - a stride
  opens it into an A. Per-column "follow your nearest leg" tore the cloth down the middle (the hips
  are four texels apart). Below a knee it DRAPES off it (`DrapeFall`), never follows the shin back
  in. Cloth far out past the legs takes less of the push (`FarFollow`).
- **It answers a beat late**: the cloth's copy of each thigh/shin angle is a spring chasing the
  real one (`LegClothStiffness`/`Damping`), plus a small trailing swing (`LegClothSwing`, 5 degrees
  - more and the hem trails off the front leg and shows it).
- Belt and Tasset share ONE length (the longer cloth's), so faulds and the skirt under them bend
  alike at every height.
- The offset is per TEXEL here (two ways at once across a row), still whole texels: where a run
  moves further along than the one before, the gap is filled by REPEATING that run's last column
  (the cloth stretches, never tears open). Rows that move as one still collapse to one quad.

**The two-handed grip**: the off shoulder RELOCATES (no rotation brings the far fist to the near one
otherwise), the off fist is placed where the main arm's angle plus a constant trail puts it and
then REACHED for (`SolveArmReach`), and the layer stack is fully PERMUTED (`TwoHandedOrder`) so both fists draw above the
weapon sprite — without it the character holds a sword with no visible hands. Fit is tuned by
measuring both fists in the weapon's own local frame, not the renderer's world AABB (a rotated
rectangle's bounding box has corners where the hand isn't).

## Finisher timing seams

Three finishers resolve on a delay, each with a rule:

- **`ChargeSeconds`** (Ruin/Skyfall) — vulnerable wind-up; player keeps moving and auto-aiming,
  only the strike commits. The hold is fists overhead, blade RAISED up and back (`ChargeArmSwing`
  40, `ChargeFist`); Slam brings it forward over the top and down past the knee (`SlamFist`,
  `SlamTo`). Both were once written as if the blade ran along the arm (-150 to 70) - in this rig it
  points away from the arm, so the held blade hung across the body and Slam swept UP.
- **`LeapSeconds`** (Falling Star/Meteor) — total immunity while airborne, paid for afterward with
  `ExposedSeconds` at `ExposedMultiplier` damage. Kept as a SEPARATE field from ChargeSeconds —
  sharing it would make every charged move invulnerable.
- **`ThrowsWeapon`** (Wanderblade/Cast) — resolves in `ThrownBlade`.

All three: the chain advances when the move STARTS, so the delayed resolve must pass
`advanceCombo: false`. (The timing bar's added wind-up is the exception: a finisher that gained one
DISPATCHES when it ends, and its chain advances then.) Every delayed resolve must call
`SettleStrike()` first - see The finisher timing bar. `ResolveArc` must take its range from the step it was given, never from
`CurrentRange` (by resolve time `NextStep` describes the *following* swing). Anything published for
UI has the same trap — `TargetHighlight` measures against `PendingRange`, a pending blast publishes
`ImpactRadius` (which `LandingZone` draws).

Immunity is `Health.Immune`, not `Vulnerability = 0` — a zero multiplier still runs the whole hit
(flash, knockback, listeners, durability wear), reading as "hit for nothing" rather than "not hit."

Only the DRAWING leaves the ground during a leap (`ICharacterRig.SetAirborne`) — the player's real
transform is what physics/camera/range-ring/blast-origin all measure from.

## Wanderblade / Cast — the thrown finisher

`AttackStep.ThrowsWeapon` sends the weapon out as a `Combat/ThrownBlade`: spears every body in a
line, then the player decides: ATTACK recalls it (a second, biting pass back to the hand), the
ABILITY button blinks the player to it, nothing lets it come home cold at its far point. Both are
fresh TAPS, not held state, or the player would never see the choice. The timing bar runs BEFORE
the throw (a wind-up) and closes as it leaves; for its linger after launch an attack tap still
belongs to the bar (`_throwTapGuard`), so a late press on the last band can't recall the blade.
Damage rolls once at launch, not per body. The blade catches when the frame's travel would carry it
PAST the hand, not at a fixed radius (a fixed radius let 26 u/s jump over the player and orbit
forever). `TryGetWeaponVisual` hands the blade the real equipped sprite/tint/size, captured before
`SetWeaponVisible(false)` — otherwise it reads as a spawned generic projectile, not the player's
own sword leaving their hand.

## Attack animation

`ICharacterRig.PlayAttack(AttackMotion motion, float duration)` — motion (Chop/Sweep/Thrust/Spin/
Jab) describes the SHAPE, duration is the real interval to next swing (not a constant — a fixed
value used to be longer than the fastest moveset's own window, so it never completed its arc).

`PlayerController.AttackLocked` is true only while a FINISHER animates — no walking, no turning.
Basics stay fully mobile. It zeroes movement INPUT (not velocity — knockback still applies) and
freezes facing for the lock's duration, never outlasting the animation
(`AttackMotions.SwingSeconds` is shared by rig and lock for this reason).

`PlayerController.MoveIntent` is the stick read BEFORE the lock takes it away — Air's momentum
meter reads this instead of the body's real state while locked, so a finisher doesn't double-charge
Air's "standing still" cost on top of the tempo it already spends. Earth is untouched by this (it
reads `IsMoving`, the body's real state, which genuinely is stationary during a lock).

**An armed finisher never expires.** `_comboTimer` only decays the chain while `!FinisherNext` —
partial progress lapses at `ComboResetSeconds`, but once the three basics are paid for the finisher
banks indefinitely. Expiring it punished exactly the play it should reward (lining up a shot,
waiting for a pack, healing first).

## Lead-ins: the second basic belongs to the finisher

The OPENER (`Basics[0]`, the Chop on most greatswords) stays as it is - the user's call, it plays
well from rest. The LEAD-IN (`Basics[1]`) is authored for the finisher: it ENDS where the
finisher's first frame starts. Every new swing blends from the current pose in 0.06s
(`EntryBlendSeconds`), and when the two disagreed the blend was a whole downstroke with no hit
in it, reversed a frame later - Rise into Whirlwind was 170 degrees, found frame by frame.

    finisher starts HIGH (the chops)          Rise   ends +85, a Chop cocks at +75
    finisher starts LOW (Spin, WhirlThrow,    Wind   starts where a Chop ENDS (-100), draws
      Throw)                                         the blade back low as the torso coils
    finisher is a thrust (Thrust, Impale)     Thrust ends -5

- **The lead-in is the swing right before the finisher, whatever the chain's length**
  (`PlayerController.LeadInNext`): Anvil's one-swing chain is just the lead-in, and Leaking's extra
  swings (`Basics[2]`) go between the opener and it. It is always staged as the alt-swing.
- **`MovesetLibrary.CheckLeadIns` warns at start-up** when a greatsword lead-in ends more than
  `LeadInWarnDegrees` (90) from its finisher's first frame (a charge's hold pose, which is snapped
  to, for charged finishers; leaps, the katana draw and Separatio are skipped). Two warn today and
  are not yet fixed: Flurry (92) and Ruin (125).
- **A hand-off must match the GRIP, not only the blade angle.** Now that the arm reaches its grip
  (see "The rig"), a lead-in whose blade matched but whose grip didn't slid the sword through the
  torso in the entry blend: Rise began with the fist behind the shoulder while a Chop ends with it
  forward and low (`RiseReachFrom`/`RiseLiftFrom` now start it where the Chop's arc ends).
  `MovesetLibrary.CheckGripHandoffs` warns at start-up about every hand-off a chain actually plays
  (NextStep order, alt staging, every chain length 1-4, into a charge's hold and out, the Blood
  Blade's release, the finisher back into the opener) whose grip travels more than
  `GripSlideTravel` while the blade turns less than `GripSlideTurnDegrees` - a slide, not a wind-up.
  Grips come from `PrimitiveCharacterRig.StartGrip`/`EndGrip`/`ChargeGrip`. A charge now BLENDS into
  its hold over `EntryBlendSeconds` (it snapped). Still judge new chains with the harness in
  `ART-NOTES.md` - the check catches slides, not every ugly frame.
- **Blade angles: 0 is tip UP, -90 points along the facing.** Thrust, Jab, Impale and the charge
  were all once authored as if 0 were horizontal (the "level skewer" was a raised vertical blade).
  The thrust family is now posed as FIST + BLADE (`ReachFor`): every thrust starts and ends in one
  GUARD (`ThrustGuardFist`/`ThrustGuardBlade`, fists low in front where a Chop leaves them), and
  Impale takes its body twist back out of the blade so the lunge stays level. Spin holds the fists
  low in front, so both Wind's coil and a Thrust's guard lead into it.
- **Between two swings of a chain the follow-through HOLDS** (`CarryReturnDelay`, 0.2s): the
  animation fills 85% of each interval, and relaxing toward the walk pose (or the grip starting back
  for the carry) in that gap swung the blade most of the way and snapped it back on the next swing.
  Past the delay it eases out to the carry as before.
- A Chop already ends where Whirlwind and Cast start, so a second Chop from the other shoulder was
  seamless too. Wind was chosen over it for being a different move - the chains were reading as
  robotic.
- Judging one: stills don't show a hand-off. Play the chain through the rig's real `Update` at the
  real intervals and capture every frame (the gait harness in `ART-NOTES.md`, `PlayAttack` per swing).

## The rig: elbows, knees, the carry, the walk

Full notes (carry pose, pauldron/glove/cloak placement, the walk) in `ART-NOTES.md`. The rules that
bite when touching the rig:

- Nothing authors the `*Lower`/shin RigLayers: `PaintElbowSplit`/`PaintKneeSplit` cut the sprites
  at the joint with two texels of overlap, so a straight limb composites to the uncut picture.
  `WithForearms`/`WithShins` rank each lower half directly above its upper. 44 layers,
  `DepthStride` 50.
- The rest carry FOLLOWS THE MIRROR (`CarryInBackArm` is `_facingAway`). Walking sideways facing
  the camera it blends (`_march`) to the MARCH carry: elbow dropped in front, wrist turned, the blade
  laid back over the trailing shoulder (17 degrees, wrist rolled so the other edge is up) with the hilt in front of the chest, on its own
  layer order (`MarchCarryOrder`, carrying forearm/glove/hand over the pauldrons). Anything reading the carry angles uses `CarryShoulderNow`/`ElbowNow`/`WeaponNow`.
- **Swing arms REACH their grip; the shoulders stay on the body.** Motions are still authored as
  a straight arm about a shoulder that `armReach` slides (`Animate`), but that slide ran to 0.38
  on a 0.17 arm and tore the shoulder off (a Chop's cock put it in the middle of the face). Now the
  grip position and blade angle the old pose describes are kept EXACTLY and reached by two-bone IK
  (`SolveArmReach`): elbow bent (`ArmBendSign`, fixed so it never flips mid-swing), wrist turning
  the weapon back onto the authored blade angle, shoulder shoved at most `MaxShoulderShove`, and
  anything a straight arm still can't reach moves the whole figure ALONG THE GROUND only
  (`_reachShift`, drawing-only like the hop - never down, or the feet sink). Arcs, hand-offs and
  lead-ins are unchanged by construction. The off hand reaches its old spot on the hilt the same
  way, and letting go of the grip blends where its HAND aims (hilt to hip), not its arm angle. In
  the carry blend the BLADE turns the short way and the wrist is derived from it.
- Pauldrons cap the arms in every stack (`PauldronsCapArms`); a cloak covers the pauldrons
  (`DrapeOverShoulders`). That also puts the arms under the HEAD, so a two-handed swing lifts them
  in two stages, read off the POSE (with hysteresis), never the motion:
  `HandsUpOrder` once the near FIST is above the shoulder line - both forearms and fists over the
  head (far under the weapon, near over it), upper arms still capped; without it a hilt in front of
  the face had a floating near fist and no far hand. `ArmsRaisedOrder` once the near elbow is past
  the neck AND the fist is overhead - the near upper arm comes over too, the far forearm goes back
  behind the head with only its fist over it (the 3/4 depth for raised arms). The elbow alone
  flashed the near arm over its pauldron for a frame on poses with the hands still low; the far
  forearm left over the head hid the face at the cock. Not under `WeaponBehindOrder`: the alt-swing
  puts the arm behind the head on purpose.
- Armour is placed against the BODY at body density (`PlateY`, `BeltY`, `SoleY`, `ArmHarnessY`),
  never on literal offsets.
- The gait is a WALK (a run was tried and dropped) on one clock (`_stride`); legs are posed by
  their FEET (`SolveLeg`), so the knees bend. Stills don't show a
  gait or a lead-in hand-off - judge it with the frame-capture harness in `ART-NOTES.md`.

## Turning and framing

The body turns toward aim: mirrored left/right, lean clamped to `MaxLean` (42°) with the arm
covering the remainder — full 360° rotation would invert the character (head under feet) in this
3/4 camera. The hierarchy splits hips (planted, legs hang here) from torso (takes the lean) so the
lean pivots at the waist rather than swinging the feet off the ground. The contact shadow is a
SIBLING of the rig root, not a child — as a child it inherited the root's spin during whole-body
finishers and orbited the character; ground doesn't move.

`MirrorDeadzone` holds the previous facing while `|aim.x|` is tiny (straight-up aim is `-4e-8`, not
0 — a bare sign test flips the mirror every frame otherwise).

Camera centres the player (not a lead-the-subject offset, and clamped against the FLOOR rather than
the walls — `FloorMargin` draws ground past the walls so centering never pans onto void).
`PrimitiveCharacterRig.TopDown` exists but is OFF by default — tried and reverted, because the 3/4
mirror+lean+residual read costs the character legibility that top-down doesn't buy back at this art
density.

**Gear must break the silhouette.** Pieces sized to sit inside the body outline make a fully
equipped character read as "the same blob in a different colour." Re-placed against the outline
(pauldrons to shoulder-line, greatsword well past the hand, cloak flared wide), equipped gear adds
~76% width and ~40% height over bare.

## Pixel art (`Art/PixelSprite.cs`, `Art/Palette.cs`)

Different from `Spr`/`Glyphs` in three ways: `FilterMode.Point` (bilinear blurs pixel art), real RGB
per texel (so `Tint` must stay white — the renderer colour is a MULTIPLY), and fixed density rather
than "one sprite = one unit."

**Densities, and why each exists:**

    ARMOUR/body    37.5 px/unit   the body's own density; ramp shading still holds at this size
    WEAPONS/JEWELLERY  75 px/unit finer silhouette for objects held away from the body and
                                   looked at directly (a disc's hole, a ring's stone)
    MENU (character screen only)  150 px/unit — cannot be drawn in the arena (below 1 screen
                                   px/texel at common resolutions), but the preview magnifies
                                   enough to resolve it. Opt-in PER PIECE via `GearItem.MenuLayers`

**Why 37.5/75/150 specifically**: at `CameraSize 4.8`, 37.5 puts one art pixel on exactly 3 screen
pixels at 1080p — an integer ratio, which is load-bearing. At a fractional scale, point-sampled
texel columns alternate between 3 and 4 screen pixels wide and *which* columns get the extra one
changes as the character moves — shimmer, not chunkiness. `Core/PixelPerfectZoom` holds the camera
on a whole number of screen pixels per texel at any window size; URP's own `PixelPerfectCamera`
can't, because `assetsPPU` is an int and this art is 37.5.

`SizeOf` derives `Size` from the grid — never hand-type a `Size` for a pixel layer, or
`LayerSprite.Size` applies as a non-uniform stretch. `PixelSprite.OutlinePadFor` derives the
auto-outline's thickness from the ppu (an outline is a world-size decision, not a texel count).

**Grid discipline**: widths must be EVEN (mirroring needs a texel-boundary pivot) except where a
piece is built symmetric about column 14 directly (many gear grids) — `PixelSprite` warns on odd
widths and ragged rows, and the warning NAMES the sprite.

**More pixels is free; more DETAIL is what costs.** Block-doubling a grid and drawing at half the
ppu is provably the same image (`floor(floor(2x)/2) == floor(x)` at any offset) — verified at zero
differing screen pixels. A denser grid only looks different when someone spends the extra cells on
new features (a nose, extra shading) — which is a style choice, not a consequence of density, and is
usually the wrong call at this project's block-flat aesthetic (see the 16×14 head, never grown for
its own sake).

**`Health.SetFlash` swaps every layer to a white silhouette** (`PixelSprite.Silhouette`) rather than
lerping a single sprite's tint — baked colour makes `Lerp(white, white)` a no-op, and depth-first
`GetComponentInChildren<SpriteRenderer>()` used to hit a leg, flashing only one limb.

## The twelve slots (+ Relic)

    Head  Shoulders  Torso  Back  Neck  Ring  Legs  Trinket  Boots  Belt  Gloves  Weapon  Relic

`GearSlot` and `RigLayer` stay decoupled — reordering `GearSlot` corrupts saved loadouts, because
`JsonUtility` serialises enums as ints. `Loadout.DropStale()` self-heals at load by removing any
entry whose item doesn't belong in the slot it sits in. New slots must be APPENDED, never inserted.

`SlotKind`: Weapon wears on hits landed, the eight armour slots wear on hits taken, Neck/Ring/
Trinket/Relic are `Cosmetic` (never wear).

## Weapon skins are locked to the weapon's class

A weapon may only be disguised as another of its own `WeaponClass` and handedness — the class
decides how the thing is HELD (two-handed grip, dual-wield order), not just how it looks. Enforced
in two places: `TransmutationScreen.SameClassOnly` filters the offer, and **`Appearance.Resolve`
refuses a mismatch at the point of use** — a valid disguise can go stale on its own (skin discs as
other discs, then equip a greatsword) and resolving falls back to the real weapon rather than
drawing the mismatch.

## Black diamond: relic and cosmetic, never one weapon

Every black-diamond item is TWO drops: a RELIC carrying the signature finisher and its mechanic, and
a separate COSMETIC weapon carrying only the look. Wielding the matching cosmetic while holding the
relic renders the full bespoke treatment; holding the relic alone with any other weapon still grants
the mechanic, just without the art.

**This is purely visual set-bonus by design** — a player holding only the relic is mechanically
identical to one holding both, so the cosmetic half trades freely on the open market without anyone
buying an advantage by owning it.

`GearRoller.RollFinisher` is what makes the relic slot worth anything below the ceiling tier: a
relic rolls a random (non-signature) finisher from `MovesetLibrary.RollablePool` exactly the way any
other slot rolls a random stat. Bronze/Silver/Gold seed the slot (still swappable by a floor
reward); Black Diamond LOCKS it (`GearItem.Signature`, gated `Slot == Weapon` for the weapon and
`Slot == Relic` for the relic — never both granting power).

`GearSlots.Accepts` requires the relic to match the currently HELD weapon's class/handedness — a
finisher of the wrong class would animate as a move the held weapon can't perform. The hand wins
when both weapon and relic could supply a signature (`GameBootstrap.FinisherSource` checks the
wielded weapon first). Relic contributes NO POWER to `Loadout.TotalPower`/`WornCount` — it's
carried, not worn. Relic transmog is disabled entirely (`GearSlots.Transmoggable` excludes it) since
its roll IS its mechanic; a picture deciding a mechanic is exactly what "a skin grants nothing"
forbids.

**The blade that heats/echoes/etc. is the one being DRAWN, not the one supplying the mechanic** —
skin the matching cosmetic over another sword and socket the relic, and the drawn blade gets the
full treatment (Emberline's heat cycle, Shadow's trailing after-image); skin something else, and the relic still
grants the mechanic with no visual tell.

Currently built this way: **Emberline** (`ember_relic`/`ember_blade`, heat cycle → Conflagration),
**Shadow** (`shadow_relic`/`shadow_blade`, echo chain → light finisher, see below), **Phantom**
(`phantom_relic`/`phantom_blade`, teleport-cosmetic dodge → Reckoning), **Blood Blade**
(`blood_relic`/`blood_blade`, a vial that fills on landed thrusts and swaps the finisher slot to a
heavy AoE release on full → Bloodletting).

### Shadow's signature: a light finisher, not a permanent passive

Not a passive (it was once +50% damage with no tradeoff). Shadow's *entire chain*, finisher
slot included, is plain basic-damage swings, and EVERY swing of that chain lands a second, echoed
hit for `ChainEchoFraction` of the first - the light finisher's damage REDISTRIBUTED into the
follow-up of each swing (the code is authoritative: `PlayerController.ActiveEchoFraction`, applied
in `ResolveArc`). That's a real cost — one of three chains gives up its finisher's damage entirely.

**The Echo finisher's RING IS GONE.** It once also summoned 3-4 shadows casting the wheel's OTHER
finishers at half strength (`SummonsEchoes`, `EchoChorus.Summon`, `CollectOtherFinishers`,
`UnpaidWindUpFraction`); that put finisher damage back on top of the redistribution and was removed
outright. `EchoChorus` now only draws the trailing after-image.

Shadow's signature is tagged Light-weight but is EXEMPT from `AttackLocked` — the one finisher-slot
move that never freezes movement or facing — and from the finisher timing bar (no wind-up, no bar,
a flat 1x: there is no finisher to time). Those exemptions and its DPS are a matched pair; don't
tune one without the other. `ChainEchoFraction` is **0.60** (was 0.5) so Shadow stays ~10% under a
Light chain landing PERFECT (and ~5% under one landing GOOD) - priced against the skilled player the
deep floors assume, since it can't earn the bar's upside. `Rooted` (the ledger cost that extends the
lock to basics) still overrules the lock exemption.

### Cosmetic weapons carry no power, ever

Every purely-cosmetic black-diamond/diamond weapon (Rift Blade, Saint, Prism, Sniper, Crossblade,
Lumen, Obsidian, Aether Greatsword) is power 0 and grants nothing beyond its own look plus whatever passive cosmetic
effect it carries (shard drift + implosion deaths on Rift Blade, a halo on Saint, per-element gems
on Prism, a haze animation on Phantom, a ribbon spring on Lumen, the Secret Fire's marks on
Aether Greatsword). It has NO relic yet - an empty `SignatureFinisher` pays out alone from a design drop. Each earns its silhouette against
the class rule rather than by ramp alone. Lessons from building them (outline erosion of narrow
gaps, two-wave turbulence, grip pivots, glass/energy ramps) are in `ART-NOTES.md`.

## Every sword the same height — and every blade measured against the BODY

Every greatsword is 158 texels (`DemoGear.SwordHeightTexels`, 1.0533 units, ~1.36x the body). A
MECHANISM, not a convention: each weapon declares its non-blade rows and `BladeRowsFor` works out
the blade. Fix a height by grid rows, never by touching `Size`/offset/pivot.

- Blade + outline never wider than the TORSO (`SwordBladeWidth`, grid 10); guard + outline never
  wider than the SHOULDERS (`SwordGuardWidth`, grid 16). Even widths. Blade width and length are
  a pair.
- The grip does NOT follow the blade: `SwordGripRows` 12, `SwordGripWidth` 4, pivot
  `GripCentre(gripTop, gripRows)` for every greatsword. A grip written as literal rows comes out
  short.
- Effects up the blade (halo, gems, shards) are DERIVED from the sword's own grid, never literals.
- Measurements inside a blade need a clamp against the outline's absolute bite (`ShadowMinFlat`,
  `RiftMinFlat`).
- Outlineless weapons go through `OutlinelessWeapon`, swappable stage sprites through
  `StageSprite` - both apply `upscale2x`; skipping it renders at half size.

The per-weapon exceptions (Sniper, Phantom, Saint, Rift, Lumen, Zanmato, Obsidian...) and the
user's calls behind them are in `ART-NOTES.md`.

## Weapons stay at their own density - body-pixel weapons were tried and REVERTED

Weapon grids are twice the body's density, so a weapon draws in pixels half the size of the hand
holding it (a "mixel", found against a clear-pixel-game reference). Drawing arena weapons at body
density was built and reverted by the user's call: WE LOSE TOO MUCH DETAIL. An automatic 2:1
reduction destroyed each detailed weapon's defining feature (Shadow's hollow, the Ripsaw's chain,
the Pacemaker's mosaic, Lumen's vein, the Rift's grooves); a hand-authored Shadow at body density
kept its hollow but still gave up the rest, and big pixels showed rotation jaggies in the carry and
every swing. Raising the BODY's density instead was also weighed: it means redrawing every body and
armour piece and breaks the whole-screen-pixel camera at 1080p (75 ppu = 1.5 px/texel). Don't
reopen this without a new idea.

## Every disc the same height

Every disc-class weapon is **28 grid cells tall** (`DemoGear.DiscHeightCells`, FinePpu before the
2x upscale), the tallest disc when the rule was set - **32 cells as DRAWN**, since the auto-outline's
canvas pad adds 2 cells a side. Width is free (Deadlights' oval mouth, Eclipse's crescents), height
is not. A mechanism, like the sword height: `Disc()` warns about any disc whose DRAWN height (grid
plus outline pad) is off the standard unless `DiscHeightExceptions` lists it with its reason (Ignis
and the Armillary's flames). An OUTLINELESS disc makes the pad up out of its own grid (Rai's is
32) - counting grid rows alone let Rai through four cells short at under half the others' area,
and it read as a smaller weapon. Fix a disc by building its field or grid to the height, never by
touching `Size`.

- The plain discs (Silver, Gilded, Nullpoint) are built by `BuildDiscAt(N)` for BOTH densities
  now (arena N = 28, menu 56); they were hand-typed at 24 and could not follow the standard.
- Field-built discs derive their scale from the standard: Deadlights' radius, Eclipse's
  `EclipseK` (layout measured off the sketch, sampled through K; K keeps the centre line on a whole
  cell). The dual blades (Void Fang, Sunfire Cleavers) were lengthened by blade rows, grip moved
  down with them.
- The off-hand disc sits IN the back hand (same hand-relative position as the main disc), and the
  back arm swings out `SplitOffArmDegrees` while split so the disc clears the torso; the disc is
  counter-rotated to stay upright. `SetWeaponSprite` carries flipbook frames to the off-hand copy.

## Weapons that cast light

`Art/Gear/HeldGlow.cs` (`GearItem.GlowColor`/`GlowIntensity`/`GlowFromRim`), additive 2D point
light. **THE NEUTRAL GLOBAL LIGHT**: once any 2D light exists, URP draws every lit sprite as its
colour x the sum of lights - a lone light turns the scene BLACK. `HeldGlow.EnsureNeutralGlobal`
adds a white global light at 1 first; anything else that ever adds a 2D light needs it. `Light2D`
lives in `Unity.RenderPipelines.Universal.2D.Runtime`.

Disc designs (Armillary, Rai, Horologe, Singularity) are in `ART-NOTES.md`. Two mechanisms from
them other code uses: a disc's off hand can hold a different picture (`GearItem.OffhandLayer`/
`OffhandIdleFrames` via `ICharacterRig.SetOffhandPicture`), and thrown discs take the half
matching which disc they are (`ICharacterRig.TryGetDiscVisual`).

## Tria Prima: three Diamond swords that forge into one

Sulfur Ripsaw + Salt Pacemaker + Mercury Reactor nest into Tria Prima (`DemoGear.TriaPrima.cs`;
the art is in `ART-NOTES.md`). Each sword is ONE continuous field and the fusion samples the SAME
fields, so a part can't drift from its fused self. Don't name a DemoGear member `Core` (shadows the
namespace); static row arrays in the partial files are LAZY (cross-file static init order).

The Armillary disc set fuses the same way (`GearForge.Fusions`, `armillary`); its Quintessence
finisher is described in `ART-NOTES.md`.

**Forged, never dropped.** `GearForge.Fusions` burns one unequipped minted instance of each part
and mints the weapon AND the relic (Forge tab FUSE; the hub Forge lights when a set is complete).
This needed minted records to know WHICH design they are: `MintedGearRecord.Design` names the
authored item an instance wears, and `ToGearItem` starts from `Instantiate(design)` so it carries
every picture and flag (layers, menu art, flipbooks, split blades, signature).
`GearItem.ForgeOnly` keeps the authored Tria Prima/Seal out of `GearOwnership.Owned`; `Owns` treats
the design as held once a minted instance (`DesignId`) is. `GameBootstrap.DevMintDesign(id)` mints
a part for testing.

**Diamond and Black Diamond drops are DESIGNS, not rolls** (`Chain/DesignDrops`). Every authored
piece of the tier is eligible, any slot or class, except Forge-only designs. A Black Diamond WEAPON
pays out WITH its relic (paired by shared `SignatureFinisher`), so a paired relic is never a
separate thing to land on. Used by both box redemptions (random draws across ALL designs, not
slot-first; targeted offers only slots/classes that have a design) and the floor-100 loot drop.

The FUSE tab only exists while the player holds a complete set (equipped pieces count, so it does
not vanish when one is worn - the preview says to unequip). `GearForge.Fusion.Class`: one set per
weapon class. Disc and Bow sets are still to be designed (their own themes and finishers).

**Separatio** (the seal's signature, Medium): hold the blade up (`Tuning.Separatio.HoldSeconds`, a
tell not a price - NOT `ChargeSeconds`, which would also send every figure through ResolveEcho's
unpaid-wind-up discount), then the player goes pale (`Ghost`, colours restored exactly), the blade
leaves the hand (reference-counted `SetWeaponVisible`), and three `ShadowEcho` figures in principle
tints (`Combat/SeparatioFigures`) each hold one part (`GearItem.SplitBlades`, from the DRAWN weapon)
and strike a third of a turn apart - Chop, Sweep, Thrust. Each hit is `ResolveEcho` at 1/3 of the
step's 5x, so the move totals exactly one Medium; the echo path also stops heat/wear/principle
marks firing three times.

Testing it from `eval`: slowed time gets reset to 1 by hit-stop, so make the enemies immune (or move
them away) before slowing; and a `VirtualAttack` set under slowed time is sometimes not consumed -
re-set it until `ComboIndex` changes.

## The Forge: stars, sub-stats, combining, re-rolling

`Chain/GearForge.cs` holds the rules; `UI/ForgeScreen` only shows them.

Every Bronze/Silver/Gold piece has a PRIMARY stat (fixed value per tier, never a range) plus
SUB-STATS (Bronze 1, Silver 2, Gold 3), each rolled in a range whose top sits below the primary.
Sub-stats may repeat and may share the primary's kind. `SubStat.Value` is the UNSCALED roll;
`MintedGearRecord.RebuildGrants` is the only thing that should change `Grants`.

**One pool per slot** (`GearRoller.Pools`, the user's call 2026-10-05): every stat in a slot's pool
can be its primary OR a sub-stat, and every one of the 25 rollable stats sits on at least one
slot, by the slot's theme (the table is in the `Pools` doc; `Assay.GearTable()` prints it with
values). Weapon 9 per class, Belt and Gloves 6, every other slot 5. A bigger pool means two pieces
share a primary less often, so combining is slower than with the old two-stat primary pools.

**Magnitudes** (`GearRoller.KindScale`, on a Gold primary of 12 points): knee stats are sized so one
fully targeted Gold 3-star piece fills about HALF the stat's knee (scale = knee / 80); the crit
family (Crit Chance 0.45, Crit Damage 1.9, Accuracy 1.75) so a roll of each is worth a roll of
Damage on the Max player (`Assay.GearWorth()`, `Assay.CritParity()` - damage-first and crit-first
Strikers within ~10% of the mixed one); Resilience at Max HP's scale, Graze/Brace at twice it (each
works about half the time). A WEAPON's primary is `WeaponPrimaryMul` (1.5x) an armour piece's - the
one slot every build fills, and the one without the armour floor; weapon-only crowd stats have that
folded out of their scale so their primary is what it was. Sub-stat ranges are the same on every
slot. The armour floor's Max HP was cut to 1.25/2.5 (Max: ~31 points, half the knee - it had been
74, past the knee on its own).

**Changing the tables re-values stored rolls.** A primary is rebuilt from the tables; a sub-stat's
value is stored. `MintedGearRecord.StatsVersion` names the tables a record's rolls were made
against (`GearRoller.TablesVersion`; 0 = the 2026-09-25 placeholders), and `GearForge.Migrate`
(run at load, BEFORE `EnsureSubStats`) moves each roll to the same PLACE in its new range and
stamps the version. EVERY `new MintedGearRecord` must set `StatsVersion = GearRoller.TablesVersion`
(zero means old and gets remapped). To change a scale: bump `TablesVersion` and give the old table
a case in `GearRoller.SubStatRangeAt`.

Four levels: base, then one to three stars (`UpgradeLevel` 0..3). Stars scale EVERY stat.
Three stars must stay below the next tier's base, or a promotion hands back a weaker piece.

Two pieces combine when `GearForge.MatchKey` matches: slot, tier, level, primary, plus class on a
weapon and defensive ability on a torso (or one ability would silently vanish). The result is one
star up, or from three stars the next tier at base; Gold three-star is the cap. Matched sub-stats
are matched per OCCURRENCE and keep the highest rolls; the rest are randomized. On a promotion the
matched kinds are certain but their values re-roll in the new tier's range. No boxes are spent -
the two pieces are the price. Equipped pieces never combine.

**Plan, then Execute.** The preview shows only the certain half; randomized sub-stats show as
`??? (?-?)` and are rolled at CONFIRM from fresh randomness. If the preview knew them, reopening
the menu would be a free re-roll.

Boxes buy randomness or remove it: random redemption, targeted redemption, and re-rolling ONE
sub-stat in boxes of the piece's own tier - 1 for a new value, 2 for a new stat (never the same
kind). A re-roll can come out worse. Diamond/Black Diamond and relics carry no stats and never
combine. Records minted before sub-stats get their tier's worth backfilled on load
(`GearForge.EnsureSubStats`).

**The stats and where each one bites** (all percentage points in `StatPercents`; magnitudes per
kind via `GearRoller.KindScale`, since crit chance and knockback don't share units):

    FinisherPower        finisher damage (swing, thrown blade, volley, arrow)
    FinisherKnockback    how far a DISPLACING (Heavy) finisher throws/pulls - never flinch time
    CritChance/CritDamage  on top of Tuning.Player.BaseCritChance (5%, so a crit-damage roll is
                         never dead) and the element's own; discs/arrows roll once per throw
    Graze / Brace        mitigation while moving / standing still (IncomingDamageMultiplier)
    Cleave               shrinks per-body LOSS: blade chain falloff and disc ricochet falloff
    ElementalEffectiveness  ElementalResource.ReleaseScale (wiring this also made Twin Spark's
                         60% finally apply - it was declared but never connected)
    ComboTime            ComboResetSeconds for a partial chain
    AoeRadius            "Area": AreaOfEffect steps' radius, the ring that draws it, leap impacts,
                         and element release radii (ElementalResource.AreaScale)
    Accuracy             raises the bottom of every hit's damage range (see The stat core)
    HealReceived / RepairReceived  the floor reward's Heal / Repair card ONLY
    Mend                 % condition restored to worn gear on every floor clear
    Splash               % of a hit to every OTHER enemy near the DESIGNATED target only (first
                         body of a swing / throw, the arrow's target) - off every body it compounds
    Pierce               BOW ONLY - % of an arrow's hit to every enemy in a strip behind the target

Splash/Pierce spill through `Combat/CrowdHits` as DUMB hits (no hooks, knockback or crit of their
own) - re-running on-hit hooks per spilled body would multiply the stat's worth. Weapon rolls are
CLASS-AWARE (`GearRoller.FitsClass`): Pierce only on a bow, Cleave never on one.

There is no Sturdiness StatKind: `DamageResistance` already slows armour wear per hit, so it is
LABELLED Sturdiness. `StatKind` is append-only (saved by ordinal).

Stars are drawn as sprites (`Spr.Star`, `UiKit.Stars`), never "★" text - the legacy font has no
reliable glyph and WebGL has no OS font to fall back on.

## The gear picker stacks identical pieces, and nothing else

`UI/GearPicker` collapses duplicate cards by `GearItem.StackKey` — empty on authored catalogue
assets (always separate cards), filled by `MintedGearRecord.StackSignature()` on minted ones (every
attribute the item actually has: slot, tier, name, stat signature, ability, rolled finisher — two
rolls merge only when genuinely identical). Durability is NOT in the key (it's state, not an
attribute) — the stack hands back the least-worn copy, or the worn one if it's a member, so
re-picking an equipped card is a no-op rather than a quiet swap.

The transmog tab stacks on a LOOSER key (`AppearanceSignature()`: slot/tier/name only) since a skin
grants nothing and stat differences the disguise can't display shouldn't split the list. Both tabs
share one `GearPicker` (no more `Cycle`) — a slot row on either tab opens the grid, with the empty
cell standing in for "as equipped."

**Minted gear has no art unless it wears a design** (`MintedGearRecord.Design` - Diamond weapons
and fusions, see Tria Prima). Stat-rolled Bronze/Silver/Gold records still set no `Layers`;
pointing those at matching `DemoGear` art is a content decision, not a UI bug.

## Finisher weight classes

Every finisher is Light, Medium, or Heavy. Damage expressed in basics, lock in multiples of a basic:

| Tier | Damage (basics) | Lock (×basic) | Flinch unarmoured | Flinch armoured | Knockback |
|---|---|---|---|---|---|
| Heavy | 6.5 | 2.5 | yes | yes — punches through | yes |
| Medium | 5 | 1.5 | yes | no | no |
| Light | 3 | 1.0 | no | no | no |

Under a 2-basic chain: Light 1.67 DPS, Medium 2.00 DPS (the efficient default), Heavy 1.89 DPS
(trades ~6% output for guaranteed flinch + knockback). Chosen for this SHAPE, not for equal DPS —
true parity would put Medium at 2.2x lock and Heavy at 3.1x, absurdly slow. `DamageInfo.Displaces`
comes from Heavy only.

The declared numbers (and these DPS figures) are the pre-timing-bar ones, and they still hold AT A
GOOD PRESS: the bar's parity factor pays back any wind-up a finisher gained (see The finisher timing
bar). PERFECT lands above the table, a miss or early press below it.

**Flinch is denial, not a punish window** — no bonus damage, no `Vulnerability` change; the value is
that the attack doesn't happen. `EnemyArmor` doubles as flinch resistance while a shield is up
(Medium is refused, Heavy is defined as strong enough to force it anyway). No per-enemy cooldown is
needed — basics never flinch, so even an all-Heavy wheel can land at most one flinch per chain.

**Every enemy kind routes into a shared "just finished attacking" beat** on either a successful
attack or a denied one — Chaser (recovers in place, deliberately does NOT give ground even on a
successful hit — it's the pressure kind), Ranged (kites back, its ordinary post-fire behaviour, not
just a flinch response), Bomb (backs off before re-arming — exists only for the denied case), Turret
(charges before engaging, drops the charge on lost line of sight so breaking cover isn't free),
Dasher (its existing `Evading` phase, unchanged).

**Elite is a TIER, not a kind** — `EnemyFactory`/`EnemyType.cs` reads one `EnemyDef` per kind
indexed by enum value (not declaration order, to survive `EnemyKind` reordering), and Elite is a
flag/multiplier applicable to any kind (HP/damage up, resistant to pull/push/knockback but NOT to
flinch, plus a per-kind extra attack pattern on a fixed cadence — never randomised, since reading
the pattern is the whole point of a telegraph):

    Chaser   Riposte     every 2   delayed second hit at x1.35
    Ranged   Volley      every 2   3 bolts in a fan around the frozen telegraph aim
    Bomb     Cluster     every 1   3 delayed shards on death (spawned from Died, not scheduled —
                                    an Invoke on the bomb would be cancelled when it's destroyed)
    Turret   Overcharge  every 3   beam swells x2.2 (counts TICKS, not charges)
             + Mire      every 7s    a lobbed YELLOW shell (MortarShell's flight, parry AND fuse) whose
                                    fuse ends in a wide SLOWING patch (Hazards.MireField, 0.55x for
                                    5s, no damage) - the countdown is the window to step out.
                                    No line of sight needed - it's the answer to hiding from the
                                    beam, and holds the player where the beam reaches. Flinching the
                                    0.6s wind-up costs the lob. Parried, it lands blue and slows
                                    ENEMIES. Read off the Elite bool; EnemyDef.Elite stays Overcharge
    Dasher   Redouble    every 2   chains into a second charge instead of evading

## Enemy movement: the route, the steer, the surround

`Enemies/NavField` + `Enemies/EnemySurround` + EnemyController's "steering" section,
`Tuning.Steering`. Every enemy used to walk a straight line at its goal and pushed into any column
in the way.

- **ONE route for the whole wave** - Dijkstra outward from the player over a 0.5u grid, every
  0.15s; each enemy reads its own cell. Cells are closed by ASKING PHYSICS for solid static colliders
  (walls, columns, a risen spire) and bosses' kinematic bodies within `Clearance` - so a new obstacle
  needs no code. Corpses and triggers never close a cell. Pits are COSTS, not walls. Driven lazily
  from enemies' FixedUpdate; `MarkDirty` on a column breaking and a floor building (plus a 1s
  re-scan backstop).
- **`RouteDirection` string-pulls**: straight at the player when the line is clear, else at the
  FARTHEST cell down the route still in a straight line - a curve round a column, not grid steps.
- **Lines ignore what they START in** (a pit being crossed, a cell the clearance closed because the
  body is pressed to a column) - judged by the START'S OWN CELL, never the first sample ahead (that
  let a line that stepped INTO a pit count as already in it), and only up to `Grace` for closed cells
  (or it saw through the column). Off the grid without leaving a closed cell is NO floor - a kiter in
  the wall's clearance band read "into the corner" as open.
- **`Steer` is the only way a walking enemy picks a direction** - the wanted one plus 16 around it,
  scored for match, free floor ahead, pits, and packmates. `Flee` (Ranged/Bomb kite, Dasher evade,
  Mortar) also scores OPEN floor and keeps off the player, so a cornered kiter breaks out along the
  wall. `Strafe` turns round at a wall. A Dasher's RUSH is never steered.
- **The surround** (melee: Chaser, Bomb, Dasher): within `SurroundRadius`, slots are handed out IN
  THE ORDER the pack already stands, centred on the pack, spacing capped and the arc capped at 240 -
  the side away from the pack always stays open. An enemy keeps its slot while telegraphing (dropping
  out reshuffled everyone). A slot only shapes the approach from OUTSIDE its radius, swinging at most
  50 deg per step and coming in slower the further round it still has to go (orbit, then close).
- **Variety**: a per-enemy weave (fades out inside 2.5u so swings land square) and turn rate.
- **Stuck**: trying to move but not moving -> a short push in a random open direction each second;
  walled off from the route for 5s -> moved to the nearest reachable floor (never for a body merely
  held up by its pack); 10s -> one `[StuckWatch]` warning naming it.
- **A Dasher's rush hitting anything solid** stuns it (`DasherWallStunSeconds`, armour-piercing) and
  breaks a cracked column. Then it is WARY for `DasherShySeconds`: it only charges down a clear line
  and walks round otherwise - without that, standing behind a column held one off forever.

## The Mortar: a lobber that runs

`EnemyKind.Mortar` (appended) + `Enemies/MortarShell.cs`. Flees inside `MortarFleeRange`, walks in
only from past `MortarRange`, drifts sideways between; plants for a short wind-up and lobs a shell
at where the player stands AT THE MOMENT OF FIRING. Floor 4+, walks in from the edge. One wind-up
(`_telegraphTimer`) opens all three of its actions, `_mortarAction` says which.

- **Elite = Barrage** (`ElitePattern.Barrage`, every lob): three shells 0.5s apart, each aimed at
  the player at its own moment, planted throughout. Each shell deflects on its OWN: at base
  tuning the parry cooldown limits it to one per volley, and a build with enough cooldown reduction
  to parry again inside the 1s volley earns the second (the user's call). Keep
  `MortarBarrageSpacing` over `ParryWindowSeconds`, or one parry turns two shells for free.
- **The flame answers a rush, and only that**: fires only when the player is inside
  `MortarFlameTriggerRange`, 2s cone, turns at 70 deg/s (outrun by circling), 15s cooldown SPENT
  AT THE WIND-UP so a flinch denies it without refunding it. Takes priority over a lob. All
  Mortars, not only elites.
- A flinch ends whatever is running (`EndMortarAction`): wind-up, rest of a barrage, flame.
  Shells already in the air still land.

- **Fleeing goes toward open floor** (the shared `Flee`, see Enemy movement); cornered, it breaks
  out sideways along the wall. A plain "away" vector pinned it in the first corner it backed into.
- **The shell is its own object** (ClusterShard's reason): a killed mortar's shell still lands.
  Rooted at the LANDING point; shell and shadow travel as children, height is a y-offset.
- **The fuse COUNTS**: blinks in groups of 1, 2, 3, then the blast (`MortarBlink*`). Group gap
  must stay clearly longer than blink gap or it reads as six even flashes.
- **Parryable in flight**, measured to the DRAWN shell (`MortarParryReach`). `TryParry` is asked
  only once in reach - it spends the window. A deflected shell hops `MortarDeflectDistance` back
  toward its side, starting from its current height, fuses in BLUE, and hits enemies (credited
  to the player) instead of the player. A landed shell can't be deflected; a parry at its blast
  still negates it, as Bomb's does.

## The floor difficulty curve

`Enemies/FloorDifficulty.cs` — the one place answering "how hard is floor N":

    HP and the WAVE POOL make a floor LONGER.
    SPEED, DAMAGE, TELEGRAPH, and CADENCE make a floor DANGEROUS.

How many enemies is no longer here - see the wave budget below.

Speed is capped at 2x base — still slower than the player (disengaging must stay possible; the crowd
is a positioning problem, not a race). Proportional scaling on the telegraph needed an ABSOLUTE
floor as well as its own percentage floor (`TelegraphMinSeconds`, 0.24s) — a purely proportional
rule applied to durations spanning 0.35s–0.80s clamps the short one below human reaction time before
the percentage floor would ever kick in. General rule: whenever a proportional rule scales things
differing by more than the rule's own range, it needs an absolute backstop too.

## The Assay: the balance model

`Assets/Scripts/Balance/` (`Assay`, `Build`, `Curve`, `Policy`). What a build is worth on a floor:
damage out AND survival in. It succeeds `Player.PlayerPower`, which priced damage only, so its
damage-greedy player took every survival cost for free. Measurement only for now - nothing in the
game reads it; it replaces PlayerPower in `WaveComposer` once the stat core lands.

- **Builds** are a playstyle (`Archetype`: Striker, Tempo, Bulwark, Elementalist) on an element, at a
  `Kit`: Reference (nothing), **Max** (board at the cap, Gold 3★ weapon + the build's three best slots
  at 3★, every other slot FILLED at Gold 1★ - the player the deep floors are balanced against), and
  Ceiling (every slot 3★). Gear is real `GearRoller` output from the real slot pools.
- **Pick policies:** Random; Sensible (log DPS + log effective HP; healing counts for at most another
  pool's worth, or lifesteal outweighed every health cost; refuses a pair that cuts survival over 20% or
  below 3 Chaser hits; prices Withering ten floors ahead); Greedy (damage only - the exploit detector).
- **Every guess is in `Assay.Assume`** (moving share, Fire's average heat, Air's momentum, a release
  every 15 s...) and judgement-call entries in `Assay.Judged`. The element numbers the calibration
  may move are copied from `Tuning.Elements` into `Assay.ElementNumbers`; `ElementNumbers.Drift()`
  must say `none` (the two agree) whenever the game's numbers change.
- **`Assay.CalibrateElements()`** searches those numbers for element parity (coordinate descent
  inside identity-preserving ranges, `AssayCalibrate.Knobs`) and prints the result to write into
  `Tuning.Elements`. Gear is rolled around each element's head starts (`Saturated`), so a model
  player never buys the stat their element already gives.
- **`Assay.Check(PowerBuild.X)` must say OK**: a legacy build replays PlayerPower's runs exactly (same
  seed, same RNG order, PlayerPower's cadence). Run it after touching the model.
- **PlayerPower leaves out `Tuning.Attack.GlobalTempo`**: it prices 0.42 s between basics, the game
  swings every 0.525 s, so today's floors run ~25-32% longer than their TargetSeconds
  (`Assay.Pricing()`).
- `Assay.Targets` + `Assay.Scorecard()` are the rebalance's phase gates; the baseline they were
  calculated from is `docs/balance/2026-10-05-baseline.md`.
- **The gear tables through the model** (`Balance/AssayGear.cs`): `GearTable()` (every stat's slots,
  scale, Gold primary, a targeted 3-star piece against its knee), `GearWorth()` (one Gold roll of
  each stat on each Max build, against a roll of Damage or Max HP - the crit family's parity scales
  are its last lines), `CritParity()` (damage-first and crit-first Strikers against the mixed one).
  The model's targeted piece takes primary + two sub-stats of the build's best unsaturated stat in
  the slot's pool and one of the second; Accuracy saturates at 100, mitigation at the floor.
- **PlayerPower's built gear rolls through the real tables too**, so a gear-table change moves the
  wave budget: Phase 3's moved the deep-floor expected DPS ~11% down (887 -> 792 at floor 100).
- **The board in the model** (`Balance/AssayBoard.cs`): every rule the single-target model can see
  is priced (Vitriol, Cohobation, Multiplication, Realgar, Alkahest, Exaltation, Circulation,
  Tartar, the humours' burn/soak/bleed, Saltpetre, Mortification, Inceration, Congelation, Antimony,
  Solution, Alum, Fixation, Aqua Vitae, Cibation); crowd, perfect-press and dodge rules are not, and
  say so. `BoardReport()` checks coverage and the chain thresholds exactly. Archetype boards: Striker
  Strikes + Weight + Armour, Tempo Speed + Strikes + Evasion, Bulwark Armour + Weight + Sustain,
  Elementalist Element + Area + Sustain.
- **PlayerPower sees only the board's Damage and Attack Speed** (Strikes + Speed), none of its
  rules, so since Phase 4 it UNDER-prices the built player: deep expected DPS 715 at floor 100.
  Phase 7 replaces it with the Assay - until then deep waves are lighter than the board justifies.
- A cold report can exceed the 5s eval limit. The simulation finishes anyway and is cached, so call
  again (or warm `Assay.Run(...)` one build at a time). A recompile clears the cache.

## The stat core: two layers and diminishing returns

`Art/Gear/StatCurves.cs` (the rules, shared by the game and the Assay), numbers in `Tuning.Stats`.

- **Two layers, each bent by its own curve, then multiplied.** The CHARACTER layer (gear + board) is
  bent ONCE in `BuildPlayer` (`StatCurves.Character`), so every reader of `pc.Stats` gets effective
  values; Element Growth is bent as one sum with the board's meter node. The RUN layer (ledger +
  spire floor boon) is bent inside `RunModifiers.Compute` (`Vessel`), converting each stat field to
  points and back - flat bonus damage as a share of the base hit, multipliers as their excess over 1.
- **The curve:** linear to a knee, then a hyperbolic tail toward a cap (slope 1 at the knee).
  Negative points pass through - stacking costs never gets cheaper.
- **The Vessel** is the run layer's thresholds, scaled by the element's mastery level
  (`RunModifiers.SetMasteryLevel`, set in `BuildPlayer`): `VesselAtLevelZero` of full at level 0,
  full at the cap. Boons cannot carry an unlevelled character past what gear and the board give.
  Conditional boons (Executioner, Echo, Deep Cut, First Blood) still bypass it until the exchange
  rebuild re-expresses them.
- **Pricing a pair** goes through `RunModifiers.Preview(boon, cost)`, never Apply on a copy of
  `Current` - that would skip the Vessel, since `Current` is already bent.
- **Pools, capped once:** crit chance from every source has ONE cap for every element
  (`CritChanceCap`), the excess added to the crit multiplier (`CritOverflowToDamage`) so no point is
  wasted (`PlayerController.RollCrit`); lifesteal is one pool under `LifestealCap`, and
  `PlayerController.Drain` heals no faster than `LifestealHealPerSecond` of max HP (a budget that
  refills over a second); stat mitigation (element DR, ledger DamageTakenMul, armour condition,
  Resilience, Graze/Brace, Bulwark) is floored at `IncomingFloor` in `Health.Vulnerability` -
  exposed-after-a-leap and Medusa's stone skin sit OUTSIDE the floor.
- **Accuracy** (`StatKind.Accuracy`, appended): every hit's damage is a range of
  `Tuning.Attack.DamageSpread` either side of its average, rolled ONCE per swing or throw
  (`PlayerController.DamageRoll`); Accuracy raises the bottom toward the top - 100 points, every hit
  at the top. Rolls on the Weapon, Head and Gloves; capped at 100, so one or two pieces of it is
  the whole stat.
- **`StatCurves.Enabled = false`** turns every rule back into the plain sum it replaced. The Assay's
  regression check runs that way, and with it off the full matrix reproduced the 2026-10-05 baseline
  row for row - the proof the plumbing changed nothing on its own.

## The elements: head starts, hit units, one set of numbers

Every element number is in `Tuning.Elements` (moved off the resource MonoBehaviours 2026-10-05) and
was set by `Assay.CalibrateElements` for parity - no element far ahead for any playstyle.

- **Head starts are stat POINTS** (`ElementalResource.DamagePoints`/`AttackSpeedPoints`/
  `MoveSpeedPoints`/`CritDamagePoints`), joined to the character's RAW points before the character
  curve bends them (`PlayerController.DamagePointsNow` etc., `RawStats`) - an element reaches a
  threshold sooner, never past it; a penalty means more gear for the same cap. Air's crit joins the
  one crit pool, Gust's lifesteal the one lifesteal pool (`LifestealBonus`), Earth's DR sits inside
  the one mitigation floor. Nothing multiplies damage or speed on the side any more.
- **Releases are counted in HIT UNITS**: `PlayerController.HitUnit` (the player's own basic hit right
  now) x Elemental Power (`ReleaseScale`), radii x Area (`ElementalResource.AreaScale`). So a release
  grows with gear, the board and the ledger the way swings do, instead of being a flat number that
  floor 30 outgrew. Target: one release on an invested build is worth 3-5 s of the player's own
  damage (`Assay.Releases()`; 3.2-4.5 s).
- **Fire's ladder** (`FireResource`): 1 FUEL - a swing on a BURNING enemy banks two stacks; 2 haste;
  3 STOKE - heat fades half as fast; 4 splash; 5 release, ERUPT (burning pools) or IGNITE (8 s in
  which every landed hit burns for 35% of itself; no re-release inside it). Burn is NOT passive any
  more: riding every hit it added ~40% to Fire on its own. Heat is one batch per SWING, never per
  body.
- **Water's tier-3 burst lands BEFORE its soak** - the soak is for the hits that follow; dealt after
  it, the burst was 35% bigger than anything priced it.
- Parity (Max kit, no ledger): worst build spread 14% (Striker, Elementalist), Tempo 9%, Bulwark
  4% - the identity ranges (`AssayCalibrate.Knobs`) cost the last few points. Verified in play:
  Fuel/Stoke/Ignite, Erupt pools, Water's burst order and surge, Earth's quake to the decimal, Gust
  and Squall (`docs/balance/2026-10-05-phase2.md`).
- **The calibration is against the gear it was run with.** Phase 3's gear tables moved it to
  Striker 30% (Earth weakest, Air strongest), Tempo 21%, Bulwark 11%, Elementalist 18%; by the
  user's call the balance pass waits until after Phase 5, so `CalibrateElements` is re-run then.

## Waves: a budget, not a count

`Enemies/WaveComposer.cs` + `Player/PlayerPower.cs`, `Tuning.Waves`. A floor is a pool of
effective HP (health + armour) = `TargetSeconds(floor)` x `PlayerPower.ExpectedDps(floor)`; enemies
are BOUGHT from it at their real EHP (`EnemyFactory.MaxHpFor`/`ArmorFor` - the factory's own
numbers, so armour steps and HP growth price themselves in). WHICH enemies is random; HOW MUCH
killing it takes is not. It replaced a count with each kind subtracted from it in order, which
starved every late kind: Gargoyle/Bubbles never spawned on an ordinary floor, floors 35+ were 13
Ranged and a Chaser.

- **Deep floors are priced against the BUILT player**: full board, three Gold 3-star pieces plus a
  Gold 3-star weapon, boons STACKED for damage - blended in from the reference player across
  `BuiltFromFloor`..`BuiltByFloor` (10..40). Survivorship: nobody else gets there. The user's rule -
  never balance a late floor against a min-level player. A built player on AVERAGE boons takes ~2.7x
  the target deep down; that gap is what stacking buys.
- **`PlayerPower` is a model built from the game's own pieces**: real `GearRoller.BuildGrants`,
  the board read off `MasteryBoard`, and the ledger simulated through the real `ExchangeOffers`
  (200 runs, seeded, `UnityEngine.Random` state restored). Single target, melee, no element - so
  every time it gives is the SLOW end; area damage clears swarm rooms 2-4x faster. Its curves take
  ~0.5s to build and are warmed in `GameBootstrap.Start`.
- **The on-screen cap is PRESSURE, not bodies** (`WaveComposer.Fits`, hand-set per kind in
  `Tuning.Waves.Pressure*`, elites x2.5): a swarm fits a dozen, elites come two or three at a time.
  Strict queue order; the first body always fits. The pressure numbers are THE by-feel dial.
- **Composition**: each floor multiplies every unlocked kind's weight by exp(N(0, 0.7)) and draws
  its own elite share (0 to 15%..40% with depth). A kind DEBUTS as exactly one body on its unlock
  floor, then ramps in. `MinCostFraction` (0.4 Chaser) stops support kinds (Bubbles is 0.09 at
  floor 100) filling `MaxBodies` with a third of the pool unspent; near the body cap the draw only
  buys what can still spend the rest. Every third floor's guaranteed elite is PAID FROM the pool.
- **Turret is elite too** (`WaveComposer.EliteKinds`): the tier once only doubled its HP, so it was
  left out; it now grants a MOVE as well - the mire lob (see the elite table).
- **Seeded** (`FloorPlanner.WaveSeed`): a restart meets the same rooms, and `ComposeWave` is the ONE
  place a wave is composed, so a preview can't lie. `FloorPlanner.Peek` plans a floor on a COPY -
  `Plan` moves counters and spends dev-force switches.
- Rift Boxes drop at 1/500 per CHASER-EQUIVALENT of the body's cost; the Collapsing Rift's estimate
  is `TargetSeconds x ClearPerDamageSecond`.
- Testing: `unity command eval 'return Convergence.Enemies.WaveComposer.Simulate(300);'` and
  `... Convergence.Player.PlayerPower.Report();`. After a recompile the first call can hit the 5s
  eval limit while the curves build - call again.

## Bosses

`Assets/Scripts/Bosses/`. Boss floors are every tenth floor plus the four avatar floors
(25/50/75/100). **Which boss** is `FloorPlanner.BossFor`: mini-boss floors draw from `MiniBossPool`
(the Cantor and Medusa so far) as a SHUFFLE BAG seeded by the run - every boss before any repeats,
never the same one twice running, the same bosses on a restart. **The avatars are all still the
Cantor**, standing in until they are four set-pieces of their own. Testing:
`FloorPlanner.DevForceBoss` (one-shot) picks the next boss floor's boss.

**`Boss` is the shared base**: the body
(kinematic, uninterpolated, `Health` scaled by `FloorDifficulty.BossHp`, immovable, IMMUNE until a
window), the window (`OpenWindow` / `WindowSpent` / `CloseWindow` - the cap below, immunity and the
collider together), and the lifecycle (`Token`, `Cease`). `GameBootstrap` reads only `DisplayName`,
`PhaseLabel`, `Enraged` and `Health`, so a new boss is a subclass, a `BossKind`, and a case in
`Boss.Spawn` - nothing in the bootstrap changes.

**The Cantor** (floor 25): a teleporting ranged boss on an eight-slice floor, cycling CALL (fires
from slices in a random order, untouchable) → ANSWER (the SAME slices erupt as hazards in the SAME
order, untouchable) → REST (drops to the middle, stunned, the only damage window). The puzzle is
that ANSWER replays CALL and nothing states this outright — a player who watched where it stood
already knows where not to stand. The phrase regenerates every cycle (length grows per cycle,
capped at 8, no immediate repeats) so the GRAMMAR is learned, never the specific notes.

Enrage (35% HP) flips at a phrase BOUNDARY, never mid-phrase, and doubles the answer's hazard
(each note also lights its opposite slice) rather than introducing new material — an enrage that
plays different material teaches nothing with its first two minutes.

Immunity outside REST is `Health.Immune` AND the collider disabled (a zero-multiplier hit would
still run flash/knockback/durability wear for nothing). Slices are rasterised per-sector against
the actual arena rectangle (not a wedge sized to fit inside or outside it — either leaves a
mismatch between what's drawn and what damages) and the angular test is the AUTHORITY; hazards ask
`ArenaSectors.IndexAt` directly rather than trusting the sprite.

**Four rules every boss follows, so the fight survives endgame power** (max gear + a capped board
is ~4.8x the reference DPS before the ledger, which emptied the flat 840 HP inside one window):

- **A per-window damage cap** (`Tuning.Boss.WindowCap`, 0.30 of max): `Health.Floor` stops a REST
  window taking more, and the window CLOSES once it's hit (never stands open to be hit for nothing).
  100 -> 70 -> 40 -> 10 -> dead is at least four windows whatever the build; gear buys leaving
  danger sooner, not skipping the cadence. The last window may kill (`Floor` is 0 when the rest fits).
- **HP scales with floor on its OWN curve** (`FloorDifficulty.BossHp`, `Boss.HpPerFloor` 0.03),
  tracking the ledger's growth so a typical player at that depth still meets ~4 windows. Not the
  wave's 9% - that would be ten windows at floor 90.
- **MECHANIC hits are a fraction of the player's max HP** (`EruptionFraction`, a failed read),
  MITIGATED through `Take`'s Vulnerability so Graze/Brace/Resilience still count. MaxHp doesn't
  help against them, by design. Heals stay fractions too (floor reward, spire) so they stay in
  proportion.
- **CHIP damage is flat, scaled by `FloorDifficulty.Damage`** like any enemy (the Call's blasts).

A boss holds its floor independently of `_alive` (it's neither a wave enemy nor addable by a wave
spawner) — the floor-clear check in `GameBootstrap` checks both, and so does StuckWatch's `settled`.
`Cease()` must run before `Destroy`
and again in `TeardownRun`, since `Destroy` is deferred and the hazard coroutine isn't a child of
anything that stops on its own.

## Medusa: hide from her gaze

`Bosses/Medusa.cs` + `PillarRing` + `GazeShade`, `Tuning.Medusa`. A mini-boss in the middle of a
ring of 8 pillars. **She never attacks the player** - the fight is one rule, be behind a pillar when
she looks, and everything else exists to make keeping it hard. Her floor builds NO other hazards
(`BuildFight` skips `HazardBuilder` - pits and columns would argue with the ring).

    BREAK      a random pillar shakes (BreakWarnSeconds) and shatters; from floor 40 a PUSH or PULL
               lands on the same beat. Skipped on the opening cycle - the rule hasn't been seen yet
    GAZE       the arena fills RED everywhere her gaze reaches, the pillars' shadows left clear,
               then she LOOKS for GazeHoldSeconds (held, so a dash's i-frames can't step through
               it; a leap that stays airborne the whole time can). Caught: 50% of max HP through
               mitigation, then STONE for the window
    OPEN       vulnerable, capped by Boss.WindowCap like every boss. A statue misses it
    DEBRIS     warned falling circles near the player (15% max HP, one test at impact). Debris
               NEVER breaks a pillar; once half the ring is missing, the empty slots are rebuilt by
               falling pillars, each with its own warned circle

- **`PillarRing.Shadowed` is THE AUTHORITY** (segment vs. each standing pillar's circle, not a
  raycast - that would meet the player's own triggers and the adds), and the red is painted from the
  same function, texel by texel, into one bilinear 8 px/unit texture inset to the walls' inner face.
  Repainted only when `StandingMask` changes (a player breaking their own cover mid-telegraph).
- **Pillars are ordinary cracked Columns** with `Tuning.Medusa.PillarHp` (60), breakable by player
  attacks too. In FIXED SLOTS, slot 0 straight below her, so the arrival point starts in cover and a
  rebuild lands exactly where a pillar stood. `Column.Break()` shatters one directly (no damage path).
- **STONE** (`StatusEffects.ApplyStone` / `EndStone`, `PlayerController.Statue`) is NOT the
  Gargoyle's petrify (a root): no moving, attacking, releasing or defending, timers paused, and the
  rig draws a statue (`ICharacterRig.SetStone` -> `PixelSprite.Stone`, luminance onto one grey ramp,
  Update/LateUpdate frozen). Ended explicitly when the window closes. Composes with `SetFlash` - each
  ends the other first so neither restores the wrong sprite.
- **STONE SKIN**: `StoneSkinBase x FloorDifficulty.AttackInterval / FloorDifficulty.Damage`, folded
  into the player's Vulnerability - both depth curves cancelled, so adds hit a statue like floor-1
  enemies. 0.35 without the cadence term killed a 163-HP character at floor 40; see the Tuning note
  for the worst-case arithmetic (a full-health caught Air character survives three adds).
- **Adds** (floor 30+, 2 a cycle, never more than 3; Ranged mixed in from 60) are HER children, not
  the wave's: not in `_alive`, destroyed by `Cease`, they pay a kill but never a Rift Box.
- **The pull goes THROUGH the ring** (`Physics2D.IgnoreCollision` against every pillar for the drag) -
  a pull a pillar could stop never moves the player it's aimed at. Lands at `PullToRadius`, in the
  open, just before the red. Pushes collide normally. Neither does damage (a zero hit still wears
  armour and fires every listener).
- Testing: `FloorPlanner.DevForceBoss = BossKind.Medusa`, then reach a boss floor. Jumping there from
  `eval` WHILE floor 1 is still spawning races floor 1's `BuildFight`, which clears `_spawning` and
  pays the boss floor out early (a door and a Rift appear mid-fight) - a harness artefact; let
  floor 1 settle first.

## Harness gotchas when testing from `eval`

- **`transform.position` on a `Rigidbody2D` doesn't reach the physics world until the next
  simulation step.** Teleporting a test enemy and immediately triggering an attack queries
  `OverlapCircleAll` against the OLD position. Call `Physics2D.SyncTransforms()` after writing a
  transform, or write `rb.position` instead. Likewise `AddForce(Impulse)` applies on the next
  physics step — measure displacement after real frames pass, not `linearVelocity` in the same call.
- **`unity command console` can return STALE entries** indistinguishable from live ones (the tell:
  a reported line number pointing at a colour constant in the current file, not the bug). `clear_
  console` does not empty what `console` reads. Check reported line numbers against source before
  trusting a trace. Also: `Screen.width/height` from `eval` returns the Editor's LOGICAL point size,
  not the render target — use `Camera.pixelHeight`/`pixelWidth`.
- **An `eval` round trip hitches the frame it lands on, so a test armed with REAL millisecond
  windows measures the hitch, not the rule.** Arm with EXAGGERATED values (a 0.5s window against a
  0.45s gate) so the path under test is the only one that could have fired, and assert on a state
  no other path can produce.
- **`Core.Controls.VirtualAttack` only behaves as a one-frame TAP while the touch overlay is
  live** - `TouchControls` rewrites it every frame. With the overlay off (the usual Editor state)
  it STAYS true: the next "tap" has no edge and the held path fires swings. And `Controls` reads
  input lazily on first access each frame, which can land AFTER coroutines - so a press set from a
  coroutine is swallowed. For timed presses (the finisher bar), queue a real keyboard Space press
  and release with `InputSystem.QueueStateEvent` from a coroutine, two frames apart, under a slowed
  `Time.timeScale` (0.1 makes the 0.36s bar 3.6 real seconds).
- **`WaitForEndOfFrame` never resumes while the Game view isn't rendering** (Editor in the
  background) - a coroutine waiting on it sits there until something repaints the view, then fires
  late. Avoid it in harness coroutines.
- **With the Editor in the background, `eval` times out** ("Main thread operation timed out after
  5000ms") and queued input is processed late. `osascript -e 'tell application "Unity" to
  activate'` brings it forward. Entering Play mode took ~5 minutes in the timing-bar session (a
  debugger was attached), during which the pipeline reports `unreachable` - wait it out.
- **Moving the player from `eval`**: `Controls.VirtualMove` is zeroed every frame by
  `TouchControls` in the arena - hold a key with `InputSystem.QueueStateEvent` instead.
- **After leaving Play mode, `GearCatalog` still holds sprites Unity destroyed** - reading one
  throws "has been destroyed". `GearCatalog.Invalidate()` rebuilds it, but the rebuild outlasts an
  `eval`'s 5s limit: it errors, finishes anyway, and the next call reads the fresh catalogue.
- **A run writes the run-start checkpoint to the local save.** Back the save files up before
  testing a run and restore them after - and never restore an OLD backup without checking the
  files' timestamps first: the save changes whenever the game is actually played.

## One owner for the pause

`Core/GamePause` is the only thing that touches `Time.timeScale`. Screens call `Hold(this)`/
`Release(this)`, and time resumes when the LAST holder releases — reference counting, because two
screens saving/restoring the timescale independently breaks the instant they overlap (e.g. toggling
the loadout screen twice while a floor-reward screen sits underneath). `FloorRewardScreen.IsOpen` is
in the same early-input-guard as `MasteryScreen`/`ConfirmDialog` for the same reason. `TeardownRun`
closes open screens and calls `ReleaseAll`, since a run can end (death, abandon) with a screen open.

## Modal input rules

- **Every modal must be in `GameBootstrap.Update`'s early-return guard** — screens don't consume
  input (nothing here goes through `EventSystem`; each hit-tests `Mouse.current` itself), so a
  screen missing from the guard lets clicks meant for it fall through to gameplay underneath.
- **A modal opened by a key must ignore its opening frame** — `wasPressedThisFrame` stays true for
  the whole frame, so a dialog that both opens AND treats the same key as cancel needs an
  `_openedFrame` guard or it closes itself on the frame it opens.

Input verification: `simulate_key`/`simulate_pointer` do NOT reach the new Input System — inject at
the device level with `InputSystem.QueueStateEvent`, press and release in separate `eval` calls so a
frame passes between them.

## Touch input

`Core/Controls` is the ONLY thing in the project that touches an input device — everything else
asks for an ACTION (a direct `Keyboard.current` read silently breaks the game on a phone).

- Edges (`wasPressedThisFrame`) are computed IN `Controls` from the union of physical+virtual input,
  not read off the device — half these actions can arrive from a touch button that has no such
  concept.
- Device detection runs separately from the frame snapshot (`[DefaultExecutionOrder(-500)]`) or the
  overlay's own touch input would be read one frame late.
- Last device wins, on HELD state (not an edge) — a touch turns the overlay on, a key/mouse turns it
  off, so a touchscreen laptop and the Editor both just work.
- Fingers are claimed by id (a stick owns the finger that started it even if it wanders; a button
  holds until its own finger lifts) so steering and attacking work simultaneously.
- The dead zone (0.01) is real, not cosmetic — Air's momentum reads the same "is moving" flag a
  resting thumb would otherwise satisfy.
- One BACK button drives every modal's Cancel, hidden for screens that must be answered
  (`FloorRewardScreen.CanDismiss`).
- `Controls.Screen` has ONE owner (`GameBootstrap` asks `HubRoom.Placing`) — two Updates writing it
  independently flickered the button set every frame.
- Hint text follows the LAST DEVICE USED (`UiKit.Hint(keyboard, touch)` + `HintSwap`), not a
  build-time choice — the mode can change mid-session.
- `TouchControls._buttons` is non-readonly and rebuilds if found empty (see Domain reload traps).
- **One HUD on every device.** The action buttons draw off touch too, as READOUTS (no stick, claim
  no input, carry the key/pad glyph, light while it's held - `PaintReadouts`); the modal BACK stays
  touch-only. So `Hud`'s swing panels live top-right on every device - there's no layout switch.
- **Landscape only** — the arena is 24×14 against a ~17×9.6 camera view; portrait would show a third
  of the fight. `PlayerSettings` refuses both portrait orientations.

`UiKit.TouchTarget` (128 canvas units) is derived from the SMALLEST-scaling device in the support
matrix (a small phone at 0.347pt/unit), not a round number — 44pt/48dp targets need different unit
counts on every device. **A press cannot commit on a page that scrolls** — the pick fires on RELEASE, and
only if the finger didn't travel far enough to have meant a drag, or every scroll attempt also
changes whatever's under the finger.

The mastery board opens at a DEVICE-DEPENDENT zoom (`DefaultZoom = TouchTarget / Pitch`, both
measured off the live grid) rather than the whole-board fit — the tightest node spacing at fit-zoom
is well under a finger's width. Zoom happens about the POINTER (`pan' = pan*k + focus*(1-k)`, only
equal to the simpler `pan -= focus*(k-1)` when pan is already zero — using the simpler form after
the first pinch makes the board visibly lurch). A pinch is computed as the LOG of the finger-
separation ratio (matches what a pinch physically means: double the separation, double the size)
and is DERIVED every frame in `Controls`, never latched as a field (a latched "pinching" flag that
sticks true is a board that can never be panned again).

## The mastery board: branches, notables, the principle chains

`Progression/MasteryBoard.cs` (the layout, `Branches` table, words), `BoardState` (what is owned,
per element), `BoardEffects` (the rules, running), numbers in `Tuning.Board`. Rebuilt 2026-10-05
(board VERSION 2 - every node id starts "v2.").

The board is TWO layers riding on one grid:

- **Domain forks** - Source (Strikes / Weapon Arts / Element), Status (Burn / Soak / Bleed /
  Stagger), Survival (Armour / Evasion / Sustain), Tempo (Speed / Weight), Space (Range / Area), and
  the Rift ladder. A branch runs KEYSTONE (4) - five fillers - TINCTURE (2) - six fillers - OPUS (4),
  21 levels. Buying a keystone closes the domain's other branches on that board **permanently**
  (only reincarnation reopens them): their keystones lock, and a branch's fillers are reached only
  through its own keystone, so the decision is the whole branch.
- **Principle affinities** - every node carries points in exactly one of Sulfur/Mercury/Salt
  (filler 1, Tincture 2, keystone and Opus 3); crossing a threshold (8/16/22/28) unlocks the next
  link of that principle's chain.

**Every node speaks gear's language.** Fillers and keystones add StatKind points (joined to gear's
before the character curve - `BoardState.Points`) or a board-only stat (`BoardStat`: lifesteal,
status POWER, Rift capacity). A branch's primary gets the keystone's three units and six fillers,
its secondary five: one unit is a 27th of the stat's character knee (so a full branch gives a third
of the knee) or, for a stat with no knee, a quarter of a Gold armour primary (`MasteryBoard.Unit`).
Range's secondary is Cleave, read as Pierce on a bow. Max HP is a PERCENTAGE now (the flat HP
nodes are gone), so Air and Earth keep their health ratio.

**Tinctures, Opuses and the humours are RULES** (`Notable`, 32 of them), each with its words
generated from `Tuning.Board` (`MasteryBoard.Describe`). The four Status keystones are the humours -
Choler, Phlegm, Sanguine, Melancholy - and let ANY element leave that status (burn, soak, bleed,
stagger); the status's POWER also strengthens the element's own (Fire's Ignite and pools, Water's
soak, Earth's quake). The full table is in `docs/balance/2026-10-05-phase4.md`.

- **One set of hooks for every hit path**: `BoardEffects.ModifyOutgoing` (target-dependent
  multipliers) and `OnHitLanded` from the melee arc (`ResolveArc`), the thrown disc, the arrow and
  the thrown blade (`PlayerController.ScaleThrownHit` / `NotifyThrownHit`); `OnBasicLanded` once per
  basic swing or throw; `OnStrikeSettled` at the timing bar (Cohobation's steep, Cinnabar's crit);
  `OnReleased`, `OnAreaAttack`, `OnEnemyDied` (from `HookDeath` - any cause of death),
  `ModifyIncoming` (chained LAST after the ledger and the principles) and `Health.ScaleIncoming`
  (info-aware - Solution needs to know who struck).
- **Statuses the player applies go through `PlayerController.Burn/Soak/Stagger/Bleed`**, which route
  through the board when there is one - so power and the status rules reach every source. A new
  status source must use them, or it silently skips the board.
- **Conditional damage and speed are POINTS** (Tartar, Exaltation, Circulation), joined in
  `DamagePointsNow`/`AttackSpeedPointsNow` like an element's head start; only target-dependent rules
  multiply.
- **`StatusEffects` composes Vulnerability** (soak x congealed) - never write `Health.Vulnerability`
  for an enemy from anywhere else. Antimony slows a staggered body's attack COOLDOWNS
  (`EnemyController.AttackTempo`), never a telegraph.
- **Owned rules are a bitmask** on `BoardEffects` (a HashSet would come back empty after a reload).

**The Rebis** - the second-ability switch - is its own node at the centre: cost 1, no principle, any
build reaches it, it locks nothing, and it is HIDDEN for an element with one ability
(`BoardState.Visible`). A second click on the owned Rebis switches which ability the release fires.

**The cap of 50 covers 42% of a board** (119 buyable: five 21-level branches, the Rift's 13, the
Rebis). Spent toward one principle it completes that chain (Sulfur 29, Mercury 31, Salt 30 against
28) and leaves ~10 for a second - one finished, one tasted, never two: `Assay.BoardReport()` proves it
exactly over every legal spend.

**Migration** (`BoardState.Migrate`, at load): a profile below `MasteryBoard.Version` has every old
purchase dropped (its levels return - `Spent` counts only nodes that exist), the old second-ability
choice cleared, and a note (`MasteryProfile.BoardNotice`) shown at the top of the mastery screen the
next time it opens. Version 1's refund is counted off the old ids (keystone 4, filler 1).

**Sulfur** (offense): basics stack a mark (from swings AND throws - Season used to be swing-only); a
release detonates every mark for 0.4 of the player's own hit each (`HitUnit`), gaining a splash,
scaling with how many marks were active. **Salt** (defense): hits taken stack Ward; at full Ward the
next hit taken reflects onto nearby enemies, scaling with the character's Resilience. **Mercury**
(mobility): moving distance stacks a charge; at full charge the next hit ignores armour and refunds
a speed burst. All three trigger off UNIVERSAL countable events, so one principle layer sits under
all four elemental boards without favouring any kit.

**Boards are active only for the element being played** - global XP funds all four independently,
so an always-on bonus would otherwise stack four ways. **Reincarnation** (~$1) resets ONE element's
board. Seasoning marks are a COMPONENT on the enemy, never a Dictionary on the player. Links and
rules are read ONCE at run start (mastery is spent only in the main menu).

## The Rift: extraction over pure loss

`Assets/Scripts/Rifts/` — `Rift`, `RunLoot`, `HubGlimpse`, `UI/RiftScreen`. A Rift is a tear in the
multiverse (an avatar's proximity thins reality), not a facility — appearing before each avatar boss
(25/50/75/100), after each one is defeated, and at every ten-floor mini-boss interval.

Loot is either CARRIED (at risk — lost on death or on leaving without reaching another Rift) or
SECURED (banked, safe even on death). At each Rift the player either **extracts** (leaves with
everything carried) or **pushes on**, securing only what fits through the Rift's capacity (base 1,
up to 3 via the Rift-capacity mastery domain) — chosen item by item, since the choice (a guaranteed
common vs. gambling the diamond still in the bag) is the point.

**XP and mastery are untouched by either outcome** — only the tradeable stake is at risk; death
never costs progression, since the daily taper (below) already governs XP.

**Rift Boxes** are a findable-only consumable (never purchasable — buying loot security with money
is the one thing this economy is built not to allow) that secure one item beyond capacity. Drop rate
is per-ENEMY (1/500 per Chaser-equivalent of its wave cost), not per-floor, so yield naturally scales with how deep a run goes. Must be
TAKEN via interact (not walked over) and COLLAPSE after 10s if left — both exist so the pickup
costs a real beat mid-fight rather than being free once the floor is cleared at leisure. Two pools:
boxes FOUND this run are at risk like carried loot; a BANKED reserve (found on earlier runs) is
untouched by death — spending order is fixed (free capacity, then found, then banked; no player
choice, since each step has exactly one right answer).

**Loot drop tiers are BANDED at boss floors**, not smoothed — a visible jump in what a floor pays is
what makes clearing a boss feel like it unlocked something. Diamond is reserved entirely for floor
100 (the completion prize). Black Diamond drops only from avatar bosses (0.5% at 25/50/75, an
ADDITIONAL drop on top of normal loot; 1% at 100, INSTEAD of the guaranteed Diamond) — the doubled
rate at 100 is deliberate: a flat rate across all four bosses would make short repeated runs to the
first avatar the efficient farm for the rarest item in the game, which is backwards.

**The server issues a STICKY run seed** — restarting an unfinished attempt returns the identical
seed, closing the reroll-scumming path without needing an abandonment rule.

## Floor planning and Rift kinds

`Rifts/FloorPlanner.cs`, `Tuning.Floors`, `Tuning.CollapsingRift`. Boss floors are fixed (every
tenth, plus avatars 25/50/75/100); the ONLY guaranteed exit is the Blue Rift after avatars 25/50/75.
Every other floor is drawn by weight - combat, Rift, puzzle - and a category's weight grows for
every floor it has been missing. Past `MaxRiftGap` eligible floors without a REACHED exit, the next
is a Blue Rift outright (never Collapsing - a guaranteed exit you can fail is no guarantee).

- **Rifts stand BESIDE the exit door** - the door is pushing on, the Rift is getting out. The Rift
  screen opens on [ E ] at the tear (never on contact) and NOT YET / Escape closes it with the tear
  still open, so a player who wants to keep fighting is never stopped. Free capacity used is held
  on the `Rift` (`Secured`), not the screen, or every reopen would refill it.
- **The floor's drop is rolled AT THE CLEAR** (`RollFloorDrop`, before the Rift settles), or
  extracting forfeits the floor just earned.
- **A failed Collapsing Rift opens a QUIET stretch** (`MarkRiftCollapsed`, 2-3 floors, seeded): no
  Rift of any kind, the gap limit included - an exit on the very next floor would make the failure
  free. The Rift count keeps climbing through it. Avatar Rifts stay guaranteed regardless.
- The Rift check in `Update` sits OUTSIDE the floor-clear chain: inside it, a Rift existing
  mid-fight blocked the floor clearing.
- **Seeded per floor** from (run seed, floor) - `_planner` is created at run start from a local
  stand-in for the server's sticky seed. The counters depend on play, the draw never does.
- **Collapsing**: torn unstable as the first wave arrives, untouchable, counting down in GAME time;
  clear the floor in time and it stabilises (`SettleFloorRift`), else it implodes like a Rift Box.
  Only a stabilised one resets the planner's Rift count (`MarkRiftReached`). The clock is the
  player's median pace over `PaceSamples` ordinary floors x `Margin`, clamped to a band around the
  floor's estimate (anti-sandbagging). Pace is stored as a RATIO to each floor's estimate, so small
  early floors predict bigger later ones. `Margin` is the number to tune by feel.
- **Red**: torn SHUT and DORMANT as the first wave arrives (`Rift.Seal` - steady, ember rim on a
  slow beat, the hub darkened behind it), farther off than an open tear (`RedTearDistance`).
  Nothing comes until the player chooses: [ E ] at the tear (`Rift.PlayerNear` works while sealed)
  opens a confirmation, and only "Break it" summons its `RedGuards` elites (+1 from floor 50), past
  the on-screen cap (`SummonRedGuard`). Broken BEFORE the clear, the guard joins `_alive` and holds
  the floor, so the clear opens the Rift; broken AFTER, it stays out of `_alive` (it must not hold a
  cleared floor), the Rift opens the moment it falls (`TryOpenRedRift`, polled), and leaving through
  the door ends it (`CloseRift` destroys a live guard - enemies never clear themselves out). Never
  touched, it stays shut: no exit that floor, nothing lost. Only an opened one counts as reached. No
  extra reward, by decision (luck of the draw). Never on a boss floor.
- **Play-testing a kind**: `FloorPlanner.DevForceCategory` / `DevForceRift` / `DevForcePuzzle`
  (statics, one-shot) make the next non-boss floor that kind - see the doc comment for the evals.
- **Puzzle floors** - see the section below. Weighted like the Rift (`PuzzleBase`/`PuzzleStep`,
  from `PuzzleMinFloor`), never on a boss floor; ~8.5 a run.
- `FloorPlanner.Simulate(runs, collapsingSuccess)` reports the floor mix, gaps between exits and how
  often a base stake (gate 20) finds an exit before floor 25, and checks no non-avatar exit lands in
  a quiet stretch. At the starting weights: ~15.2 exits a run, mean gap 6, p90 9, base stake 64%;
  a weak player (40% of Collapsing beaten) gets ~14.9, p90 10, base stake 63%. Rift shares Blue
  0.45 / Collapsing 0.35 / Red 0.20 - about 2.4 Red a run.

## Puzzle floors

`Puzzles/` (`PuzzleRoom` + `PuzzlePlate`, three kinds), `Tuning.Puzzle`, wired in `GameBootstrap`
(`BuildPuzzleRoom`, `TickPuzzle`, `AdjacentRoom`, `ClearPuzzle`). No wave, no hazards. The floor's
exit stands where it always does, SHUT (`FloorDoor` `shut:` / `Open` / `Seal`); an OPEN side door
stands far left or right (from the seed). Solve -> the door parts, becomes `_activeDoor`, and
`OfferFloorReward` runs exactly as for a cleared floor (drop, exchange, reward, stake gate). A wrong
answer SEALS the door (mark goes dead). Through the side door - giving up or after failing - is the
ADJACENT ROOM: the same floor's ordinary fight (`BuildFight`, split out of `NextFloor` for this),
floor number unchanged.

- **Echo** - watch and repeat, the Cantor's call/answer grammar on a floor where failing costs a
  fight. Centre stone plays the call (replayable until the first answer); one wrong stone fails.
  Stones differ by PIP COUNT as well as colour.
- **Elements** - four element stones, an order deduced from clues. The clue set is generated
  minimal against all 24 orders (`ElementsPuzzle.Check` verifies every set pins exactly the
  answer); from floor 50 no "is first/last" clues. Stone positions shuffled. One wrong stone fails.
- **Lights** - 3x3 lights-out. Scrambled from all-lit by k distinct presses (3x3 is invertible, so
  the scramble is the unique shortest answer); budget k + `LightsSlack`. Running out fails it -
  there is no single wrong stone. Walking THROUGH a stone presses it.

The puzzle never knows about doors or rewards - `GameBootstrap` POLLS `State` (a delegate would
not survive a domain reload). All randomness is from `FloorPlanner.PuzzleSeed`, so a restart
meets the same puzzle; `PickPuzzle` never repeats the previous kind. A puzzle holds its floor like
a boss (`_puzzle == null` is in the clear chain and StuckWatch's `settled`). Stones press on the
step ONTO them, and every stone's edge is read every frame even while input is ignored.

## The gear stake

`Chain/GearStake.cs` (rules), `UI/StakeScreen.cs` (asked at the sigil door), `Tuning.Stake`. The
player may put ONE equipped stat-bearing piece on a run: clear its gate floor and extract, and they
leave with a PARTNER - a piece matching the stake's `GearForge.MatchKey` (slot, tier, stars,
primary, class/ability) with fresh sub-stats, for the Forge to combine. Extract before the gate and
the stake comes home; die at any point and it is destroyed.

- **Statted gear only, because it does not trade.** Risking a tradeable piece would be wagering an
  asset with a price. `Stakeable` IS `GearForge.Combinable`, so a piece that couldn't use its
  payout can't be staked (Gold 3-star, relics, Diamond/Black Diamond are all out).
- **Gates scale with stars** (20 / 25 / 50, and 75 for a 3-star whose partner PROMOTES it). A flat
  gate is a farm.
- **The payout never rides the run** - not carried loot, never in `RunLoot` or a Rift's capacity.
  It is minted at the run-end checkpoint only if the run ended EXTRACTED with the gate cleared.
- **Settled in `EndRun` off `survived`**, which only extraction passes. Any new extraction system
  must keep ending an extracted run with `EndRun(survived: true)` and the stake follows.
  `_stakeGateCleared` is set in `OfferFloorReward` (a CLEARED floor), not read off `_floor`.
- **The stake is in the DATUM** (`CharacterProfile.StakedInstanceId`), written before the run-start
  checkpoint. A stake found on the profile at LOAD is from a run that never ended and is forfeited
  (`ForfeitStale`) - harsh on a crash, deliberately: otherwise quitting is the free exit from every
  losing fight. Forfeited on load, not at the next run, so it can't be combined away in between.
  Stopping Play mid-run in the Editor counts too.
- The number-key shortcut in the hub starts unstaked; only the door asks.

## The daily reward taper

`Tuning.Taper`, counted on `AccountProfile.FloorsClearedToday`, applied at the floor-reward moment
(the instant a floor is genuinely completed — a run ending mid-floor accrues nothing for it).

**Applies to XP ONLY, never loot** — loot is the run's STAKE (already throttled by risk, depth-
gating, and tier-gating three ways over); tapering it too would tax a player who just lost a deep
run's carried items on top of having lost them. XP is untouched by outcome (banked on death exactly
as on extraction) so it needs the taper to keep an account from grinding mastery without limit.

Flat rate for the first 100 floors (a full clear) per day; past that, `100/n` (harmonic, not
exponential — settles at a floor of 0.25x rather than decaying to nothing, so a long session is
never told to stop). Account-level (not per-character), UTC rollover.

## Bug reporting

Automatic stuck-state detection (no living enemies + no pending floor transition for N seconds, or
no damage dealt/taken for a long window) is worth building independent of manual reports — most
players who hit a softlock just close the app. A manual report should carry a full state dump
(floor, element, weapon class, loadout, active exchange effects, alive enemy count, player position,
recent console output, run seed) for cases the detector can't classify.

## Chain integration status — READ BEFORE TOUCHING THE CHAIN LAYER

**>>> PICK UP HERE <<<** The read path is built and verified against a stub; the service
(`web/service/`, Express + Lucid Evolution) is Railway-ready. Nothing has been called against real
Cardano yet.

Next session, in order:

1. Put a Blockfrost **Preprod or Preview** key into Railway (or local `.env`) as
   `BLOCKFROST_API_KEY` + `BLOCKFROST_URL`. Reads need nothing else — no seed, no funded wallet.
2. `curl /read/health`, confirm `network` reads `Preprod`/`Preview` and `healthy` is true.
3. `curl "/components?address=addr_test1..."` against any testnet address holding a token — closes
   the last untested link in the read path.
4. Only then: point `window.COALESCENCE_SERVICE` at the deployed URL, add the game's origin to
   `ALLOWED_ORIGINS`, and watch `WalletInventory` fill from a real wallet.

**Do not start the write path until step 3 has returned real data.**

### What's built

Reads are split from writes so they need no service-wallet seed (`src/blockfrost.ts`). The service
address is DERIVED from the seed (`GET /service/address`), never configured directly, so nothing can
drift out of sync with what actually signs. `/read/health` reports network derived from the
Blockfrost URL, not the `NETWORK` env var, so a key/network mismatch surfaces as a wrong name on a
health check rather than as silently-empty reads.

`/components?address=` maps every token an address holds to the game's `OwnedComponent` (wrapped in
`{"items": [...]}` — `JsonUtility` can't deserialise a top-level array). Capped at 60 assets per
call to avoid a single request exhausting the rate limit; `truncated` says when it happened. A
failed read returns 502 (service is fine, Blockfrost didn't answer) — the game deliberately KEEPS
its previous inventory on a failed refresh rather than emptying it, so this status code is the only
place the failure is visible at all.

**CORS defaults CLOSED** — `ALLOWED_ORIGINS` unset permits nothing cross-origin. The game is a
browser build calling a separately-deployed service, so every connector call is cross-origin;
without this the browser silently blocks all of them with no error the game can see. Origins are
matched exactly and echoed (never reflected — reflecting whatever arrives is `*` in disguise), with
`Vary: Origin` set.

Still unimplemented: `/profile/:id`, `/profiles?address=`, `/account/:id`, `/pfp/:profileId`,
`POST /auth/prove` — each needs the CIP-68 datum design settled first.

## Server authority and the chain economy (design, mostly unbuilt)

**What's actually proven on-chain is OWNERSHIP, not that a claimed event happened.** A player-signed
mint/redemption is secured by the ledger itself. A SERVICE-signed mint only records that the service
was willing to vouch for a client's claim — the attack surface is "can someone make the service sign
a true-looking transaction about a false event," not "can someone forge a transaction."

**Adopted split**: the server owns identity (`ChainWallet.Proven`, not merely `Connected`),
authoritative mastery state, sticky seed issuance, and — critically — the **loot roll itself**. The
client owns playing. The server never simulates combat, but a compromised client can only REQUEST a
roll, never dictate one — even a fully cheated client extracts a random reward at the account's
current taper rate, never a chosen one.

**Escrow and minting**: unclaimed loot mints in a periodic batch job directly into a company-
controlled wallet, tagged `mintedBy = playerId` — real on-chain tokens with provenance, not a
database promise. Claiming is a transfer the player pays for; a claim never expires (a genuine
standing liability against the company wallet, run with ledger-practice discipline: traceable
batches, reconciliation, tested backups). Only DIAMOND and BLACK DIAMOND boxes need this — both are
tradeable; every lower tier mints/redeems as an ordinary fungible server-rolled count with no
per-item identity to track, which is also free anti-farming pressure (a bot farming common boxes
extracts zero tradeable value regardless of volume).

**Not GitHub Actions for the minting worker** — a production signing key doesn't belong in a repo
secret reachable by any workflow run or third-party action. A small scheduled worker on ordinary
infrastructure (Railway/Fly/Lambda) with a limited-fund hot wallet, separate from treasury, with
policy limits on the signing path.

**Platform framing**: EGS uses purchase as the access gate (Epic Online Services gives a free
authenticated account, so progression/loot can be server-owned with zero wallet requirement). itch.io
has three tiers ascending: no wallet (local-only, nothing recorded), wallet connected (free-to-play,
server-recorded progression, drops never sent to the wallet directly), wallet with a minted PFP
(full access). The PFP is both the access gate and the deliberate trophy at once — updating it on
your own terms is what the gate asks for too; no separate "first mint vs. later trophy" split is
needed.

## NFT item images: 3200 x 3200, 10x, one multiplier for everything

Decided, not yet built. Every minted item's image is its MENU art at a fixed **10x**
nearest-neighbour, centred on a **3200 x 3200** canvas.

- **One multiplier across the whole catalogue**, never scale-to-fit — a greatsword is really
  taller than a ring, in the wallet as in the game, and the collection reads as one set. The
  largest menu art today is 208 x 316 texels (the Hellspawn Cape's width, every greatsword's height
  since the handles were lengthened), so at 10x the tallest item is 3160 on a 3200 canvas - FOUR
  texels of headroom. Menu art past **320 texels** on either axis no longer fits — grow the canvas
  for everything rather than shrinking that one item.
- **Square**, because marketplace grids thumbnail square; a tight crop makes the grid ragged.
- **Whole-number scale only.** Wallets and marketplaces resample with smoothing, so a small image
  shown large is blurred — ship it big and let them scale DOWN, which stays crisp.
- **The native 1x PNG ships too** (`files[]` in the CIP-68 metadata; the 10x render is `image`, on
  IPFS). The 1x is the source of truth — a few KB, re-renderable at any size, and what an
  arena-vs-menu drift check compares against.

Minted pieces need HAND-AUTHORED menu art: `PixelDetail.Enrich` output is fine for the character
screen and reads as an auto-shaded upscale to a collector, so derived menu art (every armour
slot, Lumen, Obsidian today) is a placeholder for anything sold. The arena grid and the menu art
must agree at the SILHOUETTE — a sold image that stops matching the item in play is a broken
promise. Still open: whether a multi-layer piece (a pauldron pair, a disc pair) mints as its
composed look, and whether the canvas carries any background/nameplate or the bare sprite.

## One character, one NFT

The character-progression NFT and the player-minted PFP are ONE CIP-68 asset per character, not two
— selling it sells a build and its portrait history together. Escrow (unclaimed loot) stays at the
ACCOUNT level, separate from the character NFT.

**Consolidation removes the roster's reason to exist** — `Hub/CharacterCouch` and the walk-up seat
switch are cut (their job was switching to a different build, which reincarnation on one character
now does more cheaply). The couch's spot becomes a weapon/armour showcase.
