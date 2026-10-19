# Automata
Automata is a .NET library that provides algorithms for composing and analyzing _regular expressions_, _automata_, and _transducers_. In addition to classical word automata, it also includes algorithms for analysis of _tree automata_ and _tree transducers_. The library covers algorithms over finite alphabets as well as their _symbolic_ counterparts. In symbolic automata concrete characters have been replaced by _character predicates_. Such predicates can range over very large or even _infinite_ alphabets, like integers. Predicates can be supported by an SMT solver as a plugin.

## Benchmarks

Le projet `src/Automata.Benchmarks` (BenchmarkDotNet) mesure l'API publique —
pipeline symbolique `CharSetSolver.Convert` → `Determinize` → `Minimize`,
matching via `Regex.Compile()` (délégation SRE), regex piégée convertie en DFA.
Les mesures de référence .NET 9 et le protocole : [`benchmarks/BASELINE-net9.0.md`](benchmarks/BASELINE-net9.0.md).

```bash
cd src/Automata.Benchmarks
dotnet run -c Release -- --filter '*'
```
