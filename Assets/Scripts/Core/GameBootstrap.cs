using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Chain;
using Convergence.Combat;
using Convergence.Enemies;
using Convergence.Hazards;
using Convergence.Player;
using Convergence.UI;

namespace Convergence.Core
{
    /// <summary>
    /// The entire scene is this one component. Everything else - arena, player, enemies, UI -
    /// is constructed at runtime, so Arena.unity can be regenerated from the Unity CLI without
    /// carrying prefab or asset references.
    ///
    /// Scope note: this is the combat-only prototype. Floors, the death economy (bank out /
    /// false start / continue), checkpoint boxes and gear are designed in the-seed docs but
    /// deliberately not implemented here - the goal is proving the four elements feel distinct.
    /// The IProfileStore boundary is real though, and is written at run start and run end only.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        enum State { Hub, Playing, RunOver }

        [Header("Arena")]
        // Half-extents rather than a radius: the arena is framed to a 16:9 viewport, so a
        // square room would either crop top/bottom or leave dead bands at the sides.
        // Framing is set by gear readability, not by how much arena fits on screen. At the old
        // size (camera 10.2) a ~1-unit character was 5% of screen height - about 4px per gear
        // slot on a 1080p display, which made the cosmetic tiers invisible. At 4.2 the character
        // is ~12% of height, comparable to Hades or Enter the Gungeon. The arena stays larger
        // than the view so the camera still pans.
        // Central values: Tuning.Arena / Tuning.Camera.
        public float HalfWidth = Tuning.Arena.HalfWidth;
        public float HalfHeight = Tuning.Arena.HalfHeight;
        [Tooltip("Half the visible height in world units. Trades the two things that pull against " +
                 "each other here: wider shows more of the fight, tighter keeps gear legible. " +
                 "4.2 = 15% of screen height (the pixel-art reference's framing), 4.8 = 12%, " +
                 "5.6 = 10.5%.")]
        public float CameraSize = Tuning.Camera.Size;

        /// <summary>DEV: use the phone's closer arena zoom on any device - to see it in the Editor.</summary>
        public static bool DevPhoneZoom;

        /// <summary>
        /// The ARENA's zoom: <see cref="CameraSize"/>, brought in by Tuning.Camera.PhoneZoom on a
        /// phone. A phone's short side is small enough that the character read at a fraction of
        /// the size it does on a monitor (11.7% of the short side, against ~17% in the clear pixel
        /// game the user compared it with). The hub keeps its own framing; PixelPerfectZoom still
        /// snaps the result to a whole number of screen pixels per texel.
        /// </summary>
        float ArenaViewSize
            => CameraSize / (Application.isMobilePlatform || DevPhoneZoom ? Tuning.Camera.PhoneZoom : 1f);

        [Tooltip("How tightly the camera holds the player. Higher is more locked.")]
        public float CameraFollow = Tuning.Camera.Follow;

        [Tooltip("Ground drawn beyond the walls. The camera keeps the player centred even at the " +
                 "arena edge, so there has to be floor out there or it pans onto empty space.")]
        public float FloorMargin = Tuning.Arena.FloorMargin;

        /// <summary>Blows this floor's PLATING still absorbs before armour wears (Tuning.Defense.ArmorPerPlate) -
        /// refilled at every floor's start, a replayed floor included. Serialized: a domain reload
        /// must not refill it mid-floor.</summary>
        [SerializeField] float _plating;
        public float PlatingLeft => _plating;
        public float PlatingPerFloor => _player != null
            ? Mathf.Max(0f, _player.Stats.Armor / Tuning.Defense.ArmorPerPlate - Modifiers.Current.PlatingLost) : 0f;   // Rust

        [Header("Durability")]
        [Tooltip("Durability each armour piece loses per hit taken, for a hit the size of the floor's Chaser swing - scaled by how hard the hit was (Tuning.Defense.WearReferenceHit).")]
        public float ArmorWearPerHit = 1.4f;
        [Tooltip("UNUSED while weapon degradation is off - nothing wears weapons and nothing " +
                 "reads their condition. Kept for the sharpness mechanic this may become.")]
        public float WeaponWearPerHit = 0.22f;
        [Tooltip("Fraction of max durability the repair reward restores.")]
        public float RepairFraction = 0.45f;
        [Tooltip("Fraction of max health the heal reward restores.")]
        public float HealFraction = 0.3f;

        [Header("Mastery")]
        [Tooltip("Element mastery XP earned for each floor cleared. Accrues during the run and " +
                 "is banked at the run-end checkpoint - a false start forfeits it.")]
        public int MasteryXpPerFloor = 45;

        [Header("Experience")]
        [Tooltip("XP each kill is worth.")]
        public int XpPerKill = 10;

        [Header("Floors")]
        [Tooltip("Every Nth floor guarantees one elite, paid for out of the wave's pool. When each " +
                 "kind first appears is Tuning.Waves' - see Enemies.WaveComposer.")]
        public int EliteEveryNFloors = 3;

        State _state = State.Hub;

        /// <summary>
        /// The stores are INTERFACE-typed, which Unity cannot serialize - so a domain reload
        /// (editing a script while playing) drops them to null while _profile, a plain
        /// [Serializable] class, is restored intact. The bootstrap then sat permanently
        /// half-initialised: every StartRun was rejected by its own "before init finished" guard
        /// and the only symptom was a warning that pointed at start-up rather than at a reload.
        ///
        /// Both implementations are stateless, so re-creating one costs nothing and is always
        /// safe. Accessed through the properties below, never the fields.
        /// </summary>
        IProfileStore _storeField;
        IProfileStore _store => _storeField ??= StoreFactory.Profiles();

        CharacterProfile _profile;

        /// <summary>
        /// The player, as opposed to the character they are currently playing. Owns the room.
        /// Loaded once at start-up and NOT reloaded when characters are switched - that is the
        /// whole point of it being account-level.
        /// </summary>
        IAccountStore _accountsField;
        IAccountStore _accounts => _accountsField ??= StoreFactory.Accounts();

        AccountProfile _account;
        string _runId;
        float _runStartTime;

        /// <summary>Floor 100's box, asked of the store (ClaimFinalFloor) and not yet credited -
        /// EndRun settles one still outstanding before the run's loot is banked.</summary>
        System.Threading.Tasks.Task<FinalFloorReward?> _finalAsk;

        /// <summary>The RUN CLOCK and its heat (Core.RunClock) - a component on this object.</summary>
        Core.RunClock Clock
        {
            get
            {
                if (_clockComponent != null) return _clockComponent;
                _clockComponent = GetComponent<Core.RunClock>();
                if (_clockComponent == null) _clockComponent = gameObject.AddComponent<Core.RunClock>();
                return _clockComponent;
            }
        }
        Core.RunClock _clockComponent;

        /// <summary>The week whose guaranteed floor-100 box this run took, or 0 - rides the run
        /// summary to the profile (ProfileRules.RunEnd).</summary>
        int _runGuaranteeWeek;

        /// <summary>An avatar's boxes, asked of the store (ClaimAvatarBoxes) and not yet credited,
        /// and the floor they are for - EndRun settles one still outstanding, as floor 100's.</summary>
        System.Threading.Tasks.Task<AvatarBoxes?> _avatarAsk;
        int _avatarAskFloor;

        Transform _arenaRoot, _enemyRoot, _hazardRoot;
        PlayerController _player;
        StuckWatch _stuckWatch;
        Hud _hud;
        Canvas _canvas;
        Canvas _touchCanvas;
        Camera _cam;
        UI.ScreenFade _fade;

        GameObject _overScreen;
        int _overScreenFrame = -1;

        /// <summary>
        /// The main menu, as a walkable room. Null while a run is live - it is torn down on
        /// entering a door and rebuilt on returning, so nothing from the menu can outlive it.
        /// </summary>
        Hub.HubRoom _hub;

        /// <summary>
        /// Every saved character EXCEPT the one being played, refreshed whenever the hub is
        /// built. Held rather than re-read on demand because the couch renders a full paper-doll
        /// per seat and must not be doing file IO to decide what to draw.
        /// </summary>
        List<CharacterProfile> _roster = new();   // not readonly - see the domain-reload note
        public UI.CharacterScreen CharacterScreen { get; private set; }
        public UI.FloorRewardScreen FloorRewardScreen { get; private set; }
        public UI.MasteryScreen MasteryScreen { get; private set; }
        public UI.TransmutationScreen TransmutationScreen { get; private set; }
        public UI.ShowcasePicker ShowcasePicker { get; private set; }
        public UI.ForgeScreen ForgeScreen { get; private set; }
        public UI.ManualScreen ManualScreen { get; private set; }
        public UI.GearDisplayScreen GearDisplayScreen { get; private set; }
        public UI.RiftScreen RiftScreen { get; private set; }
        public UI.StakeScreen StakeScreen { get; private set; }
        public UI.PauseScreen PauseScreen { get; private set; }
        public UI.SettingsScreen SettingsScreen { get; private set; }
        public UI.ControlsScreen ControlsScreen { get; private set; }
        public UI.InventoryScreen InventoryScreen { get; private set; }
        public UI.ExchangeScreen ExchangeScreen { get; private set; }
        public UI.TransmuteScreen TransmuteScreen { get; private set; }
        public UI.TxScreen TxScreen { get; private set; }
        public UI.ConfirmDialog ConfirmDialog { get; private set; }
        /// <summary>
        /// The four element boards. Element-scoped: every Get asks about the board for the
        /// element being played, because a board is only active for its own element.
        /// </summary>
        public Progression.BoardState Mastery { get; private set; }
        readonly ElementType[] _order = { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air };

        int _floor;
        // Not readonly: a domain reload cannot restore a readonly field, and this one emptying
        // mid-run would read as "floor cleared" and hand out a reward for an unfinished floor.
        List<EnemyController> _alive = new();

        /// <summary>
        /// A floor's enemies still waiting for room - the on-screen cap is pressure, see
        /// NextSpawnFits. Rebuilt fresh every floor and drained by both the
        /// initial trickle-in and HookDeath's Died handler, so a domain reload emptying it mid-
        /// floor is the same "wave rebuilds anyway" story _alive already tells; non-readonly for
        /// the same reason.
        /// </summary>
        Queue<PendingSpawn> _spawnQueue = new();

        /// <summary>One enemy still queued for the current floor: which kind, whether it rolled
        /// elite, and where to place it (evaluated at actual spawn time, not when queued, so a
        /// late trickle-in still reads the player's CURRENT position).</summary>
        readonly struct PendingSpawn
        {
            public readonly EnemyKind Kind;
            public readonly bool Elite;
            public readonly Func<Vector2> Position;
            public PendingSpawn(EnemyKind kind, bool elite, Func<Vector2> position)
            {
                Kind = kind;
                Elite = elite;
                Position = position;
            }
        }

        /// <summary>
        /// The boss holding the current floor, or null. A plain component reference, so it
        /// survives a domain reload the way an interface field would not.
        ///
        /// It is deliberately NOT in _alive: that list is EnemyController and the floor-clear
        /// check counts it, and a boss is neither an enemy archetype nor something a wave
        /// spawner should be able to add to. The floor asks about both separately.
        /// </summary>
        Bosses.Boss _boss;

        /// <summary>What this run is carrying, and what it has already got out. Rebuilt per run;
        /// nothing in it reaches the profile until a Rift or floor 100 banks it.</summary>
        readonly Rifts.RunLoot _loot = new();

        /// <summary>The Rift standing open on this floor, or null. Like the boss it is not in
        /// _alive and holds the floor on its own.</summary>
        Rifts.Rift _rift;

        /// <summary>
        /// A Red Rift's guard once the player has broken its seal, and whether they have. Kept
        /// apart from _alive: a guard called AFTER the floor clears must not hold the floor, and
        /// the Rift opens on these bodies alone. Non-readonly and null-guarded - a domain reload
        /// can hand a collection back empty (see CLAUDE.md, Domain reload traps).
        /// </summary>
        List<Health> _redGuard = new();
        bool _redSummoned;
        bool _spawning;
        bool _rewardTaken;

        /// <summary>What each floor is - see Rifts.FloorPlanner. One per run, seeded at run start.</summary>
        Rifts.FloorPlanner _planner;
        Rifts.FloorPlan _plan;

        /// <summary>When this floor's first wave arrived (scaled time), or -1 - the clear is timed
        /// from here for the Collapsing Rift's clock.</summary>
        float _fightStart = -1f;

        /// <summary>Whether this run has cleared its stake's gate floor. Run state, reset at run
        /// start; the stake itself lives on the profile.</summary>
        bool _stakeGateCleared;

        /// <summary>What the stake came to, for the run-over screen. Null with no stake.</summary>
        string _stakeLine;

        /// <summary>
        /// A floor transition is in flight - from the moment the door is reached until the new
        /// floor is built and the screen is back.
        ///
        /// This exists because _activeDoor and _spawning BOTH go quiet in the middle of a
        /// transition and neither covers the gap between them. The door's callback clears
        /// _activeDoor before it starts FloorTransition, and _spawning is not set until
        /// NextFloor runs, which is on the far side of a 0.45s fade. For those frames the
        /// floor-clear branch in Update saw an empty room with no door in it and started a
        /// SECOND NextFloor of its own - so the floor advanced twice, floor 2 was skipped
        /// outright, and two waves spawned into one room.
        ///
        /// It was invisible until the black-screen deadlock above it was fixed: both coroutines
        /// simply hung, so the skip never got as far as being drawn. Worth knowing that one bug
        /// was sitting on top of the other, if a third ever turns up here.
        /// </summary>
        bool _transitioning;
        System.Action<Combat.Health> _onPlayerDied;

        /// <summary>
        /// Element mastery earned this run, unbanked. Held here rather than written straight to
        /// the profile so a false start can throw it away - giving up current progression for
        /// another attempt to go deeper is the whole point of that mechanic.
        /// </summary>
        int _pendingMastery;

        /// <summary>
        /// Calx taken from floor reward cards this run - CARRIED, like unsecured loot: banked by
        /// EndRun on an extraction, dropped on a death.
        /// </summary>
        int _runCalx;

        /// <summary>Calx the cards have paid this run, against the depth allowance
        /// (CalxAllowanceLeft). Separate from _runCalx so nothing that ever spends or clears
        /// carried calx re-opens it.</summary>
        int _runCalxTaken;

        /// <summary>One calx per Tuning.FloorRewards.FloorsPerCalx floors cleared, less what the
        /// cards already paid. _floor is the floor whose reward is being offered - cleared.</summary>
        int CalxAllowanceLeft()
            => Mathf.Max(0, _floor / Tuning.FloorRewards.FloorsPerCalx - _runCalxTaken);

        /// <summary>
        /// Bumped by every teardown. Async continuations and coroutines capture it and bail if it
        /// has moved on - a run that has already ended must never mutate the one that replaced it.
        /// </summary>
        int _runToken;

        /// <summary>
        /// The run's equivalent-exchange ledger. Run-scoped: these are what you agreed to on the way
        /// down, and they die with the run. Written into the run's snapshot (continuation, below)
        /// so a run carried on in another session keeps them - never into the character.
        /// </summary>
        public Exchange.RunModifiers Modifiers { get; } = new();

        // ------------------------------------------------------------------ continuation (fields)
        // A run survives a closed tab and can be left to come back to - see the "continuation"
        // section further down, and CONTINUATION.md.

        /// <summary>The run in flight's store. Interface-typed, so a lazy property for the reason
        /// _store is one (a domain reload nulls the field).</summary>
        IRunStore _runsField;
        IRunStore _runs => _runsField ??= StoreFactory.Runs();

        /// <summary>Sends the live run's writes in order. Rebuilt after a domain reload.</summary>
        RunKeeper _keeperField;
        RunKeeper _keeper => _keeperField ??= NewKeeper();

        /// <summary>The session playing this run - every run write names it (one live session per run).</summary>
        string _runSession;

        /// <summary>The live run's latest snapshot - what a resume would rebuild. Null outside a run,
        /// and from the moment a run starts ending.</summary>
        RunSnapshot _runSave;

        /// <summary>In the hub: the run this character left, waiting to be resumed or ended. Null when
        /// none waits.</summary>
        SuspendedRun _waiting;

        /// <summary>The run store could not be asked about this character - not the same as no run.
        /// A stake is not forfeited on it, and nothing is offered at the gate.</summary>
        bool _waitingUnknown;

        /// <summary>Bodies spawned from this floor's wave - each one's RollIndex, the stream its drops
        /// are rolled from (Rifts.RunRolls). A kill outside the numbered ones falls back on
        /// _floorKills.</summary>
        int _floorSpawns, _floorKills;

        /// <summary>A Red Rift's guard is numbered from here, clear of the wave - summoned whenever
        /// the player chooses, it must not shift the wave's numbers.</summary>
        const int RedGuardRollBase = 10000, LooseRollBase = 20000;

        /// <summary>The boss's health a resume carries into its floor, in eighths down
        /// (RunSnapshot.BossEighthsDown): read from the waiting run, recorded by the floor-start
        /// snapshot, then handed to the boss as it spawns (_bossStartDown).</summary>
        int _resumeBossDown, _bossStartDown;

        /// <summary>The boss's eighths the last heartbeat told the store.</summary>
        int _beatBossDown;

        /// <summary>A clock left running (the Labyrinth's, a Collapsing Rift's) - seconds spent, a
        /// Collapsing Rift already run out, the Labyrinth's position: read from the waiting run,
        /// recorded by the floor-start snapshot, then handed to this floor (_floor*).</summary>
        float _resumeClockSpent, _floorClockSpent;
        bool _resumeRiftCollapsed, _floorRiftCollapsed, _resumePlaced, _floorPlaced;
        Vector2 _resumePos, _floorPos;

        /// <summary>The running clock's full length, and the reorientation seconds a resume granted
        /// on it - added back when the time spent is reported, so it is granted once, not per quit.</summary>
        float _clockFull, _clockCredit;

        /// <summary>Curator rerolls spent on the reward now showing.</summary>
        int _rewardRerolls;

        /// <summary>This floor's puzzle was given up or failed (its adjacent room is the floor now);
        /// <see cref="_resumeGivenUp"/> is the same answer read from a snapshot, for NextFloor.</summary>
        bool _puzzleGivenUp, _resumeGivenUp;

        /// <summary>The captured spire's boon riding the ledger (Hazards.SpireBoon as an int), or -1 -
        /// SetFloorBoon takes a delegate, which a snapshot cannot hold.</summary>
        int _floorBoon = -1;

        /// <summary>The profile's boxes as the run's banked pools began - what RunLoot.Restore rebases
        /// a resumed run's pools against.</summary>
        int _runStartRiftBoxes;
        LootBoxes _runStartBoxes = new();

        /// <summary>What the last heartbeat (or snapshot) said, and when - a beat goes only when
        /// these have moved, at most every Tuning.Continuation.BeatSeconds.</summary>
        float _beatHealth = -1f, _beatWear = -1f, _beatAt;

        async void Start()
        {
            Application.targetFrameRate = 120;

            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                _cam = camGo.GetComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = CameraSize;

            // Holds the zoom on a whole number of screen pixels per art texel. Added here rather
            // than in the scene so a rebuilt Arena.unity cannot lose it; see PixelPerfectZoom for
            // why URP's PixelPerfectCamera cannot express a 37.5 ppu project.
            var snap = _cam.GetComponent<PixelPerfectZoom>() ?? _cam.gameObject.AddComponent<PixelPerfectZoom>();
            snap.TargetSize = CameraSize;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.backgroundColor = new Color(0.055f, 0.06f, 0.078f);
            _cam.clearFlags = CameraClearFlags.SolidColor;

            Physics2D.gravity = Vector2.zero;

            // Every wave is priced against simulated ledger curves (Player.PlayerPower), built on
            // first use - about half a second. Built here, under the load, rather than as the first
            // floor's enemies are due.
            Enemies.WaveComposer.Pool(1);

            _canvas = UiKit.CreateCanvas("UI", 10);
            _canvas.transform.SetParent(transform, false);

            // DEV: the format the toggle last chose (Tuning.Testing.FormatToggle).
            if (DevFormatEnabled && PlayerPrefs.GetInt(DevFormatPref, 0) == 1) RunFormat.Current = RunFormatId.ShortDescent;

            _fade = UI.ScreenFade.Build(transform);

            _stuckWatch = gameObject.AddComponent<StuckWatch>();
            CharacterScreen = gameObject.AddComponent<UI.CharacterScreen>();
            CharacterScreen.Init(() => _profile, Equip, ToggleHelmHidden,
                                 () => _state == State.Playing ? Modifiers : null,
                                 () => _player, () => ReorderOpen);
            FloorRewardScreen = gameObject.AddComponent<UI.FloorRewardScreen>();
            MasteryScreen = gameObject.AddComponent<UI.MasteryScreen>();
            TransmutationScreen = gameObject.AddComponent<UI.TransmutationScreen>();
            TransmutationScreen.Init(() => _profile, Equip, ToggleHelmHidden, SaveLook);
            ShowcasePicker = gameObject.AddComponent<UI.ShowcasePicker>();
            ForgeScreen = gameObject.AddComponent<UI.ForgeScreen>();
            ManualScreen = gameObject.AddComponent<UI.ManualScreen>();
            GearDisplayScreen = gameObject.AddComponent<UI.GearDisplayScreen>();
            RiftScreen = gameObject.AddComponent<UI.RiftScreen>();
            StakeScreen = gameObject.AddComponent<UI.StakeScreen>();
            PauseScreen = gameObject.AddComponent<UI.PauseScreen>();
            SettingsScreen = gameObject.AddComponent<UI.SettingsScreen>();
            ControlsScreen = gameObject.AddComponent<UI.ControlsScreen>();
            InventoryScreen = gameObject.AddComponent<UI.InventoryScreen>();
            ExchangeScreen = gameObject.AddComponent<UI.ExchangeScreen>();
            TransmuteScreen = gameObject.AddComponent<UI.TransmuteScreen>();
            TxScreen = gameObject.AddComponent<UI.TxScreen>();

            // Placeholder art until a wallet or backend implements IShowcaseSource. Assigning a
            // real source here is the entire switchover; nothing else knows where art came from.
            //
            // The real source is a MonoBehaviour (it runs a fetch coroutine), so it needs a
            // GameObject; DemoShowcase overrides it for testing without a wallet, exactly as
            // before. WalletInventory.Changed covers a wallet connecting, disconnecting or its
            // holdings changing - every Frame is registered with Showcase directly (see its own
            // header), so a picture finishing its async fetch after the frame that wants it was
            // already built repaints through the same Showcase.Refresh() the source calls itself.
            if (Tuning.Testing.DemoShowcase && Hub.Showcase.Source == null)
                Hub.Showcase.Source = new Hub.DevShowcaseSource(8);
            else if (Hub.Showcase.Source == null)
                Hub.Showcase.Source = gameObject.AddComponent<Hub.WalletShowcaseSource>();
            WalletInventory.Changed += Hub.Showcase.Refresh;

            // Editor testing only - see WalletInventory.RefreshFromServiceAsync's own note. Awaited
            // here rather than fired and forgotten, so the crate and gallery already show the real
            // holdings the first time the hub itself is built rather than popping in a frame later.
            if (!Tuning.Testing.DemoShowcase && !string.IsNullOrEmpty(Tuning.Testing.DemoWalletAddress))
                await WalletInventory.RefreshFromServiceAsync(
                    Tuning.Testing.DemoWalletServiceUrl, Tuning.Testing.DemoWalletAddress);

            ConfirmDialog = gameObject.AddComponent<UI.ConfirmDialog>();

            // The on-screen controls get their OWN canvas at a lower sort order than the screens,
            // so a modal always draws over them - and they are hidden while one is open anyway,
            // which is belt and braces on a thing that must never sit on top of a decision.
            _touchCanvas = UiKit.CreateCanvas("touch", 8);
            _touchCanvas.transform.SetParent(transform, false);
            UI.TouchControls.Build(_touchCanvas.transform);

            // Its own canvas ABOVE the screens (10), not below like the touch overlay - a gamepad
            // cursor has to keep working while a modal is open, since that is exactly when it is
            // navigating one, where the touch stick is deliberately hidden the whole time instead.
            var padCanvas = UiKit.CreateCanvas("gamepad-cursor", 20);
            padCanvas.transform.SetParent(transform, false);
            UI.GamepadCursor.Build(padCanvas.transform);

            // The ability button reads live only when pressing it would do something. A lambda
            // rather than a field for the same reason DamageDealtMultiplier is one: the player
            // does not exist yet, and is rebuilt every run.
            //
            // Also lit while a thrown blade is airborne - the same button recalls it then, and a
            // dimmed button would read as "nothing to press" at exactly the moment there is.
            Controls.ReleaseReady = () =>
                _player != null && (
                    (_player.Blade != null && _player.Blade.Airborne)
                    || (_player.Resource != null && _player.Resource.CanRelease));

            // Same wiring, for the chest's defensive ability (GUARD).
            Controls.GuardReady = () => _player != null && _player.DefenseReady;

            // Whether a drag on the touch ATTACK button aims a weapon art right now.
            Controls.ArtAimable = () => _player != null && _player.ArtAimPossible;

            // The rings on those same buttons - how long, where the flags above only say whether.
            // Each one hands back a number somebody else already owns; none of them keeps a
            // clock of its own, for the reason GuardRing is written the way it is.
            Controls.AttackProgress01 = () => _player != null ? _player.AttackCooldown01 : 0f;
            Controls.GuardProgress01 = () => _player != null ? _player.DefenseCooldown01 : 0f;
            // Full while a thrown blade is out, matching ReleaseReady above: the button recalls
            // it then, and a ring drawing the (spent) meter would read as "not yet" at exactly
            // the moment the press is most available.
            Controls.ReleaseProgress01 = () =>
                _player == null ? 0f
                : _player.Blade != null && _player.Blade.Airborne ? 1f
                : _player.Resource != null ? _player.Resource.Fill01 : 0f;

            _profile = await _store.LoadAsync(await _store.DefaultProfileIdAsync());

            // No wallet is connected, so the account is the local one. Connecting a wallet later
            // means loading by its address, and adopting this record if that address has none.
            _account = await _accounts.LoadAsync(AccountProfile.LocalId);

            // Saved loadouts store the slot as an int, so any reshuffle of GearSlot re-points them
            // at the wrong slot. Prune anything whose item no longer belongs where it sits.
            int dropped = _profile.Gear.DropStale();
            if (dropped > 0)
                Debug.Log($"[ProfileStore] dropped {dropped} loadout entries that no longer match their slot.");
            RegisterMintedGear(_profile);
            // A run left waiting carries its stake - looked up BEFORE the stake is judged stale.
            await LookUpWaitingRunAsync();
            await ForfeitStaleStake();
            Mastery = new Progression.BoardState(_profile.Mastery);

            // A profile from an older board (the rebuild of 2026-10-05 changed every node id) has
            // its purchases dropped and their levels returned, with a note for the mastery screen;
            // then anything else no node answers to is pruned. Every read already ignores dead ids,
            // which is why they once went unnoticed - but the list only grows.
            if (Mastery.Migrate())
                Debug.Log($"[ProfileStore] mastery boards moved to version {Progression.MasteryBoard.Version}: " +
                          (string.IsNullOrEmpty(_profile.Mastery.BoardNotice) ? "nothing to return." : _profile.Mastery.BoardNotice));
            int staleNodes = Mastery.DropStale();
            if (staleNodes > 0)
                Debug.Log($"[ProfileStore] dropped {staleNodes} mastery entries the board no longer has.");
            MasteryScreen.Init(() => _profile);
            if (EnsureStarterGear(_profile))
                await _store.UnlockAsync(_profile);
            await MigrateLegacyRoom();

            // A frame's identity used to be its content key, before it could be re-assigned - see
            // RoomLayout.NormalizeTrophyIds. Idempotent, so it costs nothing to always call.
            if (_account.Room.NormalizeTrophyIds() > 0)
                await _accounts.SaveAsync(_account);

            // A run that ended while its end was still being written (the tab closed on the run-over)
            // is settled now, as it ended - never offered again as a resume.
            await SettleEndedWaitingAsync();

            BuildArena();
            ShowHub();
        }

        /// <summary>
        /// Seed a brand-new profile with a couple of silver pieces. Placeholder scaffolding for
        /// the real path: gear will arrive from checkpoint boxes and elite drops.
        /// </summary>
        static bool EnsureStarterGear(CharacterProfile profile)
        {
            if (profile.Gear.Count > 0) return false;
            profile.Gear.Set(Art.Gear.GearSlot.Weapon, "silver_blade");
            profile.Gear.Set(Art.Gear.GearSlot.Torso, "silver_plate");
            return true;
        }

        /// <summary>
        /// Equip an item and checkpoint it. Gear is permanent and account-level, so this is a
        /// write point in its own right - not something a run boundary carries.
        /// </summary>
        public async void Equip(Art.Gear.GearSlot slot, string itemId)
        {
            // A run left waiting holds the character as it went in - its weapon class, ability and
            // stats are the run's. The hub's fixtures that equip refuse first (GuardWaitingRun); this
            // is the backstop.
            if (_state == State.Hub && _waiting != null)
            {
                Debug.LogWarning($"[Run] {slot} not changed - a run is waiting for this character");
                return;
            }
            _profile.Gear.Set(slot, itemId);

            // Changing the WEAPON can invalidate the relic beside it and any weapon disguise, both
            // of which are tied to the held class. Dropping the stale entry here rather than
            // leaving it to be refused at read time means the screen shows the truth immediately -
            // an empty socket, not one holding something that quietly does nothing.
            if (slot == Art.Gear.GearSlot.Weapon)
            {
                _profile.Gear.DropStale();
                var skin = Art.Gear.GearCatalog.Get(
                    _profile.Look.Transmog.Get(Art.Gear.GearSlot.Weapon));
                var held = Art.Gear.GearCatalog.Get(itemId);
                if (skin != null && held != null
                    && (skin.Class != held.Class || skin.TwoHanded != held.TwoHanded))
                    _profile.Look.Transmog.Clear(Art.Gear.GearSlot.Weapon);
            }

            RepaintLive();

            // Paint resets the weapon layer to the loadout's own base sprite, so a mid-run swap
            // back onto a Prism (skin or the item itself) needs its gem re-lit the same way the
            // heat cycle's own swap would need redoing here, if a weapon swap could ever revive a
            // mid-run heat stage - it can't, because heat only fires from the SOURCE item locked
            // at the door, but Prism's gem depends only on the run's element, which never changes,
            // so it is always safe to just re-apply it after any weapon-slot equip.
            // The discs' combined picture in flight follows the weapon AND its relic.
            if ((slot == Art.Gear.GearSlot.Weapon || slot == Art.Gear.GearSlot.Relic) && _player != null)
                _player.CombinedInFlight = RelicMatchesDrawn(_profile, Art.Gear.GearCatalog.Get(
                    _profile.Look.Resolve(_profile.Gear).Get(Art.Gear.GearSlot.Weapon)));

            if (slot == Art.Gear.GearSlot.Weapon && _player != null)
            {
                var shown = Art.Gear.GearCatalog.Get(
                    _profile.Look.Resolve(_profile.Gear).Get(Art.Gear.GearSlot.Weapon));
                // Prism's lit gem (or, swapped away from it, the glow hidden) - see Attunement.
                var element = _player.Resource != null ? _player.Resource.Element : Art.Gear.Attunement.Current;
                Art.Gear.CharacterRigFactory.ApplyAttunement(_player.Rig, shown, element);

                // Phantom's poof re-synced the same way - a plain bool read fresh from whatever
                // is drawn now, no glow object to hide, just the flag flipping either way.
                _player.PhantomFlicker = shown != null && shown.HasPhantomFlicker;
                _player.SheatheDrawn   = shown != null && shown.HasSheathAnimation;
                _player.GeminiHalves   = shown != null && shown.HasGeminiSplit ? shown.HalfBlades : null;
                if (_player.Split != null) _player.Split.Blades = shown?.SplitBlades;
                _player.CombinedFrames = shown?.CombinedFrames;          // Quintessence's overhead picture
                _player.ArrowSprite = Art.Gear.DemoGear.BowKit(shown).arrow;   // the drawn bow's arrow
                if (shown != null) _player.CombinedFrameSeconds = shown.IdleFrameSeconds;

                // Phantom's haze animation, same re-sync. RepaintLive (above) already reset the
                // weapon layer to the new item's own base sprite, so hiding the ticker here is
                // enough - nothing needs to restore a sprite the repaint didn't already fix.
                // Also the Pacemaker's bead (GearItem.IdleFrames), through the same ticker.
                var (flipbook, flipSeconds) = shown != null ? shown.WeaponFlipbook : (null, 0f);
                if (flipbook != null)
                {
                    _player.Rig?.SetWeaponSprite(flipbook[0]);
                    var hazeAnchor = _player.Rig?.WeaponAnchor;
                    if (hazeAnchor != null)
                        Art.Gear.PhantomHaze.Attach(hazeAnchor, _player.Rig, flipbook, flipSeconds)
                                            .SetShown(true);
                }
                else _player.Rig?.WeaponAnchor?.GetComponentInChildren<Art.Gear.PhantomHaze>(true)
                                              ?.SetShown(false);
            }

            if (CharacterScreen != null) CharacterScreen.Refresh();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// An appearance edit: repaint whatever character is on screen, then checkpoint.
        ///
        /// The same write point as Equip, and for the same reason - the look is account-level and
        /// permanent, not run-scoped, and it is the half of the profile a token actually depicts.
        /// </summary>
        public async void SaveLook()
        {
            RepaintLive();
            if (CharacterScreen != null) CharacterScreen.Refresh();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Re-adds every item this profile has redeemed at the Forge back into GearCatalog. The
        /// catalog is a static, process-wide dictionary that only knows Resources/DemoGear on its
        /// own - a minted instance has to be re-registered every time the active profile changes,
        /// or its own Loadout entry would resolve to nothing the moment the catalog rebuilds.
        /// </summary>
        void RegisterMintedGear(CharacterProfile profile)
        {
            foreach (var record in profile.MintedGear)
            {
                // Rolls made against older gear tables move to the same place in the current
                // ranges FIRST - a backfilled sub-stat below is already current.
                GearForge.Migrate(record);
                // Anything minted before sub-stats existed gets its tier's worth rolled now.
                GearForge.EnsureSubStats(record);
                Art.Gear.GearCatalog.Register(record.ToGearItem());
            }
        }

        /// <summary>
        /// A stake still on the profile at load is from a run that never reached its run-end
        /// checkpoint, so it is lost - see GearStake.ForfeitStale for why a crash counts.
        /// </summary>
        async System.Threading.Tasks.Task ForfeitStaleStake()
        {
            // UNLESS THE RUN IS WAITING: a run left to come back to (or one the run store could not
            // be asked about) still has its stake riding on it - it settles when that run ends.
            if (_waiting != null || _waitingUnknown) return;
            var (outcome, stake) = GearStake.ForfeitStale(_profile);
            if (stake == null) return;
            Debug.Log($"[Stake] {stake.DisplayName} ({stake.InstanceId}) was staked on a run that never " +
                      (outcome == GearStake.Outcome.Saved ? "ended - insured, kept." : "ended - destroyed."));
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Spend a box of a given tier at the Forge for a RANDOM slot - the cheap, common path.
        /// Guarantees the box's own tier - see RollItem's fixed-tier overload.
        /// </summary>
        public async void RedeemForgeBoxRandom(Art.Gear.LootTier boxTier)
        {
            if (DesignDrops.IsDesignTier(boxTier) && StoreFactory.UsingChain)
            {
                await RedeemDesignOnChain(boxTier);
                return;
            }
            if (!TrySpendBoxes(boxTier, Tuning.GearRoll.ForgeRandomRedeemBoxCost)) return;

            // Diamond and Black Diamond are DESIGNS, drawn from every eligible piece of the tier
            // at once rather than slot-first - a slot with one design and a slot with ten are not
            // equally likely places for "any Diamond" to land.
            if (DesignDrops.IsDesignTier(boxTier))
            {
                await MintDesignBox(DesignDrops.Pool(boxTier), boxTier, Tuning.GearRoll.ForgeRandomRedeemBoxCost);
                return;
            }

            var slots = UI.ForgeScreen.RedeemableSlots;
            var slot = slots[UnityEngine.Random.Range(0, slots.Length)];
            await MintForgeBoxItem(slot, boxTier, Art.Gear.WeaponClasses.RandomAny());
        }

        /// <summary>
        /// Spend a box of a given tier at the Forge for a CHOSEN slot (and weapon class, if the
        /// slot is Weapon) - costs more than the random path because the player is paying to
        /// remove the randomness from which slot they get, not from what it rolls.
        ///
        /// Bronze/Silver/Gold only: Diamond and Black Diamond boxes always open at random (the
        /// user's call, 2026-10-09) - the Forge never offers them here, and this refuses them.
        /// </summary>
        public async void RedeemForgeBoxTargeted(
            Art.Gear.GearSlot slot, Art.Gear.LootTier boxTier,
            Art.Gear.WeaponClass weaponClass = Art.Gear.WeaponClass.Greatsword)
        {
            if (DesignDrops.IsDesignTier(boxTier)) return;
            if (!TrySpendBoxes(boxTier, Tuning.GearRoll.ForgeTargetedRedeemBoxCost)) return;
            await MintForgeBoxItem(slot, boxTier, weaponClass);
        }

        /// <summary>
        /// A design box opened against the SERVICE (the browser build): the boxes are tokens in the
        /// wallet, the server rolls the design, the player approves one transaction that burns the
        /// box and mints the design - see Chain.Web.ChainDesigns. Nothing is spent or rolled here;
        /// the records are built from what was minted, and the box count is re-read from the
        /// wallet. A refusal or a declined dialog leaves the profile as it was.
        /// TODO(forge UI): open 5-10 at once (the count is already supported) behind the opening
        /// animation, which plays whatever the chain is doing.
        /// </summary>
        async System.Threading.Tasks.Task RedeemDesignOnChain(Art.Gear.LootTier tier)
        {
            Chain.Web.ChainDesigns.Result result;
            try
            {
                result = await Chain.Web.ChainDesigns.OpenAsync(_profile.ProfileId, tier, 1);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Forge] {tier} box not opened: {e.Message}");
                await Chain.Web.ChainDesigns.SyncBalanceAsync(_profile);
                _hub?.RefreshForge();
                return;
            }

            foreach (var p in result.pieces)
            {
                var design = Art.Gear.GearCatalog.Get(p.design);
                if (design == null)
                {
                    // Minted on chain but unknown here - the service's design manifest is newer
                    // than this build. The token is in the wallet either way.
                    Debug.LogError($"[Forge] minted {p.design} ({p.unit}) - not in this build's catalogue");
                    continue;
                }
                var record = DesignDrops.RecordFor(design, p.instanceId);
                record.Unit = p.unit;
                _profile.MintedGear.Add(record);
                Art.Gear.GearCatalog.Register(record.ToGearItem());
                Debug.Log($"[Forge] minted {record.DisplayName} ({record.InstanceId}) -> {p.unit}");
            }
            await Chain.Web.ChainDesigns.SyncBalanceAsync(_profile);

            if (CharacterScreen != null) CharacterScreen.Refresh();
            _hub?.FlashForge();
            _hub?.RefreshForge();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// A Diamond / Black Diamond box's payout: one design from <paramref name="pool"/>, plus
        /// its relic if it is a Black Diamond weapon (DesignDrops.Mint). The boxes are already
        /// spent; an empty pool - which the Forge never offers - hands them back.
        /// </summary>
        async System.Threading.Tasks.Task MintDesignBox(List<Art.Gear.GearItem> pool,
                                                         Art.Gear.LootTier tier, int spent)
        {
            var design = DesignDrops.Pick(pool, Guid.NewGuid().GetHashCode());
            if (design == null)
            {
                _profile.Boxes.Add(tier, spent);
                Debug.LogWarning($"[Forge] no {tier} design to redeem into - box refunded");
                return;
            }

            foreach (var record in DesignDrops.Mint(design, "BOX"))
            {
                _profile.MintedGear.Add(record);
                Art.Gear.GearCatalog.Register(record.ToGearItem());
                Debug.Log($"[Forge] box-redeemed {record.DisplayName} ({record.InstanceId})");
            }

            if (CharacterScreen != null) CharacterScreen.Refresh();
            _hub?.FlashForge();
            _hub?.RefreshForge();
            await _store.UnlockAsync(_profile);
        }

        async System.Threading.Tasks.Task MintForgeBoxItem(
            Art.Gear.GearSlot slot, Art.Gear.LootTier tier, Art.Gear.WeaponClass weaponClass)
        {
            string instanceId = $"BOX-{slot}-{Guid.NewGuid():N}";
            var (_, grants, primary, subs, ability, finisher) =
                Art.Gear.GearRoller.RollItem(slot, tier, instanceId.GetHashCode(), weaponClass);

            var record = new MintedGearRecord
            {
                InstanceId = instanceId,
                DisplayName = MintedGearRecord.NameFor(tier, slot, weaponClass),
                Slot = slot,
                Tier = tier,
                Grants = grants,
                PrimaryStat = primary,
                SubStats = subs,
                DefensiveAbility = ability,
                Finisher = finisher,
                Class = weaponClass,
                StatsVersion = Art.Gear.GearRoller.TablesVersion,
            };

            _profile.MintedGear.Add(record);
            Art.Gear.GearCatalog.Register(record.ToGearItem());

            Debug.Log($"[Forge] box-redeemed {record.DisplayName} ({instanceId})");

            if (CharacterScreen != null) CharacterScreen.Refresh();
            _hub?.FlashForge();
            _hub?.RefreshForge();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Drains boxes of the given tier - only that tier. Rift Boxes are never Forge currency
        /// for redeeming or re-rolling (the user's call, 2026-10-09; they once stood in for
        /// Silver): they secure loot at a Rift, promote a three-star piece, and insure a stake.
        /// </summary>
        bool TrySpendBoxes(Art.Gear.LootTier tier, int amount)
        {
            if (amount <= 0) return true;
            if (_profile.Boxes.Get(tier) >= amount)
            {
                _profile.Boxes.Add(tier, -amount);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Combine two owned, unequipped pieces that share a GearForge.MatchKey into one: the next
        /// star level, or - from three stars - the next tier at base. Costs no boxes; the two
        /// pieces are the price.
        ///
        /// Returns the new piece so the Forge can show what actually rolled. Everything random in
        /// it is drawn here, at commit, never at preview (see GearForge's own header).
        /// </summary>
        public MintedGearRecord CombineGear(string instanceIdA, string instanceIdB)
        {
            var a = _profile.MintedGear.Find(r => r.InstanceId == instanceIdA);
            var b = _profile.MintedGear.Find(r => r.InstanceId == instanceIdB);
            if (IsEquipped(instanceIdA) || IsEquipped(instanceIdB)) return null;
            var plan = GearForge.Plan(a, b);
            if (plan == null) return null;

            // PROMOTING TAKES A RIFT BOX (the user's call, 2026-10-09): three stars into the next
            // tier is the one combine that needs one. A plain star-up stays free.
            if (plan.Promotes)
            {
                if (_profile.RiftBoxes < Tuning.GearRoll.PromoteRiftBoxCost) return null;
                _profile.RiftBoxes -= Tuning.GearRoll.PromoteRiftBoxCost;
            }

            var record = GearForge.Execute(plan, new System.Random(Guid.NewGuid().GetHashCode()));
            _profile.MintedGear.Remove(a);
            _profile.MintedGear.Remove(b);
            _profile.MintedGear.Add(record);
            Art.Gear.GearCatalog.Register(record.ToGearItem());

            Debug.Log($"[Forge] combined into {record.DisplayName} level {record.UpgradeLevel} ({record.InstanceId})");
            AfterForgeChange();
            return record;
        }

        /// <summary>
        /// Re-roll one sub-stat of a piece, paid in boxes of the piece's OWN tier: one box for a
        /// new value, two to change which stat it is. The result can be worse - the player makes
        /// the gamble. Works on equipped pieces too (it changes the item in place, not its id).
        /// </summary>
        public MintedGearRecord RerollSubStat(string instanceId, int index, bool changeStat)
        {
            var record = _profile.MintedGear.Find(r => r.InstanceId == instanceId);
            if (!GearForge.CanReroll(record, index)) return null;

            int cost = changeStat ? Tuning.GearRoll.ForgeRerollStatBoxCost : Tuning.GearRoll.ForgeRerollValueBoxCost;
            if (!TrySpendBoxes(record.Tier, cost)) return null;

            GearForge.Reroll(record, index, changeStat, new System.Random(Guid.NewGuid().GetHashCode()));
            Art.Gear.GearCatalog.Register(record.ToGearItem());

            Debug.Log($"[Forge] re-rolled sub-stat {index} of {record.DisplayName} ({instanceId})");
            AfterForgeChange();
            return record;
        }

        /// <summary>
        /// SALVAGE: break unwanted pieces into calx of their own tier (GearForge.CalxFor), every
        /// whole Tuning.GearRoll.CalxPerBox becoming a box. Skips anything equipped, staked or not
        /// salvageable rather than refusing the batch. Returns what came of it, for the Forge to
        /// show, or null if nothing was salvaged.
        /// </summary>
        public string SalvageGear(IReadOnlyList<string> instanceIds)
        {
            if (_profile == null || instanceIds == null) return null;

            var calx = new Dictionary<Art.Gear.LootTier, int>();
            int pieces = 0;
            foreach (var id in instanceIds)
            {
                var r = _profile.MintedGear.Find(x => x.InstanceId == id);
                if (!GearForge.Salvageable(r) || IsEquipped(id) || id == _profile.StakedInstanceId) continue;
                _profile.MintedGear.Remove(r);
                calx[r.Tier] = (calx.TryGetValue(r.Tier, out int n) ? n : 0) + GearForge.CalxFor(r);
                pieces++;
            }
            if (pieces == 0) return null;

            var parts = new List<string>();
            foreach (var (tier, n) in calx)
            {
                int boxes = GearForge.AddCalx(_profile, tier, n);
                parts.Add($"{tier} +{n} calx" + (boxes > 0 ? $" -> {boxes} box{(boxes == 1 ? "" : "es")}" : ""));
            }
            string note = $"{pieces} piece{(pieces == 1 ? "" : "s")} salvaged: {string.Join(",  ", parts)}";
            Debug.Log($"[Forge] {note}");
            AfterForgeChange();
            return note;
        }

        /// <summary>
        /// Burn the three pieces a fusion names and mint its weapon and relic. Returns the weapon,
        /// or null if refused (a piece missing, or equipped). No boxes: the three swords are the
        /// price, the same "the inputs are the cost" rule combining lives by.
        /// </summary>
        public MintedGearRecord FuseGear(string fusionId)
        {
            if (_profile == null) return null;
            var fusion = GearForge.FusionById(fusionId);
            if (!GearForge.CanFuse(fusion, _profile.MintedGear, IsEquipped)) return null;

            var burned = GearForge.FusionInputs(fusion, _profile.MintedGear, IsEquipped);
            var (weapon, relic) = GearForge.ExecuteFusion(fusion);

            foreach (var r in burned) _profile.MintedGear.Remove(r);
            _profile.MintedGear.Add(weapon);
            Art.Gear.GearCatalog.Register(weapon.ToGearItem());
            if (relic != null)                  // null for a set whose finisher is not built yet
            {
                _profile.MintedGear.Add(relic);
                Art.Gear.GearCatalog.Register(relic.ToGearItem());
            }

            Debug.Log($"[Forge] fused {string.Join(" + ", Array.ConvertAll(burned, r => r.DisplayName))} " +
                      $"into {weapon.DisplayName} ({weapon.InstanceId})" +
                      (relic != null ? $" and {relic.DisplayName}" : ""));
            AfterForgeChange();
            return weapon;
        }

        // ------------------------------------------------------------------ dev: forcing encounters

        /// <summary>
        /// DEV, ONE-SHOT: the floor the NEXT run starts on (the floors before it are skipped, paying
        /// nothing; the planner's counters start fresh). 0 = floor 1. Clamped to the format's last.
        /// A static, so a domain reload clears it - like the other DevForce switches.
        /// </summary>
        public static int DevStartFloor;

        /// <summary>
        /// DEV: start a run from `eval` (StartRun has overloads reflection cannot tell apart).
        /// Set Core.RunFormat.Current first to test a format - in Play mode, since entering it
        /// reloads the domain and resets statics:
        /// <c>Convergence.Core.RunFormat.Current = Convergence.Core.RunFormatId.ShortDescent;
        /// FindAnyObjectByType&lt;GameBootstrap&gt;().DevStartRun(ElementType.Fire, 10);</c>
        /// </summary>
        public bool DevStartRun(ElementType element, int startFloor = 0)
        {
            if (_state != State.Hub || !ReadyForRun("DevStartRun")) return false;
            DevStartFloor = startFloor;
            StartRun(element);
            return true;
        }

        /// <summary>
        /// DEV: abandon the floor being fought - its enemies, boss and door go, nothing is paid or
        /// cleared - and build <paramref name="floor"/> through the ordinary transition (so its
        /// snapshot, plan, hazards and wave are what that floor really gets). Refused while a
        /// screen or a transition is up. Returns whether it went.
        /// <c>FindAnyObjectByType&lt;GameBootstrap&gt;().DevJumpToFloor(25);</c>
        /// </summary>
        public bool DevJumpToFloor(int floor)
        {
            if (_state != State.Playing || _transitioning || _spawning) return false;
            if (FloorRewardScreen.IsOpen || (ExchangeScreen.IsOpen || ChoiceList.IsOpen) || ConfirmDialog.IsOpen) return false;
            floor = Mathf.Clamp(floor, 1, Core.RunFormat.FinalFloor);

            foreach (var e in _alive) if (e) Destroy(e.gameObject);
            _alive.Clear();
            _spawnQueue.Clear();
            Enemies.EnemyRegistry.Prune();
            if (_boss != null) { _boss.Cease(); Destroy(_boss.gameObject); _boss = null; }
            if (_activeDoor != null) { Destroy(_activeDoor.gameObject); _activeDoor = null; }

            Debug.Log($"[Dev] floor {_floor} abandoned - jumping to floor {floor}");
            _floor = floor - 1;          // NextFloor builds _floor + 1
            StartCoroutine(FloorTransition());
            return true;
        }

        /// <summary>
        /// DEV: every boss's KEY, for <see cref="DevFightBoss"/> - a NUMBER: its place in
        /// <see cref="Bosses.BossKind"/>, counted from 1 (1 = the first BossKind). READ OFF THE
        /// ENUM, so a new boss - its BossKind, APPENDED (the enum is append-only, so no key ever
        /// shifts), and its case in Boss.Spawn, which it needs anyway - has its key with nothing
        /// else to add or document.
        /// <c>unity command eval --code 'return Convergence.Core.GameBootstrap.DevBosses();'</c>
        /// </summary>
        public static string DevBosses()
        {
            var lines = new System.Text.StringBuilder();
            foreach (Bosses.BossKind kind in Enum.GetValues(typeof(Bosses.BossKind)))
                lines.AppendLine($"{(int)kind + 1,2}  {kind,-10} " +
                                 (kind == Bosses.BossKind.Cantor ? "the avatars' stand-in; " : "") +
                                 (Rifts.FloorPlanner.InMiniBossPool(kind) ? "drawn for mini-boss floors" : "not drawn for any floor yet"));
            return lines.ToString();
        }

        /// <summary>
        /// DEV: fight the boss <paramref name="key"/> numbers (<see cref="DevBosses"/>) on a boss
        /// floor - <paramref name="floor"/>, or by default the run's next boss floor (from the hub,
        /// the first). From the hub it starts a run there (<see cref="DevStartRun"/>); mid-run it
        /// jumps there (<see cref="DevJumpToFloor"/>). The floor's depth sets the boss's strength,
        /// as ever. Uses the one-shot <see cref="Rifts.FloorPlanner.DevForceBoss"/>, so an avatar
        /// floor can be forced too. Returns what it did, or why not.
        /// <c>FindAnyObjectByType&lt;GameBootstrap&gt;().DevFightBoss(2, 20)</c> - Medusa on floor 20.
        /// </summary>
        public string DevFightBoss(int key, int floor = 0, ElementType element = ElementType.Fire)
        {
            var kind = (Bosses.BossKind)(key - 1);
            if (!Enum.IsDefined(typeof(Bosses.BossKind), kind))
                return $"no boss {key}. Keys:\n{DevBosses()}";

            if (floor <= 0)
            {
                int from = _state == State.Playing ? _floor + 1 : 1;
                for (floor = from; floor <= Core.RunFormat.FinalFloor && !IsBossFloor(floor); floor++) { }
                if (floor > Core.RunFormat.FinalFloor) return "no boss floor left in this run";
            }
            if (!IsBossFloor(floor))
                return $"floor {floor} is not a boss floor ({Core.RunFormat.WireName}: every {Tuning.Boss.RiftInterval}th, and the avatars)";

            Rifts.FloorPlanner.DevForceBoss = kind;
            bool went = _state == State.Hub ? DevStartRun(element, floor)
                      : _state == State.Playing && DevJumpToFloor(floor);
            if (!went)
            {
                Rifts.FloorPlanner.DevForceBoss = null;
                return $"refused - in state {_state} (a screen or a transition may be up)";
            }
            return $"{kind} on floor {floor} ({Core.RunFormat.WireName})";
        }

        /// <summary>
        /// DEV: mint an instance of an authored design straight into the profile, no box spent -
        /// the way to hand yourself the three Diamond parts to test the fusion from `eval`:
        /// <c>FindAnyObjectByType&lt;GameBootstrap&gt;().DevMintDesign("mercury_reactor")</c>.
        /// </summary>
        public MintedGearRecord DevMintDesign(string itemId)
        {
            var design = Art.Gear.GearCatalog.Get(itemId);
            if (_profile == null || design == null) return null;
            var record = DesignDrops.RecordFor(design, $"DEV-{design.Slot}-{Guid.NewGuid():N}");
            _profile.MintedGear.Add(record);
            Art.Gear.GearCatalog.Register(record.ToGearItem());
            AfterForgeChange();
            return record;
        }

        bool IsEquipped(string instanceId)
            => _profile != null && _profile.Gear.Equipped.Exists(e => e.ItemId == instanceId);

        /// <summary>The refresh-and-checkpoint tail every Forge write shares. Fire-and-forget
        /// save, so combine and re-roll can hand their result straight back to the screen.</summary>
        async void AfterForgeChange()
        {
            if (CharacterScreen != null) CharacterScreen.Refresh();
            _hub?.FlashForge();
            _hub?.RefreshForge();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Open the collection, and hand whatever is chosen back to the room to be carried out.
        /// </summary>
        void OpenCollection()
        {
            if (_hub == null) return;
            ShowcasePicker.Open(_canvas.transform, Hub.Showcase.Source,
                key => _account.Room.Holds(key),
                item => _hub?.BeginPlacement(item));
        }

        /// <summary>Open the collection from an already-placed Frame, wall-mounted or standing -
        /// picking a piece swaps what it shows without moving it.</summary>
        void OpenCollectionForFrame(string frameId)
        {
            if (_hub == null) return;
            ShowcasePicker.Open(_canvas.transform, Hub.Showcase.Source,
                key => _account.Room.Holds(key),
                item => _hub?.SetFrameArt(frameId, item.Key));
        }

        /// <summary>
        /// A trophy was placed, moved or put away. Same write point as equipping: where things
        /// stand in the room is account-level and permanent, not run state.
        /// </summary>
        async void SaveRoom()
        {
            if (_account == null) return;
            await _accounts.SaveAsync(_account);
        }

        /// <summary>
        /// Carry a room saved on a CHARACTER (before the room became account-level) across to the
        /// account, once.
        ///
        /// Only runs when the account has no room of its own, so it can never overwrite an
        /// arrangement the player made after the move. The character copy is cleared either way,
        /// so a second character carrying its own stale layout cannot resurrect it later.
        /// </summary>
        async System.Threading.Tasks.Task MigrateLegacyRoom()
        {
            if (_account == null || _profile?.Room == null) return;
            if (_profile.Room.Trophies.Count == 0) return;

            if (_account.Room.Trophies.Count == 0)
            {
                _account.Room.Trophies.AddRange(_profile.Room.Trophies);
                await _accounts.SaveAsync(_account);
                Debug.Log($"[Convergence] migrated {_account.Room.Trophies.Count} trophies from " +
                          $"{_profile.ProfileId} to the account.");
            }

            _profile.Room.Trophies.Clear();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Point the account at a connected wallet.
        ///
        /// Three cases, and the distinction between the last two is the one that matters:
        ///   - the address already has a record  -> load it; two arrangements cannot be merged
        ///     automatically without silently discarding one
        ///   - no record, and we are UNCONNECTED -> adopt, so a player who decorated their room
        ///     before connecting a wallet keeps it
        ///   - no record, but we are already on a different wallet -> start that address fresh.
        ///     Adopting here would clone one wallet's room onto another.
        ///
        /// Nothing calls this yet: there is no wallet integration. It is the shape the call takes
        /// when there is one, and the hub is rebuilt afterwards because the room may have changed.
        /// </summary>
        public async void ConnectWallet(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress)) return;
            if (StoreFactory.UsingService) { await GraduateOntoWalletAsync(walletAddress); return; }

            if (await _accounts.ExistsAsync(walletAddress))
            {
                _account = await _accounts.LoadAsync(walletAddress);
            }
            else if (_account.IsLocal)
            {
                // Only the UNCONNECTED record is ever adopted. Adopting whatever happened to be
                // loaded would mean connecting a second wallet cloned the first one's room onto
                // it - which is not "keeping what you arranged", it is handing one player's
                // arrangement to a different address.
                _account.Adopt(walletAddress);
                await _accounts.SaveAsync(_account);
            }
            else
            {
                _account = await _accounts.LoadAsync(walletAddress);
                await _accounts.SaveAsync(_account);
            }

            Debug.Log($"[Convergence] account is now {_account.AccountId}");
            if (_state == State.Hub) ShowHub();
        }

        /// <summary>
        /// The wallet chooser: every CIP-30 wallet the page can see, offered one at a time (a
        /// two-button dialog - "another wallet" moves to the next). Connecting signs a MESSAGE only.
        /// </summary>
        async void BeginConnectWallet()
        {
            if (ConfirmDialog == null || ConfirmDialog.IsOpen) return;
            var wallets = await Chain.Web.ChainWallet.ListAsync();
            if (wallets.Length == 0)
            {
                ConfirmDialog.Show(_canvas.transform, "NO WALLET FOUND",
                    "This browser has no Cardano wallet extension. Install one, then reload the page.",
                    "OK", "Close", null);
                return;
            }
            OfferWallet(wallets, 0);
        }

        void OfferWallet(string[] wallets, int i)
        {
            bool last = i == wallets.Length - 1;
            ConfirmDialog.Show(_canvas.transform, $"CONNECT {wallets[i].ToUpperInvariant()}?",
                "The wallet signs a message proving the address - no fee, and nothing moves. If it has no " +
                "characters of its own, this browser's characters and room move onto it.",
                "Connect", last ? "Not now" : "Another wallet",
                async () =>
                {
                    var address = await Chain.Web.ChainWallet.ConnectAsync(wallets[i]);
                    if (!string.IsNullOrEmpty(address)) ConnectWallet(address);
                    _hub?.RefreshBooth();
                },
                last ? null : () => OfferWallet(wallets, i + 1));
        }

        /// <summary>
        /// A proven wallet takes over this browser's saves (Chain/Service): the guest GRADUATES onto it
        /// when the wallet has no records of its own, and the hub reloads from the wallet either way.
        /// An unproven wallet changes nothing - the service writes only for a proof.
        /// </summary>
        async System.Threading.Tasks.Task GraduateOntoWalletAsync(string walletAddress)
        {
            if (!Chain.Web.ChainWallet.Proven || string.IsNullOrEmpty(Chain.Web.ChainWallet.Session))
            {
                Debug.LogWarning("[Convergence] wallet connected but not proven - saves stay with this browser's guest");
                return;
            }

            var result = await Chain.Service.ServiceIdentity.AttachWalletAsync(walletAddress, Chain.Web.ChainWallet.Session);
            if (result == Chain.Service.ServiceIdentity.Graduation.Failed)
            {
                ConfirmDialog?.Show(_canvas.transform, "SAVES NOT MOVED",
                    "The service could not move this browser's characters to the wallet. You are still playing " +
                    "them here; connect again to retry.", "OK", "Close", null);
                return;
            }

            bool same = result is Chain.Service.ServiceIdentity.Graduation.Moved
                               or Chain.Service.ServiceIdentity.Graduation.AlreadyMoved;
            if (_state == State.Hub)
            {
                await BecomeAsync(same ? _profile.ProfileId : await _store.DefaultProfileIdAsync(), force: true);
                _account = await _accounts.LoadAsync(walletAddress);
                ShowHub();
            }

            string body = result switch
            {
                Chain.Service.ServiceIdentity.Graduation.Moved =>
                    "Your characters and room now belong to this wallet.",
                Chain.Service.ServiceIdentity.Graduation.WalletHasRecords =>
                    "This wallet already has characters, so you are now playing them. The characters from " +
                    "this browser stay here, and come back if you disconnect.",
                _ => "You are now playing this wallet's characters.",
            };
            ConfirmDialog?.Show(_canvas.transform, "WALLET CONNECTED", body, "OK", "Close", null);
        }

        /// <summary>Same write point as Equip - a cosmetic preference, but still worth persisting.</summary>
        public async void ToggleHelmHidden()
        {
            _profile.HelmHidden = !_profile.HelmHidden;

            RepaintLive();

            if (CharacterScreen != null) CharacterScreen.Refresh();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Repaint whatever character is on screen, and the shadow echoes with it.
        ///
        /// The echoes are pooled and painted ONCE when they are built, so without this a player
        /// who changed gear mid-run would keep throwing copies of themselves wearing the loadout
        /// they walked in with.
        /// </summary>
        void RepaintLive()
        {
            Art.Gear.CharacterRigFactory.Paint(LiveRig(), _profile);
            if (_player != null) _player.Echoes?.Repaint();
            if (_player != null) _player.Split?.Repaint();
        }

        /// <summary>
        /// The character currently on screen - the arena player during a run, the hub character
        /// between them.
        ///
        /// Gear is equipped from the loadout screen, which opens in BOTH places, so an equip made
        /// in the menu has to repaint the menu character. Reading only _player.Rig meant gear
        /// changed silently in the hub and only appeared once a run started.
        ///
        /// NOT written as _player?.Rig: a destroyed Unity object is "fake null", so ?. does not
        /// short-circuit on it and the caller would run against destroyed renderers. The rig's
        /// Transform is a real UnityEngine.Object, so != null uses Unity's own check.
        /// </summary>
        Art.Gear.ICharacterRig LiveRig()
        {
            if (_player != null) return _player.Rig;
            return _hub != null ? _hub.AvatarRig : null;
        }

        // ------------------------------------------------------------------ arena

        void BuildArena()
        {
            var root = new GameObject("Arena");
            root.transform.SetParent(transform, false);
            _arenaRoot = root.transform;

            var art = Art.GameArt.I;
            if (art.FloorSprite != null && art.TileFloor) BuildTiledFloor(art);
            else
            {
                var floor = new GameObject("Floor");
                floor.transform.SetParent(_arenaRoot, false);
                var fsr = floor.AddComponent<SpriteRenderer>();
                bool authored = art.FloorSprite != null;
                // No authored floor: the procedural BRICK floor (Art/FloorArt) - warm, mid-value,
                // low contrast, so what stands on it reads. It replaced a near-black square.
                fsr.sprite = authored ? art.FloorSprite : Art.FloorArt.Bricks;
                fsr.color = Color.white;
                fsr.sortingOrder = -10;

                // Oversized on purpose - see FloorMargin. The WALLS still sit at HalfWidth; this
                // is only ground for the camera to look at when it centres a player standing on
                // the boundary.
                float fw = (HalfWidth + FloorMargin) * 2f, fh = (HalfHeight + FloorMargin) * 2f;

                // The draw mode is decided ONCE, from what the sprite actually is. Setting Sliced
                // first and correcting it afterwards still assigned a nine-slice mode to a
                // procedural sprite for an instant, and Unity warns about that at assignment -
                // "Sprite Tiling might not appear correctly ... not generated with Full Rect".
                if (authored)
                {
                    fsr.drawMode = SpriteDrawMode.Sliced;
                    fsr.size = new Vector2(fw, fh);
                }
                else
                {
                    // TILED at the brick sprite's own density, so a brick is one size on screen
                    // however big the arena is (the sprite is FullRect for exactly this).
                    fsr.drawMode = SpriteDrawMode.Tiled;
                    fsr.tileMode = SpriteTileMode.Continuous;
                    fsr.size = new Vector2(fw, fh);
                }
            }

            // Grid lines, purely so movement reads. Authored floor art replaces them.
            //
            // This used to `return` here, which took the WALLS and Arena.HalfExtents with it -
            // turning on HideGrid, or dropping in a floor sprite, silently produced an arena with
            // no collision and stale published bounds.
            // The brick floor shows movement itself, so there is no longer a floor the grid is
            // drawn over - MakeLine is kept for the day the fallback goes back to a flat colour.
            bool flatFloor = false;
            if (flatFloor && !art.HideGrid && art.FloorSprite == null)
            {
                for (int x = -(int)HalfWidth; x <= (int)HalfWidth; x += 2)
                    MakeLine(new Vector3(x, 0, 0), new Vector3(0.03f, HalfHeight * 2f, 1f));
                for (int y = -(int)HalfHeight; y <= (int)HalfHeight; y += 2)
                    MakeLine(new Vector3(0, y, 0), new Vector3(HalfWidth * 2f, 0.03f, 1f));
            }

            // Publish the bounds for anything that has to keep a body inside them.
            Arena.HalfExtents = new Vector2(HalfWidth, HalfHeight);

            // Containing walls.
            float t = 0.6f;
            MakeWall(new Vector2(0, HalfHeight), new Vector2(HalfWidth * 2f + t, t));
            MakeWall(new Vector2(0, -HalfHeight), new Vector2(HalfWidth * 2f + t, t));
            MakeWall(new Vector2(-HalfWidth, 0), new Vector2(t, HalfHeight * 2f + t));
            MakeWall(new Vector2(HalfWidth, 0), new Vector2(t, HalfHeight * 2f + t));

            // The walls' tops, the north face and the dark beyond are drawn by the scenery, which
            // every floor re-rolls (DressArena); this is the look before the first floor.
            Art.Scenery.Dress(_arenaRoot, Art.Scenery.Default, new Vector2(HalfWidth, HalfHeight), FloorMargin);
        }

        /// <summary>
        /// The floor's scenery - masonry, stone, and past floor 25 the chance of windows and the
        /// time of day through them (Art.Scenery). Seeded per floor, so a restart meets the same
        /// rooms. Runs behind the floor transition's black screen, so the swap is never seen.
        /// </summary>
        void DressArena(int floor)
        {
            if (_arenaRoot == null) return;
            int seed = _planner != null ? _planner.SceneSeed(floor) : floor * 7919 + 1;
            // The BIOME first - everything built on this floor asks it (Art.Biomes.Biome).
            Art.Biomes.Biome.Set(Art.Biomes.Biome.For(_planner != null ? _planner.Seed : 0, floor));
            // The room tone follows the place; a boss floor lays a heartbeat over it.
            Convergence.Audio.Ambience.Set(Art.Biomes.Biome.Bed(floor), IsBossFloor(floor));
            Hazards.Column.LookSeed = seed;   // which columns stand whole and which are broken
            var theme = Art.Scenery.Roll(floor, seed);
            Art.Scenery.Dress(_arenaRoot, theme, new Vector2(HalfWidth, HalfHeight), FloorMargin);
            // The room's darkness: by depth today, by biome later. Windows let the day in.
            float dark = Art.Biomes.Biome.Darkness(Core.RunFormat.Depth(floor), IsBossFloor(floor));
            if (theme.Light != Art.Scenery.Daylight.None && theme.WindowPairs > 0) dark *= Tuning.Lighting.WindowFloorDarkness;
            Convergence.Art.Lighting.LightField.SetPlace(dark, Art.Biomes.Biome.Shade);
            Debug.Log($"[Floor] {Art.Biomes.Biome.Current} | scenery {theme}");
        }

        /// <summary>Repeats one floor tile across the arena - the usual shape for authored 2D art.</summary>
        void BuildTiledFloor(Art.GameArt art)
        {
            var parent = new GameObject("Floor").transform;
            parent.SetParent(_arenaRoot, false);

            float step = Mathf.Max(0.25f, art.FloorTileSize);
            float native = art.FloorSprite.bounds.size.x;
            float scale = native > 0.0001f ? step / native : 1f;

            for (float x = -HalfWidth + step * 0.5f; x < HalfWidth; x += step)
            for (float y = -HalfHeight + step * 0.5f; y < HalfHeight; y += step)
            {
                var t = new GameObject("tile");
                t.transform.SetParent(parent, false);
                t.transform.localPosition = new Vector3(x, y, 0f);
                t.transform.localScale = Vector3.one * scale;
                var sr = t.AddComponent<SpriteRenderer>();
                sr.sprite = art.FloorSprite;
                sr.sortingOrder = -10;
            }
        }

        void MakeLine(Vector3 pos, Vector3 scale)
        {
            var go = new GameObject("grid");
            go.transform.SetParent(_arenaRoot, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = new Color(1f, 1f, 1f, 0.035f);
            sr.sortingOrder = -9;
        }

        void MakeWall(Vector2 pos, Vector2 size)
        {
            var go = new GameObject("wall");
            go.transform.SetParent(_arenaRoot, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var wallArt = Art.GameArt.I.WallSprite;
            go.AddComponent<BoxCollider2D>();
            // No authored wall: Art.Scenery draws the boundary, and this is only the collider.
            if (wallArt == null) return;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = wallArt;
            sr.sortingOrder = -8;
        }
        
        // ------------------------------------------------------------------ hub (main menu)

        /// <summary>
        /// Show the menu. It is a ROOM, not a screen - see <see cref="Hub.HubRoom"/> for why.
        ///
        /// The arena is switched off rather than destroyed. It is built once at start-up and its
        /// floor sits at the same sorting order as the hub's, so leaving it on draws two rooms on
        /// top of each other; rebuilding it per run would instead make every door press pay for a
        /// full arena construction.
        /// </summary>
        async void ShowHub()
        {
            // Same guard as StartRun, for the same reason: async void drops exceptions into the
            // sync context, where a failure part-way through building the room shows up as an
            // empty scene and no message at all rather than as an error.
            try { await ShowHubAsync(); }
            catch (Exception e) { Debug.LogError($"[Convergence] ShowHub failed: {e}"); }
        }

        async System.Threading.Tasks.Task ShowHubAsync()
        {
            _state = State.Hub;
            Convergence.Audio.Ambience.Set(Convergence.Audio.Bed.Hub);
            Convergence.Art.Lighting.LightField.SetPlace(0f, Color.black);   // the hub is lit as it always was

            // The roster is read before the room is built, because the couch is part of the room
            // and rebuilding it afterwards would pop characters into existence a frame late.
            await RefreshRoster();
            if (_state != State.Hub) return;   // a run started while the read was in flight

            if (_arenaRoot) _arenaRoot.gameObject.SetActive(false);
            SetHubCamera();

            if (_hub != null) { _hub.Teardown(); Destroy(_hub); }
            _hub = gameObject.AddComponent<Hub.HubRoom>();
            _hub.Build(transform, _canvas.transform, _cam, _profile, _order, _roster,
                       EnterGate,
                       () => { if (!GuardWaitingRun()) TransmutationScreen.Open(_canvas.transform, _profile.LastElement); },
                       () => { if (!GuardWaitingRun()) MasteryScreen.Open(_canvas.transform); },
                       SwitchCharacter,
                       NewCharacter,
                       OpenCollection,
                       OpenCollectionForFrame,
                       SaveRoom,
                       _account.Room,
                       () => TxScreen.Open(_canvas.transform),
                       TakePortrait,
                       OpenForge,
                       OpenManual,
                       DressArmoury,
                       SaveLook);
            // Open from the first frame if the player has already run today - no slide on arrival.
            _hub.HoldDoorOpen(_account != null && _account.RanToday, snap: true);
            // Stone for a new week until the selector wakes them (AccountProfile.SigilsWokenWeek).
            _hub.SetSigilsStone(_account != null && _account.SigilsAsleep());
            _hub.WakeSigils = () =>
            {
                if (_account == null || !_account.WakeSigils()) return false;
                _hub.SetSigilsStone(false);
                SaveRoom();
                return true;
            };
            // A run left waiting: the gate offers it back (AskWaitingRun).
            _hub.WaitingRun = () => _waiting?.Snapshot is { } s
                ? ($"floor {s.Floor}  -  {s.Element.ToString().ToLowerInvariant()}", s.Element)
                : null;
        }

        /// <summary>Opens the Forge. Reachable only through the fixture's own interact key, which
        /// already gates on there being something to spend or combine.</summary>
        void OpenForge()
        {
            if (_profile == null) return;
            ForgeScreen.Open(_canvas.transform, new UI.ForgeScreen.Context
            {
                Boxes = _profile.Boxes,
                RiftBoxes = () => _profile.RiftBoxes,
                MintedGear = _profile.MintedGear,
                IsEquipped = IsEquipped,
                OnRedeemRandom = RedeemForgeBoxRandom,
                OnRedeemTargeted = RedeemForgeBoxTargeted,
                OnCombine = CombineGear,
                OnReroll = RerollSubStat,
                OnFuse = FuseGear,
                OnSalvage = SalvageGear,
                Calx = _profile.Calx,
            });
        }

        /// <summary>Opens the Manual Shrine's book. Pure reading - no profile, no chain, nothing
        /// that can go stale.</summary>
        void OpenManual() => ManualScreen.Open(_canvas.transform);

        /// <summary>
        /// Opens the armoury picker for one of the two stands.
        ///
        /// Deliberately NOT the loadout screen. The armoury is cosmetic - it displays pieces the
        /// character is not carrying - so pointing it at the screen that changes what they ARE
        /// carrying would undo the entire distinction the two fixtures exist to draw.
        ///
        /// The pick writes through the room, and the room writes through SaveRoom, because what
        /// is on show is part of the ROOM rather than of the character: it is the account's hub,
        /// and it stays arranged the same way whichever character is being played.
        /// </summary>
        void DressArmoury(bool weapons)
        {
            if (_hub == null) return;
            if (weapons) GearDisplayScreen.OpenWeapons(_canvas.transform, _hub.RackShown, _hub.SetRackShown);
            else GearDisplayScreen.OpenArmour(_canvas.transform, _hub.StandShown, _hub.SetStandShown);
        }

        /// <summary>Re-read the saved characters, minus the one being played.</summary>
        async System.Threading.Tasks.Task RefreshRoster()
        {
            _roster.Clear();
            if (_profile == null) return;
            try
            {
                foreach (var p in await _store.ListAsync())
                    if (p.ProfileId != _profile.ProfileId) _roster.Add(p);
            }
            catch (Exception e)
            {
                // A roster that cannot be read is a couch with nobody on it, not a broken hub.
                Debug.LogWarning($"[Convergence] could not list characters: {e.Message}");
            }
        }

        /// <summary>
        /// Play as a different saved character.
        ///
        /// Only reachable from the hub, never mid-run: the loaded profile is what every stat,
        /// every checkpoint and the rig itself is read from, and swapping it underneath a live
        /// run would commit one character's kills against another's datum.
        /// </summary>
        /// <summary>
        /// The one place in the game that opens a wallet dialog for a transaction rather than a
        /// message. Everything else the chain writes is service-authored and silent; this is the
        /// player choosing to spend a fee, so it goes through the same ConfirmDialog every other
        /// irreversible choice in the hub uses, and it must not be reachable except by pressing
        /// the booth's own interact key - see PhotoBooth's and HubRoom.RefreshBooth's docs.
        /// </summary>
        async void TakePortrait()
        {
            if (_hub == null || _profile == null) return;
            // No wallet yet: the booth's key is "connect a wallet", and it lands here.
            if (!Chain.Web.ChainWallet.Connected) { BeginConnectWallet(); return; }

            string diff;
            try { diff = await Chain.Web.ChainPfp.DescribeAsync(_profile); }
            catch (Chain.Web.ChainException e) { diff = e.Message; }

            ConfirmDialog.Show(_canvas.transform, "Take a portrait?", diff, "Take it", "Not yet", async () =>
            {
                try
                {
                    var hash = await Chain.Web.ChainPfp.TakeAsync(_profile);
                    Debug.Log($"[Convergence] portrait taken: {hash}");
                    _hub?.FlashBooth();
                }
                catch (Chain.Web.ChainException e)
                {
                    // A refused wallet dialog lands here too, and is not an error - the player
                    // said no and nothing was spent. Logged rather than surfaced further; the
                    // booth's own re-read is what the player actually sees on their next visit.
                    Debug.LogWarning($"[Convergence] portrait not taken: {e.Message}");
                }
                _hub?.RefreshBooth();
            });
        }

                async void SwitchCharacter(string profileId)
        {
            if (await BecomeAsync(profileId, force: false)) ShowHub();   // rebuilds the room, the standing character and the couch
        }

        /// <summary>
        /// Loads a character and makes it the one being played. <paramref name="force"/> reloads even
        /// the current one - after a wallet takes over the saves, the same id now reads from the wallet.
        /// </summary>
        async System.Threading.Tasks.Task<bool> BecomeAsync(string profileId, bool force)
        {
            if (_state != State.Hub) return false;
            if (string.IsNullOrEmpty(profileId) || (!force && profileId == _profile.ProfileId)) return false;

            try
            {
                var next = await _store.LoadAsync(profileId);
                if (_state != State.Hub) return false;   // a run started while the load was in flight

                _profile = next;
                _profile.Gear.DropStale();
                RegisterMintedGear(_profile);
                await LookUpWaitingRunAsync();
                await ForfeitStaleStake();
                Mastery = new Progression.BoardState(_profile.Mastery);
                if (EnsureStarterGear(_profile)) await _store.UnlockAsync(_profile);
                await SettleEndedWaitingAsync();

                Debug.Log($"[Convergence] now playing {_profile.DisplayName} ({_profile.ProfileId})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Convergence] could not switch to {profileId}: {e}");
                return false;
            }
        }

        /// <summary>
        /// Start a new character on an id the store hands out (IProfileStore.NewProfileId): the
        /// next free device slot locally, an id unique to the whole service on the service.
        ///
        /// Local slots are scanned rather than counted: deleting profile_local-dev-1 and then
        /// counting files would hand the next character an id that is already taken and silently
        /// overwrite somebody.
        /// </summary>
        async void NewCharacter()
        {
            if (_state != State.Hub) return;

            var taken = new HashSet<string> { _profile.ProfileId };
            foreach (var p in _roster) taken.Add(p.ProfileId);

            string id = _store.NewProfileId(taken);
            if (id == null) { Debug.LogWarning("[Convergence] no free character slot."); return; }

            try
            {
                var created = await _store.LoadAsync(id);   // absent id yields a fresh profile
                created.Name = $"Character {_roster.Count + 2}";
                EnsureStarterGear(created);
                await _store.UnlockAsync(created);
                if (_state != State.Hub) return;

                Debug.Log($"[Convergence] created {created.DisplayName} ({id})");
                SwitchCharacter(id);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Convergence] could not create a character: {e}");
            }
        }

        // ---- the dev run-format toggle (Tuning.Testing.FormatToggle) ----

        const string DevFormatPref = "dev.runFormat";
        Text _devFormatLabel;

        static bool DevFormatEnabled => Tuning.Testing.FormatToggle && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>
        /// Flips the next run's format on F8 (remembered in PlayerPrefs, applied again at
        /// <see cref="Start"/>) and keeps the label on RunFormat.Current itself - never written
        /// every frame, because a resume sets the run's own format and then waits in the hub, and
        /// an eval may set it for a test. A domain reload mid-Play resets it to Classic; the label
        /// then says so. True when it took the frame's input.
        /// </summary>
        bool TickDevFormat()
        {
            if (!DevFormatEnabled || _canvas == null) return false;
            bool pressed = Controls.DevFormatTapped;
            if (pressed)
            {
                RunFormat.Current = RunFormat.Short ? RunFormatId.Classic : RunFormatId.ShortDescent;
                PlayerPrefs.SetInt(DevFormatPref, RunFormat.Short ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (_devFormatLabel == null)
            {
                var rt = UiKit.Rect(_canvas.transform, "DevFormat", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-420, -54), new Vector2(420, -14));
                _devFormatLabel = UiKit.Label(rt, "", 20, new Color(0.95f, 0.82f, 0.45f), TextAnchor.MiddleCenter);
                UiKit.Legible(_devFormatLabel);
            }
            _devFormatLabel.gameObject.SetActive(true);
            _devFormatLabel.text = RunFormat.Short
                ? "NEXT RUN: SHORT DESCENT  (50 floors, 60 min)   [ F8 ]"
                : "NEXT RUN: CLASSIC  (100 floors)   [ F8 ]";
            return pressed;
        }

        void HideHub()
        {
            if (_hub == null) return;

            if (_devFormatLabel != null) _devFormatLabel.gameObject.SetActive(false);

            // Photograph the room BEFORE tearing it down - this is the only moment it exists and
            // is framed. What a Rift shows through its tear is this picture: the hub as the player
            // left it, which is the thing they are deciding whether to go back to.
            Rifts.HubGlimpse.Capture(_cam, _hub.GateFocus);

            _hub.Teardown();
            Destroy(_hub);
            _hub = null;

            // The arena and every other room stay strict top-down; only the hub tilts. Reset here
            // rather than leaving it to whatever comes next, because TrackCamera only ever writes
            // .position - a rotation left over from the hub would silently ride along into combat.
            if (_cam) _cam.transform.rotation = Quaternion.identity;

            // And the armoury's zoom, in case the hub was left from inside it - back to the
            // ARENA's, which is closer on a phone (ArenaViewSize).
            _armouryView = false;
            var zoom = _cam ? _cam.GetComponent<PixelPerfectZoom>() : null;
            if (zoom != null) zoom.TargetSize = ArenaViewSize;
        }

        /// <summary>
        /// Pitches the camera down by Tuning.Hub.CameraTiltDegrees, repositioned so it still looks
        /// at the room's own vertical centre. Orthographic, so the tilt costs no reframing beyond
        /// this: distance from the look-at point does not change apparent size the way it would
        /// under perspective, only the angle does.
        /// </summary>
        void SetHubCamera()
        {
            if (!_cam) return;
            float theta = Tuning.Hub.CameraTiltDegrees * Mathf.Deg2Rad;

            // Centre on what is DRAWN, not on the walkable floor. The room's picture runs from the
            // outside of the south wall up to the top of the gallery band, and the band is all
            // above FloorTop - so framing on the floor's own midpoint hangs the whole room high
            // and leaves a strip of dead space along the bottom of the screen.
            float top = Tuning.Hub.FloorTop + Tuning.Hub.GalleryHeight;
            float bottom = Tuning.Hub.FloorBottom - Tuning.Hub.WallThickness;
            float lookAtY = (top + bottom) * 0.5f;
            const float distance = 10f;

            // Back to the game's own zoom - the armoury is the one room that changes it.
            _armouryView = false;
            var zoom = _cam.GetComponent<PixelPerfectZoom>();
            if (zoom != null) zoom.TargetSize = CameraSize;

            _cam.transform.rotation = Quaternion.Euler(Tuning.Hub.CameraTiltDegrees, 0f, 0f);
            _cam.transform.position = new Vector3(
                0f,
                lookAtY + Mathf.Sin(theta) * distance,
                -Mathf.Cos(theta) * distance);
        }

        void Update()
        {
            // Unconditional and first - Hitstop is what's driving Time.timeScale, so it has to
            // tick on unscaled time regardless of anything below being ready yet.
            Hitstop.Tick();
            CameraKick.Tick();
            // A run write that failed is tried again on its own clock - in any state, so the last
            // snapshot of a run being left still gets through.
            _keeperField?.Tick(Time.unscaledTime);

            // This used to open with `if (kb == null) return;`. On a device with no keyboard -
            // which is every phone - that returned before the floor-clear check, the camera track
            // and every screen toggle, so the game came up, drew itself, and then did nothing at
            // all. It is the single worst line in the project for a touch build, and the symptom
            // (a run that never starts its first floor) points nowhere near input.
            //
            // Start() is async, so a frame can reach Update before these exist.
            // Every screen Update touches, checked here. Start() is async, so there are frames
            // where some of these exist and others do not - naming them individually is the only
            // version of this guard that stays correct when a new screen is added below the last
            // one listed, which is exactly how ShowcasePicker and ExchangeScreen started throwing
            // a NullReference once a frame on every launch.
            if (CharacterScreen == null || _profile == null || _canvas == null ||
                ConfirmDialog == null || TransmutationScreen == null ||
                ShowcasePicker == null || ExchangeScreen == null || TransmuteScreen == null || TxScreen == null ||
                MasteryScreen == null || FloorRewardScreen == null || ForgeScreen == null ||
                ManualScreen == null || GearDisplayScreen == null || RiftScreen == null ||
                StakeScreen == null ||
                PauseScreen == null || SettingsScreen == null || ControlsScreen == null ||
                InventoryScreen == null) return;

            // ONE owner for which buttons are on screen. The room could set this itself while
            // carrying a trophy, and did at first - but then two Updates were writing the same
            // field in an undefined order and the button set flickered between them every frame.
            // Ask the room what it is doing instead.
            bool screenOpen = MasteryScreen.IsOpen || ConfirmDialog.IsOpen || FloorRewardScreen.IsOpen
                      || TransmutationScreen.IsOpen || ShowcasePicker.IsOpen || (ExchangeScreen.IsOpen || ChoiceList.IsOpen)
                      || TransmuteScreen.IsOpen
                      || TxScreen.IsOpen || CharacterScreen.IsOpen || ForgeScreen.IsOpen
                      || ManualScreen.IsOpen || GearDisplayScreen.IsOpen || RiftScreen.IsOpen || StakeScreen.IsOpen
                      || PauseScreen.IsOpen || SettingsScreen.IsOpen || ControlsScreen.IsOpen
                      || InventoryScreen.IsOpen || UI.EndingScreen.IsOpen;
            bool modal = screenOpen || (_hub != null && _hub.Blocking);

            // The sigil door stays open for the day once the player has run, and shuts on its own
            // at the daily reset (00:00 UTC) - even with the player standing in the room.
            // Likewise the week: its reset turns the sigils to stone until the selector wakes them.
            if (_state == State.Hub && _hub != null)
            {
                _hub.HoldDoorOpen(_account != null && _account.RanToday);
                _hub.SetSigilsStone(_account != null && _account.SigilsAsleep());
            }

            // A carry blocks the room like a modal, but it has its OWN button set - checked
            // first, or the Blocking half of `modal` swallowed it and PLACE never showed.
            bool placing = _state == State.Hub && !screenOpen && _hub != null && _hub.Placing;

            // A screen that must be ANSWERED gets no back button. Everything else does, and it
            // means exactly what Escape means there.
            bool dismissible = !(FloorRewardScreen.IsOpen && !FloorRewardScreen.CanDismiss)
                            && !(ExchangeScreen.IsOpen && !ExchangeScreen.CanDismiss)
                            && !TransmuteScreen.IsOpen;

            Controls.Screen =
                  placing ? Controls.Context.Placing
                : modal ? (dismissible ? Controls.Context.Modal : Controls.Context.Hidden)
                : _state == State.Playing ? Controls.Context.Arena
                : _state == State.Hub ? Controls.Context.Hub
                : Controls.Context.Hidden;

            // The mastery grid is a MAIN MENU screen. Spending mastery mid-run would let a player
            // pause a losing fight and buy their way out of it, and the board is meant to be read
            // between runs when planning what to play next - not during one.
            // [E] closes it as well as opening it, because the table is now the main way in and
            // a prop you walk up to and press E on should let go of E. [M] still works.
            if (_state == State.Hub && !(_hub != null && _hub.Blocking) &&
                (Controls.MasteryTapped ||
                 (MasteryScreen.IsOpen && (Controls.CancelTapped || Controls.InteractTapped))))
            {
                MasteryScreen.Toggle(_canvas.transform);
                return;
            }

            // Settings, from the hub only. BOTH directions go through GameBootstrap rather than
            // letting SettingsScreen close itself on Cancel: Escape drives both
            // Controls.SettingsTapped (open) and Controls.CancelTapped (close - shared by every
            // other screen), and script execution order between two independently-built
            // MonoBehaviours is undefined. That used to produce two symptoms from one cause -
            // opening Settings raced whatever screen was already open for the very keypress that
            // was meant to close IT (this ran first and returned, so e.g. CharacterScreen never
            // saw its own Escape), and closing Settings could immediately reopen it on the same
            // frame when SettingsScreen.Update happened to run before this block did, since by
            // then IsOpen already read false while SettingsTapped was still true. Gating the open
            // on `!modal` (so it never fires while anything else, Settings and Controls included,
            // is already up) and owning the close here removes both races.
            if (_state == State.Hub && !(_hub != null && _hub.Blocking))
            {
                if (SettingsScreen.IsOpen)
                {
                    if (Controls.CancelTapped) { SettingsScreen.Dismiss(); return; }
                }
                else if (!modal && Controls.SettingsTapped)
                {
                    OpenSettings();
                    return;
                }
            }
            // Mid-run, Settings is reached from the pause menu, and Escape hands back to it.
            else if (_state == State.Playing && SettingsScreen.IsOpen && Controls.CancelTapped)
            {
                SettingsScreen.Dismiss();
                return;
            }

            // Everything below drives the game itself, and a modal is by definition covering it.
            // Without this, a click meant for the mastery grid fell through to the element cards
            // underneath and started a run, and Esc aimed at a confirm prompt re-opened it.
            if (MasteryScreen.IsOpen || ConfirmDialog.IsOpen || FloorRewardScreen.IsOpen ||
                TransmutationScreen.IsOpen || ShowcasePicker.IsOpen || (ExchangeScreen.IsOpen || ChoiceList.IsOpen) ||
                TransmuteScreen.IsOpen ||
                TxScreen.IsOpen || ForgeScreen.IsOpen || ManualScreen.IsOpen ||
                GearDisplayScreen.IsOpen || RiftScreen.IsOpen || StakeScreen.IsOpen ||
                PauseScreen.IsOpen || SettingsScreen.IsOpen || ControlsScreen.IsOpen ||
                InventoryScreen.IsOpen) return;

            // Loadout screen. From the HUB it opens on its own key. MID-RUN it lives behind the
            // pause menu (opened in the Playing branch below), so the same key there only closes
            // it - and closing it hands back to that menu via CharacterScreen's onClosed.
            if (_state == State.Hub)
            {
                // Cancel is the gear grid's while it is up - it backs out of the grid only.
                bool toggle = Controls.LoadoutTapped
                           || (CharacterScreen.IsOpen && !CharacterScreen.PickerOpen && Controls.CancelTapped);
                if (toggle) { CharacterScreen.Toggle(_canvas.transform, ScreenElement()); return; }
            }
            else if (_state == State.Playing && CharacterScreen.IsOpen
                     && (Controls.LoadoutTapped || (!CharacterScreen.PickerOpen && Controls.CancelTapped)))
            {
                CharacterScreen.Close();
                return;
            }
            if (CharacterScreen.IsOpen) return;

            // Backstop: time stopped with nothing holding the pause is always a bug. Asking
            // GamePause rather than listing every screen means a new screen cannot be forgotten
            // here and silently strand the player in a frozen game. Hitstop is excluded - it
            // deliberately drives Time.timeScale without registering as a GamePause holder, and
            // Tick() (already run above) is what hands it back; this backstop firing mid-freeze
            // would resume time a frame after Hitstop just stopped it.
            if (Time.timeScale == 0f && !GamePause.IsPaused && !Hitstop.IsActive)
            {
                Debug.LogWarning("[Convergence] time was stopped with no screen holding it - resuming.");
                Time.timeScale = GamePause.BaseScale;
            }

            if (_state == State.Hub)
            {
                // The room drives itself; it owns the keyboard while its enlarged frame view is up.
                if (_hub != null && _hub.Blocking) return;

                if (TickDevFormat()) return;

                // Number keys still work. Kept deliberately - walking to a door is the experience,
                // but a shortcut past it costs nothing and is what makes the menu testable from
                // the CLI without driving a character around.
                for (int i = 0; i < 4; i++)
                {
                    if (!Controls.DigitTapped(i)) continue;
                    // A run waiting is offered back first - the shortcut never starts over it.
                    if (_waiting != null) AskWaitingRun();
                    else StartRun(_order[i]);
                    return;
                }
            }
            else if (_state == State.Playing)
            {
                // Esc / Start, and the loadout key too, open the paused radial menu (not the pad's
                // B, which guards in a fight - Controls.MenuTapped). The loadout
                // sheet, the carried-loot screen and abandoning the run are its three points -
                // abandoning is one option among them rather than a bare keystroke. Returning
                // here matters for the same reason it always did: the floor-clear check below
                // must not run on the frame a menu goes up.
                if (Controls.MenuTapped) { OpenPauseMenu(); return; }

                // Select (I) goes straight to the carried loot, and closing it returns to play
                // rather than to the pause menu it was never opened from.
                if (Controls.InventoryTapped && !InventoryScreen.IsOpen)
                {
                    InventoryScreen.Open(_canvas.transform, _loot);
                    return;
                }

                _alive.RemoveAll(e => e == null);
                _hud?.SetFloorInfo(_floor, _alive.Count, _player ? _player.Kills : 0, GroundGearCount());
                TickRunBeat();

                // Practice-dummy mode holds the floor open forever: its target is never in
                // _alive, so without this the floor would read as cleared on the first frame and
                // the reward screen would land on top of the thing being studied.
                //
                // Floor 0 -> 1 still has to run, because this same branch is what starts the
                // FIRST floor - blocking it outright meant the dummy was never spawned at all.
                bool dummyHoldsFloor = Tuning.Testing.PracticeDummy && _floor > 0;

                // Telemetry only - never resolves anything. See StuckWatch for why a detector
                // exists alongside player reports rather than instead of them.
                // An open exit door is a floor waiting on the player's walk to it, not a stall.
                bool settled = !dummyHoldsFloor && !_spawning && _puzzle == null && _boss == null && !FloorRewardScreen.IsOpen
                               && !MasteryScreen.IsOpen && !ConfirmDialog.IsOpen && !(ExchangeScreen.IsOpen || ChoiceList.IsOpen)
                               && _activeDoor == null;
                if (_stuckWatch != null)
                {
                    float fingerprint = _player != null && _player.Health != null ? _player.Health.Current : 0f;
                    foreach (var e in _alive)
                    {
                        if (e == null) continue;
                        var eh = e.GetComponent<Combat.Health>();
                        if (eh != null) fingerprint += eh.Current;
                    }
                    _stuckWatch.Tick(_alive.Count, settled, fingerprint);
                }

                if (_boss != null && _boss.Health != null)
                    _hud?.SetBoss(_boss.Health.Current / Mathf.Max(1f, _boss.Health.Max), _boss.DisplayName,
                                  _boss.PhaseLabel, _boss.Enraged, _boss.Health.Immune);
                else _hud?.SetBoss(-1f, null, null, false, false);

                // A BOSS HOLDS ITS FLOOR ON ITS OWN. It is not in _alive (see the field), so
                // without this the floor would read as cleared the instant the last minion died
                // and the reward screen would open over a fight still in progress.
                //
                // A Rift stands BESIDE the exit door rather than holding the floor: the door is
                // pushing on, the Rift is getting out. It is checked on its own, not in the chain
                // below - a Collapsing Rift exists DURING the fight, and inside that chain it
                // stopped the floor from ever clearing.
                // ASKED FOR, never walked into: a player who wants to keep going should be able to
                // cross the floor past an open tear without a screen stopping them.
                if (_rift != null && _rift.Usable && _rift.PlayerInside && !RiftScreen.IsOpen
                    && Controls.InteractTapped) OpenRift();
                // A dormant Red Rift breaks open only on the player's word: [ E ] at the tear, then
                // a confirmation. Walking past it never starts anything.
                else if (_rift != null && _rift.Sealed && !_redSummoned && _plan.Rift == Rifts.RiftKind.Red
                         && _rift.PlayerNear && Controls.InteractTapped) AskBreakRedSeal();
                TryOpenRedRift();
                if (_rift != null && _rift.Unstable) _hud?.SetRiftTimer(_rift.Remaining);
                if (_puzzle != null) TickPuzzle();
                if (_spire != null) TickSpire();
                if (_circle != null) TickCircle();

                // _activeDoor == null is load-bearing: once the reward is taken, OpenFloorDoor
                // spawns a door and the walk-to-it callback is the ONLY thing allowed to advance
                // the floor from here on. Without this guard, _rewardTaken stays true and the
                // floor is neither a dummy nor a Rift floor, so this branch fell all the way
                // through to StartCoroutine(NextFloor()) on every single frame the player spent
                // walking toward the door it had just opened - firing once immediately (_spawning
                // blocks the rest of that frame, but not the next one after the extra floor's own
                // wave finished spawning) and then AGAIN, unconditionally, the moment the door was
                // actually reached. Two floors' worth of hazards and enemies for one door: the
                // room visibly re-rolls once when the stray call lands and again when the door's
                // own legitimate call does.
                // A puzzle floor never clears this way - it has no wave. Solving it calls
                // OfferFloorReward itself; giving up builds the adjacent room's fight, which does.
                // Nothing is built past the Short Descent's final floor: its clear opens the ending
                // (ShowEnding), with no door - and a cleared floor without one reads, to this check, as
                // a floor whose next should be built.
                bool runComplete = Core.RunFormat.Short && _floor >= Rifts.RunLoot.FinalFloor && _rewardTaken;
                if (!dummyHoldsFloor && !runComplete && _boss == null && _activeDoor == null && _puzzle == null &&
                    !_transitioning && !_spawning && _alive.Count == 0 && _spawnQueue.Count == 0 &&
                    !FloorRewardScreen.IsOpen && !MasteryScreen.IsOpen &&
                    !ConfirmDialog.IsOpen && !(ExchangeScreen.IsOpen || ChoiceList.IsOpen))
                {
                    if (_floor > 0 && !_rewardTaken) OfferFloorReward();
                    else StartCoroutine(NextFloor());
                }

            }
            else if (_state == State.RunOver)
            {
                // Only once the screen is UP. EndRun sets RunOver and then awaits two writes before
                // building it, and a press in that gap (the ABANDON click itself, a swing) went
                // to the hub early - the screen then landed over the hub, where nothing dismisses
                // it. Not on the frame it is built either, or the press that built it closes it.
                if (_overScreen && Time.frameCount != _overScreenFrame && Controls.AnyDismiss)
                    ReturnToHub();
            }
        }

        /// <summary>
        /// The camera follows in LateUpdate, after everything that can move the player has moved
        /// it.
        ///
        /// It ran at the tail of the Playing branch in Update, which is one ordering bet stacked
        /// on another: that no other component's Update moves the player (nothing guarantees
        /// that - execution order between MonoBehaviours is unspecified unless declared), and
        /// that the branch is actually reached. It is not, on any frame taking one of the early
        /// returns above - opening the pause menu returns before it, so the camera simply held
        /// still for that frame. LateUpdate is the one place where the frame's movement is
        /// finished and nothing is left to race.
        ///
        /// CameraKick.Tick() still runs first: it is unconditional at the top of Update, so the
        /// offset this reads is the one already advanced this frame, exactly as when the call sat
        /// at the bottom of the same method. Only the WAIT changed, not the order.
        ///
        /// While paused the follow is a no-op rather than a special case - GamePause zeroes
        /// timeScale, deltaTime with it, and `1 - exp(0)` is 0, so the anchor stays where it is
        /// without this needing to know that a screen is open.
        /// </summary>
        void LateUpdate()
        {
            if (_state == State.Playing && _player && _cam) TrackCamera();
            else if (_state == State.Hub && _cam && _hub != null) UpdateHubCamera();
        }

        /// <summary>True while the camera is framing the armoury rather than the hub.</summary>
        bool _armouryView;

        /// <summary>
        /// The hub's camera is fixed; the armoury's follows the player along its wall, zoomed in
        /// to menu density and untilted - see Tuning.Armoury for why each. The room says where the
        /// player is; moving the camera stays here, with every other camera decision.
        /// </summary>
        void UpdateHubCamera()
        {
            bool want = _hub.InArmoury;
            if (want != _armouryView)
            {
                if (want)
                {
                    _armouryView = true;
                    _cam.transform.rotation = Quaternion.identity;
                    TrackArmouryCamera(snap: true);
                    return;
                }
                SetHubCamera();
            }
            if (_armouryView) TrackArmouryCamera(snap: false);
        }

        void TrackArmouryCamera(bool snap)
        {
            // Every frame rather than once on entry, so a window resized while inside re-snaps.
            // PixelPerfectZoom only recomputes when this actually changes.
            var zoom = _cam.GetComponent<PixelPerfectZoom>();
            if (zoom != null) zoom.TargetSize = Tuning.Armoury.ViewHalfHeight(Screen.height);
            else _cam.orthographicSize = Tuning.Armoury.ViewHalfHeight(Screen.height);

            // Clamped to what is drawn, centred on any axis the room is smaller than the view.
            float hh = zoom != null ? zoom.TargetSize : _cam.orthographicSize;
            float hw = hh * _cam.aspect;
            var b = _hub.ArmouryBounds;
            var p = _hub.AvatarPosition;
            float x = b.width <= hw * 2f ? b.center.x : Mathf.Clamp(p.x, b.xMin + hw, b.xMax - hw);
            float y = b.height <= hh * 2f ? b.center.y : Mathf.Clamp(p.y, b.yMin + hh, b.yMax - hh);

            var target = new Vector3(x, y, -10f);
            _cam.transform.position = snap
                ? target
                : Vector3.Lerp(_cam.transform.position, target,
                               1f - Mathf.Exp(-Tuning.Camera.Follow * Time.unscaledDeltaTime));
        }

        /// <summary>
        /// Keep the player CENTRED, stopping at the arena walls.
        ///
        /// This used to track at half the player's displacement, which is a "lead the subject"
        /// framing borrowed from side-scrollers. In a twin-stick arena it means the character
        /// drifts toward the screen edge exactly when the fight gets dangerous - at the far wall
        /// they sat a full half-view off centre, with most of the screen showing the empty middle
        /// of the arena behind them and the thing they were walking into off-screen.
        /// </summary>
        void TrackCamera()
        {
            TrackCloseView();
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            // Clamped to the FLOOR, not the walls. Clamping to the walls meant the camera stopped
            // panning long before the player did - at the east wall they sat at 93% across the
            // screen with the empty middle of the arena filling the view behind them, and whatever
            // they were walking into off-screen entirely.
            float maxX = Mathf.Max(0f, HalfWidth + FloorMargin - halfW);
            float maxY = Mathf.Max(0f, HalfHeight + FloorMargin - halfH);

            var want = new Vector3(
                Mathf.Clamp(_player.transform.position.x, -maxX, maxX),
                Mathf.Clamp(_player.transform.position.y, -maxY, maxY),
                -10f);

            // The follow runs on the camera's UNKICKED position, recovered by subtracting exactly
            // what was added last frame. Lerping the kicked position toward the target instead
            // would feed the kick back into the follow, which both eats the shove (the follow
            // chases it away) and leaves a residue behind once it decays - the camera would end
            // every fight framed slightly off centre.
            //
            // Remembered rather than recomputed because CameraKick.Offset has already moved on by
            // the time this runs; the value that has to come back off is the one that went on.
            // A domain reload zeroes it and the camera is out by one kick for a single frame.
            var anchor = _cam.transform.position - _camKick;

            // Framerate-independent, and tight: a soft follow reads as the camera lagging behind
            // the player rather than carrying them.
            // Except while the close view pulls back out: the clamp widens faster than the follow
            // catches up, and the view swung out over the void past the walls.
            anchor = _viewEase >= 0f ? want : Vector3.Lerp(
                anchor, want, 1f - Mathf.Exp(-CameraFollow * Time.deltaTime));

            _camKick = CameraKick.Offset;
            _cam.transform.position = anchor + _camKick;
        }

        /// <summary>
        /// The camera offset written last frame - see TrackCamera. Not the kick's current value:
        /// this is what has to be subtracted back off to recover where the follow actually was.
        /// </summary>
        Vector3 _camKick;

        // ------------------------------------------------------------------ run lifecycle

        /// <summary>
        /// The sigil door. Asks the stake question first when there is one to ask - a character
        /// wearing no stakeable piece walks straight through. Backing out of the question stays
        /// in the hub. The number-key shortcut skips this and runs unstaked.
        ///
        /// A RUN LEFT WAITING comes first: the gate offers it back (resume, end it, or not now)
        /// and never starts a new run over it.
        /// </summary>
        void EnterGate(ElementType element)
        {
            if (_state != State.Hub || StakeScreen == null || StakeScreen.IsOpen) return;
            if (_waiting != null) { AskWaitingRun(); return; }
            var candidates = GearStake.Candidates(_profile);
            if (candidates.Count == 0) { StartRun(element); return; }
            StakeScreen.Open(_canvas.transform, candidates, _profile.RiftBoxes,
                onChoose: (chosen, insured) => StartRun(element, chosen, insured),
                onCancel: null);
        }

        void StartRun(ElementType element) => StartRun(element, null, false);

        async void StartRun(ElementType element, MintedGearRecord stake, bool insured)
        {
            // async void swallows exceptions into the sync context, where Unity reports them with
            // useless line numbers from the state machine. Catch here so failures are legible.
            try { await StartRunAsync(element, stake, insured); }
            catch (Exception e) { Debug.LogError($"[Convergence] StartRun failed: {e}"); }
        }

        /// <summary>
        /// Everything a run (started or resumed) touches before its first await, checked by name -
        /// Start() is async, so nothing it creates is guaranteed to exist on the first frames, and
        /// async stack traces report useless line numbers and hide which field was null.
        /// </summary>
        bool ReadyForRun(string what)
        {
            string missing =
                _profile == null          ? "_profile"
              : _canvas == null           ? "_canvas"
              : MasteryScreen == null     ? "MasteryScreen"
              : ConfirmDialog == null     ? "ConfirmDialog"
              : FloorRewardScreen == null ? "FloorRewardScreen"
              : CharacterScreen == null   ? "CharacterScreen"
              : PauseScreen == null       ? "PauseScreen"
              : SettingsScreen == null    ? "SettingsScreen"
              : ControlsScreen == null    ? "ControlsScreen"
              : InventoryScreen == null   ? "InventoryScreen"
              : Mastery == null           ? "Mastery"
              : null;
            if (missing == null) return true;
            Debug.LogWarning($"[Convergence] {what} before init finished ({missing} is null) - ignored.");
            return false;
        }

        /// <summary>
        /// Through the hub's door and into the dark: the fade, every hub screen closed, the room
        /// taken down. The same door beat FloorTransition plays between floors, at the one other
        /// threshold in the game that is actually a door: the hub's own sigil door.
        /// </summary>
        async System.Threading.Tasks.Task LeaveHubAsync()
        {
            // Grabbed BEFORE TeardownRun/HideHub, which destroy the hub avatar this rig belongs to -
            // by the time the screen is black beneath, that no longer matters.
            _hub?.AvatarRig?.SetFacingAway(true);
            await RunAsTask(_fade.FadeOut(DoorFadeSeconds));

            // Idempotent: starting a run while one is live would otherwise stack a second player
            // and a second HUD on top of the first.
            TeardownRun();
            if (MasteryScreen.IsOpen) MasteryScreen.Close();
            if (TransmutationScreen.IsOpen) TransmutationScreen.Close();
            if (ShowcasePicker.IsOpen) ShowcasePicker.Close();
            if (ForgeScreen.IsOpen) ForgeScreen.Close();
            if (ManualScreen.IsOpen) ManualScreen.Close();
            if (GearDisplayScreen.IsOpen) GearDisplayScreen.Close();
            if (ExchangeScreen.IsOpen) ExchangeScreen.Close();
            if (ChoiceList.IsOpen) Choices.Close();
            if (TransmuteScreen.IsOpen) TransmuteScreen.Close();
            if (TxScreen.IsOpen) TxScreen.Close();
            if (StakeScreen.IsOpen) StakeScreen.Close();
            if (ConfirmDialog.IsOpen) ConfirmDialog.Close();
            if (PauseScreen.IsOpen) PauseScreen.Close();
            if (SettingsScreen.IsOpen) SettingsScreen.Close();
            if (ControlsScreen.IsOpen) ControlsScreen.Close();
            if (InventoryScreen.IsOpen) InventoryScreen.Close();
            HideHub();
            if (_arenaRoot) _arenaRoot.gameObject.SetActive(true);
            if (_overScreen) Destroy(_overScreen);   // starting again from the run-over screen
        }

        async System.Threading.Tasks.Task StartRunAsync(ElementType element, MintedGearRecord stake, bool insured)
        {
            if (!ReadyForRun("StartRun")) return;
            await LeaveHubAsync();

            int token = _runToken;
            _runId = Guid.NewGuid().ToString("N")[..8];
            _finalAsk = null;
            _avatarAsk = null;
            _runGuaranteeWeek = 0;
            _pendingMastery = 0;
            _runCalx = 0;
            _runCalxTaken = 0;
            _runStartTime = Time.time;
            Clock.Restore(0f);
            _floor = 0;
            if (DevStartFloor > 1)
            {
                // DEV (DevStartFloor): the run opens on a later floor - NextFloor builds _floor + 1.
                _floor = Mathf.Min(DevStartFloor, Core.RunFormat.FinalFloor) - 1;
                // The skipped floor is not a CLEARED one: the floor-start check reads "a floor with
                // its reward not taken" as a clear to pay, and would pay it.
                _rewardTaken = true;
                Debug.Log($"[Dev] this run starts on floor {_floor + 1}");
            }
            DevStartFloor = 0;
            // A LOCAL stand-in for the server's sticky run seed - the day the service issues
            // seeds, this is the one line that changes.
            _planner = new Rifts.FloorPlanner(Guid.NewGuid().GetHashCode());
            _alive.Clear();
            _spawnQueue.Clear();
            _loot.Clear();

            // The safe reserve comes into the run with the player. Spending is tracked on the run
            // and written back at the checkpoint, so a run abandoned mid-flight cannot half-spend
            // the profile's count.
            _loot.SetBankedBoxes(_profile.RiftBoxes);
            _loot.SetBankedTieredBoxes(_profile.Boxes);
            _runStartDiamondBoxes = _profile.Boxes.Diamond;
            _runStartBlackDiamondBoxes = _profile.Boxes.BlackDiamond;
            Modifiers.Clear();
            Core.RunClock.TierShiftSeconds = 0f;
            // Whole armour for every run - wear is run state (ProfileRules clears it at both
            // checkpoints too; this is the copy the player is built from). A resume never comes
            // through here: it restores the snapshot's wear.
            _profile.Wear.Entries.Clear();
            _floorBoon = -1;
            _floorBoonFloorsLeft = 0;

            // The stake is placed BEFORE the run-start checkpoint so the datum carries it - see
            // CharacterProfile.StakedInstanceId. Placing overwrites; a superseded start's stake
            // is replaced by this one's rather than forfeited, since that run never began.
            _stakeGateCleared = false;
            _stakeLine = null;
            if (stake != null && GearStake.Place(_profile, stake, insured))
            {
                Debug.Log($"[Stake] {stake.DisplayName} +{stake.UpgradeLevel} staked, gate floor {_profile.StakeGateFloor}" +
                          (insured ? ", insured with a Rift Box" : ""));
                // The insurance box came out of the banked reserve the run's loot already copied.
                _loot.SetBankedBoxes(_profile.RiftBoxes);
            }
            else
            {
                _profile.StakedInstanceId = "";
                _profile.StakeGateFloor = 0;
                _profile.StakeInsured = false;
            }

            // What the banked pools began as - a resume rebases them on the profile it finds.
            _runStartRiftBoxes = _profile.RiftBoxes;
            _runStartBoxes = CopyBoxes(_profile.Boxes);

            // A new run in flight: its own session, and nothing of the last one's writes.
            BeginRunSession();
            _runSave = null;
            _resumeGivenUp = false;

            // CHECKPOINT 1 of 2 - the only writes this prototype makes. (The run's own snapshots are
            // not checkpoints - see the continuation section.)
            await _store.BeginRunAsync(_profile, _runId, element);
            if (token != _runToken) return;   // superseded while the checkpoint was in flight

            BuildRunScene(element);
            var staked = GearStake.Staked(_profile);
            if (staked != null)
                _hud.Flash($"{staked.DisplayName} staked  -  clear floor {_profile.StakeGateFloor}, then extract");

            await RunAsTask(_fade.FadeIn(DoorFadeSeconds));
            _state = State.Playing;
        }

        /// <summary>The run's roots, its player and HUD - the same for a run started and one resumed.</summary>
        void BuildRunScene(ElementType element)
        {
            _enemyRoot = new GameObject("Enemies").transform;
            _enemyRoot.SetParent(transform, false);

            _hazardRoot = new GameObject("Hazards").transform;
            _hazardRoot.SetParent(transform, false);

            _player = BuildPlayer(element);

            // Held so teardown can unsubscribe it. An anonymous lambda could not be removed, and
            // a stale subscription is exactly how a dead run ends up ending the live one.
            _onPlayerDied = _ => EndRun(false);
            _player.Health.Died += _onPlayerDied;

            _hud = gameObject.AddComponent<Hud>();
            _hud.SetWearSource(
                () => _profile.Wear.Condition01(_profile.Gear, Art.Gear.SlotKind.Armor, _player.Stats.Armor),
                () => _player.IncomingReadout,
                () => _pendingMastery);
            _hud.SetPlatingSource(() => _plating, () => PlatingPerFloor);
            _hud.SetOathSource(OathLine);
            _hud.Build(_player, _canvas.transform);
        }

        /// <summary>
        /// Bridges one of ScreenFade's coroutines into something StartRunAsync can await, without
        /// giving ScreenFade a second, duplicated implementation of the same fade just to satisfy
        /// an async caller - FloorTransition already runs the identical coroutine directly, being
        /// a coroutine itself. Only StartRunAsync needs this; nothing else in the class mixes the
        /// two styles.
        /// </summary>
        System.Threading.Tasks.Task RunAsTask(IEnumerator routine)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            StartCoroutine(Wrap());
            return tcs.Task;

            IEnumerator Wrap()
            {
                yield return routine;
                tcs.SetResult(true);
            }
        }

        /// <summary>
        /// The class of whatever is in the weapon slot, or the greatsword when the slot is empty -
        /// an unarmed run still has to pick a way of fighting.
        /// </summary>
        static Art.Gear.WeaponClass WeaponClassOf(CharacterProfile profile)
            => EquippedWeapon(profile)?.Class ?? Art.Gear.WeaponClass.Greatsword;

        static Art.Gear.GearItem EquippedWeapon(CharacterProfile profile)
            => Art.Gear.GearCatalog.Get(profile.Gear.Get(Art.Gear.GearSlot.Weapon));

        /// <summary>
        /// The chest's defensive ability, or Dash when the slot is empty - an unarmoured run
        /// still needs an answer, same reasoning WeaponClassOf falls back to the greatsword.
        /// </summary>
        static Art.Gear.DefensiveAbility DefensiveAbilityOf(CharacterProfile profile)
        {
            var torso = Art.Gear.GearCatalog.Get(profile.Gear.Get(Art.Gear.GearSlot.Torso));
            return torso?.DefensiveAbility ?? Art.Gear.DefensiveAbility.Dash;
        }

        /// <summary>
        /// The black-diamond weapon carried in the relic socket, if any.
        ///
        /// Read from `Gear`, never from the resolved visual loadout - a relic is not drawn and a
        /// disguise grants nothing, so the two must not meet.
        /// </summary>
        static Art.Gear.GearItem EquippedRelic(CharacterProfile profile)
            => Art.Gear.GearCatalog.Get(profile.Gear.Get(Art.Gear.GearSlot.Relic));

        /// <summary>
        /// Whether the DRAWN weapon's bespoke treatment is on: its own relic (the same signature
        /// art) is in the relic socket - the Black Diamond rule, cosmetic plus relic. Gates the
        /// discs' combined picture in flight (Combat.DiscPictures; the user's call, 2026-10-16).
        /// </summary>
        static bool RelicMatchesDrawn(CharacterProfile profile, Art.Gear.GearItem shown)
        {
            var relic = EquippedRelic(profile);
            return shown != null && relic != null && !string.IsNullOrEmpty(shown.SignatureFinisher)
                   && relic.SignatureFinisher == shown.SignatureFinisher;
        }

        /// <summary>
        /// The item whose signature finisher and heat cycle the run runs on: the weapon in hand if
        /// it has one, otherwise the relic in the socket.
        ///
        /// The HAND WINS when both could supply one. Wielding a black-diamond weapon already costs
        /// the weapon slot; letting a relic override what it grants would mean the socket quietly
        /// disabling the sword you chose to hold.
        /// </summary>
        /// <summary>
        /// What grants the run's signature finisher: THE RELIC, and only the relic.
        ///
        /// This used to check the wielded weapon first and fall back to the socket, on the
        /// reasoning that wielding a Black Diamond sword already costs the weapon slot so the
        /// socket should not override it. That was the pre-split model and it quietly broke the
        /// rule the whole tier rests on - "a player holding only the relic is mechanically
        /// identical to one holding both, so the cosmetic half can trade freely without anyone
        /// buying an advantage by owning it". While the sword granted the finisher, owning the
        /// sword WAS the advantage.
        ///
        /// So the split is now clean: the RELIC is the mechanic, the WEAPON is the picture. What
        /// the matching weapon still buys is the bespoke animation - see PlayerController's
        /// SheatheDrawn and the drawn-weapon gates on every other signature effect.
        /// </summary>
        static Art.Gear.GearItem FinisherSource(CharacterProfile profile)
        {
            var held = EquippedWeapon(profile);
            var relic = EquippedRelic(profile);
            return Art.Gear.GearSlots.Accepts(Art.Gear.GearSlot.Relic, relic, held) ? relic : null;
        }

        PlayerController BuildPlayer(ElementType element)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            // Same arrival point every floor - see SouthSpawnPoint - so floor 1 does not read as
            // a different convention from the door-to-door transitions that follow it.
            go.transform.position = SouthSpawnPoint();

            // Every player-sourced DamageInfo carries this exact GameObject as its Source -
            // that's the identity DamageNumbers compares against to tell "you hit something"
            // from every other kind of damage in the game.
            DamageNumbers.SetPlayer(go);
            Hitstop.SetPlayer(go);
            CameraKick.SetPlayer(go);

            // The player is a humanoid paper-doll, not a flat sprite: gear has to be visible and
            // swappable per the economy design. An authored full-character prefab still wins.
            var rig = Art.Gear.CharacterRigFactory.Build(go, element, 10);
            rig.SetVisualScale(Tuning.Player.ArenaVisualScale);


            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.42f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = Tuning.Player.BodyDamping;
            rb.freezeRotation = true;

            // Physics runs at 50Hz; the game renders at the display rate. Without this the body
            // is only written on a physics step, so at 120fps roughly three frames in five draw
            // the character at EXACTLY the position of the frame before - motion arrives in 20ms
            // jumps that do not divide evenly into the frame interval. That is judder, and it is
            // worst on the one thing the player is always looking at.
            //
            // It matters more here than smoothness alone: the finisher timing meter is a 75ms-a-
            // segment read standing beside the body, and it is computed in Update off an honest
            // clock - the body carrying it was the part being quantised.
            //
            // Interpolate, not Extrapolate: extrapolation guesses forward and overshoots on every
            // direction change, which in a twin-stick is most of the input. The cost is that the
            // DRAWN pose trails the physics pose by up to one step - so anything wanting physics
            // truth reads rb.position, not transform.position (see FixedUpdate and the
            // Physics2D.SyncTransforms note in CLAUDE.md).
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            float baseHp = element == ElementType.Air ? Tuning.Player.HpAir
                         : element == ElementType.Earth ? Tuning.Player.HpEarth
                         : Tuning.Player.HpFireWater;
            var hp = go.AddComponent<Health>();
            Convergence.Audio.CombatSounds.AttachPlayer(hp);

            // The stat block is built HERE, before Health, because max health needs it and the
            // controller does not exist yet. Gear and the grid, summed into percentage points over
            // the Tuning.Player baselines; mastery is authored in fractions (0.05 = +5%) so it is
            // converted at this single boundary rather than by re-authoring every node.
            var stats = _profile.Gear.TotalStats();
            // The board speaks gear's language since the 2026-10-05 rebuild: its points join the
            // loadout's here and are bent with them. Range's secondary reads as Pierce on a bow.
            stats.Add(Mastery.Points(element, WeaponClassOf(_profile)));

            // The CHARACTER layer bent through its diminishing returns, once, here - so every
            // reader of the stat block gets the effective value (Art.Gear.StatCurves). The meter's
            // growth is bent further down instead, after the board's meter node joins it.
            var raw = stats;
            stats = Art.Gear.StatCurves.Character(raw);

            // The RUN layer's thresholds - the Vessel - are sized by this element's mastery level,
            // so boons cannot carry an unlevelled character past what gear and the board give.
            // The ledger also learns who the run is: element and weapon entries are offered only
            // to their own, and an entry that would change nothing (Short Chain on a one-basic
            // chain under Multiplication) is never offered.
            bool shortened = false;
            foreach (var n in Mastery.Notables(element))
                if (n == Progression.Notable.Multiplication) shortened = true;
            Modifiers.SetCharacter(element, WeaponClassOf(_profile), _profile.Mastery.For(element).Level,
                                   shortened ? -1 : 0);

            // Max health is read once, here. Thickened Hide and Thin Blood taken later in the run
            // re-apply through OnLedgerChanged rather than being polled - a max that moved every
            // frame would fight Health's own clamping of Current against it.
            //
            // The PERCENTAGE - gear's and the board's Max HP alike - scales the element's own
            // baseline, so Air and Earth keep the same RATIO of health whatever is invested; the
            // ledger's Max Health is a run-layer percentage on top (the layers multiply).
            hp.Configure(Mathf.Max(1f, Art.Gear.StatPercents.Apply(baseHp, stats.MaxHp)
                                       * Modifiers.Current.MaxHpMul));

            // A hit landing on the player shoves the DRAWING for a moment - see
            // PrimitiveCharacterRig.PlayHitReaction. The white flash was the only tell a hit ever
            // had, and a flash alone is easy to miss in the middle of a crowd.
            //
            // The direction comes from where the attacker stood, NOT from DamageInfo.Knockback:
            // enemies never knock the player back (Tuning.Enemy.AttackKnockback is 0), so that
            // vector is zero on almost every hit the player actually takes. A source that has
            // already been destroyed, or a hazard that is nowhere in particular, leaves it zero
            // and the rig simply does not move.
            if (rig is Art.Gear.PrimitiveCharacterRig steps) steps.MakesSound = true;
            if (rig is Art.Gear.PrimitiveCharacterRig primitive)
                hp.Damaged += info =>
                {
                    if (info.Amount <= 0f || info.Source == null || primitive == null) return;
                    primitive.PlayHitReaction(
                        (Vector2)(go.transform.position - info.Source.transform.position));
                };

            // Added before the controller so its Awake finds it.
            var targeting = go.AddComponent<PlayerTargeting>();
            targeting.ModsSource = () => Modifiers.Current;
            var pc = go.AddComponent<PlayerController>();
            FinisherHits.SetPlayer(pc);   // the perfect streak hears its finisher hits

            // The weapon class is read ONCE, here, from what is equipped as the run begins. See
            // PlayerController.Weapon for why it does not track later swaps.
            pc.LockWeaponClass(WeaponClassOf(_profile));

            // Same reasoning, same moment: the chest's defensive ability is a run-long commitment,
            // not something a loadout swap should be able to change mid-fight.
            pc.LockDefensiveAbility(DefensiveAbilityOf(_profile));

            // A black-diamond weapon or relic's signature finisher, in a slot the floor reward
            // cannot overwrite. Locked to the run for the same reason the class is: the loadout
            // screen opens mid-run, and a swap that removed a finisher already in the rotation
            // would be a build reset three floors in.
            var source = FinisherSource(_profile);
            pc.SetSignatureFinisher(source?.Signature, Modifiers.Current.ExtraFinisherSlots);

            // A relic below Black Diamond still gets to seed slot 3's starting pick - just
            // without locking it - so equipping one gives a real pre-run playstyle direction
            // without demanding the ceiling tier for it. Skipped whenever the line above already
            // locked the slot: a lock always wins over a mere seed, and `source` is only ever the
            // relic here when the weapon itself had nothing to lock.
            if (source != null && source.Slot == Art.Gear.GearSlot.Relic)
            {
                var granted = source.GrantedFinisher;
                if (granted != null) pc.SeedThirdSlot(granted);
            }

            // Re-applied once more: a real black-diamond signature just locked a slot above, and
            // the test harness's whole point is that nothing - including that - should be able to
            // move a slot off the pinned moveset. See PlayerController.ApplyForceMovesetOverride.
            pc.ApplyForceMovesetOverride();

            // A weapon that carries a heat cycle gets the component that tracks it. Added only
            // when the weapon asks for one, so every other sword in the game pays nothing.
            // The cycle comes with the finisher, from whichever source granted it. Conflagration
            // without its heat is a blast that never grows, so the two cannot be separated.
            if (source?.HasHeatCycle == true) pc.Heat = go.AddComponent<Combat.WeaponHeat>();

            // Shadow's chain passive comes from the SOURCE, like the heat cycle and for the same
            // reason: it is mechanics, and mechanics belong to the item that was equipped, never
            // to the one that is merely drawn. So a Shadow in the relic socket makes every swing
            // land twice while the gilded greatsword in your hand keeps its own stats and art.
            if (source?.HasEchoChain == true)
                pc.EchoHitFraction = Tuning.Shadow.ChainEchoFraction;

            // Blood Blade's fuller, the same SOURCE split as heat and echo above - the mechanic
            // belongs to whichever item was actually equipped to grant the signature.
            if (source?.HasBloodVial == true) pc.Vial = go.AddComponent<Combat.BloodVial>();

            // The rig turns to face the aim direction, so it needs no separate indicator.
            Art.Gear.CharacterRigFactory.Paint(rig, _profile);
            pc.SetRig(rig);

            // The heat repaint is wired AFTER Paint, and it keys off the weapon that is DRAWN
            // rather than the one that supplies the cycle. Two reasons, both of which cost a bug:
            //
            //   ORDER - Paint calls Apply, which repaints the weapon layer from the loadout and
            //   resets the swap's baseline. Wiring the initial stage before it was silently undone.
            //
            //   DRAWN, NOT SOURCE - the blade on screen is the one the player sees change, and it
            //   is not always the one granting the cycle. Skin Emberline over another sword and
            //   socket it as a relic and you ARE holding an Emberline blade, so it must heat;
            //   keying off the source instead left that case frozen on red. Conversely a plain
            //   sword held while Emberline sits in the socket has no stages to paint, so
            //   BladeFor returns null and nothing changes - which is also correct.
            var shown = Art.Gear.GearCatalog.Get(
                _profile.Look.Resolve(_profile.Gear).Get(Art.Gear.GearSlot.Weapon));
            if (pc.Heat != null && shown != null && shown.HasHeatCycle)
            {
                pc.Heat.Changed += stage => rig?.SetWeaponSprite(shown.BladeFor(stage));
                rig?.SetWeaponSprite(shown.BladeFor(pc.Heat.Current));
            }

            // Blood Blade's own repaint, wired the identical way and for the identical reason -
            // DRAWN, not SOURCE, and after Paint so the loadout's own reset cannot undo it.
            if (pc.Vial != null && shown != null && shown.HasBloodVial)
            {
                pc.Vial.Changed += fill => rig?.SetWeaponSprite(shown.BladeFor(fill));
                rig?.SetWeaponSprite(shown.BladeFor(pc.Vial.Fill));
            }

            // The shadow duplicate. Built whenever the run has the chain passive, but the
            // TRAILING after-image is gated on the blade being the one on screen, which is the
            // same drawn-versus-source split the heat repaint above lives by.
            //
            // Two of the three rows in that table apply here unchanged: wield Shadow and you get
            // both halves; skin Shadow over another sword and socket it and you still get both,
            // because you ARE holding a shadow blade. Socket it without the skin and every swing
            // still lands twice, with nothing following you - the cost of not wielding the thing,
            // and the same cost a socketed heat cycle pays by not repainting anything.
            if (source?.HasEchoChain == true)
            {
                var chorus = go.AddComponent<Combat.EchoChorus>();
                chorus.Owner = pc;
                chorus.Element = element;
                chorus.Paint = r => Art.Gear.CharacterRigFactory.Paint(r, _profile);
                chorus.ShowsTrail = shown != null && shown.HasEchoChain;
                pc.Echoes = chorus;
            }

            // Separatio's three figures - built whenever the run HAS the finisher (the relic
            // alone grants it), holding the drawn weapon's parts if it has any. Same drawn-versus-
            // source split as the echoes above: the move always lands, the picture follows what
            // is on screen.
            if (source?.Signature?.Finisher?.SplitsThreeWays == true)
            {
                var split = go.AddComponent<Combat.SeparatioFigures>();
                split.Owner = pc;
                split.Element = element;
                split.Paint = r => Art.Gear.CharacterRigFactory.Paint(r, _profile);
                split.Blades = shown?.SplitBlades;
                pc.Split = split;
            }

            // Quintessence's overhead picture: the DRAWN weapon's whole armillary, if it has one -
            // the relic alone grants the move, and the move always lands; the picture follows
            // what is on screen.
            pc.CombinedFrames = shown?.CombinedFrames;
            if (shown != null) pc.CombinedFrameSeconds = shown.IdleFrameSeconds;
            pc.CombinedInFlight = RelicMatchesDrawn(_profile, shown);   // and its relic socketed
            pc.ArrowSprite = Art.Gear.DemoGear.BowKit(shown).arrow;     // the drawn bow's arrow

            // The arena sorts by depth now, same as the hub. The bias is the concession to
            // readability: honest sorting means a crowd closing from below covers you exactly
            // when you most need to see yourself, so the player sorts as if a little nearer and
            // wins close calls, while still going properly behind anything clearly in front.
            DepthSorted.Attach(go, rig, bias: 0.35f);

            // What the reach rings used to say, each on the thing it is about: a rim on the locked
            // target (does a press land, and how), a glint on the weapon (a finisher is banked),
            // and - kept as a ring because it is a warning, not a readout - a leap's landing zone.
            Player.TargetHighlight.Attach(pc);
            Combat.FinisherGlint.Attach(pc);
            Player.LandingZone.Attach(pc);

            // The parry window's own tell, shared by all four defensive abilities. Attached once
            // for the run like the rings above, rather than spawned per activation - see GuardRing.
            Player.GuardRing.Attach(pc);

            // What each ability adds ON TOP of that shared window. Attached only for the one the
            // chest actually granted, which the lock above has already made a run-long fact:
            // Dash's figures cost a rig apiece, and a Barrier build should not build them to
            // never drop one. Parry Stance appears here deliberately as nothing at all - it has
            // no fallback to draw, which is exactly what buys its short cooldown.
            switch (pc.EquippedDefensiveAbility)
            {
                case Art.Gear.DefensiveAbility.Dash:
                    pc.Trail = Player.DashTrail.Attach(pc);
                    break;
                case Art.Gear.DefensiveAbility.Bulwark:
                    pc.Aura = Player.BulwarkAura.Attach(pc);
                    break;
            }

            pc.Stats = stats;
            // The unbent block too: the element's head starts are bent together with it, live.
            pc.RawStats = raw;

            // Worn weapons hit softer; the stat block hits harder.
            // The element's damage is a head start bent together with the character's own
            // (PlayerController.DamagePointsNow) - not a separate factor at each hit site.
            pc.DamageDealtMultiplier = () =>
                _profile.Wear.DamageDealtMultiplier(_profile.Gear)
                * Art.Gear.StatPercents.Apply(1f, pc.DamagePointsNow);

            // Worn armour hits back harder; Resilience blunts every hit regardless of condition;
            // Bulwark layers a flat reduction on top while it is active. Three independent
            // effects, composed here into the one number both Health and the HUD read - see
            // IncomingDamageMultiplier's own doc for why that matters.
            //
            // Graze and Brace are the two sides of one coin: mitigation earned by MOVING, and
            // mitigation earned by STANDING STILL. Asked of the body's real state (IsMoving), so a
            // finisher's lock counts as standing still.
            //
            // The run layer joins here too: the ledger's Graze and Brace as factors of their own,
            // and Patina (worn armour counts half against you).
            pc.ArmourWearMultiplier = () =>
                1f + (_profile.Wear.DamageTakenMultiplier(_profile.Gear, pc.Stats.Armor) - 1f)
                     * Modifiers.Current.ArmourWearPenaltyMul;
            pc.IncomingDamageMultiplier = () =>
            {
                var m = Modifiers.Current;
                // Under Corrosion the wear moves outside the floor (PlayerController.WearOutside).
                float wear = pc.WearOutsideFloor ? 1f : pc.ArmourWearMultiplier();
                float graze = pc.Stats.Graze;
                return wear
                       * Art.Gear.StatPercents.ReductionFactor(pc.Stats.Resilience) * m.ResilienceFactor   // Adamant
                       * (pc.IsMoving ? Art.Gear.StatPercents.ReductionFactor(graze) * m.GrazeFactor
                                      : Art.Gear.StatPercents.ReductionFactor(pc.Stats.Brace) * m.BraceFactor)
                       * (pc.BulwarkActive ? Tuning.Defense.BulwarkDamageMultiplier : 1f);
            };
            pc.MoveSpeed = Art.Gear.StatPercents.Apply(pc.MoveSpeed, pc.Stats.MoveSpeed);

            // Combo Time: how long a PARTIAL chain survives between swings.
            pc.ComboResetSeconds = Art.Gear.StatPercents.Apply(pc.ComboResetSeconds, pc.Stats.ComboTime);

            // Lifesteal comes from the board, and only from THIS element's board - a fire build's
            // investment does nothing on a water run. Read through a lambda rather than captured
            // once, so buying a node mid-session takes effect without restarting the run.
            pc.LifestealFraction = () => Mastery.Extra(element, Progression.BoardStat.Lifesteal)
                                         + Modifiers.Current.BonusLifesteal;

            // The ledger, read live. Not captured: it changes between floors.
            pc.ModsSource = () => Modifiers.Current;

            // The conditional half. Bound through a narrow facade rather than handed the whole
            // controller, so it can read what it needs and cannot drive anything it should not.
            var effects = go.AddComponent<Exchange.RunEffects>();
            effects.Bind(Modifiers, pc);
            pc.Effects = effects;

            // The element trap boons: the hazards ask, and know nothing of the exchange.
            Hazards.FloorPits.PlayerImmune = kind => effects.ImmuneTo(kind);
            Hazards.Tornado.PlayerImmune = () => effects.ImmuneToTornadoes;

            // The principle chains, scoped to the element being played - see PrincipleEffects.
            // Read ONCE here: mastery is spent in the main menu and never mid-run, so a chain
            // cannot grow a link during a fight.
            var principles = go.AddComponent<Progression.PrincipleEffects>();
            principles.Bind(
                Mastery.ChainLinks(element, Progression.Principle.Sulfur),
                Mastery.ChainLinks(element, Progression.Principle.Mercury),
                Mastery.ChainLinks(element, Progression.Principle.Salt),
                new Progression.PrincipleEffects.Facade
                {
                    Health = () => hp,
                    Resilience = () => pc.Stats.Resilience / 100f,
                    Position = () => pc.transform.position,
                    HitUnit = () => pc.HitUnit,
                });
            pc.Principles = principles;

            // The board's rules - Tinctures, Opuses, the humours - and its status power, scoped to
            // the element being played and read ONCE, like the chains above.
            var board = go.AddComponent<Progression.BoardEffects>();
            board.Bind(Mastery.Notables(element),
                Mastery.Extra(element, Progression.BoardStat.BurnPower),
                Mastery.Extra(element, Progression.BoardStat.SoakPower),
                Mastery.Extra(element, Progression.BoardStat.BleedPower),
                Mastery.Extra(element, Progression.BoardStat.StaggerPower),
                new Progression.BoardEffects.Facade
                {
                    Health = () => hp,
                    Position = () => pc.transform.position,
                    IsMoving = () => pc.IsMoving,
                    Resource = () => pc.Resource,
                    HitUnit = () => pc.HitUnit,
                    AreaScale = () => pc.AreaScaleNow,
                });
            pc.Board = board;

            // CHAINED, NOT ASSIGNED. Health.ModifyIncoming is a single delegate rather than an
            // event, so the second system to claim it silently deletes the first - and the two
            // that want it here are the exchange ledger and Salt's Ward, neither of which is
            // optional. Composed explicitly so the order is visible: the ledger's own reductions
            // resolve first, then Ward and Guard reduce what is left.
            hp.ModifyIncoming = a => board.ModifyIncoming(principles.ModifyIncoming(effects.ModifyIncoming(a)));
            // Solution: a soaked enemy's blows land lighter - asked with the hit, since it needs
            // to know who struck.
            // The ledger's own (Senescence, Acetum) needs to know who struck too.
            // The run clock's HEAT on every hit (Core.RunClock): 1 outside the Short Descent.
            hp.ScaleIncoming = info => board.ScaleIncoming(info) * effects.ScaleIncoming(info) * Core.RunClock.IncomingScale(info);
            hp.ModifyHeal = effects.ModifyHeal;
            hp.SurviveLethal = effects.SurviveLethal;

            // Salt reads the hit AFTER it resolved, so the stack that fills the Ward is itself
            // reduced by the stacks already held - see PrincipleEffects.OnDamaged.
            hp.Damaged += info => { if (!info.Price) principles.OnDamaged(info.Amount); };

            // The transmutation circle counts the blows an ENEMY lands while the player stands in
            // it; the player's own landed swings and throws come through the ledger's hook.
            hp.Damaged += info => { if (!info.Price && Exchange.RunEffects.FromEnemy(info, go)) CircleContact(); };
            // The status layer's answers to a blow (Tidewater, Backdraft).
            hp.Damaged += info => { if (!info.Price && Exchange.RunEffects.FromEnemy(info, go)) effects.OnStruckBy(info); };
            effects.Contact = CircleContact;

            // OnWeaponUsed is deliberately left unhooked: weapon degradation is off (see
            // Durability.DamageDealtMultiplier). Wearing the weapon while nothing reads the
            // condition would quietly rot saved gear for a mechanic that no longer exists.

            // Worn armour means taking more; being hit is what wears it. DamageResistance slows
            // the drain the same way the ledger's own ArmourWearMul already does - composed here
            // rather than threaded into Durability, since Durability stays ignorant of stats.
            hp.Damaged += info =>
            {
                var m = Modifiers.Current;
                if (info.Price) return;   // a price the run charges is not a blow
                float resistFactor = Art.Gear.StatPercents.ReductionFactor(pc.Stats.DamageResistance);
                float blows = Mathf.Clamp(info.Amount / (Tuning.Defense.WearReferenceHit * FloorDifficulty.Damage(_floor)),
                                          0f, Tuning.Defense.WearMaxBlows);
                // PLATING first: the floor's first blows wear nothing (Tuning.Defense.ArmorPerPlate).
                float soaked = Mathf.Min(_plating, blows);
                int platesBefore = Mathf.CeilToInt(_plating - 0.001f);
                _plating -= soaked;
                int broken = platesBefore - Mathf.CeilToInt(_plating - 0.001f);
                if (broken > 0) effects.OnPlatesBroken(broken, info);   // Patina
                blows -= soaked;
                if (blows <= 0f) return;
                _profile.Wear.Wear(_profile.Gear, Art.Gear.SlotKind.Armor,
                                   ArmorWearPerHit * blows * m.ArmourWearMul * resistFactor,
                                   pc.Stats.Armor);
            };

            ElementalResource resource = element switch
            {
                ElementType.Fire => go.AddComponent<FireResource>(),
                ElementType.Water => go.AddComponent<WaterResource>(),
                ElementType.Earth => go.AddComponent<EarthResource>(),
                _ => go.AddComponent<AirResource>(),
            };
            pc.Equip(resource);

            // Everything a release does, scaled by the ledger (Twin Spark) and by gear's
            // Elemental Power. Read live: the ledger changes between floors.
            resource.ReleaseScale = () =>
                effects.ReleaseScale * Art.Gear.StatPercents.Apply(1f, pc.Stats.ElementalEffectiveness);
            // ...and the element's standing bonuses, by the CHARACTER's Elemental Power alone.
            resource.PowerScale = () => Art.Gear.StatPercents.Apply(1f, pc.Stats.ElementalEffectiveness);
            ApplyLedgerReadouts();

            // Prism's four gems - purely cosmetic, unlike the heat cycle above: which element is
            // being played is fixed for the whole run, so this is a one-time swap rather than
            // something that needs a Changed subscription. Same DRAWN-not-SOURCE rule as the heat
            // repaint: `shown` is the resolved loadout weapon, so a Prism skin worn over another
            // sword lights up exactly as a wielded one does, and a plain sword drawn while a
            // Prism sits in the relic socket has no gems to paint - BladeFor returns null and
            // nothing changes, which is correct.
            Art.Gear.Attunement.Set(element);
            Art.Gear.CharacterRigFactory.ApplyAttunement(rig, shown, element);

            // Phantom's poof - also DRAWN, not SOURCE, and also a one-time read: which weapon is
            // on screen does not change mid-run any more than which element is being played does,
            // so this needs no Changed-style subscription either.
            pc.PhantomFlicker = shown != null && shown.HasPhantomFlicker;

            // The Sniper's idle flourish: after a finisher, if no attack follows, the gun is held
            // out and the cylinder turns. DRAWN, not SOURCE, like everything here: a Sniper skin
            // brings its cylinder with it.
            var spinAnchor = rig?.WeaponAnchor;
            if (spinAnchor != null)
            {
                if (shown != null && shown.SpinFrames is { Length: > 0 })
                {
                    var spin = Art.Gear.CylinderSpin.Attach(spinAnchor, rig, shown.SpinFrames, pc);
                    spin.SetShown(true);
                    pc.AttackStarted += spin.OnAttack;
                }
                else spinAnchor.GetComponentInChildren<Art.Gear.CylinderSpin>(true)?.SetShown(false);
            }
            pc.SheatheDrawn   = shown != null && shown.HasSheathAnimation;
            pc.GeminiHalves   = shown != null && shown.HasGeminiSplit ? shown.HalfBlades : null;

            // Phantom's haze animation. SetWeaponSprite first (establishes frame 0 immediately,
            // and calls EnsureLayers internally), WeaponAnchor read after - the same ordering
            // Prism's own wiring above already learned the hard way.
            // Also the Pacemaker's bead (GearItem.IdleFrames), through the same ticker.
            var (flipbook, flipSeconds) = shown != null ? shown.WeaponFlipbook : (null, 0f);
            if (flipbook != null)
            {
                rig?.SetWeaponSprite(flipbook[0]);
                var hazeAnchor = rig?.WeaponAnchor;
                if (hazeAnchor != null)
                    Art.Gear.PhantomHaze.Attach(hazeAnchor, rig, flipbook, flipSeconds).SetShown(true);
            }

            // None of these four mastery nodes were being read before this pass. Fire's own is
            // FireStackLife rather than a gain rate - see GainRateMultiplier's doc for why that is
            // the right lever for a discrete, one-stack-per-swing resource. Composed with gear's
            // ElementGrowth into one multiplier, same as every other stat's mastery+gear boundary.
            // The board's Element Growth is already in raw (it joined the stat block above).
            float gainRatePoints = raw.ElementGrowth;
            // Bent as ONE sum - the board's meter node and gear's Element Growth are the same stat.
            resource.GainRateMultiplier = Art.Gear.StatPercents.Apply(1f,
                Art.Gear.StatCurves.Character(Art.Gear.StatKind.ElementGrowth, gainRatePoints));

            // Read once, same as everything else off Mastery here - see BoardState.UsesSecondAbility.
            resource.UseSecondAbility = Mastery.UsesSecondAbility(element);

            return pc;
        }


        IEnumerator NextFloor()
        {
            LeaveGroundGear();
            // A Rift Box left lying stays with its floor too. It lives under _enemyRoot, which
            // outlives floors, and its collapse clock is scaled time - frozen through the
            // transition - so one dropped by the door used to lie in the next room.
            foreach (var box in FindObjectsByType<Rifts.RiftBoxPickup>(FindObjectsInactive.Exclude))
                if (box != null) { box.gameObject.SetActive(false); Destroy(box.gameObject); }
            _spawning = true;
            CloseRift();   // a Rift left unanswered closes behind the player
            ClearPuzzle();

            // THE FLOOR-START SNAPSHOT: everything the last floor settled, before this one is so
            // much as planned - a resume replays the floor from here (continuation section).
            SaveRun(Chain.RunStage.Entering);

            _floor++;
            // The run clock: this floor's attempt opens now - the service opens it at the snapshot
            // just written (Core.RunClock).
            Clock.OpenAttempt(_floor);
            _plating = PlatingPerFloor;   // the floor's plating, whole again
            _rewardTaken = false;
            _floorKills = 0;
            _floorSpawns = 0;
            _rewardRerolls = 0;
            _puzzleGivenUp = false;
            // A boss fight left part-way comes back that far down - this floor's boss only.
            _bossStartDown = IsBossFloor(_floor) ? _resumeBossDown : 0;
            _resumeBossDown = 0;
            // ...and a clock left running goes on from where it was (Tuning.Continuation.ReorientSeconds).
            _floorClockSpent = _resumeClockSpent;
            _floorRiftCollapsed = _resumeRiftCollapsed;
            _floorPlaced = _resumePlaced;
            _floorPos = _resumePos;
            _resumeClockSpent = 0f;
            _resumeRiftCollapsed = _resumePlaced = false;
            _clockFull = _clockCredit = 0f;
            // A spire's boon is the floor's, not the run's: it ends at the door - unless Lodestone
            // carries it a floor further.
            if (_floorBoonFloorsLeft > 0) _floorBoonFloorsLeft--;
            else { Modifiers.SetFloorBoon(null); _floorBoon = -1; }
            _stuckWatch?.NotifyFloorChanged();
            _fightStart = -1f;
            _plan = _planner != null ? _planner.Plan(_floor)
                                     : new Rifts.FloorPlan(_floor, Rifts.FloorCategory.Combat, Rifts.RiftKind.None, false);
            Debug.Log($"[Floor] plan {_plan}");
            DressArena(_floor);

            // The floor decides which of a kind's two looks is on screen, once, before any of
            // this floor's enemies of that kind spawn - see Enemies.EnemyLooks. Same enemy either
            // way. Every kind that currently has two looks gets rolled here; a kind with only one
            // simply has nothing that ever reads EnemyLooks.Of for it.
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Bomb);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Turret);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Chaser);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Ranged);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Dasher);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Gargoyle);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Booster);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Bubbles);
            Enemies.EnemyLooks.Roll(Enemies.EnemyKind.Mortar);


            int token = _runToken;
            // REALTIME, AND THAT IS LOAD-BEARING RATHER THAN A PREFERENCE. FloorTransition runs
            // this coroutine underneath its own GamePause.Hold, so Time.timeScale is 0 for the
            // whole of it - and a scaled WaitForSeconds never completes at timeScale 0. Scaled,
            // this deadlocked: NextFloor hung here forever, FloorTransition never reached its
            // FadeIn, and the pause it was holding was never released. The screen stayed black
            // and the game stayed frozen, with no error anywhere to say why.
            //
            // It only ever showed up on the step to FLOOR 2, which is what made it look like a
            // content bug rather than a timing one: floor 0 -> 1 starts NextFloor from
            // StartRunAsync with no pause held, so the door into floor 2 is the first time this
            // wait is ever measured against a clock that the caller has already stopped.
            //
            // The general rule, for anything added here later: the waits in NextFloor pace the
            // BUILDING of a room, not anything happening inside it. Construction must not be
            // governed by a clock that the construction itself stopped. Enemy behaviour stays
            // scaled and so stays frozen behind the black screen, which is what the Hold is for.
            yield return new WaitForSecondsRealtime(_floor == 1 ? 0.6f : 1.4f);
            if (token != _runToken || _state != State.Playing || _player == null)
            {
                _spawning = false;
                yield break;
            }

            // Practice-dummy mode short-circuits the whole wave: one target, parked, unkillable.
            // Nothing else spawns and the floor never clears, so a swing can be watched on loop.
            if (Tuning.Testing.PracticeDummy)
            {
                SpawnPracticeDummy();
                _spawning = false;
                yield break;
            }

            // A PUZZLE FLOOR has no wave and no hazards - pits under the stones would make every
            // step a second question. Its fight, if it comes to one, is built by AdjacentRoom.
            // Read once and spent, whatever the floor turns out to be - it is this floor's answer.
            bool givenUp = _resumeGivenUp;
            _resumeGivenUp = false;
            if (_plan.Category == Rifts.FloorCategory.Puzzle)
            {
                // A puzzle given up (or failed) before the run was left is not offered again: the
                // floor is its adjacent room's fight now. The puzzle is still drawn, so the
                // planner remembers its kind as it did the first time.
                if (givenUp)
                {
                    _planner?.PickPuzzle(_floor);
                    MarkPuzzleGivenUp();
                    yield return BuildFight(token);
                    yield break;
                }
                if (_hazardRoot != null)
                    foreach (Transform child in _hazardRoot) Destroy(child.gameObject);
                Hazards.RoomWalls.Apply(null, _hazardRoot);
                BuildPuzzleRoom();
                _spawning = false;
                yield break;
            }

            yield return BuildFight(token);
        }

        /// <summary>
        /// The floor's room and fight: hazards, then the boss or the wave. Split out of NextFloor
        /// so a puzzle floor's ADJACENT ROOM builds exactly the fight the floor would have had.
        /// Expects _spawning set by the caller; clears it.
        /// </summary>
        IEnumerator BuildFight(int token)
        {
            // Room layout is rerolled fresh every floor, the same "a fresh room each floor" idea
            // the enemy wave already lives by - last floor's columns/fields/lava are cleared
            // first, since unlike enemies they never clear themselves out on their own.
            // Which boss, decided once per floor before the room is built - Medusa brings her own
            // room (a ring of pillars) and the floor's usual hazards would argue with it.
            var bossKind = IsBossFloor(_floor) ? _planner.BossFor(_floor) : Bosses.BossKind.Cantor;
            bool ownRoom = IsBossFloor(_floor) && bossKind == Bosses.BossKind.Medusa;

            if (_hazardRoot != null)
            {
                foreach (Transform child in _hazardRoot) Destroy(child.gameObject);
            }
            if (ownRoom) _spire = null;

            // The room's SHAPE first: everything placed after this - pits, columns, the spire, the
            // wave's arrival points, a Rift - asks Arena.OnFloor, so the walls must already be in
            // the mask. Never on a boss floor: every boss is built round the open rectangle.
            Hazards.RoomWalls.Apply(IsBossFloor(_floor) ? null : _planner?.ShapeFor(_floor), _hazardRoot);

            // The wave is decided BEFORE the room (its own seeded System.Random, so this is the
            // same wave it always was): a floor that fields a Turret - or a Red Rift, whose guard
            // may be one - is owed a column to hide behind.
            var wave = IsBossFloor(_floor) ? null : ComposeWave(_floor);
            bool needsCover = wave != null && (_plan.Rift == Rifts.RiftKind.Red || wave.Fields(EnemyKind.Turret));

            if (_hazardRoot != null && !ownRoom)
            {
                bool hurt = _player.Health.Current < _player.Health.Max - 0.5f;
                bool worn = _profile.Wear.Condition01(_profile.Gear, Art.Gear.SlotKind.Armor,
                                                      _player.Stats?.Armor ?? 0f) < 0.995f;
                _spirePool = 0f;   // set once the wave is queued; 0 holds the spire down
                bool circle = CircleDue();
                _spire = HazardBuilder.Populate(_floor, Arena.HalfExtents, _player.transform, _hazardRoot,
                                                spireAllowed: !IsBossFloor(_floor), hurt, worn,
                                                stormAllowed: !IsBossFloor(_floor), circle: circle,
                                                needsCover: needsCover);
                _circle = HazardBuilder.LastRing;
                // Set dressing last, round everything placed - a picture only (Art.Biomes.Decor).
                Art.Biomes.Decor.Dress(_hazardRoot, (_planner != null ? _planner.SceneSeed(_floor) : _floor) + 101,
                                       Arena.HalfExtents, _spire != null ? (Vector2?)_spire.transform.position : null);
                if (_circle != null)
                    _hud?.Flash("a transmutation circle is drawn  -  land or take three blows inside it");
            }

            // A BOSS FLOOR IS THE BOSS AND NOTHING ELSE. Adding a wave underneath it would put a
            // pack of chasers between the player and a fight whose whole subject is where they are
            // standing - the hazard phase is unreadable with something else pushing you around,
            // and the stun window would be spent fighting minions instead of the boss.
            if (IsBossFloor(_floor))
            {
                SpawnBoss(bossKind);
                BeginLedgerFloor();
                _spawning = false;
                yield break;
            }

            // THE WAVE IS A BUDGET - see Enemies.WaveComposer. The floor's effective-HP pool is
            // spent on a random mix of every kind the floor has unlocked: which mix is the seed's,
            // how much killing it takes is the pool's. It replaced a count with each kind
            // subtracted from it in turn, which starved every kind introduced late - floors from
            // about 35 down were thirteen Ranged and a Chaser.
            Debug.Log($"[Floor] {Enemies.FloorDifficulty.Describe(_floor)} | pool {wave.Pool:0} ehp, " +
                      $"{wave.Spawns.Count} bodies: {wave.Roster()}");

            // Every enemy this floor will ever field is decided now and queued rather than spawned
            // outright - the on-screen cap is PRESSURE (WaveComposer.Fits), so a swarm fits a dozen
            // and elites arrive two or three at a time. Only the ACTUAL position is deferred (a
            // closure evaluated at spawn time), so a body that trickles in three kills later still
            // places against the player's CURRENT spot rather than one frozen when the floor started.
            _spawnQueue.Clear();
            foreach (var s in wave.Spawns)
                _spawnQueue.Enqueue(new PendingSpawn(s.Kind, s.Elite, PlacementFor(s.Kind)));

            // The whole wave is decided: the spire rises against its cost (see TickSpire).
            _spirePool = wave.Spent;
            _spireKilled = 0f;
            _spireRaised = false;

            // Fill up to the on-screen cap now; whatever's left waits in _spawnQueue and is
            // drained one at a time by HookDeath's Died handler as slots open.
            while (NextSpawnFits())
            {
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                SpawnQueuedEnemy();
                // Realtime for the same reason as the wait above, and it would have deadlocked
                // here next had only that one been fixed.
                yield return new WaitForSecondsRealtime(0.12f);
            }

            // The first wave is in: the clear is timed from here (scaled time, so a pause or
            // the door's fade is not counted), and a Collapsing Rift's clock starts with it.
            _fightStart = Time.time;
            BeginLedgerFloor();
            if (_plan.Rift == Rifts.RiftKind.Collapsing) OpenCollapsingRift();
            if (_plan.Rift == Rifts.RiftKind.Red) OpenRedRift();

            _spawning = false;
        }

        /// <summary>A floor's fight begins: the ledger's per-floor memories start again, and the
        /// hazards it draws are priced at this floor's depth like every other hazard.</summary>
        void BeginLedgerFloor()
        {
            Hazards.ProjectionLines.DamageScale = Enemies.FloorDifficulty.Damage(_floor);
            _player?.Effects?.OnFloorStarted();
        }

        /// <summary>Floors a captured spire's boon still has to run past its own (Lodestone).</summary>
        int _floorBoonFloorsLeft;

        // ---------------------------------------------------------------- transmutation circles

        Hazards.TransmutationRing _circle;

        /// <summary>
        /// Whether THIS floor draws a transmutation circle (the user's rules, 2026-10-05). Only
        /// while the run holds a Nigredo, and only on a combat floor: if the next Rift is RED, the
        /// circle is on that Red Rift's floor; otherwise on the LAST combat floor before the next
        /// Rift - walking back past a boss or a puzzle floor to the one before it. A player who
        /// only became eligible after that floor passed waits for the Rift after.
        /// </summary>
        bool CircleDue()
        {
            if (_planner == null || !Modifiers.HoldsNigredo || !Modifiers.CanTransmute) return false;
            if (IsBossFloor(_floor) || _plan.Category == Rifts.FloorCategory.Puzzle) return false;
            return CircleFloorFrom(_floor) == _floor;
        }

        int CircleFloorFrom(int floor)
        {
            var plans = new List<Rifts.FloorPlan> { _plan };
            plans.AddRange(_planner.PeekAhead(floor, 30));
            int searchFrom = floor;
            for (int i = 0; i < plans.Count; i++)
            {
                var rift = plans[i];
                if (rift.Rift == Rifts.RiftKind.None) continue;
                int candidate = -1;
                if (rift.Rift == Rifts.RiftKind.Red) candidate = rift.Floor;
                else
                    for (int j = i - 1; j >= 0 && plans[j].Floor >= searchFrom; j--)
                        if (plans[j].Category == Rifts.FloorCategory.Combat && plans[j].Rift == Rifts.RiftKind.None)
                        { candidate = plans[j].Floor; break; }
                if (candidate >= searchFrom) return candidate;
                searchFrom = rift.Floor + 1;   // too late for this Rift: wait for the next one
            }
            return -1;
        }

        /// <summary>A combat contact for the circle: a landed swing or throw, or a blow taken.</summary>
        void CircleContact() => _circle?.Contact();

        /// <summary>Polled while playing: the circle lit - transmute, asking which when several wait.</summary>
        void TickCircle()
        {
            if (_circle == null || !_circle.TryActivate()) return;
            var nigredos = Modifiers.Nigredos();
            if (nigredos.Count == 0) { _circle.Complete(); return; }
            if (nigredos.Count == 1) { Transmute(nigredos[0]); return; }
            TransmuteScreen.Show(_canvas.transform, nigredos, Modifiers, cost => Transmute(cost ?? nigredos[0]));
        }

        void Transmute(Exchange.ExchangeEntry cost)
        {
            var albedo = Modifiers.Transmute(cost);
            ApplyLedgerToPlayer();
            _circle?.Complete();
            OfferEvolution(null);   // a circle is one of the two places a form may evolve
            if (albedo != null)
            {
                int left = Tuning.Exchange.TransmutationsPerRun - Modifiers.TransmutedCount;
                _hud?.Flash($"{cost.Name} transmuted  -  {albedo.Name}: {albedo.Effect}  " +
                            (left > 0 ? $"({left} circle{(left == 1 ? "" : "s")} left this run)" : "(the last circle this run)"));
                Debug.Log($"[Circle] {cost.Name} -> {albedo.Name} on floor {_floor}");
            }
        }

        /// <summary>
        /// The wave for <paramref name="floor"/>, from the planner's seed. The ONE place a wave
        /// is composed - BuildFight and the Augury preview both call it, so what the preview
        /// shows is what arrives.
        /// </summary>
        Enemies.Wave ComposeWave(int floor)
        {
            // BOOSTER: gated on ACCOUNT progression, not floor depth - see Tuning.Enemy's own
            // notes. BestFloor is "how far has this character ever proven they can go", so a
            // brand-new account's early floors stay exactly as tuned.
            bool boosterEligible = _profile != null
                                 && _profile.BestFloor >= Tuning.Enemy.BoosterUnlockBestFloor
                                 && Core.RunFormat.Depth(floor) <= Tuning.Enemy.BoosterMaxFloor;
            int seed = _planner != null ? _planner.WaveSeed(floor)
                                        : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return Enemies.WaveComposer.Compose(floor, seed, floor % EliteEveryNFloors == 0, boosterEligible);
        }

        /// <summary>Where a kind arrives. The stationary kinds are placed anywhere on the floor,
        /// never walked in from the edge - they never have to close a gap to matter.</summary>
        Func<Vector2> PlacementFor(EnemyKind kind) => kind switch
        {
            EnemyKind.Turret   => () => RandomFloorPoint(Tuning.Enemy.TurretMinPlayerDistance),
            EnemyKind.Gargoyle => () => RandomFloorPoint(Tuning.Enemy.GargoyleMinPlayerDistance),
            EnemyKind.Booster  => () => RandomFloorPoint(Tuning.Enemy.BoosterMinPlayerDistance),
            EnemyKind.Bubbles  => () => RandomFloorPoint(Tuning.Enemy.BubblesMinPlayerDistance),
            _                  => () => RandomEdgePoint(),
        };

        /// <summary>
        /// Whether the queue's next body may come on screen now: the PRESSURE of what is alive
        /// plus its own under the floor's cap (Enemies.WaveComposer.Fits). Strictly in queue
        /// order - an elite at the head waits for room rather than letting cheaper bodies past.
        /// Everything alive counts, Red Rift guards included.
        /// </summary>
        bool NextSpawnFits()
        {
            if (_spawnQueue.Count == 0) return false;
            var next = _spawnQueue.Peek();
            float pressure = 0f;
            int bodies = 0;
            foreach (var e in _alive)
            {
                if (e == null) continue;
                pressure += Enemies.WaveComposer.Pressure(e.Kind, e.Elite);
                bodies++;
            }
            return Enemies.WaveComposer.Fits(new Enemies.WaveSpawn(next.Kind, next.Elite),
                                             pressure, bodies, _floor);
        }

        /// <summary>Dequeues and spawns one PendingSpawn. Caller's job to have already checked
        /// there's room (NextSpawnFits).</summary>
        void SpawnQueuedEnemy()
        {
            var next = _spawnQueue.Dequeue();
            var e = EnemyFactory.Spawn(next.Position(), next.Kind, _floor, _player.transform, _enemyRoot,
                                       elite: next.Elite);
            e.RollIndex = _floorSpawns++;   // the queue's order - the seed's, not the fight's
            Harden(e);
            HookDeath(e);
            _alive.Add(e);
        }

        /// <summary>
        /// The <see cref="Tuning.Testing.PracticeDummy"/> target: a normal enemy with everything
        /// that would interrupt an animation study switched off.
        ///
        /// Kinematic rather than merely high-poise, because a dynamic body still drifts under a
        /// pull finisher; and its health is huge rather than Immune, because Immune makes attacks
        /// MISS - no flash, no hit event - and the hit feedback is half of what is being judged.
        /// </summary>
        void SpawnPracticeDummy()
        {
            if (_player == null) return;

            var pos = (Vector2)_player.transform.position
                      + Vector2.right * Tuning.Testing.DummyDistance;
            var e = Enemies.EnemyFactory.Spawn(pos, EnemyKind.Bomb, 1, _player.transform, _enemyRoot);
            e.name = "PracticeDummy";
            e.Damage = 0f;
            e.MoveSpeed = 0f;
            e.AttackRange = 0f;

            var hp = e.GetComponent<Combat.Health>();
            hp.Configure(1e9f);
            hp.Poise = 1f;

            var rb = e.GetComponent<Rigidbody2D>();
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;

            // Deliberately NOT added to _alive: the floor-clear check counts that list, and a
            // target that never dies would otherwise keep the floor permanently unfinished
            // while every other system believed a fight was still in progress.
            Debug.Log("[Convergence] PRACTICE DUMMY mode - one immortal target, no waves. " +
                      "Tuning.Testing.PracticeDummy = false to restore normal play.");
        }

        /// <summary>
        /// Floor cleared: heal, a random moveset, or repair. The moveset card is offered only
        /// when there is actually one to give - at the cap, or with every moveset already drawn,
        /// it shows as unavailable rather than burning the pick.
        /// </summary>
        /// <summary>
        /// Floor cleared. The exchange row goes up FIRST and dissolves into the floor reward.
        ///
        /// That order is the design: the reward is a gift, the exchange is a bargain, and a
        /// bargain read after you have already been handed something free stops being a decision.
        /// </summary>
        void OfferFloorReward()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.FloorClear);
            _rewardTaken = true;
            // The run clock stops at the clear - the menus after it are off it (Core.RunClock).
            Clock.CloseAttempt();

            // The spire sinks into the floor, taken or not - see Spire.Sink.
            if (_spire != null) _spire.Sink();
            // An unlit transmutation circle fades with the fight; its Nigredo waits for another.
            if (_circle != null && !_circle.Spent) _circle.Fade();
            _circle = null;
            // No enemies left: the storm stops forming and its funnels die away, harmless.
            TornadoStorm.CalmAll();

            // The stake's gate. Counted as a CLEARED floor here rather than read off _floor at run
            // end, because _floor is the floor the run ended ON, which a death leaves uncleared -
            // and whatever system ends a run extracted, a floor cleared is a floor cleared.
            if (!_stakeGateCleared && _profile.StakeGateFloor > 0 && _floor >= _profile.StakeGateFloor)
            {
                _stakeGateCleared = true;
                _hud?.Flash("stake gate cleared  -  extract to claim the matching piece");
                Debug.Log($"[Stake] gate floor {_profile.StakeGateFloor} cleared");
            }

            // The exit door goes up HERE rather than after the deal/reward flow finishes - it
            // used to spawn only once ApplyFloorReward ran, so the player never saw it until both
            // screens had already come and gone. GamePause holds timeScale for the whole exchange
            // + reward flow (see ExchangeScreen/FloorRewardScreen's own Hold/Release), so spawning
            // it early costs nothing: nobody can walk anywhere until the screens release the
            // pause, but the door is standing there the moment the floor actually cleared instead
            // of popping in afterward.
            // THE SHORT DESCENT ENDS ON ITS FINAL FLOOR (SHORT-DESCENT.md): no door, no deal, no
            // reward - its box is paid and every box secured (RollFloorDrop), then the run ends as an
            // extraction. The avatar's dialogue and a cut scene go here when built. Classic carries on.
            bool runEnds = Core.RunFormat.Short && _floor >= Rifts.RunLoot.FinalFloor;
            if (!runEnds) OpenFloorDoor();
            RollFloorDrop();
            int lying = GroundGearCount();
            if (lying > 0)
                _hud?.Flash(lying == 1 ? "a piece of gear is still on the floor"
                                       : $"{lying} pieces of gear are still on the floor");
            SettleFloorRift();
            if (runEnds)
            {
                SaveRun(Chain.RunStage.Cleared, pending: 0);   // the run clock's last floor closes (the service times it)
                Debug.Log($"[Run] floor {_floor} cleared - the {Core.RunFormat.WireName} run is complete");
                ShowEnding();
                return;
            }

            // Floor-clear costs settle FIRST, so the exchange row is read against the health you
            // actually have left rather than the health you had a moment before Toll took its cut.
            // The ledger's own counters move with the floor (Withering, Viriditas, Senescence).
            if (_player != null) _player.Effects?.OnFloorCleared();
            Modifiers.FloorCleared();
            ApplyLedgerToPlayer();

            // Mend: worn gear knits a little back together every floor (Corrosion halves it).
            if (_player != null && _player.Stats.Mend > 0f)
                _profile.Wear.Repair(_profile.Gear, _player.Stats.Mend / 100f * Modifiers.Current.RepairMul,
                                     _player.Stats.Armor);

            // THE CLEAR'S SNAPSHOT: what the clear itself paid is now the run's, and a resume from
            // here comes back to this room with the deal and the reward still to come - asked of
            // the same seeds, so they are the same deal and the same rings.
            SaveRun(Chain.RunStage.Cleared, pending: 2);
            OfferDeal();
        }

        /// <summary>
        /// THE SHORT DESCENT's ending (UI.EndingScreen): the final avatar's words, the light, the
        /// card - then the run ends as an extraction. Skippable; the run is already written down as
        /// cleared, so a tab closed during it is finished by the resume (ResumeClearedRoom).
        /// </summary>
        void ShowEnding()
        {
            var ending = GetComponent<UI.EndingScreen>();
            if (ending == null) ending = gameObject.AddComponent<UI.EndingScreen>();
            int token = _runToken;
            ending.Show(_canvas.transform, Core.Narrative.FinalAvatarWords, Clock.Seconds, () =>
            {
                if (token == _runToken && _state == State.Playing) EndRun(survived: true);
            });
        }

        /// <summary>
        /// The deal after a clear, when the floor has one, then the reward - from OfferFloorReward,
        /// or from a resume into a cleared room whose deal was still waiting.
        /// </summary>
        void OfferDeal()
        {
            // Scrying Glass. Read HERE, after SettleFloorRift: the floor below's plan depends on
            // whether this floor's Rift was reached, and nothing between now and the door moves it.
            string scry = Modifiers.StacksOf("scrying_glass") > 0 ? ScryNextFloor() : null;
            if (scry != null) Debug.Log($"[Exchange] Scrying Glass: {scry}");

            // A deal after the first floor and every second floor after it (50 a run) - see
            // Tuning.Exchange.DealEvery. Drawn from the run's seed for this floor, so a restart
            // meets the same deals.
            // THE BRAND DEAL (Phase 4): after a mini-boss, one unrefusable pair - a form against a
            // brand - whether or not the floor would have had an ordinary deal.
            bool brand = Exchange.ExchangeOffers.BrandDealAfter(_floor) && _floor < Core.RunFormat.FinalFloor;
            if ((!Exchange.ExchangeOffers.DealAfterInRun(_floor) && !brand) || _planner == null)
            {
                if (scry != null) _hud?.Flash(scry);
                OfferFloorRewardCards();
                return;
            }
            // After a BOSS, a form held with its catalyst may evolve first (Phase 5).
            if (Core.RunFormat.Depth(_floor) > 0 && Rifts.FloorPlanner.IsBossFloor(_floor) && !_evolutionAsked
                && Modifiers.Evolvable().Count > 0)
            {
                _evolutionAsked = true;
                OfferEvolution(OfferDeal);
                return;
            }
            _evolutionAsked = false;
            var offer = brand
                ? Exchange.ExchangeOffers.BuildBrand(Modifiers, new System.Random(_planner.DealSeed(_floor)))
                : Exchange.ExchangeOffers.Build(Modifiers, _floor, new System.Random(_planner.DealSeed(_floor)));
            if (brand) _hud?.Flash("a brand deal - one pair, a form against a rule");
            if (offer.Pairs.Count == 0)
            {
                if (scry != null) _hud?.Flash(scry);
                OfferFloorRewardCards();
                return;
            }

            // Transmuter's Eye. The reward is rolled ONCE, here, and the same roll is both
            // previewed and later handed to the reward screen - a preview that re-rolled would
            // be a lie, and is the obvious way to get this wrong.
            _pendingReward = RollFloorReward(_rewardRerolls);
            string preview = RingsAfter(_floor) && Modifiers.StacksOf("transmuters_eye") > 0
                ? $"next: {string.Join(" / ", System.Linq.Enumerable.Where(new[] { _pendingReward.Arts[0]?.DisplayName, _pendingReward.Arts[1]?.DisplayName }, n => n != null))}" +
                  $"  or  {RestoreText(_pendingReward.Restore, _pendingReward, 0)}  or  {RestoreText(_pendingReward.Second, _pendingReward, 0)}"
                : null;

            // Oracle: each slate shows the deal that would follow it - built on a copy of the ledger
            // with the seed the real one will use.
            var oracle = !brand && Modifiers.StacksOf("oracle") > 0
                ? Exchange.ExchangeOffers.PreviewNext(Modifiers, offer, _floor, f => _planner.DealSeed(f))
                : null;

            ExchangeScreen.Show(_canvas.transform, offer, Modifiers, _floor, preview, pair =>
            {
                int token = _runToken;
                // The deal is answered: a resume from here shows only the reward.
                void Answered()
                {
                    if (token != _runToken || _state != State.Playing) return;
                    SaveRun(Chain.RunStage.Cleared, pending: 1);
                    OfferFloorRewardCards();
                }

                if (pair != null)
                {
                    // An ART form is inscribed on the rotation socket the player picks, and its
                    // socket cost goes on the same socket (EXCHANGE.md 5.3). Must be answered.
                    if (pair.Boon != null && pair.Boon.Slot == Exchange.FormSlot.Art && _player != null && _player.Slots.Count > 0)
                    {
                        AskSocket(pair.Boon, socket => { TakePair(pair, socket); Answered(); });
                        return;
                    }
                    TakePair(pair, -1);
                }
                else
                {
                    if (offer.CanRefuse) Modifiers.Refuse();
                    Debug.Log($"[Exchange] refused ({Modifiers.RefusalsLeft} refusals left)");
                    // A refusal may also take a form out of a stacked slot - the refusal and the
                    // deal are its price (the user's call, 2026-10-20).
                    var removable = offer.CanRefuse ? Modifiers.RemovableForms() : null;
                    if (removable != null && removable.Count > 0)
                    {
                        AskRemoval(removable, Answered);
                        return;
                    }
                }
                Answered();
            }, scry, oracle);
        }

        /// <summary>
        /// The OATHS' line on the HUD (Phase 4): each sworn oath and how far along it is - and the
        /// moment one is fulfilled, said once.
        /// </summary>
        string OathLine()
        {
            var done = Modifiers.LastFulfilled;
            if (done != null)
            {
                Modifiers.ClearLastFulfilled();
                _hud?.Flash($"OATH KEPT · {done.Name}");
            }
            string line = _player != null && _player.Effects != null ? _player.Effects.GrowthLine : null;   // Kindling, Swell
            if (Modifiers.StacksOf("blood_debt") > 0)
                line = (line != null ? line + "      " : "") + $"BLOOD DEBT  {Mathf.FloorToInt(Modifiers.OathProgress("blood_debt"))} / {Tuning.Exchange.BloodDebtWorth:0}";
            if (Modifiers.StacksOf("unbloodied") > 0)
                line = (line != null ? line + "      " : "") + "UNBLOODIED  clear a floor unhit";
            return line;
        }

        bool _evolutionAsked;

        /// <summary>
        /// EVOLUTION (Phase 5): every form held with its catalyst may grow into its evolution - asked
        /// after a circle's transmutation and after a boss. Declining keeps it for the next chance.
        /// </summary>
        void OfferEvolution(System.Action then)
        {
            var ready = Modifiers.Evolvable();
            if (ready.Count == 0) { then?.Invoke(); return; }
            var answers = new List<(string, string)>();
            foreach (var evo in ready)
                answers.Add(($"{Exchange.ExchangeCatalog.Get(evo.EvolvesFrom)?.Name}  >>  {evo.Name}", evo.Effect));
            // The process NAMED, never explained (the user's calls, 2026-10-21).
            Choices.Show(_canvas.transform, "CAUDA PAVONIS", "",
                         answers, i =>
                         {
                             var evo = ready[i];
                             Modifiers.Evolve(evo);
                             ApplyLedgerToPlayer();
                             _hud?.Flash($"CAUDA PAVONIS · {evo.Name}");
                             Debug.Log($"[Exchange] evolved {evo.EvolvesFrom} into {evo.Id}");
                             then?.Invoke();
                         }, "Not now", then);
        }

        /// <summary>A pair taken: boon first (so a cost reshaping the next offer can't be undone by a
        /// boon in the same breath), then the cost; a form and a socket cost inscribed where they go.</summary>
        void TakePair(Exchange.ExchangePair pair, int socket)
        {
            if (pair.Boon != null) { Modifiers.Take(pair.Boon); Modifiers.Inscribe(pair.Boon, socket); }
            if (pair.Cost != null) { Modifiers.Take(pair.Cost); Modifiers.Inscribe(pair.Cost, socket); }
            ApplyLedgerToPlayer();
            // A UNION formed by this take (two opposite humours held level) is announced.
            var union = Modifiers.LastUnion;
            if (union != null) _hud?.Flash($"UNION · {union.Name}");
            Debug.Log($"[Exchange] took {pair.Boon?.Name ?? "nothing"} / {pair.Cost?.Name ?? "nothing"}" +
                      (socket >= 0 ? $" on socket {socket}" : ""));
        }

        UI.ChoiceList Choices
        {
            get
            {
                if (_choices != null) return _choices;
                _choices = GetComponent<UI.ChoiceList>();   // never ?? - a missing component isn't C# null
                if (_choices == null) _choices = gameObject.AddComponent<UI.ChoiceList>();
                return _choices;
            }
        }
        UI.ChoiceList _choices;

        /// <summary>Which art of the rotation an Art form is inscribed on - each with the forms
        /// already there and what every one would run at once this joins.</summary>
        void AskSocket(Exchange.ExchangeEntry form, System.Action<int> onPick)
        {
            var answers = new List<(string, string)>();
            for (int i = 0; i < _player.Slots.Count; i++)
            {
                var forms = Modifiers.FormsIn(Exchange.FormSlot.Art, i);
                string art = _player.Slots[i]?.DisplayName ?? "an art";
                string held = forms.Count == 0 ? "no forms yet - it runs at full strength"
                    : $"{string.Join(", ", forms.ConvertAll(f => f.Name))} - with this one, each runs at " +
                      $"{Mathf.RoundToInt(Exchange.RunModifiers.ShareFor(forms.Count + 1) * 100f)}%";
                answers.Add(($"{i + 1}.  {art}", held));   // numbered: one art can sit in two sockets
            }
            Choices.Show(_canvas.transform, $"Inscribe {form.Name}", "Which art carries it? Its cost goes on the same art.",
                         answers, onPick);
        }

        /// <summary>After a refusal: take a form out of a stacked slot, or keep them all.</summary>
        void AskRemoval(List<Exchange.ExchangeEntry> removable, System.Action then)
        {
            var answers = new List<(string, string)>();
            foreach (var f in removable)
            {
                int socket = Modifiers.SocketOf(f.Id);
                string where = f.Slot == Exchange.FormSlot.Art && _player != null && socket >= 0 && socket < _player.Slots.Count
                    ? $"on {_player.Slots[socket]?.DisplayName}" : $"on your {f.Slot.ToString().ToLowerInvariant()}";
                answers.Add(($"Take out {f.Name}", where + " - gone for the run; the others there grow stronger"));
            }
            Choices.Show(_canvas.transform, "Take a form out?", "Your refusal can also take one form out of a slot that holds two or more.",
                         answers, i =>
                         {
                             Modifiers.RemoveForm(removable[i]);
                             Debug.Log($"[Exchange] took out {removable[i].Name}");
                             then();
                         }, "Keep them all", then);
        }

        /// <summary>
        /// What Scrying Glass shows: the floor below's kind, and its whole roster when it is a
        /// fight. Both come from the same calls that will BUILD that floor - FloorPlanner.Peek
        /// (the plan, drawn on a copy so nothing moves) and ComposeWave (the same seed) - so the
        /// preview cannot drift from what arrives. A boss is not named: which boss is a shuffle
        /// bag that a peek would have to draw from.
        /// </summary>
        string ScryNextFloor()
        {
            int next = _floor + 1;
            if (_planner == null || next > Core.RunFormat.FinalFloor) return null;
            var plan = _planner.Peek(next);
            string rift = plan.Rift != Rifts.RiftKind.None ? $"  -  and a {plan.Rift} Rift" : "";
            return plan.Category switch
            {
                Rifts.FloorCategory.Boss   => $"below: a boss{rift}",
                Rifts.FloorCategory.Puzzle => "below: a puzzle",
                _                          => $"below: {ComposeWave(next).Roster()}{rift}",
            };
        }

        /// <summary>
        /// Push ledger values that are READ ONCE rather than polled - currently only max health,
        /// because Health clamps Current against Max and a maximum that moved every frame would
        /// fight that clamp.
        /// </summary>
        void ApplyLedgerToPlayer()
        {
            // Sealed Sands (a brand): the heat is read a tier ahead of the run clock.
            Core.RunClock.TierShiftSeconds = Modifiers.StacksOf("sealed_sands") > 0 ? Tuning.RunClock.TierSeconds : 0f;
            if (_player == null || _player.Health == null) return;

            float baseHp = _player.Resource != null && _player.Resource.Element == ElementType.Air
                    ? Tuning.Player.HpAir
                : _player.Resource != null && _player.Resource.Element == ElementType.Earth
                    ? Tuning.Player.HpEarth
                : Tuning.Player.HpFireWater;

            // Same shape as the initial Configure above: percentage on the element's baseline,
            // the ledger's percentage on top. If one moves, move both.
            float want = Mathf.Max(1f,
                Art.Gear.StatPercents.Apply(baseHp, _player.Stats?.MaxHp ?? 0f)
                * Modifiers.Current.MaxHpMul);
            _player.Health.SetMax(want);

            // The wheel is the other value that is read once rather than polled. Derived from the
            // ledger for the same reason max health is: Mods is recomputed from every entry held,
            // so a second Extra Sigil has to arrive as arithmetic, not as a one-shot that already
            // fired.
            _player.SyncSlotCount(Modifiers.Current.ExtraFinisherSlots);
            ApplyLedgerReadouts();
        }

        /// <summary>
        /// What the ledger hides or dims, pushed to the systems that draw it: Fog III's readouts,
        /// Murk's telegraphs, Sol Niger's dark and Lucid's outlines. Statics on the drawers, set
        /// whenever the ledger changes and reset when a run ends.
        /// </summary>
        void ApplyLedgerReadouts()
        {
            var m = Modifiers.Current;
            DamageNumbers.Hidden = m.HideEnemyReadouts;
            ArmorRing.Hidden = m.HideEnemyReadouts;
            Enemies.EnemyController.TelegraphStrength = m.TelegraphStrength;
            if (_player != null)
            {
                Exchange.SolNiger.Set(_player.transform, m.SilhouetteBeyond);
                Player.LucidRims.Set(_player, Modifiers.StacksOf("lucid") > 0);
            }
        }

        /// <summary>
        /// The floor's reward, rolled once so it can be previewed and then handed over unchanged:
        /// the FOUR RINGS of UI.FloorRewardScreen - two weapon arts (up, down), a restore (left)
        /// and Experience-or-Calx (right). Only the KINDS are fixed here; amounts are sized at the
        /// screen (OfferFloorRewardCards), after the floor's taper is counted.
        /// </summary>
        class RolledReward
        {
            /// <summary>The two weapon art rings, distinct; null where the class ran out.</summary>
            public Combat.Moveset[] Arts = new Combat.Moveset[2];
            /// <summary>The left ring: Heal or Repair (whichever would help), else the other of
            /// Xp/Calx, else a third art (<see cref="ThirdArt"/>, kind Moveset), else Heal.</summary>
            public UI.FloorReward Restore;
            public Combat.Moveset ThirdArt;
            /// <summary>The right ring: Xp, or Calx while the depth allowance has one.</summary>
            public UI.FloorReward Second;
            public float HealAmount;
        }

        RolledReward _pendingReward;

        /// <summary>The Repair card's fraction with Repair Received applied - that card only.</summary>
        float RepairFractionNow => RepairFraction
            * Art.Gear.StatPercents.Apply(1f, _player != null ? _player.Stats.RepairReceived : 0f)
            * Modifiers.Current.RepairMul;   // Corrosion

        /// <summary>The reward rings, seeded from the run (Rifts.RunRolls) - <paramref name="reroll"/> is
        /// the Curator reroll it is, so a floor replayed after a resume is offered the same rings.</summary>
        RolledReward RollFloorReward(int reroll)
        {
            using (Rifts.RunRolls.Seeded(_planner?.Seed ?? 0, _floor, Rifts.RunRolls.Kind.Reward, reroll))
                return RollFloorRewardNow();
        }

        RolledReward RollFloorRewardNow()
        {
            var arts = _player != null
                ? Combat.MovesetLibrary.RandomOffers(_player.Slots, _player.SlotLocked, _player.Weapon,
                                                     Tuning.FloorRewards.ArtOffers + 1)
                : new List<Combat.Moveset>();
            var r = new RolledReward
            {
                // Heal Received boosts THIS card and nothing else - not lifesteal, not boons.
                HealAmount = _player != null
                    ? _player.Health.Max * HealFraction * Art.Gear.StatPercents.Apply(1f, _player.Stats.HealReceived)
                    : 0f,
            };
            for (int i = 0; i < 2; i++) r.Arts[i] = i < arts.Count ? arts[i] : null;
            var spare = arts.Count > 2 ? arts[2] : null;

            bool calxOk = CalxAllowanceLeft() > 0;
            r.Second = calxOk && UnityEngine.Random.value < Tuning.FloorRewards.CalxChance
                ? UI.FloorReward.Calx : UI.FloorReward.Xp;

            // The restore ring: only a kind that would help. A full player is offered the other
            // of Xp/Calx beside it, then a third art; Heal only when nothing else is left.
            var helpful = new List<UI.FloorReward>(2);
            if (_player != null && _player.Health.Current < _player.Health.Max - 0.5f)
                helpful.Add(UI.FloorReward.Heal);
            // Armour only: weapons no longer wear.
            if (_profile.Wear.Condition01(_profile.Gear, Art.Gear.SlotKind.Armor, _player?.Stats.Armor ?? 0f) < 0.995f)
                helpful.Add(UI.FloorReward.Repair);

            if (helpful.Count > 0) r.Restore = helpful[UnityEngine.Random.Range(0, helpful.Count)];
            else if (r.Second == UI.FloorReward.Calx) r.Restore = UI.FloorReward.Xp;
            else if (calxOk) r.Restore = UI.FloorReward.Calx;
            else if (spare != null) { r.Restore = UI.FloorReward.Moveset; r.ThirdArt = spare; }
            else r.Restore = UI.FloorReward.Heal;
            return r;
        }

        /// <summary>The Experience card's mastery XP at the given taper - see Tuning.FloorRewards.</summary>
        int XpCardAmount(float taper)
            => Mathf.Max(1, Mathf.RoundToInt(MasteryXpPerFloor * taper * Tuning.FloorRewards.XpCardFloorShare));

        string RestoreText(UI.FloorReward kind, RolledReward r, int xpAmount) => kind switch
        {
            UI.FloorReward.Heal    => $"+{Mathf.RoundToInt(r.HealAmount)} HP",
            UI.FloorReward.Repair  => $"+{Mathf.RoundToInt(RepairFractionNow * 100f)}% condition",
            UI.FloorReward.Calx    => $"+{Tuning.FloorRewards.CalxPerCard} {Tuning.FloorRewards.CalxTier} calx",
            UI.FloorReward.Moveset => r.ThirdArt != null ? r.ThirdArt.DisplayName : "weapon art",
            _                      => xpAmount > 0 ? $"+{xpAmount} mastery XP" : "mastery XP",
        };

        /// <summary>One ring, worded. <paramref name="mastery"/> is what the floor itself paid.</summary>
        UI.FloorRewardScreen.Option RingOption(UI.FloorReward kind, Combat.Moveset art, RolledReward r,
                                               int xpAmount, int mastery, ElementType element)
        {
            switch (kind)
            {
                case UI.FloorReward.Moveset:
                    return art == null
                        ? new() { Kind = kind, Enabled = false, Value = "No art", Blurb = "You already hold every weapon art." }
                        : new() { Kind = kind, Art = art, Enabled = true, Value = art.DisplayName, Blurb = art.FinisherDescription };
                case UI.FloorReward.Heal:
                {
                    float max = _player != null ? _player.Health.Max : 0f;
                    float cur = _player != null ? _player.Health.Current : 0f;
                    float after = Mathf.Min(max, cur + r.HealAmount);
                    return new()
                    {
                        Kind = kind, Enabled = true, Value = RestoreText(kind, r, xpAmount),
                        Note = $"{Mathf.CeilToInt(cur)} → {Mathf.CeilToInt(after)} / {Mathf.CeilToInt(max)}",
                        Blurb = "Patches you up. Does nothing for worn armour.",
                    };
                }
                case UI.FloorReward.Repair:
                {
                    float now = _profile.Wear.Condition01(_profile.Gear, Art.Gear.SlotKind.Armor, _player?.Stats.Armor ?? 0f);
                    return new()
                    {
                        Kind = kind, Enabled = true, Value = RestoreText(kind, r, xpAmount),
                        Note = $"ARMOUR {Mathf.RoundToInt(now * 100f)}%",
                        Blurb = "Restores worn armour. Does nothing for your health.",
                    };
                }
                case UI.FloorReward.Calx:
                    return new()
                    {
                        Kind = kind, Enabled = true,
                        Value = $"+{Tuning.FloorRewards.CalxPerCard} {Tuning.FloorRewards.CalxTier}",
                        Note = $"CARRIED {_runCalx} → {_runCalx + Tuning.FloorRewards.CalxPerCard}",
                        Blurb = $"Carried. Kept if you extract, lost if you die. " +
                                $"{Tuning.GearRoll.CalxPerBox} make a {Tuning.FloorRewards.CalxTier} box.",
                    };
                default:
                    return new()
                    {
                        Kind = UI.FloorReward.Xp, Enabled = true, Value = $"+{xpAmount} XP",
                        Note = $"{element.ToString().ToUpperInvariant()} MASTERY",
                        Blurb = $"Banked when the run ends, on top of the {mastery} this floor already paid.",
                    };
            }
        }

        /// <summary>The four rings in screen order: up, down, left, right.</summary>
        List<UI.FloorRewardScreen.Option> RingOptions(RolledReward r, int xpAmount, int mastery, ElementType element)
            => new()
            {
                RingOption(UI.FloorReward.Moveset, r.Arts[0], r, xpAmount, mastery, element),
                RingOption(UI.FloorReward.Moveset, r.Arts[1], r, xpAmount, mastery, element),
                RingOption(r.Restore, r.ThirdArt, r, xpAmount, mastery, element),
                RingOption(r.Second, null, r, xpAmount, mastery, element),
            };

        /// <summary>
        /// The reward rings follow this floor's clear: every floor in Classic; in the Short Descent
        /// every SECOND floor (the user's call) - the EVEN ones, so every mini-boss is followed by a
        /// pick. The deal still comes every floor (Exchange.ExchangeOffers.DealAfterInRun).
        /// </summary>
        static bool RingsAfter(int floor) => !Core.RunFormat.Short || floor % 2 == 0;

        void OfferFloorRewardCards()
        {
            // THE DAILY TAPER, and it applies to XP ONLY - see Tuning.Taper for why loot is
            // deliberately left alone. Read and counted PER FLOOR rather than once at run end, so
            // a long session feels the curve within the run that crosses the allowance rather than
            // being surprised by it afterwards.
            //
            // Counted before the multiplier is read, so the floor being paid for is itself the
            // nth floor of the day rather than the (n-1)th.
            float taper = 1f;
            if (_account != null)
            {
                _account.CountFloorCleared();
                taper = Tuning.Taper.Multiplier(_account.FloorsClearedToday);
            }

            int mastery = Mathf.RoundToInt(MasteryXpPerFloor * taper);
            _pendingMastery += mastery;

            // No rings on this floor (the Short Descent's come every second floor): its XP above is
            // paid, and the floor is fully paid here.
            if (!RingsAfter(_floor))
            {
                _pendingReward = null;
                SaveRun(Chain.RunStage.Cleared, pending: 0);
                SaveAccount();
                return;
            }

            // Reuses the roll the exchange row may already have shown. Rolling again here would
            // make Transmuter's Eye show one thing and deliver another.
            var r = _pendingReward ?? RollFloorReward(_rewardRerolls);
            _pendingReward = null;

            // Sized HERE, after the taper was counted for this floor, so the card pays at the
            // same rate the floor did. (The roll may be older - Transmuter's Eye rolls it before
            // the deal - but only the KIND is fixed there, never the amount.)
            int xpAmount = XpCardAmount(taper);
            var element = _player && _player.Resource ? _player.Resource.Element : ElementType.Fire;

            // Curator: a reroll of all four rings per stack, spent on the screen. The heal amount
            // rides each roll, so the callback reads whichever roll is showing.
            // A reroll spent is written down at once, so leaving and resuming shows the roll it
            // ended on - not the first one back, which would make a reroll free to take back.
            var showing = r;
            System.Func<List<UI.FloorRewardScreen.Option>> reroll = () =>
            {
                _rewardRerolls++;
                showing = RollFloorReward(_rewardRerolls);
                ResaveRerolls();
                return RingOptions(showing, xpAmount, mastery, element);
            };

            FloorRewardScreen.Show(
                _canvas.transform, _floor, RingOptions(r, xpAmount, mastery, element),
                _player != null ? _player.Slots : System.Array.Empty<Combat.Moveset>(),
                _player != null ? _player.SlotLocked : _ => false,
                _player != null ? _player.RotationIndex : 0,
                element, reroll, Mathf.Max(0, Modifiers.Current.ExtraRewardOptions - _rewardRerolls),
                (outcome, slot, art) => ApplyFloorReward(outcome, slot, art, showing.HealAmount, xpAmount));
        }

        void ApplyFloorReward(UI.FloorOutcome outcome, int slot, Combat.Moveset offered, float healAmount,
                              int xpAmount)
        {
            switch (outcome)
            {
                case UI.FloorOutcome.Heal:
                    if (_player != null)
                    {
                        _player.Health.Heal(healAmount);
                        Spr.Flash(_player.transform.position, 1.2f, new Color(0.45f, 0.9f, 0.55f), 0.5f);
                    }
                    break;

                case UI.FloorOutcome.Xp:
                    // Element mastery, banked with the floors' own at the run-end checkpoint.
                    _pendingMastery += xpAmount;
                    Debug.Log($"[Mastery] reward card +{xpAmount} xp (run total {_pendingMastery})");
                    break;

                case UI.FloorOutcome.Calx:
                    // Carried, not banked: EndRun adds it to the profile only on an extraction.
                    int give = Mathf.Min(Tuning.FloorRewards.CalxPerCard, CalxAllowanceLeft());
                    _runCalx += give;
                    _runCalxTaken += give;
                    Debug.Log($"[Convergence] reward card +{give} calx (carrying {_runCalx}, {_runCalxTaken} taken this run)");
                    break;

                case UI.FloorOutcome.Repair:
                    int repaired = _profile.Wear.Repair(_profile.Gear, RepairFractionNow,
                                                         _player?.Stats.Armor ?? 0f);
                    Debug.Log($"[Convergence] repaired {repaired} piece(s) of gear");
                    break;

                case UI.FloorOutcome.MovesetInto:
                    if (_player != null && offered != null && _player.SetSlot(slot, offered))
                        Debug.Log($"[Convergence] slot {slot + 1} -> '{offered.DisplayName}' " +
                                  $"(earned {_player.EarnedCount}/{_player.Slots.Count})");
                    break;

                case UI.FloorOutcome.Forfeit:
                    // Backed out at the confirm step. The reward is spent regardless - that is
                    // what makes taking the finisher card a commitment rather than a free look.
                    Debug.Log("[Convergence] floor reward forfeited (swap cancelled)");
                    break;
            }

            // No OpenFloorDoor() call here any more - it already went up in OfferFloorReward,
            // before the exchange row ever showed, so the door is standing there for the whole
            // deal-and-reward flow rather than popping in only once both screens are done.

            // The floor is fully paid: a resume from here is the cleared room and nothing else. The
            // taper's count rides the account, written with it - the floor it counted is now one
            // a resume cannot pay again.
            SaveRun(Chain.RunStage.Cleared, pending: 0);
            SaveAccount();
        }

        FloorDoor _activeDoor;

        /// <summary>
        /// True while reordering the finisher wheel is open - the one crack in "all gear locked
        /// mid-run", scoped to exactly this window rather than being available whenever the
        /// character sheet is open.
        /// </summary>
        public bool ReorderOpen => _activeDoor != null;

        /// <summary>
        /// Replaces the old flat WaitForSeconds before NextFloor with a real place to walk to.
        /// Parented under _enemyRoot so a run ending mid-walk (the player dying, or abandoning)
        /// tears the door down the same way it already tears down every enemy - deactivated
        /// before the deferred Destroy, so its Update stops the same frame.
        ///
        /// Called from OfferFloorReward, before the deal/reward screens ever show - idempotent
        /// against a stray second call (there isn't one any more, but a door already open must
        /// never be replaced out from under a player who might already be standing at it).
        /// </summary>
        /// <summary>
        /// Where a floor is entered from - the same offset off the SOUTH wall that
        /// <see cref="OpenFloorDoor"/> mirrors off the north one, so a player always arrives
        /// facing into open room rather than into whatever the door itself last stood in front
        /// of. Shared by <see cref="FloorTransition"/> and by the run's very first floor, or the
        /// two would drift the moment either one's offset was retuned. Backed by
        /// <see cref="Arena.SouthSpawnPoint"/> so HazardBuilder can exclude the same spot from the
        /// column lattice rather than only ever protecting wherever the player happens to stand.
        /// </summary>
        static Vector3 SouthSpawnPoint() => Arena.SouthSpawnPoint;

        /// <summary>
        /// The door was already open and correctly detected touch - see DoorFadeSeconds. This is
        /// the one place a floor actually changes under a black screen: the character turns to
        /// face away as they step through (see ICharacterRig.SetFacingAway - a scripted beat, not
        /// combat facing), the screen covers the cut, and the new floor's build - the existing
        /// NextFloor coroutine, untouched - runs entirely behind it. Position is reset to
        /// SouthSpawnPoint BEFORE NextFloor runs, not after: hazard placement reads the player's
        /// transform for clearance (see FloorPit's own note on Large pits centring on the player
        /// spawn), and populating against the OLD position would silently place them wrong.
        /// </summary>
        const float DoorFadeSeconds = 0.45f;

        IEnumerator FloorTransition()
        {
            // Held for the whole transition so enemies/physics can't simulate behind the black
            // screen - ScreenFade itself doesn't freeze anything (see its own note), and without
            // this the new floor's enemies were spawning and acting in real time under NextFloor,
            // landing hits before the fade-in had even finished revealing the room to the player.
            // ScreenFade runs on unscaled time for exactly this reason, so the fade itself still
            // animates while paused.
            // Set BEFORE the first yield, so there is never a frame between the door being
            // reached and this being true - the whole point of it is to close a gap measured in
            // frames. See the field's own note.
            _transitioning = true;
            GamePause.Hold(this);

            // try/finally, because EVERYTHING BETWEEN THE FADE OUT AND THE FADE IN HAPPENS WITH
            // THE SCREEN BLACK AND THE GAME FROZEN, and this coroutine holds the only handle to
            // undoing either. Anything that stops it partway - an exception out of NextFloor,
            // StopAllCoroutines during teardown - otherwise leaves the black screen up and the
            // pause held with nothing left running that could release them, which is not a bug
            // the player can recover from without relaunching.
            //
            // This is a backstop and not a fix for anything in particular: the deadlock that
            // prompted it is fixed at its cause (see the realtime waits in NextFloor). It is here
            // because the COST of this failure is total, so it should not depend on every future
            // line of NextFloor being correct. A floor that fails to build now drops the player
            // into a half-built room, which is survivable and reportable; a black screen is not.
            try
            {
                _player?.Rig?.SetFacingAway(true);
                yield return _fade.FadeOut(DoorFadeSeconds);

                if (_player != null)
                {
                    _player.transform.position = SouthSpawnPoint();
                    Physics2D.SyncTransforms();   // see the physics-read trap this file already documents
                    _player.Rig?.SetFacingAway(false);
                }

                yield return NextFloor();
                yield return _fade.FadeIn(DoorFadeSeconds);
            }
            finally
            {
                _transitioning = false;
                GamePause.Release(this);
                // A no-op on the normal path - FadeIn has already run and the panel is clear.
                _fade.Clear();
            }
        }

        void OpenFloorDoor()
        {
            if (_activeDoor != null) return;
            if (_player == null) { StartCoroutine(NextFloor()); return; }

            int token = _runToken;
            var pos = Arena.NorthDoorPoint;
            _activeDoor = FloorDoor.Spawn(pos, _player.transform, _enemyRoot, ScreenElement(), () =>
            {
                _activeDoor = null;
                if (token != _runToken) return;
                StartCoroutine(FloorTransition());
            });
        }

        /// <summary>
        /// Floors that hold a boss: every mini-boss interval, plus the avatar floors.
        ///
        /// WHICH boss is Rifts.FloorPlanner.BossFor: mini-boss floors draw from a pool (the Cantor
        /// and Medusa so far) as a seeded shuffle bag; the four avatars are all still the Cantor,
        /// standing in until they are their own set-pieces. A new boss is a Bosses.Boss subclass,
        /// a BossKind, and a case in Boss.Spawn.
        /// </summary>
        static bool IsBossFloor(int floor) => Rifts.FloorPlanner.IsBossFloor(floor);

        /// <summary>The reincarnation avatars. Separate from IsBossFloor because the run economy
        /// keys the loot bands and the Black Diamond roll off these four specifically.</summary>
        static bool IsAvatarFloor(int floor) => Rifts.FloorPlanner.IsAvatarFloor(floor);

        void SpawnBoss(Bosses.BossKind kind)
        {
            if (_player == null) return;

            // Spawned at the centre so the fight opens where it will later be vulnerable - the
            // middle of the room has to mean one thing all fight, and the player should have seen
            // it there once before being asked to run to it.
            _boss = Bosses.Boss.Spawn(kind, Vector2.zero, _player.transform, _enemyRoot, _floor);
            // A boss fights at the heat of its arrival, to the end (SHORT-DESCENT.md).
            Clock.LockHeat();
            Debug.Log($"[Floor] Boss floor {_floor}: {_boss.DisplayName}");

            // A fight left part-way and resumed: the boss comes back down the eighths it had lost,
            // rounded in its favour (Tuning.Continuation.BossSteps). Its own logic reads health live
            // (the enrage, the window cap), so starting lower needs nothing else.
            if (_bossStartDown > 0 && _boss.Health != null)
            {
                int steps = Tuning.Continuation.BossSteps;
                _boss.Health.Restore(_boss.Health.Max * (steps - _bossStartDown) / steps);
                Debug.Log($"[Run] {_boss.DisplayName} resumed at {steps - _bossStartDown}/{steps} health");
            }
            _beatBossDown = _bossStartDown;

            var hp = _boss.Health;
            hp.Died += h =>
            {
                if (_player) _player.RegisterKill();
                // The board hears every death, whatever caused it - a hit, a burn, a fall.
                if (_player && _player.Board) _player.Board.OnEnemyDied(h);
                if (_player && _player.Effects) _player.Effects.OnEnemyDied(h, Tuning.Exchange.KillWorthCap);
                Spr.Flash(h.transform.position, 3.2f, new Color(1f, 0.95f, 0.8f), 0.9f);

                // Cease BEFORE destroying, so the fight's coroutine and any lit slice stop on the
                // frame it dies rather than at the end of it - Destroy is deferred, and a phrase
                // that kept firing after the boss was dead would damage the player during the
                // reward screen.
                _boss.Cease();
                _boss = null;
                Destroy(h.gameObject);
            };
        }

        /// <summary>How many pieces a Rift will take. The capacity domain is on the mastery board
        /// and is read live, so a point spent between runs is felt on the next one.</summary>
        int RiftCapacity()
            => Tuning.Boss.BaseRiftCapacity
             + Mathf.RoundToInt(Mastery.Extra(_profile.LastElement, Progression.BoardStat.RiftCapacity));

        void OpenRiftOnFloor()
        {
            if (!TearRift()) return;
            _hud?.SetRiftPrompt(true);
        }

        bool TearRift(float distance = 4.5f)
        {
            if (_rift != null || _player == null) return false;

            // Torn where the player is NOT, so walking to it is a real decision rather than
            // something that happens to them - the whole fixture is a question and they should
            // have to go and answer it.
            var at = Arena.Clamp((Vector2)_player.transform.position
                                 + UnityEngine.Random.insideUnitCircle.normalized * distance, 2f);
            // Never inside a spire's ring: the Rift's [ E ] and the capture would fight over
            // the same ground. Pushed straight out past the rim.
            if (_spire != null)
            {
                var off = at - _spire.Centre;
                float need = Tuning.Spire.CaptureRadius + 1.2f;
                if (off.magnitude < need)
                    at = Arena.Clamp(_spire.Centre + (off.sqrMagnitude > 0.0001f ? off.normalized : Vector2.up) * need, 2f);
            }
            // Off the room's walls - a tear half inside one could not be reached to answer.
            at = Arena.NearestFloor(at, 1.2f);
            _rift = Rifts.Rift.Open(at, _arenaRoot, _player.transform);
            return true;
        }

        /// <summary>
        /// A Collapsing Rift, torn as the floor's first wave arrives, on the clock the planner
        /// fits to this player. It does NOT reset the planner's Rift count here - only reaching
        /// it does (SettleFloorRift), so a collapse is followed by better odds, not a reset.
        /// </summary>
        void OpenCollapsingRift()
        {
            // Run out before the run was left: it stays run out, and its quiet stretch stands - a
            // quit must not buy the race again.
            if (_floorRiftCollapsed)
            {
                _planner?.MarkRiftCollapsed(_floor);
                _hud?.Flash("the Rift had already collapsed");
                Debug.Log($"[Floor] Collapsing Rift on floor {_floor} had run out before the run was left");
                return;
            }
            if (!TearRift()) return;
            // A race left part-way goes on from where it was (plus a moment to find one's feet)
            // rather than starting again - against the replayed floor's fresh wave.
            float full = _planner.CollapseSeconds(_floor);
            _clockFull = full;
            _clockCredit = _floorClockSpent > 0f ? Mathf.Min(Tuning.Continuation.ReorientSeconds, _floorClockSpent) : 0f;
            float seconds = Mathf.Max(Tuning.Continuation.ReorientSeconds, full - _floorClockSpent + _clockCredit);
            if (_floorClockSpent > 0f)
                Debug.Log($"[Run] Collapsing Rift resumed with {seconds:0.0}s of {full:0.0}s ({_floorClockSpent:0.0}s spent)");
            Debug.Log($"[Floor] Collapsing Rift on floor {_floor}: {seconds:0.0}s " +
                      $"(estimate {Rifts.FloorPlanner.EstimateSeconds(_floor):0.0}s, " +
                      $"{_planner.PaceRatios.Count} clears timed)");
            _rift.BeginCollapse(seconds, () =>
            {
                _rift = null;
                _hud?.SetRiftTimer(-1f);
                _hud?.Flash("the Rift collapsed");
                _planner?.MarkRiftCollapsed(_floor);
                Debug.Log($"[Floor] Collapsing Rift on floor {_floor} ran out - " +
                          $"no Rift for {_planner?.QuietFloors} floor(s)");
                // Written down at once - a resume must find it run out, not race it again.
                ResaveEntering(s => s.RiftCollapsed = true);
            });
        }

        /// <summary>
        /// A Red Rift: torn SHUT as the first wave arrives, and DORMANT. Nothing comes for the
        /// player until they break the seal at the tear and confirm (AskBreakRedSeal); then its
        /// guard of elites arrives, and the Rift opens once that guard has fallen. Never touched, it
        /// stays shut - no exit on this floor, and nothing lost. Torn farther off than an open Rift
        /// so a guard called mid-fight does not arrive on top of the player.
        /// </summary>
        void OpenRedRift()
        {
            if (!TearRift(Tuning.Floors.RedTearDistance)) return;
            _rift.Seal();
            _redSummoned = false;
            _redGuard ??= new();
            _redGuard.Clear();
            _hud?.SetRedRiftPrompt(true);
            Debug.Log($"[Floor] Red Rift on floor {_floor}, sealed until the player breaks it");
        }

        int RedGuardCount => Tuning.Floors.RedGuards + (Core.RunFormat.Depth(_floor) >= Tuning.Floors.RedGuardsExtraFloor ? 1 : 0);

        void AskBreakRedSeal()
        {
            int guards = RedGuardCount;
            ConfirmDialog.Show(_canvas.transform, "Break the seal?",
                $"Its guard of {guards} elite{(guards == 1 ? "" : "s")} will come for you. " +
                "The Rift opens once they fall. Leave it shut and nothing comes.",
                "Break it", "Not now", SummonRedGuard);
        }

        /// <summary>
        /// The seal broken: the guard comes. Called BEFORE the floor clears, it joins _alive and
        /// holds the floor like any enemy - the reward cannot open over a fight still going. Called
        /// AFTER, the exit door is already up and the guard is optional: walking out through the
        /// door leaves the Rift shut, and CloseRift takes the guard with it.
        /// </summary>
        void SummonRedGuard()
        {
            if (_rift == null || !_rift.Sealed || _redSummoned || _player == null) return;
            _redSummoned = true;
            _redGuard ??= new();
            _hud?.SetRedRiftPrompt(false);

            int guards = RedGuardCount;
            Vector2 at = _rift.transform.position;
            for (int i = 0; i < guards; i++)
            {
                // Spread round the tear, a little in front of it.
                float a = (i + 0.5f) / guards * Mathf.PI * 2f;
                var pos = Arena.NearestFloor(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f, 0.8f);
                var kind = Enemies.WaveComposer.EliteKinds[UnityEngine.Random.Range(0, Enemies.WaveComposer.EliteKinds.Length)];
                var e = EnemyFactory.Spawn(pos, kind, _floor, _player.transform, _enemyRoot, elite: true);
                e.RollIndex = RedGuardRollBase + i;
                Harden(e);
                HookDeath(e);
                _redGuard.Add(e.GetComponent<Health>());
                if (!_rewardTaken) _alive.Add(e);
            }
            _hud?.Flash("THE SEAL BREAKS  -  its guard must fall before the Rift opens");
            Debug.Log($"[Floor] Red Rift on floor {_floor}: seal broken, {guards} elite guard(s)" +
                      (_rewardTaken ? " after the clear" : ""));
        }

        /// <summary>
        /// Opens a Red Rift whose guard has fallen. Polled, so a guard called after the clear opens
        /// it the moment the last one drops; and only once the floor is clear, so a guard called
        /// mid-fight opens it with the clear, like every other exit. Only an opened one counts as
        /// an exit reached.
        /// </summary>
        void TryOpenRedRift()
        {
            if (_plan.Rift != Rifts.RiftKind.Red || _rift == null || !_rift.Sealed || !_redSummoned || !_rewardTaken)
                return;
            if (_redGuard != null)
                foreach (var g in _redGuard)
                    if (g != null && !g.IsDead) return;

            _rift.Unseal();
            _hud?.SetRiftPrompt(true);
            _planner?.MarkRiftReached();
            Debug.Log($"[Floor] Red Rift on floor {_floor} opened - its guard has fallen");
            // Opened after the clear: a resume stands the cleared room up with the Rift open.
            ResaveCleared();
        }

        /// <summary>
        /// The floor just cleared pays out - carried, not banked. AT THE CLEAR, not when the next
        /// floor starts: it used to be rolled at the top of NextFloor, so a player who extracted at
        /// this floor's Rift left without the drop they had just earned.
        /// </summary>
        void RollFloorDrop()
        {
            if (_floor <= 0) return;
            // Only a BOSS floor pays on the clear - a guaranteed box. Every other floor's gear came
            // off its enemies (TryDropGear); see Tuning.GearRoll.EnemyGearDropChance.
            // Floor 100's box is the STORE's decision - it carries the weekly guarantee.
            // Seeded from the run (Rifts.RunRolls): a boss fought again after a resume pays the
            // same box, and an avatar the same design-box rolls.
            using var rolls = Rifts.RunRolls.Seeded(_planner?.Seed ?? 0, _floor, Rifts.RunRolls.Kind.Clear, 0);
            if (_floor == Rifts.RunLoot.FinalFloor) ClaimFinalFloor();
            else if (IsBossFloor(_floor))
            {
                var tier = _loot.RollBossBox(_floor);
                Debug.Log($"[Rift] boss floor {_floor} paid a {tier} box " +
                          $"(carrying {_loot.CarriedCount}, secured {_loot.SecuredCount})");
                if (!Chain.DesignDrops.IsDesignTier(tier)) _hud?.Flash($"{tier.ToString().ToUpper()} BOX");
            }

            // An avatar's own design boxes, on top of the floor's drop - the STORE's decision (the
            // service's, for a service player), asked inside the seeded scope so a local save's
            // roll is the same on a replay.
            if (Rifts.RunLoot.IsRolledAvatar(_floor)) ClaimAvatarBoxes(_floor);

            // FLOOR 100 SECURES THE RUN'S BOXES (the user's call, 2026-10-09) - the completion
            // floor has no Rift, and the boxes it pays are the point of reaching it. From here a
            // death still banks them, and the run end mints the design boxes to the wallet.
            if (_floor == Rifts.RunLoot.FinalFloor)
            {
                _loot.SecureAllBoxes();
                _hud?.Flash($"floor {Rifts.RunLoot.FinalFloor} cleared  -  every box is secured");
            }
        }

        /// <summary>
        /// FLOOR 100's BOX, asked of the store - the service decides it for a service player (the
        /// weekly guarantee: Chain/FinalFloorReward). It arrives after the clear's own flash; a
        /// failed ask is left for EndRun to ask again (the service answers the decision it
        /// recorded, never a reroll).
        /// </summary>
        async void ClaimFinalFloor()
        {
            var ask = _finalAsk = _store.FinalFloorRewardAsync(_profile, _runId);
            FinalFloorReward? reward = null;
            try { reward = await ask; }
            catch (Exception e) { Debug.LogWarning($"[Rift] floor {Rifts.RunLoot.FinalFloor}'s box: {e.Message}"); }
            if (_finalAsk != ask) return;   // EndRun has settled it already
            if (reward == null)
            {
                _hud?.Flash($"floor {Rifts.RunLoot.FinalFloor}'s box is waiting on the service");
                return;
            }
            CreditFinalFloor(reward);
        }

        /// <summary>
        /// An AVATAR's boxes, asked of the store - the service decides them for a service player
        /// (Chain/AvatarBoxes) and the run's end mints only what it recorded. They arrive after the
        /// clear's own flash; a failed ask is left for EndRun to ask again (the service answers the
        /// decision it recorded, never a reroll).
        /// </summary>
        async void ClaimAvatarBoxes(int floor)
        {
            var ask = _avatarAsk = _store.AvatarBoxesAsync(_profile, _runId, floor);
            _avatarAskFloor = floor;
            AvatarBoxes? boxes = null;
            try { boxes = await ask; }
            catch (Exception e) { Debug.LogWarning($"[Rift] floor {floor}'s avatar boxes: {e.Message}"); }
            if (_avatarAsk != ask) return;   // EndRun has settled it already
            if (boxes == null)
            {
                Debug.Log($"[Rift] floor {floor}'s avatar boxes are waiting on the store - asked again at the run's end");
                return;
            }
            CreditAvatarBoxes(boxes.Value);
        }

        /// <summary>EndRun's half: an outstanding or failed ask is awaited, then asked once more,
        /// and credited before anything banks.</summary>
        async System.Threading.Tasks.Task SettleAvatarBoxesAsync()
        {
            var ask = _avatarAsk;
            if (ask == null) return;
            int floor = _avatarAskFloor;
            AvatarBoxes? boxes = null;
            try { boxes = await ask; } catch { /* asked again below */ }
            if (_avatarAsk != ask) return;   // ClaimAvatarBoxes credited it meanwhile
            if (boxes == null)
                try { boxes = await _store.AvatarBoxesAsync(_profile, _runId, floor); }
                catch (Exception e) { Debug.LogWarning($"[Rift] floor {floor}'s avatar boxes, asked again: {e.Message}"); }
            _avatarAsk = null;
            if (boxes != null) CreditAvatarBoxes(boxes.Value);
        }

        /// <summary>An avatar's boxes into the run's loot - FOUND, carried at risk like every box
        /// until a Rift, extraction or floor 100 secures it. No answer at all pays nothing: nothing
        /// was recorded, so the run's end could not mint it anyway.</summary>
        void CreditAvatarBoxes(AvatarBoxes boxes)
        {
            _avatarAsk = null;
            for (int i = 0; i < boxes.Diamond; i++) _loot.FindTieredBox(Art.Gear.LootTier.Diamond);
            for (int i = 0; i < boxes.BlackDiamond; i++) _loot.FindTieredBox(Art.Gear.LootTier.BlackDiamond);
            if (boxes.BlackDiamond > 0) _hud?.Flash("BLACK DIAMOND BOX");
            else if (boxes.Diamond > 0) _hud?.Flash("DIAMOND BOX");
            if (boxes.Diamond > 0 || boxes.BlackDiamond > 0)
            {
                ResaveCleared();   // the boxes are the run's now - a resume must not ask for them again
                Debug.Log($"[Rift] floor {boxes.Floor}'s avatar paid {boxes.Diamond} Diamond, {boxes.BlackDiamond} Black Diamond");
            }
        }

        /// <summary>EndRun's half: an outstanding or failed ask is awaited, then asked once more,
        /// and credited before anything banks.</summary>
        async System.Threading.Tasks.Task SettleFinalFloorAsync()
        {
            var ask = _finalAsk;
            if (ask == null) return;
            FinalFloorReward? reward = null;
            try { reward = await ask; } catch { /* asked again below */ }
            if (_finalAsk != ask) return;   // ClaimFinalFloor credited it meanwhile
            if (reward == null)
                try { reward = await _store.FinalFloorRewardAsync(_profile, _runId); }
                catch (Exception e) { Debug.LogWarning($"[Rift] floor {Rifts.RunLoot.FinalFloor}'s box, asked again: {e.Message}"); }
            CreditFinalFloor(reward);
        }

        /// <summary>Floor 100's box into the run's loot, secured like every box floor 100 pays. No
        /// answer at all pays the least Gold it could have, and leaves the week's guarantee
        /// unspent - nothing was recorded against it.</summary>
        void CreditFinalFloor(FinalFloorReward? answered)
        {
            _finalAsk = null;
            var reward = answered ?? FinalFloorReward.Unanswered;
            _loot.FindTieredBoxes(reward.Tier, reward.Count);
            _loot.SecureAllBoxes();
            if (reward.Guaranteed) _runGuaranteeWeek = reward.Week;
            string boxes = reward.Count > 1 ? $"{reward.Count} {reward.Tier.ToString().ToUpper()} BOXES"
                                            : $"{reward.Tier.ToString().ToUpper()} BOX";
            if (reward.Tier == Art.Gear.LootTier.BlackDiamond) boxes = "BLACK DIAMOND BOX";
            _hud?.Flash(reward.Guaranteed ? $"WEEKLY {boxes}" : boxes);
            ResaveCleared();   // the box is the run's now - a resume must not ask for it again
            Debug.Log($"[Rift] floor {Rifts.RunLoot.FinalFloor} paid {reward.Count} {reward.Tier}" +
                      (reward.Guaranteed ? $" (the weekly guarantee, week {reward.Week})" : "") +
                      (answered == null ? " - no answer from the store, the guarantee is still unspent" : ""));
        }

        /// <summary>
        /// The floor just cleared: time the clear for the Collapsing clock, then give the floor its
        /// exit if it has one. Reaching an exit - any Blue, a Collapsing Rift beaten in time, the
        /// avatar's guaranteed one - is what resets the planner's Rift count.
        /// </summary>
        void SettleFloorRift()
        {
            if (_planner == null) return;
            if (!IsBossFloor(_floor) && _fightStart >= 0f)
                _planner.RecordClear(_floor, Time.time - _fightStart);
            _fightStart = -1f;

            switch (_plan.Rift)
            {
                case Rifts.RiftKind.Blue:
                    OpenRiftOnFloor();
                    _planner.MarkRiftReached();
                    break;
                case Rifts.RiftKind.Collapsing:
                    if (_rift != null && _rift.Unstable)
                    {
                        _rift.Stabilise();
                        _hud?.SetRiftTimer(-1f);
                        _hud?.SetRiftPrompt(true);
                        _planner.MarkRiftReached();
                        Debug.Log($"[Floor] Collapsing Rift on floor {_floor} held with {_rift.Remaining:0.0}s left");
                    }
                    break;
                case Rifts.RiftKind.Red:
                    // A guard called before the clear was part of the wave, so a cleared floor is a
                    // fallen guard and the Rift opens now. Never called, the tear stays shut and
                    // can still be broken after the clear (see SummonRedGuard).
                    TryOpenRedRift();
                    break;
            }
        }

        void OpenRift()
        {
            _hud?.SetRiftPrompt(false);
            RiftScreen.Open(_canvas.transform, _loot, RiftCapacity(), _floor, _rift,
                onExtract: () =>
                {
                    // Extraction: everything carried comes out, and the run ends on the player's
                    // own terms rather than on a death.
                    _loot.SecureAll();
                    CloseRift();
                    EndRun(survived: true);
                },
                onPushOn: () =>
                {
                    // NOT YET: whatever was secured stays secured, the rest stays carried and at
                    // risk, and the tear stays open - the exit door is how the run goes on, and
                    // until the player takes it they may come back and leave after all.
                    if (_rift != null) _hud?.SetRiftPrompt(true);
                    ResaveCleared();   // what was secured, kept by a resume
                });
        }

        void CloseRift()
        {
            if (_rift != null) { Destroy(_rift.gameObject); _rift = null; }

            // A Red Rift's guard goes with it. One called after the clear is not in _alive, and
            // enemies never clear themselves out, so it would follow the player to the next floor.
            if (_redGuard != null)
            {
                foreach (var g in _redGuard)
                    if (g != null && !g.IsDead) Destroy(g.gameObject);
                _redGuard.Clear();
            }
            _redSummoned = false;

            _hud?.SetRiftPrompt(false);
            _hud?.SetRedRiftPrompt(false);
            _hud?.SetRiftTimer(-1f);
        }

        // ------------------------------------------------------------------ puzzle floors

        /// <summary>This floor's puzzle, while it is a puzzle floor and the player is still in
        /// the puzzle room. Like the boss and the Rift it is not in _alive and holds the floor on
        /// its own. See Tuning.Puzzle.</summary>
        Puzzles.PuzzleRoom _puzzle;

        /// <summary>This floor's spire, or null. A UnityEngine.Object reference, so it survives a
        /// domain reload; destroyed with the hazard root when the next room is built.</summary>
        Hazards.Spire _spire;

        /// <summary>The floor's wave cost and how much of it has been killed - what the spire's
        /// rise is measured against. Set when the wave is queued; a share of the COST rather
        /// than of the bodies, so a floor of five elites is not one kill from its spire.</summary>
        float _spirePool, _spireKilled;
        bool _spireRaised;

        /// <summary>
        /// POLLED, not called back (the puzzle's reason). Heal and Repair land once, here; every
        /// other boon is folded into the ledger for the rest of the floor via SetFloorBoon, which
        /// NextFloor drops.
        /// </summary>
        void TickSpire()
        {
            // THE FLOOR EVENT: it rises once a share of the floor's enemies are dead - mid-fight,
            // so taking it is a choice made with a pack still on the floor, not a stroll after.
            // Named as it rises: colour is what draws the player there, and the name is what
            // tells a colourblind player the same thing.
            if (!_spireRaised && _spirePool > 0f && _player != null)
            {
                if (_spireKilled > 0f && _spireKilled >= _spirePool * Tuning.Spire.RiseAtDefeatedFraction)
                {
                    _spireRaised = true;
                    _spire.Rise();
                    _hud?.Flash($"a spire of {Hazards.SpireBoons.NameOf(_spire.Boon)} rises  -  " +
                                Hazards.SpireBoons.Describe(_spire.Boon));
                }
            }

            if (!_spire.TryClaim(out var boon) || _player == null) return;

            switch (boon)
            {
                case Hazards.SpireBoon.Heal:
                    _player.Health.Heal(_player.Health.Max * Tuning.Spire.HealFraction);
                    break;
                case Hazards.SpireBoon.Repair:
                    _profile.Wear.Repair(_profile.Gear,
                                         Tuning.Spire.RepairFraction * Modifiers.Current.RepairMul,
                                         _player.Stats?.Armor ?? 0f);
                    break;
                default:
                    Modifiers.SetFloorBoon(Hazards.SpireBoons.Apply(boon));
                    _floorBoon = (int)boon;   // a delegate can't be snapshotted; the boon can
                    _floorBoonFloorsLeft = Modifiers.Current.SpireExtraFloors;   // Lodestone
                    break;
            }
            _hud?.Flash($"spire of {Hazards.SpireBoons.NameOf(boon)} taken  -  {Hazards.SpireBoons.Describe(boon)}");
            Debug.Log($"[Spire] captured {boon} on floor {_floor}");
        }
        /// <summary>The floor's exit, SHUT until the puzzle is solved. Becomes _activeDoor then.</summary>
        FloorDoor _puzzleDoor;
        /// <summary>The way to the adjacent room - giving up and failing both end here.</summary>
        FloorDoor _sideDoor;
        bool _puzzleSettled;

        void BuildPuzzleRoom()
        {
            if (_player == null || _planner == null) return;
            int token = _runToken;
            var kind = _planner.PickPuzzle(_floor);
            _puzzle = Puzzles.PuzzleRoom.Build(kind, _enemyRoot, _player.transform, _floor,
                                               _planner.PuzzleSeed(_floor));
            _puzzleSettled = false;

            // A clocked puzzle (the Labyrinth) left part-way is put back as it was: its clock goes on
            // from the seconds spent, plus a moment to find one's feet, and the player stands where
            // they stood. It has no other state - the maze is the seed's.
            _clockFull = _puzzle.Countdown > 0f ? _puzzle.Countdown : 0f;
            _clockCredit = 0f;
            if (_clockFull > 0f && _floorClockSpent > 0f)
            {
                _clockCredit = Mathf.Min(Tuning.Continuation.ReorientSeconds, _floorClockSpent);
                _puzzle.SetClock(Mathf.Max(Tuning.Continuation.ReorientSeconds, _clockFull - _floorClockSpent + _clockCredit));
                Debug.Log($"[Run] {kind} resumed with {_puzzle.Countdown:0.0}s ({_floorClockSpent:0.0}s spent)");
            }
            if (_floorPlaced)
            {
                _player.transform.position = _floorPos;
                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = _floorPos; rb.linearVelocity = Vector2.zero; }
                Physics2D.SyncTransforms();
            }

            // The exit stands where every floor's exit stands, shut. Its walk-through is the
            // ordinary one - only solving the puzzle can open it.
            _puzzleDoor = FloorDoor.Spawn(Arena.NorthDoorPoint, _player.transform, _enemyRoot,
                ScreenElement(), () =>
                {
                    _activeDoor = null;
                    _puzzleDoor = null;
                    if (token != _runToken) return;
                    StartCoroutine(FloorTransition());
                }, shut: true);

            // Far left or right, from the seed, OPEN from the moment the room is entered: the
            // player may decline the puzzle at any point, before trying it or after failing it.
            // Except where the puzzle fills the floor (the labyrinth) - there it opens on a failure.
            if (_puzzle.SideDoorFromStart) SpawnSideDoor();

            // Built under the transition's black, so the close view snaps rather than eases.
            SetCloseView(_puzzle.CloseView, ease: false);

            _hud?.Flash(_puzzle.Intro);
            Debug.Log($"[Floor] puzzle floor {_floor}: {kind}");
        }

        void SpawnSideDoor()
        {
            if (_sideDoor != null || _player == null || _planner == null) return;
            int token = _runToken;
            float side = (_planner.PuzzleSeed(_floor) & 1) == 0 ? -1f : 1f;
            var sideAt = new Vector2(side * (Arena.HalfExtents.x - Tuning.Puzzle.SideDoorInset),
                                     Arena.NorthDoorPoint.y);
            _sideDoor = FloorDoor.Spawn(sideAt, _player.transform, _enemyRoot, ScreenElement(), () =>
            {
                _sideDoor = null;
                if (token != _runToken) return;
                StartCoroutine(AdjacentRoom());
            });
        }

        // ------------------------------------------------------------------ the close view

        /// <summary>True while the camera is at menu density for a puzzle (the labyrinth).</summary>
        bool _closeView;
        /// <summary>The pull back out: 0..1 while easing, -1 at rest. Not serialized on purpose -
        /// a reload mid-ease just lands the camera at the arena's zoom on the next frame.</summary>
        float _viewEase = -1f, _viewEaseFrom;

        /// <summary>The armoury's zoom - a whole number of screen pixels per MENU texel.</summary>
        static float CloseViewSize => Tuning.Armoury.ViewHalfHeight(Screen.height);

        /// <summary>
        /// The CLOSE VIEW: the camera at the armoury's zoom and the character in its MENU art at
        /// scale 1 - the armoury's own reason for dropping the arena's 4/3: at this zoom 4/3 puts
        /// a body texel on a fractional number of pixels, and it shimmers. Both are undone here
        /// and only here, so a puzzle can never leave the run zoomed in or drawn in menu art.
        /// </summary>
        void SetCloseView(bool on, bool ease)
        {
            // A snap still lands an ease that is under way (a run torn down mid-pull-back must not
            // leave the zoom unsnapped).
            if (on == _closeView && (ease || _viewEase < 0f)) return;
            _closeView = on;
            var rig = _player != null ? _player.Rig : null;
            if (rig != null)
            {
                rig.SetVisualScale(on ? 1f : Tuning.Player.ArenaVisualScale);
                rig.SetDetailArt(on);
            }
            if (_cam == null) return;
            var zoom = _cam.GetComponent<PixelPerfectZoom>();
            float target = on ? CloseViewSize : ArenaViewSize;
            if (zoom == null) { _cam.orthographicSize = target; return; }
            if (ease)
            {
                // Through every size between, unsnapped - snapped, the way out from 1.8 at 1080p
                // is three hard steps (1.8, 2.4, 3.6). Snap comes back on at the end.
                _viewEaseFrom = _cam.orthographicSize;
                _viewEase = 0f;
                zoom.Snap = false;
            }
            else
            {
                _viewEase = -1f;
                zoom.Snap = true;
                zoom.TargetSize = target;
            }
        }

        /// <summary>Every arena frame, before the follow: keeps a held close view snapped to the
        /// window (a resize), and runs the ease back out.</summary>
        void TrackCloseView()
        {
            if (!_closeView && _viewEase < 0f) return;
            var zoom = _cam.GetComponent<PixelPerfectZoom>();
            if (zoom == null) return;
            float target = _closeView ? CloseViewSize : ArenaViewSize;
            if (_viewEase < 0f) { zoom.TargetSize = target; return; }

            _viewEase += Time.unscaledDeltaTime / Tuning.Puzzle.CloseViewOutSeconds;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_viewEase));
            zoom.TargetSize = Mathf.Lerp(_viewEaseFrom, target, k);
            if (_viewEase >= 1f)
            {
                _viewEase = -1f;
                zoom.Snap = true;
                zoom.TargetSize = target;
            }
        }

        /// <summary>Reads the puzzle's state once per frame and answers it ONCE.</summary>
        void TickPuzzle()
        {
            _hud?.SetPuzzle(_puzzle.Title, _puzzle.Body);
            _hud?.SetCountdown(_puzzle.Countdown);
            SetCloseView(_puzzle.CloseView, ease: true);
            if (_puzzleSettled || _puzzle.State == Puzzles.PuzzleState.Active) return;
            _puzzleSettled = true;

            if (_puzzle.State == Puzzles.PuzzleState.Solved)
            {
                // The side door goes - there is no fight to decline any more. The shut door IS the
                // floor's exit, so it becomes _activeDoor before the reward flow, whose own
                // OpenFloorDoor then finds a door already standing and leaves it alone.
                if (_sideDoor != null) { Destroy(_sideDoor.gameObject); _sideDoor = null; }
                _puzzleDoor?.Open();
                _activeDoor = _puzzleDoor;
                Debug.Log($"[Floor] puzzle floor {_floor} solved");
                OfferFloorReward();
            }
            else
            {
                _puzzleDoor?.Seal();
                MarkPuzzleGivenUp();   // a resume must not hand a failed puzzle back for another go
                SpawnSideDoor();   // already standing unless the puzzle filled the floor
                _hud?.Flash("the sigil door is sealed  -  take the side door");
                Debug.Log($"[Floor] puzzle floor {_floor} failed");
            }
        }

        /// <summary>
        /// Through the side door: the same floor's ordinary fight, in a fresh room. Built under
        /// the black screen like any floor change (see FloorTransition for why the pause and the
        /// try/finally), but the floor number does not move - this is the room NEXT to the
        /// puzzle, not the one after it.
        /// </summary>
        IEnumerator AdjacentRoom()
        {
            MarkPuzzleGivenUp();   // the floor is this fight now, on a resume too
            _transitioning = true;
            GamePause.Hold(this);
            try
            {
                _player?.Rig?.SetFacingAway(true);
                yield return _fade.FadeOut(DoorFadeSeconds);

                ClearPuzzle();
                if (_player != null)
                {
                    _player.transform.position = SouthSpawnPoint();
                    Physics2D.SyncTransforms();
                    _player.Rig?.SetFacingAway(false);
                }

                _spawning = true;
                yield return BuildFight(_runToken);
                yield return _fade.FadeIn(DoorFadeSeconds);
            }
            finally
            {
                _transitioning = false;
                GamePause.Release(this);
                _fade.Clear();
            }
        }

        void ClearPuzzle()
        {
            // Switched off before the deferred Destroy: the labyrinth takes its wall mask down in
            // OnDisable, and the adjacent room publishes its own on this same frame.
            if (_puzzle != null) { _puzzle.gameObject.SetActive(false); Destroy(_puzzle.gameObject); _puzzle = null; }
            SetCloseView(false, ease: false);
            if (_sideDoor != null) { Destroy(_sideDoor.gameObject); _sideDoor = null; }
            // Only while still shut: once solved it is _activeDoor and the walk-through owns it.
            if (_puzzleDoor != null && _puzzleDoor != _activeDoor) Destroy(_puzzleDoor.gameObject);
            _puzzleDoor = null;
            _hud?.SetPuzzle(null, null);
            _hud?.SetCountdown(-1f);
        }

        /// <summary>
        /// Is the blade in the player's hand a Rift Blade, for the purposes of how a kill LOOKS?
        ///
        /// Reads the DRAWN weapon, not the equipped one - the resolved loadout, so a transmog
        /// counts. That is the same split the heat repaint lives by and the rule is the same: this
        /// changes a picture and nothing else, so it follows the picture. A skinned Rift Blade
        /// imploding its kills is consistent with a skinned Emberline still heating.
        ///
        /// Recomputed per kill rather than cached at BuildPlayer, because the loadout screen opens
        /// mid-run and a cached answer would keep imploding after the sword had been put away.
        /// </summary>
        bool RiftBladeDrawn()
        {
            if (_profile?.Look == null || _profile.Gear == null) return false;
            var drawn = _profile.Look.Resolve(_profile.Gear);
            // A flag now, not the Rift Blade's id - the Rift Disc implodes its kills too.
            var item = Art.Gear.GearCatalog.Get(drawn.Get(Art.Gear.GearSlot.Weapon));
            return item != null && item.ImplodesKills;
        }

        /// <summary>
        /// Is the blade being DRAWN the katana, for the purpose of whether a draw-cut kill splits
        /// the body? Same "picture follows the picture" rule as <see cref="RiftBladeDrawn"/>: the
        /// bisection is art, so a skinned Zanmato still splits its kills and a plain sword
        /// skinned as Zanmato does not. The finisher's own damage and animation are granted by
        /// the weapon OR the saya relic and are unaffected by this.
        /// </summary>
        bool KatanaDrawn()
        {
            if (_profile?.Look == null || _profile.Gear == null) return false;
            var drawn = _profile.Look.Resolve(_profile.Gear);
            return drawn.Get(Art.Gear.GearSlot.Weapon) == "zanmato_blade";
        }

        /// <summary>
        /// Is the blade being DRAWN one of the reactive weapons (anything Kindled - the Aether
        /// Greatsword, the Aether Longbow, the Aether Dual Discs), for whether a Magnum Opus kill comes
        /// apart into texels? Same "picture follows the picture" rule as <see cref="KatanaDrawn"/>:
        /// the relic grants the art to any weapon of its class, the death is the reactive weapon's.
        /// </summary>
        bool ReactiveWeaponDrawn()
        {
            if (_profile?.Look == null || _profile.Gear == null) return false;
            var drawn = _profile.Look.Resolve(_profile.Gear);
            return Art.Gear.GearCatalog.Get(drawn.Get(Art.Gear.GearSlot.Weapon)) is { Kindled: true };
        }

        /// <summary>
        /// The ledger's hand in what an enemy brings (Induration, Coagulation, Mollification): its
        /// health and armour scaled as it spawns - deep down armour is most of a body, so health
        /// alone was a third of the cost on the card. The wave was bought at the factory's numbers,
        /// so a tougher wave is a longer floor - the cost - not a smaller one.
        /// </summary>
        void Harden(EnemyController enemy)
        {
            var m = Modifiers.Current;
            float mul = m.EnemyHealthMul * (enemy.Elite ? m.EliteHealthMul : 1f);
            if (Mathf.Approximately(mul, 1f)) return;
            var hp = enemy.GetComponent<Health>();
            if (hp != null) hp.Configure(hp.Max * mul);
            enemy.GetComponent<Combat.EnemyArmor>()?.Scale(mul);
        }

        void HookDeath(EnemyController enemy)
        {
            var hp = enemy.GetComponent<Health>();
            hp.Died += h =>
            {
                // What this body was worth against the floor's pool - the spire rises on it, a
                // Rift Box drops in proportion to it, and the ledger's per-kill entries count it.
                float cost = Enemies.WaveComposer.Cost(enemy.Kind, enemy.Elite, _floor);
                float worth = cost / Enemies.WaveComposer.Ehp(EnemyKind.Chaser, false, _floor);

                if (_player) _player.RegisterKill();
                // The board hears every death, whatever caused it - a hit, a burn, a fall.
                if (_player && _player.Board) _player.Board.OnEnemyDied(h);
                if (_player && _player.Effects) _player.Effects.OnEnemyDied(h, worth);

                // BASIC AND ELITE ONLY. This is HookDeath, which is the wave path - a boss has its
                // own Died handler and its own set-piece, and a weapon's cosmetic has no business
                // overriding how a boss dies. Elites come through here too and are included, being
                // a tier on an ordinary enemy rather than a kind of their own.
                // A body that FELL IN has had its death drawn already - down the drop or under
                // the water (Enemies.ChasmFall). A flash or a weapon's death at the lip, where
                // the kill is credited, would show it dying a second time somewhere else.
                if (!enemy.FellIn)
                {
                    if (RiftBladeDrawn())
                        Rifts.RiftImplosion.At(h.transform.position, 0.85f);
                    else if (h.LastDamage.Bisects && KatanaDrawn())
                        Combat.Bisection.At(h.transform, Tuning.Katana.Tint);
                    else if (h.LastDamage.Disintegrates && ReactiveWeaponDrawn())
                        Combat.Disintegration.At(h.transform, h.LastDamage);
                    else
                        Spr.Flash(h.transform.position, 0.9f, new Color(1f, 0.9f, 0.7f), 0.3f);
                }

                _spireKilled += cost;
                // Each body rolls its OWN drops (its place in the wave), so a floor replayed after a
                // resume pays the same drops off the same enemies, whatever order they fall in -
                // nothing to gain by choosing which to kill first.
                int roll = enemy.RollIndex >= 0 ? enemy.RollIndex : LooseRollBase + _floorKills++;
                TryDropRiftBox(h.transform.position, worth, roll);
                TryDropGear(h.transform.position, worth, roll);
                _alive.Remove(enemy);
                Destroy(h.gameObject);

                // Room just opened - let the next queued bodies in, as many as now fit (an elite
                // leaving frees room for two or three). Guarded the same way the initial
                // trickle-in is: a death arriving after the run/floor moved on (teardown, a fresh
                // NextFloor) must not spawn into a room nobody's fighting in any more.
                if (_state == State.Playing && _player != null)
                    while (NextSpawnFits()) SpawnQueuedEnemy();
            };
        }

        /// <summary>
        /// A Rift Box, from any enemy, at Tuning.Boss.RiftBoxDropChance per basic Chaser it was
        /// worth (<paramref name="chasers"/>, its wave cost over a Chaser's on this floor) - an
        /// elite is likelier to drop one than a Bubbles, and a floor pays in proportion to its
        /// pool whatever mix it rolled.
        ///
        /// PER ENEMY RATHER THAN PER FLOOR, so the yield scales with how deep the run went on its
        /// own - pools already grow with depth, so nothing has to say "deeper pays better"
        /// a second time. It also makes the box a thing that DROPS off a body in front of the
        /// player rather than a number that appears between floors, which is the whole reason it
        /// is worth being a physical pickup.
        ///
        /// Spawned into the enemy root, so a teardown that clears the wave clears any box lying
        /// on the floor with it - a consumable surviving into the next run would be loot the
        /// player never carried.
        /// </summary>
        void TryDropRiftBox(Vector2 at, float chasers, int kill)
        {
            if (_player == null) return;
            using (Rifts.RunRolls.Seeded(_planner?.Seed ?? 0, _floor, Rifts.RunRolls.Kind.KillBox, kill))
                if (UnityEngine.Random.value >= Tuning.Boss.RiftBoxDropChance * chasers * Tuning.GearRoll.FormatDropScale) return;
            LayRiftBoxAt(at);
        }

        /// <summary>A Rift Box lying on the floor to be taken - a fresh drop, or one a resumed cleared
        /// room puts back with the seconds it had left.</summary>
        void LayRiftBoxAt(Vector2 at, float life = -1f)
        {
            if (_player == null) return;
            Rifts.RiftBoxPickup.Drop(at, _enemyRoot, _player.transform, () =>
            {
                _loot.FindBox();
                Debug.Log($"[Rift] picked up a RIFT BOX (boxes {_loot.TotalBoxes})");
                _hud?.Flash("RIFT BOX");
            }, life);
        }

        /// <summary>
        /// A gear piece from any enemy, at Tuning.GearRoll.EnemyGearDropChance per basic Chaser it
        /// was worth - the Rift Box's measure, so the yield grows with depth the same way. Straight
        /// into the carried loot (at risk until secured or extracted), announced where it fell.
        /// </summary>
        void TryDropGear(Vector2 at, float chasers, int kill)
        {
            if (_player == null || _floor <= 0) return;
            MintedGearRecord drop;
            using (Rifts.RunRolls.Seeded(_planner?.Seed ?? 0, _floor, Rifts.RunRolls.Kind.KillGear, kill))
            {
                if (UnityEngine.Random.value >= Tuning.GearRoll.EnemyGearDropChance * chasers * Tuning.GearRoll.FormatDropScale) return;
                drop = _loot.RollGear(_floor);
            }
            Debug.Log($"[Rift] floor {_floor} enemy dropped {drop.DisplayName} (on the ground)");
            LayGearAt(at, drop);
        }

        /// <summary>A piece lying on the floor to be taken - a fresh drop, or one a resumed cleared
        /// room puts back where it lay.</summary>
        void LayGearAt(Vector2 at, MintedGearRecord drop)
        {
            if (_player == null || drop == null) return;
            _groundGear ??= new List<Rifts.GearPickup>();
            _groundGear.Add(Rifts.GearPickup.Drop(at, drop, _enemyRoot, _player.transform, taken =>
            {
                _loot.Add(taken);
                _hud?.Flash(taken.DisplayName);
                Debug.Log($"[Rift] picked up {taken.DisplayName} " +
                          $"(carrying {_loot.CarriedCount}, secured {_loot.SecuredCount})");
            }));
        }

        /// <summary>
        /// Gear dropped this floor and still lying on it. Non-readonly and null-guarded (a reload
        /// gives a fresh list; a lost entry only means a piece that isn't cleared at the door).
        /// The pickups live under _enemyRoot, which survives floor changes - so leaving the floor
        /// has to clear them here, or they would lie in the next room.
        /// </summary>
        List<Rifts.GearPickup> _groundGear = new();

        int GroundGearCount()
        {
            if (_groundGear == null) return 0;
            _groundGear.RemoveAll(g => g == null);
            return _groundGear.Count;
        }

        /// <summary>Leaving the floor leaves what is still on the ground behind - it was never
        /// picked up, so it was never the player's.</summary>
        void LeaveGroundGear()
        {
            if (_groundGear == null) return;
            int left = 0;
            foreach (var g in _groundGear)
                if (g != null) { left++; g.gameObject.SetActive(false); Destroy(g.gameObject); }
            _groundGear.Clear();
            if (left > 0) Debug.Log($"[Rift] left {left} piece(s) of gear behind on floor {_floor}");
        }

        Vector2 RandomEdgePoint()
        {
            var angle = UnityEngine.Random.value * Mathf.PI * 2f;
            var p = new Vector2(Mathf.Cos(angle) * (HalfWidth - 1.4f),
                                Mathf.Sin(angle) * (HalfHeight - 1.4f));
            if (Arena.OnFloor(p, 0.6f)) return p;
            // The room's shape walled this stretch of edge (a corner block, a hall's flank):
            // walk in toward the middle until there is floor - the pack still arrives from that
            // side of the room, just from the inside face of the wall.
            for (float t = 0.1f; t < 1f; t += 0.1f)
            {
                var q = Vector2.Lerp(p, Vector2.zero, t);
                if (Arena.OnFloor(q, 0.6f)) return q;
            }
            return Arena.NearestFloor(p, 0.6f);
        }

        /// <summary>
        /// Anywhere on the floor, edges included - unlike RandomEdgePoint, which only ever chases
        /// enemies use. Retries a few times against the min-distance floor rather than clamping
        /// into it, so the result stays uniformly random instead of piling up at that radius.
        /// </summary>
        Vector2 RandomFloorPoint(float minDistanceFromPlayer)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var p = new Vector2(
                    UnityEngine.Random.Range(-HalfWidth + 1.4f, HalfWidth - 1.4f),
                    UnityEngine.Random.Range(-HalfHeight + 1.4f, HalfHeight - 1.4f));
                if (!Arena.OnFloor(p, 0.6f)) continue;
                if (_player == null ||
                    Vector2.Distance(p, _player.transform.position) >= minDistanceFromPlayer)
                    return p;
            }
            return RandomEdgePoint();
        }

        /// <summary>The profile's design boxes when the run began - see RunSummary.SecuredDiamondBoxes.
        /// Plain ints: they survive a domain reload.</summary>
        int _runStartDiamondBoxes, _runStartBlackDiamondBoxes;

        async void EndRun(bool survived)
        {
            if (_state != State.Playing) return;
            if (_profile == null) return;
            _state = State.RunOver;
            // Before anything awaits: the run is ending, and a tab closed from here on must find it
            // ended, not waiting (SettleEndedWaitingAsync). No snapshot is written after this.
            NoteRunEnding(_profile.ProfileId, _runId, survived);
            _runSave = null;
            // State is already RunOver, so CharacterScreen's onClosed (OpenPauseMenu) no-ops.
            if (PauseScreen.IsOpen) PauseScreen.Close();
            if (InventoryScreen.IsOpen) InventoryScreen.Close();
            if (CharacterScreen.IsOpen) CharacterScreen.Close();
            if (FloorRewardScreen.IsOpen) FloorRewardScreen.Close();
            if (MasteryScreen.IsOpen) MasteryScreen.Close();
            if (ConfirmDialog.IsOpen) ConfirmDialog.Close();

            var tally = new RunTally
            {
                RunId = _runId,
                Element = _player && _player.Resource ? _player.Resource.Element : ElementType.Fire,
                Kills = _player ? _player.Kills : 0,
                Floors = _floor,
                Seconds = Time.time - _runStartTime,
                PendingMastery = _pendingMastery,
                RunCalx = _runCalx,
                StakeGateCleared = _stakeGateCleared,
                RunStartDiamondBoxes = _runStartDiamondBoxes,
                RunStartBlackDiamondBoxes = _runStartBlackDiamondBoxes,
            };
            _pendingMastery = 0;
            _runCalx = 0;

            // An avatar's boxes still outstanding, then floor 100's box, so they are secured and
            // banked with everything else.
            await SettleAvatarBoxesAsync();
            await SettleFinalFloorAsync();
            tally.GuaranteeWeek = _runGuaranteeWeek;

            var summary = await SettleRunAsync(tally, _loot, survived);

            // The run is over on the store's side too - AFTER the run-end checkpoint, so a tab closed
            // between the two finds a run still waiting, and settles it then (it is noted as ended).
            _keeper.End(_runId);
            _waiting = null;

            ShowRunOver(summary);
        }

        /// <summary>What a run comes to, for <see cref="SettleRunAsync"/> - read off the live run, or
        /// off a waiting run's snapshot.</summary>
        struct RunTally
        {
            public string RunId;
            public ElementType Element;
            public int Kills, Floors, PendingMastery, RunCalx, GuaranteeWeek;
            public float Seconds;
            public bool StakeGateCleared;
            public int RunStartDiamondBoxes, RunStartBlackDiamondBoxes;
        }

        /// <summary>
        /// THE RUN'S END, settled into the profile and checkpointed: mastery banked, the loot banked
        /// or forfeit, the reward cards' calx, the stake, and the run-end write. The live run's end
        /// (EndRun) and a waiting run's (abandoned from the hub, or one whose end never got written)
        /// both come through here, so the two can never settle a run differently.
        /// </summary>
        async System.Threading.Tasks.Task<RunSummary> SettleRunAsync(RunTally t, Rifts.RunLoot loot, bool survived)
        {
            var summary = new RunSummary
            {
                RunId = t.RunId,
                Element = t.Element,
                Kills = t.Kills,
                XpEarned = t.Kills * XpPerKill,
                Floors = t.Floors,
                DurationSeconds = t.Seconds,
                Survived = survived,
                EndedAtUtc = DateTime.UtcNow.ToString("o"),
                GuaranteeWeek = t.GuaranteeWeek,
            };

            if (t.PendingMastery > 0)
            {
                var em = _profile.Mastery.For(t.Element);
                int levels = em.Grant(t.PendingMastery);
                Mastery.Invalidate();
                Debug.Log($"[Mastery] {t.Element} +{t.PendingMastery} xp" +
                          (levels > 0 ? $" -> {levels} level(s), now {em.Level}" : "") +
                          $" (total {_profile.Mastery.TotalLevel})");
            }

            // THE EXTRACTION LOOP'S ONLY WRITE. Whatever was carried and never pushed through a
            // Rift is gone - that is the stake, and it is forfeited HERE rather than on the death
            // itself so there is exactly one place a run's loot is resolved. Secured loot banks on
            // the same checkpoint everything else rides.
            //
            // XP AND MASTERY ARE UNTOUCHED BY EITHER OUTCOME, deliberately: the run economy's rule
            // is that death costs the tradeable stake and never the progression, so a bad
            // extraction is a bad payday rather than a wasted hour.
            int lost = loot.CarriedCount;
            loot.ForfeitCarried();
            // The reward card's calx rides with the carried loot: home on an extraction, gone on a
            // death. Added before the checkpoint so both stores serialise the same profile.
            if (t.RunCalx > 0)
            {
                if (survived)
                {
                    int calxBoxes = GearForge.AddCalx(_profile, Tuning.FloorRewards.CalxTier, t.RunCalx);
                    Debug.Log($"[Rift] banked {t.RunCalx} calx" +
                              (calxBoxes > 0 ? $" -> {calxBoxes} {Tuning.FloorRewards.CalxTier} box(es)" : ""));
                }
                else Debug.Log($"[Rift] lost {t.RunCalx} carried calx");
            }
            int banked = loot.Bank(_profile);
            if (banked > 0 || lost > 0)
                Debug.Log($"[Rift] banked {banked} piece(s), lost {lost} carried");
            // What the server's box ledger is credited from - the gain, not the absolute count.
            // Design boxes are never spent mid-run, so the difference is exactly what was secured.
            summary.SecuredDiamondBoxes = Mathf.Max(0, _profile.Boxes.Diamond - t.RunStartDiamondBoxes);
            summary.SecuredBlackDiamondBoxes = Mathf.Max(0, _profile.Boxes.BlackDiamond - t.RunStartBlackDiamondBoxes);

            // THE STAKE SETTLES ON THE SAME CHECKPOINT, keyed on `survived` - which is to say on
            // EXTRACTION: a run only ends survived when the player took it out. Whatever system
            // ends a run extracted has to keep passing that, and the stake follows on its own.
            var (stakeOutcome, stakeItem, payout) =
                GearStake.Resolve(_profile, extracted: survived, gateCleared: t.StakeGateCleared,
                                  new System.Random());
            if (payout != null) Art.Gear.GearCatalog.Register(payout.ToGearItem());
            _stakeLine = stakeOutcome switch
            {
                GearStake.Outcome.Paid     => $"stake        {stakeItem.DisplayName} kept, matching piece claimed",
                GearStake.Outcome.Returned => $"stake        {stakeItem.DisplayName} came home (gate not cleared)",
                GearStake.Outcome.Lost     => $"stake        {stakeItem.DisplayName} DESTROYED",
                GearStake.Outcome.Saved    => $"stake        {stakeItem.DisplayName} came home (insured)",
                _ => null,
            };
            if (_stakeLine != null) Debug.Log($"[Stake] {stakeOutcome}: {stakeItem.InstanceId}" +
                                              (payout != null ? $" -> {payout.InstanceId}" : ""));

            // CHECKPOINT 2 of 2.
            await _store.CommitRunAsync(_profile, summary);

            // The taper counter is ACCOUNT state, not character state, so it rides the account
            // write rather than the profile checkpoint. Saved on every run end including a death -
            // a lost run still spent the taper for the floors it cleared, because it also banked
            // the XP for them. The day of the run rides it too: the hub's sigil door stays open for
            // the rest of the day (AccountProfile.LastRunDay).
            if (_account != null) _account.LastRunDay = AccountProfile.UtcDay;
            if (_account != null) await _accounts.SaveAsync(_account);
            return summary;
        }

        /// <summary>The element to open the loadout sheet against - the run's, or the last played
        /// one before a player exists.</summary>
        ElementType ScreenElement()
            => _player != null && _player.Resource != null
                ? _player.Resource.Element
                : _profile.LastElement;

        /// <summary>
        /// The mid-run pause menu: a circle that resumes, in a triangle whose three points are the
        /// loadout sheet, the carried-loot screen and abandoning the run.
        ///
        /// Each point CLOSES this menu and opens that screen; backing out of that screen calls
        /// straight back here (CharacterScreen and InventoryScreen take an onClosed, the abandon
        /// dialog an onCancel). So only ever one screen is up at a time, and this method is the
        /// single re-entry point for all three return paths.
        /// </summary>
        void OpenPauseMenu()
        {
            if (_state != State.Playing || PauseScreen.IsOpen
                || CharacterScreen.IsOpen || InventoryScreen.IsOpen || ConfirmDialog.IsOpen)
                return;

            PauseScreen.Open(_canvas.transform,
                onCharacter: () =>
                {
                    PauseScreen.Close();
                    CharacterScreen.Open(_canvas.transform, ScreenElement(), OpenPauseMenu);
                },
                onInventory: () =>
                {
                    PauseScreen.Close();
                    InventoryScreen.Open(_canvas.transform, _loot, OpenPauseMenu);
                },
                onEndRun: () =>
                {
                    PauseScreen.Close();
                    AskLeaveRun();   // suspend for later, or abandon
                },
                onSettings: () =>
                {
                    PauseScreen.Close();
                    OpenSettings();
                });
        }

        /// <summary>Settings, from the hub or the pause menu. Mid-run, closing it returns to the
        /// pause menu - through Controller Configuration and back as well.</summary>
        void OpenSettings()
            => SettingsScreen.Open(_canvas.transform, OpenControlsScreen,
                                   _state == State.Playing ? OpenPauseMenu : null);

        /// <summary>
        /// Closes SettingsScreen and opens ControlsScreen; ControlsScreen's own BACK button closes
        /// it and reopens SettingsScreen through this same method - the same closes-this-opens-
        /// that hand-off OpenPauseMenu's three points already use, never nested.
        /// </summary>
        void OpenControlsScreen()
        {
            if (SettingsScreen.IsOpen) SettingsScreen.Close();
            ControlsScreen.Open(_canvas.transform, () =>
            {
                ControlsScreen.Close();
                OpenSettings();
            });
        }

        /// <summary>
        /// Confirm before throwing a run away. Names what is actually lost: the floor in progress
        /// yields nothing, while mastery from floors already cleared was banked as they finished
        /// and is kept. Backing out returns to the pause menu it was opened from.
        /// </summary>
        void AskAbandonRun()
        {
            if (ConfirmDialog.IsOpen || _state != State.Playing) return;

            int kills = _player != null ? _player.Kills : 0;
            int carried = _loot.CarriedCount;
            string carriedLine = carried == 0
                ? " Everything on this floor is lost."
                : carried == 1
                    ? " The 1 carried piece you have not secured at a Rift is lost."
                    : $" The {carried} carried pieces you have not secured at a Rift are lost.";
            string body =
                $"Floor {_floor} is not complete, so it pays nothing.\n\n" +
                $"You keep the {kills} kill{(kills == 1 ? "" : "s")} and the mastery from floors " +
                "you already cleared." + carriedLine;
            var staked = GearStake.Staked(_profile);
            if (staked != null) body += $" Your staked {staked.DisplayName} is destroyed.";

            ConfirmDialog.Show(_canvas.transform, "ABANDON RUN?", body,
                "ABANDON", "KEEP PLAYING",
                onConfirm: () => EndRun(false),
                onCancel: OpenPauseMenu);
        }

        /// <summary>Remove everything a run owns. Safe to call when no run is running.</summary>
        void TeardownRun()
        {
            _runToken++;
            StopAllCoroutines();   // in-flight NextFloor would spawn against a destroyed player
            _spawning = false;
            // FloorTransition's own finally already clears this when StopAllCoroutines disposes
            // it. Cleared again here for the same reason _spawning is: a run boundary is where
            // this class asserts its state rather than inferring it, and a _transitioning stuck
            // true would silently stop every floor after it from ever advancing.
            _transitioning = false;

            // A run can end with a screen still up - abandoning from the confirm dialog, or dying
            // while one is open. Those screens are about to be closed or destroyed and would never
            // release their hold, leaving the menu frozen.
            FloorRewardScreen?.Close();
            ExchangeScreen?.Close();
            TransmuteScreen?.Close();
            _circle = null;
            CharacterScreen?.Close();
            InventoryScreen?.Close();
            PauseScreen?.Close();
            GamePause.ReleaseAll();

            foreach (var e in _alive) if (e) Destroy(e.gameObject);
            _alive.Clear();
            _spawnQueue.Clear();
            Enemies.EnemyRegistry.Prune();

            // The boss owns a coroutine and a hazard layer, neither of which is a child of
            // _enemyRoot's wave and neither of which stops on its own. Ceased explicitly, for the
            // documented reason: a coroutine outliving its run is one of this project's own
            // lifecycle bugs, and here it would leave slices erupting on the main menu.
            if (_boss != null) { _boss.Cease(); Destroy(_boss.gameObject); _boss = null; }
            CloseRift();
            ClearPuzzle();
            DamageNumbers.Teardown();
            DamageNumbers.Hidden = false;
            ArmorRing.Hidden = false;
            Enemies.EnemyController.TelegraphStrength = 1f;
            Hazards.FloorPits.PlayerImmune = null;
            Hazards.Tornado.PlayerImmune = null;
            Hazards.ProjectionLines.CalmAll();
            Exchange.SolNiger.Clear();
            Player.LucidRims.Clear();
            _floorBoonFloorsLeft = 0;
            Hitstop.Teardown();
            CameraKick.Teardown();
            FinisherHits.Teardown();

            // Deactivate before destroying: Destroy is deferred to end of frame, so without this
            // the outgoing player keeps taking hits from the outgoing enemies and its death
            // handler fires against whatever run has started in the meantime.
            if (_enemyRoot)
            {
                _enemyRoot.gameObject.SetActive(false);
                Destroy(_enemyRoot.gameObject);
                _enemyRoot = null;
            }

            // The door is parented under _enemyRoot, already gone above - explicit here anyway,
            // matching every other run-owned reference in this method, rather than relying on
            // Unity's destroyed-object equality to make the field read null on its own.
            _activeDoor = null;

            if (_hazardRoot)
            {
                Destroy(_hazardRoot.gameObject);
                _hazardRoot = null;
            }

            if (_player)
            {
                if (_onPlayerDied != null && _player.Health != null)
                    _player.Health.Died -= _onPlayerDied;
                _player.gameObject.SetActive(false);
                Destroy(_player.gameObject);
            }
            _onPlayerDied = null;

            // DestroyImmediate: a deferred Destroy would leave the old HUD alive for the rest of
            // the frame, and the replacement is built before it ever runs.
            if (_hud) DestroyImmediate(_hud);
            _player = null;
            _hud = null;
        }

        // ------------------------------------------------------------------ continuation
        //
        // A RUN SURVIVES A CLOSED TAB, A CRASH OR A LOST CONNECTION, AND A PLAYER CAN LEAVE ONE TO
        // COME BACK TO (CONTINUATION.md - option 2, the user's lean of 2026-10-13).
        //
        //     SNAPSHOTS   the whole run (Chain.RunSnapshot) at every floor's START (NextFloor) and
        //                 at its CLEAR - then again once the deal is answered and once the reward is
        //                 taken - to the run store beside the character (Chain.IRunStore)
        //     HEARTBEATS  health and armour, while they move, every Tuning.Continuation.BeatSeconds
        //     RESUME      a floor in progress is REPLAYED from its start - the same room, wave and
        //                 drops (Rifts.RunRolls) - at the LOWER of the snapshot's health and the
        //                 beat's: leaving buys a replay, never a heal. A cleared floor comes back as
        //                 its cleared room, the deal and the reward waiting with the same choices.
        //     LEAVE       the pause menu's bottom ring: SUSPEND (the run waits at the sigil door) or
        //                 ABANDON (a death, as it always was)
        //
        // While a run waits, its character is the run's: no equipping and no mastery board, which
        // the run read once as it began. A waiting run is never dropped silently - the gate offers
        // it back, and ending it there settles it as a death. The stake rides in it.

        RunKeeper NewKeeper()
        {
            var k = new RunKeeper(() => _runs) { Superseded = OnRunSuperseded };
            // A domain reload rebuilt the keeper mid-run: it carries on under the run's session.
            if (_profile != null && !string.IsNullOrEmpty(_runSession)) k.Begin(_profile.ProfileId, _runSession);
            return k;
        }

        /// <summary>A run in flight under a session of its own - a new run's, or the one a resume claimed.</summary>
        void BeginRunSession(string session = null)
        {
            _runSession = session ?? Guid.NewGuid().ToString("N");
            _keeper.Begin(_profile.ProfileId, _runSession);
            _floorKills = 0;
            _floorSpawns = 0;
            _rewardRerolls = 0;
            _puzzleGivenUp = false;
            _resumeBossDown = 0;
            _bossStartDown = 0;
            _beatBossDown = 0;
            _resumeClockSpent = _floorClockSpent = 0f;
            _resumeRiftCollapsed = _floorRiftCollapsed = _resumePlaced = _floorPlaced = false;
            _clockFull = _clockCredit = 0f;
            _beatHealth = -1f;
            _beatWear = -1f;
            _beatAt = 0f;
        }

        static LootBoxes CopyBoxes(LootBoxes b) => b == null ? new LootBoxes() : new LootBoxes
        {
            Bronze = b.Bronze, Silver = b.Silver, Gold = b.Gold, Diamond = b.Diamond, BlackDiamond = b.BlackDiamond,
        };

        static List<DurabilityEntry> CopyWear(List<DurabilityEntry> entries)
        {
            var list = new List<DurabilityEntry>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null) list.Add(new DurabilityEntry { ItemId = e.ItemId, Current = e.Current });
            return list;
        }

        float WearSum()
        {
            float sum = 0f;
            if (_profile?.Wear?.Entries != null)
                foreach (var e in _profile.Wear.Entries) if (e != null) sum += e.Current;
            return sum;
        }

        /// <summary>The run as it stands, written down. ENTERING is taken before the floor is planned
        /// (its Floor is the one about to be entered); CLEARED after, with the room's door, Rift and
        /// ground loot, and the screens still to come.</summary>
        RunSnapshot CaptureRun(RunStage stage, int pending)
        {
            var s = new RunSnapshot
            {
                RunId = _runId,
                ProfileId = _profile.ProfileId,
                Seq = (_runSave?.Seq ?? 0) + 1,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                Element = ScreenElement(),
                Stage = stage,
                Floor = stage == RunStage.Entering ? _floor + 1 : _floor,
                PendingScreens = stage == RunStage.Cleared ? pending : 0,
                RewardRerolls = stage == RunStage.Cleared ? _rewardRerolls : 0,
                PuzzleGivenUp = stage == RunStage.Entering ? _resumeGivenUp : _puzzleGivenUp,
                BossEighthsDown = stage == RunStage.Entering ? _resumeBossDown : 0,
                ClockSpent = stage == RunStage.Entering ? _resumeClockSpent : 0f,
                RiftCollapsed = stage == RunStage.Entering && _resumeRiftCollapsed,
                Placed = stage == RunStage.Entering && _resumePlaced,
                PosX = _resumePos.x,
                PosY = _resumePos.y,
                Planner = _planner?.Copy(),
                RunSeconds = Time.time - _runStartTime,
                RunClock = Clock.Seconds,
                Format = Core.RunFormat.WireName,
                PendingMastery = _pendingMastery,
                RunCalx = _runCalx,
                RunCalxTaken = _runCalxTaken,
                StakeGateCleared = _stakeGateCleared,
                GuaranteeWeek = _runGuaranteeWeek,
                RunStartRiftBoxes = _runStartRiftBoxes,
                RunStartBoxes = CopyBoxes(_runStartBoxes),
                Loot = _loot.Capture(),
                Ledger = Modifiers.Capture(),
                FloorBoon = _floorBoon,
                FloorBoonFloorsLeft = _floorBoonFloorsLeft,
                Health = _player.Health.Current,
                Wear = CopyWear(_profile.Wear.Entries),
            };
            if (stage == RunStage.Cleared)
            {
                s.PlanCategory = _plan.Category;
                s.PlanRift = _plan.Rift;
                s.PlanForced = _plan.Forced;
                s.Rift = _rift == null ? RiftSave.None
                       : _rift.Sealed ? RiftSave.Sealed
                       : _rift.Unstable ? RiftSave.None
                       : RiftSave.Open;
                s.RiftSecured = _rift != null ? _rift.Secured : 0;
                s.FinalFloorUnsettled = _finalAsk != null;
                s.AvatarUnsettledFloor = _avatarAsk != null ? _avatarAskFloor : 0;
                if (_groundGear != null)
                    foreach (var g in _groundGear)
                        if (g != null && !g.Taken && g.Record != null)
                            s.Ground.Add(new GroundGear { Record = g.Record, X = g.transform.position.x, Y = g.transform.position.y });
                // A Rift Box the last kills dropped is still lying there through the deal and the
                // reward (their pause stops its clock) - it comes back with the room.
                foreach (var b in FindObjectsByType<Rifts.RiftBoxPickup>(FindObjectsInactive.Exclude))
                    if (b != null && !b.Taken)
                        s.GroundBoxes.Add(new GroundBox { X = b.transform.position.x, Y = b.transform.position.y, Life = b.LifeSeconds });
            }
            _player.CaptureRun(s);
            if (_player.Effects != null) _player.Effects.CaptureCounters(s);
            return s;
        }

        /// <summary>Snapshot the live run and send it. A no-op outside a run, or once one is ending.</summary>
        void SaveRun(RunStage stage, int pending = 0)
        {
            if (_state != State.Playing || _player == null || _player.Health == null || _profile == null
                || _planner == null || string.IsNullOrEmpty(_runSession)) return;
            var s = CaptureRun(stage, pending);
            _runSave = s;
            _keeper.Save(s);
            _beatHealth = s.Health;
            _beatWear = WearSum();
            _beatBossDown = BossEighthsDownNow();
            _beatAt = Time.unscaledTime;
        }

        /// <summary>
        /// A cleared room changed (a Rift opened or used, floor 100's box credited): written again -
        /// but ONLY once its screens are done. While the deal or the reward is still to come, the
        /// stage's own snapshot is the resume point, taken BEFORE the screen's side effects: after
        /// them, a resume would build the deal a second time (its clocks ticking twice, the next
        /// deal's owed changes already spent) or pay the floor's mastery and taper again. What
        /// changes meanwhile is written by the next stage's snapshot.
        /// </summary>
        void ResaveCleared()
        {
            if (_runSave != null && _runSave.Stage == RunStage.Cleared && _runSave.PendingScreens == 0)
                SaveRun(RunStage.Cleared, 0);
        }

        /// <summary>A Curator reroll spent: the waiting reward's snapshot learns only that - the rest
        /// of it stays as it was before the reward's own side effects (see ResaveCleared).</summary>
        void ResaveRerolls()
        {
            var at = _runSave;
            if (at == null || at.Stage != RunStage.Cleared || at.PendingScreens != 1) return;
            var s = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(at));
            s.Seq = at.Seq + 1;
            s.SavedAtUtc = DateTime.UtcNow.ToString("o");
            s.RewardRerolls = _rewardRerolls;
            _runSave = s;
            _keeper.Save(s);
        }

        /// <summary>
        /// This floor's puzzle is behind the player (given up, or failed): the floor-start snapshot is
        /// told so, and a resume builds the adjacent room's fight rather than the puzzle. The snapshot
        /// stays the floor's START - its health and armour only come down to what they are now.
        /// </summary>
        void MarkPuzzleGivenUp()
        {
            _puzzleGivenUp = true;
            if (_runSave != null && !_runSave.PuzzleGivenUp) ResaveEntering(s => s.PuzzleGivenUp = true);
        }

        /// <summary>
        /// Something about THIS floor that a replay must not undo (a puzzle given up, a Collapsing
        /// Rift run out): the floor-start snapshot copied, changed, and sent again. It stays the
        /// floor's START - so what the fight has taken since (health, armour, the boss, a clock) is
        /// folded in as the heartbeat it replaces would have carried it, never given back.
        /// </summary>
        void ResaveEntering(Action<RunSnapshot> change)
        {
            var at = _runSave;
            if (at == null || at.Stage != RunStage.Entering || at.Floor != _floor
                || _player == null || _player.Health == null) return;
            var now = BeatNow();
            var w = new SuspendedRun { Snapshot = at, Beat = now };
            var s = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(at));
            s.Seq = at.Seq + 1;
            s.SavedAtUtc = DateTime.UtcNow.ToString("o");
            s.Health = w.Health;
            s.Wear = w.Wear();
            s.BossEighthsDown = w.BossEighthsDown;
            s.ClockSpent = w.ClockSpent;
            s.Placed = w.Placed;
            s.PosX = w.PosX;
            s.PosY = w.PosY;
            change?.Invoke(s);
            _runSave = s;
            _keeper.Save(s);
        }

        /// <summary>Health and armour since the last snapshot, sent while they move - polled from Update.</summary>
        /// <summary>Also sent AT ONCE when the boss crosses an eighth - a step it can't lose again.</summary>
        void TickRunBeat()
        {
            if (_runSave == null || _player == null || _player.Health == null) return;
            bool bossStep = BossEighthsDownNow() != _beatBossDown;
            if (!bossStep)
            {
                // A running clock beats every second whatever health does - its seconds ARE the state.
                if (ClockRunning(out _))
                {
                    if (Time.unscaledTime - _beatAt < Tuning.Continuation.ClockBeatSeconds) return;
                }
                // A fight beats on its cadence whatever health does too: the service times every
                // floor ATTEMPT itself (web/service runProgress.ts - the run clock), and a floor left
                // part-way counts up to the last beat it heard. A quiet fight sending none would be
                // timed as if it stopped at its last hit.
                else if (FightLive())
                {
                    if (Time.unscaledTime - _beatAt < Tuning.Continuation.BeatSeconds) return;
                }
                else
                {
                    if (Mathf.Approximately(_player.Health.Current, _beatHealth) && Mathf.Approximately(WearSum(), _beatWear)) return;
                    if (Time.unscaledTime - _beatAt < Tuning.Continuation.BeatSeconds) return;
                }
            }
            SendBeat();
        }

        /// <summary>A floor is being FOUGHT: a wave arriving or alive, a boss, or a puzzle in play -
        /// the time the run clock counts (SHORT-DESCENT.md: from the first spawn to the clear).</summary>
        bool FightLive() =>
            _state == State.Playing && _floor > 0 &&
            (_spawning || _alive.Count > 0 || _spawnQueue.Count > 0 || _boss != null ||
             (_puzzle != null && _puzzle.State == Puzzles.PuzzleState.Active));

        /// <summary>
        /// Whether a clock is running on this floor (the Labyrinth's, a Collapsing Rift's), and the
        /// seconds of it truly SPENT - the reorientation a resume granted is added back, so the
        /// next resume grants it against the real time, never on top of the last one.
        /// </summary>
        bool ClockRunning(out float spent)
        {
            spent = 0f;
            float left = _puzzle != null && _puzzle.Countdown >= 0f ? _puzzle.Countdown
                       : _rift != null && _rift.Unstable ? _rift.Remaining
                       : -1f;
            if (left < 0f || _clockFull <= 0f) return false;
            spent = Mathf.Max(0f, _clockFull - left + _clockCredit);
            return true;
        }

        /// <summary>The fight since the last snapshot, as a heartbeat carries it.</summary>
        RunBeat BeatNow()
        {
            ClockRunning(out float spent);
            bool maze = _puzzle is Puzzles.MazePuzzle && _puzzle.State == Puzzles.PuzzleState.Active;
            Vector2 at = _player != null ? (Vector2)_player.transform.position : Vector2.zero;
            return new RunBeat
            {
                RunId = _runId,
                Seq = _runSave != null ? _runSave.Seq : 0,
                Health = _player != null && _player.Health != null ? _player.Health.Current : 0f,
                Wear = CopyWear(_profile?.Wear?.Entries),
                BossEighthsDown = BossEighthsDownNow(),
                ClockSpent = spent,
                Placed = maze,
                PosX = at.x,
                PosY = at.y,
                RunClock = Clock.Seconds,
            };
        }

        /// <summary>The live boss's health in whole eighths taken, rounded in its favour - a boss at
        /// 40% is three down (it would come back at 50%). 0 with no boss.</summary>
        int BossEighthsDownNow()
        {
            if (_boss == null || _boss.Health == null || _boss.Health.Max <= 0f) return 0;
            int steps = Tuning.Continuation.BossSteps;
            float left = _boss.Health.Current / _boss.Health.Max;
            // The epsilon keeps a boss restored to exactly 6/8 at six, not seven, through float error.
            int up = Mathf.CeilToInt(left * steps - 1e-4f);
            return Mathf.Clamp(steps - up, 0, steps - 1);
        }

        void SendBeat()
        {
            if (_runSave == null || _player == null || _player.Health == null || _profile == null) return;
            var beat = BeatNow();
            // Not taken (a snapshot is ahead of it): left unmarked, so the next tick tries again.
            if (!_keeper.Beat(beat)) return;
            _beatHealth = beat.Health;
            _beatWear = WearSum();
            _beatBossDown = beat.BossEighthsDown;
            _beatAt = Time.unscaledTime;
        }

        /// <summary>The window losing focus may be the tab closing: the run's health goes now.</summary>
        void OnApplicationFocus(bool focus)
        {
            if (!focus && _state == State.Playing) SendBeat();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && _state == State.Playing) SendBeat();
        }

        async void SaveAccount()
        {
            if (_account == null) return;
            try { await _accounts.SaveAsync(_account); }
            catch (Exception e) { Debug.LogWarning($"[Run] account not saved: {e.Message}"); }
        }

        // ---- a run that ended while its end was still being written

        static string EndedKey(string profileId) => "equivalence.run-ended." + profileId;

        /// <summary>Noted on THIS device the moment a run starts to end, before anything awaits - a tab
        /// closed while the end is written finds the run ended, never waiting to be resumed.</summary>
        static void NoteRunEnding(string profileId, string runId, bool survived)
        {
            if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(runId)) return;
            PlayerPrefs.SetString(EndedKey(profileId), runId + (survived ? "|extracted" : "|died"));
            PlayerPrefs.Save();
        }

        /// <summary>How this device saw <paramref name="runId"/> end - true extracted, false died -
        /// or null when it never saw it end.</summary>
        static bool? EndedAs(string profileId, string runId)
        {
            var v = PlayerPrefs.GetString(EndedKey(profileId), "");
            if (string.IsNullOrEmpty(runId) || !v.StartsWith(runId + "|")) return null;
            return v.EndsWith("|extracted");
        }

        // ---- the hub: a run waiting

        /// <summary>Asks the run store what waits for this character. Never throws.</summary>
        async System.Threading.Tasks.Task LookUpWaitingRunAsync()
        {
            _waiting = null;
            _waitingUnknown = false;
            if (_profile == null) return;
            RunLookup look;
            try { look = await _runs.LoadAsync(_profile.ProfileId); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Run] could not ask for a waiting run: {e.Message}");
                _waitingUnknown = true;
                return;
            }
            if (!look.Read) { _waitingUnknown = true; return; }
            if (look.Run?.Snapshot == null || string.IsNullOrEmpty(look.Run.Snapshot.RunId)) return;
            _waiting = look.Run;
            var s = _waiting.Snapshot;
            Debug.Log($"[Run] {_profile.ProfileId} has run {s.RunId} waiting: {s.Element}, floor {s.Floor} ({s.Stage}" +
                      (s.Stage == RunStage.Cleared ? $", {s.PendingScreens} screen(s) to come" : "") + $"), seq {s.Seq}");
        }

        /// <summary>
        /// A waiting run that in fact ENDED - its end noted on this device but never all written
        /// (NoteRunEnding), or already in the character's runs - is settled as it ended and taken
        /// off the store, never offered as a resume.
        /// </summary>
        async System.Threading.Tasks.Task SettleEndedWaitingAsync()
        {
            var w = _waiting;
            if (w?.Snapshot == null || _profile == null) return;
            bool committed = Committed(w.Snapshot.RunId);
            bool? ended = EndedAs(_profile.ProfileId, w.Snapshot.RunId);
            if (!committed && ended == null) return;   // genuinely waiting
            Debug.Log($"[Run] run {w.Snapshot.RunId} ended before its end was all written - settling it as " +
                      (committed ? "already committed" : ended == true ? "an extraction" : "a death"));
            try { await SettleWaitingAsync(w, ended ?? false); }
            catch (Exception e) { Debug.LogWarning($"[Run] could not settle run {w.Snapshot.RunId}: {e.Message}"); }
        }

        bool Committed(string runId)
            => _profile?.RecentRuns != null && _profile.RecentRuns.Exists(r => r != null && r.RunId == runId);

        /// <summary>
        /// A WAITING run, ended without being played: settled into the profile exactly as the live
        /// run's end would be (SettleRunAsync) from its snapshot - the armour as the beat left it,
        /// the loot's banked pools rebased on the profile now - then taken off the store. Claimed
        /// first, so the end is this session's to write and a device still playing it stops.
        /// Null when it had already been committed (only its store row went).
        /// </summary>
        async System.Threading.Tasks.Task<RunSummary> SettleWaitingAsync(SuspendedRun w, bool survived)
        {
            var s = w.Snapshot;
            string session = Guid.NewGuid().ToString("N");
            var claimed = await _runs.ClaimAsync(_profile.ProfileId, s.RunId, session);
            if (claimed?.Snapshot != null) { w = claimed; s = w.Snapshot; }

            RunSummary summary = null;
            if (!Committed(s.RunId))
            {
                _profile.Wear.Entries = w.Wear();
                var loot = new Rifts.RunLoot();
                loot.Restore(s.Loot, s.RunStartRiftBoxes, s.RunStartBoxes, _profile);
                var tally = new RunTally
                {
                    RunId = s.RunId,
                    Element = s.Element,
                    Kills = s.Kills,
                    Floors = s.Floor,
                    Seconds = s.RunSeconds,
                    PendingMastery = s.PendingMastery,
                    RunCalx = s.RunCalx,
                    StakeGateCleared = s.StakeGateCleared,
                    GuaranteeWeek = s.GuaranteeWeek,
                    // Rebased pools: the profile's count now is where this run's gain is measured from.
                    RunStartDiamondBoxes = _profile.Boxes.Diamond,
                    RunStartBlackDiamondBoxes = _profile.Boxes.BlackDiamond,
                };
                // An avatar's boxes, asked again - the service answers the decision it recorded.
                if (s.AvatarUnsettledFloor > 0)
                {
                    AvatarBoxes? answered = null;
                    try { answered = await _store.AvatarBoxesAsync(_profile, s.RunId, s.AvatarUnsettledFloor); }
                    catch (Exception e) { Debug.LogWarning($"[Rift] avatar boxes, asked at settling: {e.Message}"); }
                    if (answered != null)
                    {
                        for (int i = 0; i < answered.Value.Diamond; i++) loot.FindTieredBox(Art.Gear.LootTier.Diamond);
                        for (int i = 0; i < answered.Value.BlackDiamond; i++) loot.FindTieredBox(Art.Gear.LootTier.BlackDiamond);
                    }
                }
                // Floor 100's box, asked again - the service answers the decision it recorded.
                if (s.FinalFloorUnsettled)
                {
                    FinalFloorReward? answered = null;
                    try { answered = await _store.FinalFloorRewardAsync(_profile, s.RunId); }
                    catch (Exception e) { Debug.LogWarning($"[Rift] floor {Rifts.RunLoot.FinalFloor}'s box, asked at settling: {e.Message}"); }
                    var reward = answered ?? FinalFloorReward.Unanswered;
                    loot.FindTieredBoxes(reward.Tier, reward.Count);
                    loot.SecureAllBoxes();
                    if (reward.Guaranteed) tally.GuaranteeWeek = reward.Week;
                }
                if (survived) loot.SecureAll();
                summary = await SettleRunAsync(tally, loot, survived);
            }

            _keeper.Begin(_profile.ProfileId, session);
            _keeper.End(s.RunId);
            await _keeper.FlushAsync(Tuning.Continuation.LeaveFlushSeconds);
            _waiting = null;
            return summary;
        }

        /// <summary>A hub fixture that would change the character refuses while a run waits - the run
        /// read its gear and board once, as it began. True when it refused.</summary>
        bool GuardWaitingRun()
        {
            if (_waiting?.Snapshot == null || ConfirmDialog.IsOpen) return _waiting != null;
            var s = _waiting.Snapshot;
            ConfirmDialog.ShowNotice(_canvas.transform, "A RUN IS WAITING",
                $"Your {s.Element.ToString().ToLowerInvariant()} run on floor {s.Floor} holds this character as it " +
                "went in - its gear and its mastery board stay as they are until the run ends.\n\n" +
                "Resume it, or end it, at the sigil door.",
                "OK");
            return true;
        }

        string DescribeWaiting(RunSnapshot s)
        {
            string where = s.Stage == RunStage.Cleared
                ? $"Floor {s.Floor} is cleared" + (s.PendingScreens > 0 ? " - its reward still waits for you." : ".")
                : $"Floor {s.Floor} begins again from its start.";
            int carried = s.Loot?.Carried?.Count ?? 0;
            string loot = carried == 0 ? "" : carried == 1 ? " 1 carried piece rides with it." : $" {carried} carried pieces ride with it.";
            var staked = GearStake.Staked(_profile);
            string stake = staked != null ? $" So does your staked {staked.DisplayName}." : "";
            return $"{s.Element.ToString().ToUpperInvariant()}  -  {s.Kills} kill{(s.Kills == 1 ? "" : "s")}.\n\n{where}{loot}{stake}";
        }

        /// <summary>The gate, with a run waiting: carry it on, end it, or leave it be.</summary>
        void AskWaitingRun()
        {
            if (_waiting?.Snapshot == null || ConfirmDialog.IsOpen || _state != State.Hub) return;
            ConfirmDialog.ShowChoice(_canvas.transform, "YOUR RUN WAITS", DescribeWaiting(_waiting.Snapshot),
                "END IT", "RESUME", "NOT NOW",
                onConfirm: AskEndWaiting, onAlt: ResumeRun, onCancel: null);
        }

        void AskEndWaiting()
        {
            var s = _waiting?.Snapshot;
            if (s == null || ConfirmDialog.IsOpen) return;
            int carried = s.Loot?.Carried?.Count ?? 0;
            string body = $"It ends as a death on floor {s.Floor}. You keep the {s.Kills} kill{(s.Kills == 1 ? "" : "s")}, " +
                          "the mastery from floors you cleared, and everything secured at a Rift." +
                          (carried == 0 ? "" : carried == 1 ? " The 1 carried piece is lost." : $" The {carried} carried pieces are lost.");
            var staked = GearStake.Staked(_profile);
            if (staked != null) body += $" Your staked {staked.DisplayName} is destroyed" +
                                        (_profile.StakeInsured ? " - unless insured, which brings it home." : ".");
            ConfirmDialog.Show(_canvas.transform, "END THE RUN?", body, "END IT", "KEEP IT",
                onConfirm: EndWaitingRun, onCancel: AskWaitingRun);
        }

        async void EndWaitingRun()
        {
            var w = _waiting;
            if (w?.Snapshot == null || _state != State.Hub) return;
            try
            {
                var summary = await SettleWaitingAsync(w, survived: false);
                await ShowHubAsync();
                ConfirmDialog.ShowNotice(_canvas.transform, "RUN ENDED",
                    summary == null ? "The run had already ended." :
                    $"Floor {summary.Floors}  -  {summary.Kills} kill{(summary.Kills == 1 ? "" : "s")}." +
                    (_stakeLine != null ? "\n\n" + _stakeLine.Replace("stake        ", "Stake: ") : ""),
                    "OK");
            }
            catch (Exception e) { Debug.LogError($"[Run] ending the waiting run failed: {e}"); }
        }

        async void ResumeRun()
        {
            try { await ResumeRunAsync(); }
            catch (Exception e) { Debug.LogError($"[Convergence] ResumeRun failed: {e}"); }
        }

        /// <summary>
        /// THE RESUME. The run is claimed (from now this session writes it; any other is told to
        /// stop), the run's systems are put back from its snapshot - planner, ledger, loot rebased
        /// on the profile now, the wheel, kills and streak, health and armour at the lower of
        /// snapshot and beat - and the floor is built again under the black: from its start, or as
        /// its cleared room. No run-start checkpoint: this is the same run.
        /// </summary>
        async System.Threading.Tasks.Task ResumeRunAsync()
        {
            if (_state != State.Hub || _waiting?.Snapshot == null || !ReadyForRun("ResumeRun")) return;

            string session = Guid.NewGuid().ToString("N");
            var w = await _runs.ClaimAsync(_profile.ProfileId, _waiting.Snapshot.RunId, session);
            if (_state != State.Hub) return;
            if (w?.Snapshot == null)
            {
                // Ended elsewhere, or the store could not be reached - ask again what waits.
                await LookUpWaitingRunAsync();
                ConfirmDialog.ShowNotice(_canvas.transform, "THE RUN COULD NOT BE RESUMED",
                    _waiting == null ? "It has already ended - on another device, or in another window."
                                     : "Its save could not be reached. Try again in a moment.",
                    "OK");
                return;
            }
            var s = w.Snapshot;
            // The run comes back in the FORMAT it started in (Core.RunFormat) - its rules are its own.
            if (!string.IsNullOrEmpty(s.Format))
                Core.RunFormat.Current = s.Format == "short" ? Core.RunFormatId.ShortDescent : Core.RunFormatId.Classic;
            // The run clock carries on from the last beat heard (Core.RunClock) - never restarted.
            Clock.Restore(w.RunClock);

            await LeaveHubAsync();

            _runId = s.RunId;
            _finalAsk = null;
            _avatarAsk = null;
            _runGuaranteeWeek = s.GuaranteeWeek;
            _pendingMastery = s.PendingMastery;
            _runCalx = s.RunCalx;
            _runCalxTaken = s.RunCalxTaken;
            _runStartTime = Time.time - s.RunSeconds;
            _planner = s.Planner?.Copy() ?? new Rifts.FloorPlanner(Guid.NewGuid().GetHashCode());
            _alive.Clear();
            _spawnQueue.Clear();

            // The loot's banked pools, rebased on the profile as it is now - and from here measured
            // from it, which is what the next snapshot records as the run's start.
            _loot.Restore(s.Loot, s.RunStartRiftBoxes, s.RunStartBoxes, _profile);
            _runStartRiftBoxes = _profile.RiftBoxes;
            _runStartBoxes = CopyBoxes(_profile.Boxes);
            _runStartDiamondBoxes = _profile.Boxes.Diamond;
            _runStartBlackDiamondBoxes = _profile.Boxes.BlackDiamond;

            // The ledger (and a spire's boon still riding it) BEFORE the player is built: max health
            // and the wheel's size are read from it there.
            Modifiers.Restore(s.Ledger);
            _floorBoon = s.FloorBoon;
            _floorBoonFloorsLeft = s.FloorBoonFloorsLeft;
            if (_floorBoon >= 0) Modifiers.SetFloorBoon(Hazards.SpireBoons.Apply((Hazards.SpireBoon)_floorBoon));
            _stakeGateCleared = s.StakeGateCleared;
            _stakeLine = null;
            _profile.Wear.Entries = w.Wear();

            BeginRunSession(session);
            _runSave = s;
            _resumeGivenUp = s.Stage == RunStage.Entering && s.PuzzleGivenUp;
            _resumeBossDown = s.Stage == RunStage.Entering ? w.BossEighthsDown : 0;
            if (s.Stage == RunStage.Entering)
            {
                _resumeClockSpent = w.ClockSpent;
                _resumeRiftCollapsed = s.RiftCollapsed;
                _resumePlaced = w.Placed;
                _resumePos = new Vector2(w.PosX, w.PosY);
            }
            _rewardRerolls = s.Stage == RunStage.Cleared ? s.RewardRerolls : 0;

            BuildRunScene(s.Element);
            _player.RestoreRun(s);
            if (_player.Effects != null) _player.Effects.RestoreCounters(s);
            _player.Health.Restore(w.Health);
            _hud.Flash(s.Stage == RunStage.Cleared ? $"run resumed  -  floor {s.Floor}, cleared"
                                                   : $"run resumed  -  floor {s.Floor}, from its start");
            Debug.Log($"[Run] resumed {s.RunId} at floor {s.Floor} ({s.Stage}), {w.Health:0} hp" +
                      (w.Beat != null && w.Beat.Seq == s.Seq ? $" (beat {w.Beat.Health:0}, snapshot {s.Health:0})" : ""));

            _waiting = null;
            _state = State.Playing;
            if (s.Stage == RunStage.Entering)
            {
                _floor = s.Floor - 1;
                StartCoroutine(BuildUnderBlack(NextFloor()));
            }
            else StartCoroutine(BuildUnderBlack(ResumeClearedRoom(s)));
        }

        /// <summary>A floor built under the black screen, then revealed - FloorTransition without the
        /// walk to a door (see it for why the pause and the try/finally).</summary>
        IEnumerator BuildUnderBlack(IEnumerator build)
        {
            _transitioning = true;
            GamePause.Hold(this);
            try
            {
                yield return build;
                yield return _fade.FadeIn(DoorFadeSeconds);
            }
            finally
            {
                _transitioning = false;
                GamePause.Release(this);
                _fade.Clear();
            }
        }

        /// <summary>
        /// A CLEARED floor, stood up again: its scenery and walls, the open door, the Rift as it was
        /// (open, or a Red one still sealed - and breakable), and gear still lying where it fell.
        /// No hazards or enemies - the fight is over. Then whatever screens were still to come.
        /// </summary>
        IEnumerator ResumeClearedRoom(RunSnapshot s)
        {
            int token = _runToken;
            _spawning = true;
            _floor = s.Floor;
            // A Short Descent left during its ending: its final floor was cleared and written down -
            // the run ends now, as it would have when the ending closed.
            if (Core.RunFormat.Short && s.Floor >= Rifts.RunLoot.FinalFloor)
            {
                _spawning = false;
                yield return null;
                if (token == _runToken && _state == State.Playing) EndRun(survived: true);
                yield break;
            }
            _plan = new Rifts.FloorPlan(s.Floor, s.PlanCategory, s.PlanRift, s.PlanForced);
            _rewardTaken = true;
            _puzzleGivenUp = s.PuzzleGivenUp;
            _fightStart = -1f;
            _stuckWatch?.NotifyFloorChanged();
            DressArena(_floor);
            if (_hazardRoot != null)
                foreach (Transform child in _hazardRoot) Destroy(child.gameObject);
            bool walled = !IsBossFloor(_floor) && (s.PlanCategory != Rifts.FloorCategory.Puzzle || s.PuzzleGivenUp);
            Hazards.RoomWalls.Apply(walled ? _planner.ShapeFor(_floor) : null, _hazardRoot);

            // Realtime: built under BuildUnderBlack's pause (NextFloor's note on why).
            yield return new WaitForSecondsRealtime(0.4f);
            if (token != _runToken || _state != State.Playing || _player == null) { _spawning = false; yield break; }

            OpenFloorDoor();
            switch (s.Rift)
            {
                case RiftSave.Open:
                    if (TearRift())
                    {
                        _rift.Secured = s.RiftSecured;
                        _hud?.SetRiftPrompt(true);
                    }
                    break;
                case RiftSave.Sealed:
                    OpenRedRift();
                    break;
            }
            if (s.Ground != null)
                foreach (var g in s.Ground)
                    if (g?.Record != null) LayGearAt(new Vector2(g.X, g.Y), g.Record);
            if (s.GroundBoxes != null)
                foreach (var b in s.GroundBoxes)
                    if (b != null) LayRiftBoxAt(new Vector2(b.X, b.Y), b.Life);
            if (s.FinalFloorUnsettled) ClaimFinalFloor();
            if (s.AvatarUnsettledFloor > 0) ClaimAvatarBoxes(s.AvatarUnsettledFloor);
            _spawning = false;

            if (s.PendingScreens >= 2) OfferDeal();
            else if (s.PendingScreens == 1) OfferFloorRewardCards();
        }

        // ---- leaving a run

        /// <summary>The pause menu's bottom ring: suspend the run for later, or abandon it.</summary>
        void AskLeaveRun()
        {
            if (ConfirmDialog.IsOpen || _state != State.Playing) return;

            // A run the store never took (its run start never reached the service) cannot wait for
            // anyone - leaving it is abandoning it.
            if (_runSave == null || _keeper.Refusing || _keeper.Dead) { AskAbandonRun(); return; }

            string where = _runSave.Stage == RunStage.Cleared
                ? "it picks up here, in this cleared room"
                : $"floor {_floor} starts again from its beginning, at the health you have now";
            ConfirmDialog.ShowChoice(_canvas.transform, "LEAVE THE RUN?",
                $"SUSPEND keeps it: the run waits for you at the sigil door, and {where}. " +
                "Until then this character's gear and mastery board stay as they are.\n\n" +
                "ABANDON ends it here, as a death.",
                "ABANDON", "SUSPEND", "KEEP PLAYING",
                onConfirm: AskAbandonRun, onAlt: SuspendRun, onCancel: OpenPauseMenu);
        }

        /// <summary>
        /// SUSPEND: the run is written as it stands - a cleared room as a fresh snapshot, a fight as
        /// a last heartbeat on its floor-start snapshot - and the player goes back to the hub. Nothing
        /// is settled: the run, its stake with it, waits at the gate.
        /// </summary>
        async void SuspendRun()
        {
            if (_runSave == null || _state != State.Playing) return;
            try
            {
                // A cleared room with nothing left to show is written as it stands; one with a screen
                // up already has its resume point (ResaveCleared says why it must not be redone).
                if (_runSave.Stage == RunStage.Cleared) ResaveCleared();
                // Nothing more of the run plays from here - no death, no floor logic, no further writes.
                _state = State.RunOver;
                GamePause.Hold(this);
                await RunAsTask(_fade.FadeOut(DoorFadeSeconds));

                // The beat goes once any snapshot ahead of it has landed (a beat waiting behind one
                // is dropped - it belongs to the snapshot before).
                bool sent = await _keeper.FlushAsync(Tuning.Continuation.LeaveFlushSeconds);
                if (_runSave.Stage == RunStage.Entering && _player != null && _player.Health != null)
                {
                    _beatAt = 0f;
                    _keeper.Beat(BeatNow());
                    sent = await _keeper.FlushAsync(Tuning.Continuation.LeaveFlushSeconds) && sent;
                }
                if (!sent) Debug.LogWarning("[Run] left before the last write landed - the run waits as its store last heard it");
                Debug.Log($"[Run] suspended {_runId} on floor {_floor} ({_runSave.Stage})");

                _runSave = null;
                TeardownRun();   // releases every pause, this one included
                await LookUpWaitingRunAsync();
                await ShowHubAsync();
                await RunAsTask(_fade.FadeIn(DoorFadeSeconds));
            }
            catch (Exception e) { Debug.LogError($"[Run] suspending failed: {e}"); }
        }

        /// <summary>Another session took this run over (a resume elsewhere): it is left here unsettled -
        /// it carries on there.</summary>
        void OnRunSuperseded()
        {
            if (_state != State.Playing) return;
            _state = State.RunOver;
            _runSave = null;
            Debug.LogWarning($"[Run] {_runId} was resumed in another session - leaving it here, unsettled");
            TeardownRun();
            ShowHub();
            ConfirmDialog.ShowNotice(_canvas.transform, "RUN CONTINUED ELSEWHERE",
                "This run was resumed in another window or on another device, and carries on there. " +
                "Nothing of it was lost - it can be taken back at the sigil door.",
                "OK");
            // The gate shows what the store holds - the run, as the other session last wrote it.
            RefreshWaiting();
        }

        async void RefreshWaiting()
        {
            try { await LookUpWaitingRunAsync(); }
            catch (Exception e) { Debug.LogWarning($"[Run] could not refresh the waiting run: {e.Message}"); }
        }

        void ShowRunOver(RunSummary s)
        {
            TeardownRun();

            _overScreen = new GameObject("RunOver", typeof(RectTransform));
            _overScreenFrame = Time.frameCount;
            _overScreen.transform.SetParent(_canvas.transform, false);
            var full = (RectTransform)_overScreen.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.045f, 0.06f, 0.93f));

            var box = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-330, -190), new Vector2(330, 190), new Color(0.09f, 0.10f, 0.13f, 0.98f));

            var t = UiKit.Rect(box, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(0, -34));
            UiKit.Label(t, "RUN OVER", 44, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            var body = UiKit.Rect(box, "body", new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 92), new Vector2(-40, -110));
            UiKit.Label(body,
                $"element      {s.Element.ToString().ToUpper()}\n" +
                $"floors       {s.Floors}\n" +
                $"kills        {s.Kills}\n" +
                $"duration     {s.DurationSeconds:0}s\n" +
                (_stakeLine != null ? _stakeLine + "\n" : "") + "\n" +
                $"profile      lvl {_profile.Level}   {_profile.TotalRuns} runs   best {_profile.BestKills}\n" +
                $"checkpoint   committed to IProfileStore",
                21, new Color(0.78f, 0.80f, 0.86f), TextAnchor.UpperLeft);

            var foot = UiKit.Rect(box, "foot", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 70));
            UiKit.Label(foot, "[ R ] return to the hub", 22,
                new Color(0.55f, 0.58f, 0.66f), TextAnchor.MiddleCenter);
        }

        void ReturnToHub()
        {
            if (_overScreen) Destroy(_overScreen);
            if (_enemyRoot) Destroy(_enemyRoot.gameObject);
            if (_hazardRoot) Destroy(_hazardRoot.gameObject);
            ShowHub();
        }
    }
}
