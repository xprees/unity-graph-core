using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using XNodeEditor;
using Xprees.Graph.Core.Editor.Layout;

namespace Xprees.Graph.Core.Editor.Toolbar
{
    /// Toolbar part of the layout commands: "Arrange" button plus a dropdown with Align / Distribute.
    public static partial class GraphEditorToolbar
    {
        private static VisualElement CreateLayoutGroup(NodeEditorWindow window)
        {
            var compound = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0 } };

            var binding = GraphLayoutShortcuts.ArrangeBinding;
            var arrange = CreateButton(null, "Arrange",
                "Arrange the selected nodes along the flow (left -> right, branches fan out, one lane per start). Selecting a group arranges its contents and fits the group. Other nodes never move: the arranged block moves to free space instead. One undo step."
                + (binding.Length > 0 ? $" ({binding})" : string.Empty),
                () => GraphLayoutCommands.Run(window, LayoutCommand.Arrange));
            arrange.style.marginRight = 0;
            var icon = DebugIcons.Find("HorizontalLayoutGroup Icon", "GridLayoutGroup Icon");
            if (icon)
            {
                arrange.Insert(0, DebugIcons.CreateImage(icon));
                MarkCollapsibleText(arrange);
            }

            compound.Add(arrange);

            var menuButton = new ToolbarButton
            {
                tooltip = "Layout menu: arrange, align (left / right / top / bottom / centers) and distribute the selected nodes.",
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 0, paddingLeft = 2, paddingRight = 2 }
            };
            AddChevron(menuButton, 0f);
            menuButton.clicked += () =>
            {
                var menu = new GenericMenu();
                GraphLayoutMenu.AddItems(menu, window, string.Empty);
                menu.DropDown(menuButton.worldBound);
            };
            compound.Add(menuButton);
            return compound;
        }
    }
}