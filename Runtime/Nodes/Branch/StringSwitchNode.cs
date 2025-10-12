using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.Graph.Core.Base;
using Xprees.Graph.Core.Base.Nodes;
using Xprees.Graph.Core.Extensions;
using Xprees.Graph.Core.Nodes.Variables;
using Xprees.Variables.Primitive;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Nodes.Branch
{
    [CreateNodeMenu("Branch/String Switch")]
    [NodeWidth(400)]
    [NodeTint("#27552E")]
    public class StringSwitchNode : BaseNode, IPassthroughNode
    {
        [Input] public GraphConnection input;

        [Header("Values to compare")]
        [Tooltip("The variable that will be used to manage flow.")]
        public StringVariable stringVariable;

        [Header("Value comparison method")]
        public StringComparisonMethod method = StringComparisonMethod.Equals;

        [Space]
        [Output(dynamicPortList = true, connectionType = ConnectionType.Override)]
        public List<StringReference> cases;

        [Output(connectionType = ConnectionType.Override)]
        public GraphConnection defaultCase;


        protected override UniTask<BaseNode> GetNextNode(CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < cases.Count; i++)
            {
                var testCase = cases[i];

                var result = method.CompareValues(stringVariable.CurrentValue, testCase.Value);
                if (!result) continue;

                var caseOutputPort = this.GetDynamicPort(nameof(cases), i);
                var caseConnectionNode = caseOutputPort?.Connection?.node;
                if (caseConnectionNode == null)
                {
                    UnityEngine.Debug.LogWarning($"Case {i} in {nameof(StringSwitchNode)} in {graph.name} is not connected to any node.");
                    continue;
                }

                return new(caseConnectionNode as BaseNode);
            }

            // Default case if no match found
            var defaultCaseNextNode = GetOutputPort(nameof(defaultCase)).Connection.node as BaseNode;
            return new(defaultCaseNextNode);
        }

        public override void ResetState()
        {
            base.ResetState();
            stringVariable?.ResetState();
        }
    }
}