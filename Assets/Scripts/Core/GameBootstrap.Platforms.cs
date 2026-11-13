using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Enemies;

namespace Convergence.Core
{
    /// <summary>
    /// PLATFORM FLOORS - a wave fought in ROUNDS, each on its own ground (the user's calls,
    /// 2026-11-13, on the floor-height mock; Tuning.Platforms, Hazards.RoomShape.Platforms).
    ///
    /// A floor whose pool is more than Tuning.Platforms.MinFills fills of the live pressure cap
    /// may split its queue into rounds: round one is the floor's ordinary first wave (unaware,
    /// trickling in as the cap frees), and when it is cleared the next round's ground opens - a
    /// BRIDGE rises over the chasm, the LIFT descends to its ledge, or the GATES' sealed stairs
    /// open - and the next round arrives there. The door appears only after the last.
    ///
    /// Only the GATES floor shows the next round ahead (the user's call): its first fill stands on
    /// the terraces from the start, held idle (EnemyController.BeginGated) - outside the live
    /// count, so it never holds back round one's trickle - and a body that lobs (the Mortar)
    /// fires down from up there meanwhile.
    ///
    /// The floor's plan is the seed's, so a resumed floor is rebuilt with the same platform and the
    /// same split; nothing here needs saving (a floor is resumed from its start).
    /// </summary>
    public partial class GameBootstrap
    {
        public enum PlatformKind { None, Gates, Bridge, Lift }

        /// <summary>DEV, one-shot: the next fought floor is this platform floor, whatever its pool.</summary>
        public static PlatformKind? DevForcePlatform;

        PlatformKind _platform;
        int _round;
        // Not readonly: a domain reload brings them back null.
        List<Queue<PendingSpawn>> _laterRounds = new();
        List<EnemyController> _gated = new();

        // Where this round's bodies arrive: within a box, outside one, between two tiers.
        Rect? _regionIn, _regionOut;
        int _regionMinTier = int.MinValue, _regionMaxTier = int.MaxValue;

        /// <summary>The platform this floor is fought on (None on an ordinary floor).</summary>
        public PlatformKind Platform => _platform;

        bool HasLaterRound => _laterRounds != null && _laterRounds.Count > 0;

        void ResetRounds()
        {
            _platform = PlatformKind.None;
            _round = 0;
            _laterRounds ??= new List<Queue<PendingSpawn>>();
            _laterRounds.Clear();
            _gated ??= new List<EnemyController>();
            _gated.Clear();
            ClearRegion();
        }

        /// <summary>The platform for <paramref name="floor"/> and its <paramref name="wave"/>, from
        /// the floor's seed: None unless it is an ordinary combat floor whose pool needs rounds.</summary>
        PlatformKind PlatformFor(int floor, Wave wave)
        {
            if (wave == null || IsBossFloor(floor)) return PlatformKind.None;
            if (DevForcePlatform is PlatformKind forced) { DevForcePlatform = null; return forced; }
            if (_plan.Category != Rifts.FloorCategory.Combat || _plan.Rift != Rifts.RiftKind.None) return PlatformKind.None;
            if (RunFormat.Depth(floor) < Tuning.Rooms.FromFloor) return PlatformKind.None;
            float total = 0f;
            foreach (var s in wave.Spawns) total += WaveComposer.Pressure(s.Kind, s.Elite, s.Affix);
            if (total < WaveComposer.LivePressureCap(floor) * Tuning.Platforms.MinFills) return PlatformKind.None;
            var rng = new System.Random(unchecked(PlatformSeed(floor) * 37 + 11));
            if (rng.NextDouble() >= Tuning.Platforms.Chance) return PlatformKind.None;
            return (PlatformKind)(1 + rng.Next(3));
        }

        /// <summary>The floor is torn down: the held bodies go with it, and the rounds to come.</summary>
        void DropRounds()
        {
            if (_gated != null) foreach (var e in _gated) if (e) Destroy(e.gameObject);
            ResetRounds();
        }

        int PlatformSeed(int floor) => _planner != null ? _planner.ShapeSeed(floor) : floor;

        /// <summary>The room a platform floor is built in (null: the floor's own shape).</summary>
        Hazards.RoomShape PlatformShape(int floor) => _platform switch
        {
            PlatformKind.Gates => Hazards.RoomShape.Gates(new System.Random(unchecked(PlatformSeed(floor) * 13 + 3))),
            PlatformKind.Bridge => Hazards.RoomShape.Bridge(),
            PlatformKind.Lift => Hazards.RoomShape.Lift(),
            _ => null,
        };

        /// <summary>The queued wave, split into rounds by pressure in queue order: round one stays
        /// in _spawnQueue, the rest wait in _laterRounds.</summary>
        void SplitRounds()
        {
            if (_platform == PlatformKind.None) return;
            var all = _spawnQueue.ToArray();
            _spawnQueue.Clear();
            float total = 0f;
            foreach (var s in all) total += WaveComposer.Pressure(s.Kind, s.Elite, s.Affix);
            int rounds = Mathf.Max(1, Tuning.Platforms.Rounds);
            var queues = new List<Queue<PendingSpawn>>();
            for (int i = 0; i < rounds; i++) queues.Add(new Queue<PendingSpawn>());
            float acc = 0f;
            foreach (var s in all)
            {
                int r = total > 0f ? Mathf.Min(rounds - 1, (int)(acc / total * rounds)) : 0;
                queues[r].Enqueue(s);
                acc += WaveComposer.Pressure(s.Kind, s.Elite, s.Affix);
            }
            foreach (var s in queues[0]) _spawnQueue.Enqueue(s);
            for (int i = 1; i < rounds; i++) if (queues[i].Count > 0) _laterRounds.Add(queues[i]);
            SetRegionForRound(0);
            Debug.Log($"[Floor] platform {_platform}: {rounds} rounds ({queues[0].Count} then {(queues.Count > 1 ? queues[1].Count : 0)} bodies)");
        }

        // ------------------------------------------------------------------ where a round arrives

        void ClearRegion()
        {
            _regionIn = _regionOut = null;
            _regionMinTier = int.MinValue;
            _regionMaxTier = int.MaxValue;
        }

        void SetRegionForRound(int round)
        {
            ClearRegion();
            switch (_platform)
            {
                case PlatformKind.Gates:
                    if (round == 0) _regionMaxTier = 0; else _regionMinTier = 1;
                    break;
                case PlatformKind.Bridge:
                    _regionIn = round == 0 ? Hazards.RoomShape.SouthPlatform : Hazards.RoomShape.NorthPlatform;
                    break;
                case PlatformKind.Lift:
                    if (round == 0) _regionIn = Hazards.RoomShape.LiftRect;
                    else _regionOut = Hazards.RoomShape.LiftRect;
                    break;
            }
        }

        bool HasRegion => _regionIn != null || _regionOut != null || _regionMinTier != int.MinValue || _regionMaxTier != int.MaxValue;

        bool InRegion(Vector2 p)
        {
            if (_regionIn is Rect a && !a.Contains(p)) return false;
            if (_regionOut is Rect b && b.Contains(p)) return false;
            if (_regionMinTier != int.MinValue || _regionMaxTier != int.MaxValue)
            {
                int t = Arena.TierAt(p);
                if (t < _regionMinTier || t > _regionMaxTier) return false;
            }
            return true;
        }

        /// <summary>A standing point in this round's ground, at least <paramref name="minDistance"/>
        /// from the player when it can be - the farthest found when it cannot.</summary>
        Vector2 RegionPoint(float minDistance)
        {
            var inner = Arena.Inner;
            var box = _regionIn ?? new Rect(-inner.x, -inner.y, inner.x * 2f, inner.y * 2f);
            Vector2 at = _player != null ? (Vector2)_player.transform.position : Arena.SouthSpawnPoint;
            Vector2 best = box.center;
            float bestD = -1f;
            for (int i = 0; i < 48; i++)
            {
                var p = new Vector2(Random.Range(box.xMin + 0.8f, box.xMax - 0.8f), Random.Range(box.yMin + 0.8f, box.yMax - 0.8f));
                if (!Arena.OnFloor(p, 0.6f) || !InRegion(p) || Arena.NearTierEdge(p, 0.5f)) continue;
                float d = Vector2.Distance(p, at);
                if (d >= minDistance) return p;
                if (d > bestD) { bestD = d; best = p; }
            }
            return bestD >= 0f ? best : Arena.NearestFloor(box.center, 0.6f);
        }

        // ------------------------------------------------------------------ the gated preview

        /// <summary>The GATES floor's next round, in view from the start: its first fill stands on
        /// the terraces, held idle - but a lobber, which fires from up there.</summary>
        IEnumerator PreviewGatedRound(int token)
        {
            if (_platform != PlatformKind.Gates || !HasLaterRound) yield break;
            SetRegionForRound(1);
            var q = _laterRounds[0];
            float pressure = 0f;
            int bodies = 0;
            while (q.Count > 0)
            {
                if (token != _runToken || _player == null) break;
                var next = q.Peek();
                if (!WaveComposer.Fits(new WaveSpawn(next.Kind, next.Elite, affix: next.Affix), pressure, bodies, _floor)) break;
                q.Dequeue();
                var e = EnemyFactory.Spawn(next.Position(), next.Kind, _floor, _player.transform, _enemyRoot,
                                           elite: next.Elite, affix: next.Affix);
                e.RollIndex = _floorSpawns++;
                Harden(e);
                HookDeath(e);
                if (!Lobs(next.Kind)) e.BeginGated();
                _gated.Add(e);
                pressure += WaveComposer.Pressure(next.Kind, next.Elite, next.Affix);
                bodies++;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            SetRegionForRound(0);
        }

        /// <summary>A kind that LOBS its shots - free to fire while gated (the user's call).</summary>
        static bool Lobs(EnemyKind kind) => kind == EnemyKind.Mortar;

        // ------------------------------------------------------------------ the next round

        /// <summary>Round cleared with another to come: open its ground and bring it in. False
        /// when there is none (the floor is cleared as usual).</summary>
        bool TryNextRound()
        {
            if (_platform == PlatformKind.None || !HasLaterRound || _spawning) return false;
            _spawning = true;
            StartCoroutine(NextRound(_runToken));
            return true;
        }

        IEnumerator NextRound(int token)
        {
            _round++;
            var q = _laterRounds[0];
            _laterRounds.RemoveAt(0);
            switch (_platform)
            {
                case PlatformKind.Gates: yield return OpenGates(); break;
                case PlatformKind.Bridge: yield return RaiseBridge(); break;
                case PlatformKind.Lift: yield return LowerLift(); break;
            }
            if (token != _runToken || _player == null) { _spawning = false; yield break; }

            SetRegionForRound(_round);
            foreach (var s in q) _spawnQueue.Enqueue(s);
            // The held bodies are the round's first fill: they join the fight as it opens.
            foreach (var e in _gated)
            {
                if (e == null || e.GetComponent<Combat.Health>().IsDead) continue;
                _alive.Add(e);
                e.ReleaseGate();
            }
            _gated.Clear();

            // A round arriving on fresh ground comes in as a floor's first wave does: unaware, and
            // nothing begun for the entry grace.
            bool fresh = _platform != PlatformKind.Gates;
            if (fresh) EnemyController.EntryGraceUntil = Mathf.Max(EnemyController.EntryGraceUntil, Time.time + Tuning.Enemy.EntryGraceSeconds);
            while (NextSpawnFits())
            {
                if (token != _runToken || _player == null) { _spawning = false; yield break; }
                var e = SpawnQueuedEnemy();
                if (fresh && e != null && Tuning.Idle.Enabled && !Exchange.LedgerWorld.NoUnaware) e.BeginUnaware();
                yield return new WaitForSecondsRealtime(0.12f);
            }
            _spawning = false;
        }

        IEnumerator OpenGates()
        {
            Audio.Sfx.Play(Audio.Sound.DoorOpen);
            Combat.CameraKick.Kick(Vector2.down, Tuning.Platforms.LiftShake);
            Hazards.RoomWalls.OpenGates();
            yield return new WaitForSeconds(Tuning.Platforms.GateOpenSeconds);
        }

        IEnumerator RaiseBridge()
        {
            // From the arrival's side out: the south row of the gap first.
            for (int row = Hazards.RoomShape.BridgeGapRows - 1; row >= 0; row--)
            {
                Hazards.RoomWalls.RaiseBridgeRow(row);
                Audio.Sfx.Play(Audio.Sound.Grind);
                Combat.CameraKick.Kick(Vector2.up, Tuning.Platforms.LiftShake);
                yield return new WaitForSeconds(Tuning.Platforms.BridgeRowSeconds);
            }
            yield return new WaitForSeconds(Tuning.Platforms.BridgeSettleSeconds);
        }

        IEnumerator LowerLift()
        {
            Audio.Sfx.Play(Audio.Sound.Grind);
            float t = 0f, kick = 0f;
            int beat = 0;
            while (t < Tuning.Platforms.LiftSeconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Tuning.Platforms.LiftSeconds);
                Art.Biomes.Parallax.Rise = u * u * (3f - 2f * u) * Tuning.Platforms.LiftRise;
                kick -= Time.deltaTime;
                if (kick <= 0f)
                {
                    kick = 0.09f;
                    Combat.CameraKick.Kick((beat++ & 1) == 0 ? Vector2.up : Vector2.down, Tuning.Platforms.LiftShake);
                }
                yield return null;
            }
            Audio.Sfx.Play(Audio.Sound.Slam);
            if (_fade != null) yield return _fade.FadeOut(Tuning.Platforms.LiftDipSeconds);
            Hazards.RoomWalls.DockLift();
            yield return null;
            if (_fade != null) yield return _fade.FadeIn(Tuning.Platforms.LiftDipSeconds);
        }
    }
}
