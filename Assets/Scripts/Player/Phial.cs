using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// THE PHIAL - the run's healing, carried and drunk (Tuning.Phial; the user's calls, 2026-11-03).
    /// A tap drinks one charge: the player is held for <see cref="Tuning.Phial.DrinkSeconds"/> (no
    /// moving, attacking, release or defensive - PlayerController.Drinking), the heal lands at the
    /// gulp, and a blow before it SPILLS the charge. Charges are the run's (captured with the player,
    /// PlayerController.CaptureRun); the tier is the character's (CharacterProfile.PhialTier).
    /// </summary>
    public class Phial : MonoBehaviour
    {
        PlayerController _pc;
        public int Tier { get; private set; }
        public int Charges { get; private set; }
        public int MaxCharges => Tuning.Phial.ChargesFor(Tier);
        public bool Drinking => _drinkT >= 0f;

        float _drinkT = -1f;
        bool _gulped;
        SpriteRenderer _flask;

        public static Phial Attach(PlayerController pc, int tier)
        {
            var p = pc.gameObject.AddComponent<Phial>();
            p._pc = pc;
            p.Tier = Tuning.Phial.Clamp(tier);
            p.Charges = p.MaxCharges;
            pc.Phial = p;
            if (pc.Health != null) pc.Health.Damaged += p.OnHit;
            Controls.PhialCharges = () => p != null ? p.Charges : 0;
            Controls.PhialReady = () => p != null && p.CanDrink;
            return p;
        }

        void OnDestroy()
        {
            if (_pc != null && _pc.Health != null) _pc.Health.Damaged -= OnHit;
        }

        /// <summary>A resumed run's charges (-1, from before the phial, is full).</summary>
        public void SetCharges(int charges) => Charges = charges < 0 ? MaxCharges : Mathf.Clamp(charges, 0, MaxCharges);

        /// <summary>One charge back - each boss floor's clear, and the restore ring's (GameBootstrap).
        /// False when already full.</summary>
        public bool AddCharge()
        {
            if (Charges >= MaxCharges) return false;
            Charges++;
            return true;
        }

        bool CanDrink =>
            Charges > 0 && !Drinking && _pc != null && _pc.Health != null && !_pc.Health.IsDead
            && _pc.Health.Current < _pc.Health.Max - 0.5f
            && !_pc.AttackLocked && !_pc.ArtCommitted && !_pc.Dashing && !_pc.Statue && !_pc.Petrified;

        void Update()
        {
            if (_pc == null || GamePause.IsPaused) return;
            if (!Drinking)
            {
                if (Controls.PhialTapped && CanDrink) Begin();
                return;
            }

            _drinkT += Time.deltaTime;
            if (_flask != null)   // tipped up as it is drunk
                _flask.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Lerp(0f, 150f, Mathf.Clamp01(_drinkT / Tuning.Phial.GulpSeconds)) * (_pc.Facing.x >= 0f ? 1f : -1f));
            if (!_gulped && _drinkT >= Tuning.Phial.GulpSeconds) Gulp();
            if (_drinkT >= Tuning.Phial.DrinkSeconds) End();
        }

        void Begin()
        {
            Charges--;
            _drinkT = 0f;
            _gulped = false;
            _pc.Stun(Tuning.Phial.DrinkSeconds);   // held: no moving, turning or attacking
            ShowFlask(true);
            Audio.Sfx.Play(Audio.Sound.Bubbling, (Vector2)transform.position, 0.8f);
        }

        void Gulp()
        {
            _gulped = true;
            float heal = _pc.Health.Max * Tuning.Phial.HealFor(Tier)
                       * Art.Gear.StatPercents.Apply(1f, _pc.Stats.HealReceived);
            _pc.Health.Heal(heal);
            Chain.Service.Telemetry.Drink(spilled: false);
            var feet = ReleaseCanvas.Feet(transform);
            var rig = GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            float height = rig != null ? PlayerController.HeadTop(rig).y - feet.y + 0.2f : 1.2f;
            TexelFx.Motes(feet, TexelFx.Heal, height, ReleaseCanvas.UnderBody(transform));
            ShowFlask(false);
        }

        /// <summary>A blow before the gulp spills the charge - drinking beside a mob is the mistake.
        /// Ticks and the run's own prices don't.</summary>
        void OnHit(DamageInfo d)
        {
            if (!Drinking || _gulped || d.Tick || d.Price || d.Amount <= 0f) return;
            Chain.Service.Telemetry.Drink(spilled: true);
            ShowFlask(false);
            TexelFx.Motes(ReleaseCanvas.Feet(transform), TexelFx.Heal, 0.4f, ReleaseCanvas.UnderBody(transform), 8);
            Audio.Sfx.Play(Audio.Sound.Rattle, (Vector2)transform.position, 0.7f);
            End();
        }

        void End() => _drinkT = -1f;

        // ---------------------------------------------------------------- the flask in hand

        void ShowFlask(bool on)
        {
            if (!on) { if (_flask != null) _flask.enabled = false; return; }
            if (_flask == null)
            {
                var go = new GameObject("phial.flask");
                go.transform.SetParent(transform, false);
                _flask = go.AddComponent<SpriteRenderer>();
                _flask.sprite = FlaskSprite(Tier);
            }
            var rig = GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            Vector2 head = rig != null ? PlayerController.HeadTop(rig) : (Vector2)transform.position + Vector2.up;
            _flask.transform.position = head + new Vector2(_pc.Facing.x >= 0f ? 0.12f : -0.12f, -0.12f);
            _flask.transform.localRotation = Quaternion.identity;
            var body = rig != null ? rig.GetComponentInChildren<SpriteRenderer>() : null;
            if (body != null) { _flask.sortingLayerID = body.sortingLayerID; _flask.sortingOrder = body.sortingOrder + 50; }
            _flask.enabled = true;
        }

        static Sprite[] _sprites;

        /// <summary>A placeholder flask, 6x9 texels at the body's density: a stoppered neck and a
        /// round belly of red draught, the glass's rim in the phial's tier.</summary>
        static Sprite FlaskSprite(int tier)
        {
            if (_sprites == null || _sprites.Length != Tuning.Phial.Charges.Length) _sprites = new Sprite[Tuning.Phial.Charges.Length];
            tier = Tuning.Phial.Clamp(tier);
            if (_sprites[tier] != null) return _sprites[tier];
            string[] rows =
            {
                "..SS..",
                "..GG..",
                ".G..G.",
                "GRRRRG",
                "GRRRRG",
                "GRrRRG",
                "GRRRRG",
                ".GRRG.",
                "..GG..",
            };
            Color32 rim = tier switch
            {
                0 => new Color32(176, 112, 64, 255),
                1 => new Color32(200, 204, 212, 255),
                _ => new Color32(230, 190, 80, 255),
            };
            var tex = new Texture2D(6, rows.Length) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[6 * rows.Length];
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < 6; x++)
            {
                char c = rows[rows.Length - 1 - y][x];
                px[y * 6 + x] = c switch
                {
                    'G' => rim,
                    'S' => new Color32(110, 78, 52, 255),
                    'R' => new Color32(196, 36, 48, 255),
                    'r' => new Color32(255, 140, 140, 255),
                    _ => new Color32(0, 0, 0, 0),
                };
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _sprites[tier] = Sprite.Create(tex, new Rect(0, 0, 6, rows.Length), new Vector2(0.5f, 0f), 75f);
        }
    }
}
