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

        [Header("Durability")]
        [Tooltip("Durability each armour piece loses per hit taken.")]
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
        [Tooltip("Account XP granted by one XP upgrade card. Placeholder - the progression " +
                 "system this feeds has not been designed yet.")]
        public int XpPerPick = 150;

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

        Transform _arenaRoot, _enemyRoot, _hazardRoot;
        PlayerController _player;
        StuckWatch _stuckWatch;
        Hud _hud;
        Canvas _canvas;
        Canvas _touchCanvas;
        Camera _cam;
        UI.ScreenFade _fade;

        GameObject _overScreen;

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
        /// Account XP banked from floor upgrades this run. Global progression, not run-scoped -
        /// it is added to the profile at the run-end checkpoint alongside kill XP.
        /// </summary>
        int _bonusXp;

        /// <summary>
        /// Element mastery earned this run, unbanked. Held here rather than written straight to
        /// the profile so a false start can throw it away - giving up current progression for
        /// another attempt to go deeper is the whole point of that mechanic.
        /// </summary>
        int _pendingMastery;
        int _pendingGearVouchers;

        /// <summary>
        /// Bumped by every teardown. Async continuations and coroutines capture it and bail if it
        /// has moved on - a run that has already ended must never mutate the one that replaced it.
        /// </summary>
        int _runToken;

        /// <summary>
        /// The run's equivalent-exchange ledger. Run-scoped and never persisted: these are what
        /// you agreed to on the way down, and they die with the run.
        /// </summary>
        public Exchange.RunModifiers Modifiers { get; } = new();

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

            _profile = await _store.LoadAsync("local-dev-0");

            // No wallet is connected, so the account is the local one. Connecting a wallet later
            // means loading by its address, and adopting this record if that address has none.
            _account = await _accounts.LoadAsync(AccountProfile.LocalId);

            // Saved loadouts store the slot as an int, so any reshuffle of GearSlot re-points them
            // at the wrong slot. Prune anything whose item no longer belongs where it sits.
            int dropped = _profile.Gear.DropStale();
            if (dropped > 0)
                Debug.Log($"[ProfileStore] dropped {dropped} loadout entries that no longer match their slot.");
            RegisterMintedGear(_profile);
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
                if (_player.Split != null) _player.Split.Blades = shown?.SplitBlades;
                _player.CombinedFrames = shown?.CombinedFrames;          // Quintessence's overhead picture
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
            var lost = GearStake.ForfeitStale(_profile);
            if (lost == null) return;
            Debug.Log($"[Stake] {lost.DisplayName} ({lost.InstanceId}) was staked on a run that never " +
                      "ended - destroyed.");
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Spends one Forge voucher on a freshly-rolled item for the given slot. Local stand-in
        /// for what web/service's own POST /write/redeem-gear will eventually do on chain - see
        /// that service's README for the wiring this is meant to converge with. Seeded by the
        /// new instance id, matching GearRoller's own "fixed to identity" rule elsewhere.
        /// </summary>
        public async void RedeemGearVoucher(Art.Gear.GearSlot slot)
        {
            if (_profile.PendingGearVouchers <= 0) return;

            _profile.PendingGearVouchers--;
            string instanceId = $"GEAR-{slot}-{Guid.NewGuid():N}";
            var (tier, grants, primary, subs, ability, finisher) =
                Art.Gear.GearRoller.RollItem(slot, instanceId.GetHashCode());

            var record = new MintedGearRecord
            {
                InstanceId = instanceId,
                DisplayName = $"{tier} {slot}",
                Slot = slot,
                Tier = tier,
                Grants = grants,
                PrimaryStat = primary,
                SubStats = subs,
                DefensiveAbility = ability,
                Finisher = finisher,
                Class = Art.Gear.WeaponClass.Greatsword,
                StatsVersion = Art.Gear.GearRoller.TablesVersion,
            };
            _profile.MintedGear.Add(record);
            Art.Gear.GearCatalog.Register(record.ToGearItem());

            Debug.Log($"[Forge] redeemed {record.DisplayName} ({instanceId}), " +
                      $"{_profile.PendingGearVouchers} voucher(s) remaining");

            if (CharacterScreen != null) CharacterScreen.Refresh();
            _hub?.FlashForge();
            _hub?.RefreshForge();
            await _store.UnlockAsync(_profile);
        }

        /// <summary>
        /// Spend a box of a given tier at the Forge for a RANDOM slot - the cheap, common path.
        /// Guarantees the box's own tier (unlike a voucher, which rolls Silver/Gold via
        /// GearRoller.RollTier) - see RollItem's fixed-tier overload.
        /// </summary>
        public async void RedeemForgeBoxRandom(Art.Gear.LootTier boxTier)
        {
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
            await MintForgeBoxItem(slot, boxTier, Art.Gear.WeaponClass.Greatsword);
        }

        /// <summary>
        /// Spend a box of a given tier at the Forge for a CHOSEN slot (and weapon class, if the
        /// slot is Weapon) - costs more than the random path because the player is paying to
        /// remove the randomness from which slot they get, not from what it rolls.
        /// </summary>
        public async void RedeemForgeBoxTargeted(
            Art.Gear.GearSlot slot, Art.Gear.LootTier boxTier,
            Art.Gear.WeaponClass weaponClass = Art.Gear.WeaponClass.Greatsword)
        {
            if (!TrySpendBoxes(boxTier, Tuning.GearRoll.ForgeTargetedRedeemBoxCost)) return;
            if (DesignDrops.IsDesignTier(boxTier))
            {
                var classFilter = slot == Art.Gear.GearSlot.Weapon ? weaponClass : (Art.Gear.WeaponClass?)null;
                await MintDesignBox(DesignDrops.Pool(boxTier, slot, classFilter), boxTier,
                                    Tuning.GearRoll.ForgeTargetedRedeemBoxCost);
                return;
            }
            await MintForgeBoxItem(slot, boxTier, weaponClass);
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
                DisplayName = $"{tier} {slot}",
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
        /// Drains a box of the given tier, falling back to Rift Boxes ONLY for Silver - the one
        /// tier close enough to Rift Boxes' own "untiered, common/mid" spirit that treating one
        /// as the other isn't an arbitrary call. Bronze/Gold/Diamond/BlackDiamond accept only
        /// their own box.
        /// </summary>
        bool TrySpendBoxes(Art.Gear.LootTier tier, int amount)
        {
            if (amount <= 0) return true;
            if (_profile.Boxes.Get(tier) >= amount)
            {
                _profile.Boxes.Add(tier, -amount);
                return true;
            }
            if (tier == Art.Gear.LootTier.Silver && _profile.RiftBoxes >= amount)
            {
                _profile.RiftBoxes -= amount;
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
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = wallArt != null ? wallArt : Spr.Square;
            // A warm dark stone, to sit with the brick floor - the old cold grey read as a
            // different, unlit material next to it.
            sr.color = wallArt != null ? Color.white : new Color(0.21f, 0.17f, 0.13f);
            sr.sortingOrder = -8;

            go.AddComponent<BoxCollider2D>();
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
                       () => TransmutationScreen.Open(_canvas.transform, _profile.LastElement),
                       () => MasteryScreen.Open(_canvas.transform),
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
                       DressArmoury);
        }

        /// <summary>Opens the Forge's slot picker. Reachable only through the fixture's own
        /// interact key, which already gates on there being a voucher to spend.</summary>
        void OpenForge()
        {
            if (_profile == null) return;
            ForgeScreen.Open(_canvas.transform, new UI.ForgeScreen.Context
            {
                VoucherCount = _profile.PendingGearVouchers,
                Boxes = _profile.Boxes,
                RiftBoxes = () => _profile.RiftBoxes,
                MintedGear = _profile.MintedGear,
                IsEquipped = IsEquipped,
                OnPickVoucher = RedeemGearVoucher,
                OnRedeemRandom = RedeemForgeBoxRandom,
                OnRedeemTargeted = RedeemForgeBoxTargeted,
                OnCombine = CombineGear,
                OnReroll = RerollSubStat,
                OnFuse = FuseGear,
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
            if (!Chain.Web.ChainWallet.Connected) return;   // the booth's own prompt already gates this

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
            if (_state != State.Hub) return;
            if (string.IsNullOrEmpty(profileId) || profileId == _profile.ProfileId) return;

            try
            {
                var next = await _store.LoadAsync(profileId);
                if (_state != State.Hub) return;   // a run started while the load was in flight

                _profile = next;
                _profile.Gear.DropStale();
                RegisterMintedGear(_profile);
                await ForfeitStaleStake();
                Mastery = new Progression.BoardState(_profile.Mastery);
                if (EnsureStarterGear(_profile)) await _store.UnlockAsync(_profile);

                Debug.Log($"[Convergence] now playing {_profile.DisplayName} ({_profile.ProfileId})");
                ShowHub();   // rebuilds the room, the standing character and the couch
            }
            catch (Exception e)
            {
                Debug.LogError($"[Convergence] could not switch to {profileId}: {e}");
            }
        }

        /// <summary>
        /// Start a new character on the next free id.
        ///
        /// Ids are scanned rather than counted: deleting profile_local-dev-1 and then counting
        /// files would hand the next character an id that is already taken and silently overwrite
        /// somebody.
        /// </summary>
        async void NewCharacter()
        {
            if (_state != State.Hub) return;

            var taken = new HashSet<string> { _profile.ProfileId };
            foreach (var p in _roster) taken.Add(p.ProfileId);

            string id = null;
            for (int i = 0; i < 64; i++)
            {
                var candidate = $"local-dev-{i}";
                if (taken.Contains(candidate)) continue;
                id = candidate;
                break;
            }
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

        void HideHub()
        {
            if (_hub == null) return;

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
                      || TransmutationScreen.IsOpen || ShowcasePicker.IsOpen || ExchangeScreen.IsOpen
                      || TransmuteScreen.IsOpen
                      || TxScreen.IsOpen || CharacterScreen.IsOpen || ForgeScreen.IsOpen
                      || ManualScreen.IsOpen || GearDisplayScreen.IsOpen || RiftScreen.IsOpen || StakeScreen.IsOpen
                      || PauseScreen.IsOpen || SettingsScreen.IsOpen || ControlsScreen.IsOpen
                      || InventoryScreen.IsOpen;
            bool modal = screenOpen || (_hub != null && _hub.Blocking);

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
                    if (Controls.CancelTapped) { SettingsScreen.Close(); return; }
                }
                else if (!modal && Controls.SettingsTapped)
                {
                    SettingsScreen.Open(_canvas.transform, OpenControlsScreen);
                    return;
                }
            }

            // Everything below drives the game itself, and a modal is by definition covering it.
            // Without this, a click meant for the mastery grid fell through to the element cards
            // underneath and started a run, and Esc aimed at a confirm prompt re-opened it.
            if (MasteryScreen.IsOpen || ConfirmDialog.IsOpen || FloorRewardScreen.IsOpen ||
                TransmutationScreen.IsOpen || ShowcasePicker.IsOpen || ExchangeScreen.IsOpen ||
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
                bool toggle = Controls.LoadoutTapped
                           || (CharacterScreen.IsOpen && Controls.CancelTapped);
                if (toggle) { CharacterScreen.Toggle(_canvas.transform, ScreenElement()); return; }
            }
            else if (_state == State.Playing && CharacterScreen.IsOpen
                     && (Controls.LoadoutTapped || Controls.CancelTapped))
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
                Time.timeScale = 1f;
            }

            if (_state == State.Hub)
            {
                // The room drives itself; it owns the keyboard while its enlarged frame view is up.
                if (_hub != null && _hub.Blocking) return;

                // Number keys still work. Kept deliberately - walking to a door is the experience,
                // but a shortcut past it costs nothing and is what makes the menu testable from
                // the CLI without driving a character around.
                for (int i = 0; i < 4; i++)
                {
                    if (Controls.DigitTapped(i)) { StartRun(_order[i]); return; }
                }
            }
            else if (_state == State.Playing)
            {
                // Esc / Back, and the loadout key too, open the paused radial menu. The loadout
                // sheet, the carried-loot screen and abandoning the run are its three points -
                // abandoning is one option among them rather than a bare keystroke. Returning
                // here matters for the same reason it always did: the floor-clear check below
                // must not run on the frame a menu goes up.
                if (Controls.CancelTapped || Controls.LoadoutTapped) { OpenPauseMenu(); return; }

                _alive.RemoveAll(e => e == null);
                _hud?.SetFloorInfo(_floor, _alive.Count, _player ? _player.Kills : 0);

                // Practice-dummy mode holds the floor open forever: its target is never in
                // _alive, so without this the floor would read as cleared on the first frame and
                // the reward screen would land on top of the thing being studied.
                //
                // Floor 0 -> 1 still has to run, because this same branch is what starts the
                // FIRST floor - blocking it outright meant the dummy was never spawned at all.
                bool dummyHoldsFloor = Tuning.Testing.PracticeDummy && _floor > 0;

                // Telemetry only - never resolves anything. See StuckWatch for why a detector
                // exists alongside player reports rather than instead of them.
                bool settled = !dummyHoldsFloor && !_spawning && _puzzle == null && _boss == null && !FloorRewardScreen.IsOpen
                               && !MasteryScreen.IsOpen && !ConfirmDialog.IsOpen && !ExchangeScreen.IsOpen;
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
                if (!dummyHoldsFloor && _boss == null && _activeDoor == null && _puzzle == null &&
                    !_transitioning && !_spawning && _alive.Count == 0 && _spawnQueue.Count == 0 &&
                    !FloorRewardScreen.IsOpen && !MasteryScreen.IsOpen &&
                    !ConfirmDialog.IsOpen && !ExchangeScreen.IsOpen)
                {
                    if (_floor > 0 && !_rewardTaken) OfferFloorReward();
                    else StartCoroutine(NextFloor());
                }

            }
            else if (_state == State.RunOver)
            {
                if (Controls.AnyDismiss) ReturnToHub();
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
            anchor = Vector3.Lerp(
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
        /// </summary>
        void EnterGate(ElementType element)
        {
            if (_state != State.Hub || StakeScreen == null || StakeScreen.IsOpen) return;
            var candidates = GearStake.Candidates(_profile);
            if (candidates.Count == 0) { StartRun(element); return; }
            StakeScreen.Open(_canvas.transform, candidates,
                onChoose: chosen => StartRun(element, chosen),
                onCancel: null);
        }

        void StartRun(ElementType element) => StartRun(element, null);

        async void StartRun(ElementType element, MintedGearRecord stake)
        {
            // async void swallows exceptions into the sync context, where Unity reports them with
            // useless line numbers from the state machine. Catch here so failures are legible.
            try { await StartRunAsync(element, stake); }
            catch (Exception e) { Debug.LogError($"[Convergence] StartRun failed: {e}"); }
        }

        async System.Threading.Tasks.Task StartRunAsync(ElementType element, MintedGearRecord stake)
        {
            // Start() is async, so nothing it creates is guaranteed to exist on the first frames.
            // Everything StartRunAsync touches before its await is checked here, by name, because
            // async stack traces report useless line numbers and hide which field was null.
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
            if (missing != null)
            {
                Debug.LogWarning($"[Convergence] StartRun before init finished ({missing} is null) - ignored.");
                return;
            }

            // The same door beat FloorTransition plays between floors, at the one other threshold
            // in the game that is actually a door: the hub's own sigil door. Grabbed BEFORE
            // TeardownRun/HideHub, which destroy the hub avatar this rig belongs to - by the time
            // the screen is black beneath, that no longer matters.
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

            int token = _runToken;
            _runId = Guid.NewGuid().ToString("N")[..8];
            _bonusXp = 0;
            _pendingMastery = 0;
            _pendingGearVouchers = 0;
            _runStartTime = Time.time;
            _floor = 0;
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
            Modifiers.Clear();

            // The stake is placed BEFORE the run-start checkpoint so the datum carries it - see
            // CharacterProfile.StakedInstanceId. Placing overwrites; a superseded start's stake
            // is replaced by this one's rather than forfeited, since that run never began.
            _stakeGateCleared = false;
            _stakeLine = null;
            if (stake != null && GearStake.Place(_profile, stake))
                Debug.Log($"[Stake] {stake.DisplayName} +{stake.UpgradeLevel} staked, gate floor {_profile.StakeGateFloor}");
            else
            {
                _profile.StakedInstanceId = "";
                _profile.StakeGateFloor = 0;
            }

            // CHECKPOINT 1 of 2 - the only writes this prototype makes.
            await _store.BeginRunAsync(_profile, _runId, element);
            if (token != _runToken) return;   // superseded while the checkpoint was in flight

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
                () => _player.IncomingDamageMultiplier?.Invoke() ?? 1f,
                () => _bonusXp);
            _hud.Build(_player, _canvas.transform);
            var staked = GearStake.Staked(_profile);
            if (staked != null)
                _hud.Flash($"{staked.DisplayName} staked  -  clear floor {_profile.StakeGateFloor}, then extract");

            await RunAsTask(_fade.FadeIn(DoorFadeSeconds));
            _state = State.Playing;
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
            // Patina (worn armour stops counting against you) and Lightfoot (Graze counts double
            // at full speed).
            pc.IncomingDamageMultiplier = () =>
            {
                var m = Modifiers.Current;
                float wear = m.ArmourWearIgnored ? 1f : _profile.Wear.DamageTakenMultiplier(_profile.Gear, pc.Stats.Armor);
                float graze = pc.Stats.Graze * (pc.Effects != null && pc.Effects.GrazeDoubled ? 2f : 1f);
                return wear
                       * Art.Gear.StatPercents.ReductionFactor(pc.Stats.Resilience)
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
            hp.ScaleIncoming = info => board.ScaleIncoming(info) * effects.ScaleIncoming(info);
            hp.ModifyHeal = effects.ModifyHeal;
            hp.SurviveLethal = effects.SurviveLethal;

            // Salt reads the hit AFTER it resolved, so the stack that fills the Ward is itself
            // reduced by the stacks already held - see PrincipleEffects.OnDamaged.
            hp.Damaged += info => { if (!info.Price) principles.OnDamaged(info.Amount); };

            // The transmutation circle counts the blows an ENEMY lands while the player stands in
            // it; the player's own landed swings and throws come through the ledger's hook.
            hp.Damaged += info => { if (!info.Price && Exchange.RunEffects.FromEnemy(info, go)) CircleContact(); };
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
                if (m.ArmourNeverWears || info.Price) return;   // a price the run charges is not a blow
                float resistFactor = Art.Gear.StatPercents.ReductionFactor(pc.Stats.DamageResistance);
                _profile.Wear.Wear(_profile.Gear, Art.Gear.SlotKind.Armor,
                                   ArmorWearPerHit * m.ArmourWearMul * resistFactor,
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
            _spawning = true;
            CloseRift();   // a Rift left unanswered closes behind the player
            ClearPuzzle();
            _floor++;
            _rewardTaken = false;
            // A spire's boon is the floor's, not the run's: it ends at the door - unless Lodestone
            // carries it a floor further.
            if (_floorBoonFloorsLeft > 0) _floorBoonFloorsLeft--;
            else Modifiers.SetFloorBoon(null);
            _stuckWatch?.NotifyFloorChanged();
            _fightStart = -1f;
            _plan = _planner != null ? _planner.Plan(_floor)
                                     : new Rifts.FloorPlan(_floor, Rifts.FloorCategory.Combat, Rifts.RiftKind.None, false);
            Debug.Log($"[Floor] plan {_plan}");

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
            if (_plan.Category == Rifts.FloorCategory.Puzzle)
            {
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

            if (_hazardRoot != null && !ownRoom)
            {
                bool hurt = _player.Health.Current < _player.Health.Max - 0.5f;
                bool worn = _profile.Wear.Condition01(_profile.Gear, Art.Gear.SlotKind.Armor,
                                                      _player.Stats?.Armor ?? 0f) < 0.995f;
                _spirePool = 0f;   // set once the wave is queued; 0 holds the spire down
                bool circle = CircleDue();
                _spire = HazardBuilder.Populate(_floor, Arena.HalfExtents, _player.transform, _hazardRoot,
                                                spireAllowed: !IsBossFloor(_floor), hurt, worn,
                                                stormAllowed: !IsBossFloor(_floor), circle: circle);
                _circle = HazardBuilder.LastRing;
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
            var wave = ComposeWave(_floor);
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
            if (_planner == null || !Modifiers.HoldsNigredo) return false;
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
            if (albedo != null)
            {
                _hud?.Flash($"{cost.Name} transmuted  -  {albedo.Name}: {albedo.Effect}");
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
                                 && floor <= Tuning.Enemy.BoosterMaxFloor;
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
            _rewardTaken = true;

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
            OpenFloorDoor();
            RollFloorDrop();
            SettleFloorRift();

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

            // Scrying Glass. Read HERE, after SettleFloorRift: the floor below's plan depends on
            // whether this floor's Rift was reached, and nothing between now and the door moves it.
            string scry = Modifiers.StacksOf("scrying_glass") > 0 ? ScryNextFloor() : null;
            if (scry != null) Debug.Log($"[Exchange] Scrying Glass: {scry}");

            // A deal after the first floor and every second floor after it (50 a run) - see
            // Tuning.Exchange.DealEvery. Drawn from the run's seed for this floor, so a restart
            // meets the same deals.
            if (!Exchange.ExchangeOffers.DealAfter(_floor) || _planner == null)
            {
                if (scry != null) _hud?.Flash(scry);
                OfferFloorRewardCards();
                return;
            }
            var offer = Exchange.ExchangeOffers.Build(Modifiers, _floor, new System.Random(_planner.DealSeed(_floor)));
            if (offer.Pairs.Count == 0)
            {
                if (scry != null) _hud?.Flash(scry);
                OfferFloorRewardCards();
                return;
            }

            // Transmuter's Eye. The reward is rolled ONCE, here, and the same roll is both
            // previewed and later handed to the reward screen - a preview that re-rolled would
            // be a lie, and is the obvious way to get this wrong.
            _pendingReward = RollFloorReward();
            string preview = Modifiers.StacksOf("transmuters_eye") > 0
                ? $"next: {_pendingReward.RestoreText}" +
                  (_pendingReward.Offered != null ? $"  or  {_pendingReward.Offered.DisplayName}" : "")
                : null;

            // Oracle: each slate shows the deal that would follow it - built on a copy of the ledger
            // with the seed the real one will use.
            var oracle = Modifiers.StacksOf("oracle") > 0
                ? Exchange.ExchangeOffers.PreviewNext(Modifiers, offer, _floor, f => _planner.DealSeed(f))
                : null;

            ExchangeScreen.Show(_canvas.transform, offer, Modifiers, _floor, preview, pair =>
            {
                if (pair != null)
                {
                    // Boon first, so a cost that reshapes the next offer cannot be undone by a
                    // boon taken in the same breath - both are applied, in a fixed order.
                    if (pair.Boon != null) Modifiers.Take(pair.Boon);
                    if (pair.Cost != null) Modifiers.Take(pair.Cost);
                    ApplyLedgerToPlayer();
                    Debug.Log($"[Exchange] took {pair.Boon?.Name ?? "nothing"} / {pair.Cost?.Name ?? "nothing"}");
                }
                else
                {
                    if (offer.CanRefuse) Modifiers.Refuse();
                    Debug.Log($"[Exchange] refused ({Modifiers.RefusalsLeft} refusals left)");
                }

                OfferFloorRewardCards();
            }, scry, oracle);
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
            if (_planner == null || next > Player.PlayerPower.LastFloor) return null;
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
        /// The floor's reward, rolled once so it can be previewed and then handed over unchanged.
        /// </summary>
        class RolledReward
        {
            public Combat.Moveset Offered;
            public UI.FloorReward Restore;
            public string RestoreText;
            public float HealAmount;
        }

        RolledReward _pendingReward;

        /// <summary>The Repair card's fraction with Repair Received applied - that card only.</summary>
        float RepairFractionNow => RepairFraction
            * Art.Gear.StatPercents.Apply(1f, _player != null ? _player.Stats.RepairReceived : 0f)
            * Modifiers.Current.RepairMul;   // Corrosion

        RolledReward RollFloorReward()
        {
            var restore = PickRestoreKind();
            // Heal Received boosts THIS card and nothing else - not lifesteal, not boons.
            float healAmount = _player != null
                ? _player.Health.Max * HealFraction * Art.Gear.StatPercents.Apply(1f, _player.Stats.HealReceived)
                : 0f;
            return new RolledReward
            {
                Offered = _player != null
                    ? Combat.MovesetLibrary.RandomExcluding(_player.EarnedMovesets, _player.Weapon)
                    : null,
                Restore = restore,
                HealAmount = healAmount,
                RestoreText = restore == UI.FloorReward.Heal
                    ? $"+{Mathf.RoundToInt(healAmount)} HP"
                    : $"+{Mathf.RoundToInt(RepairFractionNow * 100f)}% condition",
            };
        }

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

            _pendingMastery += Mathf.RoundToInt(MasteryXpPerFloor * taper);
            _pendingGearVouchers += Tuning.GearRoll.VouchersPerFloor;

            // Reuses the roll the exchange row may already have shown. Rolling again here would
            // make Transmuter's Eye show one thing and deliver another.
            var r = _pendingReward ?? RollFloorReward();
            _pendingReward = null;

            string xpText = $"+{XpPerPick} XP";
            string xpBlurb = "Account experience, kept whether you survive or not. " +
                             $"You are level {_profile.Level} with {_profile.Xp} XP.";

            // Curator: one more card - the restore the roll did not pick, beside the one it did.
            string otherRestore = Modifiers.Current.ExtraRewardOptions > 0
                ? (r.Restore == UI.FloorReward.Heal
                    ? $"+{Mathf.RoundToInt(RepairFractionNow * 100f)}% condition"
                    : $"+{Mathf.RoundToInt(r.HealAmount)} HP")
                : null;

            FloorRewardScreen.Show(
                _canvas.transform, _floor, r.Offered,
                _player != null ? _player.Slots : System.Array.Empty<Combat.Moveset>(),
                _player != null ? _player.SlotLocked : _ => false,
                _player != null ? _player.RotationIndex : 0,
                r.Restore, r.RestoreText, xpText, xpBlurb,
                (outcome, slot) => ApplyFloorReward(outcome, slot, r.Offered, r.HealAmount),
                otherRestore);
        }

        /// <summary>
        /// Heal and repair share a slot, so the roll is weighted by what would actually help:
        /// offering a repair on pristine gear (or a heal at full health) wastes the card.
        /// </summary>
        UI.FloorReward PickRestoreKind()
        {
            bool hurt = _player != null && _player.Health.Current < _player.Health.Max - 0.5f;

            // Armour only: weapons no longer wear, so counting them here would offer a repair
            // card for damage that can never happen.
            float armour = _profile.Wear.Condition01(_profile.Gear, Art.Gear.SlotKind.Armor,
                                                      _player?.Stats.Armor ?? 0f);
            bool worn = armour < 0.995f;

            if (hurt && worn) return UnityEngine.Random.value < 0.5f
                ? UI.FloorReward.Heal : UI.FloorReward.Repair;
            if (worn) return UI.FloorReward.Repair;
            return UI.FloorReward.Heal;
        }

        void ApplyFloorReward(UI.FloorOutcome outcome, int slot, Combat.Moveset offered, float healAmount)
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
                    _bonusXp += XpPerPick;
                    Debug.Log($"[Convergence] banked {XpPerPick} account XP (run total {_bonusXp})");
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
            Debug.Log($"[Floor] Boss floor {_floor}: {_boss.DisplayName}");

            var hp = _boss.Health;
            hp.Died += h =>
            {
                if (_player) _player.RegisterKill();
                // The board hears every death, whatever caused it - a hit, a burn, a fall.
                if (_player && _player.Board) _player.Board.OnEnemyDied(h);
                if (_player && _player.Effects) _player.Effects.OnEnemyDied(h);
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
            if (!TearRift()) return;
            float seconds = _planner.CollapseSeconds(_floor);
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

        int RedGuardCount => Tuning.Floors.RedGuards + (_floor >= Tuning.Floors.RedGuardsExtraFloor ? 1 : 0);

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
        }

        /// <summary>
        /// The floor just cleared pays out - carried, not banked. AT THE CLEAR, not when the next
        /// floor starts: it used to be rolled at the top of NextFloor, so a player who extracted at
        /// this floor's Rift left without the drop they had just earned.
        /// </summary>
        void RollFloorDrop()
        {
            if (_floor <= 0) return;
            var drop = _loot.Roll(_floor);
            if (drop != null)
            {
                _loot.Add(drop);
                Debug.Log($"[Rift] floor {_floor} dropped {drop.DisplayName} " +
                          $"(carrying {_loot.CarriedCount}, secured {_loot.SecuredCount}, " +
                          $"boxes {_loot.TotalBoxes})");
            }
            else
            {
                Debug.Log($"[Rift] floor {_floor} dropped a loot box instead of an item " +
                          $"(carrying {_loot.CarriedCount}, secured {_loot.SecuredCount})");
            }
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
                    _player.Health.Heal(_player.Health.Max * Tuning.Spire.HealFraction * Modifiers.Current.SpireBoonMul);
                    break;
                case Hazards.SpireBoon.Repair:
                    _profile.Wear.Repair(_profile.Gear,
                                         Tuning.Spire.RepairFraction * Modifiers.Current.SpireBoonMul * Modifiers.Current.RepairMul,
                                         _player.Stats?.Armor ?? 0f);
                    break;
                default:
                    Modifiers.SetFloorBoon(Hazards.SpireBoons.Apply(boon));
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
            float side = (_planner.PuzzleSeed(_floor) & 1) == 0 ? -1f : 1f;
            var sideAt = new Vector2(side * (Arena.HalfExtents.x - Tuning.Puzzle.SideDoorInset),
                                     Arena.NorthDoorPoint.y);
            _sideDoor = FloorDoor.Spawn(sideAt, _player.transform, _enemyRoot, ScreenElement(), () =>
            {
                _sideDoor = null;
                if (token != _runToken) return;
                StartCoroutine(AdjacentRoom());
            });

            _hud?.Flash("solve it and the door opens  -  or take the side door and fight");
            Debug.Log($"[Floor] puzzle floor {_floor}: {kind}, side door {(side < 0 ? "west" : "east")}");
        }

        /// <summary>Reads the puzzle's state once per frame and answers it ONCE.</summary>
        void TickPuzzle()
        {
            _hud?.SetPuzzle(_puzzle.Title, _puzzle.Body);
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
            if (_puzzle != null) { Destroy(_puzzle.gameObject); _puzzle = null; }
            if (_sideDoor != null) { Destroy(_sideDoor.gameObject); _sideDoor = null; }
            // Only while still shut: once solved it is _activeDoor and the walk-through owns it.
            if (_puzzleDoor != null && _puzzleDoor != _activeDoor) Destroy(_puzzleDoor.gameObject);
            _puzzleDoor = null;
            _hud?.SetPuzzle(null, null);
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

        void HookDeath(EnemyController enemy)
        {
            var hp = enemy.GetComponent<Health>();
            hp.Died += h =>
            {
                if (_player) _player.RegisterKill();
                // The board hears every death, whatever caused it - a hit, a burn, a fall.
                if (_player && _player.Board) _player.Board.OnEnemyDied(h);
                if (_player && _player.Effects) _player.Effects.OnEnemyDied(h);

                // BASIC AND ELITE ONLY. This is HookDeath, which is the wave path - a boss has its
                // own Died handler and its own set-piece, and a weapon's cosmetic has no business
                // overriding how a boss dies. Elites come through here too and are included, being
                // a tier on an ordinary enemy rather than a kind of their own.
                if (RiftBladeDrawn())
                    Rifts.RiftImplosion.At(h.transform.position, 0.85f);
                else if (h.LastDamage.Bisects && KatanaDrawn())
                    Combat.Bisection.At(h.transform, Tuning.Katana.Tint);
                else
                    Spr.Flash(h.transform.position, 0.9f, new Color(1f, 0.9f, 0.7f), 0.3f);

                // What this body was worth against the floor's pool - the spire rises on it and
                // a Rift Box drops in proportion to it.
                float cost = Enemies.WaveComposer.Cost(enemy.Kind, enemy.Elite, _floor);
                _spireKilled += cost;
                TryDropRiftBox(h.transform.position, cost / Enemies.WaveComposer.Ehp(EnemyKind.Chaser, false, _floor));
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
        void TryDropRiftBox(Vector2 at, float chasers)
        {
            if (_player == null) return;
            if (UnityEngine.Random.value >= Tuning.Boss.RiftBoxDropChance * chasers) return;

            Rifts.RiftBoxPickup.Drop(at, _enemyRoot, _player.transform, () =>
            {
                _loot.FindBox();
                Debug.Log($"[Rift] picked up a RIFT BOX (boxes {_loot.TotalBoxes})");
                _hud?.Flash("RIFT BOX");
            });
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

        async void EndRun(bool survived)
        {
            if (_state != State.Playing) return;
            if (_profile == null) return;
            _state = State.RunOver;
            // State is already RunOver, so CharacterScreen's onClosed (OpenPauseMenu) no-ops.
            if (PauseScreen.IsOpen) PauseScreen.Close();
            if (InventoryScreen.IsOpen) InventoryScreen.Close();
            if (CharacterScreen.IsOpen) CharacterScreen.Close();
            if (FloorRewardScreen.IsOpen) FloorRewardScreen.Close();
            if (MasteryScreen.IsOpen) MasteryScreen.Close();
            if (ConfirmDialog.IsOpen) ConfirmDialog.Close();

            int kills = _player ? _player.Kills : 0;
            var element = _player && _player.Resource ? _player.Resource.Element : ElementType.Fire;

            var summary = new RunSummary
            {
                RunId = _runId,
                Element = element,
                Kills = kills,
                XpEarned = kills * XpPerKill + _bonusXp,
                Floors = _floor,
                DurationSeconds = Time.time - _runStartTime,
                Survived = survived,
                EndedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            if (_pendingMastery > 0)
            {
                var em = _profile.Mastery.For(element);
                int levels = em.Grant(_pendingMastery);
                Mastery.Invalidate();
                Debug.Log($"[Mastery] {element} +{_pendingMastery} xp" +
                          (levels > 0 ? $" -> {levels} level(s), now {em.Level}" : "") +
                          $" (total {_profile.Mastery.TotalLevel})");
                _pendingMastery = 0;
            }

            if (_pendingGearVouchers > 0)
            {
                _profile.PendingGearVouchers += _pendingGearVouchers;
                Debug.Log($"[Forge] +{_pendingGearVouchers} voucher(s), now {_profile.PendingGearVouchers}");
                _pendingGearVouchers = 0;
            }

            // THE EXTRACTION LOOP'S ONLY WRITE. Whatever was carried and never pushed through a
            // Rift is gone - that is the stake, and it is forfeited HERE rather than on the death
            // itself so there is exactly one place a run's loot is resolved. Secured loot banks on
            // the same checkpoint everything else rides.
            //
            // XP AND MASTERY ARE UNTOUCHED BY EITHER OUTCOME, deliberately: the run economy's rule
            // is that death costs the tradeable stake and never the progression, so a bad
            // extraction is a bad payday rather than a wasted hour.
            int lost = _loot.CarriedCount;
            _loot.ForfeitCarried();
            int banked = _loot.Bank(_profile);
            if (banked > 0 || lost > 0)
                Debug.Log($"[Rift] banked {banked} piece(s), lost {lost} carried");

            // THE STAKE SETTLES ON THE SAME CHECKPOINT, keyed on `survived` - which is to say on
            // EXTRACTION: a run only ends survived when the player took it out. Whatever system
            // ends a run extracted has to keep passing that, and the stake follows on its own.
            var (stakeOutcome, stakeItem, payout) =
                GearStake.Resolve(_profile, extracted: survived, gateCleared: _stakeGateCleared,
                                  new System.Random());
            if (payout != null) Art.Gear.GearCatalog.Register(payout.ToGearItem());
            _stakeLine = stakeOutcome switch
            {
                GearStake.Outcome.Paid     => $"stake        {stakeItem.DisplayName} kept, matching piece claimed",
                GearStake.Outcome.Returned => $"stake        {stakeItem.DisplayName} came home (gate not cleared)",
                GearStake.Outcome.Lost     => $"stake        {stakeItem.DisplayName} DESTROYED",
                _ => null,
            };
            if (_stakeLine != null) Debug.Log($"[Stake] {stakeOutcome}: {stakeItem.InstanceId}" +
                                              (payout != null ? $" -> {payout.InstanceId}" : ""));

            // CHECKPOINT 2 of 2.
            await _store.CommitRunAsync(_profile, summary);

            // The taper counter is ACCOUNT state, not character state, so it rides the account
            // write rather than the profile checkpoint. Saved on every run end including a death -
            // a lost run still spent the taper for the floors it cleared, because it also banked
            // the XP for them.
            if (_account != null) await _accounts.SaveAsync(_account);

            ShowRunOver(summary);
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
                    AskAbandonRun();
                });
        }

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
                SettingsScreen.Open(_canvas.transform, OpenControlsScreen);
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

        void ShowRunOver(RunSummary s)
        {
            TeardownRun();

            _overScreen = new GameObject("RunOver", typeof(RectTransform));
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
