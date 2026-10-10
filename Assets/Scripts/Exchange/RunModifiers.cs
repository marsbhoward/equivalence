using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>
    /// The ledger: which boons and costs this run is carrying, what they add up to, and what the
    /// deals owe it (the refusals spent, the pity and mercy clocks, the combinations it has opened).
    ///
    /// Run-scoped and never persisted. These are not progression - they are what you agreed to on
    /// the way down, and they die with the run. Nothing here goes near a profile or a checkpoint.
    ///
    /// <see cref="Mods"/> is recomputed only when the ledger changes - at a deal, a transmutation
    /// or a spire - and read every frame afterwards.
    /// </summary>
    public class RunModifiers
    {
        readonly Dictionary<string, int> _stacks = new();
        readonly List<ExchangeEntry> _order = new();

        public Mods Current { get; private set; } = new();

        // ---------------------------------------------------------------- the character

        /// <summary>
        /// The played element's mastery level, which sizes the VESSEL - the run layer's own
        /// thresholds (StatCurves.Run). Full by default, so anything that builds a ledger without
        /// a character behind it (a balance simulation) gets the levelled player's Vessel; the run
        /// sets the real one when the player is built.
        /// </summary>
        public int MasteryLevel { get; private set; } = Tuning.Mastery.LevelCap;

        /// <summary>The element the run is played as - element entries are offered only to it.
        /// Null (a simulation with no element) offers none of them.</summary>
        public ElementType? Element { get; private set; }

        public WeaponClass Weapon { get; private set; } = WeaponClass.Greatsword;

        /// <summary>The board's own change to the chain (Multiplication), so an entry that would
        /// change nothing - Short Chain on a one-basic chain - is never offered.</summary>
        public int BoardChainDelta { get; private set; }

        public void SetMasteryLevel(int level)
        {
            if (level == MasteryLevel) return;
            MasteryLevel = level;
            Recompute();
        }

        /// <summary>Who the run is: element, weapon class, the Vessel's size, the board's chain.</summary>
        public void SetCharacter(ElementType? element, WeaponClass weapon, int masteryLevel, int boardChainDelta = 0)
        {
            Element = element;
            Weapon = weapon;
            BoardChainDelta = boardChainDelta;
            MasteryLevel = masteryLevel;
            Recompute();
        }

        // ---------------------------------------------------------------- the deals

        /// <summary>What the next deal owes, from entries taken since the last one was built.
        /// Read and cleared by the deal builder.</summary>
        public PendingOffer Pending { get; private set; } = new();

        /// <summary>Deals built so far this run - the clock the pity and mercy windows count in.</summary>
        public int DealIndex { get; private set; }

        public int RefusalsUsed { get; private set; }
        public int RefusalsLeft => Mathf.Max(0, T.RefusalsPerRun - RefusalsUsed);

        /// <summary>A deal was turned down with the refuse slate.</summary>
        public void Refuse() => RefusalsUsed++;

        readonly Dictionary<string, int> _lastOffered = new();
        readonly Dictionary<string, int> _restUntil = new();

        /// <summary>Combinations formed and not yet put on a guaranteed slate, oldest first.</summary>
        readonly List<ExchangeEntry> _newCombos = new();
        readonly HashSet<string> _opened = new();
        readonly HashSet<string> _transmuted = new();

        /// <summary>The deal builder opens a deal: the clock ticks.</summary>
        public void BeginDeal() => DealIndex++;

        /// <summary>An entry was shown on a deal - its pity or mercy clock starts again.</summary>
        public void MarkOffered(ExchangeEntry e)
        {
            if (e != null) _lastOffered[e.Id] = DealIndex;
        }

        /// <summary>Deals since this entry was last on a deal (or taken).</summary>
        public int DealsSince(ExchangeEntry e)
            => e != null && _lastOffered.TryGetValue(e.Id, out var at) ? DealIndex - at : int.MaxValue;

        public bool Resting(ExchangeEntry e)
            => e != null && _restUntil.TryGetValue(e.Id, out var until) && DealIndex < until;

        /// <summary>The next combination owed a guaranteed slate, or null - taken off the queue.</summary>
        public ExchangeEntry TakeNewCombination()
        {
            while (_newCombos.Count > 0)
            {
                var c = _newCombos[0];
                _newCombos.RemoveAt(0);
                if (Offerable(c)) return c;
            }
            return null;
        }

        public bool HasNewCombination => _newCombos.Count > 0;

        // ---------------------------------------------------------------- holding

        /// <summary>Entries held, in the order they were taken. Drives the ledger strip.</summary>
        public IReadOnlyList<ExchangeEntry> Held => _order;

        public int StacksOf(string id) => _stacks.TryGetValue(id, out var n) ? n : 0;
        public int StacksOf(ExchangeEntry e) => e == null ? 0 : StacksOf(e.Id);

        /// <summary>True when the run already holds as many of this as it may.</summary>
        public bool AtCap(ExchangeEntry e) => e != null && StacksOf(e.Id) >= e.MaxStacks;

        /// <summary>True when this stackable entry is at max stacks - its Rubedo or Nigredo is live.</summary>
        public bool AtMax(string id)
        {
            var e = ExchangeCatalog.Get(id);
            return e != null && e.Stackable && StacksOf(id) >= e.MaxStacks;
        }

        public bool Transmuted(string id) => _transmuted.Contains(id);

        /// <summary>Costs transmuted this run (each once - a transmuted cost never returns).</summary>
        public int TransmutedCount => _transmuted.Count;

        /// <summary>Whether a circle may still transmute this run (Tuning.Exchange.TransmutationsPerRun).</summary>
        public bool CanTransmute => _transmuted.Count < T.TransmutationsPerRun;

        public void Take(ExchangeEntry entry)
        {
            if (entry == null || AtCap(entry)) return;

            if (!_stacks.ContainsKey(entry.Id))
            {
                _stacks[entry.Id] = 0;
                _order.Add(entry);
            }
            _stacks[entry.Id]++;
            _lastOffered[entry.Id] = DealIndex;
            if (entry.Recurring) _restUntil[entry.Id] = DealIndex + T.RecurringRestDeals + 1;
            entry.OnTaken?.Invoke(Pending);
            OpenCombinations();
            Recompute();
        }

        // ---------------------------------------------------------------- Nigredo -> Albedo

        /// <summary>Costs at max stacks with an Albedo waiting - what a transmutation circle can
        /// take. In the order they reached their Nigredo... the order they were first taken.</summary>
        public List<ExchangeEntry> Nigredos()
        {
            var list = new List<ExchangeEntry>();
            foreach (var e in _order)
                if (e.Kind == ExchangeKind.Cost && e.Stackable && e.AlbedoId != null && AtCap(e))
                    list.Add(e);
            return list;
        }

        public bool HoldsNigredo
        {
            get
            {
                foreach (var e in _order)
                    if (e.Kind == ExchangeKind.Cost && e.Stackable && e.AlbedoId != null && AtCap(e)) return true;
                return false;
            }
        }

        /// <summary>
        /// THE CIRCLE: a held Nigredo leaves the ledger - the cost, every stack, and its twist -
        /// and its Albedo joins it. A transmuted cost never returns this run. Returns the Albedo,
        /// or null when there was nothing eligible to transmute.
        /// </summary>
        public ExchangeEntry Transmute(ExchangeEntry cost)
        {
            if (cost == null || cost.AlbedoId == null || !AtCap(cost) || !CanTransmute) return null;
            var albedo = ExchangeCatalog.Get(cost.AlbedoId);
            if (albedo == null) return null;

            _stacks.Remove(cost.Id);
            _order.Remove(cost);
            _transmuted.Add(cost.Id);
            if (cost.Id == "withering") Withered = 0f;

            _stacks[albedo.Id] = 1;
            _order.Add(albedo);
            OpenCombinations();
            Recompute();
            return albedo;
        }

        // ---------------------------------------------------------------- combinations

        /// <summary>A combination is OPEN once both its parts are held: it joins the pool and is
        /// owed the next guaranteed slate. Opens once a run.</summary>
        void OpenCombinations()
        {
            foreach (var c in ExchangeCatalog.Combinations)
            {
                if (_opened.Contains(c.Id) || c.Parts == null) continue;
                if (StacksOf(c.Parts[0]) <= 0 || StacksOf(c.Parts[1]) <= 0) continue;
                _opened.Add(c.Id);
                _newCombos.Add(c);
            }
        }

        public bool Opened(ExchangeEntry e) => e != null && _opened.Contains(e.Id);

        // ---------------------------------------------------------------- the pool

        /// <summary>
        /// Whether this entry may be drawn onto a deal now: in the pool (a base entry, or an open
        /// combination), not at its cap, not resting, never transmuted, fit for the element and
        /// the weapon, and not a stack that would change nothing.
        /// </summary>
        public bool Offerable(ExchangeEntry e)
        {
            if (e == null || AtCap(e) || Resting(e) || _transmuted.Contains(e.Id)) return false;
            switch (e.Origin)
            {
                case EntryOrigin.Albedo: return false;            // only a circle makes one
                case EntryOrigin.Base: break;
                default:
                    if (!_opened.Contains(e.Id)) return false;
                    // A combination needs its parts still held: a Citrinitas feeds on its cost.
                    if (StacksOf(e.Parts[0]) <= 0 || StacksOf(e.Parts[1]) <= 0) return false;
                    break;
            }
            if (e.Element != null && e.Element != Element) return false;
            if (!e.FitsClass(Weapon)) return false;
            if (DevExcluded != null && DevExcluded.Contains(e.Id)) return false;
            return !Useless(e);
        }

        /// <summary>Entries kept out of every deal - a testing switch, null in play. The balance model
        /// prices an entry by simulating runs that can never be offered it.</summary>
        public static HashSet<string> DevExcluded;

        /// <summary>A stack that would change nothing for this character is never offered.</summary>
        bool Useless(ExchangeEntry e)
        {
            switch (e.Id)
            {
                case "short_chain":
                    // The chain is floored at one basic; one already there gains nothing.
                    return 2 + BoardChainDelta + StacksOf("long_chain") <= 1;
                default:
                    return false;
            }
        }

        /// <summary>Everything of this kind and weight that may be drawn now.</summary>
        public List<ExchangeEntry> Offerable(ExchangeKind kind, int weight)
        {
            var list = new List<ExchangeEntry>();
            foreach (var e in ExchangeCatalog.All)
                if (e.Kind == kind && e.Weight == weight && Offerable(e)) list.Add(e);
            return list;
        }

        // ---------------------------------------------------------------- the spire

        /// <summary>
        /// A boon that lasts ONE FLOOR (a captured Spire), folded into <see cref="Current"/> on
        /// top of the ledger so every system that reads the ledger reads it too. Not an entry: it
        /// never shows on the ledger strip and is dropped by a later floor rather than held.
        /// Pass null to drop it.
        /// </summary>
        public void SetFloorBoon(System.Action<Mods> apply)
        {
            if (_floorBoon == null && apply == null) return;
            _floorBoon = apply;
            Recompute();
        }

        public bool HasFloorBoon => _floorBoon != null;

        System.Action<Mods> _floorBoon;

        // ---------------------------------------------------------------- the floor's own counters

        /// <summary>Max health points Withering has taken so far this run - while it is held. A
        /// transmuted Withering gives it all back: the cost leaves the ledger, and its loss with it.</summary>
        public float Withered { get; private set; }

        /// <summary>Max health points Viriditas has grown since it was made.</summary>
        public float Virid { get; private set; }

        /// <summary>Floors cleared while Senescence is held - each one makes enemies hit harder.</summary>
        public int Senescence { get; private set; }

        /// <summary>
        /// A floor was cleared: the counters some entries keep move on. Called by the run once per
        /// cleared floor, before the deal (Withering takes its cut first).
        /// </summary>
        public void FloorCleared()
        {
            int wither = StacksOf("withering");
            if (wither > 0)
            {
                float cap = AtMax("withering") ? float.PositiveInfinity : T.WitheringCap * 100f;   // Desiccation
                Withered = Mathf.Min(cap, Withered + T.WitheringPerFloor * 100f * wither);
            }
            else Withered = 0f;

            if (StacksOf("viriditas") > 0)
                Virid = Mathf.Min(T.ViriditasCap * 100f, Virid + T.ViriditasPerFloor * 100f);

            if (StacksOf("senescence") > 0) Senescence++;
            Recompute();
        }

        // ---------------------------------------------------------------- run lifecycle

        public void Clear()
        {
            _stacks.Clear();
            _order.Clear();
            Pending = new PendingOffer();
            _floorBoon = null;
            DealIndex = 0;
            RefusalsUsed = 0;
            _lastOffered.Clear();
            _restUntil.Clear();
            _newCombos.Clear();
            _opened.Clear();
            _transmuted.Clear();
            Withered = 0f;
            Virid = 0f;
            Senescence = 0;
            Recompute();
        }

        /// <summary>
        /// A full copy, clocks and all - what a deal would look like after a choice, worked out
        /// without touching the run (Oracle's preview of the next deal).
        /// </summary>
        public RunModifiers Clone()
        {
            var c = new RunModifiers
            {
                MasteryLevel = MasteryLevel,
                Element = Element,
                Weapon = Weapon,
                BoardChainDelta = BoardChainDelta,
                Pending = Pending.Copy(),
                DealIndex = DealIndex,
                RefusalsUsed = RefusalsUsed,
                _floorBoon = _floorBoon,
                Withered = Withered,
                Virid = Virid,
                Senescence = Senescence,
            };
            foreach (var kv in _stacks) c._stacks[kv.Key] = kv.Value;
            c._order.AddRange(_order);
            foreach (var kv in _lastOffered) c._lastOffered[kv.Key] = kv.Value;
            foreach (var kv in _restUntil) c._restUntil[kv.Key] = kv.Value;
            c._newCombos.AddRange(_newCombos);
            foreach (var id in _opened) c._opened.Add(id);
            foreach (var id in _transmuted) c._transmuted.Add(id);
            c.Recompute();
            return c;
        }

        /// <summary>
        /// A copy with one entry gone, every stack of it - clocks and counters kept. What the balance
        /// model asks to see what one entry was worth in a finished run (Assay.PowerMap); the game
        /// never removes an entry this way (a transmutation is <see cref="Transmute"/>).
        /// </summary>
        public RunModifiers Without(ExchangeEntry e)
        {
            var c = Clone();
            if (e == null || !c._stacks.ContainsKey(e.Id)) return c;
            c._stacks.Remove(e.Id);
            c._order.Remove(e);
            c.Recompute();
            return c;
        }

        // ---------------------------------------------------------------- what it adds up to

        void Recompute() => Current = Compute(null, null);

        /// <summary>
        /// What <see cref="Current"/> would be with one more stack of each of these - the honest
        /// way to price a pair before taking it, through the Vessel like any other stack.
        /// </summary>
        public Mods Preview(ExchangeEntry boon, ExchangeEntry cost) => Compute(boon, cost);

        Mods Compute(ExchangeEntry plusA, ExchangeEntry plusB)
        {
            var m = new Mods { Level = MasteryLevel };
            foreach (var e in _order)
            {
                if (e.Apply == null) continue;
                int n = _stacks[e.Id] + (e == plusA ? 1 : 0) + (e == plusB ? 1 : 0);
                if (n <= 0) continue;
                e.Apply(m, Mathf.Min(n, e.MaxStacks));
            }
            // A previewed entry not held yet.
            if (plusA != null && plusA.Apply != null && !_stacks.ContainsKey(plusA.Id))
                plusA.Apply(m, plusA == plusB ? Mathf.Min(2, plusA.MaxStacks) : 1);
            if (plusB != null && plusB != plusA && plusB.Apply != null && !_stacks.ContainsKey(plusB.Id))
                plusB.Apply(m, 1);

            // The counters: max health Withering has taken, and Viriditas has grown.
            if (Withered > 0f && StacksOf("withering") > 0) m.Add(StatKind.MaxHp, -Withered);
            if (Virid > 0f && StacksOf("viriditas") > 0) m.Add(StatKind.MaxHp, Virid);

            if (StacksOf("senescence") > 0) m.SenescenceFloors = Senescence;

            // Folded in before the Vessel, the same as any ledger entry.
            _floorBoon?.Invoke(m);

            Vessel(m);
            return m;
        }

        /// <summary>
        /// THE VESSEL: each stat's boon points bent through the run layer's own thresholds, sized by
        /// mastery level (StatCurves.Run), the cost points added straight on, then written out as
        /// the effective values the game reads. Floors, not cliffs: enough stacked costs can drive
        /// any of these toward nothing, and a run that ends because a multiplier went below zero
        /// reads as a bug rather than as the debt catching up.
        /// </summary>
        static void Vessel(Mods m)
        {
            float hit = Tuning.Player.BaseDamage;
            m.BonusDamage = hit * Mathf.Max(-0.75f, m.Points(StatKind.Damage) / 100f);
            m.AttackSpeedMul = Mathf.Max(0.35f, 1f + m.Points(StatKind.AttackSpeed) / 100f);
            m.MoveSpeedMul = Mathf.Max(0.35f, 1f + m.Points(StatKind.MoveSpeed) / 100f);
            m.FinisherDamageMul = Mathf.Max(0.25f, 1f + m.Points(StatKind.FinisherPower) / 100f);
            m.CritDamageAdd = m.Points(StatKind.CritDamage) / 100f;
            m.RangeMul = Mathf.Max(0.3f, 1f + m.Points(StatKind.Range) / 100f);
            m.AreaMul = Mathf.Max(0.3f, 1f + m.Points(StatKind.AoeRadius) / 100f);
            m.ReleaseMul = Mathf.Max(0.1f, 1f + m.Points(StatKind.ElementalEffectiveness) / 100f);
            m.GainMul = Mathf.Max(0.1f, 1f + m.Points(StatKind.ElementGrowth) / 100f);
            m.MaxHpMul = Mathf.Max(0.1f, 1f + m.Points(StatKind.MaxHp) / 100f);
            m.GrazeFactor = StatPercents.ReductionFactor(m.Points(StatKind.Graze));
            m.BraceFactor = StatPercents.ReductionFactor(m.Points(StatKind.Brace));
            m.ResilienceFactor = StatPercents.ReductionFactor(m.Points(StatKind.Resilience));
            m.CleavePoints = m.Points(StatKind.Cleave);
            m.PierceAdd = Mathf.Max(0f, m.Points(StatKind.Pierce) / 100f);

            m.DamageTakenMul = Mathf.Max(0.25f, m.DamageTakenMul);
            m.StrikeWidthMul = Mathf.Max(0.30f, m.StrikeWidthMul);
            m.HealMul = Mathf.Max(0.1f, m.HealMul);
        }
    }
}
