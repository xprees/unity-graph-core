using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Built-in Unity icons for the debugger UI. Every lookup tries the plain name and the dark skin variant, a missing icon gives null (text only).
    public static class DebugIcons
    {
        public const float Size = 16f;

        public static Texture2D Find(params string[] names)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;

                var texture = EditorGUIUtility.FindTexture(name);
                if (!texture) texture = EditorGUIUtility.FindTexture($"d_{name}");
                if (texture) return texture;
            }

            return null;
        }

        public static Image CreateImage(Texture2D texture, float marginRight = 3f)
        {
            var image = new Image { image = texture, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            image.style.width = Size;
            image.style.height = Size;
            image.style.flexShrink = 0;
            image.style.marginRight = marginRight;
            return image;
        }

        /// Puts an icon in front of the text of a toolbar toggle. Does nothing and returns false when no icon is found.
        public static bool Decorate(Toggle toggle, params string[] iconNames)
        {
            var texture = Find(iconNames);
            if (!texture) return false;

            var input = toggle.Q(className: Toggle.inputUssClassName) ?? toggle;
            var checkmark = toggle.Q(className: Toggle.checkmarkUssClassName);
            if (checkmark != null) checkmark.style.display = DisplayStyle.None;

            input.style.flexDirection = FlexDirection.Row;
            input.style.alignItems = Align.Center;
            input.Insert(0, CreateImage(texture, string.IsNullOrEmpty(toggle.text) ? 0f : 3f));
            return true;
        }
    }
}