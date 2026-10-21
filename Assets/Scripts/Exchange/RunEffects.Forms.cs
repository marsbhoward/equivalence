using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>
    /// FORMS (Phase 2, EXCHANGE.md 5.3): entries that change what one action DOES, inscribed on a
    /// slot - an art of the rotation, the basics, a kill, the defensive ability. Forms sharing a
    /// slot share its strength (RunModifiers.Strength), and each scales the measure its card names.
    ///
    /// THE CLUTTER GUARDS (decided 2026-10-20): what a form leaves on the ground is drawn on the
    /// ground (ResidueField, GroundDecal + 2 - under every telegraph); only a brief spark may flash
    /// above; one live field per form per target (a second refreshes nothing); no more than
    /// Tuning.Exchange.FormVisualCap form visuals alive at once - past it a form still applies but
    /// draws nothing; and a weakened form draws fainter.
    /// </summary>
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- socket costs

        /// <summary>Heavy Hand: the art on this socket locks longer.</summary>
        public float SocketLockMul(int socket)
            => _mods != null && _mods.SocketHas("heavy_hand", socket) ? 1f + T.HeavyHandLock : 1f;

        /// <summary>Brittle Script: the art on this socket can't crit.</summary>
        public bool SocketCantCrit(int socket) => _mods != null && _mods.SocketHas("brittle_script", socket);

        /// <summary>Dulled Script: the art on this socket deals less.</summary>
        float SocketDamageMul(int socket)
            => _mods != null && _mods.SocketHas("dulled_script", socket) ? 1f - T.DulledScriptDamage : 1f;

        // ---------------------------------------------------------------- state

        int _shockwaveSerial = -1, _riptideSerial = -1;
        int _burningDeaths, _soakedDeaths;

        // ---------------------------------------------------------------- growth (Phase 5)

        /// <summary>KINDLING: how much stronger the player's burns are now (1 without it).</summary>
        public float BurnGrowth => Has("kindling")
            ? 1f + Mathf.Min(T.KindlingCap, T.KindlingStep * (_burningDeaths / T.KindlingDeathsPerStep)) : 1f;

        /// <summary>SWELL: seconds the player's soaks last longer now (0 without it).</summary>
        public float SoakGrowth => Has("swell") ? Mathf.Min(T.SwellCap, T.SwellPerDeath * _soakedDeaths) : 0f;

        /// <summary>The growing entries' line for the HUD - null when none is held.</summary>
        public string GrowthLine
        {
            get
            {
                string line = null;
                if (Has("kindling")) line = $"KINDLING  +{Mathf.RoundToInt((BurnGrowth - 1f) * 100f)}%";
                if (Has("swell")) line = (line != null ? line + "      " : "") + $"SWELL  +{SoakGrowth:0.0} s";
                return line;
            }
        }
        readonly List<GameObject> _formVisuals = new();
        readonly Dictionary<(string, Health), ResidueField> _formFields = new();

        float Strength(string id) => _mods != null ? _mods.Strength(id) : 1f;

        /// <summary>An Art form fires for the art on its own socket only.</summary>
        bool OnThisSocket(string id) => _mods != null && _pc != null && Has(id) && _mods.SocketOf(id) == _pc.ArtSocket;

        // ---------------------------------------------------------------- on hit

        void FormsOnHit(Health target, DamageInfo info, in HitContext h)
        {
            if (_pc == null) return;

            if (h.IsFinisher)
            {
                // AFTERSHOCK: broken ground under each body the inscribed art strikes. EARTHQUAKE,
                // evolved: the ground staggers what stands on it too.
                if (OnThisSocket("aftershock"))
                    Field("aftershock", target, target.transform.position, T.AftershockRadius, ElementType.Earth,
                          T.AftershockSeconds * Strength("aftershock"), Strength("aftershock"));
                if (OnThisSocket("earthquake"))
                {
                    var f = Field("earthquake", target, target.transform.position, T.AftershockRadius, ElementType.Earth,
                                  T.AftershockSeconds * Strength("earthquake"), Strength("earthquake"));
                    if (f != null) f.StaggerSeconds = T.EarthquakeStagger;
                }

                // SHOCKWAVE: once per art, a ring out from the player. NOVA, evolved: wider, harder.
                bool nova = OnThisSocket("nova");
                if ((OnThisSocket("shockwave") || nova) && _shockwaveSerial != _pc.ArtSerial)
                {
                    _shockwaveSerial = _pc.ArtSerial;
                    float s = Strength(nova ? "nova" : "shockwave");
                    float radius = nova ? T.NovaRadius : T.ShockwaveRadius;
                    float dmg = (nova ? T.NovaHitUnits : T.ShockwaveHitUnits) * _pc.HitUnit * s;
                    float burn = (nova ? T.NovaBurnShare : T.ShockwaveBurnShare) * _pc.HitUnit * s;
                    float burnFor = nova ? T.NovaBurnSeconds : T.ShockwaveBurnSeconds;
                    Vector2 at = _pc.transform.position;
                    foreach (var hp in EnemiesWithin(at, radius))
                    {
                        hp.Take(new DamageInfo(dmg, ElementType.Fire, _pc.gameObject));
                        if (!hp.IsDead) _pc.Burn(hp, burn, burnFor);
                    }
                    Spr.Flash(at, radius, new Color(1f, 0.55f, 0.2f, 0.35f * s), 0.22f);
                }

                // RIPTIDE: once per art, a wave along the aim, soaking what it crosses. MAELSTROM,
                // evolved: a soaking pool where it ends.
                bool maelstrom = OnThisSocket("maelstrom");
                if ((OnThisSocket("riptide") || maelstrom) && _riptideSerial != _pc.ArtSerial)
                {
                    _riptideSerial = _pc.ArtSerial;
                    float s = Strength(maelstrom ? "maelstrom" : "riptide");
                    Vector2 from = _pc.transform.position, dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
                    foreach (var hp in EnemiesInStrip(from, dir, T.RiptideLength, T.RiptideHalfWidth))
                        _pc.Soak(hp, T.RiptideSoakSeconds * s, T.RiptideVulnerability);
                    if (maelstrom)
                        Field("maelstrom", null, from + dir * T.RiptideLength, T.MaelstromPoolRadius, ElementType.Water,
                              T.MaelstromPoolSeconds * s, s);
                    for (int i = 1; i <= 3; i++)
                        Spr.Flash(from + dir * (T.RiptideLength * i / 3f), 0.6f, new Color(0.4f, 0.7f, 1f, 0.3f * s), 0.2f, false);
                }
                return;
            }

            // FAULT LINE (greatsword basics): the swing's third body cracks the floor ahead.
            if (!h.Thrown && Has("fault_line") && _swingStruck != null && _swingStruck.Count == T.FaultLineBodies)
                StartCoroutine(FaultLine(Strength("fault_line")));

            // LONE CUT (greatsword basics): a swing that strikes ONE enemy bleeds it. The count is only
            // final once the swing has resolved every body, so it is read a frame later.
            if (!h.Thrown && h.First && _mods.Weapon == WeaponClass.Greatsword && Has("lone_cut"))
                StartCoroutine(LoneCut(target, info.Amount, Strength("lone_cut")));

            // AFTERBURN (the defensive ability): the basic that follows it burns what it strikes.
            if (_afterburnThisSwing)
            {
                float s = Strength("afterburn");
                _pc.Burn(target, info.Amount * T.AfterburnBurnShare * s, T.AfterburnBurnSeconds);
            }

            // SPLINTER (disc basics): a bounce throws a splinter at the nearest other enemy.
            if (h.Thrown && !h.First && _mods.Weapon == WeaponClass.Disc && Has("splinter"))
            {
                var near = NearestEnemy(target.transform.position, T.SplinterReach, target);
                if (near != null)
                {
                    float s = Strength("splinter");
                    near.Take(new DamageInfo(info.Amount * T.SplinterShare * s, info.Element, _pc.gameObject));
                    if (!near.IsDead)
                        _pc.Bleed(near, info.Amount * T.SplinterBleedShare * s / T.SplinterBleedSeconds, T.SplinterBleedSeconds);
                    Spr.Flash(near.transform.position, 0.35f, new Color(0.8f, 0.15f, 0.2f, 0.6f), 0.15f, false);
                }
            }
        }

        IEnumerator LoneCut(Health target, float hit, float strength)
        {
            yield return null;
            if (_pc == null || target == null || target.IsDead || _swingStruck == null || _swingStruck.Count != 1) yield break;
            _pc.Bleed(target, hit * T.LoneCutBleedShare * strength, T.LoneCutBleedSeconds);
        }

        IEnumerator FaultLine(float strength)
        {
            Vector2 from = _pc.transform.position;
            Vector2 dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
            for (int i = 1; i <= 3; i++)
                Visual(GroundCracks.Spawn(Arena.NearestFloor(from + dir * (T.FaultLineLength * i / 3f), 0.3f))?.gameObject);
            yield return new WaitForSeconds(T.FaultLineDelay);
            if (_pc == null) yield break;
            float dmg = T.FaultLineHitUnits * _pc.HitUnit * strength;
            foreach (var hp in EnemiesInStrip(from, dir, T.FaultLineLength, T.FaultLineHalfWidth))
                hp.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject));
        }

        /// <summary>SPINDRIFT (greatsword basics): the swing's arc throws spray just past its reach,
        /// soaking what stands there - the blade's reach as a setup for the art. No damage.</summary>
        void Spindrift()
        {
            if (_pc == null || _mods == null || _mods.Weapon != WeaponClass.Greatsword || !Has("spindrift")) return;
            float s = Strength("spindrift"), reach = _pc.PendingRange;
            Vector2 at = _pc.transform.position;
            Vector2 facing = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
            float cos = Mathf.Cos(T.SpindriftHalfAngle * Mathf.Deg2Rad);
            foreach (var hp in EnemiesWithin(at, reach + T.SpindriftBeyond))
            {
                Vector2 to = (Vector2)hp.transform.position - at;
                if (to.magnitude < reach * 0.8f || Vector2.Dot(to.normalized, facing) < cos) continue;
                _pc.Soak(hp, T.SpindriftSoakSeconds * s, T.SpindriftSoakVulnerability);
            }
            // The spray, faint, along the arc's far edge: three drops, not a ring.
            for (int i = -1; i <= 1; i++)
            {
                float a = i * T.SpindriftHalfAngle * 0.6f * Mathf.Deg2Rad;
                Vector2 d = new(facing.x * Mathf.Cos(a) - facing.y * Mathf.Sin(a), facing.x * Mathf.Sin(a) + facing.y * Mathf.Cos(a));
                Spr.Flash(at + d * (reach + T.SpindriftBeyond * 0.5f), 0.35f, new Color(0.45f, 0.75f, 1f, 0.35f * s), 0.14f, false);
            }
        }

        // ---------------------------------------------------------------- class basics, stamped at the throw

        float _caughtAt = -10f;

        /// <summary>A disc came home to the hand (Snapback's moment).</summary>
        public void OnDiscCaught() => _caughtAt = Time.time;

        /// <summary>LODESTAR and SNAPBACK on a basic disc throw, carried by the disc itself.</summary>
        public void ArmDisc(ThrownDisc disc)
        {
            if (disc == null || _mods == null) return;
            if (Has("lodestar"))
            {
                disc.Banks = T.LodestarBanks;
                disc.BankBonus = T.LodestarBonus * Strength("lodestar");
                disc.BankSoakSeconds = T.LodestarSoakSeconds;
                disc.BankSoakVulnerability = T.LodestarSoakVulnerability;
            }
            if (Has("snapback") && Time.time - _caughtAt <= T.SnapbackWindow)
            {
                _caughtAt = -10f;   // one throw per catch
                disc.BurnShare = T.SnapbackBurnShare * Strength("snapback");
                disc.BurnSeconds = T.SnapbackBurnSeconds;
            }
        }

        /// <summary>PATIENT STRING and LONG EMBER on a basic arrow, carried by the arrow itself.</summary>
        public void ArmArrow(ThrownArrow arrow, bool planted)
        {
            if (arrow == null || _mods == null) return;
            if (planted && Has("patient_string"))
            {
                arrow.BleedShare = T.PatientBleedShare * Strength("patient_string");
                arrow.BleedSeconds = T.PatientBleedSeconds;
            }
            if (Has("long_ember"))
            {
                arrow.EmberFrom = T.LongEmberFrom;
                arrow.EmberShare = T.LongEmberBurnShare * Strength("long_ember");
                arrow.EmberSeconds = T.LongEmberBurnSeconds;
            }
        }

        // ---------------------------------------------------------------- other actions

        /// <summary>THORNFIELD (bow basics): a basic's arrow that hits nothing lands as a caltrop.</summary>
        public void OnArrowMissed(Vector2 at)
        {
            if (_mods == null || _pc == null || !Has("thornfield")) return;
            float s = Strength("thornfield");
            Field("thornfield", null, at, T.ThornfieldRadius, ElementType.Earth, T.ThornfieldSeconds * s, s);
        }

        /// <summary>PYRE (a kill): burning ground where the enemy fell. Also where the growing entries
        /// count their deaths.</summary>
        void FormsOnKill(Health h)
        {
            var st = h != null ? h.GetComponent<StatusEffects>() : null;
            if (st != null && st.Burning) _burningDeaths++;
            if (st != null && st.Soaked) _soakedDeaths++;
            if (_pc == null || h == null) return;
            // DRAWDOWN: the crowd is drawn a step toward the body - set up for the next area hit.
            if (Has("drawdown"))
            {
                Vector2 at = h.transform.position;
                float step = T.DrawdownStep * Strength("drawdown");
                foreach (var hp in EnemiesWithin(at, T.DrawdownRadius)) DragToward.Pull(hp, at, step);
            }
            if (!Has("pyre")) return;
            float s = Strength("pyre");
            Field("pyre", null, h.transform.position, T.PyreRadius, ElementType.Fire, T.PyreSeconds * s, s);
        }

        /// <summary>LACERATE (the defensive ability): every enemy close by starts bleeding.</summary>
        public void OnDefenceUsed()
        {
            if (_mods == null || _pc == null) return;
            // AFTERBURN: the next basic inside the window gains Damage and burns (Offense, FormsOnHit).
            if (Has("afterburn")) _afterburnUntil = Time.time + T.AfterburnWindow;
            bool hem = Has("hemorrhage");   // HEMORRHAGE, evolved: wider, deeper, and it staggers
            if (!hem && !Has("lacerate")) return;
            float radius = hem ? T.HemorrhageRadius : T.LacerateRadius;
            float dps = (hem ? T.HemorrhageBleedHitUnits : T.LacerateBleedHitUnits) * _pc.HitUnit * Strength(hem ? "hemorrhage" : "lacerate");
            foreach (var hp in EnemiesWithin(_pc.transform.position, radius))
            {
                _pc.Bleed(hp, dps, T.LacerateSeconds);
                if (hem && !hp.IsDead) _pc.Stagger(hp, T.HemorrhageStagger);
            }
            Spr.Flash(_pc.transform.position, radius, new Color(0.8f, 0.12f, 0.18f, 0.3f), 0.18f);
        }

        // ---------------------------------------------------------------- the unions (Phase 3)

        readonly Dictionary<Health, float> _steamAt = new();

        /// <summary>Asked by PlayerController.Burn and Soak BEFORE the status lands: STEAM bursts when a
        /// burn meets a soak on one body (once per body per cooldown).</summary>
        public void BeforeStatus(Health target, bool burning, bool soaking)
        {
            if (_mods == null || _pc == null || target == null || target.IsDead || !Has("steam")) return;
            var st = target.GetComponent<StatusEffects>();
            if (st == null) return;
            bool meets = (burning && st.Soaked) || (soaking && st.Burning);
            if (!meets) return;
            if (_steamAt.TryGetValue(target, out var at) && Time.time - at < T.SteamCooldown) return;
            _steamAt[target] = Time.time;
            if (_steamAt.Count > 64)
                foreach (var k in new List<Health>(_steamAt.Keys)) if (k == null) _steamAt.Remove(k);
            float dmg = T.SteamHitUnits * _pc.HitUnit;
            Vector2 p = target.transform.position;
            foreach (var hp in EnemiesWithin(p, T.SteamRadius)) hp.Take(new DamageInfo(dmg, ElementType.Water, _pc.gameObject));
            Spr.Flash(p, T.SteamRadius, new Color(0.9f, 0.92f, 0.95f, 0.35f), 0.22f);
        }

        /// <summary>Asked by PlayerController.Stagger: CLOT cashes seconds of a bleed in at once.</summary>
        public void OnStaggering(Health target)
        {
            if (_mods == null || _pc == null || target == null || target.IsDead || !Has("clot")) return;
            var st = target.GetComponent<StatusEffects>();
            float dmg = st != null && st.Bleeding ? st.TakeBleed(T.ClotSeconds) : 0f;
            if (dmg > 0f) target.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject));
        }

        // ---------------------------------------------------------------- the clutter guards

        /// <summary>
        /// A field a form leaves on the ground: ONE per form per target (a body already under one
        /// gets nothing new), counted against the cap, fainter when the form is weakened.
        /// </summary>
        ResidueField Field(string formId, Health target, Vector2 at, float radius, ElementType element, float seconds, float strength)
        {
            var key = (formId, target);
            if (target != null && _formFields.TryGetValue(key, out var live) && live != null) return null;
            PruneVisuals();
            if (_formFields.Count > 64)   // forget fields long gone
                foreach (var k in new List<(string, Health)>(_formFields.Keys))
                    if (_formFields[k] == null) _formFields.Remove(k);
            var f = ResidueField.Spawn(at, radius, element, seconds, _pc);
            if (f == null) return null;
            f.AlphaMul = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0.5f, 1f, strength));
            if (_formVisuals.Count >= T.FormVisualCap) f.Hidden = true;   // applies, draws nothing
            else _formVisuals.Add(f.gameObject);
            if (target != null) _formFields[key] = f;
            return f;
        }

        void Visual(GameObject go)
        {
            if (go == null) return;
            PruneVisuals();
            if (_formVisuals.Count >= T.FormVisualCap) { Destroy(go); return; }
            _formVisuals.Add(go);
        }

        void PruneVisuals() => _formVisuals.RemoveAll(g => g == null);

        static List<Health> EnemiesInStrip(Vector2 from, Vector2 dir, float length, float halfWidth)
        {
            var list = new List<Health>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                Vector2 d = (Vector2)e.transform.position - from;
                float along = Vector2.Dot(d, dir);
                if (along < 0f || along > length) continue;
                if (Mathf.Abs(d.x * dir.y - d.y * dir.x) <= halfWidth) list.Add(hp);
            }
            return list;
        }
    }
}
