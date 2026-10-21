using UnityEditor;
using Xprees.Graph.Core.Base;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Editor toggle of <see cref="GraphParserSettings.TriggerAsyncStartFirstNode"/>, kept in EditorPrefs. It never reaches player builds,
    /// flip the default in GraphParserSettings once the affected graphs are play-tested.
    [InitializeOnLoad]
    public static class GraphParserSettingsMenu
    {
        private const string menuPath = "Tools/Graph/Trigger AsyncStart first node (experimental)";
        private const string prefsKey = "Xprees.GraphParser.TriggerAsyncStartFirstNode";

        static GraphParserSettingsMenu() => Apply(EditorPrefs.GetBool(prefsKey, false));

        private static void Apply(bool value) => GraphParserSettings.TriggerAsyncStartFirstNode = value;

        [MenuItem(menuPath, priority = 200)]
        private static void Toggle()
        {
            var value = !GraphParserSettings.TriggerAsyncStartFirstNode;
            EditorPrefs.SetBool(prefsKey, value);
            Apply(value);
        }

        [MenuItem(menuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(menuPath, GraphParserSettings.TriggerAsyncStartFirstNode);
            return true;
        }
    }
}