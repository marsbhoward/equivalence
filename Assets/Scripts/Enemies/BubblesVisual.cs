using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Drowned Bell (<see cref="BubblesArt"/>) off its own EnemyController: hovering and
    /// pulsing while it waits, turned to the ally it picked while it draws water (the tell for the
    /// last 30%), the blow as the bubble leaves (the drawn bubble is the shielded body's own
    /// <see cref="BubbleShell"/>), tending while its bubble holds or it recovers, and a jolt when
    /// flinched.
    /// </summary>
    public class BubblesVisual : TexelVisual
    {
        const float IdleFrameSeconds = 0.4f, TendFrameSeconds = 0.5f, BlowFrameSeconds = 0.15f, JoltSeconds = 0.5f;

        [System.NonSerialized] float _flinchedAt = -10f;
        bool _wasFlinched;

        public static BubblesVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
            => Attach<BubblesVisual>(visual, enemy, look, elite, (int)BubblesFrame.Idle0);

        protected override Sprite[] Build(EnemyLook look, bool elite) => BubblesArt.For(look, elite);

        static int Cycle(float seconds, int frames) => Mathf.FloorToInt(Time.time / seconds) % frames;

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;
            bool flinched = _enemy.Flinched;
            if (flinched && !_wasFlinched) _flinchedAt = Time.time;
            _wasFlinched = flinched;

            float t = _enemy.TelegraphProgress01, since = Time.time - _enemy.BubblesBlewAt;
            BubblesFrame frame;
            if (flinched)
                frame = Time.time - _flinchedAt < JoltSeconds ? BubblesFrame.Jolt : Tend();
            else if (since < BlowFrameSeconds * 2f)
                frame = since < BlowFrameSeconds ? BubblesFrame.Blow0 : BubblesFrame.Blow1;
            else if (t > 0f)
            {
                var ally = _enemy.BubblesFocus;
                if (ally != null)
                {
                    float dx = ally.transform.position.x - _enemy.transform.position.x;
                    if (Mathf.Abs(dx) > 0.15f) FaceLeft(dx < 0f);
                }
                frame = t < Tuning.Enemy.StageFullThreshold ? BubblesFrame.Fill0 : BubblesFrame.Fill1;
            }
            else if (_enemy.BubblesTending || _enemy.Recovering)
                frame = Tend();
            else
            {
                Face();
                frame = (BubblesFrame)((int)BubblesFrame.Idle0 + Cycle(IdleFrameSeconds, 4));
            }
            Show((int)frame);
        }

        static BubblesFrame Tend() => Cycle(TendFrameSeconds, 2) == 0 ? BubblesFrame.Tend0 : BubblesFrame.Tend1;
    }
}
