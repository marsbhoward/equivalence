using UnityEngine;
using Convergence.Core;

namespace Convergence.Art
{
    /// <summary>
    /// Attaches an actor's visuals as a CHILD of its logic GameObject.
    ///
    /// This is the rule that makes art swappable: gameplay components (Rigidbody2D, colliders,
    /// Health, controllers, resources) stay on the root at scale 1, and everything you can see
    /// lives on a child. An artist's prefab can therefore carry its own Animator, particles and
    /// sub-sprites without colliding with gameplay code, and swapping it changes nothing else.
    /// </summary>
    public static class ArtBinder
    {
        /// <summary>
        /// Build the visual child. Falls back to the procedural placeholder when <paramref name="art"/>
        /// has nothing authored yet.
        /// </summary>
        /// <param name="fallbackSprite">Placeholder shape used when no art is set.</param>
        /// <param name="fallbackTint">Placeholder colour - ignored once real art exists.</param>
        /// <param name="fallbackHeight">World-units tall for the placeholder.</param>
        public static GameObject AttachVisual(GameObject root, ActorArt art, Sprite fallbackSprite,
                                              Color fallbackTint, float fallbackHeight, int sortingOrder)
        {
            GameObject visual;

            if (art != null && art.Prefab != null)
            {
                // Authored prefab: trust its own transform scale.
                visual = Object.Instantiate(art.Prefab, root.transform);
                visual.transform.localPosition = Vector3.zero;
                ApplyTint(visual, art.Tint);
                ApplySorting(visual, sortingOrder);
            }
            else if (art != null && art.Sprite != null)
            {
                visual = MakeSpriteChild(root, art.Sprite, art.Tint, sortingOrder);
                NormaliseHeight(visual, art.Sprite, art.WorldHeight);
            }
            else
            {
                visual = MakeSpriteChild(root, fallbackSprite, fallbackTint, sortingOrder);
                NormaliseHeight(visual, fallbackSprite, fallbackHeight);
            }

            visual.name = "visual";
            if (visual.GetComponentInChildren<ActorVisual>() == null)
                visual.AddComponent<ActorVisual>();

            return visual;
        }

        static GameObject MakeSpriteChild(GameObject root, Sprite sprite, Color tint, int sortingOrder)
        {
            var go = new GameObject("visual");
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>Scale so the sprite stands <paramref name="worldHeight"/> units tall, whatever its PPU.</summary>
        static void NormaliseHeight(GameObject visual, Sprite sprite, float worldHeight)
        {
            if (sprite == null || worldHeight <= 0f) return;
            float native = sprite.bounds.size.y;
            if (native <= 0.0001f) return;
            visual.transform.localScale = Vector3.one * (worldHeight / native);
        }

        static void ApplyTint(GameObject visual, Color tint)
        {
            if (tint == Color.white) return;
            foreach (var sr in visual.GetComponentsInChildren<SpriteRenderer>(true))
                sr.color = tint;
        }

        static void ApplySorting(GameObject visual, int order)
        {
            foreach (var sr in visual.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.sortingOrder == 0) sr.sortingOrder = order;
        }
    }
}
