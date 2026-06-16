using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

using Microsoft.Automata;

using System.Text.RegularExpressions;

namespace Microsoft.Automata.Tests
{
    [TestClass]
    public class RegexToSMTTests
    {
        //static string medium = @"(^(?:\w\:)?(?:/|\\\\){1}[^/|\\]*(?:/|\\){1})";
        static string large = @"^(([A-Za-z]+[^0-9]*)([0-9]+[^\W]*)([\W]+[\W0-9A-Za-z]*))|(([A-Za-z]+[^\W]*)([\W]+[^0-9]*)([0-9]+[\W0-9A-Za-z]*))|(([\W]+[^A-Za-z]*)([A-Za-z]+[^0-9]*)([0-9]+[\W0-9A-Za-z]*))|(([\W]+[^0-9]*)([0-9]+[^A-Za-z]*)([A-Za-z]+[\W0-9A-Za-z]*))|(([0-9]+[^A-Za-z]*)([A-Za-z]+[^\W]*)([\W]+[\W0-9A-Za-z]*))|(([0-9]+[^\W]*)([\W]+[^A-Za-z]*)([A-Za-z]+[\W0-9A-Za-z]*))$";
        
        [TestMethod]
        public void TestRegexBV16toSMT()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV16, "CHAR");
            var res = conv.ConvertRegex(large);
            Console.WriteLine(res);
        }

        // --- Modernized RegexToSMTConverter dialect (#2979) ---------------------------
        // The converter now emits SMT-LIB 2.6 string theory (str.to_re / re.range /
        // re.union / re.* / re.+ / re.opt / (_ re.loop m n) / re.inter / re.comp), i.e.
        // the dialect modern Z3 actually consumes, with characters as single-char String
        // literals. These tests pin the surface forms; the actual *solvability* of the
        // output (ParseSMTLIB2String -> witness) is exercised by notebook 06 §7b.

        [TestMethod]
        public void TestConvertRegexSingleton()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            Assert.AreEqual<string>("(str.to_re \"a\")", conv.ConvertRegex("a"));
        }

        [TestMethod]
        public void TestConvertRegexString()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            Assert.AreEqual<string>("(str.to_re \"abc\")", conv.ConvertRegex("abc"));
        }

        [TestMethod]
        public void TestConvertRegexRange()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            Assert.AreEqual<string>("(re.range \"b\" \"z\")", conv.ConvertRegex("[b-z]"));
        }

        [TestMethod]
        public void TestConvertRegexStarPlusOpt()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            Assert.AreEqual<string>("(re.* (str.to_re \"a\"))", conv.ConvertRegex("a*"));
            Assert.AreEqual<string>("(re.+ (str.to_re \"a\"))", conv.ConvertRegex("a+"));
            Assert.AreEqual<string>("(re.opt (str.to_re \"a\"))", conv.ConvertRegex("a?"));
        }

        [TestMethod]
        public void TestConvertRegexLoop()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            Assert.AreEqual<string>("((_ re.loop 5 9) (str.to_re \"ab\"))", conv.ConvertRegex("(ab){5,9}"));
        }

        // The #2979 payoff: the fork's surface '&' (intersection) and '~' (complement)
        // operators map to SMT-LIB re.inter / re.comp.
        [TestMethod]
        public void TestConvertRegexIntersectComplement()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            Assert.AreEqual<string>(
                "(re.inter (re.range \"a\" \"b\") (re.comp (re.range \"b\" \"c\")))",
                conv.ConvertRegex("[ab]&~[bc]"));
        }

        // Regression guard: no legacy Rex tokens (the historical unsolvable dialect)
        // must leak into the modern output.
        [TestMethod]
        public void TestConvertRegexNoLegacyTokens()
        {
            var conv = new RegexToSMTConverter(BitWidth.BV7);
            string res = conv.ConvertRegex("^[A-Za-z0-9]{8}$&~(.*password.*)&~(^[0-9].*$)");
            foreach (var legacy in new[] { "re-range", "re-union", "re-of-seq", "seq-cons",
                                           "re-empty-set", "re-star", "re-plus", "re-concat", "#b" })
            {
                Assert.IsFalse(res.Contains(legacy),
                    $"Modern SMT-LIB output must not contain legacy token '{legacy}'. Got: {res}");
            }
            // and it must use the modern intersection/complement names
            StringAssert.Contains(res, "re.inter");
            StringAssert.Contains(res, "re.comp");
        }

        [TestMethod]
        public void TestSingleton()
        {
            Regex regex = new Regex("a");
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(str.to_re \"a\")", smt_format);
        }

        [TestMethod]
        public void TestString()
        {
            Regex regex = new Regex("abc");
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(str.to_re \"abc\")", smt_format);
        }

        [TestMethod]
        public void TestConcatWithRange()
        {
            Regex regex = new Regex("a[b-z-[f]]");
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(re.++ (str.to_re \"a\") (re.union (re.range \"b\" \"e\") (re.range \"g\" \"z\")))", smt_format);
        }

        [TestMethod]
        public void TestDotStar()
        {
            Regex regex = new Regex(".*.*", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(re.++ (re.all) (re.all))", smt_format);
        }

        [TestMethod]
        public void TestDotPlus()
        {
            Regex regex = new Regex(".+", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true,false,true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(re.+ (re.allchar))", smt_format);
        }

        [TestMethod]
        public void TestLoop()
        {
            Regex regex = new Regex("(ab){5,9}", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("((_ re.loop 5 9) (str.to_re \"ab\"))", smt_format);
        }

        [TestMethod]
        public void TestIgnoreCase()
        {
            Regex regex = new Regex("(?i:a)b", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(re.++ (re.union (str.to_re \"A\") (str.to_re \"a\")) (str.to_re \"b\"))", smt_format);
        }

        [TestMethod]
        public void TestConjunctionViaITE()
        {
            Regex regex = new Regex("(?([ag]*)ad+|[0-[0]])", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(re.inter (re.* (re.union (str.to_re \"a\") (str.to_re \"g\"))) (re.++ (str.to_re \"a\") (re.+ (str.to_re \"d\"))))", smt_format);
        }

        [TestMethod]
        public void TestComplViaITE()
        {
            Regex regex = new Regex("(?([aC]*)[0-[0]]|.*)", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var smt_format = sr.Pattern.ToSMTlibFormat();
            Assert.AreEqual<string>("(re.comp (re.* (re.union (str.to_re \"C\") (str.to_re \"a\"))))", smt_format);
        }

        [TestMethod]
        public void TestCharacterClasses()
        {
            Regex regex = new Regex(@"\w\d+", RegexOptions.Singleline);
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var actual = sr.Pattern.ToSMTlibFormat();
            var expected = "(re.++ (re.union (re.union (re.union (re.range \"0\" \"9\") (re.range \"A\" \"Z\")) (str.to_re \"_\")) (re.range \"a\" \"z\")) (re.+ (re.range \"0\" \"9\")))";
            Assert.AreEqual<string>(expected, actual);
        }

        [TestMethod]
        public void TestWhitespace()
        {
            Regex regex = new Regex(@"\s");
            var sr = (SymbolicRegex<ulong>)regex.Compile(true, false, true);
            var actual = sr.Pattern.ToSMTlibFormat();
            var expected = "(re.union (re.range (_ char #x9) (_ char #xD)) (str.to_re (_ char #x20)))";
            Assert.AreEqual<string>(expected, actual);
        }
    }
}
