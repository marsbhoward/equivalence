using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Audio
{
    /// <summary>
    /// The room tone: one looping BED per place (the hub, and four bands of depth - crypt, deep,
    /// abyss, throne), crossfaded when the place changes; a heartbeat laid over it on a boss
    /// floor; and now and then a DISTANT sound - a drip, stone settling, a chain, a groan - panned
    /// somewhere off the walls, so the room is a pocket in something larger.
    ///
    /// <see cref="Set"/> is the interface (GameBootstrap calls it as a floor is dressed and when
    /// the hub is shown). Fades run on unscaled time, so the reward screens' pause doesn't freeze
    /// a crossfade half way.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        // Plain enum/float fields survive a domain reload; the sources are re-found by GetComponents.
        [SerializeField] AudioSource _a, _b, _boss, _nig;
        [SerializeField] float _nigredo;
        [SerializeField] float _swell = 1f;
        [SerializeField] bool _aLive = true;
        [SerializeField] Bed _bed;
        [SerializeField] bool _bossOn;
        [SerializeField] float _nextDistant;

        static Dictionary<Bed, AudioClip> _beds = new();

        // Never UnityEngine.Random: seeded floor rolls share its state, and an ambient roll landing
        // between them would change the room a restart meets.
        static System.Random _rng = new(4242);
        static float Range(float a, float b) => a + (float)(_rng ?? (_rng = new(4242))).NextDouble() * (b - a);

        /// <summary>The bed for a floor of the run, by Classic depth (a Short Descent floor 25
        /// sounds like Classic's 50).</summary>
        public static Bed ForFloor(int floor)
        {
            int depth = RunFormat.Depth(floor);
            return depth <= 25 ? Bed.Crypt : depth <= 50 ? Bed.Deep : depth <= 75 ? Bed.Abyss : Bed.Throne;
        }

        public static void Set(Bed bed, bool boss = false)
        {
            var host = AudioHost.Get();
            if (host == null) return;
            var amb = host.GetComponent<Ambience>();
            if (amb == null) amb = host.gameObject.AddComponent<Ambience>();
            amb.Change(bed, boss);
        }

        void Ensure()
        {
            var host = GetComponent<AudioHost>();
            if (_a == null) _a = host.NewSource(true);
            if (_b == null) _b = host.NewSource(true);
            if (_boss == null) _boss = host.NewSource(true);
            if (_nig == null) _nig = host.NewSource(true);
        }

        /// <summary>ARRIVAL: the room tone rises in from silence over Tuning.Audio.ArrivalSwellSeconds,
        /// rather than starting at full - the place announcing itself as the screen opens.</summary>
        public static void Swell()
        {
            var host = AudioHost.Get();
            var amb = host != null ? host.GetComponent<Ambience>() : null;
            if (amb == null) return;
            amb._swell = 0f;
            amb.Ensure();
            foreach (var src in new[] { amb._a, amb._b, amb._boss, amb._nig }) if (src != null) src.volume = 0f;
        }

        /// <summary>The Nigredo's roar under the room tone, at its strength (0 off) - Art.Biomes.Nigredo.</summary>
        public static void SetNigredo(float strength)
        {
            var host = AudioHost.Get();
            if (host == null) return;
            var amb = host.GetComponent<Ambience>();
            if (amb == null) amb = host.gameObject.AddComponent<Ambience>();
            amb.Ensure();
            amb._nigredo = Mathf.Clamp01(strength);
            if (amb._nigredo > 0f && !amb._nig.isPlaying)
            {
                amb._nig.clip = Clip(Bed.Nigredo);
                amb._nig.volume = 0f;
                amb._nig.Play();
            }
        }

        void Change(Bed bed, bool boss)
        {
            Ensure();
            _bossOn = boss;
            if (boss && !_boss.isPlaying)
            {
                _boss.clip = Clip(Bed.Boss);
                _boss.volume = 0f;
                _boss.Play();
            }
            if (bed == _bed) return;
            _bed = bed;

            // The outgoing bed keeps playing and fades in Update; the incoming one starts at a
            // random point of its loop, so returning to a band never replays the same opening.
            _aLive = !_aLive;
            var incoming = _aLive ? _a : _b;
            incoming.Stop();
            incoming.clip = bed == Bed.None ? null : Clip(bed);
            if (incoming.clip != null)
            {
                incoming.volume = 0f;
                incoming.timeSamples = (int)Range(0, incoming.clip.samples - 1);
                incoming.Play();
            }
            _nextDistant = Time.unscaledTime + Tuning.Audio.DistantFirstSeconds;
        }

        static AudioClip Clip(Bed bed)
        {
            _beds ??= new();
            if (_beds.TryGetValue(bed, out var c) && c != null) return c;
            var g = GameSound.Instance;
            AudioClip rec = g == null ? null : bed switch
            {
                Bed.Hub => g.BedHub, Bed.Crypt => g.BedCrypt, Bed.Deep => g.BedDeep,
                Bed.Abyss => g.BedAbyss, Bed.Throne => g.BedThrone, Bed.Boss => g.BedBoss, Bed.Forest => g.BedForest, Bed.Sky => g.BedSky, Bed.Salt => g.BedSalt, Bed.Nigredo => g.BedNigredo, _ => null,
            };
            c = rec != null ? rec : Synth.Clip($"Bed_{bed}", Synth.Bed(bed));
            _beds[bed] = c;
            return c;
        }

        void Update()
        {
            if (_a == null || _b == null || _boss == null) return;
            _swell = Mathf.MoveTowards(_swell, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, Tuning.Audio.ArrivalSwellSeconds));
            float swell = _swell * _swell;   // eased in: silence, then the room
            if (_nig != null)
            {
                float nigLevel = _nigredo * Tuning.Audio.NigredoLoudness * Volume.Ambience * swell;
                _nig.volume = Mathf.MoveTowards(_nig.volume, nigLevel, Time.unscaledDeltaTime * 0.12f);
                if (_nigredo <= 0f && _nig.isPlaying && _nig.volume <= 0.0001f) _nig.Stop();
            }
            float dt = Time.unscaledDeltaTime;
            float step = dt / Mathf.Max(0.01f, Tuning.Audio.BedFadeSeconds);
            float level = Tuning.Audio.BedLoudness(_bed) * Volume.Ambience * swell;

            var live = _aLive ? _a : _b;
            var dying = _aLive ? _b : _a;
            // While swelling, the bed follows the swell exactly (the crossfade's slow step would lag it).
            live.volume = _swell < 1f ? (live.clip != null ? level : 0f)
                                      : Mathf.MoveTowards(live.volume, live.clip != null ? level : 0f, step * Mathf.Max(level, 0.01f));
            dying.volume = Mathf.MoveTowards(dying.volume, 0f, step * Mathf.Max(dying.volume, 0.01f) + dt * 0.02f);
            if (dying.isPlaying && dying.volume <= 0.0001f) dying.Stop();

            float bossLevel = _bossOn ? Tuning.Audio.BossPulseLoudness * Volume.Ambience * swell : 0f;
            _boss.volume = Mathf.MoveTowards(_boss.volume, bossLevel, dt * 0.15f);
            if (!_bossOn && _boss.isPlaying && _boss.volume <= 0.0001f) _boss.Stop();

            // Distant sounds only in the run's places - the hub is a room with people in it.
            if (_bed != Bed.None && _bed != Bed.Hub && Time.unscaledTime >= _nextDistant)
            {
                _nextDistant = Time.unscaledTime + Range(Tuning.Audio.DistantMinSeconds, Tuning.Audio.DistantMaxSeconds);
                var cam = Camera.main;
                float side = Range(0f, 1f) < 0.5f ? -1f : 1f;
                Vector2 at = cam != null ? (Vector2)cam.transform.position + new Vector2(side * Tuning.Audio.PanWidth, 0f)
                                         : Vector2.zero;
                Sfx.Play(_bed == Bed.Forest ? Sound.DistantForest : _bed == Bed.Sky ? Sound.DistantSky : _bed == Bed.Salt ? Sound.DistantSalt : Sound.Distant, at, ambient: true);
            }
        }
    }
}
