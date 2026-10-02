// Hot paths visees (digest Toub .NET 11) : pipeline symbolique complet
// CharSetSolver.Convert -> Determinize -> Minimize -> Compile (allocations par
// etat/transition), matching compile, et matching System.Text.RegularExpressions
// delegue (Regex.Compile().Matches()). Tout passe par l'API publique.
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Automata;
using System.Text.RegularExpressions;

namespace Automata.Benchmarks;

// InProcess : evite le spawn d'un process enfant par benchmark (bloque par Defender
// sur certains postes Windows), et garde la mesure dans le meme runtime (cf. tranches
// MetaGeneticSharp / Z3.Linq, EPIC CoursIA 18770).
[MemoryDiagnoser]
[Config(typeof(InProcessShortConfig))]
public class RegexPipelineBenchmarks
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

    private const string Subject = "foofoxxxobaaaazzbarfoofoxxxobaaaazzbar";

    // --- Pipeline symbolique complet sur une regex a alternances + quantificateurs ---
    [Benchmark(Baseline = true)]
    public int Pipeline_ConvertDeterminizeMinimize()
    {
        var solver = new CharSetSolver();
        var fa = solver.Convert(@"(fox*o|bar|ba+zz)");
        return fa.Determinize().Minimize().StateCount;
    }

    // NOTE Pipeline_Compile retire : Automaton.Compile() passe par CodeDomProvider
    // (CSharpCodeProvider), supprime des runtimes .NET modernes (PlatformNotSupported
    // sur .NET 9) — plafond INTRINSIC du codegen legacy, documente dans
    // benchmarks/BASELINE-net9.0.md. Le matching reste mesure via Matching_DotNetDelegate
    // (Regex.Compile(), qui delegate a System.Text.RegularExpressions).

    // --- Matching via le compilateur delegue a System.Text.RegularExpressions ---
    [Benchmark]
    public int Matching_DotNetDelegate()
    {
        var r = new Regex("(fox*o|bar|ba+zz)");
        var aut = r.Compile();
        return aut.Matches(Subject).Length;
    }

    // --- Cas degenere-force exprime : regex piege (backtracking exponentiel SRE),
    //     converti en DFA par le pipeline symbolique (le chemin que le notebook
    //     Sudoku-12 enseigne comme antidote aux regex piegeses). ---
    [Benchmark]
    public int Pipeline_EvilRegex()
    {
        var solver = new CharSetSolver();
        var fa = solver.Convert(@"^(([a-z])+.)+[A-Z]([a-z])+$");
        return fa.Determinize().Minimize().StateCount;
    }
}
