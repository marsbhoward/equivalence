using System.IO;
using UnityEditor;
using UnityEngine;
using Convergence.Art;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Creates Assets/Resources/GameArt.asset pre-populated with a slot for every element, so
    /// the art catalog exists and is ready to drop files into.
    ///
    ///   unity command eval 'return Convergence.EditorTools.GameArtSetup.CreateGameArt();'
    /// or the Unity menu: Convergence &gt; Create Game Art Asset.
    ///
    /// REFUSES ONCE THE ASSET EXISTS, deliberately - it never touches a file that might hold real
    /// authored art. That means a field added to GameArt AFTER the asset already exists gets no
    /// seeded WorldHeight from here; it sits at ActorArt's own default (1) until set by hand in
    /// the Inspector. Found the hard way: this asset predated Chaser/Ranged/Turret/Dasher
    /// entirely, so every one of those slots was silently sitting at WorldHeight 1 rather than
    /// its real size the moment art landed in them - regenerated once those fields existed, but a
    /// later field added the same way needs the same care.
    /// </summary>
    public static class GameArtSetup
    {
        public const string AssetPath = "Assets/Resources/GameArt.asset";

        [MenuItem("Convergence/Create Game Art Asset")]
        public static string CreateGameArt()
        {
            if (File.Exists(AssetPath))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameArt>(AssetPath);
                return "already exists: " + AssetPath;
            }

            Directory.CreateDirectory("Assets/Resources");

            var art = ScriptableObject.CreateInstance<GameArt>();
            art.Elements = new[]
            {
                new ActorArt { Element = ElementType.Fire,  WorldHeight = 0.85f },
                new ActorArt { Element = ElementType.Water, WorldHeight = 0.85f },
                new ActorArt { Element = ElementType.Earth, WorldHeight = 0.95f },
                new ActorArt { Element = ElementType.Air,   WorldHeight = 0.80f },
            };
            // Sourced from Tuning.Enemy's own size constants rather than restated literals - the
            // placeholder circle and any real art dropped into these slots have to agree on how
            // big the thing on screen is, and a second, independent number here is exactly the
            // kind of copy that drifts the first time one of them is retuned.
            art.BombConstruct = new ActorArt { WorldHeight = Tuning.Enemy.BombSize };
            art.BombAutomaton = new ActorArt { WorldHeight = Tuning.Enemy.BombSize };
            art.ChaserConstruct = new ActorArt { WorldHeight = Tuning.Enemy.ChaserSize };
            art.ChaserAutomaton = new ActorArt { WorldHeight = Tuning.Enemy.ChaserSize };
            art.RangedConstruct = new ActorArt { WorldHeight = Tuning.Enemy.RangedSize };
            art.RangedAutomaton = new ActorArt { WorldHeight = Tuning.Enemy.RangedSize };
            art.TurretConstruct = new ActorArt { WorldHeight = Tuning.Enemy.TurretSize };
            art.TurretAutomaton = new ActorArt { WorldHeight = Tuning.Enemy.TurretSize };
            art.DasherConstruct = new ActorArt { WorldHeight = Tuning.Enemy.DasherSize };
            art.DasherAutomaton = new ActorArt { WorldHeight = Tuning.Enemy.DasherSize };

            AssetDatabase.CreateAsset(art, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = art;
            Debug.Log($"[Convergence] Created {AssetPath} - drop sprites or prefabs into its slots.");
            return AssetPath;
        }
    }
}
