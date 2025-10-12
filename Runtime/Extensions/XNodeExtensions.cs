using XNode;

namespace Xprees.Graph.Core.Extensions
{
    public static class XNodeExtensions
    {
        // https://github.com/Siccity/xNode/wiki/Dynamic-Port-List#getting-the-ports
        public static NodePort GetDynamicPort(this Node node, string fieldName, int portIndex) =>
            node.GetPort($"{fieldName} {portIndex}");
    }
}