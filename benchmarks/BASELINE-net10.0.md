# Baseline .NET 10.0 — Automata.Benchmarks

Mesures de référence de l'API publique d'Automata sur .NET 10, point de départ
des comparaisons .NET 10 → 11 (EPIC gains runtime — CoursIA issues 18770 et
18695, digest Stephen Toub 2026-09-15). Sœur de `BASELINE-net8.0.md` et
`BASELINE-net9.0.md` (même banc, même protocole BDN) ; la **table de
comparaison des quatre runtimes** (net8.0, net9.0, net10.0, net11.0), mesurés
dans une session unique sur le même poste, est dans
`COMPARISON-net8-net11-2026-11-10.md` — c'est elle qui fait foi pour tout
écart inter-runtime, les valeurs absolues des baselines antérieures ayant été
mesurées sur une autre machine.

**Réserve du 2026-11-10, même jour** : un contrôle croisé à compilation
constante (`CONTROL-single-build-2026-11-10.md`) a **retiré** le verdict
« établi » que la table de comparaison portait sur l'axe net9 → net10. Les
valeurs absolues de cette baseline ne sont pas en cause — elles restent
valides pour la version qu'elles décrivent — mais tout écart inter-runtime lu
dans la table de comparaison doit être lu avec cette réserve.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-11-10 (session 4 runtimes, ~11:55-12:20 locales) |
| OS | Windows 11 Pro (10.0.26300.9711) |
| CPU | Intel Core i9-14900KF 3,20 GHz — 32 cœurs logiques, 24 physiques |
| Runtime | .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3 |
| SDK (build) | 10.0.114 |
| BenchmarkDotNet | 0.15.8 |
| Toolchain | InProcessEmitToolchain, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations |
| Passes | 5 passes complètes, **ordre entrelacé** (net8 → net9 → net10 → net11 dans chaque passe), **médiane des 5 moyennes** par cellule |
| Commit mesuré | `e6193d9b0988b7cf9d8023bd3a8291baca4b73f4` (master de ce dépôt) — **identique au gitlink épinglé par CoursIA** sur `MyIA.AI.Notebooks/SymbolicAI/SMT/Automata`, vérifié par `git ls-tree origin/main` |
| Machine | poste de travail partagé (charge d'hôte mesurée — cf. « Limites connues ») |

## Charges mesurées

Deux classes de banc, toutes deux via l'API publique — aucune I/O, aucun réseau,
structures **figées** (aucun RNG : mêmes opérandes à chaque itération) :

| Benchmark | Charge |
| --- | --- |
| `Determinize_NFA_L15` (baseline) | déterminisation seule d'un epsilon-NFA en 15 couches, 3 branches convergentes vers le pivot suivant (61 états / 90 transitions, 5 classes de caractères BDD rotatives + boucle par branche) |
| `Determinize_plus_MinimizeHopcroft_L15` | même charge + minimisation Hopcroft du DFA obtenu |
| `IsEquivalent_DFA_Twins_L15` | équivalence de 2 DFA de même langage, numérotations d'états inversées (2 déterminisations + exploration croisée) |
| `Pipeline_ConvertDeterminizeMinimize` | pipeline symbolique complet sur `(fox*o\|bar\|ba+zz)` : `CharSetSolver.Convert` → `Determinize` → `Minimize` |
| `Matching_DotNetDelegate` | `new Regex(...).Compile().Matches(sujet)` — compilateur délégué à System.Text.RegularExpressions, sujet de 36 caractères |
| `Pipeline_EvilRegex` | même pipeline sur la regex piégée `^(([a-z])+.)+[A-Z]([a-z])+$` (backtracking exponentiel en SRE, linéaire en DFA) |

Les charges reconstruisent l'automate à chaque invocation : le coût de
construction est identique sur tous les runtimes et fait partie de la charge
commune.

## Résultats (.NET 10.0)

Moyennes : **médiane des 5 passes** ; incertitude : médiane de l'`Error` BDN
*intra-passe* (15 itérations) ; allocations : `Allocated` du `MemoryDiagnoser`.

| Benchmark | Mean | Error (intra-passe) | Allocated | Intervalle des 5 passes |
| --- | ---: | ---: | ---: | --- |
| `Determinize_NFA_L15` | 14,260 ms | 2,164 ms (15,2 %) | 11.42 MB | [11,680 .. 16,410] |
| `Determinize_plus_MinimizeHopcroft_L15` | 44,330 ms | 9,724 ms (21,9 %) | 26.06 MB | [43,650 .. 51,610] |
| `IsEquivalent_DFA_Twins_L15` | 13,860 ms | 2,235 ms (16,1 %) | 24.07 MB | [12,760 .. 18,590] |
| `Pipeline_ConvertDeterminizeMinimize` | 1267,0 µs | 255,0 µs (20,1 %) | 2705.76 KB | [876 .. 1509] µs |
| `Matching_DotNetDelegate` | 705,0 µs | 132,0 µs (18,8 %) | 223.48 KB | [521 .. 795] µs |
| `Pipeline_EvilRegex` | 959,0 µs | 219,0 µs (22,9 %) | 2124.28 KB | [871 .. 1144] µs |

Lecture hot-path (axes Toub visés) :

- **Déterminisation** : 11,42 MB pour un NFA de 61 états — le
  PowerSetStateBuilder alloue ses ensembles-états (HashSet/Dictionary) à chaque
  sous-ensemble exploré. Les allocations sont reproductibles **au byte près**
  d'un runtime à l'autre (11,42 MB en net8/net9/net10, 11,41 MB en net11) :
  sur cette charge, le runtime ne change rien au volume alloué.
- **Minimisation Hopcroft** : la charge la plus GC-bound du banc (Gen0/Gen1/Gen2
  saturés, raffinement de partition avec `Dictionary.Remove` / churn de
  HashSet) — 26,06 MB, soit 2,28× la déterminisation seule.
- **Équivalence** : 24,07 MB (2,11×) : l'exploration croisée paie surtout en
  allocations, pas en temps CPU — 13,86 ms pour 24 Mo alloués.
- **Pipeline symbolique** : ~2,7 MB managés par exécution — cible première des
  améliorations runtime, et le seul axe où un écart inter-runtime s'est
  détaché du bruit sur cette session (cf. le document de comparaison).
- **Matching délégué** : 223 KB, 4× plus léger que le pipeline symbolique
  (0,71 ms vs 1,27 ms) : le codegen de SRE est plus économe que la
  construction symbolique.

## Protocole / reproduction

Le banc ne liste que `net8.0;net9.0` dans ses `<TargetFrameworks>`. Mesurer
.net 10 suppose donc une **édition temporaire d'une ligne**, restaurée ensuite
— le projet est dans `Core.sln`, compilé par la CI en SDK 8.0.x : ajouter
net10/net11 à la liste permanente casserait la porte CI. Une propriété globale
`-p:TargetFrameworks=net10.0` ne convient pas non plus : elle se propage au
projet référencé `Automata.csproj` et provoque `NETSDK1005`.

```bash
# 1. SDK .NET 11 RC3 (uniquement pour net11) : ZIP officielle extraite manuellement.
#    dotnet-install.ps1 echoue sous Windows -- son extraction "tar -tzf C:\..."
#    lit "C:" comme un hote distant : "Cannot connect to C: resolve failed".
#    Contournement : telecharger le .zip du SDK, Expand-Archive dans un dossier local.

# 2. Cibler un runtime : remplacer temporairement <TargetFrameworks> par la cible,
#    construire, puis restaurer le fichier.
dotnet build src/Automata.Benchmarks/Automata.Benchmarks.csproj -c Release

# 3. Executer (une passe = un run BDN complet)
cd src/Automata.Benchmarks
dotnet exec bin/Release/net10.0/Automata.Benchmarks.dll --filter '*'
```

Configuration Release, InProcess (évite le spawn d'un process enfant par
benchmark). Sujets et regex constants : résultats reproductibles à la variance
du scheduler près.

## Limites connues (honnêteté des mesures)

- **Charge d'hôte mesurée** : 66 % à 93 % de CPU sur les 20 échantillons prélevés
  pendant les 5 passes (un échantillon isolé à 11 %), 1081 à 1098 processus
  resident, dont 104 `dotnet`/`node`/`Runner`. Le poste héberge en permanence
  le vLLM de la flotte et les runners CI auto-hébergés : **ce n'est pas une
  fenêtre calme**, et les valeurs absolues ci-dessus sont majorées par cette
  charge. Elles ne sont comparables qu'à celles de la même session.
- **Erreur intra-passe de 15 à 23 %** : ShortRun borné (15 itérations) sur un
  poste partagé. L'`Error` BDN ne couvre que la dispersion *intra-passe* ; la
  dispersion *inter-passes* (12 à 53 % sur les cellules de cette baseline) est
  le terme dominant et c'est elle qui sert de critère de séparabilité.
- **Une seule machine** : comme pour les baselines net8/net9, la comparaison
  inter-runtime doit se rejouer sur le MÊME poste, même toolchain, même
  session — c'est ce que fait `COMPARISON-net8-net11-2026-11-10.md`.
- Le `Pipeline_Compile` reste absent : `Automaton<S>.Compile()` passe par
  `System.CodeDom.Compiler.CodeDomProvider`, supprimé des runtimes modernes —
  plafond INTRINSIC déjà documenté dans `BASELINE-net9.0.md`.
