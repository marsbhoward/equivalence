using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Cinder Stinger (<see cref="RangedArt"/>) off its own EnemyController: the scuttle
    /// paced by distance, the tail arching through the telegraph to the LOCKED tell (all three
    /// lobes lit when an elite's volley is next), the recoil as the bolt leaves, then the
    /// backward scuttle of its kite.
    ///
    /// Faces the player, except from the lock to the shot, when it holds the side the controller
    /// AIMED at (EnemyController.AimsLeft): the shot leaves the mirrored muzzle on that side, so
    /// the picture has to agree with it.
    /// </summary>
    public class RangedVisual : TexelVisual
    {
        public static RangedVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
            => Attach<RangedVisual>(visual, enemy, look, elite, (int)RangedFrame.Idle);

        protected override Sprite[] Build(EnemyLook look, bool elite) => RangedArt.For(look, elite);

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;

            RangedFrame frame;
            float t = _enemy.TelegraphProgress01;
            if (t > 0f)
            {
                frame = t < Tuning.Enemy.StageFullThreshold ? RangedFrame.Aim
                      : _elite && _enemy.NextShotIsElitePattern ? RangedFrame.LockedVolley
                      : RangedFrame.Locked;
                FaceLeft(_enemy.AimsLeft);
                Rest();
            }
            else if (Time.time - _enemy.FiredAt < Tuning.Enemy.RangedFireFrameSeconds)
            {
                frame = RangedFrame.Fire;
                FaceLeft(_enemy.AimsLeft);
                Rest();
            }
            else if (_enemy.Recovering)
            {
                // the kite: backing off, two long strides
                Face();
                int step = Stride(Tuning.Enemy.RangedKiteStrideUnits, 2);
                frame = step == 1 ? RangedFrame.Kite1 : RangedFrame.Kite0;
            }
            else
            {
                Face();
                int step = Stride(Tuning.Enemy.RangedStrideFrameUnits, 4);
                frame = step < 0 ? RangedFrame.Idle : (RangedFrame)((int)RangedFrame.Walk0 + step);
            }
            Show((int)frame);
        }
    }
}
