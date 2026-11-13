using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using T2 = Convergence.Core.Tuning.Overhaul;

namespace Convergence.Exchange
{
    /// <summary>
    /// THE OVERHAUL'S ENTRIES (EXCHANGE-OVERHAUL.md, the user's go-ahead 2026-11-04: "I'll leave it
    /// to you to decide what's best"). Every entry carries a KIND (drawn first), a GATE (class,
    /// element, defence, or a trait of an art in the rotation) and, where the element finishes it,
    /// the COAT. Behaviour lives in RunEffects.Overhaul*.cs, keyed by id. Names are working labels
    /// (naming comes later); the words say what happens on screen.
    ///
    /// FORMS STACK (I-III): II grows the measure its card names, III RUBEDO frees it from its
    /// socket's sharing. CONCORDS are weapon + defence + element, offered only at the brand deal.
    /// </summary>
    public static partial class ExchangeCatalog
    {
        static string P(float f) => $"{Mathf.RoundToInt(f * 100f)}%";
        static string S(float s) => $"{s:0.#} s";
        static string U(float v) => v.ToString("0.#");

        const string FormCap = "Unshared";
        const string FormCapText = "Sharing its slot no longer weakens it.";

        const ExchangeFamily Cho = ExchangeFamily.Choler, Phl = ExchangeFamily.Phlegm;
        const ExchangeFamily San = ExchangeFamily.Sanguine, Mel = ExchangeFamily.Melancholy;

        static ExchangeFamily FamilyOf(School s) => s switch
        {
            School.Choler => Cho, School.Phlegm => Phl, School.Sanguine => San, School.Melancholy => Mel,
            _ => ExchangeFamily.Ledger,
        };

        /// <summary>A FORM of the overhaul: three stacks, the Rubedo that frees it from sharing.</summary>
        static ExchangeEntry Form(string id, string name, School school, ExchangeMark mark, int weight, FormSlot slot,
                                  DealKind kind, string effect)
        {
            var e = B(id, name, FamilyOf(school), mark, weight, 3, effect, conditional: true)
                    .Cap(FormCap, FormCapText).Inscribed(slot);
            e.School = school;
            e.Deal = kind;
            return e;
        }

        /// <summary>A plain boon of the overhaul (not a form).</summary>
        static ExchangeEntry Plain(string id, string name, School school, ExchangeMark mark, int weight, int stacks,
                                   DealKind kind, string effect, string capName = null, string capText = null)
        {
            var e = B(id, name, FamilyOf(school), mark, weight, stacks, effect, conditional: true);
            if (capName != null) e.Cap(capName, capText);
            e.School = school;
            e.Deal = kind;
            return e;
        }

        static ExchangeFamily CostFamily(DealKind k) => k switch
        {
            DealKind.Toll => ExchangeFamily.Tithe,
            DealKind.Rule => ExchangeFamily.Leaden,
            DealKind.Host => ExchangeFamily.Blunted,
            DealKind.Ground => ExchangeFamily.Blindfold,
            DealKind.Socket => ExchangeFamily.Leaden,
            _ => ExchangeFamily.Brittle,
        };

        /// <summary>A cost of the overhaul: stackable ones reach a Nigredo and get an Albedo below.</summary>
        static ExchangeEntry Price(string id, string name, School school, ExchangeMark mark, int weight, int stacks,
                                   DealKind kind, string effect, Action<Mods, int> apply = null,
                                   string capName = null, string capText = null)
        {
            var e = C(id, name, CostFamily(kind), mark, weight, stacks, effect, apply, conditional: true);
            if (capName != null) e.Cap(capName, capText);
            e.School = school;
            e.Deal = kind;
            return e;
        }

        static ExchangeEntry Albedo(string id, string name, string ofCost, School school, ExchangeMark mark, string effect)
        {
            var e = A(id, name, ofCost, FamilyOf(school), mark, effect, conditional: true);
            e.School = school;
            e.Deal = DealKind.SetPiece;
            return e;
        }

        static void BuildOverhaul()
        {
            const School Ch = School.Choler, Ph = School.Phlegm, Sa = School.Sanguine, Me = School.Melancholy;
            const ExchangeMark D1 = ExchangeMark.Dot1, D2 = ExchangeMark.Dot2, D3 = ExchangeMark.Dot3, Bar = ExchangeMark.Bar;
            const ExchangeMark Ring = ExchangeMark.Ring, Cross = ExchangeMark.Cross, Stroke = ExchangeMark.Stroke, Drop = ExchangeMark.Drop;
            const DealKind Role = DealKind.ArtRole, Craft = DealKind.ArtCraft;
            const FormSlot Art = FormSlot.Art;
            const ArtRole Pull = ArtRole.Pull, Push = ArtRole.Push, Repos = ArtRole.Reposition, Ward = ArtRole.Ward;
            const ArtRole Strike = ArtRole.Strike, Sweep = ArtRole.Sweep, Setup = ArtRole.Setup;

            // ============================================================ ART ROLE: three families x seven roles
            // On an art swap a role form becomes its FAMILY's version for the new art's role (stacks kept).

            // ---- Impact: contact damage ----
            Form("ar_collide", "Collide", Ch, D1, 2, Art, Role,
                 $"Bodies dragged together by this art strike each other for {Hits(T2.CollideHitUnits)} each.").Gate(Pull, "impact");
            Form("ar_bowl", "Bowl", Me, D1, 2, Art, Role,
                 $"A body pushed by this art strikes every body it passes for {Hits(T2.BowlHitUnits)}.").Gate(Push, "impact");
            Form("ar_slipstream", "Slipstream", Sa, D1, 2, Art, Role,
                 $"Enemies near the line you travelled are struck for {Hits(T2.SlipstreamHitUnits)}.").Gate(Repos, "impact");
            Form("ar_return", "Return", Me, D1, 2, Art, Role,
                 $"A blow stopped by this art goes back: its attacker takes {Hits(T2.ReturnHitUnits)} and staggers.").Gate(Ward, "impact");
            Form("ar_carry", "Carry", Sa, D1, 2, Art, Role,
                 $"A kill with this art throws {P(T2.CarryShare)} of the hit to the nearest enemy.").Gate(Strike, "impact");
            Form("ar_link", "Link", Ph, D1, 2, Art, Role,
                 $"Bodies struck by this art are linked for {S(T2.LinkSeconds)}: {P(T2.LinkShare)} of what one takes reaches the others.").Gate(Sweep, "impact");
            Form("ar_spread", "Spread", Ch, D1, 2, Art, Role,
                 $"Each body struck by this art passes {P(T2.SpreadShare)} of the hit to the nearest enemy.").Gate(Setup, "impact");

            // ---- Control: hold, slow, stagger ----
            Form("ar_held", "Held", Me, D2, 2, Art, Role,
                 $"Bodies pulled by this art arrive staggered for {S(T2.HeldStagger)}.").Gate(Pull, "control");
            Form("ar_slam", "Slam", Me, D2, 2, Art, Role,
                 $"A body pushed by this art into a wall or another body takes {Hits(T2.SlamHitUnits)} and staggers.").Gate(Push, "control");
            Form("ar_decoy", "Decoy", Ph, D2, 2, Art, Role,
                 $"Where you left, an afterimage holds off the attacks of enemies near it for {S(T2.DecoySeconds)}.").Gate(Repos, "control");
            Form("ar_shockfront", "Shockfront", Me, D2, 2, Art, Role,
                 $"As this art ends, a ring throws back everything within {U(T2.ShockfrontRadius)}.").Gate(Ward, "control");
            Form("ar_daze", "Daze", Me, D2, 2, Art, Role,
                 $"This art's main target staggers for {S(T2.DazeStagger)}.").Gate(Strike, "control");
            Form("ar_mire", "Mire", Ph, D2, 2, Art, Role,
                 $"Bodies struck by this art are slowed {P(1f - T2.MireSlow)} for {S(T2.MireSeconds)}.").Gate(Sweep, "control");
            Form("ar_snare", "Snare", Me, D2, 2, Art, Role,
                 $"Bodies struck by this art are rooted for {S(T2.SnareRoot)}.").Gate(Setup, "control");

            // ---- Tempo: the chain and the rotation ----
            Form("ar_reel", "Reel", Ch, D3, 2, Art, Role,
                 $"Pulling {U(T2.ReelBodies)} or more bodies brings your next art sooner.").Gate(Pull, "tempo");
            Form("ar_breathing", "Breathing Room", Ch, D3, 2, Art, Role,
                 $"If nothing is within {U(T2.BreathingRadius)} after this art, your next {T2.BreathingBasics} basics come faster.").Gate(Push, "tempo");
            Form("ar_arrival", "Arrival", Sa, D3, 2, Art, Role,
                 $"After this art moves you, your next basic has +{P(T2.ArrivalCrit)} crit chance.").Gate(Repos, "tempo");
            Form("ar_steady", "Steady", Ch, D3, 2, Art, Role,
                 "Each hit stopped by this art brings your next art closer.").Gate(Ward, "tempo");
            Form("ar_cull", "Cull", Sa, D3, 2, Art, Role,
                 "A kill with this art brings your next art sooner.").Gate(Strike, "tempo");
            Form("ar_harvest", "Harvest", Ch, D3, 2, Art, Role,
                 $"Striking {T2.HarvestBodies} or more bodies brings your next art sooner.").Gate(Sweep, "tempo");
            Form("ar_quickfuse", "Quick Fuse", Ch, D3, 2, Art, Role,
                 "This art brings the one after it sooner.").Gate(Setup, "tempo");

            // ---- Payoff: primes the next art ----
            Form("ar_drawn_in", "Drawn In", Sa, Drop, 2, Art, Role,
                 $"Bodies pulled by this art take {P(T2.DrawnInShare)} more from your next art.").Gate(Pull, "payoff");
            Form("ar_flank", "Exposed Flank", Sa, Drop, 2, Art, Role,
                 $"A body slammed by this art into a wall or a body takes {P(T2.FlankExposed)} more for {S(T2.FlankSeconds)}.").Gate(Push, "payoff");
            Form("ar_ambush", "Ambush", Sa, Drop, 2, Art, Role,
                 $"An art within {S(T2.AmbushWindow)} after this one deals {P(T2.AmbushShare)} more.").Gate(Repos, "payoff");
            Form("ar_charge", "Charge", Me, Drop, 2, Art, Role,
                 $"Each hit stopped by this art adds {P(T2.ChargePerStop)} to its own strike.").Gate(Ward, "payoff");
            Form("ar_expose", "Expose", Sa, Drop, 2, Art, Role,
                 $"This art's main target takes {P(T2.ExposeShare)} more from your next art.").Gate(Strike, "payoff");
            Form("ar_gather", "Gather", Ch, Drop, 2, Art, Role,
                 $"Each body struck by this art past the second adds {P(T2.GatherPerBody)} to its hits.").Gate(Sweep, "payoff");
            Form("ar_deepen", "Deepen", Sa, Drop, 2, Art, Role,
                 $"Bodies struck by this art take {P(T2.DeepenShare)} more from your next art.").Gate(Setup, "payoff");

            // ---- Contagion: spreads your coat ----
            Form("ar_undertow", "Undertow", Ph, Cross, 2, Art, Role,
                 "Bodies pulled by this art take your coat, and so does the nearest body to each.").Gate(Pull, "contagion").Coat();
            Form("ar_wake", "Wake", Ph, Cross, 2, Art, Role,
                 "Bodies pushed by this art leave your coat where they land.").Gate(Push, "contagion").Coat();
            Form("ar_slipcoat", "Slipcoat", Ph, Cross, 2, Art, Role,
                 "Enemies near the line this art carried you along take your coat.").Gate(Repos, "contagion").Coat();
            Form("ar_taint", "Taint", Ph, Cross, 2, Art, Role,
                 "Every attacker stopped by this art takes your coat.").Gate(Ward, "contagion").Coat();
            Form("ar_transfuse", "Transfuse", Sa, Cross, 2, Art, Role,
                 "This art's main target passes its burn, soak and bleed to the nearest enemy.").Gate(Strike, "contagion");
            Form("ar_wash", "Wash", Ph, Cross, 2, Art, Role,
                 "Each body struck by this art coats its nearest neighbour.").Gate(Sweep, "contagion").Coat();
            Form("ar_prime", "Prime", Ph, Cross, 2, Art, Role,
                 "Bodies struck by this art take your coat.").Gate(Setup, "contagion").Coat();

            // ============================================================ ART CRAFT
            // ---- weight trios: Movement, Tempo, Force (translate between weights on a swap) ----
            Form("ac_step", "Step", Ch, Stroke, 2, Art, Craft,
                 $"A Light art steps you {U(T2.StepDistance)} toward its target first (bows: away).").Weighs(FinisherWeight.Light, "movement").Need(Verb.MoveWhileSwinging);
            Form("ac_stride", "Stride", Ch, Stroke, 2, Art, Craft,
                 $"After a Medium art, +{U(T2.StrideMove)} Move Speed for {S(T2.StrideSeconds)}.").Weighs(FinisherWeight.Medium, "movement");
            Form("ac_leap", "Leap", Ch, Stroke, 2, Art, Craft,
                 $"A Heavy art's landing throws you {U(T2.LeapDistance)} clear of the nearest enemy.").Weighs(FinisherWeight.Heavy, "movement");
            Form("ac_quicken", "Quicken", Ch, Ring, 2, Art, Craft,
                 "The basic after a Light art comes faster.").Weighs(FinisherWeight.Light, "tempo");
            Form("ac_rebound", "Rebound", Ch, Ring, 2, Art, Craft,
                 $"A Medium art striking {T2.ReboundBodies} or more brings your next art sooner.").Weighs(FinisherWeight.Medium, "tempo");
            Form("ac_settle", "Settle", Ch, Ring, 2, Art, Craft,
                 $"After a Heavy art, {P(T2.SettleChance)} chance your next art comes sooner.").Weighs(FinisherWeight.Heavy, "tempo");
            Form("ac_flick", "Flick", Me, Cross, 2, Art, Craft,
                 "A Light art knocks its main target back a step.").Weighs(FinisherWeight.Light, "force");
            Form("ac_shove", "Shove", Me, Cross, 2, Art, Craft,
                 "A Medium art pushes every body it strikes a step.").Weighs(FinisherWeight.Medium, "force");
            Form("ac_gravity", "Gravity", Me, Cross, 2, Art, Craft,
                 $"A Heavy art first draws the bodies within {U(T2.GravityRadius)} of its target in.").Weighs(FinisherWeight.Heavy, "force");

            Form("ac_spark", "Spark", Ch, Cross, 2, Art, Craft,
                 $"A Light art's first hit jumps to a second enemy for {P(T2.SparkShare)}.").Weighs(FinisherWeight.Light, "crowd");
            Form("ac_fan", "Fan", Ch, Cross, 2, Art, Craft,
                 $"Each body struck by a Medium art throws a shard at its neighbour for {P(T2.FanShare)}.").Weighs(FinisherWeight.Medium, "crowd");
            Form("ac_debris", "Debris", Me, Cross, 2, Art, Craft,
                 $"A Heavy art's landing throws back everything within {U(T2.DebrisRadius)}.").Weighs(FinisherWeight.Heavy, "crowd");

            // ---- the timing bar ----
            Form("ac_gilt", "Gilt", Ch, Bar, 2, Art, Craft,
                 "A PERFECT on this art fires every form on its socket at full strength.");
            Form("ac_steady_hand", "Steady Hand", Ph, Bar, 2, Art, Craft,
                 $"At a perfect streak of {T2.SteadyHandStreak}+, a PERFECT on this art coats every body it strikes.").Coat();
            Form("ac_flare", "Flare", Ch, Bar, 2, Art, Craft,
                 $"A PERFECT on this art throws a wave along your aim for {Hits(T2.FlareHitUnits)}.");

            // ---- the rotation ----
            Form("ac_primer", "Primer", Sa, Drop, 2, Art, Craft,
                 $"The next art deals {P(T2.PrimerShare)} more to bodies this art struck.");
            Form("ac_relay", "Relay", Ph, Drop, 2, Art, Craft,
                 "The art after this one coats every body it strikes.").Coat();
            Form("ac_reader", "Reader", Sa, Drop, 2, Art, Craft,
                 "This art reads the one before it: after a pull it hits near bodies harder, after a push far ones, " +
                 "after a reposition it can crit more, after a ward it grows with each hit stopped.");
            Form("ac_full_turn", "Full Turn", Ph, Drop, 2, Art, Craft,
                 $"If every art in the rotation fired within {S(T2.FullTurnSeconds)}, this art coats what it strikes and deals {P(T2.FullTurnShare)} more.").Coat();

            // ---- the classes ----
            Form("ac_seam", "Seam", Me, D1, 2, Art, Craft,
                 $"A ground-striking art splits the floor along your aim; the crack strikes {S(T2.SeamDelay)} later for {Hits(T2.SeamHitUnits)}.")
                .ForClasses(WeaponClass.Greatsword).Fits(ArtRoles.StrikesGround);
            Form("ac_skewer", "Skewer", Me, D2, 2, Art, Craft,
                 "A body this art runs through is thrown into the nearest other enemy.")
                .ForClasses(WeaponClass.Greatsword).Fits(ArtRoles.RunsThrough);
            Form("ac_inward_orbit", "Inward Orbit", Ph, D1, 2, Art, Craft,
                 "An orbiting art draws what it cuts a step inward.")
                .ForClasses(WeaponClass.Disc).Fits(ArtRoles.Orbits);
            Form("ac_art_splinter", "Art Splinter", Sa, D2, 2, Art, Craft,
                 $"This art's discs throw a splinter for {P(T2.ArtSplinterShare)} of each hit at the nearest other enemy.")
                .ForClasses(WeaponClass.Disc);
            Form("ac_recoil_step", "Recoil Step", Ph, D1, 2, Art, Craft,
                 $"This art steps you {U(T2.RecoilStepDistance)} away from the nearest enemy.")
                .ForClasses(WeaponClass.Bow).Need(Verb.MoveWhileSwinging);
            Form("ac_barbed", "Barbed", Sa, D2, 2, Art, Craft,
                 $"This art's arrows bleed what they strike: {P(T2.BarbedBleedShare)} of the hit a second for {S(T2.BarbedBleedSeconds)}.")
                .ForClasses(WeaponClass.Bow);
            Form("ac_kneeling_shot", "Kneeling Shot", Ch, D3, 2, Art, Craft,
                 $"A kneeling art sends one more arrow at the nearest other enemy for {Hits(T2.KneelingHitUnits)}.")
                .ForClasses(WeaponClass.Bow).Fits(ArtRoles.Kneels);

            // ---- class x element: one behaviour, the element's coat ----
            Form("ac_brand_blade", "Brand the Blade", Ch, Ring, 2, Art, Craft,
                 $"This art leaves your coat on the ground under each body it strikes for {S(T2.BrandBladeSeconds)}.")
                .ForClasses(WeaponClass.Greatsword).Coat();
            Form("ac_charged_disc", "Charged Disc", Ph, Ring, 2, Art, Craft,
                 "This art's discs coat every body they strike.").ForClasses(WeaponClass.Disc).Coat();
            Form("ac_tipped", "Tipped", Sa, Ring, 2, Art, Craft,
                 "This art's arrows coat every body they strike.").ForClasses(WeaponClass.Bow).Coat();

            // ============================================================ CHAIN (the basics)
            Form("ch_connector", "Connector", Ph, Bar, 1, FormSlot.Basics, DealKind.Chain,
                 "The basic that leads into an art coats what it strikes.").Coat().Need(Verb.Connector);
            Form("ch_lunge_step", "Lunge Step", Ch, Bar, 1, FormSlot.Basics, DealKind.Chain,
                 $"The first basic after an art steps you {U(T2.LungeStepDistance)} in (bows: out).").Need(Verb.MoveWhileSwinging);
            Form("ch_follow_through", "Follow-Through", Sa, Stroke, 1, FormSlot.Basics, DealKind.Chain,
                 $"A killing swing carries {P(T2.FollowThroughShare)} of the hit into the next body.").ForClasses(WeaponClass.Greatsword);
            Form("ch_ember_arc", "Ember Arc", Ch, Drop, 1, FormSlot.Basics, DealKind.Chain,
                 $"Every {T2.EmberArcEvery}rd basic coats what it strikes.").ForClasses(WeaponClass.Greatsword).Coat();
            Form("ch_shear", "Shear", Sa, Stroke, 1, FormSlot.Basics, DealKind.Chain,
                 $"A thrown disc cuts a second enemy close by for {P(T2.ShearShare)} of the hit.").ForClasses(WeaponClass.Disc);
            Form("ch_coated_bounce", "Coated Bounce", Ph, Drop, 1, FormSlot.Basics, DealKind.Chain,
                 "A basic disc's bounces coat what they strike.").ForClasses(WeaponClass.Disc).Coat();
            Form("ch_pinning_shot", "Pinning Shot", Me, Stroke, 1, FormSlot.Basics, DealKind.Chain,
                 $"An arrow that strikes a body near a wall roots it for {S(T2.PinningRoot)}.").ForClasses(WeaponClass.Bow);
            Form("ch_backstep", "Backstep", Ph, Cross, 1, FormSlot.Basics, DealKind.Chain,
                 $"If an enemy is within {U(T2.BackstepRadius)}, a basic loosed also steps you back.").ForClasses(WeaponClass.Bow).Need(Verb.MoveWhileSwinging);
            Form("ch_carried_tip", "Carried Tip", Sa, Drop, 1, FormSlot.Basics, DealKind.Chain,
                 "A basic arrow coats the first body it strikes.").ForClasses(WeaponClass.Bow).Coat();

            // ============================================================ RELEASE
            Plain("re_gathered", "Gathered", Ph, Ring, 2, 1, DealKind.Release,
                  $"A release first draws the bodies within {U(T2.GatheredRadius)} a step in.");
            Plain("re_clearing", "Clearing", Me, Ring, 2, 1, DealKind.Release,
                  $"A release pushes the bodies within {U(T2.ClearingRadius)} a step out.");
            Plain("re_consume", "Consume", Ch, Cross, 2, 1, DealKind.Release,
                  $"A release cashes every burn within {U(T2.ConsumeRadius)} at once.").ForElement(ElementType.Fire);
            Plain("re_fuel", "Fuel", Ch, Stroke, 1, 1, DealKind.Release,
                  $"A burning death refunds {P(T2.FuelRefund)} of your meter.").ForElement(ElementType.Fire);
            Plain("re_current", "Current", Ph, Stroke, 1, 1, DealKind.Release,
                  $"A soaked death refunds {P(T2.CurrentRefund)} of your meter.").ForElement(ElementType.Water);
            Plain("re_draw", "Draw", Ph, Cross, 2, 1, DealKind.Release,
                  $"A release pulls soaked enemies within {U(T2.DrawRadius)} a step in.").ForElement(ElementType.Water);
            Plain("re_landslide", "Landslide", Me, Cross, 2, 1, DealKind.Release,
                  $"A release throws the bodies within {U(T2.LandslideRadius)} back; one that meets a wall or a body is slammed.").ForElement(ElementType.Earth);
            Plain("re_rubble", "Rubble", Me, Stroke, 1, 1, DealKind.Release,
                  $"A release leaves rubble that slows enemies for {S(T2.RubbleSeconds)}.").ForElement(ElementType.Earth);
            Plain("re_funnel", "Funnel", Sa, Cross, 2, 1, DealKind.Release,
                  $"A release leaves a pulling wind for {S(T2.FunnelSeconds)}.").ForElement(ElementType.Air);
            Plain("re_carry", "Tailspin", Sa, Stroke, 1, 1, DealKind.Release,
                  $"A release carries you {U(T2.CarryDistance)} along your aim.").ForElement(ElementType.Air);

            // ---- release x class: one behaviour, the element's coat ----
            Plain("re_thrown_through", "Thrown Through", Ch, Drop, 2, 1, DealKind.Release,
                  "A release throws your blade along your aim, striking and coating what it passes.").ForClasses(WeaponClass.Greatsword).Coat();
            Plain("re_edge_cone", "Edge Cone", Me, Drop, 2, 1, DealKind.Release,
                  "A release cuts the cone ahead of you, striking and coating what is in it.").ForClasses(WeaponClass.Greatsword).Coat();
            Plain("re_rim_discs", "Rim Discs", Ph, Drop, 2, 1, DealKind.Release,
                  "A release flings both discs round its rim, striking and coating what stands there.").ForClasses(WeaponClass.Disc).Coat();
            Plain("re_seeded_orbit", "Seeded Orbit", Ph, Drop, 2, 1, DealKind.Release,
                  $"A release sets discs circling you for {S(T2.SkeletonOrbitSeconds)}, coating what comes close.").ForClasses(WeaponClass.Disc).Coat();
            Plain("re_answering_volley", "Answering Volley", Sa, Drop, 2, 1, DealKind.Release,
                  "A release looses an arrow at every enemy near you, coating each.").ForClasses(WeaponClass.Bow).Coat();
            Plain("re_ring_stakes", "Ring of Stakes", Sa, Drop, 2, 1, DealKind.Release,
                  $"A release stakes a ring of arrows for {S(T2.SkeletonOrbitSeconds)}; enemies crossing it are struck and coated.").ForClasses(WeaponClass.Bow).Coat();
            // ---- chain x class: the rest ----
            Form("ch_coated_opener", "Coated Opener", Ch, Ring, 1, FormSlot.Basics, DealKind.Chain,
                 "The first basic of each chain coats what it strikes.").ForClasses(WeaponClass.Greatsword).Coat();
            Form("ch_coated_catch", "Coated Catch", Ph, Ring, 1, FormSlot.Basics, DealKind.Chain,
                 "A caught disc coats the nearest enemy.").ForClasses(WeaponClass.Disc).Coat().Need(Verb.Catch);
            Form("ch_coated_kill", "Coated Kill", Sa, Ring, 1, FormSlot.Basics, DealKind.Chain,
                 "An arrow that kills coats the nearest enemy.").ForClasses(WeaponClass.Bow).Coat();
            // ---- art craft x class: the rest ----
            Form("ac_tempered_edge", "Tempered Edge", Ch, Drop, 2, Art, Craft,
                 "This art's main target takes your coat twice over.").ForClasses(WeaponClass.Greatsword).Coat();
            Form("ac_wake_rings", "Wake Rings", Ph, Drop, 2, Art, Craft,
                 "This art's last hit leaves your coat on the ground there.").ForClasses(WeaponClass.Disc).Coat();
            Form("ac_trailing_fletch", "Trailing Fletch", Sa, Drop, 2, Art, Craft,
                 "Where this art's arrows strike, your coat lingers on the ground.").ForClasses(WeaponClass.Bow).Coat();

            // ============================================================ FORTUNE
            Plain("fo_long_rift", "Long Rift", School.Ledger, D1, 1, 1, DealKind.Fortune,
                  $"A Collapsing Rift gives you {S(T2.LongRiftSeconds)} longer.").NotInShowcase();
            Plain("fo_ambush", "Ambush", School.Ledger, D2, 2, 1, DealKind.Fortune,
                  $"The first wave stays unaware {S(T2.AmbushUnawareSeconds)} longer; unaware enemies take {P(T2.AmbushTaken)} more.").Need(Verb.Unaware);
            Plain("fo_spire_echo", "Spire Echo", School.Ledger, D3, 2, 1, DealKind.Fortune,
                  "While a spire's boon holds, your first hit on each enemy coats it.").Coat();

            // ============================================================ GUARD: by defensive ability
            var Dash = DefensiveAbility.Dash; var Barrier = DefensiveAbility.Barrier;
            var Bulwark = DefensiveAbility.Bulwark; var Parry = DefensiveAbility.ParryStance;
            const FormSlot Def = FormSlot.Defence; const DealKind G = DealKind.Guard;

            Form("gd_cutting_dash", "Cutting Dash", Sa, D1, 2, Def, G, $"Bodies you dash past are struck for {Hits(T2.CuttingDashHitUnits)}.").ForDefences(Dash);
            Form("gd_afterimage", "Afterimage", Ph, D2, 2, Def, G, "Where a dash starts, an afterimage holds off the attacks of enemies near it.").ForDefences(Dash);
            Form("gd_landing", "Landing", Sa, D3, 2, Def, G, $"The first basic after a dash has +{P(T2.LandingCrit)} crit chance.").ForDefences(Dash);
            Form("gd_wake_dash", "Wake", Ph, Bar, 2, Def, G, $"Bodies you dash past are slowed {P(1f - T2.WakeDashSlow)} for {S(T2.WakeDashSeconds)}.").ForDefences(Dash);

            Form("gd_rebuke", "Rebuke", Ch, D1, 2, Def, G, $"A shot blocked by the dome strikes its shooter for {Hits(T2.RebukeHitUnits)}.").ForDefences(Barrier);
            Form("gd_plant", "Plant", Me, D2, 2, Def, G, $"A blow blocked by the dome staggers its attacker for {S(T2.PlantStagger)}.").ForDefences(Barrier);
            Form("gd_sap", "Sap", Ph, D3, 2, Def, G, $"Each hit blocked by the dome refunds {P(T2.SapRefund)} of your meter.").ForDefences(Barrier);
            Form("gd_bulldoze", "Bulldoze", Me, Bar, 2, Def, G, "As the dome drops, it pushes what is in front of it.").ForDefences(Barrier);

            Form("gd_backlash", "Backlash", Ch, D1, 2, Def, G, $"Blows taken during your Bulwark are banked; when it ends, {P(T2.BacklashShare)} of them bursts around you.").ForDefences(Bulwark);
            Form("gd_mire_aura", "Mire Aura", Ph, D2, 2, Def, G, $"Enemies within {U(T2.AuraRadius)} are slowed while your Bulwark is up.").ForDefences(Bulwark);
            Form("gd_hold", "Hold", Me, D3, 2, Def, G, "An enemy that strikes you during your Bulwark is pushed back a step.").ForDefences(Bulwark);
            Form("gd_taunt", "Taunt", Me, Bar, 2, Def, G, $"While your Bulwark is up, enemies within {U(T2.TauntRadius)} are drawn toward you.").ForDefences(Bulwark);

            Form("gd_counterweight", "Counterweight", Me, D1, 2, Def, G, $"A parry staggers its attacker for {S(T2.CounterweightStagger)}.").ForDefences(Parry);
            Form("gd_disarm", "Disarm", Me, D2, 2, Def, G, $"A parried enemy holds off its attacks for {S(T2.DisarmSeconds)}.").ForDefences(Parry);
            Form("gd_read", "Read", Sa, D3, 2, Def, G, $"A parry marks its attacker: your next {T2.ReadHits} hits on it have +{P(T2.ReadCrit)} crit chance.").ForDefences(Parry);
            Form("gd_echo_guard", "Echo Guard", Ch, Bar, 2, Def, G, "A parry brings your next art sooner.").ForDefences(Parry);

            // ---- defence x element: the coat ----
            Form("gd_coat_trail", "Trail", Ch, Ring, 2, Def, G, "A dash coats what it passes.").ForDefences(Dash).Coat();
            Form("gd_coat_dome", "Coated Dome", Ph, Ring, 2, Def, G, "A hit blocked by the dome coats its attacker.").ForDefences(Barrier).Coat();
            Form("gd_coat_aura", "Coated Aura", Me, Ring, 2, Def, G, $"While your Bulwark is up, it coats the enemies within {U(T2.AuraRadius)} every {S(T2.AuraTick)}.").ForDefences(Bulwark).Coat();
            Form("gd_coat_burst", "Coat Burst", Sa, Ring, 2, Def, G, $"A parry coats every enemy within {U(T2.CoatBurstRadius)}.").ForDefences(Parry).Coat();

            // ============================================================ CONCORDS: weapon + defence, the element's coat
            Concord("cc_gs_dash_a", "Trailing Blade", WeaponClass.Greatsword, Dash, "The blade trails your dash, cutting what it passes.");
            Concord("cc_gs_dash_b", "Left Arc", WeaponClass.Greatsword, Dash, "A dash leaves the blade's arc behind; it cuts a moment later.");
            Concord("cc_gs_barrier_a", "Wound Blade", WeaponClass.Greatsword, Barrier, "Blows blocked by the dome wind up the blade: your next basic sweeps a full circle.");
            Concord("cc_gs_barrier_b", "Edged Dome", WeaponClass.Greatsword, Barrier, "The dome's rim cuts the enemies that touch it.");
            Concord("cc_gs_bulwark_a", "Planted Blade", WeaponClass.Greatsword, Bulwark, "The planted blade cuts each enemy that strikes you.");
            Concord("cc_gs_bulwark_b", "Driven Blade", WeaponClass.Greatsword, Bulwark, "When your Bulwark ends, the blade drives down: a heavy blow around you.");
            Concord("cc_gs_parry_a", "Answer", WeaponClass.Greatsword, Parry, "A parry answers at once with a cut toward the attacker.");
            Concord("cc_gs_parry_b", "Crown of Blades", WeaponClass.Greatsword, Parry, "Every third parry on a floor rings you with blades.");
            Concord("cc_disc_dash_a", "Left Behind", WeaponClass.Disc, Dash, "Discs left where a dash begins hold for a second, then fly back through you.");
            Concord("cc_disc_dash_b", "Thrown Ahead", WeaponClass.Disc, Dash, "A dash throws both discs ahead; they cut the line you land on.");
            Concord("cc_disc_barrier_a", "Rim Throw", WeaponClass.Disc, Barrier, "The dome's rim throws a disc at each attacker it blocks.");
            Concord("cc_disc_barrier_b", "Face Orbit", WeaponClass.Disc, Barrier, "Discs turn on the dome's face while it is up, cutting what nears it.");
            Concord("cc_disc_bulwark_a", "Close Orbit", WeaponClass.Disc, Bulwark, "Discs circle close while your Bulwark is up, cutting what comes near.");
            Concord("cc_disc_bulwark_b", "Return Fire", WeaponClass.Disc, Bulwark, "Each blow your Bulwark takes sends a disc at its attacker.");
            Concord("cc_disc_parry_a", "Parry Orbit", WeaponClass.Disc, Parry, $"Discs circle you for {S(T2.ConcordOrbitSeconds)} after a parry, striking what is close.");
            Concord("cc_disc_parry_b", "Caught Shot", WeaponClass.Disc, Parry, "A parried blow is caught on a disc and thrown back at its attacker.");
            Concord("cc_bow_dash_a", "Parting Shot", WeaponClass.Bow, Dash, "A dash looses an arrow back at the nearest enemy.");
            Concord("cc_bow_dash_b", "Staked Trap", WeaponClass.Bow, Dash, "A dash leaves an arrow staked where it started; it bursts when an enemy reaches it.");
            Concord("cc_bow_barrier_a", "Turned Shot", WeaponClass.Bow, Barrier, "A shot blocked by the dome is loosed back at its shooter as your arrow.");
            Concord("cc_bow_barrier_b", "Embrasure", WeaponClass.Bow, Barrier, "While the dome is up, an arrow flies from it at the nearest enemy in front.");
            Concord("cc_bow_bulwark_a", "Ring of Stakes", WeaponClass.Bow, Bulwark, "Arrows stake round you as your Bulwark rises; enemies crossing them are struck.");
            Concord("cc_bow_bulwark_b", "Closing Rain", WeaponClass.Bow, Bulwark, "When your Bulwark ends, arrows rain around you.");
            Concord("cc_bow_parry_a", "Pinned", WeaponClass.Bow, Parry, "A parry pins its attacker to the ground.");
            Concord("cc_bow_parry_b", "Free Arrow", WeaponClass.Bow, Parry, "A parry nocks a free arrow: your next basic shot strikes for double.");

            // ============================================================ HUMOUR
            Plain("hu_deepening", "Deepening", Sa, Stroke, 1, 1, DealKind.Humour,
                  $"Bleeds grow {P(T2.DeepeningStep)} per {T2.DeepeningKillsPerStep} bleeding kills, to {P(T2.DeepeningCap)}.");
            Plain("hu_settling", "Settling", Me, Stroke, 1, 1, DealKind.Humour,
                  $"Staggers last {S(T2.SettlingStep)} longer per staggered kill this floor, to {S(T2.SettlingCap)}.");
            Plain("hu_wildfire", "Wildfire", Ch, Cross, 1, 1, DealKind.Humour,
                  "A burn jumps to an enemy touching the burning one.");
            Plain("hu_ripple", "Ripple", Ph, Cross, 1, 1, DealKind.Humour,
                  $"A soak also soaks one other enemy within {U(T2.RippleReach)}, for half as long.");
            Plain("hu_splatter", "Splatter", Sa, Cross, 1, 1, DealKind.Humour,
                  $"A bleeding enemy's death bleeds the nearest enemy within {U(T2.SplatterReach)}.");
            Plain("hu_topple", "Topple", Me, Cross, 1, 1, DealKind.Humour,
                  $"A staggered enemy's death staggers those within {U(T2.ToppleReach)}.");

            // ============================================================ COSTS
            // ---- Toll ----
            Price("co_spent_meter", "Spent Meter", Sa, D1, 2, 3, DealKind.Toll,
                  $"A release from under half meter costs {P(T2.SpentMeterHealth)} max health.",
                  capName: "Empty Hands", capText: "Releases from under half meter also stun you briefly.");
            Price("co_overdrawn", "Overdrawn", Ch, D2, 2, 3, DealKind.Toll,
                  $"A weapon art that isn't PERFECT costs {P(T2.OverdrawnHealth)} max health.",
                  capName: "Overdrawn III", capText: "A GOOD costs it too.");
            Price("co_fatigue", "Fatigue", Ch, D3, 2, 3, DealKind.Toll,
                  $"After {S(T2.FatigueAfter)} on a floor, -{U(T2.FatigueSpeed)} Attack Speed every {S(T2.FatigueEvery)}.",
                  capName: "Exhaustion", capText: "It never stops climbing.");

            // ---- Rule: classes and defences ----
            Price("co_overswing", "Overswing", Me, D1, 2, 3, DealKind.Rule,
                  $"A basic that hits nothing locks you for {S(T2.OverswingStun)}.",
                  capName: "Overreach", capText: "Twice as long.").ForClasses(WeaponClass.Greatsword);
            Price("co_unsteady", "Unsteady", Sa, D2, 2, 3, DealKind.Rule,
                  $"A basic shot loosed while moving deals {P(T2.UnsteadyDamage)} less.",
                  capName: "Shaking Hands", capText: "A planted shot deals less too.").ForClasses(WeaponClass.Bow).Block(Verb.MoveWhileShooting);
            Price("co_skid", "Skid", Ch, D1, 1, 3, DealKind.Rule,
                  $"A dash slides {U(T2.SkidDistance)} past its end.",
                  capName: "Runaway", capText: "The slide is twice as long.").ForDefences(Dash);
            Price("co_narrow", "Narrow", Me, D2, 1, 3, DealKind.Rule,
                  "The dome covers a narrower arc.", (m, n) => m.BarrierDotAdd += T2.NarrowDotAdd * n,
                  capName: "Slit", capText: "Narrower still.").ForDefences(Barrier);
            Price("co_heavy_bulwark", "Heavy Bulwark", Me, D3, 1, 3, DealKind.Rule,
                  $"-{U(T2.HeavyBulwarkMove)} Move Speed while your Bulwark is up.",
                  capName: "Leaden Guard", capText: "Twice as heavy.").ForDefences(Bulwark);
            Price("co_off_balance", "Off Balance", Sa, Bar, 1, 3, DealKind.Rule,
                  $"A parry pressed with nothing to parry staggers you for {S(T2.OffBalanceStun)}.",
                  capName: "Off Balance III", capText: "Twice as long.").ForDefences(Parry);

            Price("co_loose_catch", "Loose Catch", Ph, Stroke, 2, 3, DealKind.Rule,
                  $"One catch in {Mathf.RoundToInt(1f / T2.LooseCatchChance)} fumbles, stalling you for {S(T2.LooseCatchStun)}.",
                  capName: "Butterfingers", capText: "Twice as often.").ForClasses(WeaponClass.Disc).Block(Verb.Catch);

            // ---- Host: the enemies ----
            Price("co_affixed", "Affixed", Me, D1, 2, 3, DealKind.Host,
                  $"Elites get a modifier {P(T2.AffixedChanceAdd)} more often per stack.",
                  (m, n) => { m.AffixChanceAdd += T2.AffixedChanceAdd * n; if (n >= 3) m.AffixFromBandOne = true; },
                  capName: "Marked Host", capText: "Elites roll them from the first band.");
            Price("co_crowded", "Crowded", Ch, D2, 2, 3, DealKind.Host,
                  "One more enemy may be on screen at once per stack.",
                  (m, n) => m.PressureAdd += T2.CrowdedPressure * n + (n >= 3 ? 2f * T2.CrowdedPressure : 0f),
                  capName: "Throng", capText: "Two more on top.");
            Price("co_vigilant", "Vigilant", Me, D3, 2, 1, DealKind.Host,
                  "The first wave sees you as you arrive.", (m, n) => m.NoUnaware = true).Block(Verb.Unaware);
            Price("co_eager", "Eager", Ch, Bar, 3, 2, DealKind.Host,
                  "One more enemy may attack at once per stack.",
                  (m, n) => m.ExtraTurns += T2.EagerTurns * n,
                  capName: "Frenzy", capText: "Enemies take their turns sooner after one another.");
            Price("co_swift", "Swift", Ch, Ring, 2, 3, DealKind.Host,
                  $"Enemies move {P(T2.SwiftMove)} faster per stack. Their attacks don't change.",
                  (m, n) => m.EnemyMoveMul *= 1f + T2.SwiftMove * n,
                  capName: "Swift III", capText: "Elites another 10% on top.");
            Price("co_wrath", "Wrath", Ch, Cross, 2, 3, DealKind.Host,
                  $"Elites below {P(T2.WrathBelow)} health wait {P((1f - T2.WrathCooldownMul) / 3f)} less between attacks per stack.",
                  (m, n) => m.WrathCooldownMul = Mathf.Min(m.WrathCooldownMul, Mathf.Lerp(1f, T2.WrathCooldownMul, n / 3f)),
                  capName: "Wrath III", capText: $"Every enemy below {P(T2.WrathBelow)} health does too.");
            Price("co_martyrs", "Martyrs", Sa, Drop, 2, 3, DealKind.Host,
                  $"Enemies burst at their feet {S(T2.MartyrDelay)} after dying.",
                  capName: "Martyrs III", capText: "The burst is wider.");

            // ---- Ground: the room ----
            Price("co_storm", "Storm", Sa, D1, 2, 3, DealKind.Ground,
                  $"Every {S(T2.StormEvery)}, a strike lands where you stood {S(T2.StormLag)} ago.",
                  capName: "Tempest", capText: "Twice as often.");
            Price("co_gale_floor", "Gale", Ph, D2, 2, 3, DealKind.Ground,
                  $"Every {S(T2.GaleEvery)}, a wind crosses the floor, pushing everything.",
                  capName: "Gale III", capText: "Stronger, and more often.");
            Price("co_sinking", "Sinking", Me, D3, 2, 3, DealKind.Ground,
                  $"After standing still for {S(T2.SinkingAfter)}, you move slower for your first second.",
                  capName: "Mire Floor", capText: "Twice as slow.").Block(Verb.StandStill);
            Price("co_slick", "Slick", Ph, Bar, 1, 1, DealKind.Ground,
                  "You slide a little when you stop.", (m, n) => m.ExtraSlide += T2.SlickSlide);

            // ---- the world costs, second batch (2026-11-04, the user's "finish what was left out") ----
            Price("co_plated", "Plated", Me, Stroke, 2, 3, DealKind.Host,
                  $"Unarmored enemies arrive with armor worth {P(T2.PlatedShare)} of their health per stack.",
                  (m, n) => m.PlatedShare += T2.PlatedShare * n,
                  capName: "Plated III", capText: "Elites too.");
            Price("co_reinforced", "Reinforced", Me, Drop, 2, 3, DealKind.Host,
                  "An elite's death calls one more enemy per stack.",
                  capName: "Reinforced III", capText: "One more on top.");
            Price("co_volley", "Volley", Ph, Cross, 2, 1, DealKind.Host,
                  "Ranged enemies fire one more bolt with every shot.", (m, n) => m.ExtraBolts += T2.VolleyExtraBolts);
            Price("co_twisted", "Twisted", Me, Ring, 3, 1, DealKind.Host,
                  "The next mini-boss arrives a band stronger.", (m, n) => m.BossBandAdd += T2.TwistedBands).Branded().NotInShowcase();
            Price("co_encroach", "Encroach", Me, D1, 2, 3, DealKind.Ground,
                  $"Every {S(T2.EncroachEvery)} on a floor, a spike pit opens near you after a warning.",
                  capName: "Encroach III", capText: "Twice as often.");
            Price("co_bloom", "Hazard Bloom", Ch, D2, 2, 3, DealKind.Ground,
                  $"Pits hurt you {P(T2.BloomTaken)} more per stack.",
                  capName: "Bloom III", capText: "Sand slows you twice as much too.");
            Price("co_narrowing", "Narrowing", Me, D3, 2, 3, DealKind.Ground,
                  $"After {S(T2.NarrowingAfter)} on a floor, the edges of the room burn you; each stack widens them.",
                  capName: "Narrowing III", capText: "From half that time.");
            Price("co_crumble", "Crumble", Me, Bar, 2, 3, DealKind.Ground,
                  $"Every {S(T2.CrumbleEvery)} on a floor, a column falls.",
                  capName: "Crumble III", capText: "Twice as often.");
            Price("co_dim", "Dim", Ph, Ring, 1, 3, DealKind.Ground,
                  $"The room's lights are {P(T2.DimShare)} smaller per stack. A pit's own glow stays.",
                  capName: "Dim III", capText: "Darker still.");
            Price("co_tide", "Tide", Ph, Cross, 1, 1, DealKind.Ground,
                  "Each floor starts with shallow water near the middle.");
            Price("co_spite", "Spite Spires", School.Ledger, Stroke, 1, 3, DealKind.Ground,
                  $"Taking a spire's boon costs {P(T2.SpiteHealth)} max health per stack.",
                  capName: "Spite III", capText: "The deal after a spire's boon can't be refused.");
            Price("co_dead_ground", "Dead Ground", Me, Drop, 2, 1, DealKind.Ground,
                  "Pits hurt you twice as much until you clear a floor without touching one.").Sworn();

            // ---- Socket: paid on the slot a form is inscribed on; stacks as forms join it ----
            Price("co_slow_link", "Slow Link", Me, D1, 1, 3, DealKind.Socket,
                  "The basic that leads into an art is slower.", capName: "Cursed Chain", capText: "Each art also costs 2% max health.").PaysOn(FormSlot.Basics);
            Price("co_planted_link", "Planted Link", Me, D2, 1, 3, DealKind.Socket,
                  "You can't move during the basic that leads into an art.", capName: "Cursed Chain", capText: "Each art also costs 2% max health.").PaysOn(FormSlot.Basics);
            Price("co_dull_link", "Dull Link", Ph, D3, 1, 3, DealKind.Socket,
                  "The basic that leads into an art can't burn, soak, bleed or stagger.", capName: "Cursed Chain", capText: "Each art also costs 2% max health.").PaysOn(FormSlot.Basics).Block(Verb.Connector);
            Price("co_spill", "Spill", Ph, D3, 1, 3, DealKind.Socket,
                  $"A release spills another {P(T2.SpillShare)} of your meter.", capName: "Cursed Release", capText: "Each release also costs 2% max health.").PaysOn(FormSlot.Release);
            Price("co_damp", "Damp", Ph, D1, 1, 3, DealKind.Socket,
                  $"Your release's area is {P(1f - T2.DampRadiusMul)} smaller.", capName: "Cursed Release", capText: "Each release also costs 2% max health.").PaysOn(FormSlot.Release);
            Price("co_kick", "Kick", Ch, D2, 1, 3, DealKind.Socket,
                  $"A release knocks you {U(T2.KickDistance)} back.", capName: "Cursed Release", capText: "Each release also costs 2% max health.").PaysOn(FormSlot.Release);
            Price("co_strain", "Strain", Me, D1, 2, 3, DealKind.Socket,
                  $"Your defensive ability recharges {P(T2.StrainCooldown)} slower.",
                  (m, n) => m.DefenseCooldownMul *= 1f + T2.StrainCooldown,
                  capName: "Cursed Guard", capText: "Each use also costs 2% max health.").PaysOn(FormSlot.Defence);
            Price("co_brittle_guard", "Brittle Guard", Sa, D2, 2, 3, DealKind.Socket,
                  $"Your parry window is {P(1f - T2.BrittleGuardWindow)} shorter.",
                  (m, n) => m.DefenseWindowMul *= T2.BrittleGuardWindow,
                  capName: "Cursed Guard", capText: "Each use also costs 2% max health.").PaysOn(FormSlot.Defence);
            Price("co_exertion", "Exertion", Sa, D3, 1, 3, DealKind.Socket,
                  $"Using your defensive ability costs {P(T2.ExertionHealth)} max health.",
                  capName: "Cursed Guard", capText: "Each use also costs 2% max health.").PaysOn(FormSlot.Defence);

            // ============================================================ THE NEW ALBEDOS (transform, never invert)
            Albedo("al_spent_meter", "Second Draught", "co_spent_meter", Sa, D1, $"A release from under half meter heals {P(T2.SpentMeterHeal)}.");
            Albedo("al_overdrawn", "Earnest", "co_overdrawn", Ch, D2, $"A weapon art that isn't PERFECT deals {P(T2.OverdrawnArt)} more.");
            Albedo("al_fatigue", "Second Wind of War", "co_fatigue", Ch, D3,
                   $"After {S(T2.FatigueAfter)} on a floor, +{U(T2.SecondWindDamage)} Damage every {S(T2.FatigueEvery)}, to +{U(T2.SecondWindDamageCap)}.");
            Albedo("al_overswing", "Follow Swing", "co_overswing", Me, D1, $"A basic that hits nothing throws a crescent for {P(T2.OverswingCrescentShare)} of a hit.");
            Albedo("al_unsteady", "Running Shot", "co_unsteady", Sa, D2, "A basic shot loosed while moving pierces one more body.");
            Albedo("al_skid", "Carving Slide", "co_skid", Ch, D1, $"A dash's end cuts the bodies around you for {Hits(T2.SkidCutHitUnits)}.");
            Albedo("al_narrow", "Keen Dome", "co_narrow", Me, D2, $"The dome's edge staggers what touches it for {S(T2.NarrowEdgeStagger)}.");
            Albedo("al_heavy_bulwark", "Rooted Guard", "co_heavy_bulwark", Me, D3, "If you stand still while your Bulwark is up, enemies that strike you are slowed.");
            Albedo("al_off_balance", "Feint", "co_off_balance", Sa, Bar, "A parry pressed with nothing to parry pushes the enemies around you back.");
            Albedo("al_affixed", "Spoils", "co_affixed", Me, D1, $"An elite with a modifier heals you {P(T2.AffixedHeal)} when it dies.");
            Albedo("al_crowded", "Press", "co_crowded", Ch, D2, $"+{P(T2.CrowdedPerBody)} damage for each body on screen past {U(T2.CrowdedFrom)}, to +{P(T2.CrowdedCap)}.");
            Albedo("al_eager", "Overeager", "co_eager", Ch, Bar, $"An enemy whose attack misses you takes {P(T2.EagerExposed)} more for {S(T2.EagerExposedSeconds)}.");
            Albedo("al_swift", "Momentum", "co_swift", Ch, Ring, $"Bodies you push travel {P(T2.SwiftPushMul - 1f)} farther.");
            Albedo("al_wrath", "Quarry", "co_wrath", Ch, Cross, "Killing an elite under 30% health brings your next art sooner.");
            Albedo("al_martyrs", "Pyres", "co_martyrs", Sa, Drop, $"Enemies burst at their feet when they die, striking other enemies for {Hits(T2.MartyrEnemyHitUnits)}.");
            Albedo("al_storm", "Called Storm", "co_storm", Sa, D1, $"Every {S(T2.StormEvery)}, a strike lands where the nearest enemy stood for {Hits(T2.StormEnemyHitUnits)}.");
            Albedo("al_gale_floor", "Following Wind", "co_gale_floor", Ph, D2, "The wind pushes only enemies.");
            Albedo("al_sinking", "Bedrock Floor", "co_sinking", Me, D3, $"After {S(T2.SinkingAfter)} standing still, enemies that strike you are rooted for {S(T2.SinkingRoot)}.");
            Albedo("al_plated", "Cracked Plate", "co_plated", Me, Stroke, $"Armor you break leaves its enemy taking {P(T2.PlatedExposed)} more for {S(T2.PlatedExposedSeconds)}.");
            Albedo("al_reinforced", "Fallen Captain", "co_reinforced", Me, Drop, $"An elite's death staggers the enemies within {U(T2.ReinforcedRadius)} for {S(T2.ReinforcedStagger)}.");
            Albedo("al_encroach", "Pit Trap", "co_encroach", Me, D1, "Enemies pushed or caught in pits take three times as much from them.");
            Albedo("al_bloom", "Pit Dancer", "co_bloom", Ch, D2, $"While you stand in a pit, +{U(T2.BloomDamage)} Damage.");
            Albedo("al_narrowing", "Closing Ring", "co_narrowing", Me, D3, "Enemies near the edges of the room are slowed.");
            Albedo("al_crumble", "Landslip", "co_crumble", Me, Bar, $"A column that breaks strikes the enemies within {U(T2.CrumbleRadius)} for {Hits(T2.CrumbleHitUnits)}.");
            Albedo("al_dim", "Lantern", "co_dim", Ph, Ring, $"Enemies standing in a light take {P(T2.LanternTaken)} more.");
            A("al_spite", "Kept Spire", "co_spite", ExchangeFamily.Ledger, Stroke, "Spire boons last one more floor.",
              (m, n) => m.SpireExtraFloors += 1).Deal = DealKind.SetPiece;
            Albedo("al_loose_catch", "Edge Catch", "co_loose_catch", Ph, Stroke, $"A fumbled catch bursts round you for {Hits(T2.LooseCatchBurstHitUnits)}.");
            Albedo("al_dull_link", "Deep Link", "co_dull_link", Ph, D3, "The basic that leads into an art renews the burn and the soak on its target.");
            Albedo("al_spill", "Spillway", "co_spill", Ph, D3, $"A release coats every enemy within {U(T2.SpillCoatRadius)}.");
            Albedo("al_slow_link", "Weighted Link", "co_slow_link", Me, D1, $"The basic that leads into an art deals {P(T2.SlowLinkArt)} more.");
            Albedo("al_planted_link", "Anchored Link", "co_planted_link", Me, D2, $"The basic that leads into an art staggers what it strikes for {S(T2.PlantedLinkStagger)}.");
            Albedo("al_damp", "Pressed Release", "co_damp", Ph, D1, $"Your release deals {P(T2.DampCentreBonus)} more.");
            Albedo("al_kick", "Recoil Cut", "co_kick", Ch, D2, $"A release's kick cuts the bodies you pass for {Hits(T2.KickCutHitUnits)}.");
            Albedo("al_strain", "Coiled Guard", "co_strain", Me, D1, $"Each use of your defensive ability adds {P(T2.StrainBank)} to your next art.");
            Albedo("al_brittle_guard", "Glass Guard", "co_brittle_guard", Sa, D2, $"A parry staggers every enemy within {U(T2.BrittleGuardRadius)} for {S(T2.BrittleGuardStagger)}.");
            Albedo("al_exertion", "Breath Held", "co_exertion", Sa, D3, $"Using your defensive ability heals {P(T2.ExertionHeal)}.");
            Albedo("al_heavy_hand", "Bulwark Art", "heavy_hand", Me, Ring, $"While this art locks you, you take {P(1f - T2.HeavyHandBlowMul)} less from blows.");
            Albedo("al_dulled_script", "Shared Script", "dulled_script", Ph, D3, $"This art throws {P(T2.DulledScriptSpread)} of each hit at the nearest other enemy.");
            Albedo("al_brittle_script", "Stunning Script", "brittle_script", Ch, D2, $"This art staggers what it strikes for {S(T2.BrittleScriptStagger)}.");
        }

        static ExchangeEntry Concord(string id, string name, WeaponClass cls, DefensiveAbility def, string effect)
        {
            var school = def switch
            {
                DefensiveAbility.Dash => School.Sanguine, DefensiveAbility.Barrier => School.Phlegm,
                DefensiveAbility.Bulwark => School.Melancholy, _ => School.Choler,
            };
            var e = B(id, name, FamilyOf(school), ExchangeMark.Cross, 3, 3, effect + " Your element finishes it.", conditional: true)
                    .Cap("Concord III", $"It strikes {P(2f * T2.ConcordStackStep)} harder than at I.")
                    .Inscribed(FormSlot.Defence).ForClasses(cls).ForDefences(def).Coat();
            e.School = school;
            e.Deal = DealKind.Concord;
            return e;
        }

        /// <summary>
        /// The KIND of every entry the overhaul didn't build (the user's catalogue, re-filed -
        /// EXCHANGE-OVERHAUL.md section 3), and the existing socket costs made curses that stack.
        /// </summary>
        static void AssignKinds()
        {
            void K(DealKind k, string ids) { foreach (var id in ids.Split(' ')) if (_byId.TryGetValue(id, out var e) && e.Deal == DealKind.None) e.Deal = k; }
            K(DealKind.Chain, "short_chain reiteration first_blood fault_line spindrift lone_cut splinter lodestar snapback thornfield patient_string long_ember");
            K(DealKind.Release, "residue overflow twin_spark attunement banked_embers high_tide deep_roots tailwind");
            K(DealKind.Guard, "second_wind aegis_cycle ghostwalk reactive_plate bloodletters_pact lacerate afterburn");
            K(DealKind.Humour, "tinder backdraft tidewater saturate serrated lancet knell pin_down kindling swell");
            K(DealKind.Vessel, "whetstone quickening vein_finder executioner long_reach heavy_payoff thickened_hide equipoise rich_vein elixir dilation green_lion wide_arc fletching ricochet fleetfoot evanescence stonestance");
            K(DealKind.Fortune, "curator transmuters_eye scrying_glass lodestone prima_materia extra_sigil ouroboros eagle pyre drawdown salamander gnome undine sylph");
            K(DealKind.ArtCraft, "aftershock shockwave riptide");
            K(DealKind.Frailty, "dulled heavy_arms cold_iron dross short_arm thin_blood paper_guard slow_knit anchored encumbered stubborn_ore leaky_vessel overcommitted rust smother low_water becalmed");
            K(DealKind.Toll, "blood_price toll backfire withering souring open_stance unbloodied blood_debt caput_mortuum indenture debt");
            K(DealKind.Rule, "fumbler rooted long_chain retrograde wandering_eye fog restless sealed_sands");
            K(DealKind.Host, "induration");
            K(DealKind.Ground, "projection dead_weight");
            K(DealKind.Socket, "heavy_hand dulled_script brittle_script");
            foreach (var e in _all)
                if (e.Deal == DealKind.None)
                    e.Deal = e.Kind == ExchangeKind.Cost
                        ? (e.Origin == EntryOrigin.Putrefaction ? DealKind.Toll : DealKind.Frailty)
                        : DealKind.SetPiece;   // Albedos, combinations, unions, evolutions

            // The old socket costs become curses that stack as forms join their socket (a Nigredo at III).
            foreach (var id in new[] { "heavy_hand", "dulled_script", "brittle_script" })
                if (_byId.TryGetValue(id, out var sc))
                {
                    sc.MaxStacks = 3;
                    sc.Slot = FormSlot.Art;
                    sc.Cap("Cursed Art", "Firing this art also costs 2% max health.");
                }

            // The dead-trigger guard's existing pairs.
            if (_byId.TryGetValue("rooted", out var rooted)) rooted.Blocks |= Verb.MoveWhileSwinging;
            if (_byId.TryGetValue("deep_roots", out var roots)) roots.Needs |= Verb.StandStill;
            if (_byId.TryGetValue("vein_finder", out var vf)) vf.Needs |= Verb.Crit;
            if (_byId.TryGetValue("snapback", out var snap)) snap.Needs |= Verb.Catch;   // Loose Catch takes the catch

            // ONE WEIGHT LIGHTER (the tuning, 2026-11-04): entries the Assay found situational by nature -
            // within sane numbers they can't reach their weight's worth, so the scales promise less.
            foreach (var id in "ac_flare ac_gilt ac_gravity ac_inward_orbit ac_kneeling_shot ac_quicken ac_rebound ac_seam ac_settle ac_skewer ac_stride ar_arrival ar_bowl ar_breathing ar_carry ar_collide ar_cull ar_gather ar_harvest ar_reel ar_slam ar_slipstream cc_bow_barrier_a cc_bow_parry_b fo_spire_echo gd_bulldoze gd_counterweight gd_cutting_dash gd_echo_guard gd_hold gd_landing gd_mire_aura gd_plant gd_rebuke gd_sap gd_taunt gd_wake_dash re_consume re_draw re_funnel re_gathered".Split(' '))
                if (_byId.TryGetValue(id, out var lighter)) lighter.Weight = Mathf.Max(1, lighter.Weight - 1);

            // THE LEAN'S TAGS (2026-11-04): which way an entry moves the crowd, and a humour entry's part.
            void I(Intent i, string ids) { foreach (var id in ids.Split(' ')) if (_byId.TryGetValue(id, out var e)) e.Intent = i; }
            void Pt(StatusPart p, string ids) { foreach (var id in ids.Split(' ')) if (_byId.TryGetValue(id, out var e)) e.Part = p; }
            I(Intent.Gather, "ar_collide ar_held ar_reel ar_drawn_in ar_undertow ac_gravity gd_taunt re_gathered re_draw re_funnel drawdown ac_inward_orbit ar_link ar_gather ar_harvest");
            I(Intent.Scatter, "ar_bowl ar_slam ar_breathing ar_flank ar_wake ac_flick ac_shove ac_debris gd_hold gd_bulldoze re_clearing re_landslide ar_shockfront ac_recoil_step ch_backstep ac_leap");
            Pt(StatusPart.Source, "tinder tidewater serrated knell");
            Pt(StatusPart.Payoff, "backdraft saturate lancet pin_down hu_splatter hu_topple hu_wildfire hu_ripple steam clot");
            Pt(StatusPart.Growth, "kindling swell hu_deepening hu_settling");
        }

        // ---------------------------------------------------------------- the overhaul's fluent half

        static ExchangeEntry Gate(this ExchangeEntry e, ArtRole role, string family)
        {
            e.RoleGate = role;
            e.RoleFamily = family;
            return e;
        }

        static ExchangeEntry Weighs(this ExchangeEntry e, FinisherWeight weight, string trio)
        {
            e.WeightGate = weight;
            e.RoleFamily = "w_" + trio;
            return e;
        }

        static ExchangeEntry Fits(this ExchangeEntry e, Func<string, bool> fits)
        {
            e.ArtFits = fits;
            return e;
        }

        static ExchangeEntry ForDefences(this ExchangeEntry e, params DefensiveAbility[] defences)
        {
            e.Defences = defences;
            return e;
        }

        static ExchangeEntry Coat(this ExchangeEntry e) { e.Coated = true; return e; }
        static ExchangeEntry Need(this ExchangeEntry e, Verb v) { e.Needs |= v; return e; }
        static ExchangeEntry Block(this ExchangeEntry e, Verb v) { e.Blocks |= v; return e; }

        static ExchangeEntry PaysOn(this ExchangeEntry e, FormSlot slot)
        {
            e.SocketCost = true;
            e.Slot = slot;
            return e;
        }
    }
}
