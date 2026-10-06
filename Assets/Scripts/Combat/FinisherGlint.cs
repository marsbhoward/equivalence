using UnityEngine;
using Convergence.Art;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.Combat
{
    /// <summary>
    /// A light running up the weapon while a finisher is BANKED - the job the reach ring's gold,
    /// faster pulse used to do, moved onto the thing that will deliver the finisher. The first
    /// glint runs the moment the finisher banks (the change worth noticing), then one every
    /// <see cref="Tuning.Glint.Period"/> while it waits. An armed finisher never expires, so a
    /// constant shine would become wallpaper; a passing glint keeps saying it.
    ///
    /// An OVERLAY parented to the weapon layer's own transform (so it swings, mirrors and lunges
    /// with the blade for free) drawing <see cref="PixelSprite.Glint"/> frames of whatever sprite
    /// that layer currently shows. It never touches the layer's sprite - flash, stone and idle
    /// flipbooks all swap that.
    ///
    /// Sorted at the weapon's OWN order, nudged toward the camera: the rig's orders are a dense
    /// permutation, so weapon + 1 is some other layer (a gripping fist, in the two-handed stack)
    /// and a tie at the same order is settled by distance. Read every frame, like WeaponTrail -
    /// the rig re-sorts as it moves.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FinisherGlint : MonoBehaviour
    {
        [SerializeField] PlayerController _player;
        [SerializeField] SpriteRenderer _sr;
        float _since = -1f;

        public static FinisherGlint Attach(PlayerController player)
        {
            var g = player.gameObject.AddComponent<FinisherGlint>();
            g._player = player;
            return g;
        }

        void LateUpdate()
        {
            var weapon = _player != null ? _player.Rig?.WeaponRenderer : null;
            bool banked = weapon != null && weapon.enabled && weapon.sprite != null
                       && _player.FinisherNext && !_player.AttackLocked
                       && _player.Blade == null && !_player.Airborne && !_player.Statue;

            if (!banked)
            {
                _since = -1f;
                if (_sr != null) _sr.enabled = false;
                return;
            }

            if (_since < 0f) _since = Time.time;

            if (_sr == null)
            {
                var go = new GameObject("weapon.glint");
                _sr = go.AddComponent<SpriteRenderer>();
            }
            if (_sr.transform.parent != weapon.transform)
            {
                _sr.transform.SetParent(weapon.transform, false);
                _sr.transform.localRotation = Quaternion.identity;
                _sr.transform.localScale = Vector3.one;
            }
            _sr.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            float phase = (Time.time - _since) % Tuning.Glint.Period;
            if (phase >= Tuning.Glint.Sweep) { _sr.enabled = false; return; }

            int frame = Mathf.Min(Tuning.Glint.Frames - 1,
                                  Mathf.FloorToInt(phase / Tuning.Glint.Sweep * Tuning.Glint.Frames));
            var sprite = PixelSprite.Glint(weapon.sprite, frame, Tuning.Glint.Frames, Tuning.Glint.From);
            if (sprite == null) { _sr.enabled = false; return; }

            _sr.sprite = sprite;
            _sr.sharedMaterial = weapon.sharedMaterial;
            _sr.flipX = weapon.flipX;
            _sr.flipY = weapon.flipY;
            _sr.color = Tuning.Glint.Tint;
            _sr.sortingLayerID = weapon.sortingLayerID;
            _sr.sortingOrder = weapon.sortingOrder;
            _sr.enabled = true;
        }

        void OnDestroy()
        {
            if (_sr != null) Destroy(_sr.gameObject);
        }
    }
}
