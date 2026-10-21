using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>
    /// The conditional half of the catalogue: everything that fires on an EVENT, needs a clock or
    /// a counter, or depends on the target - where <see cref="Mods"/> covers what is just a number.
    ///
    /// One component holding all of it beats a hundred small ones: the state is inspectable in one
    /// place, and its public hooks are the complete list of moments a run can react to. Split by
    /// theme across partial files - Offense (swings, hits, kills), Defense (hits taken, heals, the
    /// lethal hit), Element (the meter and the release, the element and trap entries) and Field
    /// (what the run puts on the floor: Retrograde, Projection, Ley Lines, Antipathy, corpses).
    ///
    /// Every behaviour reads its own stack count, so an entry the run does not hold costs one
    /// dictionary lookup. Damage and speed bonuses are run-layer POINTS bent with the ledger's own
    /// (Mods.Factor) - a conditional boon shares the Vessel's ceiling, it does not step past it.
    /// Plain value fields only, so a domain reload loses nothing it can keep.
    /// </summary>
    public partial class RunEffects : MonoBehaviour
    {
        RunModifiers _mods;
        [SerializeField] Player.PlayerController _pc;

        static readonly Mods NoMods = new();

        public void Bind(RunModifiers mods, Player.PlayerController player)
        {
            _mods = mods;
            _pc = player;
        }

        int N(string id) => _mods == null ? 0 : _mods.StacksOf(id);
        bool Has(string id) => N(id) > 0;

        /// <summary>A stackable entry at max stacks - its Rubedo or Nigredo is live.</summary>
        bool AtMax(string id) => _mods != null && _mods.AtMax(id);

        Mods M => _mods != null ? _mods.Current : NoMods;
        Health Hp => _pc != null ? _pc.Health : null;
        Player.ElementalResource Resource => _pc != null ? _pc.Resource : null;
        ElementType Element => Resource != null ? Resource.Element : ElementType.Fire;

        // ---------------------------------------------------------------- the floor's state

        float _floorSeconds;
        bool _secondWindSpent;
        int _openStanceHits;
        /// <summary>Releases this run, for Wellspring's every-Nth.</summary>
        int _wellspringCount;

        /// <summary>Seconds on this floor, for Souring, Maturation and Acetum.</summary>
        public float FloorSeconds => _floorSeconds;

        // ---------------------------------------------------------------- the body's state

        float _stillSeconds;
        float _movingSeconds;
        bool _poiseArmed;

        /// <summary>Seconds the player has stood still - Lapis, Restless, Quicksand, Mountain.</summary>
        public float StillSeconds => _stillSeconds;

        public void Tick(float dt, bool moving, float meter01)
        {
            _floorSeconds += dt;
            // POISE (Equipoise's Rubedo): a change of pace, after a second held in the old one,
            // arms a halved hit - starting after standing, or stopping after moving.
            if (moving ? _stillSeconds >= T.PoiseAfterSeconds : _movingSeconds >= T.PoiseAfterSeconds)
                _poiseArmed = true;
            _stillSeconds = moving ? 0f : _stillSeconds + dt;
            _movingSeconds = moving ? _movingSeconds + dt : 0f;

            TickOffense(dt);
            TickDefense(dt, moving);
            TickElement(dt, meter01);
            TickField(dt);
        }

        static void Countdown(ref float t, float dt) { if (t > 0f) t = Mathf.Max(0f, t - dt); }

        // ---------------------------------------------------------------- floors

        /// <summary>A floor's fight is about to begin: every per-floor memory starts again, and
        /// the meter is primed (Attunement).</summary>
        public void OnFloorStarted()
        {
            _floorSeconds = 0f;
            _secondWindSpent = false;
            _openStanceHits = 0;
            _wardReady = Has("aegis_cycle");
            _wardTimer = 0f;
            _negationLock = 0f;
            _retroTimer = RetrogradeEvery;
            _projectionTimer = ProjectionEvery * 0.5f;
            _leyTimer = T.LeyLinesEvery * 0.5f;
            _antipathyTimer = T.AntipathyEvery;
            _hitThisFloor = false;   // Unbloodied
            _soakedDeaths = 0;       // Swell grows a floor at a time
            if (AtMax("stubborn_ore")) Resource?.Spill(1f);   // Barren: the meter empties as a floor starts

            float start = M.StartMeterFraction;
            if (start > 0f && Resource != null && Resource.Fill01 < start)
                Resource.Refund(start - Resource.Fill01);
        }

        /// <summary>The floor cleared: the prices it charges and the gifts it gives. Counters on
        /// the ledger itself (Withering, Viriditas, Senescence) move in RunModifiers.FloorCleared.</summary>
        public void OnFloorCleared()
        {
            var hp = Hp;
            int toll = N("toll");
            if (toll > 0 && hp != null && !hp.IsDead)
            {
                // Usury takes the toll from MAX health: the same bite whatever is left.
                float of = AtMax("toll") ? hp.Max : hp.Current;
                Pay(of * Mathf.Min(0.9f, T.TollFraction * toll));
            }

            // UNBLOODIED (an oath): a floor cleared without a hit fulfils it.
            if (Has("unbloodied") && !_hitThisFloor) _mods.Fulfil("unbloodied");

            Hazards.ProjectionLines.CalmAll();
        }

        // ---------------------------------------------------------------- continuation

        /// <summary>The run-long counters - the every-Nth entries' - for a run snapshot. Everything
        /// else here is a floor's memory or a short clock, which a resumed floor starts afresh.</summary>
        public void CaptureCounters(Chain.RunSnapshot s)
        {
            s.HitCount = _hitCount;
            s.BurningDeaths = _burningDeaths;
            s.Arts = _arts;
            s.VapourCount = _vapourCount;
            s.WellspringCount = _wellspringCount;
        }

        public void RestoreCounters(Chain.RunSnapshot s)
        {
            _hitCount = s.HitCount;
            _burningDeaths = s.BurningDeaths;
            _arts = s.Arts;
            _vapourCount = s.VapourCount;
            _wellspringCount = s.WellspringCount;
        }

        // ---------------------------------------------------------------- shared

        /// <summary>A price the run charges in health: straight off, through no mitigation - it is
        /// paid, not dealt - but still past Second Wind, the one thing that must be able to catch it.</summary>
        void Pay(float amount)
        {
            if (_pc == null || amount <= 0f) return;
            _pc.PayHealth(amount);
        }
    }
}
