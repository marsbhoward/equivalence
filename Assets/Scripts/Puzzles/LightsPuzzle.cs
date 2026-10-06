using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    /// <summary>
    /// LIGHTS. A 3x3 of stones; stepping one flips it and the four beside it. Light all nine.
    /// Pure logic - no memory, no clues.
    ///
    /// ALWAYS SOLVABLE, AND THE SCRAMBLE IS THE ANSWER. It starts all-lit and is scrambled by
    /// pressing a few distinct stones; on a 3x3 every pattern has exactly one set of presses that
    /// clears it, so the scramble's own presses are the shortest solution. The step budget is that
    /// count plus Tuning.Puzzle.LightsSlack, and running out is how this one fails - there is no
    /// single "wrong" stone in a puzzle where any stone can be undone.
    ///
    /// Stepping is literal: crossing the grid through a stone presses it. Walking round the
    /// outside is part of the puzzle.
    /// </summary>
    public class LightsPuzzle : PuzzleRoom
    {
        [SerializeField] List<bool> _on = new();
        [SerializeField] int _left;

        static readonly Color Gold = new(1f, 0.80f, 0.38f);

        public override string Title => "THE LIGHTS";

        public override string Body
        {
            get
            {
                if (State == PuzzleState.Solved) return "All nine lit. The sigil door opens.";
                if (State == PuzzleState.Failed) return "Out of steps. The sigil door is sealed - take the side door.";
                return "Each stone you step on flips itself and the stones beside it. Light all nine.\n" +
                       $"Steps left: {_left}";
            }
        }

        protected override void Setup()
        {
            float s = Tuning.Puzzle.LightsSpacing;
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                AddPlate(Centre + new Vector2((c - 1) * s, (1 - r) * s), Gold);
                _on.Add(true);
            }

            int k = Mathf.Min(Tuning.Puzzle.LightsPressesMax,
                              Tuning.Puzzle.LightsPressesBase + Floor / Tuning.Puzzle.LightsPressesEvery);
            var cells = Enumerable.Range(0, 9).ToList();
            for (int i = cells.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); (cells[i], cells[j]) = (cells[j], cells[i]); }
            foreach (var cell in cells.Take(k)) Press(cell);
            _left = k + Tuning.Puzzle.LightsSlack;
            Refresh();
        }

        protected override void Tick(int stepped)
        {
            if (stepped < 0) return;
            Press(stepped);
            _left--;
            Refresh();
            if (_on.All(x => x)) Solve();
            else if (_left <= 0) Fail();
        }

        void Press(int i)
        {
            int r = i / 3, c = i % 3;
            Flip(r, c); Flip(r - 1, c); Flip(r + 1, c); Flip(r, c - 1); Flip(r, c + 1);
        }

        void Flip(int r, int c)
        {
            if (r < 0 || r > 2 || c < 0 || c > 2) return;
            _on[r * 3 + c] = !_on[r * 3 + c];
        }

        void Refresh()
        {
            for (int i = 0; i < 9; i++)
                Plates[i].Set(_on[i] ? PuzzlePlate.Look.Lit : PuzzlePlate.Look.Dim);
        }
    }
}
