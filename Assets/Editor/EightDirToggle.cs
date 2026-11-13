using UnityEditor;
using Convergence.Art.Gear;

namespace Convergence.EditorTools
{
    // The eight-direction rig's switch as a menu tick, kept in EditorPrefs so it survives domain reloads and
    // entering Play. On by default (2026-11-13); builds never read it, so players always get the eight directions.
    [InitializeOnLoad]
    static class EightDirToggle
    {
        const string Path = "Convergence/Dev/Eight Directions", Key = "Convergence.DevEightDir";

        static EightDirToggle() => PrimitiveCharacterRig.DevEightDir = EditorPrefs.GetBool(Key, true);

        [MenuItem(Path)]
        static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(Key, true);
            EditorPrefs.SetBool(Key, on);
            PrimitiveCharacterRig.DevEightDir = on;
        }

        [MenuItem(Path, true)]
        static bool Check()
        {
            Menu.SetChecked(Path, EditorPrefs.GetBool(Key, true));
            return true;
        }
    }
}
