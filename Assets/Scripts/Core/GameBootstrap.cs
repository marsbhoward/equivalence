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
        public int BaseEnemiesPerFloor = 3;
        public int EliteEveryNFloors = 3;
        [Tooltip("First floor a ranged kiter can appear. Lets the player meet a plain chaser wave first.")]
        public int RangedFromFloor = 2;
        [Tooltip("First floor a stationary beam turret can appear.")]
        public int TurretFromFloor = 4;
        [Tooltip("First floor a hit-and-run dasher can appear.")]
        public int DasherFromFloor = 3;
        [Tooltip("First floor a stationary hazard gargoyle can appear.")]
        public int GargoyleFromFloor = 5;
        [Tooltip("First floor a shielding bubbles statue can appear.")]
        public int BubblesFromFloor = 6;

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
        public UI.PauseScreen PauseScreen { get; private set; }
        public UI.InventoryScreen InventoryScreen { get; private set; }
        public UI.ExchangeScreen ExchangeScreen { get; private set; }
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
        /// The boss holding the current floor, or null. A plain component reference, so it
        /// survives a domain reload the way an interface field would not.
        ///
        /// It is deliberately NOT in _alive: that list is EnemyController and the floor-clear
        /// check counts it, and a boss is neither an enemy archetype nor something a wave
        /// spawner should be able to add to. The floor asks about both separately.
        /// </summary>
        Bosses.BossController _boss;

        /// <summary>What this run is carrying, and what it has already got out. Rebuilt per run;
        /// nothing in it reaches the profile until a Rift or floor 100 banks it.</summary>
        readonly Rifts.RunLoot _loot = new();

        /// <summary>The Rift standing open on this floor, or null. Like the boss it is not in
        /// _alive and holds the floor on its own.</summary>
        Rifts.Rift _rift;
        bool _spawning;
        bool _rewardTaken;
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
            PauseScreen = gameObject.AddComponent<UI.PauseScreen>();
            InventoryScreen = gameObject.AddComponent<UI.InventoryScreen>();
            ExchangeScreen = gameObject.AddComponent<UI.ExchangeScreen>();
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
                    || (_player.Resource != null && _player.Resource.CanRelease
                        && (_player.Effects == null || _player.Effects.CanRelease(_player.Resource.Fill01))));

            // Same wiring, for the chest's defensive ability (GUARD).
            Controls.GuardReady = () => _player != null && _player.DefenseReady;

            // Same wiring again, for an element's mastery-gated second ability. Reads false for
            // every element that has not built one, which is what hides the button entirely
            // rather than showing it permanently dim.
            Controls.SecondAbilityReady = () =>
                _player != null && _player.Resource != null && _player.Resource.CanReleaseSecond;
            Controls.SecondAbilityUnlocked = () =>
                _player != null && _player.Resource != null && _player.Resource.SecondAbilityUnlocked;

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
            Mastery = new Progression.BoardState(_profile.Mastery);

            // Same reason as the loadout prune above, one system over: mastery ids became
            // element-scoped when the four-board model replaced the shared grid, so a profile
            // saved before that carries entries no node answers to. Every read already ignores
            // them - which is exactly why they went unnoticed - but the list only grows.
            int staleNodes = Mastery.DropStale();
            if (staleNodes > 0)
                Debug.Log($"[ProfileStore] dropped {staleNodes} mastery entries from the old board.");
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
                if (shown != null && shown.HasElementGems && _player.Resource != null)
                {
                    var element = _player.Resource.Element;
                    // SetWeaponSprite FIRST, WeaponAnchor read AFTER - SetWeaponSprite calls
                    // EnsureLayers internally and WeaponAnchor's own getter does not, so reading
                    // the anchor first can catch _pivots empty following a domain reload. Same
                    // ordering BuildPlayer's own wiring already relies on.
                    _player.Rig?.SetWeaponSprite(shown.BladeFor(element));
                    var prismAnchor = _player.Rig?.WeaponAnchor;
                    if (prismAnchor != null)
                        Art.Gear.PrismGlow.Attach(prismAnchor, shown.GemAlong(element),
                                                  ElementInfo.Tint(element), _player.Rig.WeaponRenderer)
                                          .SetShown(true);
                }
                // Swapped AWAY from a gemmed weapon - the glow is parented to the shared weapon
                // anchor, not to the item, so it survives the swap unless told to hide.
                else _player.Rig?.WeaponAnchor?.GetComponentInChildren<Art.Gear.PrismGlow>(true)
                                              ?.SetShown(false);

                // Phantom's poof re-synced the same way - a plain bool read fresh from whatever
                // is drawn now, no glow object to hide, just the flag flipping either way.
                _player.PhantomFlicker = shown != null && shown.HasPhantomFlicker;
                _player.SheatheDrawn   = shown != null && shown.HasSheathAnimation;

                // Phantom's haze animation, same re-sync. RepaintLive (above) already reset the
                // weapon layer to the new item's own base sprite, so hiding the ticker here is
                // enough - nothing needs to restore a sprite the repaint didn't already fix.
                if (shown != null && shown.PhantomHazeFrames != null && shown.PhantomHazeFrames.Length > 0)
                {
                    _player.Rig?.SetWeaponSprite(shown.PhantomHazeFrames[0]);
                    var hazeAnchor = _player.Rig?.WeaponAnchor;
                    if (hazeAnchor != null)
                        Art.Gear.PhantomHaze.Attach(hazeAnchor, _player.Rig, shown.PhantomHazeFrames)
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
                Art.Gear.GearCatalog.Register(record.ToGearItem());
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
            var (tier, grants, ability, finisher) = Art.Gear.GearRoller.RollItem(slot, instanceId.GetHashCode());

            var record = new MintedGearRecord
            {
                InstanceId = instanceId,
                DisplayName = $"{tier} {slot}",
                Slot = slot,
                Tier = tier,
                Grants = grants,
                DefensiveAbility = ability,
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
                fsr.sprite = authored ? art.FloorSprite : Spr.Square;
                fsr.color = authored ? Color.white : new Color(0.10f, 0.11f, 0.14f);
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
                    fsr.drawMode = SpriteDrawMode.Simple;
                    floor.transform.localScale = new Vector3(fw, fh, 1f);
                }
            }

            // Grid lines, purely so movement reads. Authored floor art replaces them.
            //
            // This used to `return` here, which took the WALLS and Arena.HalfExtents with it -
            // turning on HideGrid, or dropping in a floor sprite, silently produced an arena with
            // no collision and stale published bounds.
            if (!art.HideGrid && art.FloorSprite == null)
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
            sr.color = wallArt != null ? Color.white : new Color(0.24f, 0.26f, 0.32f);
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
                       StartRun,
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
            ForgeScreen.Open(_canvas.transform, _profile.PendingGearVouchers, RedeemGearVoucher);
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
            GearDisplayScreen.Open(
                _canvas.transform,
                weapons ? UI.GearDisplayScreen.Kind.Weapons : UI.GearDisplayScreen.Kind.Armour,
                id => _hub.SpotShowing(id, weapons),
                _hub.ArmouryCapacity(weapons),
                _hub.ArmouryInUse(weapons),
                (item, spot) => _hub.ToggleDisplayed(item, spot, weapons));
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

            _cam.transform.rotation = Quaternion.Euler(Tuning.Hub.CameraTiltDegrees, 0f, 0f);
            _cam.transform.position = new Vector3(
                0f,
                lookAtY + Mathf.Sin(theta) * distance,
                -Mathf.Cos(theta) * distance);
        }

        void Update()
        {
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
                ShowcasePicker == null || ExchangeScreen == null || TxScreen == null ||
                MasteryScreen == null || FloorRewardScreen == null || ForgeScreen == null ||
                ManualScreen == null || GearDisplayScreen == null || RiftScreen == null ||
                PauseScreen == null || InventoryScreen == null) return;

            // ONE owner for which buttons are on screen. The room could set this itself while
            // carrying a trophy, and did at first - but then two Updates were writing the same
            // field in an undefined order and the button set flickered between them every frame.
            // Ask the room what it is doing instead.
            bool modal = MasteryScreen.IsOpen || ConfirmDialog.IsOpen || FloorRewardScreen.IsOpen
                      || TransmutationScreen.IsOpen || ShowcasePicker.IsOpen || ExchangeScreen.IsOpen
                      || TxScreen.IsOpen || CharacterScreen.IsOpen || ForgeScreen.IsOpen
                      || ManualScreen.IsOpen || GearDisplayScreen.IsOpen || RiftScreen.IsOpen
                      || PauseScreen.IsOpen || InventoryScreen.IsOpen
                      || (_hub != null && _hub.Blocking);

            // A screen that must be ANSWERED gets no back button. Everything else does, and it
            // means exactly what Escape means there.
            bool dismissible = !(FloorRewardScreen.IsOpen && !FloorRewardScreen.CanDismiss)
                            && !(ExchangeScreen.IsOpen && !ExchangeScreen.CanDismiss);

            Controls.Screen =
                  modal ? (dismissible ? Controls.Context.Modal : Controls.Context.Hidden)
                : _state == State.Playing ? Controls.Context.Arena
                : _state == State.Hub ? (_hub != null && _hub.Placing
                                             ? Controls.Context.Placing
                                             : Controls.Context.Hub)
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

            // Everything below drives the game itself, and a modal is by definition covering it.
            // Without this, a click meant for the mastery grid fell through to the element cards
            // underneath and started a run, and Esc aimed at a confirm prompt re-opened it.
            if (MasteryScreen.IsOpen || ConfirmDialog.IsOpen || FloorRewardScreen.IsOpen ||
                TransmutationScreen.IsOpen || ShowcasePicker.IsOpen || ExchangeScreen.IsOpen ||
                TxScreen.IsOpen || ForgeScreen.IsOpen || ManualScreen.IsOpen ||
                GearDisplayScreen.IsOpen || RiftScreen.IsOpen ||
                PauseScreen.IsOpen || InventoryScreen.IsOpen) return;

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
            // here and silently strand the player in a frozen game.
            if (Time.timeScale == 0f && !GamePause.IsPaused)
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
                bool settled = !dummyHoldsFloor && !_spawning && !FloorRewardScreen.IsOpen
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
                    _hud?.SetBoss(_boss.Health.Current / Mathf.Max(1f, _boss.Health.Max),
                                  _boss.Phase.ToString().ToUpper(), _boss.Enraged, _boss.Health.Immune);
                else _hud?.SetBoss(-1f, null, false, false);

                // A BOSS HOLDS ITS FLOOR ON ITS OWN. It is not in _alive (see the field), so
                // without this the floor would read as cleared the instant the last minion died
                // and the reward screen would open over a fight still in progress.
                // A RIFT HOLDS ITS FLOOR the same way a boss does, and for the same reason: it is
                // not in _alive, so without this the floor would advance out from under the choice.
                if (_rift != null)
                {
                    if (_rift.PlayerInside && !RiftScreen.IsOpen) OpenRift();
                }
                //
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
                else if (!dummyHoldsFloor && _boss == null && _activeDoor == null &&
                    !_spawning && _alive.Count == 0 && !FloorRewardScreen.IsOpen && !MasteryScreen.IsOpen &&
                    !ConfirmDialog.IsOpen && !ExchangeScreen.IsOpen)
                {
                    if (_floor > 0 && !_rewardTaken) OfferFloorReward();
                    else if (_floor > 0 && IsRiftFloor(_floor)) OpenRiftOnFloor();
                    else StartCoroutine(NextFloor());
                }

                if (_player && _cam) TrackCamera();
            }
            else if (_state == State.RunOver)
            {
                if (Controls.AnyDismiss) ReturnToHub();
            }
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

            // Framerate-independent, and tight: a soft follow reads as the camera lagging behind
            // the player rather than carrying them.
            _cam.transform.position = Vector3.Lerp(
                _cam.transform.position, want, 1f - Mathf.Exp(-CameraFollow * Time.deltaTime));
        }

        // ------------------------------------------------------------------ run lifecycle

        async void StartRun(ElementType element)
        {
            // async void swallows exceptions into the sync context, where Unity reports them with
            // useless line numbers from the state machine. Catch here so failures are legible.
            try { await StartRunAsync(element); }
            catch (Exception e) { Debug.LogError($"[Convergence] StartRun failed: {e}"); }
        }

        async System.Threading.Tasks.Task StartRunAsync(ElementType element)
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
            if (TxScreen.IsOpen) TxScreen.Close();
            if (ConfirmDialog.IsOpen) ConfirmDialog.Close();
            if (PauseScreen.IsOpen) PauseScreen.Close();
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
            _alive.Clear();
            _loot.Clear();

            // The safe reserve comes into the run with the player. Spending is tracked on the run
            // and written back at the checkpoint, so a run abandoned mid-flight cannot half-spend
            // the profile's count.
            _loot.SetBankedBoxes(_profile.RiftBoxes);
            Modifiers.Clear();

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

            // The player is a humanoid paper-doll, not a flat sprite: gear has to be visible and
            // swappable per the economy design. An authored full-character prefab still wins.
            var rig = Art.Gear.CharacterRigFactory.Build(go, element, 10);


            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.42f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 6f;
            rb.freezeRotation = true;

            float baseHp = element == ElementType.Air ? Tuning.Player.HpAir
                         : element == ElementType.Earth ? Tuning.Player.HpEarth
                         : Tuning.Player.HpFireWater;
            var hp = go.AddComponent<Health>();

            // The stat block is built HERE, before Health, because max health needs it and the
            // controller does not exist yet. Gear and the grid, summed into percentage points over
            // the Tuning.Player baselines; mastery is authored in fractions (0.05 = +5%) so it is
            // converted at this single boundary rather than by re-authoring every node.
            var stats = _profile.Gear.TotalStats();
            stats.Damage      += Mastery.Get(element, Progression.MasteryStat.AllDamage) * 100f;
            stats.MoveSpeed   += Mastery.Get(element, Progression.MasteryStat.MoveSpeed) * 100f;
            stats.AttackSpeed += Mastery.Get(element, Progression.MasteryStat.AttackSpeed) * 100f;
            // The grid's node is still named Armor (only the int VALUE is serialised, so the C#
            // name is free to disagree) but it has always been a flat damage reduction - gear's
            // Resilience stat is the one that does that job, not gear's own pool-sized Armor.
            stats.Resilience  += Mastery.Get(element, Progression.MasteryStat.Armor) * 100f;

            // Max health is read once, here. Thickened Hide and Thin Blood taken later in the run
            // re-apply through OnLedgerChanged rather than being polled - a max that moved every
            // frame would fight Health's own clamping of Current against it.
            //
            // The PERCENTAGE scales the element's own baseline; mastery and the ledger add FLAT
            // health on top. Mastery's MaxHp nodes are authored as flat HP (40f, not 0.4) and are
            // left that way deliberately - as a percentage the same node would be worth +47% to
            // Air and +28% to Earth, which quietly rewrites the durability spread that is those
            // elements' whole identity.
            hp.Configure(Mathf.Max(1f, Art.Gear.StatPercents.Apply(baseHp, stats.MaxHp)
                                       + Mastery.Get(element, Progression.MasteryStat.MaxHp)
                                       + Modifiers.Current.BonusMaxHp));

            // Added before the controller so its Awake finds it.
            var targeting = go.AddComponent<PlayerTargeting>();
            targeting.ModsSource = () => Modifiers.Current;
            var pc = go.AddComponent<PlayerController>();

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

            // The shadow duplicates. Built whenever the run has the finisher OR the passive at
            // all - the finisher's ring is where its damage lives and must always be visible -
            // but the TRAILING after-image is gated on the blade being the one on screen, which
            // is the same drawn-versus-source split the heat repaint above lives by.
            //
            // Two of the three rows in that table apply here unchanged: wield Shadow and you get
            // both halves; skin Shadow over another sword and socket it and you still get both,
            // because you ARE holding a shadow blade. Socket it without the skin and every swing
            // still lands twice, with nothing following you - the cost of not wielding the thing,
            // and the same cost a socketed heat cycle pays by not repainting anything.
            if (source?.HasEchoChain == true || source?.Signature?.Finisher?.SummonsEchoes == true)
            {
                var chorus = go.AddComponent<Combat.EchoChorus>();
                chorus.Owner = pc;
                chorus.Element = element;
                chorus.Paint = r => Art.Gear.CharacterRigFactory.Paint(r, _profile);
                chorus.ShowsTrail = shown != null && shown.HasEchoChain;
                pc.Echoes = chorus;
            }

            // The arena sorts by depth now, same as the hub. The bias is the concession to
            // readability: honest sorting means a crowd closing from below covers you exactly
            // when you most need to see yourself, so the player sorts as if a little nearer and
            // wins close calls, while still going properly behind anything clearly in front.
            DepthSorted.Attach(go, rig, bias: 0.35f);

            // Reach ring on the ground, so "is that enemy actually in the arc" is answerable.
            Player.RangeRings.Attach(pc);

            pc.Stats = stats;

            // Worn weapons hit softer; the stat block hits harder.
            pc.DamageDealtMultiplier = () =>
                _profile.Wear.DamageDealtMultiplier(_profile.Gear)
                * Art.Gear.StatPercents.Apply(1f, pc.Stats.Damage);

            // Worn armour hits back harder; Resilience blunts every hit regardless of condition;
            // Barrier layers a flat reduction on top while it is active. Three independent
            // effects, composed here into the one number both Health and the HUD read - see
            // IncomingDamageMultiplier's own doc for why that matters.
            pc.IncomingDamageMultiplier = () =>
                _profile.Wear.DamageTakenMultiplier(_profile.Gear, pc.Stats.Armor)
                * Art.Gear.StatPercents.ReductionFactor(pc.Stats.Resilience)
                * (pc.BarrierActive ? Tuning.Defense.BarrierDamageMultiplier : 1f);
            pc.MoveSpeed = Art.Gear.StatPercents.Apply(pc.MoveSpeed, pc.Stats.MoveSpeed);

            // Lifesteal comes from the grid, and only from THIS element's nodes - a fire build's
            // investment does nothing on a water run. Read through a lambda rather than captured
            // once, so buying a node mid-session takes effect without restarting the run.
            var lifestealStat = Progression.MasteryStats.Lifesteal(element);
            pc.LifestealFraction = () => Mastery.Get(element, lifestealStat) + Modifiers.Current.BonusLifesteal;

            // The ledger, read live. Not captured: it changes between floors.
            pc.ModsSource = () => Modifiers.Current;

            // The conditional half. Bound through a narrow facade rather than handed the whole
            // controller, so it can read what it needs and cannot drive anything it should not.
            var effects = go.AddComponent<Exchange.RunEffects>();
            effects.Bind(Modifiers, new Exchange.RunEffects.PlayerController_Facade
            {
                Health = () => hp,
                MeterFill01 = () => pc.Resource != null ? pc.Resource.Fill01 : 0f,
                RefundFinisher = pc.RefundFinisher,
            });
            pc.Effects = effects;

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
                    Resilience = () => Mastery.Get(element, Progression.MasteryStat.Armor),
                    Position = () => pc.transform.position,
                });
            pc.Principles = principles;

            // CHAINED, NOT ASSIGNED. Health.ModifyIncoming is a single delegate rather than an
            // event, so the second system to claim it silently deletes the first - and the two
            // that want it here are the exchange ledger and Salt's Ward, neither of which is
            // optional. Composed explicitly so the order is visible: the ledger's own reductions
            // resolve first, then Ward and Guard reduce what is left.
            hp.ModifyIncoming = a => principles.ModifyIncoming(effects.ModifyIncoming(a));
            hp.ModifyHeal = effects.ModifyHeal;
            hp.SurviveLethal = effects.SurviveLethal;

            // Salt reads the hit AFTER it resolved, so the stack that fills the Ward is itself
            // reduced by the stacks already held - see PrincipleEffects.OnDamaged.
            hp.Damaged += info => principles.OnDamaged(info.Amount);

            // OnWeaponUsed is deliberately left unhooked: weapon degradation is off (see
            // Durability.DamageDealtMultiplier). Wearing the weapon while nothing reads the
            // condition would quietly rot saved gear for a mechanic that no longer exists.

            // Worn armour means taking more; being hit is what wears it. DamageResistance slows
            // the drain the same way the ledger's own ArmourWearMul already does - composed here
            // rather than threaded into Durability, since Durability stays ignorant of stats.
            hp.Damaged += _ =>
            {
                var m = Modifiers.Current;
                if (m.ArmourNeverWears) return;
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

            // Prism's four gems - purely cosmetic, unlike the heat cycle above: which element is
            // being played is fixed for the whole run, so this is a one-time swap rather than
            // something that needs a Changed subscription. Same DRAWN-not-SOURCE rule as the heat
            // repaint: `shown` is the resolved loadout weapon, so a Prism skin worn over another
            // sword lights up exactly as a wielded one does, and a plain sword drawn while a
            // Prism sits in the relic socket has no gems to paint - BladeFor returns null and
            // nothing changes, which is correct.
            if (shown != null && shown.HasElementGems)
            {
                rig?.SetWeaponSprite(shown.BladeFor(element));
                var prismAnchor = rig?.WeaponAnchor;
                if (prismAnchor != null)
                    Art.Gear.PrismGlow.Attach(prismAnchor, shown.GemAlong(element),
                                              ElementInfo.Tint(element), rig.WeaponRenderer)
                                      .SetShown(true);
            }

            // Phantom's poof - also DRAWN, not SOURCE, and also a one-time read: which weapon is
            // on screen does not change mid-run any more than which element is being played does,
            // so this needs no Changed-style subscription either.
            pc.PhantomFlicker = shown != null && shown.HasPhantomFlicker;
            pc.SheatheDrawn   = shown != null && shown.HasSheathAnimation;

            // Phantom's haze animation. SetWeaponSprite first (establishes frame 0 immediately,
            // and calls EnsureLayers internally), WeaponAnchor read after - the same ordering
            // Prism's own wiring above already learned the hard way.
            if (shown != null && shown.PhantomHazeFrames != null && shown.PhantomHazeFrames.Length > 0)
            {
                rig?.SetWeaponSprite(shown.PhantomHazeFrames[0]);
                var hazeAnchor = rig?.WeaponAnchor;
                if (hazeAnchor != null)
                    Art.Gear.PhantomHaze.Attach(hazeAnchor, rig, shown.PhantomHazeFrames).SetShown(true);
            }

            // None of these four mastery nodes were being read before this pass. Fire's own is
            // FireStackLife rather than a gain rate - see GainRateMultiplier's doc for why that is
            // the right lever for a discrete, one-stack-per-swing resource. Composed with gear's
            // ElementGrowth into one multiplier, same as every other stat's mastery+gear boundary.
            float gainRatePoints = (element switch
            {
                ElementType.Fire  => Mastery.Get(element, Progression.MasteryStat.FireStackLife),
                ElementType.Water => Mastery.Get(element, Progression.MasteryStat.WaterMeterGain),
                ElementType.Earth => Mastery.Get(element, Progression.MasteryStat.EarthChargeRate),
                _                 => Mastery.Get(element, Progression.MasteryStat.AirMomentumGain),
            }) * 100f + stats.ElementGrowth;
            resource.GainRateMultiplier = Art.Gear.StatPercents.Apply(1f, gainRatePoints);

            // Read once, same as everything else off Mastery here - see BoardState.HasSecondAbility.
            resource.SecondAbilityUnlocked = Mastery.HasSecondAbility(element);

            return pc;
        }

        /// <summary>
        /// Which archetypes may carry the elite tier. Turret is excluded deliberately: it cannot
        /// move, so displacement resistance grants it nothing, and doubling the HP of a thing that
        /// already holds an angle across the room makes it a chore rather than a threat. Booster
        /// is excluded for the same underlying reason from the opposite direction: its Elite is
        /// permanently None (see Tuning.Enemy's own Booster notes), so the tier would have nothing
        /// to grant it at all.
        ///
        /// Gargoyle joins despite its BASIC form being just as stationary as Turret's, because the
        /// elite tier is what makes it move at all (see ElitePattern.Descent) - the exact opposite
        /// of Turret's case, where the tier would grant nothing a Basic doesn't already have.
        ///
        /// Bubbles joins too: its elite grants a more resilient bubble (BubblesEliteCharges) - a
        /// real difference, just read straight off the Elite bool rather than declared as an
        /// ElitePattern (see Tuning.Enemy's own note on why).
        /// </summary>
        static readonly EnemyKind[] EliteCapableKinds =
            { EnemyKind.Chaser, EnemyKind.Bomb, EnemyKind.Ranged, EnemyKind.Dasher,
              EnemyKind.Gargoyle, EnemyKind.Bubbles };

        IEnumerator NextFloor()
        {
            // The floor just finished pays out - carried, not banked. Skipped for floor 0 -> 1,
            // which is entering the arena rather than clearing anything.
            if (_floor > 0)
            {
                var drop = Rifts.RunLoot.Roll(_floor);
                _loot.Add(drop);
                Debug.Log($"[Rift] floor {_floor} dropped {drop.DisplayName} " +
                          $"(carrying {_loot.CarriedCount}, secured {_loot.SecuredCount}, " +
                          $"boxes {_loot.TotalBoxes})");
            }

            _spawning = true;
            _floor++;
            _rewardTaken = false;
            _stuckWatch?.NotifyFloorChanged();

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


            int token = _runToken;
            yield return new WaitForSeconds(_floor == 1 ? 0.6f : 1.4f);
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

            // Room layout is rerolled fresh every floor, the same "a fresh room each floor" idea
            // the enemy wave already lives by - last floor's columns/fields/lava are cleared
            // first, since unlike enemies they never clear themselves out on their own.
            if (_hazardRoot != null)
            {
                foreach (Transform child in _hazardRoot) Destroy(child.gameObject);
                HazardBuilder.Populate(_floor, Arena.HalfExtents, _player.transform, _hazardRoot);
            }

            // A BOSS FLOOR IS THE BOSS AND NOTHING ELSE. Adding a wave underneath it would put a
            // pack of chasers between the player and a fight whose whole subject is where they are
            // standing - the hazard phase is unreadable with something else pushing you around,
            // and the stun window would be spent fighting minions instead of the boss.
            if (IsBossFloor(_floor))
            {
                SpawnBoss();
                _spawning = false;
                yield break;
            }

            // Capped - see Enemies.FloorDifficulty.Count. Uncapped this reached 53 bodies on
            // floor 100, which is where a forty-minute clear came from.
            int count = Enemies.FloorDifficulty.Count(_floor);
            Debug.Log($"[Floor] {Enemies.FloorDifficulty.Describe(_floor)}");
            bool eliteFloor = _floor % EliteEveryNFloors == 0;

            // Ranged, Turret and Dasher all eat into the chaser count rather than adding on top of
            // it, so a floor's total pressure doesn't spike the moment a new archetype is
            // introduced - each is a different fight, not a harder one.
            int rangedCount = _floor >= RangedFromFloor ? 1 + (_floor - RangedFromFloor) / 3 : 0;
            rangedCount = Mathf.Min(rangedCount, count - 1);
            int turretCount = _floor >= TurretFromFloor ? 1 + (_floor - TurretFromFloor) / 4 : 0;
            turretCount = Mathf.Min(turretCount, count - rangedCount - 1);
            int dasherCount = _floor >= DasherFromFloor ? 1 + (_floor - DasherFromFloor) / 3 : 0;
            dasherCount = Mathf.Min(dasherCount, count - rangedCount - turretCount - 1);
            int gargoyleCount = _floor >= GargoyleFromFloor ? 1 + (_floor - GargoyleFromFloor) / 5 : 0;
            gargoyleCount = Mathf.Min(gargoyleCount, count - rangedCount - turretCount - dasherCount - 1);

            // BOOSTER: gated on ACCOUNT progression, not floor depth - see Tuning.Enemy's own
            // notes. BestFloor is "how far has this character ever proven they can go", so a
            // brand-new account's early floors stay exactly as tuned; this curveball only shows up
            // for a player who has already been deeper finding a fresh run's early floors trivial.
            // At most one per floor, and not guaranteed even then - a floor-level chance roll, the
            // same "does this feature appear at all" shape Hazards.ColumnsFloorChance uses.
            bool boosterEligible = _profile != null
                                 && _profile.BestFloor >= Tuning.Enemy.BoosterUnlockBestFloor
                                 && _floor <= Tuning.Enemy.BoosterMaxFloor;
            int boosterCount = boosterEligible && UnityEngine.Random.value < Tuning.Enemy.BoosterFloorChance ? 1 : 0;
            boosterCount = Mathf.Min(boosterCount, count - rangedCount - turretCount - dasherCount - gargoyleCount - 1);

            // BUBBLES: ordinary floor-depth introduction, same shape as Gargoyle's own - nothing
            // about this one was asked to be reserved for veteran accounts.
            int bubblesCount = _floor >= BubblesFromFloor ? 1 + (_floor - BubblesFromFloor) / 5 : 0;
            bubblesCount = Mathf.Min(bubblesCount,
                count - rangedCount - turretCount - dasherCount - gargoyleCount - boosterCount - 1);

            int chaserCount = count - rangedCount - turretCount - dasherCount
                             - gargoyleCount - boosterCount - bubblesCount;

            for (int i = 0; i < chaserCount; i++)
            {
                var pos = RandomEdgePoint();
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Chaser, _floor, _player.transform, _enemyRoot,
                                           elite: UnityEngine.Random.value < Tuning.Enemy.EliteChance);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            for (int i = 0; i < rangedCount; i++)
            {
                var pos = RandomEdgePoint();
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Ranged, _floor, _player.transform, _enemyRoot,
                                           elite: UnityEngine.Random.value < Tuning.Enemy.EliteChance);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            // Turret spawns anywhere on the floor, not just the edge - it never has to close a
            // gap the way the other two do, so there is no reason to hold it at the boundary.
            for (int i = 0; i < turretCount; i++)
            {
                var pos = RandomFloorPoint(Tuning.Enemy.TurretMinPlayerDistance);
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Turret, _floor, _player.transform, _enemyRoot,
                                           elite: UnityEngine.Random.value < Tuning.Enemy.EliteChance);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            for (int i = 0; i < dasherCount; i++)
            {
                var pos = RandomEdgePoint();
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Dasher, _floor, _player.transform, _enemyRoot,
                                           elite: UnityEngine.Random.value < Tuning.Enemy.EliteChance);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            // Placed like a Turret - anywhere on the floor, never walked in from the edge, since
            // a stationary watcher never has to close a gap to matter.
            for (int i = 0; i < gargoyleCount; i++)
            {
                var pos = RandomFloorPoint(Tuning.Enemy.GargoyleMinPlayerDistance);
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Gargoyle, _floor, _player.transform, _enemyRoot,
                                           elite: UnityEngine.Random.value < Tuning.Enemy.EliteChance);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            // Placed like a Turret or a Gargoyle - never walked in from the edge. ALWAYS common:
            // this kind has no elite variant of its own, so the per-spawn elite roll every other
            // loop here makes is deliberately skipped rather than passed a chance that would only
            // ever come back false anyway (see EnemyTypes' Booster row, Elite = None).
            for (int i = 0; i < boosterCount; i++)
            {
                var pos = RandomFloorPoint(Tuning.Enemy.BoosterMinPlayerDistance);
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Booster, _floor, _player.transform, _enemyRoot,
                                           elite: false);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            // Placed like the rest of this family - never walked in from the edge. UNLIKE
            // Booster's own loop, this one DOES roll for elite - a resilient bubble is a real
            // difference worth meeting at random, not just on a guaranteed elite floor.
            for (int i = 0; i < bubblesCount; i++)
            {
                var pos = RandomFloorPoint(Tuning.Enemy.BubblesMinPlayerDistance);
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = EnemyFactory.Spawn(pos, EnemyKind.Bubbles, _floor, _player.transform, _enemyRoot,
                                           elite: UnityEngine.Random.value < Tuning.Enemy.EliteChance);
                HookDeath(e);
                _alive.Add(e);
                yield return new WaitForSeconds(0.12f);
            }

            // The guaranteed elite of an elite floor. Now a TIER on a randomly chosen archetype
            // rather than a fixed melee brawler - an elite Ranged or an elite Dasher is a
            // genuinely different fight, and neither was expressible while Elite occupied its own
            // EnemyKind slot.
            if (eliteFloor && _player != null && token == _runToken)
            {
                var eliteKind = EliteCapableKinds[UnityEngine.Random.Range(0, EliteCapableKinds.Length)];
                var e = EnemyFactory.Spawn(RandomEdgePoint(), eliteKind, _floor, _player.transform, _enemyRoot,
                                           elite: true);
                HookDeath(e);
                _alive.Add(e);
            }

            _spawning = false;
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

            // The exit door goes up HERE rather than after the deal/reward flow finishes - it
            // used to spawn only once ApplyFloorReward ran, so the player never saw it until both
            // screens had already come and gone. GamePause holds timeScale for the whole exchange
            // + reward flow (see ExchangeScreen/FloorRewardScreen's own Hold/Release), so spawning
            // it early costs nothing: nobody can walk anywhere until the screens release the
            // pause, but the door is standing there the moment the floor actually cleared instead
            // of popping in afterward.
            OpenFloorDoor();

            // Floor-clear costs settle FIRST, so the exchange row is read against the health you
            // actually have left rather than the health you had a moment before Toll took its cut.
            if (_player != null) _player.Effects?.OnFloorCleared();

            var offer = Exchange.ExchangeOffers.Build(Modifiers, _floor);
            if (offer.Pairs.Count == 0) { OfferFloorRewardCards(); return; }

            // Transmuter's Eye. The reward is rolled ONCE, here, and the same roll is both
            // previewed and later handed to the reward screen - a preview that re-rolled would
            // be a lie, and is the obvious way to get this wrong.
            _pendingReward = RollFloorReward();
            string preview = Modifiers.StacksOf("transmuters_eye") > 0
                ? $"next: {_pendingReward.RestoreText}" +
                  (_pendingReward.Offered != null ? $"  or  {_pendingReward.Offered.DisplayName}" : "")
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
                else Debug.Log("[Exchange] refused");

                OfferFloorRewardCards();
            });
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
            // flat from mastery and the ledger. If one moves, move both.
            float want = Mathf.Max(1f,
                Art.Gear.StatPercents.Apply(baseHp, _player.Stats?.MaxHp ?? 0f)
                + Mastery.Get(_player.Resource?.Element ?? _profile.LastElement,
                              Progression.MasteryStat.MaxHp)
                + Modifiers.Current.BonusMaxHp);
            _player.Health.SetMax(want);

            // The wheel is the other value that is read once rather than polled. Derived from the
            // ledger for the same reason max health is: Mods is recomputed from every entry held,
            // so a second Extra Sigil has to arrive as arithmetic, not as a one-shot that already
            // fired.
            _player.SyncSlotCount(Modifiers.Current.ExtraFinisherSlots);
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

        RolledReward RollFloorReward()
        {
            var restore = PickRestoreKind();
            float healAmount = _player != null ? _player.Health.Max * HealFraction : 0f;
            return new RolledReward
            {
                Offered = _player != null
                    ? Combat.MovesetLibrary.RandomExcluding(_player.EarnedMovesets, _player.Weapon)
                    : null,
                Restore = restore,
                HealAmount = healAmount,
                RestoreText = restore == UI.FloorReward.Heal
                    ? $"+{Mathf.RoundToInt(healAmount)} HP"
                    : $"+{Mathf.RoundToInt(RepairFraction * 100f)}% condition",
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

            _pendingMastery += Mathf.RoundToInt(MasteryXpPerFloor * Modifiers.Current.MasteryXpMul * taper);
            _pendingGearVouchers += Tuning.GearRoll.VouchersPerFloor;

            // Reuses the roll the exchange row may already have shown. Rolling again here would
            // make Transmuter's Eye show one thing and deliver another.
            var r = _pendingReward ?? RollFloorReward();
            _pendingReward = null;

            string xpText = $"+{XpPerPick} XP";
            string xpBlurb = "Account experience, kept whether you survive or not. " +
                             $"You are level {_profile.Level} with {_profile.Xp} XP.";

            FloorRewardScreen.Show(
                _canvas.transform, _floor, r.Offered,
                _player != null ? _player.Slots : System.Array.Empty<Combat.Moveset>(),
                _player != null ? _player.SlotLocked : _ => false,
                _player != null ? _player.RotationIndex : 0,
                r.Restore, r.RestoreText, xpText, xpBlurb,
                (outcome, slot) => ApplyFloorReward(outcome, slot, r.Offered, r.HealAmount));
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
                    int repaired = _profile.Wear.Repair(_profile.Gear, RepairFraction,
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
        /// two would drift the moment either one's offset was retuned.
        /// </summary>
        static Vector3 SouthSpawnPoint() => new(0f, -Arena.HalfExtents.y + 1.6f, 0f);

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

        void OpenFloorDoor()
        {
            if (_activeDoor != null) return;
            if (_player == null) { StartCoroutine(NextFloor()); return; }

            int token = _runToken;
            var pos = new Vector2(0f, Arena.HalfExtents.y - 1.6f);
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
        /// THE CANTOR STANDS IN FOR ALL OF THEM. It is the only boss that exists, so it currently
        /// answers for every mini-boss and for all four reincarnation avatars - deliberately, as a
        /// placeholder, so the run's SHAPE (a boss every ten floors, a Rift after it, avatars at
        /// the quarters) can be played and felt while the individual boss patterns are still being
        /// worked out.
        ///
        /// What that means for anyone reading this later: a floor-10 fight and a floor-75 fight
        /// are currently the same fight. Mini-bosses are meant to be RANDOMISED, each with its own
        /// tactics and phases, and the avatars are meant to be four distinct set-pieces. None of
        /// that exists. `BossController` is written as one boss rather than as a framework, so
        /// giving it siblings is a real piece of work and not a table of numbers.
        /// </summary>
        static bool IsBossFloor(int floor)
        {
            if (floor <= 0) return false;
            if (IsAvatarFloor(floor)) return true;
            return floor % Tuning.Boss.RiftInterval == 0;
        }

        /// <summary>The reincarnation avatars. Separate from IsBossFloor because the run economy
        /// keys the loot bands and the Black Diamond roll off these four specifically.</summary>
        static bool IsAvatarFloor(int floor)
            => floor == 25 || floor == 50 || floor == 75 || floor == 100;

        void SpawnBoss()
        {
            if (_player == null) return;

            // Spawned at the centre so the fight opens where it will later be vulnerable - the
            // middle of the room has to mean one thing all fight, and the player should have seen
            // it there once before being asked to run to it.
            _boss = Bosses.BossController.Spawn(Vector2.zero, _player.transform, _enemyRoot);

            var hp = _boss.Health;
            hp.Died += h =>
            {
                if (_player) _player.RegisterKill();
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

        /// <summary>
        /// Floors that end at a Rift: the ten-floor cadence, plus the floor before an avatar boss
        /// and the floor after one. See Tuning.Boss.RiftInterval.
        /// </summary>
        static bool IsRiftFloor(int floor)
        {
            if (floor <= 0) return false;
            if (floor % Tuning.Boss.RiftInterval == 0) return true;
            if (IsBossFloor(floor + 1)) return true;                 // the warning before an avatar

            // Floor 100 needs no Rift after it - clearing it banks everything and ends the run, so
            // the tear the final avatar leaves behind IS the way out.
            return IsBossFloor(floor - 1) && floor - 1 < 100;
        }

        /// <summary>How many pieces a Rift will take. The capacity domain is on the mastery board
        /// and is read live, so a point spent between runs is felt on the next one.</summary>
        int RiftCapacity()
            => Tuning.Boss.BaseRiftCapacity
             + Mathf.RoundToInt(Mastery.Get(_profile.LastElement, Progression.MasteryStat.RiftCapacity));

        void OpenRiftOnFloor()
        {
            if (_rift != null || _player == null) return;

            // Torn where the player is NOT, so walking to it is a real decision rather than
            // something that happens to them - the whole fixture is a question and they should
            // have to go and answer it.
            var at = Arena.Clamp((Vector2)_player.transform.position
                                 + UnityEngine.Random.insideUnitCircle.normalized * 4.5f, 2f);
            _rift = Rifts.Rift.Open(at, _arenaRoot, _player.transform);
            _hud?.SetRiftPrompt(true);
        }

        void OpenRift()
        {
            _hud?.SetRiftPrompt(false);
            RiftScreen.Open(_canvas.transform, _loot, RiftCapacity(), _floor,
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
                    // Whatever was not secured stays carried, and stays at risk.
                    CloseRift();
                    StartCoroutine(NextFloor());
                });
        }

        void CloseRift()
        {
            if (_rift != null) { Destroy(_rift.gameObject); _rift = null; }
            _hud?.SetRiftPrompt(false);
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
            return drawn.Get(Art.Gear.GearSlot.Weapon) == "rift_blade";
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

                TryDropRiftBox(h.transform.position);
                _alive.Remove(enemy);
                Destroy(h.gameObject);
            };
        }

        /// <summary>
        /// A Rift Box, from any enemy, at Tuning.Boss.RiftBoxDropChance.
        ///
        /// PER ENEMY RATHER THAN PER FLOOR, so the yield scales with how deep the run went on its
        /// own - enemy counts already grow with depth, so nothing has to say "deeper pays better"
        /// a second time. It also makes the box a thing that DROPS off a body in front of the
        /// player rather than a number that appears between floors, which is the whole reason it
        /// is worth being a physical pickup.
        ///
        /// Spawned into the enemy root, so a teardown that clears the wave clears any box lying
        /// on the floor with it - a consumable surviving into the next run would be loot the
        /// player never carried.
        /// </summary>
        void TryDropRiftBox(Vector2 at)
        {
            if (_player == null) return;
            if (UnityEngine.Random.value >= Tuning.Boss.RiftBoxDropChance) return;

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
            return new Vector2(Mathf.Cos(angle) * (HalfWidth - 1.4f),
                               Mathf.Sin(angle) * (HalfHeight - 1.4f));
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

            // A run can end with a screen still up - abandoning from the confirm dialog, or dying
            // while one is open. Those screens are about to be closed or destroyed and would never
            // release their hold, leaving the menu frozen.
            FloorRewardScreen?.Close();
            ExchangeScreen?.Close();
            CharacterScreen?.Close();
            InventoryScreen?.Close();
            PauseScreen?.Close();
            GamePause.ReleaseAll();

            foreach (var e in _alive) if (e) Destroy(e.gameObject);
            _alive.Clear();
            Enemies.EnemyRegistry.Prune();

            // The boss owns a coroutine and a hazard layer, neither of which is a child of
            // _enemyRoot's wave and neither of which stops on its own. Ceased explicitly, for the
            // documented reason: a coroutine outliving its run is one of this project's own
            // lifecycle bugs, and here it would leave slices erupting on the main menu.
            if (_boss != null) { _boss.Cease(); Destroy(_boss.gameObject); _boss = null; }
            CloseRift();
            DamageNumbers.Teardown();

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
                $"duration     {s.DurationSeconds:0}s\n\n" +
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
