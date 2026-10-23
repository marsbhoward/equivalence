using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Tempest Eye (<see cref="TurretArt"/>) off its own EnemyController: the idle on a
    /// timed loop (it never moves), the charge and the charged tell, the two beam frames
    /// flickering while it holds, an elite's overcharge and lob, the spun-down slump after a
    /// flinch.
    ///
    /// THE PUPIL is its own sprite, placed inside the iris toward the target every frame - on a
    /// side-on body it is the swivel the old rotating head was. Snapped to whole texels so it sits
    /// on the body's grid, sorted at the body's own order nudged toward the camera (KindledMarks'
    /// rule), after DepthSorted has written that order.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class TurretVisual : TexelVisual
    {
        const float Nudge = 0.001f;

        [SerializeField] SpriteRenderer _pupil;
        Sprite[] _pupils;

        public static TurretVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
        {
            var v = Attach<TurretVisual>(visual, enemy, look, elite, (int)TurretFrame.Idle0);
            if (v == null) return null;
            var go = new GameObject("turret.pupil");
            go.transform.SetParent(visual.transform, false);
            v._pupil = go.AddComponent<SpriteRenderer>();
            return v;
        }

        protected override Sprite[] Build(EnemyLook look, bool elite) => TurretArt.For(look, elite);
        protected override Color[] Reserved => TurretArt.Reserved;
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => TurretArt.For(look, elite, biome);

        Sprite[] Pupils => _pupils != null && _pupils.Length > 0 && _pupils[0] != null
            ? _pupils
            : _pupils = TurretArt.Pupils(_look, _elite);

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;

            float now = Time.time;
            TurretFrame frame;
            if (_enemy.Recovering) frame = now % 0.7f < 0.35f ? TurretFrame.Spun0 : TurretFrame.Spun1;
            else if (_enemy.TurretLobbing) frame = TurretFrame.Lob;
            else if (_enemy.TurretBeamOn)
                frame = _enemy.TurretOvercharged ? TurretFrame.Overcharge
                      : Mathf.FloorToInt(now / 0.1f) % 2 == 0 ? TurretFrame.Beam0 : TurretFrame.Beam1;
            else
            {
                float t = _enemy.TurretChargeProgress01;
                frame = t > 0f
                    ? (t < Tuning.Enemy.StageFullThreshold ? TurretFrame.Charge : TurretFrame.Charged)
                    : (TurretFrame)((int)TurretFrame.Idle0 + Mathf.FloorToInt(now / 0.15f) % 4);
            }

            FaceLeft(_enemy.TurretFacesLeft);
            Show((int)frame);
            PlacePupil((int)frame);
        }

        void PlacePupil(int frame)
        {
            if (_pupil == null) return;
            var pupils = Pupils;
            _pupil.sprite = pupils[Mathf.Clamp(frame, 0, pupils.Length - 1)];
            _pupil.sortingLayerID = _sr.sortingLayerID;
            _pupil.sortingOrder = _sr.sortingOrder;
            _pupil.enabled = _sr.enabled;

            // in TEXELS from the body's pivot: the iris, plus a step toward the target, rounded
            // to a whole texel (the pupil is an even width, so its centre is a texel boundary)
            float tpu = TexelRig.TexelsPerUnit;
            var iris = TurretArt.IrisOffset(frame, _elite, false) * tpu;
            Vector2 look = Vector2.zero;
            var target = _enemy.Target;
            if (target != null)
            {
                var d = (Vector2)(target.position - _enemy.transform.position);
                if (d.sqrMagnitude > 0.0001f) look = d.normalized;
            }
            var travel = TurretArt.PupilTravel(_elite) * tpu;
            // the drawing is mirrored when it faces left, so the iris is too; the look is in the world
            float x = Mathf.Round((_faceLeft ? -iris.x : iris.x) + look.x * travel.x);
            float y = Mathf.Round(iris.y + look.y * travel.y);
            _pupil.transform.localPosition = new Vector3(x / tpu, y / tpu, -Nudge);
        }
    }
}
