using System;
using UnityEngine;
using UnityEngine.Serialization;
using Convergence.Core;

namespace Convergence.Art
{
    /// <summary>
    /// The single seam between authored art and the code-built scene.
    ///
    /// The scene deliberately holds no asset references (so the Unity CLI can regenerate it),
    /// which means art cannot be dragged onto a GameObject. Instead this one ScriptableObject
    /// lives at Assets/Resources/GameArt.asset and is loaded by path at runtime.
    ///
    /// EVERY FIELD IS OPTIONAL. A null field falls back to the procedural placeholder, so art
    /// can be added one piece at a time without ever breaking the build.
    /// </summary>
    [CreateAssetMenu(menuName = "Convergence/Game Art", fileName = "GameArt")]
    public class GameArt : ScriptableObject
    {
        public const string ResourcePath = "GameArt";

        [Header("Player - one entry per element")]
        public ActorArt[] Elements = Array.Empty<ActorArt>();

        [Header("Enemies")]
        // Every kind now has TWO coexisting looks rather than one slot - see Enemies.EnemyLooks.
        // Both looks of a kind are the same enemy; the floor decides which is on screen, never a
        // stat or a behaviour.
        [FormerlySerializedAs("Enemy")]
        public ActorArt BombConstruct = new();
        public ActorArt BombAutomaton = new();
        public ActorArt ChaserConstruct = new();
        public ActorArt ChaserAutomaton = new();
        public ActorArt RangedConstruct = new();
        public ActorArt RangedAutomaton = new();
        public ActorArt TurretConstruct = new();
        public ActorArt TurretAutomaton = new();
        public ActorArt DasherConstruct = new();
        public ActorArt DasherAutomaton = new();
        public ActorArt GargoyleConstruct = new();
        public ActorArt GargoyleAutomaton = new();
        public ActorArt BoosterConstruct = new();
        public ActorArt BoosterAutomaton = new();
        public ActorArt BubblesConstruct = new();
        public ActorArt BubblesAutomaton = new();
        public ActorArt MortarConstruct = new();
        public ActorArt MortarAutomaton = new();

        [Header("Hazards")]
        public ActorArt ColumnSmall = new();
        public ActorArt ColumnMedium = new();
        public ActorArt ColumnLarge = new();
        public ActorArt ForceField = new();
        public ActorArt LavaTile = new();

        [Header("Arena")]
        [Tooltip("Leave null for the flat placeholder floor.")]
        public Sprite FloorSprite;
        [Tooltip("Tiles the floor sprite across the arena instead of stretching one copy.")]
        public bool TileFloor;
        public float FloorTileSize = 2f;
        public Sprite WallSprite;
        [Tooltip("Hides the placeholder grid lines - turn on once a real floor exists.")]
        public bool HideGrid;

        [Header("Effects")]
        [Tooltip("Used for ability bursts (the expanding ring).")]
        public Sprite BurstSprite;
        [Tooltip("Used for hit sparks (the filled puff).")]
        public Sprite HitSprite;

        static GameArt _instance;

        /// <summary>Never null. Returns an all-defaults instance when no asset exists yet.</summary>
        public static GameArt I
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameArt>(ResourcePath);
                if (_instance == null)
                {
                    _instance = CreateInstance<GameArt>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return _instance;
            }
        }

        public ActorArt ForElement(ElementType element)
        {
            foreach (var e in Elements)
                if (e != null && e.Element == element) return e;
            return null;
        }
    }

    /// <summary>Art for one actor. Prefab wins over Sprite; both may be left empty.</summary>
    [Serializable]
    public class ActorArt
    {
        [Tooltip("Only meaningful in the Elements list.")]
        public ElementType Element;

        [Tooltip("Animated actor. Instantiated as a VISUAL CHILD - keep gameplay components off it. " +
                 "Its own transform scale is used as authored.")]
        public GameObject Prefab;

        [Tooltip("PLAYER ONLY. A PSD-imported, bone-rigged character prefab with a SpriteLibrary " +
                 "and SpriteResolvers. Replaces the procedural placeholder paper-doll. See ART.md.")]
        public GameObject RigPrefab;

        [Tooltip("Static sprite. Ignored when Prefab is set.")]
        public Sprite Sprite;

        [Tooltip("Sprite path only: scales the sprite to this many world units tall.")]
        public float WorldHeight = 1f;

        [Tooltip("Multiplied into the renderer colour. Keep white for finished art; the " +
                 "placeholders rely on the element tint.")]
        public Color Tint = Color.white;

        public bool HasArt => Prefab != null || Sprite != null;
    }
}
