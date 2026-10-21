#if UNITY_EDITOR
using System.Globalization;
using UnityEngine;
using Xprees.Graph.Core.Debugging;

namespace Xprees.Graph.Core.Nodes.DebugState
{
    /// Text helpers shared by the IGraphNodeDebugState implementations of the nodes.
    public static class NodeDebugFormat
    {
        public static string Num(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        public static string Obj(Object obj) => obj ? obj.name : "none";

        public static string Val<T>(T value) => value == null ? "null" : value.ToString();

        /// Seconds the instance spent in the node so far.
        public static double Elapsed(in NodeDebugContext ctx) => Time.timeAsDouble - ctx.EnteredTime;
    }
}
#endif