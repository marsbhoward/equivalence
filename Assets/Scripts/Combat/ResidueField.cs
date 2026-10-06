using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Combat
{
    /// <summary>
    /// What a release leaves on the ground for a few seconds (the exchange's Residue), in the
    /// element's own terms: Fire's burns, Water's soaks, Earth's slows, Air's draws enemies in.
    ///
    /// Statuses go through the player's own helpers (PlayerController.Burn/Soak), so the board's
    /// status power and rules reach them like any other source - see the statuses note in
    /// CLAUDE.md. Plain value fields only, so a domain reload loses nothing.
    /// </summary>
    public class ResidueField : MonoBehaviour
    {
        const float TickSeconds = 0.5f;

        [SerializeField] float _radius, _life, _remaining, _tick;
        [SerializeField] ElementType _element;
        [SerializeField] Player.PlayerController _owner;
        SpriteRenderer _sr;

        public static ResidueField Spawn(Vector2 at, float radius, ElementType element, float seconds,
                                         Player.PlayerController owner)
        {
            var go = new GameObject("residue");
            go.transform.position = Arena.NearestFloor(at, 0.3f);
            var f = go.AddComponent<ResidueField>();
            f._radius = Mathf.Max(0.4f, radius);
            f._life = f._remaining = Mathf.Max(0.2f, seconds);
            f._element = element;
            f._owner = owner;
            f._sr = go.AddComponent<SpriteRenderer>();
            f._sr.sprite = Spr.Circle;
            f._sr.sortingOrder = SortingOrders.GroundDecal + 2;
            go.transform.localScale = Vector3.one * f._radius * 2f;
            f.Paint();
            return f;
        }

        void Paint()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;
            var c = ElementInfo.Tint(_element);
            c.a = 0.32f * Mathf.Clamp01(_remaining / Mathf.Max(0.01f, _life * 0.35f));
            _sr.color = c;
        }

        void Update()
        {
            _remaining -= Time.deltaTime;
            Paint();
            if (_remaining <= 0f) { Destroy(gameObject); return; }

            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            _tick = TickSeconds;

            Vector2 centre = transform.position;
            var inside = new List<Health>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (((Vector2)e.transform.position - centre).sqrMagnitude <= _radius * _radius) inside.Add(hp);
            }

            foreach (var hp in inside)
            {
                if (hp == null || hp.IsDead) continue;
                switch (_element)
                {
                    case ElementType.Fire:
                        float dps = T.ResidueBurnHitUnits * (_owner != null ? _owner.HitUnit : Tuning.Player.BaseDamage);
                        if (_owner != null) _owner.Burn(hp, dps, TickSeconds * 2f);
                        else StatusEffects.Get(hp.gameObject).ApplyBurn(dps, TickSeconds * 2f, null);
                        break;
                    case ElementType.Water:
                        if (_owner != null) _owner.Soak(hp, T.ResidueSoakSeconds, Tuning.Elements.Water.SoakVulnerability);
                        else StatusEffects.Get(hp.gameObject).ApplySoak(T.ResidueSoakSeconds, Tuning.Elements.Water.SoakVulnerability);
                        break;
                    case ElementType.Earth:
                        StatusEffects.Get(hp.gameObject).ApplySlow(TickSeconds * 2f, T.ResidueSlow);
                        break;
                    default:
                        DragToward.Pull(hp, centre, T.ResiduePull * TickSeconds);
                        break;
                }
            }
        }
    }
}
