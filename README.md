# Graph-Core package - `cz.xprees.graph-core`

This package provides the core functionality for the xprees Graph system, which is based on [Siccity/xNode](https://github.com/Siccity/xNode) node
system.

We used it to create:

- Dialog system
- Quest system
- Email conversation tree
- And more...

## Features

- **Runtime node system** - Allows for creating and managing no-code solutions in Unity.
- **Graph editor** - Provides a visual editor for creating and managing graphs.
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
                "com.github.siccity.xnode",
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

### Git

TBA

### Scoped registry - NPM package

TBA

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
