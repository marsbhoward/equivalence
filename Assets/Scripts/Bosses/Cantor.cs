using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// The Cantor: a teleporting ranged boss whose fight is a phrase you have to hear and then
    /// stand inside. See <see cref="Tuning.Boss"/> for the shape and every number.
    ///
    /// THREE MOVEMENTS, REPEATED:
    ///
    ///     CALL     teleports the eight slices in a random order, blasting from each. Untouchable.
    ///     ANSWER   the SAME slices erupt in the SAME order. Untouchable.
    ///     REST     drops into the middle, stunned and open. The only window there is.
    ///
    /// The puzzle is that ANSWER is CALL. Nothing tells the player that; the fight does, once.
    ///
    /// THE PHRASE IS REGENERATED EVERY CYCLE, which is the difference between a boss that is
    /// learned and one that is memorised. What carries between attempts is the GRAMMAR - that the
    /// call becomes the answer, that the rest is the window - and never the specific notes. A
    /// fixed phrase would be solved once and then performed; a random one with no grammar would be
    /// noise. This is a phrase drawn from a repertoire of eight.
    ///
    /// IMMUNITY IS <see cref="Health.Immune"/>, NOT A ZERO MULTIPLIER, which this project has
    /// already learned the hard way: a zero multiplier still runs the whole hit - flash, knockback
    /// and every Damaged listener - so it reads as "hit for nothing" rather than "not hit", and it
    /// silently wears the player's weapon down against a target they were never allowed to hurt.
    ///
    /// EVERYTHING IS DRIVEN FROM ONE COROUTINE rather than from an Update switch. The fight is a
    /// sequence in time and reads as one here; a state machine polling a timer per frame would put
    /// the phrase's structure in a dozen places. The coroutine is stopped and the object
    /// deactivated on death, and it checks its own token, because this project has been bitten
    /// before by coroutines outliving the run that started them.
    ///
    /// The body, the window cap and the lifecycle are shared with every boss - see <see cref="Boss"/>.
    /// </summary>
    public class Cantor : Boss
    {
        /// <summary>Where the fight is, for the HUD. A plain enum on a plain field - nothing here
        /// is an interface or a delegate, so a domain reload leaves it intact.</summary>
        public enum Movement { Call, Answer, Rest }
        public Movement Phase { get; private set; } = Movement.Call;

        public override string DisplayName => "THE CANTOR";
        public override string PhaseLabel => Phase.ToString().ToUpper();

        /// <summary>Cycles completed. Drives the phrase length.</summary>
        public int Cycle { get; private set; }

        SectorHazards _hazards;
        SpriteRenderer _body, _core, _halo;
        List<int> _phrase = new();

        static readonly Color Cold = new(0.42f, 0.55f, 0.95f);
        static readonly Color Hot = new(1f, 0.35f, 0.30f);
        static readonly Color Open = new(1f, 0.92f, 0.55f);

        public static Cantor Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            // NOT INTERPOLATED (Boss.CreateBody). The Cantor does not travel - it TELEPORTS
            // between slices, and interpolation would turn every jump into a visible slide that
            // hands the player a tell pointing at the destination before the boss is there. CALL
            // is read from where it STOOD, one slice at a time; a slide blurs the phrase ANSWER
            // then replays.
            var b = CreateBody<Cantor>("boss.cantor", at, target, parent, floor, 0.55f, Tuning.Boss.Hp);
            var go = b.gameObject;

            b._halo = Quad(go.transform, "halo", 2.6f, Cold, SortingOrders.Fx - 3, Spr.Glow);
            b._body = Quad(go.transform, "body", 1.15f, Cold, SortingOrders.Enemy, Spr.Circle);
            b._core = Quad(go.transform, "core", 0.55f, Color.white, SortingOrders.Enemy + 1, Spr.Circle);

            b._hazards = SectorHazards.Build(parent);
            b.StartCoroutine(b.Fight());
            return b;
        }

        public override void Cease()
        {
            base.Cease();
            if (_hazards != null) _hazards.Clear();
        }

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;

            while (true)
            {
                CheckEnrage(Tuning.Boss.EnrageAt);
                BuildPhrase();

                yield return Call(token);
                if (token != Token) yield break;

                yield return new WaitForSeconds(Tuning.Boss.BeatSeconds);
                if (token != Token) yield break;

                yield return Answer(token);
                if (token != Token) yield break;

                yield return new WaitForSeconds(Tuning.Boss.BeatSeconds);
                if (token != Token) yield break;

                yield return Rest(token);
                if (token != Token) yield break;

                Cycle++;
            }
        }

        /// <summary>
        /// The phrase for this cycle: a walk over the eight slices with no note repeated back to
        /// back.
        ///
        /// NO IMMEDIATE REPEAT, because a note played twice running is not a note the player has
        /// to remember - they are already standing clear of it - and in the answer it reads as the
        /// hazard stuttering rather than as two beats. Repeats further apart are fine and wanted:
        /// they are what makes a phrase feel composed rather than shuffled.
        /// </summary>
        void BuildPhrase()
        {
            int want = Mathf.Min(Tuning.Boss.BaseNotes + Cycle, Tuning.Boss.MaxNotes);
            _phrase ??= new List<int>();   // non-readonly + guarded: see Domain reload traps
            _phrase.Clear();
            int last = -1;
            for (int i = 0; i < want; i++)
            {
                int n;
                do { n = Random.Range(0, ArenaSectors.Count); } while (n == last);
                _phrase.Add(n);
                last = n;
            }
        }

        IEnumerator Call(int token)
        {
            Phase = Movement.Call;
            Health.Immune = true;
            SetOpen(false);

            float note = Enraged ? Tuning.Boss.CallNoteSecondsEnraged : Tuning.Boss.CallNoteSeconds;
            float aim = Mathf.Min(Tuning.Boss.CallAimSeconds, note * 0.55f);

            foreach (int sector in _phrase)
            {
                TeleportTo(ArenaSectors.Station(sector));
                _hazards.Mark(sector, Cold, 0.30f);      // a faint print of where it stood

                yield return new WaitForSeconds(aim);
                if (token != Token) yield break;

                Blast();

                yield return new WaitForSeconds(note - aim);
                if (token != Token) yield break;
            }
        }

        IEnumerator Answer(int token)
        {
            Phase = Movement.Answer;
            Health.Immune = true;
            SetOpen(false);

            // It watches from ABOVE the pattern rather than standing in it - parked at the centre
            // it would sit exactly where the player has learned the rest happens, which is the one
            // spot the fight needs to keep meaning one thing.
            TeleportTo(Vector2.zero + Vector2.up * (Tuning.Boss.TeleportRadius + 1.4f));

            float note = Enraged ? Tuning.Boss.AnswerNoteSecondsEnraged : Tuning.Boss.AnswerNoteSeconds;
            float tell = Enraged ? Tuning.Boss.TelegraphSecondsEnraged : Tuning.Boss.TelegraphSeconds;
            tell = Mathf.Min(tell, note * 0.8f);

            foreach (int sector in _phrase)
            {
                _hazards.Erupt(sector, tell, Target);
                if (Enraged && Tuning.Boss.EnragedAnswersOpposite)
                    _hazards.Erupt((sector + ArenaSectors.Count / 2) % ArenaSectors.Count, tell, Target);

                yield return new WaitForSeconds(note);
                if (token != Token) yield break;
            }
        }

        IEnumerator Rest(int token)
        {
            Phase = Movement.Rest;
            TeleportTo(Vector2.zero);

            OpenWindow();      // the per-window cap - see Boss.OpenWindow
            SetOpen(true);

            float seconds = Enraged ? Tuning.Boss.StunSecondsEnraged : Tuning.Boss.StunSeconds;
            float t = 0f;
            while (t < seconds)
            {
                // Cap reached: the window CLOSES rather than standing open to be hit for nothing.
                // A strong player's reward is leaving the danger sooner, not a skipped fight.
                if (WindowSpent) break;

                t += Time.deltaTime;
                // The core pulses faster as the window closes, so the last chain is a decision
                // rather than a surprise - the player can see they have time for one more.
                float k = t / seconds;
                float beat = Mathf.PingPong(Time.time * (3f + k * 6f), 1f);
                if (_core != null) _core.color = Color.Lerp(Open, Color.white, beat);
                yield return null;
                if (token != Token) yield break;
            }

            CloseWindow();
            SetOpen(false);
        }

        // ------------------------------------------------------------------ pieces

        void Blast()
        {
            if (Target == null) return;
            var origin = (Vector2)transform.position;
            var dir = ((Vector2)Target.position - origin).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            EnemyProjectile.Launch(gameObject, Target, Target.GetComponent<Health>(),
                                   origin, dir,
                                   Tuning.Boss.BlastDamage * Enemies.FloorDifficulty.Damage(Floor), 0f,
                                   Tuning.Boss.BlastSpeed, Tuning.Boss.BlastLifetime);
        }

        /// <summary>
        /// The one readable thing about the boss: whether it can be hit.
        ///
        /// Colour AND the collider, together. A boss that looks open and is not, or is open and
        /// looks shut, is the fight lying about the only question the player is asking - so the
        /// collider is switched with the tint rather than left on, and a swing during CALL passes
        /// through empty space instead of connecting for nothing.
        /// </summary>
        void SetOpen(bool open)
        {
            if (BodyCollider != null) BodyCollider.enabled = open;
            if (_body != null) _body.color = open ? Open : (Enraged ? Hot : Cold);
            if (_halo != null)
            {
                var c = open ? Open : (Enraged ? Hot : Cold);
                _halo.color = new Color(c.r, c.g, c.b, open ? 0.55f : 0.28f);
            }
            if (_core != null && !open) _core.color = Color.white;
        }
    }
}
