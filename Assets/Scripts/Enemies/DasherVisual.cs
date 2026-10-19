using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Gale Hound (<see cref="DasherArt"/>) off its own EnemyController's
    /// <see cref="EnemyController.CurrentDasherPhase"/>: the lope paced by distance, the crouch
    /// through both tells with the wind streak pointing the way (<see cref="DasherPointer"/>),
    /// the line-up dash, braced, the charge with the Bite frame as it goes through you, spent,
    /// and the bounding retreat; any reel (a column, a Heavy) shows as REEL. Each dash lays its
    /// wind on the floor (<see cref="DasherTrail"/>).
    ///
    /// Faces the player, except through the charge, when it faces the way it is going, and the
    /// retreat, when it faces away.
    /// </summary>
    public class DasherVisual : TexelVisual
    {
        const float DashFrameSeconds = 0.05f, SpentFrameSeconds = 0.2f, BoundFrameSeconds = 0.15f, ReelFrameSeconds = 0.35f;

        [SerializeField] DasherPointer _pointer;
        [SerializeField] DasherTrail _trail;
        [SerializeField] Rigidbody2D _rb;
        EnemyController.DasherPhase _lastPhase;
        int _trails;

        public static DasherVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
        {
            var v = Attach<DasherVisual>(visual, enemy, look, elite, (int)DasherFrame.Idle);
            if (v != null)
            {
                v._pointer = DasherPointer.Create(enemy.transform);
                v._rb = enemy.GetComponent<Rigidbody2D>();
            }
            return v;
        }

        protected override Sprite[] Build(EnemyLook look, bool elite) => DasherArt.For(look, elite);

        float Scale => _elite ? 1.2f : 1f;

        static int Alternate(float seconds) => Mathf.FloorToInt(Time.time / seconds) % 2;

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;
            var phase = _enemy.CurrentDasherPhase;
            var aim = _enemy.DasherAim;
            TickTrail(phase);

            DasherFrame frame;
            bool tell = false;
            if (_enemy.Flinched)
            {
                frame = Alternate(ReelFrameSeconds) == 0 ? DasherFrame.Reel0 : DasherFrame.Reel1;
                Rest();
            }
            else switch (phase)
            {
                case EnemyController.DasherPhase.LineUpTell:
                case EnemyController.DasherPhase.Telegraph:
                    frame = DasherFrame.Tell;
                    tell = true;
                    FaceAlong(aim);
                    Rest();
                    break;
                case EnemyController.DasherPhase.LineUp:
                    frame = Alternate(DashFrameSeconds) == 0 ? DasherFrame.Dash0 : DasherFrame.Dash1;
                    Face();
                    Rest();
                    break;
                case EnemyController.DasherPhase.Braced:
                    frame = DasherFrame.Braced;
                    Face();
                    Rest();
                    break;
                case EnemyController.DasherPhase.Rushing:
                    frame = Time.time - _enemy.DasherBitAt < Tuning.Enemy.DasherBiteFrameSeconds ? DasherFrame.Bite
                          : Alternate(DashFrameSeconds) == 0 ? DasherFrame.Rush0 : DasherFrame.Rush1;
                    FaceAlong(aim);
                    Rest();
                    break;
                case EnemyController.DasherPhase.Spent:
                    // where the charge left it, still facing the way it went
                    frame = Alternate(SpentFrameSeconds) == 0 ? DasherFrame.Spent0 : DasherFrame.Spent1;
                    Rest();
                    break;
                case EnemyController.DasherPhase.Evading:
                    frame = Alternate(BoundFrameSeconds) == 0 ? DasherFrame.Leap : DasherFrame.Land;
                    if (_rb != null && Mathf.Abs(_rb.linearVelocity.x) > 0.1f) FaceLeft(_rb.linearVelocity.x < 0f);
                    Rest();
                    break;
                default:
                {
                    Face();
                    int step = Stride(Tuning.Enemy.DasherStrideFrameUnits, 4);
                    frame = step < 0 ? DasherFrame.Idle : (DasherFrame)((int)DasherFrame.Run0 + step);
                    break;
                }
            }
            Show((int)frame);

            if (_pointer == null) return;
            if (!tell) { _pointer.Hide(); return; }
            // out of the body the way it will go: ahead of the head across, over the back up or down
            float tpu = TexelRig.TexelsPerUnit, s = Scale;
            bool across = Mathf.Abs(aim.x) >= Mathf.Abs(aim.y);
            var from = (Vector2)_enemy.transform.position + new Vector2(0f, 2.5f * s / tpu) + aim * ((across ? 16f : 10f) * s / tpu);
            _pointer.Show(from, aim, _enemy.DasherTellProgress01, _sr);
        }

        void FaceAlong(Vector2 aim)
        {
            if (Mathf.Abs(aim.x) > 0.15f) FaceLeft(aim.x < 0f);
            else Face();
        }

        /// <summary>A trail for each dash: begun as one starts, laid as it runs, ended with it.</summary>
        void TickTrail(EnemyController.DasherPhase phase)
        {
            // at the feet: the art's soles sit about four texels under its pivot
            var feet = (Vector2)_enemy.transform.position + new Vector2(0f, -4f * Scale / TexelRig.TexelsPerUnit);
            bool dashing = _enemy.DasherDashing && !_enemy.Flinched;
            if (dashing && (_trail == null || phase != _lastPhase))
            {
                if (_trail != null) _trail.End();
                _trail = DasherTrail.Begin(_enemy.transform.parent, feet, ++_trails * 17 + Mathf.RoundToInt(_enemy.transform.position.x * 31f));
            }
            if (_trail != null)
            {
                if (dashing) _trail.Extend(feet);
                else { _trail.End(); _trail = null; }
            }
            _lastPhase = phase;
        }

        void OnDisable()
        {
            if (_pointer != null) _pointer.Hide();
            if (_trail != null) { _trail.End(); _trail = null; }
        }
    }
}
