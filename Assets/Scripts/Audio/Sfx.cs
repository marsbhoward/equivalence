using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Audio
{
    public enum Sound
    {
        StepStone, StepSand, StepWater, StepAsh, StepMetal,
        HitBasic, HitLight, HitMedium, HitHeavy, Crit,
        PlayerHurt, EnemyDeath, PlayerDeath,
        Perfect, Good, Miss,
        Distant,
        Swing, SwingHeavy,
        ReleaseFire, ReleaseWater, ReleaseEarth, ReleaseAir,
        Growl, Snarl, Charge, Zap, Lob, Explosion, Hissing, Crackle, Grind, Slam, Roar,
        Bubbling, Pop, FuseTick, Flame, Swipe,
        FloorClear, DoorOpen, DistantForest, DistantSky, DistantSalt, Alert,
        Sing, Toll, Erupt, Blink, Enrage, Vulnerable, Seal, BossDeath,
        Rattle, GazeLook, Petrify, Push, Pull, Chip, Rubble,
        Arrive, ArriveNigredo,
    }

    public enum Bed { None, Hub, Crypt, Deep, Abyss, Throne, Boss, Forest, Sky, Salt, Nigredo }

    /// <summary>
    /// The player's three volume dials, kept in PlayerPrefs. Master goes straight onto the
    /// AudioListener; Effects and Ambience scale their own voices.
    /// </summary>
    public static class Volume
    {
        const string KMaster = "audio.master", KEffects = "audio.effects", KAmbience = "audio.ambience";

        public static float Master { get => Get(KMaster, Tuning.Audio.DefaultMaster); set { Set(KMaster, value); AudioListener.volume = value; } }
        public static float Effects { get => Get(KEffects, 1f); set => Set(KEffects, value); }
        public static float Ambience { get => Get(KAmbience, 1f); set => Set(KAmbience, value); }

        static float Get(string k, float d) { try { return Mathf.Clamp01(PlayerPrefs.GetFloat(k, d)); } catch { return d; } }
        static void Set(string k, float v) { try { PlayerPrefs.SetFloat(k, Mathf.Clamp01(v)); PlayerPrefs.Save(); } catch { } }

        public static void Apply() => AudioListener.volume = Master;
    }

    /// <summary>
    /// One-shot sounds. <see cref="Play"/> is the whole interface: it picks a variant, jitters the
    /// pitch, pans by where the sound happened against the camera, and refuses a play that would
    /// pile onto the same sound inside its <c>Tuning.Audio</c> spacing - a disc volley hitting
    /// five bodies on one frame is one sound, not five stacked into a clip.
    ///
    /// The voices live on one hidden <see cref="AudioHost"/> object, found again by type after a
    /// domain reload (a static reference does not survive one; the object does).
    /// </summary>
    public static class Sfx
    {
        static Dictionary<Sound, AudioClip[]> _clips = new();
        static Dictionary<Sound, float> _lastAt = new();
        static readonly System.Random _pick = new(12345);   // never UnityEngine.Random - seeded rolls depend on its state

        const int SynthVariants = 4;

        /// <summary><paramref name="ambient"/> plays on the Ambience dial instead of Effects.</summary>
        public static void Play(Sound sound, Vector2? at = null, float gain = 1f, bool ambient = false)
            => Play(sound, -1, at, gain, ambient);

        /// <summary>A CHOSEN variant (<paramref name="variant"/> 0 up; -1 picks at random) - the
        /// Cantor's note for a slice is that slice's, every time.</summary>
        public static void Play(Sound sound, int variant, Vector2? at = null, float gain = 1f, bool ambient = false)
        {
            var host = AudioHost.Get();
            if (host == null) return;

            _lastAt ??= new();
            float now = Time.unscaledTime;
            float spacing = Tuning.Audio.Spacing(sound);
            if (_lastAt.TryGetValue(sound, out float last) && now - last < spacing) return;
            _lastAt[sound] = now;

            var clip = Pick(sound, variant);
            if (clip == null) return;

            float pan = 0f;
            if (at.HasValue)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector2 off = at.Value - (Vector2)cam.transform.position;
                    pan = Mathf.Clamp(off.x / Tuning.Audio.PanWidth, -1f, 1f) * Tuning.Audio.PanAmount;
                    // Off screen is quieter, never silent - a tell from out of sight still warns.
                    float past = off.magnitude - Tuning.Audio.FalloffFrom;
                    if (past > 0f) gain *= Mathf.Max(Tuning.Audio.FalloffFloor, 1f - past / Tuning.Audio.FalloffOver);
                }
            }
            float pitch = 1f + ((float)_pick.NextDouble() * 2f - 1f) * Tuning.Audio.PitchJitter(sound);
            host.Voice(clip, Tuning.Audio.Loudness(sound) * gain * (ambient ? Volume.Ambience : Volume.Effects), pitch, pan);
        }

        static AudioClip Pick(Sound sound, int variant = -1)
        {
            _clips ??= new();
            if (!_clips.TryGetValue(sound, out var set) || set == null || set.Length == 0 || set[0] == null)
            {
                set = Recorded(sound);
                if (set == null || set.Length == 0) set = Build(sound);
                _clips[sound] = set;
            }
            return variant >= 0 ? set[variant % set.Length] : set[_pick.Next(set.Length)];
        }

        static AudioClip[] Recorded(Sound s)
        {
            var g = GameSound.Instance;
            if (g == null) return null;
            AudioClip[] one(AudioClip c) => c != null ? new[] { c } : null;
            AudioClip[] many(AudioClip[] a) => a != null && a.Length > 0 && a[0] != null ? a : null;
            return s switch
            {
                Sound.StepStone => many(g.StepStone), Sound.StepSand => many(g.StepSand),
                Sound.StepWater => many(g.StepWater), Sound.StepAsh => many(g.StepAsh),
                Sound.StepMetal => many(g.StepMetal),
                Sound.HitBasic => many(g.HitBasic), Sound.HitLight => many(g.HitLight),
                Sound.HitMedium => many(g.HitMedium), Sound.HitHeavy => many(g.HitHeavy),
                Sound.Crit => many(g.Crit), Sound.PlayerHurt => many(g.PlayerHurt),
                Sound.EnemyDeath => many(g.EnemyDeath), Sound.PlayerDeath => one(g.PlayerDeath),
                Sound.Perfect => one(g.Perfect), Sound.Good => one(g.Good), Sound.Miss => one(g.Miss),
                Sound.Distant => many(g.Distant), Sound.DistantForest => many(g.DistantForest), Sound.DistantSky => many(g.DistantSky), Sound.DistantSalt => many(g.DistantSalt), Sound.Alert => many(g.Alert), Sound.Arrive => many(g.Arrive), Sound.ArriveNigredo => one(g.ArriveNigredo),
                Sound.Swing => many(g.Swing), Sound.SwingHeavy => many(g.SwingHeavy),
                Sound.ReleaseFire => one(g.ReleaseFire), Sound.ReleaseWater => one(g.ReleaseWater),
                Sound.ReleaseEarth => one(g.ReleaseEarth), Sound.ReleaseAir => one(g.ReleaseAir),
                Sound.Growl => many(g.Growl), Sound.Snarl => many(g.Snarl), Sound.Charge => one(g.Charge),
                Sound.Zap => many(g.Zap), Sound.Lob => many(g.Lob), Sound.Explosion => many(g.Explosion),
                Sound.Hissing => many(g.Hissing), Sound.Crackle => many(g.Crackle), Sound.Grind => many(g.Grind),
                Sound.Slam => many(g.Slam), Sound.Roar => many(g.Roar), Sound.Bubbling => many(g.Bubbling),
                Sound.Pop => many(g.Pop), Sound.FuseTick => one(g.FuseTick), Sound.Flame => many(g.Flame),
                Sound.Swipe => many(g.Swipe), Sound.FloorClear => one(g.FloorClear), Sound.DoorOpen => one(g.DoorOpen),
                Sound.Sing => many(g.Sing), Sound.Toll => many(g.Toll), Sound.Erupt => many(g.Erupt),
                Sound.Blink => many(g.Blink), Sound.Enrage => one(g.Enrage), Sound.Vulnerable => one(g.Vulnerable),
                Sound.Seal => one(g.Seal), Sound.BossDeath => one(g.BossDeath), Sound.Rattle => many(g.Rattle),
                Sound.GazeLook => one(g.GazeLook), Sound.Petrify => one(g.Petrify), Sound.Push => one(g.Push),
                Sound.Pull => one(g.Pull), Sound.Chip => many(g.Chip), Sound.Rubble => many(g.Rubble),
                _ => null,
            };
        }

        static AudioClip[] Build(Sound s)
        {
            int n = s switch
            {
                Sound.PlayerDeath or Sound.Perfect or Sound.Good or Sound.Miss => 1,
                Sound.ReleaseFire or Sound.ReleaseWater or Sound.ReleaseEarth or Sound.ReleaseAir => 1,
                Sound.Charge or Sound.FuseTick or Sound.FloorClear or Sound.DoorOpen => 1,
                Sound.Roar or Sound.Explosion or Sound.Flame or Sound.Slam => 2,
                Sound.Sing or Sound.Toll => 8,   // one per Cantor slice - the variant IS the note
                Sound.Enrage or Sound.Vulnerable or Sound.Seal or Sound.BossDeath or Sound.GazeLook
                    or Sound.Petrify or Sound.Push or Sound.Pull => 1,
                Sound.Rubble or Sound.Erupt or Sound.Blink => 3,
                Sound.Arrive => 4,   // one per place: Tower, Forest, Sky, Salt
                Sound.ArriveNigredo => 1,
                Sound.Distant => 8,
                Sound.DistantForest => 6,
                Sound.DistantSky => 6,
                Sound.DistantSalt => 6,
                _ => SynthVariants,
            };
            var set = new AudioClip[n];
            for (int i = 0; i < n; i++)
            {
                int seed = (int)s * 101 + i * 17 + 3;
                float[] data = s switch
                {
                    Sound.StepStone => Synth.StepStone(seed),
                    Sound.StepSand => Synth.StepSand(seed),
                    Sound.StepWater => Synth.StepWater(seed),
                    Sound.StepAsh => Synth.StepAsh(seed),
                    Sound.StepMetal => Synth.StepMetal(seed),
                    Sound.HitBasic => Synth.Hit(0, seed),
                    Sound.HitLight => Synth.Hit(1, seed),
                    Sound.HitMedium => Synth.Hit(2, seed),
                    Sound.HitHeavy => Synth.Hit(3, seed),
                    Sound.Crit => Synth.Crit(seed),
                    Sound.PlayerHurt => Synth.PlayerHurt(seed),
                    Sound.EnemyDeath => Synth.EnemyDeath(seed),
                    Sound.PlayerDeath => Synth.PlayerDeath(),
                    Sound.Perfect => Synth.Perfect(),
                    Sound.Good => Synth.Good(),
                    Sound.Miss => Synth.Miss(),
                    Sound.Distant => Synth.Distant(i % 4, seed),
                    Sound.DistantForest => Synth.DistantForest(i % 3, seed),
                    Sound.DistantSky => Synth.DistantSky(i % 3, seed),
                    Sound.DistantSalt => Synth.DistantSalt(i % 3, seed),
                    Sound.Alert => Synth.Alert(seed),
                    Sound.Swing => Synth.Swing(false, seed),
                    Sound.SwingHeavy => Synth.Swing(true, seed),
                    Sound.ReleaseFire => Synth.Release(ElementType.Fire, seed),
                    Sound.ReleaseWater => Synth.Release(ElementType.Water, seed),
                    Sound.ReleaseEarth => Synth.Release(ElementType.Earth, seed),
                    Sound.ReleaseAir => Synth.Release(ElementType.Air, seed),
                    Sound.Growl => Synth.Growl(82f + i * 6f, seed),
                    Sound.Snarl => Synth.Growl(165f + i * 10f, seed),
                    Sound.Charge => Synth.Charge(300f, 1300f, 0.45f),
                    Sound.Zap => Synth.Zap(seed),
                    Sound.Lob => Synth.Lob(seed),
                    Sound.Explosion => Synth.Explosion(seed),
                    Sound.Hissing => Synth.Hissing(seed),
                    Sound.Crackle => Synth.Crackle(seed),
                    Sound.Grind => Synth.Grind(seed),
                    Sound.Slam => Synth.Slam(seed),
                    Sound.Roar => Synth.Roar(seed),
                    Sound.Bubbling => Synth.Bubbling(seed),
                    Sound.Pop => Synth.Pop(seed),
                    Sound.FuseTick => Synth.FuseTick(),
                    Sound.Flame => Synth.Flame(seed),
                    Sound.Swipe => Synth.Swipe(seed),
                    Sound.FloorClear => Synth.FloorClear(),
                    Sound.DoorOpen => Synth.DoorOpen(),
                    Sound.Sing => Synth.Sing(Synth.SliceNote(i)),
                    Sound.Toll => Synth.Toll(Synth.SliceNote(i)),
                    Sound.Erupt => Synth.Erupt(seed),
                    Sound.Blink => Synth.Blink(seed),
                    Sound.Enrage => Synth.Enrage(),
                    Sound.Vulnerable => Synth.Vulnerable(),
                    Sound.Seal => Synth.Seal(),
                    Sound.BossDeath => Synth.BossDeath(),
                    Sound.Rattle => Synth.Rattle(seed),
                    Sound.GazeLook => Synth.GazeLook(),
                    Sound.Petrify => Synth.Petrify(seed),
                    Sound.Push => Synth.Rush(false, seed),
                    Sound.Pull => Synth.Rush(true, seed),
                    Sound.Chip => Synth.Chip(seed),
                    Sound.Rubble => Synth.Rubble(seed),
                    Sound.Arrive => Synth.Arrive(i, seed),
                    Sound.ArriveNigredo => Synth.Arrive(4, seed),
                    _ => new float[Synth.Rate / 10],
                };
                set[i] = Synth.Clip($"{s}_{i}", data);
            }
            return set;
        }
    }

    /// <summary>
    /// The hidden object every sound plays from: a pool of 2D voices (oldest stolen when all are
    /// busy) and the <see cref="Ambience"/> beds. Created on first use, never destroyed.
    /// </summary>
    public class AudioHost : MonoBehaviour
    {
        static AudioHost _host;

        [SerializeField] AudioSource[] _voices;
        [SerializeField] int _next;

        public static AudioHost Get()
        {
            if (_host != null) return _host;
            _host = FindAnyObjectByType<AudioHost>();
            if (_host != null) return _host;
            if (!Application.isPlaying) return null;

            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            _host = go.AddComponent<AudioHost>();
            go.AddComponent<Ambience>();
            Volume.Apply();
            return _host;
        }

        public void Voice(AudioClip clip, float volume, float pitch, float pan)
        {
            if (_voices == null || _voices.Length == 0)
            {
                _voices = new AudioSource[Tuning.Audio.Voices];
                for (int i = 0; i < _voices.Length; i++) _voices[i] = NewSource(false);
            }

            // A free voice first; with none free, the next in turn (the oldest) is stolen.
            AudioSource v = null;
            for (int i = 0; i < _voices.Length; i++)
            {
                var c = _voices[(_next + i) % _voices.Length];
                if (c != null && !c.isPlaying) { v = c; break; }
            }
            if (v == null) { v = _voices[_next % _voices.Length]; _next++; }
            if (v == null) return;

            v.Stop();
            v.clip = clip;
            v.volume = volume;
            v.pitch = pitch;
            v.panStereo = pan;
            v.Play();
        }

        public AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            // Pausing the game must not cut a sound off mid-ring; the pause is time, not audio.
            s.ignoreListenerPause = true;
            return s;
        }
    }
}
