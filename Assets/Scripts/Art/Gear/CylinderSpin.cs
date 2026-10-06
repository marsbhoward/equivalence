using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The Sniper's idle flourish after a finisher: if the player does NOT attack again, the
    /// character holds the gun out at arm's length (<see cref="ICharacterRig.SetPresent"/>), the
    /// cylinder turns round through all six chambers, and the arm comes back down. An IDLE, not a
    /// reaction to the next attack - it used to play on the attack after the finisher, where it
    /// was buried inside a swing. Any attack cancels it on the spot and it does not come back
    /// until the next finisher.
    ///
    /// Waits for the finisher's own lock to end (<see cref="Convergence.Player.PlayerController.AttackLocked"/>)
    /// and then <see cref="IdleDelaySeconds"/> more, so it only starts once the player has
    /// visibly chosen not to swing.
    ///
    /// Plays <see cref="GearItem.SpinFrames"/> through the rig's weapon-sprite swap, then puts
    /// back whatever sprite was showing when it started - read then, not latched at attach, so a
    /// repaint in between is never undone.
    /// </summary>
    public class CylinderSpin : MonoBehaviour
    {
        /// <summary>After the finisher's lock ends, how long without an attack before it starts.</summary>
        const float IdleDelaySeconds = 0.35f;
        const float RaiseSeconds = 0.18f;
        /// <summary>Held out, still, before the cylinder turns - the beat that says "look".</summary>
        const float SettleSeconds = 0.12f;
        /// <summary>Per spin frame. Slower than a trigger pull's turn would be: this is shown off.</summary>
        const float FrameSeconds = 0.04f;
        /// <summary>Chambers turned - six is the whole cylinder round once.</summary>
        const int Chambers = 6;
        /// <summary>Held out, still, after the cylinder stops.</summary>
        const float HoldSeconds = 0.3f;
        const float LowerSeconds = 0.22f;

        [SerializeField] MonoBehaviour _rigBehaviour;
        [SerializeField] Convergence.Player.PlayerController _player;
        ICharacterRig _rig;
        ICharacterRig Rig => _rig ??= _rigBehaviour as ICharacterRig;

        Sprite[] _frames;
        Sprite _rest;
        bool _armed;       // a finisher went off and no attack has followed it yet
        float _idle;       // seconds since the finisher's lock ended
        float _t = -1f;    // time into the flourish, or -1 when not playing

        public static CylinderSpin Attach(Transform weaponAnchor, ICharacterRig rig, Sprite[] frames,
                                          Convergence.Player.PlayerController player)
        {
            var spin = weaponAnchor.GetComponentInChildren<CylinderSpin>(true);
            if (spin == null)
            {
                var go = new GameObject("sniper.cylinder");
                go.transform.SetParent(weaponAnchor, false);
                spin = go.AddComponent<CylinderSpin>();
            }
            spin.Stop();
            spin._rig = rig;
            spin._rigBehaviour = rig as MonoBehaviour;
            spin._player = player;
            spin._frames = frames;
            spin._armed = false;
            return spin;
        }

        public void SetShown(bool on)
        {
            if (!on) Stop();
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void OnDisable() => Stop();

        /// <summary>Called on every attack the player starts. A finisher arms the flourish; any
        /// attack cuts one that is playing.</summary>
        public void OnAttack(bool isFinisher)
        {
            if (!isActiveAndEnabled) return;
            Stop();
            _armed = isFinisher;
            _idle = 0f;
        }

        /// <summary>Hand the arm and the sprite back, at once - an attack is taking over.</summary>
        void Stop()
        {
            if (_t >= 0f && Rig != null)
            {
                Rig.SetPresent(0f);
                if (_t >= RaiseSeconds + SettleSeconds) Rig.SetWeaponSprite(_rest);
            }
            _t = -1f;
        }

        float SpinSeconds => _frames == null ? 0f : Chambers * (_frames.Length + 1) * FrameSeconds;

        void Update()
        {
            if (Rig == null || _frames is not { Length: > 0 }) return;

            if (_t < 0f)
            {
                if (!_armed) return;
                if (_player != null && _player.AttackLocked) { _idle = 0f; return; }
                _idle += Time.deltaTime;
                if (_idle < IdleDelaySeconds) return;
                _armed = false;
                _t = 0f;
            }
            else _t += Time.deltaTime;

            float spinFrom = RaiseSeconds + SettleSeconds;
            float spinTo = spinFrom + SpinSeconds;
            float lowerFrom = spinTo + HoldSeconds;

            // The arm: eased up, held, eased down.
            float w = _t < RaiseSeconds ? Mathf.SmoothStep(0f, 1f, _t / RaiseSeconds)
                    : _t < lowerFrom ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (_t - lowerFrom) / LowerSeconds);
            Rig.SetPresent(w);

            // The cylinder: each chamber is the frames and then the rest sprite, which IS the
            // cylinder one chamber on (the frames stop short of 60 degrees at both ends).
            if (_t >= spinFrom && _t < spinTo)
            {
                if (Rig.WeaponRenderer != null && _t - Time.deltaTime < spinFrom)
                    _rest = Rig.WeaponRenderer.sprite;
                int i = Mathf.FloorToInt((_t - spinFrom) / FrameSeconds) % (_frames.Length + 1);
                Rig.SetWeaponSprite(i < _frames.Length ? _frames[i] : _rest);
            }
            else if (_t >= spinTo && _t - Time.deltaTime < spinTo)
                Rig.SetWeaponSprite(_rest);

            if (_t >= lowerFrom + LowerSeconds)
            {
                Rig.SetPresent(0f);
                _t = -1f;
            }
        }
    }
}
