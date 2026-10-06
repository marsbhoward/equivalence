using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// The circle a leaping finisher is about to land in - the one piece of the old reach rings
    /// that was kept. The rest (reach, the banked finisher, the disc's throw band, the bow's
    /// sweet spot) moved onto the locked target (TargetHighlight), the weapon (FinisherGlint) and
    /// the swing itself (SwingSmear); this is not a reach readout but a WARNING, drawn while the
    /// character is off the screen and free to run, and the only thing saying where the damage
    /// will fall.
    ///
    /// It snaps to <see cref="PlayerController.ImpactRadius"/> rather than easing: a warning that
    /// slides into place is wrong about where the damage is for as long as it slides. It pulses
    /// sharply - dim most of the cycle, a brief flare - which reads as a beat rather than a throb.
    ///
    /// Lives on the PLAYER with the circle as a child, so it must never write `transform` (that
    /// is the player's own); the circle clears its own rotation.
    /// </summary>
    public class LandingZone : MonoBehaviour
    {
        public Color ImpactColor = new(1f, 0.34f, 0.26f, 0.55f);

        [Tooltip("Flares per second.")]
        public float PulseSpeed = 3.2f;

        [Tooltip("1 is a smooth sine; higher holds the ring dim and spikes briefly.")]
        public float PulseSharpness = 2.6f;

        [Tooltip("Alpha at the dim end, as a fraction of the colour's own alpha.")]
        [Range(0f, 1f)] public float PulseFloor = 0.35f;

        [Tooltip("Alpha at the flare - independent of the colour's alpha, or a quiet base could " +
                 "never flare above itself.")]
        [Range(0f, 1f)] public float PulsePeak = 0.95f;

        [SerializeField] PlayerController _player;
        [SerializeField] SpriteRenderer _ring;

        public static LandingZone Attach(PlayerController player)
        {
            var z = player.gameObject.AddComponent<LandingZone>();
            z._player = player;

            var go = new GameObject("landing.zone");
            go.transform.SetParent(player.transform, false);
            z._ring = go.AddComponent<SpriteRenderer>();
            z._ring.sprite = Spr.ThinRing;
            z._ring.sortingOrder = SortingOrders.GroundDecal + 1;
            z._ring.enabled = false;
            return z;
        }

        void LateUpdate()
        {
            if (_player == null || _ring == null) return;

            float radius = _player.ImpactRadius;
            _ring.enabled = radius > 0f;
            if (!_ring.enabled) return;

            _ring.transform.localScale = Vector3.one * radius * 2f;
            _ring.transform.localRotation = Quaternion.identity;

            float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed * Mathf.PI * 2f),
                                    PulseSharpness);
            var c = Color.Lerp(ImpactColor, Color.white, pulse * 0.5f);
            c.a = Mathf.Lerp(ImpactColor.a * PulseFloor, PulsePeak, pulse);
            _ring.color = c;
        }
    }
}
