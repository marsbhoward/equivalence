using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// Which of a KIND's two coexisting looks - an alchemical construct, or a mechanical
    /// automaton - is on screen. Purely cosmetic: EnemyDef, EnemyController and EnemyFactory never
    /// branch on this, only each kind's own Art lambda (see EnemyTypes) and its EnemyStageCycle
    /// do. Both looks of a kind share one stat block, one telegraph, one recover beat - a look is
    /// a coat of paint, not a fact about what the enemy does. (The same distinction the weapon
    /// notes elsewhere in this project make about tier: a look is a SOURCE, not a mechanic.)
    /// </summary>
    public enum EnemyLook { Construct, Automaton }

    /// <summary>
    /// Started life as Bomb-only (BombVariant). Turret becoming a second kind wanting the exact
    /// same "roll one look per floor" shape is the point at which this project's own stated
    /// discipline turns a copy into a generalisation - see the parallel-carry-flow note elsewhere
    /// in this codebase for the rule this is following: build the second one plain, and only
    /// generalise once a second consumer actually needs the identical shape.
    ///
    /// CHOSEN ONCE PER FLOOR PER KIND, NOT PER SPAWN. Rolling per spawn would mean a single room
    /// mixing both looks of one kind, which reads as two different enemies rather than one enemy
    /// wearing a different coat - the room has to agree with itself. GameBootstrap.NextFloor rolls
    /// a fresh look for every kind that has one, before any of that floor's enemies spawn.
    ///
    /// A plain static Dictionary rather than one static field per kind. Domain-reload safe by
    /// construction and for a different reason than the Dictionary trap documented elsewhere in
    /// this project: that trap is about a Dictionary FIELD ON A LIVE MONOBEHAVIOUR, where a reload
    /// empties it while the GameObject and its sibling (serializable) fields survive untouched,
    /// leaving one thing silently out of step with everything around it. This Dictionary belongs
    /// to no scene object - a reload wipes EVERY static field uniformly and reinitialises this one
    /// straight back to empty, which is exactly the same "nothing rolled yet" state a cold start
    /// begins in. There is no sibling state for it to fall out of sync with.
    /// </summary>
    public static class EnemyLooks
    {
        static readonly Dictionary<EnemyKind, EnemyLook> _current = new();

        /// <summary>Construct until something rolls it - matches the old BombVariant default.</summary>
        public static EnemyLook Of(EnemyKind kind) =>
            _current.TryGetValue(kind, out var look) ? look : EnemyLook.Construct;

        public static void Roll(EnemyKind kind) =>
            _current[kind] = Random.value < 0.5f ? EnemyLook.Construct : EnemyLook.Automaton;
    }
}
