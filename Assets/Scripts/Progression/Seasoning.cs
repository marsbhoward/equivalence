using UnityEngine;
using Convergence.Core;

namespace Convergence.Progression
{
    /// <summary>
    /// Sulfur's mark, carried by the ENEMY rather than by a table on the player.
    ///
    /// A component, not a Dictionary keyed by Health, and that is the documented reason rather
    /// than taste: a Dictionary field on a MonoBehaviour comes back EMPTY after a domain reload
    /// while every object it referenced survives, so a script edit mid-run would silently
    /// de-season a field full of marked enemies with nothing on screen changing. A component dies
    /// with the body it is attached to and needs no sweeping.
    ///
    /// It draws its own tell. A mark the player cannot see is a mark they cannot play around, and
    /// the whole chain is about choosing WHEN to release.
    /// </summary>
    public class Seasoning : MonoBehaviour
    {
        public int Stacks { get; private set; }
        float _expires;
        SpriteRenderer _tell;

        public static Seasoning Get(GameObject go)
        {
            // Not `??`: a missing component is not C# null in the Editor.
            var s = go.GetComponent<Seasoning>();
            return s != null ? s : go.AddComponent<Seasoning>();
        }

        /// <summary>
        /// Refreshes the clock on every application, so sustained pressure keeps a target seasoned
        /// - which is the behaviour Sulfur is rewarding. A per-stack clock would make the mark
        /// decay from underneath a player who is doing exactly the right thing.
        /// </summary>
        public void Add()
        {
            Stacks = Mathf.Min(Stacks + 1, Tuning.Principle.SeasonMaxStacks);
            _expires = Time.time + Tuning.Principle.SeasonSeconds;
        }

        /// <summary>Takes the marks off and reports how many there were.</summary>
        public int Consume()
        {
            int n = Stacks;
            Stacks = 0;
            return n;
        }

        void Update()
        {
            if (Stacks > 0 && Time.time >= _expires) Stacks = 0;
            Draw();
        }

        // The tell (the user's pick, 2026-10-26, group 3 - "pips over the head"): a row of ember
        // pips over the head, one per mark, so the count reads at a glance. Texel-drawn at body
        // density; an overlay, so DepthSorted never re-bases it.
        Texture2D _tex;
        Color32[] _px;
        int _drawnStacks = -1;
        static readonly Color32 Pip = new(255, 214, 90, 255), PipEdge = new(216, 64, 26, 255);

        void Draw()
        {
            if (Stacks <= 0)
            {
                if (_tell != null) _tell.enabled = false;
                return;
            }
            const float Ppu = Combat.TexelFx.Body;
            int max = Tuning.Principle.SeasonMaxStacks, w = max * 3 + 1, h = 3;
            if (_tell == null)
            {
                var go = new GameObject("seasoned");
                go.transform.SetParent(transform, false);
                _tell = go.AddComponent<SpriteRenderer>();
                _tell.sortingOrder = SortingOrders.StatusOverlay + 2;
            }
            if (_tex == null)
            {
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[w * h];
                _tell.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
                _drawnStacks = -1;
            }
            if (_drawnStacks != Stacks)
            {
                _drawnStacks = Stacks;
                System.Array.Clear(_px, 0, _px.Length);
                int x0 = (w - (Stacks * 3 + 1)) / 2;   // centred over the head
                for (int i = 0; i < Stacks; i++)
                {
                    int x = x0 + 1 + i * 3;
                    _px[1 * w + x] = Pip; _px[1 * w + x + 1] = Pip;
                    _px[0 * w + x] = PipEdge; _px[0 * w + x + 1] = PipEdge;
                    _px[2 * w + x] = Pip; _px[2 * w + x + 1] = PipEdge;
                }
                _tex.SetPixels32(_px);
                _tex.Apply();
            }

            // The first row over the head (OverHead): the stagger stars and the Mark's sigil are above.
            if (!Combat.OverHead.Top(transform, out float top, out float mx)) { top = transform.position.y + 0.8f; mx = transform.position.x; }
            _tell.transform.position = new Vector3(Mathf.Round(mx * Ppu) / Ppu, Mathf.Round((top + Combat.OverHead.PipsAt) * Ppu) / Ppu, 0f);
            var ls = transform.lossyScale;
            _tell.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), 1f);
            _tell.enabled = true;
        }

        void OnDestroy()
        {
            if (_tell != null && _tell.sprite != null) Destroy(_tell.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
