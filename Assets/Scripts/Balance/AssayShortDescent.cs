using System.Text;
using Convergence.Core;

namespace Convergence.Balance
{
    public static partial class Assay
    {
        /// <summary>
        /// THE SHORT DESCENT through the Assay (SHORT-DESCENT.md, "Building it"): how many ordinary
        /// Chaser hits each Max build survives on Short floors, against Classic at the same DEPTH,
        /// for a fast, an on-pace and a slow player - and deep in the Gate Collapse.
        ///
        /// The Assay counts in CLASSIC depth (its curves run 1..100), so the model runs as Classic and
        /// only the Short Descent's hit is priced as Short: its depth damage (divided by an on-pace
        /// player's heat) times the heat of the clock in question. On pace the two should agree
        /// within half a heat step. NOT modelled: the Short Descent's halved reward rings (its heals
        /// and art offers) - the Assay prices the ledger, not the rings - so a real Short run is a
        /// little HARDER than this says, as the user wanted it no easier than Classic.
        /// <c>unity command eval --code 'return Convergence.Balance.Assay.ShortDescentReport();'</c>
        /// </summary>
        public static string ShortDescentReport(Policy policy = Policy.Sensible, ElementType element = ElementType.Fire)
        {
            var saved = RunFormat.Current;
            var sb = new StringBuilder();
            sb.AppendLine($"Chaser hits survived, Max kit, {element}, {policy} picks. Classic at the same depth; " +
                          "Short at fast (x0.75 the on-pace clock) / on pace (60 min over 50 floors) / slow (+12 min)");
            try
            {
                foreach (Archetype arch in System.Enum.GetValues(typeof(Archetype)))
                {
                    RunFormat.Current = RunFormatId.Classic;
                    var curve = Run(Make(arch, element, Kit.Max), policy);
                    foreach (int floor in new[] { 10, 25, 40, 50 })
                    {
                        RunFormat.Current = RunFormatId.Classic;
                        int depth = floor * 2;
                        float ehp = curve.EffectiveHp[depth];
                        float classicHits = ehp / ChaserHit(depth);

                        RunFormat.Current = RunFormatId.ShortDescent;
                        float shortHit = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser).Damage
                                       * Enemies.FloorDifficulty.Damage(floor);
                        float onPace = floor * Tuning.RunClock.CollapseAtSeconds / RunFormat.FinalFloor;
                        float Hits(float clock) => ehp / (shortHit * RunClock.HeatAt(clock));
                        sb.AppendLine($"{arch,-12} floor {floor,2} (depth {depth,3}): classic {classicHits,5:0.0}  |  short " +
                                      $"{Hits(onPace * 0.75f),5:0.0} / {Hits(onPace),5:0.0} / {Hits(onPace + 720f),5:0.0}");
                    }
                }
                // The Gate Collapse at floor 50: the price of a stalled run.
                RunFormat.Current = RunFormatId.ShortDescent;
                float hit50 = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser).Damage * Enemies.FloorDifficulty.Damage(50);
                RunFormat.Current = RunFormatId.Classic;
                var striker = Run(Make(Archetype.Striker, element, Kit.Max), policy);
                sb.Append("Striker at floor 50 deep in the Gate Collapse - hits survived at 60 / 70 / 80 / 90 min: ");
                foreach (int minutes in new[] { 60, 70, 80, 90 })
                    sb.Append($"{striker.EffectiveHp[100] / (hit50 * RunClock.HeatAt(minutes * 60f)):0.0}  ");
                sb.AppendLine();
            }
            finally
            {
                RunFormat.Current = saved;
            }
            return sb.ToString();
        }
    }
}
