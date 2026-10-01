using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using Xprees.Graph.Core.Base.Nodes;
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
            DrawCustomIconInHeader();
            if (Event.current.type == EventType.MouseUp) HandleHeaderDoubleClickToEnterGraph(GUILayoutUtility.GetLastRect());
        }

        public override void AddContextMenuItems(GenericMenu menu)
        {
            base.AddContextMenuItems(menu);
            SubGraphExtraction.AddMenuItem(menu, target);
        }

        public override void OnBodyGUI()
        {
            base.OnBodyGUI();
            DrawEnterGraphButtons();
            HandleResizingIfAttributePresent();
        }

        /// Get the attribute of type TAttribute for target node or null if not present.
        protected static TAttribute GetTargetAssignedAttribute<TAttribute>(Node target) where TAttribute : Attribute
        {
            if (target == null) return null;

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