using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// The element motion styles (PrimitiveCharacterRig.Style, a prototype) side by side on one real
    /// chain, captured frame by frame for the mock sheet. Play mode, like GearSheet. Every figure's
    /// behaviours are switched off and stepped HERE on a fixed clock (PrimitiveCharacterRig.DevDt),
    /// so the capture can span several eval calls without the Editor's own frames moving anything:
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.MotionSheet.Setup("sword");'
    ///     unity command eval --code 'return Convergence.EditorTools.MotionSheet.Advance("/tmp/ms", 20);'   (repeat until "done")
    ///     unity command eval --code 'return Convergence.EditorTools.MotionSheet.Teardown();'
    ///
    /// One PNG per frame (1/30 s), every style in one row, today's first.
    /// </summary>
    public static class MotionSheet
    {
        const string RootName = "motion-sheet";
        static readonly Vector2 Origin = new(7000f, 5000f);
        const float CellW = 2.6f, CellH = 1.9f, Lift = 0.3f;
        const float Step = 1f / 120f, FrameEvery = 1f / 30f;

        static readonly string[] Armour =
        {
            "tier_hauberk_head", "tier_hauberk_shoulders", "tier_hauberk_torso", "tier_hauberk_gloves",
            "tier_hauberk_belt", "tier_hauberk_legs", "tier_hauberk_boots", "tier_hauberk_back",
        };

        struct Beat { public float At; public System.Action<PrimitiveCharacterRig> Act; public bool Styled; }

        static readonly List<PrimitiveCharacterRig> _rigs = new();
        static readonly List<(MonoBehaviour b, MethodInfo up, MethodInfo late)> _ticks = new();
        static List<Beat> _plan;
        static float _t, _nextFrame, _length;
        static int _frame, _beat;
        static string _weapon;

        /// <param name="final">The picks as built: five figures - today, then Fire, Water, Earth and
        /// Air, each with its element's motion style (SetArtElement) and its picture on the weapon
        /// (Combat.ElementArtFx, at full meter). Otherwise the mock round's thirteen styles.</param>
        public static string Setup(string weapon, bool final = false, bool perfect = false)
        {
            Teardown();
            _weapon = weapon;
            _final = final;
            _perfect = perfect;
            var root = new GameObject(RootName);
            string weaponId = weapon switch { "disc" => "tier_hauberk_disc", "bow" => "tier_hauberk_bow", _ => "tier_hauberk_greatsword" };
            int n = final ? 1 + Elements.Length : PrimitiveCharacterRig.MotionStyleNames.Length;
            for (int s = 0; s < n; s++)
            {
                var go = new GameObject($"style.{s}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(Origin.x + s * CellW, Origin.y, 0f);
                var rig = CharacterRigFactory.Build(go, ElementType.Fire, 0);
                rig.SetAppearance(new Appearance());
                var loadout = new Loadout();
                foreach (var id in Armour) { var it = GearCatalog.Get(id); if (it != null) loadout.Set(it.Slot, it.ItemId); }
                var w = GearCatalog.Get(weaponId); if (w != null) loadout.Set(w.Slot, w.ItemId);
                rig.Apply(loadout);
                // What CharacterRigFactory.Paint adds for a bow: the quiver and its arrow, and the
                // stringless picture with the nocks, so the string draws back to the arrow.
                rig.SetVialCount(0);
                var (quiver, arrow) = DemoGear.BowKit(w);
                rig.SetQuiver(quiver, arrow);
                bool strung = w != null && w.Class == WeaponClass.Bow && w.BowStringless != null;
                rig.SetBowString(strung ? w.BowStringless : null, strung ? w.BowNockTop : Vector2.zero,
                                 strung ? w.BowNockBottom : Vector2.zero, strung ? w.BowStringColor : Color.black);
                rig.SetContactShadow(true);
                rig.SetFacing(Vector2.right);
                _rigs.Add((PrimitiveCharacterRig)rig);
            }
            // Every behaviour under the sheet is stepped by hand from here on - scanned again after
            // the first frames, because the rig adds some itself (the cape's ClothBend) on its first Update.
            _plan = PlanFor(weapon);
            _t = 0f; _nextFrame = 0f; _frame = 0; _beat = 0;
            PrimitiveCharacterRig.DevDt = Step;
            Scan(root);
            for (int i = 0; i < 6; i++) Tick();
            Scan(root);
            // Settle the cloth and the carry before the chain starts.
            for (int i = 0; i < 84; i++) Tick();
            return $"{n} figures, {_weapon}, {_ticks.Count} behaviours stepped, hold {_hold:F2}s, {_length:F2}s";
        }

        // The real chains, on PlayerController's own timing: an opener and the lead-in at the basic
        // interval, then the finisher. Its hold is the bar's shortfall (BeginStrike's pad): the bar
        // (BarSecondsFor the weight) less the strike's own delay (StrikeDelay) - the whole bar for a
        // plain chop, a sliver for the Cleaver (struck partway into its swing), and NONE for a bow,
        // whose draw outlasts the bar: the bar times the draw, and the loose is the strike. A bow's
        // arrow leaves at the end of the draw (ReleaseArrow, LooseArrow) - the rig holds full draw
        // until then.
        static List<Beat> PlanFor(string weapon)
        {
            const float basic = 0.5f;   // BaseInterval / GlobalTempo, near enough
            var p = new List<Beat>();
            void At(float t, System.Action<PrimitiveCharacterRig> a, bool styled = false) => p.Add(new Beat { At = t, Act = a, Styled = styled });
            AttackMotion open, lead, fin; float openHop, delay, finLength, finHop; FinisherWeight weight;
            bool bow = weapon == "bow";
            switch (weapon)
            {
                case "disc":   // Heavy Cleaver on the default melee pair
                    open = AttackMotion.DiscChop; openHop = 0.12f; lead = AttackMotion.DiscWindUp;
                    fin = AttackMotion.DiscCleaverChop; weight = FinisherWeight.Heavy; finLength = basic * 2.5f; finHop = 0f;
                    delay = _rigs[0].StrikeDrawnSeconds(fin, finLength);
                    break;
                case "bow":    // Broadhead: a shot, the roll onto the knee, the kneeling draw
                    open = AttackMotion.Draw; openHop = 0f; lead = AttackMotion.BowKneelRoll;
                    fin = AttackMotion.KneelDraw; weight = FinisherWeight.Medium; finLength = basic * 1.5f; finHop = 0f;
                    delay = AttackMotions.SwingSeconds(finLength);
                    break;
                default:       // Overhand: Chop, Rise, the Overhand chop with its hop
                    open = AttackMotion.Chop; openHop = 0f; lead = AttackMotion.Rise;
                    fin = AttackMotion.Chop; weight = FinisherWeight.Medium; finLength = basic * 1.5f; finHop = 0.52f;
                    delay = 0f;
                    break;
            }
            float hold = Mathf.Max(0f, Tuning.StrikeTiming.BarSecondsFor(weight) - delay);
            _hold = hold;
            _strikeIn = hold + delay;
            At(0f, r => { r.SetSwingHop(openHop); r.PlayAttack(open, basic, false); });
            if (bow) At(AttackMotions.SwingSeconds(basic), r => r.LooseArrow());
            At(basic, r => { r.SetSwingHop(0f); r.PlayAttack(lead, basic, true); });
            float f = basic * 2f;
            if (hold > 0f)
            {
                At(f, r => { r.SetSwingHop(0f); r.HoldSwingStart(fin, false, hold); }, styled: true);
                At(f + hold, r => { r.SetSwingHop(finHop); r.PlayAttack(fin, finLength, false); });
            }
            else At(f, r => { r.SetSwingHop(finHop); r.PlayAttack(fin, finLength, false); }, styled: true);
            if (bow) At(f + AttackMotions.SwingSeconds(finLength), r => { if (_final) Release(r, arrow: true); r.LooseArrow(); });
            // The Cleaver throws its disc at the strike (ThrownDisc): in final mode a stand-in flies
            // off, so the pictures can be seen to move onto it (ElementArtFx.Adopt).
            if (weapon == "disc") At(f + hold + delay, r => { if (_final) Release(r, arrow: false); });
            _length = f + hold + finLength + 0.45f;
            return p;
        }

        static float _hold, _strikeIn;
        static readonly List<(Transform t, Vector2 v, float until)> _flyers = new();

        /// <summary>A thrown disc or loosed arrow's stand-in: the picture, flown straight on, adopted
        /// by the figure's art the way ThrownDisc / ThrownArrow do.</summary>
        static void Release(PrimitiveCharacterRig rig, bool arrow)
        {
            var from = arrow ? rig.NockedArrow : rig.WeaponRenderer;
            if (from == null) return;
            var go = new GameObject(arrow ? "sheet-arrow" : "sheet-disc");
            go.transform.SetParent(GameObject.Find(RootName).transform, true);
            go.transform.position = from.bounds.center;
            go.transform.localScale = from.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = from.sprite;
            sr.sortingOrder = SortingOrders.Fx - 1;
            if (!arrow) rig.SetWeaponVisible(false);
            _flyers.Add((go.transform, arrow ? new Vector2(9f, 0f) : new Vector2(2.4f, 0.3f), _t + (arrow ? 0.3f : 0.6f)));
            var feet = rig.transform.parent.Find("sheet-feet");
            if (feet != null) ElementArtFx.Adopt(feet.gameObject, sr, disc: !arrow);
        }

        static bool _final, _perfect;
        static float _perfectAt = -1f;   // final mode with a Perfect: when the bar settles
        static readonly ElementType[] Elements = { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air };

        /// <summary>A final-mode figure's art: its element's style and its picture (figure 0 is today).</summary>
        static void Arm(int i)
        {
            if (i == 0) return;
            var rig = _rigs[i];
            var cls = _weapon switch { "disc" => WeaponClass.Disc, "bow" => WeaponClass.Bow, _ => WeaponClass.Greatsword };
            rig.SetArtElement(Elements[i - 1], cls);
            // The ground under the figure: the rig's root rides above its feet (PICTURES.md: 0.57 at arena scale).
            var feet = rig.transform.Find("sheet-feet");
            if (feet == null)
            {
                feet = new GameObject("sheet-feet").transform;
                feet.SetParent(rig.transform.parent, false);
                feet.position = rig.transform.position + Vector3.down * (0.57f / Tuning.Player.ArenaVisualScale);
            }
            var fx = ElementArtFx.Begin(feet, () => Vector2.right, rig, Elements[i - 1], cls, _strikeIn, 1f, 1f);
            if (_perfect) _perfectAt = _t + _strikeIn;
            if (fx != null) fx.transform.SetParent(GameObject.Find(RootName).transform, true);
        }

        static void Scan(GameObject root)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (_ticks.Exists(x => x.b == b)) continue;
                var t = b.GetType();
                var up = t.GetMethod("Update", F, null, System.Type.EmptyTypes, null);
                var late = t.GetMethod("LateUpdate", F, null, System.Type.EmptyTypes, null);
                if (up == null && late == null) continue;
                b.enabled = false;
                _ticks.Add((b, up, late));
            }
        }

        static void Tick()
        {
            ElementArtFx.DevClock = 1000f + _t; ElementArtFx.DevDt = Step;
            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                var (t, v, until) = _flyers[i];
                if (t == null) { _flyers.RemoveAt(i); continue; }
                if (_t > until) { Object.Destroy(t.gameObject); _flyers.RemoveAt(i); continue; }
                t.position += (Vector3)(v * Step);
                if (v.y != 0f) t.Rotate(0f, 0f, -1440f * Step);
            }
            foreach (var (b, up, _) in _ticks) if (b != null && up != null) up.Invoke(b, null);
            foreach (var (b, _, late) in _ticks) if (b != null && late != null) late.Invoke(b, null);
        }

        /// <summary>Run the chain on, capturing up to <paramref name="frames"/> frames.</summary>
        public static string Advance(string dir, int frames = 20, int pxPerTexel = 1)
        {
            if (_plan == null || _rigs.Count == 0) return "run Setup first";
            System.IO.Directory.CreateDirectory(dir);
            PrimitiveCharacterRig.DevDt = Step;
            var root = GameObject.Find(RootName);
            if (root != null) Scan(root);
            int shot = 0;
            while (shot < frames && _t <= _length)
            {
                while (_beat < _plan.Count && _plan[_beat].At <= _t + 1e-5f)
                {
                    var beat = _plan[_beat++];
                    for (int i = 0; i < _rigs.Count; i++)
                    {
                        if (beat.Styled && _final) Arm(i);
                        else if (beat.Styled) _rigs[i].SetMotionStyle(i);
                        beat.Act(_rigs[i]);
                    }
                    var sheet = GameObject.Find(RootName);
                    if (sheet != null) Scan(sheet);
                }
                if (_perfectAt >= 0f && _t + 1e-5f >= _perfectAt)
                {
                    _perfectAt = -1f;
                    foreach (var r in _rigs) { var feet = r.transform.parent.Find("sheet-feet"); if (feet != null) ElementArtFx.Perfect(feet); }
                }
                if (_t + 1e-5f >= _nextFrame)
                {
                    Capture(System.IO.Path.Combine(dir, $"{_weapon}_{_frame:D3}.png"), pxPerTexel);
                    _frame++; shot++; _nextFrame += FrameEvery;
                }
                Tick();
                _t += Step;
            }
            return _t > _length ? $"done: {_frame} frames" : $"t={_t:F2} frames={_frame}";
        }

        static void Capture(string path, int pxPerTexel)
        {
            int n = _rigs.Count;
            float ppu = PixelSprite.FinestUnit * pxPerTexel;
            int w = Mathf.RoundToInt(n * CellW * ppu), h = Mathf.RoundToInt(CellH * ppu);
            var camGo = new GameObject("motion-sheet-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CellH * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f);
            camGo.transform.position = new Vector3(Origin.x + (n - 1) * CellW * 0.5f, Origin.y + Lift, -10f);
            var rt = RenderTexture.GetTemporary(w, h, 16, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(tex);
        }

        public static string Teardown()
        {
            PrimitiveCharacterRig.DevDt = -1f;
            ElementArtFx.DevClock = ElementArtFx.DevDt = -1f;
            _rigs.Clear(); _ticks.Clear(); _plan = null; _flyers.Clear(); _perfectAt = -1f;
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            return "torn down";
        }
    }
}
