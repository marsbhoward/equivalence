using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    public enum PuzzleKind { Echo, Elements, Lights }
    public enum PuzzleState { Active, Solved, Failed }

    /// <summary>
    /// A puzzle floor's puzzle. GameBootstrap owns the floor around it - the shut sigil door, the
    /// side door to the adjacent room, the reward - and only POLLS <see cref="State"/>; the
    /// puzzle knows nothing about doors or rewards. Polled rather than evented so a domain reload
    /// (which drops delegates) cannot lose the answer.
    ///
    /// Each kind fails in its own way (a wrong stone, or running out of steps); GIVING UP is not
    /// the puzzle's business at all - it is walking through the side door.
    ///
    /// All randomness comes from the floor's seed (FloorPlanner.PuzzleSeed), so a restarted run
    /// meets the same puzzle and cannot reroll an unlucky one.
    /// </summary>
    public abstract class PuzzleRoom : MonoBehaviour
    {
        [SerializeField] PuzzleState _state;
        [SerializeField] protected Transform Player;
        [SerializeField] protected int Floor;
        [SerializeField] protected List<PuzzlePlate> Plates = new();
        protected System.Random Rng;

        public PuzzleState State => _state;

        /// <summary>The panel's heading and its live instructions.</summary>
        public abstract string Title { get; }
        public abstract string Body { get; }

        /// <summary>Where the puzzle's middle sits: a little north of the room's centre, between
        /// the player's arrival point and the door line.</summary>
        protected static Vector2 Centre => new(0f, 0.3f);

        public static PuzzleRoom Build(PuzzleKind kind, Transform parent, Transform player, int floor, int seed)
        {
            var go = new GameObject($"puzzle-{kind}");
            go.transform.SetParent(parent, false);
            PuzzleRoom r = kind switch
            {
                PuzzleKind.Echo => go.AddComponent<EchoPuzzle>(),
                PuzzleKind.Elements => go.AddComponent<ElementsPuzzle>(),
                _ => go.AddComponent<LightsPuzzle>(),
            };
            r.Player = player;
            r.Floor = floor;
            r.Rng = new System.Random(seed);
            r.Setup();
            return r;
        }

        protected abstract void Setup();

        /// <param name="stepped">Index into Plates of the stone stepped onto this frame, or -1.</param>
        protected abstract void Tick(int stepped);

        void Update()
        {
            if (_state != PuzzleState.Active || Player == null) return;
            // Every stone's edge is read EVERY frame, even while a puzzle is ignoring input (an
            // Echo call playing), or a stone stood on during the call would read as a fresh step
            // the moment input opened.
            int stepped = -1;
            for (int i = 0; i < Plates.Count; i++)
                if (Plates[i] != null && Plates[i].Entered(Player.position) && stepped < 0) stepped = i;
            Tick(stepped);
        }

        protected PuzzlePlate AddPlate(Vector2 at, Color tint, Sprite glyph = null, int pips = 0,
                                       float radius = Tuning.Puzzle.PlateRadius)
        {
            var p = PuzzlePlate.Make(transform, at, radius, tint, glyph, pips);
            Plates.Add(p);
            return p;
        }

        protected void Solve()
        {
            if (_state != PuzzleState.Active) return;
            _state = PuzzleState.Solved;
            foreach (var p in Plates) if (p) p.Set(PuzzlePlate.Look.Lit);
            Spr.Flash(Centre, 4.5f, new Color(0.75f, 0.9f, 1f), 0.5f);
        }

        /// <param name="wrong">The stone that was the wrong answer, left burning red among the
        /// dead ones so the player can see what they did.</param>
        protected void Fail(PuzzlePlate wrong = null)
        {
            if (_state != PuzzleState.Active) return;
            _state = PuzzleState.Failed;
            foreach (var p in Plates) if (p) p.Set(PuzzlePlate.Look.Dead);
            if (wrong) wrong.Set(PuzzlePlate.Look.Wrong);
        }

        protected static string Upper(ElementType e) => e.ToString().ToUpperInvariant();
    }
}
