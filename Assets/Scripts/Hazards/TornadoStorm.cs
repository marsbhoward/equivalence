using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A floor's weather: forms <see cref="Tornado"/>es on open floor, one after another, for as
    /// long as the floor's fight is on. Rolled and placed by HazardBuilder with the room (never a
    /// boss or puzzle floor); lives under the hazard root, so the next room's rebuild clears it.
    ///
    /// IT STOPS WHEN THE FIGHT STOPS. The floor clear calls <see cref="CalmAll"/> (beside the
    /// spire's Sink, in GameBootstrap.OfferFloorReward): no new funnels, and every live one starts
    /// dissipating - harmless from that frame - so a cleared room never hurts the player on the
    /// walk to the door. It also waits for the first enemy before forming anything, so the
    /// weather arrives with the wave rather than before it.
    ///
    /// CalmAll FINDS the storms rather than keeping a static list of them: it runs once a floor,
    /// and a list would be one more thing a domain reload silently empties.
    /// </summary>
    public class TornadoStorm : MonoBehaviour
    {
        /// <summary>One-shot: the next floor that may roll a storm has one. For testing:
        /// <c>Convergence.Hazards.TornadoStorm.DevForceNext = true;</c></summary>
        public static bool DevForceNext;

        // Serialized, non-readonly - the domain-reload rules.
        [SerializeField] Transform _player;
        [SerializeField] int _floor;
        [SerializeField] float _damagePerSecond;
        [SerializeField] float _nextForm;
        [SerializeField] bool _calm;
        [SerializeField] List<Tornado> _live = new();

        /// <summary>Does this floor have a storm? Spends <see cref="DevForceNext"/>.</summary>
        public static bool Rolls(int floor)
        {
            if (DevForceNext) { DevForceNext = false; return true; }
            // As often as any one pit kind - Air's hazard, matched to Fire's, Earth's and Water's.
            return floor >= Tuning.Tornado.FromFloor && Random.value < HazardBuilder.PitKindChance(floor);
        }

        public static TornadoStorm Spawn(int floor, Transform player, Transform parent)
        {
            var go = new GameObject("storm");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<TornadoStorm>();
            s._player = player;
            s._floor = floor;
            s._damagePerSecond = Tuning.Tornado.DamagePerSecond * Enemies.FloorDifficulty.Damage(floor);
            s._nextForm = Random.Range(Tuning.Tornado.FirstFormMin, Tuning.Tornado.FirstFormMax);
            Debug.Log($"[Storm] floor {floor}: up to {MaxAlive(floor)} tornado(es) at a time");
            return s;
        }

        /// <summary>The floor is cleared: every storm stops forming and lets its funnels die away.</summary>
        public static void CalmAll()
        {
            foreach (var s in Object.FindObjectsByType<TornadoStorm>()) s.Calm();
        }

        public void Calm()
        {
            _calm = true;
            Prune();
            foreach (var t in _live) t.Dissipate();
        }

        static int MaxAlive(int floor) =>
            floor >= Tuning.Tornado.ThreeFrom ? 3 : floor >= Tuning.Tornado.TwoFrom ? 2 : 1;

        void Update()
        {
            if (_calm) return;
            _nextForm -= Time.deltaTime;
            if (_nextForm > 0f) return;

            // Not before the wave has arrived; checked again shortly rather than rolling a new wait.
            Enemies.EnemyRegistry.Prune();
            if (Enemies.EnemyRegistry.All.Count == 0) { _nextForm = 0.5f; return; }

            Prune();
            int forming = 0;
            foreach (var t in _live) if (!t.Ending) forming++;
            if (forming >= MaxAlive(_floor)) { _nextForm = 0.5f; return; }

            if (TryForm())
                _nextForm = Random.Range(Tuning.Tornado.FormIntervalMin, Tuning.Tornado.FormIntervalMax);
            else
                _nextForm = 0.5f;   // nowhere open right now - the player or the pack will move
        }

        /// <summary>
        /// A random point of open floor (<see cref="Tornado.Open"/>), clear of the player and of
        /// every other funnel. The player clearance is what keeps the telegraph honest: a funnel
        /// forming on top of the player would make its harmless forming phase a countdown with
        /// no answer but to run.
        /// </summary>
        bool TryForm()
        {
            var half = Arena.HalfExtents;
            var playerPos = _player != null ? (Vector2)_player.position : Vector2.zero;
            float m = Tuning.Tornado.Radius + 0.4f;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                var p = new Vector2(Random.Range(-half.x + m, half.x - m), Random.Range(-half.y + m, half.y - m));
                if (Vector2.Distance(p, playerPos) < Tuning.Tornado.FormPlayerClearance) continue;
                if (!Tornado.Open(p)) continue;

                bool crowded = false;
                foreach (var t in _live)
                    if (Vector2.Distance(p, t.transform.position) < Tuning.Tornado.FormSpacing) { crowded = true; break; }
                if (crowded) continue;

                _live.Add(Tornado.Spawn(p, _damagePerSecond, _player, transform));
                return true;
            }
            return false;
        }

        void Prune() => (_live ??= new List<Tornado>()).RemoveAll(t => t == null);
    }
}
