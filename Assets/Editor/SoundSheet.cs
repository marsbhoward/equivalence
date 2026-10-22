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
            return $"{n} files in {dir}";
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
