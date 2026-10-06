using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// How hard a floor is, as one formula in one place.
    ///
    /// THE PROBLEM THIS EXISTS TO FIX, stated plainly because it is not obvious from any single
    /// number: only HP and damage ever scaled with depth. Move speed, telegraph duration and
    /// attack cadence were flat constants from floor 1 to floor 100. A chaser moved at 2.1 against
    /// a player at 6.5 forever - so a player who knows how to kite is never in danger at ANY depth,
    /// and the growing health pool only makes the kiting take longer.
    ///
    ///     HP and COUNT make a floor LONGER.
    ///     SPEED, DAMAGE, TELEGRAPH and CADENCE make a floor DANGEROUS.
    ///
    /// Tension comes only from the second group. Scaling the first alone produces tedium, and
    /// measurement said the old curve did exactly that: with the exchange ledger's own growth
    /// factored in, a floor-70 clear took about twenty minutes and a floor-100 clear over forty,
    /// while the player stayed 3.1x faster than anything chasing them the entire way down.
    ///
    /// THE ESCAPE RATIO IS THE REAL DIFFICULTY DIAL - how many times faster the player is than the
    /// thing chasing them. Above about 3x you simply leave; below about 2x an enemy can genuinely
    /// cut you off. The old curve held it at 3.1x forever. This one closes it:
    ///
    ///     floor      10     30     50     70    100
    ///     escape    2.8x   2.3x   2.0x   1.8x   1.5x
    ///     clear      45s   166s   248s   339s   510s
    ///
    /// Clear times were MEASURED against the ledger's real growth (x1.24 at floor 10 rising to
    /// x3.70 at floor 100, sampled over 200 simulated runs of the actual boon catalogue), not
    /// assumed - the player gets stronger too, and a curve designed against a static player is a
    /// curve designed against nobody.
    ///
    /// EVERY MULTIPLIER IS CAPPED. An uncapped linear term is fine to floor 100 and absurd past
    /// it, and nothing here should stop being sane because someone changed the floor ceiling.
    /// </summary>
    public static class FloorDifficulty
    {
        /// <summary>Health per floor. LOWER than the old 0.12, deliberately - the old value was
        /// most of what made deep floors long rather than hard.</summary>
        public static float Hp(int floor) => 1f + Mathf.Max(0, floor - 1) * Tuning.Enemy.HpPerFloor;

        /// <summary>Damage per floor. Slightly steeper than health, so a deep enemy threatens more
        /// than it endures - which is the direction tension comes from.</summary>
        public static float Damage(int floor) => 1f + Mathf.Max(0, floor - 1) * Tuning.Enemy.DamagePerFloor;

        /// <summary>
        /// Move speed per floor - THE AXIS THAT DID NOT EXIST.
        ///
        /// Capped at twice base, which for a chaser is 4.2 against the player's 6.5. Deliberately
        /// still slower than the player: enemies that match or beat the player's speed remove
        /// disengaging as an option entirely, and this project's own notes make being able to
        /// disengage a standing promise ("the crowd is a positioning problem, not a race").
        /// Closing the gap is the goal; erasing it is a different game.
        /// </summary>
        public static float Speed(int floor)
            => Mathf.Min(1f + Mathf.Max(0, floor - 1) * Tuning.Enemy.SpeedPerFloor,
                         Tuning.Enemy.SpeedMaxMultiplier);

        /// <summary>
        /// Telegraph duration per floor, as a multiplier BELOW one - deep telegraphs are shorter,
        /// so the same attack demands a faster read.
        ///
        /// Floored well above zero. A telegraph that shrinks toward nothing stops being a
        /// telegraph, and an attack the player cannot react to is not difficulty, it is damage on
        /// a timer.
        ///
        /// THE MULTIPLIER ALONE IS NOT ENOUGH and EnemyFactory clamps the result against
        /// Tuning.Enemy.TelegraphMinSeconds as well. The Chaser's wind-up is the shortest in the
        /// game at 0.35s and 45% off that is 0.19s, under human reaction time - a rule that is
        /// correct for the long telegraphs producing an unreactable one when applied to the short
        /// one. A proportional rule needs an absolute backstop whenever the things it scales
        /// differ by more than the rule's own range.
        /// </summary>
        public static float Telegraph(int floor)
            => Mathf.Max(1f - Mathf.Max(0, floor - 1) * Tuning.Enemy.TelegraphShrinkPerFloor,
                         Tuning.Enemy.TelegraphMinMultiplier);

        /// <summary>Attack cadence, same shape as the telegraph: deep enemies swing more often.</summary>
        public static float AttackInterval(int floor)
            => Mathf.Max(1f - Mathf.Max(0, floor - 1) * Tuning.Enemy.CadencePerFloor,
                         Tuning.Enemy.CadenceMinMultiplier);

        // How MANY enemies a floor fields is no longer here: a floor is a budget of effective HP
        // spent on a random mix (Enemies.WaveComposer), not a count. The old count was
        // 3 + floor/2, capped at 14 - uncapped it reached 53 bodies on floor 100, and that with a
        // deep floor's health is where the forty-minute clear came from. The pool keeps that
        // lesson by being priced in seconds rather than bodies.

        /// <summary>
        /// A boss's health per floor - NOT the wave's curve. A boss fight's length is counted in
        /// REST windows, so its health follows how strong a typical player is at that depth (the
        /// ledger's measured growth, x1.24 at floor 10 to x3.70 at 100 - see the class header),
        /// not how long a crowd should take. On the wave's 9% a floor-90 boss would be nine times
        /// the Cantor and a ledger-only player would need ten windows. What stops a stronger
        /// player skipping the fight is the window cap, not this.
        /// </summary>
        public static float BossHp(int floor) => 1f + Mathf.Max(0, floor - 1) * Tuning.Boss.HpPerFloor;

        /// <summary>One line for the log and for any future difficulty readout.</summary>
        public static string Describe(int floor)
            => $"floor {floor}: hp x{Hp(floor):F2} dmg x{Damage(floor):F2} spd x{Speed(floor):F2} " +
               $"tell x{Telegraph(floor):F2} cadence x{AttackInterval(floor):F2}";
    }
}
