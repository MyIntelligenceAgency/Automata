﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Automata
{

    public class CountingAutomaton<S> : Automaton<Tuple<Maybe<S>, Sequence<CounterOperation>>>
    {
        Dictionary<int, SymbolicRegexNode<S>> stateMap;
        HashSet<ICounter> counters;
        Dictionary<int, Sequence<CounterOperation>> finalStates;

        internal CountingAutomaton(Automaton<Tuple<Maybe<S>, Sequence<CounterOperation>>> aut,
            Dictionary<int, SymbolicRegexNode<S>> stateMap, Dictionary<int, Sequence<CounterOperation>> finalStates, HashSet<ICounter> counters) : base(aut)
        {
            this.stateMap = stateMap;
            this.counters = counters;
            this.finalStates = finalStates;
        }

        /// <summary>
        /// Gets the number of counters.
        /// </summary>
        public int NrOfCounters
        {
            get
            {
                return counters.Count;
            }
        }

        public Sequence<CounterOperation> GetFinalStateCondition(int state)
        {
            return finalStates[state];
        }

        public override string DescribeState(int state)
        {
            return stateMap[state].ToString();
        }
    }

    /// <summary>
    /// Dummy Boolean algebra use only for customized pretty printing of CountingAutomaton transition labels
    /// </summary>
    internal class CABA<S> : IBooleanAlgebra<Tuple<Maybe<S>, Sequence<CounterOperation>>>, IPrettyPrinter<Tuple<Maybe<S>, Sequence<CounterOperation>>>
    {
        SymbolicRegexBuilder<S> builder;
        public CABA(SymbolicRegexBuilder<S> builder)
        {
            this.builder = builder;
        }

        public string PrettyPrint(Tuple<Maybe<S>, Sequence<CounterOperation>> t)
        {
            if (t.Item1.IsSomething)
            {
                if (t.Item2.Length > 0)
                    return builder.solver.PrettyPrint(t.Item1.Element) + "/" + t.Item2.ToString();
                else
                    return builder.solver.PrettyPrint(t.Item1.Element);
            }
            else
            {
                string s = "";
                for (int i=0; i < t.Item2.Length; i++)
                {
                    if (t.Item2[i].OperationKind != CounterOp.EXIT &&
                        t.Item2[i].OperationKind != CounterOp.EXIT_SET0)
                        throw new AutomataException(AutomataExceptionKind.InternalError);
                    if (t.Item2[i].Counter.LowerBound == t.Item2[i].Counter.UpperBound)
                    {
                        if (s != "")
                            s += " & ";
                        s += string.Format("{0}=={1}", t.Item2[i].Counter.CounterName, t.Item2[i].Counter.LowerBound);
                    }
                    else if (t.Item2[i].Counter.LowerBound > 0)
                    {
                        if (s != "")
                            s += " & ";
                        s += string.Format("{0}>={1}", t.Item2[i].Counter.CounterName, t.Item2[i].Counter.LowerBound);
                    }
                }
                return "F:" + (s == "" ? "true" : s);
            }
        }

        #region not implemented
        public Tuple<Maybe<S>, Sequence<CounterOperation>> False
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsAtomic
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsExtensional
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> True
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool AreEquivalent(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate1, Tuple<Maybe<S>, Sequence<CounterOperation>> predicate2)
        {
            throw new NotImplementedException();
        }

        public bool CheckImplication(Tuple<Maybe<S>, Sequence<CounterOperation>> lhs, Tuple<Maybe<S>, Sequence<CounterOperation>> rhs)
        {
            throw new NotImplementedException();
        }

        public bool EvaluateAtom(Tuple<Maybe<S>, Sequence<CounterOperation>> atom, Tuple<Maybe<S>, Sequence<CounterOperation>> psi)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Tuple<bool[], Tuple<Maybe<S>, Sequence<CounterOperation>>>> GenerateMinterms(params Tuple<Maybe<S>, Sequence<CounterOperation>>[] constraints)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> GetAtom(Tuple<Maybe<S>, Sequence<CounterOperation>> psi)
        {
            throw new NotImplementedException();
        }

        public bool IsSatisfiable(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkAnd(params Tuple<Maybe<S>, Sequence<CounterOperation>>[] predicates)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkAnd(IEnumerable<Tuple<Maybe<S>, Sequence<CounterOperation>>> predicates)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkAnd(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate1, Tuple<Maybe<S>, Sequence<CounterOperation>> predicate2)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkDiff(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate1, Tuple<Maybe<S>, Sequence<CounterOperation>> predicate2)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkNot(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkOr(IEnumerable<Tuple<Maybe<S>, Sequence<CounterOperation>>> predicates)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkOr(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate1, Tuple<Maybe<S>, Sequence<CounterOperation>> predicate2)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> MkSymmetricDifference(Tuple<Maybe<S>, Sequence<CounterOperation>> p1, Tuple<Maybe<S>, Sequence<CounterOperation>> p2)
        {
            throw new NotImplementedException();
        }

        public string PrettyPrint(Tuple<Maybe<S>, Sequence<CounterOperation>> t, Func<Tuple<Maybe<S>, Sequence<CounterOperation>>, string> varLookup)
        {
            throw new NotImplementedException();
        }

        public string PrettyPrintCS(Tuple<Maybe<S>, Sequence<CounterOperation>> t, Func<Tuple<Maybe<S>, Sequence<CounterOperation>>, string> varLookup)
        {
            throw new NotImplementedException();
        }

        public Tuple<Maybe<S>, Sequence<CounterOperation>> Simplify(Tuple<Maybe<S>, Sequence<CounterOperation>> predicate)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

}
