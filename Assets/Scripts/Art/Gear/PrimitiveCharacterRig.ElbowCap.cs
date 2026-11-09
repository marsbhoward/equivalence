using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // THE ELBOW CAP (point 5 of the body review, the user's go 2026-11-09). The arm is cut at the elbow into two
    // square-ended halves that overlap by two texels (CutAtElbow): straight they composite to the whole sprite,
    // but on a sharp bend - the carry, the disc draw's reach over the shoulder - the OUTSIDE of the joint opens
    // into a stepped black bite in the outline. Each bent elbow gets a HALF-ROUND cap on that outside: as wide as
    // the forearm where it meets the elbow, shaded with that row's own colours, under its outline, turned every
    // frame to face away from the bend (the bisector of the two halves), drawn under every layer of that arm.
    //
    // A half, not a whole circle: a full disc also showed its outline in the inner crook of the bend, and the
    // joint read as a dark ring (the verification pass, 2026-11-09). Nearly straight (under DevElbowCapFrom) it
    // is hidden - the halves already meet. Built from whatever covers the elbow - the glove if it reaches it,
    // else the bare arm - cached per forearm sprite.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Testing: no caps, for a before-and-after.</summary>
        public static bool DevPlainElbows;

        const string ElbowCapName = "elbow.cap";
        const float ElbowCapZ = 0.0005f;
        /// <summary>Below this bend, degrees, the cap is hidden.</summary>
        const float ElbowCapFrom = 20f;

        struct ElbowCap { public Sprite Sprite; public Vector2 Centre; }   // Centre: on the forearm sprite, pixels
        static Dictionary<Sprite, ElbowCap> _elbowCaps;

        void PlaceElbowCaps()
        {
            PlaceElbowCap(_elbowFront, RigLayer.GlovesFront, RigLayer.GlovesFrontLower, RigLayer.ArmFront, RigLayer.ArmFrontLower,
                          _armPiece == AccessoryKind.None);
            PlaceElbowCap(_elbowBack, RigLayer.GlovesBack, RigLayer.GlovesBackLower, RigLayer.ArmBack, RigLayer.ArmBackLower, true);
        }

        void PlaceElbowCap(Transform elbow, RigLayer gloveUp, RigLayer gloveLo, RigLayer armUp, RigLayer armLo, bool allowed)
        {
            if (elbow == null) return;
            var t = elbow.Find(ElbowCapName);
            SpriteRenderer cap = t != null ? t.GetComponent<SpriteRenderer>() : null;

            float bend = Mathf.DeltaAngle(0f, elbow.localEulerAngles.z);
            SpriteRenderer lo = null; ElbowCap made = default;
            if (allowed && !DevPlainElbows && !_stone && Mathf.Abs(bend) >= ElbowCapFrom)
                foreach (var l in new[] { gloveLo, armLo })   // the glove's forearm if it reaches the elbow, else the bare arm's
                {
                    if (!_layers.TryGetValue(l, out var lsr) || lsr == null || !lsr.enabled || lsr.sprite == null) continue;
                    var c = CapFor(lsr.sprite, Mathf.Max(1, Mathf.RoundToInt(ElbowOverlap * lsr.sprite.pixelsPerUnit)));
                    if (c.Sprite == null) continue;
                    lo = lsr; made = c;
                    break;
                }

            if (lo == null) { if (cap != null) cap.enabled = false; return; }
            if (cap == null)
            {
                var go = new GameObject(ElbowCapName);
                go.transform.SetParent(elbow, false);
                cap = go.AddComponent<SpriteRenderer>();
            }
            cap.sprite = made.Sprite;
            cap.color = lo.color;
            cap.enabled = true;
            cap.sortingLayerID = lo.sortingLayerID;
            // under every half of this arm - the bare arm's and the glove's
            int order = lo.sortingOrder;
            foreach (var l in new[] { gloveUp, gloveLo, armUp, armLo })
                if (_layers.TryGetValue(l, out var a) && a != null && a.enabled && a.sprite != null) order = Mathf.Min(order, a.sortingOrder);
            cap.sortingOrder = order;

            // at the joint: the elbow row's centre on the forearm sprite, in the elbow's frame
            var lt = lo.transform;
            var sp = lo.sprite;
            var onSprite = new Vector3((made.Centre.x - sp.pivot.x) / sp.pixelsPerUnit, (made.Centre.y - sp.pivot.y) / sp.pixelsPerUnit, 0f);
            var centre = lt.localPosition + lt.localRotation * Vector3.Scale(onSprite, lt.localScale);
            cap.transform.localPosition = new Vector3(centre.x, centre.y, lt.localPosition.z + ElbowCapZ);
            // facing the outside of the bend: away from both halves (the forearm runs down the elbow's -y, the
            // upper arm up its parent's +y, which in the elbow's frame is turned back by the bend)
            Vector2 fore = Vector2.down, upper = Quaternion.Euler(0f, 0f, -bend) * Vector2.up;
            Vector2 outside = -(fore + upper).normalized;
            cap.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(outside.y, outside.x) * Mathf.Rad2Deg - 90f);
            cap.transform.localScale = new Vector3(Mathf.Abs(lt.localScale.x), Mathf.Abs(lt.localScale.y), 1f);
        }

        /// <summary>
        /// The cap for one forearm sprite: a half-round, flat side through its centre, round side up (+y), as wide
        /// as the forearm's fill on the elbow row and shaded across with that row, under the forearm's own outline
        /// colour and thickness. Pivot at the centre. Sprite null when the forearm draws nothing on that row.
        /// </summary>
        static ElbowCap CapFor(Sprite lower, int overlapRows)
        {
            _elbowCaps ??= new Dictionary<Sprite, ElbowCap>();
            if (_elbowCaps.TryGetValue(lower, out var hit)) return hit;
            var made = new ElbowCap();
            var tex = lower.texture;
            if (tex != null && tex.isReadable)
            {
                var r = lower.rect;
                int w = (int)r.width, h = (int)r.height;
                var src = tex.GetPixels32();
                Color32 At(int x, int y) => src[((int)r.y + y) * tex.width + (int)r.x + x];
                int row = h - 1 - overlapRows;               // the elbow line, rows counted up from the bottom
                int first = -1;
                if (row >= 0) for (int x = 0; x < w; x++) if (At(x, row).a > 0) { first = x; break; }
                if (first >= 0)
                {
                    var outline = At(first, row);
                    int a = -1, b = -1;
                    for (int x = first; x < w; x++)
                    {
                        var c = At(x, row);
                        if (c.a > 0 && !Near(c, outline)) { if (a < 0) a = x; b = x; }
                    }
                    if (a >= 0 && b - a >= 2)
                    {
                        int R = Mathf.Clamp(a - first, 1, 4);
                        float rad = (b - a + 1) * 0.5f;
                        int half = Mathf.CeilToInt(rad + R) + 1, S = 2 * half;
                        var px = new Color32[S * S];
                        for (int y = half; y < S; y++)        // the upper half only
                            for (int x = 0; x < S; x++)
                            {
                                float dx = x + 0.5f - half, dy = y + 0.5f - half, d = Mathf.Sqrt(dx * dx + dy * dy);
                                int col = Mathf.Clamp(Mathf.FloorToInt(a + rad + dx), a, b);
                                if (d <= rad) px[y * S + x] = At(col, row);
                                else if (d <= rad + R) px[y * S + x] = outline;
                            }
                        var capTex = new Texture2D(S, S, TextureFormat.RGBA32, false)
                        {
                            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                        };
                        capTex.SetPixels32(px);
                        capTex.Apply();
                        made.Sprite = Sprite.Create(capTex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), lower.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                        made.Sprite.name = lower.name + ".cap";
                        made.Centre = new Vector2(a + rad, row + 0.5f);
                    }
                }
            }
            _elbowCaps[lower] = made;
            return made;
        }
    }
}
