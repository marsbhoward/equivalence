using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Art.Lighting;

namespace Convergence.Bosses
{
    /// <summary>
    /// The Wreathe (it was the Cantor): a hooded singer of worn smoke, and a fight of CALL AND
    /// RESPONSE (the user's rules, group 5, 2026-10-26). See <see cref="Tuning.Boss"/> for every
    /// number.
    ///
    ///     PRELUDE  two rings rise out of its hood: the phrase is about to begin.
    ///     CALL     it teleports round the slices (into its hood and out), SINGING each slice's
    ///              note; the same rings fly at the player - its voice is the attack. Untouchable.
    ///     BREATH   it holds still above the room while the player recalls the phrase; the breath
    ///              grows with the phrase.
    ///     ANSWER   the player STEPS the phrase: on each beat, stand in the slice it sang that note
    ///              from. Every other slice cracks and vents and hurts; the safe centre costs
    ///              nothing and cuts nothing. Each note landed cuts one ring of its guard.
    ///     REST     every ring cut, it unravels into the middle, open. A miss: the guard holds -
    ///              no window, and a new phrase of the same length.
    ///
    /// THE ANSWER NEVER SHOWS WHICH SLICE IS RIGHT. Every slice fills together (the clock) and the
    /// beat tolls one neutral bell; only the cut, after the beat, confirms it - and then the
    /// note it sang is heard again.
    ///
    /// THE PHRASE GROWS ONLY AFTER A WINDOW OPENS (a failed phrase is sung again at the same
    /// length), and is regenerated every cycle, so the GRAMMAR is learned, never the notes.
    ///
    /// ENRAGED, EVERY SLICE SPLITS IN TWO (the user's call): a ring cuts each into an outer and an
    /// inner half that look the same - it sings from one, an inner note an octave lower - and a
    /// darker double of it lags a beat behind.
    ///
    /// BY BAND (each adds to the ones before; Tuning.EnemyBands.Wreathe*): 1 its voice LEADS you,
    /// 2 one note a phrase is SILENT (sung unseen - a faint ring at the slice), 3 some phrases are
    /// answered BACKWARDS (its hood turned away through the breath is the tell), 4 the call is
    /// faster.
    ///
    /// Driven from ONE coroutine that checks its token (a coroutine outliving its run is a
    /// documented failure here). Immunity is <see cref="Health.Immune"/> with the collider off,
    /// never a zero multiplier. The body, the window cap and the lifecycle are <see cref="Boss"/>'s.
    /// </summary>
    public class Wreathe : Boss
    {
        public enum Movement { Prelude, Call, Breath, Answer, Rest }
        public Movement Phase { get; private set; } = Movement.Prelude;

        public override string DisplayName => "THE WREATHE";
        public override string PhaseLabel => Phase.ToString().ToUpper();

        /// <summary>The cadence - prelude to the last beat - is watched whole (the user's call).</summary>
        public override bool HoldsCamera => Phase != Movement.Rest;

        /// <summary>DEV: steps the player into each answer note's slice as the fill starts (testing
        /// the stepped answer from eval), stepping note <see cref="DevAutoMiss"/> into the wrong slice.</summary>
        public static bool DevAutoStep;
        public static int DevAutoMiss = -1;

        /// <summary>Windows opened so far - the phrase's growth.</summary>
        public int Opened { get; private set; }

        [SerializeField] WreatheFloor _floor;
        [SerializeField] WreatheFigure _figure;
        [SerializeField] Glow _glow;
        [SerializeField] int _band;
        List<int> _phrase = new();

        static readonly Color EmberLight = new(1f, 0.55f, 0.22f);
        static readonly Color OpenLight = new(1f, 0.85f, 0.45f);
        static readonly Color ColdLight = new(0.8f, 0.86f, 1f);

        /// <summary>Its hood's height over its root (where the voice leaves from).</summary>
        static float HoodUp => (WreatheCanvas.Hover + WreatheCanvas.Height - 8) / TexelFx.Body;

        public static Wreathe Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            // NOT INTERPOLATED (Boss.CreateBody): it TELEPORTS, and a slide would hand the player a
            // path pointing at where it is going.
            var b = CreateBody<Wreathe>("boss.wreathe", at, target, parent, floor, 0.55f, Tuning.Boss.Hp);
            b._band = b.Band;
            b._figure = TexelPicture.Attach<WreatheFigure>(b.transform, "figure", Vector2.zero, 34, 6, 64, 0);
            b._figure.Look.Prelude = -1f;
            DepthSorted.Attach(b.gameObject, 0f, false, b._figure.GetComponent<SpriteRenderer>());
            b._glow = Glow.Attach(b.gameObject, 3.2f, EmberLight, 0.9f, 0.12f);
            b._floor = WreatheFloor.Build(parent);
            b.StartCoroutine(b.Fight());
            return b;
        }

        public override void Cease()
        {
            base.Cease();
            if (_floor != null) Destroy(_floor.gameObject);
        }

        public override void DeathPicture() => WreatheDeath.At(transform.position);

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            while (true)
            {
                CheckEnrage(Tuning.Boss.EnrageAt);
                bool split = Enraged;
                if (_floor != null) _floor.Split = split;
                if (_figure != null) _figure.Look.Doubled = Enraged;
                BuildPhrase(split);

                bool backwards = _band >= Tuning.EnemyBands.WreatheBackwardsFromBand && Random.value < Tuning.EnemyBands.WreatheBackwardsChance;
                int silent = _band >= Tuning.EnemyBands.WreatheSilentFromBand ? Random.Range(1, Mathf.Max(2, _phrase.Count - 1)) : -1;

                yield return Prelude(token); if (token != Token) yield break;
                yield return Call(token, silent); if (token != Token) yield break;
                yield return Breath(token, backwards); if (token != Token) yield break;
                int landed = 0;
                yield return Answer(token, backwards, n => landed = n); if (token != Token) yield break;

                if (landed == _phrase.Count)
                {
                    yield return Rest(token); if (token != Token) yield break;
                    Opened++;
                }
                else
                {   // the guard holds: no window, a new phrase of the same length
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, transform.position);
                    yield return new WaitForSeconds(Tuning.Boss.GuardHoldsSeconds); if (token != Token) yield break;
                }
            }
        }

        /// <summary>A walk over the zones with no note repeated back to back (a repeat is not a
        /// note to remember - you are already standing in it).</summary>
        void BuildPhrase(bool split)
        {
            int want = Mathf.Min(Tuning.Boss.BaseNotes + Opened, Tuning.Boss.MaxNotes);
            int zones = split ? ArenaSectors.Count * 2 : ArenaSectors.Count;
            _phrase ??= new List<int>();   // non-readonly + guarded: see Domain reload traps
            _phrase.Clear();
            int last = -1;
            for (int i = 0; i < want; i++)
            {
                int n;
                do { n = Random.Range(0, zones); } while (n == last);
                _phrase.Add(n);
                last = n;
            }
        }

        IEnumerator Prelude(int token)
        {
            Phase = Movement.Prelude;
            Health.Immune = true;
            if (BodyCollider != null) BodyCollider.enabled = false;
            yield return Blink(token, Above());
            if (token != Token) yield break;
            if (_figure != null) _figure.SetRings(_phrase.Count);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Toll, NeutralToll, transform.position);
            for (float t = 0f; t < Tuning.Boss.PreludeSeconds; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Prelude = t / Tuning.Boss.PreludeSeconds;
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Prelude = -1f;
        }

        IEnumerator Call(int token, int silent)
        {
            Phase = Movement.Call;
            float note = _band >= Tuning.EnemyBands.WreatheFastCallFromBand ? Tuning.Boss.CallNoteSecondsFast : Tuning.Boss.CallNoteSeconds;
            for (int i = 0; i < _phrase.Count; i++)
            {
                int z = _phrase[i];
                float start = Time.time;
                bool hidden = i == silent;
                // A silent note: sung unseen. It goes into its hood and stays there for the note.
                yield return Blink(token, ArenaSectors.ZoneStation(z), stayHidden: hidden);
                if (token != Token) yield break;
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Sing, z, ArenaSectors.ZoneStation(z));
                if (_floor != null) { if (hidden) _floor.Silent(z); else _floor.Print(z); }

                float aim = Mathf.Min(Tuning.Boss.CallAimSeconds, note * 0.4f);
                while (Time.time - start < aim + 0.22f) { yield return null; if (token != Token) yield break; }
                if (!hidden) Voice();
                while (Time.time - start < note) { yield return null; if (token != Token) yield break; }
            }
        }

        IEnumerator Breath(int token, bool backwards)
        {
            Phase = Movement.Breath;
            yield return Blink(token, Above());
            if (token != Token) yield break;
            if (_figure != null) _figure.Look.TurnedAway = backwards;
            yield return new WaitForSeconds(Tuning.Boss.BreathBaseSeconds + Tuning.Boss.BreathPerNoteSeconds * _phrase.Count);
            if (_figure != null) _figure.Look.TurnedAway = false;
        }

        IEnumerator Answer(int token, bool backwards, System.Action<int> result)
        {
            Phase = Movement.Answer;
            float note = Tuning.Boss.AnswerNoteSeconds, fill = note * Tuning.Boss.AnswerFillShare;
            int landed = 0;
            for (int j = 0; j < _phrase.Count; j++)
            {
                int z = _phrase[backwards ? _phrase.Count - 1 - j : j];
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Toll, NeutralToll, Vector2.zero, 0.5f);
                if (DevAutoStep && Target != null) DevStep(DevAutoMiss == j ? (z + 4) % ArenaSectors.Count : z);   // a miss: the slice opposite
                for (float t = 0f; t < fill; t += Time.deltaTime)
                {
                    if (_floor != null) _floor.Fill = t / fill;
                    yield return null;
                    if (token != Token) yield break;
                }
                if (_floor != null) _floor.Fill = -1f;

                // THE BEAT: tested once, here (one misread costs one hit, at any frame rate).
                bool split = _floor != null && _floor.Split;
                int stood = Target != null ? ArenaSectors.ZoneAt(ReleaseCanvas.Feet(Target), split) : -1;
                bool cut = stood == z;
                if (_floor != null) _floor.Beat(z, cut);
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Erupt, Vector2.zero);
                if (cut)
                {
                    landed++;
                    if (_figure != null) _figure.Cut(j);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Toll, z, ArenaSectors.ZoneStation(z));   // the note, heard back
                    Glow.Flash(ReleaseCanvas.Feet(Target), 1.6f, ColdLight, 0.35f);
                }
                else if (stood >= 0 && Target != null)
                {
                    var hp = Target.GetComponent<Health>();
                    // A MECHANIC hit: a share of the player's own max health, mitigated by Take.
                    if (hp != null)
                        hp.Take(new DamageInfo(hp.Max * Tuning.Boss.EruptionFraction, ElementType.Fire, gameObject) { IgnoresHeat = true });
                }
                yield return new WaitForSeconds(note - fill);
                if (token != Token) yield break;
            }
            result(landed);
        }

        IEnumerator Rest(int token)
        {
            Phase = Movement.Rest;
            yield return Blink(token, Vector2.zero);
            if (token != Token) yield break;
            if (_figure != null) { _figure.SetRings(0); }
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { SetOpen(t / 0.4f); yield return null; if (token != Token) yield break; }
            SetOpen(1f);
            OpenWindow();      // the per-window cap - see Boss.OpenWindow

            float seconds = Enraged ? Tuning.Boss.StunSecondsEnraged : Tuning.Boss.StunSeconds;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                // Cap reached: the window CLOSES rather than standing open to be hit for nothing.
                if (WindowSpent) break;
                // The heart beats faster as the window closes - the last chain is a decision.
                float k = t / seconds;
                if (_figure != null) _figure.Look.HeartBeat = Mathf.PingPong(Time.time * (3f + k * 6f), 1f) > 0.5f;
                yield return null;
                if (token != Token) yield break;
            }
            CloseWindow();
            for (float t = 0.4f; t > 0f; t -= Time.deltaTime) { SetOpen(t / 0.4f); yield return null; if (token != Token) yield break; }
            SetOpen(0f);
        }

        // ------------------------------------------------------------------ pieces

        void DevStep(int zone)
        {
            bool split = _floor != null && _floor.Split;
            Vector2 spot = zone < 0 ? Vector2.zero
                         : zone >= ArenaSectors.Count ? ArenaSectors.ZoneStation(zone)
                         : ArenaSectors.Direction(zone) * (split ? 4.8f : 3.4f);
            var rb = Target.GetComponent<Rigidbody2D>();
            Vector2 lift = (Vector2)Target.position - ReleaseCanvas.Feet(Target);
            if (rb != null) rb.position = spot + lift;
            Target.position = spot + lift;
            Physics2D.SyncTransforms();
        }

        /// <summary>The neutral bell of the answer's clock (Sound.Toll's last variant): the same
        /// every beat, so the sound never gives the slice away.</summary>
        const int NeutralToll = 16;

        static Vector2 Above() => Vector2.up * Tuning.Boss.AboveY;

        /// <summary>The teleport (the user's pick): into its hood and gone, then out of the hood at
        /// <paramref name="to"/>. Nothing travels between - a path would point where it is going.</summary>
        IEnumerator Blink(int token, Vector2 to, bool stayHidden = false)
        {
            if (_figure == null) { TeleportTo(to); yield break; }
            if ((Vector2)transform.position != (Vector2)Arena.Clamp(to, 0.8f) || _figure.Look.Blink > 0f)
            {
                for (float t = _figure.Look.Blink * 0.12f; t < 0.12f; t += Time.deltaTime) { SetBlink(t / 0.12f); yield return null; if (token != Token) yield break; }
                SetBlink(1f);
                TeleportTo(to);
            }
            if (stayHidden) yield break;
            for (float t = 0.15f; t > 0f; t -= Time.deltaTime) { SetBlink(t / 0.15f); yield return null; if (token != Token) yield break; }
            SetBlink(0f);
        }

        void SetBlink(float k)
        {
            if (_figure != null) _figure.Look.Blink = k;
            if (_glow != null) _glow.Intensity = 0.9f * (1f - k);
        }

        void SetOpen(float k)
        {
            if (_figure != null) _figure.Look.Open = k;
            if (_glow != null) _glow.Color = Color.Lerp(EmberLight, OpenLight, k);
        }

        /// <summary>
        /// ITS VOICE IS THE ATTACK (the user's call): the sung note's two rings - the same picture
        /// the prelude raises - fly at the player. From band 1 they LEAD, aimed where the player is
        /// going. Chip damage, scaled with the floor like any enemy's.
        /// </summary>
        void Voice()
        {
            if (Target == null) return;
            var origin = (Vector2)transform.position + Vector2.up * HoodUp;
            var aim = (Vector2)Target.position;
            if (_band >= Tuning.EnemyBands.WreatheLeadFromBand && Target.TryGetComponent<Rigidbody2D>(out var rb))
            {
                float flight = Vector2.Distance(origin, aim) / Tuning.Boss.VoiceSpeed;
                aim += Vector2.ClampMagnitude(rb.linearVelocity * flight, Tuning.Boss.VoiceLeadMax);
            }
            var dir = (aim - origin).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            EnemyProjectile.Launch(gameObject, Target, Target.GetComponent<Health>(), origin, dir,
                                   Tuning.Boss.VoiceDamage * Enemies.FloorDifficulty.Damage(Floor), 0f,
                                   Tuning.Boss.VoiceSpeed, Tuning.Boss.VoiceLifetime)
                .Dress(VoiceFrames(), Tuning.Boss.VoiceHitRadius, ElementType.Earth);
        }

        static Sprite[] _voice;

        /// <summary>The voice: two ember rings, the prelude's picture, breathing as they fly.</summary>
        static Sprite[] VoiceFrames()
        {
            if (_voice != null && _voice.Length > 0 && _voice[0] != null) return _voice;
            var E = WreatheCanvas.Ember;
            _voice = new Sprite[4];
            for (int f = 0; f < 4; f++)
            {
                const int w = 21, h = 13;
                var px = new Color32[w * h];
                float grow = f < 2 ? f : 4 - f;
                for (int k = 0; k < 2; k++)
                {
                    float r = 6f + k * 2.5f + grow * 0.6f, cy = 4f + k * 4f;
                    for (float a = 0f; a < 6.283f; a += 0.05f)
                    {
                        int x = Mathf.RoundToInt(10f + Mathf.Cos(a) * r), y = Mathf.RoundToInt(cy + Mathf.Sin(a) * r * 0.35f);
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        px[y * w + x] = Mathf.Sin(a) < 0f ? E[1 + k] : E[2 + k];
                    }
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(px); tex.Apply();
                _voice[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TexelFx.Body);
            }
            return _voice;
        }
    }
}
