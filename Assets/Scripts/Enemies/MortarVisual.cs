using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Salamander (<see cref="MortarArt"/>) off its own EnemyController: the sprawling
    /// walk paced by distance (facing where it runs when it flees, at you otherwise), reared and
    /// swelling through a lob's wind-up (the tell for the last 30%), the head thrown up as each
    /// shell leaves, reared throughout a barrage, crouched with its gills flared before the flame,
    /// breathing fire toward its aim, recovering head low, and a jolt when flinched.
    /// </summary>
    public class MortarVisual : TexelVisual
    {
        const float StrideFrameUnits = 0.22f, LobFrameSeconds = 0.2f, BreathFrameSeconds = 0.1f, JoltSeconds = 0.5f;

        [SerializeField] Rigidbody2D _rb;
        [System.NonSerialized] float _flinchedAt = -10f;
        bool _wasFlinched;

        public static MortarVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
        {
            var v = Attach<MortarVisual>(visual, enemy, look, elite, (int)MortarFrame.Walk0);
            if (v != null) v._rb = enemy.GetComponent<Rigidbody2D>();
            return v;
        }

        protected override Sprite[] Build(EnemyLook look, bool elite) => MortarArt.For(look, elite);
        protected override Color[] Reserved => MortarArt.Reserved;
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => MortarArt.For(look, elite, biome);

        static int Cycle(float seconds, int frames) => Mathf.FloorToInt(Time.time / seconds) % frames;

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;
            bool flinched = _enemy.Flinched;
            if (flinched && !_wasFlinched) _flinchedAt = Time.time;
            _wasFlinched = flinched;

            MortarFrame frame;
            bool walked = false;
            if (flinched)
                frame = Time.time - _flinchedAt < JoltSeconds ? MortarFrame.Jolt : MortarFrame.Rest;
            else if (_enemy.MortarFlaming)
            {
                if (Mathf.Abs(_enemy.MortarFlameDir.x) > 0.05f) FaceLeft(_enemy.MortarFlameDir.x < 0f);
                frame = Cycle(BreathFrameSeconds, 2) == 0 ? MortarFrame.Breath0 : MortarFrame.Breath1;
            }
            else if (_enemy.MortarFlameReady)
            {
                Face();
                frame = MortarFrame.Ready;
            }
            else if (Time.time - _enemy.MortarLobbedAt < LobFrameSeconds)
                frame = MortarFrame.Lob;
            else if (_enemy.MortarWindingUp)
            {
                Face();
                frame = _enemy.TelegraphProgress01 < Tuning.Enemy.StageFullThreshold ? MortarFrame.Plant0 : MortarFrame.Plant1;
            }
            else if (_enemy.MortarBarraging)
                frame = MortarFrame.Plant1;
            else if (_enemy.Recovering)
                frame = MortarFrame.Rest;
            else
            {
                int step = Stride(StrideFrameUnits, 4);
                walked = true;
                frame = step < 0 ? MortarFrame.Walk0 : (MortarFrame)((int)MortarFrame.Walk0 + step);
                // fleeing: it runs the way it is going; otherwise it keeps its eyes on you
                var target = _enemy.Target;
                Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;
                if (step >= 0 && target != null && v.sqrMagnitude > 0.04f
                    && Vector2.Dot(v, (Vector2)target.position - (Vector2)_enemy.transform.position) < 0f && Mathf.Abs(v.x) > 0.1f)
                    FaceLeft(v.x < 0f);
                else Face();
            }
            if (!walked) Rest();
            Show((int)frame);
        }
    }
}
