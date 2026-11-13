using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// A Brimstone Heart killed before its arming ran out (the user's call, 2026-11-12): it does
    /// not go off on the frame it dies. Where it died it stays white-hot and shaking (its
    /// BURSTING frame), the blast's ring fills out to its dotted edge (<see cref="BlastTell"/>),
    /// and when the fill reaches the edge it goes off (<see cref="EnemyController.Blast"/>) -
    /// Tuning.Enemy.BombDeathFuseSeconds, enough for a dash out. Its own object: the body is
    /// destroyed in the frame it died.
    /// </summary>
    public class BombFuse : MonoBehaviour
    {
        EnemyController.BombBlast _blast;
        BlastTell _tell;
        SpriteRenderer _sr;
        Vector3 _home;
        float _from, _seconds, _t;

        internal static void Light(in EnemyController.BombBlast blast, BombVisual visual, float from, float seconds)
        {
            var go = new GameObject("bomb.fuse");
            go.transform.SetParent(blast.Parent, true);
            go.transform.position = blast.At;
            var f = go.AddComponent<BombFuse>();
            f._blast = blast;
            f._blast.Source = go;
            f._from = Mathf.Clamp01(from);
            f._seconds = Mathf.Max(0.05f, seconds);

            // The heart where it died, white-hot.
            if (visual != null)
            {
                var vsr = visual.GetComponent<SpriteRenderer>();
                var body = new GameObject("heart");
                body.transform.SetParent(go.transform, false);
                body.transform.position = visual.transform.position;
                body.transform.localScale = visual.transform.lossyScale;
                f._home = body.transform.localPosition;
                f._sr = body.AddComponent<SpriteRenderer>();
                f._sr.sprite = visual.Frame((int)BombFrame.Bursting);
                f._sr.flipX = visual.FacingLeft;
                if (vsr != null) { f._sr.sortingLayerID = vsr.sortingLayerID; f._sr.sortingOrder = vsr.sortingOrder; }
            }

            f._tell = BlastTell.Circle(go.transform, Vector2.zero, blast.Range, BombTellColour);
            f._tell.Progress = f._from;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Hissing, blast.At);
        }

        /// <summary>The Heart's tell on the floor - its fire's orange.</summary>
        internal static readonly Color BombTellColour = new(1f, 0.5f, 0.12f, 1f);

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _seconds);
            if (_tell != null) _tell.Progress = Mathf.Lerp(_from, 1f, k);
            // Shaking harder as it goes, on whole texels.
            if (_sr != null)
            {
                float amp = Mathf.Lerp(0.5f, 1.5f, k) / TexelRig.TexelsPerUnit;
                int frame = Mathf.FloorToInt(_t * 30f);
                _sr.transform.localPosition = _home + new Vector3((TexelRig.Hash(frame, 7) - 0.5f) * 2f * amp,
                                                                  (TexelRig.Hash(7, frame) - 0.5f) * 2f * amp, 0f);
            }
            if (k < 1f) return;
            EnemyController.Blast(_blast);
            Destroy(gameObject);
        }
    }
}
