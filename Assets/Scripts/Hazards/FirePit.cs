using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A fire pit on a fixed engage/disengage cycle, damaging whoever stands in it while it burns.
    /// Replaces the old free-placed circular lava tile; the cycle and the tick cadence are
    /// inherited from it unchanged, and only the geometry and the picture are new.
    ///
    /// ENEMIES RESPECT IT NOW (it was once player-only): their routes go round it while it is
    /// lit, and one knocked or caught in it burns - see EnemyController's pit section. This pit
    /// only ever damages the PLAYER itself; the enemy side ASKS the ground, the same way speed does.
    ///
    /// DAMAGE RIDES THE FLAMES, NOT THE HEAT. Damage is the same value the tongues are drawn
    /// from (<see cref="BurnFor"/>), so no flames means no damage - the picture IS the readout,
    /// the same rule WeaponHeat's blade colour lives by. It once rode the raw heat, which only
    /// touches 0 at a single instant of the cycle: for the ~30% of every cycle the pit showed
    /// ash and no flames it still ticked the player for up to ~1 HP every half second.
    ///
    /// ENGAGED IS FLAME; DISENGAGED IS ASH OVER COALS, AND THE SECOND IS THE HARDER HALF. A fire
    /// that merely dims reads as the same hazard turned down, and a player has no reason to
    /// believe it will not simply hurt them anyway. Ash is a DIFFERENT MATERIAL arriving over the
    /// top - a broken grey crust that hides the bed - and the coals still glowing through its
    /// cracks are what stop it reading as safe rather than as briefly survivable. The crust is
    /// drawn with real cracks (see PitArt.Crust) for exactly that: with no cracks there is nothing
    /// for the heat to show through, and the two states stop being the same fire.
    /// </summary>
    public class FirePit : FloorPit
    {
        // Serialized, and _tongues deliberately NOT readonly - see FloorPit's own note on why
        // every cached field here carries the attribute. A readonly collection is the worse half
        // of the same trap: Unity cannot write to one at all, so it comes back freshly empty
        // while every tongue GameObject it used to describe is still on screen, and the fire
        // would simply stop flickering with nothing saying why.
        [SerializeField] SpriteRenderer _bed, _crust, _bloom;
        [SerializeField] List<Tongue> _tongues = new();
        [SerializeField] float _phase;
        // This pit's own cadence - see Tuning.Hazards.FireCycleSecondsMin / FireBurnShareMin.
        [SerializeField] float _period, _burnShare;
        [SerializeField] float _tick;

        [System.Serializable]
        struct Tongue
        {
            public Transform T;
            public SpriteRenderer Sr;
            public float Rate, Phase, Width, Height, Sway;

            /// <summary>
            /// The tongue's home column. The sway is expressed as an OFFSET from this rather than
            /// integrated onto the live position: a per-frame nudge accumulates, so a pit left
            /// burning would slowly walk every flame out through its own wall.
            /// </summary>
            public float BaseX;
        }

        public static FirePit Spawn(Rect rect, Transform player, Transform parent)
        {
            var go = new GameObject("pit.fire");
            go.transform.SetParent(parent, false);
            var pit = go.AddComponent<FirePit>();
            pit.Init(rect, player);
            pit.Build(rect.size);
            return pit;
        }

        void Build(Vector2 size)
        {
            // Every pit its own cadence AND its own start, so several pits on one floor never
            // flare in lockstep - everything breathing on the same beat reads as one hazard
            // wearing several skins. A shared period with random starts was not enough: the gaps
            // between pits stayed fixed, so the floor still played one pattern on loop.
            _period = Random.Range(Tuning.Hazards.FireCycleSecondsMin, Tuning.Hazards.FireCycleSecondsMax);
            _burnShare = Random.Range(Tuning.Hazards.FireBurnShareMin, Tuning.Hazards.FireBurnShareMax);
            _phase = Random.value * _period;
            int seed = Random.Range(1, 9999);

            // The bottom of the hole, under everything: near-black, so a crack in the crust with
            // no live coal behind it still reads as depth rather than as a gap onto the floor.
            PitArt.Quad(gameObject, Spr.Square, new Color(0.07f, 0.05f, 0.05f), size, OrderFloor, "floor");

            _bed = PitArt.Tiled(gameObject, PitArt.EmberBed, Color.white, size, OrderMaterial, "coals");
            _crust = PitArt.Tiled(gameObject, PitArt.Crust(seed), Color.white, size, OrderDetail, "ash");

            BuildRecess(size);

            // A tongue per unit of pit area, within reason. Scaled to the footprint rather than a
            // fixed count, or a Large pit would be the same handful of flames spread thin and
            // would read as a smaller fire in a bigger hole.
            int count = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * Tuning.Hazards.FireTonguesPerSquareUnit),
                                    Tuning.Hazards.FireMinTongues, Tuning.Hazards.FireMaxTongues);
            for (int i = 0; i < count; i++) _tongues.Add(MakeTongue(size));

            // The pit lighting what is around it. Above the bed but still inside the pit band, so
            // it never draws over a body standing in the fire.
            _bloom = PitArt.Quad(gameObject, Spr.Glow, Color.white,
                                 size * Tuning.Hazards.FireBloomScale, OrderAbove, "bloom");
        }

        Tongue MakeTongue(Vector2 size)
        {
            var go = new GameObject("tongue");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PitArt.Flame;
            sr.sortingOrder = OrderAbove;

            float w = Random.Range(Tuning.Hazards.FireTongueWidthMin, Tuning.Hazards.FireTongueWidthMax);
            float h = w * Random.Range(1.9f, 3.1f);

            // Spread across the WHOLE footprint rather than along one edge - a row of flames is a
            // hearth, and this is a pit the player walks into the middle of.
            float margin = Tuning.Hazards.PitStandInset;
            float baseX = Random.Range(-size.x * 0.5f + margin, size.x * 0.5f - margin);
            // Rooted low in its own band: the sprite grows UPWARD off a base pivot, so a tongue
            // placed near the top edge would put its tip outside the pit it belongs to.
            float lowY = -size.y * 0.5f + margin;
            float baseY = Random.Range(lowY, Mathf.Max(lowY, size.y * 0.5f - margin - h * 0.55f));
            go.transform.localPosition = new Vector3(baseX, baseY, 0f);

            // Half of them mirrored, so the tongue sprite's own built-in curl leans both ways and
            // a pit does not read as every flame bending the same direction in a wind.
            if (Random.value < 0.5f) go.transform.localScale = new Vector3(-1f, 1f, 1f);

            return new Tongue
            {
                T = go.transform,
                Sr = sr,
                Width = w,
                Height = h,
                BaseX = baseX,
                // Incommensurable rates, so no two tongues ever come back into step. Shared
                // rates would make the whole pit pulse as one object.
                Rate = Random.Range(1.5f, 3.4f),
                Phase = Random.value * 10f,
                Sway = Random.Range(0.02f, 0.06f),
            };
        }

        /// <summary>
        /// 0 (ash) to 1 (burning), on a smooth rise and fall with no snap at either end - a fire
        /// that switched would be a trap rather than a cycle, and the whole value of the cycle is
        /// that the player can read where it is in it and cross during the lull.
        /// </summary>
        float Heat01() => HeatAt(Time.time);

        /// <summary>The heat at <paramref name="time"/> - public so an enemy's route can read
        /// where the cycle is GOING, not just where it is (see <see cref="RouteCost"/>).</summary>
        public float HeatAt(float time)
        {
            // A pit built before cadences were rolled has neither - the old even 6s cycle.
            float period = _period > 0f ? _period : 6f;
            float burn = _burnShare > 0f ? _burnShare : 0.5f;

            // The same sine as ever, its burning half stretched over this pit's burn share of the
            // cycle and its ashing half over the rest. Still continuous with no snap; only how
            // fast it rises and falls through half heat changes between the two halves.
            float u = Mathf.Repeat((time + _phase) / period, 1f);
            float w = u < burn ? 0.5f * u / burn : 0.5f + 0.5f * (u - burn) / (1f - burn);
            return 0.5f + 0.5f * Mathf.Sin(w * Mathf.PI * 2f);
        }

        /// <summary>
        /// How lit the FLAMES are for a given heat: 0 below <see cref="Tuning.Hazards.FireTongueThreshold"/>,
        /// rising to 1 at full heat. Damage rides THIS, not the raw heat - see the class note.
        /// </summary>
        static float BurnFor(float heat) => Mathf.Clamp01((heat - Tuning.Hazards.FireTongueThreshold)
                                                          / (1f - Tuning.Hazards.FireTongueThreshold));

        /// <summary>The burn now (0 under ash), for an enemy standing in it.</summary>
        public float Burn => BurnFor(Heat01());

        public override PitKind Kind => PitKind.Fire;

        /// <summary>Burning right now - the flames are up.</summary>
        public override bool HurtsNow => Burn > 0f;

        /// <summary>The larger of the burn now and a moment from now, so a pack does not walk in
        /// just before it flares - and does walk in once it is clearly dying down.</summary>
        public override float RouteCost => Tuning.Steering.FireRouteCost
            * BurnFor(Mathf.Max(Heat01(), HeatAt(Time.time + Tuning.Steering.FireRouteLookahead)));

        static readonly Color CoalsCold = new(0.34f, 0.11f, 0.06f);
        static readonly Color CoalsHot = new(1f, 0.62f, 0.18f);
        static readonly Color AshTint = new(0.46f, 0.44f, 0.45f);
        static readonly Color FlameTint = new(1f, 0.55f, 0.16f);

        /// <summary>
        /// Re-attach the cached renderers to the children that are still there.
        ///
        /// The belt to the [SerializeField] braces, and the same shape - and the same reason -
        /// as PrimitiveCharacterRig.EnsureLayers: re-attaching BY NAME to what already exists,
        /// never rebuilding, because building again would weld a second set of flames onto a pit
        /// that already has one. Cheap, because it can only ever find something missing on the
        /// single frame after a reload.
        /// </summary>
        bool EnsureLayers()
        {
            if (_bed != null && _crust != null && _bloom != null) return true;

            _tongues ??= new List<Tongue>();
            bool tonguesLost = _tongues.Count == 0;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                switch (sr.name)
                {
                    case "coals": _bed = sr; break;
                    case "ash": _crust = sr; break;
                    case "bloom": _bloom = sr; break;
                    case "tongue":
                        // Only when the list itself came back empty. Rediscovering on top of a
                        // list that survived would append a second entry per flame and animate
                        // each one twice, fighting itself.
                        if (tonguesLost) _tongues.Add(Recover(sr));
                        break;
                }
            }

            // A pit whose children are genuinely gone (mid-teardown) has nothing to paint and
            // must not throw trying - reported rather than guessed at, so Update can stand down
            // instead of dereferencing a null once a frame for the rest of the run.
            return _bed != null && _crust != null && _bloom != null;
        }

        /// <summary>
        /// Rebuild a Tongue entry around a flame GameObject that outlived its record of itself.
        ///
        /// Position is read back exactly; the MOTION is rolled fresh, and the height with it,
        /// because the live localScale.y is the ANIMATED height rather than the authored one -
        /// recovering from it would shrink every flame to whatever size it happened to be
        /// flickering at when the reload landed, permanently. A flame that comes back slightly
        /// different after a developer edits a script mid-play is a cost worth paying; one that
        /// comes back progressively smaller every time is not. Nothing here can happen in a
        /// build, where there is no domain reload.
        /// </summary>
        Tongue Recover(SpriteRenderer sr)
        {
            float w = Random.Range(Tuning.Hazards.FireTongueWidthMin, Tuning.Hazards.FireTongueWidthMax);
            return new Tongue
            {
                T = sr.transform,
                Sr = sr,
                Width = w,
                Height = w * Random.Range(1.9f, 3.1f),
                BaseX = sr.transform.localPosition.x,
                Rate = Random.Range(1.5f, 3.4f),
                Phase = Random.value * 10f,
                Sway = Random.Range(0.02f, 0.06f),
            };
        }

        void Update()
        {
            if (!EnsureLayers()) return;

            float heat = Heat01();
            Paint(heat);

            if (!PlayerAlive) return;
            // Reset, same as Turret's beam on LOS loss - and under ash too, so a part-charged tick
            // held across the lull does not land the instant the first flame shows.
            if (!ContainsPlayer() || BurnFor(heat) <= 0f) { _tick = 0f; return; }

            _tick += Time.deltaTime;
            if (_tick < Tuning.Hazards.FireTickInterval) return;
            _tick = 0f;

            float dps = Tuning.Hazards.FireMaxDamagePerSecond * BurnFor(heat) * DamageScale;
            if (dps <= 0f) return;
            // Salamander: fire does not burn its own.
            if (FloorPits.PlayerImmune != null && FloorPits.PlayerImmune(PitKind.Fire)) return;
            PlayerHealth.Take(new DamageInfo(dps * Tuning.Hazards.FireTickInterval, ElementType.Fire, gameObject));
        }

        void Paint(float heat)
        {
            // The coals never go fully dark: at the cold end they are a dim red under the ash,
            // which is the entire difference between "this is out" and "this is between burns".
            var bed = Color.Lerp(CoalsCold, CoalsHot, heat);
            bed.a = Mathf.Lerp(0.72f, 1f, heat);
            _bed.color = bed;

            // Ash covers the bed completely when cold and has burned away by the time the pit is
            // fully engaged. Crossing the middle of its own range is where the pit looks most like
            // it is catching, which is exactly when the damage is climbing.
            var ash = AshTint;
            ash.a = Mathf.Clamp01(1f - heat * 1.35f) * 0.94f;
            _crust.color = ash;

            var bloom = Color.Lerp(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.72f, 0.3f), heat);
            bloom.a = heat * heat * Tuning.Hazards.FireBloomAlpha;   // squared: no glow at all until it is genuinely lit
            _bloom.color = bloom;

            // Tongues only exist above a threshold, and they GROW into it rather than fading in
            // at full height - a flame that appears already tall reads as a light being switched
            // on rather than as something catching.
            float lit = BurnFor(heat);
            for (int i = 0; i < _tongues.Count; i++)
            {
                var g = _tongues[i];
                // Guarded rather than assumed live: an element of a collection that survived a
                // reload can still point at something torn down since, and dereferencing one is
                // the difference between a frame that does nothing and an exception every frame
                // for the rest of the run.
                if (g.Sr == null || g.T == null) continue;
                if (lit <= 0f) { if (g.Sr.enabled) g.Sr.enabled = false; continue; }
                if (!g.Sr.enabled) g.Sr.enabled = true;

                float flick = 0.72f + 0.28f * Mathf.Sin((Time.time + g.Phase) * g.Rate * Mathf.PI * 2f);
                float scaleX = Mathf.Sign(g.T.localScale.x) * g.Width;
                g.T.localScale = new Vector3(scaleX, g.Height * lit * flick, 1f);

                // A slow lateral drift about its own column, so the tongue is not a shape pinned
                // to one spot merely changing height - that reads as a bar chart.
                var p = g.T.localPosition;
                p.x = g.BaseX + Mathf.Sin((Time.time + g.Phase) * g.Rate * 0.4f) * g.Sway;
                g.T.localPosition = p;

                var c = Color.Lerp(FlameTint, new Color(1f, 0.87f, 0.5f), flick * 0.5f);
                c.a = lit * Mathf.Lerp(0.55f, 0.92f, flick);
                g.Sr.color = c;
            }
        }
    }
}
