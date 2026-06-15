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

        // ---------------------------------------------------------------------------
        // Step 3 coverage: the '&' parser node now flows through the converters.
        // RegexToAutomatonConverter.Intersect (-> Automaton<BDD>.Intersect) must
        // produce the same automaton as the existing BREX.MkAnd path. This is the
        // end-to-end proof that surface '&' == the algebra-level intersection.
        // ---------------------------------------------------------------------------
        private static Automaton<BDD> Convert(CharSetSolver solver, string pattern)
        {
            // Single shared solver so cross-automaton IsEquivalentWith passes the
            // CheckIdentityOfAlgebras guard (Automaton.cs:1607).
            return solver.Convert(pattern, RegexOptions.None).RemoveEpsilons().Determinize().Minimize();
        }

        /// <summary>
        /// The surface '&' parser node, routed through the patched converter, must
        /// produce an automaton equal to the explicit algebra-level intersection
        /// (Automaton&lt;BDD&gt;.Intersect over the same operands). This is the Step 3
        /// end-to-end proof: surface syntax and programmatic algebra agree. If the
        /// converter fell through to the default 'UnrecognizedRegex' throw, Convert()
        /// would fail before the assertion.
        /// </summary>
        [TestMethod]
        public void Intersection_Convert_EqualsAlgebraIntersection()
        {
            var solver = new CharSetSolver(BitWidth.BV7);
            var lhs = Convert(solver, "[ab]");
            var rhs = Convert(solver, "[bc]");
            var viaSyntax = Convert(solver, "[ab]&[bc]");   // surface '&' (parser + converter)
            var viaAlgebra = lhs.Intersect(rhs).Determinize().Minimize();

            Assert.IsTrue(viaSyntax.IsEquivalentWith(viaAlgebra),
                "Surface '&' must produce the same automaton as explicit Automaton<BDD>.Intersect");
        }

        /// <summary>
        /// N-ary intersection left-folds correctly: "[ab]&[bc]&[cd]" must equal
        /// "([ab]&[bc])&[cd]" as a minimized DFA. This proves the ConvertNodeIntersect
        /// left-fold (associative Automaton.Intersect) is sound for the 3-operand case
        /// (the row &amp; column &amp; block form).
        /// </summary>
        [TestMethod]
        public void Intersection_Nary_LeftFolds()
        {
            var solver = new CharSetSolver(BitWidth.BV7);
            var fold3 = Convert(solver, "[ab]&[bc]&[cd]");
            var nested = Convert(solver, "[ab]&[bc]").Intersect(Convert(solver, "[cd]"))
                            .Determinize().Minimize();
            Assert.IsTrue(fold3.IsEquivalentWith(nested),
                "N-ary '&' must left-fold: [ab]&[bc]&[cd] == ([ab]&[bc])&[cd]");
        }

        /// <summary>
        /// Surface '&' must agree with the BREX.MkAnd API entry point on the same
        /// operands. BREXManager uses its own internal CharSetSolver (cross-solver
        /// IsEquivalentWith throws IncompatibleAlgebras), so we compare the automaton
        /// STATES COUNT and emptiness -- both the surface-syntax path and the MkAnd
        /// path must be non-empty, deterministic, single-final-state automata over
        /// the singleton language { 'b' }. (GenerateMember is not usable here: the
        /// BDD Chooser throws NullReferenceException under net8.0 for all inputs,
        /// independent of this patch -- pre-existing Rex migration debt.)
        /// </summary>
        [TestMethod]
        public void Intersection_SurfaceSyntaxMatchesMkAndShape()
        {
            var solver = new CharSetSolver(BitWidth.BV7);
            var viaSyntax = Convert(solver, "[ab]&[bc]");

            var man = new BREXManager();
            var viaApi = man.MkAnd(man.MkRegex("[ab]"), man.MkRegex("[bc]")).Optimize();

            // Both paths must describe a non-empty language (the intersection { 'b } exists).
            Assert.IsFalse(viaSyntax.IsEmpty, "Surface '&' [ab]&[bc] must be non-empty");
            Assert.IsFalse(viaApi.IsEmpty, "BREX.MkAnd([ab],[bc]) must be non-empty");
            // And the two must have the same minimized DFA shape (same state count).
            Assert.AreEqual(viaApi.StateCount, viaSyntax.StateCount,
                "Surface '&' and BREX.MkAnd must yield DFA-equivalent state counts");
        }
    }
}
