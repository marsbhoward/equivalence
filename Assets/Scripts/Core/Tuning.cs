namespace Convergence.Core
{
    /// <summary>
    /// The one place to tweak how the game FEELS.
    ///
    /// Every value here was previously a literal buried in the component that used it. They are
    /// gathered so a balance pass is one file open, not a scavenger hunt. Each field is the
    /// DEFAULT the matching component starts from - the components still expose their own
    /// serialized fields, so a scene or prefab can override any single value without touching
    /// code, but nothing in this project does that today (everything is built at runtime via
    /// AddComponent), so in practice editing a number here IS the change.
    ///
    /// After editing, recompile: `unity command recompile` (these are compile-time constants).
    ///
    /// OUT OF SCOPE on purpose:
    ///  - Per-element mechanics (Fire stacks, Water meter, Earth charge, Air momentum) live next
    ///    to their own logic in Assets/Scripts/Player/*Resource.cs - they are element identity,
    ///    not global feel.
    ///  - Economy and progression (durability wear, mastery XP, XP per kill, floor counts, repair
    ///    and heal fractions) are already collected under [Header] groups on GameBootstrap.
    ///  - One-shot finisher shapes (leap rise/fall/height, charge trembles) stay local to the
    ///    coroutine that plays them.
    /// </summary>
    public static class Tuning
    {
        /// <summary>
        /// Test harness. OFF for anything resembling a real playtest - these deliberately break
        /// the game to make one thing observable.
        /// </summary>
        public static class Testing
        {
            /// <summary>
            /// Practice-dummy mode. Replaces every floor's spawn with ONE stationary target that
            /// cannot die, cannot be moved, and deals no damage - so a swing can be watched over
            /// and over with nothing else happening on screen.
            ///
            /// Turn it OFF (and recompile) to get the real game back. It is checked at spawn
            /// time, so toggling it mid-run takes effect on the next floor.
            /// </summary>
            /// <remarks>
            /// static readonly, not const. As a const the compiler folds it and every branch it
            /// gates becomes provably dead code the moment it is false - which is a warning on a
            /// switch whose entire purpose is to be flipped. This keeps the toggle honest while
            /// still needing a recompile, same as everything else here.
            /// </remarks>
            public static readonly bool PracticeDummy = true;

            /// <summary>How far in front of the player the dummy is parked, in world units.</summary>
            public const float DummyDistance = 2.2f;

            // ---- what the chain does, for studying one animation at a time ----
            //
            // Normally the chain is 3 basics then a finisher, and the finisher ROTATES across
            // the three earned moveset slots - so watching one swing means waiting for it to
            // come round. These two pin it down.

            /// <summary>
            /// Forces all three finisher slots to one moveset, by <c>MovesetLibrary</c> id, so the
            /// rotation always lands on the same move. Empty string = leave the loadout alone.
            ///
            /// Valid ids: "default" (Overhand), "cleave", "flurry", "lunge", "hammer",
            /// "undertow", "ruin", "wanderblade", "meteor", "riposte".
            ///
            /// OFF. It was left on from the swing-animation work and was pinning every finisher
            /// slot to Overhand, which made nine of the ten movesets unreachable in normal play.
            /// It also no longer composes with the weapon class, which resets the slots after
            /// this runs.
            /// </summary>
            public const string ForceMovesetId = "";

            /// <summary>
            /// Every swing plays the moveset's FINISHER instead of its three basics - so with
            /// ForceMovesetId = "default" every swing is the Overhand.
            ///
            /// Also suppresses the alternating reversed replay, since that only exists to vary
            /// consecutive BASICS: with this on, the chain is the same move every time, which is
            /// the point when the question is "does this one swing read correctly".
            /// </summary>
            public const bool EverySwingIsFinisher = false;

            /// <summary>
            /// Fill the showcase with invented art so the frames and the trophy placement system
            /// can be used before any wallet exists. Off now that DemoWalletAddress below reads a
            /// real one - the two are mutually exclusive, and a real wallet always wins if both are
            /// somehow left on.
            /// </summary>
            public const bool DemoShowcase = false;

            /// <summary>
            /// Read a real wallet's holdings straight from the coalescence-service REST API on
            /// startup, bypassing WebChainBridge entirely - that bridge only exists inside a
            /// browser build, so this is the one way to see the showcase/crate wired to real chain
            /// data from the Editor. Empty means off; GameBootstrap only calls it when this is set.
            ///
            /// Never used by the shipped game - see WalletInventory.RefreshFromServiceAsync's own
            /// note for why this has to stay a testing path rather than a second production one.
            /// </summary>
            public const string DemoWalletAddress =
                "addr_test1qpluffpk6kew7ed6y453m6l2gudfgvp7cw8rwz2g6yacgrhs3cykq95zr24svxakxnkla9hqjht5rdwgc6jkdncty2ws8v86aj";

            /// <summary>Where coalescence-service is listening - see web/service/README.md.</summary>
            public const string DemoWalletServiceUrl = "http://localhost:8787";
        }

        /// <summary>
        /// The character's baseline before any element, gear, or mastery is applied.
        ///
        /// THESE ARE THE GAME'S UNITS, and 100% of any stat means exactly the number here. They
        /// stay absolute because something has to be - a percentage needs something to be a
        /// percentage OF - but every one is annotated with what it is worth against a real enemy,
        /// because "10 damage" on its own tells you nothing and reading it should not require
        /// opening Tuning.Enemy to find out.
        ///
        /// Everything that MODIFIES them - gear, grid nodes - works in percentage points on top.
        /// See <see cref="Art.Gear.StatPercents"/>.
        /// </summary>
        public static class Player
        {
            /// <summary>
            /// How much bigger than authored the character is DRAWN in the arena - visual only,
            /// see ICharacterRig.SetVisualScale. Nothing gameplay reads it.
            ///
            /// 4/3 because it is the scale that puts every density the character is drawn at on
            /// whole screen pixels at the arena camera (Camera.Size 4.8): the body/helm/weapon
            /// 150 ppu becomes 112.5, one screen pixel per texel at 1080p and two at 4K, and the
            /// gear still at 75 becomes 56.25, two and four. At 1.0 those were 0.75 / 1.5 and
            /// 1.5 / 3 - fractional, so every thin feature shimmered as the character moved.
            /// 1440p is the one common height this does not land (1.33); only a whole-frame
            /// render at the art's own resolution fixes that.
            ///
            /// The same picture as zooming the camera in by a quarter and shrinking the whole
            /// world to match - chosen over that because it changes no gameplay number at all.
            /// The cost is that the character reads a third larger next to enemies and columns.
            /// </summary>
            public const float ArenaVisualScale = 4f / 3f;

            /// <summary>
            /// Walk speed, world units per second. 100%.
            ///
            /// Player and every enemy speed were cut 30% together (from a 6.5 baseline) to slow
            /// overall pacing - the RATIO between them is what matters for the escape-ratio math
            /// documented across this file, and that ratio is unchanged by scaling both sides by
            /// the same factor.
            ///
            /// For scale: a bomb enemy walks at 1.82 and an elite at 1.47, so the player still
            /// moves about two and a half times the speed of the thing chasing them - disengaging
            /// is always possible, which is what makes the crowd a positioning problem rather than
            /// a race.
            ///
            /// Cut a further 10% on its own (4.55 -> 4.1), enemies untouched - the player still
            /// out-walks every chaser by better than 2x.
            /// </summary>
            public const float MoveSpeed = 3.7f;

            /// <summary>
            /// The body's linearDamping on dry ground - about 11% of its velocity gone every
            /// physics step, on top of the movement blend. Re-solved on low-GRIP ground each step
            /// (Hazards.FloorPits.DampingFor): water that capped only the blend would leave this
            /// fighting it, and the player would WADE at ~30% speed instead of sliding.
            /// </summary>
            public const float BodyDamping = 6f;

            /// <summary>
            /// Damage of a 1.0x swing before the element's own multiplier. 100%.
            ///
            /// For scale: a bomb enemy has 65 HP and an elite 185, so an unbuffed basic swing is
            /// about a SIXTH of a common enemy and a eighteenth of an elite. A Medium finisher
            /// (5 basics) is most of a common enemy in one blow, and a Heavy (6.5) kills one.
            ///
            /// Those HP figures were scaled x1.54 alongside the finisher weight classes: a chain
            /// went from 2 basics + a ~2x finisher (4 basics of damage) to 2 basics + a 5x Medium
            /// (7 basics), so enemies grew to hold time-to-kill roughly steady. What CHANGED is
            /// the shape rather than the pace - the finisher went from about half a chain's damage
            /// to about seventy percent of it, which is the whole point of the weight classes:
            /// basics are setup, the finisher is the payoff.
            /// </summary>
            public const float BaseDamage = 10f;

            /// <summary>
            /// Reach of a basic swing, world units. 100%. Finishers add their own RangeBonus.
            ///
            /// For scale: the character is about 0.9 units tall, so a swing reaches a bit over two
            /// body-heights. Nothing draws it as a circle any more (strikes are capsules along the
            /// facing); Player.TargetHighlight shows whether the locked target is inside it.
            /// </summary>
            public const float BaseRange = 1.9f;

            /// <summary>
            /// Outward shove a basic swing applies. 100%. Finishers set their own Knockback.
            ///
            /// For scale: knockback is not in world units - measured displacement runs about a
            /// quarter of the number, so 3.2 shoves a bomb enemy roughly 0.8 units, half a swing's
            /// reach. See the knockback table in CLAUDE.md.
            /// </summary>
            public const float Knockback = 3.2f;

            /// <summary>
            /// Crit chance every character has before gear or element. Exists so a Crit Damage
            /// roll is never a dead stat on a character with no crit chance - crit is a playstyle
            /// any element can build into, not an Air exclusive. Air's own crit rides on top.
            /// </summary>
            public const float BaseCritChance = 0.05f;

            /// <summary>Every crit's multiplier before Crit Damage points - the same for every element;
            /// an element that crits harder gets Crit Damage points as a head start (Tuning.Elements).</summary>
            public const float BaseCritMultiplier = 1.8f;

            /// <summary>Swing cone: lower is wider. 0.25 ~ a 150-degree arc dead ahead.</summary>
            public const float SwingArcDot = 0.25f;

            /// <summary>
            /// The character's attack speed as a PERCENTAGE. 100 is the baseline cadence; 130 is
            /// thirty percent faster; 80 is a fifth slower.
            ///
            /// This is the BASELINE the points from gear and grid nodes ride on - see
            /// <see cref="Art.Gear.StatPercents"/> for why the modifier layer is points rather
            /// than multipliers.
            ///
            /// Distinct from <see cref="Attack.GlobalTempo"/>, which it multiplies against: tempo
            /// is the designer's global feel dial for everyone, this is one character's stat.
            /// </summary>
            public const float AttackSpeedPercent = 120f;

            // Starting max HP. 100% is whichever of these the element uses.
            //
            // For scale: a bomb enemy's blast hits for 20 and an elite for 15, so unarmoured
            // these are about 6 / 4 / 7 bomb hits, or 8 / 6 / 10 elite ones. The spread between them is
            // the element's whole durability identity.

            /// <summary>Fire and Water - the mid-durability elements. ~14 basic hits.</summary>
            public const float HpFireWater = 115f;

            /// <summary>Air, the glass cannon. ~11 basic hits, or 6 from an elite.</summary>
            public const float HpAir = 85f;

            /// <summary>Earth, the tank. ~18 basic hits.</summary>
            public const float HpEarth = 145f;

            /// <summary>
            /// How steeply travel has to point toward the top of the screen (away from the
            /// camera) before untargeted movement turns the rig around to show its back - the
            /// same treatment <see cref="Art.Gear.ICharacterRig.SetFacingAway"/> already gives the
            /// scripted door beat, now driven by ordinary walking. Shared by
            /// <c>PlayerController.UpdateTravelFacingAway</c> and
            /// <c>Hub.HubRoom.UpdateAvatarFacingAway</c> so the arena and the hub agree on what
            /// "walking away" means.
            ///
            /// A plain deadzone rather than two asymmetric thresholds, the same shape
            /// <c>PrimitiveCharacterRig.MirrorDeadzone</c> already uses on the x axis: state only
            /// flips on a clear vertical commitment, so a mostly-sideways stride never chatters
            /// between front and back as it wobbles across zero.
            /// </summary>
            public const float FacingAwayDot = 0.5f;
        }

        /// <summary>Swing cadence and how long a swing animation is allowed to run.</summary>
        public static class Attack
        {
            /// <summary>How far a Splash hit spills from its target. Small on purpose - the stat
            /// is a nudge toward fighting in a clump, not a second area-of-effect attack.</summary>
            public const float SplashRadius = 1.1f;

            /// <summary>
            /// Every hit's damage is a RANGE either side of its average: 0.2 is 80% to 120%. The
            /// average is what it always was, so nothing moves until Accuracy is invested - which
            /// raises the range's BOTTOM toward its top, and at 100 points every hit lands at the
            /// top: +20% on average, with that ceiling built in. Rolled once per swing or throw
            /// (PlayerController.DamageRoll).
            /// </summary>
            public const float DamageSpread = 0.2f;

            /// <summary>How far past its target a Pierce arrow carries on, and how wide the strip
            /// it hits is either side of the line.</summary>
            public const float PierceLength = 4f;
            public const float PierceHalfWidth = 0.35f;

            /// <summary>
            /// Universal attack-speed dial for the UNBUFFED baseline. 1 = as originally tuned;
            /// below 1 slows every swing and its animation by the same factor. Per-element
            /// attack-speed buffs multiply on top, so a ramped late-run character still climbs
            /// back above 1 - this sets the floor, not the ceiling.
            /// </summary>
            public const float GlobalTempo = 0.8f;

            /// <summary>Seconds between basic swings at a 1.0x interval, before GlobalTempo.</summary>
            public const float BaseInterval = 0.42f;

            /// <summary>Fraction of the gap to the next swing that the animation fills, leaving a little recovery.</summary>
            public const float SwingFillFraction = 0.85f;

            /// <summary>Shortest a swing may animate, however fast the attack-speed buff gets.</summary>
            public const float SwingMinSeconds = 0.09f;

            /// <summary>
            /// Longest a swing may animate. Raised from 0.45 once GlobalTempo made the baseline
            /// interval longer - at the old ceiling a slowed swing hit the clamp, finished its
            /// arc early and held the pose, so the tempo dial moved the cadence but not the
            /// visible speed of the swing.
            ///
            /// Raised again from 0.55 for the same reason, this time hit by HEAVY finishers.
            /// Heavy's 2.5x lock (Tuning.Finisher.LockHeavy) puts its interval at
            /// 0.42 * 0.8 * 2.5 = 0.840s and its animation at 0.714s, which the old 0.55 ceiling
            /// cut by a quarter - the arc finished early and the character stood in the pose, so
            /// the heaviest swing in the game would not have LOOKED any slower than a Medium.
            ///
            /// Note this never threatened the damage model: cooldown is taken from the raw
            /// interval, so DPS was always correct and only the animation was short. That is
            /// precisely what makes it the dangerous kind of bug - every number reads right and
            /// the swing just feels wrong.
            ///
            /// Only Heavy binds against this today; Medium animates 0.428s and Light 0.286s.
            /// </summary>
            public const float SwingMaxSeconds = 0.75f;

            /// <summary>
            /// Floor on the per-body damage taper of a cleaving swing, so a wide finisher still
            /// means something on its fifth target instead of decaying to nothing.
            /// </summary>
            public const float CleaveFalloffFloor = 0.35f;
        }

        /// <summary>
        /// The disc class: slow heavy melee inside your reach, faster ricocheting throws outside
        /// it. The switch is automatic - see PlayerController.ThrowsInsteadOfSwinging.
        ///
        /// NEITHER HALF IS THE MAIN ONE, and neither half is the BEST at what it does. That is
        /// the hybrid's whole price:
        ///
        ///   melee  - hits exactly as hard as a greatsword swing, but slower. Strictly less
        ///            damage per second than the dedicated melee class.
        ///   throw  - as fast as the dedicated ranged class will be, but hits for less.
        ///
        /// So the disc is never the right answer to a problem a specialist owns, and never the
        /// wrong answer to any of them. Versatility is the thing being paid for, and the payment
        /// is that both halves sit just under the specialist.
        /// </summary>
        public static class Disc
        {
            /// <summary>Melee cadence against the greatsword's. Above 1 is slower.</summary>
            public const float MeleeIntervalMul = 1.45f;

            /// <summary>
            /// Melee damage against a greatsword swing. ONE, deliberately: the disc hits exactly
            /// as hard up close as the greatsword does, and MeleeIntervalMul is the whole
            /// difference. Same punch, fewer of them.
            ///
            /// Raising this is what would make the disc a better greatsword than the greatsword,
            /// which is the one thing a hybrid must never be.
            /// </summary>
            public const float MeleeDamageMul = 1.00f;

            /// <summary>
            /// Throw cadence. This is the RANGED cadence, not a disc-specific one - the dedicated
            /// ranged class, when it exists, should fire at the same rate. The disc keeps up; it
            /// just does less with each shot.
            /// </summary>
            public const float ThrowIntervalMul = 0.72f;

            /// <summary>
            /// <summary>
            /// Damage of a basic disc throw landing at POINT-BLANK (right at the melee/throw
            /// boundary), against Player.BaseDamage - the floor of a curve that scales up to
            /// FULL (1.0, no penalty at all) at ThrowReach. Replaces the old flat 0.70 nerf:
            /// that number was never wrong, it was the AVERAGE case rather than the floor -
            /// 0.70 sits almost exactly at the midpoint of 0.45..1.0, so a throw at medium
            /// distance still lands about where it always did, while a point-blank throw is
            /// now a real mistake and holding the range circle is a real reward, instead of
            /// every ranged hit being flatly punished regardless of distance.
            ///
            /// At the far end this is 1.0, not below whatever a future dedicated ranged class
            /// lands on per shot - that constraint now applies to the CURVE'S PEAK, still
            /// enforced by hand until that class exists.
            /// </summary>
            public const float ThrowDamageNear = 0.45f;

            /// <summary>
            /// How far a throw reaches, as a MULTIPLE of the character's own melee reach.
            ///
            /// Relative rather than a fixed distance so the two never converge. Reach grows -
            /// Long Reach, a finisher's RangeBonus - and a constant throw range would let melee
            /// creep up on it until the band between them vanished and the weapon silently
            /// stopped being a hybrid. At 2x there is always exactly as much throwing space as
            /// swinging space.
            /// </summary>
            public const float ThrowRangeMul = 2f;

            /// <summary>
            /// How far a bounce looks for the next body, also as a multiple of melee reach. Equal
            /// to the throw itself: a ricochet should carry about as far as your arm does.
            /// </summary>
            public const float BounceRangeMul = 2f;

            /// <summary>
            /// Scales the disc's melee reach against every other weapon's.
            ///
            /// The disc is a hand-held ring, not a greatsword - it has no blade length to lend it
            /// the sword's reach, and at parity the two rings drawn on the floor were bigger than
            /// the swing they described. Because ThrowRangeMul is expressed as a multiple of THIS,
            /// one number moves both circles together and the hybrid's inner/outer band keeps its
            /// proportions.
            /// </summary>
            public const float RangeMul = 0.70f;

            /// <summary>Ricochets before the disc turns for home. Class progression raises it.</summary>
            public const int BaseRicochets = 1;

            /// <summary>Each successive hit deals this much of the last.</summary>
            public const float BounceFalloff = 0.85f;
        }

        /// <summary>
        /// The bow: pure ranged, single target, no bounce, no melee mode to fall back on.
        /// </summary>
        public static class Bow
        {
            /// <summary>
            /// Cadence against the greatsword's own baseline (BaseAttackInterval * this). Must
            /// clear Disc.MeleeIntervalMul (1.45 - the previous slowest attack in the game) by a
            /// real margin, or a later tuning pass on either class could silently swap which
            /// weapon actually holds "slowest."
            /// </summary>
            public const float AttackIntervalMul = 1.75f;

            /// <summary>
            /// Range against the character's own melee reach. Must clear Disc's own throw circle
            /// (RangeMul 0.70 x ThrowRangeMul 2.0 = 1.40x reach) by a real margin - the widest
            /// circle in the game is this class's whole identity.
            /// </summary>
            public const float RangeMul = 2.4f;

            /// <summary>
            /// Damage at point-blank against damage at the rim, the floor of a curve shaped like
            /// Disc.ThrowDamageNear. Held well above the disc's 0.45: the disc still has melee to
            /// fall back on when caught close, so its floor can afford to punish indecision. The
            /// bow has no fallback mode at all, and the sticky nearest-enemy lock can drag a
            /// target into its face with no decision on the player's part at all - so a bad
            /// position it did not choose should cost less than one the disc player did.
            /// </summary>
            public const float NearFraction = 0.6f;
        }

        /// <summary>
        /// The mastery board's economy. See Progression/MasteryBoard for the board itself.
        /// </summary>
        public static class Mastery
        {
            /// <summary>
            /// Hard ceiling on levels ONE element's board may spend.
            ///
            /// The identity dial is this against the board's size, not this alone: since the
            /// 2026-10-05 rebuild a board holds 119 buyable levels (one 21-level branch in each of
            /// five forked domains, the Rift's 13, the Rebis), so 50 buys 42% of one - two whole
            /// branches and a taste of a third. Before it, fillers reachable only through an owned
            /// keystone left 66 buyable and the cap bought 76%.
            ///
            /// The number that actually governs the feel is this against a PRINCIPLE, though:
            /// spent toward one principle, 50 levels finish its chain (MasteryBoard.Thresholds) and
            /// put the rest partway into a second; no spend finishes two. One chain completed, one
            /// tasted, one untouched - which falls out of the cap rather than needing a rule.
            /// </summary>
            public const int LevelCap = 50;
        }

        /// <summary>
        /// The mastery board's numbers (Progression.MasteryBoard, rebuilt 2026-10-05): the size of
        /// a node's stat, and every Tincture's, Opus's and status keystone's rule. The rules are
        /// in Progression.BoardEffects; their words are generated from these (MasteryBoard.Describe).
        /// </summary>
        public static class Board
        {
            // ---- sizes ----

            /// <summary>One stat unit on the board, as a share of a Gold armour primary of the
            /// stat (GearRoller.PrimaryPoints): a whole branch's nine units of its primary are about
            /// a third of the stat's character knee, because a targeted Gold three-star piece is a
            /// half (GearRoller.KindScale).</summary>
            public const float StatUnitOfGoldPrimary = 0.25f;

            /// <summary>For a stat with a character knee, units in the knee: 27, so a branch's nine
            /// units of its primary are a third of the knee.</summary>
            public const float UnitsPerKnee = 27f;

            /// <summary>Lifesteal per unit, as a fraction - nine units is 7.2%, under the one pool's
            /// 12% cap (Tuning.Stats.LifestealCap).</summary>
            public const float LifestealUnit = 0.008f;

            /// <summary>Percentage points of a status's strength per unit - nine units, +45%.</summary>
            public const float StatusPowerUnit = 5f;

            // ---- Strikes ----
            public const float VitriolPerHit = 0.02f, VitriolMax = 0.10f;
            public const float CohobationPerBasic = 0.08f, CohobationMax = 0.40f;

            // ---- Element ----
            public const float AlkahestRefund = 0.2f;
            public const float ExaltationSeconds = 5f, ExaltationDamagePoints = 25f;

            // ---- the four humours: status keystones any element can take ----
            /// <summary>Choler: a weapon art burns for this share of its hit each second. Low on
            /// purpose - burn riding every hit once added ~40% to Fire on its own.</summary>
            public const float CholerBurnFraction = 0.10f, CholerSeconds = 3f;
            /// <summary>Phlegm: a weapon art soaks - slowed, and taking this much damage.</summary>
            public const float PhlegmSeconds = 3f, PhlegmVulnerability = 1.12f;
            public const float SanguineBleedFraction = 0.15f, SanguineSeconds = 4f;
            public const float MelancholySeconds = 2f;

            // ---- status Tinctures and Opuses ----
            public const float SaltpetreBonus = 0.10f;
            public const float CinerationRadius = 2.5f;
            public const float AquaFortisRadius = 3f;
            /// <summary>Solution: what a soaked enemy's hits on you are worth.</summary>
            public const float SolutionDamageMul = 0.75f;
            public const int OrpimentStacks = 3;
            public const float MortificationBonus = 0.30f;
            /// <summary>Antimony: a staggered enemy's attack cooldown runs this much slower.</summary>
            public const float AntimonyAttackSlow = 0.30f;
            public const float CongelationSeconds = 1.5f, CongelationVulnerability = 1.2f;
            /// <summary>One congealing per enemy per this long - three staggers inside a chain or two
            /// would otherwise hold a body rooted for the whole fight.</summary>
            public const float CongelationCooldown = 4f;

            // ---- Survival ----
            public const float AlumDamageMul = 0.7f;
            /// <summary>Fixation: the most of max health one hit can take - mechanic hits and falls
            /// included, which is what makes it the tank's capstone.</summary>
            public const float FixationMaxHitFraction = 0.25f;
            public const float SalAmmoniacWindowMul = 1.5f;
            public const float AquaVitaeHealFraction = 0.02f;
            public const float CibationShieldFraction = 0.2f, CibationFadeSeconds = 5f;

            // ---- Tempo ----
            public const int RealgarBasics = 2;
            public const float RealgarSpeedMul = 1.2f;
            public const float CirculationWindow = 1f, CirculationPointsPerHit = 3f, CirculationMaxPoints = 30f;
            public const float TartarDamagePoints = 12f;
            public const float PrecipitationFraction = 0.4f, PrecipitationRadius = 2f;

            // ---- Space ----
            public const float AquaRegiaReachFraction = 0.667f, AquaRegiaSlow = 0.7f, AquaRegiaSeconds = 1.5f;
            public const float IncerationBonus = 0.15f;
            public const float BoraxPullDistance = 1.5f;
            /// <summary>The reach of a release for Borax and Fermentation, before Area: releases
            /// differ in shape, so the board's area rules read one radius round the player.</summary>
            public const float ReleaseRadius = 4f;
            public const float FermentationSeconds = 3f, FermentationSlow = 0.7f;
            public const float FermentationHitFractionPerSecond = 0.10f, FermentationRadius = 2.5f;
        }

        /// <summary>
        /// The equivalent exchange (Exchange/, rebuilt 2026-10-05 - docs/balance/2026-10-05-phase5-
        /// catalogue.md is the signed-off design). Every entry's number is here and its card text
        /// is generated from the same constant (ExchangeCatalog), so the two cannot drift.
        ///
        /// RUN-LAYER POINTS. "Damage", "Attack Speed" and the other stat names are points on the
        /// run layer, which MULTIPLIES the character layer: +10 Damage is +10% to every hit on any
        /// character. Boons bend through the Vessel (StatCurves.Run); costs pass straight through.
        /// </summary>
        public static class Exchange
        {
            // ---------------------------------------------------------------- the deal

            /// <summary>A deal after the first floor and every DealEvery-th floor after it (1, 3, 5
            /// ... 99): 50 a run. A deal every floor drained the pool under any refusal cap.</summary>
            public const int DealEvery = 2;

            /// <summary>Refusals a run may spend. At none left the refuse slate is gone, so 38 of
            /// the 50 deals at least are taken - the exchange cannot be skipped.</summary>
            public const int RefusalsPerRun = 12;

            public const int BasePairs = 2;
            public const int MaxPairs = 3;

            /// <summary>Deals a recurring (deal-shaping) entry rests after it is taken.</summary>
            public const int RecurringRestDeals = 4;

            /// <summary>THE PITY TIMER: a stackable boon held below max returns within this many
            /// deals of when it was last offered.</summary>
            public const int PityDeals = 3;

            /// <summary>THE MERCY PULL: a cost one stack short of its Nigredo returns within this
            /// many deals - chasing an Albedo is a strategy, so it must be reachable.</summary>
            public const int MercyDeals = 3;

            /// <summary>The floor by which cost weights have climbed to the top and the bargain
            /// (a boon one weight heavier than its cost) has drifted from near-certain to rare.
            /// Twice the old 9: there are half as many deals.</summary>
            public const float BargainUntilFloor = 20f;
            public const float BargainEarly = 0.9f;
            public const float BargainDeep = 0.15f;

            /// <summary>Past the first floors, the chance a slate draws its cost (and so its boon)
            /// light instead of at the floor's weight - a nudge still turns up late rather than
            /// vanishing after the first floors.</summary>
            public const float LightShareDeep = 0.25f;

            // ---------------------------------------------------------------- Edge
            public const float WhetstoneDamage = 10f;
            public const float QuickeningSpeed = 8f;
            public const float VeinFinderCrit = 0.05f;
            public const float ExecutionerBelow = 0.30f;
            public const float ExecutionerDamage = 25f;
            public const float CoupDeGraceBelow = 0.10f;
            public const float FirstBloodDamage = 60f;
            /// <summary>Every Nth landed hit repeats - N at one stack, one sooner at two.</summary>
            public const int ReiterationEvery = 5;
            public const float ReiterationFraction = 0.5f;
            public const float LongReachRange = 6f;
            /// <summary>Far Strike: hits past this share of the reach count as at its edge.</summary>
            public const float FarStrikeFrom = 0.75f;
            public const float FarStrikeDamage = 20f;
            public const float WideArcWidth = 0.25f;
            public const float FulminateFraction = 0.25f;
            public const float FulminateRadius = 2.2f;

            // ---------------------------------------------------------------- Anvil
            public const float HeavyPayoffArt = 12f;
            public const float OuroborosChance = 0.12f;
            public const float GreenLionArt = 15f;

            // ---------------------------------------------------------------- Hide
            public const float ThickenedHideHp = 10f;
            public const float FortitudeBelow = 0.30f;
            public const float FortitudeMul = 0.70f;
            public const float BloodletterLifesteal = 0.03f;
            public const float StonestanceBrace = 10f;
            public const float LapisStillSeconds = 1f;
            public const float LapisMul = 0.5f;
            public const float ReactivePlateSeconds = 2f;
            public const float ReactivePlateReduction = 0.20f;
            public const float ReactivePlateReductionII = 0.35f;
            public const float ReactivePlateCooldown = 5f;
            public const float TemperedCooldownCut = 2f;
            public const float AegisRenewSeconds = 10f;
            public const float AegisRenewSecondsII = 7f;
            public const float TinWardRadius = 3f;
            /// <summary>How far the ward's break throws a body (DragToward.Push), world units.</summary>
            public const float TinWardKnockback = 2.5f;
            public const float SecondWindHeal = 0.15f;
            public const float SecondWindHealSeconds = 3f;
            public const float GhostwalkSeconds = 0.6f;
            public const float GhostwalkCooldown = 6f;

            // ---------------------------------------------------------------- Quicksilver
            public const float FleetfootMove = 5f;
            public const float WakeAfterSeconds = 1f;
            public const float WakeDamage = 50f;
            public const float EagleMove = 20f;
            public const float EagleSeconds = 2f;
            public const float EagleSecondsII = 3f;
            public const float StoopSeconds = 2f;
            public const float EvanescenceGraze = 10f;
            public const int VapourEvery = 4;

            // ---------------------------------------------------------------- Azoth
            public const float AttunementFill = 0.30f;
            public const float RichVeinGrowth = 10f;
            public const float MotherLodeRefill = 0.10f;
            public const float ElixirPower = 10f;
            public const float DilationArea = 8f;
            public const float ResidueSeconds = 4f;
            public const float ResidueRadius = 2.2f;
            /// <summary>Fire's residue burns, in the player's own hit units a second.</summary>
            public const float ResidueBurnHitUnits = 1.2f;
            public const float ResidueSoakSeconds = 3f;
            public const float ResidueSlow = 0.6f;
            public const float ResiduePull = 6f;
            public const float OverflowRefund = 0.40f;
            public const float TwinSparkDelay = 1f;
            public const float TwinSparkScale = 0.5f;

            // ---------------------------------------------------------------- Ledger
            public const int CuratorCards = 1;
            public const int LodestoneFloors = 1;

            // ---------------------------------------------------------------- Blunted
            public const float DulledDamage = 12f;
            public const float HeavyArmsSpeed = 12f;
            /// <summary>Cold Iron: each stack takes this share of a crit's bonus damage.</summary>
            public const float ColdIronShare = 1f / 3f;
            public const float QuenchedMul = 0.75f;
            public const int CementationEvery = 4;
            public const float FumblerChance = 1f / 9f;
            /// <summary>Lapsus (Fumbler's Nigredo): seconds a swing that passed through holds the
            /// player where they stand - no moving, no attacking.</summary>
            public const float LapsusStumble = 0.4f;
            public const float FelicityChance = 1f / 9f;
            public const float OvercommittedLock = 0.25f;
            public const float OverextendedTaken = 0.40f;
            public const float CommittedTaken = 0.40f;
            public const float ShortArmRange = 10f;
            public const float CrampedFrom = 0.5f;
            public const float CrampedDamage = 40f;
            public const float CloseQuartersWithin = 0.5f;
            public const float CloseQuartersDamage = 25f;
            public const float DeliberateDamagePerBasic = 15f;

            // ---------------------------------------------------------------- Brittle
            public const float ThinBloodHp = 11f;
            public const float AnaemiaAbove = 0.5f;
            public const float AnaemiaMul = 0.5f;
            public const float FuryBelow = 0.5f;
            public const float FuryDamage = 25f;
            /// <summary>Paper Guard, per stack: damage taken, OUTSIDE the mitigation floor.</summary>
            public const float PaperGuardTaken = 0.17f;
            public const float ExposedFloor = 0.60f;
            public const float AdamantFloor = 0.25f;
            public const float SlowKnitHeal = 0.15f;
            public const float HollowCeiling = 0.60f;
            public const float VitalSparkRegen = 0.005f;
            public const float RustWear = 0.5f;
            public const float CorrosionRepairMul = 0.5f;
            public const int OpenStanceHits = 2;

            // ---------------------------------------------------------------- Tithe
            /// <summary>Blood Price, per stack: max health a swing costs.</summary>
            public const float BloodPriceSwing = 0.005f;
            public const float HaemorrhageBelow = 0.5f;
            public const float PelicanHeal = 0.003f;
            public const float WitheringPerFloor = 0.015f;
            public const float WitheringCap = 0.30f;
            public const float ViriditasPerFloor = 0.01f;
            public const float ViriditasCap = 0.20f;
            public const float TollFraction = 0.06f;
            public const float TributeHeal = 0.12f;
            /// <summary>Souring, per stack: Damage lost for every SouringEverySeconds on a floor,
            /// up to SouringCap a stack.</summary>
            public const float SouringDamage = 2f;
            public const float SouringEverySeconds = 10f;
            public const float SouringCap = 12f;
            public const float AcetumTaken = 0.02f;
            public const float MaturationDamage = 3f;
            public const float MaturationCap = 30f;
            public const float BackfireFraction = 0.08f;
            public const float RecoilSeconds = 0.6f;
            public const float ReboundHeal = 0.05f;
            public const float DesecratedMul = 0.5f;

            // ---------------------------------------------------------------- Leaden
            public const float AnchoredMove = 6f;
            public const float MiredSlowMul = 2f;
            public const float LightfootMove = 12f;
            public const float LightfootGrazeMul = 2f;
            public const float EncumberedCooldown = 0.20f;
            public const float ShackledWindow = 0.5f;
            public const float UnshackledWindow = 2f;
            public const float DragSlide = 0.6f;

            // ---------------------------------------------------------------- Leaking
            public const float StubbornOreGrowth = 15f;
            public const float BarrenSeconds = 3f;
            public const float ConcentratePower = 40f;
            public const float LeakyVesselDecay = 0.30f;
            public const float CrackedVesselSpill = 0.10f;
            public const float SealedVesselGain = 0.05f;
            public const float FrayingComboMul = 0.5f;
            public const int GoldenChainEvery = 3;

            // ---------------------------------------------------------------- Blindfold
            public const float MurkStrength = 0.5f;
            public const float WanderingEyeSwitch = 0.12f;
            public const float DeadWeightSeconds = 1f;   // + one per stack: 2, 3, 4
            public const float RetrogradeEvery = 10f;
            public const float RetrogradeEveryII = 6f;
            public const float RetrogradeSeconds = 1.5f;
            public const float RetrogradeWarning = 0.75f;
            public const float ContrarySeconds = 1f;
            public const float AntipathyEvery = 10f;
            public const float AntipathySeconds = 1.5f;
            public const float AntipathyRadius = 3f;
            public const float ProjectionEvery = 12f;
            public const float ProjectionEveryII = 8f;
            public const int ProjectionLines = 3;
            public const float ProjectionWarning = 1f;
            public const float ProjectionBurn = 2f;
            /// <summary>A line's burn a second on the player, scaled like a spire's lines and every
            /// other hazard (FloorDifficulty.Damage).</summary>
            public const float ProjectionDamagePerSecond = 14f;
            public const float ProjectionHalfWidth = 0.35f;
            public const float LeyLinesEvery = 10f;
            public const float LeyLinesHitUnits = 1.5f;

            // ---------------------------------------------------------------- the elements
            /// <summary>The trap boons' stack II: the same bonus for all four, for the same time
            /// after contact (the user's call).</summary>
            public const float TrapBonusDamage = 20f;
            public const float TrapBonusSeconds = 4f;
            public const int HearthStacks = 3;
            public const float HighTideSeconds = 3f;
            public const float DeepRootsHold = 1f;
            public const float BedrockHold = 1f;
            public const float TailwindFadeMul = 0.5f;
            public const float TailwindHoldII = 1f;
            public const float UpdraftRadius = 3f;
            public const float UpdraftKnockback = 2.5f;
            public const float SmotherPoints = 2.5f;
            public const float PhlogistonMul = 2f;
            public const float LowWaterSurge = 12f;
            public const float FloodVulnerability = 0.15f;
            public const float RestlessStillSeconds = 2f;
            public const float RestlessTaken = 0.15f;
            public const float QuicksandSpeed = 15f;
            public const float MountainDamage = 20f;
            public const float BecalmedCritPerHit = 0.01f;
            public const float GaleMul = 2f;

            // ---------------------------------------------------------------- the weapon classes
            public const float FletchingPierce = 15f;
            public const int RicochetBounces = 1;

            // ---------------------------------------------------------------- combinations
            public const float PhoenixRise = 0.40f;
            public const float PhoenixRadius = 3f;
            public const float DamasceneDamage = 25f;
            public const int DamasceneBasics = 3;
            public const float HuntSeconds = 3f;
            public const float CataclysmStagger = 1f;
            public const float GlassBonesBelow = 0.5f;
            public const float GlassBonesTaken = 0.25f;
            public const float SolNigerBeyond = 6f;
            public const float HaemophiliaFraction = 0.15f;
            public const float HaemophiliaSeconds = 3f;
            public const float SenescenceTaken = 0.02f;
            /// <summary>Bloodstone: one Damage point for every this-much health missing.</summary>
            public const float BloodstoneMissingPerPoint = 0.02f;
            public const float BloodstoneCap = 30f;
            public const float SlowFirePowerPerSecond = 2f;
            public const float SlowFireCap = 40f;
            public const float BlindsightCrit = 0.10f;
            public const float RetrogradeMotionMove = 40f;
            public const float RetrogradeMotionTakenMul = 0.70f;
        }

        /// <summary>
        /// The three principle chains - the identity half of the mastery board.
        ///
        /// Every node on a board carries a point in exactly one principle, and crossing a
        /// threshold (<see cref="Progression.MasteryBoard.Thresholds"/>, 8/16/22/28) unlocks the
        /// next link of that principle's chain. Four links each. The board holds roughly 47 points
        /// per principle and the cap is 50, so a capped character completes ONE chain outright and
        /// gets partway into a second - one completed, one tasted, one untouched. That shape is
        /// the whole reason the two-layer design exists, and it falls out of the cap rather than
        /// needing a rule.
        ///
        /// EVERY CHAIN TRIGGERS OFF A UNIVERSAL, COUNTABLE EVENT - attacks landed, hits taken,
        /// distance moved - and never anything element- or weapon-specific. That is what lets one
        /// principle layer sit under all four boards without secretly favouring one element's kit.
        ///
        /// EACH CHAIN CLOSES ON ITSELF at its last link: the final effect scales off the thing the
        /// chain spent its earlier links accumulating. More commitment produces a bigger final
        /// number rather than a flat one.
        /// </summary>
        public static class Principle
        {
            // ---------------------------------------------------------------- Sulfur: the volatile

            /// <summary>
            /// SEASON (link 1) - every landed BASIC marks its target.
            ///
            /// Basics only, deliberately. Sulfur is the aggression principle and its verb is
            /// sustained pressure; letting finishers season too would make the chain fire hardest
            /// off the swing that already pays best.
            /// </summary>
            public const int SeasonMaxStacks = 5;
            public const float SeasonSeconds = 6f;

            /// <summary>DETONATE (link 2) - an elemental release sets off every mark on the field.
            /// 0.4 of the player's own basic hit per stack (PlayerController.HitUnit), so five
            /// stacks on one body is two basics and a marked crowd is the real payoff - and it grows
            /// with the character, where a flat 0.4 of the BASE hit was outgrown by floor 30.</summary>
            public const float DetonateHitUnitsPerStack = 0.4f;

            /// <summary>SPLASH (link 3) - each detonation also catches what is near it.</summary>
            public const float DetonateSplashRadius = 1.6f;
            public const float DetonateSplashFraction = 0.35f;

            /// <summary>SWELL (link 4) - the splash grows with how many marks went off at once,
            /// which is the chain closing on itself. Capped so a very large crowd cannot run away
            /// with it.</summary>
            public const float DetonateSplashPerMark = 0.04f;
            public const float DetonateSplashFractionMax = 0.9f;

            // ---------------------------------------------------------------- Salt: the fixed

            /// <summary>WARD (link 1) - every hit TAKEN stacks temporary damage reduction.
            /// 8% a stack to a cap of five, so a character being worn down gets progressively
            /// harder to wear down - which is Salt's whole claim.</summary>
            public const float WardPerStack = 0.08f;
            public const int WardMaxStacks = 5;
            public const float WardSeconds = 5f;

            /// <summary>
            /// RETURN (link 2) - at full Ward the next hit taken reflects onto whatever is near,
            /// and the Ward is spent.
            ///
            /// Over 1.0 because it is paid for: five hits taken to arm it, and it costs the whole
            /// Ward. "Equivalent exchange" made literal in a combat mechanic.
            /// </summary>
            public const float ReturnFraction = 1.2f;
            public const float ReturnRadius = 3f;

            /// <summary>GUARD (link 3) - Return also buys a moment of real protection, so the hit
            /// that triggers it is not immediately followed by the one that kills you.</summary>
            public const float ReturnGuardSeconds = 1.2f;
            public const float ReturnGuardReduction = 0.4f;

            /// <summary>TEMPER (link 4) - Return scales with the character's own Resilience, so a
            /// tankier build hits back harder. The chain closing on itself.</summary>
            public const float ReturnPerResilience = 1.5f;

            // ---------------------------------------------------------------- Mercury: the fluid

            /// <summary>QUICKSILVER (link 1) - moving builds a charge. World units of travel for
            /// a full one; about three and a half seconds of running at base speed.</summary>
            public const float QuicksilverDistance = 22f;

            /// <summary>
            /// PHASE STRIKE (link 2) - at full charge the next hit ignores the target's armour.
            ///
            /// PENETRATION, NOT MORE DAMAGE, because Mercury's signature is amalgamation rather
            /// than combustion - it passes through rather than breaking. It is also the only
            /// answer in the game to a shielded elite other than chewing the shield down.
            /// </summary>
            public const float PhaseStrikeSeconds = 6f;

            /// <summary>SLIPSTREAM (link 3) - a Phase Strike refunds speed.</summary>
            public const float SlipstreamBonus = 0.45f;
            public const float SlipstreamSeconds = 1.5f;

            /// <summary>FLOW (link 4) - the refund lasts longer the further was travelled to earn
            /// it. Measured over the charge that was actually spent, so standing still and letting
            /// it sit does nothing. The chain closing on itself.</summary>
            public const float SlipstreamPerExtraDistance = 0.06f;
            public const float SlipstreamSecondsMax = 4f;
        }

        /// <summary>
        /// The daily reward taper - the run economy's anti-farming measure.
        ///
        /// IT APPLIES TO XP ONLY, AND NOT TO LOOT. That is a change from the original design note,
        /// which tapered both, and the reason is the extraction system that was built after it.
        ///
        ///     XP     accrues per floor cleared and is BANKED WHATEVER HAPPENS - EndRun grants it
        ///            on a death exactly as on an extraction. Nothing throttles it, so without a
        ///            taper an account can grind mastery without limit.
        ///
        ///     LOOT   is the run's STAKE. Lose without extracting and all of it is gone, and only
        ///            Diamond and Black Diamond are tradeable at all - both gated behind deep
        ///            floors. Risk, depth and tier-gating already throttle it three ways over.
        ///
        /// So tapering loot as well would tax the same thing twice, and it would do it in the worst
        /// possible shape: a player who pushed to 100 and died would lose every item AND face worse
        /// drop rates on the run they go back for. Losing everything and then finding it harder to
        /// replace is a compounding punishment for a bad night, and bad-night spirals are the one
        /// thing an extraction economy most needs to avoid - the loss is supposed to sting and then
        /// be answerable.
        ///
        /// A LOST RUN STILL SPENDS THE TAPER for the floors it cleared, and that is fair rather
        /// than harsh: it also BANKED the XP for those floors. The taper counts what was paid out,
        /// not what was survived.
        /// </summary>
        public static class Taper
        {
            /// <summary>
            /// Floors per day that pay FULL XP. Set to a full clear, so a player doing one deep run
            /// a day never feels the taper at all - it exists for grinding, not for playing.
            /// </summary>
            public const int FullUntilFloors = 100;

            /// <summary>
            /// The floor the curve settles to, and it is deliberately well above zero. An account
            /// is never told to stop playing, only that the marginal run pays less - a taper that
            /// reached zero would be a daily lockout wearing a curve's clothing.
            /// </summary>
            public const float MinMultiplier = 0.25f;

            /// <summary>
            /// XP multiplier for the next floor, given how many have been cleared today.
            ///
            /// Flat until the allowance, then 1/n - so the SECOND full clear of a day pays half,
            /// the third a third, settling at a quarter. Harmonic rather than exponential because
            /// exponential decay reaches "pointless" fast enough that a long session stops being
            /// worth playing, which is the outcome this is shaped to avoid.
            /// </summary>
            public static float Multiplier(int floorsToday)
                => floorsToday < FullUntilFloors
                    ? 1f
                    : UnityEngine.Mathf.Max(MinMultiplier, FullUntilFloors / (float)floorsToday);
        }

        /// <summary>
        /// Bosses.
        ///
        /// THE SHAPE, WHICH EVERY BOSS SHARES: a cadence of abilities the player has to LEARN,
        /// with the specific phrase randomised so it must be read live rather than memorised
        /// between attempts, and an enrage that plays the same material harder rather than
        /// introducing new material. A boss whose enrage is a different fight teaches nothing with
        /// its first two minutes.
        ///
        /// THE FIRST ONE - the Cantor - is a teleporting ranged boss on a floor cut into eight
        /// slices (see <see cref="Bosses.ArenaSectors"/>), and its cycle is three movements:
        ///
        ///     CALL     it teleports around the eight slices in a random order, firing a blast at
        ///              the player from each. Untouchable. This is the melody being played.
        ///     ANSWER   the same slices erupt, in the same order, as floor hazards. Untouchable.
        ///              This is the melody played back, and the player is inside it.
        ///     REST     it drops into the middle, stunned and open. The only window there is.
        ///
        /// The puzzle is that ANSWER is CALL. A player who watched where it stood already knows
        /// where not to stand, and is walking to safety while a player who did not is reacting.
        ///
        /// THE TELEGRAPH IS DELIBERATELY TOO SHORT TO CROSS A SLICE ON. A slice at the radius the
        /// player is likely to be at is about 3 units wide, so leaving one from its middle takes
        /// about 0.24s at MoveSpeed 6.5 - and the telegraph is 0.40s, which sounds generous until
        /// the player is also being pushed around by where the NEXT note lands. Reacting alone is
        /// possible and tight; remembering is what makes it comfortable. Neither a telegraph long
        /// enough to ignore the pattern nor none at all is the fight.
        /// </summary>
        public static class Boss
        {
            /// <summary>
            /// Health, and it is DERIVED from the windows rather than picked.
            ///
            /// A boss is only damageable during REST, so its health is really a statement about
            /// how many windows the fight lasts. The reference player is an unbuffed greatsword on
            /// the Medium chain: 2 basics + a 5x finisher is 7 basics of damage over 3.5 basic
            /// intervals, so 70 damage per 1.47s - about 47.6 DPS.
            ///
            /// A REST of 5.5s is not 5.5s of swinging. The player has to arrive: up to
            /// TeleportRadius away at MoveSpeed 6.5 is about 0.85s, leaving ~4.65s, which is three
            /// full chains (4.41s) with a little slack rather than three-and-a-fraction. Three
            /// chains is 210 damage, and it is countable - a player can feel that the window is
            /// exactly three.
            ///
            /// 840 is four of those windows. With <see cref="EnrageAt"/> at 35% the fight comes
            /// out as three normal cycles and two shorter enraged ones - learn it, execute it
            /// twice, then survive it - and lands around a minute, which is a boss rather than an
            /// elite with more health.
            ///
            /// This is the FLOOR-1 figure: scaled by Enemies.FloorDifficulty.BossHp (HpPerFloor),
            /// which follows the ledger's growth so a typical player at any depth still meets
            /// about four windows. A geared character kills it faster, which is what gear is
            /// for - but never in fewer windows than WindowCap allows.
            /// </summary>
            public const float Hp = 840f;

            /// <summary>Boss health per floor, as a fraction of Hp. 0.03 tracks the ledger's
            /// measured x3.70 by floor 100, so the reference player's window count holds with
            /// depth. Deliberately NOT Enemy.HpPerFloor (0.09) - see FloorDifficulty.BossHp.</summary>
            public const float HpPerFloor = 0.03f;

            /// <summary>
            /// The most of a boss's MAX health one REST window can take. Once it is reached the
            /// boss closes early (Health.Floor, then the window ends) - it does not stand there
            /// absorbing hits for nothing.
            ///
            /// THIS IS WHAT KEEPS THE FIGHT A FIGHT AT ENDGAME. Max gear and a capped board put a
            /// player near 4.8x the reference DPS before the ledger, which empties an uncapped
            /// boss inside one window and skips the cadence the fight is built to teach. 0.30 means
            /// 100 -> 70 -> 40 -> 10 -> dead: at least four windows, the fourth after EnrageAt -
            /// three cycles learned and executed, one survived - whatever is brought to it. Gear
            /// still pays: a strong player reaches the cap in the first seconds of the window and
            /// is out of danger sooner. Every boss uses it.
            /// </summary>
            public const float WindowCap = 0.30f;

            /// <summary>Fraction of health at which the cycle compresses. With WindowCap at 0.30
            /// this lands after the third window.</summary>
            public const float EnrageAt = 0.35f;

            /// <summary>How far out the boss stands when it appears in a slice. Inside the arena's
            /// SHORT half-extent (7) with clearance, so the ring is a circle rather than an
            /// ellipse squashed to the room.</summary>
            public const float TeleportRadius = 5.5f;

            // ---- CALL: the melody ----

            /// <summary>Notes in the first cycle's phrase, and one more per cycle after it. Capped
            /// at the eight slices - a phrase longer than the alphabet it is drawn from stops
            /// being a phrase and becomes a list.</summary>
            public const int BaseNotes = 5;
            public const int MaxNotes = ArenaSectorCount;

            /// <summary>Seconds per note while calling, and enraged. The gap is what the player
            /// reads the pattern in.</summary>
            public const float CallNoteSeconds = 0.85f;
            public const float CallNoteSecondsEnraged = 0.60f;

            /// <summary>How long the boss holds a slice before firing, so an appearance is a beat
            /// rather than a hit.</summary>
            public const float CallAimSeconds = 0.35f;

            /// <summary>CHIP damage: a floor-1 figure, scaled by FloorDifficulty.Damage like every
            /// other enemy's. Flat, so MaxHp still buys room against it.</summary>
            public const float BlastDamage = 12f;
            public const float BlastSpeed = 10f;
            public const float BlastLifetime = 3f;

            // ---- ANSWER: the melody played back ----

            /// <summary>Seconds per note while answering. Slightly tighter than the call, because
            /// the player already heard it once.</summary>
            public const float AnswerNoteSeconds = 0.75f;
            public const float AnswerNoteSecondsEnraged = 0.55f;

            /// <summary>How long a slice glows before it erupts - see the class header for why
            /// this is deliberately short.</summary>
            public const float TelegraphSeconds = 0.40f;
            public const float TelegraphSecondsEnraged = 0.30f;

            /// <summary>How long an eruption stays lethal. Non-zero so it is a place rather than a
            /// frame - a single-frame test would miss a player crossing it and would be pure luck
            /// at low frame rates.</summary>
            public const float EruptionSeconds = 0.25f;

            /// <summary>
            /// What standing in an erupting slice costs, as a fraction of the player's MAX health -
            /// a MECHANIC hit, so it costs the same at floor 10 and floor 90 whatever the build.
            /// A flat number goes stale as health grows and the read stops mattering.
            ///
            /// About a quarter (the old flat 22 against an Air character's 85), so a hazard phase
            /// read badly is most of a health bar and one mistake is not the run. MITIGATED like
            /// any hit (it goes through Health.Take, so Vulnerability - Graze, Brace, Resilience,
            /// worn armour - applies): defensive gear must still matter where it matters most.
            /// MaxHp does not help against it, by design - mechanic hits ignore the build's size.
            /// Heals that answer it (the floor reward, the spire) are fractions too, so the two
            /// stay in proportion.
            /// </summary>
            public const float EruptionFraction = 0.26f;

            /// <summary>ENRAGED, THE ANSWER COMES IN TWO PARTS: the note's own slice and the one
            /// opposite it. Two of eight leaves six safe, so it is a harder read rather than an
            /// unsurvivable one - and it doubles the material without inventing any.</summary>
            public const bool EnragedAnswersOpposite = true;

            // ---- REST: the window ----

            public const float StunSeconds = 5.5f;
            public const float StunSecondsEnraged = 3.5f;

            /// <summary>Beat between movements, so the fight reads as phrased rather than
            /// continuous.</summary>
            public const float BeatSeconds = 0.7f;

            /// <summary>
            /// The mini-boss cadence, in floors - every tenth floor is a boss floor.
            ///
            /// It used to set where Rifts opened too. Rifts are now drawn per floor by
            /// Rifts.FloorPlanner (see Tuning.Floors), with a guaranteed one only after each avatar.
            /// </summary>
            public const int RiftInterval = 10;

            /// <summary>How many carried pieces fit through a Rift before the capacity domain is
            /// spent on. One, so the first real decision is which ONE.</summary>
            public const int BaseRiftCapacity = 1;

            /// <summary>
            /// Chance any ENEMY drops a Rift Box, PER BASIC CHASER IT IS WORTH - its wave cost over
            /// a Chaser's on the same floor (Enemies.WaveComposer). An elite is likelier to drop
            /// one than a Bubbles, and a floor pays in proportion to its pool whatever mix it rolled.
            ///
            /// PER ENEMY RATHER THAN PER FLOOR, and the reason is not parity - it is that pools
            /// already grow with depth, so a per-enemy rate makes boxes scale with how deep the run
            /// went without needing a second rule to say so. "Deeper floors pay better" falls out
            /// of it.
            ///
            /// THE ARITHMETIC, because "one in N" is meaningless without the population. Measured
            /// off the composer: a run through floor 24 fights about 149 Chasers' worth, floor 50
            /// 485, floor 75 863, and a full clear 1166:
            ///
            ///     rate      f24    f50    f75    f100
            ///     1/250     0.6    1.9    3.5     4.7
            ///     1/500     0.3    1.0    1.7     2.3     &lt;- here
            ///     1/1000    0.15   0.5    0.9     1.2
            ///
            /// The table this replaced (0.4 / 1.6 / 3.3 / 5.6 at 1/500, "five or six a full clear")
            /// was written for UNCAPPED waves - 2800 bodies to floor 100. The 14-enemy cap halved
            /// the real yield without the rate moving, and the budget kept that yield rather than
            /// silently doubling it. If five or six a full clear is still the intent, 1/250 is it.
            ///
            /// 1/1000 WAS CONSIDERED AND IS TOO RARE: a player meets the mechanic too seldom for
            /// the pickup, its prompt or the spend decision to ever become familiar. The old
            /// per-floor 1/6 was too generous in the other direction - four boxes against a base
            /// capacity of one meant securing five pieces at a single Rift, which flattens the
            /// exact tension the Rift exists to create.
            /// </summary>
            public const float RiftBoxDropChance = 1f / 500f;

            /// <summary>
            /// How long a dropped box lasts before it collapses.
            ///
            /// THE TIMER IS WHAT GIVES THE INTERACT REQUIREMENT TEETH. Without one the box waits
            /// forever, so the player clears the floor at leisure and strolls over - and "picking
            /// it up costs you a beat mid-fight", which is the whole reason it is a press rather
            /// than a walk-over, is not true of anything. A timer turns the press from friction
            /// into a decision: break off now, or trust you can finish this pack first.
            ///
            /// IT IS NOT ABOUT REACHABILITY, and the numbers say so. The arena's full diagonal is
            /// 27.8 units and the player moves at 6.5, so the worst sprint in the game is 4.3s -
            /// ten seconds is two and a third times that. What ten seconds is short of is the
            /// FLOOR: a full chain (2 basics + a Medium) takes 1.47s, so the window holds about
            /// seven, against 8 enemies on floor 10 and 15 on floor 24. Enough to finish what is
            /// on top of you, never enough to finish the room.
            ///
            /// IT MUST BE VISIBLY WOUND DOWN, which is the condition on the whole idea. Losing a
            /// one-in-five-hundred drop is only acceptable if the player watched it happen and
            /// chose to keep swinging - a box that vanished without warning would be exactly the
            /// reflex test this was nearly rejected for being.
            ///
            /// Scaled time, so opening the loadout mid-fight does not burn it.
            /// </summary>
            public const float RiftBoxLifeSeconds = 10f;

            /// <summary>Floors that hold a boss. The reincarnation bosses of the run economy sit
            /// at 25 / 50 / 75 / 100; only the first exists so far.</summary>
            public const int FirstBossFloor = 25;

            /// <summary>Mirrors ArenaSectors.Count. Restated because Tuning may not reference the
            /// Bosses namespace, and asserted against it in ArenaSectors' own build.</summary>
            public const int ArenaSectorCount = 8;
        }

        /// <summary>
        /// Medusa, the second mini-boss - see <see cref="Bosses.Medusa"/>. She never attacks the
        /// player directly. The fight is one rule - be behind a pillar when she looks - and the
        /// adds, debris and push/pull are what make keeping it hard.
        ///
        ///     BREAK      she shatters one pillar (with a push or pull, from PushPullFromFloor)
        ///     TELEGRAPH  the arena fills red everywhere her gaze would reach
        ///     GAZE       anyone in the open: GazeFraction of max HP, then STONE for the window
        ///     WINDOW     she is open, capped by Boss.WindowCap like every boss
        ///     DEBRIS     falling circles; once half the ring is gone they bring pillars back
        /// </summary>
        public static class Medusa
        {
            /// <summary>Pillars in the ring, evenly spaced, the first straight below her so the
            /// player's arrival point (Arena.SouthSpawnPoint) starts in cover.</summary>
            public const int PillarSlots = 8;

            /// <summary>Ring radius around her. At 4 a Medium pillar's shadow is ~20 degrees, so
            /// eight cover a little under half the room - the rest is red.</summary>
            public const float RingRadius = 4f;

            /// <summary>Breakable by her AND by the player. Higher than a hazard column's so a
            /// stray swing does not take the player's own cover by accident; a deliberate one
            /// still can.</summary>
            public const float PillarHp = 60f;

            /// <summary>The empty slots refill once at least this share of the ring is gone.</summary>
            public const float RebuildAtMissingFraction = 0.5f;

            /// <summary>The pillar she is about to break shakes and glows this long first, so the
            /// player leaves its shadow before the cover vanishes.</summary>
            public const float BreakWarnSeconds = 1.0f;

            /// <summary>How long the red takes to fill before she looks. The first one is longer:
            /// the player has never seen the rule yet.</summary>
            public const float TelegraphSeconds = 2.4f;
            public const float TelegraphSecondsEnraged = 1.9f;
            public const float OpeningTelegraphSeconds = 3.6f;

            /// <summary>
            /// How long she LOOKS. Anyone exposed and not immune at any moment of it is caught. Held
            /// rather than one instant so a dash's i-frames (0.18s) cannot step through a gaze - you
            /// cannot dodge a look. A leap that keeps the player airborne the whole time can.
            /// </summary>
            public const float GazeHoldSeconds = 0.45f;

            /// <summary>The gaze hit, as a share of the player's max health. A MECHANIC hit like the
            /// Cantor's eruption: mitigated through Health.Take, so Brace and the ledger still count.</summary>
            public const float GazeFraction = 0.5f;

            /// <summary>
            /// STONE SKIN: the statue's incoming-damage multiplier, before depth. At the point of use
            /// it is multiplied by FloorDifficulty.AttackInterval(floor) and divided by
            /// FloorDifficulty.Damage(floor) - both depth curves cancelled - so on a statue every
            /// add hits as hard and as often as a floor-1 enemy would. Depth makes adds dangerous
            /// everywhere except on the stone.
            ///
            /// Sized against the worst case: MaxAdds chasers on the statue for the WHOLE window.
            /// Three floor-1 chasers are 3 x 15 / 1.3s = 34.6 DPS; x 4.5s x 0.15 = 23 before the
            /// player's own vulnerability. The thinnest base character (Air, 85) caught at full
            /// health keeps 42 and survives the adds with ~20; at a vulnerability of 1.2 it keeps
            /// 34, takes 28, and still survives. A geared one has far more room. A player caught
            /// hurt, or a second time, can die - by design.
            ///
            /// 0.35 without the cadence term was tried first and killed a 163-HP character at
            /// floor 40 (three adjacent chasers, ~80 damage against 67 left after the gaze).
            /// </summary>
            public const float StoneSkinBase = 0.15f;

            /// <summary>The window. Long enough to cross from behind a pillar to her and land a
            /// chain or two: ~4 units at MoveSpeed 4.1 is a second.</summary>
            public const float WindowSeconds = 4.5f;
            public const float WindowSecondsEnraged = 3.5f;

            public const float EnrageAt = 0.35f;

            /// <summary>A beat between the moves, so each reads as its own.</summary>
            public const float BeatSeconds = 0.5f;

            /// <summary>Debris: circles land over DebrisSpreadSeconds, each warned for
            /// DebrisWarnSeconds. A mechanic hit, a share of max health, one test at impact.</summary>
            public const int DebrisCount = 5;
            public const int DebrisCountDeep = 7;        // from AddsFromFloor
            public const float DebrisRadius = 1.1f;
            public const float DebrisWarnSeconds = 1.1f;
            public const float DebrisSpreadSeconds = 1.4f;
            public const float DebrisFraction = 0.15f;

            /// <summary>Adds: summoned at the top of each cycle from this floor, never more than
            /// MaxAdds alive. They are her only pressure - she does not attack.</summary>
            public const int AddsFromFloor = 30;
            public const int AddsPerCycle = 2;
            public const int MaxAdds = 3;
            /// <summary>Ranged adds join the chasers from here. Pillars block their shots too.</summary>
            public const int RangedAddsFromFloor = 60;

            /// <summary>Push / pull on the BREAK beat, from this floor. A push shoves the player
            /// away from her (walls and pillars stop it); a pull drags them THROUGH the ring to
            /// PullToRadius, into the open, just before the red starts.</summary>
            public const int PushPullFromFloor = 40;
            public const float PushPullWindupSeconds = 0.7f;
            public const float PushDistance = 3.5f;
            public const float PullToRadius = 2.0f;
            public const float PushPullSpeed = 14f;

            public const float Hp = 840f;    // same base as the Cantor, through FloorDifficulty.BossHp
        }

        /// <summary>
        /// The gear stake - see <see cref="Chain.GearStake"/>. The floor a staked piece's run must
        /// clear before extracting pays out its matching piece, by the stake's star level.
        ///
        /// SCALED BY STARS because a flat gate is a farm: at floor 10 a competent player survives
        /// nearly every run, and a guaranteed combine partner for no real risk is the Forge's
        /// hardest search handed out free. The higher the stake, the more it is worth losing, and
        /// the deeper it has to be carried.
        ///
        /// A THREE-star stake's partner PROMOTES it (the Forge turns two 3-star pieces into the
        /// next tier at base), which is a bigger prize than a star and sits deeper again. Gold
        /// three-star is the cap and cannot be staked at all.
        /// </summary>
        /// <summary>
        /// What each floor IS - see <see cref="Rifts.FloorPlanner"/>. Boss floors are fixed; every
        /// other floor is drawn by weight, and a category's weight GROWS for every floor it has
        /// been missing, so randomness never turns into a drought.
        ///
        /// A category's chance on a given floor is its weight over the sum:
        ///
        ///     combat  CombatWeight                                (constant)
        ///     rift    RiftBase + RiftStep * floors since a Rift was REACHED
        ///     puzzle  PuzzleBase + PuzzleStep * floors since a puzzle
        ///
        /// "Reached" matters: a Collapsing Rift whose timer ran out does not reset the Rift count,
        /// so a failed one is followed by better odds rather than by a full reset. And past
        /// MaxRiftGap floors the next eligible floor is a Rift outright - and never a Collapsing
        /// one, or a player failing timers could fail the guaranteed exit too.
        ///
        /// Numbers here are a STARTING POINT to be read against FloorPlanner.Simulate, which
        /// reports how often each category appears, the gap between exits, and how often a base
        /// stake (gate 20) finds an exit before the floor-25 avatar.
        /// </summary>
        public static class Floors
        {
            public const float CombatWeight = 1f;

            /// <summary>No Rift rolls before this floor - an exit on floor 2 is a way out of a
            /// run that has not had anything in it yet.</summary>
            public const int RiftMinFloor = 4;
            public const float RiftBase = 0.04f;
            public const float RiftStep = 0.07f;

            /// <summary>The longest run of eligible floors without a REACHED Rift; the floor after
            /// is a Blue Rift outright. A rule rather than a probability, because the player
            /// carrying a Diamond toward floor 98 cares about the worst case, not the mean.</summary>
            public const int MaxRiftGap = 9;

            /// <summary>
            /// After a Collapsing Rift runs out, no Rift of any kind for this many floors (drawn
            /// from the run seed between the two). A failed timer answered by an exit on the very
            /// next floor would make the failure cost nothing and the next Rift feel handed out -
            /// both kill the tension the timer exists for. The Rift count keeps climbing through
            /// the quiet stretch, so the odds are high once it ends, without being certain. The
            /// gap limit waits it out too.
            /// </summary>
            public const int QuietAfterCollapseMin = 2;
            public const int QuietAfterCollapseMax = 3;

            /// <summary>Once a floor is a Rift, which kind. Collapsing waits until enough ordinary
            /// floors have been timed for the clock to be fitted to the player (see
            /// CollapsingRift.PaceSamples).</summary>
            public const float BlueShare = 0.45f;
            public const float CollapsingShare = 0.35f;
            public const int CollapsingMinFloor = 6;

            /// <summary>
            /// A RED Rift is torn shut at the first wave with a guard of elites at it; the floor's
            /// clear opens it. No extra reward, by decision - the luck of the draw. Never on a
            /// boss floor (a boss floor is the boss and nothing else).
            /// </summary>
            public const float RedShare = 0.20f;
            /// <summary>Elites guarding a Red Rift, on top of the floor's own wave; one more from
            /// RedGuardsExtraFloor. Spawned beside the tear, past the on-screen cap.</summary>
            public const int RedGuards = 2;
            public const int RedGuardsExtraFloor = 50;
            /// <summary>How far from the player a Red Rift is torn - farther than an open one, so
            /// its guard does not arrive on top of them.</summary>
            public const float RedTearDistance = 7f;

            /// <summary>Puzzle floors (see Tuning.Puzzle). Same growing-weight rule as the Rift:
            /// the longer since the last one, the likelier the next. Never on a boss floor.</summary>
            public const int PuzzleMinFloor = 3;
            public const float PuzzleBase = 0.02f;
            public const float PuzzleStep = 0.015f;
        }

        /// <summary>
        /// Puzzle floors - Puzzles/. The floor's exit is a SHUT sigil door; solving the puzzle
        /// opens it and the floor pays exactly as a cleared one would (drop, exchange, reward).
        /// A wrong answer seals it. An open SIDE DOOR stands far left or right the whole time:
        /// through it is the adjacent room, the same floor's ordinary fight. Giving up and
        /// failing land in the same place.
        /// </summary>
        public static class Puzzle
        {
            public const float PlateRadius = 0.6f;
            /// <summary>The side door's distance in from the east/west wall, on the door line.</summary>
            public const float SideDoorInset = 2.4f;

            /// <summary>ECHO - watch and repeat. Stones light in a phrase; step them in order.
            /// Longer every EchoLengthEvery floors. No immediate repeats, like the Cantor's.</summary>
            public const int EchoStones = 6;
            public const float EchoRingRadius = 3.0f;
            public const int EchoLengthBase = 4;
            public const int EchoLengthEvery = 25;
            public const int EchoLengthMax = 7;
            public const float EchoNoteSeconds = 0.55f;
            public const float EchoGapSeconds = 0.25f;

            /// <summary>ELEMENTS - four stones, an order deduced from clues. From this floor the
            /// clues stop naming a first or last stone outright.</summary>
            public const int ElementsHardFloor = 50;

            /// <summary>LIGHTS - a 3x3 of stones; stepping one flips it and its four neighbours.
            /// Scrambled by this many presses from all-lit (so it is always solvable, and the
            /// scramble IS the shortest answer), with LightsSlack steps to spare.</summary>
            public const float LightsSpacing = 2.0f;
            public const int LightsPressesBase = 3;
            public const int LightsPressesEvery = 35;
            public const int LightsPressesMax = 5;
            public const int LightsSlack = 3;
        }

        /// <summary>
        /// The Collapsing Rift's clock - see <see cref="Rifts.Rift"/>. It appears unstable as the
        /// floor's first wave arrives; clear the floor before the countdown ends and it
        /// stabilises, otherwise it collapses.
        ///
        /// FITTED TO THE PLAYER, BOUNDED BY THE FLOOR. A fixed number per floor cannot be right:
        /// any timer fair to a weak build is meaningless to a strong one. So the clock is the
        /// player's own median clear time over their last PaceSamples ordinary floors, times
        /// Margin - and clamped to a band around the floor's ESTIMATE, so clearing slowly on
        /// purpose to bank a generous timer only gets as far as the band's edge.
        ///
        /// MARGIN IS THE ONE NUMBER BETWEEN FALSE TENSION (too much) AND FALSE HOPE (too little),
        /// and it is the one to tune by playing.
        /// </summary>
        public static class CollapsingRift
        {
            public const float Margin = 1.2f;
            public const int PaceSamples = 3;

            /// <summary>
            /// The floor's own estimate is its wave's target seconds of DAMAGE times this - a clear
            /// is longer than the damage in it (walking in, chasing, dodging). 1.4 is the old
            /// measured ratio (a 45s clear around 33s of damage at floor 10). Used alone until
            /// PaceSamples clears exist.
            /// </summary>
            public const float ClearPerDamageSecond = 1.4f;
            public const float BandLow = 0.7f;
            public const float BandHigh = 1.5f;
            public const float MinSeconds = 15f;

            /// <summary>The last stretch in which the tear flickers faster - the clock felt in the
            /// corner of the eye rather than read off the HUD mid-fight.</summary>
            public const float UrgentSeconds = 6f;
        }

        public static class Stake
        {
            public const int GateBase = 20;        // base -> 1 star: second boss, past the first Rift
            public const int GateOneStar = 25;     // 1 -> 2 stars: the first avatar
            public const int GateTwoStar = 50;     // 2 -> 3 stars: the second avatar
            public const int GatePromotion = 75;   // 3 stars -> next tier: the third avatar
        }

        /// <summary>
        /// Finisher weight classes - see <see cref="Combat.FinisherWeight"/>.
        ///
        /// Damage is expressed in BASIC ATTACKS rather than absolute numbers, so the tiers keep
        /// their meaning if <see cref="Player.BaseDamage"/> ever moves. Locks are multiples of a
        /// basic swing's own interval.
        ///
        /// The chain is two basics plus a finisher (<see cref="Player.PlayerController"/>'s
        /// BaseBasicsPerChain), so a full chain pays:
        ///
        ///     Light    2 + 3    = 5.0   over 3.0 time   ->  1.67 DPS
        ///     Medium   2 + 5    = 7.0   over 3.5 time   ->  2.00 DPS
        ///     Heavy    2 + 6.5  = 8.5   over 4.5 time   ->  1.89 DPS
        ///
        /// THE LOCKS WERE CHOSEN TO SHAPE THAT CURVE, NOT TO EQUALISE IT. Medium is the efficient
        /// default; Heavy pays about 6% output for flinching through armour and knocking back;
        /// Light pays about 17% for speed and the shortest commitment. Solving for true parity
        /// instead puts Medium at 2.2x and Heavy at 3.1x - absurdly slow, and it leaves Light
        /// strictly the worst option with nothing to compensate.
        ///
        /// HEAVY AT 2.0x DOES NOT WORK, recorded so it does not get "fixed" there later: it comes
        /// out at 2.13 DPS, ABOVE Medium, which makes it strictly best - more damage AND more
        /// control for a marginally longer lock. 2.5x is what keeps it honest.
        ///
        /// TRAP: at a ~0.35s basic, Heavy's lock lands near 0.88s, past AttackMotions'
        /// SwingMaxSeconds (0.55). Either that clamp rises for Heavy or Heavy's cadence comes down
        /// and its damage with it - left alone the clamp shortens the animation while the damage
        /// stays put, which pushes Heavy above Medium and makes it the best tier by accident. Same
        /// class of bug as the attack gate's clamped-animation / unclamped-cooldown disagreement.
        /// </summary>
        public static class Finisher
        {
            public const float DamageLight = 3.0f;
            public const float DamageMedium = 5.0f;
            public const float DamageHeavy = 6.5f;

            public const float LockLight = 1.0f;
            public const float LockMedium = 1.5f;
            public const float LockHeavy = 2.5f;

            /// <summary>
            /// How long a flinched enemy is interrupted for.
            ///
            /// There is deliberately NO per-enemy flinch cooldown, because the chain already is
            /// one: basics never flinch, so even an all-Heavy wheel lands at most one flinch per
            /// chain. That converts the perma-lock worry into a single constraint - THIS MUST STAY
            /// UNDER THE FASTEST POSSIBLE CHAIN CYCLE. Check it against the compressors, not the
            /// base cadence: Air's momentum cap alone is 2.0x attack speed, and the exchange
            /// ledger's BasicsPerChainDelta can shorten the chain itself.
            /// </summary>
            public const float FlinchSeconds = 0.4f;
        }

        /// <summary>
        /// Shadow, the black-diamond greatsword, and the Echo finisher it carries.
        ///
        /// EVERYTHING SHADOW DOES IS HALF, and that is the whole spine of the item: the chain's
        /// second hit is half, and each summoned echo casts its finisher at half. One number to
        /// remember, and it is the same one the exchange ledger's `echo` boon already uses for
        /// the same idea - a repeated hit is worth half of the hit it repeats. Keeping the two
        /// equal means the boon stacking on top of this reads as "more often", never as "and
        /// also harder".
        /// </summary>
        /// <summary>
        /// Tria Prima's signature, Separatio: the blade held up, then the character splits into
        /// three figures - Sulfur, Salt, Mercury - each holding one of the three swords the fused
        /// blade is made of, each striking a different way, before they fold back into one.
        /// </summary>
        public static class Separatio
        {
            /// <summary>
            /// Seconds the fused blade is held up before it splits - the charged-blade pose. Short
            /// on purpose: the move's damage is a MEDIUM finisher's, and Skyfall's 1.5s is what a
            /// Heavy pays. This is a tell, not a price.
            /// </summary>
            public const float HoldSeconds = 0.4f;

            /// <summary>
            /// Each figure's share of the step's declared damage. Three thirds of a Medium (5
            /// basics) is exactly one Medium - the move's value is the three DIRECTIONS, not more
            /// damage. Change the step's DamageMultiplier, never this, to retune its strength.
            /// </summary>
            public const float FigureDamageFraction = 1f / 3f;

            /// <summary>How far out each figure stands from the player as it strikes. Small - the
            /// three have just come out of one body.</summary>
            public const float FigureOffset = 0.45f;

            /// <summary>Seconds between one figure's strike and the next, so three hits read as a
            /// ripple rather than one thump.</summary>
            public const float FigureStaggerSeconds = 0.07f;

            /// <summary>Seconds a figure lingers after its swing before it fades out.</summary>
            public const float FigureFadeSeconds = 0.18f;

            /// <summary>
            /// The figures' multiply tints, in Sulfur, Salt, Mercury order - the same order as
            /// GearItem.SplitBlades. A MULTIPLY, like Shadow's, so the art keeps its own shading
            /// and simply takes the principle's colour; alpha is how solid the figure is.
            /// </summary>
            public static readonly UnityEngine.Color[] FigureTints =
            {
                new(1.00f, 0.92f, 0.45f, 0.82f),   // sulfur
                new(0.96f, 0.96f, 1.00f, 0.82f),   // salt
                new(0.70f, 0.82f, 1.00f, 0.82f),   // mercury
            };

            /// <summary>The player's own figure while split - a pale, see-through copy of the
            /// three, so for that beat the character "looks like a clone" as well.</summary>
            public static readonly UnityEngine.Color SplitTint = new(0.72f, 0.74f, 0.86f, 0.38f);

            /// <summary>Seconds per frame of the Pacemaker's bead - 36 frames, about two seconds a
            /// circuit, so the pacing light fires at a resting heart rate.</summary>
            public const float BeadFrameSeconds = 0.055f;
        }

        /// <summary>
        /// The Armillary's signature, QUINTESSENCE: the two halves leave the hands and join
        /// OVERHEAD into the whole four-element armillary, it holds there while four REVERSE CONES
        /// strike - one per side, widest AT the character and narrowing to a point outward - and
        /// then it comes apart and the halves return to the hands. A placeholder shape for the move
        /// the user will design; the flow (combine, hold, separate) is the part that stays.
        /// </summary>
        public static class Quintessence
        {
            /// <summary>Seconds for the halves to rise from the hands and join overhead.</summary>
            public const float MergeSeconds = 0.20f;

            /// <summary>Seconds between one cone and the next, round the four sides.</summary>
            public const float ConeStaggerSeconds = 0.09f;

            /// <summary>Seconds the whole armillary stays up after the last cone.</summary>
            public const float LingerSeconds = 0.22f;

            /// <summary>Seconds for it to come apart and the halves to return to the hands.</summary>
            public const float SplitSeconds = 0.18f;

            /// <summary>
            /// Each cone's share of the step's declared damage. Four quarters of a Medium is
            /// exactly one Medium, the same arithmetic as Separatio's thirds: the move buys four
            /// directions, not more damage. Retune the step's DamageMultiplier, never this.
            /// </summary>
            public const float ConeDamageFraction = 0.25f;

            /// <summary>A cone's full width AT the character, as a fraction of its length - wide,
            /// so the four meet round the character and nothing standing close escapes them.</summary>
            public const float ConeBaseWidthFraction = 0.9f;

            /// <summary>How high above the character's feet the armillary is held, in units.</summary>
            public const float OverheadHeight = 1.05f;

            /// <summary>The whole armillary's size overhead, against one half in the hand.</summary>
            public const float OverheadScale = 1.25f;
        }

        public static class Shadow
        {
            /// <summary>
            /// What the chain's second, echoed hit deals, as a fraction of the first.
            ///
            /// The user's brief said the attack "hits twice", and a literal second hit at full
            /// strength is +100% damage on every swing of the run - three to five times what any
            /// other single item in the game is worth. Half is what the repeat is worth
            /// everywhere else it appears, so the passive lands at +50% sustained: enormous for
            /// one item, which is what a ceiling-tier weapon is meant to be, and still a number
            /// the rest of the damage model can hold. THIS IS THE LEVER - if Shadow is too
            /// strong, it is almost certainly this and not the finisher.
            ///
            /// 0.60, up from 0.5, when finishers gained the timing bar (Tuning.StrikeTiming).
            /// Shadow's finisher slot is a basic, so it has no bar - and it was priced ~10% under
            /// a Light chain to pay for never locking. Left at 0.5 it would sit ~10% under a
            /// Light player at GOOD but ~16% under one landing PERFECT, which is the player the
            /// deep floors are balanced against. At 0.60 a 2-basic Shadow chain (Cut 0.85,
            /// Answer 0.85, Echo 1.0 over 2.72 intervals) deals 1.59 per interval against a Light
            /// chain's 1.77 at PERFECT (10% under, the original pairing) and 1.67 at GOOD (5%
            /// under). The lock exemption still costs something; skill no longer does.
            /// </summary>
            public const float ChainEchoFraction = 0.60f;

            /// <summary>Seconds an echo figure is drawn for, beyond the swing it plays.</summary>
            public const float EchoFadeSeconds = 0.22f;

            /// <summary>
            /// The ghost's colour. MULTIPLIED over the rig, so it can only ever darken what is
            /// already there - which is the whole reason a single flat colour works on art that
            /// carries its own RGB.
            ///
            /// Not as dark as "a shadow" suggests, and that is a correction: at 0.30 brightness
            /// and 0.55 alpha the figures measured darker than the arena floor itself and simply
            /// were not there. A shadow cast on a dark ground has to be LIGHTER than the ground
            /// to be seen at all - what makes it read as a shadow is the violet cast and the
            /// transparency, not the absolute brightness.
            /// </summary>
            public static readonly UnityEngine.Color Tint = new(0.46f, 0.38f, 0.66f, 0.82f);
        }

        /// <summary>The Phantom weapon/relic pair: a purely cosmetic per-swing teleport, and a
        /// Heavy signature finisher that leaps and slams. Neither number here touches damage
        /// resolution - the flicker moves nothing but the drawing, and the finisher's own numbers
        /// are priced the same way every other Heavy signature is.</summary>
        public static class Phantom
        {
            /// <summary>The haze's colour - fixed to the weapon rather than derived from the
            /// played element, the same way Shadow's own tint is. A ghost has one colour whatever
            /// you are playing; letting the element re-tint it would make Phantom read as a
            /// generic effect wearing whatever the current build's colour happens to be, rather
            /// than as its own thing.</summary>
            public static readonly UnityEngine.Color Tint = new(0.58f, 0.30f, 0.78f, 0.85f);

            /// <summary>
            /// Seconds each baked haze frame holds before advancing to the next - see
            /// DemoGear.BuildPhantomFrames and PhantomHaze. Slow enough to read as gas drifting
            /// rather than a flicker: real smoke rolls over itself lazily, and a fast cycle here
            /// would read as a glitching sprite rather than something alive.
            /// </summary>
            public const float HazeFrameSeconds = 0.16f;

            /// <summary>How far the poof can land, as a fraction of the player's own melee reach.
            /// Short of 1 so the character never poofs to the very rim of the ring the player is
            /// reading as their reach - a poof that occasionally lands OUTSIDE what the ring
            /// implies would look like the ring was lying.</summary>
            public const float PoofRadiusFraction = 0.72f;

            /// <summary>Fraction of the swing's own window spent poofed out before returning -
            /// short of the whole swing, so the return itself is visible as part of the read
            /// rather than the character simply popping back on the last frame.</summary>
            public const float PoofDurationFraction = 0.62f;

            /// <summary>Radius of the small departure/return flash, in world units.</summary>
            public const float PoofFlashRadius = 0.30f;

            // ---- the signature finisher ----

            /// <summary>Seconds spent airborne - see AttackStep.LeapSeconds. Shorter than
            /// Meteor's: this is a vertical hop over the target, not a screen-clearing flight, so
            /// the read is quick and the recovery cost below can stay honest rather than needing
            /// to be inflated to pay for a long trip.</summary>
            public const float LeapSeconds = 0.6f;

            /// <summary>Seconds of amplified damage on landing - the same trade every leap
            /// finisher makes for its invulnerable window, see AttackStep.ExposedSeconds.</summary>
            public const float ExposedSeconds = 1.4f;
            public const float ExposedMultiplier = 1.6f;

            /// <summary>Damage at the rim of the slam relative to its centre - a blast, not a
            /// swing, same as every other AreaOfEffect finisher.</summary>
            public const float EdgeDamageFraction = 0.35f;
        }

        /// <summary>The Blood Blade weapon/relic pair: a single-target Medium finisher that
        /// stains the fuller a little deeper every time it lands, and a Heavy AoE release once
        /// the fuller is full. Both halves are ordinary weight-class numbers - see
        /// Combat/AttackStep.ReleaseStep for how the two are wired as one wheel slot.</summary>
        public static class Blood
        {
            /// <summary>The blade's own colour - fixed rather than derived from the played
            /// element, the same reasoning Shadow's and Phantom's tints already carry. Blood is
            /// red whatever build is holding the sword.</summary>
            public static readonly UnityEngine.Color Tint = new(0.62f, 0.05f, 0.08f, 1f);

            /// <summary>Hits needed to fill the fuller - four stains plus the empty stage makes
            /// five sprites, the same cadence WeaponHeat's five-stage cycle already uses.</summary>
            public const int MaxFill = 4;

            /// <summary>Knockback on the release burst - between Vortex's reset-button 9 and
            /// Meteor's 11, since this is a payoff rather than a utility move.</summary>
            public const float ReleaseKnockback = 10f;

            public const float ReleaseRangeBonus = 1.0f;

            /// <summary>Damage at the rim of the release relative to its centre - a blast, not a
            /// swing, same as every other AreaOfEffect finisher.</summary>
            public const float ReleaseEdgeDamageFraction = 0.4f;
        }

        /// <summary>
        /// Zanmato - the katana signature. The finisher runs as a coroutine
        /// (<c>PlayerController.SheathDrawStrike</c>): quick-sheathe, two crescent cuts across the
        /// target while sheathed, then an unsheathing vertical draw-cut. Heavy damage, single
        /// target, and its killing blow leaves a non-boss body in two halves.
        /// </summary>
        public static class Katana
        {
            /// <summary>The blade's own colour - a cold, pale steel-blue, fixed rather than
            /// derived from the played element, the same rule Shadow's, Phantom's and Blood's
            /// tints already carry. Used for every crescent and flash in the sequence and for the
            /// bisection halves' cut-line.</summary>
            public static readonly UnityEngine.Color Tint = new(0.72f, 0.84f, 0.95f, 1f);

            /// <summary>Beat one: the blade snapping home. Short - it is a flick, not a wind-up.</summary>
            public const float SheathSeconds = 0.12f;

            /// <summary>Beat two: each of the two crescent cuts while the blade is sheathed.</summary>
            public const float CutSeconds = 0.13f;

            /// <summary>Beat three's first half: the blade coming back OUT of the saya along its
            /// axis, before the rig takes it back and swings. The cut itself then runs for the
            /// swing's own animation length on top of this.</summary>
            public const float DrawSeconds = 0.18f;

            /// <summary>The draw-cut's own damage, in the AttackStep's DamageMultiplier units.
            /// The two pre-cuts add <see cref="CutMultiplier"/> each, so the sequence sums to
            /// roughly a Heavy chain's 6.5 (see the finisher weight-class table).</summary>
            public const float DrawCutMultiplier = 4.5f;

            /// <summary>Each sheathed crescent's damage, same units as <see cref="DrawCutMultiplier"/>.</summary>
            public const float CutMultiplier = 1.0f;

            /// <summary>How long the two halves of a bisected body drift and fade before the VFX
            /// removes itself.</summary>
            public const float BisectSeconds = 0.55f;

            /// <summary>World units each half slides along its own vertical - one up, one down -
            /// over <see cref="BisectSeconds"/>. Deliberately small: a shear, not an explosion.</summary>
            public const float BisectSlide = 0.18f;
        }

        /// <summary>The three-swings-then-finisher chain.</summary>
        public static class Combo
        {
            /// <summary>Seconds of not swinging before a partial chain resets. A banked finisher never expires.</summary>
            public const float ResetSeconds = 1.8f;

            /// <summary>
            /// Extra pause a HELD attack button waits on top of the swing's interval before
            /// auto-firing. A fresh tap ignores this, so tapping on the beat is always faster
            /// than lazily holding - without capping a tapping player's true APM.
            /// </summary>
            public const float HoldExtraDelay = 0.10f;

            /// <summary>
            /// How long a fresh tap is remembered when it arrives too early to fire.
            ///
            /// This exists to absorb INPUT JITTER and nothing else: three frames of grace covers a
            /// press that landed a hair early or fell between frames. It only ever moves a press
            /// LATER, never earlier: a remembered tap fires the instant the gate it missed opens,
            /// and expires unspent otherwise. Kept small so it never turns into a mash-to-attack
            /// cushion. (It once also fed a basic-attack recovery cancel; that was removed in
            /// favour of the finisher timing bar - see StrikeTiming.)
            ///
            /// ABSOLUTE, not a fraction of the interval - human timing jitter is a fixed number of
            /// milliseconds and does not shrink when a speed buff shortens the swing. Same
            /// reasoning as TelegraphMinSeconds' own absolute floor.
            /// </summary>
            public const float TapBufferSeconds = 0.05f;

            /// <summary>How many finisher movesets a run can hold at once.</summary>
            public const int MaxMovesets = 3;
        }

        /// <summary>
        /// The finisher timing bar: every finisher winds up, a slim arc beside the character
        /// fills from the bottom, and the first tap during it decides how hard the strike lands. See
        /// Combat.StrikeTiming for the rules and PlayerController's strike section for the flow.
        ///
        /// THE BAR IS THE LAST <see cref="BarSeconds"/> BEFORE THE STRIKE, on every finisher -
        /// same size, same speed, same zones. A finisher that already delays its damage (a
        /// charge, a leap, Separatio's hold, the katana's sheathe, the bow's draw) shows it during
        /// the end of that delay and gains no time; one that resolved on the press (an ordinary
        /// swing, a disc volley, a thrown blade) gains a wind-up of exactly the shortfall. One
        /// read, learned once, true everywhere - the same consistency rule the old basic-attack
        /// beat lived by.
        ///
        /// ABSOLUTE seconds, never scaled by attack speed: human timing is a fixed number of
        /// milliseconds (TapBufferSeconds' reasoning). Speed buffs still shorten the swing, just
        /// not the read.
        /// </summary>
        public static class StrikeTiming
        {
            /// <summary>
            /// The four equal segments, bottom to top: LEAD (red - a press here is early), GOOD (yellow),
            /// PERFECT (green), GOOD - and the strike lands where the last good band ends (the top of the
            /// arc). Equal so the meter is four whole blocks of <see cref="ArcRowsPerSegment"/>
            /// rows and nothing on it sits off the pixel grid. Perfect is +-45ms around its centre; a good press
            /// has 90ms either side of that. Was 0.075 (a 0.30s bar) - widened for room to read it.
            /// </summary>
            public const float SegmentSeconds = 0.1f;
            public const float BarSeconds = SegmentSeconds * 4f;

            /// <summary>The user's numbers: perfect +20%, good +10%, early or no press -5%.</summary>
            public const float PerfectMultiplier = 1.20f;
            public const float GoodMultiplier = 1.10f;
            public const float MissMultiplier = 0.95f;

            /// <summary>
            /// The meter is a slim ARC standing beside the character on the side AWAY from the
            /// facing (the off hand - the strike goes the other way), filling from the bottom up
            /// like a basketball shot meter; the strike lands as the fill reaches the top. Drawn
            /// pixel by pixel at body density (37.5 px/unit), so it sits on the same grid as the
            /// character. 12 rows x 4 segments = 48 rows = 1.28 units, about the body's height.
            /// </summary>
            public const int ArcRowsPerSegment = 12;

            /// <summary>Width of the lit core in texels, inside a one-texel dark rim. The perfect
            /// (green) segment's core is one texel WIDER - colour is never the only channel.</summary>
            public const int ArcCoreTexels = 2;

            /// <summary>How far the middle of the arc bows out from its ends, in texels.</summary>
            public const int ArcBowTexels = 4;

            /// <summary>World units from the player's origin out to the arc's ends, on the side
            /// away from the facing - clear of the body and the off arm.</summary>
            public const float ArcSideOffset = 0.58f;

            /// <summary>World units above the player's origin to the arc's middle. The origin is
            /// mid-body (feet ~0.6 below, head ~0.77 above at the arena's visual scale).</summary>
            public const float ArcCentreY = 0.06f;

            /// <summary>How long the meter lingers, showing where the press landed, after the strike.</summary>
            public const float LingerSeconds = 0.22f;

            /// <summary>How bright an unfilled segment is against its lit colour - the zones are
            /// visible from the start, the fill is what lights them.</summary>
            public const float UnlitBrightness = 0.62f;

            public static readonly UnityEngine.Color Track = new(0.08f, 0.07f, 0.10f, 0.95f);
            public static readonly UnityEngine.Color Lead = new(0.80f, 0.24f, 0.22f, 0.95f);
            public static readonly UnityEngine.Color Good = new(1.00f, 0.82f, 0.28f, 0.95f);
            public static readonly UnityEngine.Color Perfect = new(0.30f, 0.82f, 0.38f, 1f);
            public static readonly UnityEngine.Color Fill = new(1f, 1f, 1f, 1f);
            public static readonly UnityEngine.Color Missed = new(0.85f, 0.22f, 0.20f, 0.9f);

            /// <summary>
            /// THE PERFECT STREAK: every consecutive PERFECT finisher that CONNECTS adds crit
            /// chance to every hit - basics included - so a streak is a state the player carries
            /// through the fight, not a bigger number on one swing. Anything else resets it: a
            /// GOOD, an early press, no press, or a perfect that hit nothing (so a cleared room
            /// can't be farmed by swinging at air). Lasts the run (it lives on the player).
            /// The user's numbers: 2 perfects = +4%, 6 = +12%, 10 = +20%, then capped. The COUNT
            /// keeps climbing past the cap; only the bonus stops. PlayerPower prices finishers at
            /// GOOD, so this is skill upside on top of the model, like PERFECT itself.
            /// </summary>
            public const float StreakCritPerPerfect = 0.02f;
            public const int StreakCap = 10;

            /// <summary>How long a PERFECT waits for its finisher to connect before it counts as
            /// hitting nothing. Covers the slowest deferred hits (an arrow across the arena,
            /// Orrery's sweeps); a thrown blade instead waits for as long as it is out.</summary>
            public const float StreakConnectSeconds = 1.6f;

            /// <summary>The streak's pips beside the meter: one per perfect up to the cap, mint
            /// while building, the perfect segment's own green once the cap is reached.</summary>
            public static readonly UnityEngine.Color PipLit = new(0.70f, 0.95f, 0.78f, 1f);
            public static readonly UnityEngine.Color PipPop = new(0.96f, 1.00f, 0.97f, 1f);
            public static readonly UnityEngine.Color PipEmpty = new(0.60f, 0.60f, 0.66f, 0.28f);
            public static readonly UnityEngine.Color PipBroken = new(1.00f, 0.32f, 0.30f, 1f);
        }

        /// <summary>The Undertow / Vortex finisher: the fight's gather-everything reset.</summary>
        public static class Undertow
        {
            /// <summary>
            /// The pull is full strength within this multiple of the reach ring (Player.BaseRange);
            /// past that it eases off with distance, out to the edge of the view.
            /// </summary>
            public const float FullPullRangeMul = 2f;

            /// <summary>
            /// How much of the pull still reaches an enemy at the very edge of the screen, as a
            /// fraction of full strength. Above zero so a scattered fight always closes up.
            /// </summary>
            public const float EdgePullFraction = 0.15f;
        }

        /// <summary>Sticky auto-target acquisition. Keeps the character from flip-flopping between enemies.</summary>
        public static class Targeting
        {
            /// <summary>
            /// World units of lock-on reach BEYOND the pending swing's own range.
            ///
            /// Locking exactly at reach was correct but read as late: the character only turned
            /// once an enemy was already close enough to hit, so there was no anticipation in the
            /// turn at all. This buys a step of warning without going back to locking on things
            /// across the arena, which is what broke aiming the blade throw and the air cone.
            /// </summary>
            public const float AcquireMargin = 0.85f;

            /// <summary>
            /// Extra distance a HELD target may drift past the acquire radius before it is
            /// dropped. Above zero, or a target sitting exactly on the boundary flickers in and
            /// out every frame.
            /// </summary>
            public const float DropMargin = 0.9f;

            /// <summary>Fallback acquire distance when lock-on is not bounded by reach.</summary>
            public const float AcquireRange = 14f;

            /// <summary>Fallback drop distance. Must exceed AcquireRange.</summary>
            public const float DropRange = 16f;

            /// <summary>
            /// A rival must be within this fraction of the current target's distance to steal
            /// focus. 1 = switch to any closer enemy (jittery); lower = stickier.
            /// </summary>
            public const float SwitchAdvantage = 0.7f;
        }

        /// <summary>
        /// The rim drawn round the locked target (Player.TargetHighlight) - what the ground rings
        /// used to answer, moved onto the one enemy the answer is about.
        ///
        ///     SOLID              a swing lands on it
        ///     DASHED             not yet in reach (a sword closing in), or a disc THROWS at it
        ///     bow / disc throw   brighter the more the shot deals at that distance
        /// </summary>
        public static class Highlight
        {
            /// <summary>The rim's thickness in WORLD units: one body art pixel at the arena's
            /// visual scale, the weight of the character's own outline.</summary>
            public const float Thickness = 1f / 37.5f * Player.ArenaVisualScale;

            public static readonly UnityEngine.Color Tint = new(1f, 0.92f, 0.62f, 1f);

            /// <summary>Alpha in reach (or a ranged shot at full value).</summary>
            public const float Bright = 1f;

            /// <summary>Alpha while closing in, and the floor of a ranged shot's curve.</summary>
            public const float Dim = 0.55f;

            /// <summary>How quickly the alpha follows a change (per second, exponential).</summary>
            public const float Ease = 14f;
        }

        /// <summary>
        /// The smear behind a GREATSWORD's blade on every swing (Combat.SwingSmear): the area the
        /// blade swept over the last few hundredths of a second, filled flat at body density.
        /// Basics smear the outer blade only; finishers smear most of it, for longer, warmer.
        /// </summary>
        public static class Smear
        {
            /// <summary>World texels per unit. 37.5 is on the pixel-perfect ladder, so every
            /// smear texel is a whole number of screen pixels.</summary>
            public const float Ppu = 37.5f;

            /// <summary>Seconds of blade path a basic's smear covers.</summary>
            public const float BasicWindow = 0.045f;
            public const float FinisherWindow = 0.07f;

            /// <summary>The span of the blade that smears, as fractions of its height above the
            /// grip. A basic's is the outer half - a crescent, not a fan.</summary>
            public const float BasicFrom = 0.55f, BasicTo = 0.97f;
            public const float FinisherFrom = 0.22f, FinisherTo = 1f;

            /// <summary>How far toward the tip the OLDEST part of the smear narrows, as a fraction
            /// of the smeared span - the tail thins out like a comet's.</summary>
            public const float Taper = 0.7f;

            /// <summary>Fraction of the window drawn in the bright tone; the rest is the tail.</summary>
            public const float LeadFraction = 0.4f;

            public static readonly UnityEngine.Color BasicLead = new(0.96f, 0.98f, 1f, 0.85f);
            public static readonly UnityEngine.Color BasicTail = new(0.80f, 0.86f, 0.96f, 0.45f);
            public static readonly UnityEngine.Color FinisherLead = new(1f, 0.98f, 0.90f, 0.95f);
            public static readonly UnityEngine.Color FinisherTail = new(1f, 0.82f, 0.42f, 0.60f);
        }

        /// <summary>
        /// The light running up the weapon while a finisher is banked (Combat.FinisherGlint) - the
        /// gold ring's old job, moved onto the thing that will deliver the finisher.
        /// </summary>
        public static class Glint
        {
            /// <summary>Seconds between glints. The first runs the moment the finisher banks.</summary>
            public const float Period = 1.1f;

            /// <summary>Seconds one glint takes to travel the blade.</summary>
            public const float Sweep = 0.24f;

            public const int Frames = 8;

            /// <summary>Where on the weapon the glint starts, as a fraction of its height - clear
            /// of the grip the hands close on.</summary>
            public const float From = 0.2f;

            public static readonly UnityEngine.Color Tint = new(1f, 1f, 1f, 0.9f);
        }

        /// <summary>
        /// The Secret Fire (Art.Gear.SecretFire): the one beat every kindled mark - the Aether set
        /// and its greatsword - glows and fades on, in the wearer's element. Unscaled seconds.
        /// </summary>
        public static class SecretFire
        {
            /// <summary>One glow and fade, start to start.</summary>
            public const float PeriodSeconds = 2.6f;

            /// <summary>Fractions of the period: rising to lit, holding lit, fading to black. What is
            /// left is held dark - long enough to read as the marks going OUT, short enough that they
            /// never read as switched off.</summary>
            public const float Rise = 0.30f, Hold = 0.10f, Fade = 0.45f;

            /// <summary>The overlay's alpha is the brightness to this power - see SecretFire.Alpha.</summary>
            public const float Gamma = 2.2f;

            /// <summary>An element change: the marks go out over the first half and come back in the
            /// new colour over the second.</summary>
            public const float SwapSeconds = 0.5f;
        }

        /// <summary>
        /// The MAGNUM OPUS: the reactive weapons' weapon art (Art.Gear.MagnumOpusGlow for the light,
        /// Combat.CrescentBeam for the shot, Combat.Disintegration for a body it kills). Seconds are
        /// measured from the moment the gather begins, in SCALED time - a screen opening mid-art
        /// pauses it with everything else.
        /// </summary>
        public static class MagnumOpus
        {
            /// <summary>The whole gather, press to strike - the step's ChargeSeconds, which the
            /// timing bar and the lead-in checks read. Long enough for the light to make its trip.</summary>
            public const float GatherSeconds = 1.2f;

            /// <summary>The armour's and weapon's marks go out over this, from wherever the beat had them.</summary>
            public const float DrainSeconds = 0.30f;

            /// <summary>The relic swells to full over this, starting with the drain.</summary>
            public const float SwellSeconds = 0.45f;

            /// <summary>When the relic lets go: the flash leaves it and runs out through the armour.</summary>
            public const float FlashAt = 0.50f;

            /// <summary>How fast the flash runs from the relic through the armour to the weapon,
            /// in world units a second. The figure is ~1.4 tall, so head to foot takes ~0.3s.</summary>
            public const float FlashSpeed = 4.5f;

            /// <summary>When the flash reaches the WEAPON: its last stop, after it has run through all
            /// the armour. Fixed rather than measured - the blade is held beside the hip, so by
            /// distance it lit together with the chest and the trip read as one flash.</summary>
            public const float WeaponLitAt = 0.84f;

            /// <summary>One piece's flash as the light passes through it: up, then back out.</summary>
            public const float FlashUpSeconds = 0.06f, FlashDownSeconds = 0.20f;

            /// <summary>The relic's own glow drains after it lets go.</summary>
            public const float RelicFadeSeconds = 0.30f;

            /// <summary>
            /// How far the arms may tilt toward the aim during the art (the rig's own limit is 45):
            /// the slash has to sweep THROUGH the crescent's line, and an aim straight up or down is
            /// 90 away from level. Short of 90 so the blade never lies flat along the arm.
            /// </summary>
            public const float AimRangeDegrees = 80f;

            /// <summary>The crescent's size as it leaves the blade and at the end of its range,
            /// times its drawn size - it GROWS as it travels (the user's call, 2026-10-07). Its hit
            /// strip grows with it.</summary>
            public const float BeamGrowFrom = 0.65f, BeamGrowTo = 1.6f;

            /// <summary>
            /// The SLASH's share of the art's damage (the user's call, 2026-10-07: the swing shares
            /// the damage, but only where it lands). A body the slash strikes takes this much from
            /// the blade and the rest from the crescent when it passes; a body the slash never
            /// touched takes the crescent's whole hit. So a whiffed swing costs nothing, and no body
            /// takes more than one art's worth.
            /// </summary>
            public const float SlashShare = 0.35f;

            /// <summary>The weapon's glow drains with the shot, over this.</summary>
            public const float WeaponDrainSeconds = 0.35f;

            /// <summary>Everything blends back onto the Secret Fire's own beat over this, after the drain.</summary>
            public const float RecoverSeconds = 0.6f;

            /// <summary>The crescent's flight, in world units a second, and how far it goes before it
            /// is spent (scaled by the Range stat and the ledger's range, like a reach).</summary>
            public const float BeamSpeed = 17f, BeamRange = 8f;

            /// <summary>The crescent's span ACROSS its flight - the width of the line it clears, tip
            /// to tip - and the depth of the strip it strikes each frame, in world units. Widened
            /// from 2.3 with the brush-stroke picture (the user's reference, 2026-10-07).</summary>
            public const float BeamSpan = 3.0f, BeamDepth = 0.6f;

            /// <summary>The brush stroke: how much of a circle the arc covers, tip to tip, and how
            /// thick the stroke is at its fullest, in world units.</summary>
            public const float BeamArcDegrees = 165f, BeamThickness = 0.55f;

            /// <summary>The crescent's pixel density: the body's own, so it sits on the character's grid.</summary>
            public const float BeamPpu = 37.5f;

            /// <summary>The crescent fades out over the last share of its range rather than vanishing.</summary>
            public const float BeamFadeFraction = 0.25f;

            /// <summary>A body coming apart: how long its texels take to drift off, the spread
            /// across the body the beam crosses it in, and how far they drift.</summary>
            public const float DisintegrateSeconds = 0.95f, DisintegrateSweepSeconds = 0.22f,
                               DisintegrateDrift = 0.9f;
        }

        /// <summary>Framing. See also GameBootstrap's FloorMargin note about the pan room outside the walls.</summary>
        public static class Camera
        {
            /// <summary>
            /// Half the visible height in world units - the orthographic size.
            ///
            /// 3.6, NOT the 4.8 this was declared at for a long time. 4.8 was never reachable at
            /// 1080p: the pixel-perfect snap needs a whole number of screen pixels on a texel, 4.8
            /// asks for 1.5, and the snap rounded to 2 and handed back 3.6 anyway - so the game
            /// has been running a 25% tighter view than this constant claimed, at the commonest
            /// desktop resolution, with nothing saying so.
            ///
            /// Declaring the value that is actually rendered is the whole fix. A tuned size the
            /// snap cannot honour is not a target, it is a number that disagrees with the screen,
            /// and every framing decision measured against it (camera clamp, floor margin, the
            /// range rings) was measuring against a view nobody was looking at.
            /// </summary>
            public const float Size = 3.6f;

            /// <summary>How tightly the camera holds the player. Higher is more locked; it eases as 1 - e^(-Follow*dt).</summary>
            public const float Follow = 12f;

            /// <summary>
            /// How much closer the ARENA is framed on a phone (GameBootstrap.ArenaViewSize): the
            /// view's half-height divided by this. 1.2 takes the character from ~11.7% of the
            /// screen's short side to ~14% - measured against a clear pixel game at ~17%, and held
            /// short of it because this game's crowd needs the room an idle battler's five heroes
            /// do not.
            /// </summary>
            public const float PhoneZoom = 1.2f;
        }

        /// <summary>The room. Walls sit at these half-extents; ground and camera pan extend past them.</summary>
        public static class Arena
        {
            /// <summary>Half the arena width in world units (wall to centre).</summary>
            public const float HalfWidth = 12f;

            /// <summary>Half the arena height in world units (wall to centre).</summary>
            public const float HalfHeight = 7f;

            /// <summary>Ground drawn beyond the walls, so the camera can still centre the player in a corner.</summary>
            public const float FloorMargin = 9f;
        }

        /// <summary>
        /// The HUB - the menu you walk around instead of clicking.
        ///
        /// Laid out in world units against a FIXED camera at the origin, so every number here is
        /// really "where on screen". At Camera.Size 4.8 the view is 4.8 half-height by roughly
        /// 8.5 half-width on 16:9, and the room is sized to sit just inside that: the whole room
        /// is visible at once, which is the entire point of a room used as a menu. HubRoom
        /// narrows HalfWidth at runtime on a taller aspect so the side doors can never be cropped.
        /// </summary>
        public static class Hub
        {
            /// <summary>Distance from centre to each side wall. Doors are mounted on these.</summary>
            public const float HalfWidth = 8f;

            /// <summary>Where the walkable floor stops and the gallery wall begins.</summary>
            public const float FloorTop = 3.05f;

            /// <summary>The bottom of the walkable floor.</summary>
            public const float FloorBottom = -4f;

            /// <summary>
            /// Height of the north wall's FACE - the band above the floor that the frames hang on.
            /// Top-down rooms cheat this band into view (Stardew, Hades and everything like them
            /// do the same); without it there is no surface to hang anything on, because a wall
            /// seen from directly above is a line.
            /// </summary>
            public const float GalleryHeight = 2.1f;

            /// <summary>Thickness of the side and south walls.</summary>
            public const float WallThickness = 0.5f;

            /// <summary>The avatar's own CircleCollider2D radius - how close its centre can ever
            /// get to a wall it's colliding with. Named rather than left as a literal in
            /// BuildAvatar because HubRoom's own wall-placement legality checks need the exact
            /// same number: a placement margin any looser than this carves out a strip of floor
            /// the avatar can stand on but can never place anything from, and any tighter never
            /// actually gets used.</summary>
            public const float AvatarRadius = 0.42f;

            /// <summary>Hub walk speed as a fraction of the combat speed. Deliberately under it -
            /// there is nothing to dodge here, and a menu that skids is annoying to aim at a door.</summary>
            public const float WalkSpeedFraction = 0.965f;

            /// <summary>Walk speed in the hub. DERIVED from Player.MoveSpeed, never set on its own -
            /// the character walking at one pace in the hub and another in the arena breaks
            /// continuity. Retune the combat speed and this follows.</summary>
            public const float WalkSpeed = Player.MoveSpeed * WalkSpeedFraction;

            /// <summary>
            /// The single door's height, and the wall it sits on.
            ///
            /// Was two doors per side wall, one per element. Replaced by one door carrying all
            /// four elemental marks and a wall-mounted button that selects between them - see
            /// SigilDoor and WallButton. The east wall is left fully solid on purpose: it is
            /// where the loft goes next, and a door cut into a wall the loft needs to back onto
            /// would need reopening the moment that lands.
            /// </summary>
            /// <summary>
            /// The door is on the NORTH wall, which is the only wall in the room with a drawn
            /// face (the gallery band) - and a face is what the sketch needs, since it draws a
            /// pair of stone doors seen straight on. On a side wall there is no face to draw on,
            /// so "sigils on the door" degrades into a stripe of marks running along a line;
            /// that is exactly what the first implementation did.
            ///
            /// x is west of the loft, which owns the north wall from (HalfWidth - LoftWidth)
            /// eastward. The button sits close beside the door on the RIGHT, so the two read as
            /// one fixture - a gate and its switch - rather than a doorway with a control panel
            /// on the far side of the room. Both still carry Priority 10, so the two do compete
            /// for focus in the gap between them, but that is just proximity working normally:
            /// standing at either one's own anchor is always closer to it than to the other
            /// (door anchor is 0 from itself, 1.35 from the button's; button anchor is 0 from
            /// itself, 1.35 from the door's - well outside its own 1.1 radius), so each is
            /// reachable on its own regardless of where the boundary between them falls.
            /// </summary>
            public const float DoorX = -3.0f;
            public const float ButtonX = DoorX + 1.35f;

            /// <summary>
            /// Half the width of the door fixture, along the wall.
            ///
            /// Set from the sketch's own PROPORTIONS rather than picked: the drawn doors are
            /// roughly 350 across by 660 tall, so the pair is about 0.53 as wide as it is high,
            /// and the four marks come out near-square stacked down them. Built landscape (1.7
            /// wide against 1.35 high) the same marks are squat and the slit between the slabs
            /// reads as a wide band rather than two doors standing ajar.
            /// </summary>
            public const float DoorHalfWidth = 0.45f;

            /// <summary>
            /// How far up the gallery band the doors stand.
            ///
            /// The band was raised from 1.5 to make room for this: four marks have to stack down
            /// the doors, and the mark's size is whatever a quarter of the height allows. At 1.42
            /// they came out 0.29 on a 0.9-wide door - a third of the width, against the sketch's
            /// own 43% - and small marks are exactly what floats rather than reads as carving.
            /// </summary>
            public const float DoorHeight = 1.95f;

            /// <summary>
            /// How close the character must be for a door to offer itself.
            ///
            /// Was 2.2, which is generous for a door and too generous for a room with furniture
            /// in it: doors outrank everything on focus priority, so a 2.2 radius reached across
            /// the couch and made its end seat impossible to select. The anchor already sits 1.15
            /// units out from the wall, so this still covers standing anywhere in a doorway.
            /// </summary>
            public const float DoorPromptRadius = 1.9f;

            /// <summary>
            /// How much open floor the door reserves for OTHER placements to stay clear of -
            /// deliberately much smaller than DoorPromptRadius. That number has to stay generous
            /// so the door is easy to walk up to and always wins focus over nearby furniture; used
            /// as-is for clearance too, it reserved a stretch of floor the door's own art never
            /// touches, well past its interact anchor (already 1.15 units out from the wall).
            /// Comparable to what any other single fixture asks for (crate 1.15, forge 1.05).
            /// </summary>
            public const float DoorClearanceRadius = 0.9f;

            /// <summary>World size of the element sigil mounted above each doorway.</summary>
            public const float SigilSize = 0.66f;

            /// <summary>
            /// Pitch of the HUB camera, degrees around local X. Every other room in the project -
            /// the arena included - stays strict top-down; this is hub-only, applied when entering
            /// (see GameBootstrap.ShowHubAsync) and cleared when leaving (GameBootstrap.HideHub).
            ///
            /// The floor plane needs no changes for this to read as an angled view: an orthographic
            /// camera tilted by theta shows a flat plane compressed by cos(theta) in screen-space
            /// height, which is exactly the isometric floor look, for free. What is NOT free is
            /// everything meant to stand upright - walls, furniture, the avatar - which is a flat
            /// XY quad exactly like the floor and would be squashed by the same cos(theta) unless
            /// something counters it. HubRoom's Upright() wraps each of those in a child transform
            /// rotated by -CameraTiltDegrees around its own anchor, which does not move the anchor
            /// (a rotation around a point at zero local offset does not translate it) - so physics,
            /// HubInteractable radii and DepthSorted's y-read all stay exactly as they were for a
            /// flat camera, and only the SPRITES rotate to face the tilted camera dead-on. This is
            /// what makes the tilt cheap: it is a per-object billboard computed once, not every
            /// frame, because the camera itself never moves while the hub is up.
            /// </summary>
            public const float CameraTiltDegrees = 32f;

            // ---- showcase art: Frame, either standing as an easel or mounted on the wall ----

            /// <summary>Inner (art) size of a WALL-mounted frame. Square, because most collection
            /// art is. An easel-mode Frame uses Hub.Frame.ArtHeight instead - see its own note on
            /// why a standing piece is height-locked rather than fit-to-square.</summary>
            public const float FrameSize = 0.98f;

            /// <summary>
            /// Radius of the transmutation circle at the centre of the room. Doubles as the
            /// distance you have to be INSIDE for it to offer itself, so it is big enough to
            /// stand in comfortably and small enough not to claim the whole floor.
            /// </summary>
            public const float CircleRadius = 1.85f;

            /// <summary>
            /// The loft: a raised-look platform in the NE corner, backed by the north gallery
            /// wall and the east wall - the corner both already share, cleared of doors for
            /// exactly this. Same-plane cheat, like the gallery band itself: no real elevation
            /// yet, and the constants below describe a footprint on the existing floor, not a
            /// height. See HubRoom.BuildLoft for what real occlusion would actually cost.
            /// </summary>
            public const float LoftWidth = 5f;

            /// <summary>Where the loft's open (south) edge sits. Everything above this, out to
            /// the east wall, is the platform.</summary>
            public const float LoftY0 = 0.5f;

            /// <summary>Width of the stair gap cut into the loft's south riser - the one way in.</summary>
            public const float LoftStairGapWidth = 1.4f;

            /// <summary>
            /// Clearance radius per movable fixture, for the room editor's own legality check -
            /// the same role <c>Hub.Frame.Radius</c> plays for showcase pieces, just sized to
            /// each fixture's real footprint instead of one constant for everything. A couch
            /// dropped a frame's radius from a wall would visually collide; these are measured
            /// against what each piece actually draws, not guessed.
            /// </summary>
            /// <summary>
            /// The armoury doorway on the north wall, measured in from the EAST wall so it lands
            /// on the loft at any aspect - where the couch stood before it went in the crate, and
            /// in the gap between the two wall-hung frames in the room it was placed against.
            /// Not movable: it is an opening in the wall, like the sigil door.
            /// </summary>
            public const float ArmouryDoorFromEast = 2.7f;

            public const float CouchFootprint = 1.85f;
            public const float TerminalFootprint = 1.0f;
            public const float TableFootprint = 0.9f;
            public const float CrateFootprint = 0.65f;
            public const float CircleFootprint = CircleRadius;
            public const float BoothFootprint = 0.9f;
            public const float ForgeFootprint = 0.85f;
            public const float ShrineFootprint = 0.7f;
            public const float RackFootprint = 0.95f;
            public const float StandFootprint = 0.6f;
        }

        /// <summary>
        /// Enemy baseline. EnemyFactory applies these, scaling HP and damage by
        /// (1 + (wave - 1) * WaveScalePerWave). Elites are the loot-bearing enemy in the full
        /// design; here they are just bigger, tougher, and hit harder.
        /// </summary>
        /// <summary>
        /// The armoury - a room of its own off the hub, holding every weapon design on one wall.
        ///
        /// It exists to show weapons at MENU density (300 texels per unit), which the hub cannot:
        /// the hub camera gives a world unit about 150 screen pixels at 1080p. So this room owns
        /// its zoom instead, set so a menu texel lands on a whole number of screen pixels at any
        /// resolution (see ViewHalfHeight), and it is strict top-down - the hub's tilt squashes
        /// every sprite's height by cos(32 deg), which no pixel ratio survives.
        /// </summary>
        public static class Armoury
        {
            /// <summary>Where the room is built, far enough along X that nothing in the hub can
            /// see or reach it. Along X rather than Y so depth sorting (which reads y) is unchanged.</summary>
            public const float OriginX = 80f;

            /// <summary>The zoom the room is FRAMED at - half-height in world units. The real
            /// value snaps to a whole number of screen pixels per menu texel (ViewHalfHeight), which
            /// at 1080p is exactly this.</summary>
            public const float TargetHalfHeight = 1.8f;

            /// <summary>Menu art's density - DemoGear.MenuPpu. What the zoom is snapped against.</summary>
            public const float MenuPpu = 300f;

            /// <summary>Walkable floor, from the south wall up to the foot of the weapon wall.</summary>
            public const float FloorHeight = 1.7f;

            /// <summary>The wall face the weapons hang on - the same top-down cheat as the hub's
            /// gallery band. Tall enough for a greatsword (0.99) in a bay with margin.</summary>
            public const float WallHeight = 1.65f;

            public const float WallThickness = 0.25f;

            /// <summary>Centre-to-centre between bays. A pair of discs is 0.60 across.</summary>
            public const float BayPitch = 0.9f;
            public const float BayWidth = 0.78f;
            public const float BayHeight = 1.32f;

            /// <summary>Extra space between one weapon class and the next along the wall.</summary>
            public const float ClassGap = 0.7f;

            /// <summary>Open wall at the west end for the doorway back to the hub.</summary>
            public const float EntranceWidth = 2.0f;

            public const float DoorHalfWidth = 0.42f;
            public const float DoorHeight = 1.35f;

            /// <summary>
            /// The nearest half-height to <see cref="TargetHalfHeight"/> that puts a WHOLE number
            /// of screen pixels on a menu texel: <c>H / (2k * MenuPpu)</c> for integer k >= 1.
            /// 1.8 at 1080p (k 1), 2.4 at 1440p (k 1), 1.8 at 2160p (k 2), 1.2 at 720p (k 1 - the
            /// only way to show menu art at all on a screen that small).
            /// </summary>
            public static float ViewHalfHeight(int screenHeight)
            {
                if (screenHeight < 2) return TargetHalfHeight;
                int k = System.Math.Max(1, (int)System.Math.Round(screenHeight / (2f * TargetHalfHeight * MenuPpu)));
                return screenHeight / (2f * k * MenuPpu);
            }
        }

        public static class Enemy
        {
            /// <summary>Bomb enemy starting HP, before per-wave scaling.</summary>
            public const float BombHp = 130f;

            /// <summary>Elite enemy starting HP, before per-wave scaling.</summary>
            public const float ChaserHp = 200f;

            /// <summary>
            /// The Chaser's wind-up before it swings.
            ///
            /// It had none - it simply dealt damage the frame its cooldown expired - which made it
            /// the other enemy with nothing to interrupt. Short, because a chaser that telegraphs
            /// slowly stops being pressure; long enough that the swing is a thing that happens
            /// rather than a state change.
            /// </summary>
            public const float ChaserTelegraphDuration = 0.35f;

            /// <summary>
            /// The beat after a Chaser swings - AND after a flinched one does not.
            ///
            /// IT RECOVERS IN PLACE RATHER THAN BACKING OFF, which is where it differs from every
            /// other kind, and the reason is what a chaser is FOR. The shared idea is that a
            /// denied enemy and a successful one read as the same beat, so the player learns one
            /// shape; the CONTENT of that beat still belongs to the kind. A chaser that gave
            /// ground after every landed hit would stop being the thing that presses you, and the
            /// crowd would stop being a positioning problem.
            /// </summary>
            public const float ChaserRecoverSeconds = 0.45f;

            /// <summary>
            /// How much of a knockback an ELITE keeps. 0 is a normal body, 1 is immovable.
            ///
            /// Elites flinch exactly like anything else - denying an attack works on them - but
            /// shoving them around does not. That split was written down when Elite became a tier
            /// and then never implemented: ResistsDisplacement existed and nothing consulted it,
            /// so an elite was as easy to push as a common enemy.
            /// </summary>
            /// <summary>
            /// THE ELITE TIER'S EXTRA ATTACK PATTERNS, one per kind - see Enemies.ElitePattern.
            ///
            /// Each turns that kind's OWN kit up rather than handing out one shared bonus: an
            /// elite Ranged and an elite Bomb are not the same enemy at different strengths. They
            /// land on a fixed cadence rather than a roll, because an elite whose big move is
            /// random is one the player cannot read - and reading it is the entire point of a
            /// telegraph.
            /// </summary>
            public const float RiposteDelay = 0.28f;
            public const float RiposteDamageMul = 1.35f;

            /// <summary>Bolts in an elite Ranged's volley, and the total spread across them.</summary>
            public const int VolleyBolts = 3;
            public const float VolleySpreadDegrees = 26f;

            /// <summary>Delayed shards an elite Bomb leaves where it stood.</summary>
            public const int ClusterShards = 3;
            public const float ClusterDelay = 0.75f;
            public const float ClusterRadius = 1.5f;
            public const float ClusterDamageFraction = 0.5f;
            public const float ClusterScatter = 1.6f;

            /// <summary>How long an elite Turret's beam overcharges, and by how much.</summary>
            public const float OverchargeSeconds = 1.6f;
            public const float OverchargeDamageMul = 2.2f;

            // ---- the elite Turret's MIRE: a second move, on top of Overcharge ----
            //
            // A shell lobbed like a Mortar's (MortarShell, the same flight and the same parry),
            // that lands as a wide patch of slowing ground (Hazards.MireField) rather than a
            // blast. No damage. LOBBED, so it needs no line of sight: it is the turret's answer to
            // a player hiding from the beam behind a column - and it holds a player where the
            // beam can reach. Read off the Elite bool, not EnemyDef.Elite, which stays Overcharge.

            /// <summary>Seconds between lobs, counted while the turret is not reeling. The
            /// cooldown is SPENT AT THE WIND-UP, so a flinch denies a lob without refunding it.</summary>
            public const float MireInterval = 7f;

            /// <summary>The tell before a lob - the window a Medium or Heavy finisher denies.</summary>
            public const float MireWindup = 0.6f;

            /// <summary>The patch's radius. Twice and more the Mortar's blast (1.1) - a slow is a
            /// cost to route around, so it has to cover enough ground to need routing around.</summary>
            public const float MireRadius = 2.4f;

            /// <summary>How long the patch lies on the floor.</summary>
            public const float MireSeconds = 5f;

            /// <summary>The player's speed inside an ordinary patch. Near sand's 0.51 on purpose:
            /// the same "this ground is expensive" read, arriving where the turret wants it.</summary>
            public const float MirePlayerSpeedMul = 0.55f;

            /// <summary>An enemy's speed inside a patch a parry TURNED - the deflected shell lands
            /// blue and the ground it leaves bogs down their side instead.</summary>
            public const float MireEnemySpeedMul = 0.5f;

            /// <summary>HP and damage both scale by this much per wave past the first. 0.12 = +12%/wave.</summary>
            /// <summary>
            /// LEGACY, and superseded by Enemies.FloorDifficulty - kept only because a stray
            /// reader elsewhere would silently get 1.0 if it vanished. Nothing in the spawn path
            /// uses it any more; the curve lives in FloorDifficulty, which is the one place that
            /// answers "how hard is floor N".
            /// </summary>
            public const float WaveScalePerWave = 0.12f;

            // ---- the floor difficulty curve: see Enemies.FloorDifficulty for the reasoning ----

            /// <summary>Health per floor. Down from the old 0.12 - that value was most of what made
            /// deep floors long rather than hard.</summary>
            public const float HpPerFloor = 0.09f;

            /// <summary>Damage per floor. Slightly steeper than health, so a deep enemy threatens
            /// more than it endures.</summary>
            public const float DamagePerFloor = 0.10f;

            /// <summary>An enemy body's linearDamping on dry ground, re-solved on water as the
            /// player's is (see Tuning.Player.BodyDamping).</summary>
            public const float BodyDamping = 4f;

            /// <summary>Move speed per floor - the axis that did not exist at all. Capped at twice
            /// base, which keeps every enemy slower than the player: disengaging must stay
            /// possible, so the gap CLOSES rather than being erased.</summary>
            public const float SpeedPerFloor = 0.011f;
            public const float SpeedMaxMultiplier = 2.0f;

            /// <summary>Telegraphs shrink with depth, floored well above zero - a telegraph that
            /// shrinks toward nothing stops being one, and an unreactable attack is damage on a
            /// timer rather than difficulty.</summary>
            public const float TelegraphShrinkPerFloor = 0.005f;
            public const float TelegraphMinMultiplier = 0.55f;

            /// <summary>
            /// An ABSOLUTE floor on any telegraph, whatever the multiplier works out to.
            ///
            /// The multiplier alone is not enough, and the Chaser is why: its wind-up is the
            /// shortest in the game at 0.35s (deliberately, so a melee presser stays pressure), and
            /// 45% off that is 0.19s. Human reaction time is around 0.25s, so at depth its swing
            /// would have become literally unreactable - the exact "damage on a timer" this curve's
            /// own notes warn against, arrived at by applying a rule that is correct for the long
            /// telegraphs to the one short one.
            ///
            /// A proportional rule needs an absolute backstop whenever the things it scales differ
            /// by more than the rule's own range.
            /// </summary>
            public const float TelegraphMinSeconds = 0.24f;

            /// <summary>Attack cadence tightens with depth, same shape and the same floor.</summary>
            public const float CadencePerFloor = 0.005f;
            public const float CadenceMinMultiplier = 0.55f;

            // How MANY enemies a floor fields, and how many at once, is Tuning.Waves' - a budget of
            // effective HP and an on-screen cap of pressure, both in Enemies.WaveComposer.

            /// <summary>Bomb enemy chase speed, world units per second (before the jitter below).
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float BombMoveSpeed = 1.82f;

            /// <summary>Per-enemy random spread added to BombMoveSpeed, so a pack does not move as one blob.</summary>
            public const float BombMoveSpeedJitterMin = -0.21f;
            public const float BombMoveSpeedJitterMax = 0.28f;

            /// <summary>Elite chase speed - slower than a bomb, so you can kite it into the pack.
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float ChaserMoveSpeed = 1.47f;

            /// <summary>
            /// Bomb explosion damage, before per-wave scaling. Meaningfully harder than the old
            /// flat contact hit it replaces (8) - a telegraphed, avoidable attack should hurt more
            /// than a free poke, or there is no reason to ever bother dodging it.
            /// </summary>
            public const float BombDamage = 20f;

            /// <summary>Elite contact damage, before per-wave scaling.</summary>
            public const float ChaserDamage = 15f;

            /// <summary>
            /// Bomb trigger/blast radius, world units - ONE circle serves both jobs: crossing it
            /// inward arms the telegraph, and it is also the area that takes damage when the
            /// timer runs out. Bigger than the old melee contact range (1.15) on purpose - a
            /// radius you are meant to flee needs room to actually flee across, where a melee
            /// range never did.
            /// </summary>
            public const float BombAttackRange = 2.0f;

            /// <summary>
            /// Seconds the blast is telegraphed before it goes off - long enough that a player
            /// who is watching and reacts immediately clears the circle with room to spare, short
            /// enough that someone mid-fight and not looking doesn't get a free pass. Commits once
            /// started, like the ranged bolt's own telegraph: only the target dying cancels it,
            /// not merely leaving the circle.
            /// </summary>
            public const float BombTelegraphDuration = 0.8f;

            /// <summary>
            /// How long a DENIED bomb backs off before it may arm again, and how fast.
            ///
            /// A completed explosion does not survive to need a post-attack beat, so this exists
            /// only for the flinched case - and it has to exist, because without it a denied bomb
            /// simply re-arms on the spot and the interrupt buys nothing at all. Backing off is
            /// what converts the flinch into the space the player paid a finisher for.
            /// </summary>
            public const float BombRecoverSeconds = 1.1f;
            /// <summary>Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float BombRetreatSpeed = 2.1f;

            /// <summary>How close an elite must be to land its hit, world units.</summary>
            public const float ChaserAttackRange = 1.5f;

            /// <summary>Seconds between an enemy's attacks. Shared by bombs and elites.</summary>
            public const float AttackInterval = 1.3f;

            /// <summary>Visual + collider diameter of a bomb enemy, world units.</summary>
            public const float BombSize = 0.72f;

            /// <summary>
            /// Progress (0..1) past which a stage-cycling body (Bomb arming, Turret charging)
            /// shows its FULL stage rather than its MID one. Shared by every kind that uses
            /// EnemyStageCycle's four-frame shape (Idle/Mid/Full/Cooling), since it is the same
            /// two-step bucketing of the same 0..1 signal whatever is driving it - see
            /// EnemyStageCycle. One step rather than a continuous blend because the art is baked
            /// frames, not a shader.
            /// </summary>
            public const float StageFullThreshold = 0.7f;

            /// <summary>Visual + collider diameter of an elite, world units.</summary>
            public const float ChaserSize = 1.15f;

            /// <summary>
            /// Enemies are shoved by ORDINARY hits - the bombs in a chain.
            ///
            /// OFF. Knocked back on every hit, a crowd slid around under the player's swings -
            /// weightless, and worse, a chain of hits kept landing where the target used to be.
            /// With them planted, the swing animation carries the impact instead of the physics.
            ///
            /// FINISHERS are unaffected by this and always throw enemies, via
            /// DamageInfo.Displaces. That split is the point: the payoff swing is the one that
            /// visibly moves the fight.
            /// </summary>
            public const bool TakesKnockback = false;

            /// <summary>
            /// Enemy bodies block the player's movement.
            ///
            /// OFF for bombs and elites. Solid enemies are the other half of what makes a crowd
            /// feel gummy: walking into a pack stops you dead, and being surrounded becomes a
            /// physics problem rather than a positioning one. They still collide with EACH OTHER,
            /// so a pack spreads out instead of stacking into a single point.
            ///
            /// Decided per enemy in EnemyFactory, not globally, because a boss will want to be
            /// solid.
            /// </summary>
            public const bool BlocksPlayer = false;

            /// <summary>
            /// Impulse an enemy's attack applies to the PLAYER.
            ///
            /// ZERO for bombs and elites. Being shoved every time something touches you takes
            /// control away at exactly the moment you are trying to leave, and in a crowd the
            /// shoves compound into being pinballed between bodies. A boss is expected to set
            /// its own - see EnemyController.AttackKnockback.
            /// </summary>
            public const float AttackKnockback = 0f;

            // ---- Ranged (the kiter) ----
            //
            // The second archetype: it never closes to melee range on its own. Where a bomb is a
            // wall of HP you walk through, this one pays for its reach with fragility - it dies
            // fast if you reach it, and punishes you with a telegraphed bolt if you don't.

            /// <summary>Ranged enemy starting HP, before per-wave scaling. Well under a bomb's.</summary>
            public const float RangedHp = 90f;

            /// <summary>Ranged enemy repositioning speed, world units per second.
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float RangedMoveSpeed = 1.61f;

            /// <summary>Ranged enemy bolt damage, before per-wave scaling.</summary>
            public const float RangedDamage = 7f;

            /// <summary>Visual + collider diameter of a ranged enemy, world units. Smaller than a bomb - it reads as the fragile one.</summary>
            public const float RangedSize = 0.62f;

            /// <summary>
            /// The band a ranged enemy tries to hold. Closer than the min and it backs away;
            /// farther than the max (also its cast range) and it closes in; inside the band it
            /// holds and strafes rather than standing dead still.
            /// </summary>
            public const float RangedPreferredMinRange = 4.5f;
            public const float RangedPreferredMaxRange = 7.5f;

            /// <summary>
            /// Seconds the aim is telegraphed before the bolt fires. This is the player's dodge
            /// window - the aim direction is frozen for its entire length, never re-aimed.
            /// </summary>
            public const float RangedTelegraphDuration = 0.6f;

            /// <summary>World width of the telegraph beam, world units.</summary>
            public const float RangedTelegraphWidth = 0.16f;

            /// <summary>Seconds between shots, counted from when a bolt fires.</summary>
            public const float RangedAttackInterval = 1.8f;

            /// <summary>Bolt flight speed, world units per second.</summary>
            public const float RangedProjectileSpeed = 9f;

            /// <summary>Bolt lifetime before it despawns even if it never reaches anything.</summary>
            public const float RangedProjectileLifetime = 3f;

            /// <summary>Fraction of MoveSpeed spent circling the player while holding inside the band.</summary>
            public const float RangedStrafeSpeedFraction = 0.5f;

            /// <summary>
            /// How long it backs off after firing, and how fast.
            ///
            /// ORDINARY BEHAVIOUR, NOT A FLINCH RESPONSE. The Ranged enemy had no reposition at
            /// all - it fired and then stood on cooldown - so there was no post-attack beat for a
            /// denied one to route into. Giving it the kite as its NORMAL habit is what lets a
            /// flinch mean "it lost its shot and gave up ground early" rather than being a bespoke
            /// reaction nothing else in its repertoire matches.
            /// </summary>
            public const float RangedKiteSeconds = 0.85f;
            /// <summary>Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float RangedKiteSpeed = 2.38f;

            // ---- Mortar (the lobber) ----
            //
            // Ranged's opposite temperament: it never closes in, it RUNS. Where Ranged holds a
            // band and strafes, a Mortar spends its whole life backing away from the player and
            // stops only for a moment to lob a shell at where they stood. The shell is not a bolt
            // to sidestep but a patch of ground to leave - it lands, counts down, and goes off -
            // so the threat is the player's own recent path, not a line across the room.

            public const float MortarHp = 75f;

            /// <summary>Retreat speed. Under the player's own, so chasing one down always works -
            /// it just costs the ground the shells are landing on.</summary>
            public const float MortarMoveSpeed = 1.9f;

            /// <summary>The shell's blast damage, before per-wave scaling.</summary>
            public const float MortarDamage = 9f;

            public const float MortarSize = 0.66f;

            /// <summary>Closer than this and it runs; farther and it drifts sideways, keeping its
            /// distance rather than closing it.</summary>
            public const float MortarFleeRange = 6f;

            /// <summary>Its reach - it only walks TOWARD the player when farther than this.</summary>
            public const float MortarRange = 10f;

            /// <summary>Planted wind-up before a lob. Short - the real dodge window is the fuse on
            /// the ground, not this - but long enough that a finisher has something to deny.</summary>
            public const float MortarTelegraphDuration = 0.45f;

            /// <summary>Seconds between lobs, counted from when a shell leaves.</summary>
            public const float MortarAttackInterval = 2.6f;

            /// <summary>Its recover beat after a lob: backing off, like Ranged's kite.</summary>
            public const float MortarRecoverSeconds = 0.6f;

            /// <summary>Fraction of MoveSpeed spent drifting sideways when not being pressed.</summary>
            public const float MortarDriftSpeedFraction = 0.4f;

            /// <summary>Shell flight: ground speed, clamped to a time range so a point-blank lob
            /// still visibly arcs and a cross-room one doesn't hang in the air forever.</summary>
            public const float MortarShellSpeed = 8f;
            public const float MortarShellMinFlight = 0.55f;
            public const float MortarShellMaxFlight = 1.1f;

            /// <summary>Apex height of the arc, as a fraction of the lob's ground distance, plus
            /// a floor so a short lob still reads as thrown rather than slid.</summary>
            public const float MortarShellApexPerUnit = 0.3f;
            public const float MortarShellMinApex = 1.2f;

            /// <summary>
            /// The fuse on the ground: three groups of red blinks - one, two, three - and then
            /// the blast. A pattern rather than a steady glow or a faster-and-faster flicker
            /// because it COUNTS: a player learns "third group means now" after one shell, and
            /// a count reads at a glance where a rate has to be watched.
            /// </summary>
            public const float MortarBlinkOn = 0.12f;
            /// <summary>Dark between two blinks of the SAME group.</summary>
            public const float MortarBlinkGap = 0.09f;
            /// <summary>Dark between groups - clearly longer than a blink gap, or the groups run
            /// together into six even flashes and the count is lost.</summary>
            public const float MortarGroupGap = 0.32f;

            /// <summary>Blast radius. Small - it is a patch to step out of, not a zone.</summary>
            public const float MortarBlastRadius = 1.1f;

            /// <summary>
            /// Parrying a shell IN FLIGHT: how close its DRAWN position (in the air, not its
            /// ground point) must be to the player - the player swats what they can see.
            /// A deflected shell hops back this far toward the side it came from, over this
            /// long, at this apex, then runs the same fuse in blue against enemies instead.
            /// "A bit", not all the way home: the reward is turning a threat into a trap the
            /// player places, not a guaranteed kill on the mortar.
            /// </summary>
            public const float MortarParryReach = 1.0f;
            public const float MortarDeflectDistance = 2.5f;
            public const float MortarDeflectFlight = 0.45f;
            public const float MortarDeflectApex = 0.8f;

            // ---- the elite: a barrage ----

            /// <summary>
            /// An elite lobs this many shells from one wind-up, this far apart, each aimed at the
            /// player's position at its OWN moment - moving leaves a trail of fuses behind.
            /// Every shell is deflectable on its own. At base tuning a parry turns one per volley
            /// - the window (0.2s) is under the spacing and Parry Stance's cooldown (2.5s) is over
            /// the volley's 1s - but a build with enough cooldown reduction to parry again inside
            /// the volley has EARNED the second deflection. Keep the spacing over the parry window,
            /// or one parry starts turning two shells for free.
            /// </summary>
            public const int MortarBarrageShells = 3;
            public const float MortarBarrageSpacing = 0.5f;

            // ---- the flame: the answer to being rushed ----

            /// <summary>
            /// The mortar's only close-range move, and it never goes LOOKING for it: the flame
            /// fires only when the player has come within this range. Then 15s before it will
            /// again - one rush gets answered, and the fight after that is back to the lob.
            /// The cooldown is spent at the WIND-UP, so a flinch that denies it still costs it.
            /// </summary>
            public const float MortarFlameTriggerRange = 2.4f;
            public const float MortarFlameCooldown = 15f;
            public const float MortarFlameWindup = 0.4f;
            public const float MortarFlameSeconds = 2f;
            public const float MortarFlameRange = 3f;
            public const float MortarFlameHalfAngle = 28f;

            /// <summary>How fast the flame turns to follow. At the flame's range the player
            /// (4.1 u/s) sweeps well over 100 deg/s circling it, so this can be outrun - round
            /// the side, not back through it.</summary>
            public const float MortarFlameTurnDegPerSec = 70f;

            /// <summary>Damage per second as a multiple of the shell's damage, dealt in ticks.
            /// Standing in the whole 2s costs about two shells.</summary>
            public const float MortarFlameDpsMul = 1f;
            public const float MortarFlameTickSeconds = 0.2f;

            // ---- Armor: a shield-style absorb pool in front of HP ----
            //
            // BASIC is the default state of any enemy kind (see EnemyKind's own note) - it starts
            // with none and grows one bar every ArmorGrowthFloors floors. ELITE starts with a full
            // extra HP's worth from floor 1 and grows at the same rate on top of that. Both are
            // sized off the enemy's own (post-wave-scaling) max HP, not a flat number, so armor
            // stays meaningful relative to a body that is also getting tankier every wave.

            /// <summary>How many floors between each Basic/Elite armor bar grant.</summary>
            public const int ArmorGrowthFloors = 20;

            /// <summary>One armor bar's absorb capacity, as a fraction of the enemy's own max HP.</summary>
            public const float ArmorBarFraction = 0.5f;

            /// <summary>
            /// Elite's baseline armor from floor 1, as a fraction of its own max HP - before any
            /// of the per-floor growth every kind gets on top. 1.0 = a full extra HP bar's worth
            /// of absorb before real health starts taking hits at all.
            /// </summary>
            public const float EliteArmorFraction = 1.0f;

            // ---- the ELITE TIER ----
            //
            // Elite is a TIER, not a kind. It layers on top of whichever archetype was rolled, so
            // an elite Ranged and an elite Dasher are both expressible - which they were not while
            // Elite occupied its own EnemyKind slot and quietly served as the plain melee enemy.
            //
            // What it grants is deliberately NOT just bigger numbers:
            //
            //   FLINCHES NORMALLY. An elite is interrupted by Medium and Heavy exactly as anything
            //   else is. Denying a player the one tool that answers a wind-up would make elites
            //   read as unfair rather than tough.
            //
            //   RESISTS DISPLACEMENT. Pull, push and knockback do not move it. So Heavy still
            //   stops an elite's attack, it just does not shove it - the interrupt survives, the
            //   repositioning does not, and Undertow cannot gather elites into a pile.
            //
            //   GETS A SECOND ATTACK PATTERN, on top of whatever its kind already does.

            /// <summary>Elite HP as a multiple of its kind's own baseline.</summary>
            public const float EliteHpMul = 2.0f;

            /// <summary>Elite damage as a multiple of its kind's own baseline.</summary>
            public const float EliteDamageMul = 1.4f;

            /// <summary>Elite draw scale, so the tier reads before it is in range to matter.</summary>
            public const float EliteSizeMul = 1.2f;

            // ---- Turret (the stationary beam) ----
            //
            // The third archetype, and the odd one out: it never moves and never picks a spot -
            // EnemyFactory drops it anywhere on the floor, edges included. Where Bomb and Ranged
            // both commit to a telegraphed, dodgeable attack, Turret trades that off entirely -
            // its beam is neither telegraphed nor dodgeable by distance, only by line of sight, so
            // it has to hit far softer per second than anything the player can see coming.

            /// <summary>Turret starting HP, before per-wave scaling. Dies fast once you reach it - it can never retreat or juke.</summary>
            public const float TurretHp = 105f;

            /// <summary>Visual + collider diameter of a turret, world units. Bigger than Ranged - it has to read as a target from across the arena.</summary>
            public const float TurretSize = 0.85f;

            /// <summary>
            /// Effectively unlimited - deliberately larger than the arena's own diagonal
            /// (2 * sqrt(12^2 + 7^2) =~ 27.8 at the default half-extents) so distance is never
            /// what stops the beam. Only <see cref="EnemyController.HasLineOfSight"/> does.
            /// </summary>
            public const float TurretRange = 40f;

            /// <summary>
            /// Beam damage per second, before per-wave scaling. Deliberately under Ranged's
            /// effective DPS (7 dmg / 1.8s =~ 3.9) even though Turret's beam cannot be dodged by
            /// moving away - the only counterplay is breaking line of sight or killing it, so the
            /// per-second cost has to stay low enough that both of those read as optional rather
            /// than mandatory.
            /// </summary>
            public const float TurretDamagePerSecond = 3f;

            /// <summary>
            /// Seconds between beam ticks. Matches the burn/bleed tick cadence in StatusEffects
            /// on purpose - the player already reads that rhythm as "steady damage over time".
            /// </summary>
            public const float TurretTickInterval = 0.5f;

            /// <summary>
            /// How long the beam spins up before it engages.
            ///
            /// The Turret had no telegraph at all - the beam was simply on whenever line of sight
            /// held - which made it the one enemy with nothing to interrupt. A flinch has to have
            /// something to deny, and a continuous effect denies nothing.
            /// </summary>
            public const float TurretChargeDuration = 0.7f;

            /// <summary>How long a denied Turret is off the air. It cannot gain space by moving,
            /// so its "reposition" is the beam having to spin up again from cold.</summary>
            public const float TurretRecoverSeconds = 1.4f;

            /// <summary>World width of the beam's visual, world units.</summary>
            public const float TurretBeamWidth = 0.12f;

            /// <summary>
            /// Minimum distance from the player a Turret is allowed to spawn, world units. It can
            /// land anywhere else on the floor, but "anywhere" still excludes appearing already
            /// mid-beam on someone who hasn't moved yet.
            /// </summary>
            public const float TurretMinPlayerDistance = 3.5f;

            // ---- Dasher (the hit-and-run striker) ----
            //
            // The fourth archetype, and the first that is dangerous up close on its own terms
            // rather than by walking into you (Bomb) or standing there swinging (Elite). It pays
            // for a real melee combo with fragility and total commitment either side of it: a
            // telegraphed glow you can step out of, then a frozen-line charge that whiffs if you
            // did, then a combo it cannot cancel, then a retreat that buys it nothing but distance.

            /// <summary>Dasher starting HP, before per-wave scaling - the low end of the roster, on purpose.</summary>
            public const float DasherHp = 75f;

            /// <summary>Visual + collider diameter of a Dasher, world units. Small and quick-reading.</summary>
            public const float DasherSize = 0.58f;

            /// <summary>Normal chase speed, before it commits to a charge.
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float DasherApproachSpeed = 1.96f;

            /// <summary>Charge speed during the rush itself - well above anything else on the floor, which is the point.
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float DasherRushSpeed = 7f;

            /// <summary>Retreat speed after a combo (landed or missed).
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.</summary>
            public const float DasherEvadeSpeed = 3.15f;

            /// <summary>Distance at which an approaching Dasher stops and begins its telegraph.</summary>
            public const float DasherEngageRange = 6f;

            /// <summary>Melee reach for landing a combo hit once the rush closes the distance.</summary>
            public const float DasherComboRange = 1.2f;

            /// <summary>
            /// Seconds the red glow holds before the charge fires. The charge target is frozen the
            /// MOMENT this starts, not when it ends - same reasoning as Ranged's own aim: the
            /// player needs the whole window watching a committed, known line, not a turret that
            /// could still re-aim on them.
            /// </summary>
            public const float DasherTelegraphDuration = 0.5f;

            /// <summary>Safety backstop on the charge itself, in case the frozen point is never reached
            /// (e.g. it was already adjacent when the telegraph ended).</summary>
            public const float DasherRushMaxSeconds = 1.5f;

            /// <summary>Seconds between each hit in the combo once the charge connects.</summary>
            public const float DasherComboHitInterval = 0.18f;

            /// <summary>Damage per individual combo hit, before per-wave scaling - small alone, a real punish across a full combo.</summary>
            public const float DasherComboHitDamage = 6f;

            /// <summary>Combo length is rolled in this range (inclusive) every time a charge connects.</summary>
            public const int DasherMinComboHits = 3;
            public const int DasherMaxComboHits = 5;

            /// <summary>Seconds spent retreating before the Dasher is willing to approach again.</summary>
            public const float DasherEvadeDuration = 0.9f;

            // ---- Gargoyle (the hazard watcher) ----
            //
            // The first of a planned trio of HAZARD enemies. The family rule: they do not damage
            // the player themselves - the elite's landing slam is the one exception, see below -
            // and instead control the fight by controlling the PLAYER. A basic Gargoyle is a
            // STATIONARY perch, placed anywhere on the floor like a Turret rather than walked in
            // from the edge: it never chases and never swings, and it never carries armor (see
            // EnemyDef.NeverArmored), so it is fought through raw HP alone. Its whole threat is
            // charging for something combat never otherwise costs - standing still is free
            // everywhere else in this game.
            //
            // SIGHT-GATED, NOT PROXIMITY-GATED - the one rule that sets this family apart from
            // every other kind's telegraph. Bomb/Ranged/Chaser/Dasher all commit the moment a
            // telegraph starts and only the target's death cancels it early; a Gargoyle's eyes
            // have to keep seeing you, so losing line of sight mid-glow cancels it outright (see
            // EnemyController.UpdateGargoyle). "After they see the player" is the trigger, so
            // losing sight has to be able to un-trigger it - that is the family's identity, not a
            // shortcut taken building it.

            /// <summary>High, deliberately - well above Chaser's 185, since nothing about this
            /// kind threatens you into disengaging. The HP pool is the whole cost of ignoring it.</summary>
            public const float GargoyleHp = 260f;

            /// <summary>Visual + collider diameter, world units - the biggest body in the roster,
            /// so a perched Gargoyle reads as monumental rather than merely large.</summary>
            public const float GargoyleSize = 1.3f;

            /// <summary>How long the eyes glow before the root lands. The one number the brief
            /// named directly.</summary>
            public const float GargoyleTelegraphDuration = 3f;

            /// <summary>
            /// Effectively unlimited, the same reasoning as TurretRange: a stationary watcher's
            /// reach is bounded by what it can SEE, not by distance. Shared by the basic's
            /// root-eligibility check and the elite's flight trigger - both ask "do I currently
            /// see them", never "are they close enough".
            /// </summary>
            public const float GargoyleSightRange = 40f;

            /// <summary>How long the player is rooted once a glow completes - basic or elite
            /// alike, since the elite's version differing only in radius and damage is what makes
            /// it a wider slam rather than a second, unrelated effect.</summary>
            public const float GargoylePetrifySeconds = 2f;

            /// <summary>Cooldown before a basic Gargoyle may glow again, after landing a root OR
            /// being flinched out of one.</summary>
            public const float GargoyleRecoverSeconds = 2f;

            /// <summary>Minimum distance from the player a Gargoyle is allowed to spawn - placed
            /// like a Turret, not walked in from the edge, so it needs the same "not already
            /// active on someone who has not moved yet" floor as TurretMinPlayerDistance.</summary>
            public const float GargoyleMinPlayerDistance = 4f;

            // ---- the elite: a mobile slam instead of a stationary gaze ----
            //
            // ONLY THE ELITE FLIES, AND ONLY THE ELITE DEALS DAMAGE. Every cycle - not just its
            // first sighting - it leaves its perch, flies to wherever it last saw the player,
            // lands, glows, and slams an AoE root before settling and repeating. This is
            // ElitePattern.Descent, and it is the one elite pattern in the game NOT gated by
            // ElitePatternDue/EliteEvery: every activation is the pattern, not merely every so
            // often, because a Gargoyle with the tier is a genuinely different fight throughout,
            // not a stronger version of the same one.
            //
            // GargoyleDamage IS READ ONLY BY THE ELITE'S LANDING SLAM. The basic's root never
            // calls Health.Take at all, so - unlike every other kind, where EliteDamageMul simply
            // scales a baseline every tier already uses - the basic living at "zero damage" is
            // not expressed as zero in this field. It is expressed by the basic never spending it.

            /// <summary>The elite's landing-slam damage, before EliteDamageMul and floor scaling
            /// - both already apply here through the same pipeline every other kind uses.</summary>
            public const float GargoyleDamage = 18f;

            /// <summary>Radius of the elite's landing slam, world units - meaningfully wider than
            /// the basic's implied point-target root, since this is the pattern's whole point.</summary>
            public const float GargoyleAoeRadius = 2.2f;

            /// <summary>
            /// Flight speed during the elite's swoop - a flat constant read directly rather than
            /// through the ordinary MoveSpeed/FloorDifficulty.Speed pipeline, the same choice
            /// DasherRushSpeed already makes and for the same reason: the target is frozen the
            /// moment the flight commits, so the player dodges by moving BEFORE that moment, not
            /// by outrunning the flight once it is under way. Deliberately fast.
            ///
            /// Cut 30% alongside every other enemy speed and the player's own - see Player.MoveSpeed.
            /// </summary>
            public const float GargoyleFlySpeed = 6.3f;

            /// <summary>Safety backstop on the flight itself, sized against the worst case - a
            /// swoop across the full arena diagonal (~27.8 units) at GargoyleFlySpeed, with
            /// headroom - in case the frozen point is somehow never reached.</summary>
            public const float GargoyleFlyMaxSeconds = 4f;

            /// <summary>Cooldown before the elite may swoop again, after its slam lands OR it is
            /// flinched out of a flight or a glow.</summary>
            public const float GargoyleEliteRecoverSeconds = 1.5f;

            // ---- Booster (the second hazard: a green lion statue) ----
            //
            // NEVER ATTACKS THE PLAYER, AT ALL - not even the AoE exception Gargoyle's elite
            // carries. A stationary statue, like Gargoyle's basic form, and also never armored
            // (see EnemyDef.NeverArmored) for the same reason: it is fought through raw HP, and
            // being able to flinch its cast is the only counterplay this family ever offers.
            //
            // ITS THREAT IS ENTIRELY INDIRECT: it picks one nearby ally (never another Booster,
            // never a Gargoyle - see EnemyController.FindBoosterCandidate for why the latter is
            // excluded), casts on it, and grants that ally its OWN kind's elite attack pattern -
            // Riposte, Volley, Cluster, Overcharge or Redouble - with NO stat change: no HP or
            // damage multiplier, no displacement resistance. An ALREADY-elite ally has nothing
            // left to grant that way, so the cast instead raises what it already deals
            // (BoosterEliteDamageMul). Either way the buff is PERMANENT ON THE TARGET, independent
            // of the Booster's own fate - killing the statue after a successful cast does not undo
            // it, only a fresh one can ever be prevented. The buffed ally is tinted the statue's
            // own pale green, so the effect visibly traces back to its source.
            //
            // DELIBERATELY HAS NO ELITE VARIANT OF ITS OWN. "An elite amplifier" is a real
            // question - boost two targets? boost harder? - without an obvious answer yet, so it
            // ships elite-LESS (EnemyDef.Elite = None) rather than guessed at.

            /// <summary>Lowish, on purpose - this is a priority-kill support unit, not a tank.
            /// Letting it survive is what costs a fight, not clearing it.</summary>
            public const float BoosterHp = 70f;

            /// <summary>Visual + collider diameter, world units.</summary>
            public const float BoosterSize = 1.2f;

            /// <summary>How long the statue channels before the buff lands on its chosen ally.</summary>
            public const float BoosterTelegraphDuration = 2.5f;

            /// <summary>
            /// The cast range - how far the statue can reach an ally to buff, read off
            /// EnemyDef.AttackRange the same way every other kind reuses that field for its own
            /// meaning. Effectively unlimited and sight-gated, the same reasoning as Turret's and
            /// Gargoyle's own ranges: a statue's reach is bounded by what it can see.
            /// </summary>
            public const float BoosterCastRange = 40f;

            /// <summary>Cooldown before the statue may cast again - after a successful cast fails
            /// to find a live target (rare), or after its current ally finally dies.</summary>
            public const float BoosterRecoverSeconds = 2.5f;

            /// <summary>Minimum distance from the player a Booster is allowed to spawn - placed
            /// like a Turret or a Gargoyle, not walked in from the edge.</summary>
            public const float BoosterMinPlayerDistance = 4f;

            /// <summary>Damage multiplier granted to an ALREADY-elite ally, since the ordinary
            /// buff (its kind's own attack pattern) has nothing left to give it.</summary>
            public const float BoosterEliteDamageMul = 1.35f;

            /// <summary>
            /// THE ACCOUNT-PROGRESSION GATE. CharacterProfile.BestFloor is the deepest floor this
            /// character has EVER reached, across every run - already exactly "how far has this
            /// player proven they can go", so it is reused rather than inventing a second
            /// experience stat. Below this, the Booster never spawns at all: a brand-new account's
            /// early floors stay exactly as tuned, and this curveball is reserved for a player who
            /// has already shown they can handle deeper water finding the early floors of a fresh
            /// run trivial.
            /// </summary>
            public const int BoosterUnlockBestFloor = 40;

            /// <summary>The other half of "earlier rounds": even an eligible account stops seeing
            /// it past this floor, since deep floors already have plenty going on without it.</summary>
            public const int BoosterMaxFloor = 15;

            /// <summary>Chance an ELIGIBLE floor actually rolls a Booster, the same "does this
            /// feature appear at all" shape Hazards.ColumnsFloorChance already uses - always
            /// appearing on every early floor would make it predictable furniture rather than a
            /// curveball.</summary>
            public const float BoosterFloorChance = 0.35f;

            // ---- Bubbles (the third hazard: a shielding statue) ----
            //
            // The last of the trio, and structurally Booster's twin: stationary, never armored,
            // never attacks the player, channels for a bit before doing something to a chosen
            // ally rather than to you. What it grants is different in kind rather than in
            // degree - not an attack pattern or a damage bump, but IMMUNITY: a bubbled enemy
            // takes nothing at all, from anything, until a FINISHER pops it (see
            // EnemyController.ApplyBubble and DamageInfo.IsFinisher). Ordinary INTRODUCTION rules
            // rather than Booster's account-progression gate - nothing about this mechanic was
            // asked to be reserved for veteran accounts, so it follows Gargoyle's own shape
            // instead: unlocked by FLOOR depth alone, scaling in the same way.

            /// <summary>Lowish, on purpose, the same reasoning as BoosterHp - this is a
            /// priority-kill support unit, and an active bubble is disruptive enough on its own
            /// that a long fight to reach the statue would compound it rather than balance it.</summary>
            public const float BubblesHp = 65f;

            /// <summary>Visual + collider diameter, world units.</summary>
            public const float BubblesSize = 1.2f;

            /// <summary>How long the statue channels before the bubble lands on its chosen ally -
            /// the same duration as Booster's own cast, per the brief's own "just like the
            /// others".</summary>
            public const float BubblesTelegraphDuration = 2.5f;

            /// <summary>The cast range, read off EnemyDef.AttackRange the same way Booster's own
            /// row reuses that field - effectively unlimited and sight-gated.</summary>
            public const float BubblesCastRange = 40f;

            /// <summary>Cooldown before the statue may cast again - after a successful cast fails
            /// to find a live target (rare), after its current bubble POPS, or after its bubbled
            /// ally dies some other way.</summary>
            public const float BubblesRecoverSeconds = 2.5f;

            /// <summary>Minimum distance from the player a Bubbles is allowed to spawn - placed
            /// like the rest of this family, not walked in from the edge.</summary>
            public const float BubblesMinPlayerDistance = 4f;

            /// <summary>First floor a Bubbles can appear - one floor later than Gargoyle's own,
            /// since it is the more advanced mechanic of the two sight-gated statues to face
            /// first.</summary>
            public const int BubblesFromFloor = 6;

            /// <summary>First floor a Mortar can appear - after Ranged and Dasher, so the player
            /// has met "something shoots at you" and "something runs from you" apart before
            /// meeting the thing that does both.</summary>
            public const int MortarFromFloor = 4;

            // ---- the elite: a more resilient bubble, not a different one ----
            //
            // UNLIKE its two siblings, Bubbles DOES carry an elite tier - it is in
            // WaveComposer.EliteKinds and draws the floor's elite share like any ordinary kind. What
            // the tier grants is read directly off the Elite bool in ResolveBubbles rather than
            // declared as an EnemyDef.Elite pattern, because there is no distinct extra ACTION to
            // name: a basic and an elite Bubbles perform the identical channel-then-bubble cycle,
            // and the only difference is how many finishers the result takes to break - the same
            // "read Elite directly, no ElitePattern needed" shape Gargoyle's own Elite check uses
            // for its fly-vs-stand fork, just here it is a number rather than a mechanism.

            /// <summary>Finisher hits an elite's bubble absorbs before popping - "turning an
            /// inconvenience into an issue that actually must be dealt with", per the brief.
            /// Also what a SECOND cast (elite or not) adds when it stacks onto an existing bubble
            /// - see EnemyController.BubbleCharges.</summary>
            public const int BubblesEliteCharges = 2;
        }

        /// <summary>
        /// How enemies get AROUND the room - Enemies.NavField (the route), EnemyController's
        /// steering section (the last few metres) and Enemies.EnemySurround (where a melee pack
        /// stands). None of this changes what an enemy does once it arrives.
        /// </summary>
        public static class Steering
        {
            // ---- the nav grid ----

            /// <summary>World size of one nav cell. 0.5 puts ~1300 cells on the 24x14 arena - a
            /// full Dijkstra over them is a fraction of a millisecond.</summary>
            public const float CellSize = 0.5f;

            /// <summary>How far a cell's CENTRE must sit from any solid static collider (wall,
            /// column, spire) to count as standable. Roughly the largest ordinary enemy's radius;
            /// physics still owns the real contact, this only keeps routes from hugging corners.</summary>
            public const float Clearance = 0.4f;

            /// <summary>Seconds between route recomputes. The player moves ~0.5u in this.</summary>
            public const float RouteInterval = 0.15f;

            /// <summary>Seconds between obstacle re-scans, as a backstop - a broken column and a
            /// new floor mark the grid dirty straight away.</summary>
            public const float ObstacleInterval = 1.0f;

            /// <summary>Cells of the route followed ahead when looking for the farthest one still
            /// in a straight line, so a route reads as one curve rather than grid steps.</summary>
            public const int RouteLookaheadCells = 12;

            // ---- what each pit costs a route, as EXTRA cost per unit crossed (0 = plain floor) ----

            /// <summary>Sand roughly halves speed, so crossing it takes about twice as long - a
            /// little over that so a pack prefers the floor when the detour is short.</summary>
            public const float SandRouteCost = 1.5f;

            /// <summary>Spikes hurt anything walking across them; enemies go round unless the
            /// detour is long.</summary>
            public const float SpikeRouteCost = 6f;

            /// <summary>Water does not slow or hurt, but a pack loses its footing in it; a short
            /// detour is preferred, a long one is not.</summary>
            public const float WaterRouteCost = 0.6f;

            /// <summary>Fire at FULL heat. Scaled by the heat it WILL have shortly (see
            /// FireRouteLookahead), so a pack does not walk in just before it flares.</summary>
            public const float FireRouteCost = 14f;

            /// <summary>Seconds ahead a route reads a fire pit's heat - the larger of now and then.</summary>
            public const float FireRouteLookahead = 1.5f;

            /// <summary>Extra cost above which a cell breaks a straight-line shortcut, so a route
            /// that went round a pit is not undone by cutting across it.</summary>
            public const float ShortcutCostLimit = 0.75f;

            // ---- what pits do to enemies standing in them ----
            //
            // A FRACTION OF THE ENEMY'S MAX HP, not the player's flat per-second numbers: enemy HP
            // climbs with depth and the player's pit damage is sized for a player, so a flat number
            // would be lethal on floor 1 and invisible by floor 30.

            /// <summary>Fraction of max HP per second in a fire pit at full heat.</summary>
            public const float FireEnemyHpPerSecond = 0.08f;

            /// <summary>Fraction of max HP per second while MOVING across spikes.</summary>
            public const float SpikeEnemyHpPerSecond = 0.06f;

            /// <summary>Seconds between pit damage ticks on an enemy.</summary>
            public const float PitEnemyTickInterval = 0.5f;

            // ---- local steering ----

            /// <summary>How far ahead a candidate direction is checked for walls, columns and pits.</summary>
            public const float Lookahead = 1.6f;

            /// <summary>A direction with less free floor than this ahead is not taken at all.</summary>
            public const float MinFree = 0.45f;

            /// <summary>How hard a direction is penalised for running toward an obstacle it can
            /// still reach (1 - free/Lookahead, times this).</summary>
            public const float WallWeight = 0.6f;

            /// <summary>Penalty per unit of pit route cost met along a candidate direction.</summary>
            public const float PitWeight = 0.12f;

            /// <summary>Gap between two enemies' edges inside which they steer apart.</summary>
            public const float SeparationGap = 0.35f;

            /// <summary>How hard a neighbour inside the gap pushes a direction's score down.</summary>
            public const float SeparationWeight = 0.8f;

            /// <summary>Fleeing: weight on open floor ahead, so a retreat runs toward space
            /// instead of into the nearest corner.</summary>
            public const float FleeOpenness = 0.9f;

            /// <summary>Fleeing: directions passing closer than this to the player are penalised,
            /// so a cornered kiter breaks out along the wall rather than through the player.</summary>
            public const float FleePlayerBuffer = 1.6f;

            // ---- variety (no two walk the same line) ----

            /// <summary>Range of each enemy's weave, degrees either side of its route.</summary>
            public const float WeaveDegreesMin = 10f, WeaveDegreesMax = 28f;

            /// <summary>Range of each enemy's weave rate, cycles per second.</summary>
            public const float WeaveHzMin = 0.2f, WeaveHzMax = 0.45f;

            /// <summary>The weave fades to nothing inside this distance of the player - the last
            /// steps of an approach are straight, or a swing lands from an odd angle.</summary>
            public const float WeaveFadeNear = 2.5f, WeaveFadeFar = 6f;

            /// <summary>Range of each enemy's turn rate (the velocity lerp per physics step; 0.2 was
            /// the old fixed value).</summary>
            public const float TurnRateMin = 0.14f, TurnRateMax = 0.26f;

            // ---- the surround ----

            /// <summary>Melee enemies within this distance of the player are given a side to stand on.</summary>
            public const float SurroundRadius = 7f;

            /// <summary>Seconds between slot assignments.</summary>
            public const float SurroundInterval = 0.25f;

            /// <summary>Widest gap between two neighbouring slots, degrees.</summary>
            public const float SlotSpacingMaxDegrees = 55f;

            /// <summary>The widest arc a pack spreads over, degrees. UNDER 360 ON PURPOSE: a pack
            /// always leaves a way out on the side away from where it came in - surrounded with no
            /// exit is a death sentence, not a positioning problem.</summary>
            public const float SurroundMaxArcDegrees = 240f;

            /// <summary>Where on its engage range a slot sits (1 = right at the edge of it).</summary>
            public const float SlotRadiusFraction = 0.85f;

            // ---- stuck ----

            /// <summary>Seconds per stuck sample; less than StuckMinTravel moved in one, while
            /// trying to move, counts as stuck.</summary>
            public const float StuckSampleSeconds = 1.0f;
            public const float StuckMinTravel = 0.25f;

            /// <summary>Seconds spent pushing a random open way out once stuck.</summary>
            public const float UnstickSeconds = 0.6f;

            /// <summary>Seconds stuck before an enemy that the route cannot reach at ALL (walled
            /// into a pocket, or inside geometry) is moved to the nearest reachable floor.</summary>
            public const float RelocateAfterSeconds = 5f;

            /// <summary>Seconds stuck before it is logged for StuckWatch-style reporting.</summary>
            public const float ReportAfterSeconds = 10f;

            // ---- the Dasher meets a column ----

            /// <summary>Seconds a Dasher reels after its rush hits something solid.</summary>
            public const float DasherWallStunSeconds = 1.4f;

            /// <summary>Seconds after a slam during which a Dasher only charges with a CLEAR line
            /// to the player, walking round otherwise. Without it, standing behind a column held one
            /// off forever - bait it once, then it is wary for a while.</summary>
            public const float DasherShySeconds = 8f;
        }

        /// <summary>
        /// Room shapes (Hazards.RoomShape): interior walls carving the arena rectangle into a
        /// plaza, a hall, three chambers... on ordinary combat floors. Seeded per floor.
        /// </summary>
        public static class Rooms
        {
            /// <summary>Floors before this are always the open rectangle - the basics are learned
            /// in a plain room before the room starts arguing with them.</summary>
            public const int FromFloor = 4;

            /// <summary>Chance a floor from <see cref="FromFloor"/> is shaped. Open stays the single
            /// most common shape (40% against ~8.6% for each of seven), but most floors have one.</summary>
            public const float ShapedChance = 0.6f;

            /// <summary>A cracked segment's health PER CELL - a four-cell run is about a Large
            /// column's 50, so breaking a shortcut costs what breaking the biggest pillar does.</summary>
            public const float CrackedHpPerCell = 12f;

            /// <summary>The visible south face of a wall block, as a fraction of its cell - the
            /// part of the block that reads as HEIGHT from the 3/4 camera.</summary>
            public const float FaceFraction = 0.42f;
        }

        /// <summary>Chasms (`~` in a room shape): void nothing stands on. See EnemyController.TickChasm
        /// and PlayerController's edge.</summary>
        public static class Chasm
        {
            /// <summary>How long after a hit MOVED an enemy it may still fall - a Heavy finisher's
            /// shove takes a few physics steps to carry a body over the lip. An enemy found over
            /// the void outside this is put back on the floor instead.</summary>
            public const float ShoveGrace = 0.6f;

            /// <summary>How far past the lip a body's centre must be to fall. A toe over the edge
            /// is not a fall - for the enemy, or for a player whose dash ended short.</summary>
            public const float FallInset = 0.18f;

            /// <summary>How far a displacing hit can THROW a body into a chasm along its own
            /// direction: knockback x this, clamped. Heavy finishers' knockbacks run 3-11, so the
            /// reach runs ~0.6-1.8 - stand them near the lip, then swing them over it.</summary>
            public const float ThrowReachPerKnockback = 0.16f;
            public const float ThrowReachMin = 0.6f;
            public const float ThrowReachMax = 2.0f;

            /// <summary>The glide over the lip before the fall.</summary>
            public const float ThrowSeconds = 0.14f;

            /// <summary>The fall itself: shrinking into the dark, before the kill lands.</summary>
            public const float FallSeconds = 0.4f;

            /// <summary>Where the player's walk stops: the body's centre this far from the void.</summary>
            public const float EdgeMargin = 0.12f;

            /// <summary>A dash or leap ending over the void within this of standing ground is
            /// put back on the lip, not dropped - a lunge that overshoots by a hand is not a
            /// mistake worth a fall.</summary>
            public const float Forgiveness = 0.55f;

            /// <summary>What a fall costs the player: a fraction of max HP, through mitigation (a
            /// MECHANIC hit, like the bosses' - Tuning.Boss's rule), and a return to the last
            /// ground they stood on.</summary>
            public const float PlayerFallFraction = 0.15f;
        }

        /// <summary>
        /// Arena room-layout hazards: columns, the force fields strung between them, and the three
        /// floor pits (fire, sand, spike). Placed procedurally per floor by HazardBuilder - every
        /// count/span/chance here is what that generation rolls against.
        /// </summary>
        public static class Hazards
        {
            // ---- Columns ----

            /// <summary>World radius of each column size tier.</summary>
            public const float ColumnRadiusSmall = 0.45f;
            public const float ColumnRadiusMedium = 0.7f;
            public const float ColumnRadiusLarge = 1.05f;

            /// <summary>
            /// HP of a CRACKED column at each size tier - small breaks fastest, large slowest.
            /// Non-cracked columns never get a Health component at all, so these never apply to
            /// them.
            /// </summary>
            public const float ColumnHpSmall = 12f;
            public const float ColumnHpMedium = 26f;
            public const float ColumnHpLarge = 50f;

            /// <summary>
            /// Chance a floor has ANY columns (and by extension any force fields, which only ever
            /// anchor to columns) at all - rolled once, before any size/count logic below runs.
            /// A room that's sometimes just open floor is worth more than the grid always being
            /// there in some quantity.
            /// </summary>
            public const float ColumnsFloorChance = 0.75f;

            /// <summary>At most one Large column per floor, and only ever near arena center.</summary>
            public const int MaxLargeColumns = 1;
            /// <summary>Chance a floor rolls its one Large column at all, before it scales with floor.</summary>
            public const float LargeColumnBaseChance = 0.5f;

            public const int MaxMediumColumns = 4;
            public const int MaxSmallColumns = 8;

            /// <summary>Chance any given column placed (any size) rolls Cracked rather than permanent.</summary>
            public const float ColumnCrackedChance = 0.6f;

            /// <summary>
            /// Columns snap to a lattice at this spacing rather than landing freely - a room reads
            /// as built rather than scattered, and it removes the need for a separate clearance
            /// check between two columns entirely (any two distinct points are already this far
            /// apart). Chosen so adjacent points - orthogonal AND diagonal - still fall inside
            /// ForceFieldMinSpan/MaxSpan below without retuning either: 2.5 apart orthogonally,
            /// 3.54 diagonally, both inside [2, 6]; two steps orthogonally (5.0) still fits, two
            /// diagonally (7.07) correctly falls outside it.
            /// </summary>
            public const float ColumnGridSpacing = 2.5f;

            /// <summary>How close to the arena's own walls a grid point may sit.</summary>
            public const float ColumnGridWallMargin = 1.4f;

            /// <summary>Minimum clearance between a hazard and the player. Also what lava (still
            /// freely placed, not on the column grid) keeps from every column.</summary>
            public const float HazardClearance = 1.6f;

            // ---- Force fields ----

            /// <summary>A field only forms between two columns whose distance falls in this span -
            /// too close and it's redundant with the columns' own bodies, too far and it stops
            /// reading as "strung between" them.</summary>
            public const float ForceFieldMinSpan = 2f;
            public const float ForceFieldMaxSpan = 6f;

            /// <summary>Rolled independently for every eligible pair - STRAIGHT and NEIGHBOURING
            /// (HazardBuilder). 0.6, up from 0.35 when diagonal and skip-over pairs were eligible
            /// too: simulated, it keeps ~2.2 fields a floor from floor 10 (~0.8 on the sparse early
            /// floors), with a column carrying two or more fields on ~45% of floors.</summary>
            public const float ForceFieldChancePerEligiblePair = 0.6f;
            /// <summary>4, up from 3 (the user's call) once a column could carry a field in each
            /// direction: simulated, ~2.8 fields a floor from floor 10 and a column carrying two
            /// or more on ~60% of floors (3 gave ~2.2 and ~45%).</summary>
            public const int MaxForceFieldsPerFloor = 4;

            /// <summary>Chance a placed field is the flip-capable kind rather than fixed-color.</summary>
            public const float ForceFieldFlipChance = 0.4f;
            /// <summary>Seconds between color flips on a flip-capable field.</summary>
            public const float ForceFieldFlipInterval = 5f;

            public const float ForceFieldWidth = 0.3f;

            // ---- Floor pits: shared geometry ----
            //
            // Pits occupy CELLS of the column lattice rather than points on it, which is what
            // makes "a pit can never overlap a column" structural instead of a clearance check.
            // See Hazards.PitGrid.

            /// <summary>
            /// How far a pit's drawn edge pulls in from its cell block's own boundary. Small, but
            /// not zero: at zero the pit's edge lands exactly on the lattice line a corner column
            /// is centred on, and the two read as one clipping the other rather than as adjacent.
            /// </summary>
            public const float PitCellInset = 0.18f;

            /// <summary>
            /// Extra clearance, on top of ColumnRadiusLarge, that a lattice point must have from a
            /// pit's rect to still be usable for a column. A large column is 1.05 across the
            /// radius against a 2.5 grid spacing, so a column merely OUTSIDE a pit can still
            /// overhang most of its own body into it - the exclusion has to be measured from the
            /// column's rim, not its centre.
            /// </summary>
            public const float PitColumnMargin = 0.25f;

            /// <summary>
            /// How far inside its own footprint the player must be to count as standing in a pit.
            /// A hazard that fires on the outermost texel of its art reads as having a bigger
            /// hitbox than it is drawn with - the same complaint the swept-capsule rewrite records
            /// against the old attack cone.
            /// </summary>
            public const float PitStandInset = 0.2f;

            /// <summary>Depth of the recess's far wall - how deep the hole reads, not how deep it is.</summary>
            public const float PitWallDepth = 0.34f;
            /// <summary>The bare broken-stone hairline along a pit's top edge.</summary>
            public const float PitLipThickness = 0.06f;

            // ---- Floor pits: layout ----

            /// <summary>
            /// Relative weights for the floor's layout roll: one Large pit, two Mediums (one per
            /// half), or four Smalls (one per quadrant). Small is weighted up because more, smaller
            /// pits leave more routes through the room, and a floor whose centre is one large
            /// hazard is a statement that should stay occasional.
            /// </summary>
            public const float PitLayoutWeightLarge = 1f;
            public const float PitLayoutWeightMedium = 2f;
            public const float PitLayoutWeightSmall = 3f;

            /// <summary>
            /// Chance a floor has any pits at all, rolled before size or kind - the same "a room
            /// that is sometimes just open floor" argument ColumnsFloorChance makes.
            /// </summary>
            public const float PitsFloorChance = 0.7f;

            /// <summary>
            /// The floor from which pit KINDS may mix on one board. Below it a floor's pits are
            /// all one kind; at and above it each pit rolls its own, so fire, sand and spikes can
            /// share a room. Set to the first avatar boss, so the change is something the player
            /// walks out of a milestone fight into rather than a number that quietly ticks over.
            /// </summary>
            public const int PitMixedKindsFromFloor = 25;

            // ---- Fire pit ----

            /// <summary>
            /// Full engage-to-engage cycle, rolled PER PIT between these. Damage and every layer of
            /// the art ride the pit's one clock. Was one shared 6s: with a shared period only the
            /// start offsets differed, so the gaps between pits never changed and a floor of fire
            /// played one fixed pattern on loop. Rolled lengths drift in and out of step instead.
            /// The short end still leaves a lull a small pit can be crossed in.
            /// </summary>
            public const float FireCycleSecondsMin = 4.5f, FireCycleSecondsMax = 8.5f;

            /// <summary>Share of each pit's cycle spent above half heat (burning rather than
            /// ashing), rolled per pit - a fire that burns long and rests short reads as a
            /// different hazard from one that flashes and dies. Kept near the middle so neither
            /// half is too short to read.</summary>
            public const float FireBurnShareMin = 0.38f, FireBurnShareMax = 0.62f;
            /// <summary>Damage per second at full engagement - 0 whenever no flames show (below FireTongueThreshold).</summary>
            public const float FireMaxDamagePerSecond = 9f;
            /// <summary>Tick cadence, matching the Turret beam / StatusEffects burn-bleed rhythm.</summary>
            public const float FireTickInterval = 0.5f;

            /// <summary>
            /// Below this heat there are no flames at all, only ash and coals - and NO DAMAGE. The gap is what
            /// gives the cycle a readable "safe to cross" half rather than a continuous dimming.
            /// </summary>
            public const float FireTongueThreshold = 0.22f;

            /// <summary>Flame density, so a large pit is a bigger FIRE and not the same fire spread thin.</summary>
            public const float FireTonguesPerSquareUnit = 1.1f;
            public const int FireMinTongues = 4;
            public const int FireMaxTongues = 22;

            public const float FireTongueWidthMin = 0.28f;
            public const float FireTongueWidthMax = 0.5f;

            /// <summary>How far past the pit the glow reaches, and how strong it gets at full heat.</summary>
            public const float FireBloomScale = 1.5f;
            public const float FireBloomAlpha = 0.5f;

            // ---- Sand pit ----

            /// <summary>
            /// Movement multiplier while standing in sand. Two thirds is a real cost without ever
            /// approaching the enemy walk speeds (2.1-2.6 against a slowed 4.3) - sand must make
            /// retreating expensive, never make it impossible, or it stops being a positioning
            /// problem and becomes a trap that kills you for stepping in it.
            ///
            /// Deepened from 0.66 to 0.51 (a further 15 points). At MoveSpeed 4.1 that is ~2.1 in
            /// sand - now level with the faster enemy walk speeds, so sand is where a pack catches up.
            /// </summary>
            public const float SandSpeedMultiplier = 0.51f;

            // ---- Spike pit ----

            /// <summary>Damage per second while moving inside the bed. Zero while stationary.</summary>
            public const float SpikeDamagePerSecond = 11f;
            public const float SpikeTickInterval = 0.4f;

            /// <summary>
            /// Speed below which the player counts as stopped. Measured in world units per second
            /// off real displacement, so it has to clear the residue a blended stop leaves behind
            /// (PlayerController lerps velocity rather than zeroing it) without being so high that
            /// creeping across the bed is free.
            /// </summary>
            public const float SpikeMovementThreshold = 0.35f;

            /// <summary>Nominal spacing of the jittered lattice the jacks are scattered on.</summary>
            public const float SpikeJackSpacing = 0.62f;
            /// <summary>Fraction of lattice cells deliberately left empty, so the bed is strewn rather than a grate.</summary>
            public const float SpikeJackSkipChance = 0.22f;
            public const float SpikeJackSizeMin = 0.26f;
            public const float SpikeJackSizeMax = 0.42f;

            // ---- Water pit ----

            /// <summary>
            /// GRIP: the most of the gap between the body's velocity and the one it is asking for
            /// that a single physics step may close while standing in water (dry ground closes
            /// 0.55 of it walking, 0.25 stopping - PlayerController). The lowest grip under a body
            /// wins, the same "ground cannot be layered" rule as speed.
            ///
            /// 0.05 at the 50Hz step: let go at full walking speed and the body carries on about a
            /// unit (measured ~1.1 further than on dry ground) - half a Small puddle - and
            /// reversing takes most of a second. TOP speed is untouched (measured 3.03 dry, 2.93
            /// still converging on water): the body's damping is re-solved to keep it
            /// (FloorPits.DampingFor). Water does not slow you, it stops you steering.
            /// </summary>
            public const float WaterGrip = 0.05f;

            /// <summary>
            /// SOAKED: standing in water, the player takes WaterSoakDamageTaken more damage from
            /// EVERYTHING, held while they stand in it and for WaterSoakSeconds after they leave.
            /// Outside the mitigation floor (Art.Gear.StatCurves.Incoming), like being caught
            /// exposed - a state of the fight, so armour does not buy it away. Water's other price
            /// is the slide; this one follows the player out of the puddle and into the pack.
            /// Enemies are not soaked by it (Water's own Soak is the element's debuff, not ground's).
            /// </summary>
            public const float WaterSoakDamageTaken = 0.25f;
            public const float WaterSoakSeconds = 3f;
            /// <summary>Seconds between the soaked readout's drips at the player's feet.</summary>
            public const float WaterSoakCueInterval = 0.3f;

            /// <summary>Body speed (u/s) above which wading throws up a ripple at the feet, and
            /// the seconds between them - the "you are sliding" readout.</summary>
            public const float WaterWakeSpeed = 0.6f;
            public const float WaterWakeInterval = 0.16f;

            /// <summary>Seconds between the still surface's own ripples, rolled per ripple.</summary>
            public const float WaterRippleIntervalMin = 0.5f, WaterRippleIntervalMax = 1.4f;
            /// <summary>Widest a ripple grows (world units across), and how long it takes. Shrunk
            /// near the rim so a ring never spills over the lip.</summary>
            public const float WaterRippleSize = 0.9f;
            public const float WaterRippleSeconds = 0.9f;

            /// <summary>The recess under water reads shallower than a dry pit's - it is a basin
            /// that filled, not a hole.</summary>
            public const float WaterRecessDepth = 0.55f;
        }

        /// <summary>
        /// Tornadoes: funnels that form on open floor while a floor's wave is alive, wander for
        /// <see cref="SpinSeconds"/> hurting a player standing in them, then die away. See
        /// Hazards/Tornado.cs.
        /// </summary>
        public static class Tornado
        {
            /// <summary>First floor a storm can roll. Never a boss or puzzle floor (the caller's say,
            /// as for the spire). How OFTEN is not a number here: an eligible floor storms exactly as
            /// often as it holds any one pit kind (HazardBuilder.PitKindChance), so Air's hazard -
            /// and the Sylph boon that answers it - turns up as much as Fire's, Earth's and Water's.</summary>
            public const int FromFloor = 5;

            /// <summary>Most funnels alive at once: one, then two from TwoFrom, three from ThreeFrom.</summary>
            public const int TwoFrom = 20;
            public const int ThreeFrom = 45;

            /// <summary>Seconds from the room being built to the first funnel - the wave arrives
            /// first, so the player meets the room before its weather.</summary>
            public const float FirstFormMin = 3f, FirstFormMax = 5f;
            /// <summary>Seconds between one funnel forming and the next, rolled each time.</summary>
            public const float FormIntervalMin = 4f, FormIntervalMax = 7.5f;

            /// <summary>
            /// The three phases. FORMING is the telegraph: dust and a ring on the ground where it
            /// will stand, harmless and still. SPINNING is the set lifetime, hurting and moving.
            /// DISSIPATING is harmless again, so the last frame of a fading funnel never ticks.
            /// </summary>
            public const float FormSeconds = 1.3f;
            public const float SpinSeconds = 8f;
            public const float DissipateSeconds = 0.9f;

            /// <summary>The damaging radius at the funnel's foot. The ring on the ground is drawn
            /// exactly on it - what hurts is what is drawn.</summary>
            public const float Radius = 0.6f;

            /// <summary>Damage per second inside, scaled by FloorDifficulty.Damage like the
            /// spire's lines - a roaming hazard keeps pace with the enemies it roams among.</summary>
            public const float DamagePerSecond = 8f;
            public const float TickInterval = 0.4f;

            /// <summary>
            /// Drift speed (u/s). Under every enemy's walk and well under the player's 3.1, so a
            /// funnel is always something you step round, never something that runs you down.
            /// </summary>
            public const float Speed = 1.25f;
            /// <summary>Peak wander turn rate (deg/s). Two incommensurate sines, so the path
            /// curls, loops and straightens rather than zig-zagging on a beat.</summary>
            public const float WanderDegreesPerSecond = 75f;

            /// <summary>How far from the player a funnel may form, and from another funnel.</summary>
            public const float FormPlayerClearance = 3.2f;
            public const float FormSpacing = 2.5f;

            /// <summary>Funnel height and top width (world units). The foot is narrow; the
            /// funnel widens as it rises.</summary>
            public const float Height = 2.1f;
            public const float TopWidth = 1.9f;
            /// <summary>Degrees per second the streaks circle the funnel.</summary>
            public const float SpinDegreesPerSecond = 520f;
        }

        /// <summary>
        /// Spires: a capture point that pays ONE boon for the rest of the floor (or, for Heal and
        /// Repair, once on the spot). See Hazards/Spire.cs.
        /// </summary>
        public static class Spire
        {
            // ---- When one appears ----

            /// <summary>Chance an eligible floor has a spire. Semi-rare: about one floor in five.
            /// Never on a boss or puzzle floor, and never more than one.</summary>
            public const float FloorChance = 0.2f;
            /// <summary>First floor a spire can appear - floor 1 is the introduction to the wave.</summary>
            public const int FromFloor = 2;

            // ---- The capture ----

            /// <summary>Radius of the capture ring - the player's body must be inside it.</summary>
            public const float CaptureRadius = 2.1f;
            /// <summary>Seconds of standing in the ring to capture it.</summary>
            public const float CaptureSeconds = 7f;
            /// <summary>Progress lost per second OUTSIDE the ring, as a fraction of the capture
            /// rate - 1 drains exactly as fast as it fills. Drains rather than resets: stepping
            /// out to dodge costs the time spent out, not the whole attempt. Enemies never pause
            /// it; they make standing there expensive instead.</summary>
            public const float DecayRate = 1f;
            /// <summary>Seconds for the spire to rise out of / sink into the floor.</summary>
            public const float SinkSeconds = 1.6f;
            /// <summary>Share of the floor's enemies that must be dead before the spire rises
            /// (at least one kill). Rising mid-fight is what makes it a floor EVENT rather than
            /// a fixture: the capture happens with most of the pack still on the floor.</summary>
            public const float RiseAtDefeatedFraction = 0.10f;

            /// <summary>The obelisk itself: solid, and blocks sight like a column.</summary>
            public const float BodyRadius = 0.42f;
            public const float BodyHeight = 1.9f;
            /// <summary>Clearance from the player's spawn and the doors to the RING. Pits are not
            /// avoided - a spire may stand in one.</summary>
            public const float Clearance = 1.4f;

            // ---- The boons ----

            public const float HealFraction = 0.35f;        // of max health, at capture
            public const float RepairFraction = 0.35f;      // of armour condition, at capture
            // The floor-long boons, in RUN-LAYER POINTS: added like a ledger entry and bent through
            // the same Vessel (RunModifiers), so a spire tops a run up and never past what the run
            // may hold. Haste and Swiftness were multipliers on top of the ledger - compounding with
            // it - until the stat core gave every run source one unit.
            public const float DamagePoints = 30f;          // Iron: a share of the base hit, as the ledger's damage boon
            public const float AttackSpeedPoints = 25f;     // Quicksilver
            public const float MoveSpeedPoints = 20f;       // Silver
            public const float CritChance = 0.15f;          // Gold: into the one crit pool (StatCurves.Crit)
            public const float DamageTakenMul = 0.75f;      // Tin: mitigation, inside the one floor (StatCurves.Incoming)

            // ---- Challenge: rotating damage lines ----

            /// <summary>Chance a spire carries lines, rising with depth.</summary>
            public const float LinesBaseChance = 0.35f;
            public const float LinesChancePerFloor = 0.01f;
            public const float LinesMaxChance = 0.85f;
            /// <summary>Line count by depth: 1, then 2 from LinesTwoFrom, 3 from LinesThreeFrom.</summary>
            public const int LinesTwoFrom = 15;
            public const int LinesThreeFrom = 40;
            /// <summary>Sweep speed (deg/s), rising with depth to the max. Slow enough to step over
            /// at the base speed; the danger is a second line arriving while you're stepping.</summary>
            public const float LinesBaseSpeed = 38f;
            public const float LinesSpeedPerFloor = 0.6f;
            public const float LinesMaxSpeed = 85f;
            /// <summary>Half-width of a line's damaging band, world units.</summary>
            public const float LineHalfWidth = 0.22f;
            /// <summary>Damage per hit (scaled by FloorDifficulty.Damage), and the gap before the
            /// same line can hit again - a line sweeping over a stationary player hits once.</summary>
            public const float LineDamage = 9f;
            public const float LineHitCooldown = 0.6f;

            // ---- Challenge: the range bubble ----

            /// <summary>Chance a spire carries a bubble. From BubbleFromFloor only.</summary>
            public const int BubbleFromFloor = 6;
            public const float BubbleBaseChance = 0.3f;
            public const float BubbleChancePerFloor = 0.005f;
            public const float BubbleMaxChance = 0.6f;
        }

        /// <summary>
        /// DIMINISHING RETURNS - see Art.Gear.StatCurves for the shape and docs/balance for why.
        ///
        /// Two layers, each with its own thresholds, multiplied together: the CHARACTER (gear and
        /// the board) and the RUN (the ledger and a spire's floor boon). A stat is linear up to its
        /// KNEE - so ordinary play reads the printed number exactly - then bends toward its CAP.
        /// The run layer's thresholds are the VESSEL: they grow with the element's mastery level, so
        /// boons cannot carry an unlevelled character past what gear and the board give.
        ///
        /// All in percentage points. A knee at or above its cap means no curve at all.
        /// </summary>
        public static class Stats
        {
            // ---- the character layer: knee, cap ----
            public const float DamageKnee = 80f, DamageCap = 200f;
            public const float AttackSpeedKnee = 50f, AttackSpeedCap = 120f;
            public const float MoveSpeedKnee = 20f, MoveSpeedCap = 40f;
            public const float CritDamageKnee = 60f, CritDamageCap = 150f;
            public const float WeaponArtKnee = 60f, WeaponArtCap = 150f;
            public const float RangeKnee = 25f, RangeCap = 50f;
            public const float AreaKnee = 40f, AreaCap = 80f;
            public const float ElementGrowthKnee = 50f, ElementGrowthCap = 120f;
            public const float ElementalPowerKnee = 80f, ElementalPowerCap = 200f;
            public const float MaxHpKnee = 60f, MaxHpCap = 150f;

            // ---- the run layer (the Vessel at full mastery): knee, cap ----
            public const float RunDamageKnee = 50f, RunDamageCap = 100f;
            public const float RunAttackSpeedKnee = 30f, RunAttackSpeedCap = 60f;
            public const float RunMoveSpeedKnee = 15f, RunMoveSpeedCap = 30f;
            public const float RunWeaponArtKnee = 50f, RunWeaponArtCap = 100f;
            // The exchange rebuild (2026-10-05) put every stat the ledger touches under the Vessel,
            // so no boon escapes it by naming a stat it did not cover.
            public const float RunCritDamageKnee = 40f, RunCritDamageCap = 80f;
            public const float RunElementalPowerKnee = 40f, RunElementalPowerCap = 80f;
            public const float RunElementGrowthKnee = 30f, RunElementGrowthCap = 60f;
            public const float RunAreaKnee = 25f, RunAreaCap = 50f;
            public const float RunRangeKnee = 15f, RunRangeCap = 30f;
            public const float RunMaxHpKnee = 25f, RunMaxHpCap = 50f;

            /// <summary>The Vessel at mastery level 0, as a share of its full size: an unlevelled
            /// character's boons saturate at half the thresholds a capped one's do. It grows
            /// linearly to full at Mastery.LevelCap.</summary>
            public const float VesselAtLevelZero = 0.35f;

            /// <summary>
            /// Crit chance is ONE pool - base, element, gear, ledger, the perfect streak - with ONE cap
            /// for every element. An element only reaches it sooner. Chance past the cap is not lost:
            /// each whole 1.0 of it adds this much to the crit multiplier, so a player who prefers a
            /// little less chance and more damage, or overshoots, never wastes a point.
            /// </summary>
            public const float CritChanceCap = 0.60f;
            public const float CritOverflowToDamage = 1.5f;

            /// <summary>Lifesteal from every source, as one pool, never above this fraction.</summary>
            public const float LifestealCap = 0.12f;

            /// <summary>
            /// The most lifesteal may heal per second, as a share of max health. Deep down, uncapped
            /// lifesteal healed up to 98% of max health a second (2026-10-05 baseline), so a fight was
            /// a one-shot or nothing; capped, it tops you up between hits instead.
            /// </summary>
            public const float LifestealHealPerSecond = 0.025f;

            /// <summary>The least share of a hit that always gets through stat-based mitigation
            /// (armour condition, Resilience, Graze/Brace, the element, the ledger) - stacked
            /// defence makes you sturdy, never untouchable.</summary>
            public const float IncomingFloor = 0.35f;
        }

        /// <summary>
        /// THE FOUR ELEMENTS, in one place for the game and the balance model alike.
        ///
        /// HEAD STARTS, NOT CEILINGS. An element's passives are stat POINTS that join the
        /// character's own (gear and the board) before the character curve bends them - see
        /// Art.Gear.StatCurves. An element reaches a threshold sooner than another, never higher; a
        /// penalty (Earth's slower swings) means needing more gear for the same cap, not a lower one.
        ///
        /// RELEASES SCALE WITH THE PLAYER. Release damage is in HIT UNITS - multiples of the player's
        /// own basic hit (PlayerController.HitUnit) - and then Elemental Power on top, so gear, the
        /// board and depth carry a release forward the way they carry a swing. Their radii grow with
        /// Area (StatKind.AoeRadius), like every other area attack.
        /// </summary>
        ///
        /// CALIBRATED 2026-10-05 by Balance.Assay.CalibrateElements (element parity: the worst
        /// build's spread across the four elements 42% -> 14%, every release worth 3-5 seconds of
        /// the player's own damage), inside ranges that keep each element itself - Earth heavy and
        /// clearly slower, Air light and fast. Re-run it after any change to gear, the board or the
        /// curves, and copy what it finds back here.
        public static class Elements
        {
            /// <summary>Fire: heat stacks, one banked per swing that lands, each fading on its own.</summary>
            public static class Fire
            {
                public const int MaxStacks = 5;
                public const float StackLifetime = 4f;

                /// <summary>Damage points per stack held - the ladder's steady head start.</summary>
                public const float StackDamagePoints = 7.5f;

                /// <summary>Rung 1, FUEL: a swing landing on a BURNING enemy banks this many stacks
                /// instead of one - Fire's mirror of Water's soaked enemies filling its meter twice.</summary>
                public const int FuelStacks = 2;

                /// <summary>Rung 2: attack speed points while at least two stacks are held.</summary>
                public const float HastePoints = 28f;

                /// <summary>Rung 3, STOKE: stacks fade this much slower while three or more are held.</summary>
                public const float StokeLifetimeMul = 2f;

                /// <summary>Rung 4: a landed hit splashes this share of itself onto one more nearby enemy.</summary>
                public const float SplashFraction = 0.5f;
                public const float SplashRadius = 2.2f;

                // ---- ERUPT, the first release: burning ground around the player ----
                public const int EruptPools = 7;
                public const float EruptSpread = 5.5f;
                public const float PoolRadius = 1.15f;
                public const float PoolSeconds = 5f;
                public const float PoolHitUnitsPerSecond = 7.7f;

                // ---- IGNITE, the second release: a window in which every landed hit burns ----
                public const float IgniteSeconds = 8f;
                /// <summary>Each hit inside the window burns for this share of itself per second,
                /// merged by MAX with the burn already on the target (StatusEffects.ApplyBurn).</summary>
                public const float IgniteBurnFraction = 0.35f;
                /// <summary>How long a burn lit inside the window keeps burning after its last refresh.</summary>
                public const float IgniteBurnSeconds = 2f;
            }

            /// <summary>Water: a tiered meter built by landing hits - soak, surge, burst.</summary>
            public static class Water
            {
                public const float DamagePoints = 0f;
                public const float GainPerHit = 0.09f;
                public const float DecayPerSecond = 0.02f;
                public const float SoakedGainMultiplier = 2f;
                public const float Radius = 3.6f;

                /// <summary>Attack speed points while a surge runs.</summary>
                public const float SurgePoints = 37.5f;
                public const float SurgeSecondsTier2 = 6f;
                public const float SurgeSecondsTier3 = 9f;

                public const float SoakSeconds = 4f;
                public const float SoakVulnerability = 1.35f;

                /// <summary>The tier-3 burst, SHARED between everything it catches (see
                /// BurstSplitFalloff) - one target takes it whole.</summary>
                public const float BurstHitUnits = 4f;
                public const float BurstSplitFalloff = 0.8f;
                public const float BurstKnockback = 6f;
            }

            /// <summary>Earth: a stillness meter - stand to charge, quake to spend.</summary>
            public static class Earth
            {
                public const float DamagePoints = 35f;
                public const float AttackSpeedPoints = -10f;
                public const float StillDamageReduction = 0.25f, MovingDamageReduction = 0.10f;
                public const float StillPoise = 0.9f, MovingPoise = 0.55f;

                public const float ChargeSeconds = 5f;
                public const float MovingDecaySeconds = 2f;
                public const float AftershockPerSeconds = 1.5f;
                public const float Radius = 4.2f;

                /// <summary>One shock: this plus PerCharge times the charge spent, in hit units.</summary>
                public const float ShockHitUnits = 2.2f;
                public const float ShockHitUnitsPerCharge = 4f;
                public const float ShockKnockback = 7f;
                public const float StaggerSeconds = 2.5f;
            }

            /// <summary>Air: momentum built by moving, crits by an unbroken streak.</summary>
            public static class Air
            {
                public const float DamagePoints = -50f;
                public const float AttackSpeedPoints = 0f;
                public const float AttackSpeedPointsPerMomentum = 60f;
                public const float MoveSpeedPointsPerMomentum = 35f;
                /// <summary>Crit damage points - Air's crits land at 2.0 where everyone else's
                /// start at 1.8.</summary>
                public const float CritDamagePoints = 17.5f;

                public const float BaseCrit = 0.15f;
                public const float CritPerStreak = 0.04f;
                public const int StreakCap = 8;

                public const float MomentumPerSecond = 0.20f;
                public const float MomentumDecay = 0.06f;
                public const float MomentumDecayStill = 0.20f;
                public const float MomentumPerCrit = 0.10f;

                // ---- GUST, the first release: a timed self-buff ----
                public const float GustSeconds = 6f;
                public const float GustRangeBonus = 1.3f;
                public const float GustMoveSpeedPoints = 50f;
                public const float GustLifesteal = 0.15f;
                /// <summary>Damage points while the gust runs - its damage payoff, scaled by
                /// Elemental Power like every release's.</summary>
                public const float GustDamagePoints = 35f;

                // ---- SQUALL, the second release: vortices scattered around the player ----
                public const int VortexCount = 5;
                public const float VortexSpread = 4.5f;
                public const float VortexRadius = 1.3f;
                public const float VortexPullForce = 4f;
                public const float VortexNearHitUnitsPerSecond = 4.6f;
                public const float VortexFarHitUnitsPerSecond = 0.9f;
                public const float VortexSeconds = 3.2f;
            }
        }

        /// <summary>
        /// Magnitudes for GearRoller's per-slot stat rolls. Diamond and Black Diamond roll nothing -
        /// both are cosmetic tiers (Black Diamond's power is its relic's finisher, not stats).
        /// </summary>
        public static class GearRoll
        {
            /// <summary>Guaranteed Armor points on every rollable piece, before tier scaling.</summary>
            public const float FloorArmorSilver = 4f;
            public const float FloorArmorGold = 8f;

            /// <summary>
            /// Guaranteed MaxHp points on every rollable ARMOUR piece, before tier scaling.
            ///
            /// Halved and more (3/6 to 1.25/2.5) on 2026-10-05: ten armour pieces on the Max player
            /// summed to 74 points, past Max HP's knee of 60 from the floor alone, so a piece rolled
            /// for health was mostly bent away. Now the floor gives the Max player about half the
            /// knee (31 points) and targeting health is what carries it the rest of the way.
            /// </summary>
            public const float FloorMaxHpSilver = 1.25f;
            public const float FloorMaxHpGold = 2.5f;

            /// <summary>The PRIMARY stat's fixed magnitude, before star scaling and the stat's own
            /// GearRoller.KindScale. A primary never rolls a range.</summary>
            public const float PoolStatSilver = 6f;
            public const float PoolStatGold = 12f;

            /// <summary>
            /// A WEAPON's primary against an armour piece's, for the same stat and tier. The weapon is
            /// the one slot every build fills and the only piece without the armour floor, so its
            /// primary carries more. Sub-stat ranges are the same on every slot.
            /// </summary>
            public const float WeaponPrimaryMul = 1.5f;

            /// <summary>Chance a Forge redemption rolls Gold instead of Silver. Placeholder, same
            /// spirit as XpPerPick - tune once there is real playtesting to tune it against.</summary>
            public const float ForgeGoldChance = 0.3f;

            /// <summary>
            /// What a Bronze piece is worth against the same Silver roll.
            ///
            /// Bronze is the floor tier the shallow floors pay, so it scales OFF Silver rather
            /// than carrying its own column - "a weaker Silver" is the whole idea, and it keeps
            /// GearRoller.TierScale's (silver, gold) shape intact at every call site.
            ///
            /// Deliberately NOT rollable from the Forge: RollTier stays Silver/Gold, because a
            /// voucher redemption paying out the floor tier would make spending one feel like a
            /// punishment. Bronze belongs to the floor drop table.
            /// </summary>
            public const float BronzeFraction = 0.5f;

            /// <summary>Gear vouchers banked per floor cleared, added at the run-end checkpoint
            /// alongside mastery XP - forfeit on a false start for the same reason.</summary>
            public const int VouchersPerFloor = 1;

            /// <summary>
            /// Chance a floor-clear drop hands over a loot box (of that floor's own banded tier,
            /// see RunLoot.TierFor) instead of a directly-rolled item. Boxes are the Forge's own
            /// currency - see the costs below - so this is what feeds re-rolling and targeted
            /// redemption, not just random redemption.
            /// </summary>
            public const float FloorBoxChance = 0.25f;

            /// <summary>
            /// Forge box costs, in boxes of the tier being spent. Combining and promoting cost NO
            /// boxes - two pieces are the price. Boxes buy randomness or remove it: a random
            /// redemption, a re-roll of one sub-stat's value (the item's own tier), a re-roll of
            /// which stat that sub-stat is, or a targeted redemption, which costs the most because
            /// choosing the exact slot (and weapon class) is the thing being paid for.
            /// </summary>
            public const int ForgeRandomRedeemBoxCost = 1;
            public const int ForgeRerollValueBoxCost = 1;
            public const int ForgeRerollStatBoxCost = 2;
            public const int ForgeTargetedRedeemBoxCost = 4;

            /// <summary>
            /// Star multipliers GearRoller.UpgradeScale applies to EVERY stat on a piece.
            /// Independent of LootTier (which stays pure RNG rarity). Keep ThreeStarScale below
            /// 1/BronzeFraction (and below Gold/Silver) so a promotion is never a downgrade.
            /// </summary>
            public const float OneStarScale = 1.15f;
            public const float TwoStarScale = 1.3f;
            public const float ThreeStarScale = 1.45f;

            /// <summary>
            /// A sub-stat's range as fractions of its tier's ARMOUR primary value. The top stays below
            /// 1 so a primary always beats a sub-stat of its own kind. A Gold piece can stack one stat
            /// four times (primary + three identical sub-stats) - GearRoller.KindScale sizes each
            /// stat so that piece, at three stars and mid-range rolls, is about half the stat's knee.
            /// Changing these re-values every stored roll: see GearRoller.TablesVersion.
            /// </summary>
            public const float SubStatMinFraction = 0.25f;
            public const float SubStatMaxFraction = 0.6f;
        }

        /// <summary>
        /// The chest's active defensive ability. All four share ParryWindowSeconds as an
        /// omnidirectional parry-eligible instant at the moment of activation; what follows it
        /// differs per ability - see PlayerController's own defensive-ability block.
        /// </summary>
        public static class Defense
        {
            /// <summary>
            /// Shared across all four. This is Parry Stance's ENTIRE active duration - it has no
            /// benefit beyond the check itself, which is the trade for its much shorter cooldown.
            /// </summary>
            public const float ParryWindowSeconds = 0.2f;

            /// <summary>
            /// Also the i-frame window (Health.Immune - see ActivateDefensiveAbility) and the
            /// duration Dash() locks out steering for. Cut from 1s to 0.25s: at DashSpeed below,
            /// the OLD pair (14 u/s over 1s) covered more than half the 24-unit-wide arena and felt
            /// glacial doing it once the distance itself was fixed to a sane 3.6 units at 1s. A
            /// quick burst reads as a dash; the same 3.6 units spent over a full second reads as a
            /// slow glide. Distance is what DashSpeed * DashSeconds must keep equal to - see below.
            /// </summary>
            public const float DashSeconds = 0.25f;
            public const float DashCooldown = 5f;

            /// <summary>
            /// World units per second, so DashSpeed * DashSeconds is the total distance travelled
            /// (damping is suspended for the duration - see PlayerController.Dash) - currently
            /// 14.4 * 0.25 = 3.6 units, about four of the character's own body-heights (~0.9 each).
            /// If DashSeconds is retuned, this must move with it to keep that distance - the speed
            /// alone is not the tuning knob, the pair is.
            /// </summary>
            public const float DashSpeed = 14.4f;

            public const float BarrierSeconds = 2f;
            public const float BarrierCooldown = 5f;

            /// <summary>
            /// Cosine of the half-angle a hit must fall within, relative to facing, to count as
            /// "in front" for Barrier's block. 0.5 = 60 degrees either side, a 120-degree cone.
            ///
            /// The dome is drawn from this same number (see BarrierDome) rather than from a
            /// second one that looks about right - a shell covering more of the circle than the
            /// block does is a picture telling the player they are safe where they are not.
            /// </summary>
            public const float BarrierFrontDot = 0.5f;

            /// <summary>
            /// Radius of Barrier's dome, world units. The character is about 0.9 tall, so this
            /// clears the head with room rather than sitting on the silhouette.
            /// </summary>
            public const float BarrierDomeRadius = 1.15f;

            /// <summary>
            /// Edge length of one hex panel measured ON THE SHELL, in the dome's own angular
            /// units (radians of arc from the zenith) - not screen units, which is why the cells
            /// visibly foreshorten toward the rim without anything drawing them smaller there.
            /// </summary>
            public const float BarrierHexCell = 0.30f;

            /// <summary>Electric blue. Alpha is the lattice's peak; the panel interiors sit well under it.</summary>
            public static readonly UnityEngine.Color BarrierColor = new(0.30f, 0.72f, 1f, 0.90f);

            /// <summary>
            /// How much of the lattice still draws OUTSIDE the covered arc. Not zero: the dome is
            /// a shape, and a 120-degree crescent floating beside the player reads as a stray arc
            /// rather than as a shell with an open back. Faint enough that nobody mistakes the
            /// ghosted half for protection.
            /// </summary>
            public const float BarrierGhostAlpha = 0.14f;

            public const float BulwarkSeconds = 1f;
            public const float BulwarkCooldown = 5f;

            /// <summary>Flat multiplier while Bulwark is active - a straight reduction, not run through the points system, since this is a timed activation rather than a permanent gear stat.</summary>
            public const float BulwarkDamageMultiplier = 0.5f;

            public const float ParryStanceCooldown = 2.5f;

            // ---- the shared window's tell (see Player/GuardRing.cs) ----
            //
            // One ring, drawn for ALL FOUR abilities, because the window genuinely is the same
            // for all four. What each ability adds on top of it is what differs - and Parry
            // Stance adds nothing, which is not an oversight: it HAS nothing beyond the window,
            // and drawing it a passive it does not own would be the lie.

            /// <summary>Where the ring starts. Outside BarrierDomeRadius, so the two never sit on each other.</summary>
            public const float GuardRingStartRadius = 1.70f;

            /// <summary>
            /// Where it ends - the body, not zero. The ring arriving ON the character is what
            /// says the window shut; collapsing to a point would spend its last and most
            /// important frames as an unreadable dot.
            /// </summary>
            public const float GuardRingEndRadius = 0.32f;

            /// <summary>
            /// Alpha at full radius and at the body. It BRIGHTENS as it closes, which is both the
            /// right urgency and a necessary correction: the ring sprite's band is a fraction of
            /// its own radius, so shrinking it 5x thins it 5x, and a tell that fades out as it
            /// tightens would be faintest at the only instant worth reading.
            /// </summary>
            public const float GuardRingWideAlpha = 0.40f;
            public const float GuardRingTightAlpha = 1f;

            /// <summary>
            /// Silver. The window belongs to no one ability, so it carries no ability's colour -
            /// Barrier's blue, Dash's cyan and Bulwark's amber all read against this rather than
            /// competing with it.
            /// </summary>
            public static readonly UnityEngine.Color GuardRingColor = new(0.88f, 0.93f, 1f, 1f);

            // ---- Dash's after-images (see Player/DashTrail.cs) ----

            /// <summary>
            /// World units between dropped figures, measured along the distance ACTUALLY
            /// travelled rather than off a timer - a dash cut short by a wall leaves a short
            /// trail, which is the true picture. DashSpeed * DashSeconds is 3.6 units, so this
            /// spends three figures on a clean dash.
            /// </summary>
            public const float DashTrailSpacing = 1.15f;

            /// <summary>
            /// How long one figure lasts. Tied to DashSeconds deliberately: the last figure
            /// fading is the instant the i-frames end, so the trail's existence IS the immunity
            /// window rather than a decoration that outlives it.
            /// </summary>
            public const float DashTrailFadeSeconds = DashSeconds;

            /// <summary>Ceiling on figures built for the trail. Each is a whole rig - see ShadowEcho.</summary>
            public const int DashTrailMaxFigures = 4;

            /// <summary>Pale cyan: speed, and no mass. Applied to a SILHOUETTE, so it lands flat.</summary>
            public static readonly UnityEngine.Color DashTrailColor = new(0.62f, 0.93f, 1f, 0.55f);

            // ---- Bulwark's aura (see Player/BulwarkAura.cs) ----

            /// <summary>
            /// How far each silhouette swells past the character's own outline. Small: this is
            /// padding worn on the body, and a large swell stops reading as the character's own
            /// shape and starts reading as a second figure standing behind them.
            ///
            /// TWO layers, not one, and the pair is the whole effect. A single swelled copy comes
            /// out as a crisp gilded rim one or two texels thick - legible, but an EDGE, and an
            /// edge is what Bulwark specifically must not have: the ability softens damage rather
            /// than stopping it, and a hard closed outline draws a seal around a character that
            /// is still being hit through it. A wider, fainter copy under a tighter, stronger one
            /// gives the falloff that reads as padding instead.
            /// </summary>
            public const float BulwarkAuraSwell = 1.10f;
            public const float BulwarkAuraOuterSwell = 1.24f;

            /// <summary>The outer layer's share of the inner one's alpha at every moment, flare included.</summary>
            public const float BulwarkAuraOuterFraction = 0.45f;

            /// <summary>Warm brass - the far end from Barrier's electric blue, because the two abilities are opposites and must never be confused for one another.</summary>
            public static readonly UnityEngine.Color BulwarkAuraColor = new(1f, 0.74f, 0.32f, 1f);

            /// <summary>Resting alpha, and the peak of its breath.</summary>
            public const float BulwarkAuraAlpha = 0.34f;
            public const float BulwarkAuraBreath = 0.10f;

            /// <summary>
            /// What a hit landing THROUGH Bulwark flares the aura to, and how fast that decays.
            ///
            /// It flares and RECOVERS - it never depletes. Bulwark is a flat multiplier for its
            /// whole second, not a pool being spent, and an aura that thinned with each hit would
            /// be drawing a resource the ability does not have.
            /// </summary>
            public const float BulwarkAuraFlareAlpha = 0.85f;
            public const float BulwarkAuraFlareDecay = 4.5f;

            // ---- the parry's answer ----

            /// <summary>
            /// How hard the camera is shoved away from a parried attacker. Well under a hit's own
            /// kick: the parry already owns the frame through Hitstop, and two loud answers to
            /// the same instant read as one muddy one.
            /// </summary>
            public const float ParryCameraKick = 0.10f;

            /// <summary>
            /// The ring thrown OUTWARD on a successful parry. It expands where GuardRing
            /// contracts, and that opposition is the whole point: closing means it is coming,
            /// opening means it landed. Fast and brief - an answer, not a state.
            /// </summary>
            public const float ParryBurstRadius = 1.25f;
            public const float ParryBurstSeconds = 0.14f;

            /// <summary>The counter-strike's damage and knockback, whichever ability triggered it.</summary>
            public const float CounterDamage = 20f;
            public const float CounterKnockback = 8f;
        }

        /// <summary>
        /// Floating combat text for damage the PLAYER deals - see Combat/DamageNumbers.cs. Only
        /// player-driven damage spawns a number; hits the player takes, and enemy-on-enemy
        /// damage, stay silent, since this is feedback for the player's own offense.
        /// </summary>
        public static class DamageNumbers
        {
            /// <summary>Seconds a number is on screen before it is destroyed.</summary>
            public const float LifeSeconds = 0.7f;

            /// <summary>How far it rises, in world units, over its whole life.</summary>
            public const float RiseDistance = 0.9f;

            /// <summary>Horizontal scatter so two hits landing the same frame don't overlap exactly.</summary>
            public const float Jitter = 0.25f;

            public const int FontSize = 30;
            public const int FinisherFontSize = 42;

            /// <summary>A crit's own font size, on top of whichever of the two above it would
            /// otherwise have used - see CritSizeMul.</summary>
            public const float CritSizeMul = 1.35f;

            /// <summary>Seconds a crit spends easing down from its oversized spawn scale back to
            /// 1x - the "pop" that tells a crit apart from a same-sized number that's merely big.</summary>
            public const float CritPopSeconds = 0.15f;

            /// <summary>Scale a crit number spawns at, before easing down to 1x over CritPopSeconds.</summary>
            public const float CritPopScale = 1.6f;

            public static readonly UnityEngine.Color Color = new UnityEngine.Color(1f, 0.92f, 0.6f);
            public static readonly UnityEngine.Color FinisherColor = new UnityEngine.Color(1f, 0.55f, 0.15f);
        }

        /// <summary>
        /// A brief freeze on landing a hit, giving weight to an impact - see Combat/Hitstop.cs.
        /// </summary>
        public static class Hitstop
        {
            /// <summary>A finisher connecting, non-crit - Light through Heavy. Basics carry no
            /// hitstop of their own; landing every basic in a chain was stuttering the game.</summary>
            public const float FinisherSeconds = 0.1f;

            /// <summary>A finisher connecting AND critting, or a successful parry.</summary>
            public const float CritFinisherSeconds = 0.2f;

            /// <summary>A successful parry, whichever defensive ability triggered it.</summary>
            public const float ParrySeconds = 0.2f;
        }

        /// <summary>
        /// The camera's shove when the player lands a finisher. Gated on exactly what
        /// <see cref="Hitstop"/> is gated on, so the two are one beat rather than two: basics
        /// never kick (landing every basic in a chain would be a permanently unsteady camera, the
        /// same reason they carry no freeze), and the katana sequence's pre-cuts are suppressed
        /// together with their freeze so the move produces ONE kick rather than three.
        /// </summary>
        public static class CameraKick
        {
            /// <summary>How far the camera is shoved, world units. Against a 4.8 half-height view
            /// this is about 2% of the screen - felt rather than seen.</summary>
            public const float Distance = 0.10f;

            /// <summary>The same on a crit, matching Hitstop's own crit/non-crit split.</summary>
            public const float CritDistance = 0.17f;

            public const float Stiffness = 220f;
            public const float Damping = 16f;
        }

        /// <summary>
        /// The wave BUDGET - see Enemies.WaveComposer for the reasoning, and
        /// <c>WaveComposer.Simulate</c> for what these numbers produce.
        ///
        /// A floor is a pool of effective HP (health plus armor) priced against the player
        /// EXPECTED to be standing on it, and its enemies are bought from that pool at their real
        /// effective HP. Which enemies is random; how much killing the floor takes is not.
        /// </summary>
        public static class Waves
        {
            // ---- the length of a floor ----

            /// <summary>
            /// Seconds of DAMAGE a floor should take the expected player, at these floors, linear
            /// between them. Pure damage time - walking in, chasing and dodging come on top.
            ///
            /// Early floors are measured against the reference player (an unbuffed greatsword
            /// with the average ledger); deep floors against a BUILT one (see BuiltByFloor),
            /// because nobody else gets there. A built player on average boons takes about 1.8x
            /// these figures deep down; that gap is what stacking boons buys.
            /// </summary>
            public static readonly int[] TargetFloors = { 1, 10, 20, 40, 50, 75, 100 };
            public static readonly float[] TargetSeconds = { 16f, 30f, 30f, 35f, 40f, 60f, 75f };

            /// <summary>
            /// The expected player turns from the reference player into the BUILT one across these
            /// floors: a full board, three Gold three-star pieces plus a Gold three-star weapon,
            /// and boons stacked for damage (Player.PlayerPower). Survivorship, not optimism - a
            /// player without a build does not reach floor 40 to be measured.
            /// </summary>
            public const int BuiltFromFloor = 10;
            public const int BuiltByFloor = 40;

            /// <summary>
            /// The cheapest anything may be, as a fraction of a basic Chaser on the same floor.
            /// Without it a Bubbles (65 HP, never armoured) costs 0.09 Chasers at floor 100, a room
            /// fills with them, the body cap stops the buying early, and the floor comes up short
            /// by a third - the one thing the budget exists to prevent. Support kinds are not
            /// worth their HP anyway: a Bubbles grants immunity, a Booster buffs.
            /// </summary>
            public const float MinCostFraction = 0.4f;

            /// <summary>Most bodies one floor may field in total, whatever the pool says.</summary>
            public const int MaxBodies = 30;

            // ---- what is on screen at once ----

            /// <summary>
            /// The on-screen cap is PRESSURE, not bodies: how much is threatening the player per
            /// second. A swarm of cheap kinds fits a dozen at once; elites arrive two or three at
            /// a time. Early floors match the old body cap (3 + floor/2).
            /// </summary>
            public const float LivePressureBase = 3f;
            public const float LivePressurePerFloor = 0.5f;
            public const float LivePressureMax = 9f;

            /// <summary>Hard ceiling on live bodies, for readability and frame time.</summary>
            public const int MaxLiveBodies = 16;

            /// <summary>An elite's pressure as a multiple of its kind's.</summary>
            public const float ElitePressureMul = 2.5f;

            /// <summary>
            /// Each kind's PRESSURE: how much it threatens per second while alive, in Chasers.
            /// Hand-set, unlike cost - this is the one judgement the formula cannot make, and the
            /// place to tune by feel. HP says how LONG a kind takes; this says how DANGEROUS it is.
            /// </summary>
            public const float PressureChaser = 1.0f;
            public const float PressureBomb = 0.8f;
            public const float PressureRanged = 1.2f;
            public const float PressureTurret = 1.2f;
            public const float PressureDasher = 1.2f;
            public const float PressureGargoyle = 1.0f;
            public const float PressureBooster = 0.8f;
            public const float PressureBubbles = 0.6f;
            public const float PressureMortar = 1.5f;

            // ---- when each kind first appears ----
            //
            // Moved here from GameBootstrap's serialized fields (same values) so the composer can
            // read them without a scene. Mortar and Bubbles keep their own in Tuning.Enemy.

            public const int BombFromFloor = 2;
            public const int RangedFromFloor = 2;
            public const int DasherFromFloor = 3;
            public const int TurretFromFloor = 4;
            public const int GargoyleFromFloor = 5;

            // ---- what the room is made of ----

            /// <summary>
            /// Each floor scales every eligible kind's weight by exp(N(0, sigma)), so one room
            /// leans Mortar, the next is almost all Chasers - never the same recipe twice and
            /// never a template. 0.7 gives the heaviest kind about half the room at the 90th
            /// percentile.
            /// </summary>
            public const float KindWeightSigma = 0.7f;

            /// <summary>
            /// A kind DEBUTS as exactly one body on its unlock floor, then its weight ramps from
            /// DebutWeight to full over DebutRampFloors. Without it floor 2 can be eight Ranged on
            /// the floor the player meets Ranged for the first time.
            /// </summary>
            public const float DebutWeight = 0.3f;
            public const int DebutRampFloors = 8;

            /// <summary>
            /// The floor draws its own elite share, uniformly from zero up to this cap, which
            /// rises with depth. Elites are what depth escalates once the pool stops growing, and
            /// what an area-damage build cannot shortcut.
            /// </summary>
            public const float EliteShareBase = 0.15f;
            public const float EliteSharePerFloor = 0.005f;
            public const float EliteShareMax = 0.40f;

            /// <summary>At most this many elites through floor 5, and 2 through floor 15 - the
            /// introduction stays an introduction.</summary>
            public const int EliteCapEarly = 1;
            public const int EliteCapEarlyThrough = 5;
            public const int EliteCapMid = 2;
            public const int EliteCapMidThrough = 15;
        }
    }
}
