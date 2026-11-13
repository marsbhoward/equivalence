using System.Linq;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The Forge's STARTING KIT: a new character's first visit (the user's call, 2026-11-12) - a
    /// weapon, then a defensive, each card its strength and its weakness as the code plays them
    /// (Tuning: Bow/Disc/Defense). The other tabs wait until it is chosen; the screen then moves on
    /// to OPEN A BOX, or closes when a run was waiting on the choice.
    /// </summary>
    public partial class ForgeScreen
    {
        int _kitStep, _kitWeapon, _kitDefence;

        void ResetKit() { _kitStep = 0; _kitWeapon = 0; _kitDefence = 0; }

        static readonly (WeaponClass Class, string Name, string Strength, string Weakness)[] KitWeapons =
        {
            (WeaponClass.Greatsword, "GREATSWORD",
                "The fastest attacks in the game, cutting through everything in reach.",
                "Melee only - you fight inside their reach."),
            (WeaponClass.Disc, "DISCS",
                "Two ways to fight - heavy blows up close, ricocheting throws at range.",
                "Short reach up close, and throws hit softly near you."),
            (WeaponClass.Bow, "BOW",
                "The longest reach. Planted, at range, every arrow lands in full.",
                "The slowest shots, one target each, weaker on the move or up close."),
        };

        static readonly (DefensiveAbility Ability, string Name, string Strength, string Weakness)[] KitDefensives =
        {
            (DefensiveAbility.Dash, "DASH",
                "A long burst away, untouchable while it lasts.",
                "Safe for a quarter of a second, then a 5-second wait."),
            (DefensiveAbility.Barrier, "BARRIER",
                "Stops every hit from in front for 2 seconds.",
                "Open behind and to the sides."),
            (DefensiveAbility.Bulwark, "BULWARK",
                "Half damage from every side.",
                "Lasts only a second, and the other half still lands."),
            (DefensiveAbility.ParryStance, "PARRY STANCE",
                "Ready again in 2.5 seconds, twice as often as the rest.",
                "Nothing but the parry - mistime it and the blow lands in full."),
        };

        void BuildKit()
        {
            Whole();
            bool weapon = _kitStep == 0;

            // The two steps, the first ticked once it is behind.
            var steps = Stack(48f, 18f);
            void Step(float x, string number, string label, bool now, bool done)
            {
                var dot = UiKit.Panel(steps, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, -22f), new Vector2(x + 44f, 22f),
                    done ? Hex(0x1f3a24) : now ? Primary : QuietBg);
                Txt(dot, done ? "X" : number, 24, done ? Good : now ? PrimaryInk : Dim, TextAnchor.MiddleCenter);
                Txt(UiKit.Rect(steps, "l", Vector2.zero, new Vector2(0f, 1f), new Vector2(x + 58f, 0f), new Vector2(x + 400f, 0f)),
                    label, 26, done ? Good : now ? OnInk : Off);
            }
            Step(0f, "1", weapon ? "WEAPON" : KitWeapons[_kitWeapon].Name, weapon, !weapon);
            UiKit.Panel(steps, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370f, -1f), new Vector2(430f, 1f), Hex(0x3a332c));
            Step(450f, "2", "DEFENSIVE", !weapon, false);

            Txt(Stack(56f, 6f), weapon ? "CHOOSE YOUR WEAPON" : "CHOOSE YOUR DEFENSIVE", 44, TitleInk);
            Para(weapon ? "Bronze, and yours to keep. The other two stay in your inventory - change at any time."
                        : "Your chest carries it, the others stay in your inventory. Each one also parries a blow that lands as you press it.",
                 BodySize, Dim, 76f, 18f);

            var row = Rest();
            int n = weapon ? KitWeapons.Length : KitDefensives.Length;
            for (int i = 0; i < n; i++)
            {
                var (name, strength, weakness) = weapon
                    ? (KitWeapons[i].Name, KitWeapons[i].Strength, KitWeapons[i].Weakness)
                    : (KitDefensives[i].Name, KitDefensives[i].Strength, KitDefensives[i].Weakness);
                bool on = i == (weapon ? _kitWeapon : _kitDefence);
                var card = Framed(row, new Vector2(i / (float)n, 0f), new Vector2((i + 1) / (float)n, 1f),
                    new Vector2(i == 0 ? 0f : 9f, 0f), new Vector2(i == n - 1 ? 0f : -9f, 0f), on, on ? OnBg : RowBg);

                float y = -20f;
                if (weapon)
                {
                    var look = KitLook(KitWeapons[i].Class);
                    if (look != null)
                        PiecePicture(UiKit.Rect(card, "art", new Vector2(0f, 1f), Vector2.one, new Vector2(20f, y - 150f), new Vector2(-20f, y)),
                            look, null, Vector2.zero, Vector2.one);
                    y -= 162f;
                }
                Txt(UiKit.Rect(card, "t", new Vector2(0f, 1f), Vector2.one, new Vector2(20f, y - 50f), new Vector2(-20f, y)),
                    name, 34, on ? OnInk : Ink, TextAnchor.MiddleCenter);
                y -= 64f;
                Txt(UiKit.Rect(card, "d", Vector2.zero, Vector2.one, new Vector2(24f, 20f), new Vector2(-24f, y)),
                    $"{Tint("STRENGTH", Good)}  {strength}\n\n{Tint("WEAKNESS", Bad)}  {weakness}",
                    28, Hex(0xe6d6bd), TextAnchor.UpperLeft, wrap: true).lineSpacing = 1.05f;

                int chosen = i;
                Act(card, () =>
                {
                    if (weapon) _kitWeapon = chosen; else _kitDefence = chosen;
                    Rebuild();
                });
            }

            if (weapon)
                Footer($"Comes with the {WeaponName(KitWeapons[_kitWeapon].Class)} relic", Dim,
                    ("NEXT", true, true, () => { _kitStep = 1; Rebuild(); }));
            else
                Footer(null, Dim,
                    ("BACK", false, true, () => { _kitStep = 0; Rebuild(); }),
                    ("EQUIP", true, true, ChooseKit));
        }

        /// <summary>The kit's weapon of a class, as its menu art shows it. A character from the earlier
        /// kit holds one weapon only - the choice gives it the rest - so a class it lacks shows the kit
        /// tier's look for that class (DemoGear.TierLookFor, seeded by the class: the same every time).</summary>
        GearItem KitLook(WeaponClass cls)
        {
            var r = Gear.Find(g => g.InstanceId.StartsWith("KIT-") && g.Slot == GearSlot.Weapon && g.Class == cls);
            string look = r?.Look ?? DemoGear.TierLookFor(GearSlot.Weapon, Tuning.Showcase.KitTier, cls, DemoGear.StableHash(cls.ToString()));
            return string.IsNullOrEmpty(look) ? null : GearCatalog.Get(look);
        }

        void ChooseKit()
        {
            _ctx.OnChooseKit?.Invoke(KitWeapons[_kitWeapon].Class, KitDefensives[_kitDefence].Ability);
            if (!IsOpen) return;   // a run was waiting on the choice
            _mode = Mode.Open;
            ResetSteps();
            _scroll = 0f;
            Rebuild();
        }
    }
}
