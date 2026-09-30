# Graph-Core package - `cz.xprees.graph-core`

# [![NPM Version](https://img.shields.io/npm/v/cz.xprees.npc)](https://www.npmjs.com/package/cz.xprees.graph-core)


This package provides the core functionality for the xprees Graph system, which is based on [Siccity/xNode](https://github.com/Siccity/xNode) node
system.

We used it to create:

- Dialog system
- Quest system
- Email conversation tree
- And more...

## Features

- **Runtime node system** - Allows for creating and managing no-code solutions in Unity.
- **Graph editor** - Provides a visual editor for creating and managing graphs, with a toolbar, breadcrumb navigation and a graph validator.
- **Preset of most used nodes** - Includes a set of commonly used nodes for quick setup.
- **Easily extensible** - Supports simple extension of existing nodes, graphs, graph-parsers, etc.

<details open>

<summary>Usage example</summary>

![graph-example.png](Documentation%7E/Images/graph-example.png)
![dialog-example.png](Documentation%7E/Images/dialog-example.png)

</details>

## Installation

Add to your Unity project following **OpenUPM** and **xprees-NPM** scoped registries. So you can install the package with the all dependencies
automatically with [Unity Package Manager](https://docs.unity3d.com/6000.1/Documentation/Manual/upm-scoped.html).

Either do it manually or by using the Unity Package Manager UI.
`Packages/manifest.json`

```json
{
    "scopedRegistries": [
        {
            "name": "OpenUPM",
            "url": "https://package.openupm.com",
            "scopes": [
                "com.cysharp.unitask",
                "com.github.siccity",
                "com.dbrizov.naughtyattributes"
            ]
        },
        {
            "name": "xprees-NPM",
            "url": "https://registry.npmjs.org",
            "scopes": [
                "cz.xprees"
            ]
        }
    ]
}
```

After adding the scoped registries, you can install the package easily via the Unity Package Manager UI by searching for `cz.xprees.graph-core`.

### Git URL

Install in the package manager using the following Git URL

```git
https://github.com/xprees/unity-graph-core.git
```

## Usage

For the basics of node systems, refer to the [Siccity/xNode wiki](https://github.com/Siccity/xNode/wiki)

1. Create your graph by extending the `Graph` class.
2. Create your graph parser by extending the `GraphParser` class.
3. Create your nodes by extending the `BaseNode`, `SingleOutputBaseNode` class.
    - Adding `IPassthrougNode` interface will mark the node for the parser to **directly go-on to next node** after triggering the _PassThrough_ one.
      Otherwise, the parser will wait for the next `MoveNext()` invocation.

### Building a graph

1. Each graph must have a **One** `Start` node (Single entry point), and can have **Many** `End` nodes.
2. Each "flow" in the graph should end with an `EndNode`.
3. You can create custom nodes which are triggered by outside code, such as events, which can then be used in asynchronous flows.

## Graph editor tools

All editor tools live in the `cz.xprees.graph-core.editor` assembly and work for every `GraphBase` graph (scenarios, dialogs, chats, ...).

### Toolbar

A toolbar is shown on top of the node editor window:

| Item                   | Description                                                                                                                      |
|------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| **Breadcrumbs** (left) | Path of graphs you entered, e.g. `Scenario > Setup SubGraph > Dialog`. Click a crumb to go back to that graph (the tail is cut). |
| **Frame All**          | Centers the view on all nodes and zooms to fit them into the window.                                                             |
| **Validate**           | Validates the shown graph and displays live `OK` / `N warnings` / `N errors`. Click to open the validator scoped to this graph.  |
| **Validator**          | Opens the [Graph Validator](#graph-validator) window.                                                                            |

Opening a graph (from the Project window or the validator) automatically fits all its nodes into the view.

### Breadcrumbs and entering subgraphs

- **Entering a graph** - Nodes referencing another graph (`SubGraphNode.subGraph`, nodes with a `DialogGraph` field, ...) can be entered by
  **double-clicking the node header** or with the **Open &lt;graph&gt;** button in the node. The graph is added to the breadcrumbs.
- **Opening from the Project window** - When you double-click the asset of a graph **referenced by the graph you are currently in**, it is added as a
  child in the breadcrumbs. Any other graph starts a new trail.
- **Going back up** - Click a crumb, or use the **Go To Parent Graph** shortcut (default `Alt + Up Arrow`, active when the node editor is focused).
  You land on the node you entered the child from, and it is highlighted. Rebind the shortcut in `Edit > Shortcuts... > Xprees/Graph`.
- The trail survives script recompilation (kept in `SessionState`). Code access: `GraphNavigation.EnterGraph`, `NavigateTo`, `GoToParent`.

### Graph Validator

Menu: `Tools > Graphs > Graph Validator`. Scans all graphs in the project (**Scan Project**) or the selected ones (**Scan Selection**) for:

| Check             | Severity | Description                                                                                                                                  |
|-------------------|----------|----------------------------------------------------------------------------------------------------------------------------------------------|
| Broken reference  | Error    | Connection to a missing node or port, missing (null) node entries, node of another graph. Broken entries on *input* ports are only warnings. |
| Lost edge         | Error    | An input remembers a link that its output doesn't (the flow stops at the output, the edge is invisible).                                     |
| Stale / one-sided | Warning  | One-sided link the output side knows about, or a stale input entry whose output is connected elsewhere.                                      |
| Override port     | Error    | `Override` port with more than one connection (only the first one is used).                                                                  |
| Dead end          | Warning  | Unconnected flow output (not on `End` nodes) or unconnected input.                                                                           |
| Unreachable node  | Warning  | Node not reachable from the `Start` node or any async start node (notes and groups are ignored).                                             |

Every issue has **Ping** and **Open** - opens the graph, selects the node, centers the view on it and pulses a highlight around it.

Fixes are applied with Undo support:

- **Fix All Safe** applies only fixes that **don't change what runs** - removing broken/stale entries.
- **Restore edge** (lost edge) changes the runtime flow, so it is only offered per issue for review.

> [!WARNING]
> **Do not fix or save graphs while scripts fail to compile.** When Unity loads a graph whose node scripts can't compile, references to those
> nodes are set to `null` *in memory* (the asset on disk is fine and is not marked dirty). The next save (any edit or xNode autosave) writes the
> damage to disk. If the validator reports many errors right after a compile error, **restart Unity without saving** and scan again.

### Creating nodes (searchable tree)

Right-click on empty grid space (or drop a dragged connection on empty space) opens a popup with a search field and a tree of node types grouped by
their `[CreateNodeMenu]` path. Right-click on nodes/ports keeps xNode's regular menus.

- Type to search: words match the node name, menu path and `[NodeDescription]` text (all words must match); results are listed flat with their folder.
- Up/Down moves the selection (the search field keeps the focus, so you can keep typing), Left/Right collapse/expand folders (when the search is
  empty), Enter creates the selected node (or toggles a folder), Esc closes. Clicking a node also creates it at the cursor (and auto-connects a
  dragged port).
- Node icons come from the script icon; the `[NodeDescription]` is shown as a tooltip.
- Nodes marked with `[PinnedNode(order = n)]` (End, Group, Note) are also listed in a **Pinned** section (star icon) at the top of the empty-search
  tree.
- The **+** button in front of the breadcrumbs opens the same popup and creates the node in the center of the view (no auto-connect).
- The search is always empty when the popup opens; folder expansion is remembered.
- Node types that reached their `[DisallowMultipleNodes]` limit (e.g. Start) stay listed but greyed out with the reason and can't be created.
- Right-clicking the body of a Group node (where no other node is) opens the same popup; the group title keeps xNode's node menu.

#### Restricting nodes to graph types

By default a node is available in every graph. Mark the node class to change that (graph types match their subclasses, the rules are inherited by
derived nodes):

```csharp
[AllowOnlyInGraphs(typeof(DialogGraph))]         // offered only in DialogGraph (and subclasses)
public class DialogNode : BaseNode { }

[ExcludeFromGraphs(typeof(ScenarioSO))]          // offered everywhere except ScenarioSO
public class SomeNode : BaseNode { }
```

`ExcludeFromGraphs` wins over `AllowOnlyInGraphs`. Disallowed types are hidden from the tree, and the Graph Validator reports nodes of a disallowed
type already present in a graph as a warning (no auto-fix). Pasting skips nodes of disallowed types (they stay in the copy buffer for other graphs).

### xNode rendering fixes

`GraphBaseEditor` works around issues of the bundled xNode version:

- **Edges missing until you zoom** - xNode draws edges from port positions cached by a previous repaint and never caches ports of nodes outside
  the view. Missing positions are estimated and a repaint is requested, so all edges are drawn right away.
- **Strip above the toolbar** - xNode pads its GUI by a hardcoded value that is smaller than the tab strip in Unity 6. The toolbar is stretched up
  to cover the difference.

### Extending

- Add editor behaviour for your nodes by deriving from `BaseNodeEditor` (`[CustomNodeEditor(typeof(MyNode))]`) - the header double-click and the
  Open button work for every `BaseNode` with a `NodeGraph` field, including your own nodes.
- Graph relations are resolved by `GraphReferences.GetReferencedGraphs(node)`, which finds public or `[SerializeField]` fields of type `NodeGraph`.
- `GraphValidator.Validate(graph)` is read-only and returns a list of `GraphIssue`; it can be used from your own tests or build checks.
