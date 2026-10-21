namespace Xprees.Graph.Core.Base
{
    /// Global behaviour switches of <see cref="GraphParserBase"/>. Defaults keep the historical behaviour.
    public static class GraphParserSettings
    {
        /// When true, the first node connected to an async start node is triggered like every other node of the flow.
        /// Historically the flow parser was only positioned on it and its own Trigger never ran (the node was only "moved from").
        /// Off by default until the affected graphs are play-tested. In the Editor it is toggled by Tools/Graph/Trigger AsyncStart first node.
        public static bool TriggerAsyncStartFirstNode = false;
    }
}