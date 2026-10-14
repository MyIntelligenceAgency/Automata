// Hot paths vises (digest Toub .NET 11) : operations de structures sur les automates --
// determinisation (PowerSetStateBuilder, Dictionary/HashSet churn), minimisation Hopcroft
// (raffinement de partition, Dictionary.Remove), equivalence (exploration + ensembles hashes).
// Tout passe par l'API publique : Automaton<BDD>.Create / Determinize / MinimizeHopcroft /
// IsEquivalentWith, CharSetSolver pour les conditions de symboles.
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Automata;

namespace Automata.Benchmarks;

// InProcess : evite le spawn d'un process enfant par benchmark (bloque par Defender
// sur certains postes Windows -- cf. warning BDN), et garde la mesure dans le meme runtime.
[MemoryDiagnoser]
[Config(typeof(InProcessShortConfig))]
public class AutomatonOpsBench
{
    private class InProcessShortConfig : ManualConfig
    {
        public InProcessShortConfig()
            => AddJob(Job.ShortRun
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithWarmupCount(5)
                .WithIterationCount(15));
    }

    // NFA en couches : 15 couches, chacune 3 branches convergentes en epsilon vers le
    // pivot suivant (structure d'un local-part d'email generalisee : classes de caracteres
    // rotantes + boucles par branche). Aucun RNG -- l'automate est identique a chaque appel.
    // Ordre des etats inversable pour produire un jumeau non-isomorphe de meme langage.
    private const int Layers = 15;

    private static Automaton<BDD> BuildLayeredNfa(CharSetSolver solver, bool reverseOrder)
    {
        BDD lower = solver.MkCharSetFromRange('a', 'z');
        BDD upper = solver.MkCharSetFromRange('A', 'Z');
        BDD digit = solver.MkCharSetFromRange('0', '9');
        BDD punct = solver.MkCharSetFromRanges('@', '.', '-', '_');
        BDD alnum = solver.MkOr(lower, solver.MkOr(upper, digit));
        BDD[] classes = new[] { lower, upper, digit, punct, alnum };

        // pivots p(0..Layers), branches b(layer, 0..2) -> ids apres les pivots
        int Pivot(int i) => reverseOrder ? Layers - i : i;
        int Branch(int layer, int j) => (Layers + 1) + 3 * layer + j;

        var moves = new List<Move<BDD>>();
        for (int i = 0; i < Layers; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                BDD cls = classes[(i + j) % classes.Length];
                int from = Pivot(i), branch = Branch(i, j);
                moves.Add(Move<BDD>.Epsilon(from, branch));
                moves.Add(Move<BDD>.Create(branch, branch, cls));          // boucle par branche
                moves.Add(Move<BDD>.Create(branch, Pivot(i + 1), cls));    // convergence
            }
        }

        return Automaton<BDD>.Create(solver, Pivot(0), new[] { Pivot(Layers) }, moves);
    }

    private static CharSetSolver NewSolver() => new();

    // --- Determinisation seule : NFA 61 etats / 90 moves -> DFA (PowerSetStateBuilder) ---
    [Benchmark(Baseline = true)]
    public Automaton<BDD> Determinize_NFA_L15()
        => BuildLayeredNfa(NewSolver(), reverseOrder: false).Determinize();

    // --- Determinisation + minimisation Hopcroft : la difference avec la charge baseline
    //     isole le cout du raffinement de partition (Dictionary.Remove / HashSet) ---
    [Benchmark]
    public Automaton<BDD> Determinize_plus_MinimizeHopcroft_L15()
        => BuildLayeredNfa(NewSolver(), reverseOrder: false).Determinize().MinimizeHopcroft();

    // --- Equivalence de deux DFA de meme langage, numerotations differentes :
    //     exploration croisee + ensembles hashes, sans raccourci d'isomorphisme ---
    [Benchmark]
    public bool IsEquivalent_DFA_Twins_L15()
    {
        var solver = NewSolver();
        var a = BuildLayeredNfa(solver, reverseOrder: false).Determinize();
        var b = BuildLayeredNfa(solver, reverseOrder: true).Determinize();
        return a.IsEquivalentWith(b);
    }
}
