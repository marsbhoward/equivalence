# Phase 5 — the exchange catalogue (draft for sign-off)

Nothing here is built yet. This is the whole catalogue: every boon and cost, how each one stacks, what happens at max stacks, the combinations, and the rules a deal follows. It builds on your calls so far: the transmutation circles and where they go, the mercy pull, the refusal cap, a deal every second floor, the element trap boons, Retrograde and Projection, and cutting Scavenger and Surefooted.

**What you're signing off is what each entry does.** The numbers are starting values. Each one is sized against the Max player (the board at the cap, a Gold 3★ weapon and three Gold 3★ pieces, every other slot filled). The Assay then moves them until the targets hold (section 8).

---

## 1. The rules

### Deals
- **A deal comes after floor 1 and then every second floor** (after floors 1, 3, 5 … 99): 50 deals a run instead of 99. Your first deal comes straight after the first floor.
- **Refusals are capped at 12 a run.** The refuse slate shows how many you have left. At 0 it's gone, so 38 or more of the 50 deals must be taken. Indenture still removes the refuse slate for one deal, and that deal doesn't count against the cap.
- **Pairing works as it does today:** a slate's cost weight climbs with depth, and its boon is the same weight or one heavier (the bargain). Two changes:
  - With half as many deals, the bargain phase stretches to floor 20 instead of floor 9.
  - Light (weight 1) entries can still turn up at every depth, at a third of their early rate.
- **Seeded.** Each deal is drawn from a seed for the run and floor, like waves, so a restart meets the same draws. What's on offer still depends on what you hold. Seeding is also what makes Oracle's preview honest (section 6).
- **Filtered.** Element entries appear only for their element, weapon-class entries only for their class, and an entry that would change nothing is never offered. For example, Short Chain never comes to a chain already at one basic.

### Stacks
- **Every entry is unique (one stack), stackable (two or three) or recurring.** Recurring entries are the deal-shaping ones (Prima Materia, Caput Mortuum, Indenture, Debt). They have no stacks; each rests for 4 deals after you take it.
- **No dead stacks.** Every stack changes a number, a duration or a frequency. An entry that couldn't is unique.
- **Stacks add linearly, and the card shows what the next one gives after the Vessel.**
- **Boons bend through the Vessel**, and it now covers every stat the ledger touches (table in section 8). That includes conditional bonuses: Executioner, Fury of the Frail, the trap boons' bonus and the rest are run-layer points, joined with the ledger's static points before the Vessel bends them, so they share the same ceiling. Crit chance and lifesteal stay in their one pools.
- **Costs pass straight through and are never softened by investment.**
  - Damage-taken costs land outside the mitigation floor, like the water soak.
  - Health costs are a share of max health.
  - Self-damage ignores mitigation (it's a price, not an attack) but still goes through `Take`, so Second Wind can catch it.
- **Spire boons stay floor-long run-layer points**, as Phase 2 left them. Lodestone and Desecrated now touch them.

### Max stacks: Rubedo, Nigredo, Albedo
- **Rubedo:** a stackable boon at max stacks gains a capstone that changes how it plays. The card for the last stack shows it.
- **Nigredo:** a stackable cost at max stacks comes due with a harsher twist on top. The card for the last stack warns you, and names the Albedo it can become.
- **Albedo:** a Nigredo is held until a transmutation circle. There, that cost and its Nigredo leave the ledger and its Albedo joins it. A transmuted cost never returns that run.

### Transmutation circles (your rules, plus four details I filled in)
- **Where (your rule):** any combat floor can hold one while you hold a Nigredo.
  - If the next Rift is Red, the circle goes on that Red Rift floor.
  - Otherwise it goes on the last combat floor before the next Rift. If that floor is a boss, it goes on the floor before the boss.
  - If you only became eligible after that floor passed, you wait for the next Rift.
  - Puzzle floors count like boss floors (no fight).
- **How (your rule):** it's there from the start of the floor, placed with the room like a spire. It activates after 3 combat contacts while you stand in it; a contact is a hit you land or a hit you take. One Nigredo is transmuted per circle.
- **My details:**
  - Each contact lights one of the ring's three marks (Sulfur, Mercury, Salt), so progress reads at a glance.
  - Holding one Nigredo: it transmutes on the third contact. Holding several: the third contact pauses the fight for you to choose. The risk was standing in the circle to light it; a choice made against the clock would mostly test the input.
  - If the floor clears before the third contact, the circle fades with the fight and your Nigredo waits for the next one.
  - A circle floor never also has a spire.

### Combinations
- **Conjunction** (boon + boon) opens a new boon.
- **Putrefaction** (cost + cost) opens a harsher cost. It's always offered against a weight-3 boon.
- **Citrinitas** (boon + cost) opens a boon that feeds on the cost. I've renamed this from "Transmutation", which now belongs to the circle. Citrinitas is the yellowing, the stage between Albedo and Rubedo, so the four colours of the Work each mean one thing: black for a maxed cost, white for its transmutation, yellow for a boon and cost combined, red for a maxed boon.
- A combination forms the moment you hold both parts (any stacks). It joins the pool, takes the guaranteed slate at the next deal, and is marked NEW. Every combination is unique and opens once a run.

### The guaranteed slate
One slate per deal is guaranteed, or two when Prima Materia shows three pairs. In priority order:
1. A combination that formed since the last deal.
2. **The mercy pull:** a cost exactly one stack short of its Nigredo that hasn't been offered for 3 deals.
3. **The pity timer:** a stackable boon you hold below max that hasn't been offered for 3 deals.

Ties go to whatever has waited longest. The slate's other half is drawn normally. Taking the other slate, or refusing, restarts that entry's clock. A combination is guaranteed only once; after that it's an ordinary entry in the pool.

---

## 2. Boons

W is the weight (1 a nudge, 2 changes a decision, 3 changes the build). Damage, Attack Speed and the other stat names are run-layer points: +10 Damage is +10% to every hit before the Vessel bends it.

### Edge — strikes
| Boon | W | Stacks | Each stack | Rubedo (at max) |
|---|---|---|---|---|
| Whetstone | 2 | 3 | +10 Damage | **Keen Edge:** your hits ignore enemy armour |
| Quickening | 2 | 3 | +8 Attack Speed | **Celerity:** weapon arts lock you for half as long |
| Vein Finder | 2 | 3 | +5% crit chance | **Fulminate:** a crit bursts for 25% of the hit onto enemies around its target |
| Executioner | 2 | 2 | +25 Damage against enemies under 30% health | **Coup de Grâce:** a non-elite you hit below 10% health dies outright |
| First Blood | 2 | 1 | the first hit on each full-health enemy gets +60 Damage | — |
| Reiteration *(was Echo)* | 2 | 2 | every 5th landed hit (4th at II) strikes again for 50% | **Rota:** a repeat that kills leaps to the nearest enemy and repeats again |
| Long Reach | 1 | 3 | +6 Range | **Far Strike:** hits in the outer quarter of your reach deal +20% |
| Wide Arc *(swords, discs)* | 1 | 2 | +25% swing width | **Cleaving Habit:** basics lose nothing for each body they pass through |

### Anvil — weapon arts
| Boon | W | Stacks | Each stack | Rubedo |
|---|---|---|---|---|
| Heavy Payoff | 2 | 3 | +12 Weapon Art | **Crushing Blow:** every weapon art flinches what it hits, armoured or not |
| Ouroboros | 2 | 3 | a kill has a 12% chance to bank your weapon art at once | **The Serpent Eats:** a weapon art that kills banks the next one at once |
| Green Lion | 2 | 2 | PERFECT weapon arts deal +15% | **Red Lion:** your perfect streak survives a GOOD (only an early, missed or wasted strike resets it) |
| Short Chain | 3 | 1 | one basic fewer before every weapon art | — |
| Extra Sigil | 3 | 1 | one more weapon art in the rotation | — |

### Hide — defence
| Boon | W | Stacks | Each stack | Rubedo |
|---|---|---|---|---|
| Thickened Hide | 2 | 3 | +10% max health | **Fortitude:** below 30% health, hits deal 30% less (outside the floor) |
| Bloodletter's Pact | 2 | 3 | +3% lifesteal (one pool, capped at 12%) | **Transfusion:** lifesteal also drinks from your releases, burns and bleeds |
| Stonestance | 2 | 3 | +10 Brace (mitigation standing still) | **Lapis:** after a second standing still, the next hit you take deals half |
| Reactive Plate | 2 | 2 | after a hit, 2 s of −20% damage taken (−35% at II), at most once every 5 s | **Tempered:** the window also takes 2 s off your defensive ability's cooldown |
| Aegis Cycle | 3 | 2 | a ward that cancels one hit, renewing every 10 s (7 s at II) | **Tin Ward:** when the ward breaks it throws nearby enemies back |
| Second Wind | 3 | 1 | once a floor, a lethal hit leaves you at 1 health, then heals 15% over 3 s | — |
| Kiln-Fired | 1 | 1 | your armour never wears | — |

### Quicksilver — movement
| Boon | W | Stacks | Each stack | Rubedo |
|---|---|---|---|---|
| Fleetfoot | 1 | 3 | +5 Move Speed | **Wake:** after a second at full speed, your next basic deals +50% |
| Eagle *(was Slipstream)* | 2 | 2 | after a kill, +20 Move Speed for 2 s (3 s at II) | **Stoop:** after a kill, your next hit within 2 s is a crit |
| Evanescence | 2 | 3 | +10 Graze (mitigation while moving) | **Vapour:** while moving, every fourth hit you take passes through you |
| Ghostwalk | 3 | 1 | after a hit, 0.6 s untouchable, at most once every 6 s | — |

### Azoth — the element
| Boon | W | Stacks | Each stack | Rubedo |
|---|---|---|---|---|
| Attunement | 1 | 2 | start each floor with 30% of your meter | **Primed:** at II you start each floor with it full instead |
| Rich Vein | 2 | 3 | +10 Element Growth (the meter fills faster; Fire's heat lasts longer) | **Mother Lode:** a kill refills 10% of your meter |
| Elixir | 2 | 3 | +10 Elemental Power | **Grand Elixir:** your releases can crit |
| Dilation | 1 | 3 | +8 Area | **Expansion:** area attacks deal full damage out to their edge |
| Residue | 2 | 1 | a release leaves 4 s of your element on the ground: Fire burns, Water soaks, Earth slows, Air pulls in | — |
| Overflow | 3 | 1 | a release refunds 40% of its meter | — |
| Twin Spark | 3 | 1 | your release fires again 1 s later at half strength, spending nothing (Air: Gust's window runs half as long again) | — |

### Ledger — the run itself
| Boon | W | Stacks | Effect |
|---|---|---|---|
| Curator | 2 | 1 | floor rewards offer one more card |
| Transmuter's Eye | 2 | 1 | see this floor's reward before you choose a deal |
| Scrying Glass | 2 | 1 | see what the next floor holds, and its roster if it's a fight |
| Lodestone | 2 | 1 | a spire boon you capture lasts one more floor |
| Prima Materia | 2 | recurring | the next deal shows three pairs |

---

## 3. Costs

Every stackable cost at max stacks is sized to take at least a third off whatever it hits on the Max player, before its Nigredo adds more. The Albedo is what a circle turns it into.

### Blunted — offence
| Cost | W | Stacks | Each stack | Nigredo (at max) | Albedo (after a circle) |
|---|---|---|---|---|---|
| Dulled | 2 | 3 | −12 Damage | **Rebated:** every hit rolls the bottom of its damage range (Accuracy no help) | **Honed:** your first hit on each enemy is always a crit |
| Heavy Arms | 2 | 3 | −12 Attack Speed | **Leaden Limbs:** attack speed from boons and spires stops counting | **Deliberate:** each basic in a chain deals +15% more than the one before (the weapon art too) |
| Cold Iron | 2 | 3 | crits lose a third of their bonus damage (none at III) | **Quenched:** a crit deals 25% less than an ordinary hit | **Cementation:** every 4th hit on the same enemy is a crit |
| Fumbler | 2 | 3 | one swing in nine passes through without connecting | **Lapsus:** a weapon art that passes through is lost, and the chain starts over | **Felicity:** one swing in nine strikes twice |
| Overcommitted | 2 | 3 | weapon arts lock you 25% longer | **Overextended:** +40% damage taken while an art locks you (outside the floor) | **Committed:** −40% damage taken while an art locks you |
| Short Arm | 1 | 3 | −10 Range | **Cramped:** hits beyond half your reach deal 40% less | **Close Quarters:** hits within half your reach deal +25% |

### Brittle — defence
| Cost | W | Stacks | Each stack | Nigredo | Albedo |
|---|---|---|---|---|---|
| Thin Blood | 2 | 3 | −11% max health | **Anaemia:** healing above half health is halved | **Fury of the Frail:** below half health, +25 Damage |
| Paper Guard | 2 | 3 | +17% damage taken (outside the floor) | **Exposed:** your mitigation floor rises from 35% to 60%: armour, Resilience, Graze and Brace stop short | **Adamant:** your mitigation floor drops from 35% to 25% |
| Slow Knit | 2 | 3 | −15% healing | **Hollow:** healing can't take you above 60% health | **Vital Spark:** you regenerate 0.5% of max health a second (inside the one healing limit) |
| Rust | 1 | 3 | armour wears 50% faster | **Corrosion:** repairs from every source are halved | **Patina:** worn armour no longer makes you take more damage |
| Open Stance | 1 | 1 | the first two hits you take each floor deal double | — | — |

### Tithe — prices paid
| Cost | W | Stacks | Each stack | Nigredo | Albedo |
|---|---|---|---|---|---|
| Blood Price | 3 | 2 | every swing costs 0.5% of max health | **Haemorrhage:** the price doubles below half health | **Pelican:** every swing heals 0.3% of max health (inside the healing limit) |
| Withering | 3 | 2 | max health −1.5% for every floor cleared (stops at −30%) | **Desiccation:** the loss no longer stops | **Viriditas:** max health +1% for every floor cleared (up to +20%) |
| Toll | 1 | 3 | clearing a floor costs 6% of current health | **Usury:** the toll is taken from max health instead | **Tribute:** clearing a floor heals 12% of max health |
| Souring | 2 | 3 | −2% damage for every 10 s on a floor (resets each floor) | **Acetum:** enemies also hit 2% harder for every 10 s on a floor | **Maturation:** +3% damage for every 10 s on a floor (up to +30%) |
| Backfire | 2 | 3 | a release costs you 8% of max health | **Recoil:** a release also roots you for 0.6 s (no moving or attacking) | **Rebound:** a release heals 5% of max health |
| Desecrated | 1 | 1 | spire boons are halved | — | — |
| Caput Mortuum | 2 | recurring | the next deal shows one pair | — | — |
| Indenture | 2 | recurring | the next deal can't be refused | — | — |
| Debt | 3 | recurring | the next deal gives its cost and no boon | — | — |

### Leaden — movement
| Cost | W | Stacks | Each stack | Nigredo | Albedo |
|---|---|---|---|---|---|
| Anchored | 2 | 3 | −6 Move Speed | **Mired:** sand and mire slow you twice as much | **Lightfoot:** +12 Move Speed, and Graze counts double at full speed |
| Encumbered | 2 | 3 | defensive ability cooldown +20% | **Shackled:** your parry window is halved | **Unshackled:** your parry window is doubled |
| Rooted | 2 | 1 | you can't move while any swing plays, basics included | — | — |
| Drag | 1 | 1 | you slide after you stop, everywhere, as if in water | — | — |

### Leaking — the element and the chain
| Cost | W | Stacks | Each stack | Nigredo | Albedo |
|---|---|---|---|---|---|
| Stubborn Ore | 2 | 3 | −15 Element Growth (the meter fills slower; Fire's heat fades sooner) | **Barren:** after a release, nothing fills your meter for 3 s | **Concentrate:** your releases deal +40% |
| Leaky Vessel | 1 | 3 | your meter fades 30% faster | **Cracked Vessel:** every hit you take spills 10% of your meter | **Sealed Vessel:** your meter never fades, and every hit you take adds 5% to it |
| Long Chain | 3 | 2 | one more basic before every weapon art | **Fraying:** partial chains lapse twice as fast | **Golden Chain:** every third weapon art comes with no basics before it |
| Locked Rotation | 1 | 1 | your weapon art rotation is shuffled instead of advancing in order | — | — |

### Blindfold — what you can read
| Cost | W | Stacks | Each stack | Nigredo | Albedo |
|---|---|---|---|---|---|
| Fog | 1 | 3 | I: your target loses its rim. II: the chain and rotation display is hidden too. III: enemy health bars are hidden too | **Murk:** enemy attack telegraphs are drawn at half strength | **Lucid:** an enemy winding up an attack on you is outlined |
| Wandering Eye | 1 | 3 | auto-target abandons its target more eagerly | **Blind Rage:** auto-target picks at random within reach | **Basilisk:** auto-target always picks the weakest enemy in reach |
| Dead Weight | 1 | 3 | an enemy you kill leaves a corpse that blocks you for 2 s (3 s, 4 s) | **Charnel:** corpses also stop your thrown weapons and arrows | **Ossuary:** corpses block enemies instead of you |
| **Retrograde** *(new; replaces Mirror Nerve)* | 3 | 2 | every 10 s (6 s at II) your movement inverts for 1.5 s, warned 0.75 s ahead by a ring closing at your feet | **Contrary:** every hit you take also inverts you for 1 s (Mirror Nerve's old effect) | **Antipathy:** every 10 s, enemies near you are staggered for 1.5 s |
| **Projection** *(new)* | 3 | 2 | every 12 s (8 s at II), three lines are drawn across the floor; after 1 s they burn for 2 s, at the spire lines' depth-scaled damage | **Lattice:** a second set of lines crosses the first, drawn through where you stand | **Ley Lines:** every 10 s, lines are drawn through the nearest enemies and burn them |

---

## 4. Elements and weapon classes

### The element trap boons (yours)
Offered only to their element, weight 2, two stacks. Stack II is their capstone.

| Boon | Element | I | II |
|---|---|---|---|
| Salamander | Fire | fire pits don't burn you | while in a burning pit, and for 4 s after: +20 Damage |
| Gnome | Earth | sand doesn't slow you (not the Turret's mire) | while in sand, and for 4 s after: +20 Damage |
| Undine | Water | water doesn't take your grip or soak you | while in water, and for 4 s after: +20 Damage |
| Sylph | Air | tornadoes don't hurt you | passing through a tornado: +20 Damage for 4 s |

Same bonus, same time, for all four, as you asked. I've changed my recommendation from meter fill to a damage bonus (decision 1). Tornadoes now appear as often as each pit kind (Phase 2), so the four are live on the same share of floors.

### One boon and one cost per element
| Element | Boon (weight 2, 2 stacks) → Rubedo | Cost (weight 2, 3 stacks) → Nigredo → Albedo |
|---|---|---|
| Fire | **Banked Embers:** heat never falls below 1 stack (2 at II) → **Hearth:** a release leaves you at three stacks | **Smother:** each heat stack gives 2.5 fewer Damage → **Wet Ash:** Fuel stops working → **Phlogiston:** heat stacks give double Damage |
| Water | **High Tide:** your surge lasts 3 s longer (6 s at II) → **Spring Tide:** a surge soaks every enemy around you | **Low Water:** your surge gives 12 fewer Attack Speed → **Ebb:** soaked enemies stop filling your meter double → **Flood:** soaked enemies take 15% more |
| Earth | **Deep Roots:** your charge stops bleeding for 1 s after you move (2 s at II) → **Bedrock:** your planted damage reduction holds for 1 s after you move | **Restless:** after 2 s standing still you take +15% damage (outside the floor) → **Quicksand:** standing still also slows your attacks 15% → **Mountain:** while planted, +20 Damage |
| Air | **Tailwind:** momentum fades half as fast when you stop (not at all for 1 s at II) → **Updraft:** Gust throws nearby enemies back | **Becalmed:** your crit floor climbs 1% less per hit (4% → 1% at III) → **Doldrums:** a hit you take resets your streak → **Gale:** your crit floor climbs twice as fast |

### Weapon classes
| Boon | Class | Stacks | Each stack | Rubedo |
|---|---|---|---|---|
| Wide Arc | sword, disc | 2 | (above) | Cleaving Habit |
| Fletching | bow | 2 | +15 Pierce | **Broadhead:** pierced enemies take the full hit |
| Ricochet | disc | 2 | thrown discs bounce to one more enemy | **Boomerang:** ricochets lose nothing |

---

## 5. Combinations

### Conjunctions (boon + boon)
| Name | Parts | Effect |
|---|---|---|
| Oracle | Scrying Glass + Transmuter's Eye | you also see the next deal before you choose this one |
| Wellspring | Overflow + Rich Vein | your first release each floor fires twice |
| Phoenix | Second Wind + Thickened Hide | when Second Wind catches you, you rise at 40% health and the blast throws enemies back |
| Gemini | Reiteration + Vein Finder | repeats are always crits |
| Damascene | Whetstone + Heavy Payoff | a weapon art scores its target: your next three basics on it deal +25% |
| Hunt | Eagle + Executioner | after a kill, your next hit within 3 s gets Executioner's bonus whatever the target's health |
| Athanor | Stonestance + Aegis Cycle | the ward renews twice as fast while you stand still |
| Cataclysm | Elixir + Dilation | your releases stagger everything they hit for 1 s (through your stagger, so the board's Stagger rules apply) |

### Putrefactions (cost + cost)
| Name | Parts | Effect |
|---|---|---|
| Glass Bones | Thin Blood + Paper Guard | below half health, +25% damage taken (outside the floor) |
| Sol Niger | Fog + Wandering Eye | enemies farther than 6 units are drawn as silhouettes, telegraphs and all |
| Haemophilia | Blood Price + Slow Knit | every hit you take also bleeds you for 15% of it over 3 s |
| Senescence | Souring + Withering | each floor you clear, enemies hit 2% harder for the rest of the run |

### Citrinitas (boon + cost)
| Name | Parts | Effect |
|---|---|---|
| Bloodstone | Blood Price + Bloodletter's Pact | +1 Damage for every 2% of health you're missing (up to +30) |
| Ponderous | Heavy Arms + Heavy Payoff | attack speed lost to costs becomes Weapon Art, point for point |
| Iron Rhythm | Fumbler + Reiteration | a swing that passes through makes your next hit repeat |
| Slow Fire | Stubborn Ore + Elixir | releases deal +2% for every second the meter took to fill (up to +40%) |
| Blindsight | Fog + Vein Finder | +10% crit chance while Fog hides your target |
| Retrograde Motion | Retrograde + Fleetfoot | while inverted, you move 40% faster and take 30% less damage |

A Citrinitas feeds on its cost, so it fades if that cost is transmuted away. Ponderous, for example, has nothing left to convert once Heavy Arms is gone.

---

## 6. New things the game has to learn

Most entries reuse what exists: run-layer points, the ledger's hooks, `Player.Burn/Soak/Stagger/Bleed`, `CrowdHits`, corpses, the spire's lines. The new pieces:

- **The deal planner:** seeded draws, the refusal count, the guaranteed slate, and the pity and mercy clocks. Seeding is also what lets Oracle show the next deal truthfully: it's the same draw.
- **Transmutation circles:** placement through `FloorPlanner.Peek`, the ring, contacts, the picker. Separate code from the hub's transmog circle, which shares the name.
- **"Release again"** without spending the meter, for every element (Twin Spark, Wellspring).
- **Crit rolls on releases** (Grand Elixir).
- **An override of the mitigation floor** (Exposed, Adamant).
- **Enemy telegraph strength and silhouettes at range** (Murk, Sol Niger).
- **Auto-target modes:** random within reach, or weakest in reach (Blind Rage, Basilisk).
- **Projection's lines**, built from the spire's line code and drawn across the room.
- **Retrograde's schedule and its warning ring.**
- **The Vessel's new curves** (section 8).

---

## 7. What happens to today's 71

| Verdict | Entries |
|---|---|
| Kept, re-valued to scale | Whetstone, Quickening, Vein Finder, Executioner, First Blood, Long Reach, Wide Arc (swords and discs only), Heavy Payoff, Short Chain, Extra Sigil, Ouroboros, Thickened Hide, Bloodletter's Pact, Reactive Plate (no longer refreshed by every hit), Kiln-Fired, Fleetfoot, Overflow, Residue (now each element's own field), Transmuter's Eye, Scrying Glass, Prima Materia · Dulled, Heavy Arms, Fumbler, Overcommitted, Short Arm, Thin Blood, Paper Guard (now outside the floor), Slow Knit, Rust, Open Stance, Blood Price, Withering, Toll, Souring, Backfire, Caput Mortuum, Indenture, Debt, Anchored, Rooted, Drag, Long Chain, Wandering Eye |
| Renamed | Echo → Reiteration (clashed with Shadow's Echo) · Slipstream → Eagle (clashed with Mercury's link) |
| Reworked | Stonestance → run-layer Brace · Aegis Cycle → a ward that cancels one hit (two stacks made you immune 34% of the time) · Second Wind and Ghostwalk → unique (their second stacks did nothing) · Cold Iron → crits lose their bonus by thirds · Locked Rotation → shuffles (freezing let you keep your best weapon art, a boon) · Dead Weight → duration per stack · Twin Spark → a real second release · Fog → three stacks |
| Made to work (inert today) | Attunement, Rich Vein, Curator, Leaky Vessel, Stubborn Ore |
| Folded into another entry | Cleaving Habit → Wide Arc's Rubedo · Hollow → Slow Knit's Nigredo · Damp Powder → Stubborn Ore's Nigredo (Barren, now element-aware) · Mirror Nerve → Retrograde (its old effect is Retrograde's Nigredo) · Silent Chain → Fog II |
| Cut | Scavenger, Surefooted (your calls) · Deep Cut (crit bleeds are the board's Sanguine now) · Narrowed (Short Arm covers reach) · Burning Wick, Heavy Meter (the meter costs are now Stubborn Ore, Leaky Vessel and Backfire) |
| New | Green Lion, Evanescence, Elixir, Dilation, Lodestone, Encumbered, Desecrated, Retrograde, Projection, the four trap boons, the element and class entries, every Rubedo, Nigredo and Albedo, and the 18 combinations |

**Pool per player** (one element, one class): about 75 boon stacks and 78 cost stacks, plus whatever combinations form. That's against at most 50 deals, so the pool never runs dry; today's ran dry under any refusal cap.

---

## 8. How the numbers get set

**The scale.** The run layer multiplies the character layer, so a run-layer point is a percent of everything the stat touches: −12 Damage is −12% to every hit, on any character. Starting sizes per stack:

| Weight | Boon | Cost |
|---|---|---|
| 1 | about 5% of its axis | about 11%, three stacks |
| 2 | about 10% | about 12%, three stacks |
| 3 | about 15%, or a rule | about 18%, two stacks |

At equal weight, a boon stack is a little smaller than a cost stack. The trade is still worth taking because you give up an axis your build needs less: a Bulwark sells damage, a Striker sells health. Early deals add the bargain on top. An Albedo is sized like a weight-3 boon. Transmuting also removes the maxed cost, so the full swing is bigger.

**The Vessel's curves** (knee / cap at full mastery, ×0.35 at mastery 0, as now):

| Stat | Run knee / cap |
|---|---|
| Damage | 50 / 100 (as now) |
| Attack Speed | 30 / 60 (as now) |
| Move Speed | 15 / 30 (as now) |
| Weapon Art | 50 / 100 (as now) |
| Crit Damage | 40 / 80 (new) |
| Elemental Power | 40 / 80 (new) |
| Element Growth | 30 / 60 (new) |
| Area | 25 / 50 (new) |
| Range | 15 / 30 (new) |
| Max Health | 25 / 50 (new) |
| Graze, Brace | their own fall-off, then the one mitigation floor |
| Crit chance, lifesteal | the one pools (60%, 12%) |

**The targets** (`Assay.Targets`, the phase gates):

| Target | Goal |
|---|---|
| Sensible ledger, floor 100, on the Max player | ×2.5 damage |
| Random picks | ×1.6 |
| Damage-only picks over sensible | at most ×1.10 |
| A new player's whole-run ledger | at most ×1.5 |
| Every stackable cost at max stacks | takes at least a third off what it hits |
| Healing | at most 2.5% of max health a second |
| Per sensible run | 2–4 Rubedos, 2–5 transmutations, 2–4 combinations |

Beyond each entry's own numbers, the refusal cap (12) and the Vessel's size at mastery 0 are the levers for the ledger rows.

**What the model can't measure** is priced by stated judgement in `Assay.Judged` and argued there:
- the skill costs: Retrograde, Projection, Fog, Murk, Shackled, Wandering Eye;
- PERFECT play: Green Lion and Red Lion, using one stated share of arts landed PERFECT;
- crowd effects: Fulminate, Rota, Expansion, Cleaving Habit.

The sensible pick policy also learns the circle. It weighs carrying a Nigredo against the Albedo when a circle is due within reach.

**Guards** (a start-up check plus a report, like the lead-in check): no entry without an effect, a model term and generated card text; no stack that changes nothing; every combination and every Albedo reachable; the pool lasting the run under the refusal cap.

---

## 9. Decisions for you

1. **The trap boons' stack II: damage, not meter fill.** I first suggested meter fill. But Fire's meter reads "fills faster" as "heat lasts longer", which is worth almost nothing while you're attacking, so Salamander's stack II would be near-useless. +20 Damage for 4 s is the same for all four elements in practice, not just on paper.
2. **Several Nigredos at one circle:** the third contact pauses the fight while you choose (my recommendation), rather than a quick choice against the clock as I suggested before.
3. **The prize size:** transmuting removes a maxed cost and grants an Albedo sized like a weight-3 boon. That makes maxing a cost on purpose a real strategy, with the Nigredo's floors as the risk. Too generous, too stingy, or right?
4. **Deal timing and the cap:** first deal after floor 1, then every second floor (50 a run), and a flat 12 refusals with none returned at boss floors. Earlier I offered returning one refusal at each boss floor. With the circles relieving pressure late in a run, I'd keep the flat cap.
5. **The rename:** boon + cost combinations become Citrinitas, freeing "transmutation" for the circle.
