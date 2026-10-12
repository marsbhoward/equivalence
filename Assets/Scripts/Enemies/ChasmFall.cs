using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// How a body goes into a chasm: down a DROP, or under a LAKE (Hazards.RoomShape.Lake).
    /// Pictures only - the fall's rules are EnemyController's, and the kill lands when the
    /// animation ends.
    ///
    /// BOTH ARE CUT ALONG A LINE: a body only shrinking and fading reads as a body vanishing on
    /// the spot, not as going anywhere. The cut says where it went.
    ///
    ///   DROP  the line is the NEAR lip under it (Arena.ChasmLipBelow): as the body falls - DOWN
    ///         the screen, which is down in this 3/4 view - the lip swallows it from the feet up,
    ///         in front of the hole's far wall. It carries on along the throw, tumbles the way it
    ///         was thrown, recedes and goes dark under gravity's ease, and grit from the lip
    ///         falls with it.
    ///   LAKE  the line is the WATERLINE at its feet, so the body goes UNDER rather than fading.
    ///         It plunges with a splash, bobs back up once, settles sinking with a wobble while a
    ///         ring holds at the waterline round it, and bubbles break the surface once it has
    ///         gone.
    ///
    /// THE CUT IS MADE ON THE CPU (<see cref="RowClip"/>), whole texel rows at a time, not with a
    /// SpriteMask. Measured in this Unity (6000.5, URP 2D): when more than one TEXTURE is drawn
    /// masked in a frame, the masked sprites after the first are drawn with the first one's
    /// texture - the body came out as a solid quad of the grit's white square, then as a circle
    /// when a masked test circle drew before it. Distinct sorting orders did not help; a material
    /// instance drew nothing. Two bodies going into one hole at once is the ordinary case, so
    /// masks were out.
    ///
    /// The body's depth sort and stage cycle are stopped first so nothing re-sorts or re-skins
    /// it mid-fall. Readouts riding on it - armour ring, telegraphs, status overlays - are hidden
    /// at once: they are about a fight it has left.
    /// </summary>
    static class ChasmFall
    {
        /// <summary>What a sinking body's colours are multiplied toward: wet, and under blue water.</summary>
        static readonly Color Wet = new(0.62f, 0.78f, 0.88f, 1f);

        /// <summary>The body's own pictures: everything under its "visual" child (and a turret's
        /// head). Every other renderer is a readout and is switched off.</summary>
        static List<RowClip> TakeBody(Transform root, out Bounds bounds)
        {
            var depth = root.GetComponent<DepthSorted>();
            if (depth != null) depth.enabled = false;
            foreach (var cycle in root.GetComponentsInChildren<EnemyStageCycle>()) cycle.enabled = false;

            var parts = new List<RowClip>();
            bounds = new Bounds(root.position, Vector3.zero);
            bool any = false;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!IsBody(sr.transform, root))
                {
                    sr.enabled = false;
                    continue;
                }
                parts.Add(new RowClip(sr));
                if (sr.sprite == null) continue;
                if (!any) { bounds = sr.bounds; any = true; }
                else bounds.Encapsulate(sr.bounds);
            }
            return parts;
        }

        static bool IsBody(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name == "visual" || t.name == "turret.head") return true;
            return false;
        }

        static void Finish(List<RowClip> parts)
        {
            foreach (var p in parts) { p.Hide(); p.Release(); }
        }

        // ------------------------------------------------------------------ the drop

        public static IEnumerator Drop(Transform body, Vector2 thrown)
        {
            var parts = TakeBody(body, out var bounds);
            var start = body.position;
            float lip = Arena.ChasmLipBelow(start);

            var holder = new GameObject("chasm.grit").transform;
            int order = int.MaxValue;
            foreach (var p in parts) order = Mathf.Min(order, p.Renderer.sortingOrder);
            var grit = Grit(holder, new Vector2(bounds.center.x, bounds.min.y), thrown, order - 1);

            var scale = body.localScale;
            var along = thrown.sqrMagnitude > 1e-4f ? thrown.normalized : Vector2.zero;
            // It tumbles the way it was going: thrown right, it rolls clockwise.
            float spin = (along.x != 0f ? -Mathf.Sign(along.x) : (Random.value < 0.5f ? -1f : 1f))
                       * Tuning.Chasm.DropSpinDegrees;
            float seconds = Tuning.Chasm.DropSeconds;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float g = k * k;   // gravity: slow off the lip, fast by the end
                body.position = start + (Vector3)(along * (Tuning.Chasm.DropCarry * k))
                              + Vector3.down * (Tuning.Chasm.DropDepth * g);
                body.localScale = scale * Mathf.Lerp(1f, Tuning.Chasm.DropShrink, k);
                body.rotation = Quaternion.Euler(0f, 0f, spin * g);
                float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.95f, k));
                foreach (var p in parts)
                {
                    p.Tint(Color.Lerp(p.Colour, new Color(0f, 0f, 0f, p.Colour.a), dark));
                    p.Cut(lip);
                }
                TickGrit(grit, Time.deltaTime, k, lip);
                yield return null;
            }

            Finish(parts);
            Object.Destroy(holder.gameObject);
        }

        struct Speck { public Transform T; public SpriteRenderer Sr; public Vector2 V; public Color C; }

        /// <summary>A few crumbs of the lip going down with it.</summary>
        static List<Speck> Grit(Transform holder, Vector2 at, Vector2 thrown, int order)
        {
            var list = new List<Speck>();
            var stone = new Color(0.45f, 0.38f, 0.30f);
            var away = thrown.sqrMagnitude > 1e-4f ? thrown.normalized : Vector2.zero;
            for (int i = 0; i < Tuning.Chasm.DropGrit; i++)
            {
                var go = new GameObject("grit");
                go.transform.SetParent(holder, false);
                go.transform.position = at + new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(-0.05f, 0.08f));
                float s = Random.Range(0.035f, 0.07f);
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr.Square;
                var c = Color.Lerp(stone, stone * 0.6f, Random.value);
                c.a = 1f;
                sr.color = c;
                sr.sortingOrder = order;
                var v = away * Random.Range(0.2f, 0.8f) + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.1f, 0.6f));
                list.Add(new Speck { T = go.transform, Sr = sr, V = v, C = c });
            }
            return list;
        }

        /// <summary>Falls, darkens, and is gone the moment it passes the near lip.</summary>
        static void TickGrit(List<Speck> grit, float dt, float k, float lip)
        {
            for (int i = 0; i < grit.Count; i++)
            {
                var g = grit[i];
                if (g.T == null || !g.Sr.enabled) continue;
                g.V += Vector2.down * (Tuning.Chasm.DropGritGravity * dt);
                g.T.position += (Vector3)(g.V * dt);
                g.Sr.color = Color.Lerp(g.C, new Color(0f, 0f, 0f, 1f), k);
                if (g.T.position.y < lip) g.Sr.enabled = false;
                grit[i] = g;
            }
        }

        // ------------------------------------------------------------------ the lake

        public static IEnumerator Sink(Transform body)
        {
            var parts = TakeBody(body, out var bounds);

            // The waterline: what sinks below it is gone.
            float water = bounds.min.y + Tuning.Chasm.SinkWaterline;
            float height = Mathf.Max(bounds.size.y, 0.4f);

            var start = body.position;
            var feet = new Vector2(bounds.center.x, water);
            Hazards.LakeSurface.Splash(feet);

            // Down far enough that the top of the body clears the waterline.
            float under = height - Tuning.Chasm.SinkWaterline + 0.04f;
            float plunge = Tuning.Chasm.SinkPlunge, bob = Tuning.Chasm.SinkBob;
            float seconds = Tuning.Chasm.SinkSeconds;
            float nextRing = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                // Three beats: in fast with the splash, back up once, then down for good.
                float down;
                if (k < 0.15f) down = plunge * Ease(k / 0.15f);
                else if (k < 0.32f) down = plunge - bob * Mathf.Sin(Mathf.PI * (k - 0.15f) / 0.17f);
                else down = Mathf.Lerp(plunge, under, Ease2((k - 0.32f) / 0.68f));
                body.position = start + Vector3.down * down;
                body.rotation = Quaternion.Euler(0f, 0f, Tuning.Chasm.SinkWobbleDegrees
                                                         * Mathf.Sin(t * 13f) * (1f - k));
                foreach (var p in parts)
                {
                    p.Tint(Color.Lerp(p.Colour, p.Colour * Wet, Mathf.Min(1f, k * 1.6f)));
                    p.Cut(water);
                }

                // A ring held at the waterline round it while any of it is still above.
                nextRing -= Time.deltaTime;
                if (nextRing <= 0f && k > 0.15f)
                {
                    // Spaced so two rings are out at once, not a bullseye.
                    nextRing = 0.26f;
                    Hazards.LakeSurface.Ring(feet, Mathf.Lerp(0.55f, 0.3f, k), 0.4f);
                }
                yield return null;
            }

            Finish(parts);
            Hazards.LakeSurface.Bubbles(feet);
        }

        static float Ease(float k) => 1f - (1f - k) * (1f - k);
        static float Ease2(float k) => k * k * (3f - 2f * k);

        // ------------------------------------------------------------------ the cut

        /// <summary>
        /// One renderer cut along a world-horizontal line: every texel row of its sprite below the
        /// line is dropped, by showing a sprite over the SAME texture with a shorter rect and its
        /// pivot moved to match - so what remains does not move. Whole rows, so the edge sits on
        /// the body's own texel grid. The line is measured at the pivot's column in the renderer's
        /// own axes, so a tilted body is cut across itself; at the tumble's angles that reads as
        /// the lip, not as a slant. A cut sprite is made once per row and destroyed at the end.
        /// </summary>
        sealed class RowClip
        {
            public readonly SpriteRenderer Renderer;
            public readonly Color Colour;
            readonly Sprite _source;
            readonly Dictionary<int, Sprite> _made = new();
            int _rows = -1;

            public RowClip(SpriteRenderer sr)
            {
                Renderer = sr;
                Colour = sr.color;
                _source = sr.sprite;
            }

            public void Tint(Color c) { if (Renderer != null) Renderer.color = c; }

            public void Hide() { if (Renderer != null) Renderer.enabled = false; }

            /// <summary>Show only what is above world height <paramref name="lineY"/>.</summary>
            public void Cut(float lineY)
            {
                if (Renderer == null || _source == null) return;
                var t = Renderer.transform;
                // The line, in the renderer's own space, at its pivot's column.
                var local = t.InverseTransformPoint(new Vector3(t.position.x, lineY, t.position.z));
                var rect = _source.rect;
                int h = Mathf.RoundToInt(rect.height);
                int rows = Mathf.Clamp(Mathf.CeilToInt(_source.pivot.y + local.y * _source.pixelsPerUnit), 0, h);
                if (rows == _rows) return;
                _rows = rows;

                if (rows >= h) { Renderer.enabled = false; return; }
                Renderer.enabled = true;
                if (rows == 0) { Renderer.sprite = _source; return; }
                if (!_made.TryGetValue(rows, out var cut) || cut == null)
                {
                    var r = new Rect(rect.x, rect.y + rows, rect.width, rect.height - rows);
                    var pivot = new Vector2(_source.pivot.x / r.width, (_source.pivot.y - rows) / r.height);
                    cut = Sprite.Create(_source.texture, r, pivot, _source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    cut.hideFlags = HideFlags.HideAndDontSave;
                    _made[rows] = cut;
                }
                Renderer.sprite = cut;
            }

            public void Release()
            {
                foreach (var s in _made.Values) if (s != null) Object.Destroy(s);
                _made.Clear();
            }
        }
    }
}
