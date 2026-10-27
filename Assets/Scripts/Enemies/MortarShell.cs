using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// A Mortar's lobbed shell: arcs to a FIXED point, lands, blinks in groups of one, two,
    /// three - each group lighting the next of three rings inward on the floor - then goes off in
    /// a small radius.
    ///
    /// AIMED ONCE, AT THE MOMENT OF FIRING, and never steered - the same rule the Ranged bolt and
    /// Dasher's rush live by. The point is the ground the player was standing on when the mortar
    /// let go; the shell is a reason to have moved, not a homing threat.
    ///
    /// ITS OWN OBJECT, for ClusterShard's reason: killing the mortar mid-lob must not delete the
    /// shell in the air. What's already thrown still lands.
    ///
    /// NOT RUN THROUGH HazardQuery like EnemyProjectile - a lob travels OVER the floor, so a force
    /// field it passes above has nothing to null or amplify.
    ///
    /// PARRYABLE IN FLIGHT. A parry with the shell's drawn position within reach turns it: it
    /// hops a short way back toward the side it came from, and its fuse blinks BLUE - the one
    /// change of colour that says "this one is yours now" - and the blast hurts enemies instead
    /// of the player, credited to the player like a reflected bolt. A landed shell cannot be
    /// deflected; the parry at the moment of its blast still negates it, as Bomb's does.
    ///
    /// Everything the player reads (landing ring, shell, blink) sits at Telegraph, above the depth
    /// band - a countdown hidden behind the body standing on it would be no countdown at all.
    /// Only the shadow is on the ground.
    ///
    /// TWO PAYLOADS, one shell. <see cref="Launch"/> is the Mortar's blast; <see cref="LaunchMire"/>
    /// is the elite Turret's MIRE, which does no damage - where the fuse runs out it spreads a wide
    /// patch of slowing ground (Hazards.MireField) instead of exploding. Same lob, same landing
    /// ring, same parry, SAME FUSE (the user's call: the countdown is the window to step out before
    /// the ground turns), only yellow; a turned mire shell spreads a patch that slows enemies.
    /// </summary>
    public class MortarShell : MonoBehaviour
    {
        /// <summary>A turned Salamander shell's blue, dim to hot - the blue every turned shell blinks.</summary>
        static readonly Color[] DeflectedRamp = TexelRig.Hex("#0c2440", "#1f5fa8", "#4aa0ff", "#d8ecff");

        // Plain, non-readonly fields throughout - see the domain-reload notes in CLAUDE.md.
        Vector2 _from, _to;
        float _flightSeconds, _apex, _clock;
        /// <summary>Height the current flight starts at, fading out over it - a deflection
        /// begins in the air where the parry caught it, not back on the ground.</summary>
        float _startHeight;
        bool _landed, _deflected;
        bool _wasLit;   // the fuse's last blink state - each new blink ticks

        /// <summary>In the air and still the enemy's - what the orbiting discs stop (DiscOrbit).</summary>
        public bool Hostile => !_landed && !_deflected && _shell != null;

        /// <summary>Where the shell is DRAWN (its height included), world.</summary>
        /// <summary>The ground under the shell in flight (its shadow) - where it would stand.</summary>
        public Vector2 GroundPosition => _shadowSr != null ? (Vector2)_shadowSr.transform.position : (Vector2)transform.position;

        public Vector2 DrawnPosition => _shell != null ? (Vector2)_shell.position : (Vector2)transform.position;
        float _damage;
        /// <summary>A mire shell: no fuse, no damage, lands as a slowing patch.</summary>
        bool _mire;
        /// <summary>The Tempest Eye's lob: a ball of static and charged ground in this accent
        /// ramp, not the yellow mire (null for the Mortar's shell and the yellow mire).</summary>
        Color[] _static;
        /// <summary>What the landing ring marks - the blast for a shell, the patch for a mire.</summary>
        float _radius;
        Transform _target;
        Health _targetHealth;
        Player.PlayerController _targetController;

        Transform _shell;
        SpriteRenderer _shellSr, _shadowSr;

        /// <summary>The Salamander's magma (MortarArt.Accent): its shell a texel glob and its blast a
        /// MortarBurst (the user's sign-off, 2026-10-19). Every shell is drawn that way now, in its
        /// own ramp (ShellRamp).</summary>
        Color[] _magma;
        Color[] Ramp => _deflected ? DeflectedRamp : _magma;

        /// <summary>Every shell's ramp, dark to hot: turned blue, the Salamander's magma, the
        /// Tempest Eye's static, or the mire's yellow.</summary>
        Color[] ShellRamp => _deflected ? DeflectedRamp : _magma ?? _static ?? MireRamp;
        static readonly Color[] MireRamp = TexelRig.Hex("#3a2604", "#c08a10", "#f2c230", "#fffbe0");

        /// <summary>The landing mark: THREE rings (the user's pick, 2026-10-26 - "target rings"),
        /// the outer one the blast's edge; each group of the fuse lights the next ring inward, so
        /// the 1-2-3 is counted on the floor. Not readonly - a reload brings it back null.</summary>
        SpriteRenderer[] _rings;
        static readonly float[] RingShare = { 1f, 0.66f, 0.33f };

        /// <param name="from">Where the mortar stands.</param>
        /// <param name="to">Where it lands - already clamped inside the walls by the caller.</param>
        /// <param name="magma">The Salamander's ramp - the texel glob and ring (see _magma).</param>
        public static MortarShell Launch(Vector2 from, Vector2 to, Transform parent, Transform target,
                                         float damage, Color[] magma = null)
        {
            var s = Create(from, to, parent, target, damage, mire: false);
            if (magma != null && magma.Length >= 4)
            {
                s._magma = magma;
                s.Paint(false, 0f);
            }
            return s;
        }

        /// <summary>The glob in the shell's ramp, and the three rings: in the air all faint, firming
        /// up as it comes down (<paramref name="flight"/> 0..1); landed, every ring the fuse has
        /// reached lit and the current one blinking with it.</summary>
        void Paint(bool lit, float flight)
        {
            var ramp = ShellRamp;
            _shellSr.sprite = GlobSprite(ramp, lit);
            _shellSr.color = Color.white;
            if (_rings == null) return;
            int group = _landed ? GroupAt(_clock) : 0;
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] == null) continue;
                bool on = _landed && (i < group - 1 || (i == group - 1 && LitAt(_clock)));
                _rings[i].sprite = TargetRingSprite(_radius * RingShare[i], ramp, on);
                _rings[i].color = new Color(1f, 1f, 1f, _landed ? (on ? 1f : 0.55f) : Mathf.Lerp(0.12f, 0.35f, flight));
            }
        }

        float _bounceDistance;

        /// <summary>
        /// The shell BOUNCES once (Tuning.EnemyBands.ShellBounceFromBand): after its blast a second
        /// shell hops on along the same line, <paramref name="distance"/> further, its own ring drawn
        /// from the moment it leaves. A deflected shell does not bounce.
        /// </summary>
        public MortarShell Bounces(float distance)
        {
            _bounceDistance = distance;
            return this;
        }

        /// <summary>The elite Turret's mire shell - see the class summary.</summary>
        public static MortarShell LaunchMire(Vector2 from, Vector2 to, Transform parent, Transform target,
                                             Color[] staticRamp = null)
        {
            var s = Create(from, to, parent, target, 0f, mire: true);
            if (staticRamp != null && staticRamp.Length >= 4)
            {
                s._static = staticRamp;
            }
            return s;
        }

        static MortarShell Create(Vector2 from, Vector2 to, Transform parent, Transform target,
                                  float damage, bool mire)
        {
            // Rooted AT THE LANDING POINT, not the muzzle: the ring and the blast never move, and
            // only the shell and its shadow travel toward them as children.
            var go = new GameObject(mire ? "mire.shell" : "mortar.shell");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = to;

            var s = go.AddComponent<MortarShell>();
            s._from = from;
            s._to = to;
            s._damage = damage;
            s._mire = mire;
            s._radius = mire ? Tuning.Enemy.MireRadius : Tuning.Enemy.MortarBlastRadius;
            s._target = target;
            s._targetHealth = target != null ? target.GetComponent<Health>() : null;
            s._targetController = target != null ? target.GetComponent<Player.PlayerController>() : null;

            float dist = Vector2.Distance(from, to);
            s._flightSeconds = Mathf.Clamp(dist / Tuning.Enemy.MortarShellSpeed,
                                           Tuning.Enemy.MortarShellMinFlight, Tuning.Enemy.MortarShellMaxFlight);
            s._apex = Mathf.Max(Tuning.Enemy.MortarShellMinApex, dist * Tuning.Enemy.MortarShellApexPerUnit);

            // The landing ring. Drawn from the moment of firing, faint, so the player sees where
            // the shell is coming down while it is still in the air - the ground it denies has to
            // be drawn, the same rule ClusterShard states. On a child so the root stays at scale 1
            // and the travelling shell's offsets stay in world units.
            s._rings = new SpriteRenderer[RingShare.Length];
            for (int i = 0; i < s._rings.Length; i++)
            {
                var mark = new GameObject("ring" + i);
                mark.transform.SetParent(go.transform, false);
                s._rings[i] = mark.AddComponent<SpriteRenderer>();
                s._rings[i].sortingOrder = SortingOrders.Telegraph;
            }

            // The shadow tracks the shell's GROUND position and tightens as it comes down - what
            // makes a y-offset read as height rather than as the shell sliding up the screen.
            var shadow = new GameObject("shadow");
            shadow.transform.SetParent(go.transform, false);
            s._shadowSr = shadow.AddComponent<SpriteRenderer>();
            s._shadowSr.sprite = Spr.Circle;
            // Under every body (which re-base into the depth band well above this), but over
            // the floor and the pit band so a shell falling into a pit still casts one.
            s._shadowSr.sortingOrder = SortingOrders.Enemy - 1;

            var shell = new GameObject("shell");
            shell.transform.SetParent(go.transform, false);
            s._shell = shell.transform;
            s._shellSr = shell.AddComponent<SpriteRenderer>();
            s._shellSr.sortingOrder = SortingOrders.Telegraph + 2;

            s.Place(0f);
            return s;
        }

        void Update()
        {
            _clock += Time.deltaTime;

            if (!_landed)
            {
                float t = Mathf.Clamp01(_clock / _flightSeconds);
                Place(t);
                // a ball of static crackles as it flies
                if (_static != null) Paint(Mathf.FloorToInt(_clock * 20f) % 2 == 0, t);
                if (!_deflected && TryDeflect()) return;
                if (t >= 1f) Land();
                return;
            }

            if (_clock >= FuseSeconds) { Explode(); return; }

            // The fuse COUNTS out loud too - a tick per blink, so 1, 2, 3 reads off screen.
            bool blink = LitAt(_clock);
            if (blink && !_wasLit) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.FuseTick, transform.position);
            _wasLit = blink;

            Paint(LitAt(_clock), 1f);
        }

        /// <summary>
        /// A parry catching the shell in the air. Measured to where the shell is DRAWN, since
        /// that is what the player is timing against. TryParry is only asked once the shell is in
        /// reach - it spends the window, and a shell across the room must not eat a parry meant
        /// for the chaser in front of the player.
        /// </summary>
        bool TryDeflect()
        {
            if (_target == null || _targetController == null
                || _targetHealth == null || _targetHealth.IsDead) return false;

            Vector2 drawn = _shell.position;
            if (Vector2.Distance(drawn, _target.position) > Tuning.Enemy.MortarParryReach) return false;
            if (!_targetController.TryParry(drawn)) return false;

            // Back toward the side it came from: away from the player, along the line to the
            // muzzle. Falls back to reversing its travel if it was dropped straight on them.
            Vector2 ground = (Vector2)transform.position + (Vector2)_shadowSr.transform.localPosition;
            Vector2 back = _from - (Vector2)_target.position;
            if (back.sqrMagnitude < 0.0001f) back = _from - _to;
            if (back.sqrMagnitude < 0.0001f) back = Vector2.up;

            float height = _shell.localPosition.y - _shadowSr.transform.localPosition.y;

            _deflected = true;
            _from = ground;
            _to = Core.Arena.NearestFloor(ground + back.normalized * Tuning.Enemy.MortarDeflectDistance, 0.3f);
            _flightSeconds = Tuning.Enemy.MortarDeflectFlight;
            _apex = Tuning.Enemy.MortarDeflectApex;
            _startHeight = Mathf.Max(0f, height);
            _clock = 0f;

            // The root IS the landing point, so it moves; Place re-derives every child from it.
            transform.position = _to;
            Place(0f);

            return true;   // the parry draws its own sparks (PlayerController.ParryTell)
        }

        /// <summary>Ground point along the lob, the shell lifted off it by a parabola, the shadow
        /// on it. t is 0 at the muzzle and 1 on landing.</summary>
        void Place(float t)
        {
            var ground = Vector2.Lerp(_from, _to, t) - _to;
            float height = 4f * _apex * t * (1f - t) + _startHeight * (1f - t);

            _shell.localPosition = ground + new Vector2(0f, height);
            _shadowSr.transform.localPosition = ground;

            float lift = Mathf.Clamp01(_apex > 0f ? height / _apex : 0f);   // 0 on the ground, 1 at apex
            _shadowSr.transform.localScale = new Vector3(0.34f, 0.17f, 1f) * Mathf.Lerp(1f, 0.55f, lift);
            _shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.45f, 0.18f, lift));

            // The landing rings firm up as the shell comes down.
            Paint(false, t);
        }

        void Land()
        {
            _landed = true;
            _clock = 0f;
            _shell.localPosition = Vector3.zero;
            Combat.TexelFx.Puff(transform.position, Combat.TexelFx.Grey, 8, 5f, 0.35f);
        }

        void Explode()
        {
            Convergence.Audio.Sfx.Play(_mire ? Convergence.Audio.Sound.Pop : Convergence.Audio.Sound.Explosion, transform.position);
            // The mire's fuse ends in ground, not a blast - and asks no parry: there is no hit to
            // negate, and asking would spend a window the player may want for something else.
            if (_mire)
            {
                Hazards.MireField.Spawn(transform.position, transform.parent, turned: _deflected, staticRamp: _static);
                Destroy(gameObject);
                return;
            }

            Combat.FightMarks.Scorch(transform.position, Tuning.Enemy.MortarBlastRadius);
            if (_deflected) ExplodeOnEnemies();
            else if (_target != null)
            {
                var hp = _target.GetComponent<Health>();
                var pc = _target.GetComponent<Player.PlayerController>();
                if (hp != null && !hp.IsDead
                    && Vector2.Distance(_target.position, transform.position) <= Tuning.Enemy.MortarBlastRadius
                    // Same courtesy Bomb's blast extends: a perfectly timed defence negates it.
                    && (pc == null || !pc.TryParry(transform.position)))
                    hp.Take(new DamageInfo(_damage, ElementType.Fire, gameObject));
            }

            MortarBurst.Spawn(transform.position, Tuning.Enemy.MortarBlastRadius, ShellRamp, transform.parent);

            if (_bounceDistance > 0f && !_deflected && _target != null)
            {
                Vector2 here = transform.position, dir = _to - _from;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    var next = Core.Arena.Clamp(here + dir.normalized * _bounceDistance, 0.3f);
                    if (Core.Arena.OnFloor(next, 0.2f) && (next - here).sqrMagnitude > 0.25f)
                        Launch(here, next, transform.parent, _target, _damage, _magma);
                }
            }
            Destroy(gameObject);
        }

        /// <summary>The turned blast: every enemy in the radius, never the player - the same
        /// sweep a reflected EnemyProjectile makes, credited to the player so kills count.</summary>
        void ExplodeOnEnemies()
        {
            var playerGo = _target != null ? _target.gameObject : null;
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, Tuning.Enemy.MortarBlastRadius))
            {
                if (playerGo != null && col.gameObject == playerGo) continue;
                if (col.GetComponent<EnemyController>() == null) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                hp.Take(new DamageInfo(_damage, ElementType.Fire, playerGo));
            }
        }

        // ---- the fuse: . .. ... ----
        //
        // Walked rather than stored, so the pattern and its total length come from the same three
        // Tuning numbers and can't drift apart.

        const int Groups = 3;

        static float FuseSeconds
        {
            get
            {
                float at = 0f;
                for (int g = 1; g <= Groups; g++)
                {
                    at += g * Tuning.Enemy.MortarBlinkOn + (g - 1) * Tuning.Enemy.MortarBlinkGap;
                    if (g < Groups) at += Tuning.Enemy.MortarGroupGap;
                }
                return at;
            }
        }

        /// <summary>Which group of the fuse <paramref name="t"/> seconds after landing falls in -
        /// 1, 2 or 3 (each from its first blink to the next group's), 4 past the last.</summary>
        static int GroupAt(float t)
        {
            float at = 0f;
            for (int g = 1; g <= Groups; g++)
            {
                float end = at + g * Tuning.Enemy.MortarBlinkOn + (g - 1) * Tuning.Enemy.MortarBlinkGap;
                if (g < Groups) end += Tuning.Enemy.MortarGroupGap;
                if (t < end) return g;
                at = end;
            }
            return Groups + 1;
        }

        /// <summary>Whether the shell is lit at <paramref name="t"/> seconds after landing:
        /// group g is g blinks, and the blast follows the last blink of the last group.</summary>
        static bool LitAt(float t)
        {
            float at = 0f;
            for (int g = 1; g <= Groups; g++)
            {
                for (int b = 0; b < g; b++)
                {
                    if (t >= at && t < at + Tuning.Enemy.MortarBlinkOn) return true;
                    at += Tuning.Enemy.MortarBlinkOn;
                    if (b < g - 1) at += Tuning.Enemy.MortarBlinkGap;
                }
                at += Tuning.Enemy.MortarGroupGap;
            }
            return false;
        }

        // ---- the Salamander's shell, drawn ----

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _sprites = new();
        static readonly Color Crust = TexelRig.Hex("#2c221c")[0], Outline = TexelRig.Hex("#090605")[0];

        static string Key(Color[] ramp, string what) => what + ColorUtility.ToHtmlStringRGB(ramp[2]);

        /// <summary>A molten glob with a crust: hot at the heart, brighter when the fuse is lit.</summary>
        static Sprite GlobSprite(Color[] ramp, bool lit)
        {
            string key = Key(ramp, lit ? "glob.lit" : "glob");
            if (_sprites.TryGetValue(key, out var hit) && hit != null) return hit;
            const int n = 11;
            var px = new Color[n * n];
            float c = n / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                Color col = Color.clear;
                if (d < 1.5f) col = lit ? ramp[3] : ramp[2];
                else if (d < 2.6f) col = lit ? ramp[2] : ramp[1];
                else if (d < 3.6f) col = TexelRig.Hash(x, y) < 0.35f ? ramp[1] : Crust;
                else if (d < 4.6f) col = Outline;
                px[y * n + x] = col;
            }
            return _sprites[key] = Make(key, px, n);
        }

        /// <summary>One of the three landing rings: a whole ring on the texel grid, dim or lit.</summary>
        static Sprite TargetRingSprite(float radius, Color[] ramp, bool lit)
        {
            string key = Key(ramp, (lit ? "tring.lit" : "tring") + radius.ToString("0.000"));
            if (_sprites.TryGetValue(key, out var hit) && hit != null) return hit;
            float r = radius * TexelRig.TexelsPerUnit;
            int n = Mathf.CeilToInt(r) * 2 + 3;
            var px = new Color[n * n];
            float c = n / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r || d < r - 1.2f) continue;
                px[y * n + x] = lit ? ramp[3] : ramp[1];
            }
            return _sprites[key] = Make(key, px, n);
        }

        static Sprite Make(string name, Color[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, name = name,
            };
            tex.SetPixels(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
            sp.name = name;
            return sp;
        }
    }
}
