using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// Procedural sprite factory. The prototype ships no art assets - every visual is
    /// generated at runtime so the scene stays trivial to rebuild from the Unity CLI.
    /// </summary>
    public static class Spr
    {
        static Sprite _square, _circle, _ring, _thinRing, _capsule, _slash, _halfDisc, _glow;
        static Sprite _tear, _tearRim, _halfRing, _haloRing;

        public static Sprite Square => _square ??= Build(64, (x, y, c) => 1f);

        public static Sprite Circle => _circle ??= Build(64, (x, y, c) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            return Mathf.Clamp01((1f - d) * 8f);
        });

        public static Sprite Ring => _ring ??= Build(128, (x, y, c) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            return Mathf.Clamp01((1f - Mathf.Abs(d - 0.88f) * 14f));
        });

        /// <summary>
        /// A hairline ring, for the range circle drawn on the ground.
        ///
        /// Deliberately NOT a thinner <see cref="Ring"/>: that one is shared with every impact
        /// burst in the game, and those want a soft fat band. Two uses, two sprites.
        ///
        /// Rendered at 512 rather than 128 because the thinness is the point. This is drawn about
        /// four units across, so at 128 the band would be a texel or so wide and would crawl and
        /// alias as the ring resized; the extra resolution is what buys a clean line.
        /// </summary>
        public static Sprite ThinRing => _thinRing ??= Build(512, (x, y, c) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.92f) * 90f);
        });

        /// <summary>
        /// Smooth radial falloff to nothing at the rim - glare, not a disc.
        ///
        /// Deliberately NOT a softer <see cref="Circle"/>: that one multiplies its edge by 8, so
        /// it is effectively solid with a thin soft rim, which is right for an impact burst and
        /// useless as a bloom, where the gradient IS the effect.
        /// </summary>
        /// <summary>
        /// A tear in space-time: a tall vertical rip with a ragged, uneven edge.
        ///
        /// A MANDORLA, NOT AN ELLIPSE. The base shape tapers to a point at both poles, because a
        /// rip is something that was pulled APART - it has two ends where the tear runs out. An
        /// oval reads as a portal somebody built.
        ///
        /// The raggedness is three sine harmonics of the vertical coordinate rather than real
        /// noise, deliberately: Perlin at this scale clusters around its midpoint and comes out as
        /// a gentle wobble (the same failure StatusVisuals already documents for its flame), while
        /// summed harmonics at incommensurable frequencies never repeat over the height and give
        /// the edge actual character. The frequencies are odd multiples for the same reason.
        ///
        /// Used as the MASK for the view through it - see Rifts.Rift.
        /// </summary>
        public static Sprite Tear => _tear ??= Build(512, (x, y, c) =>
        {
            float nx = (x - c) / c, ny = (y - c) / c;
            float half = TearHalfWidth(ny);
            if (half <= 0f) return 0f;
            return Mathf.Clamp01((1f - Mathf.Abs(nx) / half) * 7f);
        });

        /// <summary>
        /// The lit edge of the tear, and only the edge - what makes it read as torn rather than as
        /// a hole cut in something.
        ///
        /// Brightest exactly ON the boundary and falling away both ways, so the rim sits half
        /// inside and half outside the shape the mask cuts. A rim drawn entirely outside reads as
        /// an outline around a sticker.
        /// </summary>
        public static Sprite TearRim => _tearRim ??= Build(512, (x, y, c) =>
        {
            float nx = (x - c) / c, ny = (y - c) / c;
            float half = TearHalfWidth(ny);
            if (half <= 0f) return 0f;

            // Measured in ABSOLUTE width rather than as a fraction of the local half-width, or the
            // rim would thin to nothing at the poles - which is precisely where a rip is sharpest.
            float edge = Mathf.Abs(Mathf.Abs(nx) - half);
            float rim = Mathf.Clamp01(1f - edge / 0.035f);

            // Fade the very tips so the two ends trail off instead of stopping dead.
            float tip = Mathf.Clamp01((1f - Mathf.Abs(ny)) * 6f);
            return rim * tip;
        });

        /// <summary>
        /// Half the tear's width at a given height, -1..1. Shared by the shape and its rim so the
        /// two cannot describe different tears - the rim tracing a slightly different curve from
        /// the mask is the one artefact that would give the whole effect away.
        /// </summary>
        static float TearHalfWidth(float ny)
        {
            float taper = 1f - ny * ny;
            if (taper <= 0f) return 0f;

            // The exponent trades a usable aperture against sharp ends, and both matter. At 0.7
            // the tear narrowed the whole way down and read as a lightning crack - striking, and
            // useless for the one job the shape has. At 0.45 the middle opened up but the poles
            // went blunt and it read as a capsule. 0.55 keeps the middle full and still runs the
            // two ends out to points.
            float half = 0.34f * Mathf.Pow(taper, 0.55f);

            // Halved from the first pass for the same reason: at +/-0.44 the wobble pinched the
            // opening shut at several heights, so the view through it came in slices. The edge
            // still has to be ragged - it is a rip - but the raggedness belongs on the EDGE, not
            // in the middle of the hole.
            float wobble = 1f
                         + 0.12f * Mathf.Sin(ny * 11.3f + 1.7f)
                         + 0.07f * Mathf.Sin(ny * 23.9f + 4.1f)
                         + 0.04f * Mathf.Sin(ny * 41.7f + 2.3f);
            return half * Mathf.Max(0.05f, wobble);
        }

        /// <summary>
        /// A ring drawn as a solid BAND, and its lower half, for a hoop small enough that a
        /// hairline would not survive.
        ///
        /// They exist as a PAIR so a ring can pass BEHIND something and come back in FRONT of it:
        /// draw <see cref="HaloRing"/> under the object and <see cref="HalfRing"/> over it, and
        /// the object is genuinely inside the hoop rather than in front of a circle. That read is
        /// the entire difference between a halo encircling a blade and a decal parked behind one,
        /// and it cannot be had from one sprite at one sorting order.
        ///
        /// THE TWO MUST STAY IDENTICAL in radius and band width - they are one ring drawn twice,
        /// so a change to either that is not made to the other shows up as the near arc missing
        /// the far one where they meet.
        ///
        /// NOT a thinner <see cref="ThinRing"/>, and that is measured rather than preferred: that
        /// one's band falls to nothing 0.03 of a radius from its peak, which is right at the four
        /// units the reach ring is drawn across and under a screen pixel on a hoop drawn at two
        /// thirds of one. A hairline is a property of the size it is drawn at.
        /// </summary>
        public static Sprite HaloRing => _haloRing ??= Build(256, (x, y, c) => Band(x, y, c));

        /// <inheritdoc cref="HaloRing"/>
        public static Sprite HalfRing => _halfRing ??= Build(256, (x, y, c) =>
            y > c ? 0f : Band(x, y, c));                // the upper half belongs to the full ring

        /// <summary>The shared profile of the two above. Solid through the middle of the band with
        /// a soft texel either side, rather than a peak with falloff - at this size a falloff IS
        /// the band, and the ring comes out as a smudge.</summary>
        static float Band(int x, int y, float c)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            return Mathf.Clamp01((0.13f - Mathf.Abs(d - 0.82f)) * 26f);
        }

        public static Sprite Glow => _glow ??= Build(128, (x, y, c) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            float t = Mathf.Clamp01(1f - d);
            return t * t;
        });

        /// <summary>
        /// Half a disc, flat edge running through the PIVOT with the dome below it - light
        /// spilling out of a doorway onto the floor in front of it.
        ///
        /// The flat edge sits on the pivot rather than on the sprite's bottom edge, so the
        /// caller positions it at the threshold itself and the dome falls where the light
        /// would. Only the lower half of the texture is opaque, so a scale of (w, h) draws a
        /// semicircle w wide and h/2 deep.
        /// </summary>
        public static Sprite HalfDisc => _halfDisc ??= Build(128, (x, y, c) =>
        {
            if (y > c) return 0f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            float t = Mathf.Clamp01(1f - d);
            return t * t;
        });

        static readonly Dictionary<int, Sprite> _segmentedRings = new();

        /// <summary>
        /// A full ring divided into <paramref name="segments"/> equal arcs with a thin gap
        /// between each - the enemy armor read. Unlike <see cref="Ring"/>'s fixed band this is
        /// parameterised: more segments means more, narrower arcs, fewer means fewer, wider
        /// ones, but the ring is always whole. Losing a bar never punches a hole in an otherwise
        /// full circle - it reflows what is left into fewer, bigger pieces, down to one segment
        /// reading as a solid ring right before the last bar breaks.
        /// </summary>
        public static Sprite SegmentedRing(int segments)
        {
            int n = Mathf.Max(1, segments);
            if (_segmentedRings.TryGetValue(n, out var cached) && cached != null) return cached;

            float segSpan = 360f / n;
            // Capped against the segment span itself so a high bar count (many, thin arcs)
            // never eats a whole segment in gap alone.
            float half = Mathf.Min(3f, segSpan * 0.35f);

            var built = Build(256, (x, y, c) =>
            {
                float nx = (x - c) / c, ny = (y - c) / c;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) * 14f);
                if (band <= 0f) return 0f;

                float ang = Mathf.Atan2(ny, nx) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                float within = ang % segSpan;
                float gapAlpha = Mathf.Clamp01(Mathf.Min(within, segSpan - within) / half);

                return band * gapAlpha;
            });

            _segmentedRings[n] = built;
            return built;
        }

        /// <summary>Rounded bar - the placeholder rig's limbs and torso.</summary>
        public static Sprite Capsule => _capsule ??= Build(64, (x, y, c) =>
        {
            float nx = (x - c) / c, ny = (y - c) / c;
            float r = 0.55f;
            float span = Mathf.Max(0f, 1f - r);
            float dy = Mathf.Max(0f, Mathf.Abs(ny) - span);
            float d = Mathf.Sqrt(nx * nx + (dy / Mathf.Max(0.0001f, r)) * (dy / Mathf.Max(0.0001f, r)) * r * r);
            return Mathf.Clamp01((r - d) * 10f);
        });

        /// <summary>
        /// A crescent sweeping the front 150 degrees, thickest at the middle and tapering to
        /// points at both ends.
        ///
        /// This is what carries a swing out to its real reach. The sword itself only covers about
        /// 45% of the range the hitbox uses, and lengthening the blade until it covered the rest
        /// meant a weapon twice the character's height. A slash arc says the same thing - THIS is
        /// the space the strike filled - without the character having to carry it around between
        /// swings.
        ///
        /// Drawn pointing along +X so it can be rotated straight onto the facing.
        /// </summary>
        public static Sprite Slash => _slash ??= Build(192, (x, y, c) =>
        {
            float nx = (x - c) / c, ny = (y - c) / c;
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            if (d < 0.0001f) return 0f;

            // Band at the rim: this is a slash at the edge of the reach, not a filled fan.
            float band = 1f - Mathf.Abs(d - 0.80f) * 7.5f;
            if (band <= 0f) return 0f;

            // Taper by angle from straight ahead, so the ends come to points instead of stopping
            // in a hard edge - a blunt-ended arc reads as a drawn shape rather than a swing.
            float ang = Mathf.Abs(Mathf.Atan2(ny, nx)) * Mathf.Rad2Deg;
            const float HalfArc = 75f;
            if (ang > HalfArc) return 0f;
            float taper = 1f - Mathf.Pow(ang / HalfArc, 2.2f);

            return Mathf.Clamp01(band) * Mathf.Clamp01(taper);
        });

        /// <summary>
        /// Throw a slash arc at <paramref name="facingDeg"/>, sized so its rim sits at
        /// <paramref name="radius"/> - the swing's true reach.
        ///
        /// World-static like <see cref="Flash"/>, not parented: a slash happened at a place and a
        /// moment. Riding the player would drag it along behind them for its whole life and read
        /// as an aura rather than a strike.
        /// </summary>
        public static void Slice(Vector3 pos, Vector2 facing, float radius, Color color,
                                 float life = 0.18f)
        {
            var go = new GameObject("fx.slash");
            // The crescent's rim sits at 0.80 of the sprite's half-width, so scale up to put it
            // exactly on the reach.
            float diameter = radius * 2f / 0.80f;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * diameter;
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Slash;
            sr.color = color;
            sr.sortingOrder = SortingOrders.Fx;
            var fade = go.AddComponent<FxFade>();
            fade.Life = life;
            fade.Growth = 0.10f;   // a touch of follow-through, not a shockwave
        }

        /// <summary>
        /// Cones are cached per SHAPE, not per firing: the gradient is baked into the texture, so
        /// two cones that differ only in tint or size share one sprite.
        /// </summary>
        static readonly Dictionary<(int halfAngle, int nearPct), Sprite> _cones = new();

        /// <summary>
        /// A filled wedge pointing along +X, fading from <paramref name="nearAlphaFraction"/> at
        /// the origin to fully opaque at the rim.
        ///
        /// The gradient has to live in the TEXTURE. SpriteRenderer.color multiplies uniformly, so
        /// it can tint the whole cone but can never make one end of it more solid than the other.
        ///
        /// The ramp is linear because the thing it is reporting is linear - see
        /// <c>AirResource.Release</c>, where damage lerps from near to far across the same
        /// distance. Passing the damage floor in as <paramref name="nearAlphaFraction"/> is what
        /// keeps the picture honest: how solid the cone looks at a given point IS how hard it
        /// hits there, rather than a decoration that happens to be cone-shaped.
        /// </summary>
        public static Sprite Cone(float halfAngleDeg, float nearAlphaFraction)
        {
            float half = Mathf.Clamp(halfAngleDeg, 1f, 179f);
            float nearA = Mathf.Clamp01(nearAlphaFraction);

            var key = (Mathf.RoundToInt(half), Mathf.RoundToInt(nearA * 100f));
            if (_cones.TryGetValue(key, out var cached) && cached != null) return cached;

            var built = Build(256, (x, y, c) =>
            {
                float nx = (x - c) / c, ny = (y - c) / c;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d > 1f) return 0f;

                float ang = Mathf.Abs(Mathf.Atan2(ny, nx)) * Mathf.Rad2Deg;
                if (ang > half) return 0f;

                // Soften both boundaries. A hard angular cut reads as a cut-out triangle rather
                // than a gust, and a hard rim reads as a wall the effect stops at.
                float edge = Mathf.Clamp01((half - ang) / (half * 0.28f));
                float rim = Mathf.Clamp01((1f - d) / 0.07f);

                return Mathf.Lerp(nearA, 1f, d) * edge * rim;
            });

            _cones[key] = built;
            return built;
        }

        static Sprite _gradientDisc;

        /// <summary>
        /// A filled disc whose alpha ramps from nearly nothing at the centre to full strength at
        /// the rim - the visual counterpart of <see cref="Cone"/>'s distance lerp, just without
        /// the angular cut. Built for the bow's old range ring; the Spire's dome uses it now.
        ///
        /// Deliberately NOT keyed to a weapon's actual near-fraction damage number - an early
        /// version started the fill AT that fraction (0.6 for the bow), which reads as "already
        /// mostly opaque everywhere" the instant it covers real screen area, rather than as a
        /// hint that fades in from nothing. The alpha here is a LEGIBILITY device: it says weaker
        /// near, stronger far, and leaves the caller's own colour to decide how strong "full
        /// strength" actually looks.
        /// </summary>
        public static Sprite GradientDisc() => _gradientDisc ??= Build(256, (x, y, c) =>
        {
            float nx = (x - c) / c, ny = (y - c) / c;
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            if (d > 1f) return 0f;

            // A disc's AREA grows with the radius squared, so a plain linear ramp still puts most
            // of its visible pixels at middling-to-high alpha - it read as a wall of colour, not
            // a hint. Cubed keeps the interior close to true zero (0.5 radius -> 0.125, not 0.5)
            // and saves the climb for the outer band, which is what "nearly transparent near the
            // player, stronger toward the rim" actually needs to look like at this scale.
            float d3 = d * d * d;

            // Softened at the very rim only, so the boundary still reads as a circle rather than
            // fading into nothing before it gets there.
            float rim = Mathf.Clamp01((1f - d) / 0.04f);
            return d3 * rim;
        });

        /// <summary>
        /// Throw a cone of wind/force from <paramref name="pos"/> along <paramref name="facing"/>,
        /// its rim sitting exactly on <paramref name="radius"/>.
        ///
        /// World-static like <see cref="Slice"/>: it marks where the blast HAPPENED. Parenting it
        /// to the player would drag the cone around after the fact and turn a one-off blast into
        /// an aura they appear to be carrying.
        /// </summary>
        public static void ConeBlast(Vector3 pos, Vector2 facing, float radius, float halfAngleDeg,
                                     float nearAlphaFraction, Color color, float life = 0.32f)
        {
            var go = new GameObject("fx.cone");
            go.transform.position = pos;
            // The sprite's wedge runs to the full half-width, so the diameter IS twice the reach.
            go.transform.localScale = Vector3.one * radius * 2f;
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Cone(halfAngleDeg, nearAlphaFraction);
            sr.color = color;
            sr.sortingOrder = SortingOrders.Fx;

            var fade = go.AddComponent<FxFade>();
            fade.Life = life;
            // Barely grows. This is a claim about an area that was just resolved, and a cone that
            // visibly expands would be showing reach the attack never had.
            fade.Growth = 0.04f;
        }

        static readonly Dictionary<int, Sprite> _reverseCones = new();

        /// <summary>
        /// A cone that is WIDEST at its origin and narrows to a point along +X - the Armillary's
        /// Quintessence. The sprite is centred on the origin like <see cref="Cone"/>, so a
        /// renderer scaled to twice the length puts the tip on the reach. Brightest at the base,
        /// where it hits hardest.
        /// </summary>
        public static Sprite ReverseCone(float baseHalfFraction)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp01(baseHalfFraction) * 100f);
            if (_reverseCones.TryGetValue(key, out var cached) && cached != null) return cached;
            float half0 = key / 100f;
            var built = Build(256, (x, y, c) =>
            {
                float nx = (x - c) / c, ny = (y - c) / c;
                if (nx < 0f || nx > 1f) return 0f;
                float half = half0 * (1f - nx);
                if (Mathf.Abs(ny) > half) return 0f;
                float edge = Mathf.Clamp01((half - Mathf.Abs(ny)) / Mathf.Max(0.01f, half * 0.35f));
                return Mathf.Lerp(1f, 0.35f, nx) * edge;
            });
            _reverseCones[key] = built;
            return built;
        }

        /// <summary>Flash a reverse cone from <paramref name="pos"/> along <paramref name="facing"/>,
        /// its point on <paramref name="length"/> - world-static, like <see cref="ConeBlast"/>.
        /// Drawn ON THE GROUND by default (above pits, under every character): at its widest it
        /// sits right on the character, and drawn over them it washed them out.</summary>
        public static void ReverseConeBlast(Vector3 pos, Vector2 facing, float length, float baseHalfWidth,
                                            Color color, float life = 0.32f,
                                            int sortingOrder = SortingOrders.PitBase + 1)
        {
            var go = new GameObject("fx.reversecone");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * length * 2f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ReverseCone(baseHalfWidth / Mathf.Max(0.01f, length));
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            var fade = go.AddComponent<FxFade>();
            fade.Life = life;
            fade.Growth = 0.04f;
        }

        static Sprite _star;

        /// <summary>
        /// A five-point star, point up - the Forge's star level. A sprite rather than a glyph:
        /// the legacy UI font has no reliable star character, and a WebGL build has no OS font to
        /// fall back on, so "★" would draw as a missing-glyph box on exactly the platform that
        /// matters. 4x4 supersampled so the points stay sharp at small sizes.
        /// </summary>
        public static Sprite Star() => _star ??= Build(64, (x, y, c) =>
        {
            const int n = 4;
            int inside = 0;
            for (int sy = 0; sy < n; sy++)
                for (int sx = 0; sx < n; sx++)
                    if (InStar((x + (sx + 0.5f) / n - c) / c, (y + (sy + 0.5f) / n - c) / c)) inside++;
            return inside / (float)(n * n);
        });

        static bool InStar(float px, float py)
        {
            const float outer = 0.98f, inner = 0.42f;
            bool odd = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                float ai = Mathf.PI * 0.5f + i * Mathf.PI / 5f, aj = Mathf.PI * 0.5f + j * Mathf.PI / 5f;
                float ri = i % 2 == 0 ? outer : inner, rj = j % 2 == 0 ? outer : inner;
                float xi = Mathf.Cos(ai) * ri, yi = Mathf.Sin(ai) * ri - 0.08f;
                float xj = Mathf.Cos(aj) * rj, yj = Mathf.Sin(aj) * rj - 0.08f;
                if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) odd = !odd;
            }
            return odd;
        }

        static Sprite Build(int size, System.Func<int, int, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            float c = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y, c));
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>Spawn a short-lived tinted circle at a fixed point - an impact.</summary>
        public static void Flash(Vector3 pos, float radius, Color color, float life = 0.25f, bool ring = true)
        {
            var go = MakeFx(radius, color, life, ring);
            go.transform.position = pos;
        }

        /// <summary>
        /// The same burst, but parented so it rides a moving anchor.
        ///
        /// <see cref="Flash"/> is world-static, which is right for an impact and wrong for a cue
        /// about the player's own meter: those fire while the player is running, and a burst left
        /// behind at the spot where a stack landed reads as something that happened over there
        /// rather than something that happened to you.
        /// </summary>
        public static void Pulse(Transform anchor, float radius, Color color, float life = 0.35f,
                                 bool ring = true, float growth = 1.1f)
        {
            if (anchor == null) return;
            var go = MakeFx(radius, color, life, ring);
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = Vector3.zero;
            go.GetComponent<FxFade>().Growth = growth;
        }

        /// <summary>
        /// Fire welling out of the GROUND: a filled disc with a brighter rim, drawn under every
        /// body and fading in place.
        ///
        /// Not <see cref="Flash"/>. A Flash sits at <see cref="SortingOrders.Fx"/>, above the
        /// character and everything else - right for an impact, wrong for something the fight is
        /// standing IN. This sits on the ground layer like a FirePool's decal, so enemies caught
        /// in the circle stay visible on top of it, which is the whole point of drawing the circle
        /// at the damage radius in the first place.
        ///
        /// It is decoration only - the damage was already resolved by the step that called it.
        /// </summary>
        public static void GroundBurn(Vector3 pos, float radius, Color color, float life = 0.55f)
        {
            void Disc(Sprite sprite, float r, Color c, int orderOffset)
            {
                var go = new GameObject("groundburn");
                go.transform.position = new Vector3(pos.x, pos.y, 0f);
                go.transform.localScale = Vector3.one * r * 2f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = c;
                sr.sortingOrder = SortingOrders.GroundDecal + orderOffset;
                go.AddComponent<FxFade>().Life = life;
            }

            // The fill says where the damage went; the rim says where it STOPS, which is the half
            // a player reads when deciding whether they are clear of it.
            Disc(Circle, radius, new Color(color.r, color.g, color.b, 0.42f), 1);
            Disc(Ring, radius, new Color(color.r, color.g, color.b, 0.85f), 2);
        }

        static GameObject MakeFx(float radius, Color color, float life, bool ring)
        {
            var go = new GameObject("fx");
            go.transform.localScale = Vector3.one * radius * 2f;
            var art = Art.GameArt.I;
            var authored = ring ? art.BurstSprite : art.HitSprite;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = authored != null ? authored : (ring ? Ring : Circle);
            // Authored FX carry their own colour; placeholders need the element tint to read.
            sr.color = authored != null ? new Color(1f, 1f, 1f, color.a) : color;
            sr.sortingOrder = SortingOrders.Fx;
            go.AddComponent<FxFade>().Life = life;
            return go;
        }
    }

    public class FxFade : MonoBehaviour
    {
        public float Life = 0.25f;

        /// <summary>How far the burst expands over its life, as a fraction of its start size.</summary>
        public float Growth = 0.35f;

        float _t;
        SpriteRenderer _sr;
        Vector3 _from;
        float _a0 = 1f;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _from = transform.localScale;
            _a0 = Mathf.Max(0.02f, _sr.color.a);   // honour a caller that asked for a faint burst
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = _t / Life;
            if (k >= 1f) { Destroy(gameObject); return; }
            var c = _sr.color;
            c.a = _a0 * (1f - k);
            _sr.color = c;
            transform.localScale = _from * (1f + k * Growth);
        }
    }
}
