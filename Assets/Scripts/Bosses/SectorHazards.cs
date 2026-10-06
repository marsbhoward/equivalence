using System.Collections;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// Draws and fires the eight slices - the ANSWER half of the Cantor's cycle, and the faint
    /// print it leaves behind while CALLING.
    ///
    /// ONE RENDERER PER SLICE, HELD FOR THE FIGHT. Eight sprites already exist (see
    /// <see cref="ArenaSectors"/>), so the whole hazard layer is eight renderers whose alpha is
    /// animated - no spawning, no pooling, and nothing to leak when a phrase is cut short by the
    /// boss dying mid-note.
    ///
    /// THE DAMAGE TEST IS ANGULAR, NOT A COLLIDER. A slice is a wedge of the room and the only
    /// honest question is which wedge the player is standing in, which is one atan2 -
    /// <see cref="ArenaSectors.IndexAt"/>, the same call the boss uses to place itself. A collider
    /// shaped to the sprite would be a second description of the same wedge, free to disagree with
    /// the picture the moment either changed.
    ///
    /// It is tested ONCE, at the moment of the eruption, rather than continuously across
    /// EruptionSeconds. Standing in a slice as it goes off should cost one hit; letting it cost a
    /// hit per frame for a quarter second would make a single misread instantly lethal and would
    /// make the punishment depend on the frame rate.
    /// </summary>
    public class SectorHazards : MonoBehaviour
    {
        SpriteRenderer[] _slices;

        static readonly Color Warn = new(1f, 0.62f, 0.18f);
        static readonly Color Fire = new(1f, 0.30f, 0.16f);

        public static SectorHazards Build(Transform parent)
        {
            var go = new GameObject("boss.sectors");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            var h = go.AddComponent<SectorHazards>();
            h._slices = new SpriteRenderer[ArenaSectors.Count];

            for (int i = 0; i < ArenaSectors.Count; i++)
            {
                var s = new GameObject($"slice{i}");
                s.transform.SetParent(go.transform, false);
                var sr = s.AddComponent<SpriteRenderer>();
                sr.sprite = ArenaSectors.Shape(i);

                // On the ground, under everything that stands on it. GroundDecal rather than Fx:
                // the fight happens INSIDE these, so an enemy or the player caught in one has to
                // stay visible on top of it - the same reasoning Spr.GroundBurn already lives by.
                sr.sortingOrder = SortingOrders.GroundDecal;
                sr.color = new Color(1f, 1f, 1f, 0f);
                h._slices[i] = sr;
            }
            return h;
        }

        /// <summary>A faint print, for where the boss just stood. Purely a memory aid for the
        /// CALL - it fades well before the ANSWER so it can never be mistaken for a telegraph.</summary>
        public void Mark(int sector, Color tint, float seconds)
        {
            if (!Valid(sector)) return;
            StartCoroutine(FadeOut(_slices[sector], tint, 0.16f, seconds));
        }

        /// <summary>
        /// Light a slice, then set it off.
        ///
        /// The warning RAMPS rather than switching on, so how far through the telegraph you are is
        /// readable at a glance - a flat glow that suddenly kills gives the player a boolean where
        /// they need a clock.
        /// </summary>
        public void Erupt(int sector, float telegraph, Transform player)
        {
            if (!Valid(sector)) return;
            StartCoroutine(EruptRoutine(sector, telegraph, player));
        }

        IEnumerator EruptRoutine(int sector, float telegraph, Transform player)
        {
            var sr = _slices[sector];
            float t = 0f;
            while (t < telegraph)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, telegraph));
                if (sr != null) sr.color = new Color(Warn.r, Warn.g, Warn.b, 0.10f + k * 0.34f);
                yield return null;
            }

            if (sr != null) sr.color = new Color(Fire.r, Fire.g, Fire.b, 0.72f);

            // Tested here and only here - see the class header on why this is one hit rather than
            // one per frame.
            if (player != null && ArenaSectors.IndexAt(player.position) == sector)
            {
                var hp = player.GetComponent<Health>();
                // A MECHANIC hit: a share of the player's own max health, so a misread costs the
                // same at any depth or build. Still mitigated - Take applies Vulnerability.
                if (hp != null)
                    hp.Take(new DamageInfo(hp.Max * Tuning.Boss.EruptionFraction,
                                           ElementType.Fire, gameObject));
            }

            yield return new WaitForSeconds(Tuning.Boss.EruptionSeconds);
            if (sr != null) yield return FadeOut(sr, Fire, 0.72f, 0.35f);
        }

        IEnumerator FadeOut(SpriteRenderer sr, Color tint, float from, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (sr == null) yield break;
                float k = 1f - Mathf.Clamp01(t / seconds);
                sr.color = new Color(tint.r, tint.g, tint.b, from * k);
                yield return null;
            }
            if (sr != null) sr.color = new Color(tint.r, tint.g, tint.b, 0f);
        }

        /// <summary>Everything off, immediately. Called when the boss dies mid-phrase, so a slice
        /// cannot be left lit over a floor that is no longer a fight.</summary>
        public void Clear()
        {
            StopAllCoroutines();
            if (_slices == null) return;
            foreach (var sr in _slices)
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 0f);
        }

        bool Valid(int sector)
            => _slices != null && sector >= 0 && sector < _slices.Length && _slices[sector] != null;
    }
}
