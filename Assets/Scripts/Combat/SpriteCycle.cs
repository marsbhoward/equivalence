using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>Steps a SpriteRenderer through a flipbook on scaled time - a conjured disc's
    /// combined picture (DiscPictures), whose flames and vanes animate like the held weapon's.</summary>
    public class SpriteCycle : MonoBehaviour
    {
        public Sprite[] Frames;
        public float FrameSeconds = 0.1f;

        SpriteRenderer _sr;
        float _clock;

        void Update()
        {
            if (Frames == null || Frames.Length == 0) return;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;
            _clock += Time.deltaTime;
            int i = Mathf.FloorToInt(_clock / Mathf.Max(0.01f, FrameSeconds)) % Frames.Length;
            if (Frames[i] != null) _sr.sprite = Frames[i];
        }
    }
}
