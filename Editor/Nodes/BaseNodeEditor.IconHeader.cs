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

        private static GUIStyle iconStyle;
        private readonly static GUIContent iconContent = new();
        private readonly static GUIContent titleContent = new();

        private void DrawCustomIconInHeader()
        {
            scriptIcon = GetCachedScriptIcon();

            GUILayout.BeginHorizontal();

            var nodeTooltip = GetNodeTooltip();
            if (scriptIcon != null)
            {
                iconStyle ??= new GUIStyle(GUI.skin.label)
                {
                    fixedWidth = iconSize,
                    fixedHeight = iconSize,
                    padding = new RectOffset(-5, 0, 2, 0),
                    alignment = TextAnchor.MiddleLeft,
                };
                iconContent.image = scriptIcon;
                iconContent.tooltip = nodeTooltip;
                GUILayout.Label(iconContent, iconStyle);
            }

            // Draw the node title (the shared header style is adjusted in place, no new objects per paint)
            var titleStyle = NodeEditorResources.styles.nodeHeader;
            titleStyle.fontSize = headerFontSize;
            titleStyle.alignment = TextAnchor.UpperLeft;
            titleStyle.wordWrap = true;
            var padding = titleStyle.padding;
            padding.left = 0;
            padding.right = Mathf.CeilToInt(BreakpointDotLayout.ReservedHeaderWidth);
            padding.top = 4;
            padding.bottom = 0;
            titleContent.text = target.name;
            titleContent.tooltip = nodeTooltip;
            GUILayout.Label(titleContent, titleStyle);

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