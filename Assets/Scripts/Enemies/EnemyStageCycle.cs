using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Which of a kind's four baked stages is showing. Generic across every kind that uses
    /// EnemyStageCycle (Bomb arming, Turret charging, ...): the SHAPE is the same 0..1 progress
    /// signal bucketed the same way regardless of what is driving it, only the art and the
    /// semantics of "what Mid and Full mean" differ per kind - see each kind's own *Art class.
    /// </summary>
    public enum EnemyStage { Idle, Mid, Full, Cooling }

    /// <summary>
    /// Swaps a spawned enemy's body sprite between its four baked EnemyStage frames as its OWN
    /// EnemyController arms/charges and, if denied, recovers - so the creature's body agrees with
    /// whatever ground-level telegraph cue already exists instead of that cue carrying the whole
    /// read alone.
    ///
    /// RE-NORMALISES THE VISUAL'S SCALE ON EVERY SWAP, INCLUDING THE FIRST ONE, and this is not
    /// optional. GameArt's own catalog entries for a procedurally-built kind are never actually
    /// pointed at real art - ArtBinder.AttachVisual therefore always takes its FALLBACK branch for
    /// Bomb/Turret today, sizing the visual against Spr.Circle's own 1x1 native size. This
    /// component then overwrites that renderer's SPRITE with real, differently-proportioned
    /// PixelSprite art, but nothing was correcting the SCALE that art was still wearing - measured
    /// live and caught exactly this way: a "0.72-unit" bomb rendering at 0.46. Recomputing scale
    /// from each sprite's own bounds against the target world height, every Apply, fixes both the
    /// initial mismatch and any future stage whose canvas size happens to differ.
    ///
    /// FIRST WRITTEN WITH TWO Func&lt;&gt; DELEGATE FIELDS INSTEAD OF _enemy/_kind BELOW, and it
    /// broke exactly the way this project's own documented reload traps say it would: a live
    /// recompile mid-session wiped both delegates to null on every already-spawned enemy while
    /// _sr/_stages (Sprite/SpriteRenderer - real Unity Object references) survived untouched,
    /// and Update's existing null guard covered the survivors but not the delegates it had no
    /// reason to expect could vanish - caught as an NRE spamming every frame the moment a script
    /// edit landed during play. A delegate is exactly as unserializable as the interface and
    /// Dictionary fields this project's own rule already names; storing a `[SerializeField]`
    /// MonoBehaviour reference and an enum instead - both ordinary serializable data - and
    /// branching on the enum inline every frame sidesteps the trap entirely rather than needing a
    /// re-hook step to recover from it.
    /// </summary>
    public class EnemyStageCycle : MonoBehaviour
    {
        [SerializeField] EnemyController _enemy;
        EnemyKind _kind;
        SpriteRenderer _sr;
        Sprite[] _stages;
        float _worldHeight;
        int _current = -1;

        /// <param name="worldHeight">
        /// The FULLY RESOLVED target size (already carrying any elite multiplier - see
        /// EnemyFactory's own `size` local) - this is what stands in for ArtBinder's WorldHeight
        /// for a kind whose real art never goes through the GameArt catalog at all.
        /// </param>
        public static EnemyStageCycle Attach(GameObject visual, Sprite[] stages, float worldHeight,
                                             EnemyController enemy, EnemyKind kind)
        {
            var sr = visual.GetComponent<SpriteRenderer>();
            if (sr == null) return null; // a Prefab-based look has no single renderer to drive

            var cycle = visual.AddComponent<EnemyStageCycle>();
            cycle._enemy = enemy;
            cycle._kind = kind;
            cycle._sr = sr;
            cycle._stages = stages;
            cycle._worldHeight = worldHeight;
            cycle.Apply((int)EnemyStage.Idle);
            return cycle;
        }

        void Update()
        {
            if (_sr == null || _stages == null || _enemy == null) return;

            int wanted;
            if (_kind == EnemyKind.Dasher)
            {
                // Dasher's phases are discrete states, not a 0..1 ramp - Telegraph is the coil,
                // Rushing/Combo are one continuous strike (the charge THROUGH to the hits that
                // follow it, not two separate movements), Evading is the retreat.
                wanted = _enemy.CurrentDasherPhase switch
                {
                    EnemyController.DasherPhase.Telegraph => (int)EnemyStage.Mid,
                    EnemyController.DasherPhase.Rushing or EnemyController.DasherPhase.Combo => (int)EnemyStage.Full,
                    EnemyController.DasherPhase.Evading => (int)EnemyStage.Cooling,
                    _ => (int)EnemyStage.Idle,
                };
            }
            else if (_enemy.Recovering)
            {
                wanted = (int)EnemyStage.Cooling;
            }
            else
            {
                // Which signal actually drives the attack differs per kind - Bomb never touches
                // _turretCharge and Turret never touches _telegraphTimer (see
                // EnemyController.TurretChargeProgress01's own remarks) - so this has to ask by
                // kind rather than read one shared getter.
                float t = _kind == EnemyKind.Turret ? _enemy.TurretChargeProgress01 : _enemy.TelegraphProgress01;
                wanted = t <= 0f ? (int)EnemyStage.Idle
                       : t < Tuning.Enemy.StageFullThreshold ? (int)EnemyStage.Mid
                       : (int)EnemyStage.Full;
            }

            Apply(wanted);
        }

        void Apply(int stage)
        {
            if (stage == _current || stage < 0 || stage >= _stages.Length) return;
            _current = stage;
            var sprite = _stages[stage];
            _sr.sprite = sprite;

            // Mirrors ArtBinder.NormaliseHeight's own formula exactly - see the class remarks for
            // why this can't be skipped just because most stages of one look happen to share a
            // canvas size.
            float native = sprite != null ? sprite.bounds.size.y : 0f;
            if (native > 0.0001f)
                _sr.transform.localScale = Vector3.one * (_worldHeight / native);
        }
    }
}
