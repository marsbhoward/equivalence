using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// LINK (an Art Role form, the overhaul): bodies one art strikes are linked for a moment - a share
    /// of the damage one takes (not a tick, not a link's own) reaches the others. Grouped by the art
    /// that linked them; one link per body, the newest winning.
    /// </summary>
    public class LinkedBodies : MonoBehaviour
    {
        [SerializeField] int _group;
        [SerializeField] float _until, _share;
        GameObject _source;
        Health _health;
        static bool _passing;
        static readonly List<LinkedBodies> Live = new();

        public static void Link(Health body, int group, float seconds, float share, GameObject source)
        {
            if (body == null || body.IsDead) return;
            var l = body.GetComponent<LinkedBodies>();   // never ?? - a missing component isn't C# null
            if (l == null) l = body.gameObject.AddComponent<LinkedBodies>();
            l._group = group;
            l._until = Time.time + seconds;
            l._share = share;
            l._source = source;
            if (!Live.Contains(l)) Live.Add(l);
        }

        void Awake()
        {
            _health = GetComponent<Health>();
            if (_health != null) _health.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            Live.Remove(this);
        }

        void OnDamaged(DamageInfo info)
        {
            if (_passing || info.Tick || Time.time > _until || info.Amount <= 0f) return;
            _passing = true;
            try
            {
                Live.RemoveAll(x => x == null);
                foreach (var other in Live)
                {
                    if (other == this || other._group != _group || Time.time > other._until) continue;
                    var hp = other._health;
                    if (hp == null || hp.IsDead) continue;
                    hp.Take(new DamageInfo(info.Amount * _share, info.Element, _source) { Entry = "ar_link", Tick = true });
                    TexelFx.Star(hp.transform.position, LedgerFx.Water, 2, 0.06f);
                }
            }
            finally { _passing = false; }
        }
    }
}
