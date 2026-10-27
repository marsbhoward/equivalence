using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.FireFx;

namespace Convergence.Player
{
    /// <summary>
    /// Fire's heat on the ground under the player (the user's picks, 2026-10-25): a ring of
    /// SCORCHED FLOOR that grows with every stack, its rim flickering with embers, and embers
    /// rising off its BACK half behind the body to the knee; at full heat the SIGIL EMBLEM - the
    /// selector's four interlocking rings, no mark inside (the user's call) - burns into the floor
    /// in fire's colours and pulses until the release is spent. It replaced
    /// the soft ring that pulsed out from the body as the fifth stack landed.
    ///
    /// Nothing lies over the player (the lighting rule): the ring and the sigil are one texture on
    /// the ground, over the pits and under every body, and the embers are a second one sorted ONE
    /// BELOW the player's lowest layer, and only ever start on the side away from the camera.
    /// Both are on the floor's texel grid and follow the contact shadow, which stays on the ground
    /// through a leap. A picture only.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HeatGround : MonoBehaviour
    {
        const float Ppu = Art.FloorArt.PixelsPerUnit;

        [SerializeField] FireResource _fire;
        [SerializeField] Transform _feet;
        [SerializeField] SpriteRenderer _ground, _embers;
        [SerializeField] float _rx, _sigil;
        SpriteRenderer[] _body;
        Texture2D _gTex, _eTex;
        Color32[] _gPx, _ePx;
        int _w, _h, _cx, _cy;

        static bool[] _sigilInk;
        static int _sigilW, _sigilH;

        public static HeatGround Attach(FireResource fire)
        {
            var go = new GameObject("fire.heat");
            go.transform.SetParent(fire.transform, false);
            var g = go.AddComponent<HeatGround>();
            g._fire = fire;
            g._ground = Layer(go, "fire.heat.ground", SortingOrders.GroundLasting);
            g._embers = Layer(go, "fire.heat.embers", SortingOrders.Character);
            return g;
        }

        static SpriteRenderer Layer(GameObject parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        void LateUpdate()
        {
            if (_fire == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime, now = Time.time;
            int stacks = _fire.Stacks;

            // The ring eases toward the size the heat wants, and shrinks away as heat fades.
            float want = stacks > 0 ? T.RingBase + T.RingPerStack * (stacks - 1) : 0f;
            _rx = Mathf.MoveTowards(_rx, want, T.RingEase * dt * Mathf.Max(1f, Mathf.Abs(want - _rx)));
            _sigil = Mathf.MoveTowards(_sigil, _fire.CanRelease ? 1f : 0f, dt / T.SigilInSeconds);

            if (_rx < 1.5f && _sigil <= 0f) { _ground.enabled = false; _embers.enabled = false; return; }

            EnsureCanvas();
            FindFeet();

            // On the floor's grid, centred on the feet.
            Vector2 feet = _feet != null ? (Vector2)_feet.position : (Vector2)transform.position;
            var corner = new Vector2(Mathf.Round(feet.x * Ppu) - _cx, Mathf.Round(feet.y * Ppu) - _cy) / Ppu;
            _ground.transform.position = new Vector3(corner.x, corner.y, 0f);
            _embers.transform.position = _ground.transform.position;

            System.Array.Clear(_gPx, 0, _gPx.Length);
            System.Array.Clear(_ePx, 0, _ePx.Length);
            DrawRing(now);
            if (_sigil > 0f) DrawSigil(now);
            DrawEmbers(now, stacks);
            Upload(_gTex, _gPx);
            Upload(_eTex, _ePx);

            _ground.enabled = true;
            _embers.enabled = true;
            _embers.sortingOrder = LowestBodyOrder() - 1;
        }

        void DrawRing(float now)
        {
            if (_rx < 1.5f) return;
            float rx = _rx, ry = Mathf.Max(1.5f, _rx * T.RingSquash);
            int frame = Mathf.FloorToInt(now * 8f);
            int ex = Mathf.CeilToInt(rx * 1.1f) + 1, ey = Mathf.CeilToInt(ry * 1.1f) + 1;
            var scorch = T.Ramp[T.Ramp.Length - 1];
            for (int dy = -ey; dy <= ey; dy++)
                for (int dx = -ex; dx <= ex; dx++)
                {
                    float d = dx * dx / (rx * rx) + dy * dy / (ry * ry);
                    if (d > 1.15f) continue;
                    if (d > 0.82f)
                    {
                        // The rim: embers, flickering on and off at a slow beat.
                        if (FireArt.Hash(dx, dy, frame) > 0.35f)
                            Put(_gPx, _cx + dx, _cy + dy, FireArt.At(0.35f + FireArt.Hash(dx, dy) * 0.3f));
                    }
                    else
                    {
                        // Scorched floor: the darkest ember, half seen, mottled.
                        var c = scorch;
                        c.a = (byte)(90 + FireArt.Hash(dx, dy, 5) * 40);
                        Put(_gPx, _cx + dx, _cy + dy, c);
                    }
                }
        }

        void DrawSigil(float now)
        {
            EnsureSigil();
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * T.SigilPulse);
            Color32 lit = Color32.Lerp(FireArt.At(0.45f), FireArt.At(0.2f), pulse);
            lit.a = (byte)(255 * _sigil);
            int x0 = _cx - _sigilW / 2, y0 = _cy - _sigilH / 2;
            for (int y = 0; y < _sigilH; y++)
                for (int x = 0; x < _sigilW; x++)
                    if (_sigilInk[y * _sigilW + x]) Put(_gPx, x0 + x, y0 + y, lit);
        }

        /// <summary>Embers off the ring's back half only - north of the feet, so they start behind
        /// the body - climbing to about the knee.</summary>
        void DrawEmbers(float now, int stacks)
        {
            if (stacks <= 0 || _rx < 1.5f) return;
            float ry = Mathf.Max(1.5f, _rx * T.RingSquash);
            int count = stacks * T.GroundEmbersPerStack;
            for (int k = 0; k < count; k++)
            {
                float life = T.GroundEmberLife * (0.75f + FireArt.Hash(k, 41, 1) * 0.5f);
                float phase = FireArt.Hash(k, 41, 2) * life;
                float age = (now + phase) % life;
                int cycle = Mathf.FloorToInt((now + phase) / life);
                float a = Mathf.PI * (0.08f + 0.84f * FireArt.Hash(k, cycle, 3));   // the back arc
                float x = _rx * Mathf.Cos(a) + Mathf.Sin(age * 3.3f + k) * 1.2f;
                float y = ry * Mathf.Sin(a) + age * T.GroundEmberRise * (0.7f + FireArt.Hash(k, 9) * 0.6f);
                float u = age / life;
                if (u > 0.85f && FireArt.Hash(k, cycle, Mathf.FloorToInt(now * 12f)) > 0.5f) continue;
                Put(_ePx, _cx + Mathf.RoundToInt(x), _cy + Mathf.RoundToInt(y), FireArt.At(0.12f + u * 0.8f));
            }
        }

        /// <summary>The sigil selector's four interlocking rings (Combat.SigilEmblem), no element
        /// mark inside, flattened onto the floor's grid and sized past the full ring.</summary>
        /// <summary>The full-heat sigil's half-width in floor texels - Erupt's blast flares the same one.</summary>
        public static int SigilHalf
            => Mathf.RoundToInt((T.RingBase + T.RingPerStack * (Tuning.Elements.Fire.MaxStacks - 1)) * T.SigilFill);

        static void EnsureSigil()
        {
            int half = SigilHalf;
            if (_sigilInk != null && _sigilW == half * 2 + 1) return;
            _sigilInk = SigilEmblem.Ink(half, T.SigilSquash, T.SigilWeight, out _sigilW, out _sigilH);
        }

        void EnsureCanvas()
        {
            if (_gTex != null) return;
            float full = T.RingBase + T.RingPerStack * (Tuning.Elements.Fire.MaxStacks - 1);
            EnsureSigil();
            int half = Mathf.Max(Mathf.CeilToInt(full * 1.15f), _sigilW / 2) + 2;
            _w = half * 2 + 1;
            int below = Mathf.Max(Mathf.CeilToInt(full * T.RingSquash * 1.15f), _sigilH / 2) + 2;
            int above = below + Mathf.CeilToInt(T.GroundEmberRise * T.GroundEmberLife * 1.4f) + 2;
            _h = below + above + 1;
            _cx = half; _cy = below;
            _gTex = NewTex(); _eTex = NewTex();
            _gPx = new Color32[_w * _h]; _ePx = new Color32[_w * _h];
            _ground.sprite = Sprite.Create(_gTex, new Rect(0, 0, _w, _h), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            _embers.sprite = Sprite.Create(_eTex, new Rect(0, 0, _w, _h), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
        }

        Texture2D NewTex() => new(_w, _h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };

        /// <summary>The contact shadow: on the ground under the feet even mid-leap.</summary>
        void FindFeet()
        {
            if (_feet != null) return;
            foreach (var t in _fire.GetComponentsInChildren<Transform>(true))
                if (t.name == "shadow") { _feet = t; return; }
        }

        int LowestBodyOrder()
        {
            if (_body == null || _body.Length == 0 || _body[0] == null)
            {
                var rig = _fire.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
                _body = rig != null ? rig.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
            }
            int low = int.MaxValue;
            foreach (var r in _body)
            {
                if (r == null || !r.enabled || r.sprite == null || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                if (r.sortingOrder < low) low = r.sortingOrder;
            }
            return low == int.MaxValue ? SortingOrders.Character : low;
        }

        void Put(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            px[y * _w + x] = c;
        }

        static void Upload(Texture2D tex, Color32[] px) { tex.SetPixels32(px); tex.Apply(); }

        void OnDestroy()
        {
            if (_ground != null && _ground.sprite != null) Destroy(_ground.sprite);
            if (_embers != null && _embers.sprite != null) Destroy(_embers.sprite);
            if (_gTex != null) Destroy(_gTex);
            if (_eTex != null) Destroy(_eTex);
        }
    }
}
