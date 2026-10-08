using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Convergence.Chain;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The rig for authored art: a PSD-imported, bone-rigged character driven by Unity's
    /// 2D Animation package. Gear swapping goes through SpriteResolvers rather than through
    /// SpriteRenderers we create ourselves.
    ///
    /// HOW THE ART HAS TO BE SET UP (see ART.md for the full walkthrough):
    ///  - One PSD per character, one layer per body part, imported with the PSD Importer.
    ///  - Rig it in the Skinning Editor; the importer emits a prefab with a SpriteLibrary
    ///    and one SpriteResolver per part.
    ///  - Each gear-bearing part needs a SpriteResolver whose CATEGORY is the slot name
    ///    ("Helmet", "Chest", "MainHand", ...) and which has a label named "none" for the
    ///    unequipped state.
    ///  - Each GearItem then names the category + label it resolves to.
    ///
    /// Anything missing is skipped with a warning rather than throwing, so a half-rigged
    /// character still runs.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpriteLibraryCharacterRig : MonoBehaviour, ICharacterRig
    {
        public bool DetailArt => false;

        /// <summary>See ICharacterRig.SetVisualScale. A plain float - survives a domain reload.</summary>
        [SerializeField] float _visualScale = 1f;

        public void SetVisualScale(float scale)
        {
            _visualScale = scale > 0f ? scale : 1f;
            var s = transform.localScale;
            transform.localScale = new Vector3(Mathf.Sign(s.x == 0f ? 1f : s.x) * _visualScale, _visualScale, 1f);
        }

        public const string EmptyLabel = "none";
        const string MainHandCategory = "MainHand";

        /// <summary>What the main hand holds, so a thrown weapon can be put back.</summary>
        string _mainHandLabel = EmptyLabel;

        [Tooltip("Animator trigger fired on attack, if the controller has one.")]
        public string AttackTrigger = "Attack";
        [Tooltip("Animator float driven with movement speed, if the controller has one.")]
        public string SpeedParam = "Speed";
        [Tooltip("Animator int set to the AttackMotion of each swing, if the controller has one.")]
        public string MotionParam = "Motion";
        public string ChargeTrigger = "Charge";
        [Tooltip("Animator float scaling the attack clip to the real swing interval.")]
        public string AttackSpeedParam = "AttackSpeed";

        /// <summary>The length an authored attack clip is assumed to be authored at.</summary>
        const float BaseSwingSeconds = 0.24f;

        SpriteLibrary _library;
        Animator _animator;
        Rigidbody2D _body;
        Player.PlayerController _player;
        readonly Dictionary<string, SpriteResolver> _resolvers = new();
        bool _hasSpeed, _hasAttack, _hasMotion, _hasAttackSpeed, _hasCharge;

        public Transform Transform => transform;

        public void Initialise(int sortingOrder)
        {
            _library = GetComponentInChildren<SpriteLibrary>();
            _animator = GetComponentInChildren<Animator>();
            _body = GetComponentInParent<Rigidbody2D>();
            _player = GetComponentInParent<Player.PlayerController>();

            // Index resolvers by category so a loadout can find the right part instantly.
            foreach (var resolver in GetComponentsInChildren<SpriteResolver>(true))
            {
                var category = resolver.GetCategory();
                if (!string.IsNullOrEmpty(category)) _resolvers[category] = resolver;
            }

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder += sortingOrder;

            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.name == SpeedParam) _hasSpeed = true;
                    if (p.name == AttackTrigger) _hasAttack = true;
                    if (p.name == MotionParam) _hasMotion = true;
                    if (p.name == ChargeTrigger) _hasCharge = true;
                    if (p.name == AttackSpeedParam) _hasAttackSpeed = true;
                }
            }

            if (_library == null)
                Debug.LogWarning($"[Rig] '{name}' has no SpriteLibrary - gear will not swap.");
        }

        public void Apply(Loadout loadout)
        {
            // Clear every gear category first so unequipping works.
            foreach (var slot in System.Enum.GetValues(typeof(GearSlot)))
                Resolve(slot.ToString(), EmptyLabel, warn: false);
            _mainHandLabel = EmptyLabel;

            if (loadout == null) return;

            foreach (var entry in loadout.Equipped)
            {
                var item = GearCatalog.Get(entry.ItemId);
                if (item == null) continue;

                // A gear item may inject sprites the base library never had.
                if (item.GearLibrary != null && _library != null)
                    _library.AddOverride(item.GearLibrary, item.Category, item.Label);

                if (item.Category == MainHandCategory) _mainHandLabel = item.Label;
                Resolve(item.Category, item.Label, warn: true);
            }
        }

        void Resolve(string category, string label, bool warn)
        {
            if (string.IsNullOrEmpty(category)) return;

            if (!_resolvers.TryGetValue(category, out var resolver))
            {
                if (warn)
                    Debug.LogWarning($"[Rig] no SpriteResolver for category '{category}' on '{name}'.");
                return;
            }

            if (!resolver.SetCategoryAndLabel(category, label) && warn)
                Debug.LogWarning($"[Rig] '{category}' has no label '{label}'.");
        }

        /// <summary>
        /// Swaps the main-hand category to the empty label, which is how this rig expresses
        /// "nothing equipped" everywhere else.
        /// </summary>
        /// <summary>Null - this rig is an authored hierarchy and does not keep a transform per
        /// layer. Callers do without; see ICharacterRig.WeaponAnchor.</summary>
        public Transform WeaponAnchor => null;

        public SpriteRenderer WeaponRenderer => null;
        public SpriteRenderer TrinketRenderer => null;
        public SpriteRenderer OffhandRenderer => null;

        public bool TryGetDiscVisual(int index, out Sprite sprite, out Color tint, out Vector2 size)
            => TryGetWeaponVisual(out sprite, out tint, out size);

        public bool TryGetWeaponVisual(out Sprite sprite, out Color tint, out Vector2 size)
        {
            sprite = null; tint = Color.white; size = Vector2.one;
            if (!_resolvers.TryGetValue(MainHandCategory, out var resolver) || resolver == null)
                return false;
            var sr = resolver.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return false;

            sprite = sr.sprite;
            tint = sr.color;
            size = sr.bounds.size;
            return true;
        }

        /// <summary>Authored art flashes via its own material; nothing to do here yet.</summary>
        public void SetFlash(float t) { }

        /// <summary>Authored art would turn to stone through its own material; nothing yet.</summary>
        public void SetStone(bool on) { }

        /// <summary>Authored art resolves this through its own category system; nothing to do here yet.</summary>
        public void SetHelmHidden(bool hidden) { }

        /// <summary>No standing-preference vs. transient distinction exists for this rig yet -
        /// same resolve as SetWeaponVisible.</summary>
        public void SetWeaponHidden(bool hidden)
            => Resolve(MainHandCategory, hidden ? EmptyLabel : _mainHandLabel, warn: false);

        /// <summary>The preview idle is an authored clip's job; nothing to do here yet.</summary>
        /// <summary>
        /// No-op: the menu art set exists for the procedural rig's hand-drawn grids. An authored
        /// rig resolves its sprites through a SpriteLibrary, where the same distinction would be a
        /// second category rather than a second array.
        /// </summary>
        public void SetDetailArt(bool on) { }

        /// <summary>
        /// No-op: an authored rig resolves its weapon through a SpriteLibrary, where a heat stage
        /// would be a label on the category rather than a sprite handed in from outside.
        /// </summary>
        public void SetWeaponSprite(UnityEngine.Sprite sprite) { }

        public void SetShowcasePose(bool on) { }

        /// <summary>No-op: an authored rig faces where its animator says it does.</summary>
        public void SetFacing(Vector2 aim) { }
        public void SetAimRange(float degrees) { }

        /// <summary>No-op: no back-facing art authored for this rig yet.</summary>
        public void SetFacingAway(bool away) { }
        public void SyncAttunement(Core.ElementType element) { }

        /// <summary>
        /// No-op: this rig draws authored sprites and has no joints to pose, so it has no way to
        /// open a grip. See ICharacterRig.SetGripShouldered.
        /// </summary>
        public void SetGripShouldered(bool shouldered) { }
        public void SetHandTarget(Vector2? rootLocal, float offHand = 1f) { }
        public bool TryGetSwordHand(out Vector2 rootLocal) { rootLocal = default; return false; }
        public void SetPresent(float weight) { }

        /// <summary>An authored swing clip carries its own hop; nothing to do here yet.</summary>
        public void SetSwingHop(float height) { }

        public void SetWeaponVisible(bool visible)
            => Resolve(MainHandCategory, visible ? _mainHandLabel : EmptyLabel, warn: false);

        /// <summary>
        /// No-op: a Sprite Library rig draws whatever the authored categories contain, and there
        /// is no off-hand category to resolve against. Authored art would express a dual grip as
        /// its own main-hand label rather than as two renderers.
        /// </summary>
        public void SetWeaponSplit(bool split) { }
        public void SetOffhandPicture(Sprite arena, Sprite menu, Sprite[] mainFrames, Sprite[] offFrames) { }
        public void SetContactShadow(bool on) { }

        /// <summary>No hip vial art exists in the authored library yet.</summary>
        public void SetVialCount(int count) { }

        /// <summary>
        /// The factory plants this rig at the actor's local origin, so the lift is simply the
        /// root's local Y - the same contract the placeholder rig uses. Nothing else writes this
        /// transform's position, so there is no rest value to restore.
        /// </summary>
        public void SetAirborne(float height)
            => transform.localPosition = new Vector3(0f, Mathf.Max(0f, height) * _visualScale, 0f);

        /// <summary>Not supported on the authored rig - it has no notion of a drawing-only
        /// offset separate from its own transform, and SetAirborne here already writes that same
        /// transform directly. A no-op rather than fighting that write.</summary>
        public void SetPhantomOffset(Vector2 offset) { }

        public void PlayCharge(float seconds)
        {
            if (_animator == null || !_hasCharge) return;
            if (_hasAttackSpeed && seconds > 0.01f)
                _animator.SetFloat(AttackSpeedParam, BaseSwingSeconds / seconds);
            _animator.SetTrigger(ChargeTrigger);
        }

        /// <summary>An authored controller alternates inside its own clips, so `reversed`
        /// is not forwarded - there is no parameter on the animator to receive it.</summary>
        public void PlayAttack(Combat.AttackMotion motion, float duration, bool alt)
        {
            if (_animator == null || !_hasAttack) return;
            // Authored controllers select the clip from Motion and time it with AttackSpeed.
            // Both are optional, so a controller carrying only the trigger still works.
            if (_hasMotion) _animator.SetInteger(MotionParam, (int)motion);
            if (_hasAttackSpeed && duration > 0.01f)
                _animator.SetFloat(AttackSpeedParam, BaseSwingSeconds / duration);
            _animator.SetTrigger(AttackTrigger);
        }

        /// <summary>An authored controller has no way to be held on a clip's first frame, so the
        /// timing bar's wind-up shows the controller's charge state instead, when it has one.</summary>
        public void HoldSwingStart(Combat.AttackMotion motion, bool alt, float seconds)
            => PlayCharge(seconds);

        void Update()
        {
            if (_animator != null && _hasSpeed && _body != null)
                _animator.SetFloat(SpeedParam, _body.linearVelocity.magnitude);

            float x = _player != null ? _player.Facing.x
                    : _body != null ? _body.linearVelocity.x
                    : 0f;
            if (Mathf.Abs(x) < 0.05f) return;
            transform.localScale = new Vector3((x < 0f ? -1f : 1f) * _visualScale, _visualScale, 1f);
        }
    
        /// <summary>
        /// No-op. Authored art carries its own face and hair, so there is nothing procedural to
        /// recolour - a PSD-imported character would express this as sprite-library swaps
        /// instead, which is work for whenever real art actually arrives.
        /// </summary>
        public void SetAppearance(Chain.Appearance look) { }

        /// <summary>Rebase every renderer this rig owns, keeping their relative stacking.</summary>
        public void SetSortingBase(int order)
        {
            int lowest = int.MaxValue;
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers) if (sr.sortingOrder < lowest) lowest = sr.sortingOrder;
            if (lowest == int.MaxValue) return;
            foreach (var sr in renderers) sr.sortingOrder = order + (sr.sortingOrder - lowest);
        }
}
}
