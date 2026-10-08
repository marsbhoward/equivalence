using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>Which icon a move shows in the HUD.</summary>
    public enum Glyph
    {
        Basic,      // generic swing
        Overhand,   // the default finisher
        Whirl,
        Volley,
        Spike,
        Burst,
        Vortex,
        Cross,
        Thrown,
        Slam,
        Comet,

        /// <summary>
        /// A spray widening away from a point. Appended rather than inserted - the enum is read
        /// by the HUD and by every moveset declaration, and inserting a value would silently
        /// re-point all of them.
        /// </summary>
        Fan,

        /// <summary>
        /// One figure with smaller copies of itself ringing it. Appended, for the reason above.
        /// </summary>
        Echoes,

        /// <summary>A rounded head over three pointed tails - Phantom's own signature.
        /// Appended, for the reason above.</summary>
        Phantom,

        /// <summary>A droplet - Blood Blade's own signature. Appended, for the reason above.</summary>
        Blood,

        /// <summary>A diagonal stroke crossing a short scabbard bar - Zanmato's draw-cut.
        /// Appended, for the reason above.</summary>
        Katana,

        /// <summary>Three blades thrown outward from one point - Tria Prima's Separatio.
        /// Appended, for the reason above.</summary>
        Separatio,

        /// <summary>Four cones round one point, widest at the middle - the Armillary's
        /// Quintessence. Appended, for the reason above.</summary>
        Quintessence,

        /// <summary>A crescent flying right with a small stone behind it - the Magnum Opus's shot.
        /// Appended, for the reason above.</summary>
        MagnumOpus,

        /// <summary>A boomerang's chevron over a short arrow - the Prima Materia art. Appended, for the
        /// reason above.</summary>
        PrimaMateria,

        /// <summary>Two arcs closing like pincers on a point - the King and Queen art. Appended, for the
        /// reason above.</summary>
        KingAndQueen,

        /// <summary>A level blade driving right with speed lines behind it - the Impale art, the
        /// body lunging with the thrust (Spike is Piercing Lunge's standing thrust). Appended, for
        /// the reason above.</summary>
        Impale,
    }

    /// <summary>
    /// Procedural move icons, drawn from primitives for the same reason the rest of the art is:
    /// the game has to read correctly before any icon set exists. Each glyph is a distinct
    /// silhouette so a player can tell finishers apart at a glance rather than reading text.
    ///
    /// Shapes are defined as plain inside/outside predicates and supersampled 3x3, which keeps
    /// the definitions trivial while still giving smooth edges.
    /// </summary>
    public static class Glyphs
    {
        static readonly Dictionary<Glyph, Sprite> _cache = new();

        public static Sprite Get(Glyph glyph)
        {
            if (_cache.TryGetValue(glyph, out var cached) && cached != null) return cached;
            var sprite = Build(glyph);
            _cache[glyph] = sprite;
            return sprite;
        }

        static Sprite Build(Glyph glyph) => glyph switch
        {
            Glyph.Basic    => Render((x, y) => RotRect(x, y, 0f, 0f, 0.72f, 0.11f, 45f)),
            Glyph.Overhand => Render((x, y) =>
                                 Rect(x, y, 0f, 0.42f, 0.13f, 0.42f) ||        // haft
                                 DownWedge(x, y, -0.15f, -0.85f, 0.46f)),      // head
            Glyph.Whirl    => Render((x, y) =>
                                 Ring(x, y, 0.60f, 0.13f) && !(x > 0.25f && y > 0.25f) ||
                                 RotRect(x, y, 0.42f, 0.42f, 0.34f, 0.10f, 45f)),
            Glyph.Volley   => Render((x, y) =>
                                 Disc(x, y, -0.52f, 0f, 0.20f) ||
                                 Disc(x, y,  0.00f, 0f, 0.20f) ||
                                 Disc(x, y,  0.52f, 0f, 0.20f)),
            Glyph.Spike    => Render((x, y) => RightWedge(x, y, -0.78f, 0.86f, 0.27f)),
            Glyph.Burst    => Render((x, y) =>
                             {
                                 for (int i = 0; i < 4; i++)
                                     if (RotRect(x, y, 0f, 0f, 0.86f, 0.085f, i * 45f)) return true;
                                 return false;
                             }),
            // Four wedges aimed at a centre dot: the silhouette has to say "inward" at a glance,
            // because this is the one finisher that moves enemies toward you rather than away.
            Glyph.Vortex   => Render((x, y) =>
                             {
                                 if (Disc(x, y, 0f, 0f, 0.19f)) return true;
                                 for (int i = 0; i < 4; i++)
                                     if (WedgeAt(x, y, i * 90f, 0.95f, 0.34f, 0.30f)) return true;
                                 return false;
                             }),
            // A blade leaving the hand: the bar flying clear, with its trail behind it.
            Glyph.Thrown   => Render((x, y) =>
                                 RotRect(x, y,  0.16f,  0.16f, 0.52f, 0.11f, 45f) ||
                                 RotRect(x, y, -0.46f, -0.46f, 0.20f, 0.055f, 45f) ||
                                 RotRect(x, y, -0.74f, -0.74f, 0.11f, 0.055f, 45f)),
            // A heavy head driven down onto a struck line: the silhouette has to say "downward",
            // because this is the one finisher the player commits a second and a half to.
            Glyph.Slam     => Render((x, y) =>
                                 Rect(x, y, 0f, 0.62f, 0.10f, 0.34f) ||          // haft
                                 DownWedge(x, y, 0.30f, -0.34f, 0.52f) ||        // head
                                 Rect(x, y, 0f, -0.62f, 0.66f, 0.075f) ||        // impact line
                                 Rect(x, y, -0.52f, -0.86f, 0.16f, 0.06f) ||
                                 Rect(x, y,  0.52f, -0.86f, 0.16f, 0.06f)),
            // A body falling into a ring on the ground. The ring is the important half: this is
            // the one finisher whose damage is a marked circle on the floor rather than an arc
            // in front of the character, and the icon has to say "here, underneath you".
            Glyph.Comet    => Render((x, y) =>
                                 Ring(x, y, 0.62f, 0.10f) && y < 0.05f ||        // landing circle
                                 DownWedge(x, y, 0.86f, 0.02f, 0.30f) ||        // body, falling in
                                 Rect(x, y, -0.30f, 0.80f, 0.055f, 0.20f) ||    // motion streaks
                                 Rect(x, y,  0.30f, 0.80f, 0.055f, 0.20f)),
            // Three lines fanning out from a point at the bottom, widening upward. Drawn open
            // rather than as a filled wedge so it reads as SPRAY and not as a solid triangle,
            // which the element sigils already own.
            Glyph.Fan      => Render((x, y) =>
                                 RotRect(x, y, 0f, 0.02f, 0.72f, 0.075f, 90f) ||
                                 RotRect(x, y, -0.30f, -0.06f, 0.62f, 0.070f, 68f) ||
                                 RotRect(x, y,  0.30f, -0.06f, 0.62f, 0.070f, 112f) ||
                                 Disc(x, y, 0f, -0.78f, 0.13f)),

            // One upright bar with three smaller ones set around it at 120 degrees. The only
            // thing this icon has to say is THERE ARE MORE OF YOU, so the copies have to be the
            // same shape as the middle and visibly smaller - a ring of unrelated marks reads as a
            // blast, which Burst already owns.
            Glyph.Echoes   => Render((x, y) =>
                             {
                                 if (Rect(x, y, 0f, 0f, 0.115f, 0.34f)) return true;
                                 for (int i = 0; i < 3; i++)
                                 {
                                     // Out at 0.72, not 0.60. At the closer radius the copy
                                     // directly above ended five hundredths of a unit off the
                                     // main bar's top and the two read as one tall stack rather
                                     // than as a figure with something standing near it.
                                     float a = (90f + i * 120f) * Mathf.Deg2Rad;
                                     if (Rect(x, y, Mathf.Cos(a) * 0.72f, Mathf.Sin(a) * 0.72f,
                                              0.070f, 0.17f)) return true;
                                 }
                                 return false;
                             }),

            // A rounded head over three pointed tails - the plainest possible ghost
            // silhouette, and deliberately not fussier than that: the finisher's own colour
            // (purple haze) is what actually carries "Phantom" in the HUD, the icon just has to
            // say "ghost" at a glance.
            Glyph.Phantom  => Render((x, y) =>
                                 Disc(x, y, 0f, 0.08f, 0.58f) && y > -0.05f ||
                                 DownWedge(x + 0.34f, y, -0.05f, -0.68f, 0.19f) ||
                                 DownWedge(x,          y, -0.05f, -0.78f, 0.21f) ||
                                 DownWedge(x - 0.34f,  y, -0.05f, -0.68f, 0.19f)),

            // A droplet - a round base with a point tapering up out of it. Built as an
            // inline taper rather than reusing DownWedge, which only points DOWNWARD; flipping
            // its arguments for an upward point reads less clearly than just writing the test.
            Glyph.Blood    => Render((x, y) =>
                                 Disc(x, y, 0f, -0.22f, 0.44f) ||
                                 (y >= -0.22f && y <= 0.82f &&
                                  Mathf.Abs(x) <= 0.44f * (1f - (y + 0.22f) / 1.04f))),

            // A long diagonal draw-stroke crossing a short scabbard bar set low. The stroke has
            // to be the loud half - the sequence is all about the one cut - so it runs corner to
            // corner while the saya is a stub it passes through.
            Glyph.Katana   => Render((x, y) =>
                                 RotRect(x, y,  0.06f,  0.06f, 1.02f, 0.10f, 52f) ||   // draw-cut
                                 RotRect(x, y, -0.24f, -0.40f, 0.50f, 0.12f, 18f)),    // scabbard

            // Three blades flung out from one centre, a third of a turn apart: one figure became
            // three and each went its own way. The gap at the middle is the point - they have
            // LEFT it, so nothing is drawn joining them.
            Glyph.Separatio => Render((x, y) =>
                             {
                                 for (int i = 0; i < 3; i++)
                                 {
                                     float a = (90f + i * 120f) * Mathf.Deg2Rad;
                                     float cx = Mathf.Cos(a) * 0.52f, cy = Mathf.Sin(a) * 0.52f;
                                     if (RotRect(x, y, cx, cy, 0.36f, 0.11f, 90f + i * 120f)) return true;
                                 }
                                 return Disc(x, y, 0f, 0f, 0.13f);
                             }),

            // Four cones on the four sides, each widest at the middle and pointing out - a
            // four-pointed star. What the finisher does, drawn flat.
            Glyph.Quintessence => Render((x, y) =>
                             {
                                 float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                                 bool horizontal = ax <= 0.86f && ay <= 0.30f * (1f - ax / 0.86f);
                                 bool vertical = ay <= 0.86f && ax <= 0.30f * (1f - ay / 0.86f);
                                 return horizontal || vertical;
                             }),

            // The crescent, bowed toward its flight, and the stone it came from behind it.
            Glyph.MagnumOpus => Render((x, y) =>
                                 (Disc(x, y, 0.10f, 0f, 0.82f) && !Disc(x, y, -0.22f, 0f, 0.82f) && x > -0.2f) ||
                                 (Mathf.Abs(x + 0.55f) / 0.13f + Mathf.Abs(y) / 0.32f <= 1f)),

            // The thrown bow's chevron, and the arrow it comes back to loose.
            Glyph.PrimaMateria => Render((x, y) =>
                                 RotRect(x, y, -0.22f, 0.28f, 0.62f, 0.13f, 32f) ||
                                 RotRect(x, y, 0.22f, 0.28f, 0.62f, 0.13f, -32f) ||
                                 RotRect(x, y, 0f, -0.42f, 0.72f, 0.09f, 0f) ||
                                 (x > 0.22f && x < 0.46f && Mathf.Abs(y + 0.42f) < (0.46f - x) * 0.9f)),

            // The two halves' arcs closing on the target between them.
            Glyph.KingAndQueen => Render((x, y) =>
                             {
                                 bool left = x < 0f && Mathf.Abs(Mathf.Sqrt((x + 0.55f) * (x + 0.55f) + (y + 0.1f) * (y + 0.1f)) - 0.62f) < 0.1f && y < 0.55f && x > -1f;
                                 bool right = x > 0f && Mathf.Abs(Mathf.Sqrt((x - 0.55f) * (x - 0.55f) + (y + 0.1f) * (y + 0.1f)) - 0.62f) < 0.1f && y < 0.55f && x < 1f;
                                 return left || right || Disc(x, y, 0f, 0.5f, 0.16f);
                             }),

            // A level blade driving right out of three speed lines: the thrust AND the body
            // travelling with it, so it can't be read as Piercing Lunge's standing Spike.
            Glyph.Impale   => Render((x, y) =>
                                 Rect(x, y, 0.10f, 0f, 0.42f, 0.085f) ||           // blade
                                 RightWedge(x, y, 0.50f, 0.92f, 0.17f) ||          // point
                                 Rect(x, y, -0.62f,  0.30f, 0.20f, 0.05f) ||       // speed lines
                                 Rect(x, y, -0.70f,  0f,    0.16f, 0.05f) ||
                                 Rect(x, y, -0.62f, -0.30f, 0.20f, 0.05f)),

            _              => Render((x, y) =>
                                 RotRect(x, y, 0f, 0f, 0.80f, 0.12f, 45f) ||
                                 RotRect(x, y, 0f, 0f, 0.80f, 0.12f, -45f)),
        };

        // ---------------------------------------------------------------- element sigils

        static readonly Dictionary<Core.ElementType, Sprite> _sigils = new();

        /// <summary>
        /// The mark for one element, drawn white so the caller tints it with
        /// <see cref="Core.ElementInfo.Tint"/>.
        ///
        /// These are the classical alchemical four - fire an upward triangle, water a downward
        /// one, air and earth the same two crossed by a bar. Chosen over invented shapes for one
        /// reason: they already are a SET. Four unrelated icons make the player learn four
        /// things; these make them learn one rule (up is rising, down is falling, the bar is the
        /// heavier half of each pair) and read the fourth for free. They are also the shapes a
        /// player is most likely to have met before.
        ///
        /// Drawn as outlines rather than solids so they read as carved into the wall above a
        /// doorway rather than as a filled UI icon stuck on it.
        /// </summary>
        public static Sprite Element(Core.ElementType element)
        {
            if (_sigils.TryGetValue(element, out var cached) && cached != null) return cached;
            var sprite = BuildSigil(element);
            _sigils[element] = sprite;
            return sprite;
        }

        // Outer and inner triangles. The inner is the hole, so outer && !inner is the stroke.
        // Insetting is done by moving all three edges in rather than by scaling about the
        // centroid, which would thin the stroke at the apex and fatten it at the base.
        static bool TriUp(float x, float y, bool inner) => inner
            ? WedgeAt(x, y, 180f, 0.60f, -0.50f, 0.62f)
            : WedgeAt(x, y, 180f, 0.85f, -0.85f, 0.92f);

        static bool TriDown(float x, float y, bool inner) => inner
            ? DownWedge(x, y, 0.60f, -0.50f, 0.62f)
            : DownWedge(x, y, 0.85f, -0.85f, 0.92f);

        static Sprite BuildSigil(Core.ElementType element) => element switch
        {
            Core.ElementType.Fire  => Render((x, y) => TriUp(x, y, false) && !TriUp(x, y, true)),

            Core.ElementType.Water => Render((x, y) => TriDown(x, y, false) && !TriDown(x, y, true)),

            // The bar spans the triangle at the height it sits at, so it reads as crossing the
            // shape rather than as a separate dash laid over it.
            Core.ElementType.Air   => Render((x, y) =>
                                         (TriUp(x, y, false) && !TriUp(x, y, true)) ||
                                         (Rect(x, y, 0f, 0.20f, 0.355f, 0.075f) && TriUp(x, y, false))),

            _                      => Render((x, y) =>
                                         (TriDown(x, y, false) && !TriDown(x, y, true)) ||
                                         (Rect(x, y, 0f, -0.20f, 0.355f, 0.075f) && TriDown(x, y, false))),
        };

        // ---------------------------------------------------------------- the tria prima

        /// <summary>The three alchemical principles, used by the transmutation circle.</summary>
        public enum PrimeMark { Sulfur, Mercury, Salt }

        static readonly Dictionary<PrimeMark, Sprite> _prima = new();

        /// <summary>
        /// Sulfur, mercury and salt, drawn white for the caller to tint.
        ///
        /// The historical marks rather than invented ones, for the same reason the element sigils
        /// are the alchemical four: they are a set with an internal logic, and a player who
        /// recognises one has a good chance of reading the other two.
        /// </summary>
        public static Sprite Prime(PrimeMark mark)
        {
            if (_prima.TryGetValue(mark, out var cached) && cached != null) return cached;
            var sprite = BuildPrime(mark);
            _prima[mark] = sprite;
            return sprite;
        }

        static Sprite BuildPrime(PrimeMark mark) => mark switch
        {
            // Triangle over a cross.
            PrimeMark.Sulfur => Render((x, y) =>
                                   (WedgeAt(x, y, 180f, -0.02f, -0.96f, 0.54f) &&
                                    !WedgeAt(x, y, 180f, -0.22f, -0.72f, 0.32f)) ||
                                   Rect(x, y, 0f, -0.55f, 0.075f, 0.41f) ||
                                   Rect(x, y, 0f, -0.78f, 0.30f, 0.075f)),

            // Horns over a circle over a cross.
            // The HORNS are what separate mercury from venus, so they are drawn deliberately wide
            // and thick - at the size this renders on a floor, a thin arc reads as a single curl
            // hanging off one side and the mark stops being recognisable.
            PrimeMark.Mercury => Render((x, y) =>
                                    (Disc(x, y, 0f, 0.02f, 0.38f) && !Disc(x, y, 0f, 0.02f, 0.26f)) ||
                                    (Disc(x, y, 0f, 0.52f, 0.44f) && !Disc(x, y, 0f, 0.52f, 0.30f)
                                       && y > 0.50f) ||
                                    Rect(x, y, 0f, -0.66f, 0.075f, 0.26f) ||
                                    Rect(x, y, 0f, -0.66f, 0.28f, 0.075f)),

            // A circle cut in half.
            _                => Render((x, y) =>
                                    Ring(x, y, 0.66f, 0.13f) ||
                                    Rect(x, y, 0f, 0f, 0.66f, 0.065f)),
        };

        // ---------------------------------------------------------------- relic marks

        /// <summary>The seal a black-diamond relic is known by - design in progress.</summary>
        public enum RelicMark { Shadow }

        static readonly Dictionary<RelicMark, Sprite> _relics = new();

        /// <summary>Drawn white for the caller to tint, same as every other mark here.</summary>
        public static Sprite Relic(RelicMark mark)
        {
            if (_relics.TryGetValue(mark, out var cached) && cached != null) return cached;
            var sprite = BuildRelic(mark);
            _relics[mark] = sprite;
            return sprite;
        }

        static Sprite BuildRelic(RelicMark mark) => mark switch
        {
            // A circle housing a triangle housing a square housing a circle - the classical
            // "squaring the circle" seal. Every shape is INSCRIBED in the one around it and
            // touches it exactly, per the reference: the triangle's three vertices sit ON the
            // circle, the square sits on the triangle's base with its top two corners touching
            // the slanted sides, and the inner circle is inscribed in the square. Nothing is a
            // guessed gap - each position is the actual geometry solution, not an eyeballed
            // inset, which is what makes the shapes read as touching rather than floating
            // near each other.
            //
            // The triangle is three explicit edges rather than a hollowed wedge pair - that
            // gave a clean stroke on the two slanted sides but a thick solid band across the
            // base, because the inner and outer wedges don't reach their flat edge at the same
            // rate a slanted edge does.
            RelicMark.Shadow => Render((x, y) =>
            {
                const float stroke = 0.045f;

                // Triangle inscribed in the R=0.90 circle, apex up: standard equilateral
                // vertices at 90/210/330 degrees.
                const float r = 0.90f;
                const float apexY = r;                     // cos/sin(90) * r
                const float baseY = -r * 0.5f;              // sin(210 or 330) * r
                const float baseHalf = 0.7794f;             // r * sqrt(3)/2 - cos(330) * r
                float edgeLen = Mathf.Sqrt(baseHalf * baseHalf + (apexY - baseY) * (apexY - baseY));
                float edgeDeg = Mathf.Atan2(baseY - apexY, -baseHalf) * Mathf.Rad2Deg;
                bool triangle =
                    RotRect(x, y, -baseHalf * 0.5f, (apexY + baseY) * 0.5f, edgeLen * 0.5f, stroke, edgeDeg) ||
                    RotRect(x, y, baseHalf * 0.5f, (apexY + baseY) * 0.5f, edgeLen * 0.5f, stroke, -edgeDeg) ||
                    Rect(x, y, 0f, baseY, baseHalf, stroke);

                // Largest square standing on the triangle's base: side = 2*b*h / (2*b + h) for
                // a base half-width b and apex height h above the base - the classic
                // inscribed-square-in-a-triangle result, not tuned by eye.
                float h = apexY - baseY;
                float side = (2f * baseHalf * h) / (2f * baseHalf + h);
                float half = side * 0.5f;
                float squareCy = baseY + half;
                bool square = Rect(x, y, 0f, squareCy, half, half) &&
                              !Rect(x, y, 0f, squareCy, half - stroke, half - stroke);

                // Circle inscribed in the square, touching all four sides - Ring measures from
                // the origin, so the offset centre is applied by hand here instead.
                float dist = Mathf.Sqrt(x * x + (y - squareCy) * (y - squareCy));
                bool core = dist <= half + stroke && dist >= half - stroke;

                return Ring(x, y, r, stroke * 2f) || triangle || square || core;
            }),

            _ => Render((x, y) => Ring(x, y, 0.85f, 0.10f)),
        };

        // ---------------------------------------------------------------- primitives
        //
        // Internal rather than private: these are the project's shape vocabulary, and the
        // exchange icons are composed from the same set. Duplicating them for a second icon
        // family is how two icon sets end up with different line weights.

        internal static bool Rect(float x, float y, float cx, float cy, float hw, float hh)
            => Mathf.Abs(x - cx) <= hw && Mathf.Abs(y - cy) <= hh;

        internal static bool RotRect(float x, float y, float cx, float cy, float hw, float hh, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(-r), s = Mathf.Sin(-r);
            float dx = x - cx, dy = y - cy;
            return Mathf.Abs(dx * c - dy * s) <= hw && Mathf.Abs(dx * s + dy * c) <= hh;
        }

        internal static bool Disc(float x, float y, float cx, float cy, float r)
            => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        internal static bool Ring(float x, float y, float r, float thick)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return d <= r + thick * 0.5f && d >= r - thick * 0.5f;
        }

        /// <summary>Triangle widest at yTop, narrowing to a point at yTip.</summary>
        internal static bool DownWedge(float x, float y, float yTop, float yTip, float halfWidth)
        {
            if (y > yTop || y < yTip) return false;
            float t = (yTop - y) / (yTop - yTip);
            return Mathf.Abs(x) <= halfWidth * (1f - t);
        }

        /// <summary>A <see cref="DownWedge"/> rotated about the origin, so its point can aim any way.</summary>
        internal static bool WedgeAt(float x, float y, float deg, float yTop, float yTip, float halfWidth)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(-r), s = Mathf.Sin(-r);
            return DownWedge(x * c - y * s, x * s + y * c, yTop, yTip, halfWidth);
        }

        /// <summary>Triangle widest at xBase, narrowing to a point at xTip.</summary>
        internal static bool RightWedge(float x, float y, float xBase, float xTip, float halfHeight)
        {
            if (x < xBase || x > xTip) return false;
            float t = (x - xBase) / (xTip - xBase);
            return Mathf.Abs(y) <= halfHeight * (1f - t);
        }

        // ---------------------------------------------------------------- rasteriser

        const int Size = 96;
        const int Samples = 3;

        internal static Sprite Render(Func<float, float, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color[Size * Size];

            for (int py = 0; py < Size; py++)
            for (int pxi = 0; pxi < Size; pxi++)
            {
                int hits = 0;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    // Map pixel + subsample into [-1, 1].
                    float nx = ((pxi + (sx + 0.5f) / Samples) / Size) * 2f - 1f;
                    float ny = ((py  + (sy + 0.5f) / Samples) / Size) * 2f - 1f;
                    if (inside(nx, ny)) hits++;
                }
                float a = hits / (float)(Samples * Samples);
                px[py * Size + pxi] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
