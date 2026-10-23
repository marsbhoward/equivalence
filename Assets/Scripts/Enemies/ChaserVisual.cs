using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Grave Brute (<see cref="ChaserArt"/>) off its own EnemyController: the walk
    /// paced by distance walked, the raise and the cocked tell through the telegraph, the strike
    /// as the hit lands, the vein left lit before an elite's riposte and its backhand, then the
    /// planted recovery.
    ///
    /// Faces the player, and only turns between swings: from the telegraph to the end of the
    /// strike (and through a riposte) the facing is held, so a chop never flips mid-swing.
    /// </summary>
    public class ChaserVisual : TexelVisual
    {
        public static ChaserVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
            => Attach<ChaserVisual>(visual, enemy, look, elite, (int)ChaserFrame.Idle);

        protected override Sprite[] Build(EnemyLook look, bool elite) => ChaserArt.For(look, elite);
        protected override Color[] Reserved => ChaserArt.Reserved;
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => ChaserArt.For(look, elite, biome);

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;

            float now = Time.time;
            float strike = Tuning.Enemy.ChaserStrikeFrameSeconds;
            bool riposteOwed = _enemy.RiposteDueAt > _enemy.SwungAt && now < _enemy.RiposteDueAt;
            bool riposteShown = _enemy.RiposteAt >= _enemy.SwungAt && now - _enemy.RiposteAt < strike;

            ChaserFrame frame;
            bool swinging = true;
            float tele = _enemy.TelegraphRemaining;
            if (tele > 0f)
                frame = tele <= _enemy.TelegraphDuration * Tuning.Enemy.ChaserCockedShare
                    ? ChaserFrame.Cocked : ChaserFrame.Raise;
            else if (now - _enemy.SwungAt < strike) frame = ChaserFrame.Strike;
            else if (riposteShown) frame = ChaserFrame.Riposte;
            else if (riposteOwed && !_enemy.Flinched) frame = ChaserFrame.StillLit;
            else
            {
                swinging = false;
                if (_enemy.Recovering) { frame = ChaserFrame.Recover; Rest(); }
                else
                {
                    int step = Stride(Tuning.Enemy.ChaserStrideFrameUnits, 4);
                    frame = step < 0 ? ChaserFrame.Idle : (ChaserFrame)((int)ChaserFrame.Walk0 + step);
                }
            }

            if (!swinging) Face();
            else Rest(); // a lunge's shove is not a step
            Show((int)frame);
        }
    }
}
