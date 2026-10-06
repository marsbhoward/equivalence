using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    /// <summary>
    /// ELEMENT ORDER. Four stones, one per element, to be stepped in an order the player DEDUCES
    /// from clues ("FIRE comes somewhere before WATER", "EARTH is not last"). Reasoning, not
    /// memory - the counterpart to the Echo. One wrong stone fails it.
    ///
    /// THE CLUES ARE A MINIMAL SET THAT PINS ONE ORDER. Generated against all 24 orders: true
    /// clues are added (shuffled) only while they cut the candidates down, until one order is
    /// left, then any clue the rest already imply is dropped. So every clue is needed and the
    /// answer is never ambiguous. From Tuning.Puzzle.ElementsHardFloor the clues stop naming a
    /// first or last stone outright.
    ///
    /// The stones' positions are shuffled too, so where a stone stands never hints at its turn.
    /// </summary>
    public class ElementsPuzzle : PuzzleRoom
    {
        /// <summary>The answer, as element ints in order.</summary>
        [SerializeField] List<int> _order = new();
        /// <summary>Which element each plate is (index into Plates).</summary>
        [SerializeField] List<int> _plateElement = new();
        [SerializeField] List<string> _clues = new();
        [SerializeField] int _done;

        public override string Title => "THE ELEMENTS";

        public override string Body
        {
            get
            {
                if (State == PuzzleState.Solved) return "The order holds. The sigil door opens.";
                if (State == PuzzleState.Failed) return "Out of order. The sigil door is sealed - take the side door.";
                return "Step the four stones in the one order these allow. One wrong stone seals the door.\n\n" +
                       string.Join("\n", _clues.Select(c => "-  " + c)) +
                       $"\n\n{_done} / 4";
            }
        }

        static readonly Vector2[] Slots = { new(0f, 2.4f), new(2.9f, 0f), new(0f, -2.4f), new(-2.9f, 0f) };

        protected override void Setup()
        {
            var elems = new List<int> { 0, 1, 2, 3 };
            _order = Shuffled(elems);
            var placed = Shuffled(elems);
            for (int i = 0; i < 4; i++)
            {
                var e = (ElementType)placed[i];
                AddPlate(Centre + Slots[i], ElementInfo.Tint(e), Combat.Glyphs.Element(e));
                _plateElement.Add(placed[i]);
            }
            _clues = MakeClues(_order, Floor >= Tuning.Puzzle.ElementsHardFloor);
        }

        protected override void Tick(int stepped)
        {
            if (stepped < 0 || Plates[stepped].Current == PuzzlePlate.Look.Lit) return;
            if (_plateElement[stepped] == _order[_done])
            {
                Plates[stepped].Set(PuzzlePlate.Look.Lit);
                if (++_done >= 4) Solve();
            }
            else Fail(Plates[stepped]);
        }

        List<int> Shuffled(List<int> src)
        {
            var l = new List<int>(src);
            for (int i = l.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
            return l;
        }

        // ------------------------------------------------------------------ clues

        /// <summary>One clue: its text, and the test it puts to a candidate order (pos[element] =
        /// that element's turn, 0-based).</summary>
        readonly struct Clue
        {
            public readonly string Text;
            public readonly Func<int[], bool> Holds;
            public Clue(string text, Func<int[], bool> holds) { Text = text; Holds = holds; }
        }

        List<string> MakeClues(List<int> order, bool hard)
        {
            var pos = new int[4];
            for (int i = 0; i < 4; i++) pos[order[i]] = i;

            var pool = new List<Clue>();
            string N(int e) => Upper((ElementType)e);
            for (int a = 0; a < 4; a++)
            {
                int ca = a;
                if (!hard)
                {
                    pool.Add(new Clue($"{N(a)} is first", p => p[ca] == 0));
                    pool.Add(new Clue($"{N(a)} is last", p => p[ca] == 3));
                }
                pool.Add(new Clue($"{N(a)} is not first", p => p[ca] != 0));
                pool.Add(new Clue($"{N(a)} is not last", p => p[ca] != 3));
                for (int b = 0; b < 4; b++)
                {
                    if (a == b) continue;
                    int cb = b;
                    pool.Add(new Clue($"{N(a)} comes somewhere before {N(b)}", p => p[ca] < p[cb]));
                    pool.Add(new Clue($"{N(b)} comes right after {N(a)}", p => p[cb] == p[ca] + 1));
                    if (a < b)
                        pool.Add(new Clue($"{N(a)} and {N(b)} are not side by side",
                                          p => Math.Abs(p[ca] - p[cb]) > 1));
                }
            }
            // Only clues TRUE of the answer.
            pool = pool.Where(c => c.Holds(pos)).ToList();
            for (int i = pool.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }

            var all = AllOrders();
            var chosen = new List<Clue>();
            foreach (var c in pool)
            {
                if (Count(all, chosen) == 1) break;
                int before = Count(all, chosen);
                chosen.Add(c);
                if (Count(all, chosen) == before) chosen.RemoveAt(chosen.Count - 1);
            }
            // Drop anything the others already imply.
            for (int i = chosen.Count - 1; i >= 0; i--)
            {
                var c = chosen[i];
                chosen.RemoveAt(i);
                if (Count(all, chosen) != 1) chosen.Insert(i, c);
            }
            _lastClues = chosen;
            return chosen.Select(c => c.Text).ToList();
        }

        /// <summary>Regenerates the clue set (same Rng state as MakeClues would see) and checks
        /// exactly one order satisfies it, and that it is the answer.</summary>
        bool PinsOne(List<int> order, bool hard)
        {
            _lastClues = null;
            MakeClues(order, hard);
            var pos = new int[4];
            for (int i = 0; i < 4; i++) pos[order[i]] = i;
            var survivors = AllOrders().Where(p => _lastClues.All(c => c.Holds(p))).ToList();
            return survivors.Count == 1 && survivors[0].SequenceEqual(pos);
        }
        List<Clue> _lastClues;

        static int Count(List<int[]> orders, List<Clue> clues)
            => orders.Count(p => clues.All(c => c.Holds(p)));

        /// <summary>Every order of the four, as pos arrays.</summary>
        static List<int[]> AllOrders()
        {
            var res = new List<int[]>();
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            for (int c = 0; c < 4; c++)
            for (int d = 0; d < 4; d++)
                if (a != b && a != c && a != d && b != c && b != d && c != d)
                    res.Add(new[] { a, b, c, d });
            return res;
        }

        /// <summary>For testing from eval: the clue sets for a spread of seeds, each checked to
        /// pin exactly one order (and that order to be the answer).</summary>
        public static string Check(int seeds, bool hard)
        {
            var go = new GameObject("elements-check");
            var p = go.AddComponent<ElementsPuzzle>();
            int bad = 0, total = 0;
            var sizes = new Dictionary<int, int>();
            for (int s = 0; s < seeds; s++)
            {
                p.Rng = new System.Random(s);
                var order = p.Shuffled(new List<int> { 0, 1, 2, 3 });
                var clues = p.MakeClues(order, hard);
                sizes[clues.Count] = sizes.TryGetValue(clues.Count, out var n) ? n + 1 : 1;
                total++;
                // Re-derive the clue tests from the texts' generator and check the set pins the answer.
                var pos = new int[4];
                for (int i = 0; i < 4; i++) pos[order[i]] = i;
                p.Rng = new System.Random(s);
                p.Shuffled(new List<int> { 0, 1, 2, 3 });
                if (!p.PinsOne(order, hard)) bad++;
            }
            DestroyImmediate(go);
            return $"{total} sets, {bad} not pinning exactly the answer; clue counts " +
                   string.Join(", ", sizes.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}"));
        }
    }
}
