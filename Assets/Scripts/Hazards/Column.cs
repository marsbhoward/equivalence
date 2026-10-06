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
            if (art != null && art.HasArt)
            {
                Color tint = cracked ? CrackedBase : PermanentBase;
                ArtBinder.AttachVisual(go, art, Spr.Circle, tint, diameter, 5);
            }
            else
            {
                BuildStoneVisual(go, diameter, cracked);
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
                column._health = health;
            }
            // Non-cracked: no Health at all, so nothing anywhere can ever apply damage to it -
            // this is what makes "a field between two non-cracked columns can't be destroyed"
            // free rather than a separate rule ForceField has to enforce itself.

            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            DepthSorted.Attach(go, 0f, true, renderers);   // Fixed=true: a column never moves

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
            // Enemies route round it until told otherwise - the nav grid ignores a Broken column
            // from this frame, before Destroy has actually removed the collider.
            Enemies.NavField.MarkDirty();
            Spr.Flash(transform.position, RadiusFor(Size) * 1.4f,
                      new Color(0.75f, 0.7f, 0.62f), 0.3f);
            Destroy(gameObject);
        }

        // ---- procedural stone visual ----
        //
        // Composed from existing Spr primitives layered and tinted, the same technique every
        // other placeholder in this project uses (Forge, the sigil door) rather than one baked
        // multi-tone texture - Spr sprites are white-plus-alpha by convention, tinted per layer.

        static readonly Color PermanentBase = new(0.42f, 0.44f, 0.52f);
        static readonly Color CrackedBase = new(0.66f, 0.52f, 0.4f);
        static readonly Color CrackDark = new(0.05f, 0.04f, 0.05f);

        static void BuildStoneVisual(GameObject go, float diameter, bool cracked)
        {
            var baseTint = cracked ? CrackedBase : PermanentBase;

            // The lit top of the drum, a shadowed seam most of the way in (reads as a stacked
            // stone drum rather than a flat disc), and a bright hairline bevel near the true edge.
            Layer(go, Spr.Circle, baseTint, diameter, 5);
            Layer(go, Spr.Ring, Darken(baseTint, 0.55f), diameter * 0.72f, 6, alpha: 0.6f);
            Layer(go, Spr.ThinRing, Lighten(baseTint, 0.5f), diameter * 0.95f, 7, alpha: 0.85f);

            if (!cracked) return;

            // Cracked has to read as CRUMBLING, not just a different tint: fissures cutting
            // across the face, a couple of bites out of the rim, and a few chips fallen just
            // outside the footprint. Randomised per spawn so a floor's cracked columns don't all
            // wear identical damage.
            int crackCount = Random.Range(2, 4);
            for (int i = 0; i < crackCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                float len = diameter * Random.Range(0.55f, 0.85f);
                float startOffset = diameter * Random.Range(0.05f, 0.15f);
                var rad = angle * Mathf.Deg2Rad;

                var crack = Layer(go, Spr.Capsule, CrackDark, new Vector2(diameter * 0.05f, len), 8, alpha: 0.6f);
                crack.transform.localPosition = new Vector3(startOffset * Mathf.Cos(rad), startOffset * Mathf.Sin(rad), 0f);
                crack.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            int biteCount = Random.Range(1, 3);
            for (int i = 0; i < biteCount; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = diameter * 0.5f;
                var bite = Layer(go, Spr.Circle, CrackDark, Vector2.one * (diameter * Random.Range(0.22f, 0.32f)), 9, alpha: 0.8f);
                bite.transform.localPosition = new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), 0f);
            }

            int rubbleCount = Random.Range(2, 4);
            var rubbleTint = Darken(baseTint, 0.7f);
            for (int i = 0; i < rubbleCount; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = diameter * Random.Range(0.55f, 0.75f);
                var rubble = Layer(go, Spr.Circle, rubbleTint, Vector2.one * (diameter * Random.Range(0.08f, 0.14f)), 4);
                rubble.transform.localPosition = new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), 0f);
            }
        }

        static GameObject Layer(GameObject parent, Sprite sprite, Color color, float uniformSize,
                                int sortingOrder, float? alpha = null)
            => Layer(parent, sprite, color, Vector2.one * uniformSize, sortingOrder, alpha);

        static GameObject Layer(GameObject parent, Sprite sprite, Color color, Vector2 size,
                                int sortingOrder, float? alpha = null)
        {
            var go = new GameObject("layer");
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            var c = color;
            if (alpha.HasValue) c.a = alpha.Value;
            sr.color = c;
            sr.sortingOrder = sortingOrder;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        static Color Darken(Color c, float t) => Color.Lerp(c, Color.black, t);
        static Color Lighten(Color c, float t) => Color.Lerp(c, Color.white, t);
    }
}
