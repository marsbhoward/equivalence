using UnityEditor;
using Convergence.Art.Gear;

namespace Convergence.EditorTools
{
    // The eight-direction rig's dev switch as a menu tick, kept in EditorPrefs so it survives domain reloads and
    // entering Play. Editor only: builds never set it, so players keep today's rig.
    [InitializeOnLoad]
    static class EightDirToggle
    {
        const string Path = "Convergence/Dev/Eight Directions", Key = "Convergence.DevEightDir";

        static EightDirToggle() => PrimitiveCharacterRig.DevEightDir = EditorPrefs.GetBool(Key, false);

        [MenuItem(Path)]
        static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(Key, false);
            EditorPrefs.SetBool(Key, on);
            PrimitiveCharacterRig.DevEightDir = on;
        }

        [MenuItem(Path, true)]
        static bool Check()
        {
            Menu.SetChecked(Path, EditorPrefs.GetBool(Key, false));
            return true;
        }
    }
}
