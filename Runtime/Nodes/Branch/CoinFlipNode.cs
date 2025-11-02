using UnityEngine;
using Xprees.Graph.Core.Attributes;
using Xprees.Graph.Core.Base.Nodes;
using Random = System.Random;

namespace Xprees.Graph.Core.Nodes.Branch
{
    [NodeDescription("A branch that randomly passes or fails based on a coin flip.")]
    [NodeResizableWidth(150)]
    [CreateNodeMenu("Branch/Coin Flip Branch")]
    public class CoinFlipNode : BranchBaseNode
    {
        [Tooltip("The chance to pass the branch (0.1 = 10%, 0.99 = 99%)")]
        [Range(0.1f, .99f)]
        public float passChance = 0.5f;

        private readonly Random _random = new();
        protected override bool GetConditionResult() => _random.NextDouble() >= 1f - passChance;
    }
}