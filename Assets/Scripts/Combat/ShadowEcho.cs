using UnityEngine;
using Convergence.Core;
using Convergence.Art.Gear;

namespace Convergence.Combat
{
    /// <summary>
    /// One shadow duplicate of the player: a whole second rig, painted from the same loadout and
    /// tinted down to a translucent silhouette, that plays one swing and dissolves.
    ///
    /// A REAL RIG, not a captured pose or a generic ghost sprite, and that is the expensive
    /// decision here. The reason is the one the paper-doll exists for at all: the player's
    /// silhouette is their gear, and a duplicate that did not carry the pauldrons, the cloak and
    /// the sword would read as a spawned monster rather than as a copy of you. It is also what
    /// lets an echo play any motion in the game for free, which the finisher needs - it borrows
    /// whatever finishers are in the wheel and has no idea in advance what they animate as.
    ///
    /// The cost is paid once. <see cref="EchoChorus"/> pools these and never builds more than the
    /// ceiling (four for the finisher's ring, one for the after-image), so a run constructs at
    /// most five and then reuses them for the rest of it.
    ///
    /// The TINT MULTIPLIES, which is the only reason a flat colour works on art that carries its
    /// own RGB (see PixelSprite): everything it touches can only get darker, so a bright gilded
    /// pauldron and a dark leather grip both land in the same near-black without either one being
    /// repainted. It is also why there is no "brighten" option here - whitening pixel art needs a
    /// texture swap, which is what ICharacterRig.SetFlash exists for.
    /// </summary>
    public class ShadowEcho : MonoBehaviour
    {
        /// <summary>
        /// The rig, and the Object reference it can be re-derived from.
        ///
        /// An interface-typed field on a MonoBehaviour is NOT restored across a domain reload -
        /// the GameObjects survive and keep drawing, so the only symptom is that the echo stops
        /// animating with nothing on screen saying why. See the note in CLAUDE.md.
        /// </summary>
        [SerializeField] MonoBehaviour _rigBehaviour;
        ICharacterRig _rig;
        ICharacterRig Rig => _rig ??= _rigBehaviour as ICharacterRig;

        SpriteRenderer[] _renderers;
        float _life, _lifeMax;

        /// <summary>The multiply this figure is drawn in - Shadow's near-black unless a caller
        /// asks for another (Separatio's three principles).</summary>
        Color _tint = Tuning.Shadow.Tint;

        /// <summary>
        /// Build one, painted and tinted, and leave it switched off.
        ///
        /// <paramref name="paint"/> rather than a profile so this file stays out of the save
        /// layer: the caller already knows how to dress a rig, and passing a lambda means the
        /// echo needs no opinion about loadouts, transmog or the helmet preference.
        /// </summary>
        public static ShadowEcho Build(Transform parent, ElementType element,
                                       System.Action<ICharacterRig> paint)
        {
            var go = new GameObject("echo");
            go.transform.SetParent(parent, false);

            var rig = CharacterRigFactory.Build(go, element, SortingOrders.Character);
            // A copy of the player, so the same size as the player - see Tuning.Player.ArenaVisualScale.
            rig.SetVisualScale(Tuning.Player.ArenaVisualScale);
            paint?.Invoke(rig);

            // Sorted by where it STANDS, exactly like the player and the enemies - it is a body
            // on the floor and has to be walked in front of and behind like one. No Bias: the
            // player carries one, so a copy standing on the same line loses to the original,
            // which is the tie-break you want when four of them are around you.
            DepthSorted.Attach(go, rig);

            var echo = go.AddComponent<ShadowEcho>();
            echo._rig = rig;
            echo._rigBehaviour = rig as MonoBehaviour;

            // The contact shadow is a SIBLING of the rig root (it has to be - as a child it
            // orbited the character during a spin), so it is a child of this object and would
            // otherwise leave a full-strength black patch on the ground under a figure that is
            // itself barely there.
            var patch = go.transform.Find("shadow");
            if (patch != null) patch.gameObject.SetActive(false);

            echo.Tint(0f);
            go.SetActive(false);
            return echo;
        }

        /// <summary>
        /// Place the figure, point it, and start one swing.
        ///
        /// The facing is set BEFORE PlayAttack because the rig freezes its mirror for the
        /// duration of a swing - told the other way round, an echo placed to the west of the
        /// player would spend its whole animation facing east.
        /// </summary>
        public void Play(Vector3 position, Vector2 facing, AttackMotion motion,
                         float window, bool alt, float fadeSeconds)
            => Play(position, facing, motion, window, alt, fadeSeconds, Tuning.Shadow.Tint);

        /// <summary>The same, drawn in <paramref name="tint"/> rather than Shadow's black.</summary>
        public void Play(Vector3 position, Vector2 facing, AttackMotion motion,
                         float window, bool alt, float fadeSeconds, Color tint)
        {
            _tint = tint;
            gameObject.SetActive(true);
            transform.position = position;

            Rig?.SetFacing(facing);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(motion, window, alt);

            _lifeMax = _life = Mathf.Max(0.05f, window + fadeSeconds);
            Tint(1f);   // or the first frame draws at whatever the last fade left behind
        }

        /// <summary>
        /// Put a different sword in this figure's hands for its swing, or null for whatever it
        /// was dressed in. Separatio's figures each hold one part of the fused blade. Called
        /// AFTER Play, and undone by the next Repaint (Apply resets the weapon layer).
        /// </summary>
        public void HoldWeapon(Sprite sprite) => Rig?.SetWeaponSprite(sprite);

        public void Dismiss()
        {
            _life = 0f;
            gameObject.SetActive(false);
        }

        public bool Busy => _life > 0f;

        /// <summary>
        /// Re-dress the figure from the player's current loadout.
        ///
        /// The tint has to be re-applied afterwards: Apply repaints every layer to its own
        /// colour, which is exactly what the tint was overwriting. Without this an echo re-dressed
        /// mid-run would come back as a second, fully lit copy of the player.
        /// </summary>
        public void Repaint(System.Action<ICharacterRig> paint)
        {
            if (Rig == null) return;
            paint?.Invoke(Rig);
            Tint(Busy ? 1f : 0f);
        }

        void Update()
        {
            if (_life <= 0f) return;

            _life -= Time.deltaTime;
            if (_life <= 0f) { gameObject.SetActive(false); return; }

            // Squared, so the figure holds most of its presence through the swing and then goes
            // quickly. A linear fade spends half the window at half opacity, which reads as the
            // echo being weak rather than as it leaving.
            float t = Mathf.Clamp01(_life / _lifeMax);
            Tint(t * t);
        }

        void Tint(float alpha)
        {
            _renderers ??= GetComponentsInChildren<SpriteRenderer>(true);

            var c = _tint;
            c.a *= alpha;

            foreach (var sr in _renderers)
            {
                if (sr == null) continue;
                sr.color = c;
            }
        }
    }
}
