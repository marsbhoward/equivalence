using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// The shared half of every bestiary driver (ChaserVisual, RangedVisual, ...): one renderer
    /// shown at scale 1 (the art is at the arena's texel density), frames re-fetched by look and
    /// tier whenever a reload empties the array, a walk paced by distance covered, and facing
    /// toward the target with a dead zone. A kind's driver only decides WHICH frame, each
    /// LateUpdate, from its own EnemyController.
    ///
    /// ActorVisual's travel-direction flip is switched off for these bodies - a creature backing
    /// away or circling a column still looks at you.
    /// </summary>
    public abstract class TexelVisual : MonoBehaviour
    {
        [SerializeField] protected EnemyController _enemy;
        [SerializeField] protected SpriteRenderer _sr;
        [SerializeField] protected EnemyLook _look;
        [SerializeField] protected bool _elite;
        [SerializeField] protected bool _faceLeft;
        [SerializeField] float _stride;

        Sprite[] _frames;
        Vector2 _lastPos;
        bool _hasLast;
        int _shown = -1;

        protected static T Attach<T>(GameObject visual, EnemyController enemy, EnemyLook look, bool elite, int first)
            where T : TexelVisual
        {
            var sr = visual.GetComponent<SpriteRenderer>();
            if (sr == null) return null;

            var actor = visual.GetComponent<Art.ActorVisual>();
            if (actor != null) actor.FlipToFacing = false;

            var v = visual.AddComponent<T>();
            v._enemy = enemy;
            v._sr = sr;
            v._look = look;
            v._elite = elite;
            sr.color = Color.white;                        // the art bakes its own colour
            visual.transform.localScale = Vector3.one;     // and its own size
            v.Show(first);
            return v;
        }

        /// <summary>Every frame of this look and tier, in the kind's frame order.</summary>
        protected abstract Sprite[] Build(EnemyLook look, bool elite);

        Sprite[] Frames => _frames != null && _frames.Length > 0 && _frames[0] != null
            ? _frames
            : _frames = Build(_look, _elite);

        /// <summary>
        /// Advances the walk by the distance moved since the last call and returns which of
        /// <paramref name="frames"/> frames it is on, or -1 once it has stopped. Call it every
        /// frame the body walks; <see cref="Rest"/> on frames it does not, so a lunge or a shove
        /// is never counted as steps.
        /// </summary>
        protected int Stride(float unitsPerFrame, int frames)
        {
            Vector2 pos = _enemy.transform.position;
            float moved = _hasLast ? Vector2.Distance(pos, _lastPos) : 0f;
            _lastPos = pos;
            _hasLast = true;
            if (moved < 0.0005f) return -1;
            _stride = (_stride + moved / unitsPerFrame) % frames;
            return Mathf.FloorToInt(_stride);
        }

        protected void Rest() => _hasLast = false;

        /// <summary>Turns toward the target, with a dead zone so a target straight above or below
        /// does not flip it every frame.</summary>
        protected void Face()
        {
            var target = _enemy.Target;
            if (target == null) return;
            float dx = target.position.x - _enemy.transform.position.x;
            if (Mathf.Abs(dx) > 0.15f) _faceLeft = dx < 0f;
        }

        /// <summary>Holds a facing the controller decided (an aimed shot's side).</summary>
        protected void FaceLeft(bool left) => _faceLeft = left;

        protected void Show(int i)
        {
            var frames = Frames;
            if (i < 0 || i >= frames.Length) return;
            _sr.flipX = _faceLeft;
            if (i == _shown && _sr.sprite == frames[i]) return;
            _shown = i;
            _sr.sprite = frames[i];
        }
    }
}
