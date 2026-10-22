using UnityEngine;
using Convergence.Art;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    public enum ColumnSize { Small, Medium, Large }

    /// <summary>
    /// A room-layout pillar. Physically solid (blocks movement, the same way the arena's own
    /// walls do - just a Collider2D with no Rigidbody2D, so 2D physics treats it as static) and
    /// blocks line of sight for EVERYONE, player included - breaking a cracked one is as much
    /// about unblocking your own shot as it is about stopping a Turret's beam.
    ///
    /// Deliberately never registered with EnemyRegistry. That is the entire reason "only melee and
    /// unfocused attacks can break a cracked column, ranged auto-target can't touch one, and discs
    /// don't ricochet to one" falls out for free elsewhere in the combat code - see the Hazards
    /// plan notes. This class only has to get its OWN Health/Collider right.
    /// </summary>
    public class Column : MonoBehaviour
    {
        public ColumnSize Size { get; private set; }
        public bool Cracked { get; private set; }

        public static float RadiusFor(ColumnSize size) => size switch
        {
            ColumnSize.Small => Tuning.Hazards.ColumnRadiusSmall,
            ColumnSize.Medium => Tuning.Hazards.ColumnRadiusMedium,
            _ => Tuning.Hazards.ColumnRadiusLarge,
        };

        static float HpFor(ColumnSize size) => size switch
        {
            ColumnSize.Small => Tuning.Hazards.ColumnHpSmall,
            ColumnSize.Medium => Tuning.Hazards.ColumnHpMedium,
            _ => Tuning.Hazards.ColumnHpLarge,
        };

        public float Radius => RadiusFor(Size);

        /// <summary><paramref name="hp"/> overrides a cracked column's size-based health - Medusa's
        /// ring (Tuning.Medusa.PillarHp) is cover she breaks, and should not crumble to a stray swing.</summary>
        public static Column Spawn(Vector2 pos, ColumnSize size, bool cracked, Transform parent, float hp = 0f)
        {
            var go = new GameObject(cracked ? $"column.{size}.cracked" : $"column.{size}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var column = go.AddComponent<Column>();
            column.Size = size;
            column.Cracked = cracked;

            float diameter = RadiusFor(size) * 2f;
            var art = size switch
            {
                ColumnSize.Small => GameArt.I.ColumnSmall,
                ColumnSize.Medium => GameArt.I.ColumnMedium,
                _ => GameArt.I.ColumnLarge,
            };
            bool authored = art != null && art.HasArt;
            if (authored)
            {
                Color tint = cracked ? CrackedBase : PermanentBase;
                ArtBinder.AttachVisual(go, art, Spr.Circle, tint, diameter, 5);
            }
            else
            {
                column.BuildStanding(pos, size, cracked);
            }

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = diameter * 0.5f;
            // No Rigidbody2D, same as MakeWall's own boundary colliders - 2D physics treats a
            // bare collider as static, which is exactly what a pillar that never moves needs.

            if (cracked)
            {
                var health = go.AddComponent<Health>();
                health.Configure(hp > 0f ? hp : HpFor(size));
                health.Immovable = true;
                health.Died += column.OnBroken;
                health.Damaged += info => { if (!info.Tick && column != null) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, column.transform.position); };
                column._health = health;
            }
            // Non-cracked: no Health at all, so nothing anywhere can ever apply damage to it -
            // this is what makes "a field between two non-cracked columns can't be destroyed"
            // free rather than a separate rule ForceField has to enforce itself.

            // Fixed=true: a column never moves. The ground shadow is not depth sorted - it stays
            // on the decal layer, under every body that walks past.
            var renderers = authored ? go.GetComponentsInChildren<SpriteRenderer>(true) : new[] { column._art };
            DepthSorted.Attach(go, 0f, true, renderers);

            return column;
        }

        Health _health;
        bool _broken;

        /// <summary>True from the frame it breaks - Destroy is deferred, so a reference can
        /// outlive the pillar by a frame.</summary>
        public bool Broken => _broken;

        void OnBroken(Health h) => Break();

        /// <summary>A whole floor torn down goes this way rather than through Break.</summary>
        void OnDestroy() => Enemies.NavField.MarkDirty();

        /// <summary>Shatter it now, whatever its health - Medusa's own break, and the end of every
        /// other. Works on a column with no Health too, so a boss's verdict needs no damage path.</summary>
        public void Break()
        {
            if (_broken) return;
            _broken = true;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, transform.position);
            // Enemies route round it until told otherwise - the nav grid ignores a Broken column
            // from this frame, before Destroy has actually removed the collider.
            Enemies.NavField.MarkDirty();
            Spr.Flash(transform.position + Vector3.up * (_height * 0.5f), RadiusFor(Size) * 1.4f,
                      new Color(0.75f, 0.7f, 0.62f), 0.3f);
            Destroy(gameObject);
        }

        // ---- the standing picture (ColumnArt) ----

        /// <summary>
        /// The floor's look seed (FloorPlanner.SceneSeed, set in GameBootstrap.DressArena): each
        /// column's style and damage are hashed from it and the column's position, so a restart
        /// meets the same columns and drawing one takes nothing from UnityEngine.Random - the
        /// rolls that lay out the rest of the room are untouched by how a column looks.
        /// </summary>
        public static int LookSeed;

        static readonly Color PermanentBase = new(0.42f, 0.44f, 0.52f);
        static readonly Color CrackedBase = new(0.66f, 0.52f, 0.4f);

        [SerializeField] SpriteRenderer _art;
        [SerializeField] ColumnStyle _style;
        [SerializeField] float _height, _shaftHalf;
        float _alpha = 1f;

        public ColumnStyle Style => _style;

        /// <summary>How tall the column is DRAWN, in world units (0 for authored art, which says
        /// nothing about its height) - a force field's curtain stands no taller than its anchors.</summary>
        public float DrawnHeight => _height;

        void BuildStanding(Vector2 pos, ColumnSize size, bool cracked)
        {
            uint h = Hash(LookSeed, Mathf.RoundToInt(pos.x * 100f), Mathf.RoundToInt(pos.y * 100f), (int)size);
            _style = (h & 0xffff) / 65536f < Tuning.Hazards.ColumnTallChance ? ColumnStyle.Tall : ColumnStyle.Broken;
            _height = ColumnArt.HeightOf(_style, size, cracked);
            _shaftHalf = ColumnArt.ShaftHalfWidth(_style, RadiusFor(size));

            // "visual" FIRST: Health flashes body sprites under a child of that name and tints the
            // first renderer it finds - which must be the column, not its shadow.
            var visual = new GameObject("visual");
            visual.transform.SetParent(transform, false);
            _art = visual.AddComponent<SpriteRenderer>();
            _art.sprite = ColumnArt.For(_style, size, cracked, (int)(h >> 16));
            _art.sortingOrder = 5;

            // The contact shadow, as under a wall block: what sets it ON the ground.
            float r = RadiusFor(size);
            var shadow = new GameObject("shadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.localScale = new Vector3(r * 2.16f, r * 2.24f * ColumnArt.Squash, 1f);
            var ss = shadow.AddComponent<SpriteRenderer>();
            ss.sprite = Spr.Circle;
            ss.color = new Color(0f, 0f, 0f, 0.32f);
            ss.sortingOrder = SortingOrders.GroundDecal;
        }

        static uint Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                uint x = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du ^ (uint)d * 0x27D4EB2Fu;
                x ^= x >> 15; x *= 0x2C1B3C6Du; x ^= x >> 12; x *= 0x297A2D39u; x ^= x >> 15;
                return x;
            }
        }

        // ---- seeing the player through it ----
        //
        // A column now stands up off the floor, so it covers whatever is behind it. While the
        // player's body is behind the drawn shaft it goes see-through (Tuning.Hazards.ColumnBehind*),
        // the usual answer in a 3/4 view; enemies are not looked for.

        static Player.PlayerController _player;
        static DepthSorted _playerDepth;
        static float _lookAgainAt;
        static int _bodyFrame = -1;
        static Bounds _body;
        static bool _hasBody;

        static bool PlayerBody(out Bounds body, out float sortY)
        {
            body = default; sortY = 0f;
            if (_player == null && Time.unscaledTime >= _lookAgainAt)
            {
                _lookAgainAt = Time.unscaledTime + 1f;
                _player = FindAnyObjectByType<Player.PlayerController>();
                _playerDepth = _player != null ? _player.GetComponent<DepthSorted>() : null;
            }
            if (_player == null) return false;

            if (_bodyFrame != Time.frameCount)
            {
                // Once a frame for every column: the union of the body's drawn sprites.
                _bodyFrame = Time.frameCount;
                _hasBody = false;
                foreach (var sr in _player.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!sr.enabled || sr.sprite == null) continue;
                    if (_hasBody) _body.Encapsulate(sr.bounds); else { _body = sr.bounds; _hasBody = true; }
                }
            }
            body = _body;
            var t = _player.transform.position;
            sortY = t.y + (_playerDepth != null ? _playerDepth.FootOffset - _playerDepth.Bias : 0f);
            return _hasBody;
        }

        void LateUpdate()
        {
            if (_art == null) return;
            bool behind = false;
            if (PlayerBody(out var body, out float sortY) && sortY > transform.position.y)
            {
                float x = transform.position.x, top = _art.bounds.max.y;
                behind = body.max.x > x - _shaftHalf && body.min.x < x + _shaftHalf && body.min.y < top
                         && body.max.y > transform.position.y;
            }
            float target = behind ? Tuning.Hazards.ColumnBehindAlpha : 1f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime / Tuning.Hazards.ColumnFadeSeconds);
            // Written whenever it differs: a hit flash lerps the colour back to opaque.
            var c = _art.color;
            if (c.a != _alpha) { c.a = _alpha; _art.color = c; }
        }
    }
}
