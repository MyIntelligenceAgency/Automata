// BREX surface operator parser tests (AutomataDotNet fork, #2979).
// Verifies the patched RegexParser produces Intersect trees for the top-level
// '&' operator, with the documented precedence (concatenation > & > |), and that
// legacy patterns without '&' are byte-identical (no Intersect node emitted).
//
// Complement ('~') is deferred to a follow-up cycle; these tests cover '&' only.
// The automaton/SMT converters (Step 3) are NOT exercised here -- this isolates
// the parser. Converter equivalence vs BREX.MkAnd lands with Step 3.
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Automata;

namespace Automata.Tests
{
    [TestClass]
    public class BrexSurfaceParserTests
    {
        /// <summary>
        /// Walks the parsed tree and returns the first node of the given type,
        /// or null if none is found. Robust to reduction differences in the
        /// intermediate Alternate/Capture layers.
        /// </summary>
        private static RegexNode FindNode(RegexNode node, int type)
        {
            if (node == null)
                return null;
            if (node.Type() == type)
                return node;
            for (int i = 0; i < node.ChildCount(); i++)
            {
                var found = FindNode(node.Child(i), type);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static int CountNodes(RegexNode node, int type)
        {
            if (node == null)
                return 0;
            int n = (node.Type() == type) ? 1 : 0;
            for (int i = 0; i < node.ChildCount(); i++)
                n += CountNodes(node.Child(i), type);
            return n;
        }

        private static RegexNode Parse(string pattern)
        {
            // RegexParser.Parse is internal; Automata.Tests is granted access via
            // InternalsVisibleTo (AssemblyInfo.cs). Singleline keeps '.' simple.
            var tree = RegexParser.Parse(pattern, RegexOptions.Singleline);
            return tree._root;
        }

        /// <summary>
        /// "R & C & B" must parse to a single Intersect node with three members,
        /// each a single-char One node. This is the headline Sudoku construct
        /// (row & column & block) the fork unlocks at the syntax level.
        /// </summary>
        [TestMethod]
        public void Intersection_ThreeMembers_ProducesIntersectNode()
        {
            var root = Parse("R&C&B");

            var intersect = FindNode(root, RegexNode.Intersect);
            Assert.IsNotNull(intersect, "Pattern 'R&C&B' must produce an Intersect node");
            Assert.AreEqual(3, intersect.ChildCount(),
                "R&C&B must intersect exactly three members (R, C, B)");

            // Each member is a single-char One node, in source order.
            Assert.AreEqual(RegexNode.One, intersect.Child(0).Type());
            Assert.AreEqual('R', intersect.Child(0)._ch);
            Assert.AreEqual(RegexNode.One, intersect.Child(1).Type());
            Assert.AreEqual('C', intersect.Child(1)._ch);
            Assert.AreEqual(RegexNode.One, intersect.Child(2).Type());
            Assert.AreEqual('B', intersect.Child(2)._ch);
        }

        /// <summary>
        /// Binary intersection: "ab & cd" must split as (ab) & (cd), proving
        /// the precedence concatenation > & (concat binds tighter than &).
        /// Each operand is a multi-char run; the Intersect has exactly two children.
        /// </summary>
        [TestMethod]
        public void Intersection_Precedence_ConcatBindsTighterThanIntersection()
        {
            var root = Parse("ab&cd");

            var intersect = FindNode(root, RegexNode.Intersect);
            Assert.IsNotNull(intersect, "Pattern 'ab&cd' must produce an Intersect node");
            Assert.AreEqual(2, intersect.ChildCount(),
                "ab&cd must intersect exactly two concatenated operands");

            // Each operand is a Multi (ordinary char run), not two separate Ones
            // interleaved with the intersection -- this is what precedence encodes.
            Assert.AreEqual(RegexNode.Multi, intersect.Child(0).Type());
            Assert.AreEqual("ab", intersect.Child(0)._str);
            Assert.AreEqual(RegexNode.Multi, intersect.Child(1).Type());
            Assert.AreEqual("cd", intersect.Child(1)._str);
        }

        /// <summary>
        /// Intersection binds tighter than alternation: "a&b|c&d" must produce
        /// two Intersect nodes under a single Alternate (precedence & > |).
        /// </summary>
        [TestMethod]
        public void Intersection_Precedence_IntersectionBindsTighterThanAlternation()
        {
            var root = Parse("a&b|c&d");

            Assert.AreEqual(2, CountNodes(root, RegexNode.Intersect),
                "a&b|c&d must yield two Intersect nodes (one per alternation branch)");
            var alternate = FindNode(root, RegexNode.Alternate);
            Assert.IsNotNull(alternate, "a&b|c&d must produce an Alternate node");
        }

        /// <summary>
        /// Intersection can be nested inside a group and combined with alternation:
        /// "(a&b)|(c&d)" -- explicit grouping, two Intersect nodes.
        /// </summary>
        [TestMethod]
        public void Intersection_NestedInGroup_TwoIntersects()
        {
            var root = Parse("(a&b)|(c&d)");

            Assert.AreEqual(2, CountNodes(root, RegexNode.Intersect),
                "(a&b)|(c&d) must yield two Intersect nodes, one per group");
        }

        /// <summary>
        /// Anti-regression: a plain concatenation "abc" must NOT emit any Intersect
        /// node. The _intersection layer stays empty and the legacy path is taken
        /// (byte-identical to upstream .NET Automata parsing).
        /// </summary>
        [TestMethod]
        public void Legacy_PlainConcat_NoIntersectNode()
        {
            var root = Parse("abc");
            Assert.AreEqual(0, CountNodes(root, RegexNode.Intersect),
                "Plain concatenation 'abc' must not produce any Intersect node");
        }

        /// <summary>
        /// Anti-regression: a plain alternation "a|b|c" must keep working and
        /// emit NO Intersect node. (We do not assert the Alternate shape here
        /// because the final Reduce/StripEnation may restructure the tree; the
        /// invariant that matters is the absence of Intersect nodes.)
        /// </summary>
        [TestMethod]
        public void Legacy_PlainAlternate_NoIntersectNode()
        {
            var root = Parse("a|b|c");
            Assert.AreEqual(0, CountNodes(root, RegexNode.Intersect),
                "Plain alternation 'a|b|c' must not produce any Intersect node");
        }

        /// <summary>
        /// Anti-regression: '&' inside a character class [...] is a class member,
        /// NOT the top-level intersection operator. "[a-z&]" must parse as a Set
        /// containing the literal '&', with no Intersect node.
        /// </summary>
        [TestMethod]
        public void Legacy_AmpersandInCharClass_IsLiteralMemberNotOperator()
        {
            var root = Parse("[a-z&]");
            Assert.AreEqual(0, CountNodes(root, RegexNode.Intersect),
                "'&' inside [a-z&] is a class member, not the intersection operator");
        }
    }
}
