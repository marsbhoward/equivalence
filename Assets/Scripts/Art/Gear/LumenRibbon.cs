using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Lumen's tail - a LIVE WIRE running out of the pommel's pin header - trailing the same way
    /// the cape and the belt vials already do (see PrimitiveCharacterRig.AnimateCape /
    /// AnimateVials): the identical spring, owned by a standalone component because exactly one
    /// weapon in the game wants it, so it does not belong on the shared rig.
    ///
    /// HUNG FROM THE POMMEL'S END, not the anchor. The anchor is the fist, and a tail hung there
    /// lay straight down over the grip and read as part of it. The end is worked out from
    /// whatever sprite the weapon renderer is showing (its pivot is the grip, its bottom edge the
    /// pommel), so it lands right on the arena sprite and on a display's alike.
    ///
    /// It hangs with GRAVITY - world down, plus the spring's swing - rather than along the blade:
    /// a wire off a sword carried point-down across the back would otherwise stand straight up.
    ///
    /// PIVOTED AT ITS OWN TOP (see LumenRibbonPivot in DemoGear), so a rotation in place orbits
    /// the strip round the point it actually hangs from.
    ///
    /// The sparks flicker between frames at RANDOM intervals - on a beat they read as an
    /// animation; at random they read as a live current.
    /// </summary>
    public class LumenRibbon : MonoBehaviour
    {
        const float Swing = 22f;
        const float Stiffness = 140f;
        const float Damping = 10f;
        const float ReferenceSpeed = 6.5f;
        const float SparkMin = 0.05f, SparkMax = 0.16f;

        SpriteRenderer _sr;
        SpriteRenderer _weapon;
        Rigidbody2D _body;
        // NOT readonly and null-guarded: a domain reload can hand this back empty.
        Sprite[] _frames;
        float _angle, _angularVelocity, _nextSpark;

        public static LumenRibbon Attach(Transform parent, Sprite[] frames, SpriteRenderer weapon)
        {
            var ribbon = parent.GetComponentInChildren<LumenRibbon>(true);
            if (ribbon == null)
            {
                var go = new GameObject("lumen.ribbon");
                go.transform.SetParent(parent, false);
                ribbon = go.AddComponent<LumenRibbon>();
                ribbon._sr = go.AddComponent<SpriteRenderer>();
            }

            // Re-taken every call, never latched behind the null-check above - the body and the
            // weapon renderer this tracks can both be replaced by a repaint, the exact trap
            // PrismGlow's and SaintHalo's own notes both document for the identical reason.
            ribbon._frames = frames;
            if (frames is { Length: > 0 }) ribbon._sr.sprite = frames[0];
            ribbon._weapon = weapon;
            ribbon._body = parent.GetComponentInParent<Rigidbody2D>();
            return ribbon;
        }

        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (_sr == null) return;
            _frames ??= DemoGear.LumenWireFrames;

            // No Rigidbody2D - the hub, the character sheet, the weapon rack - and the wire just
            // settles to hanging straight down, the right pose for a display.
            float forward = _body != null ? _body.linearVelocity.x : 0f;

            // The rig mirrors the whole character by negating its ROOT's x scale; reading the
            // lossy scale answers "am I mirrored" without the rig exposing anything new.
            if (transform.lossyScale.x < 0f) forward = -forward;

            float target = -Mathf.Clamp(forward / ReferenceSpeed, -1f, 1f) * Swing;
            _angularVelocity += (target - _angle) * Stiffness * Time.deltaTime;
            _angularVelocity *= Mathf.Exp(-Damping * Time.deltaTime);
            _angle += _angularVelocity * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, _angle);

            if (_weapon != null)
            {
                transform.position = PommelEnd(_weapon);

                // +6: the fist's layers (ArmFront/GlovesFront/Ring, the forearm and lower glove)
                // sit at Weapon+1..+5 in the two-handed grip; below them the wire vanished.
                _sr.sortingOrder = _weapon.sortingOrder + 6;
                _sr.sortingLayerID = _weapon.sortingLayerID;
            }

            if (_frames is { Length: > 1 } && Time.time >= _nextSpark)
            {
                _sr.sprite = _frames[Random.Range(0, _frames.Length)];
                _nextSpark = Time.time + Random.Range(SparkMin, SparkMax);
            }
        }

        /// <summary>The bottom-centre of the weapon's sprite, in world space - the pommel's end,
        /// since every weapon sprite here is authored point up.</summary>
        static Vector3 PommelEnd(SpriteRenderer weapon)
        {
            var sprite = weapon.sprite;
            if (sprite == null) return weapon.transform.position;
            float ppu = sprite.pixelsPerUnit;
            var rect = sprite.rect;
            float x = (rect.width * 0.5f - sprite.pivot.x) / ppu;
            float y = -sprite.pivot.y / ppu;
            if (weapon.flipX) x = -x;
            if (weapon.flipY) y = (rect.height - sprite.pivot.y) / ppu;
            return weapon.transform.TransformPoint(new Vector3(x, y, 0f));
        }
    }
}
