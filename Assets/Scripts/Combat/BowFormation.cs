using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The Four Birds' FORMATION (the user's calls, 2026-10-17 - the Phoenix's art is "a formation of
    /// all the bows", a flanking volley): while the Phoenix is drawn, the Crow, the Swan and the
    /// Peacock appear as bows of light in a V either side of the player - the Crow and the Swan out
    /// to the sides, the Peacock behind and above, the Phoenix at the point - each with its own
    /// arrow nocked, drawn back with the player's draw. <see cref="Release"/> hands
    /// PlayerController.FormationStrike where each arrow leaves from; the bows snap and fade.
    ///
    /// Drawing only. The bows face the aim (the locked target, else the facing) every frame - a
    /// bow is drawn with its bird diving at the target, as the player's is - mirrored rather than
    /// turned upside down when the aim is to the left. Scaled time.
    /// </summary>
    public class BowFormation : MonoBehaviour
    {
        public enum Bird { Crow, Swan, Peacock }

        public struct Shot
        {
            public Bird Bird;
            public Vector2 From;
            public Sprite Arrow;
        }

        class Slot
        {
            public Bird Bird;
            public SpriteRenderer Bow, Arrow;
            public Vector2 At;    // (forward, across) in the aim's frame
            public float Lift;
        }

        Player.PlayerController _pc;
        Slot[] _slots;
        float _draw, _clock, _releasedAt = -1f;
        Vector2 _aim = Vector2.right;

        public static BowFormation Gather(Player.PlayerController pc, float drawSeconds)
        {
            var go = new GameObject("bow.formation");
            var f = go.AddComponent<BowFormation>();
            f._pc = pc;
            f._draw = Mathf.Max(0.05f, drawSeconds);
            f._slots = new[]
            {
                f.Make(Bird.Crow, Art.Gear.DemoGear.CrowBowId, Tuning.Formation.CrowAt, Tuning.Formation.BowLift),
                f.Make(Bird.Swan, Art.Gear.DemoGear.SwanBowId, Tuning.Formation.SwanAt, Tuning.Formation.BowLift),
                f.Make(Bird.Peacock, Art.Gear.DemoGear.PeacockBowId, Tuning.Formation.PeacockAt,
                       Tuning.Formation.BowLift + Tuning.Formation.PeacockLift),
            };
            f._aim = f.Aim();
            f.Place();
            return f;
        }

        Slot Make(Bird bird, string id, Vector2 at, float lift)
        {
            var item = Art.Gear.GearCatalog.Get(id);
            var slot = new Slot { Bird = bird, At = at, Lift = lift };
            var bow = new GameObject("bow." + bird);
            bow.transform.SetParent(transform, false);
            slot.Bow = bow.AddComponent<SpriteRenderer>();
            slot.Bow.sprite = item != null && item.Layers.Length > 0 ? item.Layers[0].Sprite : null;
            slot.Bow.sortingOrder = SortingOrders.Fx - 3;
            var arrow = new GameObject("arrow." + bird);
            arrow.transform.SetParent(transform, false);
            slot.Arrow = arrow.AddComponent<SpriteRenderer>();
            slot.Arrow.sprite = item != null ? item.ArrowSprite : null;
            slot.Arrow.sortingOrder = SortingOrders.Fx - 2;
            float s = Tuning.Player.ArenaVisualScale;
            bow.transform.localScale = arrow.transform.localScale = new Vector3(s, s, 1f);
            return slot;
        }

        Vector2 Aim()
        {
            if (_pc == null) return _aim;
            var t = _pc.Targeting != null ? _pc.Targeting.Target : null;
            if (t != null && !t.IsDead)
            {
                var d = (Vector2)t.transform.position - (Vector2)_pc.transform.position;
                if (d.sqrMagnitude > 1e-4f) return d.normalized;
            }
            return _pc.Facing.sqrMagnitude > 1e-6f ? _pc.Facing.normalized : _aim;
        }

        /// <summary>Where a slot's bow stands now.</summary>
        Vector2 BowAt(Slot s)
        {
            var across = new Vector2(-_aim.y, _aim.x);
            return (Vector2)_pc.transform.position + _aim * s.At.x + across * s.At.y + Vector2.up * s.Lift;
        }

        /// <summary>How far each arrow is drawn back, 0..1.</summary>
        float Drawn => _releasedAt >= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_clock / _draw));

        void LateUpdate()
        {
            if (_pc == null) { Destroy(gameObject); return; }
            _clock += Time.deltaTime;
            if (_releasedAt < 0f) _aim = Aim();
            Place();
            if (_releasedAt >= 0f && _clock - _releasedAt >= Tuning.Formation.FadeOut) Destroy(gameObject);
        }

        void Place()
        {
            if (_pc == null || _slots == null) return;
            float angle = Mathf.Atan2(_aim.y, _aim.x) * Mathf.Rad2Deg;
            bool left = _aim.x < 0f;
            float s = Tuning.Player.ArenaVisualScale;

            float alpha = _releasedAt >= 0f
                ? Tuning.Formation.Alpha * (1f - Mathf.Clamp01((_clock - _releasedAt) / Tuning.Formation.FadeOut))
                : Tuning.Formation.Alpha * Mathf.Clamp01(_clock / Tuning.Formation.FadeIn);
            // The loose: a snap forward and a swell as the string lets go.
            float snap = _releasedAt >= 0f ? Mathf.Sin(Mathf.Clamp01((_clock - _releasedAt) / 0.15f) * Mathf.PI) : 0f;

            foreach (var slot in _slots)
            {
                if (slot.Bow == null) continue;
                var at = BowAt(slot) + _aim * (0.08f * snap);
                // The bird dives at the target: its +x (the beak) along the aim, mirrored rather
                // than turned upside down when the aim is to the left.
                var bt = slot.Bow.transform;
                bt.position = new Vector3(at.x, at.y, 0f);
                bt.rotation = Quaternion.Euler(0f, 0f, left ? angle - 180f : angle);
                float swell = 1f + 0.08f * snap;
                bt.localScale = new Vector3((left ? -s : s) * swell, s * swell, 1f);
                slot.Bow.color = new Color(1f, 1f, 1f, alpha);

                if (slot.Arrow == null || slot.Arrow.sprite == null) continue;
                slot.Arrow.enabled = _releasedAt < 0f;
                // Nocked at the string (at the bow's pivot - the birds are held by the back, the
                // string just behind), drawn back with the player's draw.
                float len = slot.Arrow.sprite.bounds.size.x * s;
                var nock = at - _aim * (Tuning.Formation.Pull * Drawn + 0.03f);
                var mid = nock + _aim * (len * 0.5f);
                var at2 = slot.Arrow.transform;
                at2.position = new Vector3(mid.x, mid.y, 0f);
                at2.rotation = Quaternion.Euler(0f, 0f, angle);
                slot.Arrow.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        /// <summary>Loose: where each bird's arrow leaves from (its nocked arrow's middle) and with
        /// which sprite. The bows snap and fade out.</summary>
        public Shot[] Release()
        {
            _aim = Aim();
            Place();
            var shots = new Shot[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                var from = slot.Arrow != null ? (Vector2)slot.Arrow.transform.position : BowAt(slot);
                shots[i] = new Shot { Bird = slot.Bird, From = from, Arrow = slot.Arrow != null ? slot.Arrow.sprite : null };
            }
            _releasedAt = _clock;
            return shots;
        }

        /// <summary>Gone without loosing (the draw was cut off).</summary>
        public void Cancel()
        {
            if (_releasedAt < 0f) _releasedAt = _clock;
        }
    }
}
