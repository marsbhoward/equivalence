using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public ElementType Element;
        public bool Crit;
        public GameObject Source;
        public Vector2 Knockback;

        /// <summary>
        /// This effect's PURPOSE is to move the target, so it moves even an
        /// <see cref="Health.Immovable"/> body.
        ///
        /// Deliberately opt-in and rare. Undertow is the case it exists for: gathering the fight
        /// into one place is the whole move, and it is the one displacement a player aims with.
        /// Ordinary hits and heavy finishers alike leave this false - they land, they do not
        /// push.
        /// </summary>
        public bool Displaces;

        /// <summary>
        /// This damage arrived from a projectile rather than from a swing.
        ///
        /// Elements read it because "did you have to be there" is the question several of them
        /// are really asking - fire's heat is meant to come from closing in, and a thrown weapon
        /// would otherwise hand it to you from across the room for free.
        /// </summary>
        public bool Thrown;

        /// <summary>
        /// This is the killing blow of the katana signature's draw-cut. Read only by the death
        /// VFX (a non-boss body cut in two); it changes a picture and nothing about the hit.
        /// Opt-in and rarer even than <see cref="Displaces"/> - exactly one AttackStep sets it.
        /// </summary>
        public bool Bisects;

        /// <summary>
        /// This hit is a FINISHER - basic or not, Light through Heavy, however it was delivered
        /// (a swing, a thrown blade, a disc volley, an echo of one). Nothing in the game read
        /// finisher-ness at the DamageInfo level before Bubbles needed it: EnemyController.Health
        /// pops a bubbled enemy's shield through Health.BlocksHit, which only ever sees the
        /// DamageInfo a hit arrives with, never the AttackStep or the bool ResolveArc computed it
        /// from. Deliberately NOT set for an elemental release, a principle-chain payoff, or any
        /// other proc damage - "finisher" here means specifically the combo-wheel move, matching
        /// the exact word the mechanic was asked for in.
        /// </summary>
        public bool IsFinisher;

        /// <summary>
        /// This hit must not trigger <see cref="Hitstop"/>, even though it's a finisher - the
        /// katana signature's two pre-cuts set this so the sequence produces one freeze (off the
        /// draw-cut) rather than three. See Moveset.AttackStep.SuppressHitstop.
        /// </summary>
        public bool SuppressHitstop;

        /// <summary>
        /// A PRICE the run's ledger charges (Blood Price, Backfire, Toll, a bleed it owes) rather
        /// than a hit: Health takes it straight off - no immunity, no mitigation, no ward or armour,
        /// none of the incoming hooks - and only Second Wind may still catch it. Listeners that
        /// treat a hit as a hit (armour wear, Salt's Ward) skip it.
        /// </summary>
        public bool Price;

        public DamageInfo(float amount, ElementType element, GameObject source)
        {
            Amount = amount; Element = element; Source = source; Crit = false; Knockback = Vector2.zero;
            Displaces = false;
            Thrown = false;
            Bisects = false;
            IsFinisher = false;
            SuppressHitstop = false;
            Price = false;
        }
    }
}
