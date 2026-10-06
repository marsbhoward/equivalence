using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    /// <summary>
    /// WATCH AND REPEAT. A ring of stones around a pale centre stone; standing on the centre
    /// plays the CALL (stones light in a phrase), and the player ANSWERS by stepping the same
    /// stones in the same order. One wrong stone fails it.
    ///
    /// Deliberately the Cantor's grammar - call, then the same notes answered in the same order -
    /// met on a floor where getting it wrong costs a fight rather than a life. A player who has
    /// solved a few of these walks into floor 25 already knowing how to read the boss.
    ///
    /// The call can be replayed from the centre until the first stone is answered; after that the
    /// player is committed. No immediate repeats in a phrase (the Cantor's rule too): two lights
    /// on one stone in a row read as one long light.
    /// </summary>
    public class EchoPuzzle : PuzzleRoom
    {
        [SerializeField] List<int> _phrase = new();
        [SerializeField] int _answered;
        [SerializeField] bool _calling, _heard;
        [SerializeField] float _clock;
        [SerializeField] int _start;

        static readonly Color[] Hues =
        {
            new(1.00f, 0.42f, 0.30f), new(1.00f, 0.80f, 0.30f), new(0.45f, 0.90f, 0.45f),
            new(0.35f, 0.85f, 1.00f), new(0.50f, 0.55f, 1.00f), new(0.95f, 0.50f, 0.95f),
        };

        public override string Title => "THE ECHO";

        public override string Body
        {
            get
            {
                if (State == PuzzleState.Solved) return "Answered. The sigil door opens.";
                if (State == PuzzleState.Failed) return "Wrong stone. The sigil door is sealed - take the side door.";
                if (_calling) return "Listen...";
                if (!_heard)
                    return "Stand on the pale centre stone to hear the call, then step the stones in the " +
                           "same order. One wrong stone seals the door.";
                return $"Answer the call   {_answered} / {_phrase.Count}" +
                       (_answered == 0 ? "\nThe centre stone plays it again." : "");
            }
        }

        protected override void Setup()
        {
            int n = Tuning.Puzzle.EchoStones;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / n;
                var at = Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Tuning.Puzzle.EchoRingRadius;
                AddPlate(at, Hues[i % Hues.Length], pips: i + 1);
            }
            AddPlate(Centre, new Color(0.92f, 0.92f, 0.96f), radius: Tuning.Puzzle.PlateRadius * 1.15f);
            _start = Plates.Count - 1;

            int len = Mathf.Min(Tuning.Puzzle.EchoLengthMax,
                                Tuning.Puzzle.EchoLengthBase + (Floor - 1) / Tuning.Puzzle.EchoLengthEvery);
            int last = -1;
            for (int i = 0; i < len; i++)
            {
                int s = last < 0 ? Rng.Next(n) : Rng.Next(n - 1);
                if (last >= 0 && s >= last) s++;   // skip the previous stone
                _phrase.Add(s);
                last = s;
            }
        }

        protected override void Tick(int stepped)
        {
            float per = Tuning.Puzzle.EchoNoteSeconds + Tuning.Puzzle.EchoGapSeconds;

            if (_calling)
            {
                _clock += Time.deltaTime;
                if (_clock < 0f) return;
                int note = Mathf.FloorToInt(_clock / per);
                if (note >= _phrase.Count)
                {
                    _calling = false;
                    _heard = true;
                    for (int i = 0; i < _start; i++) Plates[i].Set(PuzzlePlate.Look.Dim);
                    return;
                }
                bool on = _clock - note * per < Tuning.Puzzle.EchoNoteSeconds;
                for (int i = 0; i < _start; i++)
                    Plates[i].Set(on && i == _phrase[note] ? PuzzlePlate.Look.Lit : PuzzlePlate.Look.Dim);
                return;
            }

            if (stepped < 0) return;

            if (stepped == _start)
            {
                if (_answered > 0) return;   // committed - no more replays
                _calling = true;
                _clock = -0.4f;              // a breath before the first note
                Plates[_start].Pulse(0.3f);
                return;
            }

            if (!_heard) return;             // stones mean nothing before the call

            if (stepped == _phrase[_answered])
            {
                Plates[stepped].Pulse(0.35f);
                if (++_answered >= _phrase.Count) Solve();
            }
            else Fail(Plates[stepped]);
        }
    }
}
