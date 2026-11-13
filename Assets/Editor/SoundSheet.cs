using System.IO;
using UnityEngine;
using Convergence.Audio;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Writes every placeholder sound to WAV files, so they can be judged by ear outside the game
    /// (and replaced one by one through Resources/GameSound). Not Play mode - pure synthesis.
    ///
    ///   unity command eval --code 'return Convergence.EditorTools.SoundSheet.Export("/some/dir");'
    /// </summary>
    public static class SoundSheet
    {
        public static string Export(string dir)
        {
            Directory.CreateDirectory(dir);
            int n = 0;
            void W(string name, float[] s) { Write(Path.Combine(dir, name + ".wav"), s); n++; }

            for (int i = 0; i < 2; i++)
            {
                W($"step_stone_{i}", Synth.StepStone(i)); W($"step_sand_{i}", Synth.StepSand(i));
                W($"step_water_{i}", Synth.StepWater(i)); W($"step_ash_{i}", Synth.StepAsh(i));
                W($"step_metal_{i}", Synth.StepMetal(i));
            }
            string[] weights = { "basic", "light", "medium", "heavy" };
            for (int w = 0; w < 4; w++) W($"hit_{weights[w]}", Synth.Hit(w, 1));
            W("crit", Synth.Crit(1)); W("player_hurt", Synth.PlayerHurt(1));
            W("enemy_death", Synth.EnemyDeath(1)); W("player_death", Synth.PlayerDeath());
            W("bar_perfect", Synth.Perfect()); W("bar_good", Synth.Good()); W("bar_miss", Synth.Miss());
            string[] distant = { "drip", "stones", "chain", "groan" };
            for (int k = 0; k < 4; k++) W($"distant_{distant[k]}", Synth.Distant(k, k + 5));
            W("swing", Synth.Swing(false, 1)); W("swing_heavy", Synth.Swing(true, 1));
            foreach (Core.ElementType e in new[] { Core.ElementType.Fire, Core.ElementType.Water,
                                                  Core.ElementType.Earth, Core.ElementType.Air })
                W($"release_{e.ToString().ToLowerInvariant()}", Synth.Release(e, 1));
            W("chaser_growl", Synth.Growl(82f, 1)); W("dasher_snarl", Synth.Growl(165f, 1));
            W("ranged_charge", Synth.Charge(300f, 1300f, 0.45f)); W("ranged_zap", Synth.Zap(1));
            W("mortar_lob", Synth.Lob(1)); W("explosion", Synth.Explosion(1)); W("bomb_hiss", Synth.Hissing(1));
            W("turret_crackle", Synth.Crackle(1)); W("gargoyle_grind", Synth.Grind(1)); W("gargoyle_slam", Synth.Slam(1));
            W("booster_roar", Synth.Roar(1)); W("bubbles_blow", Synth.Bubbling(1)); W("bubble_pop", Synth.Pop(1));
            W("fuse_tick", Synth.FuseTick()); W("flame", Synth.Flame(1)); W("enemy_swipe", Synth.Swipe(1));
            W("floor_clear", Synth.FloorClear()); W("door_open", Synth.DoorOpen());
            for (int k = 0; k < 8; k++) { W($"cantor_sing_{k}", Synth.Sing(Synth.SliceNote(k))); W($"cantor_toll_{k}", Synth.Toll(Synth.SliceNote(k))); }
            W("cantor_erupt", Synth.Erupt(1)); W("boss_blink", Synth.Blink(1)); W("boss_enrage", Synth.Enrage());
            W("boss_vulnerable", Synth.Vulnerable()); W("boss_seal", Synth.Seal()); W("boss_death", Synth.BossDeath());
            W("medusa_rattle", Synth.Rattle(1)); W("medusa_gaze", Synth.GazeLook()); W("medusa_petrify", Synth.Petrify(1));
            W("medusa_push", Synth.Rush(false, 1)); W("medusa_pull", Synth.Rush(true, 1));
            W("stone_chip", Synth.Chip(1)); W("stone_rubble", Synth.Rubble(1));
            foreach (Bed b in System.Enum.GetValues(typeof(Bed)))
                if (b != Bed.None) W($"bed_{b.ToString().ToLowerInvariant()}", Synth.Bed(b));
            foreach (var (name, data) in Feedback()) W(name, data);
            return $"{n} files in {dir}";
        }

        /// <summary>The 2026-11-13 pass's sounds, by file name.</summary>
        static (string, float[])[] Feedback() => new[]
        {
            ("dodge", Synth.Dodge(1)), ("ward", Synth.Ward()), ("bulwark", Synth.BulwarkUp()), ("parry_ready", Synth.ParryReady(1)),
            ("blocked", Synth.Blocked(1)), ("empower", Synth.Empower()), ("lock_on", Synth.LockTick(true)), ("lock_off", Synth.LockTick(false)),
            ("meter_full", Synth.MeterFull()), ("level_up", Synth.LevelUp()),
            ("afflict_burn", Synth.Afflict(0, 1)), ("afflict_soak", Synth.Afflict(1, 1)), ("afflict_bleed", Synth.Afflict(2, 1)), ("afflict_stagger", Synth.Afflict(3, 1)),
            ("pickup", Synth.Pickup(1)), ("box_pickup", Synth.BoxPickup(1)), ("rift_open", Synth.RiftOpen()), ("rift_close", Synth.RiftClose(1)),
            ("spikes", Synth.Spikes(1)), ("gust", Synth.Gust(1)), ("fall", Synth.Fall(1)), ("thud", Synth.Thud(1)),
            ("plate_press", Synth.PlatePress(1)), ("puzzle_solved", Synth.PuzzleSolved()), ("puzzle_failed", Synth.PuzzleFailed()),
            ("deal_taken", Synth.DealTaken(1)), ("equip", Synth.Equip(1)), ("shutter", Synth.Shutter()), ("anvil", Synth.Anvil(1)),
            ("crate", Synth.Crate(1)), ("place", Synth.Place(1)), ("transmute", Synth.Transmute()),
            ("ui_move", Synth.UiTick()), ("ui_confirm", Synth.UiTwoTone(true)), ("ui_back", Synth.UiTwoTone(false)),
        };

        /// <summary>The pass's sounds plus a few old ones for reference, each scaled by its in-game loudness
        /// (Tuning.Audio.Loudness) so a listening page compares them as the game would play them.</summary>
        public static string ExportPass(string dir)
        {
            Directory.CreateDirectory(dir);
            float L(Sound s) => Core.Tuning.Audio.Loudness(s);
            float[] Scaled(float[] d, float k) { var c = (float[])d.Clone(); for (int i = 0; i < c.Length; i++) c[i] *= k; return c; }
            var names = new System.Collections.Generic.Dictionary<string, Sound>
            {
                ["dodge"] = Sound.Dodge, ["ward"] = Sound.Ward, ["bulwark"] = Sound.Bulwark, ["parry_ready"] = Sound.ParryReady,
                ["blocked"] = Sound.Blocked, ["empower"] = Sound.Empower, ["lock_on"] = Sound.LockOn, ["lock_off"] = Sound.LockOff,
                ["meter_full"] = Sound.MeterFull, ["level_up"] = Sound.LevelUp, ["afflict_burn"] = Sound.Afflict,
                ["afflict_soak"] = Sound.Afflict, ["afflict_bleed"] = Sound.Afflict, ["afflict_stagger"] = Sound.Afflict,
                ["pickup"] = Sound.Pickup, ["box_pickup"] = Sound.BoxPickup, ["rift_open"] = Sound.RiftOpen, ["rift_close"] = Sound.RiftClose,
                ["spikes"] = Sound.Spikes, ["gust"] = Sound.Gust, ["fall"] = Sound.Fall, ["thud"] = Sound.Thud,
                ["plate_press"] = Sound.PlatePress, ["puzzle_solved"] = Sound.PuzzleSolved, ["puzzle_failed"] = Sound.PuzzleFailed,
                ["deal_taken"] = Sound.DealTaken, ["equip"] = Sound.Equip, ["shutter"] = Sound.Shutter, ["anvil"] = Sound.Anvil,
                ["crate"] = Sound.Crate, ["place"] = Sound.Place, ["transmute"] = Sound.Transmute,
                ["ui_move"] = Sound.UiMove, ["ui_confirm"] = Sound.UiConfirm, ["ui_back"] = Sound.UiBack,
            };
            int n = 0;
            foreach (var (name, data) in Feedback()) { Write(Path.Combine(dir, name + ".wav"), Scaled(data, L(names[name]))); n++; }
            // References already in the game, at their own loudness.
            Write(Path.Combine(dir, "ref_hit_medium.wav"), Scaled(Synth.Hit(2, 1), L(Sound.HitMedium)));
            Write(Path.Combine(dir, "ref_swing.wav"), Scaled(Synth.Swing(false, 1), L(Sound.Swing)));
            Write(Path.Combine(dir, "ref_release_fire.wav"), Scaled(Synth.Release(Core.ElementType.Fire, 1), L(Sound.ReleaseFire)));
            Write(Path.Combine(dir, "ref_floor_clear.wav"), Scaled(Synth.FloorClear(), L(Sound.FloorClear)));
            Write(Path.Combine(dir, "ref_door_open.wav"), Scaled(Synth.DoorOpen(), L(Sound.DoorOpen)));
            return $"{n + 5} files in {dir}";
        }

        /// <summary>16-bit mono PCM.</summary>
        static void Write(string path, float[] s)
        {
            using var f = new BinaryWriter(File.Create(path));
            int bytes = s.Length * 2;
            f.Write("RIFF".ToCharArray()); f.Write(36 + bytes); f.Write("WAVE".ToCharArray());
            f.Write("fmt ".ToCharArray()); f.Write(16); f.Write((short)1); f.Write((short)1);
            f.Write(Synth.Rate); f.Write(Synth.Rate * 2); f.Write((short)2); f.Write((short)16);
            f.Write("data".ToCharArray()); f.Write(bytes);
            foreach (var v in s) f.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}
