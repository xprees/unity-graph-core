using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Xprees.Graph.Core.Editor.Debugger;
using Xprees.Graph.Core.Editor.Toolbar;

namespace Xprees.Graph.Core.Tests
{
    public class DebugPanelRound2Tests
    {
        private enum Sample
        {
            A,
        }

        [TestCase("short", 10, "short")]
        [TestCase("exactlyten", 10, "exactlyten")]
        [TestCase("Business_WaitForSound_Done", 12, "Busine…_Done")]
        [TestCase("abcdefghij", 5, "ab…ij")]
        [TestCase("abcdefghij", 1, "…")]
        public void MiddleEllipsis_KeepsStartAndEnd(string text, int max, string expected)
        {
            var result = DebuggerText.MiddleEllipsis(text, max);
            Assert.AreEqual(expected, result);
            Assert.LessOrEqual(result.Length, Mathf.Max(max, 1));
        }

        [Test]
        public void MiddleEllipsis_NullAndEmpty_AreReturnedAsIs()
        {
            Assert.IsNull(DebuggerText.MiddleEllipsis(null, 5));
            Assert.AreEqual(string.Empty, DebuggerText.MiddleEllipsis(string.Empty, 5));
        }

        [Test]
        public void Classify_ValueKinds()
        {
            Assert.AreEqual(VariableAccess.ValueKind.None, VariableAccess.Classify(null));
            Assert.AreEqual(VariableAccess.ValueKind.Primitive, VariableAccess.Classify(typeof(bool)));
            Assert.AreEqual(VariableAccess.ValueKind.Primitive, VariableAccess.Classify(typeof(string)));
            Assert.AreEqual(VariableAccess.ValueKind.Primitive, VariableAccess.Classify(typeof(float)));
            Assert.AreEqual(VariableAccess.ValueKind.Enum, VariableAccess.Classify(typeof(Sample)));
            Assert.AreEqual(VariableAccess.ValueKind.UnityObject, VariableAccess.Classify(typeof(ScriptableObject)));
            Assert.AreEqual(VariableAccess.ValueKind.UnityObject, VariableAccess.Classify(typeof(Transform)));
            Assert.AreEqual(VariableAccess.ValueKind.Complex, VariableAccess.Classify(typeof(List<int>)));
            Assert.AreEqual(VariableAccess.ValueKind.Complex, VariableAccess.Classify(typeof(Vector3)));
        }

        [Test]
        public void DebugIcons_MissingIcon_GivesNull()
        {
            Assert.IsNull(DebugIcons.Find("definitely-not-an-icon-name"));
            Assert.IsNull(DebugIcons.Find());
        }
    }
}