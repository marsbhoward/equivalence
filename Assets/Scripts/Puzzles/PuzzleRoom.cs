using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    public enum PuzzleKind { Echo, Elements, Lights, Maze }
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

        /// <summary>The line flashed as the room is entered.</summary>
        public virtual string Intro => "solve it and the door opens  -  or take the side door and fight";

        /// <summary>True while the puzzle wants the CLOSE VIEW - the camera at menu density and
        /// the character in its menu art (GameBootstrap.SetCloseView). The labyrinth's, so its
        /// walls cannot be read off the screen.</summary>
        public virtual bool CloseView => false;

        /// <summary>Whether the side door stands open from the moment the room is entered. A
        /// puzzle that fills the floor has nowhere to put it - it opens only on a failure.</summary>
        public virtual bool SideDoorFromStart => true;

        /// <summary>Seconds left on the puzzle's clock, shown as the HUD countdown; negative for a
        /// puzzle with no clock (or one already answered).</summary>
        public virtual float Countdown => _state == PuzzleState.Active && _shortLeft >= 0f ? Mathf.Max(0f, _shortLeft) : -1f;

        /// <summary>A resumed puzzle's clock, put back to <paramref name="secondsLeft"/>. Nothing for a
        /// puzzle with no clock.</summary>
        public virtual void SetClock(float secondsLeft)
        {
            if (_state == PuzzleState.Active && _shortLeft >= 0f) _shortLeft = Mathf.Clamp(secondsLeft, 0.1f, ShortClockSeconds);
        }

        /// <summary>
        /// THE SHORT DESCENT's clock on a puzzle with none of its own (SHORT-DESCENT.md - the user's
        /// call: a puzzle floor has a set timer, and its seconds are the run clock's like any
        /// floor's). Running out fails it - the sigil door seals and the side door opens, as a wrong
        /// answer does. Negative: no clock. Classic's puzzles have none; the Labyrinth keeps its own.
        /// </summary>
        protected virtual float ShortClockSeconds => -1f;

        [SerializeField] float _shortLeft = -1f;

        /// <summary>Whether this puzzle has a clock running (the Short Descent's) - one that fails it
        /// when it runs out, so nothing else needs to.</summary>
        protected bool HasClock => _shortLeft >= 0f;

        /// <summary>The puzzle failed because its clock ran out, not on a wrong answer.</summary>
        protected bool TimedOut { get; private set; }

        protected const string TimedOutBody = "Out of time. The sigil door is sealed - take the side door.";

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
                PuzzleKind.Maze => go.AddComponent<MazePuzzle>(),
                _ => go.AddComponent<LightsPuzzle>(),
            };
            r.Player = player;
            r.Floor = floor;
            r.Rng = new System.Random(seed);
            r.Setup();
            if (RunFormat.Short && r.ShortClockSeconds > 0f) r._shortLeft = r.ShortClockSeconds;
            return r;
        }

        protected abstract void Setup();

        /// <param name="stepped">Index into Plates of the stone stepped onto this frame, or -1.</param>
        protected abstract void Tick(int stepped);

        void Update()
        {
            if (_state != PuzzleState.Active || Player == null) return;
            if (_shortLeft >= 0f)
            {
                _shortLeft -= Time.deltaTime;
                if (_shortLeft <= 0f)
                {
                    _shortLeft = 0f;
                    TimedOut = true;
                    Fail();
                    return;
                }
            }
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
            // Locked in (the user's pick, group 4): every stone sinks a texel, gold. The door then
            // opens with its own picture - no flash over the room.
            foreach (var p in Plates) if (p) p.LockIn();
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
