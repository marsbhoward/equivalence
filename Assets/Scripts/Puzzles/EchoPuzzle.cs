using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    /// <summary>
    /// WATCH AND REPEAT. Six stones in a line to the NORTH and a pale stone where the player
    /// starts; standing on the pale stone plays the CALL (stones light in a phrase), and the player
    /// ANSWERS by stepping the same stones in the same order. One wrong stone fails it.
    ///
    /// Deliberately the Wreathe's grammar - call, then the same notes answered in the same order -
    /// met on a floor where getting it wrong costs a fight rather than a life. A player who has
    /// solved a few of these walks into floor 25 already knowing how to read the boss.
    ///
    /// REWORKED 2026-11-12 (the user's picks - it was too easy; mock at
    /// https://claude.ai/artifact/EBymHtrwjxnA2JskTtzk5x), by the floor's EnemyBand:
    ///   - ROUNDS (Tuning.Puzzle.EchoRounds): each call is the last plus one note, played by
    ///     itself after a round is answered; a lamp lights gold per round. Longer by band.
    ///   - A stone may sound TWICE running - lit, dark, lit - answered by stepping off and on.
    ///   - REPLAYS cost: stepping back on the pale stone before answering replays the call, at
    ///     most EchoReplays for the whole puzzle, each EchoReplaySeconds off the Short's clock. Its
    ///     pips are the replays left. The first hearing of each call is free.
    ///   - SWAP (from band 2): from round 2, after every call the stones shake, lift and slide to
    ///     new places - answer by colour and pips.
    ///   - REVERSE (from band 3, stacked on the swap): every call is answered last note first; an
    ///     arrow runs west under the line while answering.
    /// The pale stone carries the twists' marks from the start, so the player knows what to listen for.
    /// </summary>
    public class EchoPuzzle : PuzzleRoom
    {
        [SerializeField] List<int> _phrase = new();
        [SerializeField] List<int> _slot = new();          // stone -> its place in the line
        [SerializeField] int _answered, _round, _rounds, _firstLength, _replaysLeft;
        [SerializeField] bool _calling, _heard, _swap, _reverse, _swapping;
        [SerializeField] float _clock, _nextIn = -1f;
        [SerializeField] int _start;
        [SerializeField] List<float> _swapFrom = new();
        [SerializeField] List<SpriteRenderer> _replayPips = new(), _lamps = new();
        [SerializeField] SpriteRenderer _mark, _arrow;

        static readonly Color[] Hues =
        {
            new(1.00f, 0.42f, 0.30f), new(1.00f, 0.80f, 0.30f), new(0.45f, 0.90f, 0.45f),
            new(0.35f, 0.85f, 1.00f), new(0.50f, 0.55f, 1.00f), new(0.95f, 0.50f, 0.95f),
        };
        static readonly Color MarkViolet = new(0.73f, 0.65f, 1f);
        static readonly Color LampOff = new(0.24f, 0.23f, 0.21f), LampOn = new(0.95f, 0.76f, 0.35f);
        static readonly Color PipOn = new(0.92f, 0.91f, 0.96f), PipOff = new(0.24f, 0.24f, 0.27f);

        public override string Title => "THE ECHO";

        int Band => RunFormat.EnemyBand(Floor);

        /// <summary>The Short Descent's clock on this puzzle (PuzzleRoom.ShortClockSeconds).</summary>
        protected override float ShortClockSeconds =>
            Band >= Tuning.Puzzle.EchoSwapFromBand ? Tuning.Puzzle.ShortEchoSecondsDeep : Tuning.Puzzle.ShortEchoSeconds;

        int RoundLength => _firstLength + _round;

        public override string Body
        {
            get
            {
                if (State == PuzzleState.Solved) return "Answered. The sigil door opens.";
                if (State == PuzzleState.Failed && TimedOut) return TimedOutBody;
                if (State == PuzzleState.Failed) return "Wrong stone. The sigil door is sealed - take the side door.";
                if (_calling) return "Listen...";
                if (_swapping) return "The stones are moving...";
                string round = _rounds > 1 ? $"Round {_round + 1} of {_rounds}   " : "";
                if (_nextIn > 0f) return $"Round {_round} answered.";
                if (!_heard)
                {
                    string twists = (_swap ? "\nFrom round 2 the stones move after each call." : "")
                                  + (_reverse ? "\nAnswer every call backwards." : "");
                    return "Stand on the pale stone to hear the call, then step the stones in the " +
                           "same order. One wrong stone seals the door." + twists;
                }
                string replay = _answered > 0 || _replaysLeft <= 0 ? ""
                    : $"\nThe pale stone plays it again ({_replaysLeft} left" +
                      (HasClock ? $", -{Tuning.Puzzle.EchoReplaySeconds:0}s)" : ")");
                return $"{round}{(_reverse ? "Answer it backwards" : "Answer the call")}   {_answered} / {RoundLength}" + replay;
            }
        }

        static Vector2 SlotAt(int slot) =>
            Centre + new Vector2((slot - (Tuning.Puzzle.EchoStones - 1) * 0.5f) * Tuning.Puzzle.EchoLineSpacing,
                                 Tuning.Puzzle.EchoLineRise);

        protected override void Setup()
        {
            int band = Mathf.Clamp(Band, 0, Tuning.Puzzle.EchoRounds.Length - 1);
            _rounds = Tuning.Puzzle.EchoRounds[band];
            _firstLength = Tuning.Puzzle.EchoFirstLength[band];
            _swap = Band >= Tuning.Puzzle.EchoSwapFromBand;
            _reverse = Band >= Tuning.Puzzle.EchoReverseFromBand;
            _replaysLeft = Tuning.Puzzle.EchoReplays;

            int n = Tuning.Puzzle.EchoStones;
            for (int i = 0; i < n; i++)
            {
                AddPlate(SlotAt(i), Hues[i % Hues.Length], pips: i + 1);
                _slot.Add(i);
            }
            var pale = AddPlate(Centre, new Color(0.92f, 0.92f, 0.96f), radius: Tuning.Puzzle.PlateRadius * 1.15f);
            _start = Plates.Count - 1;

            // The pale stone's replay pips on its lower arc, and the twists' mark above them.
            float r = Tuning.Puzzle.PlateRadius * 1.15f;
            for (int k = 0; k < Tuning.Puzzle.EchoReplays; k++)
            {
                float a = Mathf.Deg2Rad * (230f + k * 40f);
                var pip = Decal(pale.transform, "replay", PlateArt.Dot(r * 0.24f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.42f);
                _replayPips.Add(pip);
            }
            int markKind = (_swap ? 1 : 0) + (_reverse ? 2 : 0);
            if (markKind > 0) { _mark = Decal(pale.transform, "twist", PlateArt.EchoMark(markKind), new Vector2(0f, r * 0.12f)); _mark.color = MarkViolet; }
            ShowReplays();

            // A lamp per round between the pale stone and the line.
            for (int k = 0; k < _rounds; k++)
            {
                var at = Centre + new Vector2((k - (_rounds - 1) * 0.5f) * 0.45f, Tuning.Puzzle.EchoLineRise * 0.48f);
                var lamp = Decal(transform, "round", PlateArt.Dot(0.22f), at, world: true);
                lamp.color = LampOff;
                _lamps.Add(lamp);
            }

            // REVERSE's arrow under the line, shown while answering.
            if (_reverse)
            {
                float half = (n - 1) * 0.5f * Tuning.Puzzle.EchoLineSpacing + 0.6f;
                _arrow = Decal(transform, "reverse", PlateArt.WestArrow(half * 2f),
                               Centre + new Vector2(-half, Tuning.Puzzle.EchoLineRise - 0.95f), world: true);
                _arrow.color = MarkViolet;
                _arrow.enabled = false;
            }

            // The whole phrase, the last round's length. A stone may sound twice running, never
            // three times.
            int len = _firstLength + _rounds - 1;
            for (int i = 0; i < len; i++)
            {
                int s = Rng.Next(n);
                if (i >= 2 && _phrase[i - 1] == _phrase[i - 2] && s == _phrase[i - 1]) s = (s + 1 + Rng.Next(n - 1)) % n;
                _phrase.Add(s);
            }
        }

        SpriteRenderer Decal(Transform parent, string name, Sprite sprite, Vector2 at, bool world = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (world) go.transform.position = at; else go.transform.localPosition = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrders.GroundDecal + 3;
            return sr;
        }

        void ShowReplays()
        {
            for (int k = 0; k < _replayPips.Count; k++)
                if (_replayPips[k] != null) _replayPips[k].color = k < _replaysLeft ? PipOn : PipOff;
        }

        void BeginCall()
        {
            _calling = true;
            _clock = -0.4f;              // a breath before the first note
            Plates[_start].Pulse(0.3f);
        }

        protected override void Tick(int stepped)
        {
            if (_calling) { TickCall(); return; }
            if (_swapping) { TickSwap(); return; }
            if (_nextIn > 0f)
            {
                _nextIn -= Time.deltaTime;
                if (_nextIn <= 0f) BeginCall();
                return;
            }

            if (stepped < 0) return;

            if (stepped == _start)
            {
                // The first hearing is free; after it, a replay costs (until the first answer).
                if (!_heard) { if (_round == 0) BeginCall(); return; }
                if (_answered > 0 || _replaysLeft <= 0) return;
                if (!SpendClock(Tuning.Puzzle.EchoReplaySeconds)) return;
                _replaysLeft--;
                ShowReplays();
                BeginCall();
                return;
            }

            if (!_heard) return;             // stones mean nothing before the call

            int want = _phrase[_reverse ? RoundLength - 1 - _answered : _answered];
            if (stepped == want)
            {
                Plates[stepped].Pulse(0.35f);
                if (++_answered >= RoundLength) RoundAnswered();
            }
            else Fail(Plates[stepped]);
        }

        void TickCall()
        {
            float per = Tuning.Puzzle.EchoNoteSeconds + Tuning.Puzzle.EchoGapSeconds;
            _clock += Time.deltaTime;
            if (_clock < 0f) return;
            int note = Mathf.FloorToInt(_clock / per);
            if (note >= RoundLength)
            {
                _calling = false;
                _heard = true;
                for (int i = 0; i < _start; i++) Plates[i].Set(PuzzlePlate.Look.Dim);
                if (_swap && _round >= 1) BeginSwap();
                return;
            }
            // Each note lit then dark, so a stone sounding twice reads as two lights.
            bool on = _clock - note * per < Tuning.Puzzle.EchoNoteSeconds;
            for (int i = 0; i < _start; i++)
                Plates[i].Set(on && i == _phrase[note] ? PuzzlePlate.Look.Lit : PuzzlePlate.Look.Dim);
        }

        void RoundAnswered()
        {
            if (_round < _lamps.Count && _lamps[_round] != null) _lamps[_round].color = LampOn;
            if (_round + 1 >= _rounds) { Solve(); return; }
            _round++;
            _answered = 0;
            _heard = false;
            _nextIn = Tuning.Puzzle.EchoNextRoundSeconds;
        }

        // ---- the swap: shake, lift, slide to new places, set down ----

        void BeginSwap()
        {
            int n = Tuning.Puzzle.EchoStones;
            // A reload drops the seeded Rng (not serialized): carry on from the floor and round.
            Rng ??= new System.Random(Floor * 7919 + _round * 31 + _replaysLeft);
            // A new place for every stone - none stays where it was.
            var perm = new List<int>();
            for (int tries = 0; tries < 20; tries++)
            {
                perm.Clear();
                for (int i = 0; i < n; i++) perm.Add(i);
                for (int i = n - 1; i > 0; i--) { int j = Rng.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
                bool fixedPoint = false;
                for (int i = 0; i < n; i++) if (perm[i] == i) fixedPoint = true;
                if (!fixedPoint) break;
            }
            _swapFrom.Clear();
            for (int i = 0; i < n; i++)
            {
                _swapFrom.Add(SlotAt(_slot[i]).x);
                _slot[i] = perm[_slot[i]];
            }
            _swapping = true;
            _clock = 0f;
            Layer(true);
        }

        /// <summary>While sliding, each stone draws as one piece on its own layers, so two passing
        /// read as one over the other rather than their rims and faces interleaving.</summary>
        void Layer(bool apart)
        {
            for (int i = 0; i < Tuning.Puzzle.EchoStones; i++)
                foreach (var sr in Plates[i].GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.sortingOrder = SortingOrders.GroundDecal + ((sr.sortingOrder - SortingOrders.GroundDecal) & 3)
                                    + (apart ? 4 + i * 4 : 0);
                }
        }

        void TickSwap()
        {
            _clock += Time.deltaTime;
            float shake = Tuning.Puzzle.EchoSwapShakeSeconds, slide = Tuning.Puzzle.EchoSwapSlideSeconds;
            float texel = 1f / Art.FloorArt.PixelsPerUnit;
            for (int i = 0; i < Tuning.Puzzle.EchoStones && i < _swapFrom.Count; i++)
            {
                var to = SlotAt(_slot[i]);
                float fromX = _swapFrom[i];
                Vector2 at;
                if (_clock < shake)
                    at = new Vector2(fromX + (Mathf.FloorToInt(_clock * 25f) % 2 == 0 ? texel : -texel), to.y);
                else
                {
                    float k = Mathf.Clamp01((_clock - shake) / slide);
                    float e = k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) * 0.5f;
                    // Those going east pass above the line, those going west below it.
                    float arc = Mathf.Sin(Mathf.PI * k) * (to.x > fromX ? 0.45f : -0.45f);
                    at = new Vector2(Mathf.Lerp(fromX, to.x, e), to.y + arc);
                }
                // On whole texels, so the stones stay crisp as they move.
                Plates[i].transform.position = new Vector3(Mathf.Round(at.x / texel) * texel, Mathf.Round(at.y / texel) * texel, 0f);
            }
            if (_clock < shake + slide) return;

            _swapping = false;
            Layer(false);
            var ramp = Combat.TexelFx.RubbleOf(false);
            for (int i = 0; i < Tuning.Puzzle.EchoStones; i++)
            {
                var p = SlotAt(_slot[i]);
                Plates[i].transform.position = p;
                Combat.TexelFx.Puff(p + new Vector2(0f, -Tuning.Puzzle.PlateRadius * 0.3f), ramp, 4, 2.5f, 0.4f);
            }
        }

        void LateUpdate()
        {
            if (_arrow == null) return;
            bool show = State == PuzzleState.Active && _heard && !_calling && !_swapping && _nextIn <= 0f;
            _arrow.enabled = show;
            if (show) { var c = MarkViolet; c.a = 0.55f + 0.35f * Mathf.Sin(Time.time * 4f); _arrow.color = c; }
        }
    }
}
