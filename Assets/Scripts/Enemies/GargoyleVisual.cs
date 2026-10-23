using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Watcher (<see cref="GargoyleArt"/>) off its own EnemyController: breathing while
    /// it watches, the gaze through the telegraph (the tell for its last 30%), the root or the
    /// elite's slam the moment it lands, resting through the recover beat, a jolt when flinched,
    /// and an elite's wings in flight - lifted off the floor by the drawing alone, the body's
    /// position staying where the game measures it. An elite's gaze cracks the floor out to its
    /// slam's edge (<see cref="GargoyleCracks"/>).
    /// </summary>
    public class GargoyleVisual : TexelVisual
    {
        const float IdleFrameSeconds = 0.4f, RestFrameSeconds = 0.5f, FlyFrameSeconds = 0.12f;
        const float StrikeFrameSeconds = 0.25f, JoltSeconds = 0.5f;
        /// <summary>How high the drawing rises at the middle of a flight, world units.</summary>
        const float FlightLift = 0.9f;

        [SerializeField] Rigidbody2D _rb;
        [SerializeField] GargoyleCracks _cracks;
        [System.NonSerialized] float _flinchedAt = -10f;
        bool _wasFlinched;

        public static GargoyleVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
        {
            var v = Attach<GargoyleVisual>(visual, enemy, look, elite, (int)GargoyleFrame.Idle0);
            if (v != null) v._rb = enemy.GetComponent<Rigidbody2D>();
            return v;
        }

        protected override Sprite[] Build(EnemyLook look, bool elite) => GargoyleArt.For(look, elite);
        protected override Color[] Reserved => GargoyleArt.Reserved;
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => GargoyleArt.For(look, elite, biome);

        static int Cycle(float seconds, int frames) => Mathf.FloorToInt(Time.time / seconds) % frames;

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;
            bool flinched = _enemy.Flinched;
            if (flinched && !_wasFlinched) _flinchedAt = Time.time;
            _wasFlinched = flinched;

            float t = _enemy.TelegraphProgress01, lift = 0f;
            GargoyleFrame frame;
            if (_enemy.GargoyleFlying)
            {
                frame = Cycle(FlyFrameSeconds, 2) == 0 ? GargoyleFrame.Fly0 : GargoyleFrame.Fly1;
                if (_rb != null && Mathf.Abs(_rb.linearVelocity.x) > 0.1f) FaceLeft(_rb.linearVelocity.x < 0f);
                lift = Mathf.Sin(Mathf.PI * _enemy.GargoyleFlightProgress01) * FlightLift;
            }
            else if (flinched)
                frame = Time.time - _flinchedAt < JoltSeconds ? GargoyleFrame.Jolt
                      : Cycle(RestFrameSeconds, 2) == 0 ? GargoyleFrame.Rest0 : GargoyleFrame.Rest1;
            else if (Time.time - _enemy.GargoyleStruckAt < StrikeFrameSeconds)
                frame = _elite ? GargoyleFrame.Slam : GargoyleFrame.Cast;
            else if (t > 0f)
            {
                Face();
                frame = t < Tuning.Enemy.StageFullThreshold ? GargoyleFrame.Gaze0 : GargoyleFrame.Gaze1;
            }
            else if (_enemy.Recovering)
                frame = Cycle(RestFrameSeconds, 2) == 0 ? GargoyleFrame.Rest0 : GargoyleFrame.Rest1;
            else
            {
                Face();
                frame = (GargoyleFrame)((int)GargoyleFrame.Idle0 + Cycle(IdleFrameSeconds, 4));
            }
            Show((int)frame);
            transform.localPosition = new Vector3(0f, lift, 0f);

            TickCracks(t);
        }

        void TickCracks(float t)
        {
            if (!_elite) return;
            if (t > 0f && !_enemy.GargoyleFlying)
            {
                if (_cracks == null) _cracks = GargoyleCracks.Create(_enemy.transform.parent, _enemy.transform.position);
                _cracks.Grow(t);
                return;
            }
            if (_cracks == null) return;
            if (Time.time - _enemy.GargoyleStruckAt < StrikeFrameSeconds) _cracks.Burst();
            else _cracks.Cancel();
            _cracks = null;
        }

        void OnDisable()
        {
            transform.localPosition = Vector3.zero;
            if (_cracks != null) { _cracks.Cancel(); _cracks = null; }
        }
    }
}
