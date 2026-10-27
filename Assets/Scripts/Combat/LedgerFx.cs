using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// The pictures the exchange's boons and costs, the board's rules and the principles draw (the
    /// user's group-3 picks off https://claude.ai/artifact/RxLnUrQNPfkFSDzqcE3iK8, 2026-10-26).
    /// They replaced the soft <c>Spr.Flash</c>/<c>Spr.Pulse</c> at every one of those moments; each
    /// is a <see cref="TexelFx"/> one-shot or a component of its own (RigRim, LedgerFloorMark,
    /// StateIcons' clock, Seasoning's pips). Kept here so a call site is one line and a band's
    /// picture is changed in one place.
    /// </summary>
    public static class LedgerFx
    {
        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static Color32[] R(params string[] h) { var r = new Color32[h.Length]; for (int i = 0; i < h.Length; i++) r[i] = C(h[i]); return r; }

        public static readonly Color WardColour = new(0.7f, 0.85f, 1f), VirdisColour = new(0.55f, 0.9f, 0.45f), ShieldGold = new(1f, 0.85f, 0.45f);
        public static readonly Color32[] Ward = R("#ffffff", "#dcecff", "#b3d6ff", "#7aa6e0", "#4a6aa8", "#24345a");
        public static readonly Color32[] Violet = R("#f6eeff", "#d2b8ff", "#a07ff2", "#6c50d0", "#3e2b8a", "#1e1450");
        public static readonly Color32[] Lilac = R("#f8f4ff", "#ddd4f6", "#bfb2f0", "#8a7cc8", "#544a88", "#2a2548");
        public static readonly Color32[] Sage = R("#f6fbea", "#d8e6bc", "#bfd998", "#8aa868", "#566c40", "#2c3820");
        public static readonly Color32[] Blood = R("#ffd8dc", "#f0606e", "#c81e30", "#8a1020", "#4e0810", "#28040a");
        public static readonly Color32[] Steam = R("#ffffff", "#eef2f6", "#d2d8e0", "#a8b0bc", "#78808c", "#484e58");
        public static readonly Color32[] Water = R("#f2fdff", "#a8ecff", "#56c6f2", "#2c86cc", "#1b4f92", "#0f2a52");
        public static readonly Color32[] Coup = R("#ffffff", "#ffd0c8", "#ff4a3a", "#c02020", "#701010", "#380808");

        static Vector2 Feet(Component c) => ReleaseCanvas.Feet(c.transform);
        static int Under(Component c) => ReleaseCanvas.UnderBody(c.transform);

        /// <summary>Something readied (Wake, Ouroboros, Maturation, Unshackled, Adamant): one spark
        /// rising behind the player from the feet to over the head.</summary>
        public static void Spark(Component player)
        {
            if (player == null) return;
            var rig = player.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            Vector2 feet = Feet(player);
            float height = rig != null ? Player.PlayerController.HeadTop(rig).y - feet.y + 0.15f : 1.1f;
            TexelFx.Rise(feet, TexelFx.Gold, Mathf.Max(0.4f, height), Under(player));
        }

        /// <summary>Second Wind (gold) or the Phoenix (flame): wings from behind the shoulders.</summary>
        public static void Wings(Component player, bool phoenix)
        {
            if (player == null) return;
            var rig = player.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            Vector2 feet = Feet(player);
            float head = rig != null ? Player.PlayerController.HeadTop(rig).y : feet.y + 1f;
            var shoulders = new Vector2(player.transform.position.x, Mathf.Lerp(feet.y, head, 0.62f));
            TexelFx.Wings(shoulders, phoenix ? TexelFx.Fire : TexelFx.Gold, Under(player));
        }

        /// <summary>A burst of force round a point (Tin Ward, Antipathy, Knell, Gale, Salt's
        /// return, a corpse bursting): the shockwave DOTTED, in the entry's colours - the solid
        /// ring is the Brimstone Heart's blast, a danger (the user's call, 2026-10-26).</summary>
        public static void Force(Vector2 at, Color32[] ramp, float radius)
            => TexelFx.Shock(at, ramp, TexelFx.Stone, radius, dotted: true);

        /// <summary>The same round the player, from their feet.</summary>
        public static void ForceRound(Component player, Color32[] ramp, float radius)
        {
            if (player != null) Force(Feet(player), ramp, radius);
        }

        /// <summary>Fire round a point (Phlogiston, the Shockwave/Nova forms, Cineration).</summary>
        public static void FireRing(Vector2 at, float radius) => TexelFx.FlameRing(at, TexelFx.Fire, radius);

        /// <summary>What a burst is made of, thrown out to its radius ("its own stuff"): drops,
        /// or steel shards.</summary>
        public static void Stuff(Vector2 at, Color32[] ramp, float radius, bool shards = false)
            => TexelFx.Spray(at, ramp, radius, Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 14, 36), 0.45f, true, shards, TexelFx.Air);

        /// <summary>Steam rolling out to its radius.</summary>
        public static void SteamCloud(Vector2 at, float radius)
            => TexelFx.Puff(at, Steam, 44, radius * TexelFx.Body * 0.5f, 0.6f);

        /// <summary>A status cashed in: its colour bursting off the body in one puff.</summary>
        public static void StatusBurst(Health body, Color32[] ramp)
        {
            if (body != null) TexelFx.Spray(body.transform.position, ramp, 0.45f, 16, 0.3f, false, false, TexelFx.Air);
        }

        /// <summary>Coup de Grace: a red-white cut hanging across the body.</summary>
        public static void CoupLine(Health body)
        {
            if (body != null) TexelFx.Cut(body.transform.position, Coup, new Vector2(1f, -0.55f), 0.5f);
        }

        /// <summary>Armour pierced or stripped: steel flakes off the body, falling.</summary>
        public static void Plates(Vector2 at) => TexelFx.Spray(at, TexelFx.Steel, 0.4f, 10, 0.5f, true, true, TexelFx.Air);

        /// <summary>A strike that repeats: the swing's arc sweeping the body again in pale gold.</summary>
        public static void Echo(Vector2 at, Vector2 from)
            => TexelFx.Arc(at - (at - from).normalized * 0.2f, TexelFx.Gold, at - from, 0.45f, TexelFx.Air, 0.9f);

        /// <summary>Along the aim: a crest running out and breaking at the reach.</summary>
        public static void Crest(Vector2 from, Vector2 dir, float length, Color32[] ramp)
            => TexelFx.Crest(from, ramp, dir, length);
    }
}
