using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Lighting;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Bosses
{
    /// <summary>
    /// Fortuna, the gambler (the user's design, 2026-11-02 - BOSSES.md "Fortuna"). Every number is in
    /// <see cref="Tuning.Fortuna"/>, per band in Tuning.EnemyBands.Fortuna*.
    ///
    ///     DEAL     a tarot card flips over her head and names the game, drawn at random (never the same twice
    ///              running, except a lost game, which is dealt again): the Magician, the Wheel of Fortune, the
    ///              Tower, the Moon.
    ///     GAME     the SHELL GAME (follow the stone, strike its cup), the WHEEL (knock the ball into her gold
    ///              pockets), BLACKJACK (every strike on her draws a card; stand on a chip at 21 or under), the
    ///              BLUFF (parry a real blow - its claws come out; dodge a bluff and she has a free hit).
    ///     REST     a win opens her window (the shared cap). A loss is a mechanic hit, and the game dealt again.
    ///
    /// SHE CHEATS in any phase (the user's call: always cheating in the enrage would be boring). Each cheat plays
    /// one TELL, shuffled from a pool - the band slipping off her cat eye, an ear flattening, the crown mask's
    /// faces swapping, a card at her wrist, the tail's serpent waking, a smirk - and EVERY real tell GLINTS.
    /// Striking her while it glints CATCHES her: the game is won outright and her window stays open longer. The
    /// higher the band, the more often the tell changes; at band 4 she also plays FAKES, with no glint - strike
    /// one and you are punished.
    ///
    /// THE ENRAGE (35%): the band and the cape fall to the floor and stay; the dark plate breaks (more at higher
    /// bands, the belt a bikini at the top two); and she moves and acts faster - her whole fight, by band.
    ///
    /// She is ALWAYS hittable (her collider stays on) but nothing lands outside a window: Health.BlocksHit takes
    /// the strike and reads it - Blackjack's draw, the catch. Driven from ONE coroutine that checks its token.
    /// </summary>
    public class Fortuna : Boss
    {
        public enum Movement { Dealing, Shuffling, Choosing, Spinning, Drawing, Bluffing, Rest, Caught, Enraging, Throwing }
        enum Result { Win, Lose, Caught }

        public Movement Phase { get; private set; } = Movement.Dealing;
        public override string DisplayName => "FORTUNA";
        public override string PhaseLabel => Phase switch
        {
            Movement.Rest => "OPEN",
            Movement.Caught => "CAUGHT",
            Movement.Enraging => "ALL IN",
            _ => _game switch { FortunaFloor.Shell => "THE MAGICIAN", FortunaFloor.Wheel => "THE WHEEL", FortunaFloor.Blackjack => "THE TOWER", FortunaFloor.Bluff => "THE MOON", _ => "THE DEAL" },
        };
        public override bool ReadsShut => !_open;
        public override bool HoldsCamera => _hold;

        /// <summary>DEV: the game the next deals draw (FortunaFloor.Shell..Bluff), -1 the shuffle. Set after Play starts.</summary>
        public static int DevGame = -1;

        [SerializeField] int _seed, _rolls, _game = -1, _lastGame = -1, _tell = -1, _tellCheats, _cards, _drawRequests, _deals;
        [SerializeField] bool _redeal, _open, _hold, _bandOff, _cheating, _catchOpen, _fakeOpen, _caught, _accused, _drawing;
        [SerializeField] float _speed = 1f;
        [SerializeField] FortunaFigure _figure;
        [SerializeField] FortunaFloor _floor;
        [SerializeField] FortunaDeal _deal;
        [SerializeField] Transform _room;
        [SerializeField] Glow _glow;
        [SerializeField] List<FortunaCup> _cups = new();
        [SerializeField] List<FortunaBall> _balls = new();
        System.Random _rng;

        static readonly Color CandleLight = new(1f, 0.78f, 0.45f), CatchLight = new(1f, 0.9f, 0.55f);

        int B => Mathf.Clamp(Band, 0, 4);
        float Tm(float s) => s / Mathf.Max(0.1f, _speed);
        Health PlayerHp => Target != null ? Target.GetComponent<Health>() : null;
        Player.PlayerController Pc => Target != null ? Target.GetComponent<Player.PlayerController>() : null;
        Vector2 PlayerFeet => Target != null ? ReleaseCanvas.Feet(Target) : Vector2.zero;
        Vector2 HandAt => (Vector2)transform.position + new Vector2(0.45f, 0.75f);

        float Roll() { _rng ??= new System.Random(_seed * 7919 + _rolls * 31); _rolls++; return (float)_rng.NextDouble(); }
        int Roll(int n) => Mathf.Min(n - 1, Mathf.FloorToInt(Roll() * n));

        public static Fortuna Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            var b = CreateBody<Fortuna>("boss.fortuna", Tuning.Fortuna.DealerSpot, target, parent, floor, Tuning.Fortuna.BodyRadius, Tuning.Fortuna.Hp);
            b._seed = NextSeed;
            b._figure = TexelPicture.Attach<FortunaFigure>(b.transform, "figure", Vector2.zero, 80, 16, 140, 0, FortunaCanvas.Ppu75);
            b._figure.Look.Mood = FortunaMood.Sway;
            DepthSorted.Attach(b.gameObject, 0f, false, b._figure.GetComponent<SpriteRenderer>());
            b._glow = Glow.Attach(b.gameObject, 2.6f, CandleLight, 0.5f, 0.05f);
            var room = new GameObject("fortuna.room");
            room.transform.SetParent(parent, false);
            b._room = room.transform;
            b._floor = FortunaFloor.Build(b._room);
            b._deal = FortunaDeal.On(b.transform);
            // Always struck, never hurt outside a window: the collider stays on and BlocksHit reads the strike.
            b.Health.Immune = false;
            if (b.BodyCollider != null) b.BodyCollider.enabled = true;
            b.HookStrike();
            b.StartCoroutine(b.Fight());
            return b;
        }

        void HookStrike()
        {
            if (Health == null) return;
            Health.BlocksHit = info =>
            {
                if (_open) return false;
                if (!info.Tick) Struck();
                return true;
            };
        }

        void Update()
        {
            if (Health != null && Health.BlocksHit == null) HookStrike();   // a delegate: lost to a domain reload
            if (_figure != null) _figure.Look.Clock += Time.deltaTime * _speed;
        }

        public override void Cease()
        {
            base.Cease();
            if (_room != null) Destroy(_room.gameObject);
        }

        /// <summary>A strike that did not land: it catches a glinting cheat, is a false accusation against a fake, or draws a card.</summary>
        void Struck()
        {
            if (_cheating && _catchOpen) { _caught = true; return; }
            if (_fakeOpen) { _accused = true; return; }
            if (_drawing) _drawRequests++;
            else Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, (Vector2)transform.position + Vector2.up * 0.6f);
        }

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;
            while (true)
            {
                if (!Enraged)
                {
                    CheckEnrage(Tuning.Fortuna.EnrageAt);
                    if (Enraged) { yield return Enrage(token); if (token != Token) yield break; }
                }

                int game = DevGame >= 0 && DevGame < 4 ? DevGame : _redeal && _game >= 0 ? _game : PickGame();
                ShuffleTellOnDeal();
                yield return Deal(token, game);
                if (token != Token) yield break;

                _cheating = Roll() < Tuning.EnemyBands.FortunaCheatChance[B];
                bool fake = !_cheating && B >= Tuning.EnemyBands.FortunaFakesFromBand && Roll() < Tuning.EnemyBands.FortunaFakeChance;
                _caught = _accused = _catchOpen = _fakeOpen = false;
                _deals++;   // a tell belongs to its deal: one still playing when the game ends stops touching the flags
                var result = Result.Lose;
                bool lossHit = true;
                switch (game)
                {
                    case FortunaFloor.Shell: yield return Shell(token, fake, r => result = r); break;
                    case FortunaFloor.Wheel: yield return Wheel(token, fake, r => result = r); break;
                    case FortunaFloor.Blackjack: yield return Blackjack(token, fake, r => result = r); break;
                    default: yield return Bluff(token, fake, r => result = r); lossHit = false; break;
                }
                if (token != Token) yield break;
                _catchOpen = _fakeOpen = false;
                if (_floor != null) _floor.Active = -1;
                if (_cheating) _tellCheats++;
                _lastGame = game;

                if (_accused) LossHit(0.5f);
                if (result == Result.Caught)
                {
                    _redeal = false;
                    yield return CaughtOut(token);
                    if (token != Token) yield break;
                    if (Tuning.EnemyBands.FortunaTellShuffle[B] >= 2) _tell = PickTell();
                    yield return Rest(token, Tuning.Fortuna.CaughtRestSeconds);
                }
                else if (result == Result.Win)
                {
                    _redeal = false;
                    yield return Rest(token, Tuning.Fortuna.RestSeconds);
                }
                else
                {
                    _redeal = true;
                    if (lossHit) LossHit(1f);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, transform.position);
                }
                if (token != Token) yield break;
                _cheating = false;
                yield return new WaitForSeconds(Tm(Tuning.Fortuna.BetweenGamesSeconds));
                if (token != Token) yield break;
                yield return Throw(token, (_cards++ & 1) == 0);
                if (token != Token) yield break;
            }
        }

        int PickGame()
        {
            if (DevGame >= 0 && DevGame < 4) return DevGame;
            int g;
            do g = Roll(4); while (g == _lastGame);
            return g;
        }

        IEnumerator Deal(int token, int game)
        {
            Phase = Movement.Dealing;
            _game = game;
            _hold = true;   // the deal is watched: the camera takes in the room
            SetMood(FortunaMood.Sway);
            yield return WalkTo(token, Tuning.Fortuna.DealerSpot);
            if (token != Token) yield break;
            if (_deal != null) _deal.Card = game;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Toll, transform.position);
            float s = Tuning.Fortuna.DealSeconds;
            for (float t = 0f; t < s; t += Time.deltaTime)
            {
                if (_deal != null) _deal.Flip = t / s;
                if (t > s * 0.45f && _floor != null) _floor.Active = game;
                yield return null;
                if (token != Token) yield break;
            }
            if (_deal != null) { _deal.Flip = 0f; _deal.Card = -1; }
            _hold = false;
        }

        // ------------------------------------------------------------------ the tells

        /// <summary>The pool: the band's slip leaves it once the band has fallen.</summary>
        int PickTell()
        {
            int t;
            do t = 1 + Roll(6); while (t == FortunaTell.Slip && _bandOff);
            return t;
        }

        void ShuffleTellOnDeal()
        {
            int level = Tuning.EnemyBands.FortunaTellShuffle[B];
            if (_tell < 0 || (_tell == FortunaTell.Slip && _bandOff)) { _tell = PickTell(); return; }
            if (level == 1 || level == 2) _tell = PickTell();
            else if (level == 3 && (_tellCheats & 1) == 1) _tell = PickTell();
            else if (level == 4) _tell = PickTell();
        }

        /// <summary>
        /// The tell, run alongside the game at its moment. A real one GLINTS and from its glint on, a strike CATCHES
        /// her until the game settles it; a fake never glints, and a strike while it plays is a false accusation.
        /// </summary>
        IEnumerator PlayTell(int token, bool fake)
        {
            if (_figure == null) yield break;
            int deal = _deals;
            int tell = _tell > 0 ? _tell : PickTell();
            _figure.Look.Tell = tell; _figure.Look.Fake = fake; _figure.Look.TellAmt = 0f;
            int mood = _figure.Look.Mood;
            if (tell != FortunaTell.Serpent) SetMood(FortunaMood.Flick);
            float s = Tm(Tuning.Fortuna.TellSeconds);
            if (fake) _fakeOpen = true;
            for (float t = 0f; t < s; t += Time.deltaTime)
            {
                float u = t / s, amt = Mathf.Clamp01(u / 0.3f) * (1f - Mathf.Clamp01((u - 0.75f) / 0.25f));
                if (tell == FortunaTell.Slip)
                {
                    _figure.Look.Slip = Mathf.Clamp01(u / 0.25f) * (1f - Mathf.Clamp01((u - 0.7f) / 0.2f));
                    _figure.Look.Hand = Mathf.Clamp01((u - 0.45f) / 0.25f) * (1f - Mathf.Clamp01((u - 0.85f) / 0.15f));
                }
                else _figure.Look.TellAmt = amt;
                if (deal != _deals) break;
                if (!fake && u > 0.3f) _catchOpen = true;
                yield return null;
                if (token != Token) yield break;
            }
            _figure.Look.TellAmt = 0f; _figure.Look.Slip = 0f; _figure.Look.Hand = 0f; _figure.Look.Tell = 0; _figure.Look.Fake = false;
            if (_figure.Look.Mood == FortunaMood.Flick) SetMood(mood);
            if (fake)
            {
                yield return new WaitForSeconds(Tuning.Fortuna.CatchGraceSeconds);
                if (deal == _deals) _fakeOpen = false;
            }
        }

        // ------------------------------------------------------------------ the shell game

        Vector2 CupSlot(int k, int n, bool wander)
        {
            var c = Tuning.Fortuna.TableCentre;
            if (!wander) return c + Vector2.right * ((k - (n - 1) * 0.5f) * Tuning.Fortuna.CupRowSpacing);
            float a = -Mathf.PI * 0.5f + k * Mathf.PI * 2f / n;
            return c + new Vector2(Mathf.Cos(a) * 3.4f, Mathf.Sin(a) * 2.0f);
        }

        IEnumerator Shell(int token, bool fake, System.Action<Result> done)
        {
            Phase = Movement.Shuffling;
            int n = Tuning.EnemyBands.FortunaCups[B];
            bool wander = B >= Tuning.EnemyBands.FortunaWanderFromBand;
            ClearCups();
            var slot = new int[n];
            for (int k = 0; k < n; k++)
            {
                var cup = FortunaCup.Make(_room, k);
                cup.Root.position = CupSlot(k, n, wander);
                _cups.Add(cup); slot[k] = k;
            }
            int stone = Roll(n);
            _cups[stone].Stone = true;
            _hold = true;
            // show the stone under its cup
            float show = Tuning.Fortuna.ShowSeconds;
            for (float t = 0f; t < show; t += Time.deltaTime) { _cups[stone].Lift = Mathf.Sin(Mathf.Clamp01(t / show) * Mathf.PI); yield return null; if (token != Token) yield break; }
            _cups[stone].Lift = 0f;

            int swaps = Tuning.EnemyBands.FortunaSwaps[B];
            float swapS = Tm(Tuning.Fortuna.SwapSeconds / Tuning.EnemyBands.FortunaSwapSpeed[B]);
            for (int s = 0; s < swaps; s++)
            {
                int a = Roll(n), b; do b = Roll(n); while (b == a);
                if (s == swaps / 2 && B >= Tuning.EnemyBands.FortunaThrowsWhileShufflingFromBand) StartCoroutine(Spread(token));
                if (s == swaps - 1 && (_cheating || fake)) StartCoroutine(PlayTell(token, fake));
                yield return SwapCups(token, a, b, slot, n, wander, swapS);
                if (token != Token) yield break;
            }
            // the palm: the stone is in no cup
            if (_cheating) foreach (var c in _cups) if (c != null) c.Stone = false;
            // the last-second switch, its honest tell a shimmer on the two cups first
            if (B >= Tuning.EnemyBands.FortunaSwitchFromBand)
            {
                int a = Roll() < 0.6f ? stone : Roll(n), b; do b = Roll(n); while (b == a);
                for (float t = 0f; t < Tuning.Fortuna.SwitchTellSeconds; t += Time.deltaTime) { _cups[a].Shimmer = _cups[b].Shimmer = 1f; yield return null; if (token != Token) yield break; }
                _cups[a].Shimmer = _cups[b].Shimmer = 0f;
                yield return SwapCups(token, a, b, slot, n, wander, swapS * 0.45f);
                if (token != Token) yield break;
            }
            _hold = false;

            Phase = Movement.Choosing;
            SetMood(FortunaMood.Alert);
            foreach (var c in _cups) if (c != null) c.Choosable = true;
            int picked = -1;
            for (float t = 0f; t < Tuning.Fortuna.ChooseSeconds; t += Time.deltaTime)
            {
                if (_caught) break;
                for (int k = 0; k < _cups.Count; k++) if (_cups[k] != null && _cups[k].Struck) { picked = k; break; }
                if (picked >= 0) break;
                yield return null;
                if (token != Token) yield break;
            }
            foreach (var c in _cups) if (c != null) c.Choosable = false;
            if (_caught) { ClearCups(); done(Result.Caught); yield break; }

            bool won = picked >= 0 && _cups[picked].Stone;
            // the reveal: the picked cup lifts (and the stone's, if it was another)
            for (float t = 0f; t < Tuning.Fortuna.RevealSeconds; t += Time.deltaTime)
            {
                for (int k = 0; k < _cups.Count; k++) if (_cups[k] != null && (k == picked || _cups[k].Stone)) _cups[k].Lift = Mathf.Clamp01(t / 0.3f);
                yield return null;
                if (token != Token) yield break;
            }
            ClearCups();
            done(won ? Result.Win : Result.Lose);
        }

        IEnumerator SwapCups(int token, int a, int b, int[] slot, int n, bool wander, float seconds)
        {
            Vector2 pa = CupSlot(slot[a], n, wander), pb = CupSlot(slot[b], n, wander);
            var mid = (pa + pb) * 0.5f; var perp = Vector2.Perpendicular(pb - pa).normalized * Mathf.Min(1.2f, (pb - pa).magnitude * 0.35f);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Swipe, mid, 0.5f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = Smooth(t / seconds), arc = Mathf.Sin(u * Mathf.PI);
                if (_cups[a] != null) _cups[a].Root.position = Vector2.Lerp(pa, pb, u) + perp * arc;
                if (_cups[b] != null) _cups[b].Root.position = Vector2.Lerp(pb, pa, u) - perp * arc;
                yield return null;
                if (token != Token) yield break;
            }
            if (_cups[a] != null) _cups[a].Root.position = pb;
            if (_cups[b] != null) _cups[b].Root.position = pa;
            (slot[a], slot[b]) = (slot[b], slot[a]);
        }

        void ClearCups()
        {
            _cups ??= new List<FortunaCup>();
            foreach (var c in _cups) if (c != null) Destroy(c.Root.gameObject);
            _cups.Clear();
        }

        // ------------------------------------------------------------------ the wheel

        IEnumerator Wheel(int token, bool fake, System.Action<Result> done)
        {
            Phase = Movement.Spinning;
            SetMood(FortunaMood.Alert);
            int pockets = Tuning.Fortuna.Pockets, off = Roll(2);
            if (_floor != null) { for (int k = 0; k < pockets; k++) _floor.Pocket[k] = ((k + off) & 1) == 0; _floor.Settled = -1; }
            int nb = Tuning.EnemyBands.FortunaBalls[B];
            _balls ??= new List<FortunaBall>();
            ClearBalls();
            var theta = new float[nb]; var omega = new float[nb]; var settled = new int[nb]; var reversed = new bool[nb];
            float step = Mathf.PI * 2f / pockets, ringR = Tuning.Fortuna.WheelRadius - 0.45f;
            for (int i = 0; i < nb; i++)
            {
                _balls.Add(FortunaBall.Make(_room, i));
                theta[i] = Roll() * Mathf.PI * 2f; omega[i] = Tuning.Fortuna.BallSpin * (Roll() < 0.5f ? 1f : -1f) * (1f + i * 0.25f); settled[i] = -1;
            }
            bool tellStarted = false, threw = false;
            float reverseAt = 2f + Roll() * 3f;
            _hold = true;
            for (float t = 0f; t < Tuning.Fortuna.WheelMaxSeconds; t += Time.deltaTime)
            {
                if (_caught) break;
                bool all = true;
                for (int i = 0; i < nb; i++)
                {
                    if (settled[i] >= 0 || _balls[i] == null) continue;
                    all = false;
                    if (_balls[i].Kick != 0f) { omega[i] += _balls[i].Kick * Tuning.Fortuna.BallKick; _balls[i].Kick = 0f; }
                    if (B >= Tuning.EnemyBands.FortunaReverseFromBand && !reversed[i] && t > reverseAt + i) { omega[i] = -omega[i] * 0.9f; reversed[i] = true; }
                    omega[i] = Mathf.MoveTowards(omega[i], 0f, Tuning.Fortuna.BallDrag * _speed * Time.deltaTime);
                    theta[i] += omega[i] * _speed * Time.deltaTime;
                    if (!tellStarted && i == 0 && Mathf.Abs(omega[i]) < Tuning.Fortuna.BallSettle * 3f && (_cheating || fake)) { tellStarted = true; StartCoroutine(PlayTell(token, fake)); }
                    if (Mathf.Abs(omega[i]) < Tuning.Fortuna.BallSettle)
                    {
                        int k = Mathf.FloorToInt(Mathf.Repeat(theta[i], Mathf.PI * 2f) / step) % pockets;
                        theta[i] = (k + 0.5f) * step;
                        settled[i] = k;
                        // the shifting pocket: her gold pocket turns red under the ball
                        if (_cheating && i == 0 && _floor != null && _floor.Pocket[k]) { _floor.Pocket[k] = false; Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, FortunaFloor.WheelCentre); }
                        if (_floor != null) _floor.Settled = k;
                        Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Pop, (Vector2)_balls[i].Root.position);
                    }
                    _balls[i].Root.position = FortunaFloor.WheelCentre + new Vector2(Mathf.Cos(theta[i]), Mathf.Sin(theta[i])) * ringR;
                }
                if (!threw && t > 2.5f && B >= Tuning.EnemyBands.FortunaThrowsWhileShufflingFromBand) { threw = true; StartCoroutine(Orbit(token)); }
                if (all) break;
                yield return null;
                if (token != Token) yield break;
            }
            _hold = false;
            if (_caught) { ClearBalls(); if (_floor != null) _floor.Settled = -1; done(Result.Caught); yield break; }
            // a beat on the result, then the balls clear
            yield return new WaitForSeconds(0.6f);
            if (token != Token) yield break;
            bool won = true;
            for (int i = 0; i < nb; i++) if (settled[i] < 0 || _floor == null || !_floor.Pocket[settled[i]]) won = false;
            ClearBalls();
            if (_floor != null) _floor.Settled = -1;
            done(won ? Result.Win : Result.Lose);
        }

        void ClearBalls()
        {
            _balls ??= new List<FortunaBall>();
            foreach (var b in _balls) if (b != null) Destroy(b.Root.gameObject);
            _balls.Clear();
        }

        // ------------------------------------------------------------------ blackjack

        IEnumerator Blackjack(int token, bool fake, System.Action<Result> done)
        {
            Phase = Movement.Drawing;
            SetMood(FortunaMood.Alert);
            _hold = true;   // the pot over her head and the chips at the far end both have to be seen
            var pot = new List<int>();
            int total = 0, aces = 0;
            bool tellStarted = false, forceTen = false, stood = false;
            float lastDraw = -9f;
            _drawing = true; _drawRequests = 0;
            if (_deal != null) { _deal.Pot = new int[0]; _deal.Total = 0; _deal.House = -1; }
            for (float t = 0f; t < Tuning.Fortuna.BlackjackSeconds; t += Time.deltaTime)
            {
                if (_caught) break;
                if (_drawRequests > 0 && Time.time - lastDraw > Tuning.Fortuna.DrawCooldown)
                {
                    _drawRequests = 0; lastDraw = Time.time;
                    int v = forceTen ? 10 : Tuning.Fortuna.Deck[Roll(Tuning.Fortuna.Deck.Length)];
                    forceTen = false;
                    if (v == 11) aces++;
                    total += v;
                    while (total > Tuning.Fortuna.Limit && aces > 0) { total -= 10; aces--; }
                    pot.Add(v);
                    if (_deal != null) { _deal.Pot = pot.ToArray(); _deal.Total = total; }
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, (Vector2)transform.position + Vector2.up * 2f);
                    if (total > Tuning.Fortuna.Limit) break;
                }
                _drawRequests = 0;
                // the cheat: once the pot is worth busting, a card comes from her sleeve - caught in its glint, or it is drawn next
                if (!tellStarted && total >= 12 && (_cheating || fake)) { tellStarted = true; StartCoroutine(PlayTell(token, fake)); }
                if (_cheating && _catchOpen) forceTen = true;
                if (pot.Count >= 2)
                    foreach (var c in Tuning.Fortuna.Chips)
                        if ((PlayerFeet - c).magnitude < Tuning.Fortuna.ChipRadius) { stood = true; break; }
                if (stood) break;
                yield return null;
                if (token != Token) { _drawing = false; _hold = false; yield break; }
            }
            _drawing = false;
            _hold = false;
            if (_caught) { ClearPot(); done(Result.Caught); yield break; }
            bool bust = total > Tuning.Fortuna.Limit, won = !bust && pot.Count >= 2;
            // band 2 on, the house plays its own hand, and you must beat it
            if (won && B >= Tuning.EnemyBands.FortunaHouseFromBand)
            {
                int house = 0, hAces = 0;
                while (house < Tuning.Fortuna.HouseStands)
                {
                    int v = Tuning.Fortuna.Deck[Roll(Tuning.Fortuna.Deck.Length)];
                    if (v == 11) hAces++;
                    house += v;
                    while (house > Tuning.Fortuna.Limit && hAces > 0) { house -= 10; hAces--; }
                    if (_deal != null) _deal.House = house;
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, (Vector2)transform.position + Vector2.up * 2f, 0.6f);
                    yield return new WaitForSeconds(Tm(0.4f));
                    if (token != Token) yield break;
                }
                won = house > Tuning.Fortuna.Limit || total > house;
            }
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;
            ClearPot();
            done(won ? Result.Win : Result.Lose);
        }

        void ClearPot() { if (_deal != null) { _deal.Pot = new int[0]; _deal.Total = 0; _deal.House = -1; } }

        // ------------------------------------------------------------------ the bluff

        IEnumerator Bluff(int token, bool fake, System.Action<Result> done)
        {
            Phase = Movement.Bluffing;
            SetMood(FortunaMood.Sway);
            int blows = Tuning.Fortuna.BluffBlows + (B >= 2 ? 1 : 0);
            var real = new bool[blows];
            bool any = false;
            for (int i = 0; i < blows; i++) { real[i] = Roll() < 0.5f; any |= real[i]; }
            if (!any) real[Roll(blows)] = true;
            int lie = -1, feint = -1;
            if (_cheating) { for (int i = 0; i < blows; i++) if (!real[i]) { lie = i; break; } if (lie < 0) lie = blows - 1; }
            if (fake) for (int i = 0; i < blows; i++) if (!real[i]) { feint = i; break; }

            for (int i = 0; i < blows; i++)
            {
                // close in on the player
                var to = Target != null ? (Vector2)Target.position + ((Vector2)transform.position - (Vector2)Target.position).normalized * Tuning.Fortuna.BluffApproach : (Vector2)transform.position;
                yield return WalkTo(token, to, 2.5f);
                if (token != Token) yield break;

                bool isReal = real[i] || i == lie, honest = real[i] && i != lie;
                if (i == lie || i == feint) StartCoroutine(PlayTell(token, i == feint));
                float wind = Tm(Tuning.Fortuna.BluffWindSeconds);
                for (float t = 0f; t < wind; t += Time.deltaTime)
                {
                    if (_figure != null) { _figure.Look.Raise = Smooth(t / wind); _figure.Look.Claws = honest ? Smooth(t / wind) : 0f; }
                    if (_caught) break;
                    yield return null;
                    if (token != Token) yield break;
                }
                if (_figure != null) { _figure.Look.Raise = 0f; _figure.Look.Claws = 0f; }
                if (_caught) { done(Result.Caught); yield break; }
                Convergence.Audio.Sfx.Play(isReal ? Convergence.Audio.Sound.Swipe : Convergence.Audio.Sound.Swing, transform.position);
                var hp = PlayerHp;
                float gap = Target != null ? ((Vector2)Target.position - (Vector2)transform.position).magnitude : 99f;
                if (isReal && hp != null && !hp.IsDead && gap < Tuning.Fortuna.BluffReach)
                {
                    var pc = Pc;
                    if (pc != null && pc.TryParry(transform.position)) { done(Result.Win); yield break; }
                    if (!hp.Immune) hp.Take(new DamageInfo(Tuning.Fortuna.BluffBlowDamage * FloorDifficulty.Damage(Floor), ElementType.Air, gameObject));
                }
                else if (!isReal && hp != null && hp.Immune && gap < Tuning.Fortuna.BluffReach * 1.5f)
                {
                    // dodged a bluff: her free hit, as the dodge ends
                    for (float t = 0f; t < 0.5f && hp != null && hp.Immune; t += Time.deltaTime) { yield return null; if (token != Token) yield break; }
                    if (hp != null && !hp.IsDead && !hp.Immune) hp.Take(new DamageInfo(Tuning.Fortuna.BluffBlowDamage * 1.5f * FloorDifficulty.Damage(Floor), ElementType.Air, gameObject));
                }
                yield return new WaitForSeconds(Tm(Tuning.Fortuna.BluffGapSeconds));
                if (token != Token) yield break;
                if (_caught) { done(Result.Caught); yield break; }
            }
            done(Result.Lose);
        }

        // ------------------------------------------------------------------ the window, the catch, the enrage

        IEnumerator Rest(int token, float seconds)
        {
            Phase = Movement.Rest;
            SetMood(FortunaMood.Low);
            _open = true;
            OpenWindow();
            if (_glow != null) _glow.Color = CatchLight;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Open = Mathf.Clamp01(t / 0.4f);
                if (WindowSpent) break;
                yield return null;
                if (token != Token) yield break;
            }
            CloseWindow();
            // shut again, but never untouchable: her strikes are read, not ignored
            _open = false;
            if (Health != null) Health.Immune = false;
            if (BodyCollider != null) BodyCollider.enabled = true;
            if (_figure != null) _figure.Look.Open = 0f;
            if (_glow != null) _glow.Color = CandleLight;
            SetMood(FortunaMood.Sway);
        }

        /// <summary>Caught: her tail bristles, a flash at her hand, the cheat's cards scatter.</summary>
        IEnumerator CaughtOut(int token)
        {
            Phase = Movement.Caught;
            SetMood(FortunaMood.Puff);
            Glow.Flash(HandAt, 2.4f, CatchLight, 0.5f, 1.2f);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Crit, transform.position);
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator Enrage(int token)
        {
            Phase = Movement.Enraging;
            SetMood(FortunaMood.Lash);
            float s = Tuning.Fortuna.FallSeconds;
            if (_figure != null) { _figure.Look.Tatter = Tuning.EnemyBands.FortunaTatter[B]; _figure.Look.Bikini = B >= Tuning.EnemyBands.FortunaBikiniFromBand; }
            Glow.Flash((Vector2)transform.position + Vector2.up * 1.2f, 3.5f, new Color(1f, 0.5f, 0.35f), 0.6f, 1.2f);
            for (float t = 0f; t < s * 2.2f; t += Time.deltaTime)
            {
                if (_figure != null) { _figure.Look.Fall = Mathf.Clamp01(t / s); _figure.Look.CapeDrop = Mathf.Clamp01((t - s * 0.5f) / (s * 1.2f)); }
                yield return null;
                if (token != Token) yield break;
            }
            _bandOff = true;
            if (_tell == FortunaTell.Slip) _tell = PickTell();
            _speed = Tuning.EnemyBands.FortunaEnragedSpeed[B];
        }

        void SetMood(int mood)
        {
            if (_figure == null) return;
            // enraged, the tail lashes between the moments that set it otherwise
            _figure.Look.Mood = Enraged && mood == FortunaMood.Sway ? FortunaMood.Lash : mood;
        }

        void LossHit(float scale)
        {
            var hp = PlayerHp;
            if (hp == null || hp.IsDead) return;
            // A MECHANIC hit: a share of the player's own max health, mitigated by Take (Boss rule 3).
            hp.Take(new DamageInfo(hp.Max * Tuning.Fortuna.LossFraction * scale, ElementType.Fire, gameObject) { IgnoresHeat = true });
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Erupt, PlayerFeet);
        }

        IEnumerator WalkTo(int token, Vector2 to, float maxSeconds = 3f)
        {
            to = Arena.Clamp(to, 1f);
            for (float t = 0f; t < maxSeconds; t += Time.deltaTime)
            {
                var at = (Vector2)transform.position;
                if ((to - at).magnitude < 0.05f) break;
                transform.position = Vector2.MoveTowards(at, to, Tuning.Fortuna.WalkSpeed * _speed * Time.deltaTime);
                Physics2D.SyncTransforms();
                yield return null;
                if (token != Token) yield break;
            }
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ------------------------------------------------------------------ her cards (chip)

        IEnumerator Throw(int token, bool spread) => spread ? Spread(token) : Orbit(token);

        /// <summary>Five cards in a fan at the player - a white flick at her hand first.</summary>
        IEnumerator Spread(int token)
        {
            if (Target == null) yield break;
            Glow.Flash(HandAt, 0.8f, Color.white, Tm(Tuning.Fortuna.ThrowTellSeconds), 0.8f);
            yield return new WaitForSeconds(Tm(Tuning.Fortuna.ThrowTellSeconds));
            if (token != Token || Target == null) yield break;
            var origin = HandAt;
            var aim = ((Vector2)Target.position - origin).normalized;
            if (aim.sqrMagnitude < 1e-4f) aim = Vector2.down;
            int n = Tuning.Fortuna.SpreadCards;
            for (int i = 0; i < n; i++)
            {
                float a = (i - (n - 1) * 0.5f) * Tuning.Fortuna.SpreadDegrees / (n - 1);
                Card(origin, Quaternion.Euler(0f, 0f, a) * aim);
            }
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Swipe, origin);
        }

        /// <summary>Six cards circle her, widening, then fly straight out.</summary>
        IEnumerator Orbit(int token)
        {
            int n = Tuning.Fortuna.OrbitCards;
            var cards = new SpriteRenderer[n];
            var frames = FortunaCards.Frames();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("fortuna.orbit");
                go.transform.SetParent(_room, false);
                cards[i] = go.AddComponent<SpriteRenderer>();
                cards[i].sprite = frames[0];
                cards[i].sortingOrder = SortingOrders.Telegraph;
            }
            float s = Tm(Tuning.Fortuna.OrbitSeconds);
            Vector2 centre = transform.position + Vector3.up * 0.7f;
            for (float t = 0f; t < s; t += Time.deltaTime)
            {
                centre = transform.position + Vector3.up * 0.7f;
                float r = Mathf.Lerp(0.6f, Tuning.Fortuna.OrbitRadius, t / s);
                for (int i = 0; i < n; i++)
                    if (cards[i] != null)
                    {
                        float a = i * Mathf.PI * 2f / n + t * 3f;
                        cards[i].transform.position = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * r;
                        cards[i].sprite = frames[Mathf.FloorToInt(t * 14f + i) & 3];
                    }
                yield return null;
                if (token != Token) { foreach (var c in cards) if (c != null) Destroy(c.gameObject); yield break; }
            }
            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null) continue;
                var at = (Vector2)cards[i].transform.position;
                Destroy(cards[i].gameObject);
                var dir = (at - centre).sqrMagnitude > 1e-4f ? (at - centre).normalized : Vector2.down;
                Card(at, dir);
            }
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Swipe, centre);
        }

        void Card(Vector2 origin, Vector2 dir)
        {
            EnemyProjectile.Launch(gameObject, Target, PlayerHp, origin, dir,
                                   Tuning.Fortuna.CardDamage * FloorDifficulty.Damage(Floor), 0f,
                                   Tuning.Fortuna.CardSpeed * _speed, Tuning.Fortuna.CardLifetime)
                .Dress(FortunaCards.Frames(), Tuning.Fortuna.CardHitRadius, ElementType.Air);
        }
    }
}
