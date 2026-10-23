using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Brimstone Heart (<see cref="BombArt"/>) off its own EnemyController: the hover
    /// on a timed cycle (it bobs in place, so nothing paces it by distance), the heart swelling
    /// through a sweep's wind-up and pulsing with each fireball, the gutter after; under 20% the
    /// shaken-loose shell, the arming and the burst-open tell; flinched, the banked heart backing
    /// off.
    ///
    /// Faces the player, except through a sweep, when it holds the side the sweep was aimed at.
    /// </summary>
    public class BombVisual : TexelVisual
    {
        public static BombVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
            => Attach<BombVisual>(visual, enemy, look, elite, (int)BombFrame.Idle);

        protected override Sprite[] Build(EnemyLook look, bool elite) => BombArt.For(look, elite);
        protected override Color[] Reserved => BombArt.Reserved;
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => BombArt.For(look, elite, biome);

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;

            float now = Time.time, beat = Tuning.Enemy.BombDriftFrameSeconds;
            BombFrame frame;
            if (_enemy.Recovering)
            {
                Face();
                frame = Stride(Tuning.Enemy.ChaserStrideFrameUnits, 2) == 1 ? BombFrame.Banked1 : BombFrame.Banked0;
            }
            else if (_enemy.BombArming)
            {
                Rest();
                frame = _enemy.TelegraphRemaining <= _enemy.TelegraphDuration * (1f - Tuning.Enemy.StageFullThreshold)
                    ? BombFrame.Bursting : BombFrame.Arming;
            }
            else if (_enemy.BombGathering)
            {
                Rest();
                Face();
                frame = BombFrame.Gather;
            }
            else if (_enemy.BombSweeping || now - _enemy.BombThrewAt < Tuning.Enemy.BombThrowFrameSeconds)
            {
                Rest();
                FaceLeft(_enemy.BombAimsLeft);
                frame = now - _enemy.BombThrewAt < Tuning.Enemy.BombThrowFrameSeconds ? BombFrame.Throw0 : BombFrame.Throw1;
            }
            else if (_enemy.BombSpent)
            {
                Rest();
                FaceLeft(_enemy.BombAimsLeft);
                frame = BombFrame.Spent;
            }
            else
            {
                Rest();
                Face();
                int tick = Mathf.FloorToInt(now / beat);
                frame = _enemy.BombCritical
                    ? (tick % 2 == 0 ? BombFrame.Critical0 : BombFrame.Critical1)
                    : (BombFrame)((int)BombFrame.Drift0 + tick % 4);
            }
            Show((int)frame);
        }
    }
}
