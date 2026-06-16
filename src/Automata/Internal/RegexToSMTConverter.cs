using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Automata
{
    /// <summary>
    /// Public so that consumers (notably the .NET Interactive notebooks in CoursIA,
    /// series SMT/Z3 notebook 06) can emit SMT-LIB string-theory expressions directly
    /// from a regex with the fork's surface '&amp;'/'~' operators (#2979 Step 6).
    /// The public surface is limited to read-only conversion entry points
    /// (<see cref="ConvertRegex(string)"/>, <see cref="ConvertSeq(string)"/>) and the
    /// underlying <see cref="Solver"/> accessor; making the type public exposes no
    /// mutating or otherwise unsafe operation.
    ///
    /// <para><b>Dialect (modernized, #2979).</b> The emitter targets <b>SMT-LIB 2.6
    /// string theory</b> — the dialect modern solvers (Z3 4.x) actually consume — so
    /// the output is directly solvable: wrap it in
    /// <c>(declare-const w String) (assert (str.in_re w R)) (check-sat) (get-model)</c>.
    /// Characters are emitted as single-character <b>string literals</b> (<c>"a"</c>,
    /// or <c>"\u{XX}"</c> for non-printables), not as the historical Rex bit-vector
    /// encoding (<c>#b1100001</c>). Operators use the standard names:
    /// <c>str.to_re</c>, <c>re.range</c>, <c>re.union</c>, <c>re.++</c>, <c>re.*</c>,
    /// <c>re.+</c>, <c>re.opt</c>, <c>(_ re.loop m n)</c>, <c>re.none</c>,
    /// <c>re.allchar</c>, and — for the surface <c>&amp;</c>/<c>~</c> operators —
    /// <c>re.inter</c> / <c>re.comp</c>. Membership in <c>str.in_re</c> is a
    /// <b>full match</b>, which is the natural anchored reading of a witness query
    /// (so an unanchored <c>^</c>/<c>$</c> maps to the neutral empty-string regex).</para>
    /// </summary>
    public class RegexToSMTConverter
    {
        RegexToAutomatonConverter<BDD> automConverter;
        CharSetSolver css;
        public CharSetSolver Solver { get { return css; } }
        char maxChar;
        public RegexToSMTConverter(BitWidth encoding)
        {
            css = new CharSetSolver(encoding);
            automConverter = css.RegexConverter;
            maxChar = (encoding == BitWidth.BV16 ? '\uFFFF' :
                (encoding == BitWidth.BV8 ? '\u00FF' : '\u007F'));
        }

        // Backward-compatible overload: the char-sort alias is unused in the modern
        // SMT-LIB 2.6 string dialect (characters are emitted as single-char String
        // literals, so no (RegEx <sort>) annotation is needed). Kept for callers/tests.
        public RegexToSMTConverter(BitWidth encoding, string charSortAlias)
            : this(encoding)
        {
        }

        /// <summary>
        /// Convert a .Net regex to an equivalent SMT-LIB 2.6 string-theory regex expression.
        /// </summary>
        /// <param name="regex">the given .NET regex pattern</param>
        public string ConvertRegex(string regex)
        {
            var sb = new StringBuilder();
            this.Write = ((string s) => { sb.Append(s); return; });
            RegexTree tree = RegexParser.Parse(regex, RegexOptions.None);
            ConvertNode(tree._root);
            string res = sb.ToString();
            Write = null;
            return res;
        }

        Action<string> Write;

        /// <summary>
        /// Convert a string to an SMT-LIB 2.6 String literal (the modern equivalent of a
        /// character sequence), with proper escaping of quotes and non-printable chars.
        /// </summary>
        /// <param name="seq">given string that denotes a sequence of characters</param>
        public string ConvertSeq(string seq)
        {
            return StringLitSMT(seq);
        }

        private void ConvertNode(RegexNode node)
        {
            switch (node._type)
            {
                case RegexNode.Alternate:
                    { ConvertNodeAlternate(node); return; }
                case RegexNode.Beginning:
                    { ConvertNodeBeginning(node); return; }
                case RegexNode.Bol:
                    { ConvertNodeBol(node); return; }
                case RegexNode.Capture:  // (...)
                    { ConvertNode(node.Child(0)); return; }
                case RegexNode.Concatenate:
                    { ConvertNodeConcatenate(node); return; }
                case RegexNode.Intersect:
                    // BREX surface operator '&' (#2979) -> SMT-LIB re.inter (string theory).
                    { ConvertNodeIntersect(node); return; }
                case RegexNode.Complement:
                    // BREX surface operator '~' (#2979) -> SMT-LIB re.comp (string theory complement).
                    { ConvertNodeComplement(node); return; }
                case RegexNode.Empty:
                    { ConvertNodeEmpty(node); return; }
                case RegexNode.End:
                    { ConvertNodeEnd(node); return; }
                case RegexNode.EndZ:
                    { ConvertNodeEndZ(node); return; }
                case RegexNode.Eol:
                    { ConvertNodeEol(node); return; }
                case RegexNode.Loop:
                    { ConvertNodeLoop(node); return; }
                case RegexNode.Multi:
                    { ConvertNodeMulti(node); return; }
                case RegexNode.Notone:
                    { ConvertNodeNotone(node); return; }
                case RegexNode.Notoneloop:
                    { ConvertNodeNotoneloop(node); return; }
                case RegexNode.One:
                    { ConvertNodeOne(node); return; }
                case RegexNode.Oneloop:
                    { ConvertNodeOneloop(node); return; }
                case RegexNode.Set:
                    { ConvertNodeSet(node); return; }
                case RegexNode.Setloop:
                    { ConvertNodeSetloop(node); return; }
                default:
                    throw new AutomataException(AutomataExceptionKind.RegexConstructNotSupported);
            }
        }

        private void ConvertNodeSetloop(RegexNode node)
        {
            var set = automConverter.CreateConditionFromSet(false, node._str);
            var ranges = css.ToRanges(set);
            int m = node._m;
            int n = node._n;
            string ran = GetSMTRanges(ranges);
            WriteLoop(ran, m, n);
        }

        private void ConvertNodeSet(RegexNode node)
        {
            var set = automConverter.CreateConditionFromSet(false, node._str);
            var ranges = css.ToRanges(set);
            Write(GetSMTRanges(ranges));
        }

        private string GetSMTRanges(IList<Tuple<uint, uint>> ranges)
        {
            string res = "";
            if (ranges.Count == 0)
                res = "re.none";
            else if (ranges.Count == 1)
                res = OneRange(ranges[0].Item1, ranges[0].Item2);
            else
            {
                for (int i = 0; i < ranges.Count; i++)
                {
                    if (i < ranges.Count - 1)
                        res += "(re.union ";
                    else
                        res += " ";

                    res += OneRange(ranges[i].Item1, ranges[i].Item2);
                }
                for (int i = 0; i < ranges.Count - 1; i++)
                    res += ")";
            }
            return res;
        }

        // A single character range. A singleton {c} is emitted as (str.to_re "c");
        // a proper range [lo-hi] as (re.range "lo" "hi").
        private string OneRange(uint lo, uint hi)
        {
            if (lo == hi)
                return string.Format("(str.to_re {0})", EscapeCharSMT((char)lo));
            return string.Format("(re.range {0} {1})", EscapeCharSMT((char)lo), EscapeCharSMT((char)hi));
        }

        //loop with a singleton set
        private void ConvertNodeOneloop(RegexNode node)
        {
            char c = node._ch;
            string cond = string.Format("(str.to_re {0})", EscapeCharSMT(c));
            WriteLoop(cond, node._m, node._n);
        }

        //loop with a negated singleton set
        private void ConvertNodeNotoneloop(RegexNode node)
        {
            char c = node._ch;
            string cond = NegateSingletonSet(c);
            WriteLoop(cond, node._m, node._n);
        }

        private string NegateSingletonSet(char c)
        {
            string cond = "";
            if (c == '\0')
                cond = string.Format("(re.range {0} {1})", EscapeCharSMT('\u0001'), EscapeCharSMT(maxChar));
            else if (c == maxChar)
                cond = string.Format("(re.range {0} {1})", EscapeCharSMT('\0'), EscapeCharSMT((char)(((int)maxChar) - 1)));
            else
            {
                string r1 = string.Format("(re.range {0} {1})", EscapeCharSMT('\0'), EscapeCharSMT((char)(((int)c) - 1)));
                string r2 = string.Format("(re.range {0} {1})", EscapeCharSMT((char)(((int)c) + 1)), EscapeCharSMT(maxChar));
                cond = string.Format("(re.union {0} {1})", r1, r2);
            }
            return cond;
        }

        private void WriteLoop(string cond, int m, int n)
        {
            if (m == 1 && n == 1)                             //case: r{1,1} = r
                Write(cond);
            else if (m == 0 && n == 1)                        //case: ?
                Write(string.Format("(re.opt {0})", cond));
            else if (m == 0 && n == int.MaxValue)             //case: *
                Write(string.Format("(re.* {0})", cond));
            else if (m == 1 && n == int.MaxValue)             //case: +
                Write(string.Format("(re.+ {0})", cond));
            else if (n == int.MaxValue)                       //case {m,}
                Write(string.Format("(re.++ ((_ re.loop {0} {0}) {1}) (re.* {1}))", m, cond));
            else                                              //case {m,n}
                Write(string.Format("((_ re.loop {0} {1}) {2})", m, n, cond));
        }

        // Matches only node._ch (singleton set)
        private void ConvertNodeOne(RegexNode node)
        {
            char c = node._ch;
            Write(string.Format("(str.to_re {0})", EscapeCharSMT(c)));
        }

        //complement of the singleton set
        private void ConvertNodeNotone(RegexNode node)
        {
            char c = node._ch;
            string cond = NegateSingletonSet(c);
            Write(cond);
        }

        //explicit string as a regex
        private void ConvertNodeMulti(RegexNode node)
        {
            //given sequence of characters
            string sequence = node._str;
            Write(string.Format("(str.to_re {0})", StringLitSMT(sequence)));
        }

        //loop constructs
        private void ConvertNodeLoop(RegexNode node)
        {
            var child = node._children[0];
            int m = node._m;
            int n = node._n;
            if (m == 1 && n == 1) //trivial case: r{1,1} = r
            {
                ConvertNode(child);
            }
            else if (m == 0 && n == 1) //case: ?
            {
                Write("(re.opt ");
                ConvertNode(child);
                Write(")");
            }
            else if (m == 0 && n == int.MaxValue) //case: *
            {
                Write("(re.* ");
                ConvertNode(child);
                Write(")");
            }
            else if (m == 1 && n == int.MaxValue) //case: +
            {
                Write("(re.+ ");
                ConvertNode(child);
                Write(")");
            }
            else if (n == int.MaxValue) //case {m,}
            {
                Write(string.Format("(re.++ ((_ re.loop {0} {0}) ", m));
                ConvertNode(child);
                Write(") (re.* ");
                ConvertNode(child);
                Write("))");
            }
            else //general case {m,n}
            {
                Write(string.Format("((_ re.loop {0} {1}) ", m, n));
                ConvertNode(child);
                Write(")");
            }
        }

        //end anchors — neutral under str.in_re full-match semantics
        private void ConvertNodeEol(RegexNode node)
        {
            Write(ReEmptySeq);
        }

        private void ConvertNodeEndZ(RegexNode node)
        {
            Write(ReEmptySeq);
        }

        private void ConvertNodeEnd(RegexNode node)
        {
            Write(ReEmptySeq);
        }

        //empty regex
        private void ConvertNodeEmpty(RegexNode node)
        {
            Write(ReEmptySeq);
        }

        // (str.to_re "") matches exactly the empty string; it is the identity of re.++,
        // so begin/end anchors collapse away under full-match str.in_re semantics.
        string ReEmptySeq { get { return "(str.to_re \"\")"; } }

        //concatenation
        private void ConvertNodeConcatenate(RegexNode node)
        {
            var children = node._children;
            if (children.Count == 1)
                ConvertNode(children[0]);
            else
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (i < children.Count - 1)
                        Write("(re.++ ");
                    else
                        Write(" ");

                    ConvertNode(children[i]);
                }
                for (int i = 0; i < children.Count - 1; i++)
                    Write(")");
            }
        }

        //start anchors — neutral under str.in_re full-match semantics
        private void ConvertNodeBol(RegexNode node)
        {
            Write(ReEmptySeq);
        }

        private void ConvertNodeBeginning(RegexNode node)
        {
            Write(ReEmptySeq);
        }

        //union
        private void ConvertNodeAlternate(RegexNode node)
        {
            var children = node._children;
            if (children.Count == 1)
                ConvertNode(children[0]);
            else
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (i < children.Count - 1)
                        Write("(re.union ");
                    else
                        Write(" ");

                    ConvertNode(children[i]);
                }
                for (int i = 0; i < children.Count - 1; i++)
                    Write(")");
            }
        }

        /// <summary>
        /// BREX surface intersection operator '&amp;' (#2979) -> SMT-LIB (re.inter ...).
        /// Mirrors ConvertNodeAlternate's left-fold emit shape, substituting re.union -> re.inter.
        /// SMT-LIB string theory has supported re.inter since the 2016 string-theory integration;
        /// the 21-char witness cap (#6) is a solver-side constraint, not a syntax issue.
        /// </summary>
        private void ConvertNodeIntersect(RegexNode node)
        {
            var children = node._children;
            if (children.Count == 1)
                ConvertNode(children[0]);
            else
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (i < children.Count - 1)
                        Write("(re.inter ");
                    else
                        Write(" ");

                    ConvertNode(children[i]);
                }
                for (int i = 0; i < children.Count - 1; i++)
                    Write(")");
            }
        }

        /// <summary>
        /// BREX surface complement operator '~' (#2979) -> SMT-LIB (re.comp R).
        /// Unary wrapper: emits the single child's regex wrapped in re.comp (the SMT-LIB
        /// string-theory complement operator, distinct from the bv-level bvnot). The parser
        /// produces exactly one child per '~', so this is a straight one-arg wrap.
        /// </summary>
        private void ConvertNodeComplement(RegexNode node)
        {
            var children = node._children;
            if (children == null || children.Count == 0)
            {
                // complement of the empty regex is the universal language: re.allchar star.
                Write("(re.* (re.allchar))");
                return;
            }
            Write("(re.comp ");
            ConvertNode(children[0]);
            Write(")");
        }

        #region SMT-LIB 2.6 string-literal escaping
        /// <summary>
        /// Emit a single character as an SMT-LIB 2.6 single-character String literal:
        /// printable ASCII as-is (<c>"a"</c>), everything else as a unicode escape
        /// (<c>"\u{XX}"</c>). Quote and backslash are escaped to keep the literal well formed.
        /// </summary>
        string EscapeCharSMT(char c)
        {
            return StringLitSMT(c.ToString());
        }

        /// <summary>
        /// Emit a .NET string as an SMT-LIB 2.6 String literal. Inside such a literal the
        /// only quoting rule is that a double quote is doubled (<c>""</c>); we additionally
        /// emit any character outside printable ASCII (and the backslash) as a <c>\u{XX}</c>
        /// escape so the result is unambiguous for the Z3 parser.
        /// </summary>
        static string StringLitSMT(string s)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                int code = (int)c;
                if (c == '"')
                    sb.Append("\"\"");                      // doubled quote
                else if (code >= 0x20 && code <= 0x7e && c != '\\')
                    sb.Append(c);                           // printable ASCII, verbatim
                else
                    sb.AppendFormat("\\u{{{0:x}}}", code);  // \u{hex}
            }
            sb.Append('"');
            return sb.ToString();
        }
        #endregion
    }
}
