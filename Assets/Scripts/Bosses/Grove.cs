using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// Viriditas's grove: fixed SPOTS in Glaciron's layout (the user's screenshots) - diagonal
    /// columns, bottom left to top right, interlocking, Tuning.Viriditas.GroveColumns long - each
    /// empty, a SAPLING or a TREE. Its stomps grow saplings into trees; its roll breaks either.
    /// The player cannot break anything here (the user's call): nothing in it has a Health.
    ///
    /// A TREE is solid (a static collider, like a hazard column) and casts the only shadow from the
    /// Bloom; a sapling is neither. <see cref="Shadowed"/> is THE AUTHORITY, as PillarRing's is for
    /// Medusa: the pollen is painted from the same call the Bloom tests.
    /// </summary>
    public class Grove : MonoBehaviour
    {
        public enum State { Empty, Sapling, Tree }

        // Non-readonly and null-guarded - see ENGINEERING.md, domain reload traps.
        [SerializeField] State[] _state;
        [SerializeField] Vector2[] _pos;
        [SerializeField] GroveSprout[] _art;
        [SerializeField] CircleCollider2D[] _solid;

        public int Count => _state != null ? _state.Length : 0;

        public static Grove Build(Transform parent)
        {
            var go = new GameObject("viriditas.grove");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<Grove>();
            var cols = Tuning.Viriditas.GroveColumns;
            int n = 0;
            foreach (int c in cols) n += c;
            g._state = new State[n]; g._pos = new Vector2[n]; g._art = new GroveSprout[n]; g._solid = new CircleCollider2D[n];

            float s = Tuning.Viriditas.GroveSpacing;
            int i = 0;
            for (int ci = 0; ci < cols.Length; ci++)
            {
                float off = (ci - (cols.Length - 1) * 0.5f) * s * 0.87f;
                for (int k = 0; k < cols[ci]; k++)
                {
                    float a = (k - (cols[ci] - 1) * 0.5f) * s;
                    // Along the column (1, 1), across it (1, -1), then stretched to the room.
                    var p = new Vector2(0.7071f * (a + off) * Tuning.Viriditas.GroveStretchX,
                                        0.7071f * (a - off) * Tuning.Viriditas.GroveStretchY);
                    g._pos[i] = p;
                    var spot = new GameObject("spot." + i);
                    spot.transform.SetParent(go.transform, false);
                    spot.transform.position = p;
                    var col = spot.AddComponent<CircleCollider2D>();
                    col.radius = Tuning.Viriditas.TreeRadius;
                    col.enabled = false;
                    g._solid[i] = col;
                    g._art[i] = GroveSprout.On(spot.transform, i);
                    g.Set(i, State.Sapling, instant: true);
                    i++;
                }
            }
            return g;
        }

        public Vector2 Position(int i) => _pos != null && i >= 0 && i < _pos.Length ? _pos[i] : Vector2.zero;
        public State At(int i) => _state != null && i >= 0 && i < _state.Length ? _state[i] : State.Empty;
        public bool Standing(int i) => At(i) != State.Empty;

        public int StandingCount
        {
            get { int n = 0; for (int i = 0; i < Count; i++) if (Standing(i)) n++; return n; }
        }

        /// <summary>One bit per tree - the pollen's shadow cache is rebuilt when it changes.</summary>
        public int TreeMask
        {
            get { int m = 0; for (int i = 0; i < Count; i++) if (At(i) == State.Tree) m |= 1 << i; return m; }
        }

        void Set(int i, State s, bool instant = false)
        {
            if (_state == null || i < 0 || i >= _state.Length) return;
            _state[i] = s;
            if (_solid[i] != null) _solid[i].enabled = s == State.Tree;
            if (_art[i] != null) _art[i].Show(s, instant);
        }

        /// <summary>A sapling inside the stomp grows into a tree.</summary>
        public bool Grow(int i)
        {
            if (At(i) != State.Sapling) return false;
            Set(i, State.Tree);
            return true;
        }

        /// <summary>The roll breaks it. Returns what it was.</summary>
        public State Break(int i)
        {
            var was = At(i);
            if (was == State.Empty) return was;
            Set(i, State.Empty);
            var at = Position(i);
            TexelFx.Rubble(at, was == State.Tree ? GroveCanvas.BarkChips : GroveCanvas.SproutChips, was == State.Tree ? 0.9f : 0.3f, was == State.Tree ? 12 : 6, 0.9f);
            TexelFx.Puff(at, GroveCanvas.Leaf, was == State.Tree ? 16 : 7, 9f, 0.6f);
            return was;
        }

        /// <summary>A sapling back on spot <paramref name="i"/>, if it is empty.</summary>
        public bool Regrow(int i)
        {
            if (At(i) != State.Empty) return false;
            Set(i, State.Sapling);
            return true;
        }

        /// <summary>A push away from every tree the walker at <paramref name="at"/> would bump into -
        /// it walks round its trees rather than through them.</summary>
        public Vector2 Avoid(Vector2 at, float radius)
        {
            var push = Vector2.zero;
            float reach = radius + Tuning.Viriditas.TreeRadius + 0.35f;
            for (int i = 0; i < Count; i++)
            {
                if (At(i) != State.Tree) continue;
                var d = at - Position(i);
                float m = d.magnitude;
                if (m < reach && m > 1e-4f) push += d / m * (1f - m / reach);
            }
            return push;
        }

        /// <summary>The standing spot the ball at <paramref name="centre"/> touches, or -1.</summary>
        public int Touching(Vector2 centre, float radius)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!Standing(i)) continue;
                float r = radius + (At(i) == State.Tree ? Tuning.Viriditas.TreeRadius : Tuning.Viriditas.SaplingRadius);
                if ((Position(i) - centre).sqrMagnitude <= r * r) return i;
            }
            return -1;
        }

        /// <summary>Is <paramref name="point"/> hidden from the lotus at <paramref name="eye"/> - does
        /// the straight line between them pass through a TREE? Geometry, not a raycast (PillarRing's
        /// reasons). Saplings hide nothing.</summary>
        public bool Shadowed(Vector2 eye, Vector2 point)
        {
            float r = Tuning.Viriditas.TreeRadius;
            for (int i = 0; i < Count; i++)
            {
                if (At(i) != State.Tree) continue;
                var c = Position(i);
                var ab = point - eye;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(c - eye, ab) / len2) : 0f;
                if (((eye + ab * t) - c).sqrMagnitude <= r * r) return true;
            }
            return false;
        }
    }
}
