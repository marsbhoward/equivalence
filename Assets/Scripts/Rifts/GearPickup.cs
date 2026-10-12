using UnityEngine;
using Convergence.Core;
using Convergence.Chain;

namespace Convergence.Rifts
{
    /// <summary>
    /// A gear piece lying where an enemy dropped it (Tuning.GearRoll.EnemyGearDropChance), waiting
    /// to be picked up - so loot is FOUND, not credited (the user's call, 2026-10-11).
    ///
    /// Taken the Rift Box's way, with the interact button while standing on it, never walked over:
    /// stopping for it mid-fight is part of finding it. Unlike the Rift Box it NEVER TIMES OUT -
    /// a box is a fragment of a tear and tears close; a piece of gear just lies there. It stays
    /// until taken or until the player leaves the floor, which leaves it behind
    /// (GameBootstrap.LeaveGroundGear); the floor clear says how many are still lying about.
    ///
    /// The piece is rolled when it DROPS (its tier and stars belong to the floor it fell on) and
    /// only joins the carried loot when taken.
    ///
    /// Drawn as a turning four-point glint in the tier's colour over a soft glow, one small star
    /// circling it per star level - a glint, not a box, so it never reads as a Rift Box or a loot
    /// box, and the stars carry the level without leaning on colour alone. Stat-rolled gear has no
    /// picture of its own to show.
    /// </summary>
    public class GearPickup : MonoBehaviour
    {
        public const float Reach = 1.1f;

        Transform _player;
        System.Action<MintedGearRecord> _onTaken;
        MintedGearRecord _record;
        SpriteRenderer _glow, _glint;
        SpriteRenderer[] _pips;
        Color _tint;
        float _t;
        bool _taken;

        public MintedGearRecord Record => _record;

        public bool InReach => _player != null && !_taken
            && Vector2.Distance(_player.position, transform.position) <= Reach;

        public static GearPickup Drop(Vector2 at, MintedGearRecord record, Transform parent,
                                      Transform player, System.Action<MintedGearRecord> onTaken)
        {
            var go = new GameObject("gear-drop");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = Arena.NearestFloor(at, 0.6f);

            var p = go.AddComponent<GearPickup>();
            p._player = player;
            p._record = record;
            p._onTaken = onTaken;
            p._tint = UI.CharacterScreen.TierColor(record.Tier);
            p._t = Random.value * 10f;   // a crowd of drops shouldn't pulse in unison

            p._glow = Quad(go.transform, "glow", 1.2f, Spr.Glow,
                           new Color(p._tint.r, p._tint.g, p._tint.b, 0.35f), SortingOrders.Fx - 42);
            p._glint = Quad(go.transform, "glint", 0.42f, Spr.Star(), p._tint, SortingOrders.Fx - 41);

            int stars = Mathf.Clamp(record.UpgradeLevel, 0, 3);
            p._pips = new SpriteRenderer[stars];
            for (int i = 0; i < stars; i++)
                p._pips[i] = Quad(go.transform, "pip", 0.13f, Spr.Star(),
                                  new Color(1f, 0.95f, 0.75f), SortingOrders.Fx - 40);

            var sorted = new SpriteRenderer[2 + stars];
            sorted[0] = p._glow; sorted[1] = p._glint;
            for (int i = 0; i < stars; i++) sorted[2 + i] = p._pips[i];
            DepthSorted.Attach(go, 0f, isFixed: false, sorted);
            return p;
        }

        void Update()
        {
            if (_taken) return;
            _t += Time.deltaTime;

            bool near = InReach;
            float bob = 0.12f + Mathf.Sin(_t * 2.4f) * 0.05f;
            _glint.transform.localPosition = new Vector3(0f, bob, 0f);
            _glint.transform.localRotation = Quaternion.Euler(0f, 0f, _t * 40f);
            float s = (near ? 0.5f : 0.42f) * (1f + 0.08f * Mathf.Sin(_t * 5f));
            _glint.transform.localScale = new Vector3(s, s, 1f);
            _glow.color = new Color(_tint.r, _tint.g, _tint.b,
                                    (near ? 0.55f : 0.30f) + 0.08f * Mathf.Sin(_t * 3f));

            for (int i = 0; i < _pips.Length; i++)
            {
                float a = _t * 1.6f + i * Mathf.PI * 2f / _pips.Length;
                _pips[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 0.32f,
                                                               bob + Mathf.Sin(a) * 0.14f, 0f);
            }

            if (!near || !Controls.InteractTapped) return;

            _taken = true;
            Spr.Flash(transform.position, 1.2f, _tint, 0.35f);
            _onTaken?.Invoke(_record);
            Destroy(gameObject);
        }

        static SpriteRenderer Quad(Transform parent, string name, float size, Sprite sprite,
                                   Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
