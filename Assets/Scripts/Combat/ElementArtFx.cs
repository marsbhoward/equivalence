using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Player;
using T = Convergence.Core.Tuning.ElementArtFx;

namespace Convergence.Combat
{
    /// <summary>
    /// A weapon art's ELEMENT, on the weapon (PICTURES.md, "Element finisher pictures"; the user's
    /// picks per class off the mock sheet, 2026-11-03):
    ///
    ///                Greatsword        Discs                       Bow
    ///     Fire       Forge sparks      Molten edge + Forge sparks  Molten edge + Forge sparks
    ///     Water      Ribbon            Water film + Ribbon         Ribbon
    ///     Earth      Orbiting grit     Orbiting grit               Stone crust + Orbiting grit
    ///     Air        Gathering streaks Gathering streaks           Gathering streaks
    ///
    /// One per art, made at the press (PlayerController) and gone a few seconds after the strike.
    /// It rides whatever carries the art: the held weapon, the arrow on the string, and a disc or
    /// arrow once thrown (ThrownDisc / ThrownArrow <see cref="Adopt"/> it). Three texel canvases:
    /// one in front of the weapon and one behind it at weapon density (sparks, drops, the ribbon,
    /// pebbles, streaks), and the floor's at 37.5 (embers, wet marks, pebbles that landed). The
    /// heat, film and crust are the weapon's OWN texels recoloured on an overlay laid exactly over
    /// it, so nothing is drawn over the player. Every scatter is hashed from the art's serial,
    /// never UnityEngine.Random. Strength follows the element meter at the press.
    /// </summary>
    public class ElementArtFx : MonoBehaviour
    {
        const float WeaponPpu = 75f, GroundPpu = 37.5f;

        /// <summary>The live arts' pictures (one per figure), for a disc or arrow released inside one.
        /// Lazily made: a domain reload empties statics.</summary>
        static List<ElementArtFx> _live;
        static List<ElementArtFx> Live => _live ??= new List<ElementArtFx>();

        enum Shape { Blade, Round, Shaft }

        sealed class Source
        {
            public SpriteRenderer Sr;
            public Shape Shape;
            public bool Thrown;
            public float AdoptedAt;
            public Vector2 LastTip, Velocity;
            public bool HasLast;
            public Overlay Overlay;
            /// <summary>The held weapon's rig, when its eight-direction body may draw the weapon in place of this sprite.</summary>
            public PrimitiveCharacterRig Rig8;
        }

        /// <summary>The weapon's own texels, recoloured, on a sprite laid exactly over it.</summary>
        sealed class Overlay
        {
            public Sprite Of;
            public SpriteRenderer Sr;
            public Texture2D Tex;
            public Color32[] Px;
            public int W, H;
            public byte[] Kind;      // 0 empty, 1 outline, 2 body, 3 edge
            public float[] U;        // along the weapon, 0 at the hilt/hub/fletching
            public float[] Lum;
        }

        enum Kind : byte { Spark, Ember, Drift, Drop, Wet, Pebble, Dust, Chunk }

        struct Bit
        {
            public Kind Kind;
            public Vector2 Pos;      // world, on the ground plane
            public float Z, Vz;      // height above it
            public Vector2 V;
            public float Gravity, Born, Life, LandedAt;
            public int Seed, Src;    // Src: a source it hangs by (pebbles), or -1
            public float U, S;
            public bool Landed, Flung, Film;   // Film: a drop thrown off the water film
            public Vector2 Hang;     // where a pebble last hung by its weapon; a lined-up film drop's spot
        }

        Transform _owner;                 // the player (or a sheet's figure)
        System.Func<Vector2> _facing;
        ICharacterRig _rig;
        ElementType _element;
        WeaponClass _class;
        float _t0, _strikeAt, _k, _scale = 1f;
        int _serial;
        bool _sparks, _molten, _ribbon, _film, _grit, _crust, _streaks;
        readonly List<Source> _sources = new();
        readonly List<Bit> _bits = new();
        readonly List<(Vector2 pos, float time)> _trail = new();
        bool _windEmitted, _strikeEmitted;
        bool _even; int _evenCount; Vector2 _lineFrom, _lineDir;   // a Perfect: the film's drops land in a row
        int _sparkCount, _streakCount, _gritCount, _crustCount, _burstCount;
        readonly float[] _owed = new float[8];   // each emitter's part-made bits, carried frame to frame

        /// <summary>How many to make this frame of an emitter owed <paramref name="want"/> more.</summary>
        int Take(int emitter, float want)
        {
            _owed[emitter] += Mathf.Max(0f, want);
            int n = Mathf.FloorToInt(_owed[emitter]);
            _owed[emitter] -= n;
            return n;
        }

        Sheet _front, _back, _ground;

        sealed class Sheet
        {
            public SpriteRenderer Sr;
            public Texture2D Tex;
            public Color32[] Px;
            public int Size;
            public float Ppu;
            public int Cx, Cy;   // the canvas centre, in world texels
            public bool Drawn, WasDrawn;   // anything on it this frame / last - an empty one is neither cleared nor uploaded
        }

        static int _serials;

        /// <summary>Start the pictures for an art fired now, striking <paramref name="strikeIn"/> seconds from now.</summary>
        public static ElementArtFx Begin(PlayerController player, ICharacterRig rig, ElementType element,
                                         WeaponClass weapon, float strikeIn, float meterFill)
            => player == null ? null : Begin(player.transform, () => player.Facing, rig, element, weapon, strikeIn, meterFill,
                                             Tuning.Player.ArenaVisualScale);

        /// <summary>The same for any figure - Editor/MotionSheet drives its own.</summary>
        /// <param name="scale">The figure's drawn scale: the weapon canvases sit on ITS texels, and every
        /// speed and distance (tuned against a figure at scale 1) grows with it.</param>
        public static ElementArtFx Begin(Transform owner, System.Func<Vector2> facing, ICharacterRig rig, ElementType element,
                                         WeaponClass weapon, float strikeIn, float meterFill, float scale)
        {
            if (owner == null || rig == null) return null;
            // One art's pictures per figure: a new art ends the last one's.
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] == null) Live.RemoveAt(i);
                else if (Live[i]._owner == owner) Live[i].Finish();
            var go = new GameObject("element-art-fx");
            var fx = go.AddComponent<ElementArtFx>();
            fx._owner = owner; fx._facing = facing; fx._rig = rig; fx._element = element; fx._class = weapon;
            fx._t0 = Clock; fx._strikeAt = Clock + Mathf.Max(0f, strikeIn);
            fx._k = T.MeterFloor + (1f - T.MeterFloor) * Mathf.Clamp01(meterFill);
            fx._serial = ++_serials;
            fx._scale = Mathf.Max(0.1f, scale);
            bool disc = weapon == WeaponClass.Disc, bow = weapon == WeaponClass.Bow;
            switch (element)
            {
                case ElementType.Fire: fx._sparks = true; fx._molten = disc || bow; break;
                case ElementType.Water: fx._ribbon = true; fx._film = disc; break;
                case ElementType.Earth: fx._grit = true; fx._crust = bow; break;
                default: fx._streaks = true; break;
            }
            fx._front = fx.MakeCanvas("front", WeaponPpu / fx._scale, T.WeaponCanvas);
            fx._back = fx.MakeCanvas("back", WeaponPpu / fx._scale, T.WeaponCanvas);
            fx._ground = fx.MakeCanvas("ground", GroundPpu, T.GroundCanvas);
            Live.Add(fx);
            return fx;
        }

        /// <summary>A disc or arrow released by <paramref name="owner"/> inside the live art carries its pictures.</summary>
        public static void Adopt(GameObject owner, SpriteRenderer thrown, bool disc)
        {
            if (thrown == null || owner == null) return;
            ElementArtFx fx = null;
            foreach (var f in Live) if (f != null && f._owner != null && f._owner.gameObject == owner) fx = f;
            if (fx == null) return;
            if (Clock > fx._strikeAt + T.StrikeAfter + 0.3f) return;
            fx._sources.Add(new Source { Sr = thrown, Shape = disc ? Shape.Round : Shape.Shaft, Thrown = true, AdoptedAt = Clock });
        }

        /// <summary>The bar settled PERFECT for <paramref name="owner"/>'s art: the water film's drops,
        /// in the air and still to come, land in one even line along the cut.</summary>
        public static void Perfect(Transform owner)
        {
            if (owner == null) return;
            foreach (var f in Live) if (f != null && f._owner == owner) f.LineUp();
        }

        void LineUp()
        {
            if (!_film || _even) return;
            var main = MainSource();
            if (main == null) return;
            _even = true;
            var dir = main.Thrown ? Travel(main) : (_facing != null ? _facing() : Vector2.right);
            _lineDir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            _lineFrom = new Vector2(Point(main, 1f, 0f).x, GroundUnder(main)) + _lineDir * T.FilmLineStart * _scale;
            for (int i = 0; i < _bits.Count; i++)
                if (_bits[i].Film && !_bits[i].Landed) { var b = _bits[i]; AimAtLine(ref b); _bits[i] = b; }
        }

        /// <summary>Re-aim a film drop in flight so it lands on the next free spot of the line.</summary>
        void AimAtLine(ref Bit b)
        {
            var at = b.Hang = _lineFrom + _lineDir * (_evenCount++ * T.FilmLineStep * _scale);
            float g = Mathf.Max(1e-3f, b.Gravity);
            float t = (b.Vz + Mathf.Sqrt(b.Vz * b.Vz + 2f * g * Mathf.Max(0f, b.Z))) / g;
            b.V = (at - b.Pos) / Mathf.Max(0.05f, t);
        }

        void Finish()
        {
            Live.Remove(this);
            Destroy(gameObject);
        }

        Sheet MakeCanvas(string name, float ppu, int size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var c = new Sheet { Sr = go.AddComponent<SpriteRenderer>(), Size = size, Ppu = ppu };
            c.Tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            c.Px = new Color32[size * size];
            c.Sr.sprite = Sprite.Create(c.Tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            c.Sr.enabled = false;
            return c;
        }

        void OnDestroy()
        {
            Live.Remove(this);
            foreach (var c in new[] { _front, _back, _ground })
            {
                if (c == null) continue;
                if (c.Sr != null && c.Sr.sprite != null) Destroy(c.Sr.sprite);
                if (c.Tex != null) Destroy(c.Tex);
            }
            foreach (var s in _sources) DropOverlay(s);
        }

        // ------------------------------------------------------------------ the frame

        /// <summary>Testing: a clock and frame time for a harness that steps this itself (-1 = off).</summary>
        public static float DevClock = -1f, DevDt = -1f;
        static float Clock => DevClock >= 0f ? DevClock : Time.time;
        float Now => Clock;
        /// <summary>The bar's fill, 0..1 (1 from the strike on).</summary>
        float Bar => _strikeAt <= _t0 ? 1f : Mathf.Clamp01((Now - _t0) / (_strikeAt - _t0));
        float Since => Now - _strikeAt;

        void LateUpdate()
        {
            if (_owner == null || Since > T.Lifetime) { Finish(); return; }
            float dt = DevDt >= 0f ? DevDt : Time.deltaTime;
            RefreshHeld();
            foreach (var s in _sources) Track(s, dt);

            var main = MainSource();
            if (main != null)
            {
                if (Now < _strikeAt) WindUp(main, dt);
                Strike(dt);
            }
            Simulate(dt);
            Paint(main);
            foreach (var s in _sources) PaintOverlay(s);
        }

        /// <summary>The held source: the arrow on the string for a bow, else the weapon in the hand.</summary>
        void RefreshHeld()
        {
            SpriteRenderer held = null;
            Shape shape = Shape.Blade;
            if (_class == WeaponClass.Bow)
            {
                if (_rig is PrimitiveCharacterRig prim) held = prim.NockedArrow;
                shape = Shape.Shaft;
            }
            else
            {
                held = _rig.WeaponRenderer;
                shape = _class == WeaponClass.Disc ? Shape.Round : Shape.Blade;
            }
            if (held == null) return;
            foreach (var s in _sources) if (s.Sr == held) return;
            _sources.Add(new Source { Sr = held, Shape = shape, Rig8 = _rig as PrimitiveCharacterRig });
        }

        static bool Visible(Source s) =>
            s.Sr != null && s.Sr.sprite != null && (s.Sr.enabled && !s.Sr.forceRenderingOff || s.Rig8 != null && s.Rig8.Redrawn8(s.Sr))
            && s.Sr.gameObject.activeInHierarchy;

        Source MainSource()
        {
            // A thrown thing carries the art once it leaves; before that, what is in the hand.
            for (int i = _sources.Count - 1; i >= 0; i--) if (_sources[i].Thrown && Visible(_sources[i])) return _sources[i];
            for (int i = _sources.Count - 1; i >= 0; i--) if (!_sources[i].Thrown && Visible(_sources[i])) return _sources[i];
            return null;
        }

        int IndexOf(Source s) => _sources.IndexOf(s);

        void Track(Source s, float dt)
        {
            if (!Visible(s)) { s.HasLast = false; return; }
            var tip = Point(s, 1f, 0f);
            if (s.HasLast && dt > 0f) s.Velocity = Vector2.Lerp(s.Velocity, (tip - s.LastTip) / dt, 0.6f);
            s.LastTip = tip; s.HasLast = true;
        }

        /// <summary>A point on the source in world space: <paramref name="u"/> along it (0 hilt/hub/
        /// fletching, 1 tip/rim/head), <paramref name="s"/> across it (-1..1; round: the angle).</summary>
        static Vector2 Point(Source src, float u, float s)
        {
            var b = src.Sr.sprite.bounds;
            Vector2 local;
            switch (src.Shape)
            {
                case Shape.Round:
                {
                    float a = s * Mathf.PI, r = u * Mathf.Min(b.extents.x, b.extents.y);
                    local = new Vector2(b.center.x + Mathf.Cos(a) * r, b.center.y + Mathf.Sin(a) * r);
                    break;
                }
                case Shape.Shaft:
                    local = new Vector2(Mathf.Lerp(b.min.x, b.max.x, u), b.center.y + s * b.extents.y);
                    break;
                default:
                    local = new Vector2(b.center.x + s * b.extents.x * 0.45f, Mathf.Lerp(b.max.y * 0.2f, b.max.y, u));
                    break;
            }
            Vector3 world = src.Sr.transform.TransformPoint(local);
            // the eight-direction body draws the held weapon itself: the point where IT is drawn
            return src.Rig8 != null && src.Rig8.Redrawn8(src.Sr, world, out var drawn) ? drawn : world;
        }

        /// <summary>Which way the source is moving, else the facing.</summary>
        Vector2 Travel(Source s)
        {
            if (s.Velocity.sqrMagnitude > 0.04f) return s.Velocity.normalized;
            var f = _facing != null ? _facing() : Vector2.right;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector2.right;
        }

        float GroundUnder(Source s) => s.Thrown ? s.Sr.transform.position.y - 0.3f * _scale : _owner.position.y;

        float H(int a, int b = 0) => FireArt.Hash(_serial * 7919 + a, b, 101);

        // ------------------------------------------------------------------ emitting

        void WindUp(Source src, float dt)
        {
            float bar = Bar;
            int si = IndexOf(src);
            if (_sparks)
            {
                // Sparks spit off the edge, more as the bar fills.
                float want = T.SparkRate * _k * bar * dt;
                for (int n = Take(0, want); n > 0; n--, _sparkCount++)
                {
                    var p = Point(src, 0.3f + H(_sparkCount, 2) * 0.7f, H(_sparkCount, 3) > 0.5f ? 1f : -1f);
                    float a = H(_sparkCount, 4) * Mathf.PI * 2f, sp = Mathf.Lerp(T.SparkSpeedMin, T.SparkSpeedMax, H(_sparkCount, 5));
                    Spawn(Kind.Spark, p, GroundUnder(src), new Vector2(Mathf.Cos(a) * sp, 0f), Mathf.Abs(Mathf.Sin(a)) * sp + 0.27f, T.Gravity * 1.15f, 0.6f, _sparkCount);
                }
            }
            if (_streaks)
            {
                float want = T.StreakRate * _k * bar * dt;
                for (int n = Take(1, want); n > 0; n--, _streakCount++)
                {
                    var b = new Bit { Kind = Kind.Dust, Born = Now, Life = T.StreakSeconds, Seed = _streakCount, Src = si,
                                      U = 0.3f + H(_streakCount, 12) * 0.7f, S = -1.1f + H(_streakCount, 13) * 2.2f };
                    b.Flung = true;   // a streak: drawn in toward its point (Paint)
                    _bits.Add(b);
                }
            }
            if (_grit)
            {
                int want = Mathf.RoundToInt(T.GritPebbles * _k * (_class == WeaponClass.Disc ? 1.5f : 1f));
                while (_gritCount < want && bar * 0.85f >= (_gritCount + H(_gritCount, 21)) / want)
                {
                    float u = 0.35f + H(_gritCount, 22) * 0.62f, side = (_gritCount % 2 == 0 ? 1f : -1f) * (1.9f + H(_gritCount, 23) * 0.8f);
                    var at = Point(src, u, Mathf.Clamp(side, -1f, 1f));
                    _bits.Add(new Bit { Kind = Kind.Pebble, Born = Now, Life = 99f, Seed = _gritCount, Src = si, U = u, S = side,
                                        Pos = new Vector2(at.x + (H(_gritCount, 24) - 0.5f) * 0.16f, GroundUnder(src) + (H(_gritCount, 25) - 0.5f) * 0.1f) });
                    _gritCount++;
                }
            }
            if (_crust)
            {
                // Stones rise off the floor to the arrow as the crust spreads along it.
                int want = Mathf.RoundToInt(T.CrustStones * _k);
                float cover = CrustCover();
                while (_crustCount < want && cover >= (_crustCount + 0.5f) / want)
                {
                    float u = (_crustCount + 0.5f) / want;
                    var at = Point(src, u, 0f);
                    _bits.Add(new Bit { Kind = Kind.Chunk, Born = Now, Life = 0.28f, Seed = _crustCount, Src = si, U = u,
                                        Pos = new Vector2(at.x + (H(_crustCount, 31) - 0.5f) * 0.3f, GroundUnder(src)) });
                    _crustCount++;
                }
            }
        }

        /// <summary>The strike: on a held weapon across its window; on a thrown thing along its flight.</summary>
        void Strike(float dt)
        {
            foreach (var src in _sources)
            {
                if (!Visible(src)) continue;
                float from, to;
                if (src.Thrown) { from = src.AdoptedAt; to = from + (src.Shape == Shape.Round ? T.ThrownDiscWindow : T.ThrownArrowWindow); }
                else { from = _strikeAt - T.StrikeBefore; to = _strikeAt + T.StrikeAfter; }
                if (Now < from || Now > to) continue;
                float share = dt / Mathf.Max(0.02f, to - from);
                var dir = Travel(src);
                int si = IndexOf(src);

                if (_sparks) Burst(src, 2, T.SparkBurst * _k * share, 33, (i, p) =>
                {
                    float sp = Mathf.Lerp(T.SprayMin, T.SprayMax, H(i, 34)), side = (H(i, 35) - 0.5f) * 0.55f;
                    Spawn(Kind.Spark, p, GroundUnder(src), dir * sp + Perp(dir) * side, Mathf.Max(0f, dir.y) * sp * 0.5f + 0.5f, T.Gravity * 1.6f, 3f, i, ember: true);
                }, 0.4f);
                if (_molten) Burst(src, 3, T.DriftEmbers * _k * share, 41, (i, p) =>
                    _bits.Add(new Bit { Kind = Kind.Drift, Pos = p, V = (dir * 0.2f + Vector2.up * T.DriftRise) * _scale, Born = Now, Life = T.DriftSeconds, Seed = i }), 0.25f);
                if (_film) Burst(src, 4, T.FilmDrops * _k * share, 51, (i, p) =>
                {
                    float sp = Mathf.Lerp(T.DropSpeedMin, T.DropSpeedMax, H(i, 52));
                    Spawn(Kind.Drop, p, GroundUnder(src), dir * sp, Mathf.Max(0f, dir.y) * sp * 0.45f + 0.45f, T.Gravity * 1.1f, 3f, i);
                    var b = _bits[_bits.Count - 1]; b.Film = true;
                    if (_even) AimAtLine(ref b);
                    _bits[_bits.Count - 1] = b;
                }, 0.3f);
                if (_ribbon)
                {
                    // The ribbon peels off along the tip's path: each sample hangs, then drops.
                    float want = T.RibbonSamples * (src.Thrown ? 1.5f : 1f) * (0.5f + 0.5f * _k) * share;
                    for (int n = Take(5, want); n > 0; n--)
                    {
                        int i = ++_burstCount;
                        var p = Point(src, src.Shape == Shape.Round ? 1f : 0.97f, src.Shape == Shape.Round ? H(i, 62) * 2f - 1f : 0f);
                        _bits.Add(new Bit { Kind = Kind.Drop, Pos = new Vector2(p.x, GroundUnder(src)), Z = p.y - GroundUnder(src),
                                            Born = Now, Life = 3f, Seed = i, Gravity = 0f,
                                            LandedAt = Now + Mathf.Lerp(T.RibbonHangMin, T.RibbonHangMax, H(i, 63)), Flung = false });
                    }
                }
                if (_streaks) _trail.Add((Point(src, src.Shape == Shape.Blade ? 0.95f : 0.5f, 0f), Now));
            }

            // Pebbles leave with the strike - from whichever source they hang by, the way it is going.
            if (_grit && Now >= _strikeAt - T.StrikeBefore)
                for (int i = 0; i < _bits.Count; i++)
                {
                    var b = _bits[i];
                    if (b.Kind != Kind.Pebble || b.Flung) continue;
                    var main = MainSource();
                    if (main == null) continue;
                    float at = _strikeAt + (b.Seed % 6) * 0.015f;
                    if (main.Thrown) at = main.AdoptedAt + (b.Seed % 6) * 0.03f;
                    if (Now < at) continue;
                    var from = PebbleAt(b);
                    var dir = Travel(main);
                    float sp = Mathf.Lerp(T.GritSpeedMin, T.GritSpeedMax, H(b.Seed, 71));
                    float ground = main.Thrown ? GroundUnder(main) : _owner.position.y + (H(b.Seed, 72) - 0.5f) * 0.12f;
                    b.Flung = true; b.Pos = new Vector2(from.x, ground); b.Z = Mathf.Max(0f, from.y - ground);
                    b.V = new Vector2(dir.x * sp, dir.y * sp * 0.15f) * _scale; b.Vz = (Mathf.Max(0f, dir.y) * sp * 0.4f + 0.33f) * _scale;
                    b.Gravity = T.Gravity * _scale; b.Born = Now; b.Life = 3f;
                    _bits[i] = b;
                }

            // The crust crumbles off the arrow after the loose, in chunks that fall.
            if (_crust && Since > T.CrustCrumbleDelay && Since < T.CrustCrumbleDelay + T.CrustCrumbleSeconds)
            {
                var src = MainSource();
                if (src != null) Burst(src, 6, T.CrustStones * _k * dt / T.CrustCrumbleSeconds, 81, (i, p) =>
                    Spawn(Kind.Chunk, p, GroundUnder(src), new Vector2((H(i, 82) - 0.5f) * 0.27f, 0f), 0.13f, T.Gravity, 3f, i), 0f);
            }
        }

        static Vector2 Perp(Vector2 d) => new(-d.y, d.x);

        void Burst(Source src, int emitter, float want, int salt, System.Action<int, Vector2> make, float uFrom)
        {
            for (int n = Take(emitter, want); n > 0; n--)
            {
                int i = ++_burstCount * 31 + salt;
                float u = uFrom + H(i, salt + 1) * (1f - uFrom);
                make(i, Point(src, u, src.Shape == Shape.Round ? H(i, salt + 2) * 2f - 1f : (H(i, salt + 3) - 0.5f) * 2f));
            }
        }

        /// <summary>A thing thrown off the weapon from screen point <paramref name="p"/>: on the ground
        /// under it at <paramref name="ground"/>, its height the rest.</summary>
        void Spawn(Kind kind, Vector2 p, float ground, Vector2 v, float vz, float gravity, float life, int seed, bool ember = false)
        {
            _bits.Add(new Bit
            {
                Kind = kind, Pos = new Vector2(p.x, ground), Z = Mathf.Max(0f, p.y - ground), V = v * _scale, Vz = vz * _scale,
                Gravity = gravity * _scale, Born = Now, Life = life, Seed = seed, Src = -1,
                Flung = ember,   // on a spark: it lands as an ember
            });
        }

        // ------------------------------------------------------------------ simulating

        void Simulate(float dt)
        {
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                float age = Now - b.Born;
                switch (b.Kind)
                {
                    case Kind.Spark:
                    case Kind.Drop:
                    case Kind.Chunk when b.Life > 1f:
                    case Kind.Pebble when b.Flung:
                        if (b.Kind == Kind.Drop && b.Gravity == 0f)
                        {
                            // a ribbon sample, hanging until it lets go
                            if (Now >= b.LandedAt) { b.Gravity = T.Gravity * 0.9f * _scale; b.V = new Vector2((H(b.Seed, 64) - 0.5f) * 0.2f, 0f) * _scale; }
                            break;
                        }
                        if (!b.Landed)
                        {
                            b.Pos += b.V * dt; b.Vz -= b.Gravity * dt; b.Z += b.Vz * dt;
                            if (b.Z <= 0f)
                            {
                                b.Z = 0f; b.Landed = true; b.LandedAt = Now;
                                // A spark from the strike lands as an ember; a drop as a wet mark.
                                if (b.Kind == Kind.Spark && !b.Flung) { _bits.RemoveAt(i); continue; }
                                if (b.Kind == Kind.Spark) { b.Kind = Kind.Ember; b.Life = T.EmberSeconds; b.Born = Now; }
                                else if (b.Kind == Kind.Drop) { if (b.Film && _even) b.Pos = b.Hang; b.Kind = Kind.Wet; b.Life = T.WetSeconds; b.Born = Now; }
                                else { b.Life = b.Kind == Kind.Pebble ? T.PebbleSeconds : 1.3f; b.Born = Now; }
                            }
                        }
                        break;
                    case Kind.Drift:
                        b.Pos += b.V * dt;
                        break;
                    case Kind.Pebble:
                        // Hanging: remember where, so a weapon that leaves the hand flings it from there.
                        if (b.Src >= 0 && b.Src < _sources.Count && Visible(_sources[b.Src])) b.Hang = PebbleAt(b);
                        else if (Now > _strikeAt + 1f) b.Life = 0f;
                        break;
                }
                if (Now - b.Born > b.Life) { _bits.RemoveAt(i); continue; }
                _bits[i] = b;
            }
            _trail.RemoveAll(x => Now - x.time > T.TrailSeconds);
        }

        Vector2 PebbleAt(Bit b)
        {
            var src = b.Src >= 0 && b.Src < _sources.Count ? _sources[b.Src] : null;
            if (src == null || !Visible(src)) return b.Hang != Vector2.zero ? b.Hang : b.Pos;
            float lift = Mathf.Clamp01((Now - b.Born) / T.GritLiftSeconds);
            float e = lift * lift * (3f - 2f * lift);
            var to = Point(src, b.U, Mathf.Clamp(b.S, -1f, 1f));
            // beside the weapon, past its edge, bobbing
            to += (Vector2)(src.Sr.transform.right * (b.S - Mathf.Clamp(b.S, -1f, 1f)) * src.Sr.sprite.bounds.extents.x * 0.45f * Mathf.Abs(src.Sr.transform.lossyScale.x));
            to.y += Mathf.Sin(Now * 8f + b.Seed * 2f) * 0.027f * _scale;
            return Vector2.Lerp(b.Pos, to, e);
        }

        float CrustCover() => Now < _strikeAt ? Bar * (0.55f + 0.45f * _k) * 1.08f : (0.55f + 0.45f * _k) * 1.08f;

        // ------------------------------------------------------------------ painting

        void Paint(Source main)
        {
            // The canvases centre on the player, or on the thrown thing once it is away.
            Vector2 centre = _owner.position + Vector3.up * 0.45f;
            if (main != null && main.Thrown) centre = Vector2.Lerp(centre, main.Sr.transform.position, 0.5f);
            Begin(_front, centre); Begin(_back, centre); Begin(_ground, new Vector2(centre.x, _owner.position.y));

            var fire = Tuning.FireFx.Ramp; var water = Tuning.ReleaseFx.Water; var stone = Tuning.ReleaseFx.Stone; var air = Tuning.ReleaseFx.Air;
            var wet = Tuning.ReleaseFx.Wet;

            if (_ribbon && main != null && Now < _strikeAt) PaintRibbon(main, water);

            foreach (var b in _bits)
            {
                float age = Now - b.Born, f = Mathf.Clamp01(age / Mathf.Max(0.01f, b.Life));
                Vector2 screen = b.Pos + Vector2.up * b.Z;
                switch (b.Kind)
                {
                    case Kind.Spark:
                        Dot(_front, screen, fire[age < 0.12f ? 0 : 1], 1f, 1);
                        break;
                    case Kind.Ember:
                        Dot(_ground, b.Pos, Ramp(fire, 0.35f + f * 0.65f), f < 0.7f ? 1f : (1f - f) / 0.3f, 1);
                        break;
                    case Kind.Drift:
                        Dot(_front, b.Pos + Vector2.right * Mathf.Sin(age * 9f + b.Seed) * 0.04f * _scale, Ramp(fire, 0.2f + f * 0.7f), 1f - f * f, 1);
                        break;
                    case Kind.Drop:
                        Dot(_front, screen, water[b.Seed % 2 == 0 ? 1 : 2], 1f, b.Gravity == 0f ? 2 : 1);
                        break;
                    case Kind.Wet:
                        if (age < 0.08f) { Dot(_ground, b.Pos + Vector2.left * 0.1f, water[0], 1f, 1); Dot(_ground, b.Pos + Vector2.right * 0.1f, water[1], 1f, 1); }
                        Dot(_ground, b.Pos, wet, 0.4f * (1f - Mathf.Clamp01((f - 0.4f) / 0.6f)), 1);
                        break;
                    case Kind.Pebble:
                    {
                        var col = stone[1 + b.Seed % 3];
                        if (!b.Flung) { Dot(_front, PebbleAt(b), col, 1f, 2); break; }
                        if (!b.Landed) { Dot(_front, screen, col, 1f, 2); break; }
                        if (age < 0.2f) { Dot(_ground, b.Pos + Vector2.left * 0.1f, stone[1], 0.5f * (1f - age / 0.2f), 1); Dot(_ground, b.Pos + Vector2.right * 0.1f, stone[1], 0.5f * (1f - age / 0.2f), 1); }
                        Dot(_ground, b.Pos, col, f < 0.7f ? 1f : (1f - f) / 0.3f, 1);
                        break;
                    }
                    case Kind.Chunk:
                        if (b.Life <= 1f)
                        {
                            // a stone rising off the floor to its place on the arrow
                            var src = b.Src >= 0 && b.Src < _sources.Count ? _sources[b.Src] : null;
                            if (src == null || !Visible(src)) break;
                            var to = Point(src, b.U, 0f);
                            float e = f * f;
                            var at = Vector2.Lerp(b.Pos, to, e) + Vector2.up * Mathf.Sin(f * Mathf.PI) * 0.13f * _scale;
                            Dot(_front, at, stone[2], 1f, 2);
                            break;
                        }
                        if (!b.Landed) { Dot(_front, screen, stone[2], 1f, 2); break; }
                        Dot(_ground, b.Pos, stone[3], f < 0.7f ? 1f : (1f - f) / 0.3f, 1);
                        break;
                    case Kind.Dust when b.Flung:
                    {
                        // a gathering streak, drawn in toward its point on the weapon
                        var src = b.Src >= 0 && b.Src < _sources.Count ? _sources[b.Src] : null;
                        if (src == null || !Visible(src)) break;
                        var tgt = Point(src, b.U, 0f);
                        float d0 = T.StreakReach * _scale * (0.6f + 0.4f * H(b.Seed, 14)), d = d0 * (1f - f * f) + 0.04f * _scale;
                        var dir = new Vector2(Mathf.Cos(b.S), Mathf.Sin(b.S));
                        if (_facing != null && _facing().x < 0f) dir.x = -dir.x;
                        int len = 4 + (int)(H(b.Seed, 15) * 3f);
                        for (int k = 0; k < len; k++)
                            Dot(_front, tgt + dir * (d + k * 1.5f * _scale / WeaponPpu), air[k == 0 ? 1 : 2], (1f - f) * 0.85f * (1f - (float)k / len), 1);
                        break;
                    }
                }
            }

            // The strike's path, in broken pale streaks that thin out.
            for (int i = 0; i < _trail.Count; i++)
            {
                if (i % 3 == 2) continue;
                float age = (Now - _trail[i].time) / T.TrailSeconds;
                Dot(_front, _trail[i].pos, air[age < 0.3f ? 0 : 1], (1f - age) * 0.9f, 1);
                if (i > 0) Dot(_front, Vector2.Lerp(_trail[i - 1].pos, _trail[i].pos, 0.5f), air[1], (1f - age) * 0.7f, 1);
            }

            int order = main != null ? main.Sr.sortingOrder : SortingOrders.Fx;
            int layer = main != null ? main.Sr.sortingLayerID : 0;
            End(_front, order + 1, layer); End(_back, order - 1, layer); End(_ground, SortingOrders.GroundFx, 0);
        }

        /// <summary>The helix while the bar fills: in front of the weapon where it passes in front.</summary>
        void PaintRibbon(Source src, Color32[] water)
        {
            float top = 0.05f + 0.95f * Bar * (0.6f + 0.4f * _k);
            if (src.Shape == Shape.Round)
            {
                // round a disc: a ring past its rim, turning
                for (float a = 0f; a < 1f; a += 0.02f)
                {
                    if (a > top) break;
                    float ph = a * Mathf.PI * 2f + Now * T.RibbonSpin * 0.5f, cz = Mathf.Sin(ph * 2f);
                    var p = (Vector2)src.Sr.bounds.center + new Vector2(Mathf.Cos(ph), Mathf.Sin(ph) * 0.55f) * src.Sr.bounds.extents.x * 1.25f;
                    Dot(cz > 0f ? _front : _back, p, water[cz > 0.6f ? 1 : 2], 1f, 1);
                }
                return;
            }
            for (float u = 0.05f; u < top; u += 0.012f)
            {
                float ph = u * T.RibbonTurns * 2f - Now * T.RibbonSpin, cz = Mathf.Cos(ph);
                var p = Point(src, u, 0f);
                var across = (Vector2)(src.Shape == Shape.Shaft ? src.Sr.transform.up : src.Sr.transform.right);
                float width = (src.Shape == Shape.Shaft ? src.Sr.sprite.bounds.extents.y * 2.2f : src.Sr.sprite.bounds.extents.x * 0.7f)
                              * Mathf.Abs(src.Sr.transform.lossyScale.x);
                Dot(cz > 0f ? _front : _back, p + across.normalized * Mathf.Sin(ph) * width, water[cz > 0.6f ? 1 : 2], 1f, 1);
            }
        }

        static Color32 Ramp(Color32[] r, float f)
        {
            float i = Mathf.Clamp(f, 0f, 0.999f) * (r.Length - 1);
            int a = Mathf.FloorToInt(i);
            return Color32.Lerp(r[a], r[Mathf.Min(r.Length - 1, a + 1)], i - a);
        }

        void Begin(Sheet c, Vector2 centre)
        {
            c.Cx = Mathf.RoundToInt(centre.x * c.Ppu); c.Cy = Mathf.RoundToInt(centre.y * c.Ppu);
            if (c.WasDrawn) System.Array.Clear(c.Px, 0, c.Px.Length);
            c.Drawn = false;
        }

        /// <summary>One texel (or a square of <paramref name="size"/>) at a world point.</summary>
        static void Dot(Sheet c, Vector2 world, Color32 col, float alpha, int size)
        {
            if (alpha <= 0.01f) return;
            int x = Mathf.FloorToInt(world.x * c.Ppu) - c.Cx + c.Size / 2, y = Mathf.FloorToInt(world.y * c.Ppu) - c.Cy + c.Size / 2;
            col.a = (byte)(Mathf.Clamp01(alpha) * 255f);
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= c.Size || yy >= c.Size) continue;
                    c.Px[yy * c.Size + xx] = col;
                    c.Drawn = true;
                }
        }

        void End(Sheet c, int order, int layer)
        {
            c.WasDrawn = c.Drawn;
            if (!c.Drawn) { c.Sr.enabled = false; return; }
            c.Tex.SetPixels32(c.Px);
            c.Tex.Apply();
            c.Sr.transform.position = new Vector3((float)c.Cx / c.Ppu, (float)c.Cy / c.Ppu, 0f);
            c.Sr.sortingLayerID = layer;
            c.Sr.sortingOrder = order;
            c.Sr.enabled = true;
        }

        // ------------------------------------------------------------------ the weapon's own texels

        void PaintOverlay(Source s)
        {
            bool wants = (_molten || _film || _crust) && Visible(s);
            if (!wants) { if (s.Overlay?.Sr != null) s.Overlay.Sr.enabled = false; return; }
            var o = EnsureOverlay(s);
            if (o == null) return;
            var fire = Tuning.FireFx.Ramp; var water = Tuning.ReleaseFx.Water; var stone = Tuning.ReleaseFx.Stone;
            float bar = Bar, since = Since;
            float molten = Now < _strikeAt ? bar * (0.55f + 0.45f * _k) * 1.1f : (0.55f + 0.45f * _k) * 1.1f;
            float cool = Now < _strikeAt ? 0f : Mathf.Clamp01((since - T.MoltenCoolDelay) / T.MoltenCoolSeconds);
            // the film is thrown off with the strike / the throw
            float filmAmt = Now < _strikeAt ? bar : 1f - Mathf.Clamp01((Now - (s.Thrown ? s.AdoptedAt : _strikeAt)) / 0.25f);
            float cover = CrustCover();
            float crumble = since < T.CrustCrumbleDelay ? 0f : Mathf.Clamp01((since - T.CrustCrumbleDelay) / T.CrustCrumbleSeconds);
            int frame = Mathf.FloorToInt(Now * 14f);
            bool any = false;
            for (int i = 0; i < o.Px.Length; i++)
            {
                o.Px[i] = default;
                byte kind = o.Kind[i];
                if (kind < 2) continue;
                int x = i % o.W, y = i / o.W;
                float u = o.U[i];
                if (_crust && u <= cover)
                {
                    float cell = FireArt.Hash(x >> 2, y >> 2, _serial);
                    if (cell >= 0.18f && cell >= crumble)
                    {
                        int ix = 1 + Mathf.FloorToInt(FireArt.Hash(x >> 2, y >> 2, _serial + 1) * 2.2f) + (kind == 3 ? 1 : 0) + (o.Lum[i] < 0.45f ? 1 : 0);
                        o.Px[i] = stone[Mathf.Min(5, ix)]; any = true; continue;
                    }
                }
                if (_molten && cool < 1f)
                {
                    if (kind == 3 && u <= molten)
                    {
                        float flick = FireArt.Hash(x, y, frame);
                        int ix = flick < 0.25f ? 1 : 2;
                        o.Px[i] = Ramp(fire, Mathf.Clamp01((ix + cool * 3.5f) / 5f)); any = true; continue;
                    }
                    if (kind == 2 && u <= molten) { var c = fire[4]; c.a = (byte)(36f * (1f - cool)); o.Px[i] = c; any = true; continue; }
                }
                if (_film && filmAmt > 0f)
                {
                    float band = Mathf.Repeat(u * 3f - Now * 2.2f, 1f);
                    Color32 c; float a;
                    if (band < 0.12f * _k + 0.04f) { c = water[1]; a = 0.8f; }
                    else if (kind == 3) { c = water[2]; a = 0.55f; }
                    else { c = water[3]; a = 0.3f * _k; }
                    c.a = (byte)(255f * a * filmAmt); o.Px[i] = c; any = true;
                }
            }
            o.Tex.SetPixels32(o.Px);
            o.Tex.Apply();
            o.Sr.enabled = any && s.Sr.enabled;   // a weapon the eight-direction body draws: it lays this over its own card
            o.Sr.flipX = s.Sr.flipX; o.Sr.flipY = s.Sr.flipY;
            o.Sr.sortingLayerID = s.Sr.sortingLayerID;
            o.Sr.sortingOrder = s.Sr.sortingOrder + 1;
        }

        Overlay EnsureOverlay(Source s)
        {
            var sprite = s.Sr.sprite;
            if (s.Overlay != null && s.Overlay.Of == sprite) return s.Overlay;
            DropOverlay(s);
            var rect = sprite.textureRect;
            int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
            Color32[] src;
            try
            {
                var all = sprite.texture.GetPixels32();
                src = new Color32[w * h];
                int tw = sprite.texture.width, x0 = Mathf.RoundToInt(rect.x), y0 = Mathf.RoundToInt(rect.y);
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) src[y * w + x] = all[(y0 + y) * tw + x0 + x];
            }
            catch (UnityException) { return null; }   // not readable: no overlay, the particles still run

            var o = new Overlay { Of = sprite, W = w, H = h, Kind = new byte[w * h], U = new float[w * h], Lum = new float[w * h], Px = new Color32[w * h] };
            var pivot = sprite.pivot;
            float radius = Mathf.Min(w, h) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = src[i];
                    if (c.a < 128) continue;
                    float lum = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
                    o.Lum[i] = lum;
                    o.Kind[i] = lum < 0.1f ? (byte)1 : (byte)2;
                    o.U[i] = s.Shape switch
                    {
                        Shape.Round => Mathf.Clamp01(Mathf.Sqrt((x + 0.5f - w * 0.5f) * (x + 0.5f - w * 0.5f) + (y + 0.5f - h * 0.5f) * (y + 0.5f - h * 0.5f)) / radius),
                        Shape.Shaft => Mathf.Clamp01((x + 0.5f) / w),
                        _ => Mathf.Clamp01((y + 0.5f - pivot.y - (h - pivot.y) * 0.2f) / Mathf.Max(1f, (h - pivot.y) * 0.8f)),
                    };
                }
            // the edge: body texels touching the outline or the outside
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (o.Kind[i] != 2) continue;
                    bool edge = s.Shape == Shape.Round ? o.U[i] > 0.85f
                        : Outside(o, x - 1, y) || Outside(o, x + 1, y) || (s.Shape == Shape.Shaft && (Outside(o, x, y - 1) || Outside(o, x, y + 1)));
                    if (edge) o.Kind[i] = 3;
                    if (s.Shape == Shape.Blade && o.U[i] <= 0f) o.Kind[i] = 1;   // the hilt stays as it is
                }
            o.Tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var go = new GameObject("element-overlay");
            go.transform.SetParent(s.Sr.transform, false);
            o.Sr = go.AddComponent<SpriteRenderer>();
            o.Sr.sprite = Sprite.Create(o.Tex, new Rect(0, 0, w, h), new Vector2(pivot.x / w, pivot.y / h), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            o.Sr.sharedMaterial = s.Sr.sharedMaterial;
            s.Overlay = o;
            return o;
        }

        static bool Outside(Overlay o, int x, int y) => x < 0 || y < 0 || x >= o.W || y >= o.H || o.Kind[y * o.W + x] <= 1;

        static void DropOverlay(Source s)
        {
            var o = s.Overlay;
            if (o == null) return;
            if (o.Sr != null) { if (o.Sr.sprite != null) Destroy(o.Sr.sprite); Destroy(o.Sr.gameObject); }
            if (o.Tex != null) Destroy(o.Tex);
            s.Overlay = null;
        }
    }
}
