# Baseline .NET 9.0 — Automata.Benchmarks

Mesures de référence de l'API publique de Microsoft.Automata sur .NET 9, point de
départ des comparaisons .NET 10 / .NET 11 (EPIC gains runtime — CoursIA issues
18770 et 18695, digest Stephen Toub 2026-09-15). Troisième tranche après
MetaGeneticSharp (jsboige/MetaGeneticSharp#58) et Z3.Linq (MyIntelligenceAgency/Z3.Linq#34),
même protocole.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-10-02 |
| OS | Windows 11 Pro (10.0.26300) |
| Runtime | .NET 9.0.20 (9.0.2026.41315), X64 RyuJIT AVX2 |
| SDK (build) | 10.0.204 |
| BenchmarkDotNet | 0.14.0 |
| Toolchain | InProcessEmitToolchain, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations |
| Machine | poste de travail portable (laptop), alimentation secteur |

## Charges mesurées

| Benchmark | Charge |
| --- | --- |
| `Pipeline_ConvertDeterminizeMinimize` (baseline) | pipeline symbolique complet sur `(fox*o\|bar\|ba+zz)` : `CharSetSolver.Convert` → `Determinize` → `Minimize` |
| `Matching_DotNetDelegate` | `new Regex(...).Compile().Matches(sujet)` — compilateur délégué à System.Text.RegularExpressions, sujet de 36 caractères |
| `Pipeline_EvilRegex` | même pipeline sur la regex piégée `^(([a-z])+.)+[A-Z]([a-z])+$` (backtracking exponentiel en SRE, linéaire en DFA) |

## Résultats

| Method | Mean | Error | StdDev | Ratio | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Pipeline_ConvertDeterminizeMinimize | 2 135,7 us | 131,75 us | 116,80 us | 1.00 | 2710,51 KB | 1.00 |
| Matching_DotNetDelegate | 527,8 us | 60,18 us | 56,30 us | 0.25 | 222,17 KB | 0.08 |
| Pipeline_EvilRegex | 1 798,2 us | 147,62 us | 130,86 us | 0.84 | 2128,37 KB | 0.79 |

Lecture hot-path (axes Toub visés) :

- **Allocation-intensif par design** : un pipeline symbolique complet alloue
  **~2,1-2,7 MB managés** par exécution (états, transitions, predicates de
  CharSetSolver) — c'est la cible première des améliorations runtime (write
  barriers, allocs).
- **Matching délégué 4x plus léger** que le pipeline (0,53 ms vs 2,14 ms,
  222 KB vs 2,7 MB) : le codegen de SRE est plus économe que la construction
  symbolique — la marge runtime se joue surtout côté pipeline symbolique.
- **EvilRegex plus rapide que la baseline** (1,80 ms vs 2,14 ms) : la regex
  piégée produit un DFA plus petit (structure `[a-z]`-bornée) que
  l'alternance à quantificateurs de la baseline — la construction DFA,
  pas le matching, domine.

## Plafond INTRINSIC documenté — `Automaton.Compile()` (codegen CodeDom)

Le benchmark `Pipeline_Compile` a été retiré : `Automaton<S>.Compile()` passe par
`System.CodeDom.Compiler.CodeDomProvider` (CSharpCodeProvider), **supprimé des
runtimes .NET modernes** — lève `PlatformNotSupportedException: Operation is not
supported on this platform` sur .NET 9 (`FromFileBatch` → compilation CodeDom
indisponible). Verdict SOTA : **INTRINSIC** — aucun chemin .NET 9+ pour le codegen
legacy de ce pin ; le matching reste mesuré via `Regex.Compile()` (délégation SRE,
voie supportée). Une réécriture du codegen (Roslyn ou Reflection.Emit) serait un
projet en soi, hors périmètre banc.

## Protocole / reproduction

```bash
cd src/Automata.Benchmarks
dotnet run -c Release -- --filter '*'
```

Configuration Release, InProcess (évite le spawn d'un process enfant par
benchmark, bloqué par Windows Defender sur le poste de mesure — cf. tranches
précédentes). Sujets et regex constants : résultats reproductibles à la
variance du scheduler près. BDN a retiré 2 outliers (3,88 / 3,96 ms) sur
Pipeline et 1 (1,76 ms) sur Matching ; `Matching_DotNetDelegate` montre une
distribution multimodale (mValue 3,14) — cohérent avec les phases JIT/interp
de SRE, noté sans en tirer de conclusion.

## Limites connues (honnêteté des mesures)

- `MinIterationTime` : chaque itération dure 0,43-2,0 ms (< 100 ms recommandé
  par BDN). Grossir les regex changerait la charge pédagogique visée.
- Error ≈ 6-11 % du Mean : poste partagé (GUI active), ShortRun borné.
- Une seule machine ; la comparaison .NET 10/11 devra rejouer sur le MÊME
  poste, même toolchain, pour être comparable.
