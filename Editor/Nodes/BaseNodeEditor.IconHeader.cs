using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNodeEditor;
using Xprees.Graph.Core.Attributes;

namespace Xprees.Graph.Core.Editor.Nodes
{
    public partial class BaseNodeEditor
    {
        private readonly static Dictionary<Type, Texture2D> iconCache = new();

        private const int iconSize = 42;
        private const int headerFontSize = 17;

        private Texture2D scriptIcon = null;

        private void DrawCustomIconInHeader()
        {
            scriptIcon = GetCachedScriptIcon();

            GUILayout.BeginHorizontal();

            var nodeTooltip = GetNodeTooltip();
            if (scriptIcon != null)
            {
                var iconStyle = new GUIStyle(GUI.skin.label)
                {
                    fixedWidth = iconSize,
                    fixedHeight = iconSize,
                    padding = new RectOffset(-5, 0, 2, 0),
                    alignment = TextAnchor.MiddleLeft,
                };
                GUILayout.Label(new GUIContent(scriptIcon, nodeTooltip), iconStyle);
            }

            // Draw the node title
            var titleStyle = NodeEditorResources.styles.nodeHeader;
            titleStyle.fontSize = headerFontSize;
            titleStyle.alignment = TextAnchor.UpperLeft;
            titleStyle.wordWrap = true;
            titleStyle.padding = new RectOffset(0, 0, 4, 0);
            GUILayout.Label(new GUIContent(target.name, nodeTooltip), titleStyle);

            GUILayout.EndHorizontal();
        }

        /// This method can be overridden to provide a custom tooltip for the node header. Null by default -> turns off tooltip.
        protected string GetNodeTooltip()
        {
            var nodeDescriptionAttribute = GetTargetAssignedAttribute<NodeDescriptionAttribute>(target);
            if (nodeDescriptionAttribute == null) return null;

            return nodeDescriptionAttribute.description;
        }

        private Texture2D GetCachedScriptIcon()
        {
            var nodeType = target.GetType();
            if (!iconCache.ContainsKey(nodeType))
            {
                iconCache[nodeType] = GetScriptThumbnail(); // Use thumbnail for better performance
            }

            return iconCache[nodeType];
        }

        private Texture2D GetScriptThumbnail()
        {
            var nodeType = target.GetType();
            var thumbnail = AssetPreview.GetMiniTypeThumbnail(nodeType);
            if (thumbnail == null ||
                thumbnail.name.Contains("TextAsset Icon") ||
                thumbnail.name.Contains("cs Script Icon") ||
                thumbnail.name.Contains("ScriptableObject Icon"))
            {
                return null;
            }

            return thumbnail;
        }
    }
}