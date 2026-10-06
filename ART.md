# Adding art

Everything you see is a procedural placeholder generated at runtime by `Spr.cs`. Replacing it
does **not** mean editing that file — it means filling in a catalog.

## Why there's a catalog instead of drag-and-drop

The scene deliberately holds no asset references so the Unity CLI can regenerate it (see
`CLAUDE.md`). That means there is no GameObject in the scene to drag a sprite onto. Instead one
ScriptableObject at **`Assets/Resources/GameArt.asset`** is loaded by path at runtime and is the
single seam between authored art and the code-built world.

Select that asset and its slots appear in the Inspector. If it's ever missing:
*Convergence ▸ Create Game Art Asset*.

**Every slot is optional.** A blank slot falls back to its placeholder, so you can add one piece
of art at a time and the game keeps running the whole way.

## The one rule

> Gameplay lives on the root at scale 1. Everything visible lives on a child called `visual`.

`ArtBinder.AttachVisual` builds that child. Rigidbody2D, colliders, `Health`, controllers and
elemental resources stay on the root — so an artist's prefab can carry its own Animator,
particles and child sprites without ever colliding with gameplay code.

Never put gameplay components on an art prefab.

## Camera framing

The arena camera is `CameraSize 4.2` (view 8.4 world units tall), which puts a ~1-unit character
at **12.5% of screen height**. It was 10.2 / 5.1%, where twelve gear slots came to roughly 4px
each on a 1080p display. The arena is deliberately larger than the view (`12 x 7` half-extents)
so the camera still pans.

Combat framing can still never do gear justice, which is what the **loadout screen** is for
(`C` or `Tab`, pauses the game): it renders a second rig through its own camera into a
RenderTexture at whatever size the UI wants.

## The player is a paper-doll, not a sprite

The player character is a **humanoid rig assembled from layers** (`Art/Gear/CharacterRig.cs`), not
a single sprite — because the economy requires visible, swappable gear across **12 slots**.

That count is fixed by the design, not chosen: the arena-economy doc prices a "full 12-piece gold
set" at 5 boxes x 12 items. And every slot is one that reads visually, deliberately — diamond and
black diamond items are *purely cosmetic and NFT-backed*, so a slot the player cannot see is worth
nothing at the exact tier the collectible economy depends on. No hidden ring/trinket slots.

```
GearSlot:  Helmet  Shoulders  Chest   Cloak
           Gloves  Belt       Legs    Boots
           MainHand  OffHand  Aura    Emblem
```

The rig paints 21 `RigLayer`s back to front. Boots, gloves and shoulders each occupy **two**
layers (front and back limb) — a boot on one foot reads as a bug, not as art.

### Making a gear item

`Create ▸ Convergence ▸ Gear Item`, then put it in `Assets/Resources/Gear/`.

| Field | Meaning |
|---|---|
| `ItemId` | **Stable identity.** This string is what gets written into the character datum — never rename it once live |
| `Slot` | one of the 12 |
| `Tier` | Silver / Gold / Diamond / BlackDiamond |
| `Power` | stat roll. Diamond tiers stay **0** — they're cosmetic by design |
| `Layers[]` | which `RigLayer`s this item paints into, each with a sprite, offset, size and tint |

`Size` is in **rig units** (the body is ~1 unit tall) and the sprite is scaled to hit it whatever
its resolution or PPU — same forgiveness as `WorldHeight` elsewhere. One item can paint several
layers: a chest piece might cover `Chest` + both `Shoulders` layers.

While `Assets/Resources/Gear/` is empty the catalog falls back to `DemoGear` — twelve
runtime-generated items across all four tiers, so gear swapping is demonstrable and testable
before any art exists. Authoring one real item switches the whole catalog over; nothing in
`DemoGear` needs deleting.

### Equipping

Gear is permanent and account-level, so the loadout lives in the character datum as
**slot -> item ID** (`Chain/Loadout.cs`) — IDs only, never embedded item data, matching the
on-chain handoff. Equipping goes through `GameBootstrap.Equip(slot, itemId)`, which repaints the
rig and writes an **equip checkpoint** — the "permanent unlocks" write point named in the
roguelite doc, alongside run-start and run-end.

## The PSD Importer + 2D Animation pipeline

This is the destination for real art, and the code path already exists. Two rigs implement
`ICharacterRig` and `CharacterRigFactory` picks between them:

| Rig | When | Gear swap mechanism |
|---|---|---|
| `PrimitiveCharacterRig` | no art yet (current) | SpriteRenderers this code creates |
| `SpriteLibraryCharacterRig` | `GameArt ▸ Elements[n] ▸ RigPrefab` is set | `SpriteResolver.SetCategoryAndLabel` |

Gameplay only ever calls `Apply(Loadout)` / `PlayAttack()`, so dropping in a rig prefab switches
the whole pipeline with no other change.

### Authoring one character

1. **One PSD per character, one layer per body part.** Name layers for the parts they are:
   `Head`, `Torso`, `ArmFront`, `ArmBack`, `LegFront`, `LegBack`. Keep every layer on the same
   canvas so their relative positions are correct.
2. **Import with the PSD Importer.** In the importer settings turn on **Character Rig** and set
   **Mosaic** on. Unity generates a prefab whose hierarchy mirrors your layers.
3. **Rig it in the Skinning Editor** (`Window ▸ 2D ▸ Sprite Skinning Editor`): place bones, bind
   the geometry, weight it. This gives you skeletal animation rather than swapped frames.
4. **Add a `SpriteLibrary`** to the prefab root, pointing at a `SpriteLibraryAsset`.
5. **Add a `SpriteResolver`** to every part that gear can cover.

### The contract the rig must satisfy

`SpriteLibraryCharacterRig` finds resolvers **by category name**, so:

- One `SpriteResolver` per gear slot, with the **category named exactly after the slot** —
  `Helmet`, `Shoulders`, `Chest`, `Cloak`, `Gloves`, `Belt`, `Legs`, `Boots`, `MainHand`,
  `OffHand`, `Aura`, `Emblem`.
- Every one of those categories needs a label called **`none`** — an empty or fully transparent
  sprite. That is the unequipped state, and unequipping is just resolving back to it.
- Each `GearItem` then fills in **`Category`** (the slot name) and **`Label`** (the variant,
  e.g. `gilded`).

Anything missing is logged as a warning and skipped rather than throwing, so a half-rigged
character still runs and you can build it up slot by slot.

### Gear authored separately from the character

You usually don't want every item baked into the base character's library. Set a `GearItem`'s
**`GearLibrary`** to its own `SpriteLibraryAsset` and the rig calls
`SpriteLibrary.AddOverride(...)` at runtime, injecting those sprites into the live library before
resolving. That keeps each item shippable on its own — which matters when items are separate NFTs
that can be added after launch.

### Animation

Put an `Animator` on the rig prefab. `SpriteLibraryCharacterRig` drives it **if the parameters
exist** — `Speed` (float, movement speed) and `Attack` (trigger). Neither is required.

### A gotcha worth knowing

`Arena.unity` stores serialized values for `GameBootstrap`'s public fields. Changing a default in
C# does **not** update the already-saved scene — rebuild it:

```bash
unity command eval 'return Convergence.EditorTools.ArenaSceneBuilder.BuildArenaScene();'
```

## Slots

| Slot | Takes | Notes |
|---|---|---|
| `Elements[]` | one entry per element | Fire / Water / Earth / Air. A `Prefab` here replaces the whole paper-doll rig — use only for a fully authored character |
| `Enemy`, `Elite` | actor art | Elite's placeholder gold ring disappears once it has real art |
| `FloorSprite` | sprite | Set `TileFloor` to repeat it at `FloorTileSize`; otherwise it's stretched to the arena |
| `WallSprite` | sprite | |
| `HideGrid` | bool | The placeholder grid auto-hides as soon as a floor sprite is set |
| `BurstSprite` | sprite | Ability bursts (the expanding ring) |
| `HitSprite` | sprite | Hit sparks |

### Each actor slot

- **`Sprite`** — the simplest path. Start here.
- **`WorldHeight`** — how many world units tall the sprite should be. The binder scales to hit
  this regardless of the source resolution or Pixels Per Unit, so you never have to match art to
  an arbitrary import setting. Current placeholder sizes: player 0.85, enemy 0.72, elite 1.15.
- **`Tint`** — leave **white** for finished art. It exists so placeholders can carry the element
  colour; tinting real art usually just muddies it.
- **`Prefab`** — takes priority over `Sprite`. Use once you want animation. Its own transform
  scale is used as authored, so size it in the prefab.

## Order I'd do it in

1. **The body first, then one gear piece.** The rig's placeholder limbs are what everything else
   sits on, so their proportions set the scale for all 12 slots. Get the body right, author one
   helmet, confirm it lands where you expect, then fan out.
2. **Enemy + elite.** Readability at a glance matters more than detail — they arrive in groups.
3. **Floor and walls.** This changes the game's whole look for two slots. Turn on `TileFloor` for
   a repeating tile, the normal shape for 2D art.
4. **Effects.** `BurstSprite` and `HitSprite` are shared by all four elements, so they're a big
   visual win for two assets.
5. **Animation, last** — see below.

## Animation

Switch from `Sprite` to `Prefab` when you're ready. The project already has the **2D Aseprite
Importer**, so a layered `.aseprite` with tagged animations imports as a prefab with clips and an
Animator already wired — that's the least-effort path.

`ActorVisual` is added to every visual child automatically and drives the Animator **if the
parameters exist** — all optional, nothing breaks if they don't:

| Parameter | Type | Driven with |
|---|---|---|
| `Speed` | float | current movement speed in units/sec |
| `Attack` | trigger | fired on each attack swing |

Sprites are also flipped horizontally to face travel direction — the player faces the cursor,
everything else faces its velocity. Turn off `FlipToFacing` on `ActorVisual` for symmetrical art
or art drawn facing both ways.

## What is NOT in the catalog yet

The **HUD** (`UI/UiKit.cs`, `UI/Hud.cs`) is still code-drawn rectangles using legacy uGUI `Text`,
and is not covered by `GameArt`. It's deliberately separate: skinning the UI means bringing in
TextMeshPro and real fonts, which is its own pass. The four resource meters are the part worth
designing properly — they're the main read of what makes each element different.
