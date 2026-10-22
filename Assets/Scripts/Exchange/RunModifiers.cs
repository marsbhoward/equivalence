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
            CheckUnions();
            Recompute();
        }

        // ---------------------------------------------------------------- forms (Phase 2)

        /// <summary>Forms and socket costs inscribed, in the order taken: the entry and its socket
        /// (a rotation index for an Art slot, -1 off the rotation).</summary>
        readonly List<(string Id, int Socket)> _inscribed = new();
        readonly HashSet<string> _removed = new();

        /// <summary>Records where a FORM or a socket cost went. Take it first; this only says where.</summary>
        public void Inscribe(ExchangeEntry e, int socket)
        {
            if (e == null || (e.Slot == FormSlot.None && !e.SocketCost)) return;
            _inscribed.RemoveAll(x => x.Id == e.Id);
            _inscribed.Add((e.Id, e.Slot == FormSlot.Art || e.SocketCost ? socket : -1));
        }

        /// <summary>The socket an entry is inscribed on - -1 off the rotation, -2 not inscribed.</summary>
        public int SocketOf(string id)
        {
            foreach (var x in _inscribed) if (x.Id == id) return x.Socket;
            return -2;
        }

        /// <summary>The FORMS sharing a slot (an Art socket, or a whole off-rotation slot).</summary>
        public List<ExchangeEntry> FormsIn(FormSlot slot, int socket)
        {
            var list = new List<ExchangeEntry>();
            foreach (var x in _inscribed)
            {
                var e = ExchangeCatalog.Get(x.Id);
                if (e == null || e.SocketCost || e.Slot != slot || StacksOf(e) <= 0) continue;
                if (slot == FormSlot.Art && x.Socket != socket) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary>Strength of one form in a slot of <paramref name="forms"/>: (1 + step x (n - 1)) / n.</summary>
        public static float ShareFor(int forms)
            => forms <= 1 ? 1f : (1f + T.SlotShareStep * (forms - 1)) / forms;

        /// <summary>
        /// A form's strength now (the user's call, 2026-10-20): its slot's total shared equally by
        /// every form in it, oldest and newest alike. A form held but not inscribed (a balance model
        /// building a ledger without a run) is at full strength.
        /// </summary>
        public float Strength(string id)
        {
            var e = ExchangeCatalog.Get(id);
            if (e == null || e.Slot == FormSlot.None) return 1f;
            int socket = SocketOf(id);
            if (socket == -2) return 1f;
            return ShareFor(FormsIn(e.Slot, socket).Count);
        }

        /// <summary>Whether a socket cost of this id sits on this socket.</summary>
        public bool SocketHas(string costId, int socket) => StacksOf(costId) > 0 && SocketOf(costId) == socket;

        /// <summary>Slots holding two or more forms - the only ones a form may be taken out of.</summary>
        public List<ExchangeEntry> RemovableForms()
        {
            var list = new List<ExchangeEntry>();
            foreach (var x in _inscribed)
            {
                var e = ExchangeCatalog.Get(x.Id);
                if (e == null || e.SocketCost || e.Slot == FormSlot.None || StacksOf(e) <= 0) continue;
                if (FormsIn(e.Slot, x.Socket).Count >= 2) list.Add(e);
            }
            return list;
        }

        /// <summary>A form taken back out of a stacked slot (for a refusal and the deal - the caller
        /// spends them). Gone for the run: it never comes back on a deal.</summary>
        public bool RemoveForm(ExchangeEntry e)
        {
            if (e == null || !RemovableForms().Contains(e)) return false;
            _stacks.Remove(e.Id);
            _order.Remove(e);
            _inscribed.RemoveAll(x => x.Id == e.Id);
            _removed.Add(e.Id);
            Recompute();
            return true;
        }

        public bool Removed(string id) => _removed.Contains(id);

        // ---------------------------------------------------------------- schools (Phase 3)

        /// <summary>Boons held of one school (entries, not stacks) - what a run's lean is read from.</summary>
        public int SchoolCount(School s)
        {
            int n = 0;
            foreach (var e in _order) if (e.Kind == ExchangeKind.Boon && e.School == s) n++;
            return n;
        }

        public static School Opposite(School s) => s switch
        {
            School.Choler => School.Phlegm, School.Phlegm => School.Choler,
            School.Sanguine => School.Melancholy, School.Melancholy => School.Sanguine,
            _ => School.None,
        };

        static readonly School[] Humours = { School.Choler, School.Phlegm, School.Sanguine, School.Melancholy };

        /// <summary>
        /// The run's LEAN: the humour it holds most boons of, and how far - that humour's count less
        /// its opposite's, over every humour boon held (0 - balanced or nothing held - to 1).
        /// </summary>
        public (School Leading, float Lean) Lean()
        {
            School best = School.None; int top = 0, total = 0;
            foreach (var h in Humours)
            {
                int c = SchoolCount(h);
                total += c;
                if (c > top) { top = c; best = h; }
                else if (c == top) best = School.None;   // a tie leans nowhere
            }
            if (best == School.None || total == 0) return (School.None, 0f);
            return (best, Mathf.Clamp01((top - SchoolCount(Opposite(best))) / (float)total));
        }

        /// <summary>The humour a boon draw leans toward, picked by how many of each are held (null for
        /// none) - so a humour's boons come back more often once some are held.</summary>
        public School PickHeldHumour(System.Random rng)
        {
            int total = 0;
            foreach (var h in Humours) total += SchoolCount(h);
            if (total == 0) return School.None;
            int roll = rng.Next(total);
            foreach (var h in Humours) { roll -= SchoolCount(h); if (roll < 0) return h; }
            return School.None;
        }

        /// <summary>The union formed by the last take, if any - for the HUD to announce.</summary>
        public ExchangeEntry LastUnion { get; private set; }

        /// <summary>
        /// THE UNIONS: two opposite humours each held UnionAtEach boons deep and within UnionWithin of
        /// each other form theirs - granted, never drawn, and kept for the run once formed.
        /// </summary>
        void CheckUnions()
        {
            LastUnion = null;
            TryUnion("steam", School.Choler, School.Phlegm);
            TryUnion("clot", School.Sanguine, School.Melancholy);
        }

        void TryUnion(string id, School a, School b)
        {
            var e = ExchangeCatalog.Get(id);
            if (e == null || StacksOf(id) > 0) return;
            int ca = SchoolCount(a), cb = SchoolCount(b);
            if (ca < T.UnionAtEach || cb < T.UnionAtEach || Mathf.Abs(ca - cb) > T.UnionWithin) return;
            _stacks[id] = 1;
            _order.Add(e);
            LastUnion = e;
        }

        // ---------------------------------------------------------------- evolutions (Phase 5)

        /// <summary>Evolutions ready now: the form held with its catalyst, not yet evolved.</summary>
        public List<ExchangeEntry> Evolvable()
        {
            var list = new List<ExchangeEntry>();
            foreach (var e in ExchangeCatalog.All)
                if (e.Origin == EntryOrigin.Evolution && StacksOf(e) <= 0
                    && StacksOf(e.EvolvesFrom) > 0 && StacksOf(e.Catalyst) > 0) list.Add(e);
            return list;
        }

        /// <summary>A form EVOLVES: the evolution takes its place - its slot, its socket, its place in
        /// the order - and the catalyst stays. The base form never returns this run.</summary>
        public bool Evolve(ExchangeEntry evo)
        {
            if (evo == null || !Evolvable().Contains(evo)) return false;
            var from = ExchangeCatalog.Get(evo.EvolvesFrom);
            int at = _order.IndexOf(from);
            int socket = SocketOf(from.Id);
            _stacks.Remove(from.Id);
            _inscribed.RemoveAll(x => x.Id == from.Id);
            _removed.Add(from.Id);
            _stacks[evo.Id] = 1;
            if (at >= 0) _order[at] = evo; else _order.Add(evo);
            if (socket != -2) _inscribed.Add((evo.Id, socket));
            Recompute();
            return true;
        }

        // ---------------------------------------------------------------- ferment, oaths (Phase 4)

        readonly Dictionary<string, int> _ferment = new();
        readonly Dictionary<string, float> _potency = new();
        readonly Dictionary<string, float> _oath = new();
        readonly HashSet<string> _fulfilled = new();
        int _tributeFloors;

        /// <summary>Floors of depth a held Nigredo has fermented.</summary>
        public int Fermented(string costId) => _ferment.TryGetValue(costId, out var f) ? f : 0;

        /// <summary>The ferment steps a Nigredo has reached - what its Albedo would come out at.</summary>
        public int FermentSteps(string costId) => Mathf.Min(T.FermentSteps, Fermented(costId) / T.FermentFloorsPerStep);

        /// <summary>An Albedo's potency: 1, or more for one made from a fermented Nigredo.</summary>
        public float Potency(string albedoId) => _potency.TryGetValue(albedoId, out var p) ? p : 1f;

        /// <summary>An oath's progress (Blood Debt: Chaser-equivalents killed).</summary>
        public float OathProgress(string id) => _oath.TryGetValue(id, out var p) ? p : 0f;

        /// <summary>The oath fulfilled by the last call that fulfilled one - for the HUD.</summary>
        public ExchangeEntry LastFulfilled { get; private set; }

        /// <summary>Adds to an oath's progress; true when that fulfils it (it leaves the ledger).</summary>
        public bool AddOathProgress(string id, float amount, float needed)
        {
            if (StacksOf(id) <= 0) return false;
            _oath[id] = OathProgress(id) + amount;
            if (_oath[id] < needed) return false;
            Fulfil(id);
            return true;
        }

        /// <summary>An OATH done: it leaves the ledger and never returns this run.</summary>
        public void Fulfil(string id)
        {
            var e = ExchangeCatalog.Get(id);
            if (e == null || StacksOf(id) <= 0) return;
            _stacks.Remove(id);
            _order.Remove(e);
            _oath.Remove(id);
            _fulfilled.Add(id);
            LastFulfilled = e;
            Recompute();
        }

        public void ClearLastFulfilled() => LastFulfilled = null;

        /// <summary>TRIBUTE (Toll's Albedo, reworked): a refusal given back.</summary>
        void GiveBackRefusal() { if (RefusalsUsed > 0) RefusalsUsed--; }

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
            // FERMENT: a Nigredo carried longer makes a stronger Albedo.
            _potency[albedo.Id] = 1f + T.FermentStep * FermentSteps(cost.Id);
            _ferment.Remove(cost.Id);

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
            if (e == null || e.Retired || AtCap(e) || Resting(e) || _transmuted.Contains(e.Id) || _removed.Contains(e.Id)
                || _fulfilled.Contains(e.Id) || (e.ShortOnly && !RunFormat.Short)) return false;
            switch (e.Origin)
            {
                case EntryOrigin.Albedo: return false;            // only a circle makes one
                case EntryOrigin.Union: return false;             // only balance makes one
                case EntryOrigin.Evolution: return false;         // only a form and its catalyst grow one
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

        /// <summary>Everything of this kind and weight that may be drawn now - socket costs only
        /// when asked for (they pay for an Art form, on its own socket, and nothing else), and only
        /// of one school when one is named (leaning).</summary>
        public List<ExchangeEntry> Offerable(ExchangeKind kind, int weight, bool socketCosts = false, School only = School.None)
        {
            var list = new List<ExchangeEntry>();
            foreach (var e in ExchangeCatalog.All)
                if (e.Kind == kind && e.Weight == weight && e.SocketCost == socketCosts
                    && (only == School.None || e.School == only) && Offerable(e)) list.Add(e);
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

        /// <summary>Max health points the old Viriditas grew - kept for saves; it grows nothing now.</summary>
        public float Virid { get; private set; }

        /// <summary>Floors of DEPTH cleared while Senescence is held - each makes enemies hit harder.</summary>
        public int Senescence { get; private set; }

        /// <summary>
        /// A floor was cleared: the counters some entries keep move on. Called by the run once per
        /// cleared floor, before the deal (Withering takes its cut first). Each step is per DEPTH:
        /// a Short Descent floor is two floors deep, so it steps twice as far, or these entries
        /// would build half as far at the same danger (RunFormat).
        /// </summary>
        public void FloorCleared()
        {
            int depth = RunFormat.DepthPerFloor;
            int wither = StacksOf("withering");
            if (wither > 0)
            {
                float cap = AtMax("withering") ? float.PositiveInfinity : T.WitheringCap * 100f;   // Desiccation
                Withered = Mathf.Min(cap, Withered + T.WitheringPerFloor * 100f * wither * depth);
            }
            else Withered = 0f;

            if (StacksOf("senescence") > 0) Senescence += depth;

            // FERMENT: every Nigredo held ferments a floor of depth.
            foreach (var e in Nigredos()) _ferment[e.Id] = Fermented(e.Id) + depth;

            // TRIBUTE: every few floors cleared give a refusal back (fewer, fermented).
            if (StacksOf("tribute") > 0
                && ++_tributeFloors >= Mathf.Max(2, Mathf.RoundToInt(T.TributeFloorsPerRefusal / Potency("tribute"))))
            {
                _tributeFloors = 0;
                GiveBackRefusal();
            }
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
            _inscribed.Clear();
            _removed.Clear();
            _ferment.Clear();
            _potency.Clear();
            _oath.Clear();
            _fulfilled.Clear();
            _tributeFloors = 0;
            LastFulfilled = null;
            Recompute();
        }

        // ---------------------------------------------------------------- continuation

        /// <summary>The ledger written down for a run snapshot (Chain.RunSnapshot): entries by id,
        /// in the order taken, and every clock and counter the deals read.</summary>
        public Chain.LedgerSave Capture()
        {
            var s = new Chain.LedgerSave
            {
                Pending = Pending.Copy(),
                DealIndex = DealIndex,
                RefusalsUsed = RefusalsUsed,
                Withered = Withered,
                Virid = Virid,
                Senescence = Senescence,
            };
            foreach (var e in _order)
            {
                s.Order.Add(e.Id);
                s.Stacks.Add(StacksOf(e.Id));
            }
            foreach (var kv in _lastOffered) { s.LastOfferedIds.Add(kv.Key); s.LastOfferedAt.Add(kv.Value); }
            foreach (var kv in _restUntil) { s.RestIds.Add(kv.Key); s.RestUntil.Add(kv.Value); }
            foreach (var c in _newCombos) s.NewCombos.Add(c.Id);
            s.Opened.AddRange(_opened);
            s.Transmuted.AddRange(_transmuted);
            foreach (var x in _inscribed) { s.InscribedIds.Add(x.Id); s.InscribedSockets.Add(x.Socket); }
            s.Removed.AddRange(_removed);
            foreach (var kv in _ferment) { s.FermentIds.Add(kv.Key); s.FermentFloors.Add(kv.Value); }
            foreach (var kv in _potency) { s.PotencyIds.Add(kv.Key); s.PotencyValues.Add(kv.Value); }
            foreach (var kv in _oath) { s.OathIds.Add(kv.Key); s.OathProgress.Add(kv.Value); }
            s.Fulfilled.AddRange(_fulfilled);
            s.TributeFloors = _tributeFloors;
            return s;
        }

        /// <summary>
        /// A snapshot's ledger, back - set directly, never through <see cref="Take"/>, which would
        /// fire each entry's OnTaken into the deal clocks a second time. An id the catalogue no
        /// longer has is dropped. The spire's floor boon is not here (the run restores it).
        /// </summary>
        public void Restore(Chain.LedgerSave s)
        {
            Clear();
            if (s == null) return;
            Pending = s.Pending?.Copy() ?? new PendingOffer();
            DealIndex = s.DealIndex;
            RefusalsUsed = s.RefusalsUsed;
            Withered = s.Withered;
            Virid = s.Virid;
            Senescence = s.Senescence;
            for (int i = 0; i < s.Order.Count; i++)
            {
                var e = ExchangeCatalog.Get(s.Order[i]);
                int n = i < s.Stacks.Count ? s.Stacks[i] : 1;
                if (e == null || n <= 0 || _stacks.ContainsKey(e.Id)) continue;
                _stacks[e.Id] = n;
                _order.Add(e);
            }
            for (int i = 0; i < s.LastOfferedIds.Count && i < s.LastOfferedAt.Count; i++)
                _lastOffered[s.LastOfferedIds[i]] = s.LastOfferedAt[i];
            for (int i = 0; i < s.RestIds.Count && i < s.RestUntil.Count; i++)
                _restUntil[s.RestIds[i]] = s.RestUntil[i];
            foreach (var id in s.NewCombos) { var c = ExchangeCatalog.Get(id); if (c != null) _newCombos.Add(c); }
            foreach (var id in s.Opened) _opened.Add(id);
            foreach (var id in s.Transmuted) _transmuted.Add(id);
            if (s.InscribedIds != null)
                for (int i = 0; i < s.InscribedIds.Count && i < s.InscribedSockets.Count; i++)
                    if (ExchangeCatalog.Get(s.InscribedIds[i]) != null) _inscribed.Add((s.InscribedIds[i], s.InscribedSockets[i]));
            if (s.Removed != null) foreach (var id in s.Removed) _removed.Add(id);
            if (s.FermentIds != null)
                for (int i = 0; i < s.FermentIds.Count && i < s.FermentFloors.Count; i++) _ferment[s.FermentIds[i]] = s.FermentFloors[i];
            if (s.PotencyIds != null)
                for (int i = 0; i < s.PotencyIds.Count && i < s.PotencyValues.Count; i++) _potency[s.PotencyIds[i]] = s.PotencyValues[i];
            if (s.OathIds != null)
                for (int i = 0; i < s.OathIds.Count && i < s.OathProgress.Count; i++) _oath[s.OathIds[i]] = s.OathProgress[i];
            if (s.Fulfilled != null) foreach (var id in s.Fulfilled) _fulfilled.Add(id);
            _tributeFloors = s.TributeFloors;
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
            c._inscribed.AddRange(_inscribed);
            foreach (var id in _removed) c._removed.Add(id);
            foreach (var kv in _ferment) c._ferment[kv.Key] = kv.Value;
            foreach (var kv in _potency) c._potency[kv.Key] = kv.Value;
            foreach (var kv in _oath) c._oath[kv.Key] = kv.Value;
            foreach (var id in _fulfilled) c._fulfilled.Add(id);
            c._tributeFloors = _tributeFloors;
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

            // The counter: max health Withering has taken.
            if (Withered > 0f && StacksOf("withering") > 0) m.Add(StatKind.MaxHp, -Withered);
            m.Potency = _potency;

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
