using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Combat
{
    /// <summary>
    /// What a release leaves on the ground for a few seconds (the exchange's Residue), in the
    /// element's own terms: Fire's burns, Water's soaks, Earth's slows, Air's draws enemies in.
    /// Drawn as that element's own ground (GroundPatch - the user's pick, group 3): smouldering
    /// coals, a rippling puddle, cracked rubble, grit drawn round inward.
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
        [SerializeField] GroundPatch _patch;

        /// <summary>A FORM's field draws fainter when its slot is shared (the clutter guards).</summary>
        public float AlphaMul = 1f;
        /// <summary>Past the cap on form visuals the field still works but draws nothing.</summary>
        public bool Hidden;

        /// <summary>EARTHQUAKE (an evolved form): enemies on the field are staggered this long each
        /// tick, through the owner's own Stagger. 0 for every other field.</summary>
        public float StaggerSeconds;

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
            f._patch = GroundPatch.Spawn(go, go.transform.position, f._radius, GroundPatch.Of(element), SortingOrders.GroundLasting);   // with the fire pools, over the pits
            f.Paint();
            return f;
        }

        void Paint()
        {
            if (_patch == null) return;
            _patch.Alpha = Hidden ? 0f : AlphaMul * Mathf.Clamp01(_remaining / Mathf.Max(0.01f, _life * 0.35f));
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
                        // Earthquake: staggered once, then held - a stack a tick would reach the
                        // deepest slow at once.
                        if (StaggerSeconds > 0f && _owner != null)
                        {
                            var st = StatusEffects.Get(hp.gameObject);
                            if (st.Staggered) st.ExtendStagger(StaggerSeconds);
                            else _owner.Stagger(hp, StaggerSeconds);
                        }
                        break;
                    default:
                        DragToward.Pull(hp, centre, T.ResiduePull * TickSeconds);
                        break;
                }
            }
        }
    }
}
