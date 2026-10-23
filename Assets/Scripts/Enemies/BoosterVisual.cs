using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Green Lion (<see cref="BoosterArt"/>) off its own EnemyController: lying and
    /// breathing while it waits, turned to the ally it picked while it draws breath (the tell for
    /// the last 30%), the roar the moment the power lands - its waves rolling out to every ally it
    /// reached (<see cref="BoosterRoar"/>) - spent with its head on its paws while its allies live
    /// or it recovers, and a jolt when flinched.
    /// </summary>
    public class BoosterVisual : TexelVisual
    {
        const float IdleFrameSeconds = 0.4f, SpentFrameSeconds = 0.5f, RoarFrameSeconds = 0.1f;
        const float RoarSeconds = 0.4f, JoltSeconds = 0.5f;

        [System.NonSerialized] float _flinchedAt = -10f;
        [System.NonSerialized] float _roarShownAt = -10f;
        bool _wasFlinched;

        public static BoosterVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
            => Attach<BoosterVisual>(visual, enemy, look, elite, (int)BoosterFrame.Idle0);

        protected override Sprite[] Build(EnemyLook look, bool elite) => BoosterArt.For(look, elite);
        protected override Color[] Reserved => BoosterArt.Reserved;
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => BoosterArt.For(look, elite, biome);

        static int Cycle(float seconds, int frames) => Mathf.FloorToInt(Time.time / seconds) % frames;

        void FaceAt(EnemyController ally)
        {
            if (ally == null) { Face(); return; }
            float dx = ally.transform.position.x - _enemy.transform.position.x;
            if (Mathf.Abs(dx) > 0.15f) FaceLeft(dx < 0f);
        }

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;
            bool flinched = _enemy.Flinched;
            if (flinched && !_wasFlinched) _flinchedAt = Time.time;
            _wasFlinched = flinched;

            float t = _enemy.TelegraphProgress01;
            BoosterFrame frame;
            if (flinched)
                frame = Time.time - _flinchedAt < JoltSeconds ? BoosterFrame.Jolt : Spent();
            else if (Time.time - _enemy.BoosterRoaredAt < RoarSeconds)
            {
                FaceAt(_enemy.BoosterRoaredA);
                frame = Cycle(RoarFrameSeconds, 2) == 0 ? BoosterFrame.Roar0 : BoosterFrame.Roar1;
            }
            else if (t > 0f)
            {
                FaceAt(_enemy.BoosterFocus);
                frame = t < Tuning.Enemy.StageFullThreshold ? BoosterFrame.Breath0 : BoosterFrame.Breath1;
            }
            else if (_enemy.BoosterSpent || _enemy.Recovering)
                frame = Spent();
            else
            {
                Face();
                frame = (BoosterFrame)((int)BoosterFrame.Idle0 + Cycle(IdleFrameSeconds, 4));
            }
            Show((int)frame);

            // The waves leave once per roar, from the jaws as drawn on its first roar frame.
            if (_enemy.BoosterRoaredAt > _roarShownAt)
            {
                _roarShownAt = _enemy.BoosterRoaredAt;
                var m = BoosterArt.MouthOffset(BoosterFrame.Roar0, _elite);
                if (_faceLeft) m.x = -m.x;
                BoosterRoar.Create(_enemy.transform.parent, (Vector2)_enemy.transform.position + m,
                                   _enemy.BoosterRoaredA, _enemy.BoosterRoaredB);
            }
        }

        static BoosterFrame Spent() => Cycle(SpentFrameSeconds, 2) == 0 ? BoosterFrame.Spent0 : BoosterFrame.Spent1;
    }
}
