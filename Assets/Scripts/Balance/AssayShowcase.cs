using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Balance
{
    public static partial class Assay
    {
        /// <summary>
        /// THE SHOWCASE CLIMB in the base kit (the user's target, 2026-11-03 - BALANCE.md "The next
        /// pass"): a good player reaches the floor-10 boss in Bronze, even too weak and too hurt to
        /// win it. Walks floors 1-9 for a player taking <c>blows</c> Chaser-sized blows a floor: the
        /// blows land through the Bronze set's cover (Tuning.Defense.ProtectionFor) as it wears past
        /// its plating, the phial is drunk whenever a whole drink fits under max health, and the
        /// restore ring (even floors) puts a charge back when one is missing and its rest is over -
        /// else repairs. Reports what reaches floor 10 - health, drinks left, armour - or where the
        /// player fell. Not modelled: element sustain, spires, lifesteal, spilled drinks.
        /// <c>unity command eval --code 'return Convergence.Balance.Assay.ShowcaseClimb();'</c>
        /// </summary>
        public static string ShowcaseClimb(float maxHp = Tuning.Player.HpFireWater, float kitArmor = 16f,
                                           int phialTier = 0)
        {
            var saved = RunFormat.Current;
            var sb = new StringBuilder();
            sb.AppendLine($"Showcase climb, Bronze kit ({maxHp:0} HP, Armor {kitArmor:0} = {kitArmor / Tuning.Defense.ArmorPerPlate:0.#} plates, " +
                          $"{Tuning.Phial.TierOf(phialTier)} phial {Tuning.Phial.ChargesFor(phialTier)} x {Tuning.Phial.HealFor(phialTier):P0})");
            try
            {
                RunFormat.Current = RunFormatId.Showcase;
                int end = Tuning.Showcase.EndFloor;
                for (int blows = 1; blows <= 6; blows++)
                    sb.AppendLine($"  {blows} blow{(blows == 1 ? " " : "s")} a floor: {Climb(blows, end, maxHp, kitArmor, phialTier)}");
            }
            finally
            {
                RunFormat.Current = saved;
            }
            return sb.ToString();
        }

        static string Climb(int blows, int end, float maxHp, float kitArmor, int tier)
        {
            float protect = Assume.ArmourPieces * Tuning.Defense.ProtectionFor(LootTier.Bronze);
            float pool = StatPercents.Apply(100f, kitArmor);
            float plates = kitArmor / Tuning.Defense.ArmorPerPlate;
            float drink = maxHp * Tuning.Phial.HealFor(tier);
            int maxCharges = Tuning.Phial.ChargesFor(tier);

            float hp = maxHp, condition = 1f;
            int charges = maxCharges, cardFrom = 0, drunk = 0;
            for (int floor = 1; floor < end; floor++)
            {
                float hit = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser).Damage * Enemies.FloorDifficulty.Damage(floor);
                // Blow by blow, so a drink comes between them as a player would take it; the floor's
                // first blows are plated and wear nothing.
                for (int b = 0; b < blows; b++)
                {
                    hp -= hit * (1f - protect * condition);
                    if (b + 1 > plates) condition = Mathf.Max(0f, condition - Tuning.Defense.ArmorWearPerHit / pool);
                    if (hp <= 0f) return $"falls on floor {floor} ({drunk} drinks used)";
                    while (charges > 0 && hp <= maxHp - drink) { hp += drink; charges--; drunk++; }
                }

                // The restore ring follows even floors (GameBootstrap.RingsAfter).
                if (floor % 2 == 0)
                {
                    if (charges < maxCharges && floor >= cardFrom) { charges++; cardFrom = floor + Tuning.Phial.CardCooldownFloors; }
                    else if (condition < 0.995f) condition = Mathf.Min(1f, condition + Assume.RepairRingFraction);
                }
            }
            float bossHit = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser).Damage * Enemies.FloorDifficulty.Damage(end)
                          * (1f - protect * condition);
            return $"reaches floor {end} with {hp:0}/{maxHp:0} HP, {charges} drinks, armor {condition:P0} " +
                   $"(a Chaser-sized blow there: {bossHit:0}; {drunk} drinks used)";
        }
    }
}
