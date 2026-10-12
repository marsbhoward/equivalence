using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// Owns one floor's generated scenery textures and sprites (Art.Scenery) and destroys them
    /// with the dressing - a serialized list, so a domain reload doesn't orphan them.
    /// </summary>
    public class SceneryOwned : MonoBehaviour
    {
        [SerializeField] List<Object> _made = new();

        public void Add(Object o) => (_made ??= new List<Object>()).Add(o);

        void OnDestroy()
        {
            if (_made == null) return;
            foreach (var o in _made) if (o != null) Destroy(o);
            _made.Clear();
        }
    }
}
