using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// The scaled stride (Locomotion8.StrideFor) filmed: one figure per speed in a row, each over a strip of ground marks
    /// scrolling back at that figure's own speed, so a planted foot can be seen to hold its mark. Play mode; every
    /// behaviour is stepped here on a fixed clock (as MotionSheet), captured at 25 a second (the stride's picture rate):
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.RunSheet.Setup("sword", false);'
    ///     unity command eval --code 'return Convergence.EditorTools.RunSheet.Advance("/tmp/rs", 25);'   (repeat until "done")
    ///     unity command eval --code 'return Convergence.EditorTools.RunSheet.Teardown();'
    /// </summary>
    public static class RunSheet
    {
        const string RootName = "run-sheet";
        static readonly Vector2 Origin = new(9000f, 5000f);
        const float CellW = 2.2f, CellH = 1.9f, Lift = 0.3f, MarkEvery = 0.25f;
        const float Step = 1f / 100f, FrameEvery = 1f / 25f;
        /// <summary>The speeds filmed, arena units a second: the slowest stick tilt, half tilt, the base run, a full build.</summary>
        public static readonly float[] Speeds = { 0.7f, 1.4f, 2.9f, 4.6f, 7.4f };

        static readonly string[] Armour =
        {
            // Bronze: the set baked for the eight directions (Silver and up draw bare)
            "tier_hoplite_head", "tier_hoplite_shoulders", "tier_hoplite_torso", "tier_hoplite_gloves",
            "tier_hoplite_belt", "tier_hoplite_legs", "tier_hoplite_boots", "tier_hoplite_back",
        };

        static readonly List<PrimitiveCharacterRig> _rigs = new();
        static readonly List<(MonoBehaviour b, MethodInfo up, MethodInfo late)> _ticks = new();
        static readonly List<(Transform t, float speed, float x0)> _marks = new();
        static float _t, _nextFrame, _length;
        static int _frame;
        static string _weapon;
        static bool _facings;

        /// <param name="facings">One figure per facing (0, 45 .. 315 degrees) at <paramref name="speed"/>, rather than one per speed.</param>
        public static string Setup(string weapon, bool inCombat, float seconds = 2f, bool facings = false, float speed = 4.6f)
        {
            _facings = facings;
            int count = facings ? 8 : Speeds.Length;
            Teardown();
            _weapon = weapon + (inCombat ? "-combat" : "");
            _length = seconds;
            var root = new GameObject(RootName);
            string weaponId = weapon switch { "disc" => "tier_hoplite_disc", "bow" => "tier_hoplite_bow", _ => "tier_hoplite_greatsword" };
            var markSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f / 0.03f);
            for (int s = 0; s < count; s++)
            {
                float v = facings ? speed : Speeds[s];
                var travel = facings ? new Vector2(Mathf.Cos(s * 45f * Mathf.Deg2Rad), Mathf.Sin(s * 45f * Mathf.Deg2Rad)) : Vector2.right;
                var cell = new Vector3(Origin.x + s * CellW, Origin.y, 0f);
                var go = new GameObject($"run.{s}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = cell;
                var rig = (PrimitiveCharacterRig)CharacterRigFactory.Build(go, ElementType.Fire, 0);
                rig.SetAppearance(new Appearance());
                var loadout = new Loadout();
                foreach (var id in Armour) { var it = GearCatalog.Get(id); if (it != null) loadout.Set(it.Slot, it.ItemId); }
                var w = GearCatalog.Get(weaponId); if (w != null) loadout.Set(w.Slot, w.ItemId);
                rig.Apply(loadout);
                var (quiver, arrow) = DemoGear.BowKit(w);
                rig.SetQuiver(quiver, arrow);
                rig.SetContactShadow(true);
                rig.SetAimViews(true);   // the eight-direction body (the player's, PlayerController)
                rig.SetFacing(travel);
                rig.DevTravelOwn = travel;
                rig.SetInCombat(inCombat);
                // the rig's own speed for this ARENA speed: a rig at another scale covers its own texels
                float scale = Mathf.Abs(rig.transform.localScale.y);
                rig.DevSpeed = v * Locomotion8.ArenaTexelsPerUnit * scale / PixelSprite.FinestUnit;
                _rigs.Add(rig);
                // the ground marks, scrolling back at that speed
                float sole = cell.y - 0.57f / Tuning.Player.ArenaVisualScale * scale;   // the root rides 0.57 above the feet at arena scale (PICTURES.md)
                for (float x = -CellW * 0.5f; x < CellW * 0.5f; x += MarkEvery)
                {
                    var m = new GameObject("mark");
                    m.transform.SetParent(root.transform, false);
                    m.transform.position = new Vector3(cell.x + x, sole, 0f);
                    var sr = m.AddComponent<SpriteRenderer>();
                    sr.sprite = markSprite; sr.color = new Color(0.18f, 0.19f, 0.22f);
                    sr.sortingOrder = -1000;
                    _marks.Add((m.transform, rig.DevSpeed * travel.x, cell.x));
                }
            }
            _t = 0f; _nextFrame = 0f; _frame = 0;
            PrimitiveCharacterRig.DevDt = Step;
            PrimitiveCharacterRig.DevTravel = Vector2.right;   // every body faces its travel
            Scan(root);
            for (int i = 0; i < 6; i++) Tick();
            Scan(root);
            for (int i = 0; i < 100; i++) Tick();   // up to speed, the cloth settled
            return $"{count} figures, {_weapon}, {_ticks.Count} behaviours stepped";
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
            foreach (var (b, up, _) in _ticks) if (b != null && up != null) up.Invoke(b, null);
            foreach (var (b, _, late) in _ticks) if (b != null && late != null) late.Invoke(b, null);
            foreach (var (t, speed, x0) in _marks)
            {
                var p = t.position; p.x -= speed * Step;
                if (p.x < x0 - CellW * 0.5f) p.x += CellW;
                if (p.x > x0 + CellW * 0.5f) p.x -= CellW;
                t.position = p;
            }
        }

        /// <summary>Run on, capturing up to <paramref name="frames"/> frames.</summary>
        public static string Advance(string dir, int frames = 25, int pxPerTexel = 1)
        {
            if (_rigs.Count == 0) return "run Setup first";
            System.IO.Directory.CreateDirectory(dir);
            PrimitiveCharacterRig.DevDt = Step;
            int shot = 0;
            while (shot < frames && _t <= _length)
            {
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
            var camGo = new GameObject("run-sheet-cam");
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
            PrimitiveCharacterRig.DevTravel = null;
            _rigs.Clear(); _ticks.Clear(); _marks.Clear();
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            return "torn down";
        }
    }
}
