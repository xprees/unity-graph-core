using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Refactor;

namespace Xprees.Graph.Core.Editor.Nodes
{
    [CustomNodeEditor(typeof(BaseNode))]
    public partial class BaseNodeEditor : NodeEditor
    {
        private readonly static Dictionary<Type, Dictionary<Type, Attribute>> attributeCache = new();
        private readonly static object cacheLock = new();

        public override void OnHeaderGUI()
        {
            using var marker = GraphDebugMarkers.NodeHeader.Auto();
            BeginDebugVisuals();
            DrawCustomIconInHeader();
            var headerRect = GUILayoutUtility.GetLastRect();
            DrawBreakpointDot();
            if (Event.current.type == EventType.MouseUp
                && !BreakpointDotLayout.LocalHit(GetWidth(), window.zoom).Contains(Event.current.mousePosition))
            {
                HandleHeaderDoubleClickToEnterGraph(headerRect);
            }

            DrawRuntimeStateLine();
        }

        public override void AddContextMenuItems(GenericMenu menu)
        {
            AddTopLevelBreakpointItems(menu);
            base.AddContextMenuItems(menu);
            SubGraphExtraction.AddMenuItem(menu, target);
            AddDebugContextMenuItems(menu);
        }

        public override void OnBodyGUI()
        {
            using var marker = GraphDebugMarkers.NodeBody.Auto();
            try
            {
                if (IsLodCollapsed())
                {
                    using (GraphDebugMarkers.CollapsedBody.Auto()) DrawCollapsedBody();
                }
                else
                {
                    using (GraphDebugMarkers.BodyFields.Auto()) DrawBodyFields();
                    using (GraphDebugMarkers.EnterButtons.Auto()) DrawEnterGraphButtons();
                }

                using (GraphDebugMarkers.Resizing.Auto()) HandleResizingIfAttributePresent();
            }
            finally
            {
                EndDebugVisuals();
            }
        }

        /// Get the attribute of type TAttribute for target node or null if not present.
        protected static TAttribute GetTargetAssignedAttribute<TAttribute>(Node target) where TAttribute : Attribute
        {
            if (target == null) return null;

            using var marker = GraphDebugMarkers.NodeAttributes.Auto();
            var targetType = target.GetType();
            var attributeType = typeof(TAttribute);

            lock (cacheLock)
            {
                if (attributeCache.TryGetValue(targetType, out var attributesForType) &&
                    attributesForType.TryGetValue(attributeType, out var cachedAttribute))
                {
                    return cachedAttribute as TAttribute;
                }
            }

            var typeWithAttribute = TypeCache.GetTypesWithAttribute<TAttribute>()
                .FirstOrDefault(t => t.IsAssignableFrom(targetType));
            if (typeWithAttribute == null) return null;

            var attribute = Attribute.GetCustomAttribute(typeWithAttribute, typeof(TAttribute)) as TAttribute;
            lock (cacheLock)
            {
                if (!attributeCache.TryGetValue(targetType, out var attributesForType))
                {
                    // Create a new dictionary for attributes of this target type if it doesn't exist
                    attributesForType = new Dictionary<Type, Attribute>();
                    attributeCache[targetType] = attributesForType;
                }

                // Add the attribute to the cache
                attributesForType[attributeType] = attribute;
            }

            return attribute;
        }

    }
}